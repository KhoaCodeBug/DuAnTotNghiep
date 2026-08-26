using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class MilitaryRepairLootRulesTests
{
    private const string PrefabPath =
        "Assets/Resources/NetworkPrefabs/MilitaryRepairLootContainer.prefab";

    [Test]
    public void EverySeedContainsExactlyTheFiveRequiredPoliceRepairItems()
    {
        for (int seed = -100; seed <= 100; seed++)
        {
            MilitaryRepairLootRules.ContainerManifest[] manifest =
                MilitaryRepairLootRules.BuildManifest(seed);
            Assert.That(manifest.Length, Is.EqualTo(5));
            Assert.That(MilitaryRepairLootRules.ContainsCompleteRequiredSet(manifest), Is.True,
                $"Seed {seed} can soft-lock Route B.");

            var unique = new HashSet<ArrivalCarItemKind>();
            for (int i = 0; i < manifest.Length; i++) unique.Add(manifest[i].RequiredRepairItem);
            Assert.That(unique.Count, Is.EqualTo(5));
        }
    }

    [Test]
    public void BonusesUseOnlyApprovedRealWeaponAndAmmoPairs()
    {
        for (int seed = 0; seed < 100; seed++)
        {
            MilitaryRepairLootRules.ContainerManifest[] manifest =
                MilitaryRepairLootRules.BuildManifest(seed);
            for (int i = 0; i < manifest.Length; i++)
            {
                MilitaryRepairLootRules.ContainerManifest entry = manifest[i];
                Assert.That(MilitaryRepairLootRules.IsApprovedBonusId(entry.BonusWeaponId), Is.True);
                Assert.That(MilitaryRepairLootRules.IsApprovedBonusId(entry.BonusAmmoId), Is.True);
                Assert.That(entry.BonusAmmoAmount, Is.GreaterThan(0));
                Assert.That(entry.BonusWeaponId == "S12K" ? entry.BonusAmmoId : "Ammo762",
                    Is.EqualTo(entry.BonusAmmoId));
                System.Type loaderType = System.Type.GetType("ItemDataLoader, Assembly-CSharp");
                Assert.That(loaderType, Is.Not.Null);
                System.Reflection.MethodInfo loadItem = loaderType.GetMethod("LoadItem");
                Assert.That(loadItem?.Invoke(null, new object[] { entry.BonusWeaponId }), Is.Not.Null);
                Assert.That(loadItem?.Invoke(null, new object[] { entry.BonusAmmoId }), Is.Not.Null);
            }
        }
    }

    [Test]
    public void ManifestIsDeterministicForTheAuthoritySeed()
    {
        MilitaryRepairLootRules.ContainerManifest[] first = MilitaryRepairLootRules.BuildManifest(20260826);
        MilitaryRepairLootRules.ContainerManifest[] second = MilitaryRepairLootRules.BuildManifest(20260826);
        for (int i = 0; i < first.Length; i++)
        {
            Assert.That(second[i].RequiredRepairItem, Is.EqualTo(first[i].RequiredRepairItem));
            Assert.That(second[i].BonusWeaponId, Is.EqualTo(first[i].BonusWeaponId));
            Assert.That(second[i].BonusAmmoId, Is.EqualTo(first[i].BonusAmmoId));
            Assert.That(second[i].BonusAmmoAmount, Is.EqualTo(first[i].BonusAmmoAmount));
        }
    }

    [Test]
    public void OfficialPrefabIsARegisteredRouteBOnlyFusionPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null);
        System.Type networkObjectType = System.Type.GetType("Fusion.NetworkObject, Fusion.Runtime");
        Assert.That(networkObjectType, Is.Not.Null);
        Assert.That(prefab.GetComponent(networkObjectType), Is.Not.Null);
        System.Type containerType = System.Type.GetType("LootContainer, Assembly-CSharp");
        Assert.That(containerType, Is.Not.Null);
        Component container = prefab.GetComponent(containerType);
        Assert.That(container, Is.Not.Null);
        Assert.That((bool)containerType.GetProperty("IsMilitaryRepairLootContainer")?.GetValue(container), Is.True);
        Color highlight = (Color)containerType.GetField("highlightColor")?.GetValue(container);
        Assert.That(highlight.r, Is.EqualTo(1f).Within(0.001f));
        Assert.That(highlight.g, Is.LessThan(0.2f));
        Assert.That(AssetDatabase.GetLabels(prefab), Does.Contain("FusionPrefab"));
    }
}
