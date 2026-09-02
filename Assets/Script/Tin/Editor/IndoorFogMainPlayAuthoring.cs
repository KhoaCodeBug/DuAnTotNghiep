using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// One-shot, explicit authoring for the known MainPlay buildings. Runtime code
/// consumes only the serialized references produced here; it never scans names.
/// </summary>
public static class IndoorFogMainPlayAuthoring
{
    public const string HousePrefabPath = "Assets/Khoa/House/nhachinhxaydautien.prefab";
    public const string MainScenePath = "Assets/Scenes/Main.unity";
    public const int MainVolumeCount = 78;

    [MenuItem("Tools/Environment/Indoor Fog/Apply All Main Buildings")]
    public static void ApplyAllMainBuildings()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Authoring requires Edit Mode.");
        Scene scene = SceneManager.GetSceneByPath(MainScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (!opened && scene.isDirty) throw new InvalidOperationException("Save/review existing Main edits first.");
        if (opened) scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Additive);
        try
        {
            Transform mapRoot = FindMapRoot(scene);
            var pending = new List<Action>();
            foreach (Transform root in mapRoot)
            {
                if (root.GetComponentsInChildren<RoofVisibility>(true).Length == 0) continue;
                // Existing pilot references are preserved, including the two Hospital volumes.
                if (root.GetComponent<IndoorFogSurfaceMap>() != null)
                { ValidateConfigured(root, root.name); continue; }
                string family = System.Text.RegularExpressions.Regex.Replace(root.name, @" \(\d+\)$", "");
                string roof;
                string[] paths;
                switch (family)
                {
                    case "nhamauxam": roof = "nocnha (4)";
                        paths = new[] { "tuongnha", "tuongnha/tuongnha1", "decord", "decord/decord1" }; break;
                    case "nhamauxanhla": roof = "nocnha (5)";
                        paths = new[] { "tuongnha", "tuongnha (1)", "decord", "decord/decord1", "decord/decord1 (1)" }; break;
                    case "cannhasieuvipprodachinhsua": roof = "nocnha (3)";
                        paths = new[] { "tuongnha", "tuongnha/tuongnha2", "tuongnha (1)", "decord", "decord/decord1" }; break;
                    case "cannhamauxamhoanchinh": roof = "nocnha (4)";
                        paths = new[] { "tuongnha1", "decord1", "decord" }; break;
                    case "cannhatotamhoanchinh": roof = "nocnha (4)";
                        paths = new[] { "tuongnha", "decord", "decord/decord1", "decord/decord1 (1)" }; break;
                    case "chungcumaucamdachinhsua": roof = "nocnha (5)";
                        paths = new[] { "tuongnha3", "tuongnha", "decord", "decord/decord2" }; break;
                    case "SieuThi_FIX": roof = "nocnha (4)";
                        paths = new[] { "tuong (1)", "decords (1)" }; break;
                    case "cuahang_FIX": roof = "nocnha (2)";
                        paths = new[] { "tuongnha", "decord", "decord/decord2", "decord/decord (1)" }; break;
                    default: throw new InvalidOperationException("Unaudited building family: " + root.name);
                }
                Transform target = root;
                Collider2D volume = RequireComponent<Collider2D>(RequireChild(root, roof));
                Tilemap[] surfaces = Tilemaps(root, paths);
                if (!volume.isTrigger) throw new InvalidOperationException(root.name + " roof volume is not a trigger.");
                SpriteRenderer[] sprites = root.GetComponentsInChildren<SpriteRenderer>(true);
                pending.Add(() => Configure(target, volume, surfaces, sprites));
            }
            // Resolve every audited path before making the first scene edit.
            foreach (Action apply in pending) apply();
            // This store has two overlapping authored roof triggers. Physics may pick
            // either one; share presentation without changing RoofDetector/gameplay.
            ConfigureConvenienceStore(RequireChild(mapRoot, "cuahang_FIX"));
            ValidateAllMainBuildings(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save Main rollout.");
            Debug.Log("[IndoorFogAuthoring] Applied " + pending.Count + " additional Main volumes; total=" + MainVolumeCount);
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    public static void ValidateAllMainBuildings(Scene scene)
    {
        var roots = FindMapRoot(scene).Cast<Transform>()
            .Where(root => root.GetComponentsInChildren<RoofVisibility>(true).Length > 0).ToArray();
        var maps = roots.SelectMany(root => root.GetComponentsInChildren<IndoorFogSurfaceMap>(true)).ToArray();
        if (maps.Length != MainVolumeCount || maps.Select(m => m.indoorVolume).Distinct().Count() != maps.Length)
            throw new InvalidOperationException("Main must contain exactly " + MainVolumeCount + " unique authored volumes.");
        foreach (var map in maps)
        {
            ValidateConfigured(map.transform, map.name);
            if (map.additionalIndoorVolumes.Any(c => c == null || !c.isTrigger ||
                c.GetComponentInParent<IndoorFogSurfaceMap>() != map))
                throw new InvalidOperationException("Invalid explicit roof alias: " + map.name);
            if (map.surfaces.Any(t => t.name.StartsWith("__ColliderProxy_") || t.name.StartsWith("nocnha") || t.name.StartsWith("nennha")))
                throw new InvalidOperationException("Roof/floor/proxy accidentally included: " + map.name);
        }
        foreach (Transform root in roots)
            if (root.GetComponentsInChildren<IndoorFogSurfaceMap>(true).Length != root.GetComponentsInChildren<RoofVisibility>(true).Length)
                throw new InvalidOperationException("Uncovered roof in " + root.name);
    }

    [MenuItem("Tools/Environment/Indoor Fog/Audit All Main Buildings")]
    public static void AuditAllMainBuildings()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Authoring audit requires Edit Mode.");
        Scene scene = SceneManager.GetSceneByPath(MainScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Additive);
        try
        {
            Transform mapRoot = FindMapRoot(scene);
            var report = new System.Text.StringBuilder();
            foreach (Transform root in mapRoot)
            {
                var roofs = root.GetComponentsInChildren<RoofVisibility>(true);
                if (roofs.Length == 0) continue;
                report.AppendLine("BUILDING " + root.name);
                foreach (var roof in roofs)
                    report.AppendLine(" ROOF " + RelativePath(root, roof.transform) + " hides=" +
                        string.Join("|", roof.roofTilemaps.Where(t => t != null).Select(t => RelativePath(root, t.transform))));
                foreach (var volume in root.GetComponentsInChildren<Collider2D>(true).Where(c => c.isTrigger))
                    report.AppendLine(" TRIGGER " + RelativePath(root, volume.transform) + " " + volume.GetType().Name +
                        " bounds=" + volume.bounds);
                foreach (var surface in root.GetComponentsInChildren<IndoorFogSurfaceMap>(true))
                    report.AppendLine(" FOG " + RelativePath(root, surface.transform) + " volume=" +
                        (surface.indoorVolume != null ? RelativePath(root, surface.indoorVolume.transform) : "NULL"));
                foreach (var tilemap in root.GetComponentsInChildren<Tilemap>(true))
                    report.AppendLine(" TILEMAP " + RelativePath(root, tilemap.transform) + " layer=" +
                        LayerMask.LayerToName(tilemap.gameObject.layer) + " tiles=" + tilemap.GetUsedTilesCount());
            }
            System.IO.Directory.CreateDirectory("QA_Artifacts/IndoorFogAllMain_20260902");
            System.IO.File.WriteAllText("QA_Artifacts/IndoorFogAllMain_20260902/building-audit.txt", report.ToString());
            Debug.Log("[IndoorFogAuthoring] All-Main building audit written.");
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    private static string RelativePath(Transform root, Transform child)
    {
        if (root == child) return ".";
        return AnimationUtility.CalculateTransformPath(child, root);
    }

    [MenuItem("Tools/Environment/Indoor Fog/Apply Pilot (House + School + Hospital)")]
    public static void ApplyPilot()
    {
        ConfigureHousePrefab();
        Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        Transform mapRoot = FindMapRoot(scene);
        ConfigureSchool(RequireChild(mapRoot, "school"));
        ConfigureHospital(RequireChild(mapRoot, "hospital"));
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Could not save Main scene Indoor Fog pilot authoring.");
        AssetDatabase.SaveAssets();
        Debug.Log("[IndoorFogAuthoring] Applied pilot to the shared house prefab, School and Hospital.");
    }

    [MenuItem("Tools/Environment/Indoor Fog/Apply Smoke Sites (Supermarket + Convenience Store)")]
    public static void ApplySmokeSites()
    {
        Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        Transform mapRoot = FindMapRoot(scene);
        ConfigureSupermarket(RequireChild(mapRoot, "SieuThi_FIX"));
        ConfigureConvenienceStore(RequireChild(mapRoot, "cuahang_FIX"));
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Could not save Main scene Indoor Fog smoke-site authoring.");
        AssetDatabase.SaveAssets();
        Debug.Log("[IndoorFogAuthoring] Applied smoke-site authoring to Supermarket and Convenience Store.");
    }

    [MenuItem("Tools/Environment/Indoor Fog/Validate MainPlay Authoring")]
    public static void ValidateMainPlay()
    {
        GameObject houseRoot = PrefabUtility.LoadPrefabContents(HousePrefabPath);
        try { ValidateConfigured(houseRoot.transform, "Shared house prefab"); }
        finally { PrefabUtility.UnloadPrefabContents(houseRoot); }

        Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        Transform mapRoot = FindMapRoot(scene);
        ValidateConfigured(RequireChild(mapRoot, "school"), "School");
        Transform hospital = RequireChild(mapRoot, "hospital");
        ValidateConfigured(hospital, "Hospital large volume");
        ValidateConfigured(RequireChild(hospital, "Hospital_Small_FIXED"), "Hospital small volume");
        Debug.Log("[IndoorFogAuthoring] Pilot references are valid.");
    }

    public static void ConfigureHousePrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(HousePrefabPath);
        try
        {
            Configure(root.transform,
                RequireComponent<Collider2D>(RequireChild(root.transform, "nocnha (1)")),
                Tilemaps(root.transform, "tuongnha (1)", "Trangtri"),
                root.GetComponentsInChildren<SpriteRenderer>(true));
            PrefabUtility.SaveAsPrefabAsset(root, HousePrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    public static void ConfigureSchool(Transform school)
    {
        Transform trigger = RequireChild(school, "__SchoolRoofTrigger_FIXED");
        Configure(school, RequireComponent<Collider2D>(trigger),
            Tilemaps(school, "tuong1", "decordlophoc", "decordlophoc/decord", "dantuong", "cual", "cual/cua2"),
            school.GetComponentsInChildren<SpriteRenderer>(true));
    }

    public static void ConfigureHospital(Transform hospital)
    {
        Tilemap[] sharedSurfaces = Tilemaps(hospital,
            "decord", "trangtrituong", "trangtrisautuong", "trangtrisautuong/cuanha",
            "tuongnen", "trangtrigiay", "decords", "Hospital_Large_FIXED/tuongnha",
            "Hospital_Small_FIXED/tuongnha");
        Transform largeRoof = RequireChild(hospital, "trangtrisautuong/nocnha");
        Configure(hospital, RequireComponent<Collider2D>(largeRoof), sharedSurfaces, Array.Empty<SpriteRenderer>());

        Transform small = RequireChild(hospital, "Hospital_Small_FIXED");
        Transform smallRoof = RequireChild(small, "nocnha (1)");
        Configure(small, RequireComponent<Collider2D>(smallRoof), sharedSurfaces, Array.Empty<SpriteRenderer>());
    }

    public static void ConfigureSupermarket(Transform supermarket)
    {
        Configure(supermarket,
            RequireComponent<Collider2D>(RequireChild(supermarket, "nocnha (4)")),
            Tilemaps(supermarket, "tuong (1)", "decords (1)"), Array.Empty<SpriteRenderer>());
    }

    public static void ConfigureConvenienceStore(Transform store)
    {
        var map = Configure(store,
            RequireComponent<Collider2D>(RequireChild(store, "nocnha (2)")),
            Tilemaps(store, "tuongnha", "decord", "decord/decord2", "decord/decord (1)"),
            Array.Empty<SpriteRenderer>());
        map.additionalIndoorVolumes = new[] { RequireComponent<Collider2D>(RequireChild(store, "Trigger")) };
    }

    private static IndoorFogSurfaceMap Configure(Transform root, Collider2D indoorVolume,
        Tilemap[] surfaces, SpriteRenderer[] spriteSurfaces)
    {
        IndoorFogSurfaceMap map = root.GetComponent<IndoorFogSurfaceMap>();
        if (map == null) map = root.gameObject.AddComponent<IndoorFogSurfaceMap>();
        map.indoorVolume = indoorVolume;
        map.surfaces = surfaces.Where(surface => surface != null).Distinct().ToArray();
        map.spriteSurfaces = spriteSurfaces.Where(renderer => renderer != null).Distinct().ToArray();
        map.atlasResolution = 1024;
        map.surfaceProbeInset = 0.08f;
        map.dayAmbientOpacity = 0.86f;
        map.nightAmbientOpacity = 0.15f;
        map.litOpacity = 0.08f;
        map.coneInset = 0.06f;
        map.flashlightConeFeather = 0.20f;
        map.flashlightBoundaryFadeDistance = 0.65f;
        EditorUtility.SetDirty(map);
        return map;
    }

    private static Transform FindMapRoot(Scene scene)
    {
        Transform map = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(candidate => candidate.name == "Map" && candidate.GetComponent<Grid>() != null);
        return map != null ? map : throw new InvalidOperationException("Main scene Map/Grid root was not found.");
    }

    private static Transform RequireChild(Transform root, string relativePath)
    {
        Transform child = root.Find(relativePath);
        return child != null ? child : throw new InvalidOperationException(
            "Missing Indoor Fog authoring path: " + root.name + "/" + relativePath);
    }

    private static T RequireComponent<T>(Transform target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : throw new InvalidOperationException(
            "Missing " + typeof(T).Name + " on " + target.name);
    }

    private static Tilemap[] Tilemaps(Transform root, params string[] paths)
    {
        var result = new List<Tilemap>(paths.Length);
        foreach (string path in paths)
            result.Add(RequireComponent<Tilemap>(RequireChild(root, path)));
        return result.ToArray();
    }

    private static void ValidateConfigured(Transform root, string label)
    {
        IndoorFogSurfaceMap map = root.GetComponent<IndoorFogSurfaceMap>();
        if (map == null || map.indoorVolume == null || map.surfaces == null || map.surfaces.Length == 0 ||
            map.surfaces.Any(surface => surface == null) || map.spriteSurfaces == null)
            throw new InvalidOperationException(label + " Indoor Fog references are incomplete.");
        if (!map.indoorVolume.isTrigger)
            throw new InvalidOperationException(label + " indoor volume must remain a trigger.");
    }
}
