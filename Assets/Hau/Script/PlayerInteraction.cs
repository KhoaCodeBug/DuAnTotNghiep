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
        CheckNearbyVehicle(); // ✅ DEBUG khoảng cách
        if (Input.GetKeyDown(KeyCode.E))
        {
            Debug.Log("Pressed E");

            if (!isInVehicle)
            {
                TryEnterVehicle();
            }
            else
            {
                ExitVehicle();
            }
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

        // ✅ Khi vừa vào vùng xe
        if (foundVehicle != null && nearbyVehicle == null)
        {
            nearbyVehicle = foundVehicle;
            Debug.Log("[TEST] Player NEAR vehicle: " + foundVehicle.name);
        }

        // ✅ Khi rời khỏi vùng xe
        if (foundVehicle == null && nearbyVehicle != null)
        {
            Debug.Log("[TEST] Player LEFT vehicle range");
            nearbyVehicle = null;
        }
    }
    void TryEnterVehicle()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, interactRange);

        Debug.Log("Hit count: " + hits.Length);

        foreach (var hit in hits)
        {
            Debug.Log("Hit: " + hit.name);

            var vehicle = hit.GetComponentInParent<VehicleControllerFusion>();
            if (vehicle != null)
            {
                Debug.Log("Calling RPC Enter");
                vehicle.RequestEnter(Object);
                return;
            }
        }

        Debug.Log("No vehicle found");
    }

    void ExitVehicle()
    {
        if (currentVehicle != null)
        {
            Debug.Log("Request Exit");
            currentVehicle.RequestExit(Object);
        }
    }

    // ================= STATE =================
    public void SetVehicle(VehicleControllerFusion vehicle, bool enter, bool isDriver)
    {
        isInVehicle = enter;
        currentVehicle = enter ? vehicle : null;

        if (vehicle != null)
            vehicle.SetCamera(isDriver);

       
        var anim = GetComponent<Animator>();
        if (anim != null)
            anim.SetBool("isSitting", enter);

       
        if (movementScript != null)
            movementScript.enabled = !enter;

        
        Transform tag = transform.Find("NameTag");
        if (tag != null)
            tag.gameObject.SetActive(!enter);

       
        SpriteRenderer sprite = GetComponent<SpriteRenderer>();
        if (sprite != null)
        {
            sprite.enabled = !enter;
        }

       
    }
}