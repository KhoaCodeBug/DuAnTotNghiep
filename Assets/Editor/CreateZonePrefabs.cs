using UnityEngine;
using UnityEditor;
using Fusion;
using System.IO;

public static class CreateZonePrefabs
{
    [MenuItem("Tools/Create Zone Prefabs")]
    public static void Create()
    {
        string dir = "Assets/Prefab/Zone_Base";
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        // Tạo GameObject tạm thời cho Prefab gốc
        GameObject baseGo = new GameObject("Zone_Base");
        baseGo.AddComponent<NetworkObject>();
        ZombieSpawnZone spawnZone = baseGo.AddComponent<ZombieSpawnZone>();
        spawnZone.useAutoConfig = true; // Bật sẵn chế độ tự động cho prefab

        string basePrefabPath = dir + "/Zone_Base.prefab";
        GameObject basePrefab = PrefabUtility.SaveAsPrefabAsset(baseGo, basePrefabPath);
        Object.DestroyImmediate(baseGo);

        if (basePrefab == null)
        {
            Debug.LogError("Không thể tạo Prefab gốc Zone_Base!");
            return;
        }

        // Tạo 6 Level Prefab Variants
        for (int i = 1; i <= 6; i++)
        {
            string variantPath = dir + "/Zone_Level" + i + ".prefab";
            
            // Tạo một instance tạm từ prefab gốc
            GameObject variantGo = PrefabUtility.InstantiatePrefab(basePrefab) as GameObject;
            if (variantGo != null)
            {
                variantGo.name = "Zone_Level" + i;
                ZombieSpawnZone zoneComp = variantGo.GetComponent<ZombieSpawnZone>();
                if (zoneComp != null)
                {
                    zoneComp.level = (ZombieSpawnZone.ZoneLevel)(i - 1);
                    zoneComp.useAutoConfig = true;
                    // Áp dụng sẵn cấu hình mặc định tương ứng với level để lưu lại dữ liệu ban đầu
                    zoneComp.ApplyLevelConfig(zoneComp.level);
                }
                
                // Lưu lại dưới dạng Prefab Variant liên kết với Prefab gốc
                PrefabUtility.SaveAsPrefabAssetAndConnect(variantGo, variantPath, InteractionMode.AutomatedAction);
                Object.DestroyImmediate(variantGo);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=green>[Thành Công] Đã tạo thành công thư mục Zone_Base và 6 Level Prefabs tại Assets/Prefab/Zone_Base!</color>");
    }
}
