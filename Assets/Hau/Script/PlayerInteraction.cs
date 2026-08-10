using Fusion;
using UnityEngine;

public class PlayerInteraction : NetworkBehaviour
{
    [SerializeField] private float interactRange = 3f;

    [Networked] private NetworkBool NetworkIsInVehicle { get; set; }
    [Networked] private NetworkBool NetworkIsVehicleDriver { get; set; }
    [Networked] public NetworkObject CurrentVehicle { get; private set; }

    public bool IsInVehicle => NetworkIsInVehicle;
    private VehicleControllerFusion nearbyVehicle;
    private VehicleControllerFusion currentVehicle;
    private Rigidbody2D body;
    private SpriteRenderer sprite;
    private Transform nameTag;
    private bool presentationApplied;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        sprite = GetComponent<SpriteRenderer>();
        nameTag = transform.Find("NameTag");
    }

    private void Update()
    {
        if (!Object.HasInputAuthority) return;
        PlayerSurvival survival = GetComponent<PlayerSurvival>();
        if (survival != null && survival.IsSleepInputLocked) return;
        CheckNearbyVehicle();
        if (!Input.GetKeyDown(KeyCode.F)) return;
        if (NetworkIsInVehicle) currentVehicle?.RequestExit(Object);
        else nearbyVehicle?.RequestEnter(Object);
    }

    public override void Render() => ApplyVehiclePresentation();

    public void SetVehicleNetworkState(NetworkObject vehicleObject, bool inVehicle, bool isDriver)
    {
        if (!HasStateAuthority) return;
        CurrentVehicle = inVehicle ? vehicleObject : null;
        NetworkIsInVehicle = inVehicle;
        NetworkIsVehicleDriver = inVehicle && isDriver;
        SetPhysicsEnabled(!inVehicle);
    }

    private void CheckNearbyVehicle()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, interactRange);
        nearbyVehicle = null;
        foreach (Collider2D hit in hits)
        {
            VehicleControllerFusion vehicle = hit.GetComponentInParent<VehicleControllerFusion>();
            if (vehicle != null) { nearbyVehicle = vehicle; break; }
        }
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
        VehicleControllerFusion vehicle = CurrentVehicle != null ? CurrentVehicle.GetComponent<VehicleControllerFusion>() : null;
        bool shouldApply = NetworkIsInVehicle;
        if (presentationApplied == shouldApply && currentVehicle == vehicle) return;

        VehicleControllerFusion previousVehicle = currentVehicle;
        presentationApplied = shouldApply;
        currentVehicle = vehicle;
        if (sprite != null) sprite.enabled = !shouldApply;
        if (nameTag != null) nameTag.gameObject.SetActive(!shouldApply);
        Animator playerAnimator = GetComponent<Animator>();
        if (playerAnimator != null) playerAnimator.SetBool("isSitting", shouldApply);

        if (!Object.HasInputAuthority) return;
        if (!shouldApply) previousVehicle?.SetCamera(false);
        else if (vehicle != null) vehicle.SetCamera(NetworkIsVehicleDriver);
    }
}
