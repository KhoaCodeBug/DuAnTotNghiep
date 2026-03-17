using System.Collections.Generic;
using UnityEngine;

// Class này đại diện cho 1 Ô trong ba lô
[System.Serializable]
public class InventorySlot
{
    public ItemData item;
    public int amount;

    public InventorySlot(ItemData item, int amount)
    {
        this.item = item;
        this.amount = amount;
    }

    public void AddAmount(int value)
    {
        amount += value;
    }
}

public class InventorySystem : MonoBehaviour
{
    [Header("Cài đặt Ba lô")]
    public int maxSlots = 20; // Số ô tối đa trong ba lô

    [Header("Danh sách các ô đang chứa đồ")]
    public List<InventorySlot> slots = new List<InventorySlot>();

    // Hàm dùng để nhặt đồ vào túi
    public bool AddItem(ItemData itemToAdd, int amountToAdd)
    {
        // Nếu item cho phép cộng dồn
        if (itemToAdd.isStackable)
        {
            // Tìm xem có ô nào chứa item này mà chưa đầy không
            foreach (InventorySlot slot in slots)
            {
                if (slot.item == itemToAdd && slot.amount < itemToAdd.maxStack)
                {
                    int spaceLeft = itemToAdd.maxStack - slot.amount;

                    // Nếu số lượng nhặt vừa vặn vào khoảng trống
                    if (amountToAdd <= spaceLeft)
                    {
                        slot.AddAmount(amountToAdd);

                        UpdateUI(); // <--- ĐÃ THÊM: Cập nhật giao diện khi lụm vào ô cũ thành công
                        return true;
                    }
                    else
                    {
                        // Nhét đầy ô hiện tại, số lượng thừa lố ra giữ lại để xử lý tiếp
                        slot.AddAmount(spaceLeft);
                        amountToAdd -= spaceLeft;
                    }
                }
            }
        }

        // Nếu item KHÔNG cộng dồn, hoặc số đạn lố ra không còn ô nào chứa chung được -> Tạo ô mới
        while (amountToAdd > 0 && slots.Count < maxSlots)
        {
            int amountToStore = Mathf.Min(amountToAdd, itemToAdd.maxStack);
            slots.Add(new InventorySlot(itemToAdd, amountToStore));
            amountToAdd -= amountToStore;
        }

        UpdateUI(); // <--- ĐÃ THÊM: Cập nhật giao diện sau khi tạo ô mới xong

        // Trả về true nếu nhặt hết, false nếu ba lô đầy và rớt lại đồ
        if (amountToAdd > 0)
        {
            Debug.Log("Ba lô đầy! Không thể chứa hết " + itemToAdd.itemName);
            return false;
        }

        return true;
    }

    // ĐÃ THÊM MỚI TỪ ĐÂY XUỐNG DƯỚI: Hàm để báo cho UI biết cần vẽ lại lưới đồ
    private void UpdateUI()
    {
        if (AutoUIManager.Instance != null)
        {
            // Truyền danh sách đồ vào để UIManager vẽ lại
            AutoUIManager.Instance.RefreshUI(this.slots);
        }
    }
}