using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Connects real Main-scene interactions to the pre-military journal, live
/// world map and office reveal. MainQuestManager remains authoritative for the
/// later office / military-zone sequence.
/// </summary>
[DisallowMultipleComponent]
public sealed class PreMilitaryQuestRuntimeBridge : MonoBehaviour
{
    public static PreMilitaryQuestRuntimeBridge Instance { get; private set; }

    [SerializeField] private QuestFlowUIPrototype questUI;
    [SerializeField] private bool showClueMessages = true;
    [SerializeField] private float revealTravelDuration = 1.35f;
    [SerializeField] private float revealHoldDuration = 1.65f;
    [SerializeField] private float revealReturnDuration = 0.9f;
    [SerializeField, Range(4, 12)] private int maxSearchZoneHouses = 6;
    [SerializeField, Range(3f, 15f)] private float searchZoneWorldPadding = 7f;
    [SerializeField, Range(0, 48)] private int searchZoneVisualDownCells = 24;
    [SerializeField, Range(6f, 24f)] private float officeSearchWorldRadius = 12f;

    [Header("Soft search-zone guidance")]
    [SerializeField, Min(0f)] private float outsideWarningDistance = 2f;
    [SerializeField, Min(0f)] private float outsideWarningDelay = 1.25f;
    [SerializeField, Min(0.05f)] private float outsideWarningFadeIn = 0.65f;
    [SerializeField, Min(0.1f)] private float outsideWarningHold = 2.4f;
    [SerializeField, Min(0.05f)] private float outsideWarningFadeOut = 0.9f;
    [SerializeField, Min(0f)] private float outsideWarningCooldown = 9f;

    private Transform officeTarget;
    private Transform configuredPlayerTarget;
    private ProjectZomboidMapRasterizer.Result rasterMap;
    private Coroutine officeRevealRoutine;
    private bool lastModalState;
    private bool cinematicActive;
    private bool searchZoneConfigured;
    private readonly HashSet<string> activeSearchHouseIds = new HashSet<string>();
    private Rect searchZoneMapRect;
    private bool hasSearchZoneMapRect;
    private string configuredZoneSignature;
    private int lastAuthoritativeSnapshotSignature = int.MinValue;
    private bool hasAppliedInitialAuthoritativeSnapshot;
    private float outsideSince = -1f;
    private float outsideWarningAlpha;
    private float outsideWarningVisibleUntil;
    private float nextOutsideWarningTime;
    private Vector2 outsideGuidanceWorldTarget;
    private bool hasOutsideGuidanceTarget;
    private bool guidanceTargetsOffice;
    private QuestMapRevealTuningTool revealTuningTool;
    private int lastRevealTuningSignature = int.MinValue;

    public int ActiveSearchHouseCount => activeSearchHouseIds.Count;
    public Transform ConfiguredPlayerTarget => configuredPlayerTarget;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (questUI == null)
            questUI = GetComponent<QuestFlowUIPrototype>();
        if (questUI == null)
            questUI = FindFirstObjectByType<QuestFlowUIPrototype>(FindObjectsInactive.Include);
        questUI?.SetCarRepairInventoryQuery(HasArrivalCarRepairItem);

