using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class RouteBClueAndTopHudRegressionTests
{
    private Type localizationType;
    private Type clueCatalogType;
    private Type hudLayoutType;
    private Type presenterType;
    private Type questManagerType;
    private Type militaryManagerType;
    private object englishEnum;
    private object vietnameseEnum;
    private MethodInfo setLanguageMethod;

    [SetUp]
    public void SetUp()
    {
        localizationType = Type.GetType("GameLocalization, Assembly-CSharp");
        clueCatalogType = Type.GetType("QuestRouteClueItemCatalog, Assembly-CSharp");
        hudLayoutType = Type.GetType("GameplayHudLayout, Assembly-CSharp");
        presenterType = Type.GetType("BackpackQuestRewardPresentation, Assembly-CSharp");
        questManagerType = Type.GetType("MainQuestManager, Assembly-CSharp");
        militaryManagerType = Type.GetType("MilitaryBaseQuestManager, Assembly-CSharp");

        Assert.That(localizationType, Is.Not.Null, "GameLocalization must exist in Assembly-CSharp.");
        Assert.That(clueCatalogType, Is.Not.Null, "QuestRouteClueItemCatalog must exist in Assembly-CSharp.");
        Assert.That(hudLayoutType, Is.Not.Null, "GameplayHudLayout must exist in Assembly-CSharp.");
        Assert.That(presenterType, Is.Not.Null, "BackpackQuestRewardPresentation must exist in Assembly-CSharp.");

        Type langEnum = localizationType.GetNestedType("Language");
        Assert.That(langEnum, Is.Not.Null, "GameLocalization.Language enum must exist.");
        setLanguageMethod = localizationType.GetMethod("SetLanguage", new[] { langEnum, typeof(bool) });
        Assert.That(setLanguageMethod, Is.Not.Null, "GameLocalization.SetLanguage method must exist.");

        englishEnum = Enum.Parse(langEnum, "English");
        vietnameseEnum = Enum.Parse(langEnum, "Vietnamese");

        presenterType.GetMethod("ResetForTests", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
    }

    [TearDown]
    public void TearDown()
    {
        if (setLanguageMethod != null && vietnameseEnum != null)
        {
            setLanguageMethod.Invoke(null, new object[] { vietnameseEnum, false });
        }
        QuestUILocalization.SetVietnamese(true);
        presenterType?.GetMethod("ResetForTests", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
        QuestFlowUIPrototype.ResetInstanceForTests();
    }

    private void SetLang(bool english)
    {
        setLanguageMethod.Invoke(null, new object[] { english ? englishEnum : vietnameseEnum, false });
        QuestUILocalization.SetVietnamese(!english);
    }

    [Test]
    public void RouteBClues_EnglishLanguage_AllThreeCluesAndChromeAreEnglish()
    {
        SetLang(true);

        MethodInfo getDisplayName = clueCatalogType.GetMethod("GetDisplayName", BindingFlags.Public | BindingFlags.Static);
        MethodInfo getReadingText = clueCatalogType.GetMethod("GetReadingText", BindingFlags.Public | BindingFlags.Static);
        MethodInfo getInferenceText = clueCatalogType.GetMethod("GetInferenceText", BindingFlags.Public | BindingFlags.Static);

        Assert.That(getDisplayName, Is.Not.Null);
        Assert.That(getReadingText, Is.Not.Null);
        Assert.That(getInferenceText, Is.Not.Null);

        Type clueKindType = Type.GetType("QuestRouteClueKind, Assembly-CSharp");
        Assert.That(clueKindType, Is.Not.Null);

        Array kinds = Enum.GetValues(clueKindType);
        Assert.That(kinds.Length, Is.GreaterThanOrEqualTo(3), "Must have at least 3 clue kinds.");

        for (int i = 0; i < 3; i++)
        {
            object kind = kinds.GetValue(i);
            string name = (string)getDisplayName.Invoke(null, new object[] { kind });
            string reading = (string)getReadingText.Invoke(null, new object[] { kind });
            string inference = (string)getInferenceText.Invoke(null, new object[] { kind });

            Assert.That(name, Is.Not.Null.And.Not.Empty, $"Clue {kind} name must not be empty in English.");
            Assert.That(reading, Is.Not.Null.And.Not.Empty, $"Clue {kind} reading text must not be empty in English.");
            Assert.That(inference, Is.Not.Null.And.Not.Empty, $"Clue {kind} inference text must not be empty in English.");

            // Must NOT be Vietnamese
            Assert.That(name, Does.Not.Contain("Phiếu").And.Not.Contain("Thông báo").And.Not.Contain("Ghi chú"),
                $"Clue {kind} display name must be in English, got '{name}'.");
            Assert.That(reading, Does.Not.Contain("Phiếu điều chuyển").And.Not.Contain("Tuyến sơ tán").And.Not.Contain("Mảnh giấy"),
                $"Clue {kind} reading text must be in English.");
            Assert.That(inference, Does.StartWith("INFERENCE"),
                $"Clue {kind} inference text must start with 'INFERENCE', got '{inference}'.");
            Assert.That(inference, Does.Not.Contain("SUY LUẬN"),
                $"Clue {kind} inference text must not contain Vietnamese 'SUY LUẬN'.");
        }

        // Test UI modal chrome
        GameObject host = new GameObject("Test_ClueModalChrome_Host");
        try
        {
            QuestFlowUIPrototype proto = host.AddComponent<QuestFlowUIPrototype>();
            proto.EnsureBuiltForTests();

            proto.ShowRouteClueReading("Test Title", "Test Body", "Test Inference");

            FieldInfo eyebrowField = typeof(QuestFlowUIPrototype).GetField("clueReadingEyebrow", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(eyebrowField, Is.Not.Null);
            TextMeshProUGUI eyebrowText = (TextMeshProUGUI)eyebrowField.GetValue(proto);
            Assert.That(eyebrowText.text, Does.StartWith("ROUTE CLUES"),
                $"Eyebrow in English must start with 'ROUTE CLUES', got '{eyebrowText.text}'.");
            Assert.That(eyebrowText.text, Does.Not.Contain("MANH MỐI TUYẾN ĐƯỜNG"),
                "Eyebrow in English must NOT contain Vietnamese.");

            Transform closeHintTrans = host.transform.Find("Quest UI Canvas/Clue Reading Overlay/Clue Reading Panel/Clue Reading Close Hint");
            Assert.That(closeHintTrans, Is.Not.Null, "Close hint object must exist in clue reading panel.");
            TextMeshProUGUI closeHintText = closeHintTrans.GetComponent<TextMeshProUGUI>();
            Assert.That(closeHintText.text, Does.Not.Contain("CẤT MANH MỐI"),
                $"Close hint in English must not contain Vietnamese 'CẤT MANH MỐI', got '{closeHintText.text}'.");
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void RouteBClues_LanguageSwitch_RefreshesItemDataName_AndTryGetKindResolvesBothLocales()
    {
        // 1. Create in Vietnamese
        SetLang(false);

        MethodInfo getOrCreate = clueCatalogType.GetMethod("GetOrCreate", BindingFlags.Public | BindingFlags.Static);
        MethodInfo tryGetKind = clueCatalogType.GetMethod("TryGetKind",
            BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(string), Type.GetType("QuestRouteClueKind, Assembly-CSharp").MakeByRefType() }, null);
        Type clueKindType = Type.GetType("QuestRouteClueKind, Assembly-CSharp");

        Assert.That(getOrCreate, Is.Not.Null);
        Assert.That(tryGetKind, Is.Not.Null);

        object deliveryInvoiceKind = Enum.Parse(clueKindType, "DeliveryInvoice");
        object item = getOrCreate.Invoke(null, new object[] { deliveryInvoiceKind });
        Assert.That(item, Is.Not.Null);
        FieldInfo itemNameField = item.GetType().GetField("itemName");
        string initialName = (string)itemNameField.GetValue(item);
        Assert.That(initialName, Does.Contain("Phiếu điều chuyển"), "Initial item name in Vietnamese should be Vietnamese.");

        // 2. Switch to English
        SetLang(true);

        // Fetch again or verify refresh
        object itemEnglish = getOrCreate.Invoke(null, new object[] { deliveryInvoiceKind });
        string englishName = (string)itemNameField.GetValue(itemEnglish);
        Assert.That(englishName, Does.Not.Contain("Phiếu điều chuyển"),
            $"Item name after switching to English must not be Vietnamese, got '{englishName}'.");

        // 3. TryGetKind must resolve via:
        // - Stable ID
        object[] argsStable = new object[] { "ROUTE_CLUE_DELIVERY_INVOICE", null };
        bool resolvedStable = (bool)tryGetKind.Invoke(null, argsStable);
        Assert.That(resolvedStable, Is.True, "TryGetKind must resolve via stable ID ROUTE_CLUE_DELIVERY_INVOICE.");
        Assert.That(argsStable[1], Is.EqualTo(deliveryInvoiceKind));

        // - Vietnamese name (backward compatibility / existing saves / network)
        object[] argsVi = new object[] { "Phiếu điều chuyển vật tư", null };
        bool resolvedVi = (bool)tryGetKind.Invoke(null, argsVi);
        Assert.That(resolvedVi, Is.True, "TryGetKind must resolve via Vietnamese name even while current language is English.");
        Assert.That(argsVi[1], Is.EqualTo(deliveryInvoiceKind));

        // - English name
        object[] argsEn = new object[] { englishName, null };
        bool resolvedEn = (bool)tryGetKind.Invoke(null, argsEn);
        Assert.That(resolvedEn, Is.True, $"TryGetKind must resolve via English name '{englishName}'.");
        Assert.That(argsEn[1], Is.EqualTo(deliveryInvoiceKind));
    }

    [Test]
    public void TopCenterHud_LayoutNonOverlap_At720p_768p_1080p()
    {
        // Must provide safe lane methods in GameplayHudLayout
        MethodInfo getObjectiveRect = hudLayoutType.GetMethod("GetTopCenterObjectiveRect",
            BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(float), typeof(float) }, null);
        MethodInfo getSchoolClueRect = hudLayoutType.GetMethod("GetTopCenterSchoolClueRect",
            BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(float), typeof(float), typeof(bool) }, null);
        MethodInfo getToastBounds = hudLayoutType.GetMethod("GetTopCenterBackpackNotificationBounds",
            BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(float), typeof(float) }, null);

        Assert.That(getObjectiveRect, Is.Not.Null, "GameplayHudLayout must provide GetTopCenterObjectiveRect().");
        Assert.That(getSchoolClueRect, Is.Not.Null, "GameplayHudLayout must provide GetTopCenterSchoolClueRect().");
        Assert.That(getToastBounds, Is.Not.Null, "GameplayHudLayout must provide GetTopCenterBackpackNotificationBounds().");

        Vector2[] resolutions = new Vector2[]
        {
            new Vector2(1280f, 720f),
            new Vector2(1366f, 768f),
            new Vector2(1920f, 1080f)
        };

        foreach (Vector2 res in resolutions)
        {
            // Case 1: Toast is NOT visible
            // Objective (Lane 0) and School Clue (Lane 1) must NOT overlap.
            Rect objRect = (Rect)getObjectiveRect.Invoke(null, new object[] { res.x, res.y });
            Rect schoolRectNoToast = (Rect)getSchoolClueRect.Invoke(null, new object[] { res.x, res.y, false });

            Assert.That(objRect.yMax, Is.LessThan(schoolRectNoToast.yMin),
                $"At {res.x}x{res.y}, Objective (bottom {objRect.yMax}) must be strictly above School Clue (top {schoolRectNoToast.yMin}) when no toast.");
            Assert.That(objRect.Overlaps(schoolRectNoToast), Is.False,
                $"At {res.x}x{res.y}, Objective and School Clue must NOT overlap.");

            // Case 2: Toast IS visible
            // Objective (Lane 0), Toast (Lane 1), and School Clue (Lane 2) must all be distinct with ZERO overlap!
            Rect toastRect = (Rect)getToastBounds.Invoke(null, new object[] { res.x, res.y });
            Rect schoolRectWithToast = (Rect)getSchoolClueRect.Invoke(null, new object[] { res.x, res.y, true });

            Assert.That(objRect.yMax, Is.LessThan(toastRect.yMin),
                $"At {res.x}x{res.y}, Objective (bottom {objRect.yMax}) must be strictly above Backpack Toast (top {toastRect.yMin}).");
            Assert.That(toastRect.yMax, Is.LessThan(schoolRectWithToast.yMin),
                $"At {res.x}x{res.y}, Backpack Toast (bottom {toastRect.yMax}) must be strictly above School Clue (top {schoolRectWithToast.yMin}).");

            Assert.That(objRect.Overlaps(toastRect), Is.False,
                $"At {res.x}x{res.y}, Objective and Backpack Toast must NOT overlap.");
            Assert.That(toastRect.Overlaps(schoolRectWithToast), Is.False,
                $"At {res.x}x{res.y}, Backpack Toast and School Clue must NOT overlap.");
            Assert.That(objRect.Overlaps(schoolRectWithToast), Is.False,
                $"At {res.x}x{res.y}, Objective and School Clue must NOT overlap.");
        }
    }

    [Test]
    public void TopCenterHud_ToastAnimationEnvelope_NeverTouchesObjective_AtSupportedResolutions()
    {
        MethodInfo getObjectiveRect = hudLayoutType.GetMethod("GetTopCenterObjectiveRect",
            BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(float), typeof(float) }, null);
        MethodInfo getToastEnvelope = hudLayoutType.GetMethod("GetTopCenterBackpackNotificationAnimationEnvelope",
            BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(float), typeof(float) }, null);

        Assert.That(getObjectiveRect, Is.Not.Null);
        Assert.That(getToastEnvelope, Is.Not.Null,
            "GameplayHudLayout must expose the full toast animation envelope, not only its resting bounds.");

        foreach (Vector2 res in new[]
        {
            new Vector2(1280f, 720f),
            new Vector2(1366f, 768f),
            new Vector2(1920f, 1080f)
        })
        {
            Rect objectiveRect = (Rect)getObjectiveRect.Invoke(null, new object[] { res.x, res.y });
            Rect toastEnvelope = (Rect)getToastEnvelope.Invoke(null, new object[] { res.x, res.y });

            Assert.That(objectiveRect.yMax, Is.LessThan(toastEnvelope.yMin),
                $"At {res.x}x{res.y}, the objective must remain above every toast animation frame.");
            Assert.That(objectiveRect.Overlaps(toastEnvelope), Is.False,
                $"At {res.x}x{res.y}, the toast entrance animation must not cover the objective.");
        }
    }

    [Test]
    public void TopCenterHud_SchoolClue_YieldsToQuestEventNotice_AtSupportedResolutions()
    {
        MethodInfo getEventRect = hudLayoutType.GetMethod("GetTopCenterQuestEventNoticeRect",
            BindingFlags.Public | BindingFlags.Static, null,
            new Type[] { typeof(float), typeof(float), typeof(float), typeof(float) }, null);
        MethodInfo getSchoolAfterEvent = hudLayoutType.GetMethod("GetTopCenterSchoolClueRect",
            BindingFlags.Public | BindingFlags.Static, null,
            new Type[] { typeof(float), typeof(float), typeof(bool), typeof(float) }, null);

        Assert.That(getEventRect, Is.Not.Null);
        Assert.That(getSchoolAfterEvent, Is.Not.Null,
            "School clue layout must accept the active quest-event bottom boundary.");

        foreach (Vector2 res in new[]
        {
            new Vector2(1280f, 720f),
            new Vector2(1366f, 768f),
            new Vector2(1920f, 1080f)
        })
        {
            Rect eventRect = (Rect)getEventRect.Invoke(null, new object[] { 680f, 86f, res.x, res.y });
            Rect schoolRect = (Rect)getSchoolAfterEvent.Invoke(null,
                new object[] { res.x, res.y, false, eventRect.yMax });

            Assert.That(eventRect.yMax, Is.LessThan(schoolRect.yMin),
                $"At {res.x}x{res.y}, school-clue progress must appear below an active quest event.");
            Assert.That(eventRect.Overlaps(schoolRect), Is.False,
                $"At {res.x}x{res.y}, quest-event notice and school-clue progress must not overlap.");
        }
    }

    [Test]
    public void TopCenterHud_WaypointGroup_YieldsToEveryReservedLane_AtSupportedResolutions()
    {
        MethodInfo getToastEnvelope = hudLayoutType.GetMethod("GetTopCenterBackpackNotificationAnimationEnvelope",
            BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(float), typeof(float) }, null);
        MethodInfo getEventRect = hudLayoutType.GetMethod("GetTopCenterQuestEventNoticeRect",
            BindingFlags.Public | BindingFlags.Static, null,
            new Type[] { typeof(float), typeof(float), typeof(float), typeof(float) }, null);
        MethodInfo getSchoolAfterEvent = hudLayoutType.GetMethod("GetTopCenterSchoolClueRect",
            BindingFlags.Public | BindingFlags.Static, null,
            new Type[] { typeof(float), typeof(float), typeof(bool), typeof(float) }, null);
        MethodInfo clampWaypointGroup = hudLayoutType.GetMethod("ClampWaypointGroupAroundTopCenter",
            BindingFlags.Public | BindingFlags.Static, null,
            new Type[] { typeof(Rect), typeof(float), typeof(float), typeof(float) }, null);

        Assert.That(getToastEnvelope, Is.Not.Null);
        Assert.That(getEventRect, Is.Not.Null);
        Assert.That(getSchoolAfterEvent, Is.Not.Null);
        Assert.That(clampWaypointGroup, Is.Not.Null,
            "Waypoints must reserve space as one arrow-and-label group, not move only their label.");

        foreach (Vector2 res in new[]
        {
            new Vector2(1280f, 720f),
            new Vector2(1366f, 768f),
            new Vector2(1920f, 1080f)
        })
        {
            Rect toastEnvelope = (Rect)getToastEnvelope.Invoke(null, new object[] { res.x, res.y });
            Rect eventRect = (Rect)getEventRect.Invoke(null, new object[] { 680f, 86f, res.x, res.y });
            Rect schoolRect = (Rect)getSchoolAfterEvent.Invoke(null,
                new object[] { res.x, res.y, true, eventRect.yMax });
            float reservedBottom = Mathf.Max(toastEnvelope.yMax, eventRect.yMax, schoolRect.yMax);
            Rect initialGroup = new Rect(res.x * 0.5f - 115f, 40f, 230f, 78f);
            Rect clampedGroup = (Rect)clampWaypointGroup.Invoke(null,
                new object[] { initialGroup, reservedBottom, res.x, res.y });

            Assert.That(clampedGroup.yMin, Is.GreaterThan(reservedBottom),
                $"At {res.x}x{res.y}, the entire waypoint group must start below every active top-center lane.");
            Assert.That(clampedGroup.Overlaps(toastEnvelope), Is.False);
            Assert.That(clampedGroup.Overlaps(eventRect), Is.False);
            Assert.That(clampedGroup.Overlaps(schoolRect), Is.False);
        }
    }

    [Test]
    public void QuestEventMilestone_WhileNotificationVisible_DefersAndDoesNotAgeOut()
    {
        GameObject host = new GameObject("Test_QuestEventDeferral_Host");
        try
        {
            Component manager = host.AddComponent(questManagerType);
            MethodInfo showEventMethod = questManagerType.GetMethod("ShowLocalQuestEvent",
                BindingFlags.NonPublic | BindingFlags.Instance);
            PropertyInfo isEventActiveProp = questManagerType.GetProperty("IsQuestEventNoticeActive",
                BindingFlags.Public | BindingFlags.Instance);

            Assert.That(showEventMethod, Is.Not.Null, "ShowLocalQuestEvent method must exist on MainQuestManager.");
            Assert.That(isEventActiveProp, Is.Not.Null, "IsQuestEventNoticeActive property must exist on MainQuestManager.");

            // Simulate Backpack Notification being visible
            Component presenter = host.AddComponent(presenterType);
            presenterType.GetField("completedEffectBLevel", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, 4);
            MethodInfo showNotif = presenterType.GetMethod("ShowUpgradeNotificationInternal",
                BindingFlags.NonPublic | BindingFlags.Instance);
            showNotif.Invoke(presenter, new object[] { 4 });

            PropertyInfo isNotifVisible = presenterType.GetProperty("IsNotificationVisible",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That((bool)isNotifVisible.GetValue(null), Is.True, "Backpack notification must be active.");

            // Trigger milestone quest event while notification is visible
            showEventMethod.Invoke(manager, new object[] { "RADIO STATION RESTORED", "Transcript saved to journal." });

            // During notification: event notice must NOT be actively drawing / aging out
            Assert.That((bool)isEventActiveProp.GetValue(manager), Is.False,
                "Quest event notice must NOT be active while backpack notification is visible (must be deferred).");

            // Dismiss notification
            MethodInfo dismissNotif = presenterType.GetMethod("DismissNotification", BindingFlags.Public | BindingFlags.Static);
            dismissNotif.Invoke(null, null);
            Assert.That((bool)isNotifVisible.GetValue(null), Is.False);

            // Once notification is dismissed, the queued milestone event must become active
            // (or pending queue processed)
            PropertyInfo pendingCountProp = questManagerType.GetProperty("PendingQuestEventCount",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(pendingCountProp, Is.Not.Null, "MainQuestManager must track pending quest event count.");
            string currentTitle = (string)questManagerType.GetProperty("CurrentQuestEventTitle",
                BindingFlags.Public | BindingFlags.Instance)?.GetValue(manager);

            Assert.That((bool)isEventActiveProp.GetValue(manager) || (int)pendingCountProp.GetValue(manager) > 0 || currentTitle == "RADIO STATION RESTORED", Is.True,
                "Milestone event must not have aged out and must be queued or active for full display.");
        }
        finally
        {
            Object.DestroyImmediate(host);
            presenterType?.GetMethod("ResetForTests", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
        }
    }
}
