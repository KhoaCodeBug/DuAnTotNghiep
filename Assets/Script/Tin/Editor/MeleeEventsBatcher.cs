#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

[InitializeOnLoad]
public static class MeleeEventsBatcher
{
    static MeleeEventsBatcher()
    {
        EditorApplication.delayCall += BatchAddMeleeEvents;
    }

    [MenuItem("Tools/Add Melee Swing Events To Attack Clips")]
    public static void BatchAddMeleeEvents()
    {
        string baseDir = "Assets/Script/Tin/Player";
        if (!Directory.Exists(baseDir)) return;

        string[] animFiles = Directory.GetFiles(baseDir, "*.anim", SearchOption.AllDirectories);
        int updatedCount = 0;

        foreach (string file in animFiles)
        {
            string assetPath = file.Replace("\\", "/");
            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            string folderName = Path.GetFileName(Path.GetDirectoryName(assetPath));

            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            if (clip == null) continue;

            bool isAttack2 = folderName.Equals("Attack2", System.StringComparison.OrdinalIgnoreCase) || fileName.StartsWith("Attack2", System.StringComparison.OrdinalIgnoreCase);
            bool isAttack3 = folderName.Equals("Attack3", System.StringComparison.OrdinalIgnoreCase) || fileName.StartsWith("Attack3", System.StringComparison.OrdinalIgnoreCase);
            bool isAttack4 = folderName.Equals("Attack4", System.StringComparison.OrdinalIgnoreCase) || fileName.StartsWith("Attack4", System.StringComparison.OrdinalIgnoreCase);

            if (!isAttack2 && !isAttack3 && !isAttack4) continue;

            float targetFrame = 0f;
            float totalFrames = 0f;

            if (isAttack2)
            {
                targetFrame = 55f;
                totalFrames = 85f;
            }
            else if (isAttack3)
            {
                targetFrame = 30f;
                totalFrames = 84f;
            }
            else if (isAttack4)
            {
                targetFrame = 50f;
                totalFrames = 85f;
            }

            float eventTime = clip.length * (targetFrame / totalFrames);

            AnimationEvent evt = new AnimationEvent
            {
                time = eventTime,
                functionName = "OnMeleeSwing"
            };

            AnimationUtility.SetAnimationEvents(clip, new AnimationEvent[] { evt });
            EditorUtility.SetDirty(clip);
            updatedCount++;
            Debug.Log($"[MeleeEventsBatcher] Added OnMeleeSwing event to {fileName} at frame {targetFrame}/{totalFrames} (time: {eventTime:F3}s)");
        }

        if (updatedCount > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[MeleeEventsBatcher] Successfully added OnMeleeSwing events to {updatedCount} attack clips!");
        }
    }
}
#endif
