using System.Collections;
using System.Collections.Generic;
using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;

/// <summary>
/// Network-authoritative spine for the Main scene story. Attach this to an
/// existing scene NetworkObject (Day_Night_System is the current host).
/// </summary>
public sealed class MainQuestManager : NetworkBehaviour
{
    public enum QuestStage
    {
        NotStarted,
        SearchNeighborhood,
        LocateOffice,
        FindCityMap,
        CityMapFound
    }

    public const int MaximumSearchHouses = PreMilitaryQuestProgress.MaximumSearchHouses;
    private const int MaximumCabinetSearchPoints = 32;

    public static MainQuestManager Instance { get; private set; }

    [Header("Military-zone reveal")]
    [Tooltip("Điểm KhuVucQuanSu mà camera của mọi người chơi sẽ nhìn tới sau khi tìm thấy bản đồ.")]
    [SerializeField] private Transform khuVucQuanSuFocus;
    [Tooltip("Khoảng nghỉ để người chơi kịp đọc thông báo tìm thấy manh mối trước khi camera rời đi.")]
    [SerializeField, Min(0.5f)] private float clueLeadInSeconds = 2f;
    [Tooltip("Thời gian lia camera. Dùng smoother-step để có cảm giác Easy Ease/F9.")]
    [SerializeField, Min(1f)] private float cameraTravelSeconds = 3.5f;
    [Tooltip("Zoom cinematic được phép vượt maxZoomSize=5 của người chơi.")]
    [SerializeField, Min(5.1f)] private float cinematicZoomSize = 9f;
    [SerializeField, Min(0f)] private float cameraSettleSeconds = 0.65f;
    [SerializeField, Min(0.1f)] private float locationTitleFadeInSeconds = 0.8f;
    [SerializeField, Min(0.5f)] private float locationTitleHoldSeconds = 3f;
    [SerializeField, Min(0.1f)] private float locationTitleFadeOutSeconds = 0.8f;
    [SerializeField, Min(0f)] private float pauseAfterTitleSeconds = 0.25f;
    [SerializeField, Min(0.05f)] private float fadeToBlackSeconds = 0.65f;
    [Tooltip("Giữ màn hình đen để Host kịp gom đội hình và đồng bộ vị trí tới mọi client.")]
    [SerializeField, Min(0.1f)] private float fadeBlackHoldSeconds = 0.65f;
    [SerializeField, Min(0.05f)] private float fadeFromBlackSeconds = 0.7f;
    [Tooltip("Khoảng miễn sát thương ngắn sau khi hình ảnh đã trở lại.")]
    [SerializeField, Min(0f)] private float safetyGraceSeconds = 0.8f;

    [Header("Safe team gather after reveal")]
    [Tooltip("Chỉ zombie nằm trong vòng nhỏ này quanh người tìm thấy bản đồ mới bị xóa.")]
    [SerializeField, Min(0.5f)] private float zombieClearRadius = 3f;
    [SerializeField, Min(0.1f)] private float gatherSpacing = 0.35f;
    [SerializeField] private LayerMask gatherObstacleMask = 1 << 6;

    [Header("Quest HUD")]
    [SerializeField] private bool showBuiltInQuestHud = true;
    [SerializeField, Min(0.05f)] private float questEventFadeInSeconds = 0.45f;
    [SerializeField, Min(0.1f)] private float questEventHoldSeconds = 2.8f;
    [SerializeField, Min(0.05f)] private float questEventFadeOutSeconds = 0.65f;

    [Header("Route clue loot insurance")]
    [Tooltip("Cơ hội tủ hợp lệ đầu tiên sinh manh mối. Sau một lần trượt, tủ hợp lệ kế tiếp luôn được bảo hiểm.")]
    [SerializeField, Range(0f, 1f)] private float routeClueBaseDropChance = 0.7f;

    [Networked] public int NetworkQuestStage { get; set; }
    [Networked] public int MapCabinetId { get; set; }
    [Networked] public NetworkBool IsCityMapUnlocked { get; set; }
    [Networked] public NetworkBool IsMilitaryRevealPlaying { get; set; }
    [Networked] public NetworkBool IsNeighborhoodConfigured { get; set; }
    [Networked] public int SearchHouseCount { get; set; }
    [Networked] public NetworkString<_64> SearchHouseId0 { get; set; }
    [Networked] public NetworkString<_64> SearchHouseId1 { get; set; }
    [Networked] public NetworkString<_64> SearchHouseId2 { get; set; }
    [Networked] public NetworkString<_64> SearchHouseId3 { get; set; }
    [Networked] public NetworkString<_64> SearchHouseId4 { get; set; }
    [Networked] public NetworkString<_64> SearchHouseId5 { get; set; }
    [Networked] public int SearchedHouseMask { get; set; }
    [Networked] public int RouteClueMask { get; set; }
    [Networked] public int InsuredRouteClueMask { get; set; }
    [Networked] public int RouteClueDryOpenCount { get; set; }
    [Networked] public NetworkBool IsOfficeDiscovered { get; set; }
    [Networked] public int CheckedCabinetMask { get; set; }

    private MapController cachedMapController;
    private MinimapController cachedMinimapController;
    private Coroutine focusRoutine;
    private Coroutine authoritySafetyRoutine;
    private float localFadeAlpha;
    private float localClueNoticeAlpha;
    private float localLocationTitleAlpha;
    private float localQuestEventAlpha;
    private string localQuestEventTitle = string.Empty;
    private string localQuestEventBody = string.Empty;
    private Coroutine questEventRoutine;
    private readonly Dictionary<int, int> cabinetIndexById = new Dictionary<int, int>();
    private bool hasSpawned;

