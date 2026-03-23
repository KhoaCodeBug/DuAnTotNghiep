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

    [Header("Cài đặt Rớt Đồ")]
    public GameObject droppedItemPrefab;

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

    public void UseItem(int index)
    {
        if (index < 0 || index >= slots.Count) return;

        InventorySlot slot = slots[index];
        ItemData item = slot.item;
        bool itemUsed = false;

        // 1. TÌM TẤT CẢ SCRIPT CẦN THIẾT (Chỉ tìm một lần duy nhất ở đây)
        PlayerHealth health = Object.FindAnyObjectByType<PlayerHealth>();
        PlayerStamina stamina = Object.FindAnyObjectByType<PlayerStamina>();
        PlayerSurvival survival = Object.FindAnyObjectByType<PlayerSurvival>();

        // 2. PHÂN LOẠI XỬ LÝ
        if (item.category == ItemCategory.Medical)
        {
            if (health == null)
            {
                Debug.LogError("LỖI: Không tìm thấy script PlayerHealth!");
            }
            else if (health.currentHealth < health.maxHealth)
            {
                health.Heal(item.healAmount);
                itemUsed = true;
            }
            else
            {
                Debug.Log("⚠️ Máu đang đầy!");
            }
        }
        else if (item.category == ItemCategory.Consumable)
        {
            // Kiểm tra script Survival để hồi Đói/Khát
            if (survival != null)
            {
                if (item.hungerRestore > 0) survival.RestoreHunger(item.hungerRestore);
                if (item.thirstRestore > 0) survival.RestoreThirst(item.thirstRestore);
                itemUsed = true;
            }

            // Kiểm tra script Stamina để dùng Buff (Nước tăng lực)
            if (stamina != null && item.buffDuration > 0)
            {
                stamina.ApplyEnergyBuff(item.buffDuration, item.speedMultiplier, item.maxStaminaBoost);
                itemUsed = true;
            }

            if (itemUsed) Debug.Log("Đã nốc xong nhu yếu phẩm: " + item.itemName);
        }
        else if (item.category == ItemCategory.Ammunition)
        {
            Debug.Log("⚠️ Đạn dược không thể sử dụng trực tiếp!");
        }

        // 3. TRỪ ĐỒ NẾU DÙNG THÀNH CÔNG
        if (itemUsed)
        {
            slot.amount--;
            if (slot.amount <= 0) slots.RemoveAt(index);
            UpdateUI();
        }
    }

    public void DropItem(int index)
    {
        // 1. Kiểm tra vị trí ô hợp lệ
        if (index < 0 || index >= slots.Count) return;

        InventorySlot slot = slots[index];
        ItemData itemToDrop = slot.item;

        // 2. Trừ đồ trong túi và cập nhật giao diện
        slot.amount--;
        if (slot.amount <= 0)
        {
            slots.RemoveAt(index);
        }
        UpdateUI();

        // 3. XÁC ĐỊNH PREFAB NÀO SẼ ĐƯỢC RỚT RA
        // Ưu tiên dùng Prefab riêng của món đồ (nếu có cài trong cục ItemData), 
        // Nếu không cài, nó sẽ xài cái droppedItemPrefab dùng chung của Ba lô.
        GameObject prefabToSpawn = itemToDrop.specificDropPrefab != null ? itemToDrop.specificDropPrefab : droppedItemPrefab;

        if (prefabToSpawn != null)
        {
            // SỬA LỖI VĂNG XA: Dùng Random.insideUnitCircle * 0.3f để tạo một điểm ngẫu nhiên 
            // cực kỳ gần (bán kính 0.3) ngay xung quanh tâm nhân vật.
            Vector2 randomOffset = Random.insideUnitCircle * 0.3f;

            // Giữ cho trục Z bằng 0 để đồ không bị chìm xuống dưới map
            Vector3 spawnPos = transform.position + new Vector3(randomOffset.x, randomOffset.y, 0f);

            // Đẻ cục đồ ra map
            GameObject droppedGO = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);

            // Tự động "thay áo" (Sprite) của cục đồ rớt ra cho giống với icon của món đó trong túi
            SpriteRenderer sr = droppedGO.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sprite = itemToDrop.icon;

            // Gán dữ liệu cho cục đồ để lụm lại được
            ItemPickup pickup = droppedGO.GetComponent<ItemPickup>();
            if (pickup != null)
            {
                pickup.item = itemToDrop;
                pickup.amount = 1; // Vứt ra 1 món thì gán số lượng là 1
            }

            Debug.Log("Đã vứt " + itemToDrop.itemName + " ra ngay dưới chân!");
        }
        else
        {
            Debug.LogWarning("⚠️ Bạn chưa cài đặt Prefab rớt đồ cho hệ thống!");
        }
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
    // 🔥 ĐÃ FIX: So sánh bằng TÊN (itemName) để dứt điểm vụ Unity không nhận diện được đạn
    public int GetItemCount(ItemData itemToCount)
    {
        if (itemToCount == null) return 0;
        int total = 0;
        foreach (var slot in slots)
        {
            if (slot.item != null && slot.item.itemName == itemToCount.itemName)
            {
                total += slot.amount;
            }
        }
        return total;
    }

    // 🔥 ĐÃ FIX: Rút đạn cực chuẩn, tự động dọn ô trống
    public int ConsumeItem(ItemData itemToConsume, int amountNeeded)
    {
        if (itemToConsume == null) return 0;
        int amountExtracted = 0;
        for (int i = slots.Count - 1; i >= 0; i--)
        {
            if (slots[i].item != null && slots[i].item.itemName == itemToConsume.itemName)
            {
                int availableInSlot = slots[i].amount;
                int amountToTakeFromSlot = Mathf.Min(availableInSlot, amountNeeded - amountExtracted);

                slots[i].amount -= amountToTakeFromSlot;
                amountExtracted += amountToTakeFromSlot;

                // Nếu rút cạn ô đó thì xóa ô đó đi
                if (slots[i].amount <= 0)
                {
                    slots.RemoveAt(i);
                }

                // Đã lấy đủ số đạn cần thiết thì dừng
                if (amountExtracted >= amountNeeded) break;
            }
        }

        return amountExtracted;
    }
}