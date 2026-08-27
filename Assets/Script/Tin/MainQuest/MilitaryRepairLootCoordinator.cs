using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// State-authority setup for the five LootQuanSu containers authored directly
/// in Main. No loot object is instantiated by code.
/// </summary>
public sealed class MilitaryRepairLootCoordinator : MonoBehaviour
{
    private const float RetryDelaySeconds = 2f;

    private readonly List<LootContainer> authoredContainers = new();
    private MilitaryBaseQuestManager manager;
    private bool ready;
    private float nextRetryAt;

    public bool IsReady => ready;
    public int SpawnedContainerCount => ready ? authoredContainers.Count : 0;

    public void Configure(MilitaryBaseQuestManager owner) => manager = owner;

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

        CacheAuthoredContainers();
        if (authoredContainers.Count != MilitaryRepairLootRules.RequiredContainerCount)
        {
            Debug.LogError($"[MILITARY LOOT] Expected 5 authored LootQuanSu containers, found " +
                           $"{authoredContainers.Count}; retry scheduled.");
            return false;
        }

        int vipCount = 0;
        for (int i = 0; i < authoredContainers.Count; i++)
        {
            LootContainer container = authoredContainers[i];
            if (!container.IsMilitaryRepairLootContainer)
            {
                Debug.LogError($"[MILITARY LOOT] '{container.name}' is not configured as Route B military loot.");
                return false;
            }
            if (container.IsMilitaryLootVip) vipCount++;
        }
        if (vipCount != 1)
        {
            Debug.LogError($"[MILITARY LOOT] Expected exactly one LootQuanSuVjp, found {vipCount}.");
            return false;
        }

        int seed = unchecked((int)(manager.Runner.Tick.Raw * 397L ^ manager.Object.Id.Raw));
        MilitaryRepairLootRules.ContainerManifest[] manifest = MilitaryRepairLootRules.BuildManifest(seed);
        if (!MilitaryRepairLootRules.ContainsCompleteRequiredSet(manifest)) return false;

        for (int i = 0; i < authoredContainers.Count; i++)
        {
            LootContainer container = authoredContainers[i];
            bool configured = container.IsMilitaryLootVip
                ? ConfigureVipContainer(container, manifest[i].RequiredRepairItem)
                : ConfigureRegularContainer(container, manifest[i]);
            if (configured) continue;

            Debug.LogError($"[MILITARY LOOT] Failed to configure authored container '{container.name}'.");
            ClearAllContainers();
            return false;
        }

        ready = true;
        Debug.Log("[MILITARY LOOT] Five authored LootQuanSu containers are ready; no runtime loot was spawned.");
        return true;
    }

    public void AuthorityResetForRetry()
    {
        if (manager == null || !manager.IsNetworkReady || !manager.HasStateAuthority) return;
        CacheAuthoredContainers();
        ClearAllContainers();
        nextRetryAt = 0f;
    }

    private void CacheAuthoredContainers()
    {
        authoredContainers.Clear();
        LootContainer[] all = FindObjectsByType<LootContainer>(FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && all[i].name.StartsWith("LootQuanSu", StringComparison.Ordinal))
                authoredContainers.Add(all[i]);

        authoredContainers.Sort((left, right) =>
        {
            int vipOrder = left.IsMilitaryLootVip.CompareTo(right.IsMilitaryLootVip);
            if (vipOrder != 0) return vipOrder;
            int yOrder = left.transform.position.y.CompareTo(right.transform.position.y);
            return yOrder != 0 ? yOrder : left.transform.position.x.CompareTo(right.transform.position.x);
        });
    }

    private static bool ConfigureRegularContainer(LootContainer container,
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

    private static bool ConfigureVipContainer(LootContainer container, ArrivalCarItemKind requiredItem)
    {
        ItemData repairItem = PoliceCarItemCatalog.GetOrCreate(requiredItem);
        ItemData ak47 = ItemDataLoader.LoadItem("AK47");
        ItemData s12k = ItemDataLoader.LoadItem("S12K");
        ItemData ammo762 = ItemDataLoader.LoadItem("Ammo762");
        ItemData ammo12 = ItemDataLoader.LoadItem("Ammo12Gauge");
        if (repairItem == null || ak47 == null || s12k == null || ammo762 == null || ammo12 == null)
            return false;

        container.AuthorityClearContents();
        return container.AuthorityAddConfiguredItem(repairItem, 1) &&
               container.AuthorityAddConfiguredItem(ak47, MilitaryRepairLootRules.VipWeaponCopiesPerType) &&
               container.AuthorityAddConfiguredItem(s12k, MilitaryRepairLootRules.VipWeaponCopiesPerType) &&
               container.AuthorityAddConfiguredItem(ammo762, MilitaryRepairLootRules.VipAkAmmoAmount) &&
               container.AuthorityAddConfiguredItem(ammo12, MilitaryRepairLootRules.VipShotgunAmmoAmount);
    }

    private void ClearAllContainers()
    {
        for (int i = 0; i < authoredContainers.Count; i++)
            if (authoredContainers[i] != null) authoredContainers[i].AuthorityClearContents();
        ready = false;
    }
}
