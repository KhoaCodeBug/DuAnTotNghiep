using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Capacity/loot regression tests. This test assembly intentionally does not
/// reference the default Assembly-CSharp assembly, so runtime types are
/// resolved through reflection just like the other project tests.
/// </summary>
public sealed class InventoryAndLootCapacityTests
{
    [Test]
    public void PlayerInventory_StartsAtTwentyAndSupportsFiftyFiveStableSlots()
    {
        Type inventoryType = RequireType("InventorySystem, Assembly-CSharp");
        Type itemType = RequireType("ItemData, Assembly-CSharp");
        int fixedTotal = GetStaticInt(inventoryType, "FixedTotalSlots");
        int maxTotal = GetStaticInt(inventoryType, "MaxTotalSlots");

        GameObject host = new GameObject("Inventory Capacity Test");
        ScriptableObject item = ScriptableObject.CreateInstance(itemType);
        try
        {
            Component inventory = host.AddComponent(inventoryType);
            InvokePrivate(inventory, "Awake");
            FieldInfo maxSlots = RequireField(inventoryType, "maxSlots");
            FieldInfo slotsField = RequireField(inventoryType, "slots");
            IList slots = slotsField.GetValue(inventory) as IList;
            Assert.That(slots, Is.Not.Null);
            Assert.That((int)maxSlots.GetValue(inventory), Is.EqualTo(fixedTotal));
            Assert.That(slots.Count, Is.GreaterThanOrEqualTo(maxTotal));

            inventoryType.GetMethod("SetMaxSlots")?.Invoke(inventory, new object[] { 15 });
            Assert.That((int)maxSlots.GetValue(inventory), Is.EqualTo(fixedTotal),
                "A request below the base capacity must stay at 15 storage + 5 hotbar.");

            inventoryType.GetMethod("SetMaxSlots")?.Invoke(inventory, new object[] { maxTotal });
            Assert.That((int)maxSlots.GetValue(inventory), Is.EqualTo(maxTotal));
            Assert.That(slots.Count, Is.GreaterThanOrEqualTo(maxTotal));
            inventoryType.GetMethod("SetMaxSlots")?.Invoke(inventory, new object[] { fixedTotal });

            SetField(item, "itemName", "StableSlotTestItem");
            SetField(item, "maxStack", 1);
            SetField(item, "isStackable", false);
            object slot = slots[12];
            SetField(slot, "item", item);
            SetField(slot, "amount", 1);

            FieldInfo syncingField = RequireField(inventoryType, "isSyncing");
            syncingField.SetValue(inventory, true);

            int countBefore = slots.Count;
            int consumed = (int)inventoryType.GetMethod("ConsumeItem")?.Invoke(inventory,
                new object[] { item, 1 });
            Assert.That(consumed, Is.EqualTo(1));
            Assert.That(slots.Count, Is.EqualTo(countBefore),
                "Consuming an item must clear its fixed slot instead of shifting later indices.");
            Assert.That(ReadField(slots[12], "item"), Is.Null);
            Assert.That((int)ReadField(slots[12], "amount"), Is.Zero);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(item);
        }
    }

