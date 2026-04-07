using UnityEngine;
using Fusion;

public class PlayerInteraction : NetworkBehaviour
{
    public float interactRange = 2f;

    private VehicleControllerFusion currentVehicle;
    private bool isInVehicle = false;

    void Update()
    {
        if (!Object.HasInputAuthority) return;

        if (Input.GetKeyDown(KeyCode.E))
        {
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

    void TryEnterVehicle()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, interactRange);

        foreach (var hit in hits)
        {
            var vehicle = hit.GetComponent<VehicleControllerFusion>();
            if (vehicle != null)
            {
                vehicle.RequestEnter(Object);
                break;
            }
        }
    }

    void ExitVehicle()
    {
        if (currentVehicle != null)
        {
            currentVehicle.RequestExit(Object);
        }
    }

    // ================= STATE =================
    public void SetVehicle(VehicleControllerFusion vehicle, bool enter)
    {
        isInVehicle = enter;
        currentVehicle = enter ? vehicle : null;

        // bật/tắt camera xe
        if (vehicle != null)
            vehicle.SetCamera(enter);

        // ẩn player khi vào xe
        GetComponent<SpriteRenderer>().enabled = !enter;
        GetComponent<Collider2D>().enabled = !enter;
    }
}