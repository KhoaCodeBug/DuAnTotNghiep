using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class MainMenuTutorialSceneBuildSettingsTests
{
    private const string TutorialScenePath = "Assets/Scenes/Intro_Cinematic.unity";

    [Test]
    public void TutorialSceneIndex_PointsToEnabledIntroCinematicScene()
    {
        EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
        int expectedIndex = Array.FindIndex(buildScenes,
            scene => scene.enabled && scene.path == TutorialScenePath);

        Assert.That(expectedIndex, Is.GreaterThanOrEqualTo(0),
            $"The standalone tutorial scene must be enabled in Build Settings: {TutorialScenePath}");

        GameObject testObject = new GameObject("MainMenuTutorialSceneBuildSettingsTest");
        testObject.SetActive(false);

        try
        {
            Type menuType = Type.GetType("AutoMainMenuManager, Assembly-CSharp");
            Assert.That(menuType, Is.Not.Null, "AutoMainMenuManager must exist in Assembly-CSharp.");

            Component menu = testObject.AddComponent(menuType);
            FieldInfo tutorialSceneIndexField = menuType.GetField("tutorialSceneIndex",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(tutorialSceneIndexField, Is.Not.Null,
                "AutoMainMenuManager.tutorialSceneIndex must remain available for the menu flow.");

            int actualIndex = (int)tutorialSceneIndexField.GetValue(menu);
            Assert.That(actualIndex, Is.EqualTo(expectedIndex),
                "AutoMainMenuManager must load the enabled Intro_Cinematic build scene, not a stale scene index.");

            MethodInfo resolver = menuType.GetMethod("ResolveTutorialSceneIndex",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(resolver, Is.Not.Null,
                "AutoMainMenuManager must resolve the tutorial scene from its Build Settings path.");

            int resolvedIndex = (int)resolver.Invoke(menu, null);
            Assert.That(resolvedIndex, Is.EqualTo(expectedIndex),
                "Tutorial scene resolution must follow the enabled Intro_Cinematic build index at runtime.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(testObject);
        }
    }
}
