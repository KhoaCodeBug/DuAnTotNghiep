using System.Collections.Generic;
using UnityEngine;

// Định nghĩa một "Khe chứa đồ" (Gồm 1 Món Đồ + Số Lượng của nó)
[System.Serializable]
public class InventorySlot
{
    public Item item;
    public int count;
}

public class Inventory : MonoBehaviour
{
    // Singleton giúp các script khác dễ dàng tìm thấy túi đồ này
    public static Inventory instance;

    void Awake()
    {
        if (instance != null) { Destroy(gameObject); return; }
        instance = this;
    }

    [Header("Cài đặt Túi đồ")]
    public int space = 20; // Sức chứa tối đa (20 ô)
    public List<InventorySlot> slots = new List<InventorySlot>(); // Danh sách các ô đang có đồ

    // Sự kiện để báo cho Giao diện (UI) biết mỗi khi túi đồ có biến động
    public delegate void OnItemChanged();
    public OnItemChanged onItemChangedCallback;

    // Hàm nhặt đồ vào túi
    public bool Add(Item itemToAdd, int amount = 1)
    {
        // 1. Nếu món đồ này CÓ THỂ cộng dồn (như đạn, tiền...)
        if (itemToAdd.isStackable)
        {
            foreach (InventorySlot slot in slots)
            {
                if (slot.item == itemToAdd)
                {
                    slot.count += amount; // Tăng số lượng
                    if (onItemChangedCallback != null) onItemChangedCallback.Invoke();
                    return true;
                }
            }
        }

        // 2. Nếu món đồ KHÔNG THỂ cộng dồn (như súng), hoặc là món đồ mới hoàn toàn
        if (slots.Count >= space)
        {
            Debug.Log("Túi đồ đã đầy!");
            return false;
        }

        // Tạo một khe chứa mới và nhét vào túi
        InventorySlot newSlot = new InventorySlot();
        newSlot.item = itemToAdd;
        newSlot.count = amount;
        slots.Add(newSlot);

        if (onItemChangedCallback != null) onItemChangedCallback.Invoke();
        return true;
    }

    // Hàm vứt đồ ra khỏi túi
    public void Remove(InventorySlot slotToRemove)
    {
        slots.Remove(slotToRemove);
        if (onItemChangedCallback != null) onItemChangedCallback.Invoke();
    }
}