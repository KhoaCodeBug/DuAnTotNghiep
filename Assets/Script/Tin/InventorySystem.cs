using System.Collections.Generic;
using UnityEngine;
using Fusion; // Bắt buộc dùng mạng

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
    public void AddAmount(int value) { amount += value; }
}

public class InventorySystem : NetworkBehaviour // 🔥 ĐÃ ĐỔI sang NetworkBehaviour
{
    [Header("Cài đặt Ba lô")]
    public int maxSlots = 20;

    [Header("Danh sách các ô đang chứa đồ")]
    public List<InventorySlot> slots = new List<InventorySlot>();

    [Header("Cài đặt Rớt Đồ")]
    public GameObject droppedItemPrefab;

    public bool AddItem(ItemData itemToAdd, int amountToAdd)
    {
        // 🔥 CHỐT CHẶN: Ép Host không được phép nhặt nhầm đồ vào túi của Clone
        if (!HasInputAuthority) return false;

        if (itemToAdd.isStackable)
        {
            foreach (InventorySlot slot in slots)
            {
                if (slot.item == itemToAdd && slot.amount < itemToAdd.maxStack)
                {
                    int spaceLeft = itemToAdd.maxStack - slot.amount;
                    if (amountToAdd <= spaceLeft)
                    {
                        slot.AddAmount(amountToAdd);
                        UpdateUI();
                        return true;
                    }
                    else
                    {
                        slot.AddAmount(spaceLeft);
                        amountToAdd -= spaceLeft;
                    }
                }
            }
        }

        while (amountToAdd > 0 && slots.Count < maxSlots)
        {
            int amountToStore = Mathf.Min(amountToAdd, itemToAdd.maxStack);
            slots.Add(new InventorySlot(itemToAdd, amountToStore));
            amountToAdd -= amountToStore;
        }

        UpdateUI();

        if (amountToAdd > 0)
        {
            Debug.Log("Ba lô đầy! Không thể chứa hết " + itemToAdd.itemName);
            return false;
        }

        return true;
    }

    public void UseItem(int index)
    {
        // 🔥 CHỐT CHẶN: Chỉ máy mình mới được dùng đồ trong túi mình
        if (!HasInputAuthority) return;

        if (index < 0 || index >= slots.Count) return;

        InventorySlot slot = slots[index];
        ItemData item = slot.item;
        bool itemUsed = false;

        // 🔥 ĐÃ FIX LỖI BƠM MÁU SAI NGƯỜI: Lấy script từ đúng bản thân nhân vật này!
        PlayerHealth health = GetComponent<PlayerHealth>();
        PlayerStamina stamina = GetComponent<PlayerStamina>();
        PlayerSurvival survival = GetComponent<PlayerSurvival>();

        if (item.category == ItemCategory.Medical)
        {
            if (health != null && health.currentHealth < health.maxHealth)
            {
                health.Heal(item.healAmount);
                itemUsed = true;
            }
            else Debug.Log("⚠️ Máu đang đầy!");
        }
        else if (item.category == ItemCategory.Consumable)
        {
            if (survival != null)
            {
                if (item.hungerRestore > 0) survival.RestoreHunger(item.hungerRestore);
                if (item.thirstRestore > 0) survival.RestoreThirst(item.thirstRestore);
                itemUsed = true;
            }

            if (stamina != null && item.buffDuration > 0)
            {
                stamina.ApplyEnergyBuff(item.buffDuration, item.speedMultiplier, item.maxStaminaBoost);
                itemUsed = true;
            }

            if (itemUsed) Debug.Log("Đã nốc xong: " + item.itemName);
        }

        if (itemUsed)
        {
            slot.amount--;
            if (slot.amount <= 0) slots.RemoveAt(index);
            UpdateUI();
        }
    }

    public void DropItem(int index)
    {
        if (!HasInputAuthority) return;

        if (index < 0 || index >= slots.Count) return;
        InventorySlot slot = slots[index];
        ItemData itemToDrop = slot.item;

        slot.amount--;
        if (slot.amount <= 0) slots.RemoveAt(index);
        UpdateUI();

        GameObject prefabToSpawn = itemToDrop.specificDropPrefab != null ? itemToDrop.specificDropPrefab : droppedItemPrefab;

        if (prefabToSpawn != null)
        {
            Vector2 randomOffset = Random.insideUnitCircle * 0.3f;
            Vector3 spawnPos = transform.position + new Vector3(randomOffset.x, randomOffset.y, 0f);

            // Tạm thời để Instantiate, cục đồ rớt ra sẽ chỉ thấy trên máy của người vứt.
            // Nếu muốn mọi người cùng thấy, sau này đổi thành Runner.Spawn
            GameObject droppedGO = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);

            SpriteRenderer sr = droppedGO.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sprite = itemToDrop.icon;

            ItemPickup pickup = droppedGO.GetComponent<ItemPickup>();
            if (pickup != null) { pickup.item = itemToDrop; pickup.amount = 1; }
        }
    }

    private void UpdateUI()
    {
        // 🔥 CHỐT CHẶN: Chỉ gọi cập nhật UI khi đây là đồ của MÌNH
        if (!HasInputAuthority) return;
        if (AutoUIManager.Instance != null) AutoUIManager.Instance.RefreshUI(this.slots);
    }

    public int GetItemCount(ItemData itemToCount)
    {
        if (itemToCount == null) return 0;
        int total = 0;
        foreach (var slot in slots) { if (slot.item != null && slot.item.itemName == itemToCount.itemName) total += slot.amount; }
        return total;
    }

    // 🔥 ĐÃ FIX: Rút đạn cực chuẩn, tự động dọn ô trống và BÁO CÁO NGAY CHO UI
    public int ConsumeItem(ItemData itemToConsume, int amountNeeded)
    {
        if (itemToConsume == null) return 0;
        if (!HasInputAuthority) return 0;

        int amountExtracted = 0;
        for (int i = slots.Count - 1; i >= 0; i--)
        {
            if (slots[i].item != null && slots[i].item.itemName == itemToConsume.itemName)
            {
                int availableInSlot = slots[i].amount;
                int amountToTakeFromSlot = Mathf.Min(availableInSlot, amountNeeded - amountExtracted);

                slots[i].amount -= amountToTakeFromSlot;
                amountExtracted += amountToTakeFromSlot;

                if (slots[i].amount <= 0) slots.RemoveAt(i);
                if (amountExtracted >= amountNeeded) break;
            }
        }

        // 🔥 THÊM DÒNG NÀY: Báo cho túi đồ vẽ lại hình ảnh ngay lập tức!
        UpdateUI();

        return amountExtracted;
    }
}