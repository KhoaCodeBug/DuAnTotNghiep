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

    [Header("Balo Đang Trang Bị")]
    public ItemData equippedBackpack;
    public int currentBackpackLevel = 0; // 0 = Chưa có Balo (Mặc định 15 ô: 5 Hotbar + 10 Kho)

    // Cờ chống lặp vô hạn khi 2 máy gọi điện cho nhau
    private bool isSyncing = false;

    private void Awake()
    {
        // Khởi tạo sẵn tối đa 40 ô slot trong danh sách
        for (int i = 0; i < 40; i++)
        {
            slots.Add(new InventorySlot(null, 0));
        }
        maxSlots = 15; // Mặc định khởi đầu 15 ô (5 Hotbar + 10 Kho)
    }

    public bool EquipBackpack(ItemData backpack)
    {
        if (backpack == null || backpack.category != ItemCategory.Backpack) return false;

        // KIỂM TRA PHONG CÁCH PUBG: Nếu cấp balo định mặc nhỏ hơn hoặc bằng cấp hiện tại -> Từ chối!
        if (backpack.backpackLevel <= currentBackpackLevel)
        {
            Debug.Log($"[INVENTORY] ❌ Balo {backpack.itemName} (Cấp {backpack.backpackLevel}) không cao hơn Balo hiện tại (Cấp {currentBackpackLevel})!");
            return false;
        }

        equippedBackpack = backpack;
        currentBackpackLevel = backpack.backpackLevel;

        int targetTotalSlots = 15 + (backpack.backpackLevel * 5); // Cấp 1=20, 2=25, 3=30, 4=35, 5=40
        SetMaxSlots(targetTotalSlots);
        Debug.Log($"[INVENTORY] ✅ Đã nâng cấp Balo Cấp {currentBackpackLevel}! Tổng sức chứa: {maxSlots} ô.");
        return true;
    }

    public void SetMaxSlots(int newMax)
    {
        maxSlots = Mathf.Clamp(newMax, 15, 40);
        while (slots.Count < maxSlots)
        {
            slots.Add(new InventorySlot(null, 0));
        }
        UpdateUI();
    }

    public override void Spawned()
    {
        if (HasInputAuthority)
        {
            ItemData startingWeapon = Resources.Load<ItemData>("Items/AK47");
            if (startingWeapon != null)
            {
                AddItem(startingWeapon, 1);
            }

            ItemData startingS12K = Resources.Load<ItemData>("Items/S12K");
            if (startingS12K != null)
            {
                AddItem(startingS12K, 1);
            }
        }
    }

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
        int originalAmount = amountToAdd;

        // 1. Nối chồng đạn/đồ gộp (Stacking): Ưu tiên tìm trong Ba lô (Slot 5->maxSlots) trước, rồi mới đến Hotbar (0->4)
        if (itemToAdd.isStackable)
        {
            // Quét trong Ba lô trước
            for (int i = 5; i < maxSlots; i++)
            {
                if (amountToAdd <= 0) break;
                if (i < slots.Count && slots[i].item != null && slots[i].item.itemName == itemToAdd.itemName && slots[i].amount < itemToAdd.maxStack)
                {
                    int spaceLeft = itemToAdd.maxStack - slots[i].amount;
                    if (amountToAdd <= spaceLeft)
                    {
                        slots[i].AddAmount(amountToAdd);
                        amountToAdd = 0;
                        break;
                    }
                    else
                    {
                        slots[i].AddAmount(spaceLeft);
                        amountToAdd -= spaceLeft;
                    }
                }
            }

            // Nếu chưa xếp hết -> mới tìm tiếp trong Hotbar
            for (int i = 0; i < 5; i++)
            {
                if (amountToAdd <= 0) break;
                if (i < slots.Count && slots[i].item != null && slots[i].item.itemName == itemToAdd.itemName && slots[i].amount < itemToAdd.maxStack)
                {
                    int spaceLeft = itemToAdd.maxStack - slots[i].amount;
                    if (amountToAdd <= spaceLeft)
                    {
                        slots[i].AddAmount(amountToAdd);
                        amountToAdd = 0;
                        break;
                    }
                    else
                    {
                        slots[i].AddAmount(spaceLeft);
                        amountToAdd -= spaceLeft;
                    }
                }
            }
        }

        // 2. Xếp vào ô trống mới: Ưu tiên nhét vào Ba lô (Slot 5->maxSlots) trước!
        while (amountToAdd > 0)
        {
            int emptyIndex = -1;

            // Tìm ô trống trong Ba lô trước (5 đến maxSlots-1)
            for (int i = 5; i < maxSlots; i++)
            {
                if (i < slots.Count && (slots[i].item == null || slots[i].amount <= 0))
                {
                    emptyIndex = i;
                    break;
                }
            }

            // Nếu Ba lô đã đầy -> mới nhét vào Hotbar (0 đến 4)
            if (emptyIndex == -1)
            {
                for (int i = 0; i < 5; i++)
                {
                    if (i < slots.Count && (slots[i].item == null || slots[i].amount <= 0))
                    {
                        emptyIndex = i;
                        break;
                    }
                }
            }

            if (emptyIndex != -1)
            {
                int amountToStore = Mathf.Min(amountToAdd, itemToAdd.maxStack);
                slots[emptyIndex].item = itemToAdd;
                slots[emptyIndex].amount = amountToStore;
                amountToAdd -= amountToStore;
            }
            else
            {
                break; // Cả Ba lô lẫn Hotbar đều đã đầy
            }
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
            if (slot.amount <= 0)
            {
                slot.item = null;
                slot.amount = 0;
            }
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
        if (slot.amount <= 0)
        {
            slot.item = null;
            slot.amount = 0;
        }
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

    public void SwapSlots(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= slots.Count || toIndex < 0 || toIndex >= slots.Count) return;
        if (fromIndex == toIndex) return;

        InventorySlot temp = slots[fromIndex];
        slots[fromIndex] = slots[toIndex];
        slots[toIndex] = temp;

        UpdateUI();
    }

    private void UpdateUI()
    {
        if (Object != null && Object.IsValid && !HasInputAuthority && !HasStateAuthority) return;
        if (AutoUIManager.Instance != null) AutoUIManager.Instance.RefreshUI(this.slots, this.maxSlots);
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
        ItemData data = ItemDataLoader.LoadItem(itemName);
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
        ItemData data = ItemDataLoader.LoadItem(itemName);
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
        ItemData data = ItemDataLoader.LoadItem(itemName);
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