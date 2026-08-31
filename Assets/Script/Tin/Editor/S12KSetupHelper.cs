using UnityEditor;
using UnityEngine;

public static class S12KSetupHelper
{
    [MenuItem("Tools/Setup S12K Complete")]
    public static void SetupS12K()
    {
        // 1. Load các asset cần thiết
        ItemData s12k = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Resources/Items/S12K.asset");
        ItemData ammo12g = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Resources/Items/Ammo12Gauge.asset");

        if (s12k == null) { Debug.LogError("[S12K Setup] Không tìm thấy S12K.asset!"); return; }
        if (ammo12g == null) { Debug.LogError("[S12K Setup] Không tìm thấy Ammo12Gauge.asset!"); return; }

        // 2. Gán ammoTypeRequired cho S12K -> 12 Gauge Ammo
        s12k.ammoTypeRequired = ammo12g;
        EditorUtility.SetDirty(s12k);
        Debug.Log("[S12K Setup] ✅ Đã gán ammoTypeRequired = 12 Gauge Ammo cho S12K!");

        // 3. Thêm 12 Gauge Ammo vào LootTable MacDinh_KhuSanh
        LootTableSO lootTable = AssetDatabase.LoadAssetAtPath<LootTableSO>("Assets/Khoa/Code/LootTableS/MacDinh_KhuSanh.asset");
        if (lootTable != null)
        {
            // Kiểm tra xem đã có 12 Gauge Ammo chưa
            bool alreadyExists = false;
            foreach (var rule in lootTable.lootRules)
            {
                if (rule.itemPrefab != null && rule.itemPrefab.itemName == "12 Gauge Ammo")
                {
                    alreadyExists = true;
                    break;
                }
            }

            if (!alreadyExists)
            {
                var newRule = new LootContainer.LootSpawnData();
                newRule.itemPrefab = ammo12g;
                newRule.dropChance = 15f;
                newRule.minAmount = 5;
                newRule.maxAmount = 5;
                lootTable.lootRules.Add(newRule);
                EditorUtility.SetDirty(lootTable);
                Debug.Log("[S12K Setup] ✅ Đã thêm 12 Gauge Ammo vào loot table (15% chance, cố định 5 viên)!");
            }
            else
            {
                Debug.Log("[S12K Setup] ⚠️ 12 Gauge Ammo đã có trong loot table rồi!");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[S12K Setup] ✅ HOÀN TẤT! Tất cả đã được cấu hình xong.");
    }
}
