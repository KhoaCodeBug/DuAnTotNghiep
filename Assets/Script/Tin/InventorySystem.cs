using System.Collections.Generic;
using UnityEngine;
using Fusion;

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
    public float dropLifeTime = 30f;

    // Cờ chống lặp vô hạn khi 2 máy gọi điện cho nhau
    private bool isSyncing = false;

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
                NetworkObject netObj = pickup.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsValid)
                {
                    // 🔥 SỬA MỚI: Yêu cầu Server cùng nhặt để túi đồ 2 bên giống hệt nhau
                    RPC_RequestPickupItem(netObj, pickup.item.itemName, pickup.amount);

                    pickup.enabled = false;
                    col.enabled = false;
                    SpriteRenderer sr = pickup.GetComponent<SpriteRenderer>();
                    if (sr != null) sr.enabled = false;
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
        // 🔥 ĐÃ GỠ BỎ LỆNH "if (!HasInputAuthority)" ĐỂ SERVER CŨNG ĐƯỢC QUYỀN THÊM ĐỒ

        int originalAmount = amountToAdd;

        if (itemToAdd.isStackable)
        {
            foreach (InventorySlot slot in slots)
            {
                if (slot.item.itemName == itemToAdd.itemName && slot.amount < itemToAdd.maxStack)
                {
                    int spaceLeft = itemToAdd.maxStack - slot.amount;
                    if (amountToAdd <= spaceLeft)
                    {
                        slot.AddAmount(amountToAdd);
                        amountToAdd = 0;
                        break;
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

        int amountAdded = originalAmount - amountToAdd;

        // 🔥 HỆ THỐNG ĐỒNG BỘ: KHI 1 BÊN NHẬN ĐƯỢC ĐỒ, PHẢI GỌI ĐIỆN BÁO BÊN KIA BIẾT
        if (!isSyncing && amountAdded > 0)
        {
            isSyncing = true;
            if (HasStateAuthority && !HasInputAuthority) RPC_SyncItemToClient(itemToAdd.itemName, amountAdded, true);
            else if (HasInputAuthority && !HasStateAuthority) RPC_SyncItemToServer(itemToAdd.itemName, amountAdded, true);
            isSyncing = false;
        }

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
        }

        if (itemUsed)
        {
            slot.amount--;
            if (slot.amount <= 0) slots.RemoveAt(index);
            UpdateUI();

            // Client tự dùng đồ thì báo Server trừ đi
            if (!isSyncing)
            {
                isSyncing = true;
                RPC_SyncItemToServer(item.itemName, 1, false);
                isSyncing = false;
            }
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

        // Client tự vứt đồ thì báo Server trừ đi
        if (!isSyncing)
        {
            isSyncing = true;
            RPC_SyncItemToServer(itemToDrop.itemName, 1, false);
            isSyncing = false;
        }

        GameObject prefabToSpawn = itemToDrop.specificDropPrefab != null ? itemToDrop.specificDropPrefab : droppedItemPrefab;

        if (prefabToSpawn != null)
        {
            Vector2 randomOffset = Random.insideUnitCircle * 0.4f;
            Vector3 spawnPos = transform.position + new Vector3(randomOffset.x, randomOffset.y, 0f);

            GameObject droppedGO = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
            SpriteRenderer sr = droppedGO.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sprite = itemToDrop.icon;

            ItemPickup pickup = droppedGO.GetComponent<ItemPickup>();
            if (pickup != null) { pickup.item = itemToDrop; pickup.amount = 1; }

            Destroy(droppedGO, dropLifeTime);
        }
    }

    private void UpdateUI()
    {
        if (!HasInputAuthority) return; // Chỉ Client sở hữu nhân vật mới vẽ Balo
        if (AutoUIManager.Instance != null) AutoUIManager.Instance.RefreshUI(this.slots);
    }

    public int GetItemCount(ItemData itemToCount)
    {
        if (itemToCount == null) return 0;

        // 🔥 QUAN TRỌNG: GỠ BỎ LỆNH CẤM Ở ĐÂY ĐỂ SERVER CÓ THỂ ĐẾM ĐƯỢC ĐẠN ĐỂ BÁO LÊN HUD!
        int total = 0;
        foreach (var slot in slots)
        {
            if (slot.item != null && slot.item.itemName == itemToCount.itemName) total += slot.amount;
        }
        return total;
    }

    public int ConsumeItem(ItemData itemToConsume, int amountNeeded)
    {
        if (itemToConsume == null) return 0;

        // 🔥 QUAN TRỌNG: GỠ BỎ LỆNH CẤM Ở ĐÂY ĐỂ SERVER CÓ QUYỀN TRỪ ĐẠN KHI BẤM NẠP ĐẠN (R)
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

        // 🔥 HỆ THỐNG ĐỒNG BỘ: SÚNG CHẠY TRÊN SERVER TRỪ ĐẠN XONG PHẢI BÁO CLIENT UPDATE UI
        if (!isSyncing && amountExtracted > 0)
        {
            isSyncing = true;
            if (HasStateAuthority && !HasInputAuthority) RPC_SyncItemToClient(itemToConsume.itemName, amountExtracted, false);
            else if (HasInputAuthority && !HasStateAuthority) RPC_SyncItemToServer(itemToConsume.itemName, amountExtracted, false);
            isSyncing = false;
        }

        return amountExtracted;
    }

    // ==========================================
    // HỆ THỐNG GỌI ĐIỆN RPC ĐỒNG BỘ 2 CHIỀU
    // ==========================================

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_RequestPickupItem(NetworkObject itemNetObj, string itemName, int amount)
    {
        ItemData data = Resources.Load<ItemData>("Items/" + itemName);
        if (data != null)
        {
            bool pickedUp = AddItem(data, amount);
            if (pickedUp && itemNetObj != null && itemNetObj.IsValid)
            {
                Runner.Despawn(itemNetObj); // Server xác nhận xóa cục đồ trên mặt đất
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_SyncItemToClient(string itemName, int amount, bool isAdding)
    {
        ItemData data = Resources.Load<ItemData>("Items/" + itemName);
        if (data != null)
        {
            isSyncing = true; // Bật cờ để Client không gọi ngược lại lên Server gây lặp vô hạn
            if (isAdding) AddItem(data, amount);
            else ConsumeItem(data, amount);
            isSyncing = false;
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SyncItemToServer(string itemName, int amount, bool isAdding)
    {
        ItemData data = Resources.Load<ItemData>("Items/" + itemName);
        if (data != null)
        {
            isSyncing = true;
            if (isAdding) AddItem(data, amount);
            else ConsumeItem(data, amount);
            isSyncing = false;
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_RequestDespawnItem(NetworkObject itemNetObj)
    {
        if (itemNetObj != null && itemNetObj.IsValid)
        {
            Runner.Despawn(itemNetObj);
        }
    }
}