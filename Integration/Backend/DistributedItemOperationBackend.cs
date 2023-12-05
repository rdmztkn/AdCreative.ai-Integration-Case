using Integration.Common;
using Integration.Service;
using StackExchange.Redis;
using System.Collections.Concurrent;

namespace Integration.Backend;
internal class DistributedItemOperationBackend : IItemOperationBackend
{
    const string lockKey = "lock:saveitems";
    const string counterKey = "atomic-counter";

    readonly IConnectionMultiplexer multiplexer;
    readonly IDatabase database;

    public DistributedItemOperationBackend()
    {
        multiplexer = DistributedFactory.CreateConnection();
        database = multiplexer.GetDatabase(0);
    }

    private ConcurrentBag<Item> SavedItems { get; set; } = new();

    public List<Item> GetAllItems()
    {
        return SavedItems.ToList();
    }

    public bool ItemExists(string itemContent)
    {
        var itemLockKey = GetItemLockKey(itemContent);
        if (database.KeyExists(itemLockKey))
        {
            return true;
        }

        return SavedItems
            .Any(i => i.Content.Equals(itemContent, StringComparison.OrdinalIgnoreCase));
    }

    public Item SaveItem(string itemContent)
    {
        var itemLockKey = GetItemLockKey(itemContent);

        if (AcquireLock(itemLockKey))
        {
            try
            {
                Thread.Sleep(2_000);

                var item = new Item
                {
                    Content = itemContent,
                    Id = GetNextIdentity()
                };

                SavedItems.Add(item);
                return item;
            }
            finally
            {
                ReleaseLock(itemLockKey);
            }
        }
        else
        {
            throw new Exception("Could not acquire lock");
        }
    }

    public int GetNextIdentity()
    {
        var lockToken = Guid.NewGuid().ToString();
        
        try
        {
            if (AcquireLock(lockToken))
            {
                return (int)database.StringIncrement(counterKey);
            }
            else
                return 0;
        }
        finally
        {
            ReleaseLock(lockToken);
        }

    }

    private static string GetItemLockKey(string itemContent)
    {
        return lockKey + itemContent;
    }

    private bool AcquireLock(string lockKey)
    {
        return database
                .StringSet(lockKey,
                           Guid.NewGuid().ToString(),
                           TimeSpan.FromSeconds(30));
    }

    // Atomically release the lock if it matches the lock token
    private void ReleaseLock(string lockKey)
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