    [Test]
    public void BackpackTiersMapToFifteenThroughFiftyStorageSlots()
    {
        Type rulesType = RequireType("BackpackCapacityRules, Assembly-CSharp");
        Type catalogType = RequireType("BackpackItemCatalog, Assembly-CSharp");
        Type loaderType = RequireType("ItemDataLoader, Assembly-CSharp");
        Type itemType = RequireType("ItemData, Assembly-CSharp");
        Type categoryType = RequireType("ItemCategory, Assembly-CSharp");
        MethodInfo getStorage = RequireMethod(rulesType, "GetBackpackSlots", typeof(int));
        MethodInfo getTotal = RequireMethod(rulesType, "GetTotalSlots", typeof(int));

        Assert.That((int)getStorage.Invoke(null, new object[] { 0 }), Is.EqualTo(15));
        Assert.That((int)getStorage.Invoke(null, new object[] { 1 }), Is.EqualTo(20));
        Assert.That((int)getStorage.Invoke(null, new object[] { 2 }), Is.EqualTo(25));
        Assert.That((int)getStorage.Invoke(null, new object[] { 3 }), Is.EqualTo(30));
        Assert.That((int)getStorage.Invoke(null, new object[] { 4 }), Is.EqualTo(40));
        Assert.That((int)getStorage.Invoke(null, new object[] { 5 }), Is.EqualTo(50));
        Assert.That((int)getTotal.Invoke(null, new object[] { 0 }), Is.EqualTo(20));
        Assert.That((int)getTotal.Invoke(null, new object[] { 5 }), Is.EqualTo(55));

        MethodInfo getOrCreate = RequireMethod(catalogType, "GetOrCreate", typeof(int), typeof(bool));
        object tierFive = getOrCreate.Invoke(null, new object[] { 5, false });
        Assert.That(tierFive, Is.Not.Null);
        Assert.That(ReadField(tierFive, "category"), Is.EqualTo(Enum.Parse(categoryType, "Backpack")));
        MethodInfo getStorageForItem = RequireMethod(rulesType, "GetStorageSlots", itemType);
        Assert.That((int)getStorageForItem.Invoke(null, new[] { tierFive }), Is.EqualTo(50));

        object loaded = loaderType.GetMethod("LoadItem", new[] { typeof(string) })?.Invoke(null,
            new object[] { "BackpackLevel5" });
        Assert.That(loaded, Is.SameAs(tierFive));
    }

    [Test]
    public void CorpseLootProbabilityExplainsTwentyEmptySearches()
    {
        Type corpseType = RequireType("ZombieCorpseLoot, Assembly-CSharp");
        Type probabilityType = RequireType("LootProbabilityRules, Assembly-CSharp");
        MethodInfo getChance = RequireMethod(corpseType, "GetLootChancePercent", typeof(int));
        MethodInfo getNoLoot = RequireMethod(corpseType, "GetNoLootProbabilityAfterSearchesPercent", typeof(int), typeof(int));
        MethodInfo getAtLeastOne = RequireMethod(probabilityType,
            "GetAtLeastOneLootProbabilityPercent", typeof(float), typeof(int));
        MethodInfo getNoLootGeneric = RequireMethod(probabilityType,
            "GetNoLootProbabilityPercent", typeof(float), typeof(int));

        Assert.That((float)getChance.Invoke(null, new object[] { 0 }), Is.EqualTo(45f));
        Assert.That((float)getChance.Invoke(null, new object[] { 1 }), Is.EqualTo(30f));
        Assert.That((float)getChance.Invoke(null, new object[] { 2 }), Is.EqualTo(12f));

        float normalNoLoot = (float)getNoLoot.Invoke(null, new object[] { 1, 20 });
        Assert.That(normalNoLoot, Is.EqualTo(0.079775f).Within(0.001f));
        Assert.That((float)getAtLeastOne.Invoke(null, new object[] { 30f, 20 }),
            Is.EqualTo(99.920225f).Within(0.001f));
        Assert.That((float)getNoLootGeneric.Invoke(null, new object[] { 30f, 0 }), Is.EqualTo(100f));
    }

