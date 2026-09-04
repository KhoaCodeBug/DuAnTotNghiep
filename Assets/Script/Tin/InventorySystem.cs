using System.Collections.Generic;
using UnityEngine;
using Fusion;

[System.Serializable]
public class InventorySlot
{
    public ItemData item;
    public int amount;
    // Flashlight is non-stackable. This value is meaningful only for that item
    // and lets every looted flashlight retain its own charge.
    [Range(0f, 1f)] public float battery01 = 1f;

    public InventorySlot(ItemData item, int amount, float battery01 = 1f)
    {
        this.item = item;
        this.amount = amount;
        this.battery01 = Mathf.Clamp01(battery01);
    }
    public void AddAmount(int value) { amount += value; }
}

public class InventorySystem : NetworkBehaviour
{
    public const int HotbarSlotCount = 5;
    // Kept as a compatibility alias for old scenes/tests: the starting
    // inventory is 15 storage slots + 5 hotbar slots.
    public const int FixedTotalSlots = BackpackCapacityRules.InitialTotalSlots;
    public const int MaxTotalSlots = BackpackCapacityRules.MaxTotalSlots;

    [Header("Cài đặt Ba lô")]
    [Tooltip("Sức chứa gồm 5 ô Hotbar và 15-50 ô Kho. Nâng cấp balo tăng phần Kho, không tăng Hotbar.")]
    public int maxSlots = FixedTotalSlots;

    [SerializeField, Range(0, BackpackCapacityRules.MaxBackpackLevel)]
    private int backpackLevel;

    [Networked] private int NetworkedBackpackLevel { get; set; }
    [Networked] private NetworkString<_64> NetworkedBackpackId { get; set; }
    [Networked] private int NetworkedQuestBackpackRewardClaimMask { get; set; }
    private int localQuestBackpackRewardClaimMask;
    private int lastAppliedBackpackLevel = -1;

    public int CurrentBackpackLevel => BackpackCapacityRules.ClampLevel(backpackLevel);
    public int CurrentBackpackSlots => Mathf.Max(BackpackCapacityRules.BaseBackpackSlots,
        maxSlots - HotbarSlotCount);
    public bool HasMaximumBackpack => CurrentBackpackSlots >= BackpackCapacityRules.MaxBackpackSlots;
    public int QuestBackpackRewardClaimMask => IsNetworkObjectReady
        ? NetworkedQuestBackpackRewardClaimMask
        : localQuestBackpackRewardClaimMask;

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
    [Networked] private NetworkBool StartingLoadoutResolved { get; set; }
    private float nextStartingLoadoutRetryTime;
    private bool postRestoreEntitlementsReconciled;
    private bool pendingSpawnFlashlightGrant;
    private bool pendingSpawnFlashlightWarningLogged;
    private float nextSpawnEntitlementRetryTime;

    public bool HasPendingSpawnFlashlightGrant => pendingSpawnFlashlightGrant;

    /// <summary>
    /// Exact fixed-slot inventory state captured by State Authority before a
    /// military-finale avatar is despawned. Keeping slot order also preserves
    /// the player's equipped hotbar layout.
    /// </summary>
    public sealed class MilitaryRespawnSnapshot
    {
        public readonly string[] ItemIds = new string[MaxTotalSlots];
        public readonly int[] Amounts = new int[MaxTotalSlots];
        public int MaxSlots = FixedTotalSlots;
        public int BackpackLevel;
        public string BackpackId;
        public int QuestBackpackRewardClaimMask;
    }

    private void Awake()
    {
        maxSlots = BackpackCapacityRules.ClampTotalSlots(maxSlots);
        backpackLevel = BackpackCapacityRules.GetLevelForTotalSlots(maxSlots);
        EnsureStableSlotStorage();
    }

    private bool IsNetworkObjectReady => Object != null && Object.IsValid;

    private void EnsureStableSlotStorage()
    {
        while (slots.Count < MaxTotalSlots)
        {
            slots.Add(new InventorySlot(null, 0));
        }
    }

    public bool CanAcceptBackpackLoot(ItemData backpackItem)
    {
        if (backpackItem == null || backpackItem.category != ItemCategory.Backpack) return false;
        int itemLevel = backpackItem.backpackLevel > 0
            ? backpackItem.backpackLevel
            : BackpackCapacityRules.GetLevelForBackpackSlots(BackpackCapacityRules.GetStorageSlots(backpackItem));
        return itemLevel > CurrentBackpackLevel;
    }

    public static bool IsFlashlight(ItemData item) => item != null &&
        (item.name == FlashlightController.ItemId || item.itemName == FlashlightController.ItemId);

    public bool TryGetFlashlightBattery(int slotIndex, out float battery01)
    {
        battery01 = 0f;
        if (slotIndex < 0 || slotIndex >= slots.Count) return false;
        InventorySlot slot = slots[slotIndex];
        if (slot == null || slot.amount <= 0 || !IsFlashlight(slot.item)) return false;
        battery01 = Mathf.Clamp01(slot.battery01);
        return true;
    }

    public bool IsFlashlightInHotbarSlot(int slotIndex) =>
        slotIndex >= 0 && slotIndex < HotbarSlotCount && TryGetFlashlightBattery(slotIndex, out _);

