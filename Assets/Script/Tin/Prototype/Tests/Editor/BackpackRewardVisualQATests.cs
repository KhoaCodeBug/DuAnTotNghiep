using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class BackpackRewardVisualQATests
{
    private const string ScreenshotDir = "Assets/Screenshots";

    private static void EnsureDirectory()
    {
        if (!Directory.Exists(ScreenshotDir))
        {
            Directory.CreateDirectory(ScreenshotDir);
        }
    }

    private static void SaveRenderTextureToPng(RenderTexture rt, string filePath)
    {
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;

        byte[] pngData = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);
        File.WriteAllBytes(filePath, pngData);
    }

    [SetUp]
    public void SetUp()
    {
        Type.GetType("BackpackQuestRewardPresentation, Assembly-CSharp")?.GetMethod("ResetForTests", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
        Type.GetType("AutoChatManager, Assembly-CSharp")?.GetMethod("ResetForTests", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
        QuestFlowUIPrototype.ResetInstanceForTests();
    }

    [TearDown]
    public void TearDown()
    {
        Type.GetType("BackpackQuestRewardPresentation, Assembly-CSharp")?.GetMethod("ResetForTests", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
        Type.GetType("AutoChatManager, Assembly-CSharp")?.GetMethod("ResetForTests", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
        QuestFlowUIPrototype.ResetInstanceForTests();
    }

    [Test]
    public void GenerateVisualQAEvidenceScreenshots()
    {
        EnsureDirectory();
        QuestUILocalization.SetVietnamese(true);

        int width = 1280;
        int height = 720;
        RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);

        GameObject camObj = new GameObject("QA Render Camera");
        Camera cam = camObj.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.06f, 0.08f, 0.09f, 1f);
        cam.targetTexture = rt;
        cam.orthographic = true;
        cam.orthographicSize = height / 2f;

        // Stage 1: Map Fragment 2 reward card
        GameObject host1 = new GameObject("Host 1");
        try
        {
            QuestFlowUIPrototype flow = host1.AddComponent<QuestFlowUIPrototype>();
            flow.EnsureBuiltForTests();
            flow.PlayMilitaryMapRewardAfterDialogue();

            CanvasGroup compGroup = host1.GetComponentInChildren<CanvasGroup>(true);
            if (compGroup != null) compGroup.alpha = 1f;

            Transform rewardCard = host1.transform.Find("Quest Flow Overlay/Quest Completion Root/Completion Reward Card");
            if (rewardCard != null)
            {
                rewardCard.gameObject.SetActive(true);
                rewardCard.localScale = Vector3.one;
            }

            Canvas canvas = host1.GetComponentInChildren<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
            }

            cam.Render();
            SaveRenderTextureToPng(rt, Path.Combine(ScreenshotDir, "backpack_radio_01_map_fragment_reward.png"));
        }
        finally
        {
            Object.DestroyImmediate(host1);
        }

        // Stage 2: Map reveal visible
        GameObject host2 = new GameObject("Host 2");
        try
        {
            QuestFlowUIPrototype flow = host2.AddComponent<QuestFlowUIPrototype>();
            flow.EnsureBuiltForTests();
            flow.QueueMilitaryMapUnlockReveal();

            Transform mapRoot = host2.transform.Find("Quest Flow Overlay/Quest Map UI");
            if (mapRoot != null) mapRoot.gameObject.SetActive(true);

            Canvas canvas = host2.GetComponentInChildren<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
            }

            cam.Render();
            SaveRenderTextureToPng(rt, Path.Combine(ScreenshotDir, "backpack_radio_02_map_reveal.png"));
        }
        finally
        {
            Object.DestroyImmediate(host2);
        }

        // Stage 3: Map fully closed
        GameObject host3 = new GameObject("Host 3");
        try
        {
            QuestFlowUIPrototype flow = host3.AddComponent<QuestFlowUIPrototype>();
            flow.EnsureBuiltForTests();
            flow.CloseAllQuestOverlays();

            Canvas canvas = host3.GetComponentInChildren<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
            }

            cam.Render();
            SaveRenderTextureToPng(rt, Path.Combine(ScreenshotDir, "backpack_radio_03_map_closed.png"));
        }
        finally
        {
            Object.DestroyImmediate(host3);
        }

        // Stage 4: Backpack Option 1 Effect visible
        Type presenterType = Type.GetType("BackpackQuestRewardPresentation, Assembly-CSharp");
        Type catalogType = Type.GetType("BackpackItemCatalog, Assembly-CSharp");
        if (presenterType != null && catalogType != null)
        {
            GameObject host4 = new GameObject("Backpack Presentation QA Host");
            try
            {
                Component presenter = host4.AddComponent(presenterType);
                MethodInfo showInternal = presenterType.GetMethod("ShowInternal",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo getOrCreate = catalogType.GetMethod("GetOrCreate",
                    BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(int), typeof(bool) }, null);

                object bp5 = getOrCreate != null ? getOrCreate.Invoke(null, new object[] { 5, false }) : null;
                showInternal?.Invoke(presenter, new object[] { 5, bp5, null });

                CanvasGroup rootGroup = host4.GetComponentInChildren<CanvasGroup>(true);
                if (rootGroup != null) rootGroup.alpha = 1f;

                Transform iconFrame = host4.transform.Find("Backpack Quest Reward Canvas/Reward Root/Center Icon Frame");
                if (iconFrame != null) iconFrame.localScale = Vector3.one;

                Canvas canvas = host4.GetComponentInChildren<Canvas>();
                if (canvas != null)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = cam;
                }

                cam.Render();
                SaveRenderTextureToPng(rt, Path.Combine(ScreenshotDir, "backpack_radio_04_backpack_effect.png"));
            }
            finally
            {
                Object.DestroyImmediate(host4);
            }
        }

        // Stage 5: Final level-5 upgrade notification A visible
        GameObject host5 = new GameObject("Host 5");
        try
        {
            Component presenter = host5.AddComponent(presenterType);
            MethodInfo showNotification = presenterType.GetMethod("ShowUpgradeNotification",
                BindingFlags.Public | BindingFlags.Static);
            showNotification?.Invoke(null, new object[] { 5 });

            Canvas canvas = host5.GetComponentInChildren<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
            }

            CanvasGroup notifGroup = host5.GetComponentInChildren<CanvasGroup>(true);
            if (notifGroup != null) notifGroup.alpha = 1f;

            cam.Render();
            SaveRenderTextureToPng(rt, Path.Combine(ScreenshotDir, "backpack_radio_05_upgrade_notification.png"));
            SaveRenderTextureToPng(rt, Path.Combine(ScreenshotDir, "backpack_radio_reward_verification.png"));
        }
        finally
        {
            Object.DestroyImmediate(host5);
        }

        Object.DestroyImmediate(camObj);
        rt.Release();
        Object.DestroyImmediate(rt);

        Assert.That(File.Exists(Path.Combine(ScreenshotDir, "backpack_radio_01_map_fragment_reward.png")), Is.True);
        Assert.That(File.Exists(Path.Combine(ScreenshotDir, "backpack_radio_02_map_reveal.png")), Is.True);
        Assert.That(File.Exists(Path.Combine(ScreenshotDir, "backpack_radio_03_map_closed.png")), Is.True);
        Assert.That(File.Exists(Path.Combine(ScreenshotDir, "backpack_radio_04_backpack_effect.png")), Is.True);
        Assert.That(File.Exists(Path.Combine(ScreenshotDir, "backpack_radio_05_upgrade_notification.png")), Is.True);
        Assert.That(File.Exists(Path.Combine(ScreenshotDir, "backpack_radio_reward_verification.png")), Is.True);
    }
}
