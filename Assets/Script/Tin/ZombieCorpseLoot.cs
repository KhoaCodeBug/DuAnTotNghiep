using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

public interface IZombieCorpseSearchTarget
{
    bool IsCorpseSearchAvailable { get; }
    float CorpseSearchRange { get; }
    void RequestSearchCorpse();
}

[Serializable]
public sealed class ZombieCorpseLootTable
{
    [Header("Tỉ lệ vật phẩm khi xác có loot")]
    [Range(0f, 100f)] public float waterWeight = 25f;
    [Range(0f, 100f)] public float bandageWeight = 45f;
    [Range(0f, 100f)] public float medicineWeight = 15f;
    [Range(0f, 100f)] public float ammoWeight = 10f;
    [Range(0f, 100f)] public float ammo12GaugeWeight = 5f;

    public int RollKind()
    {
        float total = Mathf.Max(0f, waterWeight) + Mathf.Max(0f, bandageWeight) +
                      Mathf.Max(0f, medicineWeight) + Mathf.Max(0f, ammoWeight) +
                      Mathf.Max(0f, ammo12GaugeWeight);
        if (total <= 0f) return 0;

        float roll = UnityEngine.Random.value * total;
        if ((roll -= Mathf.Max(0f, waterWeight)) < 0f) return 1;
        if ((roll -= Mathf.Max(0f, bandageWeight)) < 0f) return 2;
        if ((roll -= Mathf.Max(0f, medicineWeight)) < 0f) return 3;
        if ((roll -= Mathf.Max(0f, ammoWeight)) < 0f) return 4;
        return 5;
    }

    public static ItemData LoadItem(int kind)
    {
        return kind switch
        {
            1 => ItemDataLoader.LoadItem("Water"),
            2 => ItemDataLoader.LoadItem("Bandage"),
            3 => ItemDataLoader.LoadItem("PainKiller"),
            4 => ItemDataLoader.LoadItem("Ammo762"),
            5 => ItemDataLoader.LoadItem("Ammo12Gauge"),
            _ => null
        };
    }
}

/// <summary>Deterministic math used by the loot audit and automated tests.</summary>
public static class LootProbabilityRules
{
    public static float GetNoLootProbabilityPercent(float lootChancePercent, int attempts)
    {
        if (attempts <= 0) return 100f;
        float chance = Mathf.Clamp01(lootChancePercent / 100f);
        return Mathf.Pow(1f - chance, attempts) * 100f;
    }

    public static float GetAtLeastOneLootProbabilityPercent(float lootChancePercent, int attempts)
    {
        return 100f - GetNoLootProbabilityPercent(lootChancePercent, attempts);
    }

    public static float GetIndependentContainerAnyItemProbabilityPercent(
        IReadOnlyList<float> dropChancesPercent, float lootMultiplier = 1f)
    {
        if (dropChancesPercent == null || dropChancesPercent.Count == 0) return 0f;
        float noItem = 1f;
        foreach (float chance in dropChancesPercent)
            noItem *= 1f - Mathf.Clamp01(Mathf.Max(0f, chance) * lootMultiplier / 100f);
        return (1f - noItem) * 100f;
    }
}

/// <summary>Shared quantity policy for ordinary/random loot only.</summary>
public static class LootQuantityRules
{
    public const int Ammo762Minimum = 15;
    public const int Ammo762Maximum = 30;
    public const int Ammo12GaugeFixedAmount = 5;

    // Compatibility aliases for existing balance/audit callers.  All random
    // rifle-ammo rolls now use the expanded 15-30 range.
    public const int AmmoMinimum = Ammo762Minimum;
    public const int AmmoMaximum = Ammo762Maximum;

    public static bool IsAmmo(ItemData item) => item != null && item.category == ItemCategory.Ammunition;

    public static bool IsAmmo762(ItemData item) => IsNamedItem(item, "Ammo762", "7.62mm Ammo");

    public static bool IsAmmo12Gauge(ItemData item) => IsNamedItem(item, "Ammo12Gauge", "12 Gauge Ammo");

