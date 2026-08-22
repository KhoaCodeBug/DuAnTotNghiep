using System;
using Fusion;
using UnityEngine;

/// <summary>
/// Shared contract used by all zombie implementations.  PlayerInteraction
/// discovers a corpse through this interface, while the owning NetworkBehaviour
/// remains responsible for the authoritative RPC and replicated "searched" bit.
/// </summary>
public interface IZombieCorpseSearchTarget
{
    bool IsCorpseSearchAvailable { get; }
    float CorpseSearchRange { get; }
    void RequestSearchCorpse();
}

[Serializable]
public sealed class ZombieCorpseLootTable
{
    [Header("Tỉ lệ loot xác zombie (tổng không cần bằng 100)")]
    [Range(0f, 100f)] public float waterWeight = 35f;
    [Range(0f, 100f)] public float bandageWeight = 30f;
    [Range(0f, 100f)] public float medicineWeight = 20f;
    [Range(0f, 100f)] public float ammoWeight = 15f;

    public ItemData RollItem()
    {
        float total = Mathf.Max(0f, waterWeight) + Mathf.Max(0f, bandageWeight)
            + Mathf.Max(0f, medicineWeight) + Mathf.Max(0f, ammoWeight);
        if (total <= 0f) return null;

        float roll = UnityEngine.Random.value * total;
        if ((roll -= Mathf.Max(0f, waterWeight)) < 0f) return ItemDataLoader.LoadItem("Water");
        if ((roll -= Mathf.Max(0f, bandageWeight)) < 0f) return ItemDataLoader.LoadItem("Bandage");
        if ((roll -= Mathf.Max(0f, medicineWeight)) < 0f) return ItemDataLoader.LoadItem("PainKiller");
        return ItemDataLoader.LoadItem("Ammo762");
    }
}

public sealed class ZombieCorpseLoot : NetworkBehaviour, IZombieCorpseSearchTarget
{
    [Header("Loot trên xác")]
    [SerializeField, Min(0.1f)] private float corpseSearchRange = ZombieCorpseLootService.DefaultSearchRange;
    [SerializeField, Min(0.1f)] private float searchDuration = 2f;
    [SerializeField] private ZombieCorpseLootTable corpseLootTable = new ZombieCorpseLootTable();

    [Networked] private NetworkBool IsCorpse { get; set; }
    [Networked] private NetworkBool HasCorpseBeenSearched { get; set; }

    public bool IsCorpseSearchAvailable =>
        IsCorpse && !HasCorpseBeenSearched && !locallyKnownSearched &&
        !isLocalSearchInProgress && !isAwaitingSearchResult;
    public float CorpseSearchRange => corpseSearchRange;
    private bool isLocalSearchInProgress;
    private bool isAwaitingSearchResult;
    private bool locallyKnownSearched;

    public override void Spawned()
    {
        isLocalSearchInProgress = false;
        isAwaitingSearchResult = false;
        locallyKnownSearched = false;
        if (!HasStateAuthority) return;
        IsCorpse = false;
        HasCorpseBeenSearched = false;
    }

    public void MarkAsCorpse()
    {
        if (!HasStateAuthority) return;
        // Death can be reported by more than one animation/event callback.
        // Never reopen a corpse that has already become searchable/searched.
        if (IsCorpse) return;
        IsCorpse = true;
        HasCorpseBeenSearched = false;
    }

    public void RequestSearchCorpse()
    {
        if (isLocalSearchInProgress || !IsCorpseSearchAvailable) return;

        AutoUIManager ui = AutoUIManager.Instance;
        if (ui == null || !ui.StartTimedGameplayAction(
                "TÌM KIẾM XÁC ZOMBIE", searchDuration, SubmitSearchRequest, CancelLocalSearch))
            return;

        isLocalSearchInProgress = true;
    }

    private void CancelLocalSearch()
    {
        // Cancellation must not consume the corpse. It only releases the local
        // input lock so this player may press E and start again.
        isLocalSearchInProgress = false;
    }

    private void SubmitSearchRequest()
    {
        isLocalSearchInProgress = false;
        if (!IsCorpseSearchAvailable || Runner == null) return;

        // Stop selecting this corpse locally while the authoritative result is
        // in flight. This lets a nearby unsearched corpse become the next
        // interaction target immediately instead of leaving a stale prompt.
        isAwaitingSearchResult = true;
        RPC_RequestSearchCorpse(Runner.LocalPlayer);
    }

