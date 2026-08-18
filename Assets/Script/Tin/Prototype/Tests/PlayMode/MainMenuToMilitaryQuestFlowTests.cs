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

        Type mainQuestType = Type.GetType("MainQuestManager, Assembly-CSharp");
        Assert.That(mainQuestType, Is.Not.Null);
        Component mainQuest = null;
        float mainQuestDeadline = Time.realtimeSinceStartup + 15f;
        while (Time.realtimeSinceStartup < mainQuestDeadline)
        {
            mainQuest = UnityEngine.Object.FindFirstObjectByType(mainQuestType) as Component;
            if (mainQuest != null && ReadBool(mainQuest, "IsNetworkReady") &&
                ReadBool(mainQuest, "IsNeighborhoodConfigured")) break;
            yield return null;
        }
        Assert.That(mainQuest, Is.Not.Null, "MainQuestManager was not present in Main.");
        Assert.That(ReadBool(mainQuest, "IsNeighborhoodConfigured"), Is.True,
            "State Authority did not replicate the shared opening neighborhood.");
        Assert.That(ReadProperty(mainQuest, "CurrentStage").ToString(), Is.EqualTo("SearchNeighborhood"));
        int searchHouseCount = (int)ReadProperty(mainQuest, "SearchHouseCount");
        Assert.That(searchHouseCount, Is.EqualTo(6));

        Type bridgeType = Type.GetType("PreMilitaryQuestRuntimeBridge, Assembly-CSharp");
        Assert.That(bridgeType, Is.Not.Null);
        Component bridge = UnityEngine.Object.FindFirstObjectByType(bridgeType) as Component;
        Assert.That(bridge, Is.Not.Null);
        Assert.That((int)ReadProperty(bridge, "ActiveSearchHouseCount"), Is.EqualTo(searchHouseCount));
        Assert.That(GameObject.Find("Quest Search Area Restriction"), Is.Null,
            "The opening quest must not recreate a solid world restriction.");
        Type blockerType = Type.GetType("QuestSearchBoundaryBlocker, Assembly-CSharp");
        Assert.That(blockerType, Is.Not.Null);
        Assert.That(UnityEngine.Object.FindFirstObjectByType(blockerType), Is.Null);

        Type survivalType = Type.GetType("PlayerSurvival, Assembly-CSharp");
        Component survival = ((Component)localPlayer).GetComponent(survivalType);
        Assert.That(survival, Is.Not.Null);
        Assert.That((float)ReadProperty(survival, "EffectiveHungerDrainRate"),
            Is.EqualTo(0.15f).Within(0.001f));
        Assert.That((float)ReadProperty(survival, "EffectiveThirstDrainRate"),
            Is.EqualTo(0.1875f).Within(0.001f));
        Assert.That((float)ReadProperty(survival, "EffectiveDamageOverTime"),
            Is.EqualTo(0.225f).Within(0.001f));
        Assert.That((float)ReadProperty(survival, "CriticalNeedGraceRemaining"),
            Is.EqualTo(35f).Within(0.1f));

        // Real solo-player regression: consumables cap both needs, restore the
        // full grace period and reactivate the existing well-fed buff.
        survivalType.GetMethod("SetTutorialNeeds")?.Invoke(survival, new object[] { 0.1f, 0.1f });
        survivalType.GetMethod("RestoreHunger")?.Invoke(survival, new object[] { 999f });
        survivalType.GetMethod("RestoreThirst")?.Invoke(survival, new object[] { 999f });
        Assert.That((float)ReadProperty(survival, "currentHunger"), Is.EqualTo(100f).Within(0.01f));
        Assert.That((float)ReadProperty(survival, "currentThirst"), Is.EqualTo(100f).Within(0.01f));
        Assert.That((int)survivalType.GetMethod("GetWellFedTier")?.Invoke(survival, null), Is.EqualTo(4));
        Assert.That((float)ReadProperty(survival, "CriticalNeedGraceRemaining"),
            Is.EqualTo(35f).Within(0.1f));

        Type healthType = Type.GetType("PlayerHealth, Assembly-CSharp");
        Component health = ((Component)localPlayer).GetComponent(healthType);
        Assert.That(health, Is.Not.Null);
        MethodInfo passiveHealRate = healthType.GetMethod("GetPassiveHealRate");
        float tierOneHeal = (float)passiveHealRate.Invoke(health, new object[] { 1 });
        float tierFourHeal = (float)passiveHealRate.Invoke(health, new object[] { 4 });
        Assert.That(tierFourHeal, Is.GreaterThan(tierOneHeal),
            "Higher well-fed tiers must preserve the stronger passive-heal buff.");

        // Reproduce the reported screenshot state: neighborhood complete,
        // player outside the bright office-search area.
        SetProperty(mainQuest, "NetworkQuestStage", 2); // LocateOffice
        SetPrivateField(bridge, "outsideSince", Time.unscaledTime - 2f);
        SetPrivateField(bridge, "nextOutsideWarningTime", 0f);
        MethodInfo updateWarning = bridgeType.GetMethod("UpdateOutsideSearchZoneWarning",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(updateWarning, Is.Not.Null);
        updateWarning.Invoke(bridge, new object[] { new Vector2(100000f, 100000f), mainQuest });
        Assert.That((bool)ReadPrivateField(bridge, "guidanceTargetsOffice"), Is.True);
        Assert.That((float)ReadPrivateField(bridge, "outsideWarningVisibleUntil"),
            Is.GreaterThan(Time.unscaledTime));

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
        object value = ReadProperty(target, propertyName);
        if (value is bool boolean) return boolean;
        return string.Equals(value?.ToString(), bool.TrueString, StringComparison.OrdinalIgnoreCase);
    }

    private static object ReadProperty(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName,
            BindingFlags.Public | BindingFlags.Instance);
        Assert.That(property, Is.Not.Null, "Missing property: " + propertyName);
        return property.GetValue(target);
    }

    private static void SetProperty(object target, string propertyName, object value)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName,
            BindingFlags.Public | BindingFlags.Instance);
        Assert.That(property, Is.Not.Null, "Missing property: " + propertyName);
        property.SetValue(target, value);
    }

    private static object ReadPrivateField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, "Missing field: " + fieldName);
        return field.GetValue(target);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, "Missing field: " + fieldName);
        field.SetValue(target, value);
    }
}