    public void SetFlashlightBatteryLocal(int slotIndex, float battery01)
    {
        if (!TryGetFlashlightBattery(slotIndex, out _)) return;
        slots[slotIndex].battery01 = Mathf.Clamp01(battery01);
    }

    /// <summary>Death destroys all personal flashlights; they are never restored by a respawn snapshot.</summary>
    public void AuthorityRemoveAllFlashlightsOnDeath()
    {
        if (!HasStateAuthority) return;
        bool removed = false;
        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlot slot = slots[i];
            if (slot == null || !IsFlashlight(slot.item) || slot.amount <= 0) continue;
            slot.item = null;
            slot.amount = 0;
            slot.battery01 = 0f;
            removed = true;
        }

        if (!removed) return;
        GetComponent<FlashlightController>()?.AuthorityClearFlashlightState();
        UpdateUI();
        if (!HasInputAuthority) RPC_RemoveAllFlashlightsOnDeath();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_RemoveAllFlashlightsOnDeath()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlot slot = slots[i];
            if (slot == null || !IsFlashlight(slot.item) || slot.amount <= 0) continue;
            slot.item = null;
            slot.amount = 0;
            slot.battery01 = 0f;
        }
        UpdateUI();
    }

    public bool HasClaimedQuestBackpackReward(int level) =>
        BackpackQuestRewardRules.IsClaimed(QuestBackpackRewardClaimMask, level);

    public bool EquipBackpack(ItemData backpack)
    {
        if (backpack == null || backpack.category != ItemCategory.Backpack) return false;

        // EditMode helpers and offline/tutorial inventories have no Fusion
        // object; they still use the same authoritative upgrade calculation.
        if (!IsNetworkObjectReady)
            return ApplyBackpackUpgradeLocal(backpack);

        if (!HasStateAuthority)
        {
            if (HasInputAuthority && Runner != null)
                RPC_RequestEquipBackpack(backpack.name);
            return false;
        }

        return ApplyBackpackUpgradeLocal(backpack);
    }

    /// <summary>
    /// Grants a quest milestone backpack directly to this player's inventory.
    /// The method is intentionally separate from EquipBackpack so ordinary loot
    /// upgrades never trigger the hospital/military quest rewards.
    /// </summary>
    public bool TryGrantQuestBackpackReward(int level)
    {
        if (!BackpackQuestRewardRules.IsRewardLevel(level) ||
            (IsNetworkObjectReady && !HasStateAuthority))
            return false;

        int currentMask = QuestBackpackRewardClaimMask;
        if (BackpackQuestRewardRules.IsClaimed(currentMask, level)) return false;

        ItemData rewardBackpack = BackpackItemCatalog.GetOrCreate(level);
        if (rewardBackpack == null) return false;

        int currentLevel = CurrentBackpackLevel;
        bool upgraded = currentLevel < level;
        if (upgraded && !ApplyBackpackUpgradeLocal(rewardBackpack)) return false;

        SetQuestBackpackRewardClaimMask(
            BackpackQuestRewardRules.MarkClaimed(currentMask, level));

        HostModeSpawner.Instance?.RecordQuestBackpackEntitlement(Object != null && Object.IsValid
                ? Object.InputAuthority : PlayerRef.None,
            level, QuestBackpackRewardClaimMask,
            BackpackQuestRewardRules.GetClaimBit(level));

        if (level == BackpackQuestRewardRules.RadioBackpackLevel)
        {
            lastCapturedLevelFivePreviousLevel = currentLevel;
        }

        // For hospital level 4, notify presentation immediately. For level 5,
        // presentation is sequenced after the local map reveal closes.
        if (currentLevel <= level && level != BackpackQuestRewardRules.RadioBackpackLevel)
            NotifyQuestBackpackReward(level, rewardBackpack, currentLevel);

        Debug.Log($"[BACKPACK QUEST] Claimed level {level} milestone reward " +
                  $"for {name}; equipped level is now {CurrentBackpackLevel}.");
        return true;
    }

    private int lastCapturedLevelFivePreviousLevel = -1;
    public int LastCapturedLevelFivePreviousLevel => lastCapturedLevelFivePreviousLevel;

    private System.Action pendingLevelFiveGrantedCallback;

    public void RequestClaimLevelFiveBackpackReward(System.Action onGranted = null)
    {
        if (HasClaimedQuestBackpackReward(BackpackQuestRewardRules.RadioBackpackLevel))
        {
            return;
        }

        lastCapturedLevelFivePreviousLevel = CurrentBackpackLevel;

        if (!IsNetworkObjectReady || HasStateAuthority)
        {
            bool granted = TryGrantQuestBackpackReward(BackpackQuestRewardRules.RadioBackpackLevel);
            if (granted)
            {
                onGranted?.Invoke();
            }
            return;
        }

        if (HasInputAuthority && Runner != null)
        {
            pendingLevelFiveGrantedCallback = onGranted;
            RPC_RequestClaimQuestBackpackReward(Runner.LocalPlayer, BackpackQuestRewardRules.RadioBackpackLevel, CurrentBackpackLevel);
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestClaimQuestBackpackReward(PlayerRef requester, int level, int clientPreviousLevel = -1, RpcInfo info = default)
    {
        if (requester == PlayerRef.None || (info.Source != PlayerRef.None && info.Source != requester))
        {
            Debug.LogWarning($"[BACKPACK QUEST] Rejected claim request: source={info.Source}, requested={requester}.");
            return;
        }

        if (Object != null && Object.IsValid && Object.InputAuthority != PlayerRef.None && Object.InputAuthority != requester)
        {
            Debug.LogWarning($"[BACKPACK QUEST] Rejected claim request: requester={requester} does not match InputAuthority={Object.InputAuthority}.");
            return;
        }

        if (!BackpackQuestRewardRules.IsRewardLevel(level)) return;

        if (level == BackpackQuestRewardRules.RadioBackpackLevel)
        {
            MainQuestManager mainQuest = MainQuestManager.Instance;
            if (mainQuest == null || !mainQuest.IsHospitalRadioRecovered)
            {
                Debug.LogWarning("[BACKPACK QUEST] Rejected level 5 claim: Radio 3/3 is not recovered yet.");
                return;
            }
        }

        int preGrantLevel = CurrentBackpackLevel >= 0 ? CurrentBackpackLevel : clientPreviousLevel;
        if (TryGrantQuestBackpackReward(level))
        {
            RPC_ConfirmQuestBackpackReward(requester, level, preGrantLevel);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ConfirmQuestBackpackReward([RpcTarget] PlayerRef targetPlayer, int level, int previousLevel = -1)
    {
        if (Runner == null || Runner.LocalPlayer != targetPlayer) return;

        if (previousLevel >= 0)
        {
            lastCapturedLevelFivePreviousLevel = previousLevel;
        }

        System.Action callback = pendingLevelFiveGrantedCallback;
        pendingLevelFiveGrantedCallback = null;
        callback?.Invoke();
    }

    private bool hasPendingRadioBackpackHandoffTriggered;

    public void TriggerLateOrPendingRadioBackpackRewardHandoff()
    {
        if (hasPendingRadioBackpackHandoffTriggered) return;
        if (HasClaimedQuestBackpackReward(BackpackQuestRewardRules.RadioBackpackLevel)) return;

        hasPendingRadioBackpackHandoffTriggered = true;
        StartCoroutine(WaitForMapClosedThenPresentRoutine());
    }

    private System.Collections.IEnumerator WaitForMapClosedThenPresentRoutine()
    {
        while (QuestFlowUIPrototype.Instance != null && QuestFlowUIPrototype.Instance.IsQuestOverlayOpen)
        {
            yield return null;
        }

        MainQuestManager mainQuest = MainQuestManager.Instance;
        mainQuest?.TriggerLevelFiveRewardSequence(true);
    }

    public void ReconcileLateJoinRadioBackpackReward()
    {
        if (!HasStateAuthority || !IsNetworkObjectReady) return;
        MainQuestManager mainQuest = MainQuestManager.Instance;
        if (mainQuest == null || !mainQuest.IsHospitalRadioRecovered) return;

        if (!HasClaimedQuestBackpackReward(BackpackQuestRewardRules.RadioBackpackLevel) &&
            CurrentBackpackLevel < BackpackQuestRewardRules.RadioBackpackLevel)
        {
            if (Object.InputAuthority != PlayerRef.None)
            {
                RPC_HandoffPendingRadioBackpackReward(Object.InputAuthority);
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_HandoffPendingRadioBackpackReward([RpcTarget] PlayerRef targetPlayer)
    {
        if (Runner == null || Runner.LocalPlayer != targetPlayer) return;
        TriggerLateOrPendingRadioBackpackRewardHandoff();
    }

    public void SetMaxSlots(int newMax)
    {
        int targetTotal = BackpackCapacityRules.ClampTotalSlots(newMax);
        int targetLevel = BackpackCapacityRules.GetLevelForTotalSlots(targetTotal);
        backpackLevel = targetLevel;
        SetMaxSlotsLocal(targetTotal);

        if (IsNetworkObjectReady && HasStateAuthority)
        {
            NetworkedBackpackLevel = targetLevel;
            NetworkedBackpackId = targetLevel <= 0 ? string.Empty : BackpackItemCatalog.GetOrCreate(targetLevel).name;
        }
    }

    private void SetMaxSlotsLocal(int targetTotal)
    {
        maxSlots = BackpackCapacityRules.ClampTotalSlots(targetTotal);
        EnsureStableSlotStorage();
        UpdateUI();
    }

    private void SetQuestBackpackRewardClaimMask(int claimMask)
    {
        localQuestBackpackRewardClaimMask = claimMask;
        if (IsNetworkObjectReady && HasStateAuthority)
            NetworkedQuestBackpackRewardClaimMask = claimMask;
    }

    private void NotifyQuestBackpackReward(int level, ItemData backpack, int previousLevel = -1)
    {
        if (IsNetworkObjectReady && HasStateAuthority && !HasInputAuthority)
        {
            RPC_ShowQuestBackpackReward(level, backpack != null ? backpack.name : string.Empty, previousLevel);
            return;
        }

        if (Application.isPlaying && (!IsNetworkObjectReady || HasInputAuthority))
            BackpackQuestRewardPresentation.ShowWithPreviousLevel(level, backpack, previousLevel);
    }

    private bool ApplyBackpackUpgradeLocal(ItemData backpack)
    {
        int storageSlots = BackpackCapacityRules.GetStorageSlots(backpack);
        int targetTotal = BackpackCapacityRules.HotbarSlots + storageSlots;
        if (targetTotal <= maxSlots) return false;

        backpackLevel = BackpackCapacityRules.GetLevelForBackpackSlots(storageSlots);
        SetMaxSlotsLocal(targetTotal);
        if (IsNetworkObjectReady && HasStateAuthority)
        {
            NetworkedBackpackLevel = backpackLevel;
            NetworkedBackpackId = backpack.name;
        }

        Debug.Log($"[INVENTORY] Equipped {backpack.itemName}: {CurrentBackpackSlots} storage slots.");
        return true;
    }

    private ItemData FindOwnedBackpack(string requestedId)
    {
        if (string.IsNullOrWhiteSpace(requestedId)) return null;
        for (int i = 0; i < Mathf.Min(maxSlots, slots.Count); i++)
        {
            InventorySlot slot = slots[i];
            if (slot == null || slot.item == null || slot.amount <= 0 ||
                slot.item.category != ItemCategory.Backpack) continue;
            if (string.Equals(slot.item.name, requestedId, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(slot.item.itemName, requestedId, System.StringComparison.OrdinalIgnoreCase))
                return slot.item;
        }
        return null;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestEquipBackpack(string requestedId, RpcInfo info = default)
    {
        if (!HasStateAuthority ||
            (info.Source != PlayerRef.None && info.Source != Object.InputAuthority)) return;

        ItemData requested = ItemDataLoader.LoadItem(requestedId);
        ItemData owned = FindOwnedBackpack(requested != null ? requested.name : requestedId);
        if (owned == null || !ApplyBackpackUpgradeLocal(owned)) return;

        // The State Authority consumes the exact owned backpack after applying
        // the upgrade; ConsumeItem's existing RPC mirrors that removal.
        ConsumeItem(owned, 1);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_ShowQuestBackpackReward(int level, string backpackId, int previousLevel = -1)
    {
        if (!BackpackQuestRewardRules.IsRewardLevel(level)) return;
        ItemData backpack = ItemDataLoader.LoadItem(backpackId);
        if (backpack == null) backpack = BackpackItemCatalog.GetOrCreate(level);
        BackpackQuestRewardPresentation.ShowWithPreviousLevel(level, backpack, previousLevel);
    }

    public override void Spawned()
    {
        postRestoreEntitlementsReconciled = false;
        pendingSpawnFlashlightGrant = false;
        pendingSpawnFlashlightWarningLogged = false;
        InitializeReplicatedBackpackState();
        ApplyReplicatedBackpackState();

        // A military checkpoint respawn replaces the NetworkObject. Restore
        // its saved inventory before the normal random starting-loadout path
        // can add duplicate gear.
        if (HasStateAuthority && HostModeSpawner.Instance != null &&
            HostModeSpawner.Instance.TryTakeMilitaryInventorySnapshot(Object.InputAuthority, out MilitaryRespawnSnapshot snapshot))
        {
            StartingLoadoutResolved = true;
            ApplyMilitaryRespawnSnapshot(snapshot);
        }
        else if (HasStateAuthority && !StartingLoadoutResolved)
        {
            // Grant starting gear based strictly on canonical difficulty rules on State Authority.
            if (TutorialSession.IsActive)
                MarkTutorialLoadoutReady();
            else
                TryGrantDifficultyStartingLoadout();
        }

        ApplyReplicatedStartingWeapon();

        if (HasStateAuthority)
        {
            TryReconcilePostRestoreEntitlements();
            ReconcileLateJoinRadioBackpackReward();
        }
    }

    private void TryReconcilePostRestoreEntitlements()
    {
        if (!HasStateAuthority || postRestoreEntitlementsReconciled || !StartingLoadoutResolved) return;

        ReconcileDurableQuestBackpackEntitlement();
        if (!AuthorityGrantFreshSpawnFlashlight()) return;

        pendingSpawnFlashlightGrant = false;
        postRestoreEntitlementsReconciled = true;
    }

    private void ReconcileDurableQuestBackpackEntitlement()
    {
        HostModeSpawner spawner = HostModeSpawner.Instance;
        if (spawner == null || Object == null || !Object.IsValid ||
            !spawner.TryGetQuestBackpackEntitlement(Object.InputAuthority, out int level,
                out int claimMask, out _))
            return;

        if (level > CurrentBackpackLevel)
        {
            ItemData backpack = BackpackItemCatalog.GetOrCreate(level);
            if (backpack != null) ApplyBackpackUpgradeLocal(backpack);
        }
        SetQuestBackpackRewardClaimMask(QuestBackpackRewardClaimMask | claimMask);
    }

    private bool AuthorityGrantFreshSpawnFlashlight()
    {
        EnsureStableSlotStorage();
        bool[] occupied = new bool[MaxTotalSlots];
        for (int i = 0; i < Mathf.Min(maxSlots, slots.Count); i++)
        {
            InventorySlot slot = slots[i];
            occupied[i] = slot != null && slot.item != null && slot.amount > 0 && !IsFlashlight(slot.item);
        }

        int targetSlot = SpawnFlashlightGrantRules.FindGrantSlot(occupied, maxSlots, HotbarSlotCount);
        if (targetSlot == SpawnFlashlightGrantRules.PendingSlot)
        {
            pendingSpawnFlashlightGrant = true;
            if (!pendingSpawnFlashlightWarningLogged)
            {
                pendingSpawnFlashlightWarningLogged = true;
                Debug.LogWarning($"[FLASHLIGHT] Inventory full for {Object.InputAuthority}; spawn grant remains pending.");
            }
            return false;
        }

        ItemData flashlight = ItemDataLoader.LoadItem(FlashlightController.ItemId);
        if (flashlight == null)
        {
            pendingSpawnFlashlightGrant = true;
            if (!pendingSpawnFlashlightWarningLogged)
            {
                pendingSpawnFlashlightWarningLogged = true;
                Debug.LogError("[FLASHLIGHT] Missing Resources/Items/Flashlight; spawn grant remains pending.");
            }
            return false;
        }

        ApplyFreshSpawnFlashlightLocal(flashlight, targetSlot);
        if (!HasInputAuthority)
            RPC_ApplyFreshSpawnFlashlight(targetSlot, flashlight.name);
        return true;
    }

    private void ApplyFreshSpawnFlashlightLocal(ItemData flashlight, int targetSlot)
    {
        EnsureStableSlotStorage();
        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlot slot = slots[i];
            if (slot == null || !IsFlashlight(slot.item)) continue;
            slot.item = null;
            slot.amount = 0;
            slot.battery01 = 0f;
        }

        if (targetSlot < 0 || targetSlot >= maxSlots || targetSlot >= slots.Count) return;
        if (slots[targetSlot] == null) slots[targetSlot] = new InventorySlot(flashlight, 1, 1f);
        else
        {
            slots[targetSlot].item = flashlight;
            slots[targetSlot].amount = 1;
            slots[targetSlot].battery01 = 1f;
        }
        GetComponent<FlashlightController>()?.AuthorityClearFlashlightState();
        UpdateUI();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_ApplyFreshSpawnFlashlight(int targetSlot, NetworkString<_64> flashlightId)
    {
        ItemData flashlight = ItemDataLoader.LoadItem(flashlightId.ToString());
        if (flashlight == null || targetSlot < 0 || targetSlot >= maxSlots) return;
        ApplyFreshSpawnFlashlightLocal(flashlight, targetSlot);
    }

    public override void Render()
    {
        // Late-join/input-authority clients receive the Networked values after
        // Spawned, so retry here until their starting gear is applied.
        ApplyReplicatedBackpackState();
        ApplyReplicatedStartingWeapon();

        if (HasInputAuthority && !hasPendingRadioBackpackHandoffTriggered)
        {
            MainQuestManager mainQuest = MainQuestManager.Instance;
            if (mainQuest != null && mainQuest.IsHospitalRadioRecovered &&
                !HasClaimedQuestBackpackReward(BackpackQuestRewardRules.RadioBackpackLevel) &&
                CurrentBackpackLevel < BackpackQuestRewardRules.RadioBackpackLevel)
            {
                TriggerLateOrPendingRadioBackpackRewardHandoff();
            }
        }
    }

    private void InitializeReplicatedBackpackState()
    {
        if (!HasStateAuthority || !IsNetworkObjectReady) return;

        if (NetworkedBackpackLevel <= 0 && backpackLevel > 0)
        {
            NetworkedBackpackLevel = BackpackCapacityRules.ClampLevel(backpackLevel);
            NetworkedBackpackId = BackpackItemCatalog.GetOrCreate(NetworkedBackpackLevel).name;
        }
        else if (NetworkedBackpackLevel <= 0)
        {
            NetworkedBackpackLevel = 0;
            NetworkedBackpackId = string.Empty;
        }
    }

    private void ApplyReplicatedBackpackState()
    {
        if (!IsNetworkObjectReady)
        {
            SetMaxSlotsLocal(maxSlots);
            return;
        }

        int replicatedLevel = BackpackCapacityRules.ClampLevel(NetworkedBackpackLevel);
        if (replicatedLevel <= 0)
        {
            // Render runs every frame.  Avoid rebuilding the UI repeatedly
            // while the replicated baseline (level 0) remains unchanged.
            if (lastAppliedBackpackLevel != 0 || backpackLevel != 0 ||
                maxSlots != FixedTotalSlots)
            {
                lastAppliedBackpackLevel = 0;
                backpackLevel = 0;
                SetMaxSlotsLocal(FixedTotalSlots);
            }
            return;
        }

        if (lastAppliedBackpackLevel == replicatedLevel && maxSlots ==
            BackpackCapacityRules.GetTotalSlots(replicatedLevel)) return;

        backpackLevel = replicatedLevel;
        lastAppliedBackpackLevel = replicatedLevel;
        SetMaxSlotsLocal(BackpackCapacityRules.GetTotalSlots(replicatedLevel));
    }

    private bool TryGrantDifficultyStartingLoadout()
    {
        int difficulty = DifficultyRules.ActiveDifficulty;
        DifficultyRules.StarterItem[] loadout = DifficultyRules.GetStarterGearLoadout(difficulty);
        bool fullyApplied = true;
        ItemData selectedStarterWeapon = null;

        foreach (var item in loadout)
        {
            if (string.Equals(item.ItemId, DifficultyRules.RandomStarterWeaponId,
                System.StringComparison.OrdinalIgnoreCase))
            {
                if (selectedStarterWeapon == null)
                    selectedStarterWeapon = ResolveStartingWeapon();

                if (selectedStarterWeapon == null)
                {
                    fullyApplied = false;
                    continue;
                }

                bool weaponApplied = PlaceStartingWeaponInHotbar(selectedStarterWeapon);
                if (!weaponApplied)
                {
                    fullyApplied = false;
                    continue;
                }

                StartingWeaponId = selectedStarterWeapon.name;
                HasStartingWeapon = true;
                hasAppliedStartingWeaponLocally = true;

                if (!AddStartingWeaponMagazine(selectedStarterWeapon))
                    fullyApplied = false;

                HostModeSpawner spawner = HostModeSpawner.Instance;
                if (spawner != null) spawner.CacheStartingWeapon(Object.InputAuthority, selectedStarterWeapon);
                continue;
            }

            ItemData data = ItemDataLoader.LoadItem(item.ItemId);
            if (data == null)
            {
                Debug.LogWarning($"[STARTING LOADOUT] Item '{item.ItemId}' not found in Resources/Items.");
                fullyApplied = false;
                continue;
            }

            if (item.PreferHotbar)
            {
                if (!PlaceStartingWeaponInHotbar(data))
                {
                    fullyApplied = false;
                    continue;
                }
                StartingWeaponId = data.name;
                HasStartingWeapon = true;
                hasAppliedStartingWeaponLocally = true;
                HostModeSpawner spawner = HostModeSpawner.Instance;
                if (spawner != null) spawner.CacheStartingWeapon(Object.InputAuthority, data);
            }
            else
            {
                int missingAmount = Mathf.Max(0, item.Amount - GetItemAmount(data));
                if (missingAmount > 0 && !AddItem(data, missingAmount))
                    fullyApplied = false;
            }
        }

        if (!fullyApplied) return false;

        StartingLoadoutResolved = true;
        Debug.Log($"[STARTING LOADOUT] Verified {DifficultyRules.GetDifficultyName(difficulty)} starting loadout for Player {Object.InputAuthority}.");
        return true;
    }

    private ItemData ResolveStartingWeapon()
    {
        string existingId = StartingWeaponId.ToString();
        if (HasStartingWeapon && DifficultyRules.IsStarterWeaponId(existingId))
        {
            ItemData existing = ItemDataLoader.LoadItem(existingId);
            if (existing != null && existing.category == ItemCategory.Weapon)
                return existing;
        }

        List<ItemData> availableWeapons = new List<ItemData>();
        foreach (string weaponId in DifficultyRules.GetStarterWeaponPool())
        {
            ItemData weapon = ItemDataLoader.LoadItem(weaponId);
            if (weapon != null && weapon.category == ItemCategory.Weapon)
                availableWeapons.Add(weapon);
        }

        if (availableWeapons.Count == 0)
        {
            Debug.LogWarning("[STARTING LOADOUT] No valid starter weapon exists in the configured pool.");
            return null;
        }

        ItemData selected = availableWeapons[Random.Range(0, availableWeapons.Count)];
        StartingWeaponId = selected.name;
        HasStartingWeapon = true;
        return selected;
    }

    private bool AddStartingWeaponMagazine(ItemData weapon)
    {
        if (weapon == null || weapon.category != ItemCategory.Weapon ||
            weapon.ammoTypeRequired == null || weapon.magazineCapacity <= 0)
        {
            Debug.LogWarning($"[STARTING LOADOUT] Weapon '{weapon?.name}' has no valid magazine configuration.");
            return false;
        }

        ItemData ammo = weapon.ammoTypeRequired;
        int magazineAmount = Mathf.Max(1, weapon.magazineCapacity);
        int missingAmount = Mathf.Max(0, magazineAmount - GetItemAmount(ammo));
        return missingAmount <= 0 || AddItem(ammo, missingAmount);
    }

    private void MarkTutorialLoadoutReady()
    {
        // The tutorial teaches looting before firearms. S12K is deliberately
        // placed in the kitchen cabinet with its ammunition, not in hotbar.
        HasStartingWeapon = true;
        StartingWeaponId = string.Empty;
        hasAppliedStartingWeaponLocally = true;
        StartingLoadoutResolved = true;
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

        hasAppliedStartingWeaponLocally = PlaceStartingWeaponInHotbar(selectedWeapon);
        if (!hasAppliedStartingWeaponLocally) return;
        Debug.Log($"[STARTING LOADOUT] Applied {selectedWeapon.itemName} to local hotbar.");
    }

    private bool PlaceStartingWeaponInHotbar(ItemData weapon)
    {
        if (weapon == null || weapon.category != ItemCategory.Weapon) return false;
        if (HasWeapon(weapon.name) || HasWeapon(weapon.itemName)) return true;

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
            return false;
        }

        if (slots[targetSlot] == null) slots[targetSlot] = new InventorySlot(weapon, 1);
        else
        {
            slots[targetSlot].item = weapon;
            slots[targetSlot].amount = 1;
        }

        UpdateUI();
        return true;
    }

    private int GetItemAmount(ItemData item)
    {
        if (item == null) return 0;
        int total = 0;
        for (int i = 0; i < Mathf.Min(maxSlots, slots.Count); i++)
        {
            InventorySlot slot = slots[i];
            if (slot == null || slot.item == null || slot.amount <= 0) continue;
            if (slot.item.name == item.name || slot.item.itemName == item.itemName)
                total += slot.amount;
        }
        return total;
    }

    public MilitaryRespawnSnapshot CaptureMilitaryRespawnSnapshot()
    {
        MilitaryRespawnSnapshot snapshot = new MilitaryRespawnSnapshot();
        snapshot.MaxSlots = maxSlots;
        snapshot.BackpackLevel = CurrentBackpackLevel;
        snapshot.BackpackId = CurrentBackpackLevel > 0 ? BackpackItemCatalog.GetOrCreate(CurrentBackpackLevel).name : string.Empty;
        snapshot.QuestBackpackRewardClaimMask = QuestBackpackRewardClaimMask;
        int count = Mathf.Min(MaxTotalSlots, slots.Count);
        for (int i = 0; i < count; i++)
        {
            InventorySlot slot = slots[i];
            if (slot == null || slot.item == null || slot.amount <= 0) continue;
            if (IsFlashlight(slot.item)) continue;
            snapshot.ItemIds[i] = slot.item.name;
            snapshot.Amounts[i] = slot.amount;
        }
        return snapshot;
    }

    private void ApplyMilitaryRespawnSnapshot(MilitaryRespawnSnapshot snapshot)
    {
        if (snapshot == null) return;
        int snapshotCapacity = snapshot.MaxSlots <= 0 ? FixedTotalSlots : snapshot.MaxSlots;
        int snapshotLevel = BackpackCapacityRules.GetLevelForTotalSlots(snapshotCapacity);
        backpackLevel = snapshotLevel;
        SetMaxSlotsLocal(snapshotCapacity);
        SetQuestBackpackRewardClaimMask(snapshot.QuestBackpackRewardClaimMask);
        if (HasStateAuthority && IsNetworkObjectReady)
        {
            NetworkedBackpackLevel = snapshotLevel;
            NetworkedBackpackId = string.IsNullOrWhiteSpace(snapshot.BackpackId)
                ? (snapshotLevel <= 0 ? string.Empty : BackpackItemCatalog.GetOrCreate(snapshotLevel).name)
                : snapshot.BackpackId;
        }

        EnsureStableSlotStorage();
        for (int i = 0; i < MaxTotalSlots; i++)
        {
            string itemId = i < snapshot.ItemIds.Length ? snapshot.ItemIds[i] : string.Empty;
            ItemData item = string.IsNullOrWhiteSpace(itemId) ? null : ItemDataLoader.LoadItem(itemId);
            int savedAmount = i < snapshot.Amounts.Length ? Mathf.Max(0, snapshot.Amounts[i]) : 0;
            if (slots[i] == null) slots[i] = new InventorySlot(item, savedAmount);
            else
            {
                slots[i].item = item;
                slots[i].amount = item == null ? 0 : savedAmount;
            }

            if (HasStateAuthority && !HasInputAuthority)
                RPC_ApplyMilitaryRespawnSlot(i, itemId, item == null ? 0 : savedAmount);
        }
        UpdateUI();
        if (HasStateAuthority && !HasInputAuthority) RPC_CompleteMilitaryRespawnSnapshot();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_ApplyMilitaryRespawnSlot(int index, NetworkString<_64> itemId, int amount)
    {
        if (index < 0 || index >= MaxTotalSlots) return;
        EnsureStableSlotStorage();
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
        if (HasStateAuthority && !StartingLoadoutResolved && !TutorialSession.IsActive &&
            Time.unscaledTime >= nextStartingLoadoutRetryTime)
        {
            nextStartingLoadoutRetryTime = Time.unscaledTime + 0.5f;
            TryGrantDifficultyStartingLoadout();
        }

        if (HasStateAuthority && !postRestoreEntitlementsReconciled && StartingLoadoutResolved &&
            Time.unscaledTime >= nextSpawnEntitlementRetryTime)
        {
            nextSpawnEntitlementRetryTime = Time.unscaledTime + 0.5f;
            TryReconcilePostRestoreEntitlements();
        }

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
    public bool AddItem(ItemData itemToAdd, int amountToAdd, float flashlightBattery01 = 1f)
    {
        if (itemToAdd == null || amountToAdd <= 0) return false;
        flashlightBattery01 = Mathf.Clamp(flashlightBattery01,
            FlashlightController.MinimumLootBattery01, 1f);
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
                int stackLimit = itemToAdd.isStackable ? Mathf.Max(1, itemToAdd.maxStack) : 1;
                int amountToStore = Mathf.Min(amountToAdd, stackLimit);
                slots[emptyIndex].item = itemToAdd;
                slots[emptyIndex].amount = amountToStore;
                slots[emptyIndex].battery01 = IsFlashlight(itemToAdd) ? flashlightBattery01 : 1f;
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
            if (HasStateAuthority && !HasInputAuthority)
                RPC_SyncItemToClient(itemToAdd.itemName, amountAdded, true, flashlightBattery01);
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
            // A remote client requests the upgrade on State Authority.  It
            // must not locally consume the backpack before the server has
            // validated ownership and capacity.
            if (IsNetworkObjectReady && !HasStateAuthority)
            {
                EquipBackpack(item);
                return;
            }
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
            if (!isSyncing && !HasStateAuthority)
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
        float droppedBattery01 = slot.battery01;

        slot.amount--;
        if (slot.amount <= 0)
        {
            slot.item = null;
            slot.amount = 0;
        }
        UpdateUI();

        // Client tự vứt đồ thì báo Server trừ đi
        if (!isSyncing && !HasStateAuthority)
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
            if (pickup != null)
            {
                pickup.item = itemToDrop;
                pickup.amount = 1;
                pickup.flashlightBattery01 = droppedBattery01;
            }

            Destroy(droppedGO, dropLifeTime);
        }
    }

    private void EquipFlashlightToHotbar(int inventoryIndex)
    {
        for (int i = 0; i < Mathf.Min(5, slots.Count); i++)
        {
            if (slots[i] != null && slots[i].item != null && slots[i].amount > 0) continue;
            SwapSlots(inventoryIndex, i);
            return;
        }

        Debug.Log("[FLASHLIGHT] Hotbar is full. Drag the flashlight to a Hotbar slot to equip it.");
    }

    public void SwapSlots(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= slots.Count || toIndex < 0 || toIndex >= slots.Count) return;
        if (fromIndex == toIndex) return;

        // Inventory layout must agree on the State Authority before a
        // flashlight's per-slot charge can be used in co-op.
        if (IsNetworkObjectReady && !HasStateAuthority)
        {
            RPC_RequestSwapSlots(fromIndex, toIndex);
            return;
        }

        SwapSlotsLocal(fromIndex, toIndex);
        if (IsNetworkObjectReady && HasStateAuthority && !HasInputAuthority)
            RPC_ApplySlotSwap(fromIndex, toIndex);
    }

    private void SwapSlotsLocal(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= slots.Count || toIndex < 0 || toIndex >= slots.Count) return;
        if (fromIndex == toIndex) return;

        InventorySlot temp = slots[fromIndex];
        slots[fromIndex] = slots[toIndex];
        slots[toIndex] = temp;

        UpdateUI();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestSwapSlots(int fromIndex, int toIndex)
    {
        SwapSlotsLocal(fromIndex, toIndex);
        RPC_ApplySlotSwap(fromIndex, toIndex);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_ApplySlotSwap(int fromIndex, int toIndex) => SwapSlotsLocal(fromIndex, toIndex);

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
            if (slot != null && slot.item != null && slot.item.itemName == itemToCount.itemName)
                total += slot.amount;
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

        if (pickup.item.category == ItemCategory.Backpack)
        {
            if (!CanAcceptBackpackLoot(pickup.item))
            {
                Debug.LogWarning($"[INVENTORY] Rejected pickup '{pickup.item.itemName}': player already has equal or higher backpack level.");
                return;
            }
        }

        // Item identity and quantity come only from the server-owned pickup.
        // Never trust itemName/amount supplied by Input Authority.
        if (!HasCapacityFor(pickup.item, pickup.amount))
        {
            Debug.LogWarning($"[INVENTORY] Rejected pickup '{pickup.item.itemName}': insufficient capacity.");
            return;
        }

        if (AddItem(pickup.item, pickup.amount, pickup.flashlightBattery01))
            Runner.Despawn(itemNetObj);
    }

    private bool HasCapacityFor(ItemData item, int amount)
    {
        if (item == null || amount <= 0) return false;

        int capacity = 0;
        int limit = Mathf.Min(maxSlots, slots.Count);
        int stackLimit = item.isStackable ? Mathf.Max(1, item.maxStack) : 1;
        for (int i = 0; i < limit; i++)
        {
            InventorySlot slot = slots[i];
            if (slot == null || slot.item == null || slot.amount <= 0)
            {
                capacity += stackLimit;
            }
            else if (item.isStackable && slot.item.itemName == item.itemName)
            {
                capacity += Mathf.Max(0, stackLimit - slot.amount);
            }

            if (capacity >= amount) return true;
        }

        return false;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_SyncItemToClient(string itemName, int amount, bool isAdding, float flashlightBattery01 = 1f)
    {
        ItemData data = ItemDataLoader.LoadItem(itemName);
        if (data != null)
        {
            isSyncing = true; // Bật cờ để Client không gọi ngược lại lên Server gây lặp vô hạn
            if (isAdding) AddItem(data, amount, flashlightBattery01);
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
