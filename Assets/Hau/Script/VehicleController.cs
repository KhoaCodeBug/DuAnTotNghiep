using Fusion;
using UnityEngine;

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

    [Networked] public NetworkObject Driver { get; private set; }
    [Networked] public NetworkObject FrontPassenger { get; private set; }
    [Networked] public NetworkObject RearLeftPassenger { get; private set; }
    [Networked] public NetworkObject RearRightPassenger { get; private set; }
    [Networked] public NetworkBool IsMoving { get; private set; }
    [Networked] public int DirectionIndex { get; private set; }
    [Networked] private float HeadingDegrees { get; set; }

    private const int DirectionCount = 25;
    private Rigidbody2D rb;
    private int displayedDirection = -1;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        spriteRenderer ??= GetComponent<SpriteRenderer>();
        if (vehicleCamera != null) vehicleCamera.gameObject.SetActive(false);
    }

    public override void Spawned()
    {
        if (!HasStateAuthority) return;
        DirectionIndex = Mathf.Clamp(initialDirectionIndex, 0, DirectionCount - 1);
        HeadingDegrees = VectorToHeading(GridIndexToVector(DirectionIndex));
    }

    public void RequestEnter(NetworkObject player)
    {
        if (player != null) RPC_RequestEnter(player);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestEnter(NetworkObject player, RpcInfo info = default)
    {
        if (player == null || player.InputAuthority != info.Source) return;
        PlayerInteraction interaction = player.GetComponent<PlayerInteraction>();
        if (interaction == null || interaction.IsInVehicle) return;

        SeatSlot slot = FindDoorNear(player.transform.position);
        if (slot == SeatSlot.None || GetOccupant(slot) != null) return;
        AssignSeat(slot, player);
    }

    public void RequestExit(NetworkObject player)
    {
        if (player != null) RPC_RequestExit(player);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestExit(NetworkObject player, RpcInfo info = default)
    {
        if (player == null || player.InputAuthority != info.Source) return;
        SeatSlot slot = FindOccupiedSeat(player);
        if (slot == SeatSlot.None) return;

        SetOccupant(slot, null);
        SetPlayerVehicleState(player, false, false);
        PlaceAtExit(player, slot);
        if (slot == SeatSlot.Driver) StopVehicle();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        if (Driver == null)
        {
            StopVehicle();
            return;
        }

        PlayerMovement driverMovement = Driver.GetComponent<PlayerMovement>();
        SimulateDrive(driverMovement != null ? driverMovement.NetMoveInput : Vector2.zero);
        SyncOccupants();
    }

    public override void Render() => ApplyDirectionalVisual();

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
            float distance = Vector2.Distance(playerPosition, point.position);
            if (distance <= enterDistance && distance < closestDistance)
            {
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
        MoveToSeat(player, GetSeatAnchor(slot));
    }

    private void SetPlayerVehicleState(NetworkObject player, bool inVehicle, bool isDriver)
    {
        PlayerInteraction interaction = player.GetComponent<PlayerInteraction>();
        if (interaction != null) interaction.SetVehicleNetworkState(Object, inVehicle, isDriver);
    }

    private void SyncOccupants()
    {
        MoveToSeat(Driver, driverSeat);
        MoveToSeat(FrontPassenger, frontPassengerSeat);
        MoveToSeat(RearLeftPassenger, rearLeftSeat);
        MoveToSeat(RearRightPassenger, rearRightSeat);
    }

    private static void MoveToSeat(NetworkObject player, Transform seat)
    {
        if (player == null || seat == null) return;
        Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
        if (playerBody != null) playerBody.position = seat.position;
        else player.transform.position = seat.position;
    }

    private void PlaceAtExit(NetworkObject player, SeatSlot slot)
    {
        Transform point = GetExitPoint(slot) ?? GetEnterPoint(slot) ?? transform;
        Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
        if (playerBody != null) playerBody.position = point.position;
        else player.transform.position = point.position;
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
