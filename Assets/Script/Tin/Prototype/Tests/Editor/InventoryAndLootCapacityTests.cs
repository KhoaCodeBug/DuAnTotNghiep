using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Capacity/loot regression tests. This test assembly intentionally does not
/// reference the default Assembly-CSharp assembly, so runtime types are
/// resolved through reflection just like the other project tests.
/// </summary>
public sealed class InventoryAndLootCapacityTests
{
    [TearDown]
    public void TearDown()
    {
        Type.GetType("BackpackQuestRewardPresentation, Assembly-CSharp")?.GetMethod("ResetForTests", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
        Type.GetType("AutoChatManager, Assembly-CSharp")?.GetMethod("ResetForTests", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
    }

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
    public void BackpackQuestMilestonesMapToHospitalLevelFourAndRadioLevelFive()
    {
        Type rulesType = RequireType("BackpackQuestRewardRules, Assembly-CSharp");
        Type milestoneType = RequireType("BackpackQuestRewardMilestone, Assembly-CSharp");
        MethodInfo getRewardLevel = RequireMethod(rulesType, "GetRewardLevel", milestoneType);
        MethodInfo getClaimBit = RequireMethod(rulesType, "GetClaimBit", typeof(int));
        MethodInfo isClaimed = RequireMethod(rulesType, "IsClaimed", typeof(int), typeof(int));
        MethodInfo markClaimed = RequireMethod(rulesType, "MarkClaimed", typeof(int), typeof(int));

        object hospital = Enum.Parse(milestoneType, "HospitalArrival");
        object radio = Enum.Parse(milestoneType, "RadioRestoration");
        Assert.That((int)getRewardLevel.Invoke(null, new[] { hospital }), Is.EqualTo(4));
        Assert.That((int)getRewardLevel.Invoke(null, new[] { radio }), Is.EqualTo(5));

        int mask = 0;
        Assert.That((bool)isClaimed.Invoke(null, new object[] { mask, 4 }), Is.False);
        int afterHospital = (int)markClaimed.Invoke(null, new object[] { mask, 4 });
        Assert.That(afterHospital, Is.EqualTo((int)getClaimBit.Invoke(null, new object[] { 4 })));
        Assert.That((bool)isClaimed.Invoke(null, new object[] { afterHospital, 4 }), Is.True);
        Assert.That((int)markClaimed.Invoke(null, new object[] { afterHospital, 4 }), Is.EqualTo(afterHospital),
            "A repeated hospital trigger must not produce a second claim.");

        int afterRadio = (int)markClaimed.Invoke(null, new object[] { afterHospital, 5 });
        Assert.That((bool)isClaimed.Invoke(null, new object[] { afterRadio, 5 }), Is.True);
        Assert.That((bool)isClaimed.Invoke(null, new object[] { afterRadio, 3 }), Is.False,
            "Backpack levels 1-3 are capacity-only and have no quest milestone claim.");
    }

    [Test]
    public void BackpackLootRules_OrdinaryLootOnlyRollsLevelsOneThroughThreeWithApprovedWeights()
    {
        Type lootRulesType = RequireType("BackpackLootRules, Assembly-CSharp");
        MethodInfo rollTier = RequireMethod(lootRulesType, "RollTier");
        MethodInfo getWeight = RequireMethod(lootRulesType, "GetTierWeightPercent", typeof(int));

        Assert.That((float)getWeight.Invoke(null, new object[] { 1 }), Is.EqualTo(70f).Within(0.001f));
        Assert.That((float)getWeight.Invoke(null, new object[] { 2 }), Is.EqualTo(25f).Within(0.001f));
        Assert.That((float)getWeight.Invoke(null, new object[] { 3 }), Is.EqualTo(5f).Within(0.001f));
        Assert.That((float)getWeight.Invoke(null, new object[] { 4 }), Is.EqualTo(0f));
        Assert.That((float)getWeight.Invoke(null, new object[] { 5 }), Is.EqualTo(0f));
        Assert.That((float)getWeight.Invoke(null, new object[] { 0 }), Is.EqualTo(0f));

        for (int i = 0; i < 500; i++)
        {
            int tier = (int)rollTier.Invoke(null, null);
            Assert.That(tier, Is.InRange(1, 3), "Ordinary loot must roll only tiers 1, 2 or 3.");
        }

        Type containerType = RequireType("LootContainer, Assembly-CSharp");
        GameObject host = new GameObject("Container Default Test");
        try
        {
            host.AddComponent<BoxCollider2D>();
            host.AddComponent<SpriteRenderer>();
            Component container = host.AddComponent(containerType);
            Assert.That(container, Is.Not.Null);
            FieldInfo chanceField = RequireField(containerType, "backpackDropChance");
            Assert.That((float)chanceField.GetValue(container), Is.EqualTo(5f).Within(0.001f),
                "Default backpackDropChance on LootContainer must be 5%.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void BackpackPickupPredicate_RejectsEqualOrLower_AcceptsHigher_NoDowngrade()
    {
        Type inventoryType = RequireType("InventorySystem, Assembly-CSharp");
        Type catalogType = RequireType("BackpackItemCatalog, Assembly-CSharp");
        Type itemType = RequireType("ItemData, Assembly-CSharp");
        MethodInfo getOrCreate = RequireMethod(catalogType, "GetOrCreate", typeof(int), typeof(bool));
        MethodInfo canAccept = RequireMethod(inventoryType, "CanAcceptBackpackLoot", itemType);

        GameObject host = new GameObject("Backpack Predicate Test");
        try
        {
            Component inventory = host.AddComponent(inventoryType);
            InvokePrivate(inventory, "Awake");

            object bp1 = getOrCreate.Invoke(null, new object[] { 1, false });
            object bp2 = getOrCreate.Invoke(null, new object[] { 2, false });
            object bp3 = getOrCreate.Invoke(null, new object[] { 3, false });
            object bp5 = getOrCreate.Invoke(null, new object[] { 5, false });

            // Initial level is 0
            Assert.That((bool)canAccept.Invoke(inventory, new[] { bp1 }), Is.True,
                "Level 0 inventory must accept level 1 backpack loot.");
            Assert.That((bool)canAccept.Invoke(inventory, new[] { bp2 }), Is.True);
            Assert.That((bool)canAccept.Invoke(inventory, new[] { bp3 }), Is.True);

            // Set level to 2 (total 30 slots)
            inventoryType.GetMethod("SetMaxSlots")?.Invoke(inventory, new object[] { 30 });
            PropertyInfo currentLevelProp = inventoryType.GetProperty("CurrentBackpackLevel");
            Assert.That((int)currentLevelProp.GetValue(inventory), Is.EqualTo(2));

            Assert.That((bool)canAccept.Invoke(inventory, new[] { bp1 }), Is.False,
                "Level 2 inventory must reject level 1 backpack loot (no downgrade).");
            Assert.That((bool)canAccept.Invoke(inventory, new[] { bp2 }), Is.False,
                "Level 2 inventory must reject level 2 backpack loot (no duplicate/equal loot).");
            Assert.That((bool)canAccept.Invoke(inventory, new[] { bp3 }), Is.True,
                "Level 2 inventory must accept level 3 backpack loot (upgrade).");

            // Set level to 5 (total 55 slots)
            inventoryType.GetMethod("SetMaxSlots")?.Invoke(inventory, new object[] { 55 });
            Assert.That((int)currentLevelProp.GetValue(inventory), Is.EqualTo(5));

            Assert.That((bool)canAccept.Invoke(inventory, new[] { bp3 }), Is.False);
            Assert.That((bool)canAccept.Invoke(inventory, new[] { bp5 }), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void QuestBackpackRewardUpgradesOnlyThroughTheAuthorityRewardApi()
    {
        Type inventoryType = RequireType("InventorySystem, Assembly-CSharp");
        Type catalogType = RequireType("BackpackItemCatalog, Assembly-CSharp");
        Type itemType = RequireType("ItemData, Assembly-CSharp");
        MethodInfo getOrCreate = RequireMethod(catalogType, "GetOrCreate", typeof(int), typeof(bool));
        MethodInfo reward = RequireMethod(inventoryType, "TryGrantQuestBackpackReward", typeof(int));
        FieldInfo maxSlots = RequireField(inventoryType, "maxSlots");

        GameObject host = new GameObject("Backpack Quest Reward Test");
        try
        {
            Component inventory = host.AddComponent(inventoryType);
            InvokePrivate(inventory, "Awake");

            Assert.That((bool)reward.Invoke(inventory, new object[] { 4 }), Is.True);
            Assert.That((int)maxSlots.GetValue(inventory), Is.EqualTo(45));
            Assert.That((bool)reward.Invoke(inventory, new object[] { 4 }), Is.False,
                "The same hospital milestone must be idempotent.");

            Assert.That((bool)reward.Invoke(inventory, new object[] { 5 }), Is.True);
            Assert.That((int)maxSlots.GetValue(inventory), Is.EqualTo(55));
            Assert.That((bool)reward.Invoke(inventory, new object[] { 5 }), Is.False,
                "The same military-entry milestone must be idempotent.");
            Assert.That((bool)reward.Invoke(inventory, new object[] { 3 }), Is.False,
                "The quest reward API must never downgrade or grant levels 1-3.");

            object ordinaryLevelFive = getOrCreate.Invoke(null, new object[] { 5, false });
            MethodInfo equip = RequireMethod(inventoryType, "EquipBackpack", itemType);
            Assert.That((bool)equip.Invoke(inventory, new[] { ordinaryLevelFive }), Is.False,
                "Ordinary loot/equip cannot re-trigger an already claimed quest milestone.");

            Type presentationType = RequireType("BackpackQuestRewardPresentation, Assembly-CSharp");
            PropertyInfo inputLock = presentationType.GetProperty("BlocksGameplayInput",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(inputLock, Is.Not.Null);
            Assert.That((bool)inputLock.GetValue(null), Is.False,
                "The backpack reveal must not block a player's immediate multiplayer interaction.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void TwoPlayersContestOrdinaryBackpackLoot_HigherLevelRejected_LowerLevelCanLootItem()
    {
        Type inventoryType = RequireType("InventorySystem, Assembly-CSharp");
        Type catalogType = RequireType("BackpackItemCatalog, Assembly-CSharp");
        Type containerType = RequireType("LootContainer, Assembly-CSharp");
        Type itemType = RequireType("ItemData, Assembly-CSharp");
        MethodInfo getOrCreate = RequireMethod(catalogType, "GetOrCreate", typeof(int), typeof(bool));
        MethodInfo canAccept = RequireMethod(inventoryType, "CanAcceptBackpackLoot", itemType);

        GameObject p1Host = new GameObject("Player 1 (High Tier)");
        GameObject p2Host = new GameObject("Player 2 (Low Tier)");
        GameObject containerHost = new GameObject("Contested Container");
        try
        {
            Component inv1 = p1Host.AddComponent(inventoryType);
            InvokePrivate(inv1, "Awake");
            inventoryType.GetMethod("SetMaxSlots")?.Invoke(inv1, new object[] { 35 }); // Level 3

            Component inv2 = p2Host.AddComponent(inventoryType);
            InvokePrivate(inv2, "Awake");
            inventoryType.GetMethod("SetMaxSlots")?.Invoke(inv2, new object[] { 20 }); // Level 0

            object lootItem = getOrCreate.Invoke(null, new object[] { 2, false }); // Level 2 backpack in container

            // Player 1 (Level 3) tries to loot Level 2 backpack -> Rejected
            Assert.That((bool)canAccept.Invoke(inv1, new[] { lootItem }), Is.False,
                "Higher level player cannot loot lower/equal tier backpack.");

            // Player 2 (Level 0) tries to loot Level 2 backpack -> Accepted
            Assert.That((bool)canAccept.Invoke(inv2, new[] { lootItem }), Is.True,
                "Lower level player can loot the backpack from the container.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(p1Host);
            UnityEngine.Object.DestroyImmediate(p2Host);
            UnityEngine.Object.DestroyImmediate(containerHost);
        }
    }

    [Test]
    public void DuplicateOrReconnectClaimRequest_DoesNotInvokePresentationCallbackTwice()
    {
        Type inventoryType = RequireType("InventorySystem, Assembly-CSharp");
        MethodInfo requestClaim = RequireMethod(inventoryType, "RequestClaimLevelFiveBackpackReward", typeof(Action));

        GameObject host = new GameObject("Idempotent Claim Test");
        try
        {
            Component inventory = host.AddComponent(inventoryType);
            InvokePrivate(inventory, "Awake");

            int callbackCount = 0;
            Action callback = () => { callbackCount++; };

            // First claim invocation
            requestClaim.Invoke(inventory, new object[] { callback });
            Assert.That(callbackCount, Is.EqualTo(1), "First claim must invoke the presentation callback.");

            // Duplicate claim invocation (e.g. late join / reconnect / duplicate event)
            requestClaim.Invoke(inventory, new object[] { callback });
            Assert.That(callbackCount, Is.EqualTo(1), "Duplicate claim must NOT re-invoke the presentation callback.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void MilitaryBaseEntry_HasNoBackpackRewardPath()
    {
        Type rulesType = RequireType("BackpackQuestRewardRules, Assembly-CSharp");
        Type milestoneType = RequireType("BackpackQuestRewardMilestone, Assembly-CSharp");
        MethodInfo getRewardLevel = RequireMethod(rulesType, "GetRewardLevel", milestoneType);

        object militaryEntry = Enum.Parse(milestoneType, "MilitaryBaseEntry");
        Assert.That((int)getRewardLevel.Invoke(null, new[] { militaryEntry }), Is.EqualTo(0),
            "MilitaryBaseEntry must not grant any backpack reward level.");
    }

    [Test]
    public void BackpackCatalogResolvesFiveProjectIconTextures()
    {
        for (int level = 1; level <= 5; level++)
        {
            Texture2D iconTexture = Resources.Load<Texture2D>("Backpacks/BackpackLevel" + level);
            Assert.That(iconTexture, Is.Not.Null,
                "Missing project backpack icon resource for level " + level + ".");
        }
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
    public void RandomAmmoLootUsesApprovedPerItemQuantities()
    {
        Type itemType = RequireType("ItemData, Assembly-CSharp");
        Type categoryType = RequireType("ItemCategory, Assembly-CSharp");
        Type quantityType = RequireType("LootQuantityRules, Assembly-CSharp");
        MethodInfo rollAmount = RequireMethod(quantityType, "RollRandomAmount", itemType, typeof(int), typeof(int));
        MethodInfo getCorpseAmount = RequireMethod(quantityType, "GetCorpseAmount", itemType);

        ScriptableObject ammo762 = ScriptableObject.CreateInstance(itemType);
        ScriptableObject ammo12Gauge = ScriptableObject.CreateInstance(itemType);
        try
        {
            SetField(ammo762, "category", Enum.Parse(categoryType, "Ammunition"));
            SetField(ammo762, "itemName", "Ammo762");
            SetField(ammo762, "maxStack", 30);
            SetField(ammo762, "isStackable", true);
            SetField(ammo12Gauge, "category", Enum.Parse(categoryType, "Ammunition"));
            SetField(ammo12Gauge, "itemName", "Ammo12Gauge");
            SetField(ammo12Gauge, "maxStack", 10);
            SetField(ammo12Gauge, "isStackable", true);

            UnityEngine.Random.InitState(42);
            for (int i = 0; i < 100; i++)
            {
                int rifleAmount = (int)rollAmount.Invoke(null, new object[] { ammo762, 15, 30 });
                Assert.That(rifleAmount, Is.InRange(15, 30), "Ammo762 random loot must be 15-30 inclusive.");
                int rifleCorpseAmount = (int)getCorpseAmount.Invoke(null, new object[] { ammo762 });
                Assert.That(rifleCorpseAmount, Is.InRange(15, 30), "Ammo762 corpse loot must be 15-30 inclusive.");

                int shotgunAmount = (int)rollAmount.Invoke(null, new object[] { ammo12Gauge, 5, 5 });
                Assert.That(shotgunAmount, Is.EqualTo(5), "Ordinary Ammo12Gauge loot must be exactly 5.");
                int shotgunCorpseAmount = (int)getCorpseAmount.Invoke(null, new object[] { ammo12Gauge });
                Assert.That(shotgunCorpseAmount, Is.EqualTo(5), "Corpse Ammo12Gauge loot must be exactly 5.");

                int authoredTutorialAmount = (int)rollAmount.Invoke(null, new object[] { ammo12Gauge, 12, 12 });
                Assert.That(authoredTutorialAmount, Is.EqualTo(12),
                    "Explicit authored tutorial quantities must override the ordinary gauge default.");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(ammo762);
            UnityEngine.Object.DestroyImmediate(ammo12Gauge);
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
            SetField(ammo, "itemName", "Ammo762");
            SetField(ammo, "maxStack", 30);
            ScriptableObject ammo12Gauge = ScriptableObject.CreateInstance(itemType);
            SetField(ammo12Gauge, "category", Enum.Parse(categoryType, "Ammunition"));
            SetField(ammo12Gauge, "itemName", "Ammo12Gauge");
            SetField(ammo12Gauge, "maxStack", 10);
            SetField(med, "category", Enum.Parse(categoryType, "Medical"));
            SetField(med, "itemName", "Bandage");

            try
            {
                for (int i = 0; i < 100; i++)
                {
                    int rolled = (int)rollAmount.Invoke(null, new object[] { ammo, 15, 30 });
                    Assert.That(rolled, Is.InRange(15, 30), "Ammo762 roll must always be 15-30 inclusive.");

                    int corpseAmt = (int)getCorpseAmount.Invoke(null, new object[] { ammo });
                    Assert.That(corpseAmt, Is.InRange(15, 30), "Corpse Ammo762 must always be 15-30 inclusive.");

                    int gaugeAmount = (int)rollAmount.Invoke(null, new object[] { ammo12Gauge, 5, 5 });
                    Assert.That(gaugeAmount, Is.EqualTo(5));
                    int authoredAmount = (int)rollAmount.Invoke(null, new object[] { ammo12Gauge, 12, 12 });
                    Assert.That(authoredAmount, Is.EqualTo(12));
                }

                // Non-ammo items: quantity must remain one.
                int nonAmmoCorpse = (int)getCorpseAmount.Invoke(null, new object[] { med });
                Assert.That(nonAmmoCorpse, Is.EqualTo(1));

                int nonAmmoRoll = (int)rollAmount.Invoke(null, new object[] { med, 1, 1 });
                Assert.That(nonAmmoRoll, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ammo12Gauge);
            }
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
        float ammo12GaugeWeight = (float)ReadField(table, "ammo12GaugeWeight");

        Assert.That(waterWeight, Is.EqualTo(25f));
        Assert.That(bandageWeight, Is.EqualTo(45f));
        Assert.That(medicineWeight, Is.EqualTo(15f));
        Assert.That(ammoWeight, Is.EqualTo(10f));
        Assert.That(ammo12GaugeWeight, Is.EqualTo(5f));
        Assert.That(waterWeight + bandageWeight + medicineWeight + ammoWeight + ammo12GaugeWeight,
            Is.EqualTo(100f));

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
        object item5 = loadItem.Invoke(null, new object[] { 5 });
        Assert.That(item5, Is.Not.Null);
        Assert.That(ReadObjectName(item5), Is.EqualTo("Ammo12Gauge"));
    }

    [Test]
    public void LootContainer_BackpackTiersWeightsAndDropChance()
    {
        Type rulesType = RequireType("BackpackLootRules");
        MethodInfo getWeight = RequireMethod(rulesType, "GetTierWeightPercent", typeof(int));

        Assert.That((float)getWeight.Invoke(null, new object[] { 1 }), Is.EqualTo(70f));
        Assert.That((float)getWeight.Invoke(null, new object[] { 2 }), Is.EqualTo(25f));
        Assert.That((float)getWeight.Invoke(null, new object[] { 3 }), Is.EqualTo(5f));
        Assert.That((float)getWeight.Invoke(null, new object[] { 4 }), Is.EqualTo(0f));
        Assert.That((float)getWeight.Invoke(null, new object[] { 5 }), Is.EqualTo(0f));

        Type diffRulesType = RequireType("DifficultyRules");
        MethodInfo getLootMult = RequireMethod(diffRulesType, "GetLootRateMultiplier", typeof(int));

        float baseBackpackChance = 5f;
        float easyMult = (float)getLootMult.Invoke(null, new object[] { 0 });
        float normMult = (float)getLootMult.Invoke(null, new object[] { 1 });
        float hardMult = (float)getLootMult.Invoke(null, new object[] { 2 });

        Assert.That(baseBackpackChance * easyMult, Is.EqualTo(7.5f));
        Assert.That(baseBackpackChance * normMult, Is.EqualTo(5f));
        Assert.That(baseBackpackChance * hardMult, Is.EqualTo(2f));

        Type containerType = RequireType("LootContainer, Assembly-CSharp");
        GameObject host = new GameObject("Loot Bonus Chance Test");
        try
        {
            host.AddComponent<BoxCollider2D>();
            host.AddComponent<SpriteRenderer>();
            Component container = host.AddComponent(containerType);
            FieldInfo weaponChanceField = RequireField(containerType, "bonusWeaponDropChance");
            float weaponChance = (float)weaponChanceField.GetValue(container);
            Assert.That(weaponChance, Is.EqualTo(15f));
            Assert.That(weaponChance * easyMult, Is.EqualTo(22.5f));
            Assert.That(weaponChance * normMult, Is.EqualTo(15f));
            Assert.That(weaponChance * hardMult, Is.EqualTo(6f));

            FieldInfo backpackChanceField = RequireField(containerType, "backpackDropChance");
            float backpackChance = (float)backpackChanceField.GetValue(container);
            Assert.That(backpackChance, Is.EqualTo(5f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void QuestRewards_OfficeSafeAndArmoryBackpackDistinction()
    {
        Type backpackCatalog = RequireType("BackpackItemCatalog");
        Type militaryCatalog = RequireType("MilitaryQuestItemKind");
        Type militaryCatalogType = RequireType("MilitaryQuestItemCatalog");
        Type itemType = RequireType("ItemData");
        Type rulesType = RequireType("BackpackCapacityRules");

        // Catalog items remain available for presentation and testing
        MethodInfo getGeneric = RequireMethod(backpackCatalog, "GetOrCreate", typeof(int), typeof(bool));
        object safeBackpack = getGeneric.Invoke(null, new object[] { 2, false });
        Assert.That(safeBackpack, Is.Not.Null);
        Assert.That(ReadObjectName(safeBackpack), Is.EqualTo("BackpackLevel2"));
        MethodInfo getStorage = RequireMethod(rulesType, "GetStorageSlots", itemType);
        Assert.That((int)getStorage.Invoke(null, new[] { safeBackpack }), Is.EqualTo(25));

        MethodInfo getMilitary = RequireMethod(militaryCatalogType, "GetOrCreate", militaryCatalog);
        object militaryEnumVal = Enum.Parse(militaryCatalog, "LevelThreeBackpack");
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

        // EASY MODE (0): Density 0.5x, Loot 1.5x, Damage 0.7x, five starter entries.
        Assert.That((float)getDensity.Invoke(null, new object[] { 0 }), Is.EqualTo(0.5f));
        Assert.That((float)getLoot.Invoke(null, new object[] { 0 }), Is.EqualTo(1.5f));
        Assert.That((float)getDamage.Invoke(null, new object[] { 0 }), Is.EqualTo(0.7f));
        Array easyLoadout = (Array)getLoadout.Invoke(null, new object[] { 0 });
        Assert.That(easyLoadout.Length, Is.EqualTo(5));

        // NORMAL MODE (1): Density 1.0x, Loot 1.0x, Damage 1.0x, two starter entries.
        Assert.That((float)getDensity.Invoke(null, new object[] { 1 }), Is.EqualTo(1.0f));
        Assert.That((float)getLoot.Invoke(null, new object[] { 1 }), Is.EqualTo(1.0f));
        Assert.That((float)getDamage.Invoke(null, new object[] { 1 }), Is.EqualTo(1.0f));
        Array normLoadout = (Array)getLoadout.Invoke(null, new object[] { 1 });
        Assert.That(normLoadout.Length, Is.EqualTo(2));

        // HARD MODE (2): Density 2.5x, Loot 0.4x, Damage 1.5x, no starting gear.
        Assert.That((float)getDensity.Invoke(null, new object[] { 2 }), Is.EqualTo(2.5f));
        Assert.That((float)getLoot.Invoke(null, new object[] { 2 }), Is.EqualTo(0.4f));
        Assert.That((float)getDamage.Invoke(null, new object[] { 2 }), Is.EqualTo(1.5f));
        Array hardLoadout = (Array)getLoadout.Invoke(null, new object[] { 2 });
        Assert.That(hardLoadout.Length, Is.Zero);
    }

    [Test]
    public void StarterLoadout_UsesCurrentWeaponPoolAndExactFixedQuantities()
    {
        Type diffRulesType = RequireType("DifficultyRules, Assembly-CSharp");
        MethodInfo getPool = RequireMethod(diffRulesType, "GetStarterWeaponPool");
        MethodInfo getLoadout = RequireMethod(diffRulesType, "GetStarterGearLoadout", typeof(int));

        Array pool = (Array)getPool.Invoke(null, null);
        string[] expectedPool = { "AK47", "S12K" };
        Assert.That(pool.Length, Is.EqualTo(expectedPool.Length));
        foreach (string expectedWeapon in expectedPool)
            Assert.That(pool.Cast<object>().Select(value => value.ToString()), Does.Contain(expectedWeapon));

        AssertStarterFixedAmount((Array)getLoadout.Invoke(null, new object[] { 0 }), "Water", 3);
        AssertStarterFixedAmount((Array)getLoadout.Invoke(null, new object[] { 0 }), "Meat", 3);
        AssertStarterFixedAmount((Array)getLoadout.Invoke(null, new object[] { 0 }), "Bandage", 5);
        AssertStarterFixedAmount((Array)getLoadout.Invoke(null, new object[] { 0 }), "PainKiller", 1);

        AssertStarterFixedAmount((Array)getLoadout.Invoke(null, new object[] { 1 }), "Bandage", 3);

        foreach (int difficulty in new[] { 0, 1, 2 })
        {
            Array loadout = (Array)getLoadout.Invoke(null, new object[] { difficulty });
            Assert.That(loadout.Cast<object>().Select(entry => (string)entry.GetType().GetField("ItemId").GetValue(entry)),
                Does.Not.Contain("Flashlight"),
                "Difficulty loadout tables must not duplicate the separate per-avatar flashlight entitlement.");
        }

        foreach (int difficulty in new[] { 0, 1 })
        {
            Array loadout = (Array)getLoadout.Invoke(null, new object[] { difficulty });
            int randomWeaponEntries = 0;
            foreach (object entry in loadout)
            {
                FieldInfo preferHotbar = entry.GetType().GetField("PreferHotbar");
                if (preferHotbar == null || !(bool)preferHotbar.GetValue(entry)) continue;

                randomWeaponEntries++;
                string itemId = (string)entry.GetType().GetField("ItemId").GetValue(entry);
                int amount = (int)entry.GetType().GetField("Amount").GetValue(entry);
                Assert.That(pool.Cast<object>().Select(value => value.ToString()), Does.Not.Contain(itemId),
                    "The starter loadout must resolve a random weapon instead of hard-coding one pool item.");
                Assert.That(amount, Is.EqualTo(1));
            }

            Assert.That(randomWeaponEntries, Is.EqualTo(1),
                "Easy and Normal must each contain exactly one random starter weapon entry.");
        }
    }

    [Test]
    public void AuthoredLootTables_UseApprovedRatesAndPreserveTutorialGaugeException()
    {
        AssertLootTableRule("Assets/Khoa/Code/LootTableS/MacDinh_KhuSanh.asset", "Ammo762", 20f, 15, 30);
        AssertLootTableRule("Assets/Khoa/Code/LootTableS/MacDinh_KhuSanh.asset", "Bandage", 35f, 1, 1);
        AssertLootTableRule("Assets/Khoa/Code/LootTableS/MacDinh_KhuSanh.asset", "EnergyWater", 20f, 1, 1);
        AssertLootTableRule("Assets/Khoa/Code/LootTableS/MacDinh_KhuSanh.asset", "Meat", 40f, 1, 1);
        AssertLootTableRule("Assets/Khoa/Code/LootTableS/MacDinh_KhuSanh.asset", "PainKiller", 15f, 1, 1);
        AssertLootTableRule("Assets/Khoa/Code/LootTableS/MacDinh_KhuSanh.asset", "Water", 45f, 1, 1);
        AssertLootTableRule("Assets/Khoa/Code/LootTableS/MacDinh_KhuSanh.asset", "Ammo12Gauge", 15f, 5, 5);

        // The tutorial intentionally authors a 12-round shotgun stack.  The
        // ordinary/default policy must not rewrite this authored exception.
        AssertLootTableRule("Assets/Resources/Tutorial/TutorialKitchenLootTable.asset",
            "Ammo12Gauge", 100f, 12, 12);
    }

    private static void AssertStarterFixedAmount(Array loadout, string itemId, int expectedAmount)
    {
        object matchingEntry = null;
        foreach (object entry in loadout)
        {
            string candidateId = (string)entry.GetType().GetField("ItemId").GetValue(entry);
            if (string.Equals(candidateId, itemId, StringComparison.OrdinalIgnoreCase))
            {
                matchingEntry = entry;
                break;
            }
        }

        Assert.That(matchingEntry, Is.Not.Null, $"Starter loadout must contain '{itemId}'.");
        int amount = (int)matchingEntry.GetType().GetField("Amount").GetValue(matchingEntry);
        Assert.That(amount, Is.EqualTo(expectedAmount), $"Starter amount for '{itemId}'.");
    }

    private static void AssertLootTableRule(string assetPath, string itemName,
        float expectedChance, int expectedMinimum, int expectedMaximum)
    {
        UnityEngine.Object tableAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
        Assert.That(tableAsset, Is.Not.Null, assetPath);

        SerializedObject serializedTable = new SerializedObject(tableAsset);
        SerializedProperty rules = serializedTable.FindProperty("lootRules");
        Assert.That(rules, Is.Not.Null, $"Missing lootRules on {assetPath}.");

        for (int i = 0; i < rules.arraySize; i++)
        {
            SerializedProperty rule = rules.GetArrayElementAtIndex(i);
            UnityEngine.Object item = rule.FindPropertyRelative("itemPrefab")?.objectReferenceValue;
            if (item == null || !string.Equals(item.name, itemName, StringComparison.OrdinalIgnoreCase)) continue;

            Assert.That(rule.FindPropertyRelative("dropChance").floatValue, Is.EqualTo(expectedChance),
                $"Drop chance for {itemName} in {assetPath}.");
            Assert.That(rule.FindPropertyRelative("minAmount").intValue, Is.EqualTo(expectedMinimum),
                $"Minimum amount for {itemName} in {assetPath}.");
            Assert.That(rule.FindPropertyRelative("maxAmount").intValue, Is.EqualTo(expectedMaximum),
                $"Maximum amount for {itemName} in {assetPath}.");
            return;
        }

        Assert.Fail($"Loot table '{assetPath}' must contain item '{itemName}'.");
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