        ResolveMainSceneReferences();
        ConfigureLiveMap();
    }

    private void OnEnable()
    {
        if (questUI != null)
            questUI.MapFragment1Acquired += HandleMapFragment1Acquired;
    }

    private void Start()
    {
        // PlayerMovement.LocalPlayerInstance is commonly assigned after Awake.
        ConfigureLiveMap();
        SyncModalUI(true);
    }

    private void Update()
    {
        Transform player = GetLocalPlayerTarget();
        if (player != null && player != configuredPlayerTarget)
            ConfigureLiveMap();

        MainQuestManager manager = MainQuestManager.Instance;
        if (player != null && manager != null && manager.IsNetworkReady)
        {
            if (!manager.IsNeighborhoodConfigured && manager.HasStateAuthority && manager.IsArrivalCarInspected)
                TryInitializeAuthoritativeSearchZone(player, manager);
            if (manager.IsNeighborhoodConfigured)
            {
                ConfigureSearchZoneFromAuthority(manager);
                SyncAuthoritativeQuestSnapshot(manager);
            }
        }
        else if (player != null && manager == null && !searchZoneConfigured)
        {
            // Isolated preview/test scenes without Fusion keep a local fallback.
            ConfigureLocalFallbackSearchZone(player);
        }

        if (rasterMap != null && player != null)
            questUI?.SetRasterMapPlayerPosition(rasterMap.WorldToNormalized(player.position));
        ApplyLiveRevealTuning();
        if (player != null)
            UpdateOutsideSearchZoneWarning(player.position, manager);

        SyncModalUI(false);
    }

    private void OnDisable()
    {
        if (questUI != null)
            questUI.MapFragment1Acquired -= HandleMapFragment1Acquired;
        AutoUIManager.Instance?.SetQuestOverlayOpen(false);
    }

    private void OnDestroy()
    {
        questUI?.SetCarRepairInventoryQuery(null);
        if (rasterMap != null && rasterMap.Texture != null)
            Destroy(rasterMap.Texture);
        if (Instance == this) Instance = null;
    }

    private bool HasArrivalCarRepairItem(string[] inventoryNames)
    {
        if (inventoryNames == null) return false;
        Transform player = GetLocalPlayerTarget();
        InventorySystem inventory = player != null ? player.GetComponent<InventorySystem>() : null;
        if (inventory == null) return false;

        for (int i = 0; i < inventoryNames.Length; i++)
            if (!string.IsNullOrWhiteSpace(inventoryNames[i]) && inventory.HasItemNamed(inventoryNames[i]))
                return true;
        return false;
    }

    public static void NotifyContainerOpened(LootContainer container)
    {
        Instance?.HandleContainerOpened(container);
    }

    public static void NotifyRouteClueLooted(string clueId, string displayName)
    {
        if (Instance == null || Instance.questUI == null || string.IsNullOrWhiteSpace(clueId)) return;
        if (!QuestRouteClueItemCatalog.TryGetKind(clueId, out QuestRouteClueKind kind))
        {
            AutoChatManager.Instance?.AddMessage("MANH MỐI", "Đã nhặt: " + displayName + ".");
            return;
        }

        MainQuestManager manager = MainQuestManager.Instance;
        bool usesAuthoritativeProgress = manager != null && manager.IsNetworkReady && manager.IsNeighborhoodConfigured;
        if (!usesAuthoritativeProgress)
            Instance.questUI.RegisterRouteClueForPreview(clueId, deferCompletion: true);
        Instance.questUI.ShowRouteClueReading(
            QuestRouteClueItemCatalog.GetDisplayName(kind),
            QuestRouteClueItemCatalog.GetReadingText(kind),
            QuestRouteClueItemCatalog.GetInferenceText(kind));
        if (!Instance.showClueMessages)
            Instance.questUI.CloseRouteClueReading();

        Instance.SyncModalUI(true);
    }

    public static void NotifyOfficeEntered(Component officeChild)
    {
        if (Instance == null || officeChild == null)
            return;

        if (!QuestLocationIdentity.TryResolve(officeChild, out QuestLocationIdentity identity) ||
            identity.LocationType != QuestLocationType.PurpleOffice)
            return;

        MainQuestManager manager = MainQuestManager.Instance;
        if (manager == null || !manager.IsNetworkReady)
            Instance.questUI?.RegisterOfficeDiscoveredForPreview();
    }

    public static void NotifyMapFragment2Found()
    {
        if (Instance == null || Instance.questUI == null)
            return;

        MainQuestManager manager = MainQuestManager.Instance;
        if (manager != null && manager.IsNetworkReady)
            return;

        Instance.questUI.RegisterOfficeDiscoveredForPreview();
        Instance.questUI.RegisterOfficeMapCabinetOpenedForPreview();
        Instance.questUI.RegisterMapFragment2AddedToInventoryForPreview();
    }

    private void ResolveMainSceneReferences()
    {
        foreach (QuestLocationIdentity identity in FindObjectsByType<QuestLocationIdentity>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (identity.LocationType == QuestLocationType.PurpleOffice && identity.LocationId == "OFFICE_PURPLE_MAIN")
                officeTarget = identity.transform;
        }

        if (rasterMap == null)
        {
            GameObject mapRoot = GameObject.Find("Map");
            rasterMap = ProjectZomboidMapRasterizer.Build(mapRoot);
        }

        EnsureRevealTuningTool();
    }

    private void EnsureRevealTuningTool()
    {
        if (revealTuningTool != null) return;

        revealTuningTool = GetComponentInChildren<QuestMapRevealTuningTool>(true);
        if (revealTuningTool != null) return;

        GameObject tuningObject = new GameObject("Quest Map Reveal Tuning Tool [PLAY MODE]");
        tuningObject.transform.SetParent(transform, false);
        revealTuningTool = tuningObject.AddComponent<QuestMapRevealTuningTool>();
    }

    private void ConfigureLiveMap()
    {
        if (questUI == null)
            return;

        if (officeTarget == null || rasterMap == null)
            ResolveMainSceneReferences();

        configuredPlayerTarget = GetLocalPlayerTarget();
        if (rasterMap != null)
        {
            Vector2 officePosition = rasterMap.WorldToNormalized(officeTarget != null ? officeTarget.position : Vector3.zero);
            Vector2 playerPosition = rasterMap.WorldToNormalized(configuredPlayerTarget != null ? configuredPlayerTarget.position : Vector3.zero);
            questUI.ConfigureRasterMap(rasterMap.Texture, officePosition, playerPosition);
            if (officeTarget != null)
            {
                if (revealTuningTool != null)
                {
                    Rect officeReveal = revealTuningTool.AfterQuestRect;
                    questUI.ConfigureOfficeSearchArea(officeReveal.min, officeReveal.max);
                }
                else
                {
                    Vector2 radius = Vector2.one * officeSearchWorldRadius;
                    questUI.ConfigureOfficeSearchArea(
                        rasterMap.WorldToNormalized((Vector2)officeTarget.position - radius),
                        rasterMap.WorldToNormalized((Vector2)officeTarget.position + radius));
                }
            }
        }
    }

    private void TryInitializeAuthoritativeSearchZone(Transform player, MainQuestManager manager)
    {
        Vector2 anchor = GetSharedQuestAnchor(player.position);
        List<QuestLocationIdentity> candidates = FindSearchHouseCandidates(anchor);
        int count = Mathf.Min(Mathf.Min(maxSearchZoneHouses, MainQuestManager.MaximumSearchHouses), candidates.Count);
        if (count < PreMilitaryQuestProgress.RequiredDistinctHouses) return;

        List<string> ids = new List<string>(count);
        for (int i = 0; i < count; i++) ids.Add(candidates[i].LocationId);
        manager.TryInitializeNeighborhood(ids);
    }

    private void ConfigureSearchZoneFromAuthority(MainQuestManager manager)
    {
        List<string> ids = new List<string>(manager.SearchHouseCount);
        for (int i = 0; i < manager.SearchHouseCount; i++) ids.Add(manager.GetSearchHouseId(i));
        string signature = string.Join("|", ids);
        if (searchZoneConfigured && configuredZoneSignature == signature) return;

        Dictionary<string, QuestLocationIdentity> locationsById = new Dictionary<string, QuestLocationIdentity>();
        foreach (QuestLocationIdentity location in FindObjectsByType<QuestLocationIdentity>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (location != null && location.HasValidId)
                locationsById[location.LocationId] = location;
        }

        List<QuestLocationIdentity> selected = new List<QuestLocationIdentity>(ids.Count);
        for (int i = 0; i < ids.Count; i++)
        {
            if (locationsById.TryGetValue(ids[i], out QuestLocationIdentity location) && location != null)
                selected.Add(location);
        }
        if (selected.Count != ids.Count) return;

        ConfigureSelectedSearchHouses(selected);
        configuredZoneSignature = signature;
    }

    private void ConfigureLocalFallbackSearchZone(Transform player)
    {
        List<QuestLocationIdentity> candidates = FindSearchHouseCandidates(GetSharedQuestAnchor(player.position));
        int count = Mathf.Min(Mathf.Min(maxSearchZoneHouses, MainQuestManager.MaximumSearchHouses), candidates.Count);
        if (count < PreMilitaryQuestProgress.RequiredDistinctHouses) return;
        ConfigureSelectedSearchHouses(candidates.GetRange(0, count));
        configuredZoneSignature = "LOCAL_FALLBACK";
        AutoChatManager.Instance?.AddMessage("NHIỆM VỤ", $"Đã khoanh vùng tìm kiếm gồm {count} căn nhà.");
    }

    private static List<QuestLocationIdentity> FindSearchHouseCandidates(Vector2 anchor)
    {
        List<QuestLocationIdentity> candidates = new List<QuestLocationIdentity>();
        foreach (QuestLocationIdentity identity in FindObjectsByType<QuestLocationIdentity>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (identity.LocationType == QuestLocationType.ResidentialHouse && identity.HasValidId &&
                identity.GetComponentInChildren<LootContainer>(true) != null)
                candidates.Add(identity);
        }
        candidates.Sort((a, b) =>
        {
            float aDistance = ((Vector2)a.transform.position - anchor).sqrMagnitude;
            float bDistance = ((Vector2)b.transform.position - anchor).sqrMagnitude;
            int distanceOrder = aDistance.CompareTo(bDistance);
            return distanceOrder != 0 ? distanceOrder : string.CompareOrdinal(a.LocationId, b.LocationId);
        });
        return candidates;
    }

    private static Vector2 GetSharedQuestAnchor(Vector3 fallback)
    {
        Transform[] spawnPoints = HostModeSpawner.Instance != null ? HostModeSpawner.Instance.spawnPoints : null;
        if (spawnPoints == null || spawnPoints.Length == 0) return fallback;

        Vector2 total = Vector2.zero;
        int validCount = 0;
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i] == null) continue;
            total += (Vector2)spawnPoints[i].position;
            validCount++;
        }
        return validCount > 0 ? total / validCount : fallback;
    }

    private void ConfigureSelectedSearchHouses(List<QuestLocationIdentity> selected)
    {
        if (selected == null || selected.Count == 0 || rasterMap == null || questUI == null) return;

        activeSearchHouseIds.Clear();
        Vector2 worldMin = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 worldMax = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < selected.Count; i++)
        {
            QuestLocationIdentity house = selected[i];
            activeSearchHouseIds.Add(house.LocationId);
            worldMin = Vector2.Min(worldMin, house.transform.position);
            worldMax = Vector2.Max(worldMax, house.transform.position);
        }

        // All configured spawn points belong to the opening search district so
        // a teammate never receives an outside-area warning immediately on spawn.
        Transform[] spawnPoints = HostModeSpawner.Instance != null ? HostModeSpawner.Instance.spawnPoints : null;
        if (spawnPoints != null)
        {
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (spawnPoints[i] == null) continue;
                Vector2 spawnPosition = spawnPoints[i].position;
                worldMin = Vector2.Min(worldMin, spawnPosition);
                worldMax = Vector2.Max(worldMax, spawnPosition);
            }
        }

        worldMin -= Vector2.one * searchZoneWorldPadding;
        worldMax += Vector2.one * searchZoneWorldPadding;
        Vector2 normalizedA = rasterMap.WorldToNormalized(worldMin);
        Vector2 normalizedB = rasterMap.WorldToNormalized(worldMax);
        Vector2 mapMin = Vector2.Min(normalizedA, normalizedB);
        Vector2 mapMax = Vector2.Max(normalizedA, normalizedB);
        if (revealTuningTool != null)
        {
            Rect neighborhoodReveal = revealTuningTool.BeforeQuestRect;
            mapMin = neighborhoodReveal.min;
            mapMax = neighborhoodReveal.max;
        }
        else
        {
            ExpandSearchZoneMapBoundsTowardRoad(ref mapMin, ref mapMax, rasterMap.Size);
            ShiftSearchZoneDownToRoad(ref mapMin, ref mapMax, rasterMap.Size, searchZoneVisualDownCells);
        }
        searchZoneMapRect = Rect.MinMaxRect(mapMin.x, mapMin.y, mapMax.x, mapMax.y);
        hasSearchZoneMapRect = true;
        questUI.ConfigureSearchZone(mapMin, mapMax, selected.Count);

        searchZoneConfigured = true;
        lastRevealTuningSignature = revealTuningTool != null
            ? revealTuningTool.LayoutSignature
            : int.MinValue;
    }

    private void ApplyLiveRevealTuning()
    {
        if (revealTuningTool == null || questUI == null) return;

        int signature = revealTuningTool.LayoutSignature;
        if (signature == lastRevealTuningSignature) return;
        lastRevealTuningSignature = signature;

        Rect officeReveal = revealTuningTool.AfterQuestRect;
        questUI.ConfigureOfficeSearchArea(officeReveal.min, officeReveal.max);

        if (!searchZoneConfigured || activeSearchHouseIds.Count == 0) return;
        Rect neighborhoodReveal = revealTuningTool.BeforeQuestRect;
        searchZoneMapRect = neighborhoodReveal;
        hasSearchZoneMapRect = true;
        questUI.ConfigureSearchZone(neighborhoodReveal.min, neighborhoodReveal.max, activeSearchHouseIds.Count);
    }

    private static void ExpandSearchZoneMapBoundsTowardRoad(
        ref Vector2 mapMin, ref Vector2 mapMax, Vector2Int rasterSize)
    {
        float horizontalCells = (mapMax.x - mapMin.x) * Mathf.Max(1, rasterSize.x - 1);
        float verticalCells = (mapMax.y - mapMin.y) * Mathf.Max(1, rasterSize.y - 1);
        float sideLength = Mathf.Max(horizontalCells, verticalCells);

        // The map starts at a 90-degree rotation, so positive map Y is its visual
        // right side. Preserve the spawn-side edge and extend toward the road in
        // front of the yellow house. Cell counts (rather than normalized values)
        // keep the highlighted area visually square on non-square map textures.
        if (verticalCells < sideLength)
        {
            float targetHeight = sideLength / Mathf.Max(1, rasterSize.y - 1);
            mapMax.y = Mathf.Min(1f, mapMin.y + targetHeight);
            mapMin.y = Mathf.Max(0f, mapMax.y - targetHeight);
        }

        // Defensive fallback for alternate layouts: keep the quest anchor centred
        // on the other axis if it ever becomes the shorter dimension.
        if (horizontalCells < sideLength)
        {
            float targetWidth = sideLength / Mathf.Max(1, rasterSize.x - 1);
            float centre = (mapMin.x + mapMax.x) * 0.5f;
            mapMin.x = Mathf.Max(0f, centre - targetWidth * 0.5f);
            mapMax.x = Mathf.Min(1f, mapMin.x + targetWidth);
            mapMin.x = Mathf.Max(0f, mapMax.x - targetWidth);
        }
    }

    private static void ShiftSearchZoneDownToRoad(
        ref Vector2 mapMin, ref Vector2 mapMax, Vector2Int rasterSize, int cellOffset)
    {
        if (cellOffset <= 0) return;

        // At the default quarter-turn, decreasing map X moves the highlighted
        // square visually downward. Preserve its size while aligning its lower
        // edge with the road visible beneath the opening neighborhood.
        float height = mapMax.x - mapMin.x;
        float normalizedOffset = cellOffset / (float)Mathf.Max(1, rasterSize.x - 1);
        mapMin.x = Mathf.Max(0f, mapMin.x - normalizedOffset);
        mapMax.x = Mathf.Min(1f, mapMin.x + height);
        mapMin.x = Mathf.Max(0f, mapMax.x - height);
    }

    private void SyncAuthoritativeQuestSnapshot(MainQuestManager manager)
    {
        bool mapFragment2Found = manager.IsCityMapUnlocked;
        int signature = manager.SearchedHouseMask;
        signature = signature * 397 ^ manager.RouteClueMask;
        signature = signature * 397 ^ (manager.IsOfficeDiscovered ? 1 : 0);
        signature = signature * 397 ^ (mapFragment2Found ? 1 : 0);
        signature = signature * 397 ^ (manager.IsArrivalCarInspected ? 1 : 0);
        signature = signature * 397 ^ manager.ArrivalCarRepairMask;
        signature = signature * 397 ^ (manager.IsArrivalCarRepaired ? 1 : 0);
        signature = signature * 397 ^ manager.LockedEscapeRouteValue;
        if (signature == lastAuthoritativeSnapshotSignature) return;

        questUI?.ApplyAuthoritativeSnapshot(manager.SearchedHouseMask, manager.RouteClueMask,
            manager.IsOfficeDiscovered, mapFragment2Found, mapFragment2Found,
            hasAppliedInitialAuthoritativeSnapshot, manager.IsArrivalCarInspected, manager.IsArrivalCarRepaired,
            manager.ArrivalCarRepairMask, manager.LockedEscapeRoute);
        lastAuthoritativeSnapshotSignature = signature;
        hasAppliedInitialAuthoritativeSnapshot = true;
    }

    private void UpdateOutsideSearchZoneWarning(Vector2 playerPosition, MainQuestManager manager)
    {
        bool searchNeighborhood = manager != null && manager.IsNetworkReady
            ? manager.CurrentStage == MainQuestManager.QuestStage.SearchNeighborhood
            : questUI != null && !questUI.IsHouseSearchComplete;
        bool locateOffice = manager != null && manager.IsNetworkReady &&
                            manager.CurrentStage == MainQuestManager.QuestStage.LocateOffice;
        bool outside = false;

        if (searchNeighborhood && hasSearchZoneMapRect && rasterMap != null)
        {
            Vector2 playerMapPosition = rasterMap.WorldToNormalized(playerPosition);
            Vector2 closestMapPosition = ClosestPointInRect(playerMapPosition, searchZoneMapRect);
            outsideGuidanceWorldTarget = rasterMap.NormalizedToWorld(closestMapPosition);
            hasOutsideGuidanceTarget = true;
            guidanceTargetsOffice = false;
            outside = Vector2.Distance(playerPosition, outsideGuidanceWorldTarget) >= outsideWarningDistance;
        }
        else if (locateOffice && officeTarget != null)
        {
            outsideGuidanceWorldTarget = officeTarget.position;
            hasOutsideGuidanceTarget = true;
            guidanceTargetsOffice = true;
            float distanceBeyondArea = Vector2.Distance(playerPosition, outsideGuidanceWorldTarget) -
                                       officeSearchWorldRadius;
            outside = distanceBeyondArea >= outsideWarningDistance;
        }
        else if (outsideWarningAlpha <= 0.001f)
        {
            hasOutsideGuidanceTarget = false;
        }

        float now = Time.unscaledTime;

        if (!outside)
        {
            outsideSince = -1f;
            outsideWarningVisibleUntil = 0f;
        }
        else
        {
            if (outsideSince < 0f) outsideSince = now;
            if (now - outsideSince >= outsideWarningDelay && now >= nextOutsideWarningTime)
            {
                outsideWarningVisibleUntil = now + outsideWarningHold;
                nextOutsideWarningTime = outsideWarningVisibleUntil + outsideWarningCooldown;
            }
        }

        float targetAlpha = outside && now < outsideWarningVisibleUntil ? 1f : 0f;
        float duration = targetAlpha > outsideWarningAlpha ? outsideWarningFadeIn : outsideWarningFadeOut;
        outsideWarningAlpha = Mathf.MoveTowards(outsideWarningAlpha, targetAlpha,
            Time.unscaledDeltaTime / Mathf.Max(0.05f, duration));
    }

    private static Vector2 ClosestPointInRect(Vector2 point, Rect rect)
    {
        return new Vector2(Mathf.Clamp(point.x, rect.xMin, rect.xMax),
            Mathf.Clamp(point.y, rect.yMin, rect.yMax));
    }

    private void OnGUI()
    {
        if (outsideWarningAlpha <= 0.001f || cinematicActive || TutorialSession.IsActive) return;

        int previousDepth = GUI.depth;
        Color previousColor = GUI.color;
        GUI.depth = -850;
        float width = Mathf.Min(390f, Screen.width - 32f);
        Rect panel = new Rect(18f, 92f, width, 66f);

        GUI.color = new Color(0.025f, 0.035f, 0.04f, outsideWarningAlpha * 0.88f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = new Color(1f, 0.68f, 0.16f, outsideWarningAlpha);
        GUI.DrawTexture(new Rect(panel.x, panel.y, 4f, panel.height), Texture2D.whiteTexture);

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        GUIStyle detailStyle = new GUIStyle(titleStyle)
        {
            fontSize = 12,
            fontStyle = FontStyle.Normal
        };
        GUI.color = Color.white;
        titleStyle.normal.textColor = new Color(1f, 0.78f, 0.31f, outsideWarningAlpha);
        detailStyle.normal.textColor = new Color(0.9f, 0.93f, 0.92f, outsideWarningAlpha);
        GUI.Label(new Rect(panel.x + 18f, panel.y + 7f, panel.width - 28f, 25f),
            guidanceTargetsOffice ? "NGOÀI VÙNG NGHI VẤN" : "NGOÀI VÙNG TÌM KIẾM", titleStyle);
        GUI.Label(new Rect(panel.x + 18f, panel.y + 31f, panel.width - 28f, 27f),
            "Đi theo marker để quay lại mục tiêu • Bản đồ [M].", detailStyle);

        if (hasOutsideGuidanceTarget)
            DrawReturnDirectionMarker(outsideGuidanceWorldTarget);

        GUI.color = previousColor;
        GUI.depth = previousDepth;
    }

    private void DrawReturnDirectionMarker(Vector2 worldTarget)
    {
        Camera sceneCamera = Camera.main;
        if (sceneCamera == null) return;

        Vector3 screen3 = sceneCamera.WorldToScreenPoint(worldTarget);
        Vector2 targetGui = new Vector2(screen3.x, Screen.height - screen3.y);
        const float horizontalMargin = 58f;
        const float topMargin = 78f;
        const float bottomMargin = 58f;
        bool onScreen = screen3.z > 0f && targetGui.x >= horizontalMargin &&
                        targetGui.x <= Screen.width - horizontalMargin && targetGui.y >= topMargin &&
                        targetGui.y <= Screen.height - bottomMargin;

        Vector2 markerPosition;
        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 direction = targetGui - center;
        if (screen3.z < 0f) direction = -direction;
        if (direction.sqrMagnitude < 0.001f) direction = Vector2.up;

        if (onScreen)
        {
            markerPosition = targetGui;
        }
        else
        {
            float availableX = Screen.width * 0.5f - horizontalMargin;
            float availableY = Screen.height * 0.5f - bottomMargin;
            float scaleX = availableX / Mathf.Max(0.001f, Mathf.Abs(direction.x));
            float scaleY = availableY / Mathf.Max(0.001f, Mathf.Abs(direction.y));
            markerPosition = center + direction * Mathf.Min(scaleX, scaleY);
            markerPosition.y = Mathf.Clamp(markerPosition.y, topMargin, Screen.height - bottomMargin);
        }

        Matrix4x4 previousMatrix = GUI.matrix;
        GUIStyle arrowStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 34,
            fontStyle = FontStyle.Bold
        };
        arrowStyle.normal.textColor = new Color(1f, 0.72f, 0.18f, outsideWarningAlpha);
        if (onScreen)
        {
            GUI.Label(new Rect(markerPosition.x - 24f, markerPosition.y - 24f, 48f, 48f), "◆", arrowStyle);
        }
        else
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            GUIUtility.RotateAroundPivot(angle, markerPosition);
            GUI.Label(new Rect(markerPosition.x - 24f, markerPosition.y - 24f, 48f, 48f), "▶", arrowStyle);
            GUI.matrix = previousMatrix;
        }

        float distance = PlayerMovement.LocalPlayerInstance != null
            ? Vector2.Distance(PlayerMovement.LocalPlayerInstance.transform.position, worldTarget)
            : 0f;
        GUIStyle labelStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 12,
            fontStyle = FontStyle.Bold
        };
        labelStyle.normal.textColor = new Color(1f, 0.9f, 0.63f, outsideWarningAlpha);
        float labelX = Mathf.Clamp(markerPosition.x - 92f, 8f, Screen.width - 192f);
        float labelY = Mathf.Clamp(markerPosition.y + 27f, 48f, Screen.height - 36f);
        GUI.color = new Color(1f, 1f, 1f, outsideWarningAlpha);
        GUI.Box(new Rect(labelX, labelY, 184f, 28f),
            $"QUAY LẠI MỤC TIÊU  •  {distance:0} m", labelStyle);
        GUI.matrix = previousMatrix;
        GUI.color = Color.white;
    }

    private Transform GetLocalPlayerTarget()
    {
        return PlayerMovement.LocalPlayerInstance != null
            ? PlayerMovement.LocalPlayerInstance.transform
            : null;
    }

    private void SyncModalUI(bool force)
    {
        bool modal = cinematicActive || (questUI != null && questUI.IsQuestOverlayOpen);
        if (!force && modal == lastModalState)
            return;

        lastModalState = modal;
        AutoUIManager.Instance?.SetQuestOverlayOpen(modal);
    }

    private void HandleMapFragment1Acquired()
    {
        questUI?.QueueMapUnlockReveal();
        AutoChatManager.Instance?.AddMessage(
            "MANH MỐI", "Đã ghép đủ dữ liệu tuyến đường. Mở bản đồ [M] để xem khu vực vừa mở khóa.");
    }

    private IEnumerator PlayOfficeRevealRoutine()
    {
        if (questUI != null)
            questUI.CloseAllQuestOverlays();

        cinematicActive = true;
        SyncModalUI(true);

        if (officeTarget == null)
            ResolveMainSceneReferences();

        PZ_CameraController controller = PZ_CameraController.Instance;
        Camera gameplayCamera = controller != null ? controller.GetComponentInChildren<Camera>() : Camera.main;
        if (controller == null || gameplayCamera == null || officeTarget == null)
        {
            cinematicActive = false;
            SyncModalUI(true);
            questUI?.SetMapOpenForPreview(true);
            officeRevealRoutine = null;
            yield break;
        }

        Transform cameraRig = controller.transform;
        Transform returnTarget = controller.CurrentTarget != null ? controller.CurrentTarget : GetLocalPlayerTarget();
        Vector3 startPosition = cameraRig.position;
        Vector3 focusPosition = officeTarget.position + controller.offset;
        focusPosition.z = startPosition.z;
        float startZoom = gameplayCamera.orthographicSize;
        float focusZoom = Mathf.Clamp(startZoom * 1.55f, controller.minZoomSize, controller.maxZoomSize);
        bool controllerWasEnabled = controller.enabled;
        controller.enabled = false;

        AutoChatManager.Instance?.AddMessage("MANH MỐI", "Đã xác định văn phòng màu tím.");
        yield return MoveCamera(cameraRig, gameplayCamera, startPosition, focusPosition,
            startZoom, focusZoom, revealTravelDuration);
        yield return new WaitForSecondsRealtime(revealHoldDuration);
        yield return MoveCamera(cameraRig, gameplayCamera, focusPosition, startPosition,
            focusZoom, startZoom, revealReturnDuration);

        cameraRig.position = startPosition;
        gameplayCamera.orthographicSize = startZoom;
        controller.enabled = controllerWasEnabled;
        if (returnTarget != null)
            controller.SetTarget(returnTarget);

        cinematicActive = false;
        SyncModalUI(true);
        questUI?.SetMapOpenForPreview(true);
        officeRevealRoutine = null;
    }

    private static IEnumerator MoveCamera(Transform rig, Camera camera, Vector3 from, Vector3 to,
        float fromZoom, float toZoom, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration)));
            rig.position = Vector3.LerpUnclamped(from, to, t);
            camera.orthographicSize = Mathf.LerpUnclamped(fromZoom, toZoom, t);
            yield return null;
        }
        rig.position = to;
        camera.orthographicSize = toZoom;
    }

    private void HandleContainerOpened(LootContainer container)
    {
        // Intentionally empty. Container opening is not quest progress. The
        // authoritative LootContainer sync RPC performs only the one-time clue
        // roll; progress changes later, when a paper item is actually taken.
        _ = container;
    }
}
