using Integration.Backend;
using Integration.Common;
using StackExchange.Redis;

namespace Integration.Service;

public sealed class DisributedItemIntegrationService
{
    //This is a dependency that is normally fulfilled externally.
    private ItemOperationBackend itemIntegrationBackend = new();

    readonly IConnectionMultiplexer multiplexer;
    readonly IDatabase database;

    public DisributedItemIntegrationService()
    {
        multiplexer = DistributedFactory.CreateConnection();
        database = multiplexer.GetDatabase(0);
    }

    // This is called externally and can be called multithreaded, in parallel.
    // More than one item with the same content should not be saved. However,
    // calling this with different contents at the same time is OK, and should
    // be allowed for performance reasons.
    public Result SaveItem(string itemContent)
    {
        var itemLockKey = GetItemLockKey(itemContent);

        // Check redis first in case of it's already locked and being processed
        // then check if the item exists in the backend(database)

        // PS: This backend service ('FindItemsWithContent') should be returning a single item, not a list
        if (itemIntegrationBackend.FindItemsWithContent(itemContent).Count != 0)
        {
            return new Result(false, $"Duplicate item received with content {itemContent}.");
        }

        if (SetKey(itemLockKey))
        {
            try
            {
                var item = itemIntegrationBackend.SaveItem(itemContent);
                return new Result(true, $"Item with content {itemContent} saved with id {item.Id}");
            }
            finally
            {
                RemoveKey(itemLockKey);
            }
        }
        else // if cannot set, then it's already locked and being processed
        {
            return new Result(false, $"Duplicate item received with content {itemContent}.");
        }
    }

    public List<Item> GetAllItems()
    {
        return itemIntegrationBackend.GetAllItems();
    }


    private static string GetItemLockKey(string itemContent)
    {
        return "lock:saveitems" + itemContent;
    }

    private bool SetKey(string lockKey)
    {
        return database
                .StringSet(lockKey,
                           Guid.NewGuid().ToString(),
                           TimeSpan.FromSeconds(30),
                           When.NotExists);
    }

    // Atomically release the lock if it matches the lock token
    private void RemoveKey(string lockKey)
    {
        // Lua script to release the lock safely
        var script = @"
            if redis.call('exists', KEYS[1]) == 1 then
                return redis.call('del', KEYS[1])
            else
                return 0
            end";

        database.ScriptEvaluate(script, new RedisKey[] { lockKey });
    }
}