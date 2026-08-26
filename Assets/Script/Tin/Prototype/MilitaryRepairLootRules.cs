using System;
using System.Collections.Generic;

/// <summary>
/// Pure rules for the Route B repair-loot set. Runtime placement stays on
/// State Authority; this class only builds a deterministic, testable manifest.
/// </summary>
public static class MilitaryRepairLootRules
{
    public const int RequiredContainerCount = 5;
    public const int RegularAkAmmoAmount = 90;
    public const int RegularShotgunAmmoAmount = 15;
    public const int VipWeaponCopiesPerType = 2;
    public const int VipAkAmmoAmount = 360;
    public const int VipShotgunAmmoAmount = 60;

    public readonly struct ContainerManifest
    {
        public readonly ArrivalCarItemKind RequiredRepairItem;
        public readonly string BonusWeaponId;
        public readonly string BonusAmmoId;
        public readonly int BonusAmmoAmount;

        public ContainerManifest(ArrivalCarItemKind requiredRepairItem, string bonusWeaponId,
            string bonusAmmoId, int bonusAmmoAmount)
        {
            RequiredRepairItem = requiredRepairItem;
            BonusWeaponId = bonusWeaponId;
            BonusAmmoId = bonusAmmoId;
            BonusAmmoAmount = bonusAmmoAmount;
        }
    }

    private static readonly ArrivalCarItemKind[] RequiredItems =
    {
        ArrivalCarItemKind.Toolbox,
        ArrivalCarItemKind.Hammer,
        ArrivalCarItemKind.FuelCan,
        ArrivalCarItemKind.Battery,
        ArrivalCarItemKind.Tire
    };

    private static readonly string[] WeaponIds = { "AK47", "S12K" };

    public static ArrivalCarItemKind[] GetRequiredItems()
    {
        ArrivalCarItemKind[] copy = new ArrivalCarItemKind[RequiredItems.Length];
        Array.Copy(RequiredItems, copy, RequiredItems.Length);
        return copy;
    }

    public static ContainerManifest[] BuildManifest(int seed)
    {
        var random = new Random(seed);
        ArrivalCarItemKind[] shuffledItems = GetRequiredItems();
        Shuffle(shuffledItems, random);

        var result = new ContainerManifest[RequiredContainerCount];
        for (int i = 0; i < result.Length; i++)
        {
            string weaponId = WeaponIds[random.Next(WeaponIds.Length)];
            bool shotgun = string.Equals(weaponId, "S12K", StringComparison.Ordinal);
            string ammoId = shotgun ? "Ammo12Gauge" : "Ammo762";
            int ammoAmount = shotgun ? RegularShotgunAmmoAmount : RegularAkAmmoAmount;
            result[i] = new ContainerManifest(shuffledItems[i], weaponId, ammoId, ammoAmount);
        }
        return result;
    }

    public static bool ContainsCompleteRequiredSet(IReadOnlyList<ContainerManifest> manifest)
    {
        if (manifest == null || manifest.Count != RequiredContainerCount) return false;
        var found = new HashSet<ArrivalCarItemKind>();
        for (int i = 0; i < manifest.Count; i++) found.Add(manifest[i].RequiredRepairItem);
        return found.SetEquals(RequiredItems);
    }

    public static bool IsApprovedBonusId(string itemId) =>
        string.Equals(itemId, "AK47", StringComparison.Ordinal) ||
        string.Equals(itemId, "S12K", StringComparison.Ordinal) ||
        string.Equals(itemId, "Ammo762", StringComparison.Ordinal) ||
        string.Equals(itemId, "Ammo12Gauge", StringComparison.Ordinal);

    private static void Shuffle<T>(T[] values, Random random)
    {
        for (int i = values.Length - 1; i > 0; i--)
        {
            int swapIndex = random.Next(i + 1);
            (values[i], values[swapIndex]) = (values[swapIndex], values[i]);
        }
    }
}
