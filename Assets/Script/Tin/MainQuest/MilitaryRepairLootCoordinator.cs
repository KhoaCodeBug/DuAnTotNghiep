using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// State-authority owner for the five authored Route B loot containers.
/// Failed setup attempts are rolled back and retried; partial loot is never
/// exposed as a playable match state.
/// </summary>
public sealed class MilitaryRepairLootCoordinator : MonoBehaviour
{
    private const string PrefabResourcePath = "NetworkPrefabs/MilitaryRepairLootContainer";
    private const float RetryDelaySeconds = 2f;

    private readonly List<MilitaryRepairLootMarker> markers = new();
    private readonly List<NetworkObject> spawnedObjects = new();
    private MilitaryBaseQuestManager manager;
    private NetworkObject prefab;
    private bool ready;
    private float nextRetryAt;

    public bool IsReady => ready;
    public int SpawnedContainerCount => spawnedObjects.Count;

    public void Configure(MilitaryBaseQuestManager owner)
    {
        manager = owner;
        prefab = Resources.Load<NetworkObject>(PrefabResourcePath);
    }

    private void Update()
    {
        if (ready || manager == null || !manager.IsNetworkReady || !manager.HasStateAuthority ||
            manager.CurrentPhase != MilitaryBaseQuestManager.Phase.SiegeAndRepair ||
            Time.unscaledTime < nextRetryAt) return;

        nextRetryAt = Time.unscaledTime + RetryDelaySeconds;
        AuthorityTrySetup();
    }

    public bool AuthorityTrySetup()
    {
        if (ready) return true;
        if (manager == null || !manager.IsNetworkReady || !manager.HasStateAuthority ||
            manager.CurrentPhase != MilitaryBaseQuestManager.Phase.SiegeAndRepair) return false;

        if (prefab == null)
        {
            Debug.LogError($"[MILITARY LOOT] Missing Resources/{PrefabResourcePath}.prefab; retry scheduled.");
            return false;
        }

        MilitaryRepairLootMarker.GetOrderedMarkers(markers);
        if (!ValidateMarkers()) return false;

        int seed = unchecked((int)(manager.Runner.Tick.Raw * 397L ^ manager.Object.Id.Raw));
        MilitaryRepairLootRules.ContainerManifest[] manifest = MilitaryRepairLootRules.BuildManifest(seed);
        if (!MilitaryRepairLootRules.ContainsCompleteRequiredSet(manifest))
        {
            Debug.LogError("[MILITARY LOOT] Generated manifest is incomplete; retry scheduled.");
            return false;
        }

        RollbackPartialSetup();
        for (int i = 0; i < markers.Count; i++)
        {
            NetworkObject spawned = manager.Runner.Spawn(prefab, markers[i].transform.position,
                markers[i].transform.rotation);
            LootContainer container = spawned != null ? spawned.GetComponent<LootContainer>() : null;
            if (spawned == null || container == null || !container.IsMilitaryRepairLootContainer ||
                !ConfigureContainer(container, manifest[i]))
            {
                Debug.LogError($"[MILITARY LOOT] Setup failed at authored marker {markers[i].StableId}; rolling back.");
                if (spawned != null && spawned.IsValid) spawnedObjects.Add(spawned);
                RollbackPartialSetup();
                return false;
            }

            spawned.gameObject.name = $"Military Repair Loot {markers[i].StableId}";
            spawnedObjects.Add(spawned);
        }

        ready = spawnedObjects.Count == MilitaryRepairLootRules.RequiredContainerCount;
        if (ready)
            Debug.Log("[MILITARY LOOT] Five authored containers are ready with all repair items and weapon/ammo bonuses.");
        return ready;
    }

    private bool ValidateMarkers()
    {
        if (markers.Count != MilitaryRepairLootRules.RequiredContainerCount)
        {
            Debug.LogError($"[MILITARY LOOT] Expected 5 authored markers, found {markers.Count}; retry scheduled.");
            return false;
        }

        var ids = new HashSet<int>();
        for (int i = 0; i < markers.Count; i++)
        {
            MilitaryRepairLootMarker marker = markers[i];
            if (marker.StableId < 1 || !ids.Add(marker.StableId))
            {
                Debug.LogError("[MILITARY LOOT] Marker IDs must be positive and unique.");
                return false;
            }
        }
        return true;
    }

    private static bool ConfigureContainer(LootContainer container,
        MilitaryRepairLootRules.ContainerManifest entry)
    {
        ItemData repairItem = PoliceCarItemCatalog.GetOrCreate(entry.RequiredRepairItem);
        ItemData weapon = ItemDataLoader.LoadItem(entry.BonusWeaponId);
        ItemData ammo = ItemDataLoader.LoadItem(entry.BonusAmmoId);
        if (repairItem == null || weapon == null || ammo == null) return false;

        container.AuthorityClearContents();
        return container.AuthorityAddConfiguredItem(repairItem, 1) &&
               container.AuthorityAddConfiguredItem(weapon, 1) &&
               container.AuthorityAddConfiguredItem(ammo, entry.BonusAmmoAmount);
    }

    private void RollbackPartialSetup()
    {
        if (manager == null || manager.Runner == null)
        {
            spawnedObjects.Clear();
            return;
        }

        for (int i = 0; i < spawnedObjects.Count; i++)
        {
            NetworkObject spawned = spawnedObjects[i];
            if (spawned != null && spawned.IsValid) manager.Runner.Despawn(spawned);
        }
        spawnedObjects.Clear();
        ready = false;
    }

    private void OnDestroy()
    {
        if (manager != null && manager.HasStateAuthority) RollbackPartialSetup();
    }
}
