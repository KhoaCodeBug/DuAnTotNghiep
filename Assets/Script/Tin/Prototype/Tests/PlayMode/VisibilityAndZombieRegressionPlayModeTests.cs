using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class VisibilityAndZombieRegressionPlayModeTests
{
    private bool needsRuntimeSceneCleanup;

    [UnityTearDown]
    public IEnumerator CleanupRuntimeVisionScene()
    {
        if (!needsRuntimeSceneCleanup) yield break;
        needsRuntimeSceneCleanup = false;
        MethodInfo shutdown = typeof(MainMenuToMilitaryQuestFlowTests).GetMethod(
            "ShutdownExistingRunners", BindingFlags.NonPublic | BindingFlags.Static);
        yield return (IEnumerator)shutdown.Invoke(null, null);
        // Do not leak Main's colliders/roof volumes into the synthetic physics tests.
        // TearDown also runs if the runtime QA fails before its normal end.
        var load = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("MainMenu");
        while (!load.isDone) yield return null;
    }

    [Test]
    public void StartingWeaponPlacement_IsVerifiedAndIdempotent()
    {
        Type inventoryType = Type.GetType("InventorySystem, Assembly-CSharp");
        Assert.That(inventoryType, Is.Not.Null);
        GameObject player = new GameObject("Starter loadout regression player");
        Component inventory = player.AddComponent(inventoryType);
        UnityEngine.Object ak47 = Resources.Load("Items/AK47");
        UnityEngine.Object s12k = Resources.Load("Items/S12K");
        Assert.That(ak47, Is.Not.Null);
        Assert.That(s12k, Is.Not.Null);

        MethodInfo place = inventoryType.GetMethod("PlaceStartingWeaponInHotbar",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That((bool)place.Invoke(inventory, new object[] { ak47 }), Is.True);
        Assert.That((bool)place.Invoke(inventory, new object[] { ak47 }), Is.True,
            "A retry must recognize the weapon already placed instead of duplicating it.");

        var slots = (System.Collections.IList)inventoryType.GetField("slots").GetValue(inventory);
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
        UnityEngine.Object.DestroyImmediate(player);

        // Test S12K in a separate inventory instance
        GameObject player2 = new GameObject("Starter loadout regression player 2");
        Component inventory2 = player2.AddComponent(inventoryType);
        Assert.That((bool)place.Invoke(inventory2, new object[] { s12k }), Is.True);
        Assert.That((bool)place.Invoke(inventory2, new object[] { s12k }), Is.True);
        var slots2 = (System.Collections.IList)inventoryType.GetField("slots").GetValue(inventory2);
        int s12kCount = 0;
        for (int i = 0; i < slots2.Count; i++)
        {
            object slot = slots2[i];
            if (slot == null) continue;
            UnityEngine.Object item = slot.GetType().GetField("item").GetValue(slot) as UnityEngine.Object;
            int amount = (int)slot.GetType().GetField("amount").GetValue(slot);
            if (item != null && item.name == "S12K") s12kCount += amount;
        }
        Assert.That(s12kCount, Is.EqualTo(1));
        UnityEngine.Object.DestroyImmediate(player2);
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
        UnityEngine.Object.Destroy(wallObject);
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

    [UnityTest]
    public IEnumerator ZombieVisibility_SplitRoofTriggerKeepsNearbyIndoorZombieVisible()
    {
        GameObject indoorObject = new GameObject("Split school roof trigger");
        PolygonCollider2D indoor = indoorObject.AddComponent<PolygonCollider2D>();
        indoor.isTrigger = true;
        indoor.pathCount = 2;
        indoor.SetPath(0, new[]
        {
            new Vector2(-3f, -2f), new Vector2(-1f, -2f),
            new Vector2(-1f, 2f), new Vector2(-3f, 2f)
        });
        indoor.SetPath(1, new[]
        {
            new Vector2(1f, -2f), new Vector2(3f, -2f),
            new Vector2(3f, 2f), new Vector2(1f, 2f)
        });

        Type visionType = Type.GetType("PlayerVision, Assembly-CSharp");
        Type roofDetectorType = Type.GetType("RoofDetector, Assembly-CSharp");
        Assert.That(visionType, Is.Not.Null);
        Assert.That(roofDetectorType, Is.Not.Null);
        GameObject player = new GameObject("Indoor visibility regression player");
        Component vision = player.AddComponent(visionType);
        Component roofDetector = player.AddComponent(roofDetectorType);
        SetPrivateField(roofDetector, "currentIndoorCollider", indoor);
        SetPrivateField(vision, "roofDetector", roofDetector);

        ContactFilter2D zombieFilter = new ContactFilter2D
        {
            useLayerMask = true,
            useTriggers = false
        };
        zombieFilter.SetLayerMask(1 << LayerMask.NameToLayer("Enemy"));
        SetPrivateField(vision, "zombieFilter", zombieFilter);
        ContactFilter2D obstacleFilter = new ContactFilter2D
        {
            useLayerMask = true,
            useTriggers = false
        };
        obstacleFilter.SetLayerMask(1 << LayerMask.NameToLayer("Obstacle"));
        SetPrivateField(vision, "obstacleFilter", obstacleFilter);

        GameObject zombie = new GameObject("Nearby indoor zombie");
        zombie.layer = LayerMask.NameToLayer("Enemy");
        zombie.transform.position = new Vector2(0f, 1f);
        SpriteRenderer zombieRenderer = zombie.AddComponent<SpriteRenderer>();
        zombieRenderer.color = Color.white;
        zombieRenderer.enabled = false;
        zombie.AddComponent<CapsuleCollider2D>();

        Physics2D.SyncTransforms();
        Assert.That(indoor.OverlapPoint(player.transform.position), Is.False,
            "The regression requires the player pivot to sit between split polygon islands.");
        Assert.That(indoor.OverlapPoint(zombie.transform.position), Is.False,
            "The nearby zombie must reproduce the same false-negative containment.");

        MethodInfo updateVisibility = visionType.GetMethod("UpdateZombieVisibility",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(updateVisibility, Is.Not.Null);
        updateVisibility.Invoke(vision, new object[] { 140f });

        Assert.That(zombieRenderer.enabled, Is.True,
            "A nearby zombie inside the active roof bounds must not become invisible solely because the split polygon rejects OverlapPoint.");
        Assert.That(zombieRenderer.color.a, Is.GreaterThan(0f));

        UnityEngine.Object.Destroy(player);
        UnityEngine.Object.Destroy(zombie);
        UnityEngine.Object.Destroy(indoorObject);
        yield return null;
    }

    [UnityTest]
    [Timeout(180000)]
    public IEnumerator FogRollback_SoloIndoorOutdoorTransitionDoesNotKeepOutdoorWallMask()
    {
        needsRuntimeSceneCleanup = true;
        MethodInfo shutdown = typeof(MainMenuToMilitaryQuestFlowTests).GetMethod(
            "ShutdownExistingRunners", BindingFlags.NonPublic | BindingFlags.Static);
        yield return (IEnumerator)shutdown.Invoke(null, null);
        int originalLanguage = PlayerPrefs.GetInt("GameLanguage", 0);
        try
        {
            PlayerPrefs.SetInt("GameLanguage", 0);
            var load = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("MainMenu");
            while (!load.isDone) yield return null;
            yield return PressVisionQaButton("SOLO");
            yield return PressVisionQaButton("MEDIUM");
            yield return PressVisionQaButton("ENTER THE DEAD ZONE");

            Type movementType = Type.GetType("PlayerMovement, Assembly-CSharp");
            Type readinessType = Type.GetType("GameplayReadinessCoordinator, Assembly-CSharp");
            Type visionType = Type.GetType("PlayerVision, Assembly-CSharp");
            Type fogType = Type.GetType("FogVisionController, Assembly-CSharp");
            Component player = null;
            float deadline = Time.realtimeSinceStartup + 90f;
            while (Time.realtimeSinceStartup < deadline)
            {
                player = movementType.GetField("LocalPlayerInstance").GetValue(null) as Component;
                if (player != null && (bool)readinessType.GetProperty("IsReleasedToGameplay").GetValue(null)) break;
                yield return null;
            }
            Assert.That(player, Is.Not.Null);
            Assert.That((bool)readinessType.GetProperty("IsReleasedToGameplay").GetValue(null), Is.True,
                "The production Solo loading gate must release before visual QA.");
            Component vision = player.GetComponent(visionType);
            Type inventoryType = Type.GetType("InventorySystem, Assembly-CSharp");
            Component inventory = player.GetComponent(inventoryType);
            var slots = (System.Collections.IList)inventoryType.GetField("slots").GetValue(inventory);
            int flashlightSlot = FindVisionQaFlashlight(slots);
            Assert.That(flashlightSlot, Is.GreaterThanOrEqualTo(0), "Medium must grant its production flashlight.");
            if (flashlightSlot >= 5)
                inventoryType.GetMethod("EquipFlashlightToHotbar", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(inventory, new object[] { flashlightSlot });
            flashlightSlot = FindVisionQaFlashlight(slots);
            Assert.That(flashlightSlot, Is.InRange(0, 4));
            Type flashlightType = Type.GetType("FlashlightController, Assembly-CSharp");
            Component flashlight = player.GetComponent(flashlightType);
            MethodInfo toggleFlashlight = flashlightType.GetMethod("TryToggleFromHotbar");
            yield return new WaitForSecondsRealtime(0.3f);
            Type militaryType = Type.GetType("MilitaryBaseQuestManager, Assembly-CSharp");
            MethodInfo teleport = militaryType.GetMethod("TeleportPlayer", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(teleport, Is.Not.Null);
            Component fog = fogType.GetProperty("Instance").GetValue(null) as Component;
            Assert.That(fog, Is.Not.Null);
            Type cameraControllerType = Type.GetType("PZ_CameraController, Assembly-CSharp");
            Component cameraController = cameraControllerType.GetProperty("Instance").GetValue(null) as Component;
            SetPrivateField(cameraController, "targetZoom", 5f);
            cameraController.GetComponentInChildren<Camera>().orthographicSize = 5f;
            Material material = (Material)fogType.GetField("overlayMaterial",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(fog);
            Assert.That(material.shader.isSupported, Is.True);
            string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,
                "../QA_Artifacts/VisionRollback_Runtime"));
            System.IO.Directory.CreateDirectory(folder);
            var evidence = new System.Text.StringBuilder();
            Vector2[] positions =
            {
                new Vector2(-62.29f, 30.77f),
                new Vector2(-46.646f, 16.413f),
                new Vector2(-62.29f, 30.77f),
                new Vector2(-49.584f, 37.427f),
                new Vector2(11.36f, 49.93f),
                new Vector2(11.36f, 37.5f)
            };
            string[] names = { "00_outdoor", "01_hospital_large", "02_outdoor_after_hospital",
                "03_hospital_small", "04_school", "05_outdoor_after_school" };
            bool[] indoorExpected = { false, true, false, true, true, false };
            for (int i = 0; i < positions.Length; i++)
            {
                if (i == 5)
                {
                    // Normal school progression forbids exiting before all three clues.
                    // Satisfy that prerequisite in this runtime-only fixture so a quest
                    // return teleport cannot masquerade as a stale indoor visibility mask.
                    object military = militaryType.GetProperty("Instance").GetValue(null);
                    Type storyRules = null;
                    foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        storyRules = assembly.GetType("MilitaryStoryFlowRules");
                        if (storyRules != null) break;
                    }
                    Assert.That(storyRules, Is.Not.Null);
                    object completeMask = storyRules.GetProperty("CompleteClueMask").GetValue(null);
                    militaryType.GetProperty("SchoolClueMask").SetValue(military, completeMask);
                    evidence.AppendLine("QA fixture: school clues complete before testing school exit.");
                }
                // Use the production Fusion teleport path, including interpolation reset.
                teleport.Invoke(null, new object[] { player, positions[i] });
                Physics2D.SyncTransforms();
                yield return new WaitForSecondsRealtime(1.2f);
                Collider2D indoor = visionType.GetProperty("ActiveIndoorCollider").GetValue(vision) as Collider2D;
                evidence.AppendLine(names[i] + " requested=" + positions[i] + " actual=" + player.transform.position +
                    " indoor=" + (indoor != null ? indoor.name : "none") +
                    " mask=" + material.GetFloat("_IndoorOcclusionActive"));
                System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "RUNTIME.txt"), evidence.ToString());
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder, names[i] + ".png"));
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.That(Vector2.Distance(player.transform.position, positions[i]), Is.LessThan(0.3f),
                    names[i] + ": QA must sample the requested position, not stale Fusion interpolation.");
                Assert.That(indoor != null, Is.EqualTo(indoorExpected[i]), names[i]);
                Assert.That(material.GetFloat("_IndoorActive"), Is.EqualTo(indoorExpected[i] ? 1f : 0f), names[i]);
                Assert.That(material.GetFloat("_IndoorOcclusionActive"),
                    Is.EqualTo(indoorExpected[i] ? 1f : 0f),
                    names[i] + ": wall masking must only run indoors and must clear on exit.");
                Assert.That(material.GetFloat("_FlashlightActive"), Is.Zero, names[i] + ": flashlight off");
                Assert.That((bool)toggleFlashlight.Invoke(flashlight, new object[] { flashlightSlot }), Is.True);
                yield return new WaitForSecondsRealtime(0.4f);
                Assert.That(material.GetFloat("_FlashlightActive"), Is.EqualTo(1f), names[i] + ": flashlight on");
                Assert.That(material.GetFloat("_IndoorOcclusionActive"),
                    Is.EqualTo(indoorExpected[i] ? 1f : 0f), names[i] + ": flashlight must preserve building scope");
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder, names[i] + "_flashlight.png"));
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.That((bool)toggleFlashlight.Invoke(flashlight, new object[] { flashlightSlot }), Is.True);
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.That(material.GetFloat("_FlashlightActive"), Is.Zero, names[i] + ": flashlight off again");
                evidence.AppendLine(names[i] + " flashlight off/on/off passed; indoor scope unchanged.");
            }
            System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "RUNTIME.txt"), evidence.ToString());
        }
        finally
        {
            PlayerPrefs.SetInt("GameLanguage", originalLanguage);
        }
    }

    private static IEnumerator PressVisionQaButton(string label)
    {
        float deadline = Time.realtimeSinceStartup + 20f;
        while (Time.realtimeSinceStartup < deadline)
        {
            foreach (var button in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                TMPro.TMP_Text text = button.GetComponentInChildren<TMPro.TMP_Text>();
                if (button.interactable && text != null && text.text.Trim() == label)
                {
                    button.onClick.Invoke();
                    yield return null;
                    yield break;
                }
            }
            yield return null;
        }
        Assert.Fail("Could not find active Solo flow button: " + label);
    }

    private static int FindVisionQaFlashlight(System.Collections.IList slots)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            object slot = slots[i];
            if (slot == null) continue;
            UnityEngine.Object item = slot.GetType().GetField("item").GetValue(slot) as UnityEngine.Object;
            if (item != null && item.name == "Flashlight" && (int)slot.GetType().GetField("amount").GetValue(slot) > 0)
                return i;
        }
        return -1;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(target, value);
    }
}
