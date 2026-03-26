using System.Collections.Generic;
using UnityEngine;
using Fusion; // Vẫn giữ mạng để quản lý các tính năng khác

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

public class InventorySystem : NetworkBehaviour
{
    [Header("Cài đặt Ba lô")]
    public int maxSlots = 20;

    [Header("Cài đặt Nhặt Đồ")]
    public float pickupRadius = 0.5f;

    [Header("Danh sách các ô đang chứa đồ")]
    public List<InventorySlot> slots = new List<InventorySlot>();

    [Header("Cài đặt Rớt Đồ (Cá nhân)")]
    public GameObject droppedItemPrefab;
    [Tooltip("Thời gian (giây) cục đồ tồn tại trên đất trước khi bốc hơi")]
    public float dropLifeTime = 30f; // 🔥 MỚI: Biến chỉnh thời gian biến mất

    // ==========================================
    // MẮT THẦN QUÉT ĐỒ DƯỚI CHÂN
    // ==========================================
    public override void FixedUpdateNetwork()
    {
        if (!HasInputAuthority) return;
        if (!Runner.IsForward) return;

        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, pickupRadius);
        foreach (Collider2D col in colliders)
        {
            ItemPickup pickup = col.GetComponent<ItemPickup>();

            if (pickup != null && pickup.isActiveAndEnabled)
            {
                bool pickedUp = AddItem(pickup.item, pickup.amount);
                if (pickedUp)
                {
                    Debug.Log("Đã lụm: " + pickup.item.itemName);

                    pickup.enabled = false;
                    col.enabled = false;
                    SpriteRenderer sr = pickup.GetComponent<SpriteRenderer>();
                    if (sr != null) sr.enabled = false;

                    NetworkObject netObj = pickup.GetComponent<NetworkObject>();
                    if (netObj != null && netObj.IsValid)
                    {
                        RPC_RequestDespawnItem(netObj);
                    }
                    else
                    {
                        Destroy(pickup.gameObject);
                    }
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }

    // ==========================================
    // HỆ THỐNG THÊM VÀ DÙNG ĐỒ
    // ==========================================
    public bool AddItem(ItemData itemToAdd, int amountToAdd)
    {
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
        if (!HasInputAuthority) return;
        if (index < 0 || index >= slots.Count) return;

        InventorySlot slot = slots[index];
        ItemData item = slot.item;
        bool itemUsed = false;

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
        else if (item.category == ItemCategory.Ammunition)
        {
            Debug.Log("⚠️ Đạn dược không thể sử dụng trực tiếp!");
        }

        if (itemUsed)
        {
            slot.amount--;
            if (slot.amount <= 0) slots.RemoveAt(index);
            UpdateUI();
        }
    }

    // ==========================================
    // 🔥 ĐÃ LÀM MỚI: HỆ THỐNG VỨT RÁC CÁ NHÂN (LOCAL DROP)
    // ==========================================
    public void DropItem(int index)
    {
        // Chỉ bản thân mình mới có quyền vứt đồ của mình
        if (!HasInputAuthority) return;

        if (index < 0 || index >= slots.Count) return;
        InventorySlot slot = slots[index];
        ItemData itemToDrop = slot.item;

        // Trừ đồ trong túi
        slot.amount--;
        if (slot.amount <= 0) slots.RemoveAt(index);
        UpdateUI();

        // Tạo hình ảnh cục đồ văng ra mặt đất
        GameObject prefabToSpawn = itemToDrop.specificDropPrefab != null ? itemToDrop.specificDropPrefab : droppedItemPrefab;

        if (prefabToSpawn != null)
        {
            Vector2 randomOffset = Random.insideUnitCircle * 0.4f;
            Vector3 spawnPos = transform.position + new Vector3(randomOffset.x, randomOffset.y, 0f);

            // 🔥 TẠO RA BẰNG HÀM CƠ BẢN CỦA UNITY (Chỉ máy mình thấy)
            GameObject droppedGO = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);

            // Đắp hình và thông số vào
            SpriteRenderer sr = droppedGO.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sprite = itemToDrop.icon;

            ItemPickup pickup = droppedGO.GetComponent<ItemPickup>();
            if (pickup != null) { pickup.item = itemToDrop; pickup.amount = 1; }

            // 🔥 HẸN GIỜ TỬ HÌNH: Cục đồ tự biến mất sau 'dropLifeTime' giây (VD: 30s)
            Destroy(droppedGO, dropLifeTime);

            Debug.Log($"Đã vứt {itemToDrop.itemName}. Cục đồ này sẽ tự phân hủy sau {dropLifeTime} giây.");
        }
    }

    // ==========================================
    // CÁC HÀM TIỆN ÍCH
    // ==========================================
    private void UpdateUI()
    {
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

        UpdateUI();
        return amountExtracted;
    }

    // Hàm này giữ lại để túi đồ vẫn nhờ Server xóa được đồ xịn (rớt từ quái)
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_RequestDespawnItem(NetworkObject itemNetObj)
    {
        if (itemNetObj != null && itemNetObj.IsValid)
        {
            Runner.Despawn(itemNetObj);
        }
    }
}