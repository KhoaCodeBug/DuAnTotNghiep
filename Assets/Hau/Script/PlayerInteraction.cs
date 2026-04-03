using Fusion;
using UnityEngine;

public class PlayerInteraction : NetworkBehaviour
{
    private VehicleControllerFusion _nearbyCar = null;
    private VehicleControllerFusion _currentCar;

    [SerializeField] private PZ_CameraController _cameraController;

    [Header("Player Visuals")]
    [SerializeField] private SpriteRenderer _playerSprite;
    [SerializeField] private Collider2D _playerCollider;
    [SerializeField] private UnityEngine.Behaviour _playerMovementScript;

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

        if (_nearbyCar != null && _currentCar == null && Input.GetKeyDown(KeyCode.E))
        {
            _nearbyCar.RPC_RequestEnterVehicle(Object);
        }

        if (_currentCar != null && Input.GetKeyDown(KeyCode.X))
        {
            _currentCar.RPC_RequestExitVehicle(Object);
        }
    }

    public void SetInVehicleState(VehicleControllerFusion car, bool inVehicle)
    {
        _currentCar = inVehicle ? car : null;

        if (_playerSprite != null) _playerSprite.enabled = !inVehicle;
        if (_playerCollider != null) _playerCollider.enabled = !inVehicle;
        if (_playerMovementScript != null) _playerMovementScript.enabled = !inVehicle;

        if (inVehicle)
        {
            transform.position = car.transform.position;

            // 🔥 dùng follow point
            _cameraController.SetTarget(car.CameraFollowPoint);
            _cameraController.SetVehicleMode(true);
        }
        else
        {
            transform.SetParent(null);

            _cameraController.SetTarget(transform);
            _cameraController.SetVehicleMode(false);
        }
    }

    // 🔥 PHẢI NẰM TRONG CLASS
    private void LateUpdate()
    {
        if (_currentCar != null)
        {
            transform.position = _currentCar.transform.position;
        }
    }
}