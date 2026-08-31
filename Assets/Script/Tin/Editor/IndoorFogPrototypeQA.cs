using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

// Editor-only evidence capture. Does not author or save scene content.
public static class IndoorFogPrototypeQA
{
    public const string Folder = "QA_Artifacts/IndoorFogPrototype_20260831";
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int soloStep;
    private static double soloDeadline;
    private static PlayerMovement posePlayer;
    private static bool previousMovementEnabled;
    private static float previousDayMinutes;
    private static Vector3 previousCameraOffset;
    private const int MotionProbeFrames = 180;
    private static int motionProbeFrame = -1;
    private static int motionProbeUnityFrame = -1;
    private static PlayerMovement motionProbePlayer;
    private static StringBuilder motionProbeData;
    private static float[] motionProbePreviousRays;

    [Serializable] public class Pose
    {
        public string house = "nhachinhxaydautien (12)";
        public float x = -39.2f;
        public float y = 44.3f;
        public float hour = 13.5f;
        public float directionX = 0;
        public float directionY = 1;
        public float zoom = 3;
        public bool flashlight = true;
        public bool prototype;
        public string label = "baseline";
        public float cameraUp = 1.2f;
    }

    [MenuItem("Tools/QA/Indoor Fog/Start Solo Automation")]
    public static void StartSolo()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play in MainMenu first.");
        soloStep = 0;
        soloDeadline = EditorApplication.timeSinceStartup + 90;
        EditorApplication.update -= TickSolo;
        EditorApplication.update += TickSolo;
    }

    private static void TickSolo()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > soloDeadline)
        { EditorApplication.update -= TickSolo; return; }
        string[] labels = { "SOLO", "MEDIUM", "ENTER THE DEAD ZONE" };
        if (soloStep < labels.Length)
        {
            var button = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(b => b.interactable && b.GetComponentInChildren<TMPro.TMP_Text>() != null &&
                    b.GetComponentInChildren<TMPro.TMP_Text>().text.Trim() == labels[soloStep]);
            if (button != null) { soloStep++; button.onClick.Invoke(); }
        }
        else if (PlayerMovement.LocalPlayerInstance != null && GameplayReadinessCoordinator.IsReleasedToGameplay)
        { EditorApplication.update -= TickSolo; Debug.Log("[IndoorFogQA] Solo ready."); }
    }

    [MenuItem("Tools/QA/Indoor Fog/Apply Runtime Pose")]
    public static void ApplyPose()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Runtime-only fixture.");
        var pose = JsonUtility.FromJson<Pose>(File.ReadAllText(Folder + "/pose.json"));
        var player = PlayerMovement.LocalPlayerInstance;
        if (player == null || !player.HasStateAuthority) throw new InvalidOperationException("Solo authority player required.");
        if (posePlayer != player)
        {
            posePlayer = player;
            previousMovementEnabled = player.enabled;
            previousDayMinutes = DayNightManager.Instance.realMinutesPerDay;
            previousCameraOffset = PZ_CameraController.Instance.offset;
        }
        typeof(MilitaryBaseQuestManager).GetMethod("TeleportPlayer", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { player, new Vector2(pose.x, pose.y) });
        player.NetLastLookDir = new Vector2(pose.directionX, pose.directionY).normalized;
        player.enabled = false; // Stable reference shot; never persisted to prefab.
        if (player.flashlightTransform != null)
            player.flashlightTransform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(pose.directionY, pose.directionX) * Mathf.Rad2Deg - 90);
        var clock = DayNightManager.Instance;
        clock.CurrentTime = pose.hour;
        clock.realMinutesPerDay = 1000000f;
        var camera = PZ_CameraController.Instance;
        typeof(PZ_CameraController).GetField("targetZoom", Private).SetValue(camera, pose.zoom);
        camera.GetComponentInChildren<Camera>().orthographicSize = pose.zoom;
        camera.offset = new Vector3(0, pose.cameraUp, -10f);
        var inventory = player.GetComponent<InventorySystem>();
        for (int i = 0; i < inventory.slots.Count; i++)
        {
            var slot = inventory.slots[i];
            if (slot == null || !IsFlashlight(slot.item)) continue;
            if (i >= 5) typeof(InventorySystem).GetMethod("EquipFlashlightToHotbar", Private).Invoke(inventory, new object[] { i });
            break;
        }
        var flashlight = player.GetComponent<FlashlightController>();
        if (flashlight.IsFlashlightActive != pose.flashlight)
            for (int i = 0; i < Mathf.Min(5, inventory.slots.Count); i++)
                if (IsFlashlight(inventory.slots[i].item))
                { flashlight.TryToggleFromHotbar(i); break; }
        Physics2D.SyncTransforms();
        var root = UnityEngine.Object.FindObjectsByType<RoofVisibility>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .First(r => r.name == pose.house);
        var surface = root.GetComponent<IndoorFogSurfaceMap>();
        if (pose.prototype)
        {
            if (surface == null) surface = root.gameObject.AddComponent<IndoorFogSurfaceMap>();
            surface.indoorVolume = root.GetComponentsInChildren<Collider2D>().First(c => c.isTrigger && c.GetComponent<Tilemap>() != null);
            surface.surfaces = root.GetComponentsInChildren<Tilemap>().Where(m => m.name == "tuongnha (1)" || m.name == "Trangtri").ToArray();
            surface.spriteSurfaces = root.GetComponentsInChildren<SpriteRenderer>();
            surface.enabled = true;
        }
        else if (surface != null) surface.enabled = false;
        Debug.Log("[IndoorFogQA] Applied pose " + JsonUtility.ToJson(pose));
    }

    [MenuItem("Tools/QA/Indoor Fog/Capture Game View")]
    public static void Capture()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Runtime-only capture.");
        var pose = JsonUtility.FromJson<Pose>(File.ReadAllText(Folder + "/pose.json"));
        string label = System.IO.Path.GetFileName(pose.label);
        ScreenCapture.CaptureScreenshot(Folder + "/" + label + ".png");
        var player = PlayerMovement.LocalPlayerInstance;
        var vision = player.GetComponent<PlayerVision>();
        var fog = FogVisionController.Instance;
        var material = (Material)typeof(FogVisionController).GetField("overlayMaterial", Private).GetValue(fog);
        var surface = vision.ActiveIndoorCollider != null ? vision.ActiveIndoorCollider.GetComponentInParent<IndoorFogSurfaceMap>() : null;
        File.WriteAllText(Folder + "/" + label + "-state.txt", JsonUtility.ToJson(pose, true) +
            "\nactual=" + player.transform.position + " direction=" + vision.VisionWorldDirection +
            "\nhealth=" + player.GetComponent<PlayerHealth>().CurrentHealthSafe + " dead=" + player.GetComponent<PlayerHealth>().isDead +
            "\nindoor=" + (vision.ActiveIndoorCollider != null ? PathOf(vision.ActiveIndoorCollider.transform) : "none") +
            "\nmask=" + material.GetFloat("_IndoorActive") + " surface=" + material.GetFloat("_IndoorSurfaceActive") +
            " flashlight=" + material.GetFloat("_FlashlightActive") + " angle=" + vision.CurrentVisionAngle +
            "\nlight=" + vision.playerLight.intensity + " global=" + DayNightManager.Instance.globalLight.intensity +
            "\nsurfaces=" + (surface != null ? surface.SurfaceCount : 0) + " buildMs=" + (surface != null ? surface.LastBuildMilliseconds : 0));
    }

    [MenuItem("Tools/QA/Indoor Fog/Dump Surface Atlas")]
    public static void DumpSurfaceAtlas()
    {
        if (!EditorApplication.isPlaying || PlayerMovement.LocalPlayerInstance == null)
            throw new InvalidOperationException("Runtime-only atlas capture.");
        var vision = PlayerMovement.LocalPlayerInstance.GetComponent<PlayerVision>();
        var surface = vision.ActiveIndoorCollider != null
            ? vision.ActiveIndoorCollider.GetComponentInParent<IndoorFogSurfaceMap>() : null;
        if (surface == null || !surface.EnsureAtlas()) throw new InvalidOperationException("Active surface atlas required.");
        Directory.CreateDirectory(Folder + "/V2_Diagnostic");
        RenderTexture previous = RenderTexture.active;
        var source = new Texture2D(surface.Atlas.width, surface.Atlas.height, TextureFormat.RGBAFloat, false, true);
        var raw = new Texture2D(surface.Atlas.width, surface.Atlas.height, TextureFormat.RGBA32, false, true);
        var mapped = new Texture2D(surface.Atlas.width, surface.Atlas.height, TextureFormat.RGBA32, false, true);
        try
        {
            RenderTexture.active = surface.Atlas;
            source.ReadPixels(new Rect(0, 0, surface.Atlas.width, surface.Atlas.height), 0, 0);
            source.Apply();
            Color[] pixels = source.GetPixels();
            var rawPixels = new Color[pixels.Length];
            var mappedPixels = new Color[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                Color pixel = pixels[i];
                if (pixel.a < 0.5f) { rawPixels[i] = Color.clear; mappedPixels[i] = Color.clear; continue; }
                rawPixels[i] = new Color(pixel.r, pixel.g, 0f, 1f);
                mappedPixels[i] = Color.HSVToRGB(Mathf.Repeat(pixel.r * 0.37f + pixel.g * 0.63f, 1f), 0.9f, 1f);
            }
            raw.SetPixels(rawPixels); raw.Apply();
            mapped.SetPixels(mappedPixels); mapped.Apply();
            File.WriteAllBytes(Folder + "/V2_Diagnostic/atlas-rg.png", raw.EncodeToPNG());
            File.WriteAllBytes(Folder + "/V2_Diagnostic/atlas-mapped.png", mapped.EncodeToPNG());
            Debug.Log("[IndoorFogQA] Surface atlas diagnostic captured.");
        }
        finally
        {
            RenderTexture.active = previous;
            UnityEngine.Object.Destroy(source);
            UnityEngine.Object.Destroy(raw);
            UnityEngine.Object.Destroy(mapped);
        }
    }

    [MenuItem("Tools/QA/Indoor Fog/Diagnose Near Wall Motion")]
    public static void DiagnoseNearWallMotion()
    {
        if (!EditorApplication.isPlaying || PlayerMovement.LocalPlayerInstance == null)
            throw new InvalidOperationException("Start Solo and apply the prototype pose first.");
        StopMotionProbe();
        Directory.CreateDirectory(Folder + "/V2_Diagnostic");
        motionProbePlayer = PlayerMovement.LocalPlayerInstance;
        motionProbePlayer.enabled = false;
        motionProbePlayer.NetLastLookDir = Vector2.up;
        motionProbeData = new StringBuilder("sample,unityFrame,time,phase,x,y,scanX,scanY,originMismatch,maxAdjacentJump,maxAdjacentIndex,maxRayDelta,changedRays,bigChangedRays,indoorActive,surfaceActive,nextScanIn\n");
        motionProbePreviousRays = null;
        motionProbeFrame = 0;
        motionProbeUnityFrame = -1;
        SetMotionProbePosition(MotionProbePosition(0));
        EditorApplication.update += MotionProbeTick;
        Debug.Log("[IndoorFogQA] Near-wall motion diagnostic started.");
    }

    private static Vector2 MotionProbePosition(int sample)
    {
        // First approach the front wall, then move parallel to it. Both paths stay
        // inside the selected house and reproduce the user's near-wall movement.
        if (sample < 60)
            return Vector2.Lerp(new Vector2(-39.2f, 45.7f), new Vector2(-39.2f, 44.63f), sample / 59f);
        // Follow the actual isometric front edge while remaining inside its trigger.
        return Vector2.Lerp(new Vector2(-39.7f, 44.36f), new Vector2(-37.5f, 45.56f), (sample - 60) / 119f);
    }

    private static void SetMotionProbePosition(Vector2 position)
    {
        typeof(MilitaryBaseQuestManager).GetMethod("TeleportPlayer", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { motionProbePlayer, position });
        Physics2D.SyncTransforms();
    }

    private static void MotionProbeTick()
    {
        if (!EditorApplication.isPlaying || motionProbePlayer == null)
        { StopMotionProbe(); return; }
        if (Time.frameCount == motionProbeUnityFrame) return;
        motionProbeUnityFrame = Time.frameCount;

        FogVisionController fog = FogVisionController.Instance;
        if (fog == null) return;
        var distances = (float[])typeof(FogVisionController).GetField("indoorOcclusionDistances", Private).GetValue(fog);
        Vector2 scanOrigin = (Vector2)typeof(FogVisionController).GetField("lastOcclusionOrigin", Private).GetValue(fog);
        float nextScan = (float)typeof(FogVisionController).GetField("nextIndoorOcclusionUpdate", Private).GetValue(fog);
        Vector2 playerPosition = motionProbePlayer.transform.position;
        float maxAdjacent = 0f;
        int maxAdjacentIndex = -1;
        float maxDelta = 0f;
        int changedRays = 0;
        int bigChangedRays = 0;
        for (int i = 0; i < distances.Length; i++)
        {
            float adjacent = Mathf.Abs(distances[i] - distances[(i + 1) % distances.Length]);
            if (adjacent > maxAdjacent) { maxAdjacent = adjacent; maxAdjacentIndex = i; }
            if (motionProbePreviousRays == null) continue;
            float delta = Mathf.Abs(distances[i] - motionProbePreviousRays[i]);
            maxDelta = Mathf.Max(maxDelta, delta);
            if (delta > 0.01f) changedRays++;
            if (delta > 1f) bigChangedRays++;
        }
        var material = (Material)typeof(FogVisionController).GetField("overlayMaterial", Private).GetValue(fog);
        motionProbeData.Append(motionProbeFrame).Append(',').Append(Time.frameCount).Append(',')
            .Append(Time.unscaledTime.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',')
            .Append(motionProbeFrame < 60 ? "approach" : "parallel").Append(',')
            .Append(playerPosition.x.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',')
            .Append(playerPosition.y.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',')
            .Append(scanOrigin.x.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',')
            .Append(scanOrigin.y.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',')
            .Append(Vector2.Distance(playerPosition, scanOrigin).ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',')
            .Append(maxAdjacent.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',').Append(maxAdjacentIndex).Append(',')
            .Append(maxDelta.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',').Append(changedRays).Append(',').Append(bigChangedRays).Append(',')
            .Append(material.GetFloat("_IndoorActive").ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',')
            .Append(material.GetFloat("_IndoorSurfaceActive").ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',')
            .Append((nextScan - Time.unscaledTime).ToString(System.Globalization.CultureInfo.InvariantCulture)).AppendLine();
        motionProbePreviousRays = (float[])distances.Clone();

        if (motionProbeFrame % 15 == 0 || motionProbeFrame == MotionProbeFrames - 1)
            ScreenCapture.CaptureScreenshot(Folder + "/V2_Diagnostic/near-wall-" + motionProbeFrame.ToString("D3") + ".png");
        if (motionProbeFrame >= 90 && motionProbeFrame < 120)
            ScreenCapture.CaptureScreenshot(Folder + "/V2_Diagnostic/burst-" + motionProbeFrame.ToString("D3") + ".png");

        motionProbeFrame++;
        if (motionProbeFrame >= MotionProbeFrames)
        {
            Directory.CreateDirectory(Folder + "/V2_Diagnostic");
            File.WriteAllText(Folder + "/V2_Diagnostic/near-wall-motion.csv", motionProbeData.ToString());
            Debug.Log("[IndoorFogQA] Near-wall motion diagnostic complete.");
            StopMotionProbe();
            return;
        }
        SetMotionProbePosition(MotionProbePosition(motionProbeFrame));
    }

    private static void StopMotionProbe()
    {
        EditorApplication.update -= MotionProbeTick;
        motionProbeFrame = -1;
        motionProbeUnityFrame = -1;
        motionProbePlayer = null;
        motionProbeData = null;
        motionProbePreviousRays = null;
    }

    private static Unity.Profiling.ProfilerRecorder[] recorders;
    private static readonly string[] CounterNames = { "CPU Main Thread Frame Time", "CPU Render Thread Frame Time", "GPU Frame Time", "Draw Calls Count", "Batches Count", "FogVision.UpdateMaterial" };
    private static StringBuilder profileData;
    private static int profileFrame, lastProfileFrame;
    private static string profileLabel;

    [MenuItem("Tools/QA/Indoor Fog/Profile 240 Frames")]
    public static void Profile()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Runtime-only profiling.");
        StopProfile();
        profileLabel = Path.GetFileName(JsonUtility.FromJson<Pose>(File.ReadAllText(Folder + "/pose.json")).label);
        recorders = new Unity.Profiling.ProfilerRecorder[CounterNames.Length];
        for (int i = 0; i < CounterNames.Length; i++)
            recorders[i] = Unity.Profiling.ProfilerRecorder.StartNew(i == 5 ? Unity.Profiling.ProfilerCategory.Scripts : Unity.Profiling.ProfilerCategory.Render, CounterNames[i], 1);
        profileData = new StringBuilder("frame,deltaMs," + string.Join(",", CounterNames) + "\n");
        profileFrame = 0; lastProfileFrame = -1;
        EditorApplication.update += ProfileTick;
    }

    private static void ProfileTick()
    {
        if (!EditorApplication.isPlaying) { StopProfile(); return; }
        if (Time.frameCount == lastProfileFrame) return;
        lastProfileFrame = Time.frameCount;
        profileFrame++;
        if (profileFrame > 30)
        {
            profileData.Append(Time.frameCount).Append(',').Append((Time.unscaledDeltaTime * 1000).ToString(System.Globalization.CultureInfo.InvariantCulture));
            foreach (var recorder in recorders) profileData.Append(',').Append(recorder.Valid ? recorder.LastValue : -1);
            profileData.AppendLine();
        }
        if (profileFrame < 270) return;
        File.WriteAllText(Folder + "/" + profileLabel + "-profile.csv", profileData.ToString());
        StopProfile();
        Debug.Log("[IndoorFogQA] Profile complete: " + profileLabel);
    }

    private static void StopProfile()
    {
        EditorApplication.update -= ProfileTick;
        if (recorders != null) foreach (var recorder in recorders) recorder.Dispose();
        recorders = null;
    }

    private static bool IsFlashlight(ItemData item) => item != null &&
        (item.name == FlashlightController.ItemId || item.itemName == FlashlightController.ItemId);

    [MenuItem("Tools/QA/Indoor Fog/Return Manual Control")]
    public static void ReturnManualControl()
    {
        if (!EditorApplication.isPlaying || posePlayer == null || posePlayer != PlayerMovement.LocalPlayerInstance) return;
        StopProfile();
        PlayerMovement.LocalPlayerInstance.enabled = previousMovementEnabled;
        DayNightManager.Instance.realMinutesPerDay = previousDayMinutes;
        PZ_CameraController.Instance.offset = previousCameraOffset;
        posePlayer = null;
        Debug.Log("[IndoorFogQA] Manual controls restored; prototype remains in this runtime house only.");
    }

    [MenuItem("Tools/QA/Indoor Fog/Inspect House Tiles")]
    public static void InspectHouseTiles()
    {
        var pose = JsonUtility.FromJson<Pose>(File.ReadAllText(Folder + "/pose.json"));
        var root = UnityEngine.Object.FindObjectsByType<RoofVisibility>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .First(r => r.name == pose.house);
        var text = new StringBuilder();
        foreach (var map in root.GetComponentsInChildren<Tilemap>(true))
        {
            var r = map.GetComponent<TilemapRenderer>();
            text.AppendLine("MAP " + PathOf(map.transform) + " matrix=" + map.transform.localToWorldMatrix +
                " mode=" + r.mode + " sort=" + r.sortingLayerName + "/" + r.sortingOrder);
            var tiles = new TileBase[map.GetUsedTilesCount()]; map.GetUsedTilesNonAlloc(tiles);
            var cells = map.cellBounds.allPositionsWithin;
            foreach (var cell in cells)
            {
                var sprite = map.GetSprite(cell); if (sprite == null) continue;
                text.AppendLine(" CELL " + cell + " world=" + map.GetCellCenterWorld(cell) + " sprite=" + sprite.name +
                    " bounds=" + sprite.bounds + " pivot=" + sprite.pivot + " PPU=" + sprite.pixelsPerUnit +
                    " transform=" + map.GetTransformMatrix(cell));
                if (!tiles.Contains(map.GetTile(cell))) continue;
                var points = new List<Vector2>();
                for (int s = 0; s < sprite.GetPhysicsShapeCount(); s++)
                { sprite.GetPhysicsShape(s, points); text.AppendLine("  PHYSICS " + string.Join(";", points)); }
                tiles[Array.IndexOf(tiles, map.GetTile(cell))] = null;
            }
        }
        File.WriteAllText(Folder + "/house-tiles.txt", text.ToString());
    }

    [MenuItem("Tools/QA/Indoor Fog/Inspect Scene")]
    public static void InspectScene()
    {
        Directory.CreateDirectory(Folder);
        var text = new StringBuilder();
        foreach (var roof in UnityEngine.Object.FindObjectsByType<RoofVisibility>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            text.AppendLine("ROOF " + PathOf(roof.transform));
            foreach (var col in roof.GetComponentsInChildren<Collider2D>(true))
                text.AppendLine("  COLLIDER " + col.name + " " + col.GetType().Name + " " + col.bounds + " trigger=" + col.isTrigger);
        }
        foreach (var tile in UnityEngine.Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var renderer = tile.GetComponent<TilemapRenderer>();
            text.AppendLine("TILE " + PathOf(tile.transform) + " bounds=" + (renderer != null ? renderer.bounds.ToString() : "none") +
                " cells=" + tile.cellBounds + " tiles=" + tile.GetUsedTilesCount() + " anchor=" + tile.tileAnchor +
                " material=" + (renderer != null && renderer.sharedMaterial != null ? renderer.sharedMaterial.name : "none"));
        }
        var player = PlayerMovement.LocalPlayerInstance;
        if (player != null)
        {
            var vision = player.GetComponent<PlayerVision>();
            text.AppendLine("PLAYER " + player.transform.position + " direction=" + vision.VisionWorldDirection + " indoor=" +
                (vision.ActiveIndoorCollider != null ? PathOf(vision.ActiveIndoorCollider.transform) : "none"));
        }
        File.WriteAllText(Folder + "/scene.txt", text.ToString());
        Debug.Log("[IndoorFogQA] Scene evidence: " + Folder + "/scene.txt");
    }

    [MenuItem("Tools/QA/Indoor Fog/Press Solo Flow")]
    public static void PressSoloFlow()
    {
        foreach (string label in new[] { "SOLO", "MEDIUM", "ENTER THE DEAD ZONE" })
        {
            var button = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(b => b.interactable && b.GetComponentInChildren<TMPro.TMP_Text>() != null &&
                    b.GetComponentInChildren<TMPro.TMP_Text>().text.Trim() == label);
            if (button != null) { button.onClick.Invoke(); Debug.Log("[IndoorFogQA] Pressed " + label); return; }
        }
        Debug.LogWarning("[IndoorFogQA] No matching active Solo button.");
    }

    public static string PathOf(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null) { transform = transform.parent; path = transform.name + "/" + path; }
        return path;
    }
}
