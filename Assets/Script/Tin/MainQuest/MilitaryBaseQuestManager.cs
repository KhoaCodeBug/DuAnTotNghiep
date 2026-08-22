using System.Collections.Generic;
using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;

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
        OfficeSafe
    }

    public static MilitaryBaseQuestManager Instance { get; private set; }

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

    [Header("Balance")]
    [SerializeField, Min(1f)] private float baseGateHealth = MilitaryQuestRules.BaseGateHealth;
    [SerializeField, Min(1f)] private float repairDurationSeconds = 12f;

    [Header("Roadside repair gameplay test")]
    [SerializeField] private bool enableRoadsideRepairTest = true;
    [SerializeField, Min(1f)] private float skillRepairDurationSeconds = 45f;
    [SerializeField, Min(0.1f)] private float skillCheckIntervalMinSeconds = 4f;
    [SerializeField, Min(0.1f)] private float skillCheckIntervalMaxSeconds = 7f;
    [SerializeField, Min(0.25f)] private float skillCheckRotationSeconds = 1.25f;
    [SerializeField, Range(1f, 180f)] private float skillCheckSuccessArcDegrees = 25f;
    [SerializeField, Range(1f, 90f)] private float skillCheckPerfectArcDegrees = 8f;
    [SerializeField, Range(0f, 0.8f)] private float skillCheckMinimumTravelFraction = 0.30f;
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

    private bool hasSpawned;
    private GameObject presentationRoot;
    private SiegeHordeDirector hordeDirector;
    private MilitaryGateController gateController;
    private MilitaryEscapeVehicleRepair vehicleRepair;
    private RoadsideVehicleRepairStation roadsideRepairStation;
    private VehicleControllerFusion roadsideRepairVehicle;
    private bool roadsideVehicleRelocated;

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

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(this);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (presentationRoot != null) Destroy(presentationRoot);
        if (roadsideRepairStation != null) Destroy(roadsideRepairStation);
    }

    public override void Spawned()
    {
        hasSpawned = true;
        ResolveAnchor();
        BuildPresentation();
        EnsureRoadsideRepairTest();

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
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        hasSpawned = false;
        if (presentationRoot != null) Destroy(presentationRoot);
        presentationRoot = null;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        TickRepairSkillCheck();
        if (CurrentPhase != Phase.Escaped && CurrentPhase != Phase.Failed)
            SurvivalSeconds += Runner.DeltaTime;

        if (CurrentPhase == Phase.NotReached && MainQuestManager.Instance != null &&
            MainQuestManager.Instance.LockedEscapeRoute != EscapeEndingRoute.CivilianCar &&
            MainQuestManager.Instance.CurrentStage == MainQuestManager.QuestStage.CityMapFound &&
            AnyLivingPlayerNear(GetInteractionPosition(InteractionKind.Vehicle), 7f))
        {
            MilitaryPhase = (int)Phase.Investigating;
            RPC_ShowQuestMessage("Đã tới căn cứ quân sự. Kiểm tra chiếc xe thoát hiểm.");
            RPC_ShowRouteBAudioCue((int)RouteBAudioCueId.MilitaryBaseApproach);
        }

        if ((CurrentPhase == Phase.SiegeAndRepair || CurrentPhase == Phase.ReadyToEscape) && !AnyLivingPlayer())
        {
            MilitaryPhase = (int)Phase.Failed;
            if (RepairSkillCheckSessionActive) AuthorityInterruptRepair(ActiveRepairer,
                "Việc sửa xe đã dừng.");
            else ActiveRepairer = PlayerRef.None;
            RPC_ShowQuestMessage("NHIỆM VỤ THẤT BẠI: Không còn người sống sót tại căn cứ.");
        }
    }

    public override void Render()
    {
        EnsureRoadsideRepairTest();
        gateController?.RefreshPresentation();
        vehicleRepair?.RefreshPresentation();
    }

    public Vector2 GetInteractionPosition(InteractionKind kind)
    {
        ResolveAnchor();
        Vector2 origin = militaryBaseAnchor != null ? militaryBaseAnchor.position : Vector2.zero;
        return kind switch
        {
            InteractionKind.Vehicle => origin + vehicleOffset,
            InteractionKind.Gate => origin + gateOffset,
            InteractionKind.Generator => origin + generatorOffset,
            InteractionKind.Armory => origin + armoryOffset,
            InteractionKind.BatteryCache => origin + batteryOffset,
            InteractionKind.FuelCache => origin + fuelOffset,
            InteractionKind.RepairKitCache => origin + repairKitOffset,
            InteractionKind.ExitPoint => origin + exitOffset,
            InteractionKind.OfficeSafe => GetOfficeSafePosition(),
            _ => origin
        };
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
        if (!HasStateAuthority || (CurrentPhase != Phase.NotReached && CurrentPhase != Phase.Investigating)) return;
        if (!TryGetRequestingPlayer(requester, out PlayerMovement player) ||
            !IsNear(player.transform.position, InteractionKind.Vehicle)) return;
        if (MainQuestManager.Instance == null ||
            MainQuestManager.Instance.CurrentStage != MainQuestManager.QuestStage.CityMapFound) return;
        if (!MainQuestManager.Instance.AuthorityTryLockEscapeRoute(EscapeEndingRoute.MilitaryEvacuation))
        {
            RPC_ShowQuestMessage("Không thể kích hoạt: toàn đội đã khóa ending bằng chiếc xe dân sự.");
            return;
        }

        MilitaryPhase = (int)Phase.SiegeAndRepair;
        GateCurrentHealth = Mathf.Max(GateCurrentHealth, GateMaxHealth);
        ActiveRepairer = PlayerRef.None;
        RPC_StartSiegePresentation();
        RPC_ShowRouteBAudioCue((int)RouteBAudioCueId.SiegeStarted);
        RPC_ShowQuestMessage("BÁO ĐỘNG! Cổng đã đóng. Thu thập 3 phụ tùng và bảo vệ xe thoát hiểm.");
    }

    private void ServerActivateGenerator(PlayerRef requester)
    {
        if (!HasStateAuthority || CurrentPhase != Phase.SiegeAndRepair || IsGeneratorActive) return;
        if (!TryGetRequestingPlayer(requester, out PlayerMovement player) ||
            !IsNear(player.transform.position, InteractionKind.Generator)) return;

        float oldMax = Mathf.Max(1f, GateMaxHealth);
        float ratio = Mathf.Clamp01(GateCurrentHealth / oldMax);
        GateMaxHealth = MilitaryQuestRules.GetElectrifiedGateHealth(baseGateHealth);
        GateCurrentHealth = Mathf.Max(GateCurrentHealth, GateMaxHealth * ratio);
        IsGeneratorActive = true;
        RPC_ShowRouteBAudioCue((int)RouteBAudioCueId.GeneratorOnline);
        RPC_ShowQuestMessage("Máy phát điện đã hoạt động: cổng đạt 150% HP và làm choáng zombie tiếp xúc.");
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
            RPC_ShowQuestMessage("Kho quân nhu bị khóa. Cần chìa khóa lấy từ két sắt văn phòng.");
            return;
        }

        inventory.ConsumeItem(key, 1);
        IsArmoryUnlocked = true;
        GrantItem(inventory, "AK47", 1);
        GrantItem(inventory, "S12K", 1);
        GrantItem(inventory, "Ammo762", 120);
        GrantItem(inventory, "Ammo12Gauge", 60);
        inventory.AddItem(MilitaryQuestItemCatalog.GetOrCreate(MilitaryQuestItemKind.LevelThreeBackpack), 1);
        RPC_ShowQuestMessage("Kho quân nhu đã mở: AK47, S12K, đạn dược và balo cấp 3 đã được cấp.");
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
        RPC_ShowQuestMessage("Két sắt đã mở: nhận chìa khóa kho quân nhu và S12K.");
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
        RPC_ShowQuestMessage("Đã thu thập: " + MilitaryQuestItemCatalog.GetDisplayName(kind) + ".");
    }

    private void ServerInstallPart(PlayerRef requester, MilitaryQuestItemKind kind)
    {
        if (!HasStateAuthority || CurrentPhase != Phase.SiegeAndRepair || IsPartInstalled(kind)) return;
        if (!TryGetRequestingPlayer(requester, out PlayerMovement player) ||
            !IsNear(player.transform.position, InteractionKind.Vehicle)) return;
        InventorySystem inventory = player.GetComponent<InventorySystem>();
        ItemData item = MilitaryQuestItemCatalog.GetOrCreate(kind);
        if (inventory == null || inventory.GetItemCount(item) < 1) return;

        inventory.ConsumeItem(item, 1);
        SetPartInstalled(kind);
        RPC_ShowQuestMessage("Đã lắp: " + MilitaryQuestItemCatalog.GetDisplayName(kind) + ".");
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

        VehicleRepairProgress = MilitaryQuestRules.MaxRepairProgress;
        ActiveRepairer = PlayerRef.None;
        MilitaryPhase = (int)Phase.ReadyToEscape;
        RPC_BroadcastVehicleReady();
    }

    private void ServerStartRepairSkillCheck(PlayerRef requester, PoliceCarRepairAction action)
    {
        if (!HasStateAuthority || !enableRoadsideRepairTest || requester == PlayerRef.None) return;
        EnsureRoadsideRepairTest();
        if (roadsideRepairStation == null ||
            !TryGetRequestingPlayer(requester, out PlayerMovement player) ||
            !roadsideRepairStation.IsPlayerInRepairPosition(player.transform.position))
        {
            RPC_RepairSessionResponse(requester, false, "Hãy đứng trước mũi xe để sửa chữa.");
            return;
        }

        PlayerHealth health = player.GetComponent<PlayerHealth>();
        if (health != null && (health.isDead || health.isTransforming))
        {
            RPC_RepairSessionResponse(requester, false, "Không thể sửa xe trong trạng thái hiện tại.");
            return;
        }

        if (PoliceCarRepairRules.IsApplied(PoliceCarRepairMask, action))
        {
            RPC_RepairSessionResponse(requester, false, "Hạng mục này đã được sửa hoàn tất.");
            return;
        }

        if (RepairSkillCheckSessionActive && ActiveRepairer != PlayerRef.None && ActiveRepairer != requester)
        {
            RPC_RepairSessionResponse(requester, false,
                "XE ĐANG ĐƯỢC SỬA BỞI: " + GetPlayerDisplayName(ActiveRepairer));
            return;
        }

        if (RepairSkillCheckSessionActive && ActiveRepairer == requester &&
            ActivePoliceRepairAction != (int)action)
        {
            RPC_RepairSessionResponse(requester, false, "Bạn đang sửa một hạng mục khác.");
            return;
        }

        InventorySystem inventory = player.GetComponent<InventorySystem>();
        ArrivalCarItemKind requiredKind = PoliceCarRepairRules.GetRequiredItem(action);
        if (FindPoliceCarItem(inventory, requiredKind) == null)
        {
            RPC_RepairSessionResponse(requester, false,
                "Cần vật phẩm: " + PoliceCarItemCatalog.GetDisplayName(requiredKind) + ".");
            return;
        }

        ActiveRepairer = requester;
        ActivePoliceRepairAction = (int)action;
        RepairSkillCheckProgress = GetPoliceRepairProgress(action);
        RepairSkillCheckSessionActive = true;
        RepairSkillCheckEventActive = false;
        RepairSkillCheckElapsed = 0f;
        RepairPenaltyRemaining = 0f;
        NextRepairSkillCheckSeconds = RandomSkillCheckInterval();
        RPC_RepairSessionResponse(requester, true, string.Empty);
    }

    private void ServerCancelRepairSkillCheck(PlayerRef requester)
    {
        if (!HasStateAuthority || !RepairSkillCheckSessionActive || ActiveRepairer != requester) return;
        ClearRepairSkillCheckSession();
        RPC_RepairCancelled(requester);
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
            AuthorityInterruptRepair(ActiveRepairer, "Người sửa xe đã rời trận.");
            return;
        }

        PlayerHealth health = player.GetComponent<PlayerHealth>();
        if ((health != null && (health.isDead || health.isTransforming)) || roadsideRepairStation == null ||
            !roadsideRepairStation.IsPlayerInRepairPosition(player.transform.position))
        {
            AuthorityInterruptRepair(ActiveRepairer, "Việc sửa xe bị gián đoạn.");
            return;
        }

        float delta = Runner.DeltaTime;
        if (RepairPenaltyRemaining > 0f)
            RepairPenaltyRemaining = Mathf.Max(0f, RepairPenaltyRemaining - delta);
        else
            RepairSkillCheckProgress = VehicleRepairSkillCheckRules.AdvanceBaseProgress(
                RepairSkillCheckProgress, delta, skillRepairDurationSeconds);
        StoreActivePoliceRepairProgress();

        if (RepairSkillCheckProgress >= VehicleRepairSkillCheckRules.MaxProgress)
        {
            PlayerRef completedBy = ActiveRepairer;
            PoliceCarRepairAction completedAction = (PoliceCarRepairAction)ActivePoliceRepairAction;
            RepairSkillCheckProgress = VehicleRepairSkillCheckRules.MaxProgress;
            StoreActivePoliceRepairProgress();
            if (!TryConsumePoliceRepairItem(player, completedAction))
            {
                AuthorityInterruptRepair(completedBy, "Vật phẩm sửa chữa không còn trong túi đồ.");
                return;
            }
            PoliceCarRepairMask |= (int)PoliceCarRepairRules.GetStateBit(completedAction);
            ClearRepairSkillCheckSession();
            RPC_RepairCompleted(completedBy, (int)completedAction,
                PoliceCarRepairRules.IsComplete(PoliceCarRepairMask));
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
        ClearRepairSkillCheckSession();
        RPC_InterruptRepair(player);
        RPC_RepairInterrupted(player, message);
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
        if (!HasStateAuthority || CurrentPhase != Phase.ReadyToEscape) return;
        if (MainQuestManager.Instance == null ||
            MainQuestManager.Instance.LockedEscapeRoute != EscapeEndingRoute.MilitaryEvacuation) return;
        if (!TryGetRequestingPlayer(requester, out PlayerMovement player) ||
            !IsNear(player.transform.position, InteractionKind.Vehicle)) return;

        GatherLivingPlayersForExtraction();
        MilitaryPhase = (int)Phase.Escaped;
        ActiveRepairer = PlayerRef.None;
        RPC_TriggerVictoryCutscene();
    }

    public void TakeGateDamage(float damage)
    {
        if (!HasStateAuthority || CurrentPhase != Phase.SiegeAndRepair || GateCurrentHealth <= 0f) return;
        GateCurrentHealth = MilitaryQuestRules.ApplyGateDamage(GateCurrentHealth, damage);
        if (GateCurrentHealth <= 0f) RPC_GateBroken();
    }

    public void NotifyPlayerDamaged(PlayerRef player, bool zombieAttack)
    {
        if (!HasStateAuthority || player == PlayerRef.None || ActiveRepairer != player) return;
        if (RepairSkillCheckSessionActive)
        {
            AuthorityInterruptRepair(player, "Việc sửa xe bị gián đoạn vì bạn vừa nhận sát thương.");
            return;
        }

        if (!zombieAttack) return;
        ActiveRepairer = PlayerRef.None;
        RPC_InterruptRepair(player);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_StartSiegePresentation() => hordeDirector?.BeginSiege();

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_BroadcastVehicleReady()
    {
        vehicleRepair?.SetVehicleReadyPresentation(true);
        RouteBRadioBroadcastUI.ShowCue(RouteBAudioCueId.EscapeVehicleReady);
        AutoChatManager.Instance?.AddMessage("NHIỆM VỤ",
            "Xe đã sửa xong. Tập hợp tại xe và nhấn E để thoát khỏi khu vực.");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_TriggerVictoryCutscene()
    {
        EscapeRouteDecisionUI.CloseIfOpen();
        RouteBRadioBroadcastUI.ShowCue(RouteBAudioCueId.MilitaryEvacuationComplete);
        hordeDirector?.StopSiege();
        if (vehicleRepair != null)
            vehicleRepair.PlayEscapeCutscene(() => VictorySummaryUI.ShowForCurrentMatch(
                SurvivalSeconds, EscapeEndingRoute.MilitaryEvacuation));
        else
            VictorySummaryUI.ShowForCurrentMatch(SurvivalSeconds, EscapeEndingRoute.MilitaryEvacuation);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_GateBroken()
    {
        gateController?.BreakGate();
        hordeDirector?.ReleaseHordeToPlayers();
        AutoChatManager.Instance?.AddMessage("NHIỆM VỤ", "Cổng đã vỡ! Horde chuyển mục tiêu sang đội sống sót.");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_InterruptRepair(PlayerRef player)
    {
        vehicleRepair?.InterruptRepairFor(player);
        if (Runner != null && Runner.LocalPlayer == player)
            AutoChatManager.Instance?.AddMessage("NHIỆM VỤ", "Việc sửa xe bị gián đoạn vì bạn vừa nhận sát thương.");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_RepairSessionResponse(PlayerRef target, NetworkBool accepted, string message)
    {
        if (Runner == null || Runner.LocalPlayer != target) return;
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
    private void RPC_RepairCancelled(PlayerRef target)
    {
        if (Runner == null || Runner.LocalPlayer != target) return;
        VehicleRepairSkillCheckUI.NotifyCancelled();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_RepairInterrupted(PlayerRef target, string message)
    {
        if (Runner == null || Runner.LocalPlayer != target) return;
        VehicleRepairSkillCheckUI.NotifyInterrupted(message);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_RepairCompleted(PlayerRef target, int action, NetworkBool allComplete)
    {
        if (Runner != null && Runner.LocalPlayer == target)
            VehicleRepairSkillCheckUI.NotifyCompleted((PoliceCarRepairAction)action, allComplete);
        AutoChatManager.Instance?.AddMessage("NHIỆM VỤ", allComplete
            ? "Xe cảnh sát đã hoàn tất đủ 5 hạng mục sửa chữa."
            : "Đã hoàn tất một hạng mục sửa xe cảnh sát.");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowQuestMessage(string message) =>
        AutoChatManager.Instance?.AddMessage("NHIỆM VỤ", message);

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowRouteBAudioCue(int cueId) =>
        RouteBRadioBroadcastUI.ShowCue((RouteBAudioCueId)cueId);

    private void BuildPresentation()
    {
        if (presentationRoot != null) return;
        presentationRoot = new GameObject("Military Base Quest Presentation");
        presentationRoot.transform.SetParent(transform, true);

        gateController = MilitaryGateController.Create(presentationRoot.transform,
            GetInteractionPosition(InteractionKind.Gate), this);
        vehicleRepair = MilitaryEscapeVehicleRepair.Create(presentationRoot.transform,
            GetInteractionPosition(InteractionKind.Vehicle), this);
        hordeDirector = presentationRoot.AddComponent<SiegeHordeDirector>();
        hordeDirector.Configure(this, gateController);

        CreatePoint(InteractionKind.Generator, "MÁY PHÁT ĐIỆN", new Color(0.2f, 0.8f, 0.95f));
        CreatePoint(InteractionKind.Armory, "KHO QUÂN NHU", new Color(0.9f, 0.7f, 0.18f));
        CreatePoint(InteractionKind.BatteryCache, "ẮC QUY", new Color(0.2f, 0.8f, 0.95f));
        CreatePoint(InteractionKind.FuelCache, "NHIÊN LIỆU", new Color(0.85f, 0.22f, 0.18f));
        CreatePoint(InteractionKind.RepairKitCache, "BỘ SỬA CHỮA", new Color(0.9f, 0.9f, 0.8f));
        CreatePoint(InteractionKind.ExitPoint, "EXIT", new Color(0.2f, 0.95f, 0.45f));
        CreatePoint(InteractionKind.OfficeSafe, "KÉT SẮT VĂN PHÒNG", new Color(0.72f, 0.32f, 0.85f));
    }

    private void EnsureRoadsideRepairTest()
    {
        if (!enableRoadsideRepairTest) return;

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

        if (!HasStateAuthority || roadsideVehicleRelocated) return;
        GameObject arrivalMarker = GameObject.Find("ViTriXeTest");
        if (arrivalMarker == null) return;
        roadsideRepairVehicle.AuthorityPrepareRepairTest(
            arrivalMarker.transform.position);
        roadsideVehicleRelocated = true;
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
            else failedItems.Add(PoliceCarItemCatalog.GetDisplayName(kind));
        }

        string message = failedItems.Count == 0
            ? $"F9 đã cấp {addedCount} món còn thiếu. Túi đồ hiện đủ 5/5 vật phẩm sửa xe cảnh sát."
            : $"F9 không thể cấp: {string.Join(", ", failedItems)}. Hãy dọn ô trống rồi thử lại.";
        Debug.Log("[EDITOR TEST] " + message);
        AutoChatManager.Instance?.AddMessage("EDITOR TEST", message);
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
        if (militaryBaseAnchor != null) return;
        GameObject found = GameObject.Find("KhuVucQuanSu");
        if (found != null) militaryBaseAnchor = found.transform;
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
        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth health = players[i] != null ? players[i].GetComponent<PlayerHealth>() : null;
            if (players[i] != null && (health == null || (!health.isDead && !health.isTransforming)) &&
                Vector2.Distance(players[i].transform.position, point) <= distance) return true;
        }
        return false;
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
        if (!TryGetRequestingPlayer(playerRef, out PlayerMovement player)) return "NGƯỜI CHƠI KHÁC";
        PlayerNameTag nameTag = player.GetComponent<PlayerNameTag>();
        string displayName = nameTag != null ? nameTag.PlayerName.ToString() : string.Empty;
        return string.IsNullOrWhiteSpace(displayName) ? "NGƯỜI CHƠI KHÁC" : displayName.ToUpperInvariant();
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
