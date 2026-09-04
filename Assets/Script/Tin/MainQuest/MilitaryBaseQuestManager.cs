using System.Collections;
using System.Collections.Generic;
using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// State-authoritative second half of the Main-scene quest. It intentionally
/// lives on the same scene NetworkObject as MainQuestManager so late joiners
/// receive one canonical military-base state.
/// </summary>
public sealed class MilitaryBaseQuestManager : NetworkBehaviour
{
    public enum Phase
    {
        NotReached,
        Investigating,
        SiegeAndRepair,
        ReadyToEscape,
        Escaped,
        Failed
    }

    public enum InteractionKind
    {
        Vehicle,
        Gate,
        Generator,
        Armory,
        BatteryCache,
        FuelCache,
        RepairKitCache,
        ExitPoint,
        OfficeSafe,
        School
    }

    public static MilitaryBaseQuestManager Instance { get; private set; }

    public bool IsSchoolClueProgressVisible =>
        CurrentPhase == Phase.Investigating && !HasExitedSchoolAfterClues &&
        MainQuestManager.Instance != null &&
        MainQuestManager.Instance.CurrentStage == MainQuestManager.QuestStage.CityMapFound;

    [Header("Military base layout")]
    [SerializeField] private Transform militaryBaseAnchor;
    [SerializeField] private Vector2 vehicleOffset = new Vector2(0f, -1.5f);
    [SerializeField] private Vector2 gateOffset = new Vector2(0f, -5f);
    [SerializeField] private Vector2 generatorOffset = new Vector2(-4f, 0f);
    [SerializeField] private Vector2 armoryOffset = new Vector2(4f, 0f);
    [SerializeField] private Vector2 batteryOffset = new Vector2(-4f, 3f);
    [SerializeField] private Vector2 fuelOffset = new Vector2(0f, 3f);
    [SerializeField] private Vector2 repairKitOffset = new Vector2(4f, 3f);
    [SerializeField] private Vector2 exitOffset = new Vector2(0f, 8f);
    [SerializeField, Min(0.5f)] private float interactionDistance = 1.35f;

    [Header("School investigation and story commitment")]
    [SerializeField, Min(0.5f)] private float schoolClueValidationDistance = 1.75f;
    [SerializeField, Min(0.5f)] private float roofExitValidationPadding = 4f;
    [SerializeField, Min(0.1f)] private float cinematicGatherSpacing = 0.72f;

    [Header("Balance")]
    [SerializeField, Min(1f)] private float baseGateHealth = MilitaryQuestRules.BaseGateHealth;
    [SerializeField, Min(1f)] private float repairDurationSeconds = 12f;

    [Header("Route B vehicle escape")]
    [SerializeField, Min(1f)] private float acceleratedGateDrainSeconds = 8f;
    [SerializeField, Min(0.5f)] private float escapeWaypointReachRadius = 3.25f;

    [Header("Police car repair gameplay")]
    [FormerlySerializedAs("enableRoadsideRepairTest")]
    [SerializeField] private bool enablePoliceCarRepairGameplay = true;
    [SerializeField, Min(1f)] private float skillRepairDurationSeconds = 45f;
    [SerializeField, Min(0.1f)] private float skillCheckIntervalMinSeconds = 4f;
    [SerializeField, Min(0.1f)] private float skillCheckIntervalMaxSeconds = 7f;
    [SerializeField, Min(0.25f)] private float skillCheckRotationSeconds = 1.25f;
    [SerializeField, Range(1f, 180f)] private float skillCheckSuccessArcDegrees = 25f;
    [SerializeField, Range(1f, 90f)] private float skillCheckPerfectArcDegrees = 8f;
    [SerializeField, Range(0f, 0.8f)] private float skillCheckMinimumTravelFraction = 0.30f;
    [SerializeField, Range(90f, 99.9f)] private float skillCheckFinaleCutoffProgress = 95f;
    [SerializeField, Min(0f)] private float skillCheckSuccessBonus = 3.5f;
    [SerializeField, Min(0f)] private float skillCheckPerfectBonus = 7f;
    [SerializeField, Min(0f)] private float skillCheckMissPenalty = 2f;
    [SerializeField, Min(0f)] private float skillCheckMissPauseSeconds = 1f;
    [SerializeField] private bool emitRepairFailureNoise;

    [Networked] public int MilitaryPhase { get; private set; }
    [Networked] public float GateCurrentHealth { get; private set; }
    [Networked] public float GateMaxHealth { get; private set; }
    [Networked] public float VehicleRepairProgress { get; private set; }
    [Networked] public NetworkBool IsGeneratorActive { get; private set; }
    [Networked] public NetworkBool IsArmoryUnlocked { get; private set; }
    [Networked] public NetworkBool IsOfficeSafeClaimed { get; private set; }
    [Networked] public NetworkBool HasBatteryInstalled { get; private set; }
    [Networked] public NetworkBool HasFuelInstalled { get; private set; }
    [Networked] public NetworkBool HasRepairKitInstalled { get; private set; }
    [Networked] public NetworkBool IsBatteryCacheClaimed { get; private set; }
    [Networked] public NetworkBool IsFuelCacheClaimed { get; private set; }
    [Networked] public NetworkBool IsRepairKitCacheClaimed { get; private set; }
    [Networked] public PlayerRef ActiveRepairer { get; private set; }
    [Networked] public float SurvivalSeconds { get; private set; }
    [Networked] public float RepairSkillCheckProgress { get; private set; }
    [Networked] public NetworkBool RepairSkillCheckSessionActive { get; private set; }
    [Networked] public NetworkBool RepairSkillCheckEventActive { get; private set; }
    [Networked] public float RepairSkillCheckElapsed { get; private set; }
    [Networked] public float RepairSkillCheckTargetAngle { get; private set; }
    [Networked] public float RepairPenaltyRemaining { get; private set; }
    [Networked] public float NextRepairSkillCheckSeconds { get; private set; }
    [Networked] public int RepairSkillCheckSequence { get; private set; }
    [Networked] public int PoliceCarRepairMask { get; private set; }
    [Networked] public int ActivePoliceRepairAction { get; private set; }
    [Networked] public float PoliceEngineRepairProgress { get; private set; }
    [Networked] public float PoliceHoodRepairProgress { get; private set; }
    [Networked] public float PoliceFuelRepairProgress { get; private set; }
    [Networked] public float PoliceBatteryRepairProgress { get; private set; }
    [Networked] public float PoliceTireRepairProgress { get; private set; }
    [Networked] public int SchoolClueMask { get; private set; }
    [Networked] public NetworkBool HasExitedSchoolAfterClues { get; private set; }
    [Networked] public NetworkBool IsPoliceCarStoryInspected { get; private set; }
    [Networked] public NetworkBool IsMilitaryRouteVoteActive { get; private set; }
    [Networked] public int MilitaryRouteVoteId { get; private set; }
    [Networked] public int MilitaryRouteVoteApprovedCount { get; private set; }
    [Networked] public int MilitaryRouteVoteRequiredCount { get; private set; }
    [Networked] public NetworkBool IsMilitaryIntroCinematicActive { get; private set; }

    // Team respawn checkpoint for the military finale. Saved when the Route B
    // vote commits (cinematic starts) and consumed by the authority auto-respawn.
    [Networked] public NetworkBool IsRespawnCheckpointActive { get; private set; }
    [Networked] public Vector2 RespawnCheckpointPosition { get; private set; }
    [Networked] public int TeamRespawnsRemaining { get; private set; }
    [Networked] public NetworkBool IsMultiplayerSiege { get; private set; }
    [Networked] public NetworkBool IsSoloGateDpsActive { get; private set; }
    [Networked] public float SoloGateDpsElapsed { get; private set; }
    [Networked] public NetworkBool IsSoloRetryCheckpointReady { get; private set; }
    [Networked] public NetworkBool IsEscapeVehicleEngineStarted { get; private set; }
    [Networked] public NetworkBool IsEscapeVehicleDriveUnlocked { get; private set; }
    [Networked] public float EscapeVehicleStartupRemaining { get; private set; }
    [Networked] public int EscapeWaypointIndex { get; private set; }
    [Networked] public NetworkBool IsEscapeOutroActive { get; private set; }
    [Networked] public int RecoveryBatteryCount { get; private set; }
    [Networked] public int RecoveryFuelCount { get; private set; }
    [Networked] public int RecoveryRepairKitCount { get; private set; }

    private bool hasSpawned;
    private GameObject presentationRoot;
    private SiegeHordeDirector hordeDirector;
    private MilitaryRepairLootCoordinator repairLootCoordinator;
    private MilitaryGateController gateController;
    private MilitaryEscapeVehicleRepair vehicleRepair;
    private RoadsideVehicleRepairStation roadsideRepairStation;
    private VehicleControllerFusion roadsideRepairVehicle;
    private bool roadsideVehiclePrepared;
    private MilitaryRouteCinematicController cinematicController;
    private MilitaryRouteBEscapePresentation escapePresentation;
    private PolygonCollider2D schoolRoofTrigger;
    private PolygonCollider2D militaryAreaTrigger;
    private readonly List<MilitarySchoolCluePoint> schoolCluePoints = new();
    private readonly HashSet<PlayerRef> finalMapFragmentRecipients = new();
    private readonly HashSet<PlayerRef> voteParticipants = new();
    private readonly HashSet<PlayerRef> voteApprovals = new();
    private Transform policeCarMarker;
    private Transform gateClosingMarker;
    private Transform schoolTeleportMarker;
    private readonly Transform[] escapeWaypoints = new Transform[3];
    private PolygonCollider2D escapeFinalTrigger;
    private Transform escapeVehicleOutroTarget;
    private Transform escapeCameraTarget;
    private float nextEscapeStartDeniedAt;
    private float nextMilitaryBackpackRewardScanTime;

    public bool IsNetworkReady => hasSpawned && Object != null && Object.IsValid && Runner != null && Runner.IsRunning;
    public Phase CurrentPhase => IsNetworkReady ? (Phase)MilitaryPhase : Phase.NotReached;
    public bool HasAllParts => MilitaryQuestRules.HasAllParts(HasBatteryInstalled, HasFuelInstalled,
        HasRepairKitInstalled);
    public bool IsGateBroken => IsNetworkReady && GateCurrentHealth <= 0f;
    public float InteractionDistance => interactionDistance;
    public Vector2 EscapeExitPosition => GetInteractionPosition(InteractionKind.ExitPoint);
    public bool IsLocalPlayerRepairer => IsNetworkReady && RepairSkillCheckSessionActive &&
        ActiveRepairer != PlayerRef.None && Runner.LocalPlayer == ActiveRepairer;
    public float RepairSkillCheckRotationSeconds => skillCheckRotationSeconds;
    public float RepairSkillCheckSuccessArcDegrees => skillCheckSuccessArcDegrees;
    public float RepairSkillCheckPerfectArcDegrees => skillCheckPerfectArcDegrees;
    public bool ArePoliceCarRepairsComplete => IsNetworkReady && PoliceCarRepairRules.IsComplete(PoliceCarRepairMask);
    public float PoliceCarOverallRepairProgress => IsNetworkReady
        ? (PoliceEngineRepairProgress + PoliceHoodRepairProgress + PoliceFuelRepairProgress +
           PoliceBatteryRepairProgress + PoliceTireRepairProgress) / 5f
        : 0f;
    public bool HasAllSchoolClues => IsNetworkReady && MilitaryStoryFlowRules.HasAllSchoolClues(SchoolClueMask);
    public int SchoolClueCount => CountBits(SchoolClueMask);
    public VehicleControllerFusion PoliceVehicle => roadsideRepairVehicle;
    public Vector2 PoliceCarPosition => roadsideRepairVehicle != null
        ? roadsideRepairVehicle.transform.position
        : GetInteractionPosition(InteractionKind.Vehicle);
    public Vector2 GateClosingPosition => GetInteractionPosition(InteractionKind.Gate);
    public bool ShouldOfferStoryCarInteraction => IsNetworkReady && CurrentPhase == Phase.Investigating &&
        HasExitedSchoolAfterClues && MainQuestManager.Instance != null &&
        MainQuestManager.Instance.LockedEscapeRoute == EscapeEndingRoute.None && !IsMilitaryIntroCinematicActive;
    public bool CanUsePoliceRepairMinigame => IsNetworkReady && CurrentPhase == Phase.SiegeAndRepair &&
        !IsMilitaryIntroCinematicActive;
    /// <summary>True once the military respawn system governs deaths (siege/escape phases).</summary>
    public bool GovernsRespawn => IsNetworkReady && IsRespawnCheckpointActive &&
        IsMultiplayerSiege && !IsEscapeVehicleEngineStarted &&
        (CurrentPhase == Phase.SiegeAndRepair || CurrentPhase == Phase.ReadyToEscape);
    public bool IsSoloSiege => IsNetworkReady && !IsMultiplayerSiege;
    public bool CanOfferSoloRetry => IsNetworkReady && IsRespawnCheckpointActive &&
        IsSoloRetryCheckpointReady && !IsMultiplayerSiege && CurrentPhase == Phase.Failed;
    public bool IsEscapeGuidanceActive => IsNetworkReady && IsEscapeVehicleEngineStarted &&
        !IsEscapeOutroActive && CurrentPhase == Phase.ReadyToEscape;
    public int EscapeGuidanceWaypointCount => escapeWaypoints.Length + 1;
    public Vector2 EscapeCameraTargetPosition => escapeCameraTarget != null
        ? (Vector2)escapeCameraTarget.position
        : PoliceCarPosition + new Vector2(-60f, 40f);
    public Vector2 EscapeVehicleOutroTargetPosition => escapeVehicleOutroTarget != null
        ? (Vector2)escapeVehicleOutroTarget.position
        : PoliceCarPosition + (roadsideRepairVehicle != null ? roadsideRepairVehicle.VisionDirection : Vector2.up) * 24f;

