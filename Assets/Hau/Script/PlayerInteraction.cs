using Fusion;

using UnityEngine;

public class PlayerInteraction : NetworkBehaviour
{
    private VehicleControllerFusion _nearbyCar = null;
    private VehicleControllerFusion _currentCar = null; // Xe đang ngồi trong đó

    [Header("Player Visuals")]
    [SerializeField] private SpriteRenderer _playerSprite; // Kéo Sprite của nhân vật vào đây
    [SerializeField] private Collider2D _playerCollider;   // Kéo Collider của nhân vật vào đây
    [SerializeField] private UnityEngine.Behaviour _playerMovementScript; // Script di chuyển của nhân vật (để tắt đi khi lên xe)

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Vehicle"))
            _nearbyCar = collision.GetComponent<VehicleControllerFusion>();
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Vehicle"))
            _nearbyCar = null;
    }

    private void Update()
    {
        if (HasInputAuthority == false) return;

        // NHẤN E ĐỂ LÊN XE
        if (_nearbyCar != null && _currentCar == null && Input.GetKeyDown(KeyCode.E))
        {
            // Truyền NetworkObject của chính nhân vật này lên Server
            _nearbyCar.RPC_RequestEnterVehicle(Object);
        }

        // NHẤN X ĐỂ XUỐNG XE
        if (_currentCar != null && Input.GetKeyDown(KeyCode.X))
        {
            _currentCar.RPC_RequestExitVehicle(Object);
        }
    }

    // Hàm này được gọi bởi xe (CopCarManager) khi lên/xuống xe thành công
    public void SetInVehicleState(VehicleControllerFusion car, bool inVehicle)
    {
        _currentCar = inVehicle ? car : null;

        // Ẩn/Hiện hình ảnh và collider
        if (_playerSprite != null) _playerSprite.enabled = !inVehicle;
        if (_playerCollider != null) _playerCollider.enabled = !inVehicle;
        if (_playerMovementScript != null) _playerMovementScript.enabled = !inVehicle;

        // Nếu lên xe, di chuyển transform của nhân vật theo xe
        if (inVehicle)
        {
            transform.SetParent(car.transform);
            transform.localPosition = Vector3.zero; // Hoặc vị trí ghế
        }
        else
        {
            transform.SetParent(null); // Tách khỏi xe khi xuống
        }
    }
}