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
        Assert.That(prototype.HasBuiltElement("Map Fragment Slot 1"), Is.True);
        Assert.That(prototype.HasBuiltElement("Map Fragment Slot 2"), Is.True);
        Assert.That(prototype.HasBuiltElement("Side Objective Segment 1"), Is.True);
        Assert.That(prototype.HasBuiltElement("Quest Map"), Is.True);
        Assert.That(prototype.HasBuiltElement("Approximate Office Area"), Is.True);
        Assert.That(prototype.HasBuiltElement("Exact Office Marker"), Is.True);
        Assert.That(prototype.HasBuiltElement("Quest Completion Notice"), Is.True);
        Assert.That(prototype.HasBuiltElement("Journal Hint Notice"), Is.True);
        Assert.That(prototype.HasBuiltElement("New Quest Notice"), Is.False);
        Assert.That(prototype.HasBuiltElement("Clue Reading Overlay"), Is.True);
        Assert.That(prototype.HasBuiltElement("Reward Sparkle 1"), Is.True);
        Assert.That(prototype.HasBuiltElement("Tab 2"), Is.False);
        Assert.That(prototype.ValidatePrototype(), Is.Empty);
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
    public void SelectingSideQuestShowsRouteCluesAndMapFragmentOneReward()
    {
        prototype.SelectQuestForPreview(1);

        Assert.That(prototype.SelectedQuestIndex, Is.EqualTo(1));
        Assert.That(prototype.CurrentDetailTitle, Does.Contain("GHÉP LẠI"));
        Assert.That(prototype.CurrentContextPanelTitle, Is.EqualTo("DẤU VẾT ĐÃ THU THẬP"));
        Assert.That(prototype.GetObjectiveStatusForPreview(0), Is.EqualTo("0 / 3"));
        Assert.That(prototype.GetObjectiveStatusForPreview(1), Is.EqualTo("ĐANG KHÓA"));

        prototype.SelectQuestForPreview(0);
        Assert.That(prototype.CurrentDetailTitle, Does.Contain("TÌM THÊM THÔNG TIN"));
        Assert.That(prototype.CurrentContextPanelTitle, Is.EqualTo("VẬT PHẨM NHIỆM VỤ"));
    }

    [Test]
    public void ThreeDistinctHousesGuaranteeApproximateOfficeSearchArea()
    {
        prototype.RegisterHouseLootContainerOpenedForPreview("House-A");
        prototype.RegisterHouseLootContainerOpenedForPreview("House-A");
        prototype.RegisterHouseLootContainerOpenedForPreview("House-B");

        Assert.That(prototype.GetObjectiveStatusForPreview(0), Is.EqualTo("2 / 3 NHÀ"));

        prototype.RegisterHouseLootContainerOpenedForPreview("House-C");
        Assert.That(prototype.GetObjectiveStatusForPreview(0), Is.EqualTo("HOÀN THÀNH"));
        Assert.That(prototype.GetObjectiveStatusForPreview(1), Is.EqualTo("VÙNG TƯƠNG ĐỐI"));

        prototype.SetMapOpenForPreview(true);
        Assert.That(prototype.IsMapOpen, Is.True);
        Assert.That(prototype.CurrentMapKnowledgeLabel, Is.EqualTo("ĐÃ KHOANH VÙNG TÌM KIẾM"));
    }

    [Test]
    public void ThreeOptionalRouteCluesGrantMapFragmentOneAndExactOfficeMarker()
    {
        prototype.RegisterRouteClueForPreview("Invoice");
        prototype.RegisterRouteClueForPreview("Invoice");
        prototype.RegisterRouteClueForPreview("BusRoute");
        prototype.RegisterRouteClueForPreview("AddressNote");

        prototype.SelectTabForPreview(1);
        prototype.SelectQuestForPreview(1);
        Assert.That(prototype.GetObjectiveStatusForPreview(0), Is.EqualTo("3 / 3"));
        Assert.That(prototype.GetObjectiveStatusForPreview(1), Is.EqualTo("ĐÃ GHÉP"));
        Assert.That(prototype.GetObjectiveStatusForPreview(2), Is.EqualTo("ĐÃ ĐÁNH DẤU"));

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

        prototype.SelectTabForPreview(2);
        prototype.SelectQuestForPreview(1);
        Assert.That(prototype.GetObjectiveStatusForPreview(1), Is.EqualTo("ĐÃ BỎ QUA"));
        Assert.That(prototype.GetObjectiveStatusForPreview(2), Is.EqualTo("ĐÃ TỰ TÌM"));
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
        Assert.That(prototype.IsMainQuestComplete, Is.True);
        prototype.SelectTabForPreview(1);
        prototype.SelectQuestForPreview(0);
        Assert.That(prototype.GetObjectiveStatusForPreview(2), Is.EqualTo("ĐÃ CÓ MẢNH 2"));
    }

    [Test]
    public void CompletedAndFailedTabsShowTheirOwnEmptyStates()
    {
        prototype.SelectTabForPreview(1);
        Assert.That(prototype.SelectedTabIndex, Is.EqualTo(1));
        Assert.That(prototype.IsEmptyStateVisible, Is.True);

        prototype.SelectTabForPreview(2);
        Assert.That(prototype.SelectedTabIndex, Is.EqualTo(2));
        Assert.That(prototype.IsEmptyStateVisible, Is.True);

        prototype.SelectTabForPreview(0);
        Assert.That(prototype.IsEmptyStateVisible, Is.False);
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
            prototype.RegisterHouseLootContainerOpenedForPreview("House-" + i);

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
        Assert.That(prototype.GetTabTextForPreview(1), Does.EndWith("01"));

        prototype.RegisterMapFragment2AddedToInventoryForPreview();
        Assert.That(prototype.GetTabTextForPreview(0), Does.EndWith("00"));
        Assert.That(prototype.GetTabTextForPreview(1), Does.EndWith("02"));

        prototype.SelectTabForPreview(1);
        Assert.That(prototype.IsEmptyStateVisible, Is.False);
    }

    [Test]
    public void RewardIdentityRemainsHiddenUntilItHasBeenReceived()
    {
        prototype.SelectQuestForPreview(0);
        Assert.That(prototype.CurrentRewardLabel, Is.EqualTo("PHẦN THƯỞNG"));
        Assert.That(prototype.CurrentRewardText, Is.EqualTo("Chưa xác định"));

        prototype.RegisterMapFragment2AddedToInventoryForPreview();
        prototype.SelectTabForPreview(1);
        prototype.SelectQuestForPreview(0);
        Assert.That(prototype.CurrentRewardLabel, Is.EqualTo("PHẦN THƯỞNG ĐÃ NHẬN"));
        Assert.That(prototype.CurrentRewardText, Is.EqualTo("Mảnh bản đồ 2"));
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
            Assert.That(prototype.HasBuiltElement("Quest Search Zone Label"), Is.True);
            Assert.That(prototype.HasBuiltElement("Restricted Fog West"), Is.True);
            Assert.That(prototype.HasBuiltElement("Restricted Fog East"), Is.True);
            Assert.That(prototype.HasBuiltElement("Restricted Fog South"), Is.True);
            Assert.That(prototype.HasBuiltElement("Restricted Fog North"), Is.True);
            Assert.That(prototype.CurrentMapSearchZoneHouseCount, Is.EqualTo(6));
            Assert.That(prototype.CurrentMapClueSummary, Does.Contain("6 nhà"));
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
    public void MilitaryGeneratorRaisesGateCapacityToOneHundredFiftyPercent()
    {
        Assert.That(MilitaryQuestRules.GetElectrifiedGateHealth(1000f), Is.EqualTo(1500f));
    }

    [Test]
    public void MilitaryGateDamageNeverCreatesNegativeHealth()
    {
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

}
