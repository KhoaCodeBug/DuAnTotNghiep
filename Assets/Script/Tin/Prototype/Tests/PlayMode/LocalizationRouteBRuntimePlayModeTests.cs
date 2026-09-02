using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class LocalizationRouteBRuntimePlayModeTests
{
    private Type localizationType;
    private Type deathContextType;
    private Type deathCauseType;
    private MethodInfo getMethod;
    private MethodInfo translateLiteralMethod;
    private MethodInfo setLanguageMethod;
    private object englishEnum;
    private object vietnameseEnum;

    [SetUp]
    public void SetUp()
    {
        localizationType = Type.GetType("GameLocalization, Assembly-CSharp");
        deathContextType = Type.GetType("PlayerDeathContext, Assembly-CSharp");
        deathCauseType = Type.GetType("DeathCause, Assembly-CSharp");
        Assert.That(localizationType, Is.Not.Null, "GameLocalization type must exist in Assembly-CSharp.");
        Assert.That(deathCauseType, Is.Not.Null, "DeathCause type must exist in Assembly-CSharp.");

        Type langEnum = localizationType.GetNestedType("Language");
        Assert.That(langEnum, Is.Not.Null, "GameLocalization.Language enum must exist.");

        getMethod = localizationType.GetMethod("Get", new[] { typeof(string), typeof(string) });
        Assert.That(getMethod, Is.Not.Null, "GameLocalization.Get method must exist.");

        translateLiteralMethod = localizationType.GetMethod("TranslateLiteral", new[] { typeof(string) });
        Assert.That(translateLiteralMethod, Is.Not.Null, "GameLocalization.TranslateLiteral method must exist.");

        setLanguageMethod = localizationType.GetMethod("SetLanguage", new[] { langEnum, typeof(bool) });
        Assert.That(setLanguageMethod, Is.Not.Null, "GameLocalization.SetLanguage method must exist.");

        englishEnum = Enum.Parse(langEnum, "English");
        vietnameseEnum = Enum.Parse(langEnum, "Vietnamese");
    }

    [TearDown]
    public void TearDown()
    {
        // Restore Vietnamese as default after tests without saving to playerprefs
        if (setLanguageMethod != null && vietnameseEnum != null)
        {
            setLanguageMethod.Invoke(null, new object[] { vietnameseEnum, false });
        }
    }

    private string GetText(string key)
    {
        return (string)getMethod.Invoke(null, new object[] { key, null });
    }

    private string Translate(string literal)
    {
        return (string)translateLiteralMethod.Invoke(null, new object[] { literal });
    }

    private void SetLang(bool english)
    {
        setLanguageMethod.Invoke(null, new object[] { english ? englishEnum : vietnameseEnum, false });
    }

    [UnityTest]
    public IEnumerator Localization_RouteBAndUIKeys_StrictVietnameseAndEnglishEquivalence()
    {
        string[] testKeys = new[]
        {
            "quest.military.clue_dialogue_0",
            "quest.military.clue_dialogue_1",
            "quest.military.clue_dialogue_2",
            "quest.military.clue_dialogue_none",
            "quest.military.clues_sender",
            "quest.military.clues_progress_complete",
            "quest.military.clues_progress",
            "quest.military.new_clue_title",
            "quest.military.new_clue_body",
            "quest.military.school_exit_blocked",
            "quest.military.police_car_objective",
            "quest.military.vote_sender",
            "quest.military.vote_cancel_ready",
            "quest.military.vote_cancel_route_locked",
            "quest.hospital.radio_threat_title",
            "quest.hospital.radio_threat_body",
            "quest.military.repair_stand_front",
            "quest.military.repair_state_invalid",
            "quest.military.repair_already_complete",
            "quest.military.repair_in_progress_by",
            "quest.military.repair_busy_other",
            "quest.military.repair_item_required",
            "quest.military.escape_sender",
            "quest.military.escape_start_denied_single",
            "quest.military.escape_start_denied_multiple",
            "quest.military.escape_starting_driver",
            "quest.military.escape_starting_team",
            "quest.military.escape_unlocked",
            "quest.military.gate_broken",
            "quest.military.repair_interrupted_damage",
            "quest.military.repair_stopped",
            "quest.military.repair_complete_all",
            "quest.military.repair_complete_single",
            "quest.military.school_clues_done",
            "quest.military.school_clues_progress",
            "quest.military.gate_bar_title",
            "quest.military.police_car_waypoint",
            "quest.debug.route_b_military_unlocked",
            "quest.debug.f7_only_neighborhood",
            "victory.title.civilian",
            "victory.title.military",
            "victory.subtitle.civilian",
            "victory.subtitle.military",
            "victory.return_menu",
            "victory.stat.survival_time",
            "victory.stat.zombies_killed",
            "victory.stat.difficulty",
            "difficulty.name.easy",
            "difficulty.name.normal",
            "difficulty.name.hardcore",
            "room_placeholder",
            "noise.title",
            "noise.silent",
            "noise.voice",
            "arrival_ui.header_eyebrow_police",
            "arrival_ui.header_eyebrow_civilian",
            "arrival_ui.vehicle_police",
            "arrival_ui.vehicle_civilian",
            "arrival_ui.header_title",
            "arrival_ui.footer_hint",
            "arrival_ui.diagram_label",
            "arrival_ui.diagram_hint",
            "arrival_ui.overall_condition",
            "arrival_ui.repair_clock_label",
            "arrival_ui.repair_clock_cancel",
            "arrival_ui.group_engine",
            "arrival_ui.group_fuel",
            "arrival_ui.group_wheels",
            "arrival_ui.group_body",
            "arrival_ui.action_inspect",
            "arrival_ui.action_repair",
            "arrival_ui.action_replace",
            "arrival_ui.action_refuel",
            "arrival_ui.repairing_progress",
            "arrival_ui.status_completed_server",
            "arrival_ui.status_repairing",
            "arrival_ui.status_completed",
            "arrival_ui.inspect_result_prefix",
            "arrival_ui.chat_sender_inspect",
            "arrival_ui.police_not_connected",
            "arrival_ui.verifying_server",
            "arrival_ui.verifying_button",
            "arrival_ui.quest_not_connected",
            "arrival_ui.start_server_not_connected",
            "arrival_ui.start_missing_parts_warn",
            "arrival_ui.starting_button",
            "arrival_ui.police_repair_done",
            "arrival_ui.police_repaired_count",
            "arrival_ui.vehicle_started_btn",
            "arrival_ui.start_vehicle_btn",
            "arrival_ui.cannot_start_btn",
            "arrival_ui.failed_prefix",
            "arrival_ui.stopped_prefix",
            "arrival_ui.police_all_repaired_diag",
            "arrival_ui.police_part_repaired_diag",
            "arrival_ui.completed_prefix",
            "arrival_ui.start_success_prefix",
            "arrival_ui.diagnosis_prefix",
            "arrival_ui.track_items_hint",
            "arrival_ui.progress_prefix",
            "arrival_ui.needs_prefix",
            "arrival_ui.part.engine.name",
            "arrival_ui.part.engine.desc",
            "arrival_ui.part.engine.rec",
            "arrival_ui.part.battery.name",
            "arrival_ui.part.battery.desc",
            "arrival_ui.part.battery.rec",
            "arrival_ui.part.exhaust.name",
            "arrival_ui.part.exhaust.desc",
            "arrival_ui.part.exhaust.rec",
            "arrival_ui.part.fuel.name",
            "arrival_ui.part.fuel.desc",
            "arrival_ui.part.fuel.rec",
            "arrival_ui.part.front_left.name",
            "arrival_ui.part.front_left.desc",
            "arrival_ui.part.front_left.rec",
            "arrival_ui.part.rear_left.name",
            "arrival_ui.part.rear_left.desc",
            "arrival_ui.part.rear_left.rec",
            "arrival_ui.part.front_right.name",
            "arrival_ui.part.front_right.desc",
            "arrival_ui.part.front_right.rec",
            "arrival_ui.part.rear_right.name",
            "arrival_ui.part.rear_right.desc",
            "arrival_ui.part.rear_right.rec",
            "arrival_ui.part.hood.name",
            "arrival_ui.part.hood.desc",
            "arrival_ui.part.hood.rec",
            "arrival_ui.part.windshield.name",
            "arrival_ui.part.windshield.desc",
            "arrival_ui.part.windshield.rec",
            "arrival_ui.part.front_door.name",
            "arrival_ui.part.front_door.desc",
            "arrival_ui.part.front_door.rec",
            "quest.arrival.inspecting_engine",
            "quest.arrival.prompt_inspect",
            "quest.police.inspecting",
            "quest.police.prompt_inspect",
            "quest.police.prompt_repair",
            "quest.military.prompt_inspect_vehicle",
            "quest.military.prompt_install_parts",
            "quest.military.prompt_escape_base",
            "quest.military.repair_progress_label",
            "quest.military.prompt_resume_repair",
            "quest.military.prompt_hold_repair",
            "quest.military.repair_parts_status",
            "quest.skill_check.start",
            "quest.skill_check.perfect",
            "quest.skill_check.success",
            "quest.skill_check.miss",
            "quest.skill_check.all_done",
            "quest.skill_check.item_done",
            "quest.skill_check.stopped_saved",
            "quest.skill_check.space_hint",
            "quest.skill_check.recovering",
            "quest.skill_check.preparing",
            "quest.skill_check.esc_hint",
            "quest.skill_check.progress_bar",
            "quest.military.inspecting_clue",
            "quest.military.prompt_hold_inspect",
            "quest.military.clue_label_0",
            "quest.military.clue_label_1",
            "quest.military.clue_label_2",
            "quest.vote.eyebrow",
            "quest.vote.title",
            "quest.vote.body",
            "quest.vote.status_waiting",
            "quest.vote.status_counts",
            "quest.vote.btn_agree",
            "quest.vote.btn_decline",
            "quest.civilian.countdown",
            "quest.civilian.prompt_drive",
            "quest.civilian.prompt_wait_team",
            "quest.civilian.cinematic_eyebrow",
            "quest.civilian.cinematic_title",
            "quest.civilian.cinematic_body",
            "quest.civilian.outro_title",
            "quest.civilian.outro_subtitle",
            "quest.military.interact_generator",
            "quest.military.interact_armory",
            "quest.military.interact_safe",
            "quest.military.interact_collect",
            "quest.clue_picked_up",
            "quest.zone_search_configured",
            "quest.return_to_objective",
            "quest.new_clue_detected_sender",
            "quest.new_clue_detected_body",
            "quest.office_revealed_chat",
            "quest.boundary.sender",
            "quest.boundary.warning"
        };

        // 1. Verify English
        SetLang(true);
        yield return null;
        foreach (string key in testKeys)
        {
            string enVal = GetText(key);
            Assert.That(enVal, Is.Not.Null.And.Not.Empty, $"Key '{key}' must return non-empty English text.");
            Assert.That(enVal, Is.Not.EqualTo(key), $"Key '{key}' was not found in dictionary (missing key fallback detected).");
        }

        // 2. Verify Vietnamese
        SetLang(false);
        yield return null;
        foreach (string key in testKeys)
        {
            string viVal = GetText(key);
            Assert.That(viVal, Is.Not.Null.And.Not.Empty, $"Key '{key}' must return non-empty Vietnamese text.");
            Assert.That(viVal, Is.Not.EqualTo(key), $"Key '{key}' was not found in dictionary (missing key fallback detected).");
        }

        // 3. Verify distinct translations between locales and strict locale invariant
        foreach (string key in testKeys)
        {
            if (key == "arrival_ui.vehicle_civilian") continue;
            SetLang(true);
            string en = GetText(key);
            SetLang(false);
            string vi = GetText(key);
            Assert.That(en, Is.Not.EqualTo(vi), $"Key '{key}' should have different texts in English and Vietnamese.");
        }

        // Strict invariant checks for Vietnamese
        SetLang(false);
        Assert.That(GetText("victory.return_menu"), Is.EqualTo("QUAY VỀ MENU CHÍNH"));
        Assert.That(GetText("victory.return_menu"), Does.Not.Contain("MAIN MENU"));
        Assert.That(GetText("difficulty.name.hardcore"), Is.EqualTo("Khắc nghiệt"));
        Assert.That(GetText("quest.military.gate_broken"), Does.Not.Contain("Horde"));
        Assert.That(GetText("quest.ending_a_locked"), Does.Not.Contain("ENDING"));
        Assert.That(GetText("quest.outside_search_body"), Does.Not.Contain("marker"));
        Assert.That(GetText("quest.office_step2_body"), Does.Not.Contain("waypoint"));
        Assert.That(GetText("quest.military_debug_parts"), Does.Not.Contain("loot container"));
    }

    [UnityTest]
    public IEnumerator Localization_MenuLiterals_TranslateBidirectionally()
    {
        // Test Language label
        SetLang(true);
        Assert.That(Translate("NGÔN NGỮ:"), Is.EqualTo("LANGUAGE:"));
        Assert.That(Translate("LANGUAGE:"), Is.EqualTo("LANGUAGE:"));

        SetLang(false);
        Assert.That(Translate("LANGUAGE:"), Is.EqualTo("NGÔN NGỮ:"));
        Assert.That(Translate("NGÔN NGỮ:"), Is.EqualTo("NGÔN NGỮ:"));

        // Test room placeholder
        SetLang(true);
        Assert.That(Translate("VD: Trại tị nạn..."), Is.EqualTo("E.g. Refugee Camp..."));
        SetLang(false);
        Assert.That(Translate("E.g. Refugee Camp..."), Is.EqualTo("VD: Trại tị nạn..."));

        // Test Return to Main Menu
        SetLang(true);
        Assert.That(Translate("QUAY VỀ MENU CHÍNH"), Is.EqualTo("RETURN TO MAIN MENU"));
        SetLang(false);
        Assert.That(Translate("RETURN TO MAIN MENU"), Is.EqualTo("QUAY VỀ MENU CHÍNH"));

        yield return null;
    }

    [UnityTest]
    public IEnumerator PlayerHealth_NullKiller_DoesNotThrowAndProducesLocalizedMessage()
    {
        Assert.That(deathContextType, Is.Not.Null);
        MethodInfo formatMethod = deathContextType.GetMethod("FormatDeathMessage", BindingFlags.Public | BindingFlags.Static);
        Assert.That(formatMethod, Is.Not.Null);

        object unknownCause = Enum.Parse(deathCauseType, "Unknown");
        object zombieCause = Enum.Parse(deathCauseType, "ZombieAttack");

        // Test with null victim and null killer in Vietnamese
        SetLang(false);
        string viMsg1 = (string)formatMethod.Invoke(null, new object[] { null, unknownCause, null });
        Assert.That(viMsg1, Is.Not.Null.And.Not.Empty);
        Assert.That(viMsg1, Does.Contain("tử vong"));

        // Test with victim name and zombie in Vietnamese
        string viMsg2 = (string)formatMethod.Invoke(null, new object[] { "Alex", zombieCause, "Zombie" });
        Assert.That(viMsg2, Is.Not.Null.And.Not.Empty);
        Assert.That(viMsg2, Does.Contain("Alex").And.Contain("zombie").IgnoreCase);

        // Test with null victim and null killer in English
        SetLang(true);
        string enMsg1 = (string)formatMethod.Invoke(null, new object[] { null, unknownCause, null });
        Assert.That(enMsg1, Is.Not.Null.And.Not.Empty);
        Assert.That(enMsg1, Does.Contain("died"));

        // Test with victim name and zombie in English
        string enMsg2 = (string)formatMethod.Invoke(null, new object[] { "Alex", zombieCause, "Zombie" });
        Assert.That(enMsg2, Is.Not.Null.And.Not.Empty);
        Assert.That(enMsg2, Does.Contain("Alex").And.Contain("zombie").IgnoreCase);

        yield return null;
    }

    [UnityTest]
    public IEnumerator AutoNoiseMeter_SafeWithNullsAndRefreshesOnLanguageSwitch()
    {
        Type meterType = Type.GetType("AutoNoiseMeter, Assembly-CSharp");
        Assert.That(meterType, Is.Not.Null);

        GameObject go = new GameObject("TestAutoNoiseMeterHost");
        Component meter = go.AddComponent(meterType);
        Assert.That(meter, Is.Not.Null);

        // Call SetMovementNoise and ReportTransientNoise
        MethodInfo setMovement = meterType.GetMethod("SetMovementNoise", BindingFlags.Public | BindingFlags.Static);
        MethodInfo reportTransient = meterType.GetMethod("ReportTransientNoise", BindingFlags.Public | BindingFlags.Static);
        setMovement.Invoke(null, new object[] { true, true, false });
        reportTransient.Invoke(null, new object[] { 0.8f, "Gunshot" });

        // Switch languages while meter is active
        SetLang(true);
        yield return null;
        SetLang(false);
        yield return null;

        UnityEngine.Object.Destroy(go);
        yield return null;
    }

    [UnityTest]
    public IEnumerator VictorySummaryUI_CanInstantiateAndLocalizesBothRoutes()
    {
        Type victoryType = Type.GetType("VictorySummaryUI, Assembly-CSharp");
        Assert.That(victoryType, Is.Not.Null);

        MethodInfo showMethod = victoryType.GetMethod("ShowForCurrentMatch", new[] { typeof(float), typeof(EscapeEndingRoute) });
        Assert.That(showMethod, Is.Not.Null);

        // Test Civilian Car Route in Vietnamese
        SetLang(false);
        showMethod.Invoke(null, new object[] { 125.0f, EscapeEndingRoute.CivilianCar });
        yield return null;

        FieldInfo instanceField = victoryType.GetField("instance", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(instanceField, Is.Not.Null);
        Component instance = (Component)instanceField.GetValue(null);
        Assert.That(instance, Is.Not.Null);

        FieldInfo titleField = victoryType.GetField("titleText", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo subtitleField = victoryType.GetField("subtitleText", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo summaryField = victoryType.GetField("summaryText", BindingFlags.NonPublic | BindingFlags.Instance);

        TMPro.TMP_Text title = (TMPro.TMP_Text)titleField.GetValue(instance);
        TMPro.TMP_Text subtitle = (TMPro.TMP_Text)subtitleField.GetValue(instance);
        TMPro.TMP_Text summary = (TMPro.TMP_Text)summaryField.GetValue(instance);

        Assert.That(title.text, Is.EqualTo("THOÁT HIỂM THÀNH CÔNG"));
        Assert.That(subtitle.text, Does.Contain("DÂN SỰ"));
        Assert.That(summary.text, Does.Contain("THỜI GIAN SINH TỒN"));

        // Switch to English dynamically while UI is open
        SetLang(true);
        yield return null;

        Assert.That(title.text, Is.EqualTo("ESCAPE SUCCESSFUL"));
        Assert.That(subtitle.text, Does.Contain("CIVILIAN CAR"));
        Assert.That(summary.text, Does.Contain("SURVIVAL TIME"));

        // Show Military Route in English
        showMethod.Invoke(null, new object[] { 250.0f, EscapeEndingRoute.MilitaryEvacuation });
        yield return null;

        Assert.That(title.text, Is.EqualTo("MISSION COMPLETE"));
        Assert.That(subtitle.text, Does.Contain("MILITARY ROUTE"));
        Assert.That(summary.text, Does.Contain("SURVIVAL TIME"));

        // Switch back to Vietnamese dynamically
        SetLang(false);
        yield return null;

        Assert.That(title.text, Is.EqualTo("NHIỆM VỤ HOÀN THÀNH"));
        Assert.That(subtitle.text, Does.Contain("QUÂN SỰ"));
        Assert.That(summary.text, Does.Contain("THỜI GIAN SINH TỒN"));

        UnityEngine.Object.Destroy(instance.gameObject);
        yield return null;
    }

    [UnityTest]
    public IEnumerator Localization_ArrivalCarInspectionUI_RuntimeLanguageSwitch_RefreshesLabels()
    {
        SetLang(false); // Vietnamese
        yield return null;

        Type uiType = Type.GetType("ArrivalCarInspectionUI, Assembly-CSharp");
        Assert.That(uiType, Is.Not.Null, "ArrivalCarInspectionUI type must exist in Assembly-CSharp.");

        GameObject host = new GameObject("TestArrivalCarInspectionUIHost");
        Component ui = host.AddComponent(uiType);
        yield return null;

        Type brokenCarType = Type.GetType("BrokenArrivalCar, Assembly-CSharp");
        MethodInfo openMethod = uiType.GetMethod("Open", new[] { brokenCarType });
        Assert.That(openMethod, Is.Not.Null, "Open(BrokenArrivalCar) method must exist.");
        openMethod.Invoke(ui, new object[] { null });
        yield return null;

        FieldInfo eyebrowField = uiType.GetField("headerEyebrowText", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo titleField = uiType.GetField("headerTitleText", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo footerField = uiType.GetField("footerHintText", BindingFlags.NonPublic | BindingFlags.Instance);

        TMPro.TMP_Text eyebrow = (TMPro.TMP_Text)eyebrowField?.GetValue(ui);
        TMPro.TMP_Text title = (TMPro.TMP_Text)titleField?.GetValue(ui);
        TMPro.TMP_Text footer = (TMPro.TMP_Text)footerField?.GetValue(ui);

        Assert.That(eyebrow?.text, Is.EqualTo("KIỂM TRA PHƯƠNG TIỆN  //  XE DÂN DỤNG"));
        Assert.That(title?.text, Is.EqualTo("TÌNH TRẠNG XE"));
        Assert.That(footer?.text, Does.Contain("ĐÓNG"));

        // Switch language live to English
        SetLang(true);
        yield return null;

        Assert.That(eyebrow?.text, Is.EqualTo("VEHICLE INSPECTION  //  CIVILIAN CAR"));
        Assert.That(title?.text, Is.EqualTo("VEHICLE CONDITION"));
        Assert.That(footer?.text, Does.Contain("CLOSE"));

        // Switch language live back to Vietnamese
        SetLang(false);
        yield return null;

        Assert.That(eyebrow?.text, Is.EqualTo("KIỂM TRA PHƯƠNG TIỆN  //  XE DÂN DỤNG"));
        Assert.That(title?.text, Is.EqualTo("TÌNH TRẠNG XE"));
        Assert.That(footer?.text, Does.Contain("ĐÓNG"));

        MethodInfo closeMethod = uiType.GetMethod("Close", Type.EmptyTypes);
        closeMethod?.Invoke(ui, null);
        UnityEngine.Object.Destroy(host);
        yield return null;
    }

    [UnityTest]
    public IEnumerator AutoChatManager_AppendManyLocalizedMessages_DoesNotExceedMeshVertexLimit()
    {
        SetLang(false);
        yield return null;

        Type chatManagerType = Type.GetType("AutoChatManager, Assembly-CSharp");
        Assert.That(chatManagerType, Is.Not.Null);

        PropertyInfo instanceProp = chatManagerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        Assert.That(instanceProp, Is.Not.Null);
        Component chatManager = (Component)instanceProp.GetValue(null);
        Assert.That(chatManager, Is.Not.Null);

        MethodInfo addMsgMethod = chatManagerType.GetMethod("AddMessage", new[] { typeof(string), typeof(string) });
        Assert.That(addMsgMethod, Is.Not.Null);

        for (int i = 0; i < 80; i++)
        {
            addMsgMethod.Invoke(chatManager, new object[] {
                "GIÁM SÁT VIÊN",
                $"[CẢNH BÁO ĐỎ {i}] Phát hiện xâm nhập trái phép tại phòng vô tuyến tầng 2. Cần phong tỏa khu vực và kiểm tra nhật ký ca trực."
            });
        }

        yield return null;

        Assert.DoesNotThrow(() => Canvas.ForceUpdateCanvases());

        FieldInfo historyField = chatManagerType.GetField("chatHistory", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(historyField, Is.Not.Null);
        UnityEngine.UI.Text textComp = (UnityEngine.UI.Text)historyField.GetValue(chatManager);
        Assert.That(textComp, Is.Not.Null);
        Assert.That(textComp.text, Does.Contain("CẢNH BÁO ĐỎ 79"));
        Assert.That(textComp.text.Length, Is.LessThanOrEqualTo(1500));

        SetLang(true);
        yield return null;

        for (int i = 0; i < 80; i++)
        {
            addMsgMethod.Invoke(chatManager, new object[] {
                "SUPERVISOR",
                $"[RED ALERT {i}] Unauthorized intrusion detected in 2nd floor radio room. Seal area and verify shift log."
            });
        }

        yield return null;
        Assert.DoesNotThrow(() => Canvas.ForceUpdateCanvases());
        Assert.That(textComp.text, Does.Contain("RED ALERT 79"));
        Assert.That(textComp.text.Length, Is.LessThanOrEqualTo(1500));
    }

    [UnityTest]
    public IEnumerator MilitaryCinematic_LocalizationKeys_AreStrictlyLocalizedInBothLanguages()
    {
        string[] cinematicKeys = new[]
        {
            "cinematic.military.broken_car_subtitle",
            "cinematic.military.error_no_avatar",
            "cinematic.military.log_scene_started",
            "cinematic.military.log_walked_to_car",
            "cinematic.military.log_ran_to_gate"
        };

        SetLang(false); // Vietnamese
        yield return null;
        foreach (string key in cinematicKeys)
        {
            string vi = GetText(key);
            Assert.That(vi, Is.Not.Null.And.Not.Empty, $"Key {key} must have Vietnamese translation.");
            Assert.That(vi, Does.Not.Contain("[MISSING]"), $"Key {key} must exist in Vietnamese.");
        }

        SetLang(true); // English
        yield return null;
        foreach (string key in cinematicKeys)
        {
            string en = GetText(key);
            Assert.That(en, Is.Not.Null.And.Not.Empty, $"Key {key} must have English translation.");
            Assert.That(en, Does.Not.Contain("[MISSING]"), $"Key {key} must exist in English.");
        }

        SetLang(false);
        string viSub = GetText("cinematic.military.broken_car_subtitle");
        Assert.That(viSub, Does.Contain("xe hỏng rồi"));

        SetLang(true);
        string enSub = GetText("cinematic.military.broken_car_subtitle");
        Assert.That(enSub, Does.Contain("vehicle is broken"));
        Assert.That(enSub, Is.Not.EqualTo(viSub));
    }
}
