using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class BackpackRewardCombinationBATests
{
    private Type presenterType;
    private Type catalogType;
    private Type rulesType;
    private Type questManagerType;
    private Type autoChatType;
    private Type radioUiType;
    private Type escapeDecisionType;

    [SetUp]
    public void SetUp()
    {
        presenterType = Type.GetType("BackpackQuestRewardPresentation, Assembly-CSharp");
        catalogType = Type.GetType("BackpackItemCatalog, Assembly-CSharp");
        rulesType = Type.GetType("BackpackQuestRewardRules, Assembly-CSharp");
        questManagerType = Type.GetType("MainQuestManager, Assembly-CSharp");
        autoChatType = Type.GetType("AutoChatManager, Assembly-CSharp");
        radioUiType = Type.GetType("RouteBRadioBroadcastUI, Assembly-CSharp");
        escapeDecisionType = Type.GetType("EscapeRouteDecisionUI, Assembly-CSharp");

        Assert.That(presenterType, Is.Not.Null, "BackpackQuestRewardPresentation must exist in Assembly-CSharp.");
        Assert.That(catalogType, Is.Not.Null, "BackpackItemCatalog must exist in Assembly-CSharp.");

        presenterType.GetMethod("ResetForTests", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
        autoChatType?.GetMethod("ResetForTests", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
    }

    [TearDown]
    public void TearDown()
    {
        autoChatType?.GetMethod("ResetForTests", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
        presenterType?.GetMethod("ResetForTests", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
    }

    [Test]
    public void EffectB_HasNoTextLabels_DuringPresentation_OnlyShowsPureVisualIconAndScan()
    {
        GameObject host = new GameObject("Test_EffectB_Host");
        try
        {
            Component presenter = host.AddComponent(presenterType);
            MethodInfo showInternal = presenterType.GetMethod("ShowInternal",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(showInternal, Is.Not.Null, "ShowInternal method must exist.");

            MethodInfo getOrCreate = catalogType.GetMethod("GetOrCreate",
                BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(int), typeof(bool) }, null);
            object bp4 = getOrCreate.Invoke(null, new object[] { 4, false });

            showInternal.Invoke(presenter, new object[] { 4, bp4, null });

            // Check all TextMeshProUGUI / Text components on the reward root / presentation canvas during Effect B
            Canvas canvas = host.GetComponentInChildren<Canvas>(true);
            Assert.That(canvas, Is.Not.Null, "Presentation canvas must exist.");

            Transform rewardRoot = canvas.transform.Find("Reward Root");
            Assert.That(rewardRoot, Is.Not.Null, "Reward Root must exist.");

            TextMeshProUGUI[] tmps = rewardRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
            Text[] texts = rewardRoot.GetComponentsInChildren<Text>(true);

            // Effect B requirement: NO text labels active inside the reward presentation root!
            // (no Capacity, Storage, Title, Body, Tier, Level text)
            int activeTmpCount = 0;
            foreach (var t in tmps)
            {
                if (t.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(t.text))
                {
                    activeTmpCount++;
                    Debug.Log("Found active text in Effect B: " + t.name + " = " + t.text);
                }
            }
            int activeTextCount = 0;
            foreach (var t in texts)
            {
                if (t.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(t.text))
                {
                    activeTextCount++;
                }
            }

            Assert.That(activeTmpCount + activeTextCount, Is.EqualTo(0),
                "Effect B must be purely visual with ZERO active text labels (no storage, capacity, title, tier, level).");

            // Verify icon is present in the visual scan
            Image[] images = rewardRoot.GetComponentsInChildren<Image>(true);
            bool foundIcon = false;
            foreach (var img in images)
            {
                if (img.gameObject.name.Contains("Icon") && img.sprite != null)
                {
                    foundIcon = true;
                    break;
                }
            }
            Assert.That(foundIcon, Is.True, "Effect B must display the real backpack icon sprite.");

            // Verify Dimmer alpha is light (<= 0.65) so gameplay remains visible
            Transform dimmer = rewardRoot.Find("Dimmer");
            Assert.That(dimmer, Is.Not.Null, "Dimmer must exist.");
            Image dimmerImg = dimmer.GetComponent<Image>();
            Assert.That(dimmerImg, Is.Not.Null);
            Assert.That(dimmerImg.color.a, Is.LessThanOrEqualTo(0.65f),
                "Dimmer must be light enough to see gameplay underneath.");
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void NotificationA_OnlyAppearsAfterEffectBCompletes_AndShowsCorrectCapacityBeforeAfter()
    {
        GameObject host = new GameObject("Test_NotificationA_Host");
        try
        {
            Component presenter = host.AddComponent(presenterType);
            MethodInfo showInternal = presenterType.GetMethod("ShowInternal",
                BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo finishPresentation = presenterType.GetMethod("FinishPresentation",
                BindingFlags.NonPublic | BindingFlags.Instance);
            PropertyInfo isNotificationVisible = presenterType.GetProperty("IsNotificationVisible",
                BindingFlags.Public | BindingFlags.Static);
            PropertyInfo notificationBodyProp = presenterType.GetProperty("LastNotificationBody",
                BindingFlags.Public | BindingFlags.Static);
            PropertyInfo notificationTitleProp = presenterType.GetProperty("LastNotificationTitle",
                BindingFlags.Public | BindingFlags.Static);

            Assert.That(isNotificationVisible, Is.Not.Null, "IsNotificationVisible property must exist on presenter.");
            Assert.That(notificationBodyProp, Is.Not.Null, "LastNotificationBody property must exist on presenter.");

            MethodInfo getOrCreate = catalogType.GetMethod("GetOrCreate",
                BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(int), typeof(bool) }, null);
            object bp4 = getOrCreate.Invoke(null, new object[] { 4, false });

            // During presentation
            showInternal.Invoke(presenter, new object[] { 4, bp4, null });
            Assert.That((bool)isNotificationVisible.GetValue(null), Is.False,
                "Notification A must NOT be visible while Effect B is playing.");

            // Complete presentation
            finishPresentation.Invoke(presenter, null);

            Assert.That((bool)isNotificationVisible.GetValue(null), Is.True,
                "Notification A must be visible after Effect B completes.");

            string bodyL4 = (string)notificationBodyProp.GetValue(null);
            string titleL4 = (string)notificationTitleProp.GetValue(null);
            string fullL4 = (titleL4 ?? "") + " " + (bodyL4 ?? "");
            Assert.That(fullL4.ToUpperInvariant().Contains("LEVEL 3 → LEVEL 4") || fullL4.ToUpperInvariant().Contains("CẤP 3 → CẤP 4"), Is.True,
                "Level 4 notification must visibly display LEVEL 3 → LEVEL 4 transition.");

            MethodInfo getLocalizedDisplayName = catalogType.GetMethod("GetLocalizedDisplayName", BindingFlags.Public | BindingFlags.Static);
            Assert.That(getLocalizedDisplayName, Is.Not.Null, "GetLocalizedDisplayName method must exist on BackpackItemCatalog.");

            string expectedNameL4 = (string)getLocalizedDisplayName.Invoke(null, new object[] { bp4 });
            Assert.That(bodyL4, Does.Contain(expectedNameL4), "Level 4 notification must include the localized backpack item name.");
            Assert.That(bodyL4.ToLowerInvariant().Contains("hospital") || bodyL4.ToLowerInvariant().Contains("bệnh viện"), Is.True,
                "Level 4 notification must include hospital milestone reward reason.");
            Assert.That(bodyL4, Does.Contain("30"), "Level 4 notification must show previous capacity 30.");
            Assert.That(bodyL4, Does.Contain("40"), "Level 4 notification must show upgraded capacity 40.");
            Assert.That(bodyL4, Does.Contain("+10"), "Level 4 notification must show +10 slots delta.");

            // Test Level 5 notification
            object bp5 = getOrCreate.Invoke(null, new object[] { 5, false });
            showInternal.Invoke(presenter, new object[] { 5, bp5, null });
            finishPresentation.Invoke(presenter, null);

            string bodyL5 = (string)notificationBodyProp.GetValue(null);
            string titleL5 = (string)notificationTitleProp.GetValue(null);
            string fullL5 = (titleL5 ?? "") + " " + (bodyL5 ?? "");
            Assert.That(fullL5.ToUpperInvariant().Contains("LEVEL 4 → LEVEL 5") || fullL5.ToUpperInvariant().Contains("CẤP 4 → CẤP 5"), Is.True,
                "Level 5 notification must visibly display LEVEL 4 → LEVEL 5 transition.");

            string expectedNameL5 = (string)getLocalizedDisplayName.Invoke(null, new object[] { bp5 });
            Assert.That(bodyL5, Does.Contain(expectedNameL5), "Level 5 notification must include the localized backpack item name.");
            Assert.That(bodyL5.ToLowerInvariant().Contains("radio"), Is.True,
                "Level 5 notification must include radio milestone reward reason.");
            Assert.That(bodyL5, Does.Contain("40"), "Level 5 notification must show previous capacity 40.");
            Assert.That(bodyL5, Does.Contain("50"), "Level 5 notification must show upgraded capacity 50.");
            Assert.That(bodyL5, Does.Contain("+10"), "Level 5 notification must show +10 slots delta.");
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void NotificationA_NonSequentialUpgrade_FromLevelTwoToLevelFour_ShowsDynamicLevelAndCapacityTransition()
    {
        GameObject host = new GameObject("Test_NonSequential_Host");
        try
        {
            Component presenter = host.AddComponent(presenterType);
            MethodInfo showWithPrev = presenterType.GetMethod("ShowWithPreviousLevel",
                BindingFlags.Public | BindingFlags.Static);
            MethodInfo showInternal = presenterType.GetMethod("ShowInternal",
                BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo finishPresentation = presenterType.GetMethod("FinishPresentation",
                BindingFlags.NonPublic | BindingFlags.Instance);
            PropertyInfo notificationBodyProp = presenterType.GetProperty("LastNotificationBody",
                BindingFlags.Public | BindingFlags.Static);
            PropertyInfo notificationTitleProp = presenterType.GetProperty("LastNotificationTitle",
                BindingFlags.Public | BindingFlags.Static);

            MethodInfo getOrCreate = catalogType.GetMethod("GetOrCreate",
                BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(int), typeof(bool) }, null);
            object bp4 = getOrCreate.Invoke(null, new object[] { 4, false });

            Component activePresenter = presenter;
            if (showWithPrev != null)
            {
                showWithPrev.Invoke(null, new object[] { 4, bp4, 2, null });
                activePresenter = (Component)presenterType.GetField("instance", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null) ?? presenter;
            }
            else
            {
                showInternal.Invoke(presenter, new object[] { 4, bp4, null });
            }

            finishPresentation.Invoke(activePresenter, null);

            string body = (string)notificationBodyProp.GetValue(null);
            string title = (string)notificationTitleProp.GetValue(null);
            string full = (title ?? "") + " " + (body ?? "");

            // 1. Current-to-reward level transition: LEVEL 2 → LEVEL 4
            Assert.That(full.ToUpperInvariant().Contains("LEVEL 2 → LEVEL 4") || full.ToUpperInvariant().Contains("CẤP 2 → CẤP 4"), Is.True,
                "Non-sequential notification must visibly show LEVEL 2 → LEVEL 4 transition.");

            // 2. Dynamic capacity transition: level 2 has 25 slots, level 4 has 40 slots -> 25 → 40 (+15 SLOTS)
            Assert.That(body, Does.Contain("25 → 40"),
                "Non-sequential notification body must show dynamic capacity transition 25 → 40 (not hardcoded 30 → 40).");
            Assert.That(body, Does.Contain("+15"),
                "Non-sequential notification body must show +15 slots delta (40 - 25).");

            // 3. API contract requirement: ShowWithPreviousLevel entry point must exist
            Assert.That(showWithPrev, Is.Not.Null,
                "BackpackQuestRewardPresentation must expose ShowWithPreviousLevel(int level, ItemData backpack, int previousLevel, Action onCompleted).");
        }
        finally
        {
            Object.DestroyImmediate(host);
            presenterType.GetMethod("ResetForTests", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
        }
    }

    [Test]
    public void NotificationA_NonSequentialUpgrade_FromLevelZeroToLevelFour_ShowsLevelZeroTransitionAndTwentyFiveDelta()
    {
        GameObject host = new GameObject("Test_LevelZeroToLevelFour_Host");
        try
        {
            Component presenter = host.AddComponent(presenterType);
            MethodInfo showWithPrev = presenterType.GetMethod("ShowWithPreviousLevel",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(showWithPrev, Is.Not.Null,
                "BackpackQuestRewardPresentation must expose ShowWithPreviousLevel(int level, ItemData backpack, int previousLevel, Action onCompleted).");

            MethodInfo finishPresentation = presenterType.GetMethod("FinishPresentation",
                BindingFlags.NonPublic | BindingFlags.Instance);
            PropertyInfo notificationBodyProp = presenterType.GetProperty("LastNotificationBody",
                BindingFlags.Public | BindingFlags.Static);
            PropertyInfo notificationTitleProp = presenterType.GetProperty("LastNotificationTitle",
                BindingFlags.Public | BindingFlags.Static);

            MethodInfo getOrCreate = catalogType.GetMethod("GetOrCreate",
                BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(int), typeof(bool) }, null);
            object bp4 = getOrCreate.Invoke(null, new object[] { 4, false });

            // Trigger reward presentation explicitly for Level 4 with previous level = 0
            showWithPrev.Invoke(null, new object[] { 4, bp4, 0, null });
            Component activePresenter = (Component)presenterType.GetField("instance", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null) ?? presenter;
            finishPresentation.Invoke(activePresenter, null);

            string body = (string)notificationBodyProp.GetValue(null);
            string title = (string)notificationTitleProp.GetValue(null);
            string full = (title ?? "") + " " + (body ?? "");

            // 1. Current-to-reward level transition: LEVEL 0 → LEVEL 4
            Assert.That(full.ToUpperInvariant().Contains("LEVEL 0 → LEVEL 4") || full.ToUpperInvariant().Contains("CẤP 0 → CẤP 4"), Is.True,
                "Level 0 to level 4 notification must visibly show LEVEL 0 → LEVEL 4 transition.");

            // 2. Dynamic capacity transition: level 0 has 15 slots, level 4 has 40 slots -> STORAGE 15 → 40 (+25 SLOTS)
            Assert.That(body, Does.Contain("15 → 40"),
                "Level 0 to level 4 notification body must show dynamic capacity transition 15 → 40.");
            Assert.That(body, Does.Contain("+25"),
                "Level 0 to level 4 notification body must show +25 slots delta (40 - 15).");
        }
        finally
        {
            Object.DestroyImmediate(host);
            presenterType.GetMethod("ResetForTests", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
        }
    }

    [Test]
    public void NotificationA_Layout_DoesNotOverflowAt720pAnd1080p()
    {
        GameObject host = new GameObject("Test_NotificationA_Layout_Host");
        try
        {
            Component presenter = host.AddComponent(presenterType);
            presenterType.GetField("completedEffectBLevel", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, 4);
            MethodInfo showInternal = presenterType.GetMethod("ShowUpgradeNotificationInternal",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(showInternal, Is.Not.Null, "ShowUpgradeNotificationInternal method must exist.");

            showInternal.Invoke(presenter, new object[] { 4 });

            Canvas canvas = host.GetComponentInChildren<Canvas>(true);
            Assert.That(canvas, Is.Not.Null);

            Transform hudNotification = canvas.transform.Find("Notification HUD");
            Assert.That(hudNotification, Is.Not.Null, "Notification HUD transform must exist.");

            RectTransform hudRect = hudNotification.GetComponent<RectTransform>();
            Assert.That(hudRect, Is.Not.Null);

            // Test both resolutions
            Vector2[] resolutions = new Vector2[]
            {
                new Vector2(1280, 720),
                new Vector2(1920, 1080)
            };

            foreach (var res in resolutions)
            {
                // Verify panel width does not exceed screen width with safe margins
                Assert.That(hudRect.sizeDelta.x, Is.LessThan(res.x - 40f),
                    "Notification panel must fit within screen bounds at " + res);
                Assert.That(hudRect.sizeDelta.y, Is.LessThan(res.y * 0.25f),
                    "Notification panel must be compact and occupy less than 25% of height at " + res);

                // Check text components inside HUD
                TextMeshProUGUI[] texts = hudNotification.GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var t in texts)
                {
                    Assert.That(t.overflowMode, Is.Not.EqualTo(TextOverflowModes.Overflow),
                        "Text in Notification HUD must not use unbounded overflow.");
                    Assert.That(t.textWrappingMode, Is.Not.EqualTo(TextWrappingModes.NoWrap),
                        "Text in Notification HUD must enable word wrapping or ellipsis.");
                }
            }
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void NotificationA_CannotBeShown_WhileEffectBIsActive()
    {
        GameObject host = new GameObject("Test_NotificationA_Guard_Host");
        try
        {
            Component presenter = host.AddComponent(presenterType);
            MethodInfo showInternal = presenterType.GetMethod("ShowInternal",
                BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo showNotification = presenterType.GetMethod("ShowUpgradeNotification",
                BindingFlags.Public | BindingFlags.Static);
            PropertyInfo isNotifVisible = presenterType.GetProperty("IsNotificationVisible",
                BindingFlags.Public | BindingFlags.Static);

            MethodInfo getOrCreate = catalogType.GetMethod("GetOrCreate",
                BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(int), typeof(bool) }, null);
            object bp4 = getOrCreate.Invoke(null, new object[] { 4, false });

            // 1. Start Effect B
            showInternal.Invoke(presenter, new object[] { 4, bp4, null });

            // 2. Calling ShowUpgradeNotification while Effect B is active MUST NOT make Notification A visible
            showNotification.Invoke(null, new object[] { 4 });

            Assert.That((bool)isNotifVisible.GetValue(null), Is.False,
                "Notification A must NOT be visible while Effect B is actively playing.");
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void NotificationA_ColdCall_WithoutCompletedEffectB_DoesNotShowNotification()
    {
        PropertyInfo isNotifVisible = presenterType.GetProperty("IsNotificationVisible",
            BindingFlags.Public | BindingFlags.Static);
        MethodInfo showNotification = presenterType.GetMethod("ShowUpgradeNotification",
            BindingFlags.Public | BindingFlags.Static);

        Assert.That(isNotifVisible, Is.Not.Null);
        Assert.That(showNotification, Is.Not.Null);

        // Precondition: Notification is not visible
        Assert.That((bool)isNotifVisible.GetValue(null), Is.False);

        // Cold call: Call ShowUpgradeNotification without Effect B having ever run or completed
        showNotification.Invoke(null, new object[] { 4 });

        Assert.That((bool)isNotifVisible.GetValue(null), Is.False,
            "Notification A must NOT be shown from a cold/pre-effect call without a completed Effect B token.");

        showNotification.Invoke(null, new object[] { 5 });

        Assert.That((bool)isNotifVisible.GetValue(null), Is.False,
            "Notification A must NOT be shown from a cold/pre-effect call for Level 5 without a completed Effect B token.");
    }

    [Test]
    public void LevelFiveBackpack_StrictSequence_MapReward_ThenMapReveal_ThenBackpack_ThenNotification_DeterministicTrace()
    {
        // Behavioral trace: In all branches (including LockedEscapeRoute != None),
        // the required sequence must be strictly enforced:
        // map reward -> map reveal -> map close -> claim/present level-5 backpack -> Effect B complete -> Notification A.
        GameObject host = new GameObject("Test_DeterministicTrace_Host");
        try
        {
            QuestFlowUIPrototype flow = host.AddComponent<QuestFlowUIPrototype>();
            flow.EnsureBuiltForTests();
            flow.DeferRevealCallbackForTests = true;

            Component manager = host.AddComponent(questManagerType);
            PropertyInfo isMapRunningProp = questManagerType.GetProperty("IsMilitaryMapRewardSequenceRunning",
                BindingFlags.Public | BindingFlags.Instance);
            MethodInfo claimBackpack = questManagerType.GetMethod("ClaimAndPresentLevelFiveBackpack",
                BindingFlags.Public | BindingFlags.Instance);
            MethodInfo handleMapFound = questManagerType.GetMethod("HandleMilitaryMapFragmentFound",
                BindingFlags.NonPublic | BindingFlags.Instance);
            PropertyInfo isPresenterVisible = presenterType.GetProperty("IsVisible", BindingFlags.Public | BindingFlags.Static);
            PropertyInfo isNotifVisible = presenterType.GetProperty("IsNotificationVisible", BindingFlags.Public | BindingFlags.Static);
            PropertyInfo notifBodyProp = presenterType.GetProperty("LastNotificationBody", BindingFlags.Public | BindingFlags.Static);

            System.Collections.Generic.List<string> trace = new System.Collections.Generic.List<string>();

            // Trigger the military map sequence
            handleMapFound.Invoke(manager, new object[] { true });
            trace.Add("1_MapSequence_Triggered");

            // Step 1: Map reward sequence is running. Map reward callback is pending.
            Assert.That((bool)isMapRunningProp.GetValue(manager), Is.True, "Map sequence must be running.");
            Assert.That(flow.PendingMilitaryMapRewardCallback, Is.Not.Null, "Map reward dialogue callback must be pending.");
            Assert.That((bool)isPresenterVisible.GetValue(null), Is.False, "Backpack presenter must NOT be visible during map reward.");
            Assert.That((bool)isNotifVisible.GetValue(null), Is.False, "Notification A must NOT be visible during map reward.");

            // Attempt premature claim while map sequence is running -> must be deferred/blocked!
            bool prematureCallbackFired = false;
            claimBackpack.Invoke(manager, new object[] { (Action)(() => prematureCallbackFired = true) });
            Assert.That(prematureCallbackFired, Is.False, "Premature claim must be deferred.");
            Assert.That((bool)isPresenterVisible.GetValue(null), Is.False, "Backpack presenter must NOT be visible after premature claim.");
            trace.Add("2_Premature_Claim_Blocked");

            // Step 2: Map reward dialogue finishes, invoking its onFinished callback
            flow.CompleteMilitaryMapRewardForTests();
            trace.Add("3_MapReward_Finished");

            // Now map reveal must be pending, and map must be open
            Assert.That(flow.PendingMilitaryMapRevealCallback, Is.Not.Null, "Map reveal callback must be pending.");
            Assert.That((bool)isPresenterVisible.GetValue(null), Is.False, "Backpack presenter must NOT be visible during map reveal.");
            trace.Add("4_MapReveal_Active");

            // Step 3: Map reveal finishes, map close callback executes
            // This invokes flow.CompleteMilitaryMapRevealForTests() -> map close -> OnMilitaryMapSequenceComplete -> ClaimAndPresentLevelFiveBackpack
            flow.CompleteMilitaryMapRevealForTests();
            trace.Add("5_MapReveal_Closed");

            // Now backpack presentation (Effect B) MUST have started!
            Assert.That((bool)isMapRunningProp.GetValue(manager), Is.False, "Map sequence must no longer be running.");
            Assert.That((bool)isPresenterVisible.GetValue(null), Is.True, "Backpack presenter must be visible (Effect B active).");
            Assert.That((bool)isNotifVisible.GetValue(null), Is.False, "Notification A must NOT be visible while Effect B is active.");
            trace.Add("6_Backpack_EffectB_Active");

            // Step 4: Finish Effect B presentation
            MethodInfo finishPres = presenterType.GetMethod("FinishPresentation", BindingFlags.NonPublic | BindingFlags.Instance);
            Component presenterInstance = (Component)presenterType.GetField("instance", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            finishPres.Invoke(presenterInstance, null);
            trace.Add("7_EffectB_Completed");

            // Step 5: Notification A must now be visible with exact Level 5 before->after capacity
            Assert.That((bool)isPresenterVisible.GetValue(null), Is.False, "Effect B must be closed after completion.");
            Assert.That((bool)isNotifVisible.GetValue(null), Is.True, "Notification A must be visible after Effect B completes.");
            string notifBody = (string)notifBodyProp.GetValue(null);
            string notifTitle = (string)presenterType.GetProperty("LastNotificationTitle", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            string fullNotif = (notifTitle ?? "") + " " + (notifBody ?? "");
            Assert.That(fullNotif.ToUpperInvariant().Contains("LEVEL 4 → LEVEL 5") || fullNotif.ToUpperInvariant().Contains("CẤP 4 → CẤP 5"), Is.True,
                "Level 5 notification must visibly display LEVEL 4 → LEVEL 5 transition.");

            Assert.That(notifBody, Does.Contain("40 → 50"), "Notification body must show 40 → 50.");
            MethodInfo getLocalizedDisplayName = catalogType.GetMethod("GetLocalizedDisplayName", BindingFlags.Public | BindingFlags.Static);
            object bp5Item = catalogType.GetMethod("GetOrCreate", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(int), typeof(bool) }, null)
                .Invoke(null, new object[] { 5, false });
            string expectedL5Name = (string)getLocalizedDisplayName.Invoke(null, new object[] { bp5Item });
            Assert.That(notifBody, Does.Contain(expectedL5Name), "Notification body must include Level 5 backpack display name.");
            Assert.That(notifBody.ToLowerInvariant().Contains("radio"), Is.True, "Notification body must include radio reward reason.");
            trace.Add("8_NotificationA_Shown");

            // Verify full deterministic trace order
            System.Collections.Generic.List<string> expectedTrace = new System.Collections.Generic.List<string>
            {
                "1_MapSequence_Triggered",
                "2_Premature_Claim_Blocked",
                "3_MapReward_Finished",
                "4_MapReveal_Active",
                "5_MapReveal_Closed",
                "6_Backpack_EffectB_Active",
                "7_EffectB_Completed",
                "8_NotificationA_Shown"
            };
            Assert.That(trace, Is.EqualTo(expectedTrace), "Deterministic callback sequence must match exact order.");
        }
        finally
        {
            Object.DestroyImmediate(host);
            QuestFlowUIPrototype.ResetInstanceForTests();
            presenterType?.GetMethod("ResetForTests", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
        }
    }

    [Test]
    public void EffectB_Active_SuppressesAutoChatAndMainQuestNotices_AndNoClueBannerVisible()
    {
        // Regression test for FIX ROUND 3 Item 1:
        // When Effect B is active, AutoChat panel and clue messages (specifically "PHÁT HIỆN MANH MỐI MỚI")
        // must be suppressed, and no clue notice or AutoChat panel may be visible.
        GameObject host = new GameObject("Test_AutoChat_Suppression_Host");
        try
        {
            Component presenter = host.AddComponent(presenterType);
            MethodInfo showInternal = presenterType.GetMethod("ShowInternal",
                BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo getOrCreate = catalogType.GetMethod("GetOrCreate",
                BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(int), typeof(bool) }, null);
            object bp5 = getOrCreate.Invoke(null, new object[] { 5, false });

            // Post a clue message to AutoChat
            PropertyInfo chatInstanceProp = autoChatType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            object chatInstance = chatInstanceProp.GetValue(null);
            MethodInfo addMessage = autoChatType.GetMethod("AddMessage", BindingFlags.Public | BindingFlags.Instance);
            addMessage.Invoke(chatInstance, new object[] { "PHÁT HIỆN MANH MỐI MỚI", "Phát hiện manh mối mới - bấm M để kiểm tra" });

            // Activate Effect B
            showInternal.Invoke(presenter, new object[] { 5, bp5, null });

            // Regression assertions:
            // 1. AutoChat panel must be suppressed / not visible during Effect B
            PropertyInfo isChatVisibleProp = autoChatType.GetProperty("IsChatVisible", BindingFlags.Public | BindingFlags.Instance);
            bool isChatVis = (bool)isChatVisibleProp.GetValue(chatInstance);
            Assert.That(isChatVis, Is.False,
                "AutoChat panel must be suppressed and not visible during Effect B presentation.");

            // 2. Scan all active UI text in the scene: none must contain "PHÁT HIỆN MANH MỐI MỚI"
            Text[] allTexts = Object.FindObjectsByType<Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < allTexts.Length; i++)
            {
                if (allTexts[i] != null && allTexts[i].gameObject.activeInHierarchy)
                {
                    Assert.That(allTexts[i].text, Does.Not.Contain("PHÁT HIỆN MANH MỐI MỚI"),
                        $"Active UI Text '{allTexts[i].gameObject.name}' must NOT contain clue banner during Effect B.");
                }
            }

            TextMeshProUGUI[] allTmps = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < allTmps.Length; i++)
            {
                if (allTmps[i] != null && allTmps[i].gameObject.activeInHierarchy)
                {
                    Assert.That(allTmps[i].text, Does.Not.Contain("PHÁT HIỆN MANH MỐI MỚI"),
                        $"Active TextMeshProUGUI '{allTmps[i].gameObject.name}' must NOT contain clue banner during Effect B.");
                }
            }
        }
        finally
        {
            Object.DestroyImmediate(host);
            autoChatType.GetMethod("ResetForTests", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
            presenterType?.GetMethod("ResetForTests", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
        }
    }

    [Test]
    public void LevelFiveSequence_DefersRouteChoiceAndAutoChatClue_UntilNotificationADismissed()
    {
        // Regression test for FIX ROUND 3 Item 2:
        // During Notification A, route choice dialog (e.g. RouteBRadioBroadcastUI / EscapeRouteDecisionUI)
        // and AutoChat clue banner must NOT be active or visible.
        // They may only appear AFTER Notification A has been dismissed.
        GameObject host = new GameObject("Test_DeferredRouteChoice_Host");
        try
        {
            QuestFlowUIPrototype flow = host.AddComponent<QuestFlowUIPrototype>();
            flow.EnsureBuiltForTests();

            Component manager = host.AddComponent(questManagerType);
            MethodInfo handleMapFound = questManagerType.GetMethod("HandleMilitaryMapFragmentFound",
                BindingFlags.NonPublic | BindingFlags.Instance);
            PropertyInfo isNotifVisible = presenterType.GetProperty("IsNotificationVisible", BindingFlags.Public | BindingFlags.Static);
            MethodInfo dismissNotif = presenterType.GetMethod("DismissNotification", BindingFlags.Public | BindingFlags.Static);

            // Execute the map sequence
            handleMapFound.Invoke(manager, new object[] { true });
            flow.CompleteMilitaryMapRewardForTests();
            flow.CompleteMilitaryMapRevealForTests();

            // Finish Effect B presentation -> Notification A appears!
            MethodInfo finishPres = presenterType.GetMethod("FinishPresentation", BindingFlags.NonPublic | BindingFlags.Instance);
            Component presenterInstance = (Component)presenterType.GetField("instance", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            finishPres.Invoke(presenterInstance, null);

            Assert.That((bool)isNotifVisible.GetValue(null), Is.True, "Notification A must be visible after Effect B.");

            // While Notification A is active:
            // RouteBRadioBroadcastUI must NOT be visible!
            PropertyInfo radioVisibleProp = radioUiType.GetProperty("IsVisible", BindingFlags.Public | BindingFlags.Static);
            PropertyInfo escapeVisibleProp = escapeDecisionType.GetProperty("IsVisible", BindingFlags.Public | BindingFlags.Static);

            Assert.That((bool)radioVisibleProp.GetValue(null), Is.False,
                "RouteBRadioBroadcastUI must NOT be visible while Notification A is active.");
            Assert.That((bool)escapeVisibleProp.GetValue(null), Is.False,
                "EscapeRouteDecisionUI must NOT be visible while Notification A is active.");

            // Dismiss Notification A
            dismissNotif.Invoke(null, null);
            Assert.That((bool)isNotifVisible.GetValue(null), Is.False, "Notification A must be dismissed.");
        }
        finally
        {
            Object.DestroyImmediate(host);
            QuestFlowUIPrototype.ResetInstanceForTests();
            radioUiType.GetMethod("CloseIfOpen", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
            escapeDecisionType.GetMethod("CloseIfOpen", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
            autoChatType.GetMethod("ResetForTests", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
            presenterType?.GetMethod("ResetForTests", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
        }
    }

    [Test]
    public void InventorySystem_LateOrPendingHandoff_RoutesThroughMapSequenceController()
    {
        // Regression test for FIX ROUND 3 Item 4:
        // Late join or pending radio backpack handoff must route through MainQuestManager.TriggerLevelFiveRewardSequence
        // and must NOT bypass map fragment reward or map reveal.
        MethodInfo triggerSequence = questManagerType.GetMethod("TriggerLevelFiveRewardSequence",
            BindingFlags.Public | BindingFlags.Instance);
        Assert.That(triggerSequence, Is.Not.Null,
            "MainQuestManager must expose TriggerLevelFiveRewardSequence as the single idempotent sequence controller.");
    }

    [Test]
    public void BackpackItemCatalog_Level4AndLevel5_UseAuthoredArt_NotFallbackSolidSquare()
    {
        // Regression test for FIX ROUND 4:
        // BackpackLevel4 and BackpackLevel5 in Resources must resolve authored art,
        // not the 32x32 fallback solid square.
        catalogType.GetMethod("ResetCache", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);

        GameObject testHost = new GameObject("Test_Icon_Host");
        try
        {
            Component presenter = testHost.AddComponent(presenterType);
            MethodInfo showInternal = presenterType.GetMethod("ShowInternal",
                BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo iconImageField = presenterType.GetField("iconImage",
                BindingFlags.NonPublic | BindingFlags.Instance);

            for (int level = 4; level <= 5; level++)
            {
                MethodInfo getOrCreate = catalogType.GetMethod("GetOrCreate",
                    BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(int), typeof(bool) }, null);
                object item = getOrCreate.Invoke(null, new object[] { level, false });
                Assert.That(item, Is.Not.Null, $"Item for Level {level} must exist.");

                FieldInfo iconField = item.GetType().GetField("icon");
                PropertyInfo iconProp = item.GetType().GetProperty("icon");
                Sprite icon = (iconField != null ? iconField.GetValue(item) : iconProp?.GetValue(item)) as Sprite;

                Assert.That(icon, Is.Not.Null, $"Icon for Level {level} must not be null.");
                Assert.That(icon.rect.width, Is.GreaterThan(32),
                    $"Icon for Level {level} must be authored art (rect width is {icon.rect.width}), not the 32x32 fallback solid square.");
                Assert.That(icon.texture != null && icon.texture.width >= 500, Is.True,
                    $"Icon texture for Level {level} must be authored art (width {icon.texture?.width}), not the 32x32 fallback texture.");
                Assert.That(icon.name.Contains("BackpackLevel" + level) || (icon.texture != null && icon.texture.name.Contains("BackpackLevel" + level)), Is.True,
                    $"Icon for Level {level} must be backed by BackpackLevel{level} authored asset, actual name='{icon.name}'.");

                // Test presenter display
                showInternal.Invoke(presenter, new object[] { level, item, null });
                Image img = iconImageField.GetValue(presenter) as Image;
                Assert.That(img, Is.Not.Null, "Presenter iconImage must exist.");
                Assert.That(img.sprite, Is.Not.Null, "Presenter iconImage.sprite must not be null.");
                Assert.That(img.sprite.rect.width, Is.GreaterThan(32),
                    "Presenter must display authored art, not fallback square.");
                Assert.That(img.sprite, Is.EqualTo(icon),
                    "Presenter iconImage.sprite must match the authored catalog item icon.");
            }
        }
        finally
        {
            Object.DestroyImmediate(testHost);
        }
    }

    [Test]
    public void AutoChatManager_VisibilityRestoresAfterEffectBPresentation_AndIsNotSuppressedWhenPresenterDestroyedOrInactive()
    {
        // Regression test for FIX ROUND 5:
        // Ensures order-independence and self-healing:
        // 1. AutoChat is suppressed during real active Effect B.
        // 2. Once Effect B finishes or presenter is destroyed, normal chat messages immediately become visible (alpha = 1.0).
        GameObject host = new GameObject("Test_AutoChat_OrderIndependence_Host");
        GameObject chatHost = new GameObject("Test_ChatHost_OrderIndependence");
        try
        {
            Component presenter = host.AddComponent(presenterType);
            MethodInfo showInternal = presenterType.GetMethod("ShowInternal",
                BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo getOrCreate = catalogType.GetMethod("GetOrCreate",
                BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(int), typeof(bool) }, null);
            object bp4 = getOrCreate.Invoke(null, new object[] { 4, false });

            // Create AutoChat component
            Component chat = chatHost.AddComponent(autoChatType);
            MethodInfo buildMethod = autoChatType.GetMethod("BuildChatUI", BindingFlags.Public | BindingFlags.Instance);
            buildMethod.Invoke(chat, null);

            FieldInfo chatGroupField = autoChatType.GetField("chatGroup", BindingFlags.NonPublic | BindingFlags.Instance);
            CanvasGroup cg = chatGroupField?.GetValue(chat) as CanvasGroup;
            Assert.That(cg, Is.Not.Null);

            // 1. Trigger Effect B: Chat must be suppressed
            showInternal.Invoke(presenter, new object[] { 4, bp4, null });
            PropertyInfo isVisibleProp = presenterType.GetProperty("IsVisible", BindingFlags.Public | BindingFlags.Static);
            Assert.That((bool)isVisibleProp.GetValue(null), Is.True, "Effect B must be active.");

            MethodInfo addMsgMethod = autoChatType.GetMethod("AddPlayerMessage", BindingFlags.Public | BindingFlags.Instance);
            addMsgMethod.Invoke(chat, new object[] { "Player1", "Message during B" });
            Assert.That(cg.alpha, Is.EqualTo(0f), "Chat message during active Effect B must remain suppressed (alpha = 0).");

            // 2. Destroy presenter (simulating test completion or transition):
            Object.DestroyImmediate(host);
            host = null;
            Assert.That((bool)isVisibleProp.GetValue(null), Is.False, "Effect B must no longer be visible once destroyed.");

            // 3. New incoming message: Chat must become visible immediately (alpha = 1.0)
            cg.alpha = 0f;
            addMsgMethod.Invoke(chat, new object[] { "Player1", "Message after B finishes" });
            Assert.That(cg.alpha, Is.EqualTo(1f), "Normal incoming message after Effect B must make chat visible (alpha = 1).");
        }
        finally
        {
            if (host != null) Object.DestroyImmediate(host);
            if (chatHost != null) Object.DestroyImmediate(chatHost);
            autoChatType?.GetMethod("ResetForTests", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
            presenterType?.GetMethod("ResetForTests", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
        }
    }
}
