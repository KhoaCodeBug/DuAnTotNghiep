using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public sealed class MainMenuToMilitaryQuestFlowTests
{
    [UnityTest]
    [Timeout(60000)]
    public IEnumerator HospitalRadioH2SceneHasCanonicalCluesAndStartsWithClosedDoor()
    {
        yield return ShutdownExistingRunners();
        AsyncOperation loadMain = SceneManager.LoadSceneAsync("Main");
        while (!loadMain.isDone) yield return null;
        yield return null;

        Type controllerType = Type.GetType("HospitalRadioRoomController, Assembly-CSharp");
        Assert.That(controllerType, Is.Not.Null);
        Component controller = UnityEngine.Object.FindFirstObjectByType(controllerType) as Component;
        Assert.That(controller, Is.Not.Null, "Main scene must contain the H1 radio-room controller.");

        Type mainQuestType = Type.GetType("MainQuestManager, Assembly-CSharp");
        Assert.That(mainQuestType, Is.Not.Null);
        Component mainQuest = UnityEngine.Object.FindFirstObjectByType(mainQuestType) as Component;
        Assert.That(mainQuest, Is.Not.Null);
        FieldInfo restoreDuration = mainQuestType.GetField("hospitalRadioRestoreDuration",
            BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo spawnDelay = mainQuestType.GetField("hospitalRadioZombieSpawnDelay",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(restoreDuration, Is.Not.Null);
        Assert.That((float)restoreDuration.GetValue(mainQuest), Is.EqualTo(14f).Within(0.001f));
        Assert.That((float)spawnDelay.GetValue(mainQuest), Is.EqualTo(0.25f).Within(0.001f));
        Assert.That(GameObject.Find("HospitalQuest_ZombieEntry_A"), Is.Not.Null);
        Assert.That(GameObject.Find("HospitalQuest_ZombieEntry_B"), Is.Not.Null);
        Assert.That(GameObject.Find("TeleportToHospital"), Is.Not.Null,
            "Main scene must keep the authored F12 arrival marker for the hospital objective.");

        GameObject environmentStory = GameObject.Find("HospitalQuest_EnvironmentalStory");
        Assert.That(environmentStory, Is.Not.Null,
            "H4 must keep the hospital breadcrumb corpses in the authored scene.");
        Assert.That(environmentStory.transform.childCount, Is.EqualTo(4));
        for (int i = 0; i < environmentStory.transform.childCount; i++)
        {
            GameObject corpse = environmentStory.transform.GetChild(i).gameObject;
            Assert.That(corpse.GetComponent<SpriteRenderer>(), Is.Not.Null);
            Assert.That(corpse.GetComponent<Collider2D>(), Is.Null,
                "Environmental corpses are static story props and must not block movement.");
            Assert.That(corpse.GetComponents<MonoBehaviour>(), Is.Empty,
                "Environmental corpses must not own AI, interaction, networking or loot behaviour.");
        }

        Type clueType = Type.GetType("HospitalQuestClueInteractionPoint, Assembly-CSharp");
        Assert.That(clueType, Is.Not.Null);
        GameObject shiftLog = GameObject.Find("HospitalQuest_ShiftLog");
        GameObject shiftLog2 = GameObject.Find("HospitalQuest_ShiftLog2");
        Assert.That(shiftLog, Is.Not.Null);
        Assert.That(shiftLog2, Is.Not.Null);
        Component shiftLogPoint = shiftLog.GetComponent(clueType);
        Component shiftLog2Point = shiftLog2.GetComponent(clueType);
        Assert.That(shiftLogPoint, Is.Not.Null);
        Assert.That(shiftLog2Point, Is.Not.Null);

        const BindingFlags clueFields = BindingFlags.Instance | BindingFlags.NonPublic;
        FieldInfo interactionDistance = clueType.GetField("interactionDistance", clueFields);
        Assert.That(interactionDistance, Is.Not.Null);
        Assert.That((float)interactionDistance.GetValue(shiftLogPoint), Is.EqualTo(1.5f).Within(0.001f),
            "Reception ShiftLog must be reachable from the public side of the deep counter.");
        Assert.That((float)interactionDistance.GetValue(shiftLog2Point), Is.EqualTo(0.85f).Within(0.001f),
            "Only the deep reception counter should use the extended interaction distance.");
        FieldInfo clueZoneField = clueType.GetField("interactionZone", clueFields);
        Assert.That(clueZoneField?.GetValue(shiftLogPoint), Is.TypeOf<PolygonCollider2D>());
        Assert.That(clueZoneField?.GetValue(shiftLog2Point), Is.TypeOf<PolygonCollider2D>());
        Assert.That(clueZoneField.GetValue(shiftLogPoint), Is.Not.SameAs(clueZoneField.GetValue(shiftLog2Point)),
            "Every hospital interaction point must own an independently editable polygon.");

        Type keyLootType = Type.GetType("HospitalRadioKeyLootPoint, Assembly-CSharp");
        Assert.That(keyLootType, Is.Not.Null);
        UnityEngine.Object[] allKeyLoot = Resources.FindObjectsOfTypeAll(keyLootType);
        HashSet<int> keyLootIds = new HashSet<int>();
        int sceneKeyLootCount = 0;
        PropertyInfo keyInteractionId = keyLootType.GetProperty("InteractionId");
        PropertyInfo keyInteractionZone = keyLootType.GetProperty("InteractionZone");
        for (int i = 0; i < allKeyLoot.Length; i++)
        {
            Component point = allKeyLoot[i] as Component;
            if (point == null || !point.gameObject.scene.IsValid()) continue;
            sceneKeyLootCount++;
            int id = (int)keyInteractionId.GetValue(point);
            PolygonCollider2D zone = keyInteractionZone.GetValue(point) as PolygonCollider2D;
            Assert.That(id, Is.Not.EqualTo(0));
            Assert.That(keyLootIds.Add(id), Is.True, "Every KeyLoot must have a unique stable network ID.");
            Assert.That(zone, Is.Not.Null);
            Assert.That(zone.transform.parent, Is.SameAs(point.transform));
            Assert.That(zone.isTrigger, Is.True);
        }
        Assert.That(sceneKeyLootCount, Is.EqualTo(6));

        PolygonCollider2D[] polygons = UnityEngine.Object.FindObjectsByType<PolygonCollider2D>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        int hospitalInteractionZoneCount = 0;
        for (int i = 0; i < polygons.Length; i++)
            if (polygons[i] != null && polygons[i].name == "InteractionZone")
                hospitalInteractionZoneCount++;
        Assert.That(hospitalInteractionZoneCount, Is.EqualTo(10),
            "ShiftLog, ShiftLog2, Door, Radio and all six KeyLoot points need separate polygons.");

        Type legacyPointType = Type.GetType("MainQuestSearchCabinet, Assembly-CSharp");
        Assert.That(legacyPointType, Is.Not.Null);
        UnityEngine.Object[] legacyPoints = Resources.FindObjectsOfTypeAll(legacyPointType);
        for (int i = 0; i < legacyPoints.Length; i++)
        {
            Behaviour point = legacyPoints[i] as Behaviour;
            if (point != null && point.gameObject.scene.IsValid())
                Assert.That(point.enabled, Is.False,
                    "Temporary dispatch/radio/records-cabinet interactions must be disabled in H2.");
        }

        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        Tilemap tilemap = controllerType.GetField("doorTilemap", fields)?.GetValue(controller) as Tilemap;
        object cellValue = controllerType.GetField("doorCell", fields)?.GetValue(controller);
        TileBase closedTile = controllerType.GetField("closedDoorTile", fields)?.GetValue(controller) as TileBase;
        TileBase openTile = controllerType.GetField("openDoorTile", fields)?.GetValue(controller) as TileBase;
        Collider2D blocker = controllerType.GetField("doorBlocker", fields)?.GetValue(controller) as Collider2D;
        MethodInfo applyState = controllerType.GetMethod("ApplyState", fields);

        Assert.That(tilemap, Is.Not.Null);
        Assert.That(cellValue, Is.TypeOf<Vector3Int>());
        Assert.That(closedTile, Is.Not.Null);
        Assert.That(openTile, Is.Not.Null);
        Assert.That(blocker, Is.Not.Null);
        Assert.That(applyState, Is.Not.Null);
        Vector3Int cell = (Vector3Int)cellValue;

        Assert.That(tilemap.GetTile(cell), Is.SameAs(closedTile),
            "Awake must close the authored open door before the first rendered frame.");
        Assert.That(blocker.enabled, Is.True, "Closed door must have a solid blocker.");

        applyState.Invoke(controller, new object[] { true, true });
        Assert.That(tilemap.GetTile(cell), Is.SameAs(openTile));
        Assert.That(blocker.enabled, Is.False, "Opening must remove the physical blocker.");

        applyState.Invoke(controller, new object[] { false, true });
        Assert.That(tilemap.GetTile(cell), Is.SameAs(closedTile));
        Assert.That(blocker.enabled, Is.True);
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator MilitaryRepairStationUsesAuthoredPoliceCarWithoutRelocatingIt()
    {
        yield return ShutdownExistingRunners();
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
        Assert.That(SceneManager.GetActiveScene().buildIndex, Is.EqualTo(1));

        Type managerType = Type.GetType("MilitaryBaseQuestManager, Assembly-CSharp");
        Component manager = null;
        float managerDeadline = Time.realtimeSinceStartup + 30f;
        while (Time.realtimeSinceStartup < managerDeadline)
        {
            manager = UnityEngine.Object.FindFirstObjectByType(managerType) as Component;
            if (manager != null && ReadBool(manager, "IsNetworkReady")) break;
            yield return null;
        }
        Assert.That(manager, Is.Not.Null);
        Assert.That(ReadBool(manager, "HasStateAuthority"), Is.True);

        Type stationType = Type.GetType("RoadsideVehicleRepairStation, Assembly-CSharp");
        Component station = null;
        float stationDeadline = Time.realtimeSinceStartup + 10f;
        while (Time.realtimeSinceStartup < stationDeadline)
        {
            station = UnityEngine.Object.FindFirstObjectByType(stationType) as Component;
            if (station != null) break;
            yield return null;
        }
        Assert.That(station, Is.Not.Null, "Roadside repair station was not attached to the police car.");
        Assert.That(station.gameObject.name, Is.EqualTo("Car"));

        Type vehicleType = Type.GetType("VehicleControllerFusion, Assembly-CSharp");
        Component vehicle = station.GetComponent(vehicleType);
        Assert.That(vehicle, Is.Not.Null);
        Assert.That(ReadBool(vehicle, "IsEntryLockedForRepair"), Is.True,
            "The repair-test police car must not allow entering or driving.");

        GameObject policeCarMarker = GameObject.Find("SpawnXeCanhSat");
        Assert.That(policeCarMarker, Is.Not.Null);
        Assert.That(Vector2.Distance(station.transform.position, policeCarMarker.transform.position),
            Is.LessThan(2f),
            "The authored Car should remain beside SpawnXeCanhSat instead of being moved to ViTriXeTest.");
        Assert.That((int)ReadProperty(vehicle, "DirectionIndex"), Is.EqualTo(0),
            "The locked roadside repair car must use the preview's direction index.");
        FieldInfo stationPolygonField = stationType.GetField("inspectionPolygon",
            BindingFlags.NonPublic | BindingFlags.Instance);
        PolygonCollider2D policePolygon = stationPolygonField?.GetValue(station) as PolygonCollider2D;
        Assert.That(policePolygon, Is.Not.Null,
            "The authored Car must receive a usable front inspection polygon at runtime.");
        Assert.That(policePolygon.enabled, Is.True);
        Assert.That(policePolygon.isTrigger, Is.True);

        Assert.That(GameObject.Find("Vehicle Repair Skill Check UI"), Is.Not.Null);
    }

    [UnityTest]
    [Timeout(180000)]
    public IEnumerator SoloMenuFlowLoadsMainAndSpawnsMilitaryQuestWithoutModalOverlap()
    {
        yield return ShutdownExistingRunners();
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
        Transform authoredGate = FindInactiveTransform("CongRao");
        Assert.That(authoredGate, Is.Not.Null,
            "The finale must reuse the authored CongRao object instead of drawing a runtime gate.");
        Type gateType = Type.GetType("MilitaryGateController, Assembly-CSharp");
        Assert.That(authoredGate.GetComponent(gateType), Is.Not.Null);
        Assert.That(authoredGate.gameObject.activeSelf, Is.False,
            "CongRao must stay hidden before the military cinematic closes the entrance.");
        Assert.That(GameObject.Find("Car"), Is.Not.Null,
            "The authored completed Car is reused as the police repair vehicle.");
        Assert.That(GameObject.Find("Military School Clue 1 - HỒ SƠ TRỰC BAN"), Is.Not.Null);
        Assert.That(GameObject.Find("Military School Clue 2 - BẢN GHI TIẾP VẬN"), Is.Not.Null);
        Assert.That(GameObject.Find("Military School Clue 3 - LỆNH PHONG TỎA"), Is.Not.Null);
        GameObject gateMarker = GameObject.Find("ViTriDongCong");
        Assert.That(gateMarker, Is.Not.Null);
        Transform gateCollider = authoredGate.Find("CongRao Collider [RUNTIME]");
        Assert.That(gateCollider, Is.Not.Null);
        Assert.That(gateCollider.GetComponent<BoxCollider2D>(), Is.Not.Null);
        Assert.That(gateCollider.GetComponent<BoxCollider2D>().enabled, Is.False);
        Assert.That(gateCollider.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("Obstacle")),
            "The runtime gate must use the same Obstacle layer queried by every zombie AI.");
        Type visionPassType = Type.GetType("MilitaryGateVisionPassThrough, Assembly-CSharp");
        Assert.That(visionPassType, Is.Not.Null);
        Assert.That(gateCollider.GetComponent(visionPassType), Is.Not.Null,
            "The physical/A* gate must be ignored by Player fog line-of-sight.");
        Assert.That(Vector2.Distance(gateCollider.position, gateMarker.transform.position), Is.LessThan(2f));
        Type hordeType = Type.GetType("SiegeHordeDirector, Assembly-CSharp");
        Component horde = UnityEngine.Object.FindFirstObjectByType(hordeType) as Component;
        Assert.That(horde, Is.Not.Null);
        ICollection spawnPoints = ReadPrivateField(horde, "spawnPoints") as ICollection;
        Assert.That(spawnPoints?.Count, Is.EqualTo(4),
            "The siege must use all four authored ViTriSpawnZombie markers.");

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

        Type autoUIType = Type.GetType("AutoUIManager, Assembly-CSharp");
        Component autoUI = UnityEngine.Object.FindFirstObjectByType(autoUIType) as Component;
        Assert.That(autoUI, Is.Not.Null);
        Type inventoryType = Type.GetType("InventorySystem, Assembly-CSharp");
        Component inventory = ((Component)localPlayer).GetComponent(inventoryType);
        Assert.That(inventory, Is.Not.Null);
        Assert.That((int)inventoryType.GetField("maxSlots")?.GetValue(inventory), Is.EqualTo(20));

        RectTransform inventoryGrid = FindInactiveTransform("SlotGrid") as RectTransform;
        RectTransform inventoryScroll = FindInactiveTransform("InvScrollView") as RectTransform;
        RectTransform inventoryPanel = FindInactiveTransform("InventoryPanel") as RectTransform;
        RectTransform containerPanel = FindInactiveTransform("ContainerPanel") as RectTransform;
        RectTransform containerGrid = FindInactiveTransform("ContainerSlotGrid") as RectTransform;
        Assert.That(inventoryGrid, Is.Not.Null);
        Assert.That(inventoryPanel, Is.Not.Null);
        Assert.That(inventoryPanel.sizeDelta.y, Is.EqualTo(530f).Within(0.1f));
        Assert.That(inventoryGrid.childCount, Is.EqualTo(15),
            "Fixed inventory UI must render all 15 non-hotbar slots.");
        Assert.That(inventoryScroll, Is.Not.Null);
        ScrollRect inventoryScrollRect = inventoryScroll.GetComponent<ScrollRect>();
        Assert.That(inventoryScrollRect, Is.Not.Null);
        Assert.That(inventoryScrollRect.vertical, Is.True);
        Assert.That(inventoryScrollRect.horizontal, Is.False);

        Assert.That(containerPanel, Is.Not.Null);
        Assert.That(containerGrid, Is.Not.Null);
        Assert.That(containerGrid.childCount, Is.EqualTo(20),
            "Container data supports 20 stacks, so all 20 slots must be reachable in UI.");
        GridLayoutGroup containerLayout = containerGrid.GetComponent<GridLayoutGroup>();
        Assert.That(containerLayout, Is.Not.Null);
        Assert.That(containerLayout.constraint, Is.EqualTo(GridLayoutGroup.Constraint.FixedColumnCount));
        Assert.That(containerLayout.constraintCount, Is.EqualTo(4));

        Type tabManagerType = Type.GetType("AutoTabManager, Assembly-CSharp");
        Component tabManager = UnityEngine.Object.FindFirstObjectByType(tabManagerType) as Component;
        Assert.That(tabManager, Is.Not.Null);
        GameObject tabContainerObject = ReadPrivateField(tabManager, "tabContainer") as GameObject;
        RectTransform tabContainer = tabContainerObject != null
            ? tabContainerObject.GetComponent<RectTransform>()
            : null;
        RectTransform inventoryTitle = inventoryPanel.Find("TitleText") as RectTransform;
        Assert.That(tabContainer, Is.Not.Null);
        Assert.That(inventoryTitle, Is.Not.Null);
        float tabBottom = tabContainer.anchoredPosition.y + tabContainer.rect.yMin;
        float inventoryTitleTop = inventoryPanel.anchoredPosition.y + inventoryPanel.rect.yMax +
                                  inventoryTitle.anchoredPosition.y;
        Assert.That(inventoryTitleTop, Is.LessThanOrEqualTo(tabBottom - 4f),
            "The standalone inventory title must stay below the Inventory/Health tab bar.");

        inventoryPanel.gameObject.SetActive(true);
        containerPanel.gameObject.SetActive(true);
        autoUIType.GetMethod("UpdatePanelsLayout", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(autoUI, null);
        LayoutRebuilder.ForceRebuildLayoutImmediate(inventoryPanel);
        LayoutRebuilder.ForceRebuildLayoutImmediate(inventoryGrid);
        LayoutRebuilder.ForceRebuildLayoutImmediate(containerGrid);
        Canvas.ForceUpdateCanvases();
        Bounds inventoryBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
            inventoryScrollRect.viewport, inventoryGrid);
        Assert.That(inventoryBounds.min.y,
            Is.GreaterThanOrEqualTo(inventoryScrollRect.viewport.rect.yMin - 1f));
        Assert.That(inventoryBounds.max.y,
            Is.LessThanOrEqualTo(inventoryScrollRect.viewport.rect.yMax + 1f),
            "All 15 storage slots should be visible at once at the 1920x1080 reference layout.");
        Bounds containerBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(containerPanel, containerGrid);
        Assert.That(containerBounds.min.x, Is.GreaterThanOrEqualTo(containerPanel.rect.xMin - 0.5f));
        Assert.That(containerBounds.max.x, Is.LessThanOrEqualTo(containerPanel.rect.xMax + 0.5f));
        Assert.That(containerBounds.min.y, Is.GreaterThanOrEqualTo(containerPanel.rect.yMin - 0.5f));
        Assert.That(containerBounds.max.y, Is.LessThanOrEqualTo(containerPanel.rect.yMax + 0.5f),
            "The expanded 4x5 container grid must stay inside its panel.");
        Assert.That(inventoryPanel.anchoredPosition.x + inventoryPanel.rect.xMax,
            Is.LessThanOrEqualTo(containerPanel.anchoredPosition.x + containerPanel.rect.xMin),
            "Inventory and container panels must not overlap when opened together.");

        inventoryPanel.gameObject.SetActive(false);
        containerPanel.gameObject.SetActive(false);

        autoUIType.GetMethod("ShowReloadUI")?.Invoke(autoUI,
            new object[] { 0.8f, 2f, "ĐANG KIỂM TRA ĐỘNG CƠ..." });
        RectTransform actionBar = FindInactiveTransform("ActionBarPanel") as RectTransform;
        TMP_Text actionBarLabel = actionBar != null ? actionBar.GetComponentInChildren<TMP_Text>(true) : null;
        Assert.That(actionBar, Is.Not.Null);
        Assert.That(actionBar.sizeDelta.x, Is.GreaterThanOrEqualTo(420f));
        Assert.That(actionBarLabel, Is.Not.Null);
        Assert.That(actionBarLabel.textWrappingMode, Is.EqualTo(TextWrappingModes.NoWrap),
            "Hold-E progress text must never wrap below the action bar.");
        autoUIType.GetMethod("HideReloadUI")?.Invoke(autoUI, null);

        Type mainQuestType = Type.GetType("MainQuestManager, Assembly-CSharp");
        Assert.That(mainQuestType, Is.Not.Null);
        Component mainQuest = null;
        float mainQuestDeadline = Time.realtimeSinceStartup + 15f;
        while (Time.realtimeSinceStartup < mainQuestDeadline)
        {
            mainQuest = UnityEngine.Object.FindFirstObjectByType(mainQuestType) as Component;
            if (mainQuest != null && ReadBool(mainQuest, "IsNetworkReady")) break;
            yield return null;
        }
        Assert.That(mainQuest, Is.Not.Null, "MainQuestManager was not present in Main.");
        GameObject arrivalCar = GameObject.Find("Broken Arrival Car (from Intro)");
        Assert.That(arrivalCar, Is.Not.Null,
            "Main must continue the Intro scene with the same broken car beside the player spawn cluster.");
        Assert.That(ReadBool(mainQuest, "IsArrivalCarInspected"), Is.False);
        Assert.That(ReadBool(mainQuest, "IsNeighborhoodConfigured"), Is.False,
            "The neighborhood investigation must wait until the broken engine is inspected.");

        Type arrivalCarType = Type.GetType("BrokenArrivalCar, Assembly-CSharp");
        Assert.That(arrivalCarType, Is.Not.Null);
        Component arrivalCarComponent = arrivalCar.GetComponent(arrivalCarType);
        Type inspectionUIType = Type.GetType("ArrivalCarInspectionUI, Assembly-CSharp");
        Component inspectionUI = arrivalCar.GetComponent(inspectionUIType);
        Assert.That(inspectionUI, Is.Not.Null);
        Assert.That(ReadProperty(inspectionUI, "SelectedPartId").ToString(), Is.EqualTo("engine"));
        RectTransform engineHotspot = AssertHotspotLayout("engine",
            new Vector2(47.09247f, 222.6873f), new Vector2(103f, 63f));
        AssertHotspotLayout("battery", new Vector2(-47.5f, 218f), new Vector2(46f, 35f));
        AssertHotspotLayout("exhaust", new Vector2(55f, -222f), new Vector2(38f, 69f));
        AssertHotspotLayout("fuel", new Vector2(-48f, -224.5f), new Vector2(89f, 59f));
        AssertHotspotLayout("front_left", new Vector2(-103f, 86.5f), new Vector2(41f, 73f));
        AssertHotspotLayout("rear_left", new Vector2(-103f, -102f), new Vector2(41f, 73f));
        AssertHotspotLayout("front_right", new Vector2(107f, 86.5f), new Vector2(41f, 73f));
        AssertHotspotLayout("rear_right", new Vector2(107f, -102f), new Vector2(41f, 73f));
        AssertHotspotLayout("hood", new Vector2(0f, 119f), new Vector2(110f, 85f));
        AssertHotspotLayout("windshield", new Vector2(0f, 48f), new Vector2(107f, 52f));
        RectTransform selectedPart = FindInactiveTransform("Selected Vehicle Part") as RectTransform;
        Assert.That(selectedPart, Is.Not.Null);
        Assert.That(selectedPart.sizeDelta.x, Is.EqualTo(engineHotspot.sizeDelta.x).Within(0.1f));
        Assert.That(selectedPart.sizeDelta.y, Is.EqualTo(engineHotspot.sizeDelta.y).Within(0.1f),
            "The active-part outline must match the white artwork frame exactly.");
        RectTransform headerTitle = FindInactiveTransform("Header Title") as RectTransform;
        RectTransform headerRule = FindInactiveTransform("Header Rule") as RectTransform;
        RectTransform inspectionShell = FindInactiveTransform("Vehicle Condition Window") as RectTransform;
        RectTransform closeButtonRect = FindInactiveTransform("Close Button") as RectTransform;
        RectTransform startEngineRect = FindInactiveTransform("Start Engine Button") as RectTransform;
        Assert.That(headerTitle, Is.Not.Null);
        Assert.That(headerRule, Is.Not.Null);
        Assert.That(inspectionShell, Is.Not.Null);
        Assert.That(closeButtonRect, Is.Not.Null);
        Assert.That(startEngineRect, Is.Not.Null);
        Assert.That(headerTitle.anchoredPosition.y - headerTitle.sizeDelta.y,
            Is.GreaterThan(headerRule.anchoredPosition.y),
            "The title must finish above the header rule instead of being clipped by it.");
        Bounds startBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
            inspectionShell, startEngineRect);
        Bounds closeBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
            inspectionShell, closeButtonRect);
        Bounds headerRuleBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
            inspectionShell, headerRule);
        Assert.That(startBounds.max.x, Is.LessThanOrEqualTo(closeBounds.min.x - 4f),
            "Start Engine must not overlap the close button.");
        Assert.That(startBounds.min.y, Is.GreaterThanOrEqualTo(headerRuleBounds.max.y + 1f),
            "Start Engine must stay completely above the header divider.");
        Button leftTireHotspot = FindInactiveButtonUnder(inspectionUI, "Vehicle Part Hotspot front_left");
        Assert.That(leftTireHotspot, Is.Not.Null, "The vehicle diagram must expose clickable part hotspots.");
        leftTireHotspot.onClick.Invoke();
        Assert.That(ReadProperty(inspectionUI, "SelectedPartId").ToString(), Is.EqualTo("front_left"));
        Assert.That(ReadProperty(inspectionUI, "SelectedPartActionText").ToString(), Is.EqualTo("THAY LINH KIỆN"));
        TMP_Text selectedPartTitle = (FindInactiveTransform("Selected Part Title") as RectTransform)
            ?.GetComponent<TMP_Text>();
        Assert.That(selectedPartTitle, Is.Not.Null);
        Assert.That(selectedPartTitle.text, Does.Contain("0%"),
            "The actually broken front-left tire must start at 0%. ");
        Assert.That(FindInactiveButton("Selected Part Action Button"), Is.Not.Null,
            "The selected part detail must preserve the approved contextual action button.");
        Button startEngineButton = FindInactiveButtonUnder(inspectionUI, "Start Engine Button");
        Assert.That(startEngineButton, Is.Not.Null);
        Assert.That(startEngineButton.interactable, Is.False,
            "The car cannot start before all required repairs are complete.");
        Button healthyTireHotspot = FindInactiveButtonUnder(inspectionUI, "Vehicle Part Hotspot front_right");
        Assert.That(healthyTireHotspot, Is.Not.Null);
        healthyTireHotspot.onClick.Invoke();
        Assert.That(ReadProperty(inspectionUI, "SelectedPartActionText").ToString(), Is.EqualTo("KIỂM TRA"),
            "A temporary 60%+ tire must not consume the only replacement tire.");
        Button exhaustHotspot = FindInactiveButtonUnder(inspectionUI, "Vehicle Part Hotspot exhaust");
        Assert.That(exhaustHotspot, Is.Not.Null);
        exhaustHotspot.onClick.Invoke();
        Assert.That(ReadProperty(inspectionUI, "SelectedPartActionText").ToString(), Is.EqualTo("KIỂM TRA"));
        Assert.That(FindInactiveTransform("Damaged Hood Polygon"), Is.Not.Null,
            "The damaged hood should use an irregular translucent polygon, not a square label.");
        Assert.That(FindInactiveTransform("Repair Requirements Panel"), Is.Null,
            "Repair inventory belongs in the J journal, not in the inspection modal.");
        Vector3 inspectionPoint = (Vector3)ReadProperty(arrivalCarComponent, "InspectionZoneWorldCenter");
        ((Component)localPlayer).transform.position = inspectionPoint;
        yield return null;
        inspectionUIType.GetMethod("Open", BindingFlags.Public | BindingFlags.Instance, null,
            new[] { arrivalCarType }, null)?.Invoke(inspectionUI, new object[] { arrivalCarComponent });
        Assert.That(ReadBool(inspectionUI, "IsOpen"), Is.True);
        mainQuestType.GetMethod("RequestInspectArrivalCar")?.Invoke(mainQuest, null);
        float investigationDeadline = Time.realtimeSinceStartup + 15f;
        while (Time.realtimeSinceStartup < investigationDeadline &&
               !ReadBool(mainQuest, "IsNeighborhoodConfigured"))
            yield return null;

        Assert.That(ReadBool(mainQuest, "IsArrivalCarInspected"), Is.True,
            "Inspecting the arrival car did not advance the story hand-off.");
        yield return null;
        Assert.That(ReadBool(inspectionUI, "IsOpen"), Is.False,
            "The inspection panel must close before the parts-search quest notice appears.");
        Type radioBroadcastType = Type.GetType("RouteBRadioBroadcastUI, Assembly-CSharp");
        Assert.That(radioBroadcastType, Is.Not.Null);
        Assert.That(GameObject.Find("Route B Radio Broadcast UI"), Is.Not.Null,
            "The emergency broadcast must introduce Route B before the tracking choice.");
        Assert.That((bool)radioBroadcastType.GetProperty("IsVisible")?.GetValue(null), Is.True);
        Assert.That((bool)radioBroadcastType.GetProperty("BlocksLocalGameplayInput")?.GetValue(null), Is.True,
            "Dialogue must block only this client's local gameplay input.");
        GameObject radioPanel = GameObject.Find("Route B Radio Panel");
        Assert.That(radioPanel, Is.Not.Null);
        RectTransform radioPanelRect = radioPanel.GetComponent<RectTransform>();
        Assert.That(radioPanelRect.anchorMin.y, Is.EqualTo(0f).Within(0.001f));
        Assert.That(radioPanelRect.anchoredPosition.y, Is.EqualTo(128f).Within(0.01f),
            "The dialogue panel must sit above the bottom-center hotbar area.");
        AudioSource dialogueSource = GameObject.Find("Route B Radio Broadcast UI").GetComponent<AudioSource>();
        Assert.That(dialogueSource.ignoreListenerVolume, Is.True,
            "Dialogue voice must remain clear while the local game mix is ducked.");
        TMP_Text radioSpeaker = GameObject.Find("Radio Speaker").GetComponent<TMP_Text>();
        Assert.That(radioSpeaker.text, Is.Not.Empty);
        Assert.That(radioSpeaker.text, Does.Not.Contain("ROUTE B").And.Not.Contain("TUYẾN"),
            "The dialogue header should identify only the speaker, not the route.");
        Type localUIStateType = Type.GetType("LocalGameplayUIState, Assembly-CSharp");
        PropertyInfo blocksHintsProperty = localUIStateType?.GetProperty("BlocksWorldInteractionHints",
            BindingFlags.Public | BindingFlags.Static);
        Assert.That((bool)blocksHintsProperty?.GetValue(null), Is.True,
            "Collider prompts must be suppressed while dialogue owns local input.");
        LineRenderer arrivalPromptLine = FindInactiveTransform("Front Inspection Zone")
            ?.GetComponent<LineRenderer>();
        Assert.That(arrivalPromptLine == null || !arrivalPromptLine.enabled, Is.True,
            "The arrival-car collider outline must be hidden behind dialogue.");
        Canvas fogOverlayCanvas = FindInactiveTransform("Local Fog Vision Overlay")?.GetComponent<Canvas>();
        Assert.That(fogOverlayCanvas, Is.Not.Null);
        Assert.That(fogOverlayCanvas.enabled, Is.True,
            "Dialogue may suppress interactive HUD canvases, but must preserve world fog.");
        Assert.That(GameObject.Find("Escape Route Decision UI"), Is.Null,
            "The tracking choice must wait until the opening radio sequence finishes.");
        radioBroadcastType.GetMethod("SkipIfOpen", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
        float routeChoiceDeadline = Time.realtimeSinceStartup + 5f;
        while (GameObject.Find("Escape Route Decision UI") == null &&
               Time.realtimeSinceStartup < routeChoiceDeadline)
            yield return null;

        Type routeDecisionType = Type.GetType("EscapeRouteDecisionUI, Assembly-CSharp");
        Assert.That(routeDecisionType, Is.Not.Null);
        Assert.That(GameObject.Find("Escape Route Decision UI"), Is.Not.Null,
            "Closing the first inspection must introduce both escape routes.");
        Assert.That((bool)radioBroadcastType.GetProperty("BlocksLocalGameplayInput")?.GetValue(null), Is.False,
            "Local gameplay input must be restored after dialogue ends.");
        Assert.That(fogOverlayCanvas.enabled, Is.True,
            "The route-choice transition must not flash or disable world fog.");
        Assert.That(FindInactiveTransform("Tracking Does Not Lock Ending"), Is.Null,
            "The removed ending-lock footer must not return to the route choice.");
        Assert.That(FindInactiveTransform("Route Profile"), Is.Not.Null,
            "Each tracking card must explain its experience and risk profile.");
        Assert.That(ReadProperty(mainQuest, "LockedEscapeRoute").ToString(), Is.EqualTo("None"),
            "Introducing or tracking a route must not lock an ending.");
        routeDecisionType.GetMethod("CloseIfOpen", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
        yield return null;
        Assert.That((bool)blocksHintsProperty?.GetValue(null), Is.False,
            "Closing the modal while still in the collider must release prompt suppression.");
        Assert.That(arrivalPromptLine == null || arrivalPromptLine.enabled, Is.True,
            "The collider outline must reappear when the player is still inside after closing UI.");
        Assert.That(ReadBool(mainQuest, "IsNeighborhoodConfigured"), Is.True,
            "State Authority did not replicate the shared opening neighborhood.");
        Assert.That(ReadProperty(mainQuest, "CurrentStage").ToString(), Is.EqualTo("SearchNeighborhood"));
        int searchHouseCount = (int)ReadProperty(mainQuest, "SearchHouseCount");
        Assert.That(searchHouseCount, Is.EqualTo(6));

        Type itemLoaderType = Type.GetType("ItemDataLoader, Assembly-CSharp");
        MethodInfo loadItem = itemLoaderType?.GetMethod("LoadItem", BindingFlags.Public | BindingFlags.Static);
        MethodInfo addItem = inventoryType?.GetMethod("AddItem");
        MethodInfo hasItemNamed = inventoryType?.GetMethod("HasItemNamed");
        Assert.That(loadItem, Is.Not.Null);
        Assert.That(addItem, Is.Not.Null);
        Assert.That(hasItemNamed, Is.Not.Null);
        foreach (string itemId in new[]
                 {
                     "ArrivalCarToolbox", "ArrivalCarHammer", "ArrivalCarFuelCan",
                     "ArrivalCarBattery", "ArrivalCarTire"
                 })
        {
            object item = loadItem.Invoke(null, new object[] { itemId });
            Assert.That(item, Is.Not.Null, "Arrival-car item catalog did not resolve " + itemId);
            Assert.That((bool)addItem.Invoke(inventory, new[] { item, (object)1 }), Is.True);
        }

        MethodInfo requestRepair = mainQuestType.GetMethod("RequestRepairArrivalCarPart");
        Assert.That(requestRepair, Is.Not.Null);
        requestRepair.Invoke(mainQuest, new object[] { "engine" });
        yield return null;
        Assert.That(ReadBool(mainQuest, "IsArrivalCarRepaired"), Is.False,
            "Core repair alone must not complete the optional quest.");
        requestRepair.Invoke(mainQuest, new object[] { "fuel" });
        yield return null;
        requestRepair.Invoke(mainQuest, new object[] { "battery" });
        yield return null;
        requestRepair.Invoke(mainQuest, new object[] { "front_left" });
        yield return null;
        Assert.That(ReadBool(mainQuest, "AreArrivalCarRequiredRepairsComplete"), Is.True);
        Assert.That(ReadBool(mainQuest, "IsArrivalCarRepaired"), Is.False,
            "Completing the parts must not bypass the approved Start Engine button.");
        Assert.That(GameObject.Find("Repaired Arrival Car"), Is.Null,
            "The drivable vehicle must not spawn before Start Engine succeeds.");

        inspectionUIType.GetMethod("Open", BindingFlags.Public | BindingFlags.Instance, null,
            new[] { arrivalCarType }, null)?.Invoke(inspectionUI, new object[] { arrivalCarComponent });
        yield return null;
        startEngineButton = FindInactiveButtonUnder(inspectionUI, "Start Engine Button");
        Assert.That(startEngineButton, Is.Not.Null);
        Assert.That(startEngineButton.interactable, Is.True,
            "Start Engine must become available after all four repairs.");
        TMP_Text startEngineText = startEngineButton.GetComponentInChildren<TMP_Text>(true);
        Assert.That(startEngineText, Is.Not.Null);
        Assert.That(startEngineText.text, Is.EqualTo("KHỞI ĐỘNG XE"));
        startEngineButton.onClick.Invoke();
        float repairDeadline = Time.realtimeSinceStartup + 10f;
        while (!ReadBool(mainQuest, "IsArrivalCarRepaired") && Time.realtimeSinceStartup < repairDeadline)
            yield return null;

        Assert.That(ReadBool(mainQuest, "IsArrivalCarRepaired"), Is.True);
        Assert.That((bool)hasItemNamed.Invoke(inventory, new object[] { "ArrivalCarToolbox" }), Is.True,
            "Toolbox is a retained tool, not a consumed part.");
        Assert.That((bool)hasItemNamed.Invoke(inventory, new object[] { "ArrivalCarHammer" }), Is.True,
            "Hammer is a retained tool, not a consumed part.");
        Assert.That((bool)hasItemNamed.Invoke(inventory, new object[] { "ArrivalCarFuelCan" }), Is.False,
            "Fuel must be consumed by the authoritative transaction.");
        Assert.That((bool)hasItemNamed.Invoke(inventory, new object[] { "ArrivalCarBattery" }), Is.False,
            "The installed battery must be consumed by the authoritative transaction.");
        Assert.That((bool)hasItemNamed.Invoke(inventory, new object[] { "ArrivalCarTire" }), Is.False,
            "The installed tire must be consumed by the authoritative transaction.");
        GameObject repairedArrivalCar = GameObject.Find("Repaired Arrival Car");
        Assert.That(repairedArrivalCar, Is.Not.Null,
            "Starting after all required repairs must replace the broken prop with a drivable Fusion vehicle.");
        Type sedanControllerType = Type.GetType("VehicleControllerFusion, Assembly-CSharp");
        Assert.That(sedanControllerType, Is.Not.Null);
        Component sedanController = repairedArrivalCar.GetComponent(sedanControllerType);
        Assert.That(sedanController, Is.Not.Null,
            "The repaired arrival sedan must use the existing network vehicle interaction/controller flow.");
        Assert.That(ReadPrivateField(sedanController, "directionLayout").ToString(),
            Is.EqualTo("EightWayIsometric"));
        Sprite[] sedanDirections = ReadPrivateField(sedanController, "directionSprites") as Sprite[];
        Assert.That(sedanDirections, Is.Not.Null);
        Assert.That(sedanDirections.Length, Is.EqualTo(8));
        Assert.That(sedanDirections, Has.All.Not.Null,
            "The sedan sprite order must be N, NE, E, SE, S, SW, W, NW without gaps.");
        Assert.That((int)ReadProperty(sedanController, "DirectionIndex"), Is.EqualTo(7),
            "The repaired sedan must initially match the canonical NW broken-car pose.");
        Transform sedanVisual = repairedArrivalCar.transform.Find("SedanVisual");
        Assert.That(sedanVisual, Is.Not.Null);
        Assert.That(sedanVisual.GetComponent<SpriteRenderer>()?.enabled, Is.True);
        Assert.That(repairedArrivalCar.GetComponent<SpriteRenderer>()?.enabled, Is.False,
            "The duplicated police renderer must stay disabled; only sedan art may be visible.");

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

        MethodInfo getSearchReturnPoint = bridgeType.GetMethod("TryGetSearchZoneReturnPoint",
            BindingFlags.Public | BindingFlags.Instance);
        Assert.That(getSearchReturnPoint, Is.Not.Null);
        object[] insideProbe = { (Vector2)((Component)localPlayer).transform.position, 1.25f, null, 0f };
        Assert.That((bool)getSearchReturnPoint.Invoke(bridge, insideProbe), Is.True);
        Assert.That((float)insideProbe[3], Is.EqualTo(0f).Within(0.001f),
            "Accepting the search quest must not add boundary darkness while the player is inside.");
        Rect visualSearchRect = (Rect)ReadPrivateField(bridge, "searchZoneMapRect");
        Rect gameplaySearchRect = (Rect)ReadPrivateField(bridge, "gameplaySearchZoneMapRect");
        Assert.That(gameplaySearchRect.xMin, Is.EqualTo(visualSearchRect.xMin).Within(0.0001f));
        Assert.That(gameplaySearchRect.yMin, Is.EqualTo(visualSearchRect.yMin).Within(0.0001f));
        Assert.That(gameplaySearchRect.xMax, Is.EqualTo(visualSearchRect.xMax).Within(0.0001f));
        Assert.That(gameplaySearchRect.yMax, Is.EqualTo(visualSearchRect.yMax).Within(0.0001f),
            "The rectangle shown on the map must exactly match fog and server correction bounds.");

        object rasterMap = ReadPrivateField(bridge, "rasterMap");
        MethodInfo normalizedToWorld = rasterMap.GetType().GetMethod("NormalizedToWorld");
        Vector3 nearbyOutsideWorld = (Vector3)normalizedToWorld.Invoke(rasterMap,
            new object[] { new Vector2(gameplaySearchRect.xMax + 0.015f, gameplaySearchRect.center.y) });
        object[] nearbyOutsideProbe = { (Vector2)nearbyOutsideWorld, 1.25f, null, 0f };
        Assert.That((bool)getSearchReturnPoint.Invoke(bridge, nearbyOutsideProbe), Is.True);
        Assert.That((float)nearbyOutsideProbe[3], Is.GreaterThan(0.35f),
            "Walking a short distance beyond the visible district must activate fog and return guidance.");

        object[] outsideProbe = { new Vector2(100000f, 100000f), 1.25f, null, 0f };
        Assert.That((bool)getSearchReturnPoint.Invoke(bridge, outsideProbe), Is.True);
        Vector2 expectedSearchReturn = (Vector2)outsideProbe[2];
        Assert.That((float)outsideProbe[3], Is.GreaterThan(10f),
            "The configured clue district must report a real outside distance.");

        Rigidbody2D boundaryBody = ((Component)localPlayer).GetComponent<Rigidbody2D>();
        if (boundaryBody != null) boundaryBody.position = new Vector2(100000f, 100000f);
        ((Component)localPlayer).transform.position = new Vector2(100000f, 100000f);
        Physics2D.SyncTransforms();
        mainQuestType.GetMethod("RequestReturnPlayerToSearchZone")?.Invoke(mainQuest, null);
        yield return null;
        Assert.That(Vector2.Distance(((Component)localPlayer).transform.position, expectedSearchReturn),
            Is.LessThan(0.35f),
            "State Authority must return only the player who crossed the clue-district boundary.");

        if (boundaryBody != null) boundaryBody.position = new Vector2(100000f, 100000f);
        ((Component)localPlayer).transform.position = new Vector2(100000f, 100000f);
        Physics2D.SyncTransforms();
        mainQuestType.GetMethod("RequestReturnPlayerToSearchZone")?.Invoke(mainQuest, null);
        yield return null;
        Assert.That(Vector2.Distance(((Component)localPlayer).transform.position, expectedSearchReturn),
            Is.LessThan(0.35f),
            "Crossing the district repeatedly must never become a one-shot correction.");

        MethodInfo updateSearchWarning = bridgeType.GetMethod("UpdateOutsideSearchZoneWarning",
            BindingFlags.NonPublic | BindingFlags.Instance);
        updateSearchWarning?.Invoke(bridge, new object[] { new Vector2(100000f, 100000f), mainQuest });
        Type fogType = Type.GetType("FogVisionController, Assembly-CSharp");
        Component fog = UnityEngine.Object.FindFirstObjectByType(fogType) as Component;
        Assert.That(fog, Is.Not.Null);
        Assert.That((bool)ReadProperty(fog, "IsQuestSearchBoundaryActive"), Is.True,
            "The world fog must cover the area beyond the active clue district.");
        Assert.That((float)ReadProperty(bridge, "OutsideBoundaryDistance"), Is.GreaterThan(10f));
        Assert.That((float)ReadProperty(bridge, "BoundaryObscureAlpha"), Is.GreaterThan(0f),
            "The offending client's view must start darkening outside the district.");

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

        // Once the neighborhood is complete, travel to the office is free-roam.
        // The map reveal replaces the old outside-area warning and correction.
        SetProperty(mainQuest, "NetworkQuestStage", 2); // LocateOffice
        SetPrivateField(bridge, "outsideSince", Time.unscaledTime - 2f);
        SetPrivateField(bridge, "nextOutsideWarningTime", 0f);
        MethodInfo updateWarning = bridgeType.GetMethod("UpdateOutsideSearchZoneWarning",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(updateWarning, Is.Not.Null);
        updateWarning.Invoke(bridge, new object[] { new Vector2(100000f, 100000f), mainQuest });
        Assert.That((bool)ReadPrivateField(bridge, "guidanceTargetsOffice"), Is.False);
        Assert.That((float)ReadPrivateField(bridge, "outsideWarningVisibleUntil"),
            Is.EqualTo(0f));

        QuestFlowUIPrototype questUI = UnityEngine.Object.FindFirstObjectByType<QuestFlowUIPrototype>(
            FindObjectsInactive.Include);
        Assert.That(questUI == null || !questUI.IsQuestOverlayOpen, Is.True,
            "Quest journal/map modal overlaps gameplay immediately after Main loads.");
    }

    [UnityTest]
    [Timeout(180000)]
    public IEnumerator RouteBDebugFlowRunsFromMainMenuThroughMilitaryExtractionWithoutLootContainers()
    {
        yield return ShutdownExistingRunners();
        PlayerPrefs.SetInt("GameLanguage", 1);
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
        Assert.That(SceneManager.GetActiveScene().buildIndex, Is.EqualTo(1));

        Type mainType = Type.GetType("MainQuestManager, Assembly-CSharp");
        Type militaryType = Type.GetType("MilitaryBaseQuestManager, Assembly-CSharp");
        Assert.That(mainType, Is.Not.Null);
        Assert.That(militaryType, Is.Not.Null);
        Component main = null;
        Component military = null;
        float managersDeadline = Time.realtimeSinceStartup + 30f;
        while (Time.realtimeSinceStartup < managersDeadline)
        {
            main = UnityEngine.Object.FindFirstObjectByType(mainType) as Component;
            military = UnityEngine.Object.FindFirstObjectByType(militaryType) as Component;
            if (main != null && military != null && ReadBool(main, "IsNetworkReady") &&
                ReadBool(military, "IsNetworkReady")) break;
            yield return null;
        }
        Assert.That(main, Is.Not.Null);
        Assert.That(military, Is.Not.Null);
        Assert.That(ReadBool(main, "HasStateAuthority"), Is.True);
        Assert.That(ReadBool(military, "HasStateAuthority"), Is.True);

        MethodInfo advanceStory = mainType.GetMethod("DebugAdvanceRouteB");
        MethodInfo advanceBase = militaryType.GetMethod("DebugAdvanceMilitaryRoute");
        Assert.That(advanceStory, Is.Not.Null);
        Assert.That(advanceBase, Is.Not.Null);

        // B0: debug inspection still lets the authoritative bridge choose the
        // real shared neighborhood; no quest LootContainer transaction is used.
        advanceStory.Invoke(main, null);
        float neighborhoodDeadline = Time.realtimeSinceStartup + 15f;
        while (ReadProperty(main, "CurrentStage").ToString() == "NotStarted" &&
               Time.realtimeSinceStartup < neighborhoodDeadline)
            yield return null;
        Assert.That(ReadProperty(main, "CurrentStage").ToString(), Is.EqualTo("SearchNeighborhood"));

        // B1: three presses simulate the three document pickups one by one so
        // Cue 03, 04 and 05 all pass through their production presentation path.
        advanceStory.Invoke(main, null);
        advanceStory.Invoke(main, null);
        advanceStory.Invoke(main, null);
        Assert.That((int)ReadProperty(main, "RouteClueCount"), Is.EqualTo(3));
        Assert.That(ReadProperty(main, "CurrentStage").ToString(), Is.EqualTo("LocateOffice"));

        // H5 canonical path: arrive → ShiftLog → ShiftLog2 → randomized shared key → door.
        advanceStory.Invoke(main, null);
        Assert.That(ReadProperty(main, "CurrentStage").ToString(), Is.EqualTo("FindCityMap"));
        Assert.That(ReadProperty(main, "CurrentHospitalInvestigationStage").ToString(),
            Is.EqualTo("FindShiftLog"));
        advanceStory.Invoke(main, null);
        Assert.That(ReadProperty(main, "CurrentHospitalInvestigationStage").ToString(),
            Is.EqualTo("FindShiftLog2"));
        advanceStory.Invoke(main, null);
        Assert.That(ReadBool(main, "HasHospitalRadioKeyState"), Is.False,
            "ShiftLog2 must reveal a random key location instead of granting the key immediately.");
        Assert.That(ReadProperty(main, "CurrentHospitalInvestigationStage").ToString(),
            Is.EqualTo("FindRadioKey"));
        Assert.That((int)ReadProperty(main, "SelectedHospitalRadioKeyLootIdState"), Is.Not.EqualTo(0));
        advanceStory.Invoke(main, null);
        Assert.That(ReadBool(main, "HasHospitalRadioKeyState"), Is.True,
            "Collecting the selected KeyLoot must grant one replicated team key.");
        Assert.That(ReadProperty(main, "CurrentHospitalInvestigationStage").ToString(),
            Is.EqualTo("UnlockRadioRoom"));
        advanceStory.Invoke(main, null);
        Assert.That(ReadProperty(main, "CurrentHospitalInvestigationStage").ToString(),
            Is.EqualTo("RadioReady"));
        Assert.That(ReadBool(main, "IsHospitalRadioDoorOpenState"), Is.True);
        Assert.That(ReadBool(main, "IsCityMapUnlocked"), Is.False,
            "H2 must stop at RadioReady; Radio story and military-map recovery belong to H3.");

        // Three further F6 presses exercise the H4 milestone path: each of the
        // first two stages release the operator and, on Easy, spawn 3 zombies at A + 3 at B.
        advanceStory.Invoke(main, null);
        float firstThreatDeadline = Time.realtimeSinceStartup + 4f;
        while ((int)ReadProperty(main, "HospitalRadioThreatSpawnCountState") < 6 &&
               Time.realtimeSinceStartup < firstThreatDeadline)
            yield return null;
        Assert.That((int)ReadProperty(main, "HospitalRadioCheckpointCountState"), Is.EqualTo(1));
        Assert.That((int)ReadProperty(main, "HospitalRadioThreatSpawnCountState"), Is.EqualTo(6));
        Assert.That(ReadProperty(main, "CurrentStage").ToString(), Is.EqualTo("FindCityMap"));
        Assert.That(ReadBool(main, "HasHospitalRadioOperator"), Is.False);

        advanceStory.Invoke(main, null);
        float secondThreatDeadline = Time.realtimeSinceStartup + 4f;
        while ((int)ReadProperty(main, "HospitalRadioThreatSpawnCountState") < 12 &&
               Time.realtimeSinceStartup < secondThreatDeadline)
            yield return null;
        Assert.That((int)ReadProperty(main, "HospitalRadioCheckpointCountState"), Is.EqualTo(2));
        Assert.That((int)ReadProperty(main, "HospitalRadioThreatSpawnCountState"), Is.EqualTo(12));
        Assert.That(ReadProperty(main, "CurrentStage").ToString(), Is.EqualTo("FindCityMap"));
        Assert.That(ReadBool(main, "HasHospitalRadioOperator"), Is.False);

        advanceStory.Invoke(main, null);
        Assert.That(ReadProperty(main, "CurrentStage").ToString(), Is.EqualTo("CityMapFound"));
        Assert.That(ReadBool(main, "IsCityMapUnlocked"), Is.True);
        Assert.That(ReadBool(main, "IsHospitalRadioRecoveredState"), Is.True);
        Assert.That((float)ReadProperty(main, "HospitalRadioRestoreNormalized"), Is.EqualTo(1f));
        Assert.That((int)ReadProperty(main, "HospitalRadioCheckpointCountState"), Is.EqualTo(3));
        Assert.That(ReadBool(main, "HasHospitalRadioOperator"), Is.False,
            "Completing H3 must release the shared Radio operator slot.");
        Assert.That(ReadProperty(main, "LockedEscapeRoute").ToString(), Is.EqualTo("None"),
            "The second tracking choice must not lock an ending.");

        QuestFlowUIPrototype journal = UnityEngine.Object.FindFirstObjectByType<QuestFlowUIPrototype>(
            FindObjectsInactive.Include);
        float militaryMarkerDeadline = Time.realtimeSinceStartup + 5f;
        while (journal != null && !journal.IsMapMilitaryDestinationVisible &&
               Time.realtimeSinceStartup < militaryMarkerDeadline)
            yield return null;
        Assert.That(journal, Is.Not.Null);
        Assert.That(journal.CurrentHospitalRadioTranscript, Does.Contain("không quay lại"));
        Assert.That(journal.CurrentHospitalRadioTranscript, Does.Contain("BRAVO–BẮC"));
        Assert.That(journal.IsMapMilitaryDestinationVisible, Is.True,
            "Fragment 2 must replace the active hospital destination with the military-base marker.");

        Type minimapType = Type.GetType("MinimapController, Assembly-CSharp");
        Assert.That(minimapType, Is.Not.Null);
        Component minimap = UnityEngine.Object.FindFirstObjectByType(minimapType,
            FindObjectsInactive.Include) as Component;
        Assert.That(minimap, Is.Not.Null);
        Assert.That(minimap.GetComponent<Canvas>().enabled, Is.False,
            "Unlocking the full mission map must not enable the corner minimap.");

        // B4: the debug approach completes the three temporary school clue
        // states and exits the roof trigger without touching LootContainers.
        advanceBase.Invoke(military, null);
        Assert.That(ReadProperty(military, "CurrentPhase").ToString(), Is.EqualTo("Investigating"));
        Assert.That((int)ReadProperty(military, "SchoolClueCount"), Is.EqualTo(3));
        Assert.That(ReadBool(military, "HasExitedSchoolAfterClues"), Is.True);

        // In Solo the unanimous snapshot contains one voter. The debug
        // confirmation opens that production vote and submits the local yes.
        MethodInfo confirmFinale = militaryType.GetMethod("DebugConfirmMilitaryFinale",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(confirmFinale, Is.Not.Null);
        confirmFinale.Invoke(military, null);
        Assert.That(ReadProperty(main, "LockedEscapeRoute").ToString(), Is.EqualTo("MilitaryEvacuation"));
        GameObject cinematicClone = null;
        float cloneDeadline = Time.realtimeSinceStartup + 4f;
        while (cinematicClone == null && Time.realtimeSinceStartup < cloneDeadline)
        {
            cinematicClone = GameObject.Find("Military Cinematic Host Visual");
            yield return null;
        }
        Assert.That(cinematicClone, Is.Not.Null);
        Type cinematicLightType = Type.GetType("MilitaryCinematicVisionLight, Assembly-CSharp");
        Assert.That(cinematicLightType, Is.Not.Null);
        Assert.That(cinematicClone.GetComponent(cinematicLightType), Is.Not.Null,
            "The Host clone must carry the cinematic light proxy used by fog and flashlight presentation.");
        Type playerMovementType = Type.GetType("PlayerMovement, Assembly-CSharp");
        Assert.That(playerMovementType, Is.Not.Null);
        Component localMovement = playerMovementType.GetField("LocalPlayerInstance",
            BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as Component;
        Assert.That(localMovement, Is.Not.Null);
        FieldInfo presentationSuppressed = playerMovementType.GetField("cinematicPresentationSuppressed",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That((bool)presentationSuppressed.GetValue(localMovement), Is.True,
            "The real Host must not emit footsteps while its cinematic clone is active.");
        Renderer[] realHostRenderers = localMovement.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < realHostRenderers.Length; i++)
        {
            Assert.That(realHostRenderers[i].enabled, Is.False,
                "The real Host visual must remain hidden for the whole cinematic.");
            Assert.That(realHostRenderers[i].forceRenderingOff, Is.True);
        }
        Type playerNameTagType = Type.GetType("PlayerNameTag, Assembly-CSharp");
        Component playerNameTag = localMovement.GetComponent(playerNameTagType);
        Component nameText = playerNameTagType.GetField("nameText")?.GetValue(playerNameTag) as Component;
        Assert.That(nameText, Is.Not.Null);
        Assert.That(nameText.gameObject.activeSelf, Is.False,
            "The stationary real Host nametag must be hidden during the cinematic.");
        Type playerVisionType = Type.GetType("PlayerVision, Assembly-CSharp");
        Behaviour realHostVision = localMovement.GetComponent(playerVisionType) as Behaviour;
        Assert.That(realHostVision, Is.Not.Null);
        Assert.That(realHostVision.enabled, Is.False,
            "The stationary real Host Light2D/vision service must be suppressed during the cinematic.");
        Type fogVisionType = Type.GetType("FogVisionController, Assembly-CSharp");
        Assert.That(fogVisionType, Is.Not.Null);
        Component fogVision = UnityEngine.Object.FindFirstObjectByType(fogVisionType) as Component;
        Assert.That(fogVision, Is.Not.Null);
        FieldInfo fogCinematicTarget = fogVisionType.GetField("cinematicVisionTarget",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(fogCinematicTarget.GetValue(fogVision), Is.SameAs(cinematicClone.transform),
            "Fog of war must follow the cinematic visual instead of the stationary real Host.");
        // The cinematic intentionally uses the serialized gameplay speeds
        // (Player.prefab: walk 0.7, run 1.2), so its full authored route takes
        // roughly 30 seconds rather than the former fast-glide duration.
        float cinematicDeadline = Time.realtimeSinceStartup + 45f;
        while (ReadProperty(military, "CurrentPhase").ToString() != "SiegeAndRepair" &&
               Time.realtimeSinceStartup < cinematicDeadline)
            yield return null;
        Assert.That(ReadProperty(military, "CurrentPhase").ToString(), Is.EqualTo("SiegeAndRepair"));
        Transform closedGate = FindInactiveTransform("CongRao");
        Assert.That(closedGate, Is.Not.Null);
        BoxCollider2D closedGateCollider = closedGate.Find("CongRao Collider [RUNTIME]")
            ?.GetComponent<BoxCollider2D>();
        Assert.That(closedGateCollider, Is.Not.Null);
        Assert.That(closedGateCollider.enabled, Is.True,
            "The authored gate must become a physical obstacle when the cinematic closes it.");
        Assert.That(closedGateCollider.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("Obstacle")));
        Assert.That((float)ReadProperty(military, "GateMaxHealth"), Is.EqualTo(5000f).Within(0.01f),
            "Legacy Main.unity serialization must not lower the canonical gate HP back to 1,000.");
        float gateHealthBefore = (float)ReadProperty(military, "GateCurrentHealth");
        Type gateType = Type.GetType("MilitaryGateController, Assembly-CSharp");
        Assert.That(gateType, Is.Not.Null);
        MethodInfo tryGateHit = gateType.GetMethod("TryApplyHordeHit");
        Assert.That(tryGateHit, Is.Not.Null);
        for (int i = 0; i < 100; i++) tryGateHit.Invoke(closedGate.GetComponent(gateType), null);
        float gateHealthAfter = (float)ReadProperty(military, "GateCurrentHealth");
        Assert.That(gateHealthBefore - gateHealthAfter, Is.LessThanOrEqualTo(48.01f),
            "Even 100 simultaneous attack requests must be capped to four 12-HP beats per second.");
        Assert.That(gateHealthAfter, Is.GreaterThan(4500f),
            "A 5,000 HP gate must not collapse at siege startup.");

        // B6: the no-loot shortcut completes the same canonical five-action
        // police-car state used by the real repair minigame.
        advanceBase.Invoke(military, null);
        Assert.That(ReadBool(military, "ArePoliceCarRepairsComplete"), Is.True);
        Assert.That((float)ReadProperty(military, "PoliceCarOverallRepairProgress"), Is.EqualTo(100f));
        Assert.That(ReadProperty(military, "CurrentPhase").ToString(), Is.EqualTo("ReadyToEscape"));

        // B7: extraction and authoritative journal completion.
        advanceBase.Invoke(military, null);
        Assert.That(ReadProperty(military, "CurrentPhase").ToString(), Is.EqualTo("Escaped"));
        float journalDeadline = Time.realtimeSinceStartup + 5f;
        while (journal != null && !journal.IsMainQuestComplete && Time.realtimeSinceStartup < journalDeadline)
            yield return null;
        Assert.That(journal, Is.Not.Null);
        Assert.That(journal.IsMainQuestComplete, Is.True,
            "Journal must complete Route B only after military extraction.");
        Assert.That(journal.MilitaryPresentationPhase, Is.EqualTo(RouteBMilitaryPresentationPhase.Escaped));
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

    private static Button FindInactiveButton(string objectName)
    {
        Transform transform = FindInactiveTransform(objectName);
        return transform != null ? transform.GetComponent<Button>() : null;
    }

    private static IEnumerator ShutdownExistingRunners()
    {
        Type runnerType = Type.GetType("Fusion.NetworkRunner, Fusion.Runtime");
        if (runnerType == null) yield break;
        UnityEngine.Object[] runners = Resources.FindObjectsOfTypeAll(runnerType);
        Type shutdownReasonType = Type.GetType("Fusion.ShutdownReason, Fusion.Runtime");
        MethodInfo shutdown = shutdownReasonType == null ? null : runnerType.GetMethod("Shutdown",
            new[] { typeof(bool), shutdownReasonType, typeof(bool) });
        for (int i = 0; i < runners.Length; i++)
        {
            if (runners[i] == null || shutdown == null) continue;
            object ok = Enum.Parse(shutdownReasonType, "Ok");
            Task task = shutdown.Invoke(runners[i], new[] { (object)true, ok, false }) as Task;
            float deadline = Time.realtimeSinceStartup + 10f;
            while (task != null && !task.IsCompleted && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        Type menuType = Type.GetType("AutoMainMenuManager, Assembly-CSharp");
        if (menuType != null)
        {
            UnityEngine.Object[] menus = Resources.FindObjectsOfTypeAll(menuType);
            for (int i = 0; i < menus.Length; i++)
                if (menus[i] is Component menu && menu != null)
                    UnityEngine.Object.Destroy(menu.transform.root.gameObject);
        }

        GameObject[] persistentObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < persistentObjects.Length; i++)
            if (persistentObjects[i] != null && persistentObjects[i].name == "AutoMenuCanvas")
                UnityEngine.Object.Destroy(persistentObjects[i]);

        yield return null;
    }

    private static Button FindInactiveButtonUnder(Component owner, string objectName)
    {
        if (owner == null) return null;
        Transform root = owner.transform;
        FieldInfo canvasField = owner.GetType().GetField("canvasObject",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (canvasField?.GetValue(owner) is GameObject canvasObject && canvasObject != null)
            root = canvasObject.transform;
        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
            if (buttons[i] != null && buttons[i].name == objectName) return buttons[i];
        return null;
    }

    private static RectTransform AssertHotspotLayout(string partId, Vector2 expectedPosition,
        Vector2 expectedSize)
    {
        RectTransform hotspot = FindInactiveButton("Vehicle Part Hotspot " + partId)
            ?.GetComponent<RectTransform>();
        Assert.That(hotspot, Is.Not.Null, "Missing hotspot for " + partId + ".");
        Assert.That(hotspot.anchoredPosition.x, Is.EqualTo(expectedPosition.x).Within(0.01f));
        Assert.That(hotspot.anchoredPosition.y, Is.EqualTo(expectedPosition.y).Within(0.01f));
        Assert.That(hotspot.sizeDelta.x, Is.EqualTo(expectedSize.x).Within(0.01f));
        Assert.That(hotspot.sizeDelta.y, Is.EqualTo(expectedSize.y).Within(0.01f));
        return hotspot;
    }

    private static Transform FindInactiveTransform(string objectName)
    {
        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && candidate.name == objectName && candidate.gameObject.scene.IsValid())
                return candidate;
        }
        return null;
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
