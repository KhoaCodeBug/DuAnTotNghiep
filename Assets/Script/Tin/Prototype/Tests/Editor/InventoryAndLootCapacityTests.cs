using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class InventoryAndLootCapacityTests
{
    [Test]
    public void PlayerInventory_AlwaysUsesTwentyStableSlots()
    {
        Type inventoryType = Type.GetType("InventorySystem, Assembly-CSharp");
        Type itemType = Type.GetType("ItemData, Assembly-CSharp");
        Assert.That(inventoryType, Is.Not.Null);
        Assert.That(itemType, Is.Not.Null);

        GameObject host = new GameObject("Inventory Capacity Test");
        ScriptableObject item = ScriptableObject.CreateInstance(itemType);
        try
        {
            Component inventory = host.AddComponent(inventoryType);
            InvokePrivate(inventory, "Awake");
            FieldInfo maxSlots = inventoryType.GetField("maxSlots");
            FieldInfo slotsField = inventoryType.GetField("slots");
            IList slots = slotsField?.GetValue(inventory) as IList;
            Assert.That(maxSlots, Is.Not.Null);
            Assert.That(slots, Is.Not.Null);
            Assert.That((int)maxSlots.GetValue(inventory), Is.EqualTo(20));
            Assert.That(slots.Count, Is.GreaterThanOrEqualTo(20));

            inventoryType.GetMethod("SetMaxSlots")?.Invoke(inventory, new object[] { 15 });
            Assert.That((int)maxSlots.GetValue(inventory), Is.EqualTo(20),
                "Legacy backpack-level calls must not shrink the fixed inventory.");

            SetField(item, "itemName", "StableSlotTestItem");
            SetField(item, "maxStack", 1);
            SetField(item, "isStackable", false);
            object slot = slots[12];
            SetField(slot, "item", item);
            SetField(slot, "amount", 1);

            FieldInfo syncingField = inventoryType.GetField("isSyncing",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(syncingField, Is.Not.Null);
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
    public void LootContainer_DefaultCapacityIsTwentyAndRejectsOverflow()
    {
        Type containerType = Type.GetType("LootContainer, Assembly-CSharp");
        Type itemType = Type.GetType("ItemData, Assembly-CSharp");
        Assert.That(containerType, Is.Not.Null);
        Assert.That(itemType, Is.Not.Null);

        GameObject host = new GameObject("Loot Capacity Test");
        ScriptableObject item = ScriptableObject.CreateInstance(itemType);
        try
        {
            host.AddComponent<BoxCollider2D>();
            host.AddComponent<SpriteRenderer>();
            Component container = host.AddComponent(containerType);
            Assert.That(container, Is.Not.Null);
            InvokePrivate(container, "Awake");
            PropertyInfo maxSlots = containerType.GetProperty("MaxSlots");
            IList items = containerType.GetField("itemsInContainer")?.GetValue(container) as IList;
            Assert.That(maxSlots, Is.Not.Null);
            Assert.That(items, Is.Not.Null);
            Assert.That((int)maxSlots.GetValue(container), Is.EqualTo(20));

            SetField(item, "itemName", "ContainerCapacityTestItem");
            SetField(item, "maxStack", 1);
            SetField(item, "isStackable", false);

            MethodInfo store = containerType.GetMethod("StoreItemLocal",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo canStore = containerType.GetMethod("CanStoreItem",
                BindingFlags.Instance | BindingFlags.Public, null, new[] { itemType, typeof(int) }, null);
            Assert.That(store, Is.Not.Null);
            Assert.That(canStore, Is.Not.Null);
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

    private static object ReadField(object target, string name) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.GetValue(target);

    private static void InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Missing method '{methodName}' on {target.GetType().Name}.");
        method.Invoke(target, null);
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing field '{name}' on {target.GetType().Name}.");
        field.SetValue(target, value);
    }
}
