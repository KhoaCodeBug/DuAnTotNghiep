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

    public enum HospitalInvestigationStage
    {
        NotStarted,
        FindShiftLog,
        FindShiftLog2,
        FindRadioKey,
        UnlockRadioRoom,
        RadioReady
    }

    public enum CivilianRouteStage
    {
        PreparingCar,
        CarReady,
        ExploringExits,
        AwaitingTeam,
        EscapeRun,
        Completed
    }

    public const int MaximumSearchHouses = PreMilitaryQuestProgress.MaximumSearchHouses;
    private const int MaximumCabinetSearchPoints = 32;

    public static MainQuestManager Instance { get; private set; }

    [Header("Military-zone reveal")]
    [Tooltip("Điểm KhuVucQuanSu mà camera của mọi người chơi sẽ nhìn tới sau khi tìm thấy bản đồ.")]
    [SerializeField] private Transform khuVucQuanSuFocus;
    [SerializeField, Min(1f)] private float militaryMarkerArrivalDistance = 15f;
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

    [Header("Hospital Radio restoration")]
    [Tooltip("Thời gian một hoặc nhiều người chơi cần giữ E tổng cộng để phục hồi tín hiệu.")]
    [SerializeField, Min(1f)] private float hospitalRadioRestoreDuration = 14f;
    [Tooltip("Bán kính tiếng nhiễu ở hai mốc đầu thu hút zombie đang có quanh bệnh viện.")]
    [SerializeField, Min(1f)] private float hospitalRadioNoiseRadius = 28f;
    [SerializeField, Min(0.05f)] private float hospitalRadioZombieSpawnDelay = 0.25f;
    [SerializeField, Min(0.1f)] private float hospitalRadioZombieHorizontalSpacing = 0.8f;

    [Header("Arrival car completion")]
    [Tooltip("Fusion vehicle spawned over the broken story car after the required repair is complete.")]
    [SerializeField] private NetworkPrefabRef repairedArrivalCarPrefab;
    [Tooltip("Sau bộ 5 món được bảo đảm lúc khởi tạo, mỗi loại có cơ hội sinh thêm một bản để hỗ trợ trao đổi co-op.")]
    [SerializeField, Range(0f, 1f)] private float arrivalCarDuplicateItemChance = 0.35f;

    [Header("Civilian escape finale")]
    [Tooltip("Điểm tập kết đầu tiên của tuyến A. Có thể kéo CivilianRouteCheckpoint trong scene Main.")]
    [SerializeField] private Transform civilianEscapeExit;
    [Tooltip("Mốc chỉ hướng con đường rời thành phố. Outro bắt đầu tại Checkpoint; xe không cần chạm mốc này.")]
    [SerializeField] private Transform civilianCityExit;
    [Tooltip("Điểm chiếc xe chạy tới trong cảnh outro. Đây chỉ là mốc trình diễn, không phải trigger gameplay.")]
    [SerializeField] private Transform civilianOutroEnd;
    [SerializeField] private Vector2 civilianEscapeFallbackOffset = new Vector2(30f, 0f);
    [SerializeField, Min(1f)] private float civilianEscapeTriggerRadius = 2.75f;
    [SerializeField, Min(1f)] private float civilianTeamGatherRadius = 6f;

    [Networked] public int NetworkQuestStage { get; set; }
    [Networked] public int MapCabinetId { get; set; }
    [Networked] public NetworkBool IsCityMapUnlocked { get; set; }
    [Networked] public NetworkBool IsMilitaryRevealPlaying { get; set; }
    [Networked] public NetworkBool IsArrivalCarInspected { get; set; }
    [Networked] public int ArrivalCarRepairMask { get; set; }
    [Networked] public NetworkBool ArrivalCarRepairSessionActive { get; private set; }
    [Networked] public int ActiveArrivalCarRepairActionValue { get; private set; }
    [Networked] public PlayerRef ActiveArrivalCarRepairer { get; private set; }
    [Networked] public TickTimer ArrivalCarRepairTimer { get; private set; }
    [Networked] public float ArrivalCarRepairDurationSeconds { get; private set; }
    [Networked] public NetworkBool IsArrivalCarRepaired { get; set; }
    [Networked] public NetworkObject RepairedArrivalCarObject { get; set; }
    [Networked] public int LockedEscapeRouteValue { get; private set; }
    [Networked] public NetworkBool IsCivilianEscapeComplete { get; private set; }
    [Networked] public int CivilianRouteStageValue { get; private set; }
    [Networked] public Vector2 CivilianCheckpointPosition { get; private set; }
    [Networked] public Vector2 CivilianCityExitPosition { get; private set; }
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
    [Networked] public NetworkBool IsHospitalRadioDoorOpen { get; set; }
    [Networked] public int NetworkHospitalInvestigationStage { get; set; }
    [Networked] public NetworkBool HasHospitalRadioKey { get; set; }
    [Networked] public int SelectedHospitalRadioKeyLootId { get; private set; }
    [Networked] public float HospitalRadioRestoreSeconds { get; private set; }
    [Networked] public PlayerRef HospitalRadioOperator { get; private set; }
    [Networked] public NetworkBool IsHospitalRadioRecovered { get; private set; }
    [Networked] public int HospitalRadioCheckpointCount { get; private set; }
    [Networked] public int HospitalRadioThreatSpawnCount { get; private set; }

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
    private Coroutine authorityHospitalRadioSpawnRoutine;
    private Transform cachedHospitalZombieEntryA;
    private Transform cachedHospitalZombieEntryB;
    private MainQuestStartTrigger activeHospitalEntryTrigger;
    private float nextHospitalBackpackRewardScanTime;
    private readonly List<NetworkPrefabRef> cachedHospitalRadioZombiePrefabs = new List<NetworkPrefabRef>();
    private readonly List<HospitalRadioKeyLootPoint> cachedHospitalRadioKeyLootPoints =
        new List<HospitalRadioKeyLootPoint>();
    private readonly Dictionary<int, int> cabinetIndexById = new Dictionary<int, int>();
    private bool hasSpawned;
    private bool hasGeneratedCivilianEscapeFallback;
    private bool localMilitaryDestinationReached;

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
    public bool IsHospitalRadioDoorOpenState => IsNetworkReady && IsHospitalRadioDoorOpen;
    public HospitalInvestigationStage CurrentHospitalInvestigationStage => IsNetworkReady
        ? (HospitalInvestigationStage)NetworkHospitalInvestigationStage
        : HospitalInvestigationStage.NotStarted;
    public bool HasHospitalRadioKeyState => IsNetworkReady && HasHospitalRadioKey;
    public int SelectedHospitalRadioKeyLootIdState => IsNetworkReady ? SelectedHospitalRadioKeyLootId : 0;
    public float HospitalRadioRestoreDuration => hospitalRadioRestoreDuration;
    public float HospitalRadioRestoreSecondsState => IsNetworkReady ? HospitalRadioRestoreSeconds : 0f;
    public float HospitalRadioRestoreNormalized => hospitalRadioRestoreDuration <= 0f
        ? 0f
        : Mathf.Clamp01(HospitalRadioRestoreSecondsState / hospitalRadioRestoreDuration);
    public bool HasHospitalRadioOperator => IsNetworkReady && HospitalRadioOperator != PlayerRef.None;
    public bool IsLocalPlayerHospitalRadioOperator => HasHospitalRadioOperator && Runner != null &&
                                                       Runner.LocalPlayer == HospitalRadioOperator;
    public bool IsHospitalRadioRecoveredState => IsNetworkReady && IsHospitalRadioRecovered;
    public int HospitalRadioCheckpointCountState => IsNetworkReady
        ? Mathf.Clamp(HospitalRadioCheckpointCount, 0, HospitalRadioRoomRules.RestoreSegmentCount)
        : 0;
    public int HospitalRadioThreatSpawnCountState => IsNetworkReady ? HospitalRadioThreatSpawnCount : 0;
    public bool IsQuestCutsceneActive => IsNetworkReady && IsMilitaryRevealPlaying;
    public bool IsNeighborhoodSearchActive => IsNetworkReady && CurrentStage == QuestStage.SearchNeighborhood;
    public int SearchedHouseCount => CountBits(SearchedHouseMask);
    public int RouteClueCount => CountBits(RouteClueMask);
    public int CurrentOfficeInvestigationStep => GetCurrentOfficeInvestigationStep();
    public bool HasMapFragment1 => RouteClueCount >= PreMilitaryQuestProgress.RequiredRouteClues;
    public bool AreArrivalCarRequiredRepairsComplete => IsNetworkReady &&
        ArrivalCarRepairRules.IsRequiredRepairComplete(ArrivalCarRepairMask);
    public bool IsLocalPlayerRepairingArrivalCar => IsNetworkReady && ArrivalCarRepairSessionActive &&
                                                    ActiveArrivalCarRepairer != PlayerRef.None &&
                                                    Runner.LocalPlayer == ActiveArrivalCarRepairer;
    public float ArrivalCarRepairProgressNormalized
    {
        get
        {
            if (!IsNetworkReady || !ArrivalCarRepairSessionActive || ArrivalCarRepairDurationSeconds <= 0f)
                return 0f;
            float remaining = ArrivalCarRepairTimer.RemainingTime(Runner) ?? 0f;
            return 1f - Mathf.Clamp01(remaining / ArrivalCarRepairDurationSeconds);
        }
    }
    private EscapeEndingRoute localLockedEscapeRoute = EscapeEndingRoute.None;
    public EscapeEndingRoute LocalLockedEscapeRoute
    {
        get => localLockedEscapeRoute;
        set => localLockedEscapeRoute = value;
    }
    public EscapeEndingRoute LockedEscapeRoute => IsNetworkReady
        ? (EscapeEndingRoute)LockedEscapeRouteValue
        : localLockedEscapeRoute;
    public CivilianRouteStage CurrentCivilianRouteStage => IsNetworkReady
        ? (CivilianRouteStage)CivilianRouteStageValue
        : CivilianRouteStage.PreparingCar;
    public bool IsCivilianCityMapUnlocked => IsNetworkReady &&
        CurrentCivilianRouteStage >= CivilianRouteStage.CarReady;
    public bool IsSearchNeighborhoodBoundaryActive => IsNetworkReady &&
        CurrentStage == QuestStage.SearchNeighborhood && !IsCivilianCityMapUnlocked;
    public Vector2 CivilianEscapePosition => IsCivilianCityMapUnlocked
        ? CivilianCheckpointPosition
        : ResolveCivilianEscapeExit().position;
    public float CivilianEscapeTriggerRadius => civilianEscapeTriggerRadius;
    public float CivilianTeamGatherRadius => civilianTeamGatherRadius;
    public Vector2 CivilianOutroEndPosition
    {
        get
        {
            if (civilianOutroEnd != null) return civilianOutroEnd.position;
            Vector2 direction = CivilianCityExitPosition - CivilianCheckpointPosition;
            if (direction.sqrMagnitude < 0.01f) direction = Vector2.right;
            return CivilianCityExitPosition + direction.normalized * 14f;
        }
    }

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
            IsArrivalCarInspected = false;
            ArrivalCarRepairMask = 0;
            ArrivalCarRepairSessionActive = false;
            ActiveArrivalCarRepairActionValue = -1;
            ActiveArrivalCarRepairer = PlayerRef.None;
            ArrivalCarRepairTimer = TickTimer.None;
            ArrivalCarRepairDurationSeconds = 0f;
            IsArrivalCarRepaired = false;
            RepairedArrivalCarObject = null;
            LockedEscapeRouteValue = (int)EscapeEndingRoute.None;
            IsCivilianEscapeComplete = false;
            CivilianRouteStageValue = (int)CivilianRouteStage.PreparingCar;
            CivilianCheckpointPosition = default;
            CivilianCityExitPosition = default;
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
            IsHospitalRadioDoorOpen = false;
            NetworkHospitalInvestigationStage = (int)HospitalInvestigationStage.NotStarted;
            HasHospitalRadioKey = false;
            SelectedHospitalRadioKeyLootId = 0;
            HospitalRadioRestoreSeconds = 0f;
            HospitalRadioOperator = PlayerRef.None;
            IsHospitalRadioRecovered = false;
            HospitalRadioCheckpointCount = 0;
            HospitalRadioThreatSpawnCount = 0;
            activeHospitalEntryTrigger = null;
            nextHospitalBackpackRewardScanTime = 0f;
        }

        ApplyMapAccess();
        CivilianEscapeRouteController.Attach(this);
        CivilianRoutePresentationController.Attach(this);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        hasSpawned = false;
        if (focusRoutine != null) StopCoroutine(focusRoutine);
        if (authoritySafetyRoutine != null) StopCoroutine(authoritySafetyRoutine);
        if (authorityHospitalRadioSpawnRoutine != null) StopCoroutine(authorityHospitalRadioSpawnRoutine);
        focusRoutine = null;
        authoritySafetyRoutine = null;
        authorityHospitalRadioSpawnRoutine = null;
        activeHospitalEntryTrigger = null;
        nextHospitalBackpackRewardScanTime = 0f;
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
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.F8))
            EditorGrantMissingArrivalCarRepairItems();
