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
    public const int HotbarSlotCount = 5;
    public const int FixedTotalSlots = 20;

    [Header("Cài đặt Ba lô")]
    [Tooltip("Sức chứa cố định: 5 ô Hotbar + 15 ô Kho. Hệ thống nâng cấp balo hiện không được sử dụng.")]
    public int maxSlots = FixedTotalSlots;

    [Header("Cài đặt Nhặt Đồ")]
    public float pickupRadius = 0.5f;

    [Header("Danh sách các ô đang chứa đồ")]
    public List<InventorySlot> slots = new List<InventorySlot>();

    [Header("Cài đặt Rớt Đồ (Cá nhân)")]
    public GameObject droppedItemPrefab;
    public float dropLifeTime = 30f;

    // Cờ chống lặp vô hạn khi 2 máy gọi điện cho nhau
    private bool isSyncing = false;

    // Starting loadout is selected once by State Authority.  The item ID is
    // replicated so the owning client can place the exact same item in its
    // local fixed-index inventory without running its own random roll.
    [Networked] private NetworkBool HasStartingWeapon { get; set; }
    [Networked] private NetworkString<_64> StartingWeaponId { get; set; }
    private bool hasAppliedStartingWeaponLocally;
    [Networked] private NetworkBool HasStartingFlashlight { get; set; }
    private bool hasAppliedStartingFlashlightLocally;

    /// <summary>
    /// Exact fixed-slot inventory state captured by State Authority before a
    /// military-finale avatar is despawned. Keeping slot order also preserves
    /// the player's equipped hotbar layout.
    /// </summary>
    public sealed class MilitaryRespawnSnapshot
    {
        public readonly string[] ItemIds = new string[FixedTotalSlots];
        public readonly int[] Amounts = new int[FixedTotalSlots];
    }

    private void Awake()
    {
        maxSlots = FixedTotalSlots;
        while (slots.Count < FixedTotalSlots)
        {
            slots.Add(new InventorySlot(null, 0));
        }
    }

    public bool EquipBackpack(ItemData backpack)
    {
        if (backpack != null && backpack.category == ItemCategory.Backpack)
            Debug.LogWarning($"[INVENTORY] Bỏ qua '{backpack.itemName}': sức chứa hiện được cố định ở {FixedTotalSlots} ô.");
        return false;
    }

    public void SetMaxSlots(int newMax)
    {
        if (newMax != FixedTotalSlots)
            Debug.LogWarning($"[INVENTORY] Yêu cầu đổi sức chứa thành {newMax} bị bỏ qua; sức chứa cố định là {FixedTotalSlots} ô.");

        maxSlots = FixedTotalSlots;
        while (slots.Count < maxSlots)
        {
            slots.Add(new InventorySlot(null, 0));
        }
        UpdateUI();
    }

    public override void Spawned()
    {
        // A military checkpoint respawn replaces the NetworkObject. Restore
        // its saved inventory before the normal random starting-loadout path
        // can add duplicate gear.
        if (HasStateAuthority && HostModeSpawner.Instance != null &&
            HostModeSpawner.Instance.TryTakeMilitaryInventorySnapshot(Object.InputAuthority, out MilitaryRespawnSnapshot snapshot))
        {
            ApplyMilitaryRespawnSnapshot(snapshot);
            return;
        }

        // Grant starting gear based strictly on canonical difficulty rules on State Authority
        if (HasStateAuthority && !HasStartingWeapon && !HasStartingFlashlight)
        {
            if (TutorialSession.IsActive)
                MarkTutorialLoadoutReady();
            else
                GrantDifficultyStartingLoadout();
        }

        ApplyReplicatedStartingWeapon();
        ApplyReplicatedStartingFlashlight();
    }

    public override void Render()
    {
        // Late-join/input-authority clients receive the Networked values after
        // Spawned, so retry here until their starting gear is applied.
        ApplyReplicatedStartingWeapon();
        ApplyReplicatedStartingFlashlight();
    }

    private void GrantDifficultyStartingLoadout()
    {
        int difficulty = DifficultyRules.ActiveDifficulty;
        DifficultyRules.StarterItem[] loadout = DifficultyRules.GetStarterGearLoadout(difficulty);

        foreach (var item in loadout)
        {
            ItemData data = ItemDataLoader.LoadItem(item.ItemId);
            if (data == null)
            {
                Debug.LogWarning($"[STARTING LOADOUT] Item '{item.ItemId}' not found in Resources/Items.");
                continue;
            }

            if (item.PreferHotbar)
            {
                StartingWeaponId = data.name;
                HasStartingWeapon = true;
                PlaceStartingWeaponInHotbar(data);
                hasAppliedStartingWeaponLocally = true;
                HostModeSpawner spawner = HostModeSpawner.Instance;
                if (spawner != null) spawner.CacheStartingWeapon(Object.InputAuthority, data);
            }
            else
            {
                if (data.name == FlashlightController.ItemId || data.itemName == FlashlightController.ItemId)
                {
                    HasStartingFlashlight = true;
                    PlaceStartingFlashlightInBackpack();
                    hasAppliedStartingFlashlightLocally = true;
                }
                else
                {
                    AddItem(data, item.Amount);
                }
            }
        }

        Debug.Log($"[STARTING LOADOUT] Granted {DifficultyRules.GetDifficultyName(difficulty)} starting loadout to Player {Object.InputAuthority}.");
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

    private void ApplyReplicatedStartingFlashlight()
    {
        if (!HasInputAuthority || hasAppliedStartingFlashlightLocally || !HasStartingFlashlight) return;
        PlaceStartingFlashlightInBackpack();
        hasAppliedStartingFlashlightLocally = true;
    }

    private void PlaceStartingFlashlightInBackpack()
    {
        ItemData flashlight = ItemDataLoader.LoadItem(FlashlightController.ItemId);
        if (flashlight == null || HasItemNamed(FlashlightController.ItemId)) return;

        while (slots.Count < maxSlots) slots.Add(new InventorySlot(null, 0));
        for (int i = 5; i < maxSlots; i++)
        {
            if (slots[i] != null && slots[i].item != null && slots[i].amount > 0) continue;
            if (slots[i] == null) slots[i] = new InventorySlot(flashlight, 1);
            else { slots[i].item = flashlight; slots[i].amount = 1; }
            UpdateUI();
            return;
        }

        Debug.LogWarning("[FLASHLIGHT] Backpack is full; starting flashlight could not be placed.");
    }

    public MilitaryRespawnSnapshot CaptureMilitaryRespawnSnapshot()
    {
        MilitaryRespawnSnapshot snapshot = new MilitaryRespawnSnapshot();
        int count = Mathf.Min(FixedTotalSlots, slots.Count);
        for (int i = 0; i < count; i++)
        {
            InventorySlot slot = slots[i];
            if (slot == null || slot.item == null || slot.amount <= 0) continue;
            snapshot.ItemIds[i] = slot.item.name;
            snapshot.Amounts[i] = slot.amount;
        }
        return snapshot;
    }

    private void ApplyMilitaryRespawnSnapshot(MilitaryRespawnSnapshot snapshot)
    {
        if (snapshot == null) return;
        while (slots.Count < FixedTotalSlots) slots.Add(new InventorySlot(null, 0));
        for (int i = 0; i < FixedTotalSlots; i++)
        {
            string itemId = snapshot.ItemIds[i];
            ItemData item = string.IsNullOrWhiteSpace(itemId) ? null : ItemDataLoader.LoadItem(itemId);
            if (slots[i] == null) slots[i] = new InventorySlot(item, Mathf.Max(0, snapshot.Amounts[i]));
            else
            {
                slots[i].item = item;
                slots[i].amount = item == null ? 0 : Mathf.Max(0, snapshot.Amounts[i]);
            }

            if (HasStateAuthority && !HasInputAuthority)
                RPC_ApplyMilitaryRespawnSlot(i, itemId, item == null ? 0 : Mathf.Max(0, snapshot.Amounts[i]));
        }
        UpdateUI();
        if (HasStateAuthority && !HasInputAuthority) RPC_CompleteMilitaryRespawnSnapshot();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_ApplyMilitaryRespawnSlot(int index, NetworkString<_64> itemId, int amount)
    {
        if (index < 0 || index >= FixedTotalSlots) return;
        while (slots.Count < FixedTotalSlots) slots.Add(new InventorySlot(null, 0));
        ItemData item = string.IsNullOrWhiteSpace(itemId.ToString()) ? null : ItemDataLoader.LoadItem(itemId.ToString());
        if (slots[index] == null) slots[index] = new InventorySlot(item, item == null ? 0 : Mathf.Max(0, amount));
        else
        {
            slots[index].item = item;
            slots[index].amount = item == null ? 0 : Mathf.Max(0, amount);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_CompleteMilitaryRespawnSnapshot()
    {
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
                    RPC_RequestPickupItem(netObj);

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

        // A flashlight is equipped by moving it into Hotbar, never consumed.
        if (item.name == FlashlightController.ItemId || item.itemName == FlashlightController.ItemId)
        {
            if (index >= 5) EquipFlashlightToHotbar(index);
            return;
        }
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
                    health.RequestBandageForFirstWound(_ => { });
                    return;
                }
            }
            else if (nameLower.Contains("painkiller") || nameLower.Contains("thuốc") || nameLower.Contains("đau"))
            {
                if (health != null && (health.isInPain || health.currentHealth < health.maxHealth))
                {
                    health.UsePainkiller();
                    // PlayerHealth validates and consumes the PainKiller on
                    // State Authority, which then syncs the inventory owner.
                    return;
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

    private void EquipFlashlightToHotbar(int inventoryIndex)
    {
        for (int i = 0; i < Mathf.Min(5, slots.Count); i++)
        {
            if (slots[i] != null && slots[i].item != null && slots[i].amount > 0) continue;
            InventorySlot source = slots[inventoryIndex];
            if (slots[i] == null) slots[i] = new InventorySlot(source.item, source.amount);
            else { slots[i].item = source.item; slots[i].amount = source.amount; }
            source.item = null;
            source.amount = 0;
            UpdateUI();
            return;
        }

        Debug.Log("[FLASHLIGHT] Hotbar is full. Drag the flashlight to a Hotbar slot to equip it.");
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

                if (slots[i].amount <= 0)
                {
                    // Inventory uses stable slot indices for Hotbar/UI/network
                    // sync. Removing the list element shifts every later slot
                    // and gradually reduces usable capacity after consuming.
                    slots[i].item = null;
                    slots[i].amount = 0;
                }
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
    public void RPC_RequestPickupItem(NetworkObject itemNetObj)
    {
        if (itemNetObj == null || !itemNetObj.IsValid)
        {
            Debug.LogWarning("[INVENTORY] Rejected pickup request for an invalid NetworkObject.");
            return;
        }

        ItemPickup pickup = itemNetObj.GetComponent<ItemPickup>();
        if (pickup == null || !pickup.isActiveAndEnabled || pickup.item == null || pickup.amount <= 0)
        {
            Debug.LogWarning("[INVENTORY] Rejected pickup request without valid authoritative item data.");
            return;
        }

        float maxPickupDistance = Mathf.Max(0.1f, pickupRadius) + 0.75f;
        if ((itemNetObj.transform.position - transform.position).sqrMagnitude > maxPickupDistance * maxPickupDistance)
        {
            Debug.LogWarning($"[INVENTORY] Rejected out-of-range pickup '{pickup.item.itemName}'.");
            return;
        }

        // Item identity and quantity come only from the server-owned pickup.
        // Never trust itemName/amount supplied by Input Authority.
        if (!HasCapacityFor(pickup.item, pickup.amount))
        {
            Debug.LogWarning($"[INVENTORY] Rejected pickup '{pickup.item.itemName}': insufficient capacity.");
            return;
        }

        if (AddItem(pickup.item, pickup.amount))
            Runner.Despawn(itemNetObj);
    }

    private bool HasCapacityFor(ItemData item, int amount)
    {
        if (item == null || amount <= 0 || item.maxStack <= 0) return false;

        int capacity = 0;
        int limit = Mathf.Min(maxSlots, slots.Count);
        for (int i = 0; i < limit; i++)
        {
            InventorySlot slot = slots[i];
            if (slot == null || slot.item == null || slot.amount <= 0)
            {
                capacity += item.maxStack;
            }
            else if (item.isStackable && slot.item.itemName == item.itemName)
            {
                capacity += Mathf.Max(0, item.maxStack - slot.amount);
            }

            if (capacity >= amount) return true;
        }

        return false;
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
        // A client may report consumption, but it may never mint inventory on the
        // server. All additions must originate from an authoritative gameplay
        // action such as a validated world pickup, loot grant or trade.
        if (isAdding)
        {
            Debug.LogWarning($"[INVENTORY] Rejected client item-add request: '{itemName}' x{amount}.");
            return;
        }

        if (amount <= 0)
        {
            Debug.LogWarning($"[INVENTORY] Rejected invalid client item-sync amount: {amount}.");
            return;
        }

        ItemData data = ItemDataLoader.LoadItem(itemName);
        if (data != null)
        {
            isSyncing = true;
            ConsumeItem(data, amount);
            isSyncing = false;
        }
    }

}
