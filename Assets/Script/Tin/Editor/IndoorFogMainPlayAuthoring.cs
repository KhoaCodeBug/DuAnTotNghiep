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
        Configure(store,
            RequireComponent<Collider2D>(RequireChild(store, "nocnha (2)")),
            Tilemaps(store, "tuongnha", "decord", "decord/decord2", "decord/decord (1)"),
            Array.Empty<SpriteRenderer>());
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
