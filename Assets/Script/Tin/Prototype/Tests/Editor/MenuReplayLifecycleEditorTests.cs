using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;

public sealed class MenuReplayLifecycleEditorTests
{
    [UnityTest]
    [Timeout(180000)]
    public IEnumerator SoloReplaysWithoutDomainReload_AlwaysHidesMenuAndCleansHotbar()
    {
        Assert.That(EditorSettings.enterPlayModeOptionsEnabled &&
            (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) != 0,
            Is.True, "This regression must run with the project's no-Domain-Reload configuration.");
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        string folder = Path.GetFullPath("QA_Artifacts/MenuReplayFix_20260831");
        Directory.CreateDirectory(folder);
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            yield return new EnterPlayMode(false);
            Type readiness = Type.GetType("GameplayReadinessCoordinator, Assembly-CSharp");
            Assert.That((bool)readiness.GetProperty("IsReleasedToGameplay").GetValue(null), Is.False,
                "A new Play must not inherit the previous session's released state.");
            yield return PressButton("SOLO");
            yield return PressButton("MEDIUM");
            yield return PressButton("ENTER THE DEAD ZONE");
            Type movement = Type.GetType("PlayerMovement, Assembly-CSharp");
            Type menuType = Type.GetType("AutoMainMenuManager, Assembly-CSharp");
            Component player = null;
            Canvas menuCanvas = null;
            float deadline = Time.realtimeSinceStartup + 55f;
            while (Time.realtimeSinceStartup < deadline)
            {
                player = movement.GetField("LocalPlayerInstance").GetValue(null) as Component;
                object menu = menuType.GetProperty("Instance").GetValue(null);
                if (menu != null)
                    menuCanvas = menuType.GetField("mainCanvas", BindingFlags.NonPublic | BindingFlags.Instance)
                        .GetValue(menu) as Canvas;
                if (player != null && menuCanvas != null && !menuCanvas.gameObject.activeInHierarchy &&
                    (bool)readiness.GetProperty("IsReleasedToGameplay").GetValue(null)) break;
                yield return null;
            }
            Assert.That(player, Is.Not.Null, "Attempt " + attempt);
            Assert.That(menuCanvas, Is.Not.Null);
            Assert.That(menuCanvas.gameObject.activeInHierarchy, Is.False,
                "Main is loaded but the menu Canvas is still covering gameplay on attempt " + attempt);
            Assert.That((bool)readiness.GetProperty("IsReleasedToGameplay").GetValue(null), Is.True);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, "replay-" + attempt + ".png"));
            yield return new WaitForSecondsRealtime(0.5f);
            yield return new ExitPlayMode();
            Type hotbar = Type.GetType("HotbarHUDManager, Assembly-CSharp");
            Assert.That(hotbar.GetProperty("Instance").GetValue(null), Is.Null,
                "Querying HUD outside Play must not create another hotbar.");
            foreach (UnityEngine.Object leftover in Resources.FindObjectsOfTypeAll(hotbar))
                if (leftover is Component component && component.gameObject.scene.IsValid())
                    Assert.Fail("Hotbar survived ExitPlayMode: " + component.name);
        }
    }

    private static IEnumerator PressButton(string label)
    {
        Type buttonType = Type.GetType("UnityEngine.UI.Button, UnityEngine.UI");
        Assert.That(buttonType, Is.Not.Null);
        float deadline = Time.realtimeSinceStartup + 10f;
        while (Time.realtimeSinceStartup < deadline)
        {
            foreach (UnityEngine.Object candidate in UnityEngine.Object.FindObjectsByType(buttonType,
                FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                Component button = candidate as Component;
                TMP_Text text = button.GetComponentInChildren<TMP_Text>();
                if (text == null || text.text.Trim() != label ||
                    !(bool)buttonType.GetProperty("interactable").GetValue(button)) continue;
                ((UnityEvent)buttonType.GetProperty("onClick").GetValue(button)).Invoke();
                yield return null;
                yield break;
            }
            yield return null;
        }
        Assert.Fail("Missing active menu button " + label);
    }
}
