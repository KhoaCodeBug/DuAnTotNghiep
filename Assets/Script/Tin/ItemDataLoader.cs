using UnityEngine;

public static class ItemDataLoader
{
    public static ItemData LoadItem(string itemIdentifier)
    {
        if (string.IsNullOrEmpty(itemIdentifier)) return null;

        // 1. Thử load trực tiếp theo tên file trong folder Resources/Items/
        ItemData data = Resources.Load<ItemData>("Items/" + itemIdentifier);
        if (data != null) return data;

        // 2. Dự phòng: Tìm kiếm theo thuộc tính itemName hiển thị hoặc tên file (không phân biệt hoa thường)
        ItemData[] allItems = Resources.LoadAll<ItemData>("Items");
        foreach (var item in allItems)
        {
            if (item != null)
            {
                if (item.name.Equals(itemIdentifier, System.StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(item.itemName) && item.itemName.Equals(itemIdentifier, System.StringComparison.OrdinalIgnoreCase)))
                {
                    return item;
                }
            }
        }

        if (QuestRouteClueItemCatalog.TryLoad(itemIdentifier, out ItemData questItem))
            return questItem;

        if (MilitaryQuestItemCatalog.TryLoad(itemIdentifier, out ItemData militaryItem))
            return militaryItem;

        Debug.LogWarning($"[ItemDataLoader] Không thể tìm thấy ItemData có tên hoặc ID '{itemIdentifier}' trong Assets/Resources/Items/");
        return null;
    }
}
