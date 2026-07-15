using UnityEngine;
using UnityEditor;
using Photon.Voice.Fusion;

[InitializeOnLoad]
public class VoiceSetupHelper
{
    static VoiceSetupHelper()
    {
        // Tự động kiểm tra mỗi khi compile xong
        EditorApplication.delayCall += CheckAndFixPlayerPrefabs;
    }

    [MenuItem("Tools/Fix Voice Chat Prefabs")]
    public static void ManualFix()
    {
        CheckAndFixPlayerPrefabs();
        EditorUtility.DisplayDialog("Voice Setup Helper", "Đã kiểm tra và cập nhật thiết lập Voice Chat cho các Player Prefabs thành công!", "OK");
    }

    private static void CheckAndFixPlayerPrefabs()
    {
        string[] prefabPaths = new string[] 
        {
            "Assets/Prefab/Player.prefab",
            "Assets/Prefab/Player2.prefab"
        };

        foreach (string path in prefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[VoiceSetupHelper] Không tìm thấy prefab tại: {path}");
                continue;
            }

            bool isModified = false;

            // Kiểm tra VoiceNetworkObject
            VoiceNetworkObject voiceNetObj = prefab.GetComponent<VoiceNetworkObject>();
            if (voiceNetObj == null)
            {
                voiceNetObj = prefab.AddComponent<VoiceNetworkObject>();
                Debug.Log($"[VoiceSetupHelper] ✅ Đã thêm VoiceNetworkObject vào prefab: {path}");
                isModified = true;
            }

            if (isModified)
            {
                // Lưu lại thay đổi lên Prefab Asset
                PrefabUtility.SavePrefabAsset(prefab);
                Debug.Log($"[VoiceSetupHelper] 💾 Đã lưu thay đổi cho prefab: {path}");
            }
        }
    }
}
