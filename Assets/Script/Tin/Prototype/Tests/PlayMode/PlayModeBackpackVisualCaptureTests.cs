using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class PlayModeBackpackVisualCaptureTests
{
    private const string ScreenshotDir = "Assets/Screenshots/RuntimeBackpack";

    private static void EnsureDirectory()
    {
        if (!Directory.Exists(ScreenshotDir))
        {
            Directory.CreateDirectory(ScreenshotDir);
        }
    }

    [UnityTest]
    [Timeout(180000)]
    public IEnumerator PlayModeFlow_SoloEasyDeadZone_TriggerAndCaptureLevel4AndLevel5()
    {
        EnsureDirectory();
        yield return ShutdownExistingRunners();

        // English language for canonical verification
        PlayerPrefs.SetInt("GameLanguage", 0);

        AsyncOperation loadMenu = SceneManager.LoadSceneAsync(0);
        while (!loadMenu.isDone) yield return null;
        yield return null;

        yield return WaitForActiveButton("SOLO", 15f);
        InvokeButton("SOLO");

        yield return WaitForActiveButton("EASY", 10f);
        InvokeButton("EASY");

        yield return WaitForActiveButton("ENTER THE DEAD ZONE", 10f);
        InvokeButton("ENTER THE DEAD ZONE");

        float sceneDeadline = Time.realtimeSinceStartup + 60f;
        while (SceneManager.GetActiveScene().buildIndex != 1 && Time.realtimeSinceStartup < sceneDeadline)
            yield return null;
        Assert.That(SceneManager.GetActiveScene().buildIndex, Is.EqualTo(1), "Main scene (buildIndex 1) must load.");

        // Wait for player to spawn
        Type playerMovementType = Type.GetType("PlayerMovement, Assembly-CSharp");
        Component player = null;
        float playerDeadline = Time.realtimeSinceStartup + 30f;
        while (Time.realtimeSinceStartup < playerDeadline)
        {
            if (playerMovementType != null)
                player = UnityEngine.Object.FindFirstObjectByType(playerMovementType) as Component;
            if (player != null) break;
            yield return null;
        }
        Assert.That(player, Is.Not.Null, "Player must spawn in Main scene.");

        // Wait for intro/scene settling
        yield return new WaitForSecondsRealtime(2.0f);

        // Capture fresh gameplay and UI state post-transition to prove lifecycle and canvas input remain intact
        yield return CaptureDeterministicScreen(Path.Combine(ScreenshotDir, "runtime_lifecycle_cleanup_gameplay_ui.png"));

        // Assert exactly one active EventSystem and exactly one BaseInputModule post scene transition
        EventSystem[] activeEventSystems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Assert.That(activeEventSystems.Length, Is.EqualTo(1),
            "After MainMenu -> Main transition, exactly one EventSystem must be active.");
        BaseInputModule[] modules = activeEventSystems[0].GetComponents<BaseInputModule>();
        Assert.That(modules.Length, Is.EqualTo(1),
            "Canonical EventSystem must have exactly one BaseInputModule.");
        Assert.That(modules[0].GetType().Name, Is.EqualTo("InputSystemUIInputModule"),
            "Canonical EventSystem must use InputSystemUIInputModule.");

        Type presenterType = Type.GetType("BackpackQuestRewardPresentation, Assembly-CSharp");
        Type catalogType = Type.GetType("BackpackItemCatalog, Assembly-CSharp");
        Assert.That(presenterType, Is.Not.Null);
        Assert.That(catalogType, Is.Not.Null);

        MethodInfo showMethod = presenterType.GetMethod("Show", BindingFlags.Public | BindingFlags.Static);
        MethodInfo getOrCreate = catalogType.GetMethod("GetOrCreate",
            BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(int), typeof(bool) }, null);
        Assert.That(showMethod, Is.Not.Null);
        Assert.That(getOrCreate, Is.Not.Null);

        // Purge any stale preview objects from memory
        Type presentationType = Type.GetType("BackpackQuestRewardPresentation, Assembly-CSharp");
        presentationType?.GetMethod("PurgeStalePreviewObjects", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);

        PropertyInfo isNotifVisibleProp = presenterType.GetProperty("IsNotificationVisible", BindingFlags.Public | BindingFlags.Static);
        PropertyInfo notifBodyProp = presenterType.GetProperty("LastNotificationBody", BindingFlags.Public | BindingFlags.Static);

        // --- LEVEL 4 PRESENTATION ---
        object bp4 = getOrCreate.Invoke(null, new object[] { 4, false });
        Assert.That(bp4, Is.Not.Null, "Level 4 backpack item must exist.");
        FieldInfo iconField = bp4.GetType().GetField("icon");
        PropertyInfo iconProp = bp4.GetType().GetProperty("icon");
        Sprite bp4Icon = (iconField != null ? iconField.GetValue(bp4) : iconProp?.GetValue(bp4)) as Sprite;
        Assert.That(bp4Icon, Is.Not.Null, "Level 4 backpack must have an icon.");
        Assert.That(bp4Icon.rect.width, Is.GreaterThan(32),
            $"Level 4 backpack icon must use authored art, not 32x32 fallback (actual width={bp4Icon.rect.width}).");
        Assert.That(bp4Icon.texture != null && bp4Icon.texture.width >= 500, Is.True,
            $"Level 4 backpack icon texture must be authored art (actual width={bp4Icon.texture?.width}).");

        bool level4Completed = false;
        showMethod.Invoke(null, new object[] { 4, bp4, (Action)(() => level4Completed = true) });

        // 1) During Effect B: Notification A must NOT be visible yet
        yield return new WaitForSecondsRealtime(0.9f);
        Assert.That((bool)isNotifVisibleProp.GetValue(null), Is.False, "Notification A must not be visible during Effect B.");

        FieldInfo iconImageField = presenterType.GetField("iconImage", BindingFlags.NonPublic | BindingFlags.Instance);
        Component presenterInstance = UnityEngine.Object.FindAnyObjectByType(presenterType) as Component;
        if (presenterInstance != null && iconImageField != null)
        {
            Image activeImg = iconImageField.GetValue(presenterInstance) as Image;
            Assert.That(activeImg != null && activeImg.sprite != null, Is.True, "Effect B must have an active backpack sprite.");
            Assert.That(activeImg.sprite.rect.width, Is.GreaterThan(32),
                $"Effect B must display authored art, not 32x32 fallback (actual width={activeImg.sprite.rect.width}).");
        }

        // Regression assertion: Assert NO text overlays (storage, capacity, slots, upgrade) exist on active screen
        AssertZeroStaleRewardOverlayTextsInScene();

        string l4EffectBCamera = Path.Combine(ScreenshotDir, "runtime_level4_effect_b_camera.png");
        string l4EffectBScreen = Path.Combine(ScreenshotDir, "runtime_level4_effect_b_screencapture.png");
        CaptureCanvasScreenshot(presenterType, l4EffectBCamera);
        yield return CaptureDeterministicScreen(l4EffectBScreen);

        // 2) Wait for Effect B to complete -> Notification A appears!
        yield return new WaitForSecondsRealtime(1.8f);
        Assert.That(level4Completed, Is.True, "Level 4 presentation should finish.");
        Assert.That((bool)isNotifVisibleProp.GetValue(null), Is.True, "Notification A must be visible after Effect B.");
        string l4Body = (string)notifBodyProp.GetValue(null);
        Assert.That(l4Body, Does.Contain("30 → 40"), "Level 4 notification must show 30 -> 40.");
        MethodInfo getLocalizedDisplayName = catalogType.GetMethod("GetLocalizedDisplayName", BindingFlags.Public | BindingFlags.Static);
        string expectedL4Name = (string)getLocalizedDisplayName.Invoke(null, new object[] { bp4 });
        Assert.That(l4Body, Does.Contain(expectedL4Name), "Level 4 notification must contain the backpack item display name.");
        Assert.That(l4Body.ToLowerInvariant().Contains("hospital") || l4Body.ToLowerInvariant().Contains("bệnh viện"), Is.True,
            "Level 4 notification must contain hospital reward reason.");

        string l4NotifCamera = Path.Combine(ScreenshotDir, "runtime_level4_notification_a_camera.png");
        string l4NotifScreen = Path.Combine(ScreenshotDir, "runtime_level4_notification_a_screencapture.png");
        CaptureCanvasScreenshot(presenterType, l4NotifCamera);
        yield return CaptureDeterministicScreen(l4NotifScreen);
        yield return new WaitForSecondsRealtime(0.4f);
        Assert.That((bool)isNotifVisibleProp.GetValue(null), Is.True, "Notification A must remain visible during capture.");

        MethodInfo dismissNotif = presenterType.GetMethod("DismissNotification", BindingFlags.Public | BindingFlags.Static);
        dismissNotif?.Invoke(null, null);
        yield return new WaitForSecondsRealtime(0.5f);

        // --- LEVEL 5 FULL DETERMINISTIC SEQUENCE: Triggered through MainQuestManager ---
        // Authoritative sequence: Map Fragment Reward -> Map Reveal -> Map Closes -> Level-5 Claim/Presentation -> Effect B -> Notification A
        Type questManagerType = Type.GetType("MainQuestManager, Assembly-CSharp");
        Assert.That(questManagerType, Is.Not.Null, "MainQuestManager must exist.");
        Component mainQuest = (Component)UnityEngine.Object.FindAnyObjectByType(questManagerType);
        Assert.That(mainQuest, Is.Not.Null, "MainQuestManager component must exist in Main scene.");

        MethodInfo handleMapFound = questManagerType.GetMethod("HandleMilitaryMapFragmentFound",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(handleMapFound, Is.Not.Null, "HandleMilitaryMapFragmentFound must exist.");

        PropertyInfo isVisibleProp = presenterType.GetProperty("IsVisible", BindingFlags.Public | BindingFlags.Static);

        // Stage 1: Trigger the military map sequence via MainQuestManager
        handleMapFound.Invoke(mainQuest, new object[] { true });

        // During map reward dialogue: Backpack presenter and Notification must NOT be visible
        yield return new WaitForSecondsRealtime(0.8f);
        Assert.That((bool)isVisibleProp.GetValue(null), Is.False, "Backpack presenter must NOT be visible during map reward dialogue.");
        Assert.That((bool)isNotifVisibleProp.GetValue(null), Is.False, "Notification A must NOT be visible during map reward dialogue.");
        yield return CaptureDeterministicScreen(Path.Combine(ScreenshotDir, "runtime_level5_01_map_reward.png"));

        // Stage 2: Wait for map reward dialogue to complete and map reveal to open
        QuestFlowUIPrototype flow = QuestFlowUIPrototype.Instance;
        float revealWaitStart = Time.realtimeSinceStartup;
        while (flow != null && !flow.IsMapOpen && Time.realtimeSinceStartup - revealWaitStart < 8f)
        {
            yield return null;
        }
        yield return new WaitForSecondsRealtime(0.4f);
        Assert.That((bool)isVisibleProp.GetValue(null), Is.False, "Backpack presenter must NOT be visible during map reveal.");
        yield return CaptureDeterministicScreen(Path.Combine(ScreenshotDir, "runtime_level5_02_map_reveal.png"));

        // Stage 3: Wait for map reveal to finish and map to close
        float mapCloseWaitStart = Time.realtimeSinceStartup;
        while (flow != null && flow.IsMapOpen && Time.realtimeSinceStartup - mapCloseWaitStart < 8f)
        {
            yield return null;
        }
        yield return new WaitForSecondsRealtime(0.3f);
        AssertZeroOverlaysDuringBackpackRewardFlow(duringEffectB: true);
        yield return CaptureDeterministicScreen(Path.Combine(ScreenshotDir, "runtime_level5_03_map_closed.png"));

        // Stage 4: Map has closed -> OnMilitaryMapSequenceComplete has called ClaimAndPresentLevelFiveBackpack!
        // Backpack Level 5 presenter (Effect B) becomes active!
        float effectBWaitStart = Time.realtimeSinceStartup;
        while (!(bool)isVisibleProp.GetValue(null) && Time.realtimeSinceStartup - effectBWaitStart < 6f)
        {
            yield return null;
        }
        Assert.That((bool)isVisibleProp.GetValue(null), Is.True, "Backpack Level 5 presentation must become active after map closes.");
        Assert.That((bool)isNotifVisibleProp.GetValue(null), Is.False, "Notification A must not be visible during Effect B.");

        if (presenterInstance == null)
            presenterInstance = UnityEngine.Object.FindAnyObjectByType(presenterType) as Component;
        if (presenterInstance != null && iconImageField != null)
        {
            Image activeImg = iconImageField.GetValue(presenterInstance) as Image;
            Assert.That(activeImg != null && activeImg.sprite != null, Is.True, "Effect B Level 5 must have an active backpack sprite.");
            Assert.That(activeImg.sprite.rect.width, Is.GreaterThan(32),
                $"Effect B Level 5 must display authored art, not 32x32 fallback (actual width={activeImg.sprite.rect.width}).");
            Assert.That(activeImg.sprite.name.Contains("BackpackLevel5") || (activeImg.sprite.texture != null && activeImg.sprite.texture.name.Contains("BackpackLevel5")), Is.True,
                $"Effect B Level 5 sprite must be backed by BackpackLevel5 authored art (name='{activeImg.sprite.name}').");
        }

        // Regression assertion: Assert NO text overlays, AutoChat banner, or Route dialogue during Effect B
        AssertZeroOverlaysDuringBackpackRewardFlow(duringEffectB: true);

        string l5EffectBCamera = Path.Combine(ScreenshotDir, "runtime_level5_effect_b_camera.png");
        string l5EffectBScreen = Path.Combine(ScreenshotDir, "runtime_level5_effect_b_screencapture.png");
        CaptureCanvasScreenshot(presenterType, l5EffectBCamera);
        yield return CaptureDeterministicScreen(l5EffectBScreen);
        yield return CaptureDeterministicScreen(Path.Combine(ScreenshotDir, "runtime_level5_04_backpack_b.png"));

        // Stage 5: Wait for Effect B to complete -> Notification A appears automatically!
        float notifWaitStart = Time.realtimeSinceStartup;
        while (!(bool)isNotifVisibleProp.GetValue(null) && Time.realtimeSinceStartup - notifWaitStart < 6f)
        {
            yield return null;
        }
        Assert.That((bool)isNotifVisibleProp.GetValue(null), Is.True, "Notification A must be visible after Effect B.");
        string l5Body = (string)notifBodyProp.GetValue(null);
        Assert.That(l5Body, Does.Contain("40 → 50"), "Level 5 notification must show 40 -> 50.");
        object bp5Item = getOrCreate.Invoke(null, new object[] { 5, false });
        string expectedL5Name = (string)getLocalizedDisplayName.Invoke(null, new object[] { bp5Item });
        Assert.That(l5Body, Does.Contain(expectedL5Name), "Level 5 notification must contain the backpack item display name.");
        Assert.That(l5Body.ToLowerInvariant().Contains("radio"), Is.True,
            "Level 5 notification must contain radio reward reason.");

        // Regression assertion: Assert NO story/dialogue/route-choice panel during Notification A
        AssertZeroOverlaysDuringBackpackRewardFlow(duringEffectB: false);

        // Wait for slide-down and fade-in animation (0.22s) to fully settle
        yield return new WaitForSecondsRealtime(0.4f);

        string l5NotifCamera = Path.Combine(ScreenshotDir, "runtime_level5_notification_a_camera.png");
        string l5NotifScreen = Path.Combine(ScreenshotDir, "runtime_level5_notification_a_screencapture.png");
        CaptureCanvasScreenshot(presenterType, l5NotifCamera);
        yield return CaptureDeterministicScreen(l5NotifScreen);
        yield return CaptureDeterministicScreen(Path.Combine(ScreenshotDir, "runtime_level5_05_notification_a.png"));
        yield return new WaitForSecondsRealtime(0.4f);
        Assert.That((bool)isNotifVisibleProp.GetValue(null), Is.True, "Notification A must still be active during capture.");

        yield return new WaitForSecondsRealtime(1.5f);

        // Assert files exist
        Assert.That(File.Exists(l4EffectBCamera), Is.True, "Level 4 Effect B canvas screenshot missing.");
        Assert.That(File.Exists(l4NotifCamera), Is.True, "Level 4 Notification A canvas screenshot missing.");
        Assert.That(File.Exists(l5EffectBCamera), Is.True, "Level 5 Effect B canvas screenshot missing.");
        Assert.That(File.Exists(l5NotifCamera), Is.True, "Level 5 Notification A canvas screenshot missing.");
    }

    private static IEnumerator CaptureDeterministicScreen(string path)
    {
        if (File.Exists(path))
        {
            try { File.Delete(path); } catch { }
        }
        ScreenCapture.CaptureScreenshot(path);
        yield return new WaitForEndOfFrame();
        float waitStart = Time.realtimeSinceStartup;
        while (!File.Exists(path) && Time.realtimeSinceStartup - waitStart < 2.5f)
        {
            yield return null;
        }
        yield return new WaitForSecondsRealtime(0.1f);
    }

    private static void CaptureCanvasScreenshot(Type presenterType, string outputPath)
    {
        if (File.Exists(outputPath))
        {
            try { File.Delete(outputPath); } catch { }
        }
        Canvas canvas = null;
        GameObject presObj = GameObject.Find("Backpack Quest Reward Presentation");
        if (presObj != null)
            canvas = presObj.GetComponentInChildren<Canvas>(true);

        if (canvas == null)
        {
            Canvas[] allCanvases = Resources.FindObjectsOfTypeAll<Canvas>();
            for (int i = 0; i < allCanvases.Length; i++)
            {
                if (allCanvases[i] != null && allCanvases[i].name.Contains("Backpack"))
                {
                    canvas = allCanvases[i];
                    break;
                }
            }
        }
        if (canvas == null) return;

        int width = 1920;
        int height = 1080;
        RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        GameObject camObj = new GameObject("QA Canvas Capture Camera");
        try
        {
            Camera cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.08f, 0.09f, 1f);
            cam.targetTexture = rt;
            cam.orthographic = true;
            cam.orthographicSize = height / 2f;
            cam.nearClipPlane = -1000f;
            cam.farClipPlane = 1000f;

            RenderMode prevMode = canvas.renderMode;
            Camera prevCam = canvas.worldCamera;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;

            cam.Render();

            canvas.renderMode = prevMode;
            canvas.worldCamera = prevCam;

            RenderTexture prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;

            byte[] png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);
            File.WriteAllBytes(outputPath, png);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(camObj);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
        }
    }

    private static IEnumerator WaitForActiveButton(string label, float seconds)
    {
        float deadline = Time.realtimeSinceStartup + seconds;
        while (GameObject.Find("Btn_" + label) == null && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(GameObject.Find("Btn_" + label), Is.Not.Null, "Active button not found: " + label);
    }

    private static void InvokeButton(string label)
    {
        GameObject target = GameObject.Find("Btn_" + label);
        Assert.That(target, Is.Not.Null, "Button not found: " + label);
        Button button = target.GetComponent<Button>();
        Assert.That(button, Is.Not.Null, "Button component missing: " + label);
        Assert.That(button.interactable, Is.True, "Button not interactable: " + label);
        button.onClick.Invoke();
    }

    private static IEnumerator ShutdownExistingRunners()
    {
        Type runnerType = Type.GetType("Fusion.NetworkRunner, Fusion.Runtime");
        if (runnerType == null) yield break;
        UnityEngine.Object[] runners = Resources.FindObjectsOfTypeAll(runnerType);
        Type shutdownReasonType = Type.GetType("Fusion.ShutdownReason, Fusion.Runtime");
        MethodInfo shutdown = shutdownReasonType == null ? null : runnerType.GetMethod("Shutdown",
            new[] { typeof(bool), shutdownReasonType, typeof(bool) });
        for (int i = 0; i < runners.Length; i++)
        {
            if (runners[i] == null || shutdown == null) continue;
            object ok = Enum.Parse(shutdownReasonType, "Ok");
            Task task = shutdown.Invoke(runners[i], new[] { (object)true, ok, false }) as Task;
            float deadline = Time.realtimeSinceStartup + 10f;
            while (task != null && !task.IsCompleted && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        Type menuType = Type.GetType("AutoMainMenuManager, Assembly-CSharp");
        if (menuType != null)
        {
            UnityEngine.Object[] menus = Resources.FindObjectsOfTypeAll(menuType);
            for (int i = 0; i < menus.Length; i++)
                if (menus[i] is Component menu && menu != null)
                    UnityEngine.Object.Destroy(menu.transform.root.gameObject);
        }

        GameObject[] persistentObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < persistentObjects.Length; i++)
            if (persistentObjects[i] != null && persistentObjects[i].name == "AutoMenuCanvas")
                UnityEngine.Object.Destroy(persistentObjects[i]);

        yield return null;
    }

    private static void AssertZeroStaleRewardOverlayTextsInScene()
    {
        TextMeshProUGUI[] allTexts = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();
        for (int i = 0; i < allTexts.Length; i++)
        {
            TextMeshProUGUI t = allTexts[i];
            if (t == null || !t.gameObject.activeInHierarchy) continue;
            string text = t.text;
            if (string.IsNullOrEmpty(text)) continue;

            Assert.That(text, Does.Not.Contain("INVENTORY UPGRADE"), "Stale preview text 'INVENTORY UPGRADE' found in active scene!");
            Assert.That(text, Does.Not.Contain("LOADOUT UPDATED"), "Stale preview text 'LOADOUT UPDATED' found in active scene!");
            Assert.That(text, Does.Not.Contain("BACKPACK LV.4 RECEIVED"), "Stale preview text 'BACKPACK LV.4 RECEIVED' found in active scene!");
            Assert.That(text, Does.Not.Contain("UPGRADE APPLIED"), "Stale preview text 'UPGRADE APPLIED' found in active scene!");
        }
    }

    private static void AssertZeroOverlaysDuringBackpackRewardFlow(bool duringEffectB)
    {
        AssertZeroStaleRewardOverlayTextsInScene();

        // Radio / route choice dialogue must NOT be visible
        Type radioType = Type.GetType("RouteBRadioBroadcastUI, Assembly-CSharp");
        if (radioType != null)
        {
            PropertyInfo isRadioVis = radioType.GetProperty("IsVisible", BindingFlags.Public | BindingFlags.Static);
            if (isRadioVis != null)
            {
                Assert.That((bool)isRadioVis.GetValue(null), Is.False,
                    "RouteBRadioBroadcastUI must NOT be visible during backpack reward flow.");
            }
        }

        Type decisionType = Type.GetType("EscapeRouteDecisionUI, Assembly-CSharp");
        if (decisionType != null)
        {
            PropertyInfo isDecisionVis = decisionType.GetProperty("IsVisible", BindingFlags.Public | BindingFlags.Static);
            if (isDecisionVis != null)
            {
                Assert.That((bool)isDecisionVis.GetValue(null), Is.False,
                    "EscapeRouteDecisionUI must NOT be visible during backpack reward flow.");
            }
        }

        // AutoChat panel must NOT be visible during Effect B
        if (duringEffectB)
        {
            Type chatType = Type.GetType("AutoChatManager, Assembly-CSharp");
            if (chatType != null)
            {
                PropertyInfo chatInstProp = chatType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                object chatInst = chatInstProp?.GetValue(null);
                if (chatInst != null)
                {
                    PropertyInfo isChatVisProp = chatType.GetProperty("IsChatVisible", BindingFlags.Public | BindingFlags.Instance);
                    if (isChatVisProp != null)
                    {
                        Assert.That((bool)isChatVisProp.GetValue(chatInst), Is.False,
                            "AutoChat panel must NOT be visible during Effect B presentation.");
                    }
                }
            }
        }

        // Check active UI texts for clue / route dialogue strings
        Text[] allUiTexts = Resources.FindObjectsOfTypeAll<Text>();
        for (int i = 0; i < allUiTexts.Length; i++)
        {
            Text t = allUiTexts[i];
            if (t == null || !t.gameObject.activeInHierarchy) continue;
            string text = t.text;
            if (string.IsNullOrEmpty(text)) continue;

            if (duringEffectB)
            {
                Assert.That(text, Does.Not.Contain("PHÁT HIỆN MANH MỐI MỚI"),
                    $"Active UI Text '{t.gameObject.name}' must NOT contain 'PHÁT HIỆN MANH MỐI MỚI' during Effect B.");
            }
            Assert.That(text, Does.Not.Contain("MILITARY ROUTE IDENTIFIED"),
                $"Active UI Text '{t.gameObject.name}' must NOT contain 'MILITARY ROUTE IDENTIFIED' during backpack flow.");
        }

        TextMeshProUGUI[] allTmps = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();
        for (int i = 0; i < allTmps.Length; i++)
        {
            TextMeshProUGUI t = allTmps[i];
            if (t == null || !t.gameObject.activeInHierarchy) continue;
            string text = t.text;
            if (string.IsNullOrEmpty(text)) continue;

            if (duringEffectB)
            {
                Assert.That(text, Does.Not.Contain("PHÁT HIỆN MANH MỐI MỚI"),
                    $"Active TextMeshProUGUI '{t.gameObject.name}' must NOT contain 'PHÁT HIỆN MANH MỐI MỚI' during Effect B.");
            }
            Assert.That(text, Does.Not.Contain("MILITARY ROUTE IDENTIFIED"),
                $"Active TextMeshProUGUI '{t.gameObject.name}' must NOT contain 'MILITARY ROUTE IDENTIFIED' during backpack flow.");
        }
    }
}
