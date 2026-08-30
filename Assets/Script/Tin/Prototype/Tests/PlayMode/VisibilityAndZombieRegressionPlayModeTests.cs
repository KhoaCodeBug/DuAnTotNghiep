using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public sealed class VisibilityAndZombieRegressionPlayModeTests
{
    public static string ActiveQaDirectory { get; set; }
    private static RenderTexture visualQaCaptureTexture;
    private static RenderTexture visualQaPreviousTexture;
    private static Camera visualQaCamera;
    private static float visualQaPreviousAspect;

    private static string ComputeSha256(byte[] data)
    {
        using (var sha = System.Security.Cryptography.SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(data);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hash.Length; i++)
                sb.Append(hash[i].ToString("x2"));
            return sb.ToString();
        }
    }

    [Test]
    public void PlayerVision_IndoorFlashlightUsesOcclusionAwarePhysicalLightPolicy()
    {
        Type visionType = Type.GetType("PlayerVision, Assembly-CSharp");
        Assert.That(visionType, Is.Not.Null);

        MethodInfo policy = visionType.GetMethod("ShouldRenderPhysicalLight",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(policy, Is.Not.Null,
            "PlayerVision must centralize the indoor flashlight occlusion policy instead of letting an unshadowed Light2D leak through walls.");

        bool indoorFlashlight = (bool)policy.Invoke(null, new object[]
            { true, false, true, true });
        bool indoorAmbient = (bool)policy.Invoke(null, new object[]
            { true, false, true, false });
        bool outdoorFlashlight = (bool)policy.Invoke(null, new object[]
            { true, false, false, true });
        bool nonTarget = (bool)policy.Invoke(null, new object[]
            { false, false, false, true });

        Assert.That(indoorFlashlight, Is.False,
            "An unshadowed physical flashlight must be suppressed indoors; the FOW shader is the occlusion-aware source there.");
        Assert.That(indoorAmbient, Is.True,
            "The small indoor ambient light may remain available when the flashlight is off.");
        Assert.That(outdoorFlashlight, Is.True,
            "The physical flashlight must remain available outdoors.");
        Assert.That(nonTarget, Is.False,
            "A non-camera target must never render its local physical light.");
    }

    [Test]
    public void RoofDetector_OnlyAcceptsAuthoredIndoorAreaTriggers()
    {
        Type detectorType = Type.GetType("RoofDetector, Assembly-CSharp");
        Type roofType = Type.GetType("RoofVisibility, Assembly-CSharp");
        Type indoorType = Type.GetType("IndoorVisionArea, Assembly-CSharp");
        Assert.That(detectorType, Is.Not.Null);
        Assert.That(roofType, Is.Not.Null);
        Assert.That(indoorType, Is.Not.Null);

        MethodInfo isCandidate = detectorType.GetMethod("IsAuthoredIndoorAreaCandidate",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(isCandidate, Is.Not.Null,
            "RoofDetector must distinguish the authored room trigger from child interaction triggers.");

        GameObject building = new GameObject("Roof detector regression building");
        building.AddComponent(roofType);

        GameObject bed = new GameObject("Bed trigger");
        bed.transform.SetParent(building.transform);
        BoxCollider2D bedCollider = bed.AddComponent<BoxCollider2D>();
        bedCollider.isTrigger = true;

        GameObject room = new GameObject("nocnha regression room", typeof(Tilemap));
        room.transform.SetParent(building.transform);
        BoxCollider2D roomCollider = room.AddComponent<BoxCollider2D>();
        roomCollider.isTrigger = true;

        GameObject marked = new GameObject("Marked indoor room");
        marked.transform.SetParent(building.transform);
        marked.AddComponent(indoorType);
        BoxCollider2D markedCollider = marked.AddComponent<BoxCollider2D>();
        markedCollider.isTrigger = true;

        Assert.That((bool)isCandidate.Invoke(null, new object[] { bedCollider }), Is.False,
            "A bed/loot trigger under a roof must not replace the room trigger.");
        Assert.That((bool)isCandidate.Invoke(null, new object[] { roomCollider }), Is.True);
        Assert.That((bool)isCandidate.Invoke(null, new object[] { markedCollider }), Is.True);

        UnityEngine.Object.DestroyImmediate(building);
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
        wallObject.transform.SetParent(building.transform);
        wallObject.transform.position = new Vector2(2f, 0f);
        BoxCollider2D wall = wallObject.AddComponent<BoxCollider2D>();
        wall.size = new Vector2(0.2f, 3f);

        GameObject fenceObject = new GameObject("Unrelated outdoor fence");
        fenceObject.layer = LayerMask.NameToLayer("Obstacle");
        fenceObject.transform.position = new Vector2(0.8f, 0f);
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
            "The +X ray must skip the nearer external fence and stop at this building's wall.");

        UnityEngine.Object.Destroy(cameraObject);
        UnityEngine.Object.Destroy(building);
        UnityEngine.Object.Destroy(fenceObject);
        yield return null;
    }

    [UnityTest]
    public IEnumerator OutdoorFowLos_StopsAtWall_AndSkipsVisionPassThroughGate()
    {
        Type fogType = Type.GetType("FogVisionController, Assembly-CSharp");
        Assert.That(fogType, Is.Not.Null);

        GameObject cameraObject = new GameObject("Outdoor FOW LOS regression camera", typeof(Camera));
        Component fog = cameraObject.AddComponent(fogType);

        int obstacleLayer = LayerMask.NameToLayer("Obstacle");
        Assert.That(obstacleLayer, Is.GreaterThanOrEqualTo(0));

        GameObject gateObject = new GameObject("Vision pass-through gate");
        gateObject.layer = obstacleLayer;
        gateObject.transform.position = new Vector2(0.8f, 0f);
        BoxCollider2D gateCollider = gateObject.AddComponent<BoxCollider2D>();
        gateCollider.size = new Vector2(0.2f, 2f);
        Type visionPassThroughType = Type.GetType("MilitaryGateVisionPassThrough, Assembly-CSharp");
        Assert.That(visionPassThroughType, Is.Not.Null);
        gateObject.AddComponent(visionPassThroughType);

        GameObject wallObject = new GameObject("Outdoor LOS wall");
        wallObject.layer = obstacleLayer;
        wallObject.transform.position = new Vector2(2f, 0f);
        BoxCollider2D wall = wallObject.AddComponent<BoxCollider2D>();
        wall.size = new Vector2(0.2f, 3f);

        Physics2D.SyncTransforms();
        MethodInfo updateLos = fogType.GetMethod("UpdateOutdoorLineOfSight",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(updateLos, Is.Not.Null,
            "FOW must expose a world LOS fan that uses the same obstacle semantics as PlayerVision.");

        bool active = (bool)updateLos.Invoke(fog, new object[] { Vector2.zero, 10f });
        Assert.That(active, Is.True);

        FieldInfo distancesField = fogType.GetField("lineOfSightDistances",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(distancesField, Is.Not.Null);
        float[] distances = (float[])distancesField.GetValue(fog);
        Assert.That(distances[0], Is.InRange(1.75f, 2.05f),
            "The +X world LOS ray must skip a MilitaryGateVisionPassThrough and stop at the real wall.");

        UnityEngine.Object.Destroy(cameraObject);
        UnityEngine.Object.Destroy(gateObject);
        UnityEngine.Object.Destroy(wallObject);
        yield return null;
    }

    [UnityTest]
    public IEnumerator IndoorOcclusion_OnlyExposesVerifiedOpenDoorPortal()
    {
        Shader fogShader = Shader.Find("ProjectZomboid/FogVisionOverlay");
        Assert.That(fogShader, Is.Not.Null);

        Type fogType = Type.GetType("FogVisionController, Assembly-CSharp");
        Type roofType = Type.GetType("RoofVisibility, Assembly-CSharp");
        Assert.That(fogType, Is.Not.Null);
        Assert.That(roofType, Is.Not.Null);

        GameObject cameraObject = new GameObject("Fog portal regression camera", typeof(Camera));
        Component fog = cameraObject.AddComponent(fogType);

        GameObject buildingFamily = new GameObject("Portal regression building family");
        GameObject building = new GameObject("Portal regression building");
        building.transform.SetParent(buildingFamily.transform);

        GameObject indoorObject = new GameObject("Portal regression indoor trigger");
        indoorObject.transform.SetParent(building.transform);
        indoorObject.AddComponent(roofType);
        BoxCollider2D indoor = indoorObject.AddComponent<BoxCollider2D>();
        indoor.isTrigger = true;
        indoor.size = new Vector2(8f, 8f);

        // The two wall segments leave one authored doorway at the centre.
        CreatePortalRegressionWall(building.transform, "Upper wall", new Vector2(2f, 1.55f));
        CreatePortalRegressionWall(building.transform, "Lower wall", new Vector2(2f, -1.55f));

        GameObject doorObject = new GameObject("DoorBlocker");
        doorObject.layer = LayerMask.NameToLayer("Obstacle");
        // Match Main: the door blocker is authored beside the fixed map group,
        // while the indoor trigger resolves to the smaller building structure.
        doorObject.transform.SetParent(buildingFamily.transform);
        doorObject.transform.position = Vector2.zero;
        BoxCollider2D doorBlocker = doorObject.AddComponent<BoxCollider2D>();
        doorBlocker.offset = new Vector2(2f, 0f);
        doorBlocker.size = new Vector2(0.2f, 1.1f);

        Physics2D.SyncTransforms();
        MethodInfo updateOcclusion = fogType.GetMethod("UpdateIndoorOcclusion",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(updateOcclusion, Is.Not.Null);

        bool active = (bool)updateOcclusion.Invoke(fog,
            new object[] { indoor, Vector2.zero, 10f });
        Assert.That(active, Is.True);

        FieldInfo portalField = fogType.GetField("indoorPortalDistances",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(portalField, Is.Not.Null,
            "Indoor FOW must publish a separate portal fan; a room envelope is not an open doorway.");
        float[] portals = (float[])portalField.GetValue(fog);
        Assert.That(portals[0], Is.LessThan(0.001f),
            "A closed DoorBlocker must not expose the exterior through the doorway.");
        Assert.That(portals[90], Is.LessThan(0.001f),
            "A closed doorway must not expose the opposite side of the room.");

        doorBlocker.enabled = false;
        Physics2D.SyncTransforms();
        SetPrivateField(fog, "nextIndoorOcclusionUpdate", 0f);
        active = (bool)updateOcclusion.Invoke(fog,
            new object[] { indoor, Vector2.zero, 10f });
        Assert.That(active, Is.True);

        portals = (float[])portalField.GetValue(fog);
        Assert.That(portals[0], Is.GreaterThan(0.5f),
            "Only the verified open doorway may publish an exterior portal distance.");
        Assert.That(portals[5], Is.GreaterThan(0.5f),
            "An open doorway must preserve its authored aperture width after its DoorBlocker is disabled.");
        Assert.That(portals[90], Is.LessThan(0.001f),
            "Opening one doorway must not turn the whole room into an exterior portal.");

        UnityEngine.Object.Destroy(cameraObject);
        UnityEngine.Object.Destroy(buildingFamily);
        yield return null;
    }

    private static void CreatePortalRegressionWall(Transform parent, string name, Vector2 position)
    {
        GameObject wallObject = new GameObject(name);
        wallObject.layer = LayerMask.NameToLayer("Obstacle");
        wallObject.transform.SetParent(parent);
        wallObject.transform.position = position;
        BoxCollider2D wall = wallObject.AddComponent<BoxCollider2D>();
        wall.size = new Vector2(0.2f, 1.8f);
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
    public IEnumerator PlayerVision_DirectionalConeAndLOS_HidesZombieBehindWall_AndRevealsInFront()
    {
        Type visionType = Type.GetType("PlayerVision, Assembly-CSharp");
        Assert.That(visionType, Is.Not.Null);

        GameObject playerObj = new GameObject("Vision Regression Player");
        playerObj.transform.position = new Vector2(200f, 200f);
        Component vision = playerObj.AddComponent(visionType);
        ContactFilter2D obstacleFilter = new ContactFilter2D { useLayerMask = true, useTriggers = false };
        obstacleFilter.SetLayerMask(1 << LayerMask.NameToLayer("Obstacle"));
        SetPrivateField(vision, "obstacleFilter", obstacleFilter);

        GameObject wallObj = new GameObject("LOS Blocker Wall");
        wallObj.layer = LayerMask.NameToLayer("Obstacle");
        wallObj.transform.position = new Vector2(202f, 202f);
        BoxCollider2D wall = wallObj.AddComponent<BoxCollider2D>();
        wall.size = new Vector2(0.5f, 2f);
        Physics2D.SyncTransforms();

        MethodInfo isBlocked = visionType.GetMethod("IsSightBlocked",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(isBlocked, Is.Not.Null);

        // Direct path with no wall in front (200,200) -> (204,200)
        bool directBlocked = (bool)isBlocked.Invoke(vision,
            new object[] { new Vector2(200f, 200f), Vector2.right, 4f });
        Assert.That(directBlocked, Is.False, "Direct line of sight with no wall must NOT be blocked.");

        // Obstructed path across wall (200,200) -> (204, 202)
        Vector2 targetPos = new Vector2(204f, 202f);
        Vector2 origin = new Vector2(200f, 200f);
        Vector2 dir = (targetPos - origin).normalized;
        float dist = (targetPos - origin).magnitude;
        bool wallBlocked = (bool)isBlocked.Invoke(vision,
            new object[] { origin, dir, dist });
        Assert.That(wallBlocked, Is.True, "Line of sight passing through obstacle wall must be blocked.");

        UnityEngine.Object.DestroyImmediate(playerObj);
        UnityEngine.Object.DestroyImmediate(wallObj);
        yield return null;
    }

    [UnityTest]
    [Timeout(180000)]
    public IEnumerator RuntimeVisualQA_CapturesAllLightingScenarios()
    {
        yield return ShutdownExistingRunners();
        PlayerPrefs.SetInt("GameLanguage", 0);
        AsyncOperation loadMenu = SceneManager.LoadSceneAsync(0);
        while (!loadMenu.isDone) yield return null;

        yield return WaitForActiveButton("SOLO", 15f);
        InvokeButton("SOLO");
        yield return WaitForActiveButton("EASY", 10f);
        InvokeButton("EASY");
        yield return WaitForActiveButton("ENTER THE DEAD ZONE", 10f);
        InvokeButton("ENTER THE DEAD ZONE");

        float sceneDeadline = Time.realtimeSinceStartup + 60f;
        while (SceneManager.GetActiveScene().buildIndex != 1 && Time.realtimeSinceStartup < sceneDeadline)
            yield return null;
        Assert.That(SceneManager.GetActiveScene().buildIndex, Is.EqualTo(1), "The SOLO menu flow did not load Main.");

        Type playerType = Type.GetType("PlayerMovement, Assembly-CSharp");
        Type visionType = Type.GetType("PlayerVision, Assembly-CSharp");
        Type flashlightType = Type.GetType("FlashlightController, Assembly-CSharp");
        Type dayNightType = Type.GetType("DayNightManager, Assembly-CSharp");
        Type roofType = Type.GetType("RoofVisibility, Assembly-CSharp");

        FieldInfo localPlayerField = playerType?.GetField("LocalPlayerInstance", BindingFlags.Public | BindingFlags.Static);
        Component player = null;
        float playerDeadline = Time.realtimeSinceStartup + 60f;
        while (Time.realtimeSinceStartup < playerDeadline)
        {
            player = localPlayerField?.GetValue(null) as Component;
            if (player != null) break;
            yield return null;
        }
        Assert.That(player, Is.Not.Null, "Local player did not spawn in Main.");

        Component vision = player.GetComponent(visionType);
        Component flashlight = player.GetComponent(flashlightType);
        Assert.That(vision, Is.Not.Null);

        PropertyInfo isFlashlightActiveProp = flashlightType?.GetProperty("IsFlashlightActive", BindingFlags.Public | BindingFlags.Instance);
        PropertyInfo lookDirProp = playerType?.GetProperty("NetLastLookDir", BindingFlags.Public | BindingFlags.Instance);
        PropertyInfo activeIndoorProp = visionType?.GetProperty("ActiveIndoorCollider",
            BindingFlags.Public | BindingFlags.Instance);
        PropertyInfo dayNightInstProp = dayNightType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        PropertyInfo currentTimeProp = dayNightType?.GetProperty("CurrentTime", BindingFlags.Public | BindingFlags.Instance);
        object dayNight = dayNightInstProp?.GetValue(null);

        Type fogType = Type.GetType("FogVisionController, Assembly-CSharp");
        Component fog = fogType != null ? UnityEngine.Object.FindFirstObjectByType(fogType) as Component : null;
        FieldInfo portalDistancesField = fogType?.GetField("indoorPortalDistances",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Camera cam = Camera.main ?? UnityEngine.Object.FindFirstObjectByType<Camera>();
        Assert.That(cam, Is.Not.Null, "Camera must exist in Main.");
        PrepareVisualQACaptureTarget(cam);

        if (string.IsNullOrEmpty(ActiveQaDirectory) || !Directory.Exists(ActiveQaDirectory))
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            ActiveQaDirectory = Path.Combine(Application.dataPath, "../QA_Artifacts/FOW_LOS_Fix_" + timestamp);
            Directory.CreateDirectory(ActiveQaDirectory);
        }
        Debug.Log("[FOW QA RUNNER] Active QA Directory initialized: " + ActiveQaDirectory);

        yield return new WaitForSeconds(1.5f);
        LogIndoorAreaCatalog();

        // START-TO-HOUSE FLOW: capture the real initial arrival, the approach
        // just outside the nearest authored house, and the first indoor pose.
        // Keep the flashlight disabled so this sequence validates FOW/LOS only.
        Vector3 initialSpawnPosition = player.transform.position;
        if (dayNight != null && currentTimeProp != null) currentTimeProp.SetValue(dayNight, 15f);
        SetFlashlightActive(flashlight, false);
        if (lookDirProp != null) lookDirProp.SetValue(player, Vector2.down);
        cam.transform.position = new Vector3(initialSpawnPosition.x, initialSpawnPosition.y, cam.transform.position.z);
        Physics2D.SyncTransforms();
        yield return new WaitForSeconds(0.8f);
        LogLightingState(player, vision, visionType, activeIndoorProp, isFlashlightActiveProp, "flow-start-spawn");
        LogFogState(fog, fogType, "flow-start-spawn");
        CaptureCameraView(cam, "FOW_FLOW_00_start_spawn.png");

        // The small house at (33, -19.68) is the closest authored house to
        // the story-arrival spawn. This point is outside its north wall.
        Vector3 approachHousePosition = new Vector3(33.83f, -16.80f, 0f);
        MovePlayerForVisualQA(player, approachHousePosition);
        cam.transform.position = new Vector3(approachHousePosition.x, approachHousePosition.y, cam.transform.position.z);
        Physics2D.SyncTransforms();
        yield return new WaitForSeconds(0.8f);
        Assert.That(activeIndoorProp?.GetValue(vision) as Collider2D, Is.Null,
            "The approach capture must remain outside the house trigger.");
        LogLightingState(player, vision, visionType, activeIndoorProp, isFlashlightActiveProp, "flow-outside-house");
        LogFogState(fog, fogType, "flow-outside-house");
        CaptureCameraView(cam, "FOW_FLOW_01_outside_house.png");

        Vector3 firstHouseInteriorPosition = new Vector3(33.00f, -19.68f, 0f);
        MovePlayerForVisualQA(player, firstHouseInteriorPosition);
        cam.transform.position = new Vector3(firstHouseInteriorPosition.x, firstHouseInteriorPosition.y, cam.transform.position.z);
        Physics2D.SyncTransforms();
        yield return new WaitForSeconds(0.8f);
        Assert.That(activeIndoorProp?.GetValue(vision) as Collider2D, Is.Not.Null,
            "The entry capture must be inside the authored house trigger.");
        LogLightingState(player, vision, visionType, activeIndoorProp, isFlashlightActiveProp, "flow-inside-house");
        LogFogState(fog, fogType, "flow-inside-house");
        CaptureCameraView(cam, "FOW_FLOW_02_inside_house.png");

        // ARRIVAL HOUSE REGRESSION: this is the same preserved story-arrival
        // position used when Main.unity is missing ViTriXeChetMay.  The user
        // reported the roof/wall-shaped light leak specifically at this view,
        // so capture it before the generic room cases below.
        Vector3 arrivalHousePos = new Vector3(33.83f, -15.08f, 0f);
        MovePlayerForVisualQA(player, arrivalHousePos);
        cam.transform.position = new Vector3(arrivalHousePos.x, arrivalHousePos.y, cam.transform.position.z);
        Physics2D.SyncTransforms();
        if (dayNight != null && currentTimeProp != null) currentTimeProp.SetValue(dayNight, 23f);
        SetFlashlightActive(flashlight, true);
        if (lookDirProp != null) lookDirProp.SetValue(player, Vector2.right);
        yield return new WaitForSeconds(0.8f);
        LogLightingState(player, vision, visionType, activeIndoorProp, isFlashlightActiveProp, "arrival-on");
        LogFogState(fog, fogType, "arrival-on");
        CaptureCameraView(cam, "FOW_ARRIVAL_house_flashlight_on.png");

        SetFlashlightActive(flashlight, false);
        yield return new WaitForSeconds(0.8f);
        LogLightingState(player, vision, visionType, activeIndoorProp, isFlashlightActiveProp, "arrival-off");
        LogFogState(fog, fogType, "arrival-off");
        CaptureCameraView(cam, "FOW_ARRIVAL_house_flashlight_off.png");

        // Sample the residential interiors around the preserved arrival area.
        // This catches houses whose trigger/root hierarchy differs from the
        // bathroom and hospital regression fixtures.
        if (dayNight != null && currentTimeProp != null) currentTimeProp.SetValue(dayNight, 15f);
        yield return CaptureHouseCandidate(player, vision, flashlight, cam, fog, fogType,
            activeIndoorProp, isFlashlightActiveProp, lookDirProp,
            new Vector3(27.96f, -10.58f, 0f), "small_house_a");
        yield return CaptureHouseCandidate(player, vision, flashlight, cam, fog, fogType,
            activeIndoorProp, isFlashlightActiveProp, lookDirProp,
            new Vector3(33.00f, -19.68f, 0f), "small_house_b");
        yield return CaptureHouseCandidate(player, vision, flashlight, cam, fog, fogType,
            activeIndoorProp, isFlashlightActiveProp, lookDirProp,
            new Vector3(11.35f, -16.59f, 0f), "large_house_a");
        yield return CaptureHouseCandidate(player, vision, flashlight, cam, fog, fogType,
            activeIndoorProp, isFlashlightActiveProp, lookDirProp,
            new Vector3(21.27f, -5.27f, 0f), "large_house_b");

        // 1. WINDOWLESS ROOM (Small House B / Enclosed Room at (33.00, -19.68))
        Vector3 windowlessRoomPos = new Vector3(33.00f, -19.68f, 0f);
        MovePlayerForVisualQA(player, windowlessRoomPos);
        cam.transform.position = new Vector3(windowlessRoomPos.x, windowlessRoomPos.y, cam.transform.position.z);
        Physics2D.SyncTransforms();

        // 1.1 Flashlight OFF at night
        if (dayNight != null && currentTimeProp != null) currentTimeProp.SetValue(dayNight, 23f);
        SetFlashlightActive(flashlight, false);
        if (lookDirProp != null) lookDirProp.SetValue(player, Vector2.up);
        yield return new WaitForSeconds(0.6f);
        LogLightingState(player, vision, visionType, activeIndoorProp, isFlashlightActiveProp, "windowless-off");
        LogFogState(fog, fogType, "windowless-off");
        CaptureCameraView(cam, "FOW_01_windowless_room_flashlight_off.png");
        CaptureCameraView(cam, "01_indoor_flashlight_off.png");

        FieldInfo windowlessFogMatField = fogType?.GetField("overlayMaterial", BindingFlags.NonPublic | BindingFlags.Instance);
        Material windowlessFogMat = windowlessFogMatField?.GetValue(fog) as Material;
        if (windowlessFogMat != null)
        {
            float savedIndoor = windowlessFogMat.GetFloat("_IndoorActive");
            float savedOcclusion = windowlessFogMat.GetFloat("_IndoorOcclusionActive");
            windowlessFogMat.SetFloat("_IndoorActive", 0f);
            windowlessFogMat.SetFloat("_IndoorOcclusionActive", 0f);
            CaptureCameraView(cam, "FOW_01_windowless_room_NO_OVERLAY.png");
            windowlessFogMat.SetFloat("_IndoorActive", savedIndoor);
            windowlessFogMat.SetFloat("_IndoorOcclusionActive", savedOcclusion);
        }

        // 1.2 Flashlight ON aiming at solid interior wall (North)
        SetFlashlightActive(flashlight, true);
        if (lookDirProp != null) lookDirProp.SetValue(player, Vector2.up);
        yield return new WaitForSeconds(0.6f);
        LogLightingState(player, vision, visionType, activeIndoorProp, isFlashlightActiveProp, "windowless-on");
        LogFogState(fog, fogType, "windowless-on");
        CaptureCameraView(cam, "FOW_02_windowless_room_aim_wall.png");
        CaptureCameraView(cam, "02_indoor_flashlight_on_wall.png");

        // 1.3 Flashlight ON rotated 180 degrees (South)
        if (lookDirProp != null) lookDirProp.SetValue(player, Vector2.down);
        yield return new WaitForSeconds(0.6f);
        LogLightingState(player, vision, visionType, activeIndoorProp, isFlashlightActiveProp, "windowless-rotate");
        LogFogState(fog, fogType, "windowless-rotate");
        CaptureCameraView(cam, "FOW_03_windowless_room_rotate_180.png");
        CaptureCameraView(cam, "05_indoor_rotate_180.png");

        // 2. VERIFIED DOOR BLOCKER PORTAL (Hospital Radio Room)
        Type hospitalRadioType = Type.GetType("HospitalRadioRoomController, Assembly-CSharp");
        UnityEngine.Object[] radioControllers = hospitalRadioType != null ? UnityEngine.Object.FindObjectsByType(hospitalRadioType, FindObjectsSortMode.None) : null;
        Component radioCtrl = radioControllers != null && radioControllers.Length > 0 ? (Component)radioControllers[0] : null;
        Collider2D doorBlocker = radioCtrl != null
            ? hospitalRadioType.GetField("doorBlocker", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(radioCtrl) as Collider2D
            : null;
        Type mainQuestType = Type.GetType("MainQuestManager, Assembly-CSharp");
        Component mainQuest = mainQuestType != null
            ? UnityEngine.Object.FindFirstObjectByType(mainQuestType) as Component
            : null;
        PropertyInfo networkDoorState = mainQuestType?.GetProperty("IsHospitalRadioDoorOpen",
            BindingFlags.Public | BindingFlags.Instance);
        Assert.That(mainQuest, Is.Not.Null,
            "Main must contain the network-authoritative MainQuestManager for the hospital door test.");
        Assert.That(networkDoorState, Is.Not.Null,
            "The hospital door test must drive the replicated IsHospitalRadioDoorOpen state.");

        Vector3 radioRoomPos = radioCtrl != null ? radioCtrl.transform.position : windowlessRoomPos;
        Vector3 doorCenter = doorBlocker != null ? doorBlocker.bounds.center : radioRoomPos;
        Vector2 doorDir = doorBlocker != null
            ? ((Vector2)doorCenter - (Vector2)radioRoomPos).normalized
            : Vector2.down;
        Vector3 playerNearDoorPos = doorCenter - (Vector3)(doorDir * 1.5f);
        Vector2 aimAtDoor = ((Vector2)doorCenter - (Vector2)playerNearDoorPos).normalized;
        MovePlayerForVisualQA(player, playerNearDoorPos);
        cam.transform.position = new Vector3(doorCenter.x, doorCenter.y, cam.transform.position.z);
        Physics2D.SyncTransforms();

        // Create a dedicated bright ceiling light and floor visual target in the connected hallway outside the doorway
        Type light2DType = Type.GetType("UnityEngine.Rendering.Universal.Light2D, Unity.RenderPipelines.Universal.Runtime");
        GameObject hallwayLightObj = new GameObject("Hallway_Ceiling_Light_QA");
        Vector3 hallwayLightPos = doorCenter + (Vector3)(doorDir * 2.5f);
        hallwayLightObj.transform.position = hallwayLightPos;
        Component hallwayLight = light2DType != null ? hallwayLightObj.AddComponent(light2DType) : null;
        if (hallwayLight != null)
        {
            light2DType.GetProperty("lightType")?.SetValue(hallwayLight, 3);
            light2DType.GetProperty("pointLightOuterRadius")?.SetValue(hallwayLight, 9f);
            light2DType.GetProperty("pointLightInnerRadius")?.SetValue(hallwayLight, 2f);
            light2DType.GetProperty("pointLightOuterAngle")?.SetValue(hallwayLight, 360f);
            light2DType.GetProperty("intensity")?.SetValue(hallwayLight, 1.5f);
            light2DType.GetProperty("color")?.SetValue(hallwayLight, new Color(1f, 0.95f, 0.85f, 1f));
            SortingLayer[] allLayers = SortingLayer.layers;
            int[] allLayerIds = new int[allLayers.Length];
            for (int i = 0; i < allLayers.Length; i++) allLayerIds[i] = allLayers[i].id;
            light2DType.GetProperty("targetSortingLayers")?.SetValue(hallwayLight, allLayerIds);
        }

        GameObject hallwayMarkerObj = new GameObject("Hallway_Visual_Target_QA");
        hallwayMarkerObj.transform.position = doorCenter + (Vector3)(doorDir * 2.2f);
        SpriteRenderer markerSr = hallwayMarkerObj.AddComponent<SpriteRenderer>();
        markerSr.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
        markerSr.color = new Color(0.95f, 0.88f, 0.55f, 1f);
        markerSr.sortingLayerName = "Default";
        markerSr.sortingOrder = 5;
        markerSr.transform.localScale = new Vector3(200f, 120f, 1f);

        // 2.1 CLOSED DOOR: doorBlocker is enabled and door tile is closed
        MethodInfo applyState = hospitalRadioType?.GetMethod("ApplyState", BindingFlags.NonPublic | BindingFlags.Instance);
        SetNetworkBoolProperty(networkDoorState, mainQuest, false);
        applyState?.Invoke(radioCtrl, new object[] { false, true });
        if (doorBlocker != null) doorBlocker.enabled = true;
        SetPrivateField(fog, "nextIndoorOcclusionUpdate", 0f);
        SetFlashlightActive(flashlight, false);
        if (lookDirProp != null) lookDirProp.SetValue(player, aimAtDoor);
        yield return new WaitForSeconds(0.6f);
        LogLightingState(player, vision, visionType, activeIndoorProp, isFlashlightActiveProp, "radio-closed");
        LogFogState(fog, fogType, "radio-closed");
        CaptureCameraView(cam, "PORTAL_CLOSED_DIRECT.png");
        CaptureCameraView(cam, "FOW_04_closed_door_blocked.png");
        CaptureCameraView(cam, "04_indoor_closed_door.png");
        float closedDoorPortalMax = GetMaxPortalDistance(fog, portalDistancesField);

        // 2.2 OPEN DOOR PORTAL: doorBlocker is disabled and door tile is open
        SetNetworkBoolProperty(networkDoorState, mainQuest, true);
        applyState?.Invoke(radioCtrl, new object[] { true, true });
        if (doorBlocker != null) doorBlocker.enabled = false;
        SetPrivateField(fog, "nextIndoorOcclusionUpdate", 0f);
        if (lookDirProp != null) lookDirProp.SetValue(player, aimAtDoor);
        yield return new WaitForSeconds(0.6f);
        LogLightingState(player, vision, visionType, activeIndoorProp, isFlashlightActiveProp, "radio-open");
        LogFogState(fog, fogType, "radio-open");
        CaptureCameraView(cam, "PORTAL_OPEN_DIRECT.png");
        CaptureCameraView(cam, "FOW_05_open_door_portal.png");
        CaptureCameraView(cam, "03_indoor_looking_open_door.png");
        float openDoorPortalMax = GetMaxPortalDistance(fog, portalDistancesField);

        Debug.Log($"[FOW QA PORTAL AUDIT] doorBlocker={doorBlocker?.name} path={(doorBlocker != null ? doorBlocker.transform.name : "null")} enabled={doorBlocker?.enabled} bounds={doorBlocker?.bounds} doorCenter={doorCenter} playerPos={playerNearDoorPos} doorDir={doorDir} closedMax={closedDoorPortalMax} openMax={openDoorPortalMax}");

        // Compute and log pixel diff between PORTAL_CLOSED_DIRECT.png and PORTAL_OPEN_DIRECT.png
        string capturesDir = Path.Combine(Application.dataPath, "../Captures");
        string closedPath = Path.Combine(capturesDir, "PORTAL_CLOSED_DIRECT.png");
        string openPath = Path.Combine(capturesDir, "PORTAL_OPEN_DIRECT.png");
        if (File.Exists(closedPath) && File.Exists(openPath))
        {
            byte[] closedBytes = File.ReadAllBytes(closedPath);
            byte[] openBytes = File.ReadAllBytes(openPath);
            string closedSha = ComputeSha256(closedBytes);
            string openSha = ComputeSha256(openBytes);
            Texture2D texClosed = new Texture2D(2, 2);
            Texture2D texOpen = new Texture2D(2, 2);
            texClosed.LoadImage(closedBytes);
            texOpen.LoadImage(openBytes);
            int totalPixels = texClosed.width * texClosed.height;
            int diffPixels = 0;
            Color32[] closedPixels = texClosed.GetPixels32();
            Color32[] openPixels = texOpen.GetPixels32();
            for (int i = 0; i < totalPixels; i++)
            {
                Color32 c1 = closedPixels[i];
                Color32 c2 = openPixels[i];
                if (Mathf.Abs(c1.r - c2.r) > 5 || Mathf.Abs(c1.g - c2.g) > 5 || Mathf.Abs(c1.b - c2.b) > 5)
                    diffPixels++;
            }
            float diffPct = (float)diffPixels / totalPixels * 100f;
            string manifestText = $"PORTAL DIRECT AUDIT MANIFEST\n" +
                $"Width: {texClosed.width}, Height: {texClosed.height}\n" +
                $"Total Pixels: {totalPixels}\n" +
                $"Differing Pixels: {diffPixels} ({diffPct:F2}%)\n" +
                $"Player Pos: {playerNearDoorPos}\n" +
                $"Camera Pos: {cam.transform.position}\n" +
                $"Door Center: {doorCenter}\n" +
                $"Door Direction: {doorDir}\n" +
                $"DoorBlocker Path: {(doorBlocker != null ? doorBlocker.transform.name : "<null>")}\n" +
                $"DoorBlocker State: Closed=True, Open=False\n" +
                $"IsHospitalRadioDoorOpen: Closed=False, Open=True\n" +
                $"Indoor Collider: {(activeIndoorProp?.GetValue(vision) is Collider2D ind ? ind.name : "<none>")}\n" +
                $"Closed Portal Max: {closedDoorPortalMax}\n" +
                $"Open Portal Max: {openDoorPortalMax}\n" +
                $"Closed SHA256: {closedSha}\n" +
                $"Open SHA256: {openSha}\n" +
                $"Closed File Size: {closedBytes.Length} bytes\n" +
                $"Open File Size: {openBytes.Length} bytes\n";
            string manifestPath = Path.Combine(capturesDir, "PORTAL_AUDIT_MANIFEST.txt");
            File.WriteAllText(manifestPath, manifestText);
            Assert.That(File.Exists(manifestPath), Is.True);
            if (!string.IsNullOrEmpty(ActiveQaDirectory) && Directory.Exists(ActiveQaDirectory))
            {
                string qaManifestPath = Path.Combine(ActiveQaDirectory, "PORTAL_AUDIT_MANIFEST.txt");
                File.WriteAllText(qaManifestPath, manifestText);
                Assert.That(File.Exists(qaManifestPath), Is.True);
            }
            Debug.Log($"[FOW QA PORTAL MANIFEST] diffPixels={diffPixels}/{totalPixels} ({diffPct:F2}%) closedSha={closedSha.Substring(0, 12)} openSha={openSha.Substring(0, 12)}");
        }

        Assert.That(fog, Is.Not.Null, "Main must contain the FogVisionController under test.");
        Assert.That(portalDistancesField, Is.Not.Null,
            "Main FOW must expose the separate verified-door portal fan.");
        Assert.That(activeIndoorProp?.GetValue(vision) as Collider2D, Is.Not.Null,
            "The radio-room capture must be taken while the local Player is inside a real indoor trigger.");
        Assert.That(closedDoorPortalMax, Is.LessThan(0.001f),
            "A closed hospital door must not publish an exterior portal.");
        Assert.That(openDoorPortalMax, Is.GreaterThan(0.5f),
            "Opening the authored hospital door must publish a visible exterior portal.");

        // Clean up temporary hallway fixtures
        UnityEngine.Object.Destroy(hallwayLightObj);
        UnityEngine.Object.Destroy(hallwayMarkerObj);

        // Restore door blocker to closed
        SetNetworkBoolProperty(networkDoorState, mainQuest, false);
        applyState?.Invoke(radioCtrl, new object[] { false, true });
        if (doorBlocker != null) doorBlocker.enabled = true;

        // 3. OUTDOOR NEAR FENCE
        Vector3 fencePos = new Vector3(3.5f, -7.5f, 0f);
        MovePlayerForVisualQA(player, fencePos);
        cam.transform.position = new Vector3(fencePos.x, fencePos.y, cam.transform.position.z);
        Physics2D.SyncTransforms();
        if (dayNight != null && currentTimeProp != null) currentTimeProp.SetValue(dayNight, 14f);
        SetFlashlightActive(flashlight, false);
        if (lookDirProp != null) lookDirProp.SetValue(player, Vector2.right);
        yield return new WaitForSeconds(0.6f);
        LogLightingState(player, vision, visionType, activeIndoorProp, isFlashlightActiveProp, "fence-day");
        LogFogState(fog, fogType, "fence-day");

        FieldInfo fogMaterialField = fogType?.GetField("overlayMaterial",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Material fogMaterial = fogMaterialField?.GetValue(fog) as Material;
        Assert.That(fogMaterial, Is.Not.Null);
        Assert.That(fogMaterial.GetFloat("_FlashlightActive"), Is.LessThan(0.5f),
            "The outdoor FOW/LOS screenshot must be captured with the flashlight pass disabled.");
        Assert.That(fogMaterial.GetFloat("_LineOfSightActive"), Is.GreaterThan(0.5f),
            "Outdoor FOW must publish the structural LOS fan while the Player is outside.");
        CaptureCameraView(cam, "FOW_06_outdoor_fence_los.png");
        CaptureCameraView(cam, "06_outdoor_fence.png");

        if (fogMaterial != null)
        {
            float savedLos = fogMaterial.GetFloat("_LineOfSightActive");
            float savedDensity = fogMaterial.GetFloat("_FogDensity");
            fogMaterial.SetFloat("_LineOfSightActive", 0f);
            fogMaterial.SetFloat("_FogDensity", 0f);
            CaptureCameraView(cam, "FOW_06_outdoor_fence_NO_OVERLAY.png");
            fogMaterial.SetFloat("_LineOfSightActive", savedLos);
            fogMaterial.SetFloat("_FogDensity", savedDensity);
        }

        // 4. DAYTIME OUTDOOR (Open Road at Arrival Area (33.83, -15.08))
        Vector3 outdoorPos = new Vector3(33.83f, -15.08f, 0f);
        MovePlayerForVisualQA(player, outdoorPos);
        cam.transform.position = new Vector3(outdoorPos.x, outdoorPos.y, cam.transform.position.z);
        Physics2D.SyncTransforms();
        if (dayNight != null && currentTimeProp != null) currentTimeProp.SetValue(dayNight, 12f);
        SetFlashlightActive(flashlight, false);
        if (lookDirProp != null) lookDirProp.SetValue(player, Vector2.right);
        yield return new WaitForSeconds(0.6f);
        CaptureCameraView(cam, "FOW_07_outdoor_day.png");
        CaptureCameraView(cam, "07_day_outdoor.png");

        if (fogMaterial != null)
        {
            float savedLos = fogMaterial.GetFloat("_LineOfSightActive");
            float savedDensity = fogMaterial.GetFloat("_FogDensity");
            fogMaterial.SetFloat("_LineOfSightActive", 0f);
            fogMaterial.SetFloat("_FogDensity", 0f);
            CaptureCameraView(cam, "FOW_07_outdoor_day_NO_OVERLAY.png");
            fogMaterial.SetFloat("_LineOfSightActive", savedLos);
            fogMaterial.SetFloat("_FogDensity", savedDensity);
        }

        // 5. NIGHTTIME OUTDOOR
        if (dayNight != null && currentTimeProp != null) currentTimeProp.SetValue(dayNight, 23f);
        SetFlashlightActive(flashlight, true);
        if (lookDirProp != null) lookDirProp.SetValue(player, Vector2.right);
        yield return new WaitForSeconds(0.6f);
        CaptureCameraView(cam, "FOW_08_outdoor_night.png");
        CaptureCameraView(cam, "08_night_outdoor.png");

        if (fogMaterial != null)
        {
            float savedLos = fogMaterial.GetFloat("_LineOfSightActive");
            float savedDensity = fogMaterial.GetFloat("_FogDensity");
            fogMaterial.SetFloat("_LineOfSightActive", 0f);
            fogMaterial.SetFloat("_FogDensity", 0f);
            CaptureCameraView(cam, "FOW_08_outdoor_night_NO_OVERLAY.png");
            fogMaterial.SetFloat("_LineOfSightActive", savedLos);
            fogMaterial.SetFloat("_FogDensity", savedDensity);
        }

        // 6. MOVING WITH FLASHLIGHT
        Vector3 movePos = new Vector3(28f, -9f, 0f);
        MovePlayerForVisualQA(player, movePos);
        cam.transform.position = new Vector3(movePos.x, movePos.y, cam.transform.position.z);
        Physics2D.SyncTransforms();
        if (lookDirProp != null) lookDirProp.SetValue(player, new Vector2(1f, 1f).normalized);
        yield return new WaitForSeconds(0.6f);
        CaptureCameraView(cam, "FOW_09_moving_flashlight.png");
        CaptureCameraView(cam, "09_moving_light.png");

        RestoreVisualQACaptureTarget();
        yield return ShutdownExistingRunners();
    }

    private static void PrepareVisualQACaptureTarget(Camera cam)
    {
        RestoreVisualQACaptureTarget();
        visualQaCamera = cam;
        visualQaPreviousTexture = cam.targetTexture;
        visualQaPreviousAspect = cam.aspect;
        visualQaCaptureTexture = new RenderTexture(1280, 720, 24);
        visualQaCaptureTexture.name = "Visibility QA Capture Target";
        cam.targetTexture = visualQaCaptureTexture;
        cam.aspect = 1280f / 720f;
    }

    private static void RestoreVisualQACaptureTarget()
    {
        if (visualQaCamera != null)
        {
            visualQaCamera.targetTexture = visualQaPreviousTexture;
            visualQaCamera.aspect = visualQaPreviousAspect;
        }

        if (visualQaCaptureTexture != null)
            UnityEngine.Object.DestroyImmediate(visualQaCaptureTexture);

        visualQaCaptureTexture = null;
        visualQaPreviousTexture = null;
        visualQaCamera = null;
    }

    private static void CaptureCameraView(Camera cam, string filename)
    {
        int width = 1280;
        int height = 720;
        RenderTexture rt = cam.targetTexture;
        bool ownsTemporaryTarget = rt == null || rt.width != width || rt.height != height;
        if (ownsTemporaryTarget)
            rt = new RenderTexture(width, height, 24);

        RenderTexture prevRT = cam.targetTexture;
        RenderTexture prevActive = RenderTexture.active;

        cam.targetTexture = rt;
        cam.Render();

        RenderTexture.active = rt;
        Texture2D screenShot = new Texture2D(width, height, TextureFormat.RGB24, false);
        screenShot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        screenShot.Apply();

        if (ownsTemporaryTarget)
        {
            cam.targetTexture = prevRT;
            UnityEngine.Object.DestroyImmediate(rt);
        }
        else
        {
            cam.targetTexture = prevRT;
        }
        RenderTexture.active = prevActive;

        byte[] bytes = screenShot.EncodeToPNG();
        UnityEngine.Object.DestroyImmediate(screenShot);

        string artifactDir = @"C:\Users\triti\.gemini\antigravity-ide\brain\829b507d-1262-4d38-9af6-2aa7361e056a";
        if (Directory.Exists(artifactDir))
        {
            string brainPath = Path.Combine(artifactDir, filename);
            File.WriteAllBytes(brainPath, bytes);
        }
        string workspaceDir = Path.Combine(Application.dataPath, "../Captures");
        if (!Directory.Exists(workspaceDir)) Directory.CreateDirectory(workspaceDir);
        string capturePath = Path.Combine(workspaceDir, filename);
        File.WriteAllBytes(capturePath, bytes);
        Assert.That(File.Exists(capturePath), Is.True, $"Captured image must exist at {capturePath}");
        Debug.Log($"[FOW QA CAPTURE] Wrote {filename} ({bytes.Length} bytes) to {capturePath}");

        if (!string.IsNullOrEmpty(ActiveQaDirectory) && Directory.Exists(ActiveQaDirectory))
        {
            string qaPath = Path.Combine(ActiveQaDirectory, filename);
            File.WriteAllBytes(qaPath, bytes);
            Assert.That(File.Exists(qaPath), Is.True, $"Captured image must exist at {qaPath}");
            Debug.Log($"[FOW QA CAPTURE] Wrote {filename} ({bytes.Length} bytes) to {qaPath}");
        }
    }

    private static IEnumerator CaptureHouseCandidate(Component player, Component vision,
        Component flashlight, Camera cam, Component fog, Type fogType,
        PropertyInfo activeIndoorProp, PropertyInfo isFlashlightActiveProp,
        PropertyInfo lookDirProp, Vector3 position, string label)
    {
        MovePlayerForVisualQA(player, position);
        cam.transform.position = new Vector3(position.x, position.y, cam.transform.position.z);
        Physics2D.SyncTransforms();
        SetFlashlightActive(flashlight, true);
        if (lookDirProp != null) lookDirProp.SetValue(player, Vector2.right);
        yield return new WaitForSeconds(0.45f);
        if (label == "large_house_b")
        {
            Vector3 screenPlayer = cam.WorldToScreenPoint(player.transform.position);
            Vector3 screenWorldRight = cam.WorldToScreenPoint(player.transform.position + Vector3.right) - screenPlayer;
            Vector3 screenWorldUp = cam.WorldToScreenPoint(player.transform.position + Vector3.up) - screenPlayer;
            Debug.Log($"[FOW QA CAMERA {label}] pos={cam.transform.position} rot={cam.transform.rotation.eulerAngles} " +
                      $"ortho={cam.orthographicSize:0.###} screenPlayer={screenPlayer} " +
                      $"worldRightScreen={screenWorldRight} worldUpScreen={screenWorldUp}");
        }
        LogLightingState(player, vision, vision.GetType(), activeIndoorProp, isFlashlightActiveProp, label);
        LogFogState(fog, fogType, label);
        CaptureCameraView(cam, "FOW_CANDIDATE_" + label + ".png");

        if (label == "large_house_b")
        {
            FieldInfo materialField = fogType?.GetField("overlayMaterial",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Material material = materialField?.GetValue(fog) as Material;
            if (material != null)
            {
                float savedOcclusion = material.GetFloat("_IndoorOcclusionActive");
                float savedFlashlight = material.GetFloat("_FlashlightActive");
                material.SetFloat("_IndoorOcclusionActive", 0f);
                CaptureCameraView(cam, "FOW_CANDIDATE_large_house_b_no_occlusion.png");
                material.SetFloat("_FlashlightActive", 0f);
                CaptureCameraView(cam, "FOW_CANDIDATE_large_house_b_no_flashlight_mask.png");
                material.SetFloat("_IndoorOcclusionActive", savedOcclusion);
                material.SetFloat("_FlashlightActive", savedFlashlight);
            }
        }

        // The default capture is paired with an OFF capture so the reviewer
        // can distinguish a flashlight mask leak from ordinary indoor lighting.
        SetFlashlightActive(flashlight, false);
        yield return new WaitForSeconds(0.35f);
        LogLightingState(player, vision, vision.GetType(), activeIndoorProp, isFlashlightActiveProp, label + "_off");
        LogFogState(fog, fogType, label + "_off");
        CaptureCameraView(cam, "FOW_CANDIDATE_" + label + "_off.png");
    }

    private static void LogLightingState(Component player, Component vision, Type visionType,
        PropertyInfo activeIndoorProp, PropertyInfo isFlashlightActiveProp, string label)
    {
        Collider2D indoor = activeIndoorProp?.GetValue(vision) as Collider2D;
        FieldInfo playerLightField = visionType?.GetField("playerLight",
            BindingFlags.Public | BindingFlags.Instance);
        Component playerLight = playerLightField?.GetValue(vision) as Component;
        bool lightActive = playerLight != null && playerLight.gameObject.activeInHierarchy;
        Component flashlight = vision != null ? vision.GetComponent("FlashlightController") : null;
        bool flashlightActive = flashlight != null && isFlashlightActiveProp != null &&
            (bool)isFlashlightActiveProp.GetValue(flashlight);
        PropertyInfo lineOfSightOriginProp = visionType?.GetProperty("LineOfSightOrigin",
            BindingFlags.Public | BindingFlags.Instance);
        PropertyInfo lineOfSightDirectionProp = visionType?.GetProperty("LineOfSightDirection",
            BindingFlags.Public | BindingFlags.Instance);
        PropertyInfo lineOfSightRadiusProp = visionType?.GetProperty("LineOfSightRadius",
            BindingFlags.Public | BindingFlags.Instance);
        PropertyInfo obstacleLayerProp = visionType?.GetProperty("VisionObstacleLayer",
            BindingFlags.Public | BindingFlags.Instance);
        Vector2 lineOfSightOrigin = lineOfSightOriginProp?.GetValue(vision) is Vector2 origin
            ? origin
            : Vector2.zero;
        Vector2 lineOfSightDirection = lineOfSightDirectionProp?.GetValue(vision) is Vector2 direction
            ? direction
            : Vector2.zero;
        float lineOfSightRadius = lineOfSightRadiusProp?.GetValue(vision) is float radius
            ? radius
            : 0f;
        LayerMask visionObstacleLayer = obstacleLayerProp?.GetValue(vision) is LayerMask mask
            ? mask
            : default;

        List<string> activeLightNames = new List<string>();
        Type light2DType = playerLight != null ? playerLight.GetType() : null;
        UnityEngine.Object[] lights = light2DType != null
            ? UnityEngine.Object.FindObjectsByType(light2DType, FindObjectsSortMode.None)
            : new UnityEngine.Object[0];
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] is Component light && light != null && light.gameObject.activeInHierarchy && light is Behaviour behaviour && behaviour.isActiveAndEnabled)
                activeLightNames.Add($"{light.name}@{light.transform.position}");
        }

        Debug.Log($"[FOW QA {label}] pos={player.transform.position} indoor={(indoor != null ? indoor.name : "<none>")} " +
                  $"flashlight={flashlightActive} playerLightActive={lightActive} " +
                  $"activeLight2D=[{string.Join(", ", activeLightNames)}] " +
                  $"losOrigin={lineOfSightOrigin} losDirection={lineOfSightDirection} " +
                  $"losRadius={lineOfSightRadius:0.###} losObstacleMask={visionObstacleLayer.value}");
    }

    private static void LogFogState(Component fog, Type fogType, string label)
    {
        if (fog == null || fogType == null)
        {
            Debug.Log($"[FOW QA FOG {label}] controller=<none>");
            return;
        }

        FieldInfo materialField = fogType.GetField("overlayMaterial",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Material material = materialField?.GetValue(fog) as Material;
        FieldInfo indoorColliderField = fogType.GetField("cachedIndoorCollider",
            BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo structureField = fogType.GetField("cachedIndoorStructureRoot",
            BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo distancesField = fogType.GetField("indoorOcclusionDistances",
            BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo portalsField = fogType.GetField("indoorPortalDistances",
            BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo lineOfSightDistancesField = fogType.GetField("lineOfSightDistances",
            BindingFlags.NonPublic | BindingFlags.Instance);

        float indoorActive = material != null ? material.GetFloat("_IndoorActive") : -1f;
        float pointCount = material != null ? material.GetFloat("_IndoorPointCount") : -1f;
        float occlusionActive = material != null ? material.GetFloat("_IndoorOcclusionActive") : -1f;
        float rayCount = material != null ? material.GetFloat("_IndoorOcclusionRayCount") : -1f;
        float flashlightActive = material != null ? material.GetFloat("_FlashlightActive") : -1f;
        float flashlightRadius = material != null ? material.GetFloat("_FlashlightRadius") : -1f;
        float lineOfSightActive = material != null ? material.GetFloat("_LineOfSightActive") : -1f;
        float lineOfSightRayCount = material != null ? material.GetFloat("_LineOfSightRayCount") : -1f;

        float[] distances = distancesField?.GetValue(fog) as float[];
        float[] portals = portalsField?.GetValue(fog) as float[];
        float[] lineOfSightDistances = lineOfSightDistancesField?.GetValue(fog) as float[];
        float minDistance = float.MaxValue;
        float maxDistance = 0f;
        int clippedRays = 0;
        if (distances != null)
        {
            for (int i = 0; i < distances.Length; i++)
            {
                minDistance = Mathf.Min(minDistance, distances[i]);
                maxDistance = Mathf.Max(maxDistance, distances[i]);
                if (distances[i] < 20f) clippedRays++;
            }
        }

        float maxPortal = 0f;
        int portalRays = 0;
        if (portals != null)
        {
            for (int i = 0; i < portals.Length; i++)
            {
                maxPortal = Mathf.Max(maxPortal, portals[i]);
                if (portals[i] > 0.05f) portalRays++;
            }
        }

        float minLineOfSightDistance = float.MaxValue;
        float maxLineOfSightDistance = 0f;
        if (lineOfSightDistances != null)
        {
            for (int i = 0; i < lineOfSightDistances.Length; i++)
            {
                minLineOfSightDistance = Mathf.Min(minLineOfSightDistance, lineOfSightDistances[i]);
                maxLineOfSightDistance = Mathf.Max(maxLineOfSightDistance, lineOfSightDistances[i]);
            }
        }

        Collider2D cachedCollider = indoorColliderField?.GetValue(fog) as Collider2D;
        Transform structure = structureField?.GetValue(fog) as Transform;
        string raySamples = "<none>";
        int sampleRayCount = Mathf.Clamp(Mathf.RoundToInt(rayCount), 0, 180);
        if (distances != null && sampleRayCount > 0)
        {
            int[] sampleIndices = { 0, sampleRayCount / 8, sampleRayCount / 4,
                sampleRayCount * 3 / 8, sampleRayCount / 2, sampleRayCount * 5 / 8,
                sampleRayCount * 3 / 4, sampleRayCount * 7 / 8 };
            List<string> samples = new List<string>(sampleIndices.Length);
            for (int i = 0; i < sampleIndices.Length; i++)
            {
                int index = Mathf.Clamp(sampleIndices[i], 0, distances.Length - 1);
                float portal = portals != null && index < portals.Length ? portals[index] : 0f;
                samples.Add($"{index}:{distances[index]:0.###}/{portal:0.###}");
            }
            raySamples = string.Join(",", samples);
        }

        Vector4[] polygonPoints = fogType.GetField("indoorPoints",
            BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(fog) as Vector4[];
        string polygonBounds = "<none>";
        int polygonCount = material != null ? Mathf.Clamp(Mathf.RoundToInt(pointCount), 0, 16) : 0;
        if (polygonPoints != null && polygonCount >= 3)
        {
            Vector2 min = polygonPoints[0];
            Vector2 max = polygonPoints[0];
            for (int i = 1; i < polygonCount && i < polygonPoints.Length; i++)
            {
                min = Vector2.Min(min, polygonPoints[i]);
                max = Vector2.Max(max, polygonPoints[i]);
            }
            polygonBounds = $"({min.x:0.###},{min.y:0.###})-({max.x:0.###},{max.y:0.###})";
        }
        Debug.Log($"[FOW QA FOG {label}] material={(material != null ? material.name : "<none>")} " +
                  $"indoorActive={indoorActive:0.###} points={pointCount:0.###} " +
                  $"occlusionActive={occlusionActive:0.###} rays={rayCount:0.###} " +
                  $"flashlight={flashlightActive:0.###} radius={flashlightRadius:0.###} " +
                  $"losActive={lineOfSightActive:0.###} losRays={lineOfSightRayCount:0.###} " +
                  $"losDistance=[{(minLineOfSightDistance == float.MaxValue ? 0f : minLineOfSightDistance):0.###},{maxLineOfSightDistance:0.###}] " +
                  $"cachedIndoor={(cachedCollider != null ? cachedCollider.name : "<none>")} " +
                  $"structure={(structure != null ? structure.name : "<none>")} " +
                  $"polygonBounds={polygonBounds} " +
                  $"wallDistance=[{(minDistance == float.MaxValue ? 0f : minDistance):0.###},{maxDistance:0.###}] " +
                  $"clippedRays={clippedRays} samples(angle0..315)=[{raySamples}] " +
                  $"portalMax={maxPortal:0.###} portalRays={portalRays}");

        if (label == "large_house_b" && structure != null)
        {
            int obstacleLayer = LayerMask.NameToLayer("Obstacle");
            Collider2D[] structureColliders = structure.GetComponentsInChildren<Collider2D>(true);
            List<string> details = new List<string>();
            for (int i = 0; i < structureColliders.Length; i++)
            {
                Collider2D collider = structureColliders[i];
                if (collider == null || collider.isTrigger || collider.gameObject.layer != obstacleLayer) continue;
                details.Add($"{collider.name}:{collider.GetType().Name}@{collider.bounds.center} size={collider.bounds.size} on={collider.enabled}");
            }
            Debug.Log($"[FOW QA COLLIDERS {label}] root={structure.name} count={details.Count} " +
                      string.Join(" | ", details));
        }
    }

    private static void LogIndoorAreaCatalog()
    {
        Type roofType = Type.GetType("RoofVisibility, Assembly-CSharp");
        Type indoorType = Type.GetType("IndoorVisionArea, Assembly-CSharp");
        Collider2D[] colliders = UnityEngine.Object.FindObjectsByType<Collider2D>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (collider == null || !collider.isTrigger) continue;

            bool isNocnha = collider.gameObject.name.StartsWith("nocnha", StringComparison.OrdinalIgnoreCase);
            bool hasRoof = roofType != null && collider.GetComponentInParent(roofType) != null;
            bool hasIndoorMarker = indoorType != null && collider.GetComponentInParent(indoorType) != null;
            if (!isNocnha && !hasRoof && !hasIndoorMarker) continue;

            Debug.Log($"[FOW QA AREA] name={collider.name} type={collider.GetType().Name} " +
                      $"center={collider.bounds.center} size={collider.bounds.size} " +
                      $"parent={(collider.transform.parent != null ? collider.transform.parent.name : "<root>")} " +
                      $"active={collider.gameObject.activeInHierarchy} hasRoof={hasRoof} hasIndoorMarker={hasIndoorMarker}");
        }
    }

    private static IEnumerator WaitForActiveButton(string label, float timeoutSeconds)
    {
        float deadline = Time.realtimeSinceStartup + timeoutSeconds;
        while (Time.realtimeSinceStartup < deadline)
        {
            GameObject button = GameObject.Find("Btn_" + label);
            if (button != null && button.activeInHierarchy &&
                button.GetComponent<Button>() != null &&
                button.GetComponent<Button>().interactable)
                yield break;
            yield return null;
        }
        Assert.That(GameObject.Find("Btn_" + label), Is.Not.Null, "Active button not found: " + label);
    }

    private static void InvokeButton(string label)
    {
        GameObject target = GameObject.Find("Btn_" + label);
        Assert.That(target, Is.Not.Null);
        Button button = target.GetComponent<Button>();
        Assert.That(button, Is.Not.Null);
        Assert.That(button.interactable, Is.True);
        button.onClick.Invoke();
    }

    private static IEnumerator ShutdownExistingRunners()
    {
        Type runnerType = Type.GetType("Fusion.NetworkRunner, Fusion.Runtime");
        if (runnerType == null) yield break;
        UnityEngine.Object[] runners = Resources.FindObjectsOfTypeAll(runnerType);
        Type shutdownReasonType = Type.GetType("Fusion.ShutdownReason, Fusion.Runtime");
        MethodInfo shutdown = shutdownReasonType == null ? null : runnerType.GetMethod("Shutdown",
            new[] { typeof(bool), shutdownReasonType, typeof(bool) });
        for (int i = 0; i < runners.Length; i++)
        {
            if (runners[i] == null || shutdown == null) continue;
            object ok = Enum.Parse(shutdownReasonType, "Ok");
            Task task = shutdown.Invoke(runners[i], new[] { (object)true, ok, false }) as Task;
            float deadline = Time.realtimeSinceStartup + 10f;
            while (task != null && !task.IsCompleted && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        Type menuType = Type.GetType("AutoMainMenuManager, Assembly-CSharp");
        if (menuType != null)
        {
            UnityEngine.Object[] menus = Resources.FindObjectsOfTypeAll(menuType);
            for (int i = 0; i < menus.Length; i++)
                if (menus[i] is Component menu && menu != null)
                    UnityEngine.Object.Destroy(menu.transform.root.gameObject);
        }

        GameObject[] persistentObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < persistentObjects.Length; i++)
            if (persistentObjects[i] != null && persistentObjects[i].name == "AutoMenuCanvas")
                UnityEngine.Object.Destroy(persistentObjects[i]);

        yield return null;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(target, value);
    }

    private static void SetFlashlightActive(Component flashlight, bool active)
    {
        if (flashlight == null) return;
        Type t = flashlight.GetType();
        PropertyInfo onProp = t.GetProperty("IsFlashlightOn", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        PropertyInfo equippedProp = t.GetProperty("IsEquippedInHotbar", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        PropertyInfo batteryProp = t.GetProperty("Battery01", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        if (onProp != null) SetNetworkBoolProperty(onProp, flashlight, active);
        if (equippedProp != null) SetNetworkBoolProperty(equippedProp, flashlight, active);
        if (batteryProp != null) batteryProp.SetValue(flashlight, active ? 1f : 0f);

        FieldInfo cachedField = t.GetField("cachedActive", BindingFlags.NonPublic | BindingFlags.Instance);
        cachedField?.SetValue(flashlight, active);
    }

    private static void MovePlayerForVisualQA(Component player, Vector3 position)
    {
        Assert.That(player, Is.Not.Null);
        Component networkBody = player.GetComponent("NetworkRigidbody2D");
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.position = new Vector2(position.x, position.y);
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        player.transform.position = position;
        networkBody?.GetType().GetMethod("Teleport", BindingFlags.Public | BindingFlags.Instance)
            ?.Invoke(networkBody, new object[] { new Vector3(position.x, position.y, position.z), null });
        Physics2D.SyncTransforms();
    }

    private static float GetMaxPortalDistance(Component fog, FieldInfo portalDistancesField)
    {
        if (fog == null || portalDistancesField == null) return 0f;
        float[] distances = portalDistancesField.GetValue(fog) as float[];
        if (distances == null) return 0f;

        float maxDistance = 0f;
        for (int i = 0; i < distances.Length; i++)
            maxDistance = Mathf.Max(maxDistance, distances[i]);
        return maxDistance;
    }

    private static void SetNetworkBoolProperty(PropertyInfo property, Component target, bool value)
    {
        object networkValue = Activator.CreateInstance(property.PropertyType, new object[] { value });
        property.SetValue(target, networkValue);
    }
}