    private readonly Dictionary<PlayerRef, float> militaryDeathObservedAt = new();
    private readonly HashSet<PlayerRef> recoveredPermanentEliminations = new();
    private float soloRetryCheckpointSurvivalSeconds;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(this);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (presentationRoot != null) Destroy(presentationRoot);
        presentationRoot = null;
        if (roadsideRepairStation != null) Destroy(roadsideRepairStation);
        roadsideRepairStation = null;
        MilitaryRouteVoteUI.Close();
        if (cinematicController != null)
        {
            cinematicController.StopImmediate();
            cinematicController = null;
        }
        if (escapePresentation != null)
        {
            escapePresentation.StopImmediate();
            escapePresentation = null;
        }
    }

    public override void Spawned()
    {
        // Main.unity still contains the legacy serialized 1,000 HP value.
        // Enforce the canonical minimum without saving over the user's scene.
        baseGateHealth = Mathf.Max(baseGateHealth, MilitaryQuestRules.BaseGateHealth);
        hasSpawned = true;
        ResolveAnchor();
        BuildPresentation();
        EnsurePoliceCarRepairGameplay();

        if (!HasStateAuthority) return;
        MilitaryPhase = (int)Phase.NotReached;
        GateMaxHealth = Mathf.Max(1f, baseGateHealth);
        GateCurrentHealth = GateMaxHealth;
        VehicleRepairProgress = 0f;
        IsGeneratorActive = false;
        IsArmoryUnlocked = false;
        IsOfficeSafeClaimed = false;
        HasBatteryInstalled = false;
        HasFuelInstalled = false;
        HasRepairKitInstalled = false;
        IsBatteryCacheClaimed = false;
        IsFuelCacheClaimed = false;
        IsRepairKitCacheClaimed = false;
        ActiveRepairer = PlayerRef.None;
        SurvivalSeconds = 0f;
        RepairSkillCheckProgress = 0f;
        RepairSkillCheckSessionActive = false;
        RepairSkillCheckEventActive = false;
        RepairSkillCheckElapsed = 0f;
        RepairSkillCheckTargetAngle = 0f;
        RepairPenaltyRemaining = 0f;
        NextRepairSkillCheckSeconds = 0f;
        RepairSkillCheckSequence = 0;
        PoliceCarRepairMask = 0;
        ActivePoliceRepairAction = -1;
        PoliceEngineRepairProgress = 0f;
        PoliceHoodRepairProgress = 0f;
        PoliceFuelRepairProgress = 0f;
        PoliceBatteryRepairProgress = 0f;
        PoliceTireRepairProgress = 0f;
        SchoolClueMask = 0;
        finalMapFragmentRecipients.Clear();
        HasExitedSchoolAfterClues = false;
        IsPoliceCarStoryInspected = false;
        IsMilitaryRouteVoteActive = false;
        MilitaryRouteVoteId = 0;
        MilitaryRouteVoteApprovedCount = 0;
        MilitaryRouteVoteRequiredCount = 0;
        IsMilitaryIntroCinematicActive = false;
        IsRespawnCheckpointActive = false;
        RespawnCheckpointPosition = Vector2.zero;
        TeamRespawnsRemaining = 0;
        IsMultiplayerSiege = false;
        IsSoloGateDpsActive = false;
        SoloGateDpsElapsed = 0f;
        IsSoloRetryCheckpointReady = false;
        ResetEscapeVehicleState();
        soloRetryCheckpointSurvivalSeconds = 0f;
        militaryDeathObservedAt.Clear();
        recoveredPermanentEliminations.Clear();
        RecoveryBatteryCount = 0;
        RecoveryFuelCount = 0;
        RecoveryRepairKitCount = 0;
        voteParticipants.Clear();
        voteApprovals.Clear();
        nextMilitaryBackpackRewardScanTime = 0f;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        hasSpawned = false;
        if (presentationRoot != null) Destroy(presentationRoot);
        presentationRoot = null;
        MilitaryRouteVoteUI.Close();
        if (cinematicController != null)
        {
            cinematicController.StopImmediate();
            cinematicController = null;
        }
        if (escapePresentation != null)
        {
            escapePresentation.StopImmediate();
            escapePresentation = null;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        TickRepairSkillCheck();
        if (IsMilitaryRouteVoteActive) PruneDisconnectedVoters();
        if (IsMilitaryIntroCinematicActive) LockAllLivingPlayersForCinematic();
        if (CurrentPhase != Phase.Escaped && CurrentPhase != Phase.Failed)
            SurvivalSeconds += Runner.DeltaTime;

        TickSoloGateDps();
        TickEscapeVehicleFlow();
        if (IsEscapeOutroActive) TickMilitaryOutroFollowers();
        if (CurrentPhase == Phase.SiegeAndRepair || CurrentPhase == Phase.ReadyToEscape)
            TickPermanentEliminationRecovery();

        if ((CurrentPhase == Phase.SiegeAndRepair || CurrentPhase == Phase.ReadyToEscape) && !AnyLivingPlayer())
        {
            MilitaryPhase = (int)Phase.Failed;
            if (RepairSkillCheckSessionActive) AuthorityInterruptRepair(ActiveRepairer,
                "quest.military.repair_stopped");
            else ActiveRepairer = PlayerRef.None;
        }

        if (GovernsRespawn) TickAuthorityAutoRespawn();
    }

    public override void Render()
    {
        EnsurePoliceCarRepairGameplay();
        gateController?.RefreshPresentation();
        vehicleRepair?.RefreshPresentation();
        escapePresentation?.RefreshPresentation();
    }

    public Vector2 GetInteractionPosition(InteractionKind kind)
    {
        ResolveAnchor();
        Vector2 origin = militaryBaseAnchor != null ? militaryBaseAnchor.position : Vector2.zero;
        return kind switch
        {
            InteractionKind.Vehicle => roadsideRepairVehicle != null
                ? (Vector2)roadsideRepairVehicle.transform.position
                : policeCarMarker != null ? (Vector2)policeCarMarker.position : origin + vehicleOffset,
            InteractionKind.Gate => gateClosingMarker != null
                ? (Vector2)gateClosingMarker.position
                : origin + gateOffset,
            InteractionKind.Generator => origin + generatorOffset,
            InteractionKind.Armory => origin + armoryOffset,
            InteractionKind.BatteryCache => origin + batteryOffset,
            InteractionKind.FuelCache => origin + fuelOffset,
            InteractionKind.RepairKitCache => origin + repairKitOffset,
            InteractionKind.ExitPoint => origin + exitOffset,
            InteractionKind.OfficeSafe => GetOfficeSafePosition(),
            InteractionKind.School => schoolTeleportMarker != null
                ? (Vector2)schoolTeleportMarker.position
                : origin,
            _ => origin
        };
    }

    public bool CanInvestigateSchoolClue(int clueIndex)
    {
        if (!IsNetworkReady || clueIndex < 0 || clueIndex >= MilitaryStoryFlowRules.RequiredSchoolClues ||
            CurrentPhase != Phase.NotReached || MainQuestManager.Instance == null ||
            MainQuestManager.Instance.CurrentStage != MainQuestManager.QuestStage.CityMapFound ||
            MainQuestManager.Instance.LockedEscapeRoute != EscapeEndingRoute.None)
            return false;
        return true;
    }

    public void RequestInvestigateSchoolClue(int clueIndex)
    {
        if (!CanInvestigateSchoolClue(clueIndex)) return;
        if (HasStateAuthority) ServerInvestigateSchoolClue(Runner.LocalPlayer, clueIndex);
        else RPC_RequestInvestigateSchoolClue(clueIndex);
    }

    public void RequestHandleSchoolRoofExit()
    {
        if (!IsNetworkReady || HasExitedSchoolAfterClues) return;
        if (HasStateAuthority) ServerHandleSchoolRoofExit(Runner.LocalPlayer);
        else RPC_RequestHandleSchoolRoofExit();
    }

    public void RequestInspectPoliceCarStory()
    {
        if (!ShouldOfferStoryCarInteraction) return;
        if (HasStateAuthority) ServerInspectPoliceCarStory(Runner.LocalPlayer);
        else RPC_RequestInspectPoliceCarStory();
    }

    public void RequestSubmitMilitaryRouteVote(int voteId, bool approve)
    {
        if (!IsNetworkReady) return;
        if (HasStateAuthority) ServerSubmitMilitaryRouteVote(Runner.LocalPlayer, voteId, approve);
        else RPC_RequestSubmitMilitaryRouteVote(voteId, approve);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestInvestigateSchoolClue(int clueIndex, RpcInfo info = default) =>
        ServerInvestigateSchoolClue(info.Source, clueIndex);

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestHandleSchoolRoofExit(RpcInfo info = default) =>
        ServerHandleSchoolRoofExit(info.Source);

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestInspectPoliceCarStory(RpcInfo info = default) =>
        ServerInspectPoliceCarStory(info.Source);

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestSubmitMilitaryRouteVote(int voteId, NetworkBool approve, RpcInfo info = default) =>
        ServerSubmitMilitaryRouteVote(info.Source, voteId, approve);

    private void ServerInvestigateSchoolClue(PlayerRef requester, int clueIndex)
    {
        if (!HasStateAuthority || !CanInvestigateSchoolClue(clueIndex) ||
            !TryGetRequestingPlayer(requester, out PlayerMovement player) ||
            clueIndex >= schoolCluePoints.Count || schoolCluePoints[clueIndex] == null ||
            Vector2.Distance(player.transform.position, schoolCluePoints[clueIndex].transform.position) >
            schoolClueValidationDistance)
            return;

        int clueBit = 1 << clueIndex;
        bool firstTeamDiscovery = (SchoolClueMask & clueBit) == 0;
        if (firstTeamDiscovery) SchoolClueMask |= clueBit;
        bool grantsFinalMapFragment = clueIndex == 2 && finalMapFragmentRecipients.Add(requester);
        RPC_ShowSchoolClueDialogue(requester, clueIndex, firstTeamDiscovery, SchoolClueCount,
            MilitaryStoryFlowRules.RequiredSchoolClues, grantsFinalMapFragment);
        if (firstTeamDiscovery && SchoolClueCount >= MilitaryStoryFlowRules.RequiredSchoolClues)
        {
            RPC_ShowQuestMessage(string.Format(GameLocalization.Get("quest.military.clues_progress_complete"),
                SchoolClueCount, MilitaryStoryFlowRules.RequiredSchoolClues));
        }
    }

    private void ServerHandleSchoolRoofExit(PlayerRef requester)
    {
        if (!HasStateAuthority || CurrentPhase != Phase.NotReached || HasExitedSchoolAfterClues ||
            !TryGetRequestingPlayer(requester, out PlayerMovement player) || schoolRoofTrigger == null ||
            schoolRoofTrigger.OverlapPoint(player.transform.position) ||
            Vector2.Distance(player.transform.position, schoolRoofTrigger.bounds.ClosestPoint(player.transform.position)) >
            roofExitValidationPadding)
            return;

        if (!HasAllSchoolClues)
        {
            Vector2 closest = schoolRoofTrigger.bounds.ClosestPoint(player.transform.position);
            Vector2 towardCenter = ((Vector2)schoolRoofTrigger.bounds.center - closest).normalized;
            Vector2 returnPoint = closest + towardCenter * 0.75f;
            if (!schoolRoofTrigger.OverlapPoint(returnPoint)) returnPoint = schoolRoofTrigger.bounds.center;
            TeleportPlayer(player, returnPoint);
            RPC_ShowSchoolExitBlocked(requester, SchoolClueCount, MilitaryStoryFlowRules.RequiredSchoolClues);
            return;
        }

        HasExitedSchoolAfterClues = true;
        MilitaryPhase = (int)Phase.Investigating;
        RPC_ShowPoliceCarObjective(requester);
    }

    private void ServerInspectPoliceCarStory(PlayerRef requester)
    {
        if (!HasStateAuthority || !ShouldOfferStoryCarInteraction || IsMilitaryRouteVoteActive ||
            !TryGetRequestingPlayer(requester, out PlayerMovement player) || roadsideRepairStation == null ||
            !roadsideRepairStation.IsPlayerInRepairPosition(player.transform.position))
            return;

        IsPoliceCarStoryInspected = true;
        BeginMilitaryRouteVote(requester);
    }

    private void BeginMilitaryRouteVote(PlayerRef requester)
    {
        if (!HasStateAuthority || IsMilitaryRouteVoteActive || IsMilitaryIntroCinematicActive) return;
        voteParticipants.Clear();
        voteApprovals.Clear();
        foreach (PlayerRef player in Runner.ActivePlayers)
            voteParticipants.Add(player);
        if (voteParticipants.Count == 0 && requester != PlayerRef.None)
            voteParticipants.Add(requester);
        if (voteParticipants.Count == 0) return;

        MilitaryRouteVoteId++;
        IsMilitaryRouteVoteActive = true;
        MilitaryRouteVoteApprovedCount = 0;
        MilitaryRouteVoteRequiredCount = voteParticipants.Count;
        RPC_OpenMilitaryRouteVote(MilitaryRouteVoteId, MilitaryRouteVoteRequiredCount);
    }

    private void ServerSubmitMilitaryRouteVote(PlayerRef requester, int voteId, bool approve)
    {
        if (!HasStateAuthority || !IsMilitaryRouteVoteActive || voteId != MilitaryRouteVoteId ||
            !voteParticipants.Contains(requester)) return;

        if (!approve)
        {
            CancelMilitaryRouteVote("quest.military.vote_cancel_ready");
            return;
        }

        voteApprovals.Add(requester);
        MilitaryRouteVoteApprovedCount = voteApprovals.Count;
        MilitaryRouteVoteRequiredCount = voteParticipants.Count;
        RPC_UpdateMilitaryRouteVote(MilitaryRouteVoteId, MilitaryRouteVoteApprovedCount,
            MilitaryRouteVoteRequiredCount);
        if (voteApprovals.Count == voteParticipants.Count)
            CommitMilitaryRouteVote();
    }

    private void PruneDisconnectedVoters()
    {
        HashSet<PlayerRef> active = new HashSet<PlayerRef>();
        foreach (PlayerRef player in Runner.ActivePlayers) active.Add(player);
        foreach (PlayerRef player in voteParticipants)
        {
            if (active.Contains(player)) continue;

            if (IsMilitaryRouteVoteActive && voteApprovals.Count > 0 && voteApprovals.Contains(player))
            {
                CancelMilitaryRouteVote("quest.military.vote_cancel_ready");
                return;
            }

            if (IsMilitaryRouteVoteActive)
            {
                voteParticipants.Remove(player);
                voteApprovals.Remove(player);
                if (voteParticipants.Count == 0)
                {
                    CancelMilitaryRouteVote("quest.military.vote_cancel_no_players");
                    return;
                }

                MilitaryRouteVoteApprovedCount = voteApprovals.Count;
                MilitaryRouteVoteRequiredCount = voteParticipants.Count;
                RPC_UpdateMilitaryRouteVote(MilitaryRouteVoteId, MilitaryRouteVoteApprovedCount,
                    MilitaryRouteVoteRequiredCount);
                if (voteApprovals.Count == voteParticipants.Count)
                    CommitMilitaryRouteVote();
            }
        }
    }

    private void CancelMilitaryRouteVote(string messageKey)
    {
        int closedVoteId = MilitaryRouteVoteId;
        IsMilitaryRouteVoteActive = false;
        MilitaryRouteVoteApprovedCount = 0;
        MilitaryRouteVoteRequiredCount = 0;
        voteParticipants.Clear();
        voteApprovals.Clear();
        RPC_CloseMilitaryRouteVote(closedVoteId, messageKey);
    }

    private void CommitMilitaryRouteVote()
    {
        if (!HasStateAuthority || !IsMilitaryRouteVoteActive || MainQuestManager.Instance == null ||
            !MainQuestManager.Instance.AuthorityTryLockEscapeRoute(EscapeEndingRoute.MilitaryEvacuation))
        {
            CancelMilitaryRouteVote("quest.military.vote_cancel_route_locked");
            return;
        }

        int closedVoteId = MilitaryRouteVoteId;
        IsMilitaryRouteVoteActive = false;
        MilitaryRouteVoteApprovedCount = voteApprovals.Count;
        MilitaryRouteVoteRequiredCount = voteParticipants.Count;
        voteParticipants.Clear();
        voteApprovals.Clear();
        IsMilitaryIntroCinematicActive = true;
        DayNightManager.Instance?.AuthorityLockMilitaryFinaleTime();
        // Route B is committed: save the team respawn checkpoint around the
        // police car so later deaths respawn inside the closed base.
        IsRespawnCheckpointActive = true;
        RespawnCheckpointPosition = GetInteractionPosition(InteractionKind.Vehicle);
        int activePlayers = CountActivePlayers();
        IsMultiplayerSiege = activePlayers > 1;
        IsSoloRetryCheckpointReady = !IsMultiplayerSiege &&
            HostModeSpawner.Instance != null &&
            HostModeSpawner.Instance.CaptureSoloMilitaryCheckpoint(Runner.LocalPlayer);
        soloRetryCheckpointSurvivalSeconds = SurvivalSeconds;
        ResolveMilitaryAreaTrigger();
        int removedZombies = AuthorityClearZombiesInsideMilitaryArea();
        Vector2 cinematicStart = GetCinematicStartPosition();
        Debug.Log($"[MILITARY CINEMATIC] Dọn {removedZombies} zombie trong KhuVucQuanSu; " +
                  $"Host visual bắt đầu tại {cinematicStart}.");
        RPC_CloseMilitaryRouteVote(closedVoteId, string.Empty);
        RPC_PlayMilitaryIntroCinematic(Runner.LocalPlayer, cinematicStart);
    }

    public void AuthorityCompleteMilitaryIntroCinematic(PlayerRef hostPlayer)
    {
        if (!HasStateAuthority || !IsMilitaryIntroCinematicActive || CurrentPhase != Phase.Investigating)
            return;
        GatherLivingPlayersNearClosedGate();
        IsMilitaryIntroCinematicActive = false;
        MilitaryPhase = (int)Phase.SiegeAndRepair;
        // Establish the authoritative loot set as part of the phase transition.
        // The coordinator's Update retry remains as recovery for temporarily
        // unavailable prefab/marker data, but normal gameplay does not depend
        // on a later MonoBehaviour frame happening to run.
        repairLootCoordinator?.AuthorityTrySetup();
        int activePlayers = CountActivePlayers();
        // Lock the mode for this entire siege. A disconnect must not turn an
        // already multiplayer finale into Solo and invalidate its respawn pool.
        IsMultiplayerSiege = activePlayers > 1;
        GateMaxHealth = Mathf.Max(1f, MilitaryQuestRules.ComputeSiegeGateMaxHealthForDifficulty(activePlayers,
            GetSelectedDifficulty()));
        GateCurrentHealth = GateMaxHealth;
        TeamRespawnsRemaining = MilitaryQuestRules.ComputeTeamRespawnCharges(activePlayers);
        IsSoloGateDpsActive = false;
        SoloGateDpsElapsed = 0f;
        ResetEscapeVehicleState();
        militaryDeathObservedAt.Clear();
        recoveredPermanentEliminations.Clear();
        RecoveryBatteryCount = 0;
        RecoveryFuelCount = 0;
        RecoveryRepairKitCount = 0;
        IsGeneratorActive = false;
        ActiveRepairer = PlayerRef.None;
        RPC_StartSiegePresentation();
    }

    public void RequestSoloMilitaryRetry()
    {
        if (!IsNetworkReady || Runner.LocalPlayer == PlayerRef.None) return;
        if (HasStateAuthority) AuthorityRetrySoloMilitaryFinale(Runner.LocalPlayer);
        else RPC_RequestSoloMilitaryRetry(Runner.LocalPlayer);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestSoloMilitaryRetry(PlayerRef requester, RpcInfo info = default)
    {
        if (info.Source != PlayerRef.None && info.Source != requester) return;
        AuthorityRetrySoloMilitaryFinale(requester);
    }

    private void AuthorityRetrySoloMilitaryFinale(PlayerRef requester)
    {
        if (!HasStateAuthority || !CanOfferSoloRetry || requester == PlayerRef.None || requester != Runner.LocalPlayer)
            return;

        HostModeSpawner spawner = HostModeSpawner.Instance;
        if (spawner == null || !spawner.PrepareSoloMilitaryCheckpointRespawn(requester))
        {
            Debug.LogError("[MILITARY RETRY] Không tìm thấy snapshot Solo trước cinematic; từ chối reset để tránh mất đồ.");
            return;
        }

        hordeDirector?.AuthorityResetAndDespawnAll();
        repairLootCoordinator?.AuthorityResetForRetry();
        ClearRepairSkillCheckSession();
        militaryDeathObservedAt.Clear();
        recoveredPermanentEliminations.Clear();
        RecoveryBatteryCount = 0;
        RecoveryFuelCount = 0;
        RecoveryRepairKitCount = 0;

        MilitaryPhase = (int)Phase.Investigating;
        GateMaxHealth = Mathf.Max(1f, baseGateHealth);
        GateCurrentHealth = GateMaxHealth;
        VehicleRepairProgress = 0f;
        IsGeneratorActive = false;
        IsArmoryUnlocked = false;
        IsOfficeSafeClaimed = false;
        HasBatteryInstalled = false;
        HasFuelInstalled = false;
        HasRepairKitInstalled = false;
        IsBatteryCacheClaimed = false;
        IsFuelCacheClaimed = false;
        IsRepairKitCacheClaimed = false;
        RepairSkillCheckProgress = 0f;
        RepairSkillCheckSequence = 0;
        PoliceCarRepairMask = 0;
        PoliceEngineRepairProgress = 0f;
        PoliceHoodRepairProgress = 0f;
        PoliceFuelRepairProgress = 0f;
        PoliceBatteryRepairProgress = 0f;
        PoliceTireRepairProgress = 0f;
        SurvivalSeconds = soloRetryCheckpointSurvivalSeconds;
        TeamRespawnsRemaining = 0;
        IsMultiplayerSiege = false;
        IsSoloGateDpsActive = false;
        SoloGateDpsElapsed = 0f;
        IsMilitaryIntroCinematicActive = true;
        ResetEscapeVehicleState();

        RPC_ResetSoloRetryPresentation();
        Vector2 spawnPosition = GetCinematicStartPosition();
        if (!spawner.AuthorityRespawnAtCheckpoint(requester, spawnPosition))
        {
            IsMilitaryIntroCinematicActive = false;
            MilitaryPhase = (int)Phase.Failed;
            Debug.LogError("[MILITARY RETRY] Không thể tạo lại avatar tại checkpoint.");
            return;
        }

        ResolveMilitaryAreaTrigger();
        AuthorityClearZombiesInsideMilitaryArea();
        StartCoroutine(AuthorityReplaySoloCinematicWhenAvatarReady(requester, spawnPosition));
        Debug.Log("[MILITARY RETRY] Đã reset trắng Route B Solo; đang chờ avatar mới trước khi phát cinematic.");
    }

    private IEnumerator AuthorityReplaySoloCinematicWhenAvatarReady(PlayerRef requester, Vector2 spawnPosition)
    {
        float deadline = Time.realtimeSinceStartup + 5f;
        while (Time.realtimeSinceStartup < deadline)
        {
            PlayerMovement movement = null;
            if (Runner != null && Runner.TryGetPlayerObject(requester, out NetworkObject playerObject) &&
                playerObject != null && playerObject.IsValid)
            {
                movement = playerObject.GetComponent<PlayerMovement>();
            }
            if (movement == null)
            {
                TryGetRequestingPlayer(requester, out movement);
            }
            if (movement == null && PlayerMovement.LocalPlayerInstance != null)
            {
                movement = PlayerMovement.LocalPlayerInstance;
            }

            if (movement != null && movement.gameObject != null)
            {
                PlayerHealth health = movement.GetComponent<PlayerHealth>();
                if (health == null || (!health.isDead && !health.isTransforming))
                {
                    // Let Spawned/Render initialize the replacement avatar before
                    // any peer snapshots its renderers for the cinematic clone.
                    yield return null;
                    yield return null;
                    RPC_PlayMilitaryIntroCinematic(requester, spawnPosition);
                    yield break;
                }
            }
            yield return null;
        }

        IsMilitaryIntroCinematicActive = false;
        MilitaryPhase = (int)Phase.Failed;
        Debug.LogError("[MILITARY RETRY] Avatar mới không sẵn sàng sau 5 giây; giữ màn hình Chơi lại để thử lại.");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ResetSoloRetryPresentation()
    {
        cinematicController?.StopImmediate();
        escapePresentation?.StopImmediate();
        hordeDirector?.StopSiege();
        vehicleRepair?.SetVehicleReadyPresentation(false);
        roadsideRepairVehicle?.SetCinematicAlarm(false);
        roadsideRepairVehicle?.SetRepairEntryLocked(true);
        roadsideRepairStation?.StopTimedRepairAudio();
        VehicleRepairSkillCheckUI.NotifyInterrupted(string.Empty);
        EscapeRouteDecisionUI.CloseIfOpen();
        gateController?.RefreshPresentation();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowSchoolClueDialogue([RpcTarget] PlayerRef targetPlayer, int clueIndex,
        NetworkBool firstTeamDiscovery, int collected, int required, NetworkBool grantsFinalMapFragment)
    {
        if (Runner == null || Runner.LocalPlayer != targetPlayer) return;
        string line = clueIndex switch
        {
            0 => GameLocalization.Get("quest.military.clue_dialogue_0"),
            1 => GameLocalization.Get("quest.military.clue_dialogue_1"),
            2 => GameLocalization.Get("quest.military.clue_dialogue_2"),
            _ => GameLocalization.Get("quest.military.clue_dialogue_none")
        };
        RouteBRadioBroadcastUI.ShowSelfDialogue(line);
        if (firstTeamDiscovery && collected < required)
        {
            AutoChatManager.Instance?.AddMessage(GameLocalization.Get("quest.military.clues_sender"),
                string.Format(GameLocalization.Get("quest.military.clues_progress"), collected, required));
        }
        if (grantsFinalMapFragment)
        {
            QuestFlowUIPrototype.Instance?.RegisterFinalMapFragmentForLocalPlayer();
            AutoChatManager.Instance?.AddMessage(GameLocalization.Get("quest.military.new_clue_title"),
                GameLocalization.Get("quest.military.new_clue_body"));
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowSchoolExitBlocked([RpcTarget] PlayerRef targetPlayer, int collected, int required)
    {
        if (Runner == null || Runner.LocalPlayer != targetPlayer) return;
        AutoChatManager.Instance?.AddMessage(GameLocalization.Get("quest.sender"),
            string.Format(GameLocalization.Get("quest.military.school_exit_blocked"), collected, required));
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowPoliceCarObjective(PlayerRef focusPlayer)
    {
        if (Runner == null || Runner.LocalPlayer != focusPlayer) return;
        AutoChatManager.Instance?.AddMessage(GameLocalization.Get("quest.sender"),
            GameLocalization.Get("quest.military.police_car_objective"));
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OpenMilitaryRouteVote(int voteId, int requiredCount) =>
        MilitaryRouteVoteUI.Show(this, voteId, 0, requiredCount);

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_UpdateMilitaryRouteVote(int voteId, int approvedCount, int requiredCount) =>
        MilitaryRouteVoteUI.UpdateProgress(voteId, approvedCount, requiredCount);

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_CloseMilitaryRouteVote(int voteId, string message)
    {
        MilitaryRouteVoteUI.Close(voteId);
        _ = message;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayMilitaryIntroCinematic(PlayerRef hostPlayer, Vector2 stagedStartPosition) =>
        cinematicController?.Play(hostPlayer, stagedStartPosition);

    /// <summary>
    /// Developer presentation path used by F10/CheatMenu. It never creates
    /// LootContainers; it only advances the already replicated base state.
    /// The point-of-no-return confirmation is intentionally preserved.
    /// </summary>
    public void DebugAdvanceMilitaryRoute()
    {
        if (!IsNetworkReady || !HasStateAuthority)
        {
            Debug.LogWarning("[QUEST TEST] NEXT BASE STEP chỉ dùng được trên Solo/Host đang có authority.");
            return;
        }

        PlayerRef requester = Runner.LocalPlayer;
        switch (CurrentPhase)
        {
            case Phase.NotReached:
                if (MainQuestManager.Instance == null ||
                    MainQuestManager.Instance.CurrentStage != MainQuestManager.QuestStage.CityMapFound)
                {
                    Debug.LogWarning("[QUEST TEST] Hãy hoàn tất nửa đầu Tuyến B bằng F6/F7 trước.");
                    return;
                }
                SchoolClueMask = MilitaryStoryFlowRules.CompleteClueMask;
                HasExitedSchoolAfterClues = true;
                MilitaryPhase = (int)Phase.Investigating;
                RPC_ShowPoliceCarObjective(requester);
                Debug.Log("[QUEST TEST] F10: mô phỏng đủ 3 manh mối và đã rời trường; mở mục tiêu xe cảnh sát.");
                break;

            case Phase.Investigating:
                RouteBRadioBroadcastUI.ShowCue(RouteBAudioCueId.AlarmPointOfNoReturn,
                    () => EscapeRouteDecisionUI.ShowFinaleConfirmation(
                        EscapeEndingRoute.MilitaryEvacuation, DebugConfirmMilitaryFinale));
                Debug.Log("[QUEST TEST] F10: mở xác nhận điểm không thể quay lại; ending chỉ khóa khi bấm XÁC NHẬN.");
                break;

            case Phase.SiegeAndRepair:
                ClearRepairSkillCheckSession();
                PoliceEngineRepairProgress = VehicleRepairSkillCheckRules.MaxProgress;
                PoliceHoodRepairProgress = VehicleRepairSkillCheckRules.MaxProgress;
                PoliceFuelRepairProgress = VehicleRepairSkillCheckRules.MaxProgress;
                PoliceBatteryRepairProgress = VehicleRepairSkillCheckRules.MaxProgress;
                PoliceTireRepairProgress = VehicleRepairSkillCheckRules.MaxProgress;
                PoliceCarRepairMask = (int)PoliceCarRepairState.RequiredComplete;
                roadsideRepairVehicle?.SetRepairEntryLocked(false);
                MilitaryPhase = (int)Phase.ReadyToEscape;
                RPC_BroadcastVehicleReady(requester);
                Debug.Log("[QUEST TEST] F10: mô phỏng hoàn tất 5/5 hạng mục Car; xe đã sẵn sàng.");
                break;

            case Phase.ReadyToEscape:
                AuthorityCompleteEscape(requester);
                Debug.Log("[QUEST TEST] F10: bắt đầu extraction Ending B.");
                break;

            case Phase.Escaped:
                Debug.Log("[QUEST TEST] Tuyến B đã hoàn thành.");
                break;

            default:
                Debug.LogWarning("[QUEST TEST] Tuyến B đang Failed; cần bắt đầu session mới để chạy lại đầy đủ.");
                break;
        }
    }

    public void DebugTeleportToCurrentObjective()
    {
        if (!IsNetworkReady || !HasStateAuthority ||
            !TryGetRequestingPlayer(Runner.LocalPlayer, out PlayerMovement player))
        {
            Debug.LogWarning("[QUEST TEST] F12 căn cứ cần Solo/Host và player local đã spawn.");
            return;
        }

        InteractionKind target;
        string targetKey;
        switch (CurrentPhase)
        {
            case Phase.NotReached:
                target = InteractionKind.School;
                targetKey = "quest.debug.target_school";
                break;
            case Phase.Investigating:
                target = InteractionKind.Vehicle;
                targetKey = "quest.debug.target_inspect_car";
                break;
            case Phase.SiegeAndRepair:
                target = InteractionKind.Vehicle;
                targetKey = "quest.debug.target_repair_car";
                break;
            case Phase.ReadyToEscape:
                target = InteractionKind.Vehicle;
                targetKey = "quest.debug.target_regroup_car";
                break;
            default:
                Debug.LogWarning("[QUEST TEST] Nhiệm vụ căn cứ không còn mục tiêu dịch chuyển hợp lệ.");
                return;
        }

        Vector2 destination = GetInteractionPosition(target) + new Vector2(0f, -0.45f);
        PlayerInteraction interaction = player.GetComponent<PlayerInteraction>();
        if (interaction != null && interaction.IsInVehicle)
        {
            VehicleControllerFusion vehicle = interaction.CurrentVehicleController;
            bool exited = vehicle != null && vehicle.AuthorityTryExit(player.Object);
            if (!exited)
                interaction.SetVehicleNetworkState(null, false, false, 0, destination);
        }
        TeleportPlayer(player, destination);
        Physics2D.SyncTransforms();
        string targetLabel = GameLocalization.Get(targetKey);
        Debug.Log($"[QUEST TEST] F12: đã dịch chuyển tới {targetLabel}.");
        AutoChatManager.Instance?.AddMessage(GameLocalization.Get("quest.test_sender"),
            string.Format(GameLocalization.Get("quest.debug.teleported_to"), targetLabel));
    }

    private void DebugConfirmMilitaryFinale()
    {
        if (!IsNetworkReady || !HasStateAuthority || CurrentPhase != Phase.Investigating) return;
        BeginMilitaryRouteVote(Runner.LocalPlayer);
        if (IsMilitaryRouteVoteActive)
            ServerSubmitMilitaryRouteVote(Runner.LocalPlayer, MilitaryRouteVoteId, true);
    }

    public void RequestTriggerAlarm()
    {
        if (!IsNetworkReady) return;
        if (HasStateAuthority) ServerTriggerAlarm(Runner.LocalPlayer);
        else RPC_RequestTriggerAlarm();
    }

    public void RequestActivateGenerator()
    {
        if (!IsNetworkReady) return;
        if (HasStateAuthority) ServerActivateGenerator(Runner.LocalPlayer);
        else RPC_RequestActivateGenerator();
    }

    public void RequestUnlockArmory()
    {
        if (!IsNetworkReady) return;
        if (HasStateAuthority) ServerUnlockArmory(Runner.LocalPlayer);
        else RPC_RequestUnlockArmory();
    }

    public void RequestClaimOfficeSafe()
    {
        if (!IsNetworkReady) return;
        if (HasStateAuthority) ServerClaimOfficeSafe(Runner.LocalPlayer);
        else RPC_RequestClaimOfficeSafe();
    }

    public void RequestCollectPart(MilitaryQuestItemKind kind)
    {
        if (!IsNetworkReady) return;
        if (HasStateAuthority) ServerCollectPart(Runner.LocalPlayer, kind);
        else RPC_RequestCollectPart((int)kind);
    }

    public void RequestInstallPart(MilitaryQuestItemKind kind)
    {
        if (!IsNetworkReady) return;
        if (HasStateAuthority) ServerInstallPart(Runner.LocalPlayer, kind);
        else RPC_RequestInstallPart((int)kind);
    }

    public void RequestProgressRepair(float deltaSeconds)
    {
        if (!IsNetworkReady) return;
        if (HasStateAuthority) ServerProgressRepair(Runner.LocalPlayer, deltaSeconds);
        else RPC_RequestProgressRepair(deltaSeconds);
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.F9)) EditorGrantMissingPoliceCarRepairItems();
#endif
    }

    public void RequestStartPoliceCarRepair(string partId)
    {
        if (!IsNetworkReady || !PoliceCarRepairRules.TryGetAction(partId, out PoliceCarRepairAction action)) return;
        if (HasStateAuthority) ServerStartRepairSkillCheck(Runner.LocalPlayer, action);
        else RPC_RequestStartPoliceCarRepair((int)action);
    }

    public float GetPoliceRepairProgress(PoliceCarRepairAction action) => action switch
    {
        PoliceCarRepairAction.RepairEngine => PoliceEngineRepairProgress,
        PoliceCarRepairAction.RepairHood => PoliceHoodRepairProgress,
        PoliceCarRepairAction.AddFuel => PoliceFuelRepairProgress,
        PoliceCarRepairAction.ReplaceBattery => PoliceBatteryRepairProgress,
        PoliceCarRepairAction.ReplaceTire => PoliceTireRepairProgress,
        _ => 0f
    };

    public void RequestCancelRepairSkillCheck()
    {
        if (!IsNetworkReady) return;
        if (HasStateAuthority) ServerCancelRepairSkillCheck(Runner.LocalPlayer);
        else RPC_RequestCancelRepairSkillCheck();
    }

    public void RequestResolveRepairSkillCheck(int sequence, float needleAngle)
    {
        if (!IsNetworkReady) return;
        if (HasStateAuthority) ServerResolveRepairSkillCheck(Runner.LocalPlayer, sequence, needleAngle);
        else RPC_RequestResolveRepairSkillCheck(sequence, needleAngle);
    }

    public void RequestEscape()
    {
        if (!IsNetworkReady) return;
        if (HasStateAuthority) ServerEscape(Runner.LocalPlayer);
        else RPC_RequestEscape();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestTriggerAlarm(RpcInfo info = default) => ServerTriggerAlarm(info.Source);

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestActivateGenerator(RpcInfo info = default) => ServerActivateGenerator(info.Source);

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestUnlockArmory(RpcInfo info = default) => ServerUnlockArmory(info.Source);

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestClaimOfficeSafe(RpcInfo info = default) => ServerClaimOfficeSafe(info.Source);

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestCollectPart(int kind, RpcInfo info = default)
    {
        if (kind < (int)MilitaryQuestItemKind.Battery || kind > (int)MilitaryQuestItemKind.RepairKit) return;
        ServerCollectPart(info.Source, (MilitaryQuestItemKind)kind);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestInstallPart(int kind, RpcInfo info = default)
    {
        if (kind < (int)MilitaryQuestItemKind.Battery || kind > (int)MilitaryQuestItemKind.RepairKit) return;
        ServerInstallPart(info.Source, (MilitaryQuestItemKind)kind);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestProgressRepair(float deltaSeconds, RpcInfo info = default) =>
        ServerProgressRepair(info.Source, deltaSeconds);

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestStartPoliceCarRepair(int action, RpcInfo info = default)
    {
        if (action < (int)PoliceCarRepairAction.RepairEngine || action > (int)PoliceCarRepairAction.ReplaceTire)
            return;
        ServerStartRepairSkillCheck(info.Source, (PoliceCarRepairAction)action);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestCancelRepairSkillCheck(RpcInfo info = default) =>
        ServerCancelRepairSkillCheck(info.Source);

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestResolveRepairSkillCheck(int sequence, float needleAngle, RpcInfo info = default) =>
        ServerResolveRepairSkillCheck(info.Source, sequence, needleAngle);

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestEscape(RpcInfo info = default) => ServerEscape(info.Source);

    private void ServerTriggerAlarm(PlayerRef requester)
    {
        ServerInspectPoliceCarStory(requester);
    }

    private void AuthorityStartSiege(PlayerRef requester)
    {
        BeginMilitaryRouteVote(requester);
    }

    private void ServerActivateGenerator(PlayerRef requester)
    {
        _ = requester;
        // Generator/electric-gate behavior belonged to the discarded prototype
        // and is deliberately unavailable in the canonical military flow.
    }

    private void AuthorityActivateGenerator(PlayerRef requester)
    {
        _ = requester;
    }

    private void ServerUnlockArmory(PlayerRef requester)
    {
        if (!HasStateAuthority || CurrentPhase < Phase.SiegeAndRepair || IsArmoryUnlocked) return;
        if (!TryGetRequestingPlayer(requester, out PlayerMovement player) ||
            !IsNear(player.transform.position, InteractionKind.Armory)) return;
        InventorySystem inventory = player.GetComponent<InventorySystem>();
        ItemData key = MilitaryQuestItemCatalog.GetOrCreate(MilitaryQuestItemKind.ArmoryKey);
        if (inventory == null || inventory.GetItemCount(key) < 1)
        {
            RPC_ShowLocalizedQuestMessage("quest.military_armory_locked", 0, requester, false);
            return;
        }

        inventory.ConsumeItem(key, 1);
        IsArmoryUnlocked = true;
        GrantItem(inventory, "AK47", 1);
        GrantItem(inventory, "S12K", 1);
        GrantItem(inventory, "Ammo762", 120);
        GrantItem(inventory, "Ammo12Gauge", 60);
        RPC_ShowLocalizedQuestMessage("quest.military_armory_open", 0, requester, false);
    }

    private void ServerClaimOfficeSafe(PlayerRef requester)
    {
        if (!HasStateAuthority || IsOfficeSafeClaimed) return;
        if (MainQuestManager.Instance == null || !MainQuestManager.Instance.IsCityMapUnlocked) return;
        if (!TryGetRequestingPlayer(requester, out PlayerMovement player) ||
            !IsNear(player.transform.position, InteractionKind.OfficeSafe)) return;
        InventorySystem inventory = player.GetComponent<InventorySystem>();
        if (inventory == null) return;

        inventory.AddItem(MilitaryQuestItemCatalog.GetOrCreate(MilitaryQuestItemKind.ArmoryKey), 1);
        GrantItem(inventory, "S12K", 1);
        GrantItem(inventory, "Ammo12Gauge", 24);
        IsOfficeSafeClaimed = true;
        RPC_ShowLocalizedQuestMessage("quest.military_safe_open", 0, requester, false);
    }

    private void ServerCollectPart(PlayerRef requester, MilitaryQuestItemKind kind)
    {
        if (!HasStateAuthority || CurrentPhase != Phase.SiegeAndRepair || IsPartCacheClaimed(kind)) return;
        if (!TryGetRequestingPlayer(requester, out PlayerMovement player)) return;
        InteractionKind interaction = GetCacheInteraction(kind);
        if (!IsNear(player.transform.position, interaction)) return;
        InventorySystem inventory = player.GetComponent<InventorySystem>();
        if (inventory == null || !inventory.AddItem(MilitaryQuestItemCatalog.GetOrCreate(kind), 1)) return;

        SetPartCacheClaimed(kind);
        RPC_ShowLocalizedQuestMessage("quest.military_collected", (int)kind, requester, false);
    }

    private void ServerInstallPart(PlayerRef requester, MilitaryQuestItemKind kind)
    {
        if (!HasStateAuthority || CurrentPhase != Phase.SiegeAndRepair || IsPartInstalled(kind)) return;
        if (!TryGetRequestingPlayer(requester, out PlayerMovement player) ||
            !IsNear(player.transform.position, InteractionKind.Vehicle)) return;
        InventorySystem inventory = player.GetComponent<InventorySystem>();
        ItemData item = MilitaryQuestItemCatalog.GetOrCreate(kind);
        if (inventory == null) return;
        if (inventory.GetItemCount(item) >= 1) inventory.ConsumeItem(item, 1);
        else if (!TryConsumeRecoveryPool(kind)) return;
        SetPartInstalled(kind);
        RPC_ShowLocalizedQuestMessage("quest.military_installed", (int)kind, requester, false);
    }

    private void ServerProgressRepair(PlayerRef requester, float deltaSeconds)
    {
        if (!HasStateAuthority || CurrentPhase != Phase.SiegeAndRepair || !HasAllParts) return;
        if (!TryGetRequestingPlayer(requester, out PlayerMovement player) ||
            !IsNear(player.transform.position, InteractionKind.Vehicle)) return;
        PlayerHealth health = player.GetComponent<PlayerHealth>();
        if (health != null && (health.isDead || health.isTransforming)) return;

        ActiveRepairer = requester;
        float trustedDelta = Mathf.Clamp(deltaSeconds, 0f, 0.2f);
        VehicleRepairProgress = MilitaryQuestRules.ApplyRepairProgress(VehicleRepairProgress,
            trustedDelta, repairDurationSeconds);
        if (VehicleRepairProgress < MilitaryQuestRules.MaxRepairProgress) return;

        PlayerRef completedBy = requester;
        VehicleRepairProgress = MilitaryQuestRules.MaxRepairProgress;
        ActiveRepairer = PlayerRef.None;
        MilitaryPhase = (int)Phase.ReadyToEscape;
        RPC_BroadcastVehicleReady(completedBy);
    }

    private void ServerStartRepairSkillCheck(PlayerRef requester, PoliceCarRepairAction action)
    {
        if (!HasStateAuthority || !enablePoliceCarRepairGameplay || requester == PlayerRef.None ||
            CurrentPhase != Phase.SiegeAndRepair) return;
        EnsurePoliceCarRepairGameplay();
        if (roadsideRepairStation == null ||
            !TryGetRequestingPlayer(requester, out PlayerMovement player) ||
            !roadsideRepairStation.IsPlayerInRepairPosition(player.transform.position))
        {
            SendRepairSessionResponse(requester, action, false, "quest.military.repair_stand_front");
            return;
        }

        PlayerHealth health = player.GetComponent<PlayerHealth>();
        if (health != null && (health.isDead || health.isTransforming))
        {
            SendRepairSessionResponse(requester, action, false, "quest.military.repair_state_invalid");
            return;
        }

        if (PoliceCarRepairRules.IsApplied(PoliceCarRepairMask, action))
        {
            SendRepairSessionResponse(requester, action, false, "quest.military.repair_already_complete");
            return;
        }

        if (RepairSkillCheckSessionActive && ActiveRepairer != PlayerRef.None && ActiveRepairer != requester)
        {
            SendRepairSessionResponse(requester, action, false, "quest.military.repair_in_progress_by",
                GetPlayerDisplayName(ActiveRepairer));
            return;
        }

        if (RepairSkillCheckSessionActive && ActiveRepairer == requester &&
            ActivePoliceRepairAction != (int)action)
        {
            SendRepairSessionResponse(requester, action, false, "quest.military.repair_busy_other");
            return;
        }

        InventorySystem inventory = player.GetComponent<InventorySystem>();
        ArrivalCarItemKind requiredKind = PoliceCarRepairRules.GetRequiredItem(action);
        if (FindPoliceCarItem(inventory, requiredKind) == null)
        {
            SendRepairSessionResponse(requester, action, false, "quest.military.repair_item_required",
                PoliceCarItemCatalog.GetDisplayName(requiredKind));
            return;
        }

        ActiveRepairer = requester;
        ActivePoliceRepairAction = (int)action;
        bool timedInteraction = PoliceCarRepairRules.UsesTimedArrivalCarInteraction(action);
        RepairSkillCheckProgress = timedInteraction ? 0f : GetPoliceRepairProgress(action);
        RepairSkillCheckSessionActive = true;
        RepairSkillCheckEventActive = false;
        RepairSkillCheckElapsed = 0f;
        RepairPenaltyRemaining = 0f;
        NextRepairSkillCheckSeconds = timedInteraction ? 0f : RandomSkillCheckInterval();
        if (timedInteraction)
            RPC_PlayPoliceTimedRepairAudio((int)action,
                PoliceCarRepairRules.GetTimedInteractionDurationSeconds(action));
        SendRepairSessionResponse(requester, action, true, string.Empty);
    }

    private void SendRepairSessionResponse(PlayerRef requester, PoliceCarRepairAction action,
        bool accepted, string messageKey, string messageArg = "")
    {
        bool timed = PoliceCarRepairRules.UsesTimedArrivalCarInteraction(action);
        float duration = timed ? PoliceCarRepairRules.GetTimedInteractionDurationSeconds(action) : 0f;
        RPC_RepairSessionResponse(requester, (int)action, accepted, timed, duration, messageKey ?? string.Empty, messageArg ?? string.Empty);
    }

    private void ServerCancelRepairSkillCheck(PlayerRef requester)
    {
        if (!HasStateAuthority || !RepairSkillCheckSessionActive || ActiveRepairer != requester) return;
        PoliceCarRepairAction action = (PoliceCarRepairAction)ActivePoliceRepairAction;
        bool timed = PoliceCarRepairRules.UsesTimedArrivalCarInteraction(action);
        ClearRepairSkillCheckSession();
        if (timed) RPC_StopPoliceTimedRepairAudio();
        RPC_RepairCancelled(requester, (int)action, timed);
    }

    private void ServerResolveRepairSkillCheck(PlayerRef requester, int sequence, float needleAngle)
    {
        if (!HasStateAuthority || !RepairSkillCheckSessionActive || !RepairSkillCheckEventActive ||
            ActiveRepairer != requester || sequence != RepairSkillCheckSequence ||
            float.IsNaN(needleAngle) || float.IsInfinity(needleAngle)) return;

        VehicleRepairSkillCheckResult result = VehicleRepairSkillCheckRules.Evaluate(
            Mathf.Repeat(needleAngle, 360f), RepairSkillCheckTargetAngle,
            skillCheckSuccessArcDegrees, skillCheckPerfectArcDegrees);
        AuthorityApplyRepairSkillCheckResult(result);
    }

    private void TickRepairSkillCheck()
    {
        if (!RepairSkillCheckSessionActive || ActiveRepairer == PlayerRef.None) return;
        if (!TryGetRequestingPlayer(ActiveRepairer, out PlayerMovement player))
        {
            AuthorityInterruptRepair(ActiveRepairer, "quest.military.repair_interrupted_left");
            return;
        }

        // Movement is also frozen authoritatively so stale/queued client input
        // cannot slide the active repairer while the local minigame owns input.
        player.LockMovement(Mathf.Max(0.2f, Runner.DeltaTime * 2f));

        PlayerHealth health = player.GetComponent<PlayerHealth>();
        if ((health != null && (health.isDead || health.isTransforming)) || roadsideRepairStation == null ||
            !roadsideRepairStation.IsPlayerInRepairPosition(player.transform.position))
        {
            AuthorityInterruptRepair(ActiveRepairer, "quest.military.repair_interrupted_generic");
            return;
        }

        float delta = Runner.DeltaTime;
        PoliceCarRepairAction activeAction = (PoliceCarRepairAction)ActivePoliceRepairAction;
        bool timedInteraction = PoliceCarRepairRules.UsesTimedArrivalCarInteraction(activeAction);
        float activeDuration = timedInteraction
            ? PoliceCarRepairRules.GetTimedInteractionDurationSeconds(activeAction)
            : skillRepairDurationSeconds;
        if (RepairPenaltyRemaining > 0f)
            RepairPenaltyRemaining = Mathf.Max(0f, RepairPenaltyRemaining - delta);
        else
            RepairSkillCheckProgress = VehicleRepairSkillCheckRules.AdvanceBaseProgress(
                RepairSkillCheckProgress, delta, activeDuration);
        StoreActivePoliceRepairProgress();

        if (RepairSkillCheckProgress >= VehicleRepairSkillCheckRules.MaxProgress)
        {
            PlayerRef completedBy = ActiveRepairer;
            PoliceCarRepairAction completedAction = (PoliceCarRepairAction)ActivePoliceRepairAction;
            RepairSkillCheckProgress = VehicleRepairSkillCheckRules.MaxProgress;
            StoreActivePoliceRepairProgress();
            if (!TryConsumePoliceRepairItem(player, completedAction))
            {
                AuthorityInterruptRepair(completedBy, "quest.military.repair_interrupted_item_missing");
                return;
            }
            PoliceCarRepairMask |= (int)PoliceCarRepairRules.GetStateBit(completedAction);
            bool allComplete = PoliceCarRepairRules.IsComplete(PoliceCarRepairMask);
            ClearRepairSkillCheckSession();
            if (timedInteraction) RPC_StopPoliceTimedRepairAudio();
            RPC_RepairCompleted(completedBy, (int)completedAction,
                allComplete);
            if (allComplete)
            {
                roadsideRepairVehicle?.SetRepairEntryLocked(false);
                MilitaryPhase = (int)Phase.ReadyToEscape;
                RPC_BroadcastVehicleReady(completedBy);
            }
            return;
        }

        if (timedInteraction)
        {
            RepairSkillCheckEventActive = false;
            RepairSkillCheckElapsed = 0f;
            NextRepairSkillCheckSeconds = 0f;
            return;
        }


        // The final five percent are a clean completion runway. Cancel any check
        // that reaches the cutoff and never create another one near 100%.
        if (!VehicleRepairSkillCheckRules.CanRunSkillCheck(
                RepairSkillCheckProgress, skillCheckFinaleCutoffProgress))
        {
            RepairSkillCheckEventActive = false;
            RepairSkillCheckElapsed = 0f;
            NextRepairSkillCheckSeconds = 0f;
            return;
        }

        if (RepairSkillCheckEventActive)
        {
            RepairSkillCheckElapsed += delta;
            if (RepairSkillCheckElapsed >= skillCheckRotationSeconds)
                AuthorityApplyRepairSkillCheckResult(VehicleRepairSkillCheckResult.Miss);
            return;
        }

        NextRepairSkillCheckSeconds -= delta;
        if (NextRepairSkillCheckSeconds > 0f) return;

        float projectedProgressAtTimeout = VehicleRepairSkillCheckRules.AdvanceBaseProgress(
            RepairSkillCheckProgress, skillCheckRotationSeconds, skillRepairDurationSeconds);
        if (!VehicleRepairSkillCheckRules.CanRunSkillCheck(
                projectedProgressAtTimeout, skillCheckFinaleCutoffProgress))
            return;

        RepairSkillCheckSequence++;
        float minimumTargetAngle = VehicleRepairSkillCheckRules.GetMinimumTargetCenterAngle(
            skillCheckMinimumTravelFraction, skillCheckSuccessArcDegrees);
        float maximumTargetAngle = 360f - skillCheckSuccessArcDegrees * 0.5f;
        RepairSkillCheckTargetAngle = Random.Range(minimumTargetAngle, Mathf.Max(minimumTargetAngle, maximumTargetAngle));
        RepairSkillCheckElapsed = 0f;
        RepairSkillCheckEventActive = true;
    }

    private void AuthorityApplyRepairSkillCheckResult(VehicleRepairSkillCheckResult result)
    {
        if (!HasStateAuthority || !RepairSkillCheckSessionActive) return;
        PlayerRef repairer = ActiveRepairer;
        RepairSkillCheckProgress = VehicleRepairSkillCheckRules.ApplyResult(RepairSkillCheckProgress, result,
            skillCheckSuccessBonus, skillCheckPerfectBonus, skillCheckMissPenalty);
        StoreActivePoliceRepairProgress();
        RepairSkillCheckEventActive = false;
        RepairSkillCheckElapsed = 0f;
        NextRepairSkillCheckSeconds = RandomSkillCheckInterval();
        RepairPenaltyRemaining = result == VehicleRepairSkillCheckResult.Miss
            ? skillCheckMissPauseSeconds
            : 0f;

        // Failure noise is intentionally disabled for the roadside gameplay test.
        // The serialized switch is retained so military-base integration can enable it later.
        if (emitRepairFailureNoise && result == VehicleRepairSkillCheckResult.Miss)
            Debug.Log("[VehicleRepair] Failure-noise hook is armed but suppressed during roadside testing.");

        RPC_RepairSkillCheckOutcome(repairer, (int)result);
    }

    private void AuthorityInterruptRepair(PlayerRef player, string message)
    {
        if (!HasStateAuthority || player == PlayerRef.None || ActiveRepairer != player) return;
        PoliceCarRepairAction action = (PoliceCarRepairAction)ActivePoliceRepairAction;
        bool timed = PoliceCarRepairRules.UsesTimedArrivalCarInteraction(action);
        ClearRepairSkillCheckSession();
        if (timed) RPC_StopPoliceTimedRepairAudio();
        RPC_InterruptRepair(player);
        RPC_RepairInterrupted(player, (int)action, timed, message);
    }

    private void ClearRepairSkillCheckSession()
    {
        StoreActivePoliceRepairProgress();
        RepairSkillCheckSessionActive = false;
        RepairSkillCheckEventActive = false;
        RepairSkillCheckElapsed = 0f;
        RepairPenaltyRemaining = 0f;
        NextRepairSkillCheckSeconds = 0f;
        ActiveRepairer = PlayerRef.None;
        ActivePoliceRepairAction = -1;
    }

    private void StoreActivePoliceRepairProgress()
    {
        if (ActivePoliceRepairAction < (int)PoliceCarRepairAction.RepairEngine ||
            ActivePoliceRepairAction > (int)PoliceCarRepairAction.ReplaceTire) return;
        float progress = Mathf.Clamp(RepairSkillCheckProgress, 0f, VehicleRepairSkillCheckRules.MaxProgress);
        switch ((PoliceCarRepairAction)ActivePoliceRepairAction)
        {
            case PoliceCarRepairAction.RepairEngine: PoliceEngineRepairProgress = progress; break;
            case PoliceCarRepairAction.RepairHood: PoliceHoodRepairProgress = progress; break;
            case PoliceCarRepairAction.AddFuel: PoliceFuelRepairProgress = progress; break;
            case PoliceCarRepairAction.ReplaceBattery: PoliceBatteryRepairProgress = progress; break;
            case PoliceCarRepairAction.ReplaceTire: PoliceTireRepairProgress = progress; break;
        }
    }

    private static bool TryConsumePoliceRepairItem(PlayerMovement player, PoliceCarRepairAction action)
    {
        InventorySystem inventory = player != null ? player.GetComponent<InventorySystem>() : null;
        ArrivalCarItemKind kind = PoliceCarRepairRules.GetRequiredItem(action);
        ItemData item = FindPoliceCarItem(inventory, kind);
        return item != null && inventory.ConsumeItem(item, 1) == 1;
    }

    private float RandomSkillCheckInterval()
    {
        float min = Mathf.Max(0.1f, skillCheckIntervalMinSeconds);
        float max = Mathf.Max(min, skillCheckIntervalMaxSeconds);
        return Random.Range(min, max);
    }

    private void ServerEscape(PlayerRef requester)
    {
        _ = requester;
        // Production extraction is intentionally driven by the authoritative
        // driver's W input and the authored EndB route, never by a nearby E press.
    }

    private void AuthorityCompleteEscape(PlayerRef requester)
    {
        if (!HasStateAuthority || CurrentPhase != Phase.ReadyToEscape ||
            MainQuestManager.Instance == null ||
            MainQuestManager.Instance.LockedEscapeRoute != EscapeEndingRoute.MilitaryEvacuation)
            return;
        IsEscapeVehicleEngineStarted = true;
        IsEscapeVehicleDriveUnlocked = true;
        EscapeVehicleStartupRemaining = 0f;
        IsEscapeOutroActive = true;
        ResolveEscapeRouteAnchors();
        if (roadsideRepairVehicle != null && escapeFinalTrigger != null)
            roadsideRepairVehicle.AuthorityBeginMilitaryOutroDrive(
                escapeFinalTrigger.transform.position, EscapeVehicleOutroTargetPosition);
        AuthorityPrepareOutsidePlayersForMilitaryOutro();
        MilitaryPhase = (int)Phase.Escaped;
        ActiveRepairer = PlayerRef.None;
        RPC_TriggerVictoryCutscene(requester);
    }

    public void TakeGateDamage(float damage)
    {
        if (!HasStateAuthority ||
            (CurrentPhase != Phase.SiegeAndRepair && CurrentPhase != Phase.ReadyToEscape) ||
            GateCurrentHealth <= 0f) return;
        if (IsSoloSiege) return;
        GateCurrentHealth = MilitaryQuestRules.ApplyGateDamage(GateCurrentHealth, damage);
        if (GateCurrentHealth <= 0f) RPC_GateBroken();
    }

    /// <summary>
    /// Solo begins its difficulty-scaled deterministic gate countdown only when a
    /// zombie visibly reaches and attacks the gate for the first time.
    /// </summary>
    public bool TryStartSoloGateDps()
    {
        if (!HasStateAuthority || !IsSoloSiege ||
            (CurrentPhase != Phase.SiegeAndRepair && CurrentPhase != Phase.ReadyToEscape) || IsGateBroken)
            return false;
        if (!IsSoloGateDpsActive)
        {
            IsSoloGateDpsActive = true;
            SoloGateDpsElapsed = 0f;
            float holdSeconds = MilitaryQuestRules.GetSoloGateHoldSeconds(GetSelectedDifficulty());
            Debug.Log($"[MILITARY GATE] Zombie đã chạm cổng: bắt đầu DPS Solo {holdSeconds / 60f:0} phút.");
        }
        return true;
    }

    /// <summary>Host-only developer action. A broken gate is intentionally not resurrected.</summary>
    public bool DebugHealGate()
    {
        if (!HasStateAuthority || CurrentPhase != Phase.SiegeAndRepair || IsGateBroken) return false;
        GateCurrentHealth = GateMaxHealth;
        if (IsSoloSiege)
        {
            SoloGateDpsElapsed = 0f;
        }
        Debug.Log("[MILITARY GATE] Cheat healed the intact gate to full health and reset its Solo timer.");
        return true;
    }

    public void NotifyPlayerDamaged(PlayerRef player, bool zombieAttack)
    {
        if (!HasStateAuthority || player == PlayerRef.None || ActiveRepairer != player) return;
        if (!MilitaryStoryFlowRules.ShouldInterruptVehicleRepair(zombieAttack)) return;
        if (RepairSkillCheckSessionActive)
        {
            AuthorityInterruptRepair(player, "quest.military.repair_interrupted_zombie");
            return;
        }

        ActiveRepairer = PlayerRef.None;
        RPC_InterruptRepair(player);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_StartSiegePresentation()
    {
        // Close the physical/A* gate before any existing or newly spawned
        // zombie receives its siege objective on this peer.
        gateController?.RefreshPresentation();
        hordeDirector?.BeginSiege();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_BroadcastVehicleReady(PlayerRef focusPlayer)
    {
        roadsideRepairVehicle?.SetCinematicAlarm(false);
        vehicleRepair?.SetVehicleReadyPresentation(true);
        if (Runner != null && Runner.LocalPlayer == focusPlayer)
            RouteBRadioBroadcastUI.ShowCue(RouteBAudioCueId.EscapeVehicleReady);
        AutoChatManager.Instance?.AddMessage(
            GameLocalization.Get("quest.sender"),
            GameLocalization.Get("quest.military_vehicle_ready"));
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowEscapeStartDenied(PlayerRef driver, int missingPlayers)
    {
        if (Runner == null || Runner.LocalPlayer != driver) return;
        string message = missingPlayers == 1
            ? GameLocalization.Get("quest.military.escape_start_denied_single")
            : string.Format(GameLocalization.Get("quest.military.escape_start_denied_multiple"), missingPlayers);
        AutoChatManager.Instance?.AddMessage(GameLocalization.Get("quest.military.escape_sender"), message);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_EscapeVehicleStarting(PlayerRef driver, float startupSeconds)
    {
        if (Runner == null || Runner.LocalPlayer != driver) return;
        _ = startupSeconds;
        AutoChatManager.Instance?.AddMessage(GameLocalization.Get("quest.military.escape_sender"),
            GameLocalization.Get("quest.military.escape_starting_driver"));
        escapePresentation?.RefreshPresentation();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_EscapeVehicleDriveUnlocked(PlayerRef driver)
    {
        if (Runner == null || Runner.LocalPlayer != driver) return;
        AutoChatManager.Instance?.AddMessage(GameLocalization.Get("quest.military.escape_sender"),
            GameLocalization.Get("quest.military.escape_unlocked"));
        escapePresentation?.RefreshPresentation();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_TriggerVictoryCutscene(PlayerRef focusPlayer)
    {
        EscapeRouteDecisionUI.CloseIfOpen();
        QuestFlowUIPrototype questJournal = FindFirstObjectByType<QuestFlowUIPrototype>(
            FindObjectsInactive.Include);
        questJournal?.ApplyMilitarySnapshot((int)Phase.Escaped, false, true, 100f,
            GateCurrentHealth, GateMaxHealth, true);
        if (Runner != null && Runner.LocalPlayer == focusPlayer)
            RouteBRadioBroadcastUI.ShowCue(RouteBAudioCueId.MilitaryEvacuationComplete);
        hordeDirector?.StopSiege();
        if (escapePresentation != null)
            escapePresentation.PlayOutro(() => VictorySummaryUI.ShowForCurrentMatch(
                SurvivalSeconds, EscapeEndingRoute.MilitaryEvacuation));
        else
            VictorySummaryUI.ShowForCurrentMatch(SurvivalSeconds, EscapeEndingRoute.MilitaryEvacuation);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_GateBroken()
    {
        gateController?.BreakGate();
        hordeDirector?.ReleaseHordeToPlayers();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_InterruptRepair(PlayerRef player)
    {
        vehicleRepair?.InterruptRepairFor(player);
        if (Runner != null && Runner.LocalPlayer == player)
            AutoChatManager.Instance?.AddMessage(GameLocalization.Get("quest.sender"),
                GameLocalization.Get("quest.military.repair_interrupted_damage"));
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_RepairSessionResponse(PlayerRef target, int action, NetworkBool accepted,
        NetworkBool timedInteraction, float duration, string messageKey, string messageArg = "")
    {
        if (Runner == null || Runner.LocalPlayer != target) return;
        string message = string.Empty;
        if (!string.IsNullOrEmpty(messageKey))
        {
            string format = GameLocalization.Get(messageKey, messageKey);
            if (!string.IsNullOrEmpty(messageArg))
            {
                string localizedArg = GameLocalization.TranslateLiteral(messageArg);
                message = string.Format(format, localizedArg);
            }
            else
            {
                message = format;
            }
        }
        if (timedInteraction)
            roadsideRepairStation?.NotifyTimedRepairStart((PoliceCarRepairAction)action, accepted, duration, message);
        else
            VehicleRepairSkillCheckUI.NotifyStartResponse(accepted, message);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_RepairSkillCheckOutcome(PlayerRef target, int result)
    {
        if (Runner == null || Runner.LocalPlayer != target) return;
        if (result < (int)VehicleRepairSkillCheckResult.Miss ||
            result > (int)VehicleRepairSkillCheckResult.Perfect) return;
        VehicleRepairSkillCheckUI.NotifyOutcome((VehicleRepairSkillCheckResult)result);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_RepairCancelled(PlayerRef target, int action, NetworkBool timedInteraction)
    {
        if (Runner == null || Runner.LocalPlayer != target) return;
        if (timedInteraction)
            roadsideRepairStation?.NotifyTimedRepairInterrupted(GameLocalization.Get("quest.military.repair_stopped"));
        else
            VehicleRepairSkillCheckUI.NotifyCancelled();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_RepairInterrupted(PlayerRef target, int action, NetworkBool timedInteraction, string messageKey)
    {
        if (Runner == null || Runner.LocalPlayer != target) return;
        string message = string.IsNullOrEmpty(messageKey) ? string.Empty : GameLocalization.Get(messageKey, messageKey);
        if (timedInteraction)
            roadsideRepairStation?.NotifyTimedRepairInterrupted(message);
        else
            VehicleRepairSkillCheckUI.NotifyInterrupted(message);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_RepairCompleted(PlayerRef target, int action, NetworkBool allComplete)
    {
        if (Runner != null && Runner.LocalPlayer == target)
        {
            PoliceCarRepairAction completedAction = (PoliceCarRepairAction)action;
            if (PoliceCarRepairRules.UsesTimedArrivalCarInteraction(completedAction))
                roadsideRepairStation?.NotifyTimedRepairCompleted(allComplete);
            else
                VehicleRepairSkillCheckUI.NotifyCompleted(completedAction, allComplete);
        }
        if (allComplete || (Runner != null && Runner.LocalPlayer == target))
        {
            AutoChatManager.Instance?.AddMessage(GameLocalization.Get("quest.sender"), allComplete
                ? GameLocalization.Get("quest.military.repair_complete_all")
                : GameLocalization.Get("quest.military.repair_complete_single"));
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayPoliceTimedRepairAudio(int action, float duration) =>
        roadsideRepairStation?.PlayTimedRepairAudio((PoliceCarRepairAction)action, duration);

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_StopPoliceTimedRepairAudio() => roadsideRepairStation?.StopTimedRepairAudio();

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowQuestMessage(string message) =>
        AutoChatManager.Instance?.AddMessage(GameLocalization.Get("quest.sender"), message);

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowLocalizedQuestMessage(string localizationKey, int itemKind,
        PlayerRef focusPlayer, NetworkBool serverWide)
    {
        if (!serverWide &&
            (focusPlayer == PlayerRef.None || Runner == null || Runner.LocalPlayer != focusPlayer))
            return;

        string message = GameLocalization.Get(localizationKey, localizationKey);
        if (localizationKey == "quest.military_collected" || localizationKey == "quest.military_installed")
        {
            if (itemKind >= (int)MilitaryQuestItemKind.ArmoryKey &&
                itemKind <= (int)MilitaryQuestItemKind.LevelThreeBackpack)
                message = string.Format(message,
                    MilitaryQuestItemCatalog.GetLocalizedDisplayName((MilitaryQuestItemKind)itemKind));
        }
        AutoChatManager.Instance?.AddMessage(GameLocalization.Get("quest.sender"), message);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowRouteBAudioCue(int cueId, PlayerRef focusPlayer)
    {
        if (Runner != null && Runner.LocalPlayer == focusPlayer)
            RouteBRadioBroadcastUI.ShowCue((RouteBAudioCueId)cueId);
    }

    private void BuildPresentation()
    {
        if (presentationRoot != null) return;
        presentationRoot = new GameObject("Military Base Quest Presentation");
        presentationRoot.transform.SetParent(transform, true);

        gateController = MilitaryGateController.Create(presentationRoot.transform,
            GetInteractionPosition(InteractionKind.Gate), this);
        hordeDirector = presentationRoot.AddComponent<SiegeHordeDirector>();
        hordeDirector.Configure(this, gateController);
        repairLootCoordinator = presentationRoot.AddComponent<MilitaryRepairLootCoordinator>();
        repairLootCoordinator.Configure(this);
        cinematicController = presentationRoot.AddComponent<MilitaryRouteCinematicController>();
        cinematicController.Configure(this);
        escapePresentation = presentationRoot.AddComponent<MilitaryRouteBEscapePresentation>();
        escapePresentation.Configure(this);
        BuildSchoolInvestigationPresentation();
    }

    private void BuildSchoolInvestigationPresentation()
    {
        GameObject roofObject = GameObject.Find("__SchoolRoofTrigger_FIXED");
        schoolRoofTrigger = roofObject != null ? roofObject.GetComponent<PolygonCollider2D>() : null;
        if (schoolRoofTrigger == null)
        {
            Debug.LogError("[MILITARY STORY] Không tìm thấy PolygonCollider2D __SchoolRoofTrigger_FIXED.");
            return;
        }

        MilitarySchoolRoofExitTrigger exitTrigger = roofObject.GetComponent<MilitarySchoolRoofExitTrigger>();
        if (exitTrigger == null) exitTrigger = roofObject.AddComponent<MilitarySchoolRoofExitTrigger>();
        exitTrigger.Configure(this);

        schoolCluePoints.Clear();
        string[] labels = { "quest.military.clue_label_0", "quest.military.clue_label_1", "quest.military.clue_label_2" };
        for (int i = 0; i < MilitaryStoryFlowRules.RequiredSchoolClues; i++)
        {
            GameObject clue = GameObject.Find($"ManhMoi{i + 1}");
            if (clue == null)
            {
                Debug.LogError($"[MILITARY STORY] Không tìm thấy object scene ManhMoi{i + 1}.");
                schoolCluePoints.Add(null);
                continue;
            }
            MilitarySchoolCluePoint point = clue.GetComponent<MilitarySchoolCluePoint>();
            if (point == null) point = clue.AddComponent<MilitarySchoolCluePoint>();
            point.Configure(this, i, labels[i]);
            schoolCluePoints.Add(point);
        }
    }

    /// <summary>
    /// Attaches the canonical five-action repair loop to the authored scene
    /// object named Car. The vehicle stays exactly where the scene places it.
    /// </summary>
    private void EnsurePoliceCarRepairGameplay()
    {
        if (!enablePoliceCarRepairGameplay) return;

        if (roadsideRepairStation == null)
        {
            VehicleControllerFusion[] vehicles = FindObjectsByType<VehicleControllerFusion>(FindObjectsSortMode.None);
            for (int i = 0; i < vehicles.Length; i++)
            {
                VehicleControllerFusion candidate = vehicles[i];
                if (candidate == null || candidate.gameObject.name != "Car") continue;
                roadsideRepairVehicle = candidate;
                break;
            }
            if (roadsideRepairVehicle == null) return;

            roadsideRepairStation = roadsideRepairVehicle.GetComponent<RoadsideVehicleRepairStation>();
            if (roadsideRepairStation == null)
                roadsideRepairStation = roadsideRepairVehicle.gameObject.AddComponent<RoadsideVehicleRepairStation>();
            roadsideRepairStation.Configure(this, roadsideRepairVehicle);
        }

        if (!HasStateAuthority || roadsideVehiclePrepared) return;
        roadsideRepairVehicle.AuthorityPrepareRepairAtCurrentPosition();
        roadsideVehiclePrepared = true;
    }

#if UNITY_EDITOR
    private void EditorGrantMissingPoliceCarRepairItems()
    {
        if (!IsNetworkReady || !HasStateAuthority)
        {
            Debug.LogWarning("[EDITOR TEST] F9 chỉ cấp vật phẩm xe cảnh sát khi đang Play ở Solo/Host.");
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
            ArrivalCarItemKind.Toolbox, ArrivalCarItemKind.Hammer, ArrivalCarItemKind.FuelCan,
            ArrivalCarItemKind.Battery, ArrivalCarItemKind.Tire
        };
        int addedCount = 0;
        List<string> failedItems = new List<string>();
        for (int i = 0; i < requiredItems.Length; i++)
        {
            ArrivalCarItemKind kind = requiredItems[i];
            if (FindPoliceCarItem(inventory, kind) != null) continue;
            ItemData item = PoliceCarItemCatalog.GetOrCreate(kind);
            if (item != null && inventory.AddItem(item, 1)) addedCount++;
            else failedItems.Add(GameLocalization.TranslateLiteral(PoliceCarItemCatalog.GetDisplayName(kind)));
        }

        string message = failedItems.Count == 0
            ? string.Format(GameLocalization.Get("quest.debug.f9_granted"), addedCount)
            : string.Format(GameLocalization.Get("quest.debug.f9_failed"), string.Join(", ", failedItems));
        Debug.Log("[EDITOR TEST] " + message);
        AutoChatManager.Instance?.AddMessage(GameLocalization.Get("quest.editor_test_sender"), message);
    }
#endif

    private void CreatePoint(InteractionKind kind, string label, Color color)
    {
        GameObject point = new GameObject(label);
        point.transform.SetParent(presentationRoot.transform, true);
        point.transform.position = GetInteractionPosition(kind);
        MilitaryQuestInteractionPoint interaction = point.AddComponent<MilitaryQuestInteractionPoint>();
        interaction.Configure(this, kind, label, color);
    }

    private void ResolveAnchor()
    {
        ResolveMilitaryAreaTrigger();
        if (militaryBaseAnchor == null)
        {
            if (militaryAreaTrigger != null)
                militaryBaseAnchor = militaryAreaTrigger.transform;
            else
            {
                GameObject found = GameObject.Find("KhuVucQuanSu");
                if (found != null) militaryBaseAnchor = found.transform;
            }
        }
        if (policeCarMarker == null)
        {
            GameObject found = GameObject.Find("SpawnXeCanhSat");
            if (found != null) policeCarMarker = found.transform;
        }
        if (gateClosingMarker == null)
        {
            GameObject found = GameObject.Find("ViTriDongCong");
            if (found != null) gateClosingMarker = found.transform;
        }
        if (schoolTeleportMarker == null)
        {
            GameObject found = GameObject.Find("TeleportToSchool");
            if (found != null) schoolTeleportMarker = found.transform;
        }
        ResolveEscapeRouteAnchors();
    }

    private void ResolveEscapeRouteAnchors()
    {
        for (int i = 0; i < escapeWaypoints.Length; i++)
        {
            if (escapeWaypoints[i] != null) continue;
            GameObject found = GameObject.Find($"EndB{i + 1}");
            if (found != null) escapeWaypoints[i] = found.transform;
        }

        if (escapeFinalTrigger == null)
        {
            GameObject found = GameObject.Find("EndBFinal");
            if (found != null) escapeFinalTrigger = found.GetComponent<PolygonCollider2D>();
        }

        if (escapeVehicleOutroTarget == null)
        {
            GameObject found = GameObject.Find("EndBFinal2");
            if (found != null) escapeVehicleOutroTarget = found.transform;
        }

        if (escapeCameraTarget == null)
        {
            GameObject found = GameObject.Find("EndBToCinemachine");
            if (found != null) escapeCameraTarget = found.transform;
        }
    }

    private void ResolveMilitaryAreaTrigger()
    {
        if (militaryAreaTrigger != null) return;

        PolygonCollider2D[] polygons = FindObjectsByType<PolygonCollider2D>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < polygons.Length; i++)
        {
            PolygonCollider2D candidate = polygons[i];
            if (candidate != null && candidate.name == "KhuVucQuanSu")
            {
                militaryAreaTrigger = candidate;
                return;
            }
        }

        Debug.LogWarning("[MILITARY CINEMATIC] Không tìm thấy PolygonCollider2D trên KhuVucQuanSu; " +
                         "không thể xác định chính xác vùng dọn zombie.");
    }

    private Vector2 GetCinematicStartPosition()
    {
        ResolveMilitaryAreaTrigger();
        Vector2 car = PoliceCarPosition;
        Vector2[] candidates =
        {
            car + new Vector2(-3.5f, 1.5f),
            car + new Vector2(-3f, -1.5f),
            car + new Vector2(3f, 1.5f),
            car + new Vector2(3f, -1.5f)
        };

        if (militaryAreaTrigger != null)
        {
            for (int i = 0; i < candidates.Length; i++)
                if (militaryAreaTrigger.OverlapPoint(candidates[i]))
                    return candidates[i];

            Vector2 center = militaryAreaTrigger.bounds.center;
            if (militaryAreaTrigger.OverlapPoint(center)) return center;

            Vector2 closest = militaryAreaTrigger.ClosestPoint(car);
            Vector2 inward = ((Vector2)militaryAreaTrigger.bounds.center - closest).normalized;
            Vector2 inside = closest + inward * 0.5f;
            if (militaryAreaTrigger.OverlapPoint(inside)) return inside;
        }

        // Scene chưa có vùng hợp lệ: vẫn bắt đầu gần xe để cinematic không kéo diễn viên từ ngoài đường vào.
        return candidates[0];
    }

    private int AuthorityClearZombiesInsideMilitaryArea()
    {
        if (!HasStateAuthority) return 0;
        ResolveMilitaryAreaTrigger();
        if (militaryAreaTrigger == null) return 0;

        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        HashSet<NetworkObject> handled = new HashSet<NetworkObject>();
        int removed = 0;
        for (int i = 0; i < enemies.Length; i++)
        {
            GameObject enemy = enemies[i];
            if (enemy == null) continue;
            NetworkObject networkObject = enemy.GetComponentInParent<NetworkObject>();
            GameObject zombieRoot = networkObject != null ? networkObject.gameObject : enemy;
            if (!IsMilitaryZombie(zombieRoot) ||
                zombieRoot.GetComponentInParent<PlayerMovement>() != null ||
                !militaryAreaTrigger.OverlapPoint(zombieRoot.transform.position))
                continue;

            if (networkObject != null)
            {
                if (!networkObject.IsValid || !networkObject.HasStateAuthority || !handled.Add(networkObject))
                    continue;
                Runner.Despawn(networkObject);
            }
            else
            {
                Destroy(zombieRoot);
            }
            removed++;
        }
        return removed;
    }

    private static bool IsMilitaryZombie(GameObject target)
    {
        return target != null &&
               (target.GetComponent<ZombieAI>() != null ||
                target.GetComponent<ZombieHealth>() != null ||
                target.GetComponent<ZOmbieAI_Khoa>() != null ||
                target.GetComponent<ZombieAIKhoaRebuilt>() != null);
    }

    private Vector2 GetOfficeSafePosition()
    {
        MainQuestStartTrigger office = FindFirstObjectByType<MainQuestStartTrigger>();
        return office != null ? (Vector2)office.transform.position + new Vector2(0f, 1.2f) : new Vector2(-45.96f, 23.14f);
    }

    private bool IsNear(Vector3 playerPosition, InteractionKind kind) =>
        Vector2.Distance(playerPosition, GetInteractionPosition(kind)) <= interactionDistance;

    private bool AnyLivingPlayerNear(Vector2 point, float distance)
    {
        return TryFindLivingPlayerNear(point, distance, out _);
    }

    private bool TryFindLivingPlayerNear(Vector2 point, float distance, out PlayerRef playerRef)
    {
        playerRef = PlayerRef.None;
        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth health = players[i] != null ? players[i].GetComponent<PlayerHealth>() : null;
            if (players[i] != null && (health == null || (!health.isDead && !health.isTransforming)) &&
                players[i].Object != null && players[i].Object.IsValid &&
                Vector2.Distance(players[i].transform.position, point) <= distance)
            {
                playerRef = players[i].Object.InputAuthority;
                return playerRef != PlayerRef.None;
            }
        }
        return false;
    }

    private void LockAllLivingPlayersForCinematic()
    {
        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerMovement movement = players[i];
            if (movement == null || movement.Object == null || !movement.Object.IsValid ||
                !movement.Object.HasStateAuthority) continue;
            PlayerHealth health = movement.GetComponent<PlayerHealth>();
            if (health != null && (health.isDead || health.isTransforming)) continue;
            movement.LockMovement(Mathf.Max(0.2f, Runner.DeltaTime * 2f));
        }
    }

    private void GatherLivingPlayersNearClosedGate()
    {
        Vector2 gatherCenter = GateClosingPosition + new Vector2(0f, 1.15f);
        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        int gathered = 0;
        for (int i = 0; i < players.Length; i++)
        {
            PlayerMovement movement = players[i];
            if (movement == null || movement.Object == null || !movement.Object.IsValid ||
                !movement.Object.HasStateAuthority) continue;
            PlayerHealth health = movement.GetComponent<PlayerHealth>();
            if (health != null && (health.isDead || health.isTransforming)) continue;

            float angle = gathered * 137.50776f * Mathf.Deg2Rad;
            float radius = gathered == 0 ? 0f : cinematicGatherSpacing * (1f + gathered / 7f);
            Vector2 destination = gatherCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            // Positive Y is the school/base side of the authored gate marker.
            if (destination.y <= GateClosingPosition.y + 0.25f)
                destination.y = GateClosingPosition.y + 0.45f;

            PlayerInteraction interaction = movement.GetComponent<PlayerInteraction>();
            if (interaction != null && interaction.IsInVehicle)
            {
                VehicleControllerFusion currentVehicle = interaction.CurrentVehicleController;
                bool exitedNormally = currentVehicle != null && currentVehicle.AuthorityTryExit(movement.Object);
                if (!exitedNormally)
                    interaction.SetVehicleNetworkState(null, false, false, 0, destination);
            }
            TeleportPlayer(movement, destination);
            gathered++;
        }
        Physics2D.SyncTransforms();
    }

    private void GatherLivingPlayersForExtraction()
    {
        Vector2 vehiclePosition = GetInteractionPosition(InteractionKind.Vehicle);
        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        int gathered = 0;
        for (int i = 0; i < players.Length; i++)
        {
            PlayerMovement movement = players[i];
            if (movement == null || movement.Object == null || !movement.Object.IsValid ||
                !movement.Object.HasStateAuthority) continue;

            PlayerHealth health = movement.GetComponent<PlayerHealth>();
            if (health != null && (health.isDead || health.isTransforming)) continue;

            float angle = gathered * 137.50776f * Mathf.Deg2Rad;
            float radius = gathered == 0 ? 0.35f : 0.65f + 0.15f * (gathered / 6);
            Vector2 destination = vehiclePosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

            PlayerInteraction interaction = movement.GetComponent<PlayerInteraction>();
            if (interaction != null && interaction.IsInVehicle)
            {
                VehicleControllerFusion currentVehicle = interaction.CurrentVehicleController;
                bool exitedNormally = currentVehicle != null && currentVehicle.AuthorityTryExit(movement.Object);
                if (!exitedNormally)
                    interaction.SetVehicleNetworkState(null, false, false, 0, destination);
            }

            TeleportPlayer(movement, destination);
            gathered++;
        }

        Physics2D.SyncTransforms();
    }

    private static void TeleportPlayer(PlayerMovement movement, Vector2 destination)
    {
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

    private bool AnyLivingPlayer()
    {
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
            if (players[i] != null && !players[i].isDead && !players[i].isTransforming) return true;
        return false;
    }

    public bool IsPoliceEscapeVehicle(VehicleControllerFusion vehicle) =>
        vehicle != null && roadsideRepairVehicle != null && vehicle == roadsideRepairVehicle &&
        (CurrentPhase == Phase.ReadyToEscape || CurrentPhase == Phase.Escaped);

    public bool RequiresEscapeVehicleStartSequence(VehicleControllerFusion vehicle) =>
        IsPoliceEscapeVehicle(vehicle) && CurrentPhase == Phase.ReadyToEscape;

    public bool TryGetEscapeGuidanceWaypoint(int index, out Vector2 position, out Vector2 direction)
    {
        ResolveEscapeRouteAnchors();
        position = default;
        direction = Vector2.up;
        if (index < 0 || index >= EscapeGuidanceWaypointCount) return false;

        Transform current = index < escapeWaypoints.Length
            ? escapeWaypoints[index]
            : escapeFinalTrigger != null ? escapeFinalTrigger.transform : null;
        if (current == null) return false;
        position = current.position;

        Transform next = index + 1 < escapeWaypoints.Length
            ? escapeWaypoints[index + 1]
            : index < escapeWaypoints.Length && escapeFinalTrigger != null
                ? escapeFinalTrigger.transform
                : null;
        Vector2 rawDirection = next != null
            ? (Vector2)next.position - position
            : index > 0 && escapeWaypoints[escapeWaypoints.Length - 1] != null
                ? position - (Vector2)escapeWaypoints[escapeWaypoints.Length - 1].position
                : Vector2.up;
        if (rawDirection.sqrMagnitude > 0.001f) direction = rawDirection.normalized;
        return true;
    }

    private void ResetEscapeVehicleState()
    {
        AuthorityClearMilitaryOutroProtection();
        IsEscapeVehicleEngineStarted = false;
        IsEscapeVehicleDriveUnlocked = false;
        EscapeVehicleStartupRemaining = 0f;
        EscapeWaypointIndex = 0;
        IsEscapeOutroActive = false;
        nextEscapeStartDeniedAt = 0f;
    }

    private void TickEscapeVehicleFlow()
    {
        if (CurrentPhase != Phase.ReadyToEscape || roadsideRepairVehicle == null || IsEscapeOutroActive) return;
        NetworkObject driver = roadsideRepairVehicle.Driver;

        if (!IsEscapeVehicleEngineStarted)
        {
            PlayerMovement driverMovement = driver != null ? driver.GetComponent<PlayerMovement>() : null;
            if (driverMovement == null || driverMovement.NetMoveInput.y < 0.25f) return;

            if (!AreAllLivingPlayersReadyForPoliceEscape(out int missingPlayers))
            {
                if (Time.time >= nextEscapeStartDeniedAt)
                {
                    nextEscapeStartDeniedAt = Time.time + 1.15f;
                    RPC_ShowEscapeStartDenied(driver.InputAuthority, missingPlayers);
                }
                return;
            }

            IsEscapeVehicleEngineStarted = true;
            IsEscapeVehicleDriveUnlocked = false;
            EscapeVehicleStartupRemaining = Mathf.Max(0.05f,
                roadsideRepairVehicle.EngineStarterDurationSeconds);
            EscapeWaypointIndex = 0;
            roadsideRepairVehicle.AuthorityStartEngine();
            if (IsSoloSiege && !IsGateBroken) TryStartSoloGateDps();
            RPC_EscapeVehicleStarting(driver.InputAuthority, EscapeVehicleStartupRemaining);
            return;
        }

        if (!IsEscapeVehicleDriveUnlocked)
        {
            EscapeVehicleStartupRemaining = Mathf.Max(0f,
                EscapeVehicleStartupRemaining - Runner.DeltaTime);
            if (EscapeVehicleStartupRemaining <= 0f)
            {
                IsEscapeVehicleDriveUnlocked = true;
                RPC_EscapeVehicleDriveUnlocked(driver.InputAuthority);
            }
        }

        TickAcceleratedGateDrain();
        if (!IsEscapeVehicleDriveUnlocked) return;

        while (EscapeWaypointIndex < escapeWaypoints.Length)
        {
            Transform waypoint = escapeWaypoints[EscapeWaypointIndex];
            if (waypoint == null || Vector2.Distance(roadsideRepairVehicle.transform.position,
                    waypoint.position) > escapeWaypointReachRadius)
                break;
            EscapeWaypointIndex++;
        }

        if (EscapeWaypointIndex < escapeWaypoints.Length || escapeFinalTrigger == null ||
            !escapeFinalTrigger.OverlapPoint(roadsideRepairVehicle.transform.position)) return;

        PlayerRef focusPlayer = driver != null ? driver.InputAuthority : Runner.LocalPlayer;
        AuthorityCompleteEscape(focusPlayer);
    }

    private bool AreAllLivingPlayersReadyForPoliceEscape(out int missingPlayers)
    {
        missingPlayers = 0;
        if (Runner == null || roadsideRepairVehicle == null) return false;

        foreach (PlayerRef playerRef in Runner.ActivePlayers)
        {
            if (!Runner.TryGetPlayerObject(playerRef, out NetworkObject playerObject) ||
                playerObject == null || !playerObject.IsValid)
            {
                if (!militaryDeathObservedAt.ContainsKey(playerRef)) missingPlayers++;
                continue;
            }

            PlayerHealth health = playerObject.GetComponent<PlayerHealth>();
            if (health != null && (health.isDead || health.isTransforming)) continue;
            PlayerInteraction interaction = playerObject.GetComponent<PlayerInteraction>();
            bool isInAnyVehicle = interaction != null && interaction.IsInVehicle;
            bool isOnPoliceVehicle = isInAnyVehicle &&
                interaction.CurrentVehicleController == roadsideRepairVehicle;
            float distanceToPoliceVehicle = Vector2.Distance(playerObject.transform.position,
                roadsideRepairVehicle.transform.position);
            if (!MilitaryStoryFlowRules.IsPlayerReadyForMilitaryEscape(isOnPoliceVehicle,
                    isInAnyVehicle, distanceToPoliceVehicle))
                missingPlayers++;
        }

        return missingPlayers == 0;
    }

    private void AuthorityPrepareOutsidePlayersForMilitaryOutro()
    {
        if (!HasStateAuthority || Runner == null || roadsideRepairVehicle == null) return;

        int followerIndex = 0;
        foreach (PlayerRef playerRef in Runner.ActivePlayers)
        {
            if (!Runner.TryGetPlayerObject(playerRef, out NetworkObject playerObject) ||
                playerObject == null || !playerObject.IsValid) continue;

            PlayerHealth health = playerObject.GetComponent<PlayerHealth>();
            if (health == null || health.isDead || health.isTransforming) continue;
            PlayerInteraction interaction = playerObject.GetComponent<PlayerInteraction>();
            bool isPoliceOccupant = interaction != null && interaction.IsInVehicle &&
                interaction.CurrentVehicleController == roadsideRepairVehicle;
            if (isPoliceOccupant)
            {
                health.AuthoritySetMilitaryOutroProtected(false);
                continue;
            }

            if (interaction != null && interaction.IsInVehicle)
            {
                VehicleControllerFusion currentVehicle = interaction.CurrentVehicleController;
                bool exitedNormally = currentVehicle != null && currentVehicle.AuthorityTryExit(playerObject);
                if (!exitedNormally)
                    interaction.SetVehicleNetworkState(null, false, false, 0,
                        roadsideRepairVehicle.transform.position);
            }

            health.AuthoritySetMilitaryOutroProtected(true);
            PlayerMovement movement = playerObject.GetComponent<PlayerMovement>();
            if (movement != null)
            {
                movement.LockMovement(0.5f);
                TeleportPlayer(movement, GetMilitaryOutroFollowerPosition(followerIndex));
            }
            followerIndex++;
        }
        Physics2D.SyncTransforms();
    }

    private void TickMilitaryOutroFollowers()
    {
        if (!HasStateAuthority || Runner == null || roadsideRepairVehicle == null) return;

        int followerIndex = 0;
        foreach (PlayerRef playerRef in Runner.ActivePlayers)
        {
            if (!Runner.TryGetPlayerObject(playerRef, out NetworkObject playerObject) ||
                playerObject == null || !playerObject.IsValid) continue;
            PlayerHealth health = playerObject.GetComponent<PlayerHealth>();
            if (health == null || !health.IsMilitaryOutroProtected || health.isDead || health.isTransforming)
                continue;

            PlayerMovement movement = playerObject.GetComponent<PlayerMovement>();
            if (movement != null)
            {
                movement.LockMovement(Mathf.Max(0.2f, Runner.DeltaTime * 2f));
                TeleportPlayer(movement, GetMilitaryOutroFollowerPosition(followerIndex));
            }
            followerIndex++;
        }
    }

    private Vector2 GetMilitaryOutroFollowerPosition(int followerIndex)
    {
        Vector2 forward = roadsideRepairVehicle != null
            ? roadsideRepairVehicle.VisionDirection.normalized
            : Vector2.up;
        if (forward.sqrMagnitude < 0.001f) forward = Vector2.up;
        Vector2 right = new Vector2(forward.y, -forward.x);
        int row = followerIndex / 2;
        float side = followerIndex % 2 == 0 ? -1f : 1f;
        float lateral = 0.75f + row * 0.22f;
        float trailing = 0.8f + row * 0.48f;
        return (Vector2)roadsideRepairVehicle.transform.position - forward * trailing + right * lateral * side;
    }

    private void AuthorityClearMilitaryOutroProtection()
    {
        if (!HasStateAuthority) return;
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
            if (players[i] != null && players[i].Object != null && players[i].Object.IsValid &&
                players[i].Object.HasStateAuthority)
                players[i].AuthoritySetMilitaryOutroProtected(false);
    }

    private void TickAcceleratedGateDrain()
    {
        if (!IsEscapeVehicleEngineStarted || IsGateBroken || IsSoloSiege) return;
        float damagePerSecond = MilitaryStoryFlowRules.GetEscapeGateDamagePerSecond(
            GateMaxHealth, acceleratedGateDrainSeconds);
        GateCurrentHealth = MilitaryQuestRules.ApplyGateDamage(GateCurrentHealth,
            damagePerSecond * Runner.DeltaTime);
        if (GateCurrentHealth <= 0f) RPC_GateBroken();
    }

    private void TickSoloGateDps()
    {
        if (!IsSoloGateDpsActive || !IsSoloSiege ||
            (CurrentPhase != Phase.SiegeAndRepair && CurrentPhase != Phase.ReadyToEscape) || IsGateBroken)
            return;

        int difficulty = GetSelectedDifficulty();
        float holdSeconds = MilitaryQuestRules.GetSoloGateHoldSeconds(difficulty);
        float elapsedRate = MilitaryStoryFlowRules.GetSoloGateElapsedRate(
            IsEscapeVehicleEngineStarted, holdSeconds, acceleratedGateDrainSeconds);
        SoloGateDpsElapsed = Mathf.Min(holdSeconds,
            SoloGateDpsElapsed + Runner.DeltaTime * elapsedRate);
        GateCurrentHealth = MilitaryQuestRules.GetSoloGateHealthAtElapsedForDifficulty(GateMaxHealth, SoloGateDpsElapsed,
            difficulty);
        if (GateCurrentHealth <= 0f) RPC_GateBroken();
    }

    private static int GetSelectedDifficulty() => DifficultyRules.ActiveDifficulty;

    public int CountActivePlayers()
    {
        if (!IsNetworkReady || Runner == null) return 1;
        int count = 0;
        foreach (PlayerRef _ in Runner.ActivePlayers) count++;
        return Mathf.Max(1, count);
    }

    /// <summary>
    /// Authority-side military respawn: track deaths, wait the canonical delay,
    /// then respawn the player at the team checkpoint while charges remain.
    /// Entries persist through avatar despawn (zombie transformation) and are
    /// pruned only when the player returns alive or leaves the session.
    /// </summary>
    private void TickAuthorityAutoRespawn()
    {
        HashSet<PlayerRef> activeSessionPlayers = new HashSet<PlayerRef>();
        foreach (PlayerRef player in Runner.ActivePlayers) activeSessionPlayers.Add(player);

        PlayerHealth[] avatars = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        float now = Time.time;
        for (int i = 0; i < avatars.Length; i++)
        {
            PlayerHealth health = avatars[i];
            if (health == null || health.Object == null || !health.Object.IsValid) continue;
            PlayerRef owner = health.Object.InputAuthority;
            if (owner == PlayerRef.None) continue;
            if (health.isDead || health.isTransforming)
            {
                if (!militaryDeathObservedAt.ContainsKey(owner))
                {
                    militaryDeathObservedAt[owner] = now;
                    HostModeSpawner.Instance?.CaptureMilitaryRespawnState(owner);
                    RPC_BeginMilitaryRespawnCountdown(owner, MilitaryQuestRules.RespawnDelaySeconds);
                }
            }
            else
            {
                militaryDeathObservedAt.Remove(owner);
            }
        }

        List<PlayerRef> departed = null;
        List<PlayerRef> readyToRespawn = null;
        foreach (KeyValuePair<PlayerRef, float> entry in militaryDeathObservedAt)
        {
            if (!activeSessionPlayers.Contains(entry.Key))
            {
                if (departed == null) departed = new List<PlayerRef>();
                departed.Add(entry.Key);
                continue;
            }
            if (!MilitaryQuestRules.IsRespawnDelayElapsed(now - entry.Value)) continue;
            if (!MilitaryQuestRules.CanUseTeamRespawn(IsSoloSiege, TeamRespawnsRemaining)) continue;

            if (readyToRespawn == null) readyToRespawn = new List<PlayerRef>();
            readyToRespawn.Add(entry.Key);
        }

        if (departed != null)
            for (int i = 0; i < departed.Count; i++) militaryDeathObservedAt.Remove(departed[i]);

        if (readyToRespawn == null) return;
        readyToRespawn.Sort((left, right) => left.PlayerId.CompareTo(right.PlayerId));
        for (int i = 0; i < readyToRespawn.Count; i++)
        {
            PlayerRef player = readyToRespawn[i];
            Vector2 destination = RespawnCheckpointPosition +
                new Vector2(Mathf.Cos(player.PlayerId * 137.5f * Mathf.Deg2Rad),
                    Mathf.Sin(player.PlayerId * 137.5f * Mathf.Deg2Rad)) * 0.65f;
            HostModeSpawner spawner = HostModeSpawner.Instance;
            bool respawned = spawner != null && spawner.AuthorityRespawnAtCheckpoint(player, destination);
            if (!respawned)
            {
                Debug.LogWarning($"[MILITARY RESPAWN] Chưa thể hồi sinh {player}; sẽ thử lại, không trừ lượt.");
                continue;
            }

            TeamRespawnsRemaining = MilitaryQuestRules.ConsumeTeamRespawnCharge(TeamRespawnsRemaining);
            militaryDeathObservedAt.Remove(player);
            Debug.Log($"[MILITARY RESPAWN] {player} hồi sinh tại checkpoint; còn " +
                      $"{TeamRespawnsRemaining} lượt đội. Kết quả: {respawned}.");
            RPC_AnnounceTeamRespawnUsed(TeamRespawnsRemaining);
        }
    }

    private void TickPermanentEliminationRecovery()
    {
        if (!HasStateAuthority || TeamRespawnsRemaining > 0) return;
        PlayerHealth[] avatars = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        for (int i = 0; i < avatars.Length; i++)
        {
            PlayerHealth health = avatars[i];
            if (health == null || health.Object == null || !health.Object.IsValid ||
                (!health.isDead && !health.isTransforming)) continue;
            PlayerRef owner = health.Object.InputAuthority;
            if (owner == PlayerRef.None || recoveredPermanentEliminations.Contains(owner)) continue;
            RecoverEssentialItemsFromEliminatedPlayer(owner, health.GetComponent<InventorySystem>());
            recoveredPermanentEliminations.Add(owner);
        }

        TryAssignRecoveryPoolToLivingSurvivors();
    }

    private void RecoverEssentialItemsFromEliminatedPlayer(PlayerRef owner, InventorySystem source)
    {
        if (source == null) return;
        MilitaryQuestItemKind[] kinds =
        {
            MilitaryQuestItemKind.Battery,
            MilitaryQuestItemKind.FuelCanister,
            MilitaryQuestItemKind.RepairKit
        };
        for (int i = 0; i < kinds.Length; i++)
        {
            MilitaryQuestItemKind kind = kinds[i];
            if (IsPartInstalled(kind)) continue;
            ItemData item = MilitaryQuestItemCatalog.GetOrCreate(kind);
            int amount = source.GetItemCount(item);
            if (amount <= 0) continue;
            int removed = source.ConsumeItem(item, amount);
            if (removed <= 0) continue;
            AddToRecoveryPool(kind, removed);
            Debug.Log($"[MILITARY RECOVERY] Authority recovered {removed}x {item.name} from {owner}.");
        }
    }

    private void TryAssignRecoveryPoolToLivingSurvivors()
    {
        MilitaryQuestItemKind[] kinds =
        {
            MilitaryQuestItemKind.Battery,
            MilitaryQuestItemKind.FuelCanister,
            MilitaryQuestItemKind.RepairKit
        };
        for (int i = 0; i < kinds.Length; i++)
        {
            MilitaryQuestItemKind kind = kinds[i];
            while (GetRecoveryPoolCount(kind) > 0)
            {
                ItemData item = MilitaryQuestItemCatalog.GetOrCreate(kind);
                InventorySystem recipient = FindDeterministicRecoveryRecipient(item);
                if (recipient == null || !recipient.AddItem(item, 1)) break;
                AddToRecoveryPool(kind, -1);
            }
        }
    }

    private InventorySystem FindDeterministicRecoveryRecipient(ItemData item)
    {
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        InventorySystem selected = null;
        float bestDistance = float.PositiveInfinity;
        int bestPlayerId = int.MaxValue;
        Vector2 objective = GetInteractionPosition(InteractionKind.Vehicle);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth health = players[i];
            if (health == null || health.isDead || health.isTransforming || health.Object == null ||
                !health.Object.IsValid) continue;
            InventorySystem inventory = health.GetComponent<InventorySystem>();
            if (inventory == null || !inventory.CanAcceptItemAmount(item, 1)) continue;
            float distance = Vector2.SqrMagnitude((Vector2)health.transform.position - objective);
            int playerId = health.Object.InputAuthority.PlayerId;
            if (distance > bestDistance || (Mathf.Approximately(distance, bestDistance) && playerId >= bestPlayerId))
                continue;
            selected = inventory;
            bestDistance = distance;
            bestPlayerId = playerId;
        }
        return selected;
    }

    private int GetRecoveryPoolCount(MilitaryQuestItemKind kind) => kind switch
    {
        MilitaryQuestItemKind.Battery => RecoveryBatteryCount,
        MilitaryQuestItemKind.FuelCanister => RecoveryFuelCount,
        MilitaryQuestItemKind.RepairKit => RecoveryRepairKitCount,
        _ => 0
    };

    private void AddToRecoveryPool(MilitaryQuestItemKind kind, int delta)
    {
        if (kind == MilitaryQuestItemKind.Battery) RecoveryBatteryCount = Mathf.Max(0, RecoveryBatteryCount + delta);
        else if (kind == MilitaryQuestItemKind.FuelCanister) RecoveryFuelCount = Mathf.Max(0, RecoveryFuelCount + delta);
        else if (kind == MilitaryQuestItemKind.RepairKit) RecoveryRepairKitCount = Mathf.Max(0, RecoveryRepairKitCount + delta);
    }

    private bool TryConsumeRecoveryPool(MilitaryQuestItemKind kind)
    {
        if (GetRecoveryPoolCount(kind) <= 0) return false;
        AddToRecoveryPool(kind, -1);
        return true;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BeginMilitaryRespawnCountdown(PlayerRef player, float seconds)
    {
        if (Runner != null && Runner.LocalPlayer == player)
            AutoUIManager.Instance?.BeginMilitaryRespawnCountdown(seconds);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_AnnounceTeamRespawnUsed(int remainingCharges)
    {
        string sender = GameLocalization.Get("quest.military.respawn_sender");
        string body = string.Format(GameLocalization.Get("quest.military.respawn_body"), remainingCharges);
        AutoChatManager.Instance?.AddMessage(sender, body);
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

    private static string GetPlayerDisplayName(PlayerRef playerRef)
    {
        string fallback = GameLocalization.Get("player.other");
        if (!TryGetRequestingPlayer(playerRef, out PlayerMovement player)) return fallback;
        PlayerNameTag nameTag = player.GetComponent<PlayerNameTag>();
        string displayName = nameTag != null ? nameTag.PlayerName.ToString() : string.Empty;
        return string.IsNullOrWhiteSpace(displayName) ? fallback : displayName.ToUpperInvariant();
    }

    private static void GrantItem(InventorySystem inventory, string id, int amount)
    {
        ItemData item = ItemDataLoader.LoadItem(id);
        if (item != null) inventory.AddItem(item, amount);
    }

    private static ItemData FindPoliceCarItem(InventorySystem inventory, ArrivalCarItemKind kind)
    {
        if (inventory == null) return null;
        for (int i = 0; i < inventory.slots.Count; i++)
        {
            InventorySlot slot = inventory.slots[i];
            if (slot != null && slot.amount > 0 &&
                PoliceCarItemCatalog.TryGetKind(slot.item, out ArrivalCarItemKind existing) && existing == kind)
                return slot.item;
        }
        return null;
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

    private void OnGUI()
    {
        if (GameplayReadinessCoordinator.IsGameplaySuppressed || GameplayHudLayout.AreGameplayPromptsSuppressed()) return;
        if (!IsNetworkReady || TutorialSession.IsActive || IsMilitaryIntroCinematicActive ||
            MainQuestManager.Instance == null ||
            MainQuestManager.Instance.CurrentStage != MainQuestManager.QuestStage.CityMapFound)
            return;

        if (CurrentPhase == Phase.NotReached)
        {
            GUIStyle clueStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                fontStyle = FontStyle.Bold
            };
            clueStyle.normal.textColor = new Color(1f, 0.84f, 0.3f);
            string objective = HasAllSchoolClues
                ? GameLocalization.Get("quest.military.school_clues_done")
                : string.Format(GameLocalization.Get("quest.military.school_clues_progress"), SchoolClueCount);
            Rect schoolRect = GameplayHudLayout.GetTopCenterSchoolClueRect();
            GUI.Box(schoolRect, objective, clueStyle);
            return;
        }

        if (CurrentPhase == Phase.SiegeAndRepair || CurrentPhase == Phase.ReadyToEscape)
        {
            DrawGateHealthBar();
            return;
        }

        if (CurrentPhase != Phase.Investigating || !HasExitedSchoolAfterClues ||
            MainQuestManager.Instance.LockedEscapeRoute != EscapeEndingRoute.None)
            return;
        DrawPoliceCarWaypoint();
    }

    private void DrawGateHealthBar()
    {
        float ratio = GateMaxHealth > 0f ? Mathf.Clamp01(GateCurrentHealth / GateMaxHealth) : 0f;
        float width = Mathf.Clamp(Screen.width * 0.46f, 520f, 820f);
        const float height = 58f;
        float x = (Screen.width - width) * 0.5f;
        float y = Screen.height - 175f;
        Rect outer = new Rect(x, y, width, height);
        Rect track = new Rect(x + 8f, y + 29f, width - 16f, 19f);
        Rect fill = new Rect(track.x + 2f, track.y + 2f,
            Mathf.Max(0f, (track.width - 4f) * ratio), track.height - 4f);

        Color oldColor = GUI.color;
        int oldDepth = GUI.depth;
        GUI.depth = -1200;
        GUI.color = new Color(0.025f, 0.035f, 0.045f, 0.94f);
        GUI.DrawTexture(outer, Texture2D.whiteTexture);
        GUI.color = new Color(0.5f, 0.58f, 0.62f, 1f);
        GUI.DrawTexture(track, Texture2D.whiteTexture);
        GUI.color = new Color(0.035f, 0.045f, 0.055f, 1f);
        GUI.DrawTexture(new Rect(track.x + 2f, track.y + 2f, track.width - 4f, track.height - 4f),
            Texture2D.whiteTexture);
        GUI.color = Color.Lerp(new Color(0.82f, 0.12f, 0.08f, 1f),
            new Color(0.24f, 0.82f, 0.42f, 1f), ratio);
        GUI.DrawTexture(fill, Texture2D.whiteTexture);

        GUIStyle label = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 16,
            fontStyle = FontStyle.Bold
        };
        label.normal.textColor = Color.white;
        GUI.color = Color.white;
        GUI.Label(new Rect(x + 8f, y + 3f, width - 16f, 25f),
            $"{GameLocalization.Get("quest.military.gate_bar_title")}   {Mathf.CeilToInt(GateCurrentHealth):N0} / {Mathf.CeilToInt(GateMaxHealth):N0}   •   {ratio * 100f:0}%",
            label);
        GUI.depth = oldDepth;
        GUI.color = oldColor;
    }

    private void DrawPoliceCarWaypoint()
    {
        Camera camera = Camera.main;
        if (camera == null || roadsideRepairVehicle == null) return;
        Vector3 screen3 = camera.WorldToScreenPoint(roadsideRepairVehicle.transform.position);
        Vector2 screen = new Vector2(screen3.x, Screen.height - screen3.y);
        float x = Mathf.Clamp(screen.x, 64f, Screen.width - 64f);
        float y = Mathf.Clamp(screen.y, 96f, Screen.height - 72f);
        float pulse = 0.72f + Mathf.Sin(Time.unscaledTime * 4f) * 0.18f;
        GUIStyle style = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 14,
            fontStyle = FontStyle.Bold
        };
        style.normal.textColor = new Color(0.35f, 0.95f, 1f, pulse);
        float distance = PlayerMovement.LocalPlayerInstance != null
            ? Vector2.Distance(PlayerMovement.LocalPlayerInstance.transform.position,
                roadsideRepairVehicle.transform.position)
            : 0f;
        Rect wpRect = new Rect(x - 105f, y - 24f, 210f, 48f);
        wpRect = GameplayHudLayout.ClampWaypointGroupAroundTopCenter(wpRect);
        GUI.Box(wpRect,
            string.Format(GameLocalization.Get("quest.military.police_car_waypoint"), distance), style);
    }

    private bool IsPartInstalled(MilitaryQuestItemKind kind) => kind switch
    {
        MilitaryQuestItemKind.Battery => HasBatteryInstalled,
        MilitaryQuestItemKind.FuelCanister => HasFuelInstalled,
        MilitaryQuestItemKind.RepairKit => HasRepairKitInstalled,
        _ => true
    };

    private void SetPartInstalled(MilitaryQuestItemKind kind)
    {
        if (kind == MilitaryQuestItemKind.Battery) HasBatteryInstalled = true;
        else if (kind == MilitaryQuestItemKind.FuelCanister) HasFuelInstalled = true;
        else if (kind == MilitaryQuestItemKind.RepairKit) HasRepairKitInstalled = true;
    }

    private bool IsPartCacheClaimed(MilitaryQuestItemKind kind) => kind switch
    {
        MilitaryQuestItemKind.Battery => IsBatteryCacheClaimed,
        MilitaryQuestItemKind.FuelCanister => IsFuelCacheClaimed,
        MilitaryQuestItemKind.RepairKit => IsRepairKitCacheClaimed,
        _ => true
    };

    private void SetPartCacheClaimed(MilitaryQuestItemKind kind)
    {
        if (kind == MilitaryQuestItemKind.Battery) IsBatteryCacheClaimed = true;
        else if (kind == MilitaryQuestItemKind.FuelCanister) IsFuelCacheClaimed = true;
        else if (kind == MilitaryQuestItemKind.RepairKit) IsRepairKitCacheClaimed = true;
    }

    private static InteractionKind GetCacheInteraction(MilitaryQuestItemKind kind) => kind switch
    {
        MilitaryQuestItemKind.Battery => InteractionKind.BatteryCache,
        MilitaryQuestItemKind.FuelCanister => InteractionKind.FuelCache,
        _ => InteractionKind.RepairKitCache
    };
}
