using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public sealed class RuntimeWarningCleanupRegressionTests
{
    private static Type ResolveGameType(string typeName)
    {
        Type direct = Type.GetType(typeName);
        if (direct != null) return direct;

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type resolved = assembly.GetType(typeName);
            if (resolved != null) return resolved;
        }

        return null;
    }

    [TearDown]
    public void TearDown()
    {
        // Clean up any test EventSystems left behind
        EventSystem[] eventSystems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (EventSystem es in eventSystems)
        {
            if (es != null && es.gameObject != null && es.gameObject.name.StartsWith("Test_"))
            {
                UnityEngine.Object.DestroyImmediate(es.gameObject);
            }
        }
    }

    [Test]
    public void MainScene_HasExactlyOneViTriXeChetMay_WithExactFallbackPosition()
    {
        string scenePath = "Assets/Scenes/Main.unity";
        string fullPath = Path.Combine(Application.dataPath, "Scenes/Main.unity");
        Assert.That(File.Exists(fullPath), Is.True, "Main.unity scene file must exist.");

        // Check YAML content
        string yaml = File.ReadAllText(fullPath);
        Assert.That(yaml.Contains("m_Name: ViTriXeChetMay"), Is.True,
            "Main.unity must contain an authored GameObject named 'ViTriXeChetMay'.");

        // Inspect scene additively
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        try
        {
            GameObject[] rootObjects = scene.GetRootGameObjects();
            GameObject[] matches = rootObjects
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(t => t.gameObject.name == "ViTriXeChetMay")
                .Select(t => t.gameObject)
                .ToArray();

            Assert.That(matches.Length, Is.EqualTo(1),
                "Main.unity must have exactly one 'ViTriXeChetMay' arrival anchor GameObject.");

            Vector3 position = matches[0].transform.position;
            Vector3 expected = new Vector3(35.73f, -13.73f, -0.025169304f);
            Assert.That(position.x, Is.EqualTo(expected.x).Within(0.005f), "ViTriXeChetMay X mismatch");
            Assert.That(position.y, Is.EqualTo(expected.y).Within(0.005f), "ViTriXeChetMay Y mismatch");
            Assert.That(position.z, Is.EqualTo(expected.z).Within(0.005f), "ViTriXeChetMay Z mismatch");
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void EventSystemLifecycle_CanonicalEventSystem_NeverUsesLegacyStandaloneModule_AndNeverDuplicates()
    {
        // 1. EnsureEventSystem must not add legacy StandaloneInputModule
        Type chatType = ResolveGameType("AutoChatManager");
        Assert.That(chatType, Is.Not.Null, "AutoChatManager must exist.");
        MethodInfo ensureMethod = chatType.GetMethod("EnsureEventSystem", BindingFlags.Public | BindingFlags.Static);
        Assert.That(ensureMethod, Is.Not.Null, "EnsureEventSystem method must exist.");

        // Clear existing EventSystems in test scene
        EventSystem[] allSceneEs = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var es in allSceneEs) es.gameObject.SetActive(false);

        GameObject createdEs = null;
        try
        {
            ensureMethod.Invoke(null, null);

            EventSystem current = EventSystem.current ?? UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            Assert.That(current, Is.Not.Null, "EnsureEventSystem must provide an EventSystem.");
            createdEs = current.gameObject;

            // Must NOT have StandaloneInputModule
            Component standalone = current.GetComponent("StandaloneInputModule");
            Assert.That(standalone, Is.Null,
                "Canonical EventSystem must NOT use legacy StandaloneInputModule. Project uses InputSystemUIInputModule.");

            // Must have InputSystemUIInputModule
            Component inputSystemModule = current.GetComponent("InputSystemUIInputModule");
            Assert.That(inputSystemModule, Is.Not.Null,
                "Canonical EventSystem must use InputSystemUIInputModule.");

            // Calling EnsureEventSystem a second time must NOT create a duplicate EventSystem
            ensureMethod.Invoke(null, null);
            EventSystem[] allEs = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(e => e.gameObject.activeInHierarchy)
                .ToArray();
            Assert.That(allEs.Length, Is.EqualTo(1),
                "Calling EnsureEventSystem repeatedly must be idempotent and never duplicate the EventSystem.");

            Assert.That(allEs[0].GetComponents<BaseInputModule>().Length, Is.EqualTo(1),
                "Canonical EventSystem must have exactly one BaseInputModule.");
        }
        finally
        {
            if (createdEs != null)
            {
                UnityEngine.Object.DestroyImmediate(createdEs);
            }
            foreach (var es in allSceneEs)
            {
                if (es != null && es.gameObject != null) es.gameObject.SetActive(true);
            }
        }
    }

    [Test]
    public void EventSystemLifecycle_TransitionFromMainMenuToMain_MaintainsSingleActiveEventSystemWithSingleInputModule()
    {
        Type chatType = ResolveGameType("AutoChatManager");
        Assert.That(chatType, Is.Not.Null, "AutoChatManager must exist.");
        MethodInfo ensureMethod = chatType.GetMethod("EnsureEventSystem", BindingFlags.Public | BindingFlags.Static);
        Assert.That(ensureMethod, Is.Not.Null, "EnsureEventSystem method must exist.");

        Type inputSystemModuleType = ResolveGameType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem")
            ?? ResolveGameType("UnityEngine.InputSystem.UI.InputSystemUIInputModule");
        Assert.That(inputSystemModuleType, Is.Not.Null, "InputSystemUIInputModule type must be resolvable.");

        // Isolate test: deactivate existing scene EventSystems so the transition test specifically examines menuEs and mainEs
        EventSystem[] existingSceneEs = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var es in existingSceneEs) es.gameObject.SetActive(false);

        // Simulate: MainMenu had created an EventSystem, and Main scene also has an authored EventSystem
        GameObject menuEs = new GameObject("Test_MainMenu_EventSystem");
        menuEs.AddComponent<EventSystem>();
        menuEs.AddComponent(inputSystemModuleType);

        GameObject mainEs = new GameObject("Test_Main_EventSystem");
        mainEs.AddComponent<EventSystem>();
        mainEs.AddComponent(inputSystemModuleType);

        try
        {
            // When transition occurs and EnsureEventSystem is called
            ensureMethod.Invoke(null, null);

            EventSystem[] activeEventSystems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(e => e.gameObject != null && e.gameObject.name.StartsWith("Test_"))
                .ToArray();

            Assert.That(activeEventSystems.Length, Is.EqualTo(1),
                "After scene transition and manager initialization, exactly one active EventSystem must remain.");

            BaseInputModule[] modules = activeEventSystems[0].GetComponents<BaseInputModule>();
            Assert.That(modules.Length, Is.EqualTo(1),
                "Canonical EventSystem must have exactly one BaseInputModule.");
            Assert.That(modules[0].GetType().Name, Is.EqualTo("InputSystemUIInputModule"),
                "Canonical EventSystem must use InputSystemUIInputModule.");
        }
        finally
        {
            if (menuEs != null) UnityEngine.Object.DestroyImmediate(menuEs);
            if (mainEs != null) UnityEngine.Object.DestroyImmediate(mainEs);
            foreach (var es in existingSceneEs)
            {
                if (es != null && es.gameObject != null) es.gameObject.SetActive(true);
            }
        }
    }

    [Test]
    public void VoiceSystem_InitializesWithTransmitDisabled_AndMaintainsPushToTalkSemantics()
    {
        // 1. Check MainMenuManager.ConfigureVoiceForRunner initializes with TransmitEnabled = false
        Type menuType = ResolveGameType("AutoMainMenuManager");
        Assert.That(menuType, Is.Not.Null, "AutoMainMenuManager must exist.");
        MethodInfo configureVoice = menuType.GetMethod("ConfigureVoiceForRunner", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(configureVoice, Is.Not.Null, "ConfigureVoiceForRunner method must exist.");

        GameObject runnerGo = new GameObject("Test_VoiceRunner");
        try
        {
            Type runnerType = ResolveGameType("Fusion.NetworkRunner");
            if (runnerType != null)
            {
                Component runner = runnerGo.AddComponent(runnerType);
                configureVoice.Invoke(null, new object[] { runner });

                Type recorderType = ResolveGameType("Photon.Voice.Unity.Recorder");
                Assert.That(recorderType, Is.Not.Null, "Photon Voice Recorder type must exist.");
                Component recorder = runnerGo.GetComponent(recorderType);
                Assert.That(recorder, Is.Not.Null, "Recorder component must be added to runner.");

                PropertyInfo transmitProp = recorderType.GetProperty("TransmitEnabled");
                Assert.That(transmitProp, Is.Not.Null);
                bool transmitEnabled = (bool)transmitProp.GetValue(recorder);
                Assert.That(transmitEnabled, Is.False,
                    "Voice recorder MUST initialize with TransmitEnabled = false for push-to-talk.");

                PropertyInfo recordProp = recorderType.GetProperty("RecordingEnabled");
                Assert.That(recordProp, Is.Not.Null);
                bool recordingEnabled = (bool)recordProp.GetValue(recorder);
                Assert.That(recordingEnabled, Is.True,
                    "Voice recorder should have RecordingEnabled = true ready for push-to-talk key.");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(runnerGo);
        }

        // 2. Check PlayerInputHandler2D push-to-talk semantics
        Type handlerType = ResolveGameType("PlayerInputHandler2D");
        Assert.That(handlerType, Is.Not.Null, "PlayerInputHandler2D must exist.");

        FieldInfo heldField = handlerType.GetField("pushToTalkHeld", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(heldField, Is.Not.Null, "pushToTalkHeld field must exist.");

        GameObject playerGo = new GameObject("Test_PlayerHandler");
        try
        {
            Component handler = playerGo.AddComponent(handlerType);
            bool initialHeld = (bool)heldField.GetValue(handler);
            Assert.That(initialHeld, Is.False,
                "Player push-to-talk held state must be false initially; mic must not be hot without pressing V.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(playerGo);
        }
    }

    [Test]
    public void PlayerPrefabs_HaveLocalVoiceLogger_WithLogLevelError()
    {
        string[] prefabPaths = new[] { "Assets/Prefab/Player.prefab", "Assets/Prefab/Player2.prefab" };
        foreach (string path in prefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, $"Prefab at {path} must exist.");

            Component voiceLogger = prefab.GetComponent("VoiceLogger");
            Assert.That(voiceLogger, Is.Not.Null,
                $"Prefab at {path} must have a local VoiceLogger attached to its root.");

            PropertyInfo levelProp = voiceLogger.GetType().GetProperty("LogLevel");
            FieldInfo levelField = voiceLogger.GetType().GetField("LogLevel");

            object levelVal = levelProp != null ? levelProp.GetValue(voiceLogger) : levelField?.GetValue(voiceLogger);
            Assert.That(levelVal, Is.Not.Null, "LogLevel on VoiceLogger must be accessible.");
            Assert.That((int)levelVal, Is.EqualTo(1),
                $"Prefab at {path} VoiceLogger must have LogLevel = Error (1), but was {(int)levelVal}.");
        }
    }
}
