using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class HospitalRadioH1Setup
{
    private const string RootName = "HospitalQuest_RadioRoom";
    private const string DoorName = "DoorInteraction";
    private const string RadioName = "RadioInteraction";
    private const string BlockerName = "DoorBlocker";
    private const string ShiftLogName = "HospitalQuest_ShiftLog";
    private const string ShiftLog2Name = "HospitalQuest_ShiftLog2";
    private const string EnvironmentStoryRootName = "HospitalQuest_EnvironmentalStory";
    private const string InteractionZoneName = "InteractionZone";
    private const string ClosedTilePath =
        "Assets/SmallScaleInt/2D Zombie interior Tile pack 1/Environment/Tiles/Door13_W.asset";
    private const string OpenTilePath =
        "Assets/SmallScaleInt/2D Zombie interior Tile pack 1/Environment/Tiles/Door14_W.asset";

    [MenuItem("Tools/Main Quest/Setup Hospital Radio H5")]
    public static void Setup()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != "Assets/Scenes/Main.unity")
            throw new InvalidOperationException("H1 setup requires Assets/Scenes/Main.unity to be the active scene.");

        GameObject root = GameObject.Find(RootName);
        if (root == null) throw new InvalidOperationException($"Missing scene anchor: {RootName}");
        Transform doorAnchor = root.transform.Find(DoorName);
        Transform radioAnchor = root.transform.Find(RadioName);
        if (doorAnchor == null || radioAnchor == null)
            throw new InvalidOperationException("Hospital radio room is missing DoorInteraction or RadioInteraction.");
        GameObject shiftLogObject = GameObject.Find(ShiftLogName);
        GameObject shiftLog2Object = GameObject.Find(ShiftLog2Name);
        if (shiftLogObject == null || shiftLog2Object == null)
            throw new InvalidOperationException("Hospital H2 is missing ShiftLog or ShiftLog2 scene anchors.");

        TileBase closedTile = AssetDatabase.LoadAssetAtPath<TileBase>(ClosedTilePath);
        TileBase openTile = AssetDatabase.LoadAssetAtPath<TileBase>(OpenTilePath);
        if (closedTile == null || openTile == null)
            throw new InvalidOperationException("Door13_W/Door14_W tile assets could not be loaded.");

        Tilemap doorTilemap = FindDoorTilemap(doorAnchor.position, closedTile, openTile, out Vector3Int doorCell);
        if (doorTilemap == null)
            throw new InvalidOperationException("No 'cuanha' Tilemap has Door13_W/Door14_W at the DoorInteraction cell.");

        Transform blockerTransform = root.transform.Find(BlockerName);
        GameObject blockerObject;
        if (blockerTransform == null)
        {
            blockerObject = new GameObject(BlockerName);
            Undo.RegisterCreatedObjectUndo(blockerObject, "Create hospital radio door blocker");
            blockerObject.transform.SetParent(root.transform, false);
        }
        else blockerObject = blockerTransform.gameObject;

        Undo.RecordObject(blockerObject.transform, "Configure hospital radio door blocker");
        blockerObject.transform.localPosition = doorAnchor.localPosition;
        blockerObject.transform.localRotation = Quaternion.Euler(0f, 0f, 26.565f);
        blockerObject.transform.localScale = Vector3.one;
        int obstacleLayer = LayerMask.NameToLayer("Obstacle");
        if (obstacleLayer < 0) throw new InvalidOperationException("Project layer 'Obstacle' is missing.");
        blockerObject.layer = obstacleLayer;

        BoxCollider2D blocker = blockerObject.GetComponent<BoxCollider2D>();
        if (blocker == null) blocker = Undo.AddComponent<BoxCollider2D>(blockerObject);
        Undo.RecordObject(blocker, "Configure hospital radio door collider");
        blocker.isTrigger = false;
        blocker.offset = Vector2.zero;
        blocker.size = new Vector2(1.05f, 0.22f);
        blocker.enabled = true;

        HospitalRadioInteractionPoint door = doorAnchor.GetComponent<HospitalRadioInteractionPoint>();
        if (door == null) door = Undo.AddComponent<HospitalRadioInteractionPoint>(doorAnchor.gameObject);
        HospitalRadioInteractionPoint radio = radioAnchor.GetComponent<HospitalRadioInteractionPoint>();
        if (radio == null) radio = Undo.AddComponent<HospitalRadioInteractionPoint>(radioAnchor.gameObject);
        PolygonCollider2D doorZone = EnsureInteractionZone(doorAnchor, 0.7f);
        PolygonCollider2D radioZone = EnsureInteractionZone(radioAnchor, 0.6f);

        LayerMask obstacles = 1 << obstacleLayer;
        Undo.RecordObject(door, "Configure hospital radio door interaction");
        door.EditorConfigure(HospitalRadioInteractionRole.Door, 0.7f, 0.6f, obstacles, blocker, doorZone);
        Undo.RecordObject(radio, "Configure hospital radio interaction");
        radio.EditorConfigure(HospitalRadioInteractionRole.Radio, 0.6f, 0.5f, obstacles, null, radioZone);

        HospitalRadioRoomController controller = root.GetComponent<HospitalRadioRoomController>();
        if (controller == null) controller = Undo.AddComponent<HospitalRadioRoomController>(root);
        Undo.RecordObject(controller, "Configure hospital radio room H1");
        controller.EditorConfigure(doorTilemap, doorCell, closedTile, openTile, blocker, door, radio);
        ConfigureHospitalRadioTiming();
        ConfigureEnvironmentalStory(root.transform.parent);

        HospitalQuestClueInteractionPoint shiftLog =
            shiftLogObject.GetComponent<HospitalQuestClueInteractionPoint>();
        if (shiftLog == null) shiftLog = Undo.AddComponent<HospitalQuestClueInteractionPoint>(shiftLogObject);
        HospitalQuestClueInteractionPoint shiftLog2 =
            shiftLog2Object.GetComponent<HospitalQuestClueInteractionPoint>();
        if (shiftLog2 == null) shiftLog2 = Undo.AddComponent<HospitalQuestClueInteractionPoint>(shiftLog2Object);
        PolygonCollider2D shiftLogZone = EnsureInteractionZone(shiftLogObject.transform, 1.5f);
        PolygonCollider2D shiftLog2Zone = EnsureInteractionZone(shiftLog2Object.transform, 0.85f);
        Undo.RecordObject(shiftLog, "Configure hospital ShiftLog interaction");
        // The default polygon is deliberately generous. The project owner can
        // edit the InteractionZone child around the public side of reception;
        // rerunning this setup preserves authored polygon points.
        shiftLog.EditorConfigure(HospitalQuestClueRole.ShiftLog, 1.5f, 0.7f, obstacles, shiftLogZone);
        Undo.RecordObject(shiftLog2, "Configure hospital ShiftLog2 interaction");
        shiftLog2.EditorConfigure(HospitalQuestClueRole.ShiftLog2, 0.85f, 0.7f, obstacles, shiftLog2Zone);

        int keyLootCount = ConfigureKeyLootPoints(scene);
        if (keyLootCount == 0)
            throw new InvalidOperationException("No KeyLoot points were found in Main.unity.");

        MainQuestSearchCabinet[] legacyPoints = UnityEngine.Object.FindObjectsByType<MainQuestSearchCabinet>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < legacyPoints.Length; i++)
        {
            if (legacyPoints[i] == null) continue;
            Undo.RecordObject(legacyPoints[i], "Disable legacy hospital investigation point");
            legacyPoints[i].enabled = false;
            EditorUtility.SetDirty(legacyPoints[i]);
        }

        EditorUtility.SetDirty(blockerObject);
        EditorUtility.SetDirty(blocker);
        EditorUtility.SetDirty(door);
        EditorUtility.SetDirty(radio);
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(shiftLog);
        EditorUtility.SetDirty(shiftLog2);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Unity could not save Main.unity after H1 setup.");

        Selection.activeGameObject = root;
        Debug.Log($"[HOSPITAL H5 SETUP] Complete. {keyLootCount} random KeyLoot candidates + editable " +
                  $"interaction polygons; Door cell {doorCell} on {doorTilemap.name}; " +
                  $"disabled {legacyPoints.Length} legacy points.");
    }

    private static PolygonCollider2D EnsureInteractionZone(Transform owner, float halfExtent)
    {
        Transform existing = owner.Find(InteractionZoneName);
        GameObject zoneObject;
        bool created = existing == null;
        if (created)
        {
            zoneObject = new GameObject(InteractionZoneName);
            Undo.RegisterCreatedObjectUndo(zoneObject, "Create hospital interaction polygon");
            zoneObject.transform.SetParent(owner, false);
            zoneObject.transform.localPosition = Vector3.zero;
            zoneObject.transform.localRotation = Quaternion.identity;
            zoneObject.transform.localScale = Vector3.one;
        }
        else zoneObject = existing.gameObject;

        int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
        if (ignoreRaycastLayer >= 0) zoneObject.layer = ignoreRaycastLayer;
        PolygonCollider2D polygon = zoneObject.GetComponent<PolygonCollider2D>();
        if (polygon == null)
        {
            polygon = Undo.AddComponent<PolygonCollider2D>(zoneObject);
            created = true;
        }
        Undo.RecordObject(polygon, "Configure hospital interaction polygon");
        polygon.isTrigger = true;
        polygon.enabled = true;
        if (created)
        {
            float extent = Mathf.Max(0.2f, halfExtent);
            polygon.pathCount = 1;
            polygon.SetPath(0, new[]
            {
                new Vector2(-extent, -extent), new Vector2(extent, -extent),
                new Vector2(extent, extent), new Vector2(-extent, extent)
            });
        }
        EditorUtility.SetDirty(zoneObject);
        EditorUtility.SetDirty(polygon);
        return polygon;
    }

    private static int ConfigureKeyLootPoints(Scene scene)
    {
        Transform[] transforms = UnityEngine.Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        int configured = 0;
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null || candidate.gameObject.scene != scene ||
                !candidate.name.StartsWith("KeyLoot", StringComparison.OrdinalIgnoreCase)) continue;
            HospitalRadioKeyLootPoint point = candidate.GetComponent<HospitalRadioKeyLootPoint>();
            if (point == null) point = Undo.AddComponent<HospitalRadioKeyLootPoint>(candidate.gameObject);
            PolygonCollider2D zone = EnsureInteractionZone(candidate, 0.75f);
            Undo.RecordObject(point, "Configure hospital Radio key loot point");
            point.EditorConfigure(zone);
            EditorUtility.SetDirty(point);
            configured++;
        }
        return configured;
    }

    private static void ConfigureHospitalRadioTiming()
    {
        MainQuestManager manager = UnityEngine.Object.FindFirstObjectByType<MainQuestManager>(
            FindObjectsInactive.Include);
        if (manager == null) throw new InvalidOperationException("MainQuestManager is missing from Main.unity.");
        SerializedObject serializedManager = new SerializedObject(manager);
        serializedManager.FindProperty("hospitalRadioRestoreDuration").floatValue = 14f;
        serializedManager.FindProperty("hospitalRadioZombieSpawnDelay").floatValue = 0.25f;
        serializedManager.FindProperty("hospitalRadioZombieHorizontalSpacing").floatValue = 0.8f;
        serializedManager.FindProperty("hospitalRadioNoiseRadius").floatValue = 28f;
        serializedManager.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(manager);
    }

    private static void ConfigureEnvironmentalStory(Transform hospitalRoot)
    {
        if (hospitalRoot == null) throw new InvalidOperationException("Hospital scene root is missing.");
        Transform existing = hospitalRoot.Find(EnvironmentStoryRootName);
        GameObject storyRoot;
        if (existing == null)
        {
            storyRoot = new GameObject(EnvironmentStoryRootName);
            Undo.RegisterCreatedObjectUndo(storyRoot, "Create hospital environmental story root");
            storyRoot.transform.SetParent(hospitalRoot, true);
        }
        else storyRoot = existing.gameObject;

        string[] deathClips =
        {
            "Assets/Khoa/Zombie1/Die/DieS.anim",
            "Assets/Khoa/Zombie1/Die/DieSE.anim",
            "Assets/Khoa/Zombie1/Die/DieSW.anim",
            "Assets/Khoa/Zombie1/Die/DieN.anim"
        };
        Vector3[] positions =
        {
            new Vector3(-46.7f, 20.1f, 0f),
            new Vector3(-49.8f, 24.0f, 0f),
            new Vector3(-52.0f, 28.7f, 0f),
            new Vector3(-49.0f, 33.2f, 0f)
        };
        float[] rotations = { -18f, 22f, -31f, 14f };
        for (int i = 0; i < positions.Length; i++)
        {
            string corpseName = $"HospitalStory_Corpse_{i + 1:00}";
            Transform corpseTransform = storyRoot.transform.Find(corpseName);
            GameObject corpse;
            if (corpseTransform == null)
            {
                corpse = new GameObject(corpseName);
                Undo.RegisterCreatedObjectUndo(corpse, "Create hospital story corpse");
                corpse.transform.SetParent(storyRoot.transform, true);
            }
            else corpse = corpseTransform.gameObject;

            Sprite sprite = LoadLastAnimationSprite(deathClips[i]);
            if (sprite == null) throw new InvalidOperationException("Missing death-frame sprite: " + deathClips[i]);
            Undo.RecordObject(corpse.transform, "Position hospital story corpse");
            corpse.transform.position = positions[i];
            corpse.transform.rotation = Quaternion.Euler(0f, 0f, rotations[i]);
            corpse.transform.localScale = Vector3.one;
            SpriteRenderer renderer = corpse.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = Undo.AddComponent<SpriteRenderer>(corpse);
            Undo.RecordObject(renderer, "Configure hospital story corpse");
            renderer.sprite = sprite;
            renderer.sortingLayerName = "Player";
            renderer.sortingOrder = -2;
            renderer.color = new Color(0.72f, 0.68f, 0.62f, 1f);
            EditorUtility.SetDirty(corpse);
            EditorUtility.SetDirty(renderer);
        }

        EditorUtility.SetDirty(storyRoot);
    }

    private static Sprite LoadLastAnimationSprite(string clipPath)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null) return null;
        EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
        for (int i = 0; i < bindings.Length; i++)
        {
            ObjectReferenceKeyframe[] frames = AnimationUtility.GetObjectReferenceCurve(clip, bindings[i]);
            for (int frame = frames.Length - 1; frame >= 0; frame--)
                if (frames[frame].value is Sprite sprite) return sprite;
        }
        return null;
    }

    private static Tilemap FindDoorTilemap(Vector3 doorPosition, TileBase closedTile, TileBase openTile,
        out Vector3Int doorCell)
    {
        Tilemap[] tilemaps = UnityEngine.Object.FindObjectsByType<Tilemap>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < tilemaps.Length; i++)
        {
            Tilemap tilemap = tilemaps[i];
            if (tilemap == null || tilemap.name != "cuanha") continue;
            Vector3Int cell = tilemap.WorldToCell(doorPosition);
            TileBase tile = tilemap.GetTile(cell);
            if (tile != closedTile && tile != openTile) continue;
            doorCell = cell;
            return tilemap;
        }

        doorCell = default;
        return null;
    }
}
