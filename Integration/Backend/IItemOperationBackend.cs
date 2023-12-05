using Integration.Common;

namespace Integration.Backend;
public interface IItemOperationBackend
{
    List<Item> GetAllItems();
    bool ItemExists(string itemContent);
    Item SaveItem(string itemContent);
}