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

    private bool hasSpawned;
    private GameObject presentationRoot;
    private SiegeHordeDirector hordeDirector;
    private MilitaryGateController gateController;
    private MilitaryEscapeVehicleRepair vehicleRepair;

    public bool IsNetworkReady => hasSpawned && Object != null && Object.IsValid && Runner != null && Runner.IsRunning;
    public Phase CurrentPhase => IsNetworkReady ? (Phase)MilitaryPhase : Phase.NotReached;
    public bool HasAllParts => MilitaryQuestRules.HasAllParts(HasBatteryInstalled, HasFuelInstalled,
        HasRepairKitInstalled);
    public bool IsGateBroken => IsNetworkReady && GateCurrentHealth <= 0f;
    public float InteractionDistance => interactionDistance;
    public Vector2 EscapeExitPosition => GetInteractionPosition(InteractionKind.ExitPoint);

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(this);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (presentationRoot != null) Destroy(presentationRoot);
    }

    public override void Spawned()
    {
        hasSpawned = true;
        ResolveAnchor();
        BuildPresentation();

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
            ActiveRepairer = PlayerRef.None;
            RPC_ShowQuestMessage("NHIỆM VỤ THẤT BẠI: Không còn người sống sót tại căn cứ.");
        }
    }

    public override void Render()
    {
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
        if (!HasStateAuthority || !zombieAttack || player == PlayerRef.None || ActiveRepairer != player) return;
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

    private static void GrantItem(InventorySystem inventory, string id, int amount)
    {
        ItemData item = ItemDataLoader.LoadItem(id);
        if (item != null) inventory.AddItem(item, amount);
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
