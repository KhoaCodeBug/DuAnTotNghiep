#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

public static class FootstepEventsBatcher
{
    [MenuItem("Tools/Add Footstep Events To Player Clips")]
    public static void AddEventsToAllPlayerClips()
    {
        string[] runFolders = new string[]
        {
            "Assets/Script/Tin/Player/Survivor 3/Run",
            "Assets/Script/Tin/Player/Survivor/Run"
        };

        string[] walkFolders = new string[]
        {
            "Assets/Script/Tin/Player/Survivor 3/Walk",
            "Assets/Script/Tin/Player/Survivor/Walk"
        };

        string[] aimAndSilentFolders = new string[]
        {
            "Assets/Script/Tin/Player/Survivor 3/CrouchRun",
            "Assets/Script/Tin/Player/Survivor 3/RunBackwards",
            "Assets/Script/Tin/Player/Survivor 3/StrafeLeft",
            "Assets/Script/Tin/Player/Survivor 3/StrafeRight",
            "Assets/Script/Tin/Player/Survivor 3/RunAttack",
            "Assets/Script/Tin/Player/Survivor 3/RunBackwardsAttack",
            "Assets/Script/Tin/Player/Survivor 3/StrafeLAttack",
            "Assets/Script/Tin/Player/Survivor 3/StrafeRAttack",
            "Assets/Script/Tin/Player/Survivor/CrouchRun",
            "Assets/Script/Tin/Player/Survivor/RunBackWard",
            "Assets/Script/Tin/Player/Survivor/StrafeLeft",
            "Assets/Script/Tin/Player/Survivor/StrafeRight",
            "Assets/Script/Tin/Player/Survivor/RunAttack",
            "Assets/Script/Tin/Player/Survivor/RunBackWardAtk",
            "Assets/Script/Tin/Player/Survivor/StrafeLAtk",
            "Assets/Script/Tin/Player/Survivor/StrafeRAtk"
        };

        int count = 0;

        // 1. RUN Clips ONLY (Frame 30 @ 30/84 & Frame 78 @ 78/84)
        foreach (string folder in runFolders)
        {
            if (!Directory.Exists(folder)) continue;
            string[] animFiles = Directory.GetFiles(folder, "*.anim", SearchOption.AllDirectories);
            foreach (string file in animFiles)
            {
                string assetPath = file.Replace("\\", "/");
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                if (clip == null || clip.length <= 0) continue;

                float time1 = clip.length * (30f / 84f); // Frame 30
                float time2 = clip.length * (78f / 84f); // Frame 78

                AnimationEvent evt1 = new AnimationEvent { time = time1, functionName = "OnFootstep" };
                AnimationEvent evt2 = new AnimationEvent { time = time2, functionName = "OnFootstep" };

                AnimationUtility.SetAnimationEvents(clip, new AnimationEvent[] { evt1, evt2 });
                EditorUtility.SetDirty(clip);
                count++;
            }
        }

        // 2. WALK Clips ONLY (Frame 31 @ 31/85 & Frame 79 @ 79/85)
        foreach (string folder in walkFolders)
        {
            if (!Directory.Exists(folder)) continue;
            string[] animFiles = Directory.GetFiles(folder, "*.anim", SearchOption.AllDirectories);
            foreach (string file in animFiles)
            {
                string assetPath = file.Replace("\\", "/");
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                if (clip == null || clip.length <= 0) continue;

                float time1 = clip.length * (31f / 85f); // Frame 31
                float time2 = clip.length * (79f / 85f); // Frame 79

                AnimationEvent evt1 = new AnimationEvent { time = time1, functionName = "OnFootstep" };
                AnimationEvent evt2 = new AnimationEvent { time = time2, functionName = "OnFootstep" };

                AnimationUtility.SetAnimationEvents(clip, new AnimationEvent[] { evt1, evt2 });
                EditorUtility.SetDirty(clip);
                count++;
            }
        }

        // 3. Clear events from AIM / STRAFE / CROUCH clips (0 sound when aiming!)
        foreach (string folder in aimAndSilentFolders)
        {
            if (!Directory.Exists(folder)) continue;
            string[] animFiles = Directory.GetFiles(folder, "*.anim", SearchOption.AllDirectories);
            foreach (string file in animFiles)
            {
                string assetPath = file.Replace("\\", "/");
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                if (clip == null) continue;

                AnimationUtility.SetAnimationEvents(clip, new AnimationEvent[0]);
                EditorUtility.SetDirty(clip);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[FootstepBatcher] COMPLETED! Assigned events ONLY to normal Run (Frame 30 & 78) and Walk (Frame 31 & 79). Cleared events for Aiming/Strafe/Crouch clips (0 sound).");
    }
}
#endif
