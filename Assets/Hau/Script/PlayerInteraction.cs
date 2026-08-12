using Fusion;
using UnityEngine;

public class PlayerInteraction : NetworkBehaviour
{
    [SerializeField] private float interactRange = 3f;

    [Networked] private NetworkBool NetworkIsInVehicle { get; set; }
    [Networked] private NetworkBool NetworkIsVehicleDriver { get; set; }
    [Networked] public int CurrentSeatNumber { get; private set; }
    [Networked] public NetworkObject CurrentVehicle { get; private set; }
    [Networked] public Vector2 LastVehicleExitPosition { get; private set; }
    [Networked] private NetworkBool HasVehicleExitPosition { get; set; }

    public bool IsInVehicle => NetworkIsInVehicle;
    public bool IsVehicleDriver => NetworkIsVehicleDriver;
    public static bool IsProtectedOccupant(PlayerHealth health)
    {
        if (health == null) return false;
        PlayerInteraction interaction = health.GetComponent<PlayerInteraction>();
        return interaction != null && interaction.IsInVehicle;
    }
    public VehicleControllerFusion CurrentVehicleController =>
        CurrentVehicle != null ? CurrentVehicle.GetComponent<VehicleControllerFusion>() : null;
    private VehicleControllerFusion nearbyVehicle;
    private VehicleControllerFusion currentVehicle;
    private Rigidbody2D body;
    private SpriteRenderer sprite;
    private Transform nameTag;
    private Transform muzzleFlash;
    private Animator playerAnimator;
    private bool hasSittingParameter;
    private bool presentationApplied;
    private bool driverPresentationApplied;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        sprite = GetComponent<SpriteRenderer>();
        nameTag = transform.Find("NameTagCanvas");
        muzzleFlash = transform.Find("MuzzleFlash");
        playerAnimator = GetComponent<Animator>();
        if (playerAnimator != null)
        {
            foreach (AnimatorControllerParameter parameter in playerAnimator.parameters)
            {
                if (parameter.type != AnimatorControllerParameterType.Bool || parameter.name != "isSitting") continue;
                hasSittingParameter = true;
                break;
            }
        }
    }

    private void Update()
    {
        if (!Object.HasInputAuthority) return;
        PlayerSurvival survival = GetComponent<PlayerSurvival>();
        if (survival != null && survival.IsSleepInputLocked) return;
        CheckNearbyVehicle();

        if (NetworkIsInVehicle && NetworkIsVehicleDriver && Input.GetKeyDown(KeyCode.L))
        {
            if (CurrentVehicle != null) RPC_RequestToggleHeadlights(CurrentVehicle);
            return;
        }

        if (NetworkIsInVehicle && IsSeatModifierHeld())
        {
            int requestedSeat = GetRequestedSeatNumber();
            if (requestedSeat > 0)
            {
                if (CurrentVehicle != null) RPC_RequestSeatChange(CurrentVehicle, requestedSeat);
                return;
            }
        }

        if (!Input.GetKeyDown(KeyCode.F)) return;
        if (NetworkIsInVehicle)
        {
            if (CurrentVehicle != null) RPC_RequestExitVehicle(CurrentVehicle);
        }
        else
        {
            if (nearbyVehicle == null) nearbyVehicle = FindClosestVehicleFallback();
            if (nearbyVehicle != null) RPC_RequestEnterVehicle(nearbyVehicle.Object);
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestEnterVehicle(NetworkObject vehicleObject)
    {
        VehicleControllerFusion vehicle = vehicleObject != null
            ? vehicleObject.GetComponent<VehicleControllerFusion>()
            : null;
        vehicle?.AuthorityTryEnter(Object);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestExitVehicle(NetworkObject vehicleObject)
    {
        VehicleControllerFusion vehicle = vehicleObject != null
            ? vehicleObject.GetComponent<VehicleControllerFusion>()
            : null;
        vehicle?.AuthorityTryExit(Object);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestSeatChange(NetworkObject vehicleObject, int seatNumber)
    {
        VehicleControllerFusion vehicle = vehicleObject != null
            ? vehicleObject.GetComponent<VehicleControllerFusion>()
            : null;
        vehicle?.AuthorityTryChangeSeat(Object, seatNumber);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestToggleHeadlights(NetworkObject vehicleObject)
    {
        VehicleControllerFusion vehicle = vehicleObject != null
            ? vehicleObject.GetComponent<VehicleControllerFusion>()
            : null;
        vehicle?.AuthorityTryToggleHeadlights(Object);
    }

    public override void Render() => ApplyVehiclePresentation();

    private void LateUpdate()
    {
        if (Object != null && Object.IsValid) ApplyVehiclePresentation();
    }

    public void SetVehicleNetworkState(
        NetworkObject vehicleObject,
        bool inVehicle,
        bool isDriver,
        int seatNumber,
        Vector2 exitPosition)
    {
        if (!HasStateAuthority) return;
        if (!inVehicle)
        {
            LastVehicleExitPosition = exitPosition;
            HasVehicleExitPosition = true;
        }
        else
        {
            HasVehicleExitPosition = false;
        }
        CurrentVehicle = inVehicle ? vehicleObject : null;
        NetworkIsInVehicle = inVehicle;
        NetworkIsVehicleDriver = inVehicle && isDriver;
        CurrentSeatNumber = inVehicle ? Mathf.Clamp(seatNumber, 1, 4) : 0;
        SetPhysicsEnabled(!inVehicle);
    }

    private void OnGUI()
    {
        if (Object == null || !Object.IsValid || !Object.HasInputAuthority || !NetworkIsInVehicle) return;

        string seatName = CurrentSeatNumber switch
        {
            1 => GameLocalization.Get("vehicle.seat.driver"),
            2 => GameLocalization.Get("vehicle.seat.front"),
            3 => GameLocalization.Get("vehicle.seat.rear_left"),
            4 => GameLocalization.Get("vehicle.seat.rear_right"),
            _ => GameLocalization.Get("vehicle.seat.unknown")
        };
        string message = string.Format(GameLocalization.Get("vehicle.seat.status"), CurrentSeatNumber, seatName);
        GUIStyle style = new(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
        GUI.Box(new Rect(Screen.width * 0.5f - 360f, 58f, 720f, 38f), message, style);
    }

    private void CheckNearbyVehicle()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, interactRange);
        nearbyVehicle = null;
        float closestDistance = float.MaxValue;
        foreach (Collider2D hit in hits)
        {
            VehicleControllerFusion vehicle = hit.GetComponentInParent<VehicleControllerFusion>();
            if (vehicle == null) continue;
            float distance = Vector2.Distance(transform.position, hit.ClosestPoint(transform.position));
            if (distance >= closestDistance) continue;
            closestDistance = distance;
            nearbyVehicle = vehicle;
        }
    }

    private VehicleControllerFusion FindClosestVehicleFallback()
    {
        VehicleControllerFusion closest = null;
        float closestDistance = interactRange;
        foreach (VehicleControllerFusion vehicle in FindObjectsByType<VehicleControllerFusion>(FindObjectsSortMode.None))
        {
            if (vehicle == null) continue;
            Collider2D trigger = vehicle.GetComponent<CircleCollider2D>();
            Vector2 closestPoint = trigger != null
                ? trigger.ClosestPoint(transform.position)
                : (Vector2)vehicle.transform.position;
            float distance = Vector2.Distance(transform.position, closestPoint);
            if (distance > closestDistance) continue;
            closestDistance = distance;
            closest = vehicle;
        }
        return closest;
    }

    private void SetPhysicsEnabled(bool enabled)
    {
        if (body == null) return;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.simulated = enabled;
    }

    private void ApplyVehiclePresentation()
    {
        VehicleControllerFusion vehicle = CurrentVehicleController;
        bool shouldApply = NetworkIsInVehicle;
        bool shouldDrive = shouldApply && NetworkIsVehicleDriver;
        if (presentationApplied == shouldApply && driverPresentationApplied == shouldDrive && currentVehicle == vehicle) return;

        VehicleControllerFusion previousVehicle = currentVehicle;
        presentationApplied = shouldApply;
        driverPresentationApplied = shouldDrive;
        currentVehicle = vehicle;
        if (sprite != null) sprite.enabled = !shouldApply;
        if (nameTag != null) nameTag.gameObject.SetActive(!shouldApply);
        if (muzzleFlash != null) muzzleFlash.gameObject.SetActive(!shouldApply);
        if (playerAnimator != null && hasSittingParameter)
            playerAnimator.SetBool("isSitting", shouldApply);
        SetPhysicsEnabled(!shouldApply);

        if (!Object.HasInputAuthority) return;
        if (!shouldApply)
        {
            if (HasVehicleExitPosition)
            {
                transform.position = LastVehicleExitPosition;
                if (body != null)
                {
                    body.position = LastVehicleExitPosition;
                    body.linearVelocity = Vector2.zero;
                    body.angularVelocity = 0f;
                }
                Physics2D.SyncTransforms();
            }
            previousVehicle?.SetCamera(false);
        }
        else if (vehicle != null) vehicle.SetCamera(true);
    }

    private static bool IsSeatModifierHeld() =>
        Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

    private static int GetRequestedSeatNumber()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) return 1;
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) return 2;
        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) return 3;
        if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) return 4;
        return 0;
    }
}