    /// <summary>
    /// Fusion networked properties are not legal to access between Awake and
    /// Spawned (or after Despawned), even though the scene component exists.
    /// All non-network callers must pass through this gate first.
    /// </summary>
    public bool IsNetworkReady => hasSpawned && Object != null && Object.IsValid &&
                                  Runner != null && Runner.IsRunning;
    public QuestStage CurrentStage => IsNetworkReady
        ? (QuestStage)NetworkQuestStage
        : QuestStage.NotStarted;
    public bool IsMapSearchActive => IsNetworkReady && CurrentStage == QuestStage.FindCityMap;
    public bool IsQuestCutsceneActive => IsNetworkReady && IsMilitaryRevealPlaying;
    public bool IsNeighborhoodSearchActive => IsNetworkReady && CurrentStage == QuestStage.SearchNeighborhood;
    public int SearchedHouseCount => CountBits(SearchedHouseMask);
    public int RouteClueCount => CountBits(RouteClueMask);
    public bool HasMapFragment1 => RouteClueCount >= PreMilitaryQuestProgress.RequiredRouteClues;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(this);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public override void Spawned()
    {
        // Set this before the first networked-property access in this method.
        hasSpawned = true;

        if (HasStateAuthority)
        {
            NetworkQuestStage = (int)QuestStage.NotStarted;
            MapCabinetId = 0;
            IsCityMapUnlocked = false;
            IsMilitaryRevealPlaying = false;
            IsNeighborhoodConfigured = false;
            SearchHouseCount = 0;
            SearchHouseId0 = default;
            SearchHouseId1 = default;
            SearchHouseId2 = default;
            SearchHouseId3 = default;
            SearchHouseId4 = default;
            SearchHouseId5 = default;
            SearchedHouseMask = 0;
            RouteClueMask = 0;
            InsuredRouteClueMask = 0;
            RouteClueDryOpenCount = 0;
            IsOfficeDiscovered = false;
            CheckedCabinetMask = 0;
        }

        ApplyMapAccess();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        hasSpawned = false;
        if (focusRoutine != null) StopCoroutine(focusRoutine);
        if (authoritySafetyRoutine != null) StopCoroutine(authoritySafetyRoutine);
        focusRoutine = null;
        authoritySafetyRoutine = null;
        localFadeAlpha = 0f;
        localClueNoticeAlpha = 0f;
        localLocationTitleAlpha = 0f;
        localQuestEventAlpha = 0f;
        if (questEventRoutine != null) StopCoroutine(questEventRoutine);
        questEventRoutine = null;
        ApplyMapAccess();
    }

    private void Update()
    {
        ApplyMapAccess();
    }

    /// <summary>
    /// State Authority chooses the opening neighborhood exactly once. The six
    /// stable scene IDs are replicated so every client and late joiner uses the
    /// same quest area regardless of its random spawn point.
    /// </summary>
    public bool TryInitializeNeighborhood(IReadOnlyList<string> houseIds)
    {
        if (!IsNetworkReady || !HasStateAuthority || IsNeighborhoodConfigured ||
            CurrentStage != QuestStage.NotStarted || houseIds == null)
            return false;

        int count = Mathf.Min(MaximumSearchHouses, houseIds.Count);
        if (count < PreMilitaryQuestProgress.RequiredDistinctHouses)
        {
            Debug.LogError($"[MAIN QUEST] Cần ít nhất {PreMilitaryQuestProgress.RequiredDistinctHouses} nhà hợp lệ để bắt đầu nhiệm vụ.");
            return false;
        }

        HashSet<string> uniqueIds = new HashSet<string>(System.StringComparer.Ordinal);
        for (int i = 0; i < count; i++)
        {
            string id = houseIds[i];
            if (string.IsNullOrWhiteSpace(id) || !uniqueIds.Add(id))
            {
                Debug.LogError("[MAIN QUEST] Danh sách nhà khởi tạo có ID rỗng hoặc trùng.");
                return false;
            }
        }

        SearchHouseCount = count;
        for (int i = 0; i < MaximumSearchHouses; i++)
            SetSearchHouseId(i, i < count ? houseIds[i] : string.Empty);
        SearchedHouseMask = 0;
        RouteClueMask = 0;
        InsuredRouteClueMask = 0;
        RouteClueDryOpenCount = 0;
        IsOfficeDiscovered = false;
        IsNeighborhoodConfigured = true;
        NetworkQuestStage = (int)QuestStage.SearchNeighborhood;
        RPC_ShowQuestMessage($"MỤC TIÊU MỚI: Tìm kiếm {PreMilitaryQuestProgress.RequiredRouteClues} manh mối ở các ngôi nhà xung quanh.");
        return true;
    }

    public string GetSearchHouseId(int index)
    {
        if (!IsNetworkReady || index < 0 || index >= SearchHouseCount) return string.Empty;
        return index switch
        {
            0 => SearchHouseId0.ToString(),
            1 => SearchHouseId1.ToString(),
            2 => SearchHouseId2.ToString(),
            3 => SearchHouseId3.ToString(),
            4 => SearchHouseId4.ToString(),
            5 => SearchHouseId5.ToString(),
            _ => string.Empty
        };
    }

    private void SetSearchHouseId(int index, string value)
    {
        switch (index)
        {
            case 0: SearchHouseId0 = value; break;
            case 1: SearchHouseId1 = value; break;
            case 2: SearchHouseId2 = value; break;
            case 3: SearchHouseId3 = value; break;
            case 4: SearchHouseId4 = value; break;
            case 5: SearchHouseId5 = value; break;
        }
    }

    /// <summary>
    /// Called inside LootContainer's authoritative open/sync RPC. Pity is
    /// therefore resolved before this exact container's slots are sent back.
    /// </summary>
    public void AuthorityRegisterOpenedContainer(LootContainer openedContainer, PlayerRef requester)
    {
        if (!IsNetworkReady || !HasStateAuthority || openedContainer == null ||
            !QuestLocationIdentity.TryResolve(openedContainer, out QuestLocationIdentity location) ||
            location.LocationType != QuestLocationType.ResidentialHouse)
            return;

        ServerRollContainerRouteClue(requester, openedContainer, location.LocationId);
    }

    private void ServerRollContainerRouteClue(PlayerRef requester, LootContainer openedContainer, string houseId)
    {
        if (!HasStateAuthority || CurrentStage != QuestStage.SearchNeighborhood) return;
        if (!TryGetRequestingPlayer(requester, out PlayerMovement player) ||
            !openedContainer.CanPlayerOpenFrom(player.transform.position))
            return;

        RecoverMissingInsuredRouteClue(openedContainer);
        if (!openedContainer.AuthorityTryBeginRouteClueRoll()) return;
        TryPlaceInsuredRouteClue(openedContainer, houseId);
    }

