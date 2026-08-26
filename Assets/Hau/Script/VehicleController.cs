using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Rigidbody2D))]
public class VehicleControllerFusion : NetworkBehaviour
{
    private enum SeatSlot { Driver, FrontPassenger, RearLeftPassenger, RearRightPassenger, None }
    private enum DirectionLayout { LegacyFiveByFive, EightWayIsometric }

    [Header("Seat anchors")]
    [SerializeField] private Transform driverSeat;
    [SerializeField] private Transform frontPassengerSeat;
    [SerializeField] private Transform rearLeftSeat;
    [SerializeField] private Transform rearRightSeat;

    [Header("Entry points - one point per door")]
    [SerializeField] private Transform driverEnterPoint;
    [SerializeField] private Transform frontPassengerEnterPoint;
    [SerializeField] private Transform rearLeftEnterPoint;
    [SerializeField] private Transform rearRightEnterPoint;
    [SerializeField, Min(0.1f)] private float enterDistance = 1.15f;

    [Header("Exit points - one safe point per seat")]
    [SerializeField] private Transform driverExitPoint;
    [SerializeField] private Transform frontPassengerExitPoint;
    [SerializeField] private Transform rearLeftExitPoint;
    [SerializeField] private Transform rearRightExitPoint;

    [Header("Driving")]
    [SerializeField] private bool entryLockedForRepair;
    [SerializeField, Min(0.1f)] private float maxForwardSpeed = 5.5f;
    [SerializeField, Min(0.1f)] private float maxReverseSpeed = 2.2f;
    [SerializeField, Min(0.1f)] private float acceleration = 7f;
    [SerializeField, Min(0.1f)] private float braking = 10f;
    [SerializeField, Min(0.1f)] private float handbrakeDeceleration = 18f;
    [SerializeField, Min(0f)] private float coastDeceleration = 2.5f;
    [SerializeField, Min(0f)] private float turnSpeed = 90f;
    [SerializeField, Range(0f, 1f)] private float lateralGrip = 0.68f;
    [SerializeField, Range(0f, 1f)] private float steeringAtLowSpeed = 0.45f;
    [SerializeField, Min(0.01f)] private float gearChangeSpeedThreshold = 0.12f;
    [SerializeField] private DirectionLayout directionLayout = DirectionLayout.LegacyFiveByFive;
    [SerializeField, Range(0, LegacyDirectionCount - 1)] private int initialDirectionIndex = 2;
    [SerializeField, Range(0.1f, 1f)] private float isometricVerticalScale =
        IsometricMovementProjection.DefaultVerticalScale;
    [SerializeField] private bool showDirectionDebug;

    [Header("Visuals")]
    [SerializeField] private Sprite[] directionSprites = new Sprite[LegacyDirectionCount];
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator animator;
    [SerializeField] private Camera vehicleCamera;

    [Header("Collision")]
    [SerializeField] private PolygonCollider2D bodyCollider;
    [SerializeField] private CircleCollider2D interactionCollider;
    [SerializeField, Min(0.01f)] private float directionColliderScale = 1f;

    [Header("Headlights and vehicle vision")]
    [Tooltip("Legacy/main beam reference. Used as the left headlight.")]
    [SerializeField] private Light2D headlights;
    [SerializeField] private Light2D secondaryHeadlight;
    [SerializeField, Min(1f)] private float headlightVisionRadius = 12f;
    [SerializeField, Range(20f, 90f)] private float headlightVisionAngle = 48f;
    [SerializeField, Min(0.1f)] private float headlightForwardOffset = 0.52f;
    [SerializeField, Min(0.02f)] private float headlightLateralOffset = 0.14f;
    [SerializeField, Range(0.05f, 2f)] private float headlightBeamIntensity = 0.36f;
    [SerializeField, Range(0f, 8f)] private float headlightToeInAngle = 2.2f;
    [SerializeField, Min(0.5f)] private float lightsOffVisionRadius = 2.5f;

    [Header("Zombie collision")]
    [SerializeField, Min(0.1f)] private float minimumImpactSpeed = 1.25f;
    [SerializeField, Min(1f)] private float minimumImpactDamage = 55f;
    [SerializeField, Min(1f)] private float maximumImpactDamage = 180f;
    [SerializeField, Min(0f)] private float impactForce = 4f;

    [Header("Door audio")]
    [SerializeField] private AudioSource doorAudioSource;
    [SerializeField] private AudioClip doorOpenClip;
    [SerializeField] private AudioClip doorCloseClip;

    [Header("Vehicle noise and zombie attraction")]
    [SerializeField, Min(1f)] private float starterNoiseRadius = 16f;
    [SerializeField, Min(1f)] private float idleEngineNoiseRadius = 11f;
    [SerializeField, Min(1f)] private float drivingEngineNoiseRadius = 22f;
    [SerializeField, Min(1f)] private float hornSingleNoiseRadius = 36f;
    [SerializeField, Min(1f)] private float hornHoldNoiseRadius = 46f;
    [SerializeField, Min(0.1f)] private float engineNoiseInterval = 0.75f;
    [SerializeField, Min(0.1f)] private float hornNoiseInterval = 0.6f;

    [Networked] public NetworkObject Driver { get; private set; }
    [Networked] public NetworkObject FrontPassenger { get; private set; }
    [Networked] public NetworkObject RearLeftPassenger { get; private set; }
    [Networked] public NetworkObject RearRightPassenger { get; private set; }
    [Networked] public NetworkBool IsMoving { get; private set; }
    [Networked] public NetworkBool EngineRunning { get; private set; }
    [Networked] public NetworkBool HornHeld { get; private set; }
    [Networked] public NetworkBool HeadlightsOn { get; private set; }
    // This lock is story state, not a local presentation flag. Keeping it
    // replicated prevents clients from seeing the police car as enterable
    // while the State Authority is still holding it for repair.
    [Networked] private NetworkBool RepairEntryLocked { get; set; }
    [Networked] public int DirectionIndex { get; private set; }
    [Networked] private float HeadingDegrees { get; set; }

