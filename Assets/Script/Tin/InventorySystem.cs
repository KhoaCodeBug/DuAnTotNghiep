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

    // Starting loadout is selected once by State Authority.  The item ID is
    // replicated so the owning client can place the exact same item in its
    // local fixed-index inventory without running its own random roll.
    [Networked] private NetworkBool HasStartingWeapon { get; set; }
    [Networked] private NetworkString<_64> StartingWeaponId { get; set; }
    private bool hasAppliedStartingWeaponLocally;

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

        if (HasInputAuthority && !HasStateAuthority)
        {
            RPC_SyncBackpackToServer(maxSlots, currentBackpackLevel);
        }
    }

    public override void Spawned()
    {
        // Only the server chooses the random starting weapon.  A regular
        // client must never roll independently, otherwise its inventory can
        // disagree with the host and other players.
        if (HasStateAuthority && !HasStartingWeapon)
        {
            if (TutorialSession.IsActive)
                MarkTutorialLoadoutReady();
            else
                GrantRandomStartingWeapon();
        }

        ApplyReplicatedStartingWeapon();
    }

    public override void Render()
    {
        // Late-join/input-authority clients receive the Networked values after
        // Spawned, so retry here until their single starting weapon is applied.
        ApplyReplicatedStartingWeapon();
    }

    private void GrantRandomStartingWeapon()
    {
        ItemData selectedWeapon = null;
        HostModeSpawner spawner = HostModeSpawner.Instance;
        if (spawner != null)
        {
            spawner.TryGetCachedStartingWeapon(Object.InputAuthority, out selectedWeapon);
        }

        if (selectedWeapon == null)
        {
            List<ItemData> weaponPool = new List<ItemData>();
            foreach (ItemData item in Resources.LoadAll<ItemData>("Items"))
            {
                if (item != null && item.category == ItemCategory.Weapon && !string.IsNullOrWhiteSpace(item.name))
                {
                    weaponPool.Add(item);
                }
            }

            if (weaponPool.Count == 0)
            {
                Debug.LogError("[STARTING LOADOUT] No valid Weapon ItemData was found in Resources/Items.");
                return;
            }

            selectedWeapon = weaponPool[Random.Range(0, weaponPool.Count)];
            if (spawner != null) spawner.CacheStartingWeapon(Object.InputAuthority, selectedWeapon);
        }

        StartingWeaponId = selectedWeapon.name;
        HasStartingWeapon = true;

        // State Authority owns the canonical inventory.  Its local view is
        // updated immediately; the client receives the replicated ID below.
        PlaceStartingWeaponInHotbar(selectedWeapon);
        hasAppliedStartingWeaponLocally = true;
        Debug.Log($"[STARTING LOADOUT] Player {Object.InputAuthority} received {selectedWeapon.itemName} in hotbar.");
    }

    private void MarkTutorialLoadoutReady()
    {
        // The tutorial teaches looting before firearms. S12K is deliberately
        // placed in the kitchen cabinet with its ammunition, not in hotbar.
        HasStartingWeapon = true;
        StartingWeaponId = string.Empty;
        hasAppliedStartingWeaponLocally = true;
    }

    public bool HasItemNamed(string itemName)
    {
        foreach (InventorySlot slot in slots)
        {
            if (slot == null || slot.item == null || slot.amount <= 0) continue;
            if (string.Equals(slot.item.name, itemName, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(slot.item.itemName, itemName, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private void ApplyReplicatedStartingWeapon()
    {
        if (TutorialSession.IsActive) return;
        if (!HasInputAuthority || hasAppliedStartingWeaponLocally || !HasStartingWeapon) return;

        ItemData selectedWeapon = ItemDataLoader.LoadItem(StartingWeaponId.ToString());
        if (selectedWeapon == null || selectedWeapon.category != ItemCategory.Weapon)
        {
            Debug.LogError($"[STARTING LOADOUT] Invalid replicated weapon ID '{StartingWeaponId}'.");
            return;
        }

        PlaceStartingWeaponInHotbar(selectedWeapon);
        hasAppliedStartingWeaponLocally = true;
        Debug.Log($"[STARTING LOADOUT] Applied {selectedWeapon.itemName} to local hotbar.");
    }

    private void PlaceStartingWeaponInHotbar(ItemData weapon)
    {
        if (weapon == null) return;

        while (slots.Count < 5)
        {
            slots.Add(new InventorySlot(null, 0));
        }

        // Slot 0 is preferred.  The fallback protects future flows that add a
        // hotbar item before this player finishes spawning.
        int targetSlot = -1;
        for (int i = 0; i < 5; i++)
        {
            if (slots[i] == null || slots[i].item == null || slots[i].amount <= 0)
            {
                targetSlot = i;
                break;
            }
        }

        if (targetSlot < 0)
        {
            Debug.LogWarning($"[STARTING LOADOUT] No empty hotbar slot for {weapon.itemName}; loadout was not applied.");
            return;
        }

        if (slots[targetSlot] == null) slots[targetSlot] = new InventorySlot(weapon, 1);
        else
        {
            slots[targetSlot].item = weapon;
            slots[targetSlot].amount = 1;
        }

        UpdateUI();
    }

    public int GetWeaponItemCount()
    {
        int total = 0;
        foreach (InventorySlot slot in slots)
        {
            if (slot != null && slot.item != null && slot.item.category == ItemCategory.Weapon && slot.amount > 0)
            {
                total += slot.amount;
            }
        }
        return total;
    }

    public bool HasWeapon(string itemIdOrName)
    {
        if (string.IsNullOrWhiteSpace(itemIdOrName)) return false;
        foreach (InventorySlot slot in slots)
        {
            if (slot == null || slot.item == null || slot.item.category != ItemCategory.Weapon || slot.amount <= 0) continue;
            if (slot.item.name == itemIdOrName || slot.item.itemName == itemIdOrName) return true;
        }
        return false;
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
        if (item == null) return;
        bool itemUsed = false;

        PlayerHealth health = GetComponent<PlayerHealth>();
        PlayerStamina stamina = GetComponent<PlayerStamina>();
        PlayerSurvival survival = GetComponent<PlayerSurvival>();

        if (item.category == ItemCategory.Medical)
        {
            string nameLower = item.itemName.ToLower();
            if (nameLower.Contains("bandage") || nameLower.Contains("băng"))
            {
                if (health != null && (health.isBleeding || health.currentHealth < health.maxHealth))
                {
                    health.SetGlobalBleeding(false);
                    if (item.healAmount > 0) health.Heal(item.healAmount);
                    itemUsed = true;
                }
            }
            else if (nameLower.Contains("painkiller") || nameLower.Contains("thuốc") || nameLower.Contains("đau"))
            {
                if (health != null && (health.isInPain || health.currentHealth < health.maxHealth))
                {
                    health.UsePainkiller();
                    if (item.healAmount > 0) health.Heal(item.healAmount);
                    itemUsed = true;
                }
            }
            else if (health != null && health.currentHealth < health.maxHealth)
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
        else if (item.category == ItemCategory.Backpack)
        {
            itemUsed = EquipBackpack(item);
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
        // State Authority can be the Host for every remote player.  It owns
        // their canonical data, but must never paint a remote inventory into
        // the Host's local UI.  Only the local Input Authority owns a UI.
        if (!HasInputAuthority) return;
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

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SyncBackpackToServer(int newMaxSlots, int backpackLevel)
    {
        maxSlots = Mathf.Clamp(newMaxSlots, 15, 40);
        currentBackpackLevel = backpackLevel;
        while (slots.Count < maxSlots)
        {
            slots.Add(new InventorySlot(null, 0));
        }
        Debug.Log($"[SERVER INVENTORY] ✅ Đã đồng bộ sức chứa Balo mới cho Player: {maxSlots} ô (Level {currentBackpackLevel})");
    }
}
