using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class VisibilityAndZombieRegressionPlayModeTests
{
    [Test]
    public void StartingWeaponPlacement_IsVerifiedAndIdempotent()
    {
        Type inventoryType = Type.GetType("InventorySystem, Assembly-CSharp");
        Assert.That(inventoryType, Is.Not.Null);
        GameObject player = new GameObject("Starter loadout regression player");
        Component inventory = player.AddComponent(inventoryType);
        UnityEngine.Object ak47 = Resources.Load("Items/AK47");
        Assert.That(ak47, Is.Not.Null);

        var slots = (System.Collections.IList)inventoryType.GetField("slots").GetValue(inventory);
        object backpackSlot = slots[5];
        backpackSlot.GetType().GetField("item").SetValue(backpackSlot, ak47);
        backpackSlot.GetType().GetField("amount").SetValue(backpackSlot, 1);

        MethodInfo place = inventoryType.GetMethod("PlaceStartingWeaponInHotbar",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That((bool)place.Invoke(inventory, new object[] { ak47 }), Is.True);
        Assert.That((bool)place.Invoke(inventory, new object[] { ak47 }), Is.True,
            "A retry must recognize the weapon already placed instead of duplicating it.");

        int akCount = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            object slot = slots[i];
            if (slot == null) continue;
            UnityEngine.Object item = slot.GetType().GetField("item").GetValue(slot) as UnityEngine.Object;
            int amount = (int)slot.GetType().GetField("amount").GetValue(slot);
            if (item != null && item.name == "AK47") akCount += amount;
        }
        Assert.That(akCount, Is.EqualTo(1));
        object firstHotbarSlot = slots[0];
        Assert.That(firstHotbarSlot.GetType().GetField("item").GetValue(firstHotbarSlot), Is.SameAs(ak47),
            "An existing starter weapon in storage must be moved into Hotbar instead of being accepted silently.");
        UnityEngine.Object.DestroyImmediate(player);
    }

    [UnityTest]
    public IEnumerator IndoorOcclusion_IgnoresExternalFence_AndStopsAtThisBuildingsWall()
    {
        Shader fogShader = Shader.Find("ProjectZomboid/FogVisionOverlay");
        Assert.That(fogShader, Is.Not.Null);
        Assert.That(fogShader.isSupported, Is.True);
        GameObject cameraObject = new GameObject("Fog regression camera", typeof(Camera));
        Type fogType = Type.GetType("FogVisionController, Assembly-CSharp");
        Type roofType = Type.GetType("RoofVisibility, Assembly-CSharp");
        Assert.That(fogType, Is.Not.Null);
        Assert.That(roofType, Is.Not.Null);
        Component fog = cameraObject.AddComponent(fogType);

        GameObject building = new GameObject("Regression building");
        building.AddComponent(roofType);
        GameObject indoorObject = new GameObject("Indoor trigger");
        indoorObject.transform.SetParent(building.transform);
        BoxCollider2D indoor = indoorObject.AddComponent<BoxCollider2D>();
        indoor.isTrigger = true;
        indoor.size = new Vector2(8f, 8f);

        GameObject wallObject = new GameObject("Building wall");
        wallObject.layer = LayerMask.NameToLayer("Obstacle");
        // Hospital/School walls are authored in a separate hierarchy branch
        // from their roof trigger. Geometry must still classify this as a wall.
        wallObject.transform.position = new Vector2(2f, 0f);
        BoxCollider2D wall = wallObject.AddComponent<BoxCollider2D>();
        wall.size = new Vector2(0.2f, 3f);

        GameObject fenceObject = new GameObject("Unrelated outdoor fence");
        fenceObject.layer = LayerMask.NameToLayer("Obstacle");
        fenceObject.transform.position = new Vector2(-4.8f, 0f);
        BoxCollider2D fence = fenceObject.AddComponent<BoxCollider2D>();
        fence.size = new Vector2(0.2f, 3f);

        Physics2D.SyncTransforms();
        MethodInfo updateOcclusion = fogType.GetMethod("UpdateIndoorOcclusion",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(updateOcclusion, Is.Not.Null);
        bool active = (bool)updateOcclusion.Invoke(fog,
            new object[] { indoor, Vector2.zero, 10f });
        Assert.That(active, Is.True);

        FieldInfo distancesField = fogType.GetField("indoorOcclusionDistances",
            BindingFlags.NonPublic | BindingFlags.Instance);
        float[] distances = (float[])distancesField.GetValue(fog);
        Assert.That(distances[0], Is.InRange(1.75f, 2.05f),
            "The +X ray must stop at a sibling wall inside the current building volume.");
        Assert.That(distances[90], Is.EqualTo(10f).Within(0.05f),
            "The -X ray must ignore an unrelated fence reached after leaving the indoor volume.");

        UnityEngine.Object.Destroy(cameraObject);
        UnityEngine.Object.Destroy(building);
        UnityEngine.Object.Destroy(fenceObject);
        yield return null;
    }

    [UnityTest]
    public IEnumerator ZombieMovementSweep_StopsBothBrainsBeforeStaticWall()
    {
        foreach (string typeName in new[] { "ZOmbieAI_Khoa", "ZombieAIKhoaRebuilt" })
        {
            GameObject zombie = new GameObject(typeName + " regression body");
            zombie.layer = LayerMask.NameToLayer("Enemy");
            Rigidbody2D body = zombie.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            CapsuleCollider2D capsule = zombie.AddComponent<CapsuleCollider2D>();
            capsule.size = new Vector2(0.2f, 0.4f);
            zombie.AddComponent<Animator>();

            Type zombieType = Type.GetType(typeName + ", Assembly-CSharp");
            Assert.That(zombieType, Is.Not.Null, typeName);
            Behaviour brain = zombie.AddComponent(zombieType) as Behaviour;
            brain.enabled = false;

            ContactFilter2D obstacleFilter = new ContactFilter2D
            {
                useLayerMask = true,
                useTriggers = false
            };
            obstacleFilter.SetLayerMask(1 << LayerMask.NameToLayer("Obstacle"));
            SetPrivateField(brain, "obstacleMovementFilter", obstacleFilter);

            GameObject wallObject = new GameObject(typeName + " static wall");
            wallObject.layer = LayerMask.NameToLayer("Obstacle");
            wallObject.transform.position = new Vector2(0.5f, 0f);
            BoxCollider2D wall = wallObject.AddComponent<BoxCollider2D>();
            wall.size = new Vector2(0.1f, 2f);
            Physics2D.SyncTransforms();

            MethodInfo sweep = zombieType.GetMethod("MoveWithObstacleSweep",
                BindingFlags.NonPublic | BindingFlags.Instance);
            float moved = (float)sweep.Invoke(brain, new object[] { Vector2.right });
            yield return new WaitForFixedUpdate();

            Assert.That(moved, Is.LessThan(0.4f), typeName);
            Assert.That(body.position.x, Is.LessThan(0.4f),
                typeName + " must not tunnel through an Obstacle wall.");

            UnityEngine.Object.Destroy(zombie);
            UnityEngine.Object.Destroy(wallObject);
            yield return null;
        }
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(target, value);
    }
}