    /// <summary>Called only by the authoritative loot transaction.</summary>
    public void AuthorityRegisterRouteClue(QuestRouteClueKind kind)
    {
        if (!IsNetworkReady || !HasStateAuthority || CurrentStage == QuestStage.CityMapFound) return;
        int bit = 1 << (int)kind;
        if ((RouteClueMask & bit) != 0) return;
        RouteClueMask |= bit;
        RPC_ShowQuestMessage($"Manh mối tuyến đường: {RouteClueCount}/{PreMilitaryQuestProgress.RequiredRouteClues}.");
        if (RouteClueCount >= PreMilitaryQuestProgress.RequiredRouteClues)
        {
            NetworkQuestStage = (int)QuestStage.LocateOffice;
            RPC_ShowAllRouteCluesFound();
        }
    }

    private void TryPlaceInsuredRouteClue(LootContainer openedContainer, string houseId)
    {
        if (openedContainer == null || !openedContainer.HasStateAuthority ||
            !QuestLocationIdentity.TryResolve(openedContainer, out QuestLocationIdentity location) ||
            !string.Equals(location.LocationId, houseId, System.StringComparison.Ordinal))
            return;

        int unavailableClueMask = InsuredRouteClueMask | RouteClueMask;
        int completeMask = (1 << PreMilitaryQuestProgress.RequiredRouteClues) - 1;
        if ((unavailableClueMask & completeMask) == completeMask) return;

        // Every new residential cabinet gets one authoritative roll. If it
        // misses, the next new cabinet is guaranteed. The result remains a real
        // paper item; merely opening the cabinet never advances the quest.
        bool guaranteed = RouteClueDryOpenCount >= 1;
        if (!guaranteed && Random.value > routeClueBaseDropChance)
        {
            RouteClueDryOpenCount++;
            Debug.Log($"[QUEST LOOT] Cabinet '{openedContainer.name}' in '{houseId}' rolled no clue. " +
                      "The next new residential cabinet is guaranteed.");
            return;
        }

        for (int clueIndex = 0; clueIndex < PreMilitaryQuestProgress.RequiredRouteClues; clueIndex++)
        {
            int clueBit = 1 << clueIndex;
            if ((InsuredRouteClueMask & clueBit) != 0 || (RouteClueMask & clueBit) != 0)
                continue;

            if (openedContainer.EnsureQuestClueItem((QuestRouteClueKind)clueIndex))
            {
                InsuredRouteClueMask |= clueBit;
                RouteClueDryOpenCount = 0;
                Debug.Log($"[QUEST LOOT] Placed '{QuestRouteClueItemCatalog.GetDisplayName((QuestRouteClueKind)clueIndex)}' " +
                          $"inside opened container '{openedContainer.name}' in house '{houseId}'.");
            }

            return;
        }
    }

    private void RecoverMissingInsuredRouteClue(LootContainer openedContainer)
    {
        if (openedContainer == null || !openedContainer.HasStateAuthority)
            return;

        for (int clueIndex = 0; clueIndex < PreMilitaryQuestProgress.RequiredRouteClues; clueIndex++)
        {
            int clueBit = 1 << clueIndex;
            bool spawnedButNotCollected = (InsuredRouteClueMask & clueBit) != 0 &&
                                          (RouteClueMask & clueBit) == 0;
            if (!spawnedButNotCollected || RouteClueExistsInResidentialContainers((QuestRouteClueKind)clueIndex))
                continue;

            // A destroyed/reset container must not permanently soft-lock the
            // side route. Reinsert that exact document on the next valid open.
            openedContainer.EnsureQuestClueItem((QuestRouteClueKind)clueIndex);
            return;
        }
    }