    // A zombie NetworkObject has no player Input Authority.  Every peer may
    // request a search, but State Authority validates the requester, range,
    // inventory capacity, and one-time searched state below.
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestSearchCorpse(PlayerRef requestingPlayer, RpcInfo info = default)
    {
        // The explicit PlayerRef is required in Host mode because this corpse
        // has no player Input Authority.  RpcInfo.Source is still used to
        // reject a client trying to request loot for another player.
        if (info.Source != PlayerRef.None && info.Source != requestingPlayer)
        {
            Debug.LogWarning($"[CORPSE LOOT] Rejected spoofed request: source={info.Source}, requested={requestingPlayer}.");
            return;
        }

        if (!IsCorpse) return;
        if (HasCorpseBeenSearched)
        {
            RPC_ShowCorpseSearchResult(
                requestingPlayer, "Xác này đã được người khác lục soát.", true);
            return;
        }

        bool granted = ZombieCorpseLootService.TryAwardLoot(
            transform, requestingPlayer, corpseSearchRange, corpseLootTable, out string message);
        if (granted) HasCorpseBeenSearched = true;
        RPC_ShowCorpseSearchResult(requestingPlayer, message, granted);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowCorpseSearchResult(PlayerRef recipient, string message, bool corpseWasSearched)
    {
        // Broadcast the consumed state as well as relying on snapshot
        // replication, so every client hides this corpse's prompt immediately.
        if (corpseWasSearched) locallyKnownSearched = true;

        if (Runner == null || Runner.LocalPlayer != recipient) return;
        isAwaitingSearchResult = false;
        if (!corpseWasSearched) locallyKnownSearched = false;
        AutoChatManager.Instance?.AddMessage("TÌM XÁC", message);
    }
}

public static class ZombieCorpseLootService
{
    public const float DefaultSearchRange = 0.5f;

    public static bool TryGetPlayer(PlayerRef playerRef, out PlayerMovement player)
    {
        if (HostModeSpawner.Instance != null &&
            HostModeSpawner.Instance.TryGetPlayerInventory(playerRef, out InventorySystem cachedInventory) &&
            cachedInventory != null)
        {
            player = cachedInventory.GetComponent<PlayerMovement>();
            if (player != null) return true;
        }

        PlayerMovement[] players = UnityEngine.Object.FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerMovement candidate = players[i];
            if (candidate == null || candidate.Object == null || !candidate.Object.IsValid) continue;
            if (candidate.Object.InputAuthority != playerRef) continue;
            player = candidate;
            return true;
        }

        player = null;
        return false;
    }

    public static bool TryAwardLoot(
        Transform corpse,
        PlayerRef requester,
        float searchRange,
        ZombieCorpseLootTable lootTable,
        out string message)
    {
        if (!TryGetPlayer(requester, out PlayerMovement player))
        {
            message = "Không tìm thấy người chơi.";
            return false;
        }

        if (Vector2.Distance(player.transform.position, corpse.position) > Mathf.Max(0.1f, searchRange))
        {
            message = "Bạn phải đứng gần xác zombie để tìm kiếm.";
            return false;
        }

        InventorySystem inventory = player.GetComponent<InventorySystem>();
        if (inventory == null)
        {
            message = "Không tìm thấy túi đồ của người chơi.";
            return false;
        }

        ItemData item = lootTable != null ? lootTable.RollItem() : null;
        if (item == null)
        {
            message = "Bảng loot xác zombie chưa có vật phẩm hợp lệ.";
            return false;
        }

        if (!inventory.CanAddItem(item, 1))
        {
            message = "Túi đồ đã đầy.";
            return false;
        }

        // This is the same authoritative path used by LootContainer. AddItem
        // updates the Host's canonical copy and its existing targeted RPC then
        // updates only this InventorySystem's Input Authority client.
        if (!inventory.AddItem(item, 1))
        {
            message = "Không thể thêm vật phẩm vào túi đồ.";
            return false;
        }

        message = "Tìm thấy: " + item.itemName + ".";
        Debug.Log($"[CORPSE LOOT] Granted 1x {item.itemName} to {requester}.");
        return true;
    }
}
