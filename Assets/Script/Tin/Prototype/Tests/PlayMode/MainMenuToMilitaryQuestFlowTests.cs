using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class MainMenuToMilitaryQuestFlowTests
{
    [UnityTest]
    [Timeout(180000)]
    public IEnumerator SoloMenuFlowLoadsMainAndSpawnsMilitaryQuestWithoutModalOverlap()
    {
        PlayerPrefs.SetInt("GameLanguage", 0);
        AsyncOperation loadMenu = SceneManager.LoadSceneAsync(0);
        while (!loadMenu.isDone) yield return null;

        yield return WaitForActiveButton("SOLO", 15f);
        InvokeButton("SOLO");
        yield return WaitForActiveButton("EASY", 10f);
        InvokeButton("EASY");
        yield return WaitForActiveButton("ENTER THE DEAD ZONE", 10f);
        InvokeButton("ENTER THE DEAD ZONE");

        float sceneDeadline = Time.realtimeSinceStartup + 60f;
        while (SceneManager.GetActiveScene().buildIndex != 1 && Time.realtimeSinceStartup < sceneDeadline)
            yield return null;
        Assert.That(SceneManager.GetActiveScene().buildIndex, Is.EqualTo(1),
            "The real SOLO menu flow did not reach Main (build index 1).");

        Type managerType = Type.GetType("MilitaryBaseQuestManager, Assembly-CSharp");
        Assert.That(managerType, Is.Not.Null);
        Component manager = null;
        float managerDeadline = Time.realtimeSinceStartup + 30f;
        while (Time.realtimeSinceStartup < managerDeadline)
        {
            manager = UnityEngine.Object.FindFirstObjectByType(managerType) as Component;
            if (manager != null && ReadBool(manager, "IsNetworkReady")) break;
            yield return null;
        }

        Assert.That(manager, Is.Not.Null, "MilitaryBaseQuestManager was not present in Main.");
        Assert.That(ReadBool(manager, "IsNetworkReady"), Is.True,
            "MilitaryBaseQuestManager scene NetworkObject did not spawn.");
        Assert.That(ReadBool(manager, "HasStateAuthority"), Is.True,
            "Single-player Host must own military quest state authority.");

        Assert.That(GameObject.Find("Military Base Quest Presentation"), Is.Not.Null);
        Assert.That(GameObject.Find("Military Iron Gate"), Is.Not.Null);
        Assert.That(GameObject.Find("Military Escape Vehicle"), Is.Not.Null);

        Type playerType = Type.GetType("PlayerMovement, Assembly-CSharp");
        FieldInfo localPlayerField = playerType?.GetField("LocalPlayerInstance",
            BindingFlags.Public | BindingFlags.Static);
        object localPlayer = null;
        float playerDeadline = Time.realtimeSinceStartup + 60f;
        while (Time.realtimeSinceStartup < playerDeadline)
        {
            localPlayer = localPlayerField?.GetValue(null);
            if (localPlayer != null) break;
            yield return null;
        }
        Assert.That(localPlayer, Is.Not.Null, "Local player did not spawn.");

        QuestFlowUIPrototype questUI = UnityEngine.Object.FindFirstObjectByType<QuestFlowUIPrototype>(
            FindObjectsInactive.Include);
        Assert.That(questUI == null || !questUI.IsQuestOverlayOpen, Is.True,
            "Quest journal/map modal overlaps gameplay immediately after Main loads.");
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
        Assert.That(target, Is.Not.Null);
        Button button = target.GetComponent<Button>();
        Assert.That(button, Is.Not.Null);
        Assert.That(button.interactable, Is.True);
        button.onClick.Invoke();
    }

    private static bool ReadBool(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName,
            BindingFlags.Public | BindingFlags.Instance);
        Assert.That(property, Is.Not.Null, "Missing property: " + propertyName);
        return (bool)property.GetValue(target);
    }
}
