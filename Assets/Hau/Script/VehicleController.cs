using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Rigidbody2D))]
public class VehicleControllerFusion : NetworkBehaviour
{
    private enum SeatSlot { Driver, FrontPassenger, RearLeftPassenger, RearRightPassenger, None }

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
    [SerializeField, Min(0.1f)] private float maxForwardSpeed = 10f;
    [SerializeField, Min(0.1f)] private float maxReverseSpeed = 4f;
    [SerializeField, Min(0.1f)] private float acceleration = 16f;
    [SerializeField, Min(0.1f)] private float braking = 24f;
    [SerializeField, Min(0f)] private float coastDeceleration = 8f;
    [SerializeField, Min(0f)] private float turnSpeed = 150f;
    [SerializeField, Range(0f, 1f)] private float lateralGrip = 0.9f;
    [SerializeField, Range(0f, 1f)] private float steeringAtLowSpeed = 0.15f;
    [SerializeField, Range(0, DirectionCount - 1)] private int initialDirectionIndex = 2;

    [Header("Visuals")]
    [SerializeField] private Sprite[] directionSprites = new Sprite[25];
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator animator;
    [SerializeField] private Camera vehicleCamera;

    [Header("Collision")]
    [SerializeField] private PolygonCollider2D bodyCollider;
    [SerializeField] private CircleCollider2D interactionCollider;

    [Header("Headlights and vehicle vision")]
    [SerializeField] private Light2D headlights;
    [SerializeField, Min(1f)] private float headlightVisionRadius = 12f;
    [SerializeField, Range(20f, 180f)] private float headlightVisionAngle = 85f;
    [SerializeField, Min(0.5f)] private float lightsOffVisionRadius = 2.5f;

    [Header("Zombie collision")]
    [SerializeField, Min(0.1f)] private float minimumImpactSpeed = 2.5f;
    [SerializeField, Min(1f)] private float maximumImpactDamage = 100f;
    [SerializeField, Min(0f)] private float impactForce = 7f;

    [Header("Door audio")]
    [SerializeField] private AudioSource doorAudioSource;
    [SerializeField] private AudioClip doorOpenClip;
    [SerializeField] private AudioClip doorCloseClip;

    [Networked] public NetworkObject Driver { get; private set; }
    [Networked] public NetworkObject FrontPassenger { get; private set; }
    [Networked] public NetworkObject RearLeftPassenger { get; private set; }
    [Networked] public NetworkObject RearRightPassenger { get; private set; }
    [Networked] public NetworkBool IsMoving { get; private set; }
    [Networked] public NetworkBool HeadlightsOn { get; private set; }
    [Networked] public int DirectionIndex { get; private set; }
    [Networked] private float HeadingDegrees { get; set; }

    private const int DirectionCount = 25;
    private Rigidbody2D rb;
    private int displayedDirection = -1;
    private Coroutine doorSequence;
    private readonly Dictionary<int, float> zombieImpactCooldown = new();