#endif
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        TickArrivalCarRepair();
        TickHospitalRadioRestore();
        TickHospitalBackpackRewards();

        if (!IsArrivalCarRepaired || RepairedArrivalCarObject == null || IsCivilianEscapeComplete)
            return;

        float checkpointDistance = Vector2.Distance(RepairedArrivalCarObject.transform.position,
            CivilianCheckpointPosition);
        switch (CurrentCivilianRouteStage)
        {
            case CivilianRouteStage.CarReady:
                CivilianRouteStageValue = (int)CivilianRouteStage.ExploringExits;
                break;
            case CivilianRouteStage.ExploringExits:
                if (checkpointDistance <= civilianEscapeTriggerRadius)
                {
                    CivilianRouteStageValue = (int)CivilianRouteStage.AwaitingTeam;
                    RPC_ShowLocalizedQuestMessage("quest.route_a_regroup", 0, 0);
                }
                break;
            case CivilianRouteStage.AwaitingTeam:
                if (checkpointDistance > civilianEscapeTriggerRadius * 1.75f)
                    CivilianRouteStageValue = (int)CivilianRouteStage.ExploringExits;
                break;
        }
    }

    /// <summary>
    /// Runtime test shortcut that completes the residential clue-search
    /// objective. Clients forward the request to State Authority so F7 behaves
    /// consistently in Solo, Host and multiplayer builds.
    /// </summary>
    public void DebugCompleteClueSearch()
    {
        if (!IsNetworkReady)
        {
            Debug.LogWarning("[QUEST TEST] F7 chưa dùng được vì hệ thống nhiệm vụ chưa sẵn sàng.");
            return;
        }

        if (HasStateAuthority)
            ServerDebugCompleteClueSearch(Runner.LocalPlayer);
        else
            RPC_RequestDebugCompleteClueSearch();
    }

    /// <summary>
    /// Host/Solo development helper. It moves only the local authoritative
    /// player and intentionally refuses objectives backed by LootContainers.
    /// </summary>
    public void DebugTeleportToCurrentObjective()
    {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        return;
#else
        if (!IsNetworkReady || !HasStateAuthority ||
            !TryGetRequestingPlayer(Runner.LocalPlayer, out PlayerMovement player))
        {
            Debug.LogWarning("[QUEST TEST] F12 cần Solo/Host và player local đã spawn.");
            return;
        }

        Vector2 destination;
        string targetName;
        switch (CurrentStage)
        {
            case QuestStage.NotStarted:
                if (BrokenArrivalCar.Instance == null)
                {
                    Debug.LogWarning("[QUEST TEST] Không tìm thấy xe hỏng đầu game.");
                    return;
                }
                destination = BrokenArrivalCar.Instance.InspectionZoneWorldCenter;
                targetName = "vùng kiểm tra xe đầu game";
                break;

            case QuestStage.SearchNeighborhood:
                Debug.LogWarning("[QUEST TEST] F12 không dịch chuyển: mục tiêu hiện tại cần tìm hồ sơ trong LootContainer.");
                AutoChatManager.Instance?.AddMessage("QUEST TEST",
                    "F12 bị bỏ qua: nhiệm vụ tìm hồ sơ dùng LootContainer.");
                return;

            case QuestStage.LocateOffice:
                GameObject hospitalTeleport = GameObject.Find("TeleportToHospital");
                if (hospitalTeleport != null)
                {
                    destination = hospitalTeleport.transform.position;
                    targetName = "điểm vào bệnh viện";
                    break;
                }

                MainQuestStartTrigger office = FindFirstObjectByType<MainQuestStartTrigger>();
                if (office == null)
                {
                    Debug.LogWarning("[QUEST TEST] Không tìm thấy TeleportToHospital hoặc vùng Khu Điều phối.");
                    return;
                }
                destination = office.transform.position;
                targetName = "Khu Điều phối trong bệnh viện";
                break;

            case QuestStage.FindCityMap:
                Transform hospitalTarget = ResolveCurrentHospitalObjective();
                if (hospitalTarget == null)
                {
                    Debug.LogWarning("[QUEST TEST] Không tìm thấy anchor H2 hiện tại trong bệnh viện.");
                    return;
                }
                destination = (Vector2)hospitalTarget.position + new Vector2(0f, -0.45f);
                targetName = GetHospitalObjectiveLabel(CurrentHospitalInvestigationStage);
                break;

            case QuestStage.CityMapFound:
                MilitaryBaseQuestManager military = MilitaryBaseQuestManager.Instance;
                if (military == null || !military.IsNetworkReady)
                {
                    Debug.LogWarning("[QUEST TEST] Hệ thống căn cứ quân sự chưa sẵn sàng.");
                    return;
                }
                military.DebugTeleportToCurrentObjective();
                return;

            default:
                return;
        }

        DebugTeleportPlayer(player, destination);
        Debug.Log($"[QUEST TEST] F12: đã dịch chuyển tới {targetName}.");
        AutoChatManager.Instance?.AddMessage("QUEST TEST", $"Đã dịch chuyển tới {targetName}.");
#endif
    }

    private static void DebugTeleportPlayer(PlayerMovement player, Vector2 destination)
    {
        PlayerInteraction interaction = player != null ? player.GetComponent<PlayerInteraction>() : null;
        if (interaction != null && interaction.IsInVehicle)
        {
            VehicleControllerFusion vehicle = interaction.CurrentVehicleController;
            bool exited = vehicle != null && vehicle.AuthorityTryExit(player.Object);
            if (!exited)
                interaction.SetVehicleNetworkState(null, false, false, 0, destination);
        }
        TeleportPlayer(player, destination);
        Physics2D.SyncTransforms();
    }

    private Transform ResolveCurrentHospitalObjective()
    {
        switch (CurrentHospitalInvestigationStage)
        {
            case HospitalInvestigationStage.FindShiftLog:
                return HospitalQuestClueInteractionPoint.TryGetForRole(HospitalQuestClueRole.ShiftLog,
                    out HospitalQuestClueInteractionPoint shiftLog) ? shiftLog.transform : null;
            case HospitalInvestigationStage.FindShiftLog2:
                return HospitalQuestClueInteractionPoint.TryGetForRole(HospitalQuestClueRole.ShiftLog2,
                    out HospitalQuestClueInteractionPoint shiftLog2) ? shiftLog2.transform : null;
            case HospitalInvestigationStage.FindRadioKey:
                return HospitalRadioKeyLootPoint.TryGet(SelectedHospitalRadioKeyLootIdState,
                    out HospitalRadioKeyLootPoint keyLoot) ? keyLoot.transform : null;
            case HospitalInvestigationStage.UnlockRadioRoom:
                return HospitalRadioInteractionPoint.TryGetForRole(HospitalRadioInteractionRole.Door,
                    out HospitalRadioInteractionPoint door) ? door.transform : null;
            case HospitalInvestigationStage.RadioReady:
                return HospitalRadioInteractionPoint.TryGetForRole(HospitalRadioInteractionRole.Radio,
                    out HospitalRadioInteractionPoint radio) ? radio.transform : null;
            default:
                return null;
        }
    }

    private static string GetHospitalObjectiveLabel(HospitalInvestigationStage stage)
    {
        bool vietnamese = QuestUILocalization.IsVietnamese;
        return stage switch
        {
            HospitalInvestigationStage.FindShiftLog => vietnamese ? "sổ trực tại quầy tiếp tân" : "reception shift log",
            HospitalInvestigationStage.FindShiftLog2 => vietnamese ? "văn phòng trưởng ca" : "chief-shift office",
            HospitalInvestigationStage.FindRadioKey => vietnamese ? "chìa khóa Radio dự phòng" : "backup Radio key",
            HospitalInvestigationStage.UnlockRadioRoom => vietnamese ? "cửa Trạm Radio phụ trợ" : "auxiliary Radio room door",
            HospitalInvestigationStage.RadioReady => vietnamese ? "thiết bị Radio" : "Radio console",
            _ => vietnamese ? "Khu Điều phối" : "Coordination Section"
        };
    }

    /// <summary>
    /// Developer-only presentation path used by F6/CheatMenu. It advances one
    /// authoritative Route-B beat without creating or modifying LootContainers.
    /// Natural gameplay continues to use the validated interaction methods.
    /// </summary>
    public void DebugAdvanceRouteB()
    {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        return;
#else
        if (!IsNetworkReady || !HasStateAuthority)
        {
            Debug.LogWarning("[QUEST TEST] NEXT ROUTE B chỉ dùng được trên Solo/Host đang có authority.");
            return;
        }

        PlayerRef requester = Runner.LocalPlayer;
        switch (CurrentStage)
        {
            case QuestStage.NotStarted:
                if (!IsArrivalCarInspected)
                {
                    IsArrivalCarInspected = true;
                    RPC_ShowArrivalCarInspected(requester);
                    Debug.Log("[QUEST TEST] F6: đã hoàn tất kiểm tra xe. Chờ khu dân cư khởi tạo rồi nhấn F6 tiếp.");
                }
                break;

            case QuestStage.SearchNeighborhood:
                for (int clueIndex = 0; clueIndex < PreMilitaryQuestProgress.RequiredRouteClues; clueIndex++)
                {
                    int bit = 1 << clueIndex;
                    if ((RouteClueMask & bit) != 0) continue;
                    AuthorityRegisterRouteClue((QuestRouteClueKind)clueIndex, requester, false);
                    Debug.Log($"[QUEST TEST] F6: mô phỏng nhận tài liệu Tuyến B {clueIndex + 1}/3, không đụng LootContainer.");
                    return;
                }
                break;

            case QuestStage.LocateOffice:
                AuthorityStartOfficeInvestigation(requester);
                Debug.Log("[QUEST TEST] F6: mô phỏng đã tới Khu Điều phối trong bệnh viện.");
                break;

            case QuestStage.FindCityMap:
                switch (CurrentHospitalInvestigationStage)
                {
                    case HospitalInvestigationStage.FindShiftLog:
                        AuthorityCompleteHospitalClue(HospitalQuestClueRole.ShiftLog, requester);
                        break;
                    case HospitalInvestigationStage.FindShiftLog2:
                        AuthorityCompleteHospitalClue(HospitalQuestClueRole.ShiftLog2, requester);
                        break;
                    case HospitalInvestigationStage.FindRadioKey:
                        AuthorityDebugCollectHospitalRadioKey(requester);
                        break;
                    case HospitalInvestigationStage.UnlockRadioRoom:
                        AuthorityOpenHospitalRadioRoom(requester);
                        break;
                    case HospitalInvestigationStage.RadioReady:
                        AuthorityDebugAdvanceHospitalRadioStage(requester);
                        break;
                }
                break;

            case QuestStage.CityMapFound:
                Debug.Log("[QUEST TEST] Nửa đầu Tuyến B đã xong. Dùng F10 hoặc CheatMenu để chạy phần căn cứ.");
                RPC_ShowDebugShortcutMessage(requester,
                    "Đã mở tuyến quân sự. Dùng F10 hoặc CheatMenu để chạy nhiệm vụ căn cứ.");
                break;
        }
#endif
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestDebugCompleteClueSearch(RpcInfo info = default)
    {
        ServerDebugCompleteClueSearch(info.Source);
    }

    private void ServerDebugCompleteClueSearch(PlayerRef requester)
    {
        if (!HasStateAuthority) return;

        if (CurrentStage != QuestStage.SearchNeighborhood)
        {
            const string unavailable = "F7 chỉ hoạt động khi nhiệm vụ tìm kiếm manh mối trong khu dân cư đang diễn ra.";
            Debug.Log("[QUEST TEST] " + unavailable);
            RPC_ShowDebugShortcutMessage(requester, unavailable);
            return;
        }

        int completeClueMask = (1 << PreMilitaryQuestProgress.RequiredRouteClues) - 1;
        RouteClueMask = completeClueMask;
        InsuredRouteClueMask |= completeClueMask;
        RouteClueDryOpenCount = 0;
        NetworkQuestStage = (int)QuestStage.LocateOffice;
        RPC_ShowAllRouteCluesFound(requester);

        const string message = "F7 đã thu thập đủ 3/3 manh mối; bắt đầu chuỗi hội thoại nhiệm vụ.";
        Debug.Log("[QUEST TEST] " + message);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowDebugShortcutMessage([RpcTarget] PlayerRef targetPlayer, string message)
    {
        AutoChatManager.Instance?.AddMessage("QUEST TEST", message);
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor-only shortcut for focused vehicle/quest testing. It grants only
    /// missing repair items and leaves every repair/start interaction intact.
    /// The method is compiled out of player builds.
    /// </summary>
    private void EditorGrantMissingArrivalCarRepairItems()
    {
        if (!IsNetworkReady || !HasStateAuthority)
        {
            Debug.LogWarning("[EDITOR TEST] F8 cấp vật phẩm sửa xe chỉ dùng được khi đang Play ở Solo/Host.");
            return;
        }

        PlayerMovement player = PlayerMovement.LocalPlayerInstance;
        InventorySystem inventory = player != null ? player.GetComponent<InventorySystem>() : null;
        if (inventory == null)
        {
            Debug.LogWarning("[EDITOR TEST] Chưa tìm thấy túi đồ của Player local.");
            return;
        }

        ArrivalCarItemKind[] requiredItems =
        {
            ArrivalCarItemKind.Toolbox,
            ArrivalCarItemKind.Hammer,
            ArrivalCarItemKind.FuelCan,
            ArrivalCarItemKind.Battery,
            ArrivalCarItemKind.Tire
        };

        int addedCount = 0;
        List<string> failedItems = new List<string>();
        for (int i = 0; i < requiredItems.Length; i++)
        {
            ArrivalCarItemKind kind = requiredItems[i];
            if (FindArrivalCarItem(inventory, kind) != null) continue;

            ItemData item = ArrivalCarItemCatalog.GetOrCreate(kind);
            if (item != null && inventory.AddItem(item, 1))
                addedCount++;
            else
                failedItems.Add(ArrivalCarItemCatalog.GetDisplayName(kind));
        }

        string message = failedItems.Count == 0
            ? $"F8 đã cấp {addedCount} món còn thiếu. Túi đồ hiện đủ 5/5 vật phẩm sửa xe."
            : $"F8 không thể cấp: {string.Join(", ", failedItems)}. Hãy dọn ô trống trong túi rồi thử lại.";

        Debug.Log("[EDITOR TEST] " + message);
        AutoChatManager.Instance?.AddMessage("EDITOR TEST", message);
    }
#endif

    /// <summary>
    /// State Authority chooses the opening neighborhood exactly once. The six
    /// stable scene IDs are replicated so every client and late joiner uses the
    /// same quest area regardless of its random spawn point.
    /// </summary>
    public bool TryInitializeNeighborhood(IReadOnlyList<string> houseIds)
    {
        if (!IsNetworkReady || !HasStateAuthority || !IsArrivalCarInspected || IsNeighborhoodConfigured ||
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
        DistributeArrivalCarRepairItems(houseIds, count);
        RPC_ShowLocalizedQuestMessage("quest.route_b_new_search", PreMilitaryQuestProgress.RequiredRouteClues, 0);
        return true;
    }

    public void RequestInspectArrivalCar()
    {
        if (!IsNetworkReady || IsArrivalCarInspected) return;
        if (HasStateAuthority) ServerInspectArrivalCar(Runner.LocalPlayer);
        else RPC_RequestInspectArrivalCar();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestInspectArrivalCar(RpcInfo info = default)
    {
        ServerInspectArrivalCar(info.Source);
    }

    private void ServerInspectArrivalCar(PlayerRef requester)
    {
        if (!HasStateAuthority || CurrentStage != QuestStage.NotStarted || IsArrivalCarInspected) return;
        BrokenArrivalCar car = BrokenArrivalCar.Instance;
        if (car == null || !TryGetRequestingPlayer(requester, out PlayerMovement player) ||
            !car.CanInspect(player.transform.position))
            return;

        IsArrivalCarInspected = true;
        RPC_ShowArrivalCarInspected(requester);
    }

    public void RequestRepairArrivalCarPart(string partId)
    {
        if (!IsNetworkReady || !ArrivalCarRepairRules.TryGetAction(partId, out ArrivalCarRepairAction action))
            return;
        if (HasStateAuthority) ServerRepairArrivalCarPart(Runner.LocalPlayer, action);
        else RPC_RequestRepairArrivalCarPart((int)action);
    }

    public void RequestCancelArrivalCarRepair()
    {
        if (!IsNetworkReady) return;
        if (HasStateAuthority) ServerCancelArrivalCarRepair(Runner.LocalPlayer);
        else RPC_RequestCancelArrivalCarRepair();
    }

    public void RequestStartArrivalCar()
    {
        if (!IsNetworkReady || IsArrivalCarRepaired) return;
        if (HasStateAuthority) ServerStartArrivalCar(Runner.LocalPlayer);
        else RPC_RequestStartArrivalCar();
    }

    public void RequestCivilianEscape()
    {
        if (!IsNetworkReady || !IsArrivalCarRepaired || IsCivilianEscapeComplete ||
            CurrentCivilianRouteStage != CivilianRouteStage.AwaitingTeam)
            return;
        if (HasStateAuthority) ServerBeginCivilianEscape(Runner.LocalPlayer);
        else RPC_RequestCivilianEscape();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestCivilianEscape(RpcInfo info = default)
    {
        ServerBeginCivilianEscape(info.Source);
    }

    public bool AuthorityTryLockEscapeRoute(EscapeEndingRoute requestedRoute)
    {
        if (!HasStateAuthority || !EscapeEndingRules.CanLock(LockedEscapeRoute, requestedRoute))
            return false;
        LockedEscapeRouteValue = (int)requestedRoute;
        return true;
    }

    private void ServerBeginCivilianEscape(PlayerRef requester)
    {
        if (!HasStateAuthority || !IsArrivalCarRepaired || IsCivilianEscapeComplete ||
            CurrentCivilianRouteStage != CivilianRouteStage.AwaitingTeam ||
            !EscapeEndingRules.CanLock(LockedEscapeRoute, EscapeEndingRoute.CivilianCar))
            return;

        if (!TryGetRequestingPlayer(requester, out PlayerMovement player)) return;
        PlayerInteraction interaction = player.GetComponent<PlayerInteraction>();
        if (interaction == null || !interaction.IsInVehicle || !interaction.IsVehicleDriver ||
            interaction.CurrentVehicle == null || RepairedArrivalCarObject == null ||
            interaction.CurrentVehicle != RepairedArrivalCarObject)
            return;

        if (Vector2.Distance(RepairedArrivalCarObject.transform.position, CivilianEscapePosition) >
            civilianEscapeTriggerRadius)
            return;

        if (!AreAllLivingPlayersGatheredForCivilianEscape())
        {
            RPC_ShowLocalizedQuestMessage("quest.route_a_wait_team", 0, 0);
            return;
        }

        if (!AuthorityTryLockEscapeRoute(EscapeEndingRoute.CivilianCar)) return;
        CivilianRouteStageValue = (int)CivilianRouteStage.EscapeRun;
        RPC_ShowLocalizedQuestMessage("quest.ending_a_locked", 0, 0);
        // The authored city exit sits beside the gray edge of Main. Begin the
        // visual road loop here at the safe regroup point instead of asking the
        // real network vehicle to drive into the unrendered edge first.
        ServerCompleteCivilianEscape();
    }

    private void ServerCompleteCivilianEscape()
    {
        if (!HasStateAuthority || CurrentCivilianRouteStage != CivilianRouteStage.EscapeRun ||
            IsCivilianEscapeComplete || LockedEscapeRoute != EscapeEndingRoute.CivilianCar)
            return;
        CivilianRouteStageValue = (int)CivilianRouteStage.Completed;
        IsCivilianEscapeComplete = true;
        RPC_TriggerCivilianVictory(Time.timeSinceLevelLoad);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_TriggerCivilianVictory(float survivalSeconds)
    {
        EscapeRouteDecisionUI.CloseIfOpen();
        CivilianRoutePresentationController.Attach(this)?.PlayOutro(
            RepairedArrivalCarObject, CivilianOutroEndPosition, survivalSeconds);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestRepairArrivalCarPart(int actionValue, RpcInfo info = default)
    {
        if (!System.Enum.IsDefined(typeof(ArrivalCarRepairAction), actionValue)) return;
        ServerRepairArrivalCarPart(info.Source, (ArrivalCarRepairAction)actionValue);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestCancelArrivalCarRepair(RpcInfo info = default)
    {
        ServerCancelArrivalCarRepair(info.Source);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestStartArrivalCar(RpcInfo info = default)
    {
        ServerStartArrivalCar(info.Source);
    }

    private void ServerRepairArrivalCarPart(PlayerRef requester, ArrivalCarRepairAction action)
    {
        if (!HasStateAuthority || !IsArrivalCarInspected) return;

        if (ArrivalCarRepairRules.IsApplied(ArrivalCarRepairMask, action))
        {
            RPC_ShowArrivalCarRepairStartResult(requester, false, (int)action, 0f,
                "Hạng mục này đã được sửa hoàn tất.");
            return;
        }

        if (ArrivalCarRepairSessionActive)
        {
            if (ActiveArrivalCarRepairer == requester &&
                ActiveArrivalCarRepairActionValue == (int)action)
            {
                RPC_ShowArrivalCarRepairStartResult(requester, true, (int)action,
                    ArrivalCarRepairDurationSeconds, string.Empty);
                return;
            }

            string message = ActiveArrivalCarRepairer == requester
                ? "Bạn đang sửa một hạng mục khác."
                : "Một người chơi khác đang sửa chiếc xe này.";
            RPC_ShowArrivalCarRepairStartResult(requester, false, (int)action, 0f, message);
            return;
        }

        BrokenArrivalCar car = BrokenArrivalCar.Instance;
        if (car == null || !TryGetRequestingPlayer(requester, out PlayerMovement player) ||
            !car.CanInspect(player.transform.position))
        {
            RPC_ShowArrivalCarRepairStartResult(requester, false, (int)action, 0f,
                "Hãy đứng trong vùng kiểm tra trước capo để sửa xe.");
            return;
        }

        PlayerHealth health = player.GetComponent<PlayerHealth>();
        if (health != null && (health.isDead || health.isTransforming))
        {
            RPC_ShowArrivalCarRepairStartResult(requester, false, (int)action, 0f,
                "Không thể sửa xe trong trạng thái hiện tại.");
            return;
        }

        InventorySystem inventory = player.GetComponent<InventorySystem>();
        ArrivalCarItemKind[] requirements = ArrivalCarItemCatalog.GetRequiredItems(action);
        if (inventory == null || !HasEveryArrivalCarItem(inventory, requirements))
        {
            RPC_ShowArrivalCarRepairStartResult(requester, false, (int)action, 0f,
                "Thiếu vật phẩm phù hợp. Mở nhật ký [J] để xem checklist.");
            return;
        }

        float duration = ArrivalCarRepairRules.GetRepairDurationSeconds(action);
        ArrivalCarRepairSessionActive = true;
        ActiveArrivalCarRepairActionValue = (int)action;
        ActiveArrivalCarRepairer = requester;
        ArrivalCarRepairDurationSeconds = duration;
        ArrivalCarRepairTimer = TickTimer.CreateFromSeconds(Runner, duration);
        RPC_PlayArrivalCarRepairAudio((int)action, duration);
        RPC_ShowArrivalCarRepairStartResult(requester, true, (int)action, duration, string.Empty);
    }

    private void ServerCancelArrivalCarRepair(PlayerRef requester)
    {
        if (!HasStateAuthority || !ArrivalCarRepairSessionActive ||
            ActiveArrivalCarRepairer != requester) return;
        RPC_StopArrivalCarRepairAudio();
        ClearArrivalCarRepairSession();
        RPC_ShowArrivalCarRepairInterrupted(requester, "Đã dừng sửa xe.");
    }

    private void TickArrivalCarRepair()
    {
        if (!ArrivalCarRepairSessionActive || ActiveArrivalCarRepairer == PlayerRef.None) return;

        PlayerRef repairer = ActiveArrivalCarRepairer;
        if (!TryGetRequestingPlayer(repairer, out PlayerMovement player))
        {
            AuthorityInterruptArrivalCarRepair(repairer, "Người sửa xe đã rời trận.");
            return;
        }

        player.LockMovement(Mathf.Max(0.2f, Runner.DeltaTime * 2f));
        PlayerHealth health = player.GetComponent<PlayerHealth>();
        BrokenArrivalCar car = BrokenArrivalCar.Instance;
        if ((health != null && (health.isDead || health.isTransforming)) || car == null ||
            !car.CanInspect(player.transform.position))
        {
            AuthorityInterruptArrivalCarRepair(repairer, "Việc sửa xe đã bị gián đoạn.");
            return;
        }

        if (!ArrivalCarRepairTimer.Expired(Runner)) return;
        if (!System.Enum.IsDefined(typeof(ArrivalCarRepairAction), ActiveArrivalCarRepairActionValue))
        {
            AuthorityInterruptArrivalCarRepair(repairer, "Dữ liệu hạng mục sửa xe không hợp lệ.");
            return;
        }

        ArrivalCarRepairAction action = (ArrivalCarRepairAction)ActiveArrivalCarRepairActionValue;
        InventorySystem inventory = player.GetComponent<InventorySystem>();
        ArrivalCarItemKind[] requirements = ArrivalCarItemCatalog.GetRequiredItems(action);
        if (inventory == null || !HasEveryArrivalCarItem(inventory, requirements))
        {
            AuthorityInterruptArrivalCarRepair(repairer,
                "Vật phẩm sửa chữa không còn trong túi đồ.");
            return;
        }

        if (ArrivalCarRepairRules.ConsumesInstalledPart(action))
        {
            ItemData consumable = FindArrivalCarItem(inventory, requirements[0]);
            if (consumable == null || inventory.ConsumeItem(consumable, 1) != 1)
            {
                AuthorityInterruptArrivalCarRepair(repairer,
                    "Vật phẩm vừa thay đổi trong túi đồ. Hãy kiểm tra lại [J].");
                return;
            }
        }

        ArrivalCarRepairMask |= (int)ArrivalCarRepairRules.GetStateBit(action);
        RPC_StopArrivalCarRepairAudio();
        ClearArrivalCarRepairSession();
        RPC_ShowArrivalCarRepairResult(repairer, true, (int)action,
            GetArrivalCarActionSuccessMessage(action));
        if (ArrivalCarRepairRules.IsRequiredRepairComplete(ArrivalCarRepairMask))
            RPC_ShowLocalizedQuestMessage("quest.route_a_ready", 0, 0);
    }

    private void AuthorityInterruptArrivalCarRepair(PlayerRef repairer, string message)
    {
        if (!HasStateAuthority || repairer == PlayerRef.None) return;
        RPC_StopArrivalCarRepairAudio();
        ClearArrivalCarRepairSession();
        RPC_ShowArrivalCarRepairInterrupted(repairer, message);
    }

    private void ClearArrivalCarRepairSession()
    {
        ArrivalCarRepairSessionActive = false;
        ActiveArrivalCarRepairActionValue = -1;
        ActiveArrivalCarRepairer = PlayerRef.None;
        ArrivalCarRepairTimer = TickTimer.None;
        ArrivalCarRepairDurationSeconds = 0f;
    }

    private void ServerStartArrivalCar(PlayerRef requester)
    {
        if (!HasStateAuthority || !IsArrivalCarInspected || IsArrivalCarRepaired) return;

        BrokenArrivalCar car = BrokenArrivalCar.Instance;
        if (car == null || !TryGetRequestingPlayer(requester, out PlayerMovement player) ||
            !car.CanInspect(player.transform.position))
        {
            RPC_ShowArrivalCarStartResult(requester, false,
                "Phải đứng trong vùng kiểm tra trước mũi xe để khởi động.");
            return;
        }

        if (!ArrivalCarRepairRules.IsRequiredRepairComplete(ArrivalCarRepairMask))
        {
            RPC_ShowArrivalCarStartResult(requester, false,
                "Động cơ, nhiên liệu, ắc quy và lốp trước trái chưa được xử lý đầy đủ.");
            return;
        }

        if (!SpawnRepairedArrivalCar(car, player.Object))
        {
            RPC_ShowArrivalCarStartResult(requester, false,
                "Không thể kích hoạt phương tiện. Hãy thử lại hoặc kiểm tra cấu hình prefab xe.");
            return;
        }

        IsArrivalCarRepaired = true;
        ConfigureCivilianEscapeRoute(RepairedArrivalCarObject.transform.position);
        CivilianRouteStageValue = (int)CivilianRouteStage.CarReady;
        RPC_ShowArrivalCarStartResult(requester, true,
            "Động cơ đã nổ máy. Xe dân sự đã sẵn sàng để khám phá và thoát hiểm.");
        RPC_ShowLocalizedQuestMessage("quest.route_a_started", 0, 0);
        RPC_ShowCivilianMapUnlocked(CivilianCheckpointPosition, CivilianCityExitPosition);
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
    public void AuthorityRegisterRouteClue(QuestRouteClueKind kind, PlayerRef focusPlayer,
        bool consumeCollectedDocuments = true)
    {
        if (!IsNetworkReady || !HasStateAuthority || CurrentStage == QuestStage.CityMapFound) return;
        int bit = 1 << (int)kind;
        if ((RouteClueMask & bit) != 0) return;
        RouteClueMask |= bit;
        RPC_ShowLocalizedQuestMessage("quest.route_clue_count", RouteClueCount,
            PreMilitaryQuestProgress.RequiredRouteClues);
        if (RouteClueCount == 1)
            RPC_ShowRouteBAudioCue((int)RouteBAudioCueId.FirstSupplyDocument, focusPlayer);
        else if (RouteClueCount == 2)
            RPC_ShowRouteBAudioCue((int)RouteBAudioCueId.SecondEvacuationDocument, focusPlayer);
        if (RouteClueCount >= PreMilitaryQuestProgress.RequiredRouteClues)
        {
            if (consumeCollectedDocuments)
                ConsumeCollectedRouteCluesAuthoritatively();
            NetworkQuestStage = (int)QuestStage.LocateOffice;
            RPC_ShowAllRouteCluesFound(focusPlayer);
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

    /// <summary>
    /// H1 vertical slice: clients may only request the interaction. State
    /// Authority re-resolves the scene point and validates stage, role,
    /// distance and line-of-sight before changing the replicated door state.
    /// </summary>
    public void RequestHospitalRadioInteraction(int interactionId)
    {
        if (!IsNetworkReady) return;
        if (HasStateAuthority) ServerUseHospitalRadioInteraction(interactionId, Runner.LocalPlayer);
        else RPC_RequestHospitalRadioInteraction(interactionId);
    }

    public void RequestSetHospitalRadioOperating(int interactionId, bool operating)
    {
        if (!IsNetworkReady) return;
        if (HasStateAuthority) ServerSetHospitalRadioOperating(interactionId, operating, Runner.LocalPlayer);
        else RPC_RequestSetHospitalRadioOperating(interactionId, operating);
    }

    public void RequestHospitalQuestClue(int interactionId)
    {
        if (!IsNetworkReady) return;
        if (HasStateAuthority) ServerUseHospitalQuestClue(interactionId, Runner.LocalPlayer);
        else RPC_RequestHospitalQuestClue(interactionId);
    }

    public void RequestHospitalRadioKeyLoot(int interactionId)
    {
        if (!IsNetworkReady) return;
        if (HasStateAuthority) ServerCollectHospitalRadioKey(interactionId, Runner.LocalPlayer);
        else RPC_RequestHospitalRadioKeyLoot(interactionId);
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

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestHospitalRadioInteraction(int interactionId, RpcInfo info = default)
    {
        ServerUseHospitalRadioInteraction(interactionId, info.Source);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestSetHospitalRadioOperating(int interactionId, bool operating, RpcInfo info = default)
    {
        ServerSetHospitalRadioOperating(interactionId, operating, info.Source);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestHospitalQuestClue(int interactionId, RpcInfo info = default)
    {
        ServerUseHospitalQuestClue(interactionId, info.Source);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestHospitalRadioKeyLoot(int interactionId, RpcInfo info = default)
    {
        ServerCollectHospitalRadioKey(interactionId, info.Source);
    }

    private void ServerUseHospitalQuestClue(int interactionId, PlayerRef requester)
    {
        if (!HasStateAuthority ||
            !HospitalQuestClueInteractionPoint.TryGet(interactionId, out HospitalQuestClueInteractionPoint point) ||
            !TryGetRequestingPlayer(requester, out PlayerMovement player) ||
            !point.CanPlayerInteract(player.transform.position) ||
            !HospitalInvestigationRules.IsClueAvailable(IsNetworkReady, CurrentStage,
                CurrentHospitalInvestigationStage, point.Role))
            return;

        AuthorityCompleteHospitalClue(point.Role, requester);
    }

    private void ServerCollectHospitalRadioKey(int interactionId, PlayerRef requester)
    {
        if (!HasStateAuthority || CurrentStage != QuestStage.FindCityMap ||
            CurrentHospitalInvestigationStage != HospitalInvestigationStage.FindRadioKey ||
            HasHospitalRadioKey || interactionId == 0 || interactionId != SelectedHospitalRadioKeyLootId ||
            !HospitalRadioKeyLootPoint.TryGet(interactionId, out HospitalRadioKeyLootPoint point) ||
            !TryGetRequestingPlayer(requester, out PlayerMovement player) || !IsLivingPlayer(player) ||
            !point.CanPlayerInteract(player.transform.position)) return;

        AuthorityGrantHospitalRadioKey(requester);
    }

    private void ServerUseHospitalRadioInteraction(int interactionId, PlayerRef requester)
    {
        if (!HasStateAuthority ||
            !HospitalRadioInteractionPoint.TryGet(interactionId, out HospitalRadioInteractionPoint point) ||
            !TryGetRequestingPlayer(requester, out PlayerMovement player) ||
            !point.CanPlayerInteract(player.transform.position))
            return;

        if (point.Role == HospitalRadioInteractionRole.Door)
        {
            if (!HospitalInvestigationRules.CanOpenDoor(IsNetworkReady, CurrentStage,
                    CurrentHospitalInvestigationStage, IsHospitalRadioDoorOpen, HasHospitalRadioKey))
            {
                if (HospitalInvestigationRules.IsDoorDiscoverable(IsNetworkReady, CurrentStage,
                        CurrentHospitalInvestigationStage, IsHospitalRadioDoorOpen))
                    RPC_ShowHospitalDoorLocked(requester, (int)CurrentHospitalInvestigationStage);
                return;
            }

            AuthorityOpenHospitalRadioRoom(requester);
            return;
        }

    }

    private void ServerSetHospitalRadioOperating(int interactionId, bool operating, PlayerRef requester)
    {
        if (!HasStateAuthority) return;
        if (!operating)
        {
            if (HospitalRadioOperator == requester) HospitalRadioOperator = PlayerRef.None;
            return;
        }

        if (HospitalRadioOperator != PlayerRef.None && HospitalRadioOperator != requester) return;
        if (!HospitalRadioRoomRules.CanOperateRadio(IsNetworkReady, CurrentStage,
                CurrentHospitalInvestigationStage, IsHospitalRadioDoorOpen, IsHospitalRadioRecovered) ||
            !HospitalRadioInteractionPoint.TryGet(interactionId, out HospitalRadioInteractionPoint point) ||
            point.Role != HospitalRadioInteractionRole.Radio ||
            !TryGetRequestingPlayer(requester, out PlayerMovement player) ||
            !IsLivingPlayer(player) || !point.CanPlayerInteract(player.transform.position))
            return;

        HospitalRadioOperator = requester;
    }

    private void TickHospitalRadioRestore()
    {
        if (HospitalRadioOperator == PlayerRef.None) return;
        if (!HospitalRadioRoomRules.CanOperateRadio(IsNetworkReady, CurrentStage,
                CurrentHospitalInvestigationStage, IsHospitalRadioDoorOpen, IsHospitalRadioRecovered) ||
            !HospitalRadioInteractionPoint.TryGetForRole(HospitalRadioInteractionRole.Radio,
                out HospitalRadioInteractionPoint point) ||
            !TryGetRequestingPlayer(HospitalRadioOperator, out PlayerMovement player) ||
            !IsLivingPlayer(player) || !point.CanPlayerInteract(player.transform.position))
        {
            HospitalRadioOperator = PlayerRef.None;
            return;
        }

        float segmentEnd = HospitalRadioRoomRules.GetSegmentEndSeconds(
            HospitalRadioCheckpointCount, hospitalRadioRestoreDuration);
        HospitalRadioRestoreSeconds = Mathf.Min(segmentEnd, HospitalRadioRoomRules.AdvanceRestore(
            HospitalRadioRestoreSeconds, Runner.DeltaTime, hospitalRadioRestoreDuration));
        if (HospitalRadioRestoreSeconds < segmentEnd) return;

        PlayerRef completedBy = HospitalRadioOperator;
        if (HospitalRadioCheckpointCount < HospitalRadioRoomRules.RestoreSegmentCount - 1)
        {
            HospitalRadioCheckpointCount++;
            HospitalRadioOperator = PlayerRef.None;
            AuthorityTriggerHospitalRadioThreat(completedBy, point.transform.position,
                HospitalRadioCheckpointCount);
            return;
        }

        HospitalRadioCheckpointCount = HospitalRadioRoomRules.RestoreSegmentCount;
        AuthorityCompleteHospitalRadio(completedBy, point.transform.position);
    }

    /// <summary>
    /// Hospital level 4 is a personal arrival reward. The first player is
    /// granted immediately by the validated entry request, while teammates
    /// and late joiners receive it only when their authoritative avatar enters
    /// the same hospital trigger. This keeps the reward shared in progression
    /// but personal in inventory ownership.
    /// </summary>
    private void TickHospitalBackpackRewards()
    {
        if (!HasStateAuthority || CurrentStage < QuestStage.FindCityMap ||
            activeHospitalEntryTrigger == null || Runner == null ||
            Time.unscaledTime < nextHospitalBackpackRewardScanTime)
            return;

        nextHospitalBackpackRewardScanTime = Time.unscaledTime + 0.25f;
        foreach (PlayerRef playerRef in Runner.ActivePlayers)
        {
            if (!Runner.TryGetPlayerObject(playerRef, out NetworkObject playerObject) ||
                playerObject == null || !playerObject.IsValid ||
                !playerObject.TryGetComponent(out PlayerMovement player) ||
                !IsLivingPlayer(player) ||
                !activeHospitalEntryTrigger.Contains(player.transform.position))
                continue;

            InventorySystem inventory = player.GetComponent<InventorySystem>();
            inventory?.TryGrantQuestBackpackReward(
                BackpackQuestRewardRules.HospitalBackpackLevel);
        }
    }

    private void AuthorityTriggerHospitalRadioThreat(PlayerRef completedBy, Vector3 radioPosition,
        int completedSegment)
    {
        if (!HasStateAuthority || completedSegment < 1 || completedSegment >= HospitalRadioRoomRules.RestoreSegmentCount)
            return;

        if (TryGetRequestingPlayer(completedBy, out PlayerMovement operatorPlayer))
            operatorPlayer.MakeNoise(hospitalRadioNoiseRadius, true, 100, hospitalRadioNoiseRadius, 1f);
        RPC_ShowHospitalRadioThreat(radioPosition, completedSegment);
        if (authorityHospitalRadioSpawnRoutine != null) StopCoroutine(authorityHospitalRadioSpawnRoutine);
        authorityHospitalRadioSpawnRoutine = StartCoroutine(
            AuthoritySpawnHospitalRadioThreat(completedBy, radioPosition, completedSegment));
        int difficulty = DifficultyRules.ActiveDifficulty;
        int zombiesPerEntry = HospitalRadioRoomRules.GetThreatZombiesPerEntry(difficulty);
        Debug.Log($"[HOSPITAL H4] Radio checkpoint {completedSegment}/3: noise + " +
                  $"{zombiesPerEntry} zombies at each entry (difficulty {difficulty}).");
    }

    private IEnumerator AuthoritySpawnHospitalRadioThreat(PlayerRef completedBy, Vector3 radioPosition,
        int completedSegment)
    {
        if (!ResolveHospitalRadioThreatSetup(radioPosition))
        {
            authorityHospitalRadioSpawnRoutine = null;
            yield break;
        }

        int difficulty = DifficultyRules.ActiveDifficulty;
        int zombiesPerEntry = HospitalRadioRoomRules.GetThreatZombiesPerEntry(difficulty);
        for (int spawnIndex = 0; spawnIndex < zombiesPerEntry; spawnIndex++)
        {
            float horizontalOffset = HospitalRadioRoomRules.GetThreatSpawnHorizontalOffset(
                spawnIndex, zombiesPerEntry, hospitalRadioZombieHorizontalSpacing);
            SpawnHospitalRadioZombie(cachedHospitalZombieEntryA.position +
                                     Vector3.right * horizontalOffset);
            SpawnHospitalRadioZombie(cachedHospitalZombieEntryB.position +
                                     Vector3.right * horizontalOffset);

            if (TryGetRequestingPlayer(completedBy, out PlayerMovement operatorPlayer))
                operatorPlayer.MakeNoise(hospitalRadioNoiseRadius, true, 100, hospitalRadioNoiseRadius, 1f);
            if (spawnIndex < zombiesPerEntry - 1)
                yield return new WaitForSeconds(hospitalRadioZombieSpawnDelay);
        }

        Debug.Log($"[HOSPITAL H4] Spawned {zombiesPerEntry * 2} milestone zombies " +
                  $"({zombiesPerEntry} per entry, difficulty {difficulty}) for checkpoint {completedSegment}/3.");
        authorityHospitalRadioSpawnRoutine = null;
    }

    private bool ResolveHospitalRadioThreatSetup(Vector3 radioPosition)
    {
        if (cachedHospitalZombieEntryA == null)
            cachedHospitalZombieEntryA = GameObject.Find("HospitalQuest_ZombieEntry_A")?.transform;
        if (cachedHospitalZombieEntryB == null)
            cachedHospitalZombieEntryB = GameObject.Find("HospitalQuest_ZombieEntry_B")?.transform;
        if (cachedHospitalZombieEntryA == null || cachedHospitalZombieEntryB == null)
        {
            Debug.LogError("[HOSPITAL H4] Missing HospitalQuest_ZombieEntry_A/B; milestone wave cannot spawn.");
            return false;
        }

        if (cachedHospitalRadioZombiePrefabs.Count > 0) return true;
        ZombieSpawnZone[] zones = FindObjectsByType<ZombieSpawnZone>(FindObjectsSortMode.None);
        ZombieSpawnZone closestZone = null;
        float closestDistance = float.PositiveInfinity;
        for (int i = 0; i < zones.Length; i++)
        {
            ZombieSpawnZone zone = zones[i];
            if (zone == null || zone.zombiePrefabs == null || zone.zombiePrefabs.Count == 0) continue;
            float distance = Vector2.Distance(zone.transform.position, radioPosition);
            if (distance >= closestDistance) continue;
            closestDistance = distance;
            closestZone = zone;
        }

        if (closestZone != null)
            for (int i = 0; i < closestZone.zombiePrefabs.Count; i++)
                if (closestZone.zombiePrefabs[i].IsValid)
                    cachedHospitalRadioZombiePrefabs.Add(closestZone.zombiePrefabs[i]);
        if (cachedHospitalRadioZombiePrefabs.Count > 0) return true;

        Debug.LogError("[HOSPITAL H4] No valid zombie NetworkPrefabRef found in scene spawn zones.");
        return false;
    }

    private void SpawnHospitalRadioZombie(Vector3 position)
    {
        if (Runner == null || cachedHospitalRadioZombiePrefabs.Count == 0) return;
        NetworkPrefabRef prefab = cachedHospitalRadioZombiePrefabs[
            Random.Range(0, cachedHospitalRadioZombiePrefabs.Count)];
        try
        {
            NetworkObject spawned = Runner.Spawn(prefab, position, Quaternion.identity);
            if (spawned != null) HospitalRadioThreatSpawnCount++;
        }
        catch (System.Exception exception)
        {
            Debug.LogError("[HOSPITAL H4] Zombie spawn failed: " + exception.Message);
        }
    }

    private static bool IsLivingPlayer(PlayerMovement player)
    {
        if (player == null) return false;
        PlayerHealth health = player.GetComponent<PlayerHealth>();
        return health == null || (!health.isDead && !health.isTransforming);
    }

    private void ServerStartMapSearch(int triggerId, PlayerRef requester)
    {
        if (!HasStateAuthority || CurrentStage != QuestStage.LocateOffice) return;
        if (!MainQuestStartTrigger.TryGet(triggerId, out MainQuestStartTrigger trigger) || trigger == null) return;
        if (!TryGetRequestingPlayer(requester, out PlayerMovement player) || !trigger.Contains(player.transform.position)) return;

        activeHospitalEntryTrigger = trigger;
        AuthorityStartOfficeInvestigation(requester);
    }

    private void AuthorityStartOfficeInvestigation(PlayerRef requester)
    {
        if (!HasStateAuthority || CurrentStage != QuestStage.LocateOffice) return;

        if (!HospitalQuestClueInteractionPoint.TryGetForRole(HospitalQuestClueRole.ShiftLog, out _) ||
            !HospitalQuestClueInteractionPoint.TryGetForRole(HospitalQuestClueRole.ShiftLog2, out _) ||
            !HospitalRadioInteractionPoint.TryGetForRole(HospitalRadioInteractionRole.Door, out _) ||
            !HospitalRadioInteractionPoint.TryGetForRole(HospitalRadioInteractionRole.Radio, out _))
        {
            Debug.LogError("[HOSPITAL H2] Thiếu ShiftLog, ShiftLog2, Door hoặc Radio interaction anchor.");
            RPC_ShowLocalizedQuestMessage("quest.office_missing_points", 0, 0);
            return;
        }

        // H2 replaces the temporary dispatch desk → radio → records cabinet path.
        // The legacy fields remain serialized for compatibility but are no longer
        // authoritative objectives in the hospital.
        CheckedCabinetMask = 0;
        MapCabinetId = 0;
        IsOfficeDiscovered = true;
        HasHospitalRadioKey = false;
        SelectedHospitalRadioKeyLootId = 0;
        IsHospitalRadioDoorOpen = false;
        HospitalRadioRestoreSeconds = 0f;
        HospitalRadioOperator = PlayerRef.None;
        IsHospitalRadioRecovered = false;
        HospitalRadioCheckpointCount = 0;
        HospitalRadioThreatSpawnCount = 0;
        cachedHospitalRadioZombiePrefabs.Clear();
        NetworkHospitalInvestigationStage = (int)HospitalInvestigationStage.FindShiftLog;
        NetworkQuestStage = (int)QuestStage.FindCityMap;
        if (TryGetRequestingPlayer(requester, out PlayerMovement arrivingPlayer))
        {
            arrivingPlayer.GetComponent<InventorySystem>()?.TryGrantQuestBackpackReward(
                BackpackQuestRewardRules.HospitalBackpackLevel);
        }
        RPC_ShowLocalizedQuestMessage("quest.office_new_objective", 0, 0);
        RPC_ShowOfficeSearchStarted(requester);
    }

    private void AuthorityCompleteHospitalClue(HospitalQuestClueRole role, PlayerRef requester)
    {
        if (!HasStateAuthority || CurrentStage != QuestStage.FindCityMap) return;

        if (role == HospitalQuestClueRole.ShiftLog &&
            CurrentHospitalInvestigationStage == HospitalInvestigationStage.FindShiftLog)
        {
            NetworkHospitalInvestigationStage = (int)HospitalInvestigationStage.FindShiftLog2;
            RPC_ShowHospitalClueResult(requester, (int)role);
            Debug.Log("[HOSPITAL H2] ShiftLog hoàn tất; waypoint chuyển tới văn phòng trưởng ca.");
            return;
        }

        if (role == HospitalQuestClueRole.ShiftLog2 &&
            CurrentHospitalInvestigationStage == HospitalInvestigationStage.FindShiftLog2)
        {
            if (!AuthoritySelectHospitalRadioKeyLoot(requester)) return;
            NetworkHospitalInvestigationStage = (int)HospitalInvestigationStage.FindRadioKey;
            RPC_ShowHospitalClueResult(requester, (int)role);
            Debug.Log($"[HOSPITAL H5] ShiftLog2 hoàn tất; Host chọn KeyLoot ID " +
                      $"{SelectedHospitalRadioKeyLootId} cho toàn đội.");
        }
    }

    private bool AuthoritySelectHospitalRadioKeyLoot(PlayerRef requester)
    {
        if (!HasStateAuthority) return false;
        HospitalRadioKeyLootPoint.GetAll(cachedHospitalRadioKeyLootPoints);
        if (cachedHospitalRadioKeyLootPoints.Count == 0)
        {
            Debug.LogError("[HOSPITAL H5] Main.unity không có HospitalRadioKeyLootPoint hợp lệ.");
            RPC_ShowLocalizedQuestMessage("quest.office_missing_points", 0, 0);
            return false;
        }

        int selectedIndex = Random.Range(0, cachedHospitalRadioKeyLootPoints.Count);
        SelectedHospitalRadioKeyLootId = cachedHospitalRadioKeyLootPoints[selectedIndex].InteractionId;
        return SelectedHospitalRadioKeyLootId != 0;
    }

    private void AuthorityGrantHospitalRadioKey(PlayerRef requester)
    {
        if (!HasStateAuthority || CurrentStage != QuestStage.FindCityMap ||
            CurrentHospitalInvestigationStage != HospitalInvestigationStage.FindRadioKey ||
            HasHospitalRadioKey || SelectedHospitalRadioKeyLootId == 0) return;
        HasHospitalRadioKey = true;
        NetworkHospitalInvestigationStage = (int)HospitalInvestigationStage.UnlockRadioRoom;
        RPC_ShowHospitalRadioKeyCollected(requester);
        Debug.Log($"[HOSPITAL H5] Team nhặt shared key tại ID {SelectedHospitalRadioKeyLootId}.");
    }

    private void AuthorityDebugCollectHospitalRadioKey(PlayerRef requester)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        AuthorityGrantHospitalRadioKey(requester);
#endif
    }

    private void AuthorityOpenHospitalRadioRoom(PlayerRef requester)
    {
        if (!HasStateAuthority ||
            !HospitalInvestigationRules.CanOpenDoor(IsNetworkReady, CurrentStage,
                CurrentHospitalInvestigationStage, IsHospitalRadioDoorOpen, HasHospitalRadioKey))
            return;

        IsHospitalRadioDoorOpen = true;
        NetworkHospitalInvestigationStage = (int)HospitalInvestigationStage.RadioReady;
        RPC_ShowHospitalRadioH1Result(requester, (int)HospitalRadioInteractionRole.Door);
        Debug.Log("[HOSPITAL H2] Cửa Radio đã mở; H2 đạt RadioReady và H3 có thể bắt đầu.");
    }

    private void AuthorityDebugAdvanceHospitalRadioStage(PlayerRef requester)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!HasStateAuthority || CurrentStage != QuestStage.FindCityMap ||
            CurrentHospitalInvestigationStage != HospitalInvestigationStage.RadioReady) return;
        Vector3 radioPosition = HospitalRadioInteractionPoint.TryGetForRole(
            HospitalRadioInteractionRole.Radio, out HospitalRadioInteractionPoint radio)
            ? radio.transform.position
            : Vector3.zero;
        HospitalRadioRestoreSeconds = HospitalRadioRoomRules.GetSegmentEndSeconds(
            HospitalRadioCheckpointCount, hospitalRadioRestoreDuration);
        HospitalRadioOperator = requester;
        if (HospitalRadioCheckpointCount < HospitalRadioRoomRules.RestoreSegmentCount - 1)
        {
            HospitalRadioCheckpointCount++;
            HospitalRadioOperator = PlayerRef.None;
            AuthorityTriggerHospitalRadioThreat(requester, radioPosition, HospitalRadioCheckpointCount);
            Debug.Log($"[QUEST TEST] F6 hoàn tất chặng Radio {HospitalRadioCheckpointCount}/3.");
            return;
        }

        HospitalRadioCheckpointCount = HospitalRadioRoomRules.RestoreSegmentCount;
        AuthorityCompleteHospitalRadio(requester, radioPosition);
        Debug.Log("[QUEST TEST] F6 hoàn tất chặng Radio 3/3 qua production completion path.");
#endif
    }

    private void AuthorityCompleteHospitalRadio(PlayerRef completedBy, Vector3 radioPosition)
    {
        if (!HasStateAuthority || IsHospitalRadioRecovered) return;

        HospitalRadioRestoreSeconds = hospitalRadioRestoreDuration;
        HospitalRadioOperator = PlayerRef.None;
        IsHospitalRadioRecovered = true;
        HospitalRadioCheckpointCount = HospitalRadioRoomRules.RestoreSegmentCount;
        IsCityMapUnlocked = true;
        MapCabinetId = 0;
        NetworkQuestStage = (int)QuestStage.CityMapFound;
        IsMilitaryRevealPlaying = false;

        HashSet<PlayerRef> notified = new HashSet<PlayerRef>();
        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerMovement player = players[i];
            if (player == null || player.Object == null || !player.Object.IsValid || !IsLivingPlayer(player)) continue;
            PlayerRef target = player.Object.InputAuthority;
            if (target == PlayerRef.None || notified.Contains(target)) continue;
            notified.Add(target);
            RPC_PlayHospitalRadioRecording(target, target == completedBy);
        }

        Debug.Log("[HOSPITAL H3] Radio recovered; Fragment 2 and the North Base route are now shared team state.");
    }

    private void ServerSearchCabinet(int cabinetId, PlayerRef requester)
    {
        if (!HasStateAuthority || !IsMapSearchActive) return;
        if (!MainQuestSearchCabinet.TryGet(cabinetId, out MainQuestSearchCabinet cabinet) || cabinet == null) return;
        if (!TryGetRequestingPlayer(requester, out PlayerMovement player) ||
            !cabinet.CanPlayerSearch(player.transform.position)) return;
        int cabinetIndex = GetCabinetIndex(cabinetId);
        if (cabinetIndex >= 0 && (CheckedCabinetMask & (1 << cabinetIndex)) != 0) return;

        if (cabinetId != MapCabinetId) return;

        AuthorityCompleteOfficeStep(cabinetId, requester);
    }

    private void AuthorityCompleteOfficeStep(int cabinetId, PlayerRef requester)
    {
        if (!HasStateAuthority || !IsMapSearchActive || cabinetId == 0 || cabinetId != MapCabinetId) return;
        int cabinetIndex = GetCabinetIndex(cabinetId);
        if (cabinetIndex >= 0 && (CheckedCabinetMask & (1 << cabinetIndex)) != 0) return;

        List<MainQuestSearchCabinet> investigationOrder = BuildOfficeInvestigationOrder(
            FindObjectsByType<MainQuestSearchCabinet>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        int investigationStep = investigationOrder.FindIndex(point => point.CabinetId == cabinetId);
        if (investigationStep < 0 || investigationStep > 2) return;
        if (cabinetIndex >= 0) CheckedCabinetMask |= 1 << cabinetIndex;

        if (investigationStep < 2)
        {
            MapCabinetId = investigationOrder[investigationStep + 1].CabinetId;
            RPC_ShowOfficeInvestigationProgress(investigationStep, requester);
            return;
        }

        IsCityMapUnlocked = true;
        MapCabinetId = 0;
        NetworkQuestStage = (int)QuestStage.CityMapFound;
        IsMilitaryRevealPlaying = false;

        RPC_ShowOfficeInvestigationProgress(2, requester);
        RPC_ShowCabinetSearchResult(requester, true);
        RPC_ShowLocalizedQuestMessage("quest.route_b_go_military", 0, 0);
    }

    public bool IsCabinetChecked(int cabinetId)
    {
        if (!IsNetworkReady || cabinetId == 0)
            return false;

        int cabinetIndex = GetCabinetIndex(cabinetId);
        return cabinetIndex >= 0 && (CheckedCabinetMask & (1 << cabinetIndex)) != 0;
    }

    public bool IsCurrentOfficeObjective(int cabinetId)
    {
        return IsNetworkReady && IsMapSearchActive && cabinetId != 0 && cabinetId == MapCabinetId;
    }

    public string GetCurrentOfficeInteractionLabel()
    {
        string action = GetCurrentOfficeInteractionActionLabel();
        return QuestUILocalization.IsVietnamese
            ? $"GIỮ [E] ĐỂ {action}"
            : $"HOLD [E] TO {action}";
    }

    public string GetCurrentOfficeInteractionActionLabel()
    {
        int step = GetCurrentOfficeInvestigationStep();
        string key = step switch
        {
            0 => "quest.office_action_dispatch",
            1 => "quest.office_action_radio",
            2 => "quest.office_action_cabinet",
            _ => "quest.office_action_generic"
        };
        return GameLocalization.Get(key);
    }

    public string GetCurrentOfficeProgressLabel()
    {
        int step = GetCurrentOfficeInvestigationStep();
        string key = step switch
        {
            0 => "quest.office_progress_dispatch",
            1 => "quest.office_progress_radio",
            2 => "quest.office_progress_cabinet",
            _ => "quest.office_progress_generic"
        };
        return GameLocalization.Get(key);
    }

    private int GetCurrentOfficeInvestigationStep()
    {
        if (!IsNetworkReady || MapCabinetId == 0) return -1;
        List<MainQuestSearchCabinet> order = BuildOfficeInvestigationOrder(
            FindObjectsByType<MainQuestSearchCabinet>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        return order.FindIndex(point => point.CabinetId == MapCabinetId);
    }

    private static List<MainQuestSearchCabinet> BuildOfficeInvestigationOrder(
        IEnumerable<MainQuestSearchCabinet> source)
    {
        List<MainQuestSearchCabinet> candidates = new List<MainQuestSearchCabinet>();
        foreach (MainQuestSearchCabinet point in source)
            if (point != null && point.CabinetId != 0)
                candidates.Add(point);

        List<MainQuestSearchCabinet> result = new List<MainQuestSearchCabinet>(3);
        TakePoint(candidates, result, (left, right) => left.transform.position.x > right.transform.position.x);
        TakePoint(candidates, result, (left, right) => left.transform.position.y < right.transform.position.y);
        TakePoint(candidates, result, (left, right) => left.transform.position.y > right.transform.position.y);
        return result;
    }

    private static void TakePoint(List<MainQuestSearchCabinet> candidates,
        List<MainQuestSearchCabinet> result, System.Func<MainQuestSearchCabinet, MainQuestSearchCabinet, bool> prefer)
    {
        if (candidates.Count == 0) return;
        MainQuestSearchCabinet selected = candidates[0];
        for (int i = 1; i < candidates.Count; i++)
            if (prefer(candidates[i], selected)) selected = candidates[i];
        result.Add(selected);
        candidates.Remove(selected);
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

    private void DistributeArrivalCarRepairItems(IReadOnlyList<string> houseIds, int count)
    {
        List<LootContainer> containers = new List<LootContainer>();
        QuestLocationIdentity[] locations = FindObjectsByType<QuestLocationIdentity>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int idIndex = 0; idIndex < count; idIndex++)
        {
            string houseId = houseIds[idIndex];
            for (int locationIndex = 0; locationIndex < locations.Length; locationIndex++)
            {
                QuestLocationIdentity location = locations[locationIndex];
                if (location == null || location.LocationType != QuestLocationType.ResidentialHouse ||
                    !string.Equals(location.LocationId, houseId, System.StringComparison.Ordinal))
                    continue;
                containers.AddRange(location.GetComponentsInChildren<LootContainer>(true));
                break;
            }
        }

        containers.RemoveAll(container => container == null || container.Object == null ||
                                          !container.Object.IsValid || !container.HasStateAuthority);
        containers.Sort((left, right) => left.GetInstanceID().CompareTo(right.GetInstanceID()));
        if (containers.Count == 0)
        {
            Debug.LogError("[ARRIVAL CAR] Không tìm thấy container authoritative để đặt vật phẩm sửa xe.");
            return;
        }

        ArrivalCarItemKind[] items =
        {
            ArrivalCarItemKind.Toolbox,
            ArrivalCarItemKind.Hammer,
            ArrivalCarItemKind.FuelCan,
            ArrivalCarItemKind.Battery,
            ArrivalCarItemKind.Tire
        };
        ShuffleContainers(containers);
        HashSet<LootContainer> guaranteedContainers = new HashSet<LootContainer>();
        for (int itemIndex = 0; itemIndex < items.Length; itemIndex++)
        {
            ArrivalCarItemKind kind = items[itemIndex];
            LootContainer placedIn = TryPlaceArrivalCarItem(containers, guaranteedContainers, kind);
            if (placedIn == null)
            {
                Debug.LogError($"[ARRIVAL CAR] Không thể đặt vật phẩm bắt buộc '{kind}' vào bất kỳ container nào.");
                continue;
            }

            guaranteedContainers.Add(placedIn);
            Debug.Log($"[ARRIVAL CAR] Guaranteed '{kind}' in container '{placedIn.name}'.");
        }

        // No continuous server scan is needed. The authoritative setup above
        // prevents an initial soft-lock; a small number of random extras makes
        // sharing/trading useful without deleting or policing player loot.
        for (int itemIndex = 0; itemIndex < items.Length; itemIndex++)
        {
            if (Random.value > arrivalCarDuplicateItemChance) continue;
            ArrivalCarItemKind kind = items[itemIndex];
            ShuffleContainers(containers);
            for (int containerIndex = 0; containerIndex < containers.Count; containerIndex++)
            {
                LootContainer candidate = containers[containerIndex];
                if (candidate.ContainsArrivalCarItem(kind)) continue;
                if (!candidate.EnsureArrivalCarItem(kind)) continue;
                Debug.Log($"[ARRIVAL CAR] Bonus co-op copy '{kind}' in container '{candidate.name}'.");
                break;
            }
        }
    }

    private static LootContainer TryPlaceArrivalCarItem(IReadOnlyList<LootContainer> containers,
        ISet<LootContainer> alreadyUsed, ArrivalCarItemKind kind)
    {
        // Prefer a distinct cabinet for every guaranteed kind. If the selected
        // neighborhood contains fewer than five usable cabinets, fall back to
        // sharing a cabinet instead of failing the route.
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < containers.Count; i++)
            {
                LootContainer candidate = containers[i];
                if (pass == 0 && alreadyUsed.Contains(candidate)) continue;
                if (candidate.EnsureArrivalCarItem(kind)) return candidate;
            }
        }

        return null;
    }

    private static void ShuffleContainers(List<LootContainer> containers)
    {
        for (int i = containers.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            (containers[i], containers[swapIndex]) = (containers[swapIndex], containers[i]);
        }
    }

    private void ConsumeCollectedRouteCluesAuthoritatively()
    {
        InventorySystem[] inventories = FindObjectsByType<InventorySystem>(FindObjectsSortMode.None);
        int removed = 0;
        for (int clueIndex = 0; clueIndex < PreMilitaryQuestProgress.RequiredRouteClues; clueIndex++)
        {
            QuestRouteClueKind kind = (QuestRouteClueKind)clueIndex;
            for (int inventoryIndex = 0; inventoryIndex < inventories.Length; inventoryIndex++)
            {
                InventorySystem inventory = inventories[inventoryIndex];
                if (inventory == null || inventory.Object == null || !inventory.Object.IsValid ||
                    !inventory.HasStateAuthority)
                    continue;
                ItemData clue = FindRouteClueItem(inventory, kind);
                if (clue == null) continue;
                removed += inventory.ConsumeItem(clue, 1);
                break;
            }
        }

        if (removed != PreMilitaryQuestProgress.RequiredRouteClues)
            Debug.LogWarning($"[MAIN QUEST] State Authority assembled Mảnh 1 but removed {removed}/3 route documents. " +
                             "A collected clue may have been moved out of all player inventories.");
    }

    private static ItemData FindRouteClueItem(InventorySystem inventory, QuestRouteClueKind kind)
    {
        if (inventory == null) return null;
        for (int i = 0; i < inventory.slots.Count; i++)
        {
            InventorySlot slot = inventory.slots[i];
            if (slot != null && slot.amount > 0 &&
                QuestRouteClueItemCatalog.TryGetKind(slot.item, out QuestRouteClueKind existing) &&
                existing == kind)
                return slot.item;
        }
        return null;
    }

    private static bool HasEveryArrivalCarItem(InventorySystem inventory, ArrivalCarItemKind[] kinds)
    {
        for (int i = 0; i < kinds.Length; i++)
            if (FindArrivalCarItem(inventory, kinds[i]) == null) return false;
        return true;
    }

    private static ItemData FindArrivalCarItem(InventorySystem inventory, ArrivalCarItemKind kind)
    {
        if (inventory == null) return null;
        for (int i = 0; i < inventory.slots.Count; i++)
        {
            InventorySlot slot = inventory.slots[i];
            if (slot != null && slot.amount > 0 &&
                ArrivalCarItemCatalog.TryGetKind(slot.item, out ArrivalCarItemKind existing) && existing == kind)
                return slot.item;
        }
        return null;
    }

    private static string GetArrivalCarActionSuccessMessage(ArrivalCarRepairAction action) => action switch
    {
        ArrivalCarRepairAction.RepairCore => "Đã xử lý nắp capo, bộ đề và động cơ. Búa và bộ dụng cụ được giữ lại.",
        ArrivalCarRepairAction.AddFuel => "Đã đổ can nhiên liệu vào bình.",
        ArrivalCarRepairAction.ReplaceBattery => "Đã lắp ắc quy mới. Đây là hạng mục bắt buộc trước khi khởi động.",
        ArrivalCarRepairAction.ReplaceTire => "Đã thay lốp trước trái bị hỏng. Đây là hạng mục bắt buộc trước khi chạy.",
        _ => "Đã cập nhật tình trạng xe."
    };

    private void ConfigureCivilianEscapeRoute(Vector2 origin)
    {
        Vector2 checkpoint = origin + civilianEscapeFallbackOffset;
        Vector2 cityExit = checkpoint + civilianEscapeFallbackOffset.normalized * 20f;
        PreMilitaryQuestRuntimeBridge bridge = PreMilitaryQuestRuntimeBridge.Instance;
        if (bridge != null && bridge.TryGetCivilianEscapeRoute(origin, out Vector2 roadCheckpoint,
                out Vector2 roadExit))
        {
            checkpoint = roadCheckpoint;
            cityExit = roadExit;
        }

        Transform configuredCheckpoint = hasGeneratedCivilianEscapeFallback ? null : civilianEscapeExit;
        if (configuredCheckpoint == null && !hasGeneratedCivilianEscapeFallback)
        {
            GameObject configuredObject = GameObject.Find("CivilianRouteCheckpoint");
            if (configuredObject == null) configuredObject = GameObject.Find("CivilianEscapeExit");
            if (configuredObject != null) configuredCheckpoint = configuredObject.transform;
        }
        if (configuredCheckpoint != null) checkpoint = configuredCheckpoint.position;

        Transform configuredCityExit = civilianCityExit;
        if (configuredCityExit == null)
        {
            GameObject configuredObject = GameObject.Find("CivilianCityExit");
            if (configuredObject != null) configuredCityExit = configuredObject.transform;
        }
        if (configuredCityExit != null) cityExit = configuredCityExit.position;

        CivilianCheckpointPosition = checkpoint;
        CivilianCityExitPosition = cityExit;
    }

    public bool AreAllLivingPlayersGatheredForCivilianEscape()
    {
        GetCivilianEscapeGatherCounts(out int gatheredPlayers, out int livingPlayers);
        return livingPlayers > 0 && gatheredPlayers == livingPlayers;
    }

    public bool IsAtLeastHalfOfLivingPlayersGatheredForCivilianEscape()
    {
        GetCivilianEscapeGatherCounts(out int gatheredPlayers, out int livingPlayers);
        return livingPlayers > 0 && gatheredPlayers * 2 >= livingPlayers;
    }

    private void GetCivilianEscapeGatherCounts(out int gatheredPlayers, out int livingPlayers)
    {
        gatheredPlayers = 0;
        livingPlayers = 0;
        if (!IsNetworkReady || RepairedArrivalCarObject == null) return;

        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerMovement candidate = players[i];
            if (candidate == null || candidate.Object == null || !candidate.Object.IsValid) continue;
            PlayerHealth health = candidate.GetComponent<PlayerHealth>();
            if (health != null && health.isDead) continue;
            livingPlayers++;

            PlayerInteraction interaction = candidate.GetComponent<PlayerInteraction>();
            if (interaction != null && interaction.IsInVehicle &&
                interaction.CurrentVehicle == RepairedArrivalCarObject)
            {
                gatheredPlayers++;
                continue;
            }

            if (Vector2.Distance(candidate.transform.position, RepairedArrivalCarObject.transform.position) <=
                civilianTeamGatherRadius)
                gatheredPlayers++;
        }
    }

    public bool AuthorityForceCivilianEscape()
    {
        if (!HasStateAuthority || !IsArrivalCarRepaired || RepairedArrivalCarObject == null ||
            IsCivilianEscapeComplete || CurrentCivilianRouteStage != CivilianRouteStage.AwaitingTeam ||
            !IsAtLeastHalfOfLivingPlayersGatheredForCivilianEscape() ||
            !EscapeEndingRules.CanLock(LockedEscapeRoute, EscapeEndingRoute.CivilianCar))
            return false;

        if (!AuthorityTryLockEscapeRoute(EscapeEndingRoute.CivilianCar)) return false;
        AuthorityGatherLivingPlayersAtCivilianCar();
        CivilianRouteStageValue = (int)CivilianRouteStage.EscapeRun;
        RPC_ShowLocalizedQuestMessage("quest.ending_a_locked", 0, 0);
        ServerCompleteCivilianEscape();
        return IsCivilianEscapeComplete;
    }

    private void AuthorityGatherLivingPlayersAtCivilianCar()
    {
        Vector2 center = RepairedArrivalCarObject.transform.position;
        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        List<Vector2> occupiedPositions = new List<Vector2> { center };
        int gatherIndex = 1;

        for (int i = 0; i < players.Length; i++)
        {
            PlayerMovement candidate = players[i];
            if (candidate == null || candidate.Object == null || !candidate.Object.IsValid) continue;
            PlayerHealth health = candidate.GetComponent<PlayerHealth>();
            if (health != null && health.isDead) continue;

            PlayerInteraction interaction = candidate.GetComponent<PlayerInteraction>();
            if (interaction != null && interaction.IsInVehicle &&
                interaction.CurrentVehicle == RepairedArrivalCarObject)
                continue;

            Vector2 destination = FindSafeGatherPosition(center, gatherIndex++, occupiedPositions);
            if (interaction != null && interaction.IsInVehicle)
            {
                VehicleControllerFusion vehicle = interaction.CurrentVehicleController;
                bool exitedNormally = vehicle != null && vehicle.AuthorityTryExit(candidate.Object);
                if (!exitedNormally)
                    interaction.SetVehicleNetworkState(null, false, false, 0, destination);
            }

            TeleportPlayer(candidate, destination);
            occupiedPositions.Add(destination);
        }

        Physics2D.SyncTransforms();
    }

    private Transform ResolveCivilianEscapeExit()
    {
        if (civilianEscapeExit != null) return civilianEscapeExit;
        GameObject configured = GameObject.Find("CivilianEscapeExit");
        if (configured != null)
        {
            civilianEscapeExit = configured.transform;
            hasGeneratedCivilianEscapeFallback = false;
            return civilianEscapeExit;
        }

        GameObject anchor = new GameObject("CivilianEscapeExit");
        GameObject carSpawn = GameObject.Find("ViTriXeChetMay");
        Vector3 origin = carSpawn != null
            ? carSpawn.transform.position
            : BrokenArrivalCar.Instance != null ? BrokenArrivalCar.Instance.transform.position : Vector3.zero;
        anchor.transform.position = origin + (Vector3)civilianEscapeFallbackOffset;
        civilianEscapeExit = anchor.transform;
        hasGeneratedCivilianEscapeFallback = true;
        return civilianEscapeExit;
    }

    private bool SpawnRepairedArrivalCar(BrokenArrivalCar brokenCar, NetworkObject sourcePlayer)
    {
        if (!HasStateAuthority || brokenCar == null) return false;
        if (RepairedArrivalCarObject != null) return true;
        try
        {
            NetworkObject spawned = Runner.Spawn(repairedArrivalCarPrefab, brokenCar.transform.position,
                brokenCar.transform.rotation);
            if (spawned == null)
            {
                Debug.LogError("[ARRIVAL CAR] Fusion không thể spawn prefab xe chạy được.");
                return false;
            }
            spawned.name = "Repaired Arrival Car";
            RepairedArrivalCarObject = spawned;
            VehicleControllerFusion controller = spawned.GetComponent<VehicleControllerFusion>();
            if (controller == null || !controller.AuthorityPlayStarterConfirmation(sourcePlayer))
                Debug.LogWarning("[ARRIVAL CAR] Xe đã spawn nhưng không thể phát tiếng đề máy xác nhận.");
            return true;
        }
        catch (System.Exception exception)
        {
            Debug.LogError("[ARRIVAL CAR] Không thể kích hoạt xe sau sửa chữa: " + exception.Message);
            return false;
        }
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

        bool unlocked = IsNetworkReady && (IsCityMapUnlocked || IsCivilianCityMapUnlocked);
        if (cachedMapController != null) cachedMapController.SetMapUnlocked(unlocked);
        // Either route can unlock the full mission map. The corner minimap is a
        // separate exploration aid and remains disabled for the whole story.
        if (cachedMinimapController != null) cachedMinimapController.SetMapUnlocked(false);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowQuestMessage(string message)
    {
        AutoChatManager.Instance?.AddMessage("NHIỆM VỤ", message);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowHospitalRadioH1Result(PlayerRef focusPlayer, int roleValue)
    {
        if (Runner == null || Runner.LocalPlayer != focusPlayer) return;
        HospitalRadioInteractionRole role = (HospitalRadioInteractionRole)roleValue;
        string message = role == HospitalRadioInteractionRole.Door
            ? "Đã mở Trạm liên lạc phụ trợ. Radio hiện sẵn sàng để kiểm tra."
            : "Radio đã sẵn sàng. Nội dung phát sóng và phục hồi tín hiệu sẽ bắt đầu ở H3.";
        AutoChatManager.Instance?.AddMessage("NHIỆM VỤ BỆNH VIỆN", message);
        ShowLocalQuestEvent(role == HospitalRadioInteractionRole.Door
                ? "TRẠM RADIO ĐÃ MỞ"
                : "RADIO SẴN SÀNG",
            message);
        Debug.Log("[HOSPITAL H2] " + message);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayHospitalRadioRecording([RpcTarget] PlayerRef targetPlayer, bool ownsMilitaryReveal)
    {
        if (Runner == null || Runner.LocalPlayer != targetPlayer) return;
        QuestFlowUIPrototype.Instance?.NotifyAuthoritativeQuestStage((int)QuestStage.CityMapFound);
        AutoChatManager.Instance?.AddMessage("BẢN GHI RADIO ĐÃ KHÔI PHỤC",
            "Transcript đã lưu trong Nhật ký. Bộ nhớ máy chứa tọa độ Căn cứ phía Bắc; chưa rõ ở đó còn ai sống.");
        ShowLocalQuestEvent("MẢNH BẢN ĐỒ 2",
            "Đã trích xuất tần số đèn hiệu và tọa độ từ bộ nhớ Radio.");
        RouteBRadioBroadcastUI.ShowHospitalRecording(() => HandleMilitaryMapFragmentFound(ownsMilitaryReveal));
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowHospitalRadioThreat(Vector3 radioPosition, int completedSegment)
    {
        PlayerMovement localPlayer = PlayerMovement.LocalPlayerInstance;
        if (localPlayer == null || Vector2.Distance(localPlayer.transform.position, radioPosition) > 24f) return;

        HospitalRadioMilestonePresentation.Play(radioPosition);
        AutoNoiseMeter.ReportTransientNoise(1f, "RADIO NHIỄU");
        bool vietnamese = QuestUILocalization.IsVietnamese;
        string title = vietnamese ? $"RADIO BÙNG NHIỄU  •  CHẶNG {completedSegment}/3"
            : $"RADIO NOISE BURST  •  STAGE {completedSegment}/3";
        string body = vietnamese
            ? "Có tiếng động bên ngoài. Bạn có thể ra kiểm tra hoặc tiếp tục sửa Radio."
            : "There is movement outside. You may investigate or continue repairing the Radio.";
        AutoChatManager.Instance?.AddMessage(title, body);
        ShowLocalQuestEvent(title, body);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowHospitalDoorLocked([RpcTarget] PlayerRef targetPlayer, int hospitalStageValue)
    {
        if (Runner == null || Runner.LocalPlayer != targetPlayer) return;
        HospitalInvestigationStage stage = (HospitalInvestigationStage)hospitalStageValue;
        string message = stage switch
        {
            HospitalInvestigationStage.FindRadioKey =>
                "Cửa bị khóa. Chìa dự phòng đã được đánh dấu trong khu văn phòng trưởng ca.",
            HospitalInvestigationStage.FindShiftLog2 =>
                "Cửa bị khóa. Kiểm tra văn phòng trưởng ca phía sau quầy tiếp tân.",
            _ => "Cửa bị khóa. Kiểm tra sổ trực tại quầy tiếp tân để tìm nơi cất chìa dự phòng."
        };
        AutoChatManager.Instance?.AddMessage("CỬA TRẠM RADIO", message);
        ShowLocalQuestEvent("CẦN CHÌA KHÓA", message);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowHospitalClueResult([RpcTarget] PlayerRef targetPlayer, int roleValue)
    {
        if (Runner == null || Runner.LocalPlayer != targetPlayer) return;
        HospitalQuestClueRole role = (HospitalQuestClueRole)roleValue;
        if (role == HospitalQuestClueRole.ShiftLog)
        {
            const string body = "Khu điều trị đã đóng.\n" +
                                "Toàn bộ liên lạc khẩn cấp chuyển sang Trạm phụ trợ phía sau bệnh viện.\n" +
                                "Chìa khóa dự phòng do trưởng ca giữ tại văn phòng hành chính.\n\n" +
                                "Nhật ký: Kiểm tra văn phòng trưởng ca phía sau quầy tiếp tân.";
            AutoChatManager.Instance?.AddMessage("SỔ TRỰC BỆNH VIỆN",
                "Chìa khóa Trạm Radio do trưởng ca giữ tại văn phòng hành chính.");
            ShowLocalQuestEvent("SỔ TRỰC BỆNH VIỆN", body);
            return;
        }

        const string secondBody = "Lệnh phong tỏa cấp đỏ đã được xác nhận.\n" +
                                  "Đoàn xe không được dừng tại bệnh viện.\n" +
                                  "Nhân viên liên lạc có dấu hiệu nhiễm bệnh và đã tự khóa mình tại Trạm phụ trợ để giữ kênh Radio hoạt động.\n\n" +
                                  "Sổ bàn giao cho biết chìa dự phòng được giấu trong khu văn phòng.\n" +
                                  "Nhật ký: Tìm chìa khóa Radio tại vị trí đã được đánh dấu.";
        AutoChatManager.Instance?.AddMessage("VĂN PHÒNG TRƯỞNG CA",
            "Đã xác định một vị trí có thể cất chìa dự phòng. Waypoint đã được cập nhật.");
        ShowLocalQuestEvent("LỆNH PHONG TỎA CẤP ĐỎ", secondBody);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowHospitalRadioKeyCollected([RpcTarget] PlayerRef targetPlayer)
    {
        if (Runner == null || Runner.LocalPlayer != targetPlayer) return;
        const string body = "Đã nhặt chìa khóa dự phòng của Trạm Radio.\n" +
                            "Chìa khóa là trạng thái dùng chung của toàn đội và không chiếm ô inventory.\n\n" +
                            "Nhật ký: Mở Trạm liên lạc phụ trợ phía sau bệnh viện.";
        AutoChatManager.Instance?.AddMessage("CHÌA KHÓA RADIO",
            "Đội đã nhận chìa khóa dùng chung. Waypoint chuyển tới Trạm Radio.");
        ShowLocalQuestEvent("ĐÃ NHẶT CHÌA KHÓA RADIO", body);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowOfficeSearchStarted(PlayerRef focusPlayer)
    {
        _ = focusPlayer;
        QuestFlowUIPrototype.Instance?.NotifyAuthoritativeQuestStage((int)QuestStage.FindCityMap);
        ShowLocalQuestEvent(
            GameLocalization.Get("quest.office_area_title"),
            GameLocalization.Get("quest.office_area_body"));
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowArrivalCarInspected(PlayerRef focusPlayer)
    {
        ArrivalCarInspectionUI.ActiveInstance?.Close();
        StartCoroutine(ShowArrivalCarInspectedNoticeNextFrame(focusPlayer));
    }

    public void RequestReturnPlayerToSearchZone()
    {
        if (!IsSearchNeighborhoodBoundaryActive) return;
        if (HasStateAuthority) ServerReturnPlayerToSearchZone(Runner.LocalPlayer);
        else RPC_RequestReturnPlayerToSearchZone();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestReturnPlayerToSearchZone(RpcInfo info = default)
    {
        ServerReturnPlayerToSearchZone(info.Source);
    }

    private void ServerReturnPlayerToSearchZone(PlayerRef requester)
    {
        if (!HasStateAuthority || !IsSearchNeighborhoodBoundaryActive ||
            !TryGetRequestingPlayer(requester, out PlayerMovement player)) return;

        PreMilitaryQuestRuntimeBridge bridge = PreMilitaryQuestRuntimeBridge.Instance;
        if (bridge == null || !bridge.TryGetSearchZoneReturnPoint(player.transform.position, 1.25f,
                out Vector2 safePosition, out float distanceOutside) ||
            distanceOutside < bridge.ReturnActivationDistance)
            return;

        PlayerInteraction interaction = player.GetComponent<PlayerInteraction>();
        if (interaction != null && interaction.IsInVehicle)
        {
            VehicleControllerFusion vehicle = interaction.CurrentVehicleController;
            bool exitedNormally = vehicle != null && vehicle.AuthorityTryExit(player.Object);
            if (!exitedNormally)
                interaction.SetVehicleNetworkState(null, false, false, 0, safePosition);
        }

        TeleportPlayer(player, safePosition);
        Physics2D.SyncTransforms();
    }

    private IEnumerator ShowArrivalCarInspectedNoticeNextFrame(PlayerRef focusPlayer)
    {
        // The modal canvas must finish closing before the global quest notice is
        // drawn, including when another client was still inspecting the car.
        yield return null;
        AutoChatManager.Instance?.AddMessage(
            GameLocalization.Get("quest.vehicle_sender"),
            GameLocalization.Get("quest.vehicle_signal"));
        if (Runner != null && Runner.LocalPlayer == focusPlayer)
            RouteBRadioBroadcastUI.ShowOpeningSequence(EscapeRouteDecisionUI.ShowInitialChoice);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayArrivalCarRepairAudio(int actionValue, float durationSeconds)
    {
        if (!System.Enum.IsDefined(typeof(ArrivalCarRepairAction), actionValue)) return;
        BrokenArrivalCar car = BrokenArrivalCar.Instance;
        ArrivalCarInspectionUI inspection = car != null
            ? car.GetComponent<ArrivalCarInspectionUI>()
            : null;
        inspection?.PlayRepairAudioForNetwork(
            (ArrivalCarRepairAction)actionValue, durationSeconds);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_StopArrivalCarRepairAudio()
    {
        BrokenArrivalCar car = BrokenArrivalCar.Instance;
        ArrivalCarInspectionUI inspection = car != null
            ? car.GetComponent<ArrivalCarInspectionUI>()
            : null;
        inspection?.StopRepairAudioForNetwork();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowArrivalCarRepairStartResult([RpcTarget] PlayerRef targetPlayer, bool accepted,
        int actionValue, float durationSeconds, string message)
    {
        if (Runner.LocalPlayer != targetPlayer) return;
        ArrivalCarInspectionUI inspection = ArrivalCarInspectionUI.ActiveInstance;
        if (inspection != null)
            inspection.NotifyRepairStartResult(
                (ArrivalCarRepairAction)actionValue, accepted, durationSeconds, message);
        else if (accepted)
            RequestCancelArrivalCarRepair();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowArrivalCarRepairInterrupted([RpcTarget] PlayerRef targetPlayer, string message)
    {
        if (Runner.LocalPlayer != targetPlayer) return;
        ArrivalCarInspectionUI.ActiveInstance?.NotifyRepairInterrupted(message);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowArrivalCarRepairResult([RpcTarget] PlayerRef targetPlayer, bool success,
        int actionValue, string message)
    {
        if (Runner.LocalPlayer != targetPlayer) return;
        AutoChatManager.Instance?.AddMessage(success ? "SỬA XE" : "KHÔNG THỂ SỬA", message);
        ArrivalCarInspectionUI.ActiveInstance?.NotifyRepairResult(
            (ArrivalCarRepairAction)actionValue, success, message);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowArrivalCarStartResult([RpcTarget] PlayerRef targetPlayer, bool success, string message)
    {
        if (Runner.LocalPlayer != targetPlayer) return;
        AutoChatManager.Instance?.AddMessage(success ? "KHỞI ĐỘNG XE" : "KHÔNG THỂ KHỞI ĐỘNG", message);
        ArrivalCarInspectionUI.ActiveInstance?.NotifyStartResult(success, message);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowCivilianMapUnlocked(Vector2 checkpointPosition, Vector2 cityExitPosition)
    {
        QuestFlowUIPrototype flow = QuestFlowUIPrototype.Instance;
        PreMilitaryQuestRuntimeBridge bridge = PreMilitaryQuestRuntimeBridge.Instance;
        bridge?.ConfigureCivilianRouteMap(checkpointPosition, cityExitPosition,
            CivilianRouteStage.CarReady);
        flow?.SetCivilianCityMapUnlocked(true);
        CivilianRoutePresentationController.Attach(this)?.PlayCarReadySequence(
            checkpointPosition, cityExitPosition);
    }

    private void OnDrawGizmos()
    {
        if (civilianEscapeExit != null)
        {
            Gizmos.color = new Color(0.25f, 0.95f, 0.72f, 0.95f);
            Gizmos.DrawWireSphere(civilianEscapeExit.position, civilianEscapeTriggerRadius);
        }
        if (civilianCityExit != null)
        {
            Gizmos.color = new Color(1f, 0.72f, 0.18f, 0.95f);
            Gizmos.DrawWireCube(civilianCityExit.position, Vector3.one * 1.2f);
            if (civilianEscapeExit != null)
                Gizmos.DrawLine(civilianEscapeExit.position, civilianCityExit.position);
        }
        if (civilianOutroEnd != null)
        {
            Gizmos.color = new Color(0.4f, 0.75f, 1f, 0.95f);
            Gizmos.DrawWireCube(civilianOutroEnd.position, Vector3.one * 1.2f);
            if (civilianCityExit != null)
                Gizmos.DrawLine(civilianCityExit.position, civilianOutroEnd.position);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowOfficeInvestigationProgress(int completedStep, PlayerRef focusPlayer)
    {
        QuestFlowUIPrototype.Instance?.NotifyAuthoritativeQuestStage((int)(
            completedStep >= 2 ? QuestStage.CityMapFound : QuestStage.FindCityMap));
        string key = completedStep == 0 ? "quest.office_step0" :
            completedStep == 1 ? "quest.office_step1" : "quest.office_step2";
        string title = GameLocalization.Get(key + "_title");
        string body = GameLocalization.Get(key + "_body");
        AutoChatManager.Instance?.AddMessage(GameLocalization.Get("quest.investigation_sender"), body);
        if (Runner != null && Runner.LocalPlayer == focusPlayer)
        {
            if (completedStep == 0)
                RouteBRadioBroadcastUI.ShowCue(RouteBAudioCueId.DispatchDeskLog);
            else if (completedStep == 1)
                RouteBRadioBroadcastUI.ShowCue(RouteBAudioCueId.OfficeRadioRecording);
            else
                RouteBRadioBroadcastUI.ShowCue(
                    RouteBAudioCueId.MilitaryRouteRevealed,
                    ShowMilitaryMapRewardThenReveal);
        }
        ShowLocalQuestEvent(title, body);
    }

    private void ShowMilitaryMapRewardThenReveal()
    {
        HandleMilitaryMapFragmentFound(true);
    }

    public void DebugUnlockHospitalAndMilitaryMapRegions()
    {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        return;
#else
        if (!IsNetworkReady || !HasStateAuthority)
        {
            Debug.LogWarning("[QUEST TEST] Cheat mở bản đồ cần Solo/Host authority.");
            return;
        }
        int completeClueMask = (1 << PreMilitaryQuestProgress.RequiredRouteClues) - 1;
        RouteClueMask = completeClueMask;
        InsuredRouteClueMask |= completeClueMask;
        IsOfficeDiscovered = true;
        HasHospitalRadioKey = true;
        IsHospitalRadioDoorOpen = true;
        NetworkHospitalInvestigationStage = (int)HospitalInvestigationStage.RadioReady;
        IsHospitalRadioRecovered = true;
        HospitalRadioRestoreSeconds = hospitalRadioRestoreDuration;
        HospitalRadioCheckpointCount = HospitalRadioRoomRules.RestoreSegmentCount;
        IsCityMapUnlocked = true;
        NetworkQuestStage = (int)QuestStage.CityMapFound;
        RPC_DebugUnlockHospitalAndMilitaryMapRegions();
#endif
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_DebugUnlockHospitalAndMilitaryMapRegions()
    {
        QuestFlowUIPrototype.Instance?.DebugUnlockHospitalAndMilitaryMapRegions();
        AutoChatManager.Instance?.AddMessage("QUEST TEST",
            "Đã mở toàn bộ hai vùng bản đồ Bệnh viện và Quân sự.");
    }

    private bool isMilitaryMapRewardSequenceRunning;
    public bool IsMilitaryMapRewardSequenceRunning => isMilitaryMapRewardSequenceRunning;
    private System.Action pendingLevelFiveBackpackClaims;

    public void TriggerLevelFiveRewardSequence(bool introduceRouteChoice = true)
    {
        PlayerMovement localPlayer = PlayerMovement.LocalPlayerInstance;
        InventorySystem inventory = localPlayer != null ? localPlayer.GetComponent<InventorySystem>() : null;
        if (inventory != null && inventory.HasClaimedQuestBackpackReward(BackpackQuestRewardRules.RadioBackpackLevel))
        {
            return;
        }

        HandleMilitaryMapFragmentFound(introduceRouteChoice);
    }

    private void HandleMilitaryMapFragmentFound(bool introduceRouteChoice)
    {
        if (isMilitaryMapRewardSequenceRunning) return;
        isMilitaryMapRewardSequenceRunning = true;

        RouteBRadioBroadcastUI.CloseIfOpen();
        EscapeRouteDecisionUI.CloseIfOpen();
        BackpackQuestRewardPresentation.DismissNotification();

        QuestFlowUIPrototype flow = QuestFlowUIPrototype.Instance;
        flow?.QueueMilitaryMapUnlockReveal();

        if (flow != null)
        {
            flow.PlayMilitaryMapRewardAfterDialogue(() =>
            {
                flow.PlayMilitaryMapReveal(() =>
                {
                    isMilitaryMapRewardSequenceRunning = false;
                    CloseStaleRouteIntroduction(flow);
                    OnMilitaryMapSequenceComplete(introduceRouteChoice);
                });
            });
        }
        else
        {
            isMilitaryMapRewardSequenceRunning = false;
            CloseStaleRouteIntroduction(flow);
            OnMilitaryMapSequenceComplete(introduceRouteChoice);
        }
    }

    private void OnMilitaryMapSequenceComplete(bool introduceRouteChoice)
    {
        ClaimAndPresentLevelFiveBackpack(() =>
        {
            // Effect B has finished, and authoritative grant is complete (FinishPresentation timing/API preserved).
            // Defer the route choice story and AutoChat clue banner until after Notification A has dismissed.
            BackpackQuestRewardPresentation.RegisterPostNotificationAction(() =>
            {
                ExecutePostBackpackFlow(introduceRouteChoice);
            });
        });
    }

    private void ExecutePostBackpackFlow(bool introduceRouteChoice)
    {
        AutoChatManager.Instance?.AddMessage("PHÁT HIỆN MANH MỐI MỚI",
            "Phát hiện manh mối mới - bấm M để kiểm tra");
        ShowLocalQuestEvent("MẢNH BẢN ĐỒ 2", "Vị trí căn cứ quân sự đã được ghi vào bản đồ.");

        if (introduceRouteChoice && LockedEscapeRoute == EscapeEndingRoute.None)
        {
            RouteBRadioBroadcastUI.ShowCue(RouteBAudioCueId.MilitaryRouteRevealed,
                EscapeRouteDecisionUI.ShowPreMilitaryChoice);
        }
    }

    public void ClaimAndPresentLevelFiveBackpack(System.Action onComplete = null)
    {
        if (isMilitaryMapRewardSequenceRunning)
        {
            pendingLevelFiveBackpackClaims += onComplete;
            return;
        }

        PlayerMovement localPlayer = PlayerMovement.LocalPlayerInstance;
        InventorySystem inventory = localPlayer != null ? localPlayer.GetComponent<InventorySystem>() : null;
        ItemData backpack = BackpackItemCatalog.GetOrCreate(BackpackQuestRewardRules.RadioBackpackLevel);

        if (inventory != null && inventory.HasClaimedQuestBackpackReward(BackpackQuestRewardRules.RadioBackpackLevel))
        {
            onComplete?.Invoke();
            return;
        }

        int preClaimLevel = inventory != null && inventory.CurrentBackpackLevel >= 0
            ? inventory.CurrentBackpackLevel
            : (inventory != null && inventory.LastCapturedLevelFivePreviousLevel >= 0
                ? inventory.LastCapturedLevelFivePreviousLevel
                : 4);

        void StartBackpackPresentation()
        {
            RouteBRadioBroadcastUI.CloseIfOpen();
            EscapeRouteDecisionUI.CloseIfOpen();
            int previousLevel = inventory != null && inventory.LastCapturedLevelFivePreviousLevel >= 0
                ? inventory.LastCapturedLevelFivePreviousLevel
                : preClaimLevel;

            BackpackQuestRewardPresentation.ShowWithPreviousLevel(BackpackQuestRewardRules.RadioBackpackLevel, backpack, previousLevel, () =>
            {
                onComplete?.Invoke();
                System.Action pending = pendingLevelFiveBackpackClaims;
                pendingLevelFiveBackpackClaims = null;
                pending?.Invoke();
            });
        }

        if (inventory != null)
        {
            inventory.RequestClaimLevelFiveBackpackReward(StartBackpackPresentation);
        }
        else
        {
            StartBackpackPresentation();
        }
    }

    private static void CloseStaleRouteIntroduction(QuestFlowUIPrototype flow)
    {
        flow?.CloseAllQuestOverlays();
        EscapeRouteDecisionUI.CloseIfOpen();
        RouteBRadioBroadcastUI.CloseIfOpen();
        AutoUIManager.Instance?.SetQuestOverlayOpen(false);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowAllRouteCluesFound(PlayerRef focusPlayer)
    {
        _ = focusPlayer;
        QuestFlowUIPrototype flow = QuestFlowUIPrototype.Instance;
        flow?.NotifyAuthoritativeQuestStage((int)QuestStage.LocateOffice);
        flow?.PrepareForMapFragmentDialogue();
        RouteBRadioBroadcastUI.ShowCue(RouteBAudioCueId.ThirdCoordinationDocument,
            () =>
            {
                QuestFlowUIPrototype.Instance?.QueueMapUnlockReveal();
                AutoChatManager.Instance?.AddMessage("PHÁT HIỆN MANH MỐI MỚI",
                    "Phát hiện manh mối mới - bấm M để kiểm tra");
                ShowLocateOfficeObjectiveNotification();
            });
    }

    public void ShowLocateOfficeObjectiveNotification()
    {
        if (!IsNetworkReady || CurrentStage != QuestStage.LocateOffice) return;
        string message = GameLocalization.Get("quest.all_clues_body");
        AutoChatManager.Instance?.AddMessage(GameLocalization.Get("quest.clues_sender"), message);
        ShowLocalQuestEvent(GameLocalization.Get("quest.all_clues_title"), message);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowCabinetSearchResult(PlayerRef requester, bool found)
    {
        if (!IsNetworkReady || Runner.LocalPlayer != requester)
            return;

        string message = GameLocalization.Get(found ? "quest.cabinet_found_body" : "quest.cabinet_empty_body");
        AutoChatManager.Instance?.AddMessage(GameLocalization.Get("quest.sender"), message);
        ShowLocalQuestEvent(GameLocalization.Get(found ? "quest.cabinet_found_title" : "quest.cabinet_empty_title"), message);
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
    private void RPC_ShowLocalizedQuestMessage(string localizationKey, int argument0, int argument1)
    {
        string template = GameLocalization.Get(localizationKey, localizationKey);
        AutoChatManager.Instance?.AddMessage(
            GameLocalization.Get("quest.sender"),
            string.Format(template, argument0, argument1));
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowRouteBAudioCue(int cueId, PlayerRef focusPlayer)
    {
        if (cueId < (int)RouteBAudioCueId.OpeningEmergencyBroadcast ||
            cueId > (int)RouteBAudioCueId.MilitaryEvacuationComplete)
            return;
        if (Runner != null && Runner.LocalPlayer == focusPlayer)
            RouteBRadioBroadcastUI.ShowCue((RouteBAudioCueId)cueId);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayMilitaryZoneReveal()
    {
        BeginLocalMilitaryReveal();
    }

    public void BeginLocalMilitaryReveal(System.Action onFinished = null)
    {
        if (LockedEscapeRoute != EscapeEndingRoute.None)
        {
            CloseStaleRouteIntroduction(QuestFlowUIPrototype.Instance);
            return;
        }
        PreMilitaryQuestRuntimeBridge.NotifyMapFragment2Found();
        if (khuVucQuanSuFocus == null || PZ_CameraController.Instance == null)
        {
            onFinished?.Invoke();
            return;
        }

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
        focusRoutine = StartCoroutine(FocusMilitaryZoneRoutine(onFinished));
    }

    private IEnumerator FocusMilitaryZoneRoutine(System.Action onFinished)
    {
        PZ_CameraController cameraController = PZ_CameraController.Instance;
        Transform initialTarget = cameraController != null ? cameraController.CurrentTarget : null;
        if (cameraController == null || initialTarget == null || khuVucQuanSuFocus == null)
        {
            focusRoutine = null;
            onFinished?.Invoke();
            yield break;
        }

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
        onFinished?.Invoke();
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
        if (GameplayReadinessCoordinator.IsGameplaySuppressed || GameplayHudLayout.AreGameplayPromptsSuppressed() || BackpackQuestRewardPresentation.IsVisible) return;

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

        QuestFlowUIPrototype journal = QuestFlowUIPrototype.Instance;
        if (CurrentStage == QuestStage.FindCityMap && !IsQuestCutsceneActive &&
            (journal == null || journal.IsMilitaryRouteTracked))
            DrawHospitalDirectionMarker();
        if (CurrentStage == QuestStage.CityMapFound && !IsQuestCutsceneActive && localFadeAlpha < 0.001f &&
            (journal == null || journal.TrackedEscapeRoute != EscapeEndingRoute.CivilianCar))
            DrawMilitaryDirectionMarker();

        bool isPreMilitaryObjective = CurrentStage == QuestStage.SearchNeighborhood ||
                                      CurrentStage == QuestStage.LocateOffice ||
                                      CurrentStage == QuestStage.FindCityMap;
        string objective;
        if (isPreMilitaryObjective && journal != null)
        {
            // The journal's Follow button owns HUD visibility. This gives the
            // click an immediate gameplay effect instead of being cosmetic only.
            if (!journal.TryGetTrackedObjectiveText(out objective))
                return;
        }
        else if (CurrentStage == QuestStage.CityMapFound && journal != null &&
                 journal.TryGetTrackedObjectiveText(out string trackedMilitaryObjective))
        {
            objective = trackedMilitaryObjective;
        }
        else
        {
            objective = CurrentStage switch
            {
                QuestStage.NotStarted when IsNetworkReady && !IsArrivalCarInspected =>
                    GameLocalization.Get("quest.objective_inspect_car"),
                QuestStage.SearchNeighborhood =>
                    string.Format(GameLocalization.Get("quest.objective_search_records"), RouteClueCount,
                        PreMilitaryQuestProgress.RequiredRouteClues),
                QuestStage.LocateOffice => GameLocalization.Get("quest.objective_locate_office"),
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
        if (BackpackQuestRewardPresentation.IsVisible || BackpackQuestRewardPresentation.IsNotificationVisible) return;

        int previousDepth = GUI.depth;
        Color previousColor = GUI.color;
        GUI.depth = -1450;

        float width = Mathf.Min(680f, Screen.width - 48f);
        float bodyWidth = width - 44f;
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.025f), 16, 26),
            fontStyle = FontStyle.Bold,
            wordWrap = false,
            clipping = TextClipping.Clip
        };
        GUIStyle bodyStyle = new GUIStyle(titleStyle)
        {
            alignment = TextAnchor.UpperCenter,
            fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.016f), 12, 16),
            fontStyle = FontStyle.Normal,
            wordWrap = true,
            clipping = TextClipping.Clip
        };
        float bodyHeight = Mathf.Max(32f, bodyStyle.CalcHeight(new GUIContent(localQuestEventBody), bodyWidth));
        float height = Mathf.Min(Screen.height * 0.25f, 52f + bodyHeight + 14f);
        Rect panel = new Rect((Screen.width - width) * 0.5f, 78f, width, height);
        GUI.color = new Color(0.015f, 0.02f, 0.02f, localQuestEventAlpha * 0.9f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = new Color(1f, 0.67f, 0.14f, localQuestEventAlpha);
        GUI.DrawTexture(new Rect(panel.x, panel.y, 4f, panel.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 2f), Texture2D.whiteTexture);

        DrawShadowedLabel(new Rect(panel.x + 14f, panel.y + 8f, panel.width - 28f, 32f),
            localQuestEventTitle, titleStyle, new Color(1f, 0.76f, 0.27f), localQuestEventAlpha, 2f);
        DrawShadowedLabel(new Rect(panel.x + 22f, panel.y + 44f, bodyWidth, bodyHeight),
            localQuestEventBody, bodyStyle, new Color(0.94f, 0.95f, 0.94f), localQuestEventAlpha, 1f);

        GUI.color = previousColor;
        GUI.depth = previousDepth;
    }

    private void DrawClueNotice()
    {
        if (BackpackQuestRewardPresentation.IsVisible || BackpackQuestRewardPresentation.IsNotificationVisible) return;

        int previousDepth = GUI.depth;
        Color previousColor = GUI.color;
        GUI.depth = -1400;

        float width = Mathf.Min(680f, Screen.width - 48f);
        float height = 100f;
        Rect panel = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.27f, width, height);

        GUI.color = new Color(0.015f, 0.02f, 0.025f, localClueNoticeAlpha * 0.82f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = new Color(1f, 0.78f, 0.16f, localClueNoticeAlpha);
        GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(panel.x, panel.yMax - 2f, panel.width, 2f), Texture2D.whiteTexture);

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.032f), 20, 30),
            fontStyle = FontStyle.Bold,
            clipping = TextClipping.Clip
        };
        GUIStyle subtitleStyle = new GUIStyle(titleStyle)
        {
            fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.018f), 13, 18),
            fontStyle = FontStyle.Normal,
            clipping = TextClipping.Clip
        };

        DrawShadowedLabel(new Rect(panel.x + 12f, panel.y + 10f, panel.width - 24f, 44f),
            GameLocalization.Get("quest.clue_title"), titleStyle,
            new Color(1f, 0.82f, 0.2f), localClueNoticeAlpha, 2f);
        DrawShadowedLabel(new Rect(panel.x + 12f, panel.y + 52f, panel.width - 24f, 28f),
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
        PlayerMovement localPlayer = PlayerMovement.LocalPlayerInstance;
        if (localMilitaryDestinationReached || localPlayer != null &&
            Vector2.Distance(localPlayer.transform.position, khuVucQuanSuFocus.position) <= militaryMarkerArrivalDistance)
        {
            localMilitaryDestinationReached = true;
            return;
        }

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

    private void DrawHospitalDirectionMarker()
    {
        Transform target = ResolveCurrentHospitalObjective();
        if (target == null) return;

        Camera sceneCamera = Camera.main;
        if (sceneCamera == null && PZ_CameraController.Instance != null)
            sceneCamera = PZ_CameraController.Instance.GetComponentInChildren<Camera>();
        if (sceneCamera == null) return;

        Vector3 screen3 = sceneCamera.WorldToScreenPoint(target.position);
        Vector2 targetGui = new Vector2(screen3.x, Screen.height - screen3.y);
        const float horizontalMargin = 68f;
        const float topMargin = 92f;
        const float bottomMargin = 74f;
        bool isOnScreen = screen3.z > 0f && targetGui.x >= horizontalMargin &&
                          targetGui.x <= Screen.width - horizontalMargin && targetGui.y >= topMargin &&
                          targetGui.y <= Screen.height - bottomMargin;

        int previousDepth = GUI.depth;
        Color previousColor = GUI.color;
        Color previousBackgroundColor = GUI.backgroundColor;
        Color previousContentColor = GUI.contentColor;
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
        markerStyle.normal.textColor = new Color(0.9f, 1f, 0.97f, 1f);

        Vector2 markerPosition;
        if (isOnScreen)
        {
            markerPosition = targetGui + new Vector2(0f, -42f + Mathf.Sin(Time.unscaledTime * 3.2f) * 4f);
            DrawShadowedLabel(new Rect(markerPosition.x - 25f, markerPosition.y - 25f, 50f, 50f),
                "▼", arrowStyle, new Color(0.25f, 0.94f, 0.82f), pulse, 2f);
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
                "▶", arrowStyle, new Color(0.25f, 0.94f, 0.82f), pulse, 2f);
            GUI.matrix = previousMatrix;
        }

        float distance = PlayerMovement.LocalPlayerInstance != null
            ? Vector2.Distance(PlayerMovement.LocalPlayerInstance.transform.position, target.position)
            : 0f;
        string markerText = GetHospitalObjectiveLabel(CurrentHospitalInvestigationStage);
        if (distance > 0.1f) markerText += $"  •  {distance:0} m";
        const float labelWidth = 230f;
        float labelX = Mathf.Clamp(markerPosition.x - labelWidth * 0.5f, 8f, Screen.width - labelWidth - 8f);
        float labelY = isOnScreen ? markerPosition.y - 34f : markerPosition.y + 31f;
        labelY = Mathf.Clamp(labelY, 50f, Screen.height - 36f);
        // GUI.color also multiplies textColor, which made this label nearly
        // black. Tint only the box background and leave the text at full
        // contrast.
        GUI.color = Color.white;
        GUI.backgroundColor = new Color(0.03f, 0.045f, 0.043f, 0.9f);
        GUI.contentColor = Color.white;
        GUI.Box(new Rect(labelX, labelY, labelWidth, 28f), markerText, markerStyle);

        GUI.matrix = previousMatrix;
        GUI.color = previousColor;
        GUI.backgroundColor = previousBackgroundColor;
        GUI.contentColor = previousContentColor;
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
