using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed class QuestFlowUIPrototypeTests
{
    private GameObject host;
    private QuestFlowUIPrototype prototype;

    [SetUp]
    public void SetUp()
    {
        QuestUILocalization.SetVietnamese(true);
        host = new GameObject("Quest UI Test Host");
        prototype = host.AddComponent<QuestFlowUIPrototype>();
        prototype.EnsureBuiltForTests();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(host);
    }

    [Test]
    public void PrototypeBuildsJournalCluesAndOpenMapWithoutCriticalLayoutErrors()
    {
        Assert.That(prototype.HasBuiltElement("Main Quest Header"), Is.True);
        Assert.That(prototype.HasBuiltElement("Side Quest Header"), Is.True);
        Assert.That(prototype.HasBuiltElement("Main Quest Card"), Is.True);
        Assert.That(prototype.HasBuiltElement("Side Quest Card"), Is.True);
        Assert.That(prototype.HasBuiltElement("Current Objective"), Is.True);
        Assert.That(prototype.HasBuiltElement("Current Objective Progress Bar"), Is.True);
        Assert.That(prototype.HasBuiltElement("Car Repair Requirements"), Is.True);
        Assert.That(prototype.HasBuiltElement("Tracking Button"), Is.True);
        Assert.That(prototype.HasBuiltElement("Open Map Button"), Is.True);
        Assert.That(prototype.HasBuiltElement("Map Close Hint"), Is.True);
        Assert.That(prototype.HasBuiltElement("Footer"), Is.False);
        Assert.That(prototype.HasBuiltElement("Quest Map"), Is.True);
        Assert.That(prototype.HasBuiltElement("Approximate Office Area"), Is.True);
        Assert.That(prototype.HasBuiltElement("Exact Office Marker"), Is.True);
        Assert.That(prototype.HasBuiltElement("Quest Completion Notice"), Is.True);
        Assert.That(prototype.HasBuiltElement("Journal Hint Notice"), Is.False);
        Assert.That(prototype.HasBuiltElement("New Quest Notice"), Is.False);
        Assert.That(prototype.HasBuiltElement("Clue Reading Overlay"), Is.True);
        Assert.That(prototype.HasBuiltElement("Reward Sparkle 1"), Is.True);
        Assert.That(prototype.HasBuiltElement("Tab 2"), Is.False);
        Assert.That(prototype.ValidatePrototype(), Is.Empty);
    }

    [Test]
    public void MapCloseHintIsAClickableButtonThatClosesTheMap()
    {
        prototype.SetMapOpenForPreview(true);
        GameObject closeObject = GameObject.Find("Map Close Hint");

        Assert.That(closeObject, Is.Not.Null);
        Button closeButton = closeObject.GetComponent<Button>();
        Assert.That(closeButton, Is.Not.Null);

        closeButton.onClick.Invoke();
        Assert.That(prototype.IsMapOpen, Is.False);
    }

    [Test]
    public void TrackingButtonChangesVisualStateAndBecomesCancelTracking()
    {
        prototype.SelectQuestForPreview(0);
        prototype.SetJournalOpenForPreview(true);
        GameObject buttonObject = GameObject.Find("Tracking Button");
        Image buttonImage = buttonObject.GetComponent<Image>();
        Button trackingButton = buttonObject.GetComponent<Button>();
        Color idleColor = buttonImage.color;

        Assert.That(prototype.TrackedQuestIndex, Is.EqualTo(-1));
        Assert.That(prototype.TrackingButtonText, Is.EqualTo("[V]  THEO DÕI"));

        trackingButton.onClick.Invoke();

        Assert.That(prototype.TrackedQuestIndex, Is.EqualTo(0));
        Assert.That(prototype.IsSelectedQuestTracked, Is.True);
        Assert.That(prototype.TrackingButtonText, Is.EqualTo("[V]  HỦY THEO DÕI"));
        Assert.That(buttonImage.color, Is.Not.EqualTo(idleColor));
        Assert.That(prototype.TryGetTrackedObjectiveText(out string objective), Is.True);
        Assert.That(objective, Does.Contain("0/3"));

        trackingButton.onClick.Invoke();

        Assert.That(prototype.TrackedQuestIndex, Is.EqualTo(-1));
        Assert.That(prototype.TrackingButtonText, Is.EqualTo("[V]  THEO DÕI"));
        Assert.That(prototype.TryGetTrackedObjectiveText(out _), Is.False);
    }

    [Test]
    public void ReadingAClueClosesJournalAndMapSoModalUiCannotOverlap()
    {
        prototype.SetMapOpenForPreview(true);
        prototype.ShowRouteClueReading("Hóa đơn giao hàng", "Nội dung manh mối", "SUY LUẬN: phía đông.");
        Assert.That(prototype.IsMapOpen, Is.False);
        Assert.That(prototype.IsClueReadingOpen, Is.True);
        Assert.That(prototype.IsQuestOverlayOpen, Is.True);

        prototype.CloseRouteClueReading();
        prototype.SetJournalOpenForPreview(true);
        prototype.ShowRouteClueReading("Sơ đồ tuyến xe", "Nội dung manh mối", "SUY LUẬN: tuyến 04.");

        Assert.That(prototype.IsJournalOpen, Is.False);
        Assert.That(prototype.IsMapOpen, Is.False);
        Assert.That(prototype.IsClueReadingOpen, Is.True);

        prototype.CloseRouteClueReading();
        Assert.That(prototype.IsClueReadingOpen, Is.False);
    }

    [Test]
    public void ClueReadingKeepsNarrativeAndInferenceVisible()
    {
        prototype.ShowRouteClueReading("Ghi chú", "Nội dung manh mối", "SUY LUẬN: đã xác định.");

        Assert.That(prototype.CurrentClueReadingBody, Is.EqualTo("Nội dung manh mối"));
        Assert.That(prototype.CurrentClueReadingConclusion, Does.StartWith("SUY LUẬN:"));
    }

    [Test]
    public void PlayerCanTrackEitherEscapeRouteWithoutLockingAnEnding()
    {
        prototype.SetTrackedEscapeRoute(EscapeEndingRoute.CivilianCar);
        Assert.That(prototype.TrackedEscapeRoute, Is.EqualTo(EscapeEndingRoute.CivilianCar));
        Assert.That(prototype.LockedEscapeRoute, Is.EqualTo(EscapeEndingRoute.None));

        prototype.SetTrackedEscapeRoute(EscapeEndingRoute.MilitaryEvacuation);
        Assert.That(prototype.TrackedEscapeRoute, Is.EqualTo(EscapeEndingRoute.MilitaryEvacuation));
        Assert.That(prototype.LockedEscapeRoute, Is.EqualTo(EscapeEndingRoute.None));
    }

    [Test]
    public void ClosingFirstCarInspectionUnlocksOptionalRepairQuestInJournalSnapshot()
    {
        prototype.ApplyAuthoritativeSnapshot(0, 0, false, false, false, false,
            arrivalCarRepairUnlocked: false, arrivalCarRepaired: false);
        Assert.That(prototype.GetTabTextForPreview(0), Does.Contain("01"));

        prototype.ApplyAuthoritativeSnapshot(0, 0, false, false, false, false,
            arrivalCarRepairUnlocked: true, arrivalCarRepaired: false);
        Assert.That(prototype.GetTabTextForPreview(0), Does.Contain("02"));

        prototype.SelectQuestForPreview(2);
        Assert.That(prototype.SelectedQuestIndex, Is.EqualTo(2));
        Assert.That(prototype.CurrentDetailTitle, Does.Contain("KHÔI PHỤC CHIẾC XE"));
        Assert.That(prototype.GetObjectiveStatusForPreview(0), Is.EqualTo("0 / 2"));
        Assert.That(prototype.GetObjectiveStatusForPreview(1), Is.EqualTo("0 / 2"));
        Assert.That(prototype.CurrentRewardText, Does.Contain("khám phá"));
        Assert.That(prototype.GetCarRepairRequirementStateForPreview(0), Is.EqualTo("THIẾU"));
        Assert.That(prototype.GetCarRepairRequirementStateForPreview(4), Is.EqualTo("THIẾU"));

        prototype.ApplyAuthoritativeSnapshot(0, 0, false, false, false, false,
            arrivalCarRepairUnlocked: true, arrivalCarRepaired: false,
            arrivalCarRepairMask: (int)ArrivalCarRepairState.CoreRepaired);
        prototype.SelectQuestForPreview(2);
        Assert.That(prototype.GetObjectiveStatusForPreview(0), Is.EqualTo("1 / 2"));
        Assert.That(prototype.GetObjectiveStatusForPreview(1), Is.EqualTo("0 / 2"));
        Assert.That(prototype.CurrentObjectiveProgress, Is.EqualTo("2 / 5 BẮT BUỘC"));

        prototype.ApplyAuthoritativeSnapshot(0, 0, false, false, false, false,
            arrivalCarRepairUnlocked: true, arrivalCarRepaired: true,
            arrivalCarRepairMask: (int)ArrivalCarRepairState.RequiredComplete);
        prototype.SelectTabForPreview(1);
        prototype.SelectQuestForPreview(2);
        Assert.That(prototype.SelectedQuestIndex, Is.EqualTo(2));
        Assert.That(prototype.CurrentDetailTitle, Does.Contain("KHÔI PHỤC CHIẾC XE"));
        Assert.That(prototype.GetCarRepairRequirementStateForPreview(0), Is.EqualTo("GIỮ LẠI"));
        Assert.That(prototype.GetCarRepairRequirementStateForPreview(2), Is.EqualTo("ĐÃ DÙNG"));
        Assert.That(prototype.GetCarRepairRequirementStateForPreview(3), Is.EqualTo("ĐÃ LẮP"));
    }

    [Test]
    public void OpeningHousesNeverAdvancesTheClueObjective()
    {
        prototype.RegisterHouseLootContainerOpenedForPreview("House-A");
        prototype.RegisterHouseLootContainerOpenedForPreview("House-A");
        prototype.RegisterHouseLootContainerOpenedForPreview("House-B");
        prototype.RegisterHouseLootContainerOpenedForPreview("House-C");

        Assert.That(prototype.GetObjectiveStatusForPreview(0),
            Is.EqualTo("ĐÃ TÌM THẤY  0 / 3 MANH MỐI"));
        Assert.That(prototype.HasMapFragment1, Is.False);
    }

    [Test]
    public void AuthoritativeSnapshotRestoresSharedProgressForLateJoiner()
    {
        int searchedHouseMask = (1 << 0) | (1 << 2) | (1 << 5);
        int routeClueMask = (1 << 0) | (1 << 1) | (1 << 2);

        prototype.ApplyAuthoritativeSnapshot(searchedHouseMask, routeClueMask,
            officeDiscovered: true, officeInvestigationComplete: false,
            hasMapFragment2: false, playTransitions: false);

        Assert.That(prototype.GetObjectiveStatusForPreview(0), Is.EqualTo("HOÀN THÀNH"));
        Assert.That(prototype.GetObjectiveStatusForPreview(1), Is.EqualTo("ĐÃ TÌM THẤY"));
        Assert.That(prototype.HasMapFragment1, Is.True);

        Assert.That(prototype.HasMapFragment1, Is.True);
    }

    [Test]
    public void RouteBQuestNoticeStageAndJournalAdvanceTogether()
    {
        prototype.ApplyAuthoritativeSnapshot(
            searchedHouseMask: (1 << 0) | (1 << 1) | (1 << 2),
            routeClueMask: (1 << 0) | (1 << 1) | (1 << 2),
            officeDiscovered: false, officeInvestigationComplete: false,
            hasMapFragment2: false, playTransitions: false,
            authoritativeStage: (int)PreMilitaryQuestStage.LocateOffice);
        prototype.SetJournalOpenForPreview(true);
        prototype.SelectQuestForPreview(0);

        Assert.That(GameObject.Find("Main Quest Name").GetComponent<TMPro.TMP_Text>().text,
            Is.EqualTo("Tìm Khu Điều phối trong bệnh viện"));
        Assert.That(GameObject.Find("Main Quest Meta").GetComponent<TMPro.TMP_Text>().text,
            Is.EqualTo("BƯỚC 2"));
        Assert.That(prototype.CurrentDetailTitle, Is.EqualTo("TÌM KHU ĐIỀU PHỐI TRONG BỆNH VIỆN"));
        Assert.That(prototype.CurrentObjectiveProgress, Is.EqualTo("ĐÃ XÁC ĐỊNH"));
    }

    [Test]
    public void HospitalH5SnapshotRestoresRandomKeyAndRadioReadyJournalForLateJoiner()
    {
        prototype.ApplyAuthoritativeSnapshot(
            searchedHouseMask: 7, routeClueMask: 7,
            officeDiscovered: true, officeInvestigationComplete: false,
            hasMapFragment2: false, playTransitions: false,
            authoritativeStage: (int)PreMilitaryQuestStage.FindCityMap,
            hospitalInvestigationStage: 3);
        prototype.SelectQuestForPreview(0);
        prototype.ToggleSelectedQuestTrackingForPreview();

        Assert.That(prototype.GetObjectiveStatusForPreview(2), Is.EqualTo("ĐÃ ĐÁNH DẤU VỊ TRÍ CHÌA"));
        Assert.That(prototype.TryGetTrackedObjectiveText(out string findKeyObjective), Is.True);
        Assert.That(findKeyObjective, Is.EqualTo("Tìm chìa khóa Radio tại vị trí được đánh dấu"));

        prototype.ApplyAuthoritativeSnapshot(
            searchedHouseMask: 7, routeClueMask: 7,
            officeDiscovered: true, officeInvestigationComplete: false,
            hasMapFragment2: false, playTransitions: false,
            authoritativeStage: (int)PreMilitaryQuestStage.FindCityMap,
            hospitalInvestigationStage: 4);
        Assert.That(prototype.GetObjectiveStatusForPreview(2), Is.EqualTo("ĐÃ CÓ CHÌA KHÓA CHUNG"));
        Assert.That(prototype.TryGetTrackedObjectiveText(out string unlockObjective), Is.True);
        Assert.That(unlockObjective, Is.EqualTo("Dùng chìa khóa mở Trạm liên lạc phụ trợ phía sau bệnh viện"));

        prototype.ApplyAuthoritativeSnapshot(
            searchedHouseMask: 7, routeClueMask: 7,
            officeDiscovered: true, officeInvestigationComplete: false,
            hasMapFragment2: false, playTransitions: false,
            authoritativeStage: (int)PreMilitaryQuestStage.FindCityMap,
            hospitalInvestigationStage: 5, hospitalRadioProgress: 0.35f,
            hospitalRadioCheckpointCount: 1);
        Assert.That(prototype.GetObjectiveStatusForPreview(2), Is.EqualTo("ĐANG KHÔI PHỤC  35%"));
        Assert.That(prototype.TryGetTrackedObjectiveText(out string readyObjective), Is.True);
        Assert.That(readyObjective, Is.EqualTo("Giữ E để khôi phục tín hiệu Radio"));
        Assert.That(prototype.CurrentObjectiveProgress, Is.EqualTo("CHẶNG  2/3  •  35%"));
    }

    [Test]
    public void MainTrackerUsesOnlyActuallyCollectedRouteClues()
    {
        prototype.ApplyAuthoritativeSnapshot(
            searchedHouseMask: (1 << 0) | (1 << 1) | (1 << 2),
            routeClueMask: 1 << 0,
            officeDiscovered: false, officeInvestigationComplete: false,
            hasMapFragment2: false, playTransitions: false);
        prototype.SelectQuestForPreview(0);
        prototype.ToggleSelectedQuestTrackingForPreview();

        Assert.That(prototype.TryGetTrackedObjectiveText(out string objective), Is.True);
        Assert.That(objective, Does.Contain("1/3"));
        Assert.That(objective, Does.Not.Contain("3/3"));
    }

    [Test]
    public void CurrentObjectiveProgressUsesOnlyTheCountSoItCannotOverlapTheRow()
    {
        prototype.ApplyAuthoritativeSnapshot(0, 1 << 0,
            officeDiscovered: false, officeInvestigationComplete: false,
            hasMapFragment2: false, playTransitions: false);
        prototype.SelectQuestForPreview(0);

        Assert.That(prototype.CurrentObjectiveProgress, Is.EqualTo("1 / 3"));
        Assert.That(prototype.CurrentObjectiveProgress, Does.Not.Contain("ĐÃ TÌM THẤY"));
    }

    [Test]
    public void ArrivalCarRepairRequiresCoreFuelBatteryAndExactlyOneBrokenTire()
    {
        int state = (int)ArrivalCarRepairState.CoreRepaired;
        Assert.That(ArrivalCarRepairRules.IsRequiredRepairComplete(state), Is.False);

        state |= (int)ArrivalCarRepairState.FuelAdded;
        Assert.That(ArrivalCarRepairRules.IsRequiredRepairComplete(state), Is.False);
        state |= (int)ArrivalCarRepairState.BatteryReplaced;
        Assert.That(ArrivalCarRepairRules.IsRequiredRepairComplete(state), Is.False);
        state |= (int)ArrivalCarRepairState.TireReplaced;
        Assert.That(ArrivalCarRepairRules.IsRequiredRepairComplete(state), Is.True);
        Assert.That(ArrivalCarRepairRules.TryGetAction("front_left", out ArrivalCarRepairAction tireAction), Is.True);
        Assert.That(tireAction, Is.EqualTo(ArrivalCarRepairAction.ReplaceTire));
        Assert.That(ArrivalCarRepairRules.TryGetAction("front_right", out _), Is.False,
            "Only the actually broken tire may consume the single replacement tire.");
        Assert.That(ArrivalCarRepairRules.ConsumesInstalledPart(ArrivalCarRepairAction.RepairCore), Is.False);
        Assert.That(ArrivalCarRepairRules.ConsumesInstalledPart(ArrivalCarRepairAction.AddFuel), Is.True);
    }

    [Test]
    public void CompletedCluesCanQueueUnlockForTheNextManualMapOpen()
    {
        prototype.QueueMapUnlockReveal();

        Assert.That(prototype.HasPendingMapUnlockReveal, Is.True);
        Assert.That(prototype.HasBuiltElement("Map Unlock Reveal"), Is.True);
    }

    [Test]
    public void CompletedAuthoritativeCluesPreserveExactMapStateWithoutDependingOnRpcOrder()
    {
        prototype.ApplyAuthoritativeSnapshot(
            searchedHouseMask: (1 << 0) | (1 << 1) | (1 << 2),
            routeClueMask: (1 << 0) | (1 << 1) | (1 << 2),
            officeDiscovered: false, officeInvestigationComplete: false,
            hasMapFragment2: false, playTransitions: false);

        prototype.SetMapOpenForPreview(true);

        Assert.That(prototype.HasMapFragment1, Is.True);
        Assert.That(prototype.CurrentMapKnowledgeLabel, Is.EqualTo("MẢNH 1  •  VỊ TRÍ CHÍNH XÁC"));
        Assert.That(prototype.HasBuiltElement("Map Unlock Reveal"), Is.True);
    }

    [Test]
    public void CompletingClueSearchHidesBorderAndKeepsUnrevealedMapFogged()
    {
        prototype.ConfigureSearchZone(new Vector2(0.2f, 0.2f), new Vector2(0.45f, 0.45f), 6);
        prototype.ApplyAuthoritativeSnapshot(
            searchedHouseMask: 0,
            routeClueMask: (1 << 0) | (1 << 1) | (1 << 2),
            officeDiscovered: false, officeInvestigationComplete: false,
            hasMapFragment2: false, playTransitions: false);
        prototype.SetMapOpenForPreview(true);

        GameObject searchBorder = GameObject.Find("Quest Search Zone");
        GameObject fallbackFog = GameObject.Find("Fog North");

        Assert.That(searchBorder, Is.Null,
            "The completed 3-clue objective should remove its amber search border.");
        Assert.That(fallbackFog, Is.Not.Null,
            "Completing clues must not reveal the entire city map.");
    }

    [Test]
    public void ThreeOptionalRouteCluesGrantMapFragmentOneAndExactOfficeMarker()
    {
        prototype.RegisterRouteClueForPreview("Invoice");
        prototype.RegisterRouteClueForPreview("Invoice");
        prototype.RegisterRouteClueForPreview("BusRoute");
        prototype.RegisterRouteClueForPreview("AddressNote");

        Assert.That(prototype.HasMapFragment1, Is.True);

        prototype.SetMapOpenForPreview(true);
        Assert.That(prototype.CurrentMapKnowledgeLabel, Is.EqualTo("MẢNH 1  •  VỊ TRÍ CHÍNH XÁC"));
    }

    [Test]
    public void DiscoveringOfficeWithoutSideQuestKeepsMainRouteValidAndResolvesSideQuest()
    {
        prototype.RegisterHouseLootContainerOpenedForPreview("House-A");
        prototype.RegisterHouseLootContainerOpenedForPreview("House-B");
        prototype.RegisterHouseLootContainerOpenedForPreview("House-C");
        prototype.RegisterOfficeDiscoveredForPreview();

        Assert.That(prototype.GetObjectiveStatusForPreview(1), Is.EqualTo("ĐÃ TÌM THẤY"));

        Assert.That(prototype.HasMapFragment1, Is.False,
            "Finding the office directly must not pretend Map Fragment 1 was awarded.");
    }

    [Test]
    public void OfficeInvestigationAndMapFragmentTwoRemainSeparateEvents()
    {
        prototype.RegisterOfficeDiscoveredForPreview();
        Assert.That(prototype.GetObjectiveStatusForPreview(2), Is.EqualTo("TÌM MẢNH 2"));

        prototype.RegisterOfficeMapCabinetOpenedForPreview();
        Assert.That(prototype.GetObjectiveStatusForPreview(2), Is.EqualTo("ĐÃ KIỂM TRA"));
        Assert.That(prototype.IsMainQuestComplete, Is.False);

        prototype.RegisterMapFragment2AddedToInventoryForPreview();
        Assert.That(prototype.IsMainQuestComplete, Is.False,
            "The military map opens the second half of Route B; it is not the ending.");
        prototype.SelectQuestForPreview(0);
        Assert.That(prototype.GetObjectiveStatusForPreview(2), Is.EqualTo("ĐÃ CÓ MẢNH 2"));
        prototype.ApplyMilitarySnapshot((int)RouteBMilitaryPresentationPhase.NotReached,
            false, false, 0f, 100f, 100f, false);
        prototype.SelectTabForPreview(0);
        prototype.SelectQuestForPreview(0);
        Assert.That(prototype.CurrentDetailTitle, Is.EqualTo("KHÁM PHÁ TRƯỜNG HỌC BỎ HOANG"));
    }

    [Test]
    public void CompletedAndFailedTabsShowTheirOwnEmptyStates()
    {
        prototype.SelectTabForPreview(1);
        Assert.That(prototype.SelectedTabIndex, Is.EqualTo(1));
        Assert.That(prototype.IsEmptyStateVisible, Is.True);
        Assert.That(prototype.IsQuestListDividerVisible, Is.False,
            "The list divider must not cut through the completed-tab empty state.");

        prototype.SelectTabForPreview(2);
        Assert.That(prototype.SelectedTabIndex, Is.EqualTo(2));
        Assert.That(prototype.IsEmptyStateVisible, Is.True);

        prototype.SelectTabForPreview(0);
        Assert.That(prototype.IsEmptyStateVisible, Is.False);
        Assert.That(prototype.IsQuestListDividerVisible, Is.True);
    }

    [Test]
    public void JournalAndMapAreMutuallyExclusiveInPreviewApi()
    {
        prototype.SetJournalOpenForPreview(true);
        Assert.That(prototype.IsJournalOpen, Is.True);

        prototype.SetMapOpenForPreview(true);
        Assert.That(prototype.IsMapOpen, Is.True);
        Assert.That(prototype.IsJournalOpen, Is.False);

        prototype.SetMapOpenForPreview(false);
        Assert.That(prototype.IsMapOpen, Is.False);
    }

    [Test]
    public void ProgressStopsAtRequiredCountsAndNeverShowsSevenOfThree()
    {
        for (int i = 0; i < 7; i++)
            prototype.RegisterRouteClueForPreview("Clue-" + i);

        prototype.SetMapOpenForPreview(true);
        Assert.That(prototype.CurrentMapClueSummary, Does.Contain("3/3"));
        Assert.That(prototype.CurrentMapClueSummary, Does.Not.Contain("7/3"));
    }

    [Test]
    public void CompletedQuestsMoveFromActiveTabToCompletedTab()
    {
        prototype.RegisterRouteClueForPreview("Invoice");
        prototype.RegisterRouteClueForPreview("BusRoute");
        prototype.RegisterRouteClueForPreview("AddressNote");

        Assert.That(prototype.GetTabTextForPreview(0), Does.EndWith("01"));
        Assert.That(prototype.GetTabTextForPreview(1), Does.EndWith("00"));

        prototype.RegisterMapFragment2AddedToInventoryForPreview();
        Assert.That(prototype.GetTabTextForPreview(0), Does.EndWith("01"));
        Assert.That(prototype.GetTabTextForPreview(1), Does.EndWith("00"));

        prototype.ApplyMilitarySnapshot((int)RouteBMilitaryPresentationPhase.Escaped,
            true, true, 100f, 100f, 150f, false);
        Assert.That(prototype.GetTabTextForPreview(0), Does.EndWith("00"));
        Assert.That(prototype.GetTabTextForPreview(1), Does.EndWith("01"));

        prototype.SelectTabForPreview(1);
        Assert.That(prototype.IsEmptyStateVisible, Is.False);
    }

    [Test]
    public void JournalExplainsTheRewardForEachRouteBPhase()
    {
        prototype.SelectQuestForPreview(0);
        Assert.That(prototype.CurrentRewardLabel, Is.EqualTo("PHẦN THƯỞNG NHIỆM VỤ"));
        Assert.That(prototype.CurrentRewardText, Is.EqualTo("3 hồ sơ ghép thành Mảnh bản đồ 1"));

        prototype.RegisterMapFragment2AddedToInventoryForPreview();
        prototype.ApplyMilitarySnapshot((int)RouteBMilitaryPresentationPhase.NotReached,
            false, false, 0f, 100f, 100f, false);
        prototype.SelectTabForPreview(0);
        prototype.SelectQuestForPreview(0);
        Assert.That(prototype.CurrentRewardLabel, Is.EqualTo("PHẦN THƯỞNG  •  NHẤN ĐỂ ĐỌC TRANSCRIPT"));
        Assert.That(prototype.CurrentRewardText, Is.EqualTo("Quyền tiếp cận căn cứ + đánh giá xe sơ tán"));

        prototype.ApplyMilitarySnapshot((int)RouteBMilitaryPresentationPhase.Escaped,
            true, true, 100f, 100f, 150f, false);
        prototype.SelectTabForPreview(1);
        prototype.SelectQuestForPreview(0);
        Assert.That(prototype.CurrentRewardLabel, Is.EqualTo("KẾT QUẢ THOÁT HIỂM"));
        Assert.That(prototype.CurrentRewardText, Is.EqualTo("Sơ tán quân sự — hoàn thành Tuyến B"));
    }

    [Test]
    public void RouteBJournalTracksMilitaryBasePhasesUntilExtraction()
    {
        prototype.RegisterMapFragment2AddedToInventoryForPreview();
        prototype.ApplyMilitarySnapshot((int)RouteBMilitaryPresentationPhase.NotReached,
            false, false, 0f, 100f, 100f, false);
        prototype.SelectQuestForPreview(0);
        Assert.That(prototype.CurrentDetailTitle, Is.EqualTo("KHÁM PHÁ TRƯỜNG HỌC BỎ HOANG"));
        Assert.That(prototype.IsMainQuestComplete, Is.False);

        prototype.ApplyMilitarySnapshot((int)RouteBMilitaryPresentationPhase.Investigating,
            false, false, 0f, 100f, 100f, true);
        Assert.That(prototype.CurrentDetailTitle, Is.EqualTo("KIỂM TRA XE SƠ TÁN"));

        prototype.ApplyMilitarySnapshot((int)RouteBMilitaryPresentationPhase.SiegeAndRepair,
            true, true, 64f, 90f, 150f, true);
        Assert.That(prototype.CurrentDetailTitle, Is.EqualTo("PHÒNG THỦ VÀ KHÔI PHỤC XE THOÁT HIỂM"));
        Assert.That(prototype.CurrentObjectiveProgress, Is.EqualTo("64%"));

        prototype.ApplyMilitarySnapshot((int)RouteBMilitaryPresentationPhase.ReadyToEscape,
            true, true, 100f, 70f, 150f, true);
        Assert.That(prototype.CurrentDetailTitle, Is.EqualTo("XE SƠ TÁN ĐÃ SẴN SÀNG"));

        prototype.ApplyMilitarySnapshot((int)RouteBMilitaryPresentationPhase.Escaped,
            true, true, 100f, 70f, 150f, true);
        Assert.That(prototype.IsMainQuestComplete, Is.True);
        Assert.That(prototype.GetTabTextForPreview(1), Does.EndWith("01"));
    }

    [Test]
    public void MainSceneConfigurationBuildsLiveMapInsteadOfOnlySchematicGeometry()
    {
        GameObject cameraObject = new GameObject("Map Camera Template");
        Camera cameraTemplate = cameraObject.AddComponent<Camera>();
        cameraTemplate.orthographic = true;
        cameraTemplate.orthographicSize = 12f;
        GameObject office = new GameObject("Office Target");
        GameObject player = new GameObject("Player Target");

        try
        {
            prototype.ConfigureWorldMap(cameraTemplate, office.transform, player.transform);
            prototype.SetMapOpenForPreview(true);

            Assert.That(prototype.HasBuiltElement("Live Main Scene Map"), Is.True);
            Assert.That(prototype.HasBuiltElement("Live Office Marker"), Is.True);
            Assert.That(GameObject.Find("Quest World Map Camera"), Is.Not.Null);
        }
        finally
        {
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(office);
            Object.DestroyImmediate(player);
        }
    }

    [Test]
    public void MainSceneCoordinatesBuildAnIllustratedMapWithoutCameraProjection()
    {
        GameObject office = new GameObject("Office Target");
        GameObject player = new GameObject("Player Target");
        Vector3[] houses =
        {
            new Vector3(-20f, -10f),
            new Vector3(12f, 8f),
            new Vector3(40f, 25f)
        };

        try
        {
            prototype.ConfigureSceneLayoutMap(houses, office.transform, player.transform);
            prototype.SetMapOpenForPreview(true);

            Assert.That(prototype.HasBuiltElement("Main Scene Illustrated Map"), Is.True);
            Assert.That(prototype.HasBuiltElement("Scene House 1"), Is.True);
            Assert.That(prototype.HasBuiltElement("Scene House 3"), Is.True);
            Assert.That(prototype.HasBuiltElement("Live Main Scene Map"), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(office);
            Object.DestroyImmediate(player);
        }
    }

    [Test]
    public void CellAccurateRasterMapUsesProvidedTextureAndDoesNotDrawAFakeRoute()
    {
        Texture2D texture = new Texture2D(96, 128, TextureFormat.RGBA32, false);
        texture.name = "Test Main Map Raster";

        try
        {
            prototype.ConfigureRasterMap(texture, new Vector2(0.75f, 0.7f), new Vector2(0.25f, 0.2f));
            prototype.SetMapOpenForPreview(true);

            Assert.That(prototype.HasBuiltElement("Cell Accurate Main Map"), Is.True);
            Assert.That(prototype.HasBuiltElement("Zomboid Map Raster"), Is.True);
            Assert.That(prototype.HasBuiltElement("Map Rotation State"), Is.True);
            Assert.That(prototype.HasBuiltElement("Live Main Scene Map"), Is.False);

            GameObject rasterObject = GameObject.Find("Zomboid Map Raster");
            Assert.That(rasterObject.GetComponent<RawImage>().texture, Is.SameAs(texture));

            RectTransform mapArt = GameObject.Find("Cell Accurate Main Map").GetComponent<RectTransform>();
            Vector2 landscapeSize = mapArt.rect.size;
            Assert.That(prototype.CurrentMapRotationQuarterTurns, Is.EqualTo(1));
            prototype.RotateRasterMapForPreview(1);
            Assert.That(prototype.CurrentMapRotationQuarterTurns, Is.EqualTo(2));
            Assert.That(mapArt.rect.width, Is.LessThan(landscapeSize.x));
            Assert.That(Quaternion.Angle(mapArt.localRotation, Quaternion.Euler(0f, 0f, -180f)),
                Is.LessThan(0.01f));

            prototype.RotateRasterMapForPreview(3);
            Assert.That(prototype.CurrentMapRotationQuarterTurns, Is.EqualTo(1));

            prototype.RegisterRouteClueForPreview("Raster Invoice");
            prototype.RegisterRouteClueForPreview("Raster Route");
            prototype.RegisterRouteClueForPreview("Raster Address");
            GameObject exactLocation = GameObject.Find("Exact Location Revealed");
            Assert.That(exactLocation, Is.Not.Null);
            Assert.That(exactLocation.GetComponent<Image>(), Is.Null,
                "The exact clue must reveal a marker, not invent a straight road through the city.");
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void SearchZoneIsVisibleAndReportsOnlyTheBoundedNeighborhood()
    {
        Texture2D texture = new Texture2D(96, 128, TextureFormat.RGBA32, false);
        try
        {
            prototype.ConfigureRasterMap(texture, new Vector2(0.8f, 0.75f), new Vector2(0.25f, 0.3f));
            prototype.ConfigureSearchZone(new Vector2(0.12f, 0.2f), new Vector2(0.38f, 0.47f), 6);
            prototype.SetMapOpenForPreview(true);

            Assert.That(prototype.HasBuiltElement("Quest Search Zone"), Is.True);
            Assert.That(prototype.HasBuiltElement("Quest Search Zone Label"), Is.False);
            Assert.That(GameObject.Find("Quest Search Zone Label Plate"), Is.Null,
                "The clue-zone text and its dark plate were intentionally removed from the map.");
            Assert.That(prototype.HasBuiltElement("Restricted Fog West"), Is.True);
            Assert.That(prototype.HasBuiltElement("Restricted Fog East"), Is.True);
            Assert.That(prototype.HasBuiltElement("Restricted Fog South"), Is.True);
            Assert.That(prototype.HasBuiltElement("Restricted Fog North"), Is.True);
            Assert.That(prototype.CurrentMapSearchZoneHouseCount, Is.EqualTo(6));
            Assert.That(prototype.CurrentMapClueSummary, Does.Contain("Các ngôi nhà xung quanh"));
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void ThreeCluesRevealTheConfiguredOfficeMarker()
    {
        Texture2D texture = new Texture2D(100, 100, TextureFormat.RGBA32, false);
        try
        {
            prototype.ConfigureRasterMap(texture, new Vector2(0.7f, 0.7f), new Vector2(0.2f, 0.2f));
            prototype.ConfigureOfficeSearchArea(new Vector2(0.55f, 0.55f), new Vector2(0.85f, 0.85f));
            prototype.RegisterRouteClueForPreview("Invoice");
            prototype.RegisterRouteClueForPreview("BusRoute");
            prototype.RegisterRouteClueForPreview("AddressNote");
            prototype.SetMapOpenForPreview(true);

            Assert.That(GameObject.Find("Exact Office Marker"), Is.Not.Null);
            Assert.That(prototype.CurrentMapKnowledgeLabel, Is.EqualTo("MẢNH 1  •  VỊ TRÍ CHÍNH XÁC"));
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void OfficeRevealCutsASecondIndependentOpeningInRestrictedFog()
    {
        Texture2D texture = new Texture2D(100, 100, TextureFormat.RGBA32, false);
        try
        {
            prototype.ConfigureRasterMap(texture, new Vector2(0.72f, 0.62f), new Vector2(0.2f, 0.2f));
            prototype.ConfigureSearchZone(new Vector2(0.1f, 0.1f), new Vector2(0.42f, 0.55f), 6);
            prototype.ConfigureOfficeSearchArea(new Vector2(0.58f, 0.25f), new Vector2(0.88f, 0.75f));
            prototype.RegisterRouteClueForPreview("Invoice");
            prototype.RegisterRouteClueForPreview("BusRoute");
            prototype.RegisterRouteClueForPreview("AddressNote");
            prototype.SetMapOpenForPreview(true);

            Assert.That(GameObject.Find("Restricted Fog Segment 5"), Is.Not.Null,
                "Two independent openings need more than the original four union-rectangle fog strips.");
            Assert.That(GameObject.Find("Restricted Fog West").GetComponent<Image>().color.a,
                Is.EqualTo(1f),
                "Unknown map districts must be opaque enough to hide undiscovered streets.");
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void MapFragmentTwoReplacesHospitalTargetWithMilitaryBaseMarker()
    {
        Texture2D texture = new Texture2D(128, 96, TextureFormat.RGBA32, false);
        try
        {
            prototype.ConfigureRasterMap(texture, new Vector2(0.42f, 0.55f), new Vector2(0.2f, 0.2f));
            prototype.ConfigureMilitaryDestination(new Vector2(0.86f, 0.78f));
            prototype.ConfigureSearchZone(new Vector2(0.1f, 0.1f), new Vector2(0.35f, 0.45f), 6);
            prototype.RegisterRouteClueForPreview("Invoice");
            prototype.RegisterRouteClueForPreview("Route");
            prototype.RegisterRouteClueForPreview("Address");
            prototype.RegisterOfficeDiscoveredForPreview();
            prototype.RegisterOfficeMapCabinetOpenedForPreview();

            Assert.That(prototype.IsMapMilitaryDestinationVisible, Is.False);
            prototype.RegisterMapFragment2AddedToInventoryForPreview();
            prototype.SetMapOpenForPreview(true);

            Assert.That(prototype.IsMapMilitaryDestinationVisible, Is.True);
            Assert.That(prototype.IsMapOfficeDestinationVisible, Is.False,
                "Fragment 2 replaces the completed hospital target instead of leaving two competing markers.");
            Assert.That(GameObject.Find("Military Base Marker"), Is.Not.Null);
            Assert.That(prototype.CurrentMapKnowledgeLabel, Is.EqualTo("MẢNH 2  •  TUYẾN QUÂN SỰ"));
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void CivilianCarUnlockRevealsCityWithoutLeakingMilitaryDestination()
    {
        Texture2D texture = new Texture2D(128, 96, TextureFormat.RGBA32, false);
        try
        {
            prototype.ConfigureRasterMap(texture, new Vector2(0.42f, 0.55f), new Vector2(0.2f, 0.2f));
            prototype.ConfigureMilitaryDestination(new Vector2(0.86f, 0.78f));
            prototype.ConfigureSearchZone(new Vector2(0.1f, 0.1f), new Vector2(0.35f, 0.45f), 6);
            prototype.ConfigureCivilianEscapeRoute(new Vector2(0.75f, 0.4f), new Vector2(0.98f, 0.4f),
                CivilianEscapePresentationStage.ExploringExits);
            prototype.SetCivilianCityMapUnlocked(true);
            prototype.SetMapOpenForPreview(true);

            Assert.That(prototype.ActiveMapRestrictedFogCount, Is.Zero,
                "Route A must reveal city terrain after the repaired car starts.");
            Assert.That(prototype.IsMapMilitaryDestinationVisible, Is.False,
                "Route A terrain access must not reveal Route B's military coordinates.");
            Assert.That(GameObject.Find("Civilian Regroup Marker"), Is.Not.Null);
            Assert.That(GameObject.Find("Civilian City Exit Marker"), Is.Null);

            prototype.ConfigureCivilianEscapeRoute(new Vector2(0.75f, 0.4f), new Vector2(0.98f, 0.4f),
                CivilianEscapePresentationStage.EscapeRun);
            Assert.That(GameObject.Find("Civilian Regroup Marker"), Is.Null);
            Assert.That(GameObject.Find("Civilian City Exit Marker"), Is.Not.Null);
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void MilitaryMapRewardUsesTheSamePhysicalFragmentArtAsFragmentOne()
    {
        Texture2D texture = new Texture2D(256, 128, TextureFormat.RGBA32, false);
        texture.name = "Real Quest Raster Reward";
        try
        {
            prototype.ConfigureRasterMap(texture, new Vector2(0.4f, 0.5f), new Vector2(0.2f, 0.2f));
            prototype.PlayMilitaryMapRewardAfterDialogue();

            Texture2D fragmentArt = Resources.Load<Texture2D>("QuestUI/MapFragmentReward");
            Assert.That(fragmentArt, Is.Not.Null);
            Assert.That(prototype.CurrentCompletionRewardTexture, Is.SameAs(fragmentArt));
            Assert.That(prototype.CurrentCompletionRewardTexture, Is.Not.SameAs(texture),
                "The full map raster belongs to the map screen, not the inventory reward card.");
            Assert.That(prototype.HasBuiltElement("Map Fragment Reward Art"), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void MilitaryRepairRequiresAllThreeDistinctParts()
    {
        Assert.That(MilitaryQuestRules.HasAllParts(true, true, true), Is.True);
        Assert.That(MilitaryQuestRules.HasAllParts(true, true, false), Is.False);
        Assert.That(MilitaryQuestRules.HasAllParts(false, true, true), Is.False);
    }

    [Test]
    public void MilitaryHordeScalesOpeningBatchAndNearbyTargetForSoloAndMultiplayer()
    {
        Assert.That(MilitaryStoryFlowRules.GetBatchSize(1), Is.EqualTo(8));
        Assert.That(MilitaryStoryFlowRules.GetNearbyTarget(1), Is.EqualTo(24));
        Assert.That(MilitaryStoryFlowRules.GetBatchSize(2), Is.EqualTo(16));
        Assert.That(MilitaryStoryFlowRules.GetNearbyTarget(2), Is.EqualTo(50));
        Assert.That(MilitaryStoryFlowRules.ShouldSpawnBatch(2, 49), Is.True);
        Assert.That(MilitaryStoryFlowRules.ShouldSpawnBatch(2, 50), Is.False);
    }

    [Test]
    public void MilitaryRepairOnlyStopsForDirectZombieAttack()
    {
        Assert.That(MilitaryStoryFlowRules.ShouldInterruptVehicleRepair(true), Is.True);
        Assert.That(MilitaryStoryFlowRules.ShouldInterruptVehicleRepair(false), Is.False,
            "Hunger, thirst, bleeding and other damage-over-time must not interrupt the finale repair.");
    }

    [Test]
    public void MilitarySchoolRequiresExactlyThreeQuestStateClues()
    {
        Assert.That(MilitaryStoryFlowRules.RequiredSchoolClues, Is.EqualTo(3));
        Assert.That(MilitaryStoryFlowRules.HasAllSchoolClues(0b011), Is.False);
        Assert.That(MilitaryStoryFlowRules.HasAllSchoolClues(0b111), Is.True);
    }

    [Test]
    public void MilitaryGateDamageNeverCreatesNegativeHealth()
    {
        Assert.That(MilitaryQuestRules.BaseGateHealth, Is.EqualTo(5000f));
        Assert.That(MilitaryQuestRules.ApplyGateDamage(25f, 40f), Is.Zero);
        Assert.That(MilitaryQuestRules.ApplyGateDamage(25f, -10f), Is.EqualTo(25f));
    }

    [Test]
    public void MilitaryRepairProgressIsTimeBasedAndClampedAtCompletion()
    {
        float halfway = MilitaryQuestRules.ApplyRepairProgress(0f, 6f, 12f);
        Assert.That(halfway, Is.EqualTo(50f).Within(0.001f));
        Assert.That(MilitaryQuestRules.ApplyRepairProgress(halfway, 30f, 12f), Is.EqualTo(100f));
    }

    [Test]
    public void EscapeEndingLocksOnlyOnceAtPointOfNoReturn()
    {
        Assert.That(EscapeEndingRules.CanLock(EscapeEndingRoute.None, EscapeEndingRoute.CivilianCar), Is.True);
        Assert.That(EscapeEndingRules.CanLock(EscapeEndingRoute.None, EscapeEndingRoute.MilitaryEvacuation), Is.True);
        Assert.That(EscapeEndingRules.CanLock(EscapeEndingRoute.CivilianCar, EscapeEndingRoute.CivilianCar), Is.True);
        Assert.That(EscapeEndingRules.CanLock(EscapeEndingRoute.CivilianCar, EscapeEndingRoute.MilitaryEvacuation), Is.False);
        Assert.That(EscapeEndingRules.CanLock(EscapeEndingRoute.MilitaryEvacuation, EscapeEndingRoute.CivilianCar), Is.False);
    }

    [Test]
    public void RouteBAudioSourceCoversTheFullStoryWithUniqueRecordingPaths()
    {
        Assert.That(RouteBAudioContent.All.Count, Is.EqualTo(15));
        HashSet<RouteBAudioCueId> ids = new HashSet<RouteBAudioCueId>();
        HashSet<string> paths = new HashSet<string>();
        for (int i = 0; i < RouteBAudioContent.All.Count; i++)
        {
            RouteBAudioCue cue = RouteBAudioContent.All[i];
            Assert.That(ids.Add(cue.Id), Is.True, "Route B cue IDs must be unique.");
            Assert.That(paths.Add(cue.AudioResourcePath), Is.True, "Recording resource paths must be unique.");
            Assert.That(cue.AudioResourcePath, Does.StartWith("Sound/Story/RouteB/"));
            AudioClip clip = Resources.Load<AudioClip>(cue.AudioResourcePath);
            bool canonicalHospitalFallback = cue.Id == RouteBAudioCueId.ThirdCoordinationDocument ||
                                             cue.Id == RouteBAudioCueId.OfficeLocated ||
                                             cue.Id == RouteBAudioCueId.DispatchDeskLog ||
                                             cue.Id == RouteBAudioCueId.OfficeRadioRecording;
            if (!canonicalHospitalFallback)
                Assert.That(clip, Is.Not.Null,
                    $"Missing Route B recording at Resources/{cue.AudioResourcePath}.");
            Assert.That(cue.Vietnamese, Is.Not.Empty);
            Assert.That(cue.English, Is.Not.Empty);
            Assert.That(cue.FallbackDuration, Is.GreaterThan(0f));
        }
    }

    [Test]
    public void RouteBOpeningIntroducesBothRoutesBeforeTrackingChoice()
    {
        Assert.That(RouteBAudioContent.OpeningSequence.Count, Is.EqualTo(2));
        Assert.That(RouteBAudioContent.OpeningSequence[0].Id,
            Is.EqualTo(RouteBAudioCueId.OpeningEmergencyBroadcast));
        Assert.That(RouteBAudioContent.OpeningSequence[1].Id,
            Is.EqualTo(RouteBAudioCueId.PlayerRouteReaction));
        Assert.That(RouteBAudioContent.OpeningSequence[1].Vietnamese, Does.Contain("cả hai hướng"));
    }

    [Test]
    public void HospitalRecordingPreservesTheCanonicalStoryAndUncertainty()
    {
        Assert.That(RouteBAudioContent.HospitalRecordingSequence.Count, Is.EqualTo(3));
        Assert.That(RouteBAudioContent.HospitalTranscriptVietnamese, Does.Contain("hai mươi sáu dân thường"));
        Assert.That(RouteBAudioContent.HospitalTranscriptVietnamese, Does.Contain("không quay lại"));
        Assert.That(RouteBAudioContent.HospitalTranscriptVietnamese, Does.Contain("Tôi không biết ở đó còn ai nữa"));
        Assert.That(RouteBAudioContent.HospitalTranscriptVietnamese, Does.Contain("BRAVO–BẮC"));
        Assert.That(RouteBAudioContent.Get(RouteBAudioCueId.MilitaryRouteRevealed).AudioResourcePath,
            Does.EndWith("09_MilitaryRouteRevealed_Clean"));

        prototype.RegisterMapFragment2AddedToInventoryForPreview();
        prototype.ShowHospitalRadioTranscript();
        Assert.That(prototype.IsClueReadingOpen, Is.True);
        Assert.That(prototype.CurrentClueReadingBody, Does.Contain("BRAVO–BẮC"));
    }

    [Test]
    public void JournalHeadersAndCurrentRouteContentRefreshWhenLanguageChanges()
    {
        prototype.SetJournalOpenForPreview(true);
        prototype.SelectQuestForPreview(0);
        Assert.That(prototype.CurrentDetailTitle, Is.EqualTo("THU THẬP HỒ SƠ SƠ TÁN"));

        QuestUILocalization.SetVietnamese(false);

        Assert.That(prototype.CurrentDetailTitle, Is.EqualTo("RECOVER THE EVACUATION RECORDS"));
        Assert.That(GameObject.Find("Journal Title").GetComponent<TMPro.TMP_Text>().text,
            Is.EqualTo("MISSION JOURNAL"));
        Assert.That(prototype.TrackingButtonText, Does.Contain("TRACK"));
        QuestUILocalization.SetVietnamese(true);
    }

}