    private const int LegacyDirectionCount = 25;
    private const int RepairTestDirectionIndex = 0;
    private Rigidbody2D rb;
    private int displayedDirection = -1;
    private Coroutine doorSequence;
    private readonly Dictionary<int, float> zombieImpactCooldown = new();
    private readonly Collider2D[] zombieContacts = new Collider2D[32];
    private readonly RaycastHit2D[] zombieSweepHits = new RaycastHit2D[32];
    private ContactFilter2D zombieContactFilter;
    private bool networkSpawned;
    private float engineNoiseCooldown;
    private float hornNoiseCooldown;

    public Vector2 VisionOrigin => transform.position;
    public Vector2 VisionDirection => DirectionIndexToWorldVector(DirectionIndex);
    public float VisionRadius => HeadlightsOn ? headlightVisionRadius : lightsOffVisionRadius;
    public float VisionAngle => HeadlightsOn ? EffectiveHeadlightAngle : 360f;
    public bool IsEntryLockedForRepair => networkSpawned ? RepairEntryLocked : entryLockedForRepair;
    public bool IsNetworkSpawned => networkSpawned;
    public bool IsEngineRunning => EngineRunning;
    public bool IsHornHeld => HornHeld;
    public bool HasLocalDriver => Driver != null && Driver.HasInputAuthority;
    public bool HasLocalOccupant => HasLocalInputAuthority(Driver) ||
                                    HasLocalInputAuthority(FrontPassenger) ||
                                    HasLocalInputAuthority(RearLeftPassenger) ||
                                    HasLocalInputAuthority(RearRightPassenger);
    private float EffectiveHeadlightAngle => Mathf.Clamp(headlightVisionAngle, 20f, 60f);

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.mass = Mathf.Max(300f, rb.mass);
        spriteRenderer ??= GetComponent<SpriteRenderer>();
        bodyCollider ??= GetComponent<PolygonCollider2D>();
        interactionCollider ??= GetComponent<CircleCollider2D>();
        ConfigureColliders();
        ConfigureHeadlights();
        ConfigureDoorAudio();
        VehicleEngineAudioController.Attach(this);
        VehicleHornAudioController.Attach(this);
        ConfigureZombieContactFilter();
        UpdateBodyCollider(ClampDirectionIndex(initialDirectionIndex));
        SetVehicleMotionLocked(true);
        if (vehicleCamera != null) vehicleCamera.gameObject.SetActive(false);
    }

    public override void Spawned()
    {
        networkSpawned = true;
        if (!HasStateAuthority) return;
        rb.bodyType = RigidbodyType2D.Dynamic;
        DirectionIndex = ClampDirectionIndex(initialDirectionIndex);
        HeadingDegrees = DirectionIndexToHeading(DirectionIndex);
        EngineRunning = false;
        HornHeld = false;
        HeadlightsOn = false;
        RepairEntryLocked = entryLockedForRepair;
        UpdateBodyCollider(DirectionIndex);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        networkSpawned = false;
        GetComponent<VehicleEngineAudioController>()?.NotifyNetworkDespawned();
        GetComponent<VehicleHornAudioController>()?.NotifyNetworkDespawned();
    }

    public void RequestEnter(NetworkObject player)
    {
        if (player != null) RPC_RequestEnter(player);
    }

    public bool AuthorityTryEnter(NetworkObject player)
    {
        if (!HasStateAuthority || player == null || IsEntryLockedForRepair) return false;
        PlayerInteraction interaction = player.GetComponent<PlayerInteraction>();
        if (interaction == null || interaction.IsInVehicle) return false;

        SeatSlot slot = FindDoorNear(player.transform.position);
        if (slot == SeatSlot.None) return false;
        AssignSeat(slot, player);
        return true;
    }

    public void SetRepairEntryLocked(bool locked)
    {
        if (networkSpawned)
        {
            if (!HasStateAuthority) return;
            RepairEntryLocked = locked;
        }
        else
        {
            entryLockedForRepair = locked;
        }
        if (locked && HasStateAuthority)
        {
            HornHeld = false;
            EngineRunning = false;
            StopVehicle();
        }
    }

    public bool AuthorityStartEngine()
    {
        if (!HasStateAuthority || IsEntryLockedForRepair) return false;
        EngineRunning = true;
        return true;
    }

    public bool AuthorityPlayStarterConfirmation(NetworkObject sourcePlayer)
    {
        if (!HasStateAuthority || IsEntryLockedForRepair) return false;
        EmitVehicleNoise(sourcePlayer, starterNoiseRadius, 18, 0.78f);
        RPC_PlayStarterConfirmation(sourcePlayer);
        return true;
    }

    public void PlayCinematicDoorSequence()
    {
        if (doorSequence != null) StopCoroutine(doorSequence);
        doorSequence = StartCoroutine(PlayDoorSequence());
    }

    public void PlayCinematicFailedStarter() =>
        GetComponent<VehicleEngineAudioController>()?.PlayStarterConfirmation(null);

    public void SetCinematicAlarm(bool active) =>
        GetComponent<VehicleHornAudioController>()?.SetCinematicAlarm(active);

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayStarterConfirmation(NetworkObject sourcePlayer)
    {
        GetComponent<VehicleEngineAudioController>()?.PlayStarterConfirmation(sourcePlayer);
    }

    /// <summary>Development-only placement helper. The canonical repair flow
    /// uses <see cref="AuthorityPrepareRepairAtCurrentPosition"/> and never
    /// relocates the authored police car.</summary>
    public void AuthorityPrepareRepairTest(Vector2 position)
    {
        if (!HasStateAuthority) return;
        AuthorityPrepareRepairAtCurrentPosition();

        if (rb != null)
        {
            rb.position = position;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
        transform.position = new Vector3(position.x, position.y, transform.position.z);
        Physics2D.SyncTransforms();
    }

    public void AuthorityPrepareRepairAtCurrentPosition()
    {
        if (!HasStateAuthority) return;
        SetRepairEntryLocked(true);
        HornHeld = false;
        EngineRunning = false;
        StopVehicle();

        // The authored police car already sits at SpawnXeCanhSat. Lock its
        // canonical preview direction without relocating the scene vehicle.
        DirectionIndex = ClampDirectionIndex(RepairTestDirectionIndex);
        HeadingDegrees = DirectionIndexToHeading(DirectionIndex);
        displayedDirection = -1;
        ApplyDirectionalVisual();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestEnter(NetworkObject player, RpcInfo info = default)
    {
        if (player == null || player.InputAuthority != info.Source) return;
        AuthorityTryEnter(player);
    }

    public void RequestExit(NetworkObject player)
    {
        if (player != null) RPC_RequestExit(player);
    }

    public bool AuthorityTryExit(NetworkObject player)
    {
        if (!HasStateAuthority || player == null) return false;
        SeatSlot slot = FindOccupiedSeat(player);
        if (slot == SeatSlot.None) return false;

        Vector2 exitPosition = GetExitWorldPosition(slot);
        SetOccupant(slot, null);
        MovePlayerImmediately(player, exitPosition);
        SetPlayerVehicleState(player, false, false, 0, exitPosition);
        if (slot == SeatSlot.Driver)
        {
            HeadlightsOn = false;
            HornHeld = false;
            EngineRunning = false;
            StopVehicle();
        }
        RPC_PlayDoorSequence();
        return true;
    }

    public void RequestSeatChange(NetworkObject player, int seatNumber)
    {
        if (player != null && seatNumber >= 1 && seatNumber <= 4)
            RPC_RequestSeatChange(player, seatNumber);
    }

    public void RequestHornSingle(NetworkObject player)
    {
        if (player != null) RPC_RequestHornSingle(player);
    }

    public void RequestHornHold(NetworkObject player, bool held)
    {
        if (player != null) RPC_RequestHornHold(player, held);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestHornSingle(NetworkObject player, RpcInfo info = default)
    {
        if (player == null || player.InputAuthority != info.Source || Driver != player) return;
        EmitVehicleNoise(player, hornSingleNoiseRadius, 100, 1f);
        RPC_PlayHornSingle(player);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestHornHold(NetworkObject player, bool held, RpcInfo info = default)
    {
        if (player == null || player.InputAuthority != info.Source || Driver != player) return;
        HornHeld = held;
        if (!held) return;
        hornNoiseCooldown = hornNoiseInterval;
        EmitVehicleNoise(player, hornHoldNoiseRadius, 100, 1f);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayHornSingle(NetworkObject sourcePlayer)
    {
        GetComponent<VehicleHornAudioController>()?.PlaySingle(sourcePlayer);
    }

    public bool AuthorityTryChangeSeat(NetworkObject player, int seatNumber)
    {
        if (!HasStateAuthority || player == null || seatNumber < 1 || seatNumber > 4) return false;
        return TryChangeSeat(player, SeatFromNumber(seatNumber));
    }

    public void RequestToggleHeadlights(NetworkObject player)
    {
        if (player != null) RPC_RequestToggleHeadlights(player);
    }

    public bool AuthorityTryToggleHeadlights(NetworkObject player)
    {
        return HasStateAuthority && TryToggleHeadlights(player);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestToggleHeadlights(NetworkObject player, RpcInfo info = default)
    {
        if (player == null || player.InputAuthority != info.Source) return;
        TryToggleHeadlights(player);
    }

    private bool TryToggleHeadlights(NetworkObject player)
    {
        if (Driver != player) return false;
        HeadlightsOn = !HeadlightsOn;
        return true;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestSeatChange(NetworkObject player, int seatNumber, RpcInfo info = default)
    {
        if (player == null || player.InputAuthority != info.Source) return;
        SeatSlot target = SeatFromNumber(seatNumber);
        TryChangeSeat(player, target);
    }

    private bool TryChangeSeat(NetworkObject player, SeatSlot target)
    {
        SeatSlot current = FindOccupiedSeat(player);
        if (current == SeatSlot.None || target == SeatSlot.None || current == target) return false;
        if (GetOccupant(target) != null) return false;

        SetOccupant(current, null);
        SetOccupant(target, player);
        SetPlayerVehicleState(player, true, target == SeatSlot.Driver, SeatNumber(target), default);
        MoveToSeat(player, GetAnchorWorldPosition(GetSeatAnchor(target)));

        if (current == SeatSlot.Driver && target != SeatSlot.Driver)
        {
            HeadlightsOn = false;
            HornHeld = false;
            EngineRunning = false;
            StopVehicle();
        }
        if (target == SeatSlot.Driver)
        {
            EngineRunning = true;
            SetVehicleMotionLocked(false);
        }
        return true;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestExit(NetworkObject player, RpcInfo info = default)
    {
        if (player == null || player.InputAuthority != info.Source) return;
        AuthorityTryExit(player);
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        if (Driver == null)
        {
            HeadlightsOn = false;
            HornHeld = false;
            EngineRunning = false;
            StopVehicle();
            SetVehicleMotionLocked(true);
            ProcessZombieContacts();
            return;
        }

        SetVehicleMotionLocked(false);
        PlayerMovement driverMovement = Driver.GetComponent<PlayerMovement>();
        SimulateDrive(
            driverMovement != null ? driverMovement.NetMoveInput : Vector2.zero,
            driverMovement != null && driverMovement.NetIsVehicleBraking);
        SyncOccupants();
        UpdateVehicleNoise(driverMovement);
        ProcessZombieContacts();
    }

    private void UpdateVehicleNoise(PlayerMovement driverMovement)
    {
        if (driverMovement == null || driverMovement.Object == null || !driverMovement.Object.IsValid) return;

        if (EngineRunning)
        {
            engineNoiseCooldown -= Runner.DeltaTime;
            if (engineNoiseCooldown <= 0f)
            {
                bool driving = rb != null && rb.linearVelocity.magnitude >= gearChangeSpeedThreshold;
                EmitVehicleNoise(driverMovement.Object,
                    driving ? drivingEngineNoiseRadius : idleEngineNoiseRadius,
                    driving ? 24 : 12, driving ? 0.78f : 0.42f);
                engineNoiseCooldown = engineNoiseInterval;
            }
        }
        else
        {
            engineNoiseCooldown = 0f;
        }

        if (!HornHeld)
        {
            hornNoiseCooldown = 0f;
            return;
        }

        hornNoiseCooldown -= Runner.DeltaTime;
        if (hornNoiseCooldown > 0f) return;
        EmitVehicleNoise(driverMovement.Object, hornHoldNoiseRadius, 100, 1f);
        hornNoiseCooldown = hornNoiseInterval;
    }

    private static void EmitVehicleNoise(NetworkObject sourcePlayer, float radius, int responderLimit,
        float urgency)
    {
        if (sourcePlayer == null || !sourcePlayer.IsValid) return;
        PlayerMovement movement = sourcePlayer.GetComponent<PlayerMovement>();
        movement?.MakeNoise(radius, true, responderLimit, radius, urgency);
    }

    private static bool HasLocalInputAuthority(NetworkObject player) =>
        player != null && player.IsValid && player.HasInputAuthority;

    public override void Render()
    {
        ApplyDirectionalVisual();
        ApplyHeadlightVisual();
    }

    private void OnGUI()
    {
        if (!showDirectionDebug || directionLayout != DirectionLayout.EightWayIsometric) return;

        PlayerMovement localPlayer = PlayerMovement.LocalPlayerInstance;
        PlayerInteraction interaction = localPlayer != null
            ? localPlayer.GetComponent<PlayerInteraction>()
            : null;
        if (interaction == null || !interaction.IsVehicleDriver || interaction.CurrentVehicleController != this)
            return;

        int index = EightWayDirection.NormalizeIndex(DirectionIndex);
        string direction = EightWayDirection.IndexToLabel(index);
        GUIStyle style = new(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
        GUI.Box(
            new Rect(Screen.width * 0.5f - 150f, 104f, 300f, 38f),
            $"SEDAN FRONT: {direction}   |   INDEX: {index}",
            style);
    }

    public void SetCamera(bool enable)
    {
        PZ_CameraController camera = PZ_CameraController.Instance;
        if (camera == null) return;
        if (enable) camera.SetTarget(transform);
        else if (PlayerMovement.LocalPlayerInstance != null) camera.SetTarget(PlayerMovement.LocalPlayerInstance.transform);
    }

    private SeatSlot FindDoorNear(Vector3 playerPosition)
    {
        SeatSlot closest = SeatSlot.None;
        float closestDistance = float.MaxValue;
        foreach (SeatSlot slot in new[] { SeatSlot.Driver, SeatSlot.FrontPassenger, SeatSlot.RearLeftPassenger, SeatSlot.RearRightPassenger })
        {
            Transform point = GetEnterPoint(slot);
            if (point == null) continue;
            if (GetOccupant(slot) != null) continue;
            float distance = Vector2.Distance(playerPosition, GetAnchorWorldPosition(point));
            if (distance <= enterDistance && distance < closestDistance)
            {
                closest = slot;
                closestDistance = distance;
            }
        }


        // The CircleCollider2D is the single source of truth for interaction.
        // Door markers only decide which free seat is closest; they must not
        // reject a player who is visibly standing inside the trigger.
        if (closest == SeatSlot.None && interactionCollider != null &&
            interactionCollider.OverlapPoint((Vector2)playerPosition))
        {
            foreach (SeatSlot slot in new[] { SeatSlot.Driver, SeatSlot.FrontPassenger, SeatSlot.RearLeftPassenger, SeatSlot.RearRightPassenger })
            {
                if (GetOccupant(slot) != null) continue;
                Transform point = GetEnterPoint(slot);
                if (point == null) continue;
                float distance = Vector2.Distance(playerPosition, GetAnchorWorldPosition(point));
                if (distance >= closestDistance) continue;
                closest = slot;
                closestDistance = distance;
            }
        }
        return closest;
    }

    private void AssignSeat(SeatSlot slot, NetworkObject player)
    {
        SetOccupant(slot, player);
        SetPlayerVehicleState(player, true, slot == SeatSlot.Driver, SeatNumber(slot), default);
        MoveToSeat(player, GetAnchorWorldPosition(GetSeatAnchor(slot)));
        if (slot == SeatSlot.Driver)
        {
            EngineRunning = true;
            SetVehicleMotionLocked(false);
        }
        RPC_PlayDoorSequence();
    }

    private void SetPlayerVehicleState(
        NetworkObject player,
        bool inVehicle,
        bool isDriver,
        int seatNumber,
        Vector2 exitPosition)
    {
        PlayerInteraction interaction = player.GetComponent<PlayerInteraction>();
        if (interaction != null)
            interaction.SetVehicleNetworkState(Object, inVehicle, isDriver, seatNumber, exitPosition);
    }

    private void SyncOccupants()
    {
        MoveToSeat(Driver, GetAnchorWorldPosition(driverSeat));
        MoveToSeat(FrontPassenger, GetAnchorWorldPosition(frontPassengerSeat));
        MoveToSeat(RearLeftPassenger, GetAnchorWorldPosition(rearLeftSeat));
        MoveToSeat(RearRightPassenger, GetAnchorWorldPosition(rearRightSeat));
    }

    private static void MoveToSeat(NetworkObject player, Vector2 seatPosition)
    {
        if (player == null) return;
        Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
        if (playerBody != null) playerBody.position = seatPosition;
        player.transform.position = seatPosition;
    }

    private Vector2 GetExitWorldPosition(SeatSlot slot)
    {
        Transform point = GetExitPoint(slot) ?? GetEnterPoint(slot) ?? transform;
        return GetAnchorWorldPosition(point);
    }

    private static void MovePlayerImmediately(NetworkObject player, Vector2 worldPosition)
    {
        if (player == null) return;
        Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
        if (playerBody != null)
        {
            playerBody.position = worldPosition;
            playerBody.linearVelocity = Vector2.zero;
            playerBody.angularVelocity = 0f;
        }
        player.transform.position = worldPosition;
    }

    private NetworkObject GetOccupant(SeatSlot slot) => slot switch
    {
        SeatSlot.Driver => Driver,
        SeatSlot.FrontPassenger => FrontPassenger,
        SeatSlot.RearLeftPassenger => RearLeftPassenger,
        SeatSlot.RearRightPassenger => RearRightPassenger,
        _ => null
    };

    private void SetOccupant(SeatSlot slot, NetworkObject player)
    {
        switch (slot)
        {
            case SeatSlot.Driver: Driver = player; break;
            case SeatSlot.FrontPassenger: FrontPassenger = player; break;
            case SeatSlot.RearLeftPassenger: RearLeftPassenger = player; break;
            case SeatSlot.RearRightPassenger: RearRightPassenger = player; break;
        }
    }

    private SeatSlot FindOccupiedSeat(NetworkObject player)
    {
        foreach (SeatSlot slot in new[] { SeatSlot.Driver, SeatSlot.FrontPassenger, SeatSlot.RearLeftPassenger, SeatSlot.RearRightPassenger })
            if (GetOccupant(slot) == player) return slot;
        return SeatSlot.None;
    }

    private static SeatSlot SeatFromNumber(int seatNumber) => seatNumber switch
    {
        1 => SeatSlot.Driver,
        2 => SeatSlot.FrontPassenger,
        3 => SeatSlot.RearLeftPassenger,
        4 => SeatSlot.RearRightPassenger,
        _ => SeatSlot.None
    };

    private static int SeatNumber(SeatSlot slot) => slot switch
    {
        SeatSlot.Driver => 1,
        SeatSlot.FrontPassenger => 2,
        SeatSlot.RearLeftPassenger => 3,
        SeatSlot.RearRightPassenger => 4,
        _ => 0
    };

    private Transform GetSeatAnchor(SeatSlot slot) => slot switch
    {
        SeatSlot.Driver => driverSeat,
        SeatSlot.FrontPassenger => frontPassengerSeat,
        SeatSlot.RearLeftPassenger => rearLeftSeat,
        SeatSlot.RearRightPassenger => rearRightSeat,
        _ => null
    };

    private Transform GetEnterPoint(SeatSlot slot) => slot switch
    {
        SeatSlot.Driver => driverEnterPoint,
        SeatSlot.FrontPassenger => frontPassengerEnterPoint,
        SeatSlot.RearLeftPassenger => rearLeftEnterPoint,
        SeatSlot.RearRightPassenger => rearRightEnterPoint,
        _ => null
    };

    private Transform GetExitPoint(SeatSlot slot) => slot switch
    {
        SeatSlot.Driver => driverExitPoint,
        SeatSlot.FrontPassenger => frontPassengerExitPoint,
        SeatSlot.RearLeftPassenger => rearLeftExitPoint,
        SeatSlot.RearRightPassenger => rearRightExitPoint,
        _ => null
    };

    private void SimulateDrive(Vector2 input, bool handbrake)
    {
        Vector2 forward = HeadingToWorldVector(HeadingDegrees);
        float forwardSpeed = Vector2.Dot(rb.linearVelocity, forward);
        float throttle = Mathf.Clamp(input.y, -1f, 1f);

        // A gearbox cannot jump directly from forward to reverse. Opposite
        // throttle first acts as a service brake until the car is almost still.
        bool changingDirection =
            (throttle < -0.01f && forwardSpeed > gearChangeSpeedThreshold) ||
            (throttle > 0.01f && forwardSpeed < -gearChangeSpeedThreshold);

        float targetSpeed;
        float rate;
        if (handbrake)
        {
            targetSpeed = 0f;
            rate = handbrakeDeceleration;
        }
        else if (changingDirection)
        {
            targetSpeed = 0f;
            rate = braking;
        }
        else
        {
            targetSpeed = throttle >= 0f ? throttle * maxForwardSpeed : throttle * maxReverseSpeed;
            rate = Mathf.Abs(targetSpeed) > Mathf.Abs(forwardSpeed) ? acceleration : braking;
            if (Mathf.Abs(throttle) < 0.01f)
                targetSpeed = Mathf.MoveTowards(forwardSpeed, 0f, coastDeceleration * Runner.DeltaTime);
        }

        forwardSpeed = Mathf.MoveTowards(forwardSpeed, targetSpeed, rate * Runner.DeltaTime);
        Vector2 lateral = rb.linearVelocity - forward * Vector2.Dot(rb.linearVelocity, forward);
        rb.linearVelocity = forward * forwardSpeed + lateral * (1f - lateralGrip);
        float speedFactor = Mathf.Lerp(steeringAtLowSpeed, 1f, Mathf.Clamp01(Mathf.Abs(forwardSpeed) / maxForwardSpeed));
        HeadingDegrees = Mathf.Repeat(HeadingDegrees + input.x * turnSpeed * speedFactor * (forwardSpeed < -0.01f ? -1f : 1f) * Runner.DeltaTime, 360f);
        DirectionIndex = HeadingToDirectionIndex(HeadingDegrees);
        UpdateBodyCollider(DirectionIndex);
        IsMoving = Mathf.Abs(forwardSpeed) > 0.05f;
    }

    private void StopVehicle()
    {
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        IsMoving = false;
    }

    private void ApplyDirectionalVisual()
    {
        if (DirectionIndex == displayedDirection) return;
        displayedDirection = DirectionIndex;
        if (directionSprites != null && directionSprites.Length == ActiveDirectionCount && directionSprites[DirectionIndex] != null)
        {
            if (animator != null) animator.enabled = false;
            if (spriteRenderer != null) spriteRenderer.sprite = directionSprites[DirectionIndex];
        }
        UpdateBodyCollider(DirectionIndex);
    }

    private void ConfigureColliders()
    {
        if (interactionCollider != null)
        {
            interactionCollider.isTrigger = true;
            interactionCollider.offset = Vector2.zero;
            interactionCollider.radius = Mathf.Max(1.35f, interactionCollider.radius);
        }

        if (bodyCollider != null)
        {
            bodyCollider.isTrigger = false;
            bodyCollider.offset = Vector2.zero;
        }
    }

    private void UpdateBodyCollider(int directionIndex)
    {
        if (bodyCollider == null) return;
        int index = ClampDirectionIndex(directionIndex);
        Sprite sprite = directionSprites != null && index < directionSprites.Length
            ? directionSprites[index]
            : null;
        if (sprite == null || sprite.GetPhysicsShapeCount() == 0) return;

        int shapeCount = sprite.GetPhysicsShapeCount();
        bodyCollider.pathCount = shapeCount;
        List<Vector2> points = new(20);
        for (int path = 0; path < shapeCount; path++)
        {
            points.Clear();
            sprite.GetPhysicsShape(path, points);
            if (!Mathf.Approximately(directionColliderScale, 1f))
            {
                for (int point = 0; point < points.Count; point++)
                    points[point] *= directionColliderScale;
            }
            bodyCollider.SetPath(path, points);
        }
    }

    private void ConfigureHeadlights()
    {
        headlights = GetOrCreateVehicleLight(headlights, "Vehicle Headlight Left");
        secondaryHeadlight = GetOrCreateVehicleLight(secondaryHeadlight, "Vehicle Headlight Right");

        // Both real lamp positions now cast their own full road beam. The small
        // toe-in angle makes them overlap naturally at distance without ever
        // inventing a third light source in the middle of the vehicle.
        float individualBeamAngle = Mathf.Clamp(EffectiveHeadlightAngle * 0.9f, 38f, 52f);
        ConfigureHeadlightBeam(
            headlights, headlightVisionRadius, individualBeamAngle,
            headlightBeamIntensity, true, 0.3f, 0.9f);
        ConfigureHeadlightBeam(
            secondaryHeadlight, headlightVisionRadius, individualBeamAngle,
            headlightBeamIntensity, true, 0.3f, 0.9f);

        SetHeadlightObjectsActive(false);
    }

    private void ApplyHeadlightVisual()
    {
        if (headlights == null || secondaryHeadlight == null)
            ConfigureHeadlights();

        bool enabled = HeadlightsOn;
        SetHeadlightObjectsActive(enabled);

        Vector2 forward = VisionDirection.normalized;
        Vector2 right = new(forward.y, -forward.x);
        Vector2 frontCenter = (Vector2)transform.position + forward * headlightForwardOffset;
        Vector2 leftBeamDirection = Quaternion.Euler(0f, 0f, -headlightToeInAngle) * forward;
        Vector2 rightBeamDirection = Quaternion.Euler(0f, 0f, headlightToeInAngle) * forward;

        PositionHeadlight(headlights, frontCenter - right * headlightLateralOffset, leftBeamDirection);
        PositionHeadlight(secondaryHeadlight, frontCenter + right * headlightLateralOffset, rightBeamDirection);
    }

    private Light2D GetOrCreateVehicleLight(Light2D light, string objectName)
    {
        if (light != null) return light;
        Transform existing = transform.Find(objectName);
        if (existing != null && existing.TryGetComponent(out Light2D existingLight)) return existingLight;

        GameObject lightObject = new(objectName);
        lightObject.transform.SetParent(transform, false);
        return lightObject.AddComponent<Light2D>();
    }

    private static void ConfigureHeadlightBeam(
        Light2D light,
        float radius,
        float outerAngle,
        float intensity,
        bool castShadows,
        float innerAngleRatio,
        float falloff)
    {
        light.lightType = Light2D.LightType.Point;
        light.pointLightInnerRadius = 0.12f;
        light.pointLightOuterRadius = radius;
        light.pointLightInnerAngle = Mathf.Clamp(outerAngle * innerAngleRatio, 5f, outerAngle - 2f);
        light.pointLightOuterAngle = outerAngle;
        light.falloffIntensity = falloff;
        light.intensity = intensity;
        light.color = new Color(1f, 0.93f, 0.78f, 1f);
        light.shadowsEnabled = castShadows;
        light.shadowIntensity = castShadows ? 0.72f : 0f;
        light.shadowSoftness = castShadows ? 0.55f : 0f;
    }

    private void SetHeadlightObjectsActive(bool active)
    {
        SetLightActive(headlights, active);
        SetLightActive(secondaryHeadlight, active);
    }

    private static void SetLightActive(Light2D light, bool active)
    {
        if (light != null && light.gameObject.activeSelf != active)
            light.gameObject.SetActive(active);
    }

    private static void PositionHeadlight(Light2D light, Vector2 worldPosition, Vector2 forward)
    {
        if (light == null) return;
        light.transform.position = worldPosition;

        // URP Light2D point cones face along Transform.up.
        light.transform.up = forward;
    }

    private void ConfigureDoorAudio()
    {
        doorAudioSource ??= GetComponent<AudioSource>();
        if (doorAudioSource == null) doorAudioSource = gameObject.AddComponent<AudioSource>();
        GameplayAudioSpatializer.Configure(doorAudioSource, GameplayAudioSpatializer.Profile.Body);
        doorAudioSource.volume = Mathf.Clamp01(PlayerPrefs.GetFloat("GameSFXVolume", 0.8f));
        // Serialized UnityEngine.Object references can be Unity "fake null",
        // therefore use Unity's == null operator instead of ??= here.
        if (doorOpenClip == null)
            doorOpenClip = Resources.Load<AudioClip>("Intro/VehicleAudio/CarOpenDoor");
        if (doorCloseClip == null)
            doorCloseClip = Resources.Load<AudioClip>("Intro/VehicleAudio/CarCloseDoor");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayDoorSequence()
    {
        if (doorSequence != null) StopCoroutine(doorSequence);
        doorSequence = StartCoroutine(PlayDoorSequence());
    }

    private IEnumerator PlayDoorSequence()
    {
        ConfigureDoorAudio();
        doorAudioSource.Stop();
        if (doorOpenClip != null)
        {
            doorAudioSource.clip = doorOpenClip;
            doorAudioSource.Play();
            yield return new WaitForSecondsRealtime(doorOpenClip.length + 0.08f);
        }
        if (doorCloseClip != null)
        {
            doorAudioSource.clip = doorCloseClip;
            doorAudioSource.Play();
        }
        doorSequence = null;
    }

    private void ConfigureZombieContactFilter()
    {
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        zombieContactFilter = new ContactFilter2D
        {
            useLayerMask = enemyLayer >= 0,
            useTriggers = false
        };
        if (enemyLayer >= 0) zombieContactFilter.SetLayerMask(1 << enemyLayer);
    }

    private void ProcessZombieContacts()
    {
        if (!HasStateAuthority || bodyCollider == null) return;
        float speed = rb != null ? rb.linearVelocity.magnitude : 0f;

        // Sweep the body through the distance it will travel this tick. This
        // prevents a fast car from tunnelling through a narrow zombie collider.
        if (speed >= minimumImpactSpeed)
        {
            Vector2 travelDirection = rb.linearVelocity.normalized;
            float sweepDistance = speed * Runner.DeltaTime + 0.12f;
            int sweepCount = bodyCollider.Cast(
                travelDirection, zombieContactFilter, zombieSweepHits, sweepDistance);
            for (int i = 0; i < sweepCount; i++)
            {
                Collider2D hitCollider = zombieSweepHits[i].collider;
                zombieSweepHits[i] = default;
                if (hitCollider != null) TryDamageZombie(hitCollider.gameObject, speed);
            }
        }

        int count = bodyCollider.Overlap(zombieContactFilter, zombieContacts);

        for (int i = 0; i < count; i++)
        {
            Collider2D zombieCollider = zombieContacts[i];
            zombieContacts[i] = null;
            if (zombieCollider == null || !IsZombie(zombieCollider.gameObject)) continue;

            // Commit damage before depenetration so pushing a zombie out of the
            // car can never consume the collision without registering a hit.
            if (speed >= minimumImpactSpeed)
                TryDamageZombie(zombieCollider.gameObject, speed);

            // Zombie bodies are Kinematic, so Unity's solver would otherwise
            // let them push the Dynamic car. Move the authoritative zombie out
            // of the vehicle footprint before the next physics solve instead.
            Rigidbody2D zombieBody = zombieCollider.attachedRigidbody;
            ColliderDistance2D gap = bodyCollider.Distance(zombieCollider);
            if (zombieBody != null && zombieCollider.enabled && gap.isOverlapped)
            {
                Vector2 outward = gap.normal;
                Vector2 centerDelta = zombieCollider.bounds.center - bodyCollider.bounds.center;
                if (outward.sqrMagnitude < 0.001f) outward = centerDelta.normalized;
                if (Vector2.Dot(outward, centerDelta) < 0f) outward = -outward;
                float depth = Mathf.Max(0.02f, -gap.distance + 0.025f);
                zombieBody.position += outward.normalized * depth;
                zombieBody.linearVelocity = Vector2.zero;
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!HasStateAuthority || rb == null) return;
        float speed = rb.linearVelocity.magnitude;
        if (speed < minimumImpactSpeed) return;

        TryDamageZombie(collision.collider.gameObject, speed);
    }

    private bool TryDamageZombie(GameObject target, float speed)
    {
        if (target == null || !IsZombie(target)) return false;
        NetworkObject zombieObject = target.GetComponentInParent<NetworkObject>();
        if (zombieObject == null) return false;
        int id = zombieObject.GetInstanceID();
        if (zombieImpactCooldown.TryGetValue(id, out float allowedAt) && Time.time < allowedAt) return false;

        float lethalSpeed = Mathf.Max(minimumImpactSpeed + 0.1f, maxForwardSpeed * 0.72f);
        float damage = Mathf.Lerp(minimumImpactDamage, maximumImpactDamage,
            Mathf.InverseLerp(minimumImpactSpeed, lethalSpeed, speed));
        PlayerRef driverRef = Driver != null ? Driver.InputAuthority : PlayerRef.None;
        bool hitZombie = false;

        if (target.GetComponentInParent<ZombieAIKhoaRebuilt>() is { } rebuilt)
        {
            rebuilt.RPC_TakeDamage(damage, driverRef);
            hitZombie = true;
        }
        else if (target.GetComponentInParent<ZOmbieAI_Khoa>() is { } legacyKhoa)
        {
            legacyKhoa.RPC_TakeDamage(damage, driverRef);
            hitZombie = true;
        }
        else if (target.GetComponentInParent<ZombieHealth>() is { } thaiZombie)
        {
            thaiZombie.RPC_TakeDamage(damage, driverRef, false);
            hitZombie = true;
        }

        if (!hitZombie) return false;
        zombieImpactCooldown[id] = Time.time + 0.3f;
        Rigidbody2D zombieBody = zombieObject.GetComponent<Rigidbody2D>();
        if (zombieBody != null)
        {
            Vector2 impactDirection = rb != null && rb.linearVelocity.sqrMagnitude > 0.01f
                ? rb.linearVelocity.normalized
                : VisionDirection;
            zombieBody.position += impactDirection * Mathf.Min(0.5f, impactForce * 0.06f);
            zombieBody.linearVelocity = Vector2.zero;
        }
        return true;
    }

    private static bool IsZombie(GameObject target) =>
        target.GetComponentInParent<ZombieAIKhoaRebuilt>() != null ||
        target.GetComponentInParent<ZOmbieAI_Khoa>() != null ||
        target.GetComponentInParent<ZombieHealth>() != null;

    private Vector2 GetAnchorWorldPosition(Transform anchor)
    {
        if (anchor == null) return transform.position;
        Vector2 referenceForward = DirectionIndexToWorldVector(ClampDirectionIndex(initialDirectionIndex));
        Vector2 currentForward = DirectionIndexToWorldVector(ClampDirectionIndex(DirectionIndex));
        float delta = Vector2.SignedAngle(referenceForward, currentForward);
        Vector2 rotatedLocal = Quaternion.Euler(0f, 0f, delta) * (Vector2)anchor.localPosition;
        return transform.TransformPoint(rotatedLocal);
    }

    private void SetVehicleMotionLocked(bool locked)
    {
        if (rb == null) return;
        RigidbodyConstraints2D target = locked
            ? RigidbodyConstraints2D.FreezeAll
            : RigidbodyConstraints2D.FreezeRotation;
        if (rb.constraints != target) rb.constraints = target;
    }

    private static Vector2 HeadingToVector(float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
    }

    private int ActiveDirectionCount =>
        directionLayout == DirectionLayout.EightWayIsometric
            ? EightWayDirection.Count
            : LegacyDirectionCount;

    private int ClampDirectionIndex(int index) =>
        directionLayout == DirectionLayout.EightWayIsometric
            ? EightWayDirection.NormalizeIndex(index)
            : Mathf.Clamp(index, 0, LegacyDirectionCount - 1);

    private float DirectionIndexToHeading(int index) =>
        directionLayout == DirectionLayout.EightWayIsometric
            ? EightWayDirection.IndexToHeadingDegrees(index)
            : VectorToHeading(GridIndexToVector(Mathf.Clamp(index, 0, LegacyDirectionCount - 1)));

    private Vector2 DirectionIndexToWorldVector(int index) =>
        directionLayout == DirectionLayout.EightWayIsometric
            ? EightWayDirection.IndexToIsometricDirection(index, isometricVerticalScale)
            : GridIndexToVector(Mathf.Clamp(index, 0, LegacyDirectionCount - 1));

    private Vector2 HeadingToWorldVector(float degrees)
    {
        Vector2 logicalDirection = HeadingToVector(degrees);
        return directionLayout == DirectionLayout.EightWayIsometric
            ? IsometricMovementProjection.ProjectDirection(logicalDirection, isometricVerticalScale)
            : logicalDirection;
    }

    private int HeadingToDirectionIndex(float headingDegrees) =>
        directionLayout == DirectionLayout.EightWayIsometric
            ? EightWayDirection.HeadingDegreesToIndex(headingDegrees)
            : HeadingToGridIndex(HeadingToVector(headingDegrees));

    private static int HeadingToGridIndex(Vector2 direction)
    {
        int column = Mathf.Clamp(Mathf.RoundToInt(direction.x * 2f) + 2, 0, 4);
        int row = Mathf.Clamp(2 - Mathf.RoundToInt(direction.y * 2f), 0, 4);
        return row * 5 + column;
    }

    private static Vector2 GridIndexToVector(int index)
    {
        Vector2 direction = new(index % 5 - 2, 2 - index / 5);
        return direction == Vector2.zero ? Vector2.up : direction.normalized;
    }

    private static float VectorToHeading(Vector2 direction) => Mathf.Repeat(Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg, 360f);
}