    private static bool RouteClueExistsInResidentialContainers(QuestRouteClueKind kind)
    {
        QuestLocationIdentity[] locations = FindObjectsByType<QuestLocationIdentity>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int locationIndex = 0; locationIndex < locations.Length; locationIndex++)
        {
            QuestLocationIdentity location = locations[locationIndex];
            if (location == null || location.LocationType != QuestLocationType.ResidentialHouse)
                continue;

            LootContainer[] containers = location.GetComponentsInChildren<LootContainer>(true);
            for (int containerIndex = 0; containerIndex < containers.Length; containerIndex++)
            {
                LootContainer container = containers[containerIndex];
                if (container == null) continue;
                for (int slotIndex = 0; slotIndex < container.itemsInContainer.Count; slotIndex++)
                {
                    InventorySlot slot = container.itemsInContainer[slotIndex];
                    if (slot != null && QuestRouteClueItemCatalog.TryGetKind(slot.item,
                            out QuestRouteClueKind existingKind) && existingKind == kind)
                        return true;
                }
            }
        }
        return false;
    }

    private static int CountBits(int value)
    {
        int count = 0;
        uint remaining = unchecked((uint)value);
        while (remaining != 0)
        {
            remaining &= remaining - 1;
            count++;
        }
        return count;
    }

    /// <summary>Called when the local player reaches KhuVucNhiemVu.</summary>
    public void RequestStartMapSearch(int triggerId)
    {
        if (!IsNetworkReady) return;
        if (HasStateAuthority) ServerStartMapSearch(triggerId, Runner.LocalPlayer);
        else RPC_RequestStartMapSearch(triggerId);
    }

    /// <summary>Called by the closest highlighted quest-search point when E is pressed.</summary>
    public void RequestSearchCabinet(int cabinetId)
    {
        if (!IsNetworkReady) return;
        if (HasStateAuthority) ServerSearchCabinet(cabinetId, Runner.LocalPlayer);
        else RPC_RequestSearchCabinet(cabinetId);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestStartMapSearch(int triggerId, RpcInfo info = default)
    {
        ServerStartMapSearch(triggerId, info.Source);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestSearchCabinet(int cabinetId, RpcInfo info = default)
    {
        ServerSearchCabinet(cabinetId, info.Source);
    }

    private void ServerStartMapSearch(int triggerId, PlayerRef requester)
    {
        if (!HasStateAuthority || CurrentStage != QuestStage.LocateOffice) return;
        if (!MainQuestStartTrigger.TryGet(triggerId, out MainQuestStartTrigger trigger) || trigger == null) return;
        if (!TryGetRequestingPlayer(requester, out PlayerMovement player) || !trigger.Contains(player.transform.position)) return;

        MainQuestSearchCabinet[] allCabinets = FindObjectsByType<MainQuestSearchCabinet>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        List<MainQuestSearchCabinet> validCabinets = new List<MainQuestSearchCabinet>(allCabinets.Length);
        for (int i = 0; i < allCabinets.Length; i++)
        {
            if (allCabinets[i] != null && allCabinets[i].CabinetId != 0)
                validCabinets.Add(allCabinets[i]);
        }

        if (validCabinets.Count == 0)
        {
            Debug.LogError("[MAIN QUEST] Không có MainQuestSearchCabinet nào để random bản đồ.");
            RPC_ShowQuestMessage("Chưa thể bắt đầu: khu vực chưa có điểm kiểm tra nhiệm vụ.");
            return;
        }

        validCabinets.Sort((left, right) => left.CabinetId.CompareTo(right.CabinetId));
        if (validCabinets.Count > MaximumCabinetSearchPoints)
            validCabinets.RemoveRange(MaximumCabinetSearchPoints,
                validCabinets.Count - MaximumCabinetSearchPoints);
        RebuildCabinetIndexCache(validCabinets);
        CheckedCabinetMask = 0;

        // Random.Range(int, int) phân phối đều: mỗi điểm có đúng xác suất 1/N.
        MapCabinetId = validCabinets[Random.Range(0, validCabinets.Count)].CabinetId;
        IsOfficeDiscovered = true;
        NetworkQuestStage = (int)QuestStage.FindCityMap;
        RPC_ShowQuestMessage("MỤC TIÊU MỚI: Kiểm tra các vị trí màu vàng bên trong văn phòng để tìm bản đồ thành phố.");
        RPC_ShowOfficeSearchStarted();
    }

    private void ServerSearchCabinet(int cabinetId, PlayerRef requester)
    {
        if (!HasStateAuthority || !IsMapSearchActive) return;
        if (!MainQuestSearchCabinet.TryGet(cabinetId, out MainQuestSearchCabinet cabinet) || cabinet == null) return;
        if (!TryGetRequestingPlayer(requester, out PlayerMovement player) ||
            !cabinet.CanPlayerSearch(player.transform.position)) return;
        int cabinetIndex = GetCabinetIndex(cabinetId);
        if (cabinetIndex >= 0 && (CheckedCabinetMask & (1 << cabinetIndex)) != 0) return;

        if (cabinetId != MapCabinetId)
        {
            if (cabinetIndex >= 0)
                CheckedCabinetMask |= 1 << cabinetIndex;
            RPC_ShowCabinetSearchResult(requester, false);
            return;
        }

        IsCityMapUnlocked = true;
        MapCabinetId = 0;
        NetworkQuestStage = (int)QuestStage.CityMapFound;
        IsMilitaryRevealPlaying = true;

        RPC_ShowCabinetSearchResult(requester, true);
        RPC_ShowLocalizedQuestMessage("quest.map_clue_chat");
        RPC_PlayMilitaryZoneReveal();

        if (authoritySafetyRoutine != null) StopCoroutine(authoritySafetyRoutine);
        authoritySafetyRoutine = StartCoroutine(AuthorityRevealSequence(requester, player.transform.position));
    }

    public bool IsCabinetChecked(int cabinetId)
    {
        if (!IsNetworkReady || cabinetId == 0)
            return false;

        int cabinetIndex = GetCabinetIndex(cabinetId);
        return cabinetIndex >= 0 && (CheckedCabinetMask & (1 << cabinetIndex)) != 0;
    }

    private int GetCabinetIndex(int cabinetId)
    {
        if (cabinetIndexById.TryGetValue(cabinetId, out int index))
            return index;

        MainQuestSearchCabinet[] cabinets = FindObjectsByType<MainQuestSearchCabinet>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        List<MainQuestSearchCabinet> sorted = new List<MainQuestSearchCabinet>(cabinets.Length);
        for (int i = 0; i < cabinets.Length; i++)
        {
            if (cabinets[i] != null && cabinets[i].CabinetId != 0)
                sorted.Add(cabinets[i]);
        }
        sorted.Sort((left, right) => left.CabinetId.CompareTo(right.CabinetId));
        if (sorted.Count > MaximumCabinetSearchPoints)
            sorted.RemoveRange(MaximumCabinetSearchPoints, sorted.Count - MaximumCabinetSearchPoints);
        RebuildCabinetIndexCache(sorted);
        return cabinetIndexById.TryGetValue(cabinetId, out index) ? index : -1;
    }

    private void RebuildCabinetIndexCache(IReadOnlyList<MainQuestSearchCabinet> sortedCabinets)
    {
        cabinetIndexById.Clear();
        for (int i = 0; i < sortedCabinets.Count && i < MaximumCabinetSearchPoints; i++)
            cabinetIndexById[sortedCabinets[i].CabinetId] = i;
    }

    private IEnumerator AuthorityRevealSequence(PlayerRef mapFinder, Vector2 gatherPosition)
    {
        // Teleport ở giữa đoạn giữ màn hình đen: client có đủ thời gian nhận vị trí mới
        // trước khi fade sáng trở lại, kể cả khi có một ít độ trễ mạng.
        float beforeGather = clueLeadInSeconds + cameraTravelSeconds + cameraSettleSeconds +
                             locationTitleFadeInSeconds + locationTitleHoldSeconds +
                             locationTitleFadeOutSeconds + pauseAfterTitleSeconds + fadeToBlackSeconds +
                             fadeBlackHoldSeconds * 0.5f;
        yield return new WaitForSecondsRealtime(beforeGather);

        if (HasStateAuthority)
        {
            ClearNearbyZombies(gatherPosition);
            GatherPlayersAtMapFinder(mapFinder, gatherPosition);
        }

        float afterGather = fadeBlackHoldSeconds * 0.5f + fadeFromBlackSeconds + safetyGraceSeconds;
        yield return new WaitForSecondsRealtime(afterGather);

        if (HasStateAuthority) IsMilitaryRevealPlaying = false;
        authoritySafetyRoutine = null;
    }

    private void GatherPlayersAtMapFinder(PlayerRef mapFinder, Vector2 gatherPosition)
    {
        PlayerMovement[] allPlayers = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        List<PlayerMovement> players = new List<PlayerMovement>(allPlayers.Length);
        for (int i = 0; i < allPlayers.Length; i++)
        {
            PlayerMovement movement = allPlayers[i];
            if (movement == null || movement.Object == null || !movement.Object.IsValid || !movement.HasStateAuthority)
                continue;

            PlayerHealth health = movement.GetComponent<PlayerHealth>();
            if (health != null && (health.isDead || health.isTransforming)) continue;
            players.Add(movement);
        }

        // Người nhặt map đứng đúng tại điểm gốc; đồng đội được xếp vòng nhỏ xung quanh.
        players.Sort((left, right) =>
        {
            bool leftIsFinder = left.Object.InputAuthority == mapFinder;
            bool rightIsFinder = right.Object.InputAuthority == mapFinder;
            if (leftIsFinder == rightIsFinder) return 0;
            return leftIsFinder ? -1 : 1;
        });

        List<Vector2> occupiedPositions = new List<Vector2>(players.Count);
        for (int i = 0; i < players.Count; i++)
        {
            PlayerMovement movement = players[i];
            Vector2 destination = i == 0
                ? gatherPosition
                : FindSafeGatherPosition(gatherPosition, i, occupiedPositions);

            PlayerInteraction interaction = movement.GetComponent<PlayerInteraction>();
            if (interaction != null && interaction.IsInVehicle)
            {
                VehicleControllerFusion vehicle = interaction.CurrentVehicleController;
                bool exitedNormally = vehicle != null && vehicle.AuthorityTryExit(movement.Object);
                if (!exitedNormally)
                    interaction.SetVehicleNetworkState(null, false, false, 0, destination);
            }

            TeleportPlayer(movement, destination);
            occupiedPositions.Add(destination);
        }

        Physics2D.SyncTransforms();
    }

    private Vector2 FindSafeGatherPosition(Vector2 center, int playerIndex, List<Vector2> occupiedPositions)
    {
        const int attempts = 16;
        float startAngle = playerIndex * 137.50776f;
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            int ring = 1 + attempt / 8;
            float angle = (startAngle + attempt * 45f) * Mathf.Deg2Rad;
            Vector2 candidate = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * gatherSpacing * ring;

            if (gatherObstacleMask.value != 0 &&
                Physics2D.OverlapCircle(candidate, 0.12f, gatherObstacleMask) != null)
                continue;

            bool overlapsPlayer = false;
            for (int i = 0; i < occupiedPositions.Count; i++)
            {
                if (Vector2.Distance(candidate, occupiedPositions[i]) >= gatherSpacing * 0.75f) continue;
                overlapsPlayer = true;
                break;
            }
            if (!overlapsPlayer) return candidate;
        }

        // Điểm người nhặt map chắc chắn đang đứng được; dùng làm fallback nếu căn phòng quá hẹp.
        return center;
    }

    private static void TeleportPlayer(PlayerMovement movement, Vector2 destination)
    {
        if (movement == null) return;

        NetworkRigidbody2D networkBody = movement.GetComponent<NetworkRigidbody2D>();
        Rigidbody2D body = movement.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        if (networkBody != null)
            networkBody.Teleport(new Vector3(destination.x, destination.y, movement.transform.position.z));
        else
        {
            if (body != null) body.position = destination;
            movement.transform.position = new Vector3(destination.x, destination.y, movement.transform.position.z);
        }
    }

    private void ClearNearbyZombies(Vector2 center)
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        HashSet<NetworkObject> handled = new HashSet<NetworkObject>();
        int removedCount = 0;

        for (int i = 0; i < enemies.Length; i++)
        {
            GameObject enemy = enemies[i];
            if (enemy == null) continue;

            NetworkObject networkObject = enemy.GetComponentInParent<NetworkObject>();
            if (networkObject == null || !networkObject.IsValid || !networkObject.HasStateAuthority ||
                handled.Contains(networkObject)) continue;
            if (Vector2.Distance(center, networkObject.transform.position) > zombieClearRadius) continue;
            if (!IsZombie(networkObject.gameObject)) continue;

            handled.Add(networkObject);
            Runner.Despawn(networkObject);
            removedCount++;
        }

        Debug.Log($"[MAIN QUEST] Đã dọn {removedCount} zombie trong bán kính {zombieClearRadius:0.#} quanh điểm tập kết.");
    }

    private static bool IsZombie(GameObject target)
    {
        return target.GetComponent<ZombieAI>() != null ||
               target.GetComponent<ZombieHealth>() != null ||
               target.GetComponent<ZOmbieAI_Khoa>() != null ||
               target.GetComponent<ZombieAIKhoaRebuilt>() != null;
    }

    private static bool TryGetRequestingPlayer(PlayerRef requester, out PlayerMovement player)
    {
        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] == null || players[i].Object == null || !players[i].Object.IsValid) continue;
            if (players[i].Object.InputAuthority != requester) continue;
            player = players[i];
            return true;
        }

        player = null;
        return false;
    }

    private void ApplyMapAccess()
    {
        if (cachedMapController == null)
            cachedMapController = FindFirstObjectByType<MapController>(FindObjectsInactive.Include);
        if (cachedMinimapController == null)
            cachedMinimapController = FindFirstObjectByType<MinimapController>(FindObjectsInactive.Include);

        bool unlocked = IsNetworkReady && IsCityMapUnlocked;
        if (cachedMapController != null) cachedMapController.SetMapUnlocked(unlocked);
        if (cachedMinimapController != null) cachedMinimapController.SetMapUnlocked(unlocked);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowQuestMessage(string message)
    {
        AutoChatManager.Instance?.AddMessage("NHIỆM VỤ", message);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowOfficeSearchStarted()
    {
        ShowLocalQuestEvent(
            "KHU VỰC NHIỆM VỤ MỚI",
            "VĂN PHÒNG MÀU TÍM  •  Kiểm tra các điểm vàng để tìm bản đồ thành phố.");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowAllRouteCluesFound()
    {
        const string message = "Dữ liệu mới đã được giải mã. Mở bản đồ [M] để kiểm tra vị trí vừa phát hiện.";
        AutoChatManager.Instance?.AddMessage("MANH MỐI", message);
        ShowLocalQuestEvent("ĐÃ PHÁT HIỆN ĐỦ MANH MỐI", message);
        QuestFlowUIPrototype.Instance?.QueueMapUnlockReveal();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowCabinetSearchResult(PlayerRef requester, bool found)
    {
        if (!IsNetworkReady || Runner.LocalPlayer != requester)
            return;

        string message = found
            ? "Đã tìm thấy manh mối quan trọng."
            : "Chẳng có manh mối gì ở đây cả. Hãy kiểm tra vị trí khác.";
        AutoChatManager.Instance?.AddMessage("NHIỆM VỤ", message);
        ShowLocalQuestEvent(found ? "ĐÃ TÌM THẤY MANH MỐI" : "KHÔNG CÓ MANH MỐI", message);
    }

    private void ShowLocalQuestEvent(string title, string body)
    {
        localQuestEventTitle = title;
        localQuestEventBody = body;
        if (questEventRoutine != null)
            StopCoroutine(questEventRoutine);
        questEventRoutine = StartCoroutine(QuestEventNoticeRoutine());
    }

    private IEnumerator QuestEventNoticeRoutine()
    {
        localQuestEventAlpha = 0f;
        for (float elapsed = 0f; elapsed < questEventFadeInSeconds; elapsed += Time.unscaledDeltaTime)
        {
            localQuestEventAlpha = CinematicEase(elapsed / Mathf.Max(0.001f, questEventFadeInSeconds));
            yield return null;
        }
        localQuestEventAlpha = 1f;
        yield return new WaitForSecondsRealtime(questEventHoldSeconds);
        for (float elapsed = 0f; elapsed < questEventFadeOutSeconds; elapsed += Time.unscaledDeltaTime)
        {
            localQuestEventAlpha = 1f - CinematicEase(elapsed / Mathf.Max(0.001f, questEventFadeOutSeconds));
            yield return null;
        }
        localQuestEventAlpha = 0f;
        questEventRoutine = null;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowLocalizedQuestMessage(string localizationKey)
    {
        AutoChatManager.Instance?.AddMessage(
            GameLocalization.Get("quest.sender"),
            GameLocalization.Get(localizationKey, localizationKey));
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayMilitaryZoneReveal()
    {
        PreMilitaryQuestRuntimeBridge.NotifyMapFragment2Found();
        if (khuVucQuanSuFocus == null || PZ_CameraController.Instance == null) return;

        // Dọn các bảng phủ màn hình để mọi client đều thực sự nhìn thấy cùng cutscene.
        AutoTabManager.Instance?.ShowTabs(false);
        if (AutoUIManager.Instance != null)
        {
            AutoUIManager.Instance.ForceHideInventoryOnly();
            AutoUIManager.Instance.CloseContainerUI();
            AutoUIManager.Instance.HideTradeWindow();
        }
        if (AutoHealthPanel.Instance != null) AutoHealthPanel.Instance.SetOpenState(false);

        if (focusRoutine != null) StopCoroutine(focusRoutine);
        focusRoutine = StartCoroutine(FocusMilitaryZoneRoutine());
    }

    private IEnumerator FocusMilitaryZoneRoutine()
    {
        PZ_CameraController cameraController = PZ_CameraController.Instance;
        Transform initialTarget = cameraController != null ? cameraController.CurrentTarget : null;
        if (cameraController == null || initialTarget == null || khuVucQuanSuFocus == null) yield break;

        localFadeAlpha = 0f;
        localClueNoticeAlpha = 0f;
        localLocationTitleAlpha = 0f;

        // Cho người chơi nhận biết phần thưởng trước khi tầm nhìn bị điều khiển.
        yield return ShowClueNotice(clueLeadInSeconds);

        Camera sceneCamera = cameraController.GetComponentInChildren<Camera>();
        float playerZoom = sceneCamera != null
            ? cameraController.GetTargetZoom()
            : 0f;
        cameraController.enabled = false;

        Vector3 from = cameraController.transform.position;
        Vector3 to = khuVucQuanSuFocus.position + cameraController.offset;
        float revealZoom = sceneCamera != null
            ? Mathf.Max(cinematicZoomSize, cameraController.maxZoomSize + 0.1f)
            : 0f;
        yield return MoveCameraAndZoom(cameraController.transform, sceneCamera, from, to,
            sceneCamera != null ? sceneCamera.orthographicSize : 0f, revealZoom, cameraTravelSeconds);
        yield return new WaitForSecondsRealtime(cameraSettleSeconds);

        yield return FadeLocationTitle(0f, 1f, locationTitleFadeInSeconds);
        yield return new WaitForSecondsRealtime(locationTitleHoldSeconds);
        yield return FadeLocationTitle(1f, 0f, locationTitleFadeOutSeconds);
        yield return new WaitForSecondsRealtime(pauseAfterTitleSeconds);

        yield return FadeCutscene(0f, 1f, fadeToBlackSeconds);
        yield return new WaitForSecondsRealtime(fadeBlackHoldSeconds);

        // Trong lúc đen, Host đã gom cả đội về người nhặt map và đồng bộ vị trí mới.
        // Gán lại target local để xử lý cả trường hợp người chơi vừa bị đưa ra khỏi xe.
        Transform localPlayer = PlayerMovement.LocalPlayerInstance != null
            ? PlayerMovement.LocalPlayerInstance.transform
            : initialTarget;
        cameraController.SetTarget(localPlayer);
        if (sceneCamera != null) sceneCamera.orthographicSize = playerZoom;
        cameraController.enabled = true;
        yield return FadeCutscene(1f, 0f, fadeFromBlackSeconds);

        localFadeAlpha = 0f;
        localClueNoticeAlpha = 0f;
        localLocationTitleAlpha = 0f;
        focusRoutine = null;
    }

    private IEnumerator ShowClueNotice(float duration)
    {
        float fadeIn = Mathf.Min(0.3f, duration * 0.25f);
        float fadeOut = Mathf.Min(0.45f, duration * 0.35f);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            if (elapsed < fadeIn)
                localClueNoticeAlpha = CinematicEase(elapsed / Mathf.Max(0.001f, fadeIn));
            else if (elapsed > duration - fadeOut)
                localClueNoticeAlpha = 1f - CinematicEase((elapsed - (duration - fadeOut)) /
                                                          Mathf.Max(0.001f, fadeOut));
            else
                localClueNoticeAlpha = 1f;
            yield return null;
        }
        localClueNoticeAlpha = 0f;
    }

    private static IEnumerator MoveCameraAndZoom(Transform cameraTransform, Camera sceneCamera,
        Vector3 from, Vector3 to, float zoomFrom, float zoomTo, float duration)
    {
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float eased = CinematicEase(elapsed / Mathf.Max(0.001f, duration));
            cameraTransform.position = Vector3.LerpUnclamped(from, to, eased);
            if (sceneCamera != null)
                sceneCamera.orthographicSize = Mathf.LerpUnclamped(zoomFrom, zoomTo, eased);
            yield return null;
        }
        cameraTransform.position = to;
        if (sceneCamera != null) sceneCamera.orthographicSize = zoomTo;
    }

    private IEnumerator FadeLocationTitle(float from, float to, float duration)
    {
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            localLocationTitleAlpha = Mathf.LerpUnclamped(from, to,
                CinematicEase(elapsed / Mathf.Max(0.001f, duration)));
            yield return null;
        }
        localLocationTitleAlpha = to;
    }

    private IEnumerator FadeCutscene(float from, float to, float duration)
    {
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            localFadeAlpha = Mathf.LerpUnclamped(from, to,
                CinematicEase(elapsed / Mathf.Max(0.001f, duration)));
            yield return null;
        }
        localFadeAlpha = to;
    }

    /// <summary>
    /// Quintic smoother-step: velocity and acceleration are both zero at the
    /// beginning and end, producing an After Effects Easy Ease/F9-like move.
    /// </summary>
    private static float CinematicEase(float value)
    {
        float t = Mathf.Clamp01(value);
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    private void OnGUI()
    {
        if (localQuestEventAlpha > 0.001f) DrawQuestEventNotice();
        if (localClueNoticeAlpha > 0.001f) DrawClueNotice();
        if (localLocationTitleAlpha > 0.001f) DrawMilitaryLocationTitle();

        if (localFadeAlpha > 0.001f)
        {
            int previousDepth = GUI.depth;
            Color previousColor = GUI.color;
            GUI.depth = -2000;
            GUI.color = new Color(0f, 0f, 0f, localFadeAlpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previousColor;
            GUI.depth = previousDepth;
        }

        if (!showBuiltInQuestHud || TutorialSession.IsActive) return;

        if (CurrentStage == QuestStage.CityMapFound && !IsQuestCutsceneActive && localFadeAlpha < 0.001f)
            DrawMilitaryDirectionMarker();

        bool isPreMilitaryObjective = CurrentStage == QuestStage.SearchNeighborhood ||
                                      CurrentStage == QuestStage.LocateOffice ||
                                      CurrentStage == QuestStage.FindCityMap;
        string objective;
        QuestFlowUIPrototype journal = QuestFlowUIPrototype.Instance;
        if (isPreMilitaryObjective && journal != null)
        {
            // The journal's Follow button owns HUD visibility. This gives the
            // click an immediate gameplay effect instead of being cosmetic only.
            if (!journal.TryGetTrackedObjectiveText(out objective))
                return;
        }
        else
        {
            objective = CurrentStage switch
            {
                QuestStage.SearchNeighborhood =>
                    $"Tìm kiếm manh mối trong các ngôi nhà  •  {RouteClueCount}/{PreMilitaryQuestProgress.RequiredRouteClues}",
                QuestStage.LocateOffice => "Tìm văn phòng màu tím trong khu vực đã xác định",
                QuestStage.FindCityMap => GameLocalization.Get("quest.find_map"),
                QuestStage.CityMapFound => GameLocalization.Get("quest.reach_military"),
                _ => string.Empty
            };
        }
        if (string.IsNullOrEmpty(objective)) return;

        GUIStyle style = new GUIStyle(GUI.skin.box)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        GUI.Box(new Rect(Screen.width * 0.5f - 260f, 24f, 520f, 38f), objective, style);
    }

    private void DrawQuestEventNotice()
    {
        int previousDepth = GUI.depth;
        Color previousColor = GUI.color;
        GUI.depth = -1450;

        float width = Mathf.Min(760f, Screen.width - 40f);
        float height = 88f;
        Rect panel = new Rect((Screen.width - width) * 0.5f, 78f, width, height);
        GUI.color = new Color(0.015f, 0.02f, 0.02f, localQuestEventAlpha * 0.9f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = new Color(1f, 0.67f, 0.14f, localQuestEventAlpha);
        GUI.DrawTexture(new Rect(panel.x, panel.y, 4f, panel.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 2f), Texture2D.whiteTexture);

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.025f), 18, 28),
            fontStyle = FontStyle.Bold
        };
        GUIStyle bodyStyle = new GUIStyle(titleStyle)
        {
            fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.016f), 13, 18),
            fontStyle = FontStyle.Normal
        };
        DrawShadowedLabel(new Rect(panel.x + 14f, panel.y + 8f, panel.width - 28f, 34f),
            localQuestEventTitle, titleStyle, new Color(1f, 0.76f, 0.27f), localQuestEventAlpha, 2f);
        DrawShadowedLabel(new Rect(panel.x + 14f, panel.y + 43f, panel.width - 28f, 30f),
            localQuestEventBody, bodyStyle, new Color(0.94f, 0.95f, 0.94f), localQuestEventAlpha, 1f);

        GUI.color = previousColor;
        GUI.depth = previousDepth;
    }

    private void DrawClueNotice()
    {
        int previousDepth = GUI.depth;
        Color previousColor = GUI.color;
        GUI.depth = -1400;

        float width = Mathf.Min(760f, Screen.width - 40f);
        float height = 104f;
        Rect panel = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.27f, width, height);

        GUI.color = new Color(0.015f, 0.02f, 0.025f, localClueNoticeAlpha * 0.82f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = new Color(1f, 0.78f, 0.16f, localClueNoticeAlpha);
        GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(panel.x, panel.yMax - 2f, panel.width, 2f), Texture2D.whiteTexture);

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.032f), 23, 34),
            fontStyle = FontStyle.Bold
        };
        GUIStyle subtitleStyle = new GUIStyle(titleStyle)
        {
            fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.018f), 14, 20),
            fontStyle = FontStyle.Normal
        };

        DrawShadowedLabel(new Rect(panel.x + 12f, panel.y + 10f, panel.width - 24f, 48f),
            GameLocalization.Get("quest.clue_title"), titleStyle,
            new Color(1f, 0.82f, 0.2f), localClueNoticeAlpha, 2f);
        DrawShadowedLabel(new Rect(panel.x + 12f, panel.y + 56f, panel.width - 24f, 30f),
            GameLocalization.Get("quest.clue_subtitle"), subtitleStyle,
            new Color(0.92f, 0.94f, 0.96f), localClueNoticeAlpha, 1f);

        GUI.color = previousColor;
        GUI.depth = previousDepth;
    }

    private void DrawMilitaryLocationTitle()
    {
        int previousDepth = GUI.depth;
        Color previousColor = GUI.color;
        GUI.depth = -1300;

        float centerY = Screen.height * 0.7f;
        float bandHeight = 142f;
        GUI.color = new Color(0f, 0f, 0f, localLocationTitleAlpha * 0.48f);
        GUI.DrawTexture(new Rect(0f, centerY - bandHeight * 0.5f, Screen.width, bandHeight),
            Texture2D.whiteTexture);

        float lineWidth = Mathf.Min(820f, Screen.width * 0.72f);
        GUI.color = new Color(0.92f, 0.73f, 0.2f, localLocationTitleAlpha * 0.9f);
        GUI.DrawTexture(new Rect((Screen.width - lineWidth) * 0.5f, centerY - 54f, lineWidth, 1f),
            Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect((Screen.width - lineWidth) * 0.5f, centerY + 54f, lineWidth, 1f),
            Texture2D.whiteTexture);

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.038f), 28, 42),
            fontStyle = FontStyle.Bold
        };
        GUIStyle subtitleStyle = new GUIStyle(titleStyle)
        {
            fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.021f), 16, 24),
            fontStyle = FontStyle.Normal
        };

        DrawShadowedLabel(new Rect(20f, centerY - 47f, Screen.width - 40f, 52f),
            GameLocalization.Get("quest.military_title"), titleStyle,
            new Color(1f, 0.86f, 0.47f), localLocationTitleAlpha, 3f);
        DrawShadowedLabel(new Rect(20f, centerY + 5f, Screen.width - 40f, 35f),
            GameLocalization.Get("quest.military_subtitle"), subtitleStyle,
            new Color(0.96f, 0.96f, 0.96f), localLocationTitleAlpha, 2f);

        GUI.color = previousColor;
        GUI.depth = previousDepth;
    }

    private void DrawMilitaryDirectionMarker()
    {
        if (khuVucQuanSuFocus == null) return;

        Camera sceneCamera = Camera.main;
        if (sceneCamera == null && PZ_CameraController.Instance != null)
            sceneCamera = PZ_CameraController.Instance.GetComponentInChildren<Camera>();
        if (sceneCamera == null) return;

        Vector3 screen3 = sceneCamera.WorldToScreenPoint(khuVucQuanSuFocus.position);
        Vector2 targetGui = new Vector2(screen3.x, Screen.height - screen3.y);
        const float horizontalMargin = 68f;
        const float topMargin = 92f;
        const float bottomMargin = 74f;
        bool isOnScreen = screen3.z > 0f && targetGui.x >= horizontalMargin &&
                          targetGui.x <= Screen.width - horizontalMargin && targetGui.y >= topMargin &&
                          targetGui.y <= Screen.height - bottomMargin;

        int previousDepth = GUI.depth;
        Color previousColor = GUI.color;
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.depth = -900;

        float pulse = 0.82f + (Mathf.Sin(Time.unscaledTime * 3.2f) + 1f) * 0.09f;
        GUIStyle arrowStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 38,
            fontStyle = FontStyle.Bold
        };
        GUIStyle markerStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 13,
            fontStyle = FontStyle.Bold
        };

        Vector2 markerPosition;
        if (isOnScreen)
        {
            markerPosition = targetGui + new Vector2(0f, -42f + Mathf.Sin(Time.unscaledTime * 3.2f) * 4f);
            DrawShadowedLabel(new Rect(markerPosition.x - 25f, markerPosition.y - 25f, 50f, 50f),
                "▼", arrowStyle, new Color(1f, 0.82f, 0.12f), pulse, 2f);
        }
        else
        {
            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 direction = targetGui - center;
            if (screen3.z < 0f) direction = -direction;
            if (direction.sqrMagnitude < 0.001f) direction = Vector2.up;

            float availableX = Screen.width * 0.5f - horizontalMargin;
            float availableY = Screen.height * 0.5f - bottomMargin;
            float scaleX = availableX / Mathf.Max(0.001f, Mathf.Abs(direction.x));
            float scaleY = availableY / Mathf.Max(0.001f, Mathf.Abs(direction.y));
            markerPosition = center + direction * Mathf.Min(scaleX, scaleY);
            markerPosition.y = Mathf.Clamp(markerPosition.y, topMargin, Screen.height - bottomMargin);

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            GUIUtility.RotateAroundPivot(angle, markerPosition);
            DrawShadowedLabel(new Rect(markerPosition.x - 25f, markerPosition.y - 25f, 50f, 50f),
                "▶", arrowStyle, new Color(1f, 0.82f, 0.12f), pulse, 2f);
            GUI.matrix = previousMatrix;
        }

        float distance = PlayerMovement.LocalPlayerInstance != null
            ? Vector2.Distance(PlayerMovement.LocalPlayerInstance.transform.position, khuVucQuanSuFocus.position)
            : 0f;
        string markerText = distance > 0.1f
            ? $"{GameLocalization.Get("quest.military_marker")}  •  {distance:0} m"
            : GameLocalization.Get("quest.military_marker");
        float labelWidth = 190f;
        float labelX = Mathf.Clamp(markerPosition.x - labelWidth * 0.5f, 8f, Screen.width - labelWidth - 8f);
        float labelY = isOnScreen ? markerPosition.y - 34f : markerPosition.y + 31f;
        labelY = Mathf.Clamp(labelY, 50f, Screen.height - 36f);
        GUI.color = new Color(0.03f, 0.035f, 0.04f, 0.88f);
        GUI.Box(new Rect(labelX, labelY, labelWidth, 28f), markerText, markerStyle);

        GUI.matrix = previousMatrix;
        GUI.color = previousColor;
        GUI.depth = previousDepth;
    }

    private static void DrawShadowedLabel(Rect rect, string text, GUIStyle style, Color color,
        float alpha, float shadowOffset)
    {
        Color previousColor = GUI.color;
        style.normal.textColor = Color.white;

        GUI.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha) * 0.9f);
        GUI.Label(new Rect(rect.x + shadowOffset, rect.y + shadowOffset, rect.width, rect.height), text, style);
        GUI.color = new Color(color.r, color.g, color.b, color.a * Mathf.Clamp01(alpha));
        GUI.Label(rect, text, style);

        GUI.color = previousColor;
    }
}