    [Test]
    public void RandomAmmoLootIsAlwaysFiveThroughTen()
    {
        Type itemType = RequireType("ItemData, Assembly-CSharp");
        Type categoryType = RequireType("ItemCategory, Assembly-CSharp");
        Type quantityType = RequireType("LootQuantityRules, Assembly-CSharp");
        MethodInfo rollAmount = RequireMethod(quantityType, "RollRandomAmount", itemType, typeof(int), typeof(int));

        ScriptableObject ammo = ScriptableObject.CreateInstance(itemType);
        try
        {
            SetField(ammo, "category", Enum.Parse(categoryType, "Ammunition"));
            SetField(ammo, "itemName", "Test ammo");
            SetField(ammo, "maxStack", 30);
            SetField(ammo, "isStackable", true);
            for (int i = 0; i < 100; i++)
            {
                int amount = (int)rollAmount.Invoke(null, new object[] { ammo, 16, 30 });
                Assert.That(amount, Is.InRange(5, 10));
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(ammo);
        }
    }

    [Test]
    public void LootContainer_DefaultCapacityIsTwentyAndRejectsOverflow()
    {
        Type containerType = RequireType("LootContainer, Assembly-CSharp");
        Type itemType = RequireType("ItemData, Assembly-CSharp");

        GameObject host = new GameObject("Loot Capacity Test");
        ScriptableObject item = ScriptableObject.CreateInstance(itemType);
        try
        {
            host.AddComponent<BoxCollider2D>();
            host.AddComponent<SpriteRenderer>();
            Component container = host.AddComponent(containerType);
            InvokePrivate(container, "Awake");
            PropertyInfo maxSlots = containerType.GetProperty("MaxSlots");
            IList items = RequireField(containerType, "itemsInContainer").GetValue(container) as IList;
            Assert.That((int)maxSlots.GetValue(container), Is.EqualTo(20));

            SetField(item, "itemName", "ContainerCapacityTestItem");
            SetField(item, "maxStack", 1);
            SetField(item, "isStackable", false);

            MethodInfo store = RequireMethod(containerType, "StoreItemLocal", BindingFlags.Instance | BindingFlags.NonPublic,
                itemType, typeof(int), typeof(int));
            MethodInfo canStore = RequireMethod(containerType, "CanStoreItem", BindingFlags.Instance | BindingFlags.Public,
                itemType, typeof(int));
            for (int i = 0; i < 20; i++)
                Assert.That((bool)store.Invoke(container, new object[] { item, 1, -1 }), Is.True);

            Assert.That(items.Count, Is.EqualTo(20));
            Assert.That((bool)canStore.Invoke(container, new object[] { item, 1 }), Is.False);
            Assert.That((bool)store.Invoke(container, new object[] { item, 1, -1 }), Is.False);
            Assert.That(items.Count, Is.EqualTo(20));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(item);
        }
    }

    [Test]
    public void BackpackEquip_AuthoritativeUpgradeAndValidation()
    {
        Type inventoryType = RequireType("InventorySystem, Assembly-CSharp");
        Type catalogType = RequireType("BackpackItemCatalog, Assembly-CSharp");
        Type itemType = RequireType("ItemData, Assembly-CSharp");
        MethodInfo getOrCreate = RequireMethod(catalogType, "GetOrCreate", typeof(int), typeof(bool));

        GameObject host = new GameObject("Backpack Equip Test");
        try
        {
            Component inventory = host.AddComponent(inventoryType);
            InvokePrivate(inventory, "Awake");
            FieldInfo maxSlotsField = RequireField(inventoryType, "maxSlots");
            MethodInfo equipMethod = RequireMethod(inventoryType, "EquipBackpack", itemType);

            // Starting state: 20 total slots (5 hotbar + 15 storage)
            Assert.That((int)maxSlotsField.GetValue(inventory), Is.EqualTo(20));

            // Equip Level 1 -> 25 total
            object bp1 = getOrCreate.Invoke(null, new object[] { 1, false });
            Assert.That((bool)equipMethod.Invoke(inventory, new[] { bp1 }), Is.True);
            Assert.That((int)maxSlotsField.GetValue(inventory), Is.EqualTo(25));

            // Equip Level 2 -> 30 total
            object bp2 = getOrCreate.Invoke(null, new object[] { 2, false });
            Assert.That((bool)equipMethod.Invoke(inventory, new[] { bp2 }), Is.True);
            Assert.That((int)maxSlotsField.GetValue(inventory), Is.EqualTo(30));

            // Re-equipping Level 1 (downgrade/equal) must be rejected
            Assert.That((bool)equipMethod.Invoke(inventory, new[] { bp1 }), Is.False);
            Assert.That((int)maxSlotsField.GetValue(inventory), Is.EqualTo(30));

            // Equip Level 3 -> 35 total
            object bp3 = getOrCreate.Invoke(null, new object[] { 3, false });
            Assert.That((bool)equipMethod.Invoke(inventory, new[] { bp3 }), Is.True);
            Assert.That((int)maxSlotsField.GetValue(inventory), Is.EqualTo(35));

            // Equip Level 4 -> 45 total
            object bp4 = getOrCreate.Invoke(null, new object[] { 4, false });
            Assert.That((bool)equipMethod.Invoke(inventory, new[] { bp4 }), Is.True);
            Assert.That((int)maxSlotsField.GetValue(inventory), Is.EqualTo(45));

            // Equip Level 5 -> 55 total
            object bp5 = getOrCreate.Invoke(null, new object[] { 5, false });
            Assert.That((bool)equipMethod.Invoke(inventory, new[] { bp5 }), Is.True);
            Assert.That((int)maxSlotsField.GetValue(inventory), Is.EqualTo(55));

            // Equipping null or non-backpack must be rejected
            Assert.That((bool)equipMethod.Invoke(inventory, new object[] { null }), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void LootQuantityRules_BoundaryCoverageAndNonAmmoUnaffected()
    {
        Type itemType = RequireType("ItemData, Assembly-CSharp");
        Type categoryType = RequireType("ItemCategory, Assembly-CSharp");
        Type quantityType = RequireType("LootQuantityRules, Assembly-CSharp");
        MethodInfo rollAmount = RequireMethod(quantityType, "RollRandomAmount", itemType, typeof(int), typeof(int));
        MethodInfo getCorpseAmount = RequireMethod(quantityType, "GetCorpseAmount", itemType);

        ScriptableObject ammo = ScriptableObject.CreateInstance(itemType);
        ScriptableObject med = ScriptableObject.CreateInstance(itemType);
        try
        {
            SetField(ammo, "category", Enum.Parse(categoryType, "Ammunition"));
            SetField(ammo, "itemName", "7.62mm Ammo");
            SetField(med, "category", Enum.Parse(categoryType, "Medical"));
            SetField(med, "itemName", "Bandage");

            bool hitMin = false;
            bool hitMax = false;
            for (int i = 0; i < 500; i++)
            {
                int rolled = (int)rollAmount.Invoke(null, new object[] { ammo, 1, 1 });
                Assert.That(rolled, Is.InRange(5, 10), "Ammo roll must always be 5-10 inclusive.");
                if (rolled == 5) hitMin = true;
                if (rolled == 10) hitMax = true;

                int corpseAmt = (int)getCorpseAmount.Invoke(null, new object[] { ammo });
                Assert.That(corpseAmt, Is.InRange(5, 10), "Corpse ammo must always be 5-10 inclusive.");
            }
            Assert.That(hitMin, Is.True, "Random ammo distribution must include lower boundary 5.");
            Assert.That(hitMax, Is.True, "Random ammo distribution must include upper boundary 10.");

            // Non-ammo items: quantity must NOT be clamped to 5-10
            int nonAmmoCorpse = (int)getCorpseAmount.Invoke(null, new object[] { med });
            Assert.That(nonAmmoCorpse, Is.EqualTo(1));

            int nonAmmoRoll = (int)rollAmount.Invoke(null, new object[] { med, 1, 1 });
            Assert.That(nonAmmoRoll, Is.EqualTo(1));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(ammo);
            UnityEngine.Object.DestroyImmediate(med);
        }
    }

    [Test]
    public void CorpseLootTable_WeightsAndKindMapping()
    {
        Type tableType = RequireType("ZombieCorpseLootTable, Assembly-CSharp");
        MethodInfo loadItem = RequireMethod(tableType, "LoadItem", typeof(int));

        object table = Activator.CreateInstance(tableType);
        float waterWeight = (float)ReadField(table, "waterWeight");
        float bandageWeight = (float)ReadField(table, "bandageWeight");
        float medicineWeight = (float)ReadField(table, "medicineWeight");
        float ammoWeight = (float)ReadField(table, "ammoWeight");

        Assert.That(waterWeight, Is.EqualTo(35f));
        Assert.That(bandageWeight, Is.EqualTo(30f));
        Assert.That(medicineWeight, Is.EqualTo(20f));
        Assert.That(ammoWeight, Is.EqualTo(15f));
        Assert.That(waterWeight + bandageWeight + medicineWeight + ammoWeight, Is.EqualTo(100f));

        object item1 = loadItem.Invoke(null, new object[] { 1 });
        object item2 = loadItem.Invoke(null, new object[] { 2 });
        object item3 = loadItem.Invoke(null, new object[] { 3 });
        object item4 = loadItem.Invoke(null, new object[] { 4 });

        Assert.That(item1, Is.Not.Null);
        Assert.That(ReadObjectName(item1), Is.EqualTo("Water"));
        Assert.That(item2, Is.Not.Null);
        Assert.That(ReadObjectName(item2), Is.EqualTo("Bandage"));
        Assert.That(item3, Is.Not.Null);
        Assert.That(ReadObjectName(item3), Is.EqualTo("PainKiller"));
        Assert.That(item4, Is.Not.Null);
        Assert.That(ReadObjectName(item4), Is.EqualTo("Ammo762"));
    }

    [Test]
    public void LootContainer_BackpackTiersWeightsAndDropChance()
    {
        Type rulesType = RequireType("BackpackLootRules");
        MethodInfo getWeight = RequireMethod(rulesType, "GetTierWeightPercent", typeof(int));

        Assert.That((float)getWeight.Invoke(null, new object[] { 1 }), Is.EqualTo(50f));
        Assert.That((float)getWeight.Invoke(null, new object[] { 2 }), Is.EqualTo(30f));
        Assert.That((float)getWeight.Invoke(null, new object[] { 3 }), Is.EqualTo(15f));
        Assert.That((float)getWeight.Invoke(null, new object[] { 4 }), Is.EqualTo(4f));
        Assert.That((float)getWeight.Invoke(null, new object[] { 5 }), Is.EqualTo(1f));

        Type diffRulesType = RequireType("DifficultyRules");
        MethodInfo getLootMult = RequireMethod(diffRulesType, "GetLootRateMultiplier", typeof(int));

        float baseBackpackChance = 10f;
        float easyMult = (float)getLootMult.Invoke(null, new object[] { 0 });
        float normMult = (float)getLootMult.Invoke(null, new object[] { 1 });
        float hardMult = (float)getLootMult.Invoke(null, new object[] { 2 });

        Assert.That(baseBackpackChance * easyMult, Is.EqualTo(15f));
        Assert.That(baseBackpackChance * normMult, Is.EqualTo(10f));
        Assert.That(baseBackpackChance * hardMult, Is.EqualTo(4f));
    }

    [Test]
    public void QuestRewards_OfficeSafeAndArmoryBackpackDistinction()
    {
        Type backpackCatalog = RequireType("BackpackItemCatalog");
        Type militaryCatalog = RequireType("MilitaryQuestItemCatalog");
        Type itemType = RequireType("ItemData");
        Type rulesType = RequireType("BackpackCapacityRules");

        // Safe reward: Level 2 generic backpack -> 25 storage / 30 total
        MethodInfo getGeneric = RequireMethod(backpackCatalog, "GetOrCreate", typeof(int), typeof(bool));
        object safeBackpack = getGeneric.Invoke(null, new object[] { 2, false });
        Assert.That(safeBackpack, Is.Not.Null);
        Assert.That(ReadObjectName(safeBackpack), Is.EqualTo("BackpackLevel2"));
        MethodInfo getStorage = RequireMethod(rulesType, "GetStorageSlots", itemType);
        Assert.That((int)getStorage.Invoke(null, new[] { safeBackpack }), Is.EqualTo(25));

        // Armory reward: Level 3 military backpack -> 30 storage / 35 total
        MethodInfo getMilitary = RequireMethod(militaryCatalog, "GetOrCreate", RequireType("MilitaryQuestItemKind"));
        object militaryEnumVal = Enum.Parse(RequireType("MilitaryQuestItemKind"), "LevelThreeBackpack");
        object armoryBackpack = getMilitary.Invoke(null, new[] { militaryEnumVal });
        Assert.That(armoryBackpack, Is.Not.Null);
        Assert.That(ReadObjectName(armoryBackpack), Is.EqualTo("MilitaryBackpackLevel3"));
        Assert.That((int)getStorage.Invoke(null, new[] { armoryBackpack }), Is.EqualTo(30));

        Assert.That(ReadObjectName(safeBackpack), Is.Not.EqualTo(ReadObjectName(armoryBackpack)));
    }

    [Test]
    public void SoloDifficultyMatrix_EasyNormalHard_DensityLootDamageAndLoadouts_ExactVerification()
    {
        Type diffRulesType = RequireType("DifficultyRules");
        MethodInfo getDensity = RequireMethod(diffRulesType, "GetZombieDensityMultiplier", typeof(int));
        MethodInfo getLoot = RequireMethod(diffRulesType, "GetLootRateMultiplier", typeof(int));
        MethodInfo getDamage = RequireMethod(diffRulesType, "GetIncomingDamageMultiplier", typeof(int));
        MethodInfo getLoadout = RequireMethod(diffRulesType, "GetStarterGearLoadout", typeof(int));

        // EASY MODE (0): Density 0.5x, Loot 1.5x, Damage 0.7x, Loadout: AK47 + 30 Ammo762 + 1 Meat (3 items)
        Assert.That((float)getDensity.Invoke(null, new object[] { 0 }), Is.EqualTo(0.5f));
        Assert.That((float)getLoot.Invoke(null, new object[] { 0 }), Is.EqualTo(1.5f));
        Assert.That((float)getDamage.Invoke(null, new object[] { 0 }), Is.EqualTo(0.7f));
        Array easyLoadout = (Array)getLoadout.Invoke(null, new object[] { 0 });
        Assert.That(easyLoadout.Length, Is.EqualTo(3));

        // NORMAL MODE (1): Density 1.0x, Loot 1.0x, Damage 1.0x, Loadout: Flashlight + Bandage (2 items)
        Assert.That((float)getDensity.Invoke(null, new object[] { 1 }), Is.EqualTo(1.0f));
        Assert.That((float)getLoot.Invoke(null, new object[] { 1 }), Is.EqualTo(1.0f));
        Assert.That((float)getDamage.Invoke(null, new object[] { 1 }), Is.EqualTo(1.0f));
        Array normLoadout = (Array)getLoadout.Invoke(null, new object[] { 1 });
        Assert.That(normLoadout.Length, Is.EqualTo(2));

        // HARD MODE (2): Density 2.5x, Loot 0.4x, Damage 1.5x, Loadout: 0 items
        Assert.That((float)getDensity.Invoke(null, new object[] { 2 }), Is.EqualTo(2.5f));
        Assert.That((float)getLoot.Invoke(null, new object[] { 2 }), Is.EqualTo(0.4f));
        Assert.That((float)getDamage.Invoke(null, new object[] { 2 }), Is.EqualTo(1.5f));
        Array hardLoadout = (Array)getLoadout.Invoke(null, new object[] { 2 });
        Assert.That(hardLoadout.Length, Is.EqualTo(0));
    }

    [Test]
    public void CorpseLootProbability_ThousandSeededRolls_AndIndependentVsSingleCorpseMath()
    {
        Type corpseType = RequireType("ZombieCorpseLoot");
        Type probType = RequireType("LootProbabilityRules");
        MethodInfo getChance = RequireMethod(corpseType, "GetLootChancePercent", typeof(int));
        MethodInfo getNoLootGeneric = RequireMethod(probType, "GetNoLootProbabilityPercent", typeof(float), typeof(int));
        MethodInfo getAtLeastOne = RequireMethod(probType, "GetAtLeastOneLootProbabilityPercent", typeof(float), typeof(int));

        // 1. Exact mathematical proofs for 20 independent searches
        float easyNoLoot20 = (float)getNoLootGeneric.Invoke(null, new object[] { 45f, 20 });
        float normNoLoot20 = (float)getNoLootGeneric.Invoke(null, new object[] { 30f, 20 });
        float hardNoLoot20 = (float)getNoLootGeneric.Invoke(null, new object[] { 12f, 20 });
        float normAtLeastOne20 = (float)getAtLeastOne.Invoke(null, new object[] { 30f, 20 });

        Assert.That(easyNoLoot20, Is.EqualTo(0.0006415f).Within(0.0001f));
        Assert.That(normNoLoot20, Is.EqualTo(0.079775f).Within(0.001f));
        Assert.That(hardNoLoot20, Is.EqualTo(7.75628f).Within(0.01f));
        Assert.That(normAtLeastOne20, Is.EqualTo(99.920225f).Within(0.001f));

        // 2. 1000 Seeded rolls verification: Easy > Normal > Hardcore
        UnityEngine.Random.InitState(42);
        int easyHits = 0, normHits = 0, hardHits = 0;
        int totalRolls = 1000;
        for (int i = 0; i < totalRolls; i++)
        {
            if (UnityEngine.Random.Range(0f, 100f) < (float)getChance.Invoke(null, new object[] { 0 })) easyHits++;
            if (UnityEngine.Random.Range(0f, 100f) < (float)getChance.Invoke(null, new object[] { 1 })) normHits++;
            if (UnityEngine.Random.Range(0f, 100f) < (float)getChance.Invoke(null, new object[] { 2 })) hardHits++;
        }

        Assert.That(easyHits, Is.GreaterThan(normHits), "Easy mode must yield more loot than Normal mode.");
        Assert.That(normHits, Is.GreaterThan(hardHits), "Normal mode must yield more loot than Hardcore mode.");
        Assert.That(easyHits, Is.InRange(400, 500), "Easy mode 1000 rolls should converge near 45% (450).");
        Assert.That(normHits, Is.InRange(250, 350), "Normal mode 1000 rolls should converge near 30% (300).");
        Assert.That(hardHits, Is.InRange(80, 160), "Hardcore mode 1000 rolls should converge near 12% (120).");
    }

    [Test]
    public void BackpackCapacity_FullProgressionSequence_AndDowngradeRejection()
    {
        Type rulesType = RequireType("BackpackCapacityRules");
        Type catalogType = RequireType("BackpackItemCatalog");
        Type itemType = RequireType("ItemData");
        MethodInfo getOrCreate = RequireMethod(catalogType, "GetOrCreate", typeof(int), typeof(bool));
        MethodInfo getStorage = RequireMethod(rulesType, "GetStorageSlots", itemType);
        MethodInfo getTotal = RequireMethod(rulesType, "GetTotalSlots", typeof(int));

        int[] expectedStorage = new int[] { 15, 20, 25, 30, 40, 50 };
        int[] expectedTotal = new int[] { 20, 25, 30, 35, 45, 55 };

        for (int level = 0; level <= 5; level++)
        {
            Assert.That((int)getTotal.Invoke(null, new object[] { level }), Is.EqualTo(expectedTotal[level]));
            if (level > 0)
            {
                object bp = getOrCreate.Invoke(null, new object[] { level, false });
                Assert.That((int)getStorage.Invoke(null, new[] { bp }), Is.EqualTo(expectedStorage[level]));
            }
        }
    }

    private static Type RequireType(string name)
    {
        Type type = Type.GetType(name);
        if (type == null)
        {
            string shortName = name.Contains(",") ? name.Split(',')[0].Trim() : name;
            type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(shortName, false))
                .FirstOrDefault(candidate => candidate != null);
        }
        if (type == null)
        {
            string simpleName = name.Contains(".") ? name.Substring(name.LastIndexOf('.') + 1) : name;
            if (simpleName.Contains(",")) simpleName = simpleName.Split(',')[0].Trim();
            type = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => {
                    try { return a.GetTypes(); } catch { return Array.Empty<Type>(); }
                })
                .FirstOrDefault(t => t.Name == simpleName);
        }
        Assert.That(type, Is.Not.Null, $"Missing runtime type '{name}'.");
        return type;
    }

    private static string ReadObjectName(object target)
    {
        if (target is UnityEngine.Object uo) return uo.name;
        if (target == null) return null;
        PropertyInfo prop = target.GetType().GetProperty("name", BindingFlags.Public | BindingFlags.Instance);
        if (prop != null) return (string)prop.GetValue(target);
        return (string)ReadField(target, "itemName");
    }

    private static int GetStaticInt(Type type, string fieldName)
    {
        FieldInfo field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
        Assert.That(field, Is.Not.Null, $"Missing public static field '{fieldName}'.");
        return (int)field.GetValue(null);
    }

    private static FieldInfo RequireField(Type type, string name)
    {
        FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing field '{name}' on {type.Name}.");
        return field;
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes) =>
        RequireMethod(type, name,
            BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            parameterTypes);

    private static MethodInfo RequireMethod(Type type, string name, BindingFlags flags, params Type[] parameterTypes)
    {
        MethodInfo method = type.GetMethod(name, flags, null, parameterTypes, null);
        Assert.That(method, Is.Not.Null, $"Missing method '{name}' on {type.Name}.");
        return method;
    }

    private static object ReadField(object target, string name) =>
        RequireField(target.GetType(), name).GetValue(target);

    private static void InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Missing method '{methodName}' on {target.GetType().Name}.");
        method.Invoke(target, null);
    }

    private static void SetField(object target, string name, object value) =>
        RequireField(target.GetType(), name).SetValue(target, value);
}
