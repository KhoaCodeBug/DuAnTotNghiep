using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[CustomEditor(typeof(QuestLocationIdentity))]
public sealed class QuestLocationIdentityEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("locationType"));

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("locationId"));
        }

        EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"));
        serializedObject.ApplyModifiedProperties();

        QuestLocationIdentity identity = (QuestLocationIdentity)target;
        if (string.IsNullOrWhiteSpace(identity.LocationId))
            EditorGUILayout.HelpBox("Location ID chưa được gán. Chạy Tools/Tin/Quest/Assign & Validate Location IDs.", MessageType.Warning);

        if (GUILayout.Button("Copy Location ID") && !string.IsNullOrWhiteSpace(identity.LocationId))
            EditorGUIUtility.systemCopyBuffer = identity.LocationId;
    }
}

public static class QuestLocationIdentityTools
{
    private const string HousePrefabFolder = "Assets/Khoa/House/";
    private const string OfficeRootName = "KhuVucNhiemVu";

    [MenuItem("Tools/Tin/Quest/Assign & Validate Location IDs")]
    public static void AssignAndValidateLoadedScenes()
    {
        int changed = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded)
                changed += AssignIdsToScene(scene);
        }

        Debug.Log($"[QUEST LOCATION] Hoàn tất kiểm tra ID. Đã thêm/sửa {changed} location trong các scene đang mở.");
    }

    public static int AssignIdsToScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return 0;

        List<GameObject> houseInstances = FindHousePrefabInstances(scene)
            .OrderBy(go => go.transform.position.x)
            .ThenBy(go => go.transform.position.y)
            .ThenBy(go => go.name, StringComparer.Ordinal)
            .ToList();

        List<QuestLocationIdentity> allIdentities = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<QuestLocationIdentity>(true))
            .ToList();

        HashSet<string> usedIds = new HashSet<string>(StringComparer.Ordinal);
        int nextHouseNumber = 1;
        int changed = 0;

        foreach (GameObject house in houseInstances)
        {
            bool locationChanged = false;
            QuestLocationIdentity identity = house.GetComponent<QuestLocationIdentity>();
            if (identity == null)
            {
                identity = Undo.AddComponent<QuestLocationIdentity>(house);
                allIdentities.Add(identity);
                locationChanged = true;
            }

            string id = identity.LocationId;
            bool mustReplace = string.IsNullOrWhiteSpace(id) ||
                               !id.StartsWith("HOUSE_", StringComparison.Ordinal) ||
                               id.Length < 3 ||
                               !usedIds.Add(id);
            if (mustReplace)
            {
                do
                {
                    id = $"HOUSE_{Sanitize(scene.name)}_{nextHouseNumber:000}";
                    nextHouseNumber++;
                }
                while (!usedIds.Add(id));
            }

            string prefabName = GetSourcePrefabName(house);
            string label = $"Nhà dân cư {id.Substring(id.Length - 3)} ({prefabName})";
            if (mustReplace || identity.LocationType != QuestLocationType.ResidentialHouse || identity.DisplayName != label)
            {
                identity.EditorSetIdentity(QuestLocationType.ResidentialHouse, id, label);
                locationChanged = true;
            }

            if (locationChanged)
                changed++;
        }

        GameObject office = FindSceneObject(scene, OfficeRootName);
        if (office != null)
        {
            bool locationChanged = false;
            QuestLocationIdentity identity = office.GetComponent<QuestLocationIdentity>();
            if (identity == null)
            {
                identity = Undo.AddComponent<QuestLocationIdentity>(office);
                locationChanged = true;
            }

            string officeId = $"OFFICE_PURPLE_{Sanitize(scene.name)}";
            if (identity.LocationId != officeId || identity.LocationType != QuestLocationType.PurpleOffice)
            {
                identity.EditorSetIdentity(QuestLocationType.PurpleOffice, officeId, "Văn phòng màu tím");
                locationChanged = true;
            }

            if (locationChanged)
                changed++;
        }

        // Report any remaining duplicates (including manually authored non-house locations).
        string[] duplicates = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<QuestLocationIdentity>(true))
            .Where(identity => identity.HasValidId)
            .GroupBy(identity => identity.LocationId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicates.Length > 0)
            Debug.LogError($"[QUEST LOCATION] ID còn bị trùng trong {scene.name}: {string.Join(", ", duplicates)}");

        if (changed > 0)
            EditorSceneManager.MarkSceneDirty(scene);

        return changed;
    }

    private static IEnumerable<GameObject> FindHousePrefabInstances(Scene scene)
    {
        HashSet<GameObject> uniqueRoots = new HashSet<GameObject>();
        foreach (GameObject sceneRoot in scene.GetRootGameObjects())
        {
            foreach (Transform transform in sceneRoot.GetComponentsInChildren<Transform>(true))
            {
                GameObject instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(transform.gameObject);
                if (instanceRoot == null || instanceRoot.scene != scene || !uniqueRoots.Add(instanceRoot))
                    continue;

                UnityEngine.Object source = PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot);
                string sourcePath = source == null ? string.Empty : AssetDatabase.GetAssetPath(source);
                if (sourcePath.StartsWith(HousePrefabFolder, StringComparison.OrdinalIgnoreCase))
                    yield return instanceRoot;
            }
        }
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name == objectName)
                    return transform.gameObject;
            }
        }

        return null;
    }

    private static string GetSourcePrefabName(GameObject instanceRoot)
    {
        UnityEngine.Object source = PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot);
        return source == null ? instanceRoot.name : source.name;
    }

    private static string Sanitize(string value)
    {
        char[] chars = value.ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray();
        return chars.Length == 0 ? "SCENE" : new string(chars);
    }
}