    public Vector2 VisionOrigin => transform.position;
    public Vector2 VisionDirection => GridIndexToVector(DirectionIndex);
    public float VisionRadius => HeadlightsOn ? headlightVisionRadius : lightsOffVisionRadius;
    public float VisionAngle => HeadlightsOn ? headlightVisionAngle : 360f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.mass = Mathf.Max(300f, rb.mass);
        spriteRenderer ??= GetComponent<SpriteRenderer>();
        bodyCollider ??= GetComponent<PolygonCollider2D>();
        interactionCollider ??= GetComponent<CircleCollider2D>();
        ConfigureColliders();
        ConfigureHeadlights();
        ConfigureDoorAudio();
        UpdateBodyCollider(initialDirectionIndex);
        SetVehicleMotionLocked(true);
        if (vehicleCamera != null) vehicleCamera.gameObject.SetActive(false);
    }

    public override void Spawned()
    {
        if (!HasStateAuthority) return;
        rb.bodyType = RigidbodyType2D.Dynamic;
        DirectionIndex = Mathf.Clamp(initialDirectionIndex, 0, DirectionCount - 1);
        HeadingDegrees = VectorToHeading(GridIndexToVector(DirectionIndex));
        HeadlightsOn = false;
        UpdateBodyCollider(DirectionIndex);
    }

    public void RequestEnter(NetworkObject player)
    {
        if (player != null) RPC_RequestEnter(player);
    }

    public bool AuthorityTryEnter(NetworkObject player)
    {
        if (!HasStateAuthority || player == null) return false;
        PlayerInteraction interaction = player.GetComponent<PlayerInteraction>();
        if (interaction == null || interaction.IsInVehicle) return false;

        SeatSlot slot = FindDoorNear(player.transform.position);
        if (slot == SeatSlot.None) return false;
        AssignSeat(slot, player);
        return true;
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

        SetOccupant(slot, null);
        SetPlayerVehicleState(player, false, false);
        PlaceAtExit(player, slot);
        if (slot == SeatSlot.Driver) StopVehicle();
        RPC_PlayDoorSequence();
        return true;
    }

    public void RequestSeatChange(NetworkObject player, int seatNumber)
    {
        if (player != null && seatNumber >= 1 && seatNumber <= 4)
            RPC_RequestSeatChange(player, seatNumber);
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
        SetPlayerVehicleState(player, true, target == SeatSlot.Driver);
        MoveToSeat(player, GetAnchorWorldPosition(GetSeatAnchor(target)));

        if (current == SeatSlot.Driver && target != SeatSlot.Driver)
            StopVehicle();
        if (target == SeatSlot.Driver)
            SetVehicleMotionLocked(false);
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
            StopVehicle();
            SetVehicleMotionLocked(true);
            return;
        }

        SetVehicleMotionLocked(false);
        PlayerMovement driverMovement = Driver.GetComponent<PlayerMovement>();
        SimulateDrive(driverMovement != null ? driverMovement.NetMoveInput : Vector2.zero);
        SyncOccupants();
    }

    public override void Render()
    {
        ApplyDirectionalVisual();
        ApplyHeadlightVisual();
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
        SetPlayerVehicleState(player, true, slot == SeatSlot.Driver);
        MoveToSeat(player, GetAnchorWorldPosition(GetSeatAnchor(slot)));
        if (slot == SeatSlot.Driver) SetVehicleMotionLocked(false);
        RPC_PlayDoorSequence();
    }

    private void SetPlayerVehicleState(NetworkObject player, bool inVehicle, bool isDriver)
    {
        PlayerInteraction interaction = player.GetComponent<PlayerInteraction>();
        if (interaction != null) interaction.SetVehicleNetworkState(Object, inVehicle, isDriver);
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
        else player.transform.position = seatPosition;
    }

    private void PlaceAtExit(NetworkObject player, SeatSlot slot)
    {
        Transform point = GetExitPoint(slot) ?? GetEnterPoint(slot) ?? transform;
        Vector2 exitPosition = GetAnchorWorldPosition(point);
        Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
        if (playerBody != null) playerBody.position = exitPosition;
        else player.transform.position = exitPosition;
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

    private void SimulateDrive(Vector2 input)
    {
        Vector2 forward = HeadingToVector(HeadingDegrees);
        float forwardSpeed = Vector2.Dot(rb.linearVelocity, forward);
        float throttle = Mathf.Clamp(input.y, -1f, 1f);
        float targetSpeed = throttle >= 0f ? throttle * maxForwardSpeed : throttle * maxReverseSpeed;
        float rate = Mathf.Abs(targetSpeed) > Mathf.Abs(forwardSpeed) ? acceleration : braking;
        if (Mathf.Abs(throttle) < 0.01f) targetSpeed = Mathf.MoveTowards(forwardSpeed, 0f, coastDeceleration * Runner.DeltaTime);
        forwardSpeed = Mathf.MoveTowards(forwardSpeed, targetSpeed, rate * Runner.DeltaTime);
        Vector2 lateral = rb.linearVelocity - forward * Vector2.Dot(rb.linearVelocity, forward);
        rb.linearVelocity = forward * forwardSpeed + lateral * (1f - lateralGrip);
        float speedFactor = Mathf.Lerp(steeringAtLowSpeed, 1f, Mathf.Clamp01(Mathf.Abs(forwardSpeed) / maxForwardSpeed));
        HeadingDegrees = Mathf.Repeat(HeadingDegrees + input.x * turnSpeed * speedFactor * (forwardSpeed < -0.01f ? -1f : 1f) * Runner.DeltaTime, 360f);
        DirectionIndex = HeadingToGridIndex(HeadingToVector(HeadingDegrees));
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
        if (directionSprites != null && directionSprites.Length == DirectionCount && directionSprites[DirectionIndex] != null)
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
        int index = Mathf.Clamp(directionIndex, 0, DirectionCount - 1);
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
            bodyCollider.SetPath(path, points);
        }
    }

    private void ConfigureHeadlights()
    {
        if (headlights == null)
        {
            GameObject lightObject = new("Vehicle Headlights");
            lightObject.transform.SetParent(transform, false);
            headlights = lightObject.AddComponent<Light2D>();
        }

        headlights.lightType = Light2D.LightType.Point;
        headlights.pointLightInnerRadius = 0.25f;
        headlights.pointLightOuterRadius = headlightVisionRadius;
        headlights.pointLightInnerAngle = Mathf.Max(10f, headlightVisionAngle - 20f);
        headlights.pointLightOuterAngle = headlightVisionAngle;
        headlights.intensity = 1.15f;
        headlights.color = new Color(1f, 0.91f, 0.68f, 1f);
        headlights.gameObject.SetActive(false);
    }

    private void ApplyHeadlightVisual()
    {
        if (headlights == null) return;
        bool enabled = HeadlightsOn;
        if (headlights.gameObject.activeSelf != enabled)
            headlights.gameObject.SetActive(enabled);

        Vector2 forward = VisionDirection;
        float angle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;
        headlights.transform.localPosition = forward * 0.48f;
        headlights.transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void ConfigureDoorAudio()
    {
        doorAudioSource ??= GetComponent<AudioSource>();
        if (doorAudioSource == null) doorAudioSource = gameObject.AddComponent<AudioSource>();
        GameplayAudioSpatializer.Configure(doorAudioSource, GameplayAudioSpatializer.Profile.Body);
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
            doorAudioSource.PlayOneShot(doorOpenClip);
            yield return new WaitForSeconds(doorOpenClip.length);
        }
        if (doorCloseClip != null) doorAudioSource.PlayOneShot(doorCloseClip);
        doorSequence = null;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!HasStateAuthority || rb == null) return;
        float speed = rb.linearVelocity.magnitude;
        if (speed < minimumImpactSpeed) return;

        GameObject target = collision.collider.gameObject;
        NetworkObject zombieObject = target.GetComponentInParent<NetworkObject>();
        if (zombieObject == null) return;
        int id = zombieObject.GetInstanceID();
        if (zombieImpactCooldown.TryGetValue(id, out float allowedAt) && Time.time < allowedAt) return;

        float damage = Mathf.Lerp(15f, maximumImpactDamage,
            Mathf.InverseLerp(minimumImpactSpeed, maxForwardSpeed, speed));
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

        if (!hitZombie) return;
        zombieImpactCooldown[id] = Time.time + 0.45f;
        Rigidbody2D zombieBody = zombieObject.GetComponent<Rigidbody2D>();
        if (zombieBody != null)
            zombieBody.AddForce(VisionDirection * impactForce, ForceMode2D.Impulse);
    }

    private Vector2 GetAnchorWorldPosition(Transform anchor)
    {
        if (anchor == null) return transform.position;
        Vector2 referenceForward = GridIndexToVector(Mathf.Clamp(initialDirectionIndex, 0, DirectionCount - 1));
        Vector2 currentForward = GridIndexToVector(Mathf.Clamp(DirectionIndex, 0, DirectionCount - 1));
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
