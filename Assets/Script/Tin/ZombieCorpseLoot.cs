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
    [Range(0f, 100f)] public float waterWeight = 35f;
    [Range(0f, 100f)] public float bandageWeight = 30f;
    [Range(0f, 100f)] public float medicineWeight = 20f;
    [Range(0f, 100f)] public float ammoWeight = 15f;

    public int RollKind()
    {
        float total = Mathf.Max(0f, waterWeight) + Mathf.Max(0f, bandageWeight) +
                      Mathf.Max(0f, medicineWeight) + Mathf.Max(0f, ammoWeight);
        if (total <= 0f) return 0;

        float roll = UnityEngine.Random.value * total;
        if ((roll -= Mathf.Max(0f, waterWeight)) < 0f) return 1;
        if ((roll -= Mathf.Max(0f, bandageWeight)) < 0f) return 2;
        if ((roll -= Mathf.Max(0f, medicineWeight)) < 0f) return 3;
        return 4;
    }

    public static ItemData LoadItem(int kind)
    {
        return kind switch
        {
            1 => ItemDataLoader.LoadItem("Water"),
            2 => ItemDataLoader.LoadItem("Bandage"),
            3 => ItemDataLoader.LoadItem("PainKiller"),
            4 => ItemDataLoader.LoadItem("Ammo762"),
            _ => null
        };
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
            RPC_ShowSearchResult(requestingPlayer, (int)SearchResult.AlreadySearched, string.Empty, true);
            return;
        }

        if (!TryResolvePlayer(requestingPlayer, out PlayerMovement player, out InventorySystem inventory))
        {
            RPC_ShowSearchResult(requestingPlayer, (int)SearchResult.PlayerMissing, string.Empty, false);
            return;
        }

        if (Vector2.Distance(player.transform.position, transform.position) > Mathf.Max(0.1f, corpseSearchRange))
        {
            RPC_ShowSearchResult(requestingPlayer, (int)SearchResult.TooFar, string.Empty, false);
            return;
        }

        // Empty corpses are still consumed by the first completed search.
        if (LootKind == 0)
        {
            ConsumeCorpse();
            RPC_ShowSearchResult(requestingPlayer, (int)SearchResult.Empty, string.Empty, true);
            return;
        }

        if (inventory == null)
        {
            RPC_ShowSearchResult(requestingPlayer, (int)SearchResult.InventoryMissing, string.Empty, false);
            return;
        }

        ItemData item = ZombieCorpseLootTable.LoadItem(LootKind);
        if (item == null)
        {
            RPC_ShowSearchResult(requestingPlayer, (int)SearchResult.InvalidLoot, string.Empty, false);
            return;
        }

        if (!CanFitOne(inventory, item) || !inventory.AddItem(item, 1))
        {
            RPC_ShowSearchResult(requestingPlayer, (int)SearchResult.InventoryFull, item.name, false);
            return;
        }

        ConsumeCorpse();
        RPC_ShowSearchResult(requestingPlayer, (int)SearchResult.Granted, item.name, true);
        Debug.Log($"[CORPSE LOOT] Granted 1x {item.itemName} to {requestingPlayer}.");
    }

    private void ConsumeCorpse()
    {
        HasCorpseBeenSearched = true;
        CorpseDespawnTimer = TickTimer.CreateFromSeconds(Runner, searchedCorpseLifetime);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowSearchResult(
        PlayerRef recipient,
        int resultValue,
        string itemId,
        NetworkBool corpseWasSearched)
    {
        if (corpseWasSearched) locallyKnownSearched = true;
        if (Runner == null || Runner.LocalPlayer != recipient) return;

        isAwaitingSearchResult = false;
        if (!corpseWasSearched) locallyKnownSearched = false;

        string message = BuildLocalResultMessage((SearchResult)resultValue, itemId);
        AutoChatManager.Instance?.AddMessage(GameLocalization.Get("corpse.title"), message);
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

    private static bool CanFitOne(InventorySystem inventory, ItemData item)
    {
        if (inventory == null || item == null) return false;

        int slotCount = Mathf.Min(inventory.maxSlots, inventory.slots.Count);
        for (int i = 0; i < slotCount; i++)
        {
            InventorySlot slot = inventory.slots[i];
            if (slot == null || slot.item == null || slot.amount <= 0) return true;
            if (item.isStackable && slot.item.itemName == item.itemName && slot.amount < Mathf.Max(1, item.maxStack))
                return true;
        }

        return false;
    }

    private static string BuildLocalResultMessage(SearchResult result, string itemId)
    {
        switch (result)
        {
            case SearchResult.Granted:
                ItemData item = ItemDataLoader.LoadItem(itemId);
                string displayName = item != null ? GameLocalization.TranslateLiteral(item.itemName) : itemId;
                return string.Format(GameLocalization.Get("corpse.found"), displayName);
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
