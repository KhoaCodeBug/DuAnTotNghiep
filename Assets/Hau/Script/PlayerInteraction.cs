using UnityEngine;
using Fusion;

public class PlayerInteraction : NetworkBehaviour
{
    public float interactRange = 3f;

    private VehicleControllerFusion nearbyVehicle;
    private VehicleControllerFusion currentVehicle;

    private bool isInVehicle = false;
    private MonoBehaviour movementScript;

    void Start()
    {
        movementScript = GetComponent<PlayerMovement>();
    }

    void Update()
    {
        if (!Object.HasInputAuthority) return;

        CheckNearbyVehicle();

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (!isInVehicle)
                TryEnterVehicle();
            else
                ExitVehicle();
        }
    }

    void CheckNearbyVehicle()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, interactRange);

        VehicleControllerFusion foundVehicle = null;

        foreach (var hit in hits)
        {
            var vehicle = hit.GetComponentInParent<VehicleControllerFusion>();
            if (vehicle != null)
            {
                foundVehicle = vehicle;
                break;
            }
        }

        if (foundVehicle != null && nearbyVehicle == null)
        {
            nearbyVehicle = foundVehicle;
            Debug.Log("[NEAR VEHICLE] " + foundVehicle.name);
        }

        if (foundVehicle == null && nearbyVehicle != null)
        {
            nearbyVehicle = null;
            Debug.Log("[LEFT VEHICLE]");
        }
    }

    void TryEnterVehicle()
    {
        if (nearbyVehicle == null)
        {
            Debug.Log("No vehicle nearby");
            return;
        }

        nearbyVehicle.RequestEnter(Object);
    }

    void ExitVehicle()
    {
        if (currentVehicle != null)
        {
            currentVehicle.RequestExit(Object);
        }
    }

    public void SetVehicle(VehicleControllerFusion vehicle, bool enter, bool isDriver)
    {
        isInVehicle = enter;
        currentVehicle = enter ? vehicle : null;

        // 🔥 FIX: CHỈ LOCAL PLAYER ĐƯỢC ĐỔI CAMERA
        if (Object.HasInputAuthority && vehicle != null)
        {
            vehicle.SetCamera(isDriver);
        }

        var anim = GetComponent<Animator>();
        if (anim != null)
            anim.SetBool("isSitting", enter);

        if (movementScript != null)
            movementScript.enabled = !enter;

        Transform tag = transform.Find("NameTag");
        if (tag != null)
            tag.gameObject.SetActive(!enter);

        var sprite = GetComponent<SpriteRenderer>();
        if (sprite != null)
            sprite.enabled = !enter;
    }
}