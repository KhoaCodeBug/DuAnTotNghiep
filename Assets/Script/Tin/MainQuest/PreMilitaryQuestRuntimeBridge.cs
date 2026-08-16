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
    [SerializeField, Range(0.005f, 0.08f)] private float searchZoneMapPadding = 0.025f;
    [SerializeField, Range(3f, 15f)] private float searchZoneWorldPadding = 7f;

    private Transform officeTarget;
    private Transform configuredPlayerTarget;
    private ProjectZomboidMapRasterizer.Result rasterMap;
    private Coroutine officeRevealRoutine;
    private bool lastModalState;
    private bool cinematicActive;
    private bool searchZoneConfigured;
    private bool routeClueConsumptionScheduled;
    private readonly HashSet<string> activeSearchHouseIds = new HashSet<string>();
    private GameObject worldRestrictionRoot;

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
        if (player != null && !searchZoneConfigured)
            ConfigureSearchZone(player);
        if (rasterMap != null && player != null)
            questUI?.SetRasterMapPlayerPosition(rasterMap.WorldToNormalized(player.position));
        if (worldRestrictionRoot != null && questUI != null && questUI.IsHouseSearchComplete)
        {
            Destroy(worldRestrictionRoot);
            worldRestrictionRoot = null;
        }

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
        if (rasterMap != null && rasterMap.Texture != null)
            Destroy(rasterMap.Texture);
        if (Instance == this) Instance = null;
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

        bool completedNow = Instance.questUI.RegisterRouteClueForPreview(clueId, deferCompletion: true);
        Instance.questUI.ShowRouteClueReading(
            QuestRouteClueItemCatalog.GetDisplayName(kind),
            QuestRouteClueItemCatalog.GetReadingText(kind),
            QuestRouteClueItemCatalog.GetInferenceText(kind));
        if (!Instance.showClueMessages)
            Instance.questUI.CloseRouteClueReading();

        if (completedNow && !Instance.routeClueConsumptionScheduled)
        {
            Instance.routeClueConsumptionScheduled = true;
            Instance.StartCoroutine(Instance.ConsumeRouteClueItemsNextFrame());
        }

        Instance.SyncModalUI(true);
    }

    private IEnumerator ConsumeRouteClueItemsNextFrame()
    {
        // Let Fusion finish the loot add/sync transaction before consuming the
        // three source documents that have just been assembled into Mảnh 1.
        yield return null;

        Transform player = GetLocalPlayerTarget();
        InventorySystem inventory = player != null ? player.GetComponent<InventorySystem>() : null;
        if (inventory == null)
        {
            Debug.LogWarning("[PRE-MILITARY QUEST] Could not remove route clues: local inventory was not found.");
            yield break;
        }

        int removed = 0;
        for (int i = 0; i < PreMilitaryQuestProgress.RequiredRouteClues; i++)
            removed += inventory.ConsumeItem(QuestRouteClueItemCatalog.GetOrCreate((QuestRouteClueKind)i), 1);

        if (removed != PreMilitaryQuestProgress.RequiredRouteClues)
            Debug.LogWarning($"[PRE-MILITARY QUEST] Removed {removed}/3 route-clue items after assembling Mảnh 1.");
    }

    public static void NotifyOfficeEntered(Component officeChild)
    {
        if (Instance == null || officeChild == null)
            return;

        if (!QuestLocationIdentity.TryResolve(officeChild, out QuestLocationIdentity identity) ||
            identity.LocationType != QuestLocationType.PurpleOffice)
            return;

        Instance.questUI?.RegisterOfficeDiscoveredForPreview();
    }

    public static void NotifyMapFragment2Found()
    {
        if (Instance == null || Instance.questUI == null)
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
        }
    }

    private void ConfigureSearchZone(Transform player)
    {
        if (player == null || rasterMap == null || questUI == null) return;

        List<QuestLocationIdentity> candidates = new List<QuestLocationIdentity>();
        foreach (QuestLocationIdentity identity in FindObjectsByType<QuestLocationIdentity>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (identity.LocationType == QuestLocationType.ResidentialHouse && identity.HasValidId &&
                identity.GetComponentInChildren<LootContainer>(true) != null)
                candidates.Add(identity);
        }
        candidates.Sort((a, b) =>
        {
            float aDistance = ((Vector2)a.transform.position - (Vector2)player.position).sqrMagnitude;
            float bDistance = ((Vector2)b.transform.position - (Vector2)player.position).sqrMagnitude;
            int distanceOrder = aDistance.CompareTo(bDistance);
            return distanceOrder != 0 ? distanceOrder : string.CompareOrdinal(a.LocationId, b.LocationId);
        });
        if (candidates.Count == 0) return;

        int count = Mathf.Min(maxSearchZoneHouses, candidates.Count);
        List<QuestLocationIdentity> selected = candidates.GetRange(0, count);
        activeSearchHouseIds.Clear();
        Vector2 min = Vector2.one;
        Vector2 max = Vector2.zero;
        Vector2 worldMin = player.position;
        Vector2 worldMax = player.position;
        foreach (QuestLocationIdentity house in selected)
        {
            activeSearchHouseIds.Add(house.LocationId);
            Vector2 point = rasterMap.WorldToNormalized(house.transform.position);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
            worldMin = Vector2.Min(worldMin, house.transform.position);
            worldMax = Vector2.Max(worldMax, house.transform.position);
        }
        min = Vector2.Max(Vector2.zero, min - Vector2.one * searchZoneMapPadding);
        max = Vector2.Min(Vector2.one, max + Vector2.one * searchZoneMapPadding);
        questUI.ConfigureSearchZone(min, max, count);
        CreateWorldRestriction(worldMin - Vector2.one * searchZoneWorldPadding,
            worldMax + Vector2.one * searchZoneWorldPadding);

        int[] clueHouseIndices = count >= 7
            ? new[] { 1, count / 2, count - 2 }
            : new[] { 0, Mathf.Min(1, count - 1), count - 1 };
        for (int i = 0; i < 3 && i < count; i++)
        {
            QuestLocationIdentity house = selected[clueHouseIndices[i]];
            LootContainer container = house.GetComponentInChildren<LootContainer>(true);
            if (container == null) continue;
            QuestRouteClueKind kind = (QuestRouteClueKind)i;
            QuestRouteClueSource source = container.GetComponent<QuestRouteClueSource>();
            if (source == null) source = container.gameObject.AddComponent<QuestRouteClueSource>();
            source.Configure(kind);
            container.EnsureQuestClueItem(kind);
        }

        searchZoneConfigured = true;
        AutoChatManager.Instance?.AddMessage("NHIỆM VỤ", $"Đã khoanh khu tìm kiếm: {count} căn nhà gần điểm xuất phát.");
    }

    private void CreateWorldRestriction(Vector2 minimum, Vector2 maximum)
    {
        if (worldRestrictionRoot != null) Destroy(worldRestrictionRoot);
        worldRestrictionRoot = new GameObject("Quest Search Area Restriction");
        float width = Mathf.Max(12f, maximum.x - minimum.x);
        float height = Mathf.Max(12f, maximum.y - minimum.y);
        const float thickness = 2f;
        CreateBoundary("North", new Vector2((minimum.x + maximum.x) * 0.5f, maximum.y + thickness * 0.5f),
            new Vector2(width + thickness * 2f, thickness));
        CreateBoundary("South", new Vector2((minimum.x + maximum.x) * 0.5f, minimum.y - thickness * 0.5f),
            new Vector2(width + thickness * 2f, thickness));
        CreateBoundary("West", new Vector2(minimum.x - thickness * 0.5f, (minimum.y + maximum.y) * 0.5f),
            new Vector2(thickness, height));
        CreateBoundary("East", new Vector2(maximum.x + thickness * 0.5f, (minimum.y + maximum.y) * 0.5f),
            new Vector2(thickness, height));
    }

    private void CreateBoundary(string suffix, Vector2 position, Vector2 size)
    {
        GameObject boundary = new GameObject("Restricted Boundary " + suffix,
            typeof(BoxCollider2D), typeof(QuestSearchBoundaryBlocker));
        boundary.transform.SetParent(worldRestrictionRoot.transform, false);
        boundary.transform.position = position;
        boundary.GetComponent<BoxCollider2D>().size = size;
        int obstacleLayer = LayerMask.NameToLayer("Obstacle");
        if (obstacleLayer >= 0) boundary.layer = obstacleLayer;
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
        if (officeRevealRoutine == null)
            officeRevealRoutine = StartCoroutine(PlayOfficeRevealRoutine());
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
        if (container == null || questUI == null)
            return;

        if (!QuestLocationIdentity.TryResolve(container, out QuestLocationIdentity location) ||
            location.LocationType != QuestLocationType.ResidentialHouse)
            return;

        // Only the compact neighborhood highlighted on the quest map counts.
        // Opening a cabinet outside it cannot silently advance the objective.
        if (!searchZoneConfigured || !activeSearchHouseIds.Contains(location.LocationId))
            return;

        questUI.RegisterHouseLootContainerOpenedForPreview(location.LocationId);
    }
}