    public static int RollRandomAmount(ItemData item, int configuredMinimum, int configuredMaximum)
    {
        if (IsAmmo762(item)) return UnityEngine.Random.Range(Ammo762Minimum, Ammo762Maximum + 1);
        if (IsAmmo12Gauge(item))
        {
            int authoredMinimum = Mathf.Max(1, Mathf.Min(configuredMinimum, configuredMaximum));
            int authoredMaximum = Mathf.Max(authoredMinimum, configuredMaximum);
            // TutorialKitchenLootTable intentionally authors a 12-round
            // guaranteed stack.  Preserve explicit fixed authoring while the
            // ordinary default table uses the canonical fixed amount of five.
            if (authoredMinimum == authoredMaximum) return authoredMinimum;
            return Ammo12GaugeFixedAmount;
        }

        int minimum = Mathf.Max(1, Mathf.Min(configuredMinimum, configuredMaximum));
        int maximum = Mathf.Max(minimum, configuredMaximum);
        return UnityEngine.Random.Range(minimum, maximum + 1);
    }

    public static int GetCorpseAmount(ItemData item)
    {
        if (IsAmmo762(item)) return UnityEngine.Random.Range(Ammo762Minimum, Ammo762Maximum + 1);
        if (IsAmmo12Gauge(item)) return Ammo12GaugeFixedAmount;
        return 1;
    }

    private static bool IsNamedItem(ItemData item, string assetName, string displayName)
    {
        if (!IsAmmo(item)) return false;
        return string.Equals(item.name, assetName, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(item.itemName, assetName, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(item.itemName, displayName, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>Weighted backpack tiers used by ordinary loot containers.</summary>
public static class BackpackLootRules
{
    private static readonly float[] TierWeights = { 50f, 30f, 15f, 4f, 1f };

    public static int RollTier()
    {
        float total = 0f;
        foreach (float weight in TierWeights) total += Mathf.Max(0f, weight);
        float roll = UnityEngine.Random.value * total;
        for (int i = 0; i < TierWeights.Length; i++)
        {
            roll -= Mathf.Max(0f, TierWeights[i]);
            if (roll < 0f) return i + 1;
        }
        return TierWeights.Length;
    }

    public static float GetTierWeightPercent(int level)
    {
        if (level < 1 || level > TierWeights.Length) return 0f;
        float total = 0f;
        foreach (float weight in TierWeights) total += Mathf.Max(0f, weight);
        return Mathf.Max(0f, TierWeights[level - 1]) / total * 100f;
    }
}

/// <summary>
/// Authoritative, one-use corpse loot shared by every zombie implementation.
/// State Authority rolls the corpse contents on death and grants any item only
/// through the requesting player's authoritative InventorySystem.
/// </summary>
public sealed class ZombieCorpseLoot : NetworkBehaviour, IZombieCorpseSearchTarget
{
    private enum SearchResult
    {
        Granted = 1,
        Empty = 2,
        AlreadySearched = 3,
        TooFar = 4,
        InventoryMissing = 5,
        InventoryFull = 6,
        InvalidLoot = 7,
        PlayerMissing = 8
    }

    private static readonly HashSet<ZombieCorpseLoot> ActiveInstances = new HashSet<ZombieCorpseLoot>();
    public static IReadOnlyCollection<ZombieCorpseLoot> ActiveCorpses => ActiveInstances;

    [Header("Tương tác lục xác")]
    [SerializeField, Min(0.1f)] private float corpseSearchRange = 0.5f;
    [SerializeField, Min(0.1f)] private float searchDuration = 2f;

    [Header("Cơ hội xác có vật phẩm")]
    [SerializeField, Range(0f, 100f)] private float easyLootChance = 45f;
    [SerializeField, Range(0f, 100f)] private float normalLootChance = 30f;
    [SerializeField, Range(0f, 100f)] private float hardcoreLootChance = 12f;
    [SerializeField] private ZombieCorpseLootTable lootTable = new ZombieCorpseLootTable();

    [Header("Dọn xác")]
    [SerializeField, Min(10f)] private float unsearchedCorpseLifetime = 120f;
    [SerializeField, Min(1f)] private float searchedCorpseLifetime = 20f;

    [Networked] private NetworkBool IsCorpse { get; set; }
    [Networked] private NetworkBool HasCorpseBeenSearched { get; set; }
    [Networked] private int LootKind { get; set; }
    [Networked] private int LootAmount { get; set; }
    [Networked] private TickTimer CorpseDespawnTimer { get; set; }

    private bool isLocalSearchInProgress;
    private bool isAwaitingSearchResult;
    private bool locallyKnownSearched;

    public bool IsCorpseSearchAvailable => IsCorpse && !HasCorpseBeenSearched &&
        !locallyKnownSearched && !isLocalSearchInProgress && !isAwaitingSearchResult;
    public float CorpseSearchRange => corpseSearchRange;

    private void OnEnable() => ActiveInstances.Add(this);
    private void OnDisable() => ActiveInstances.Remove(this);

    public override void Spawned()
    {
        isLocalSearchInProgress = false;
        isAwaitingSearchResult = false;
        locallyKnownSearched = false;

        if (!HasStateAuthority) return;
        IsCorpse = false;
        HasCorpseBeenSearched = false;
        LootKind = 0;
        LootAmount = 0;
        CorpseDespawnTimer = TickTimer.None;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || !IsCorpse || !CorpseDespawnTimer.Expired(Runner)) return;
        if (Object != null && Object.IsValid) Runner.Despawn(Object);
    }

    public void MarkAsCorpse()
    {
        if (!HasStateAuthority || IsCorpse) return;

        IsCorpse = true;
        HasCorpseBeenSearched = false;
        LootKind = RollCorpseLoot();
        ItemData rolledItem = ZombieCorpseLootTable.LoadItem(LootKind);
        LootAmount = rolledItem == null ? 0 : LootQuantityRules.GetCorpseAmount(rolledItem);
        CorpseDespawnTimer = TickTimer.CreateFromSeconds(Runner, unsearchedCorpseLifetime);
    }

    public void RequestSearchCorpse()
    {
        if (isLocalSearchInProgress || isAwaitingSearchResult || !IsCorpseSearchAvailable) return;

        AutoUIManager ui = AutoUIManager.Instance;
        if (ui == null || !ui.StartTimedGameplayAction(
                GameLocalization.Get("corpse.searching"), searchDuration,
                SubmitSearchRequest, CancelLocalSearch))
            return;

        isLocalSearchInProgress = true;
    }

    private void CancelLocalSearch()
    {
        isLocalSearchInProgress = false;
    }

    private void SubmitSearchRequest()
    {
        isLocalSearchInProgress = false;
        if (!IsCorpseSearchAvailable || Runner == null) return;

        isAwaitingSearchResult = true;
        RPC_RequestSearchCorpse(Runner.LocalPlayer);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestSearchCorpse(PlayerRef requestingPlayer, RpcInfo info = default)
    {
        if (requestingPlayer == PlayerRef.None ||
            (info.Source != PlayerRef.None && info.Source != requestingPlayer))
        {
            Debug.LogWarning($"[CORPSE LOOT] Rejected requester: source={info.Source}, requested={requestingPlayer}.");
            return;
        }

        if (!IsCorpse) return;
        if (HasCorpseBeenSearched)
        {
            RPC_ShowSearchResult(requestingPlayer, (int)SearchResult.AlreadySearched, string.Empty, 0);
            return;
        }

        if (!TryResolvePlayer(requestingPlayer, out PlayerMovement player, out InventorySystem inventory))
        {
            RPC_ShowSearchResult(requestingPlayer, (int)SearchResult.PlayerMissing, string.Empty, 0);
            return;
        }

        if (Vector2.Distance(player.transform.position, transform.position) > Mathf.Max(0.1f, corpseSearchRange))
        {
            RPC_ShowSearchResult(requestingPlayer, (int)SearchResult.TooFar, string.Empty, 0);
            return;
        }

        // Empty corpses are still consumed by the first completed search.
        if (LootKind == 0 || LootAmount <= 0)
        {
            ConsumeCorpse();
            RPC_ShowSearchResult(requestingPlayer, (int)SearchResult.Empty, string.Empty, 0);
            return;
        }

        if (inventory == null)
        {
            RPC_ShowSearchResult(requestingPlayer, (int)SearchResult.InventoryMissing, string.Empty, 0);
            return;
        }

        ItemData item = ZombieCorpseLootTable.LoadItem(LootKind);
        if (item == null)
        {
            RPC_ShowSearchResult(requestingPlayer, (int)SearchResult.InvalidLoot, string.Empty, 0);
            return;
        }

        int amount = Mathf.Max(1, LootAmount);
        if (!CanFitAmount(inventory, item, amount) || !inventory.AddItem(item, amount))
        {
            RPC_ShowSearchResult(requestingPlayer, (int)SearchResult.InventoryFull, item.name, amount);
            return;
        }

        ConsumeCorpse();
        RPC_ShowSearchResult(requestingPlayer, (int)SearchResult.Granted, item.name, amount);
        Debug.Log($"[CORPSE LOOT] Granted {amount}x {item.itemName} to {requestingPlayer}.");
    }

    private void ConsumeCorpse()
    {
        HasCorpseBeenSearched = true;
        CorpseDespawnTimer = TickTimer.CreateFromSeconds(Runner, searchedCorpseLifetime);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowSearchResult(
        [RpcTarget] PlayerRef recipient,
        int resultValue,
        string itemId,
        int amount)
    {
        if (Runner == null || Runner.LocalPlayer != recipient) return;

        isAwaitingSearchResult = false;

        string message = BuildLocalResultMessage((SearchResult)resultValue, itemId, amount);
        if (!string.IsNullOrEmpty(message))
        {
            AutoChatManager.Instance?.AddSystemMessage(message);
        }
    }

    private int RollCorpseLoot()
    {
        int difficulty = DifficultyRules.ActiveDifficulty;
        float chance = difficulty switch
        {
            0 => easyLootChance,
            2 => hardcoreLootChance,
            _ => normalLootChance
        };

        return UnityEngine.Random.Range(0f, 100f) < chance && lootTable != null
            ? lootTable.RollKind()
            : 0;
    }

    public static float GetLootChancePercent(int difficulty)
    {
        return difficulty switch
        {
            0 => 45f,
            2 => 12f,
            _ => 30f
        };
    }

    public static float GetNoLootProbabilityAfterSearchesPercent(int difficulty, int attempts)
    {
        return LootProbabilityRules.GetNoLootProbabilityPercent(GetLootChancePercent(difficulty), attempts);
    }

    private static bool TryResolvePlayer(
        PlayerRef playerRef,
        out PlayerMovement player,
        out InventorySystem inventory)
    {
        inventory = null;
        player = null;

        if (HostModeSpawner.Instance != null &&
            HostModeSpawner.Instance.TryGetPlayerInventory(playerRef, out inventory) && inventory != null)
        {
            player = inventory.GetComponent<PlayerMovement>();
            if (player != null) return true;
        }

        foreach (PlayerMovement candidate in UnityEngine.Object.FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
        {
            if (candidate == null || candidate.Object == null || !candidate.Object.IsValid ||
                candidate.Object.InputAuthority != playerRef)
                continue;

            player = candidate;
            inventory = candidate.GetComponent<InventorySystem>();
            return true;
        }

        return false;
    }

    private static bool CanFitAmount(InventorySystem inventory, ItemData item, int amount)
    {
        if (inventory == null || item == null || amount <= 0) return false;

        int remaining = amount;
        int slotCount = Mathf.Min(inventory.maxSlots, inventory.slots.Count);
        for (int i = 0; i < slotCount; i++)
        {
            InventorySlot slot = inventory.slots[i];
            if (slot == null || slot.item == null || slot.amount <= 0)
            {
                remaining -= item.isStackable ? Mathf.Max(1, item.maxStack) : 1;
            }
            else if (item.isStackable && slot.item.itemName == item.itemName)
            {
                remaining -= Mathf.Max(0, Mathf.Max(1, item.maxStack) - slot.amount);
            }

            if (remaining <= 0) return true;
        }

        return false;
    }

    private static string BuildLocalResultMessage(SearchResult result, string itemId, int amount)
    {
        switch (result)
        {
            case SearchResult.Granted:
                ItemData item = ItemDataLoader.LoadItem(itemId);
                string displayName = item != null ? GameLocalization.TranslateLiteral(item.itemName) : itemId;
                return string.Format(GameLocalization.Get("corpse.found"), displayName, Mathf.Max(1, amount));
            case SearchResult.Empty:
                return GameLocalization.Get("corpse.empty");
            case SearchResult.AlreadySearched:
                return GameLocalization.Get("corpse.already_searched");
            case SearchResult.TooFar:
                return GameLocalization.Get("corpse.too_far");
            case SearchResult.InventoryFull:
                return GameLocalization.Get("corpse.inventory_full");
            case SearchResult.InventoryMissing:
                return GameLocalization.Get("corpse.inventory_missing");
            case SearchResult.PlayerMissing:
                return GameLocalization.Get("corpse.player_missing");
            default:
                return GameLocalization.Get("corpse.invalid_loot");
        }
    }
}
