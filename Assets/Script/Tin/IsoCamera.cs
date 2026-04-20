using UnityEngine;
using UnityEngine.EventSystems;

public class PZ_CameraController : MonoBehaviour
{
    [Header("--- Target Setup ---")]
    private Transform player;
    private bool hasTarget = false;

    public Vector3 offset = new Vector3(0, 0, -10f);

    [Header("--- PZ Camera Panning ---")]
    public float smoothFollow = 0.12f;
    public float maxLookAhead = 6f;

    [Header("--- Orthographic Zoom ---")]
    public float zoomSpeed = 15f;
    public float zoomSmoothTime = 0.05f;
    public float minZoomSize = 5f;
    public float maxZoomSize = 14f;

    private Camera cam;
    private Vector3 velocity;
    private float targetZoom;
    private float zoomVelocity;

    // 🔥 MỚI: Biến đánh dấu camera có đang ở chế độ xem ké người khác không
    public bool isSpectatingMode { get; private set; } = false;

    public void SetTarget(Transform targetTransform)
    {
        player = targetTransform;
        hasTarget = true;
        isSpectatingMode = false; // Khi mới vào game, đặt lại là không spectate
        transform.position = player.position + offset;
    }

    // 🔥 MỚI: Hàm riêng dùng để chuyển góc nhìn sang người khác
    public void SpectateTarget(Transform targetTransform)
    {
        player = targetTransform;
        hasTarget = true;
        isSpectatingMode = true;
    }

    void Start()
    {
        cam = GetComponentInChildren<Camera>();
        if (!cam.orthographic) cam.orthographic = true;
        targetZoom = cam.orthographicSize;
    }

    void Update()
    {
        if (!hasTarget || player == null) return;
        HandleZoom();
    }

    void LateUpdate()
    {
        if (!hasTarget || player == null) return;
        HandleCameraFollowAndPan();
    }

    private bool IsPlayerBusyWithUI()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return true;
        if (AutoUIManager.Instance != null && AutoUIManager.Instance.IsInventoryOpen()) return true;
        if (AutoHealthPanel.Instance != null && AutoHealthPanel.Instance.IsOpen) return true;
        if (AutoChatManager.Instance != null && AutoChatManager.Instance.IsTyping()) return true;
        return false;
    }

    private void HandleCameraFollowAndPan()
    {
        Vector3 targetPos = player.position + offset;

        // KHÔNG cho phép lia camera (LookAhead) nếu đang ở chế độ Spectator
        if (Input.GetMouseButton(1) && !IsPlayerBusyWithUI() && !isSpectatingMode)
        {
            Vector3 mouseWorldPos = cam.ScreenToWorldPoint(Input.mousePosition);
            mouseWorldPos.z = player.position.z;

            Vector3 directionToMouse = mouseWorldPos - player.position;
            Vector3 panOffset = Vector3.ClampMagnitude(directionToMouse, maxLookAhead);

            targetPos += (panOffset / 2f);
        }

        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPos,
            ref velocity,
            smoothFollow,
            Mathf.Infinity,
            Time.deltaTime
        );
    }

    private void HandleZoom()
    {
        if (!Application.isFocused) return;
        Vector3 mousePos = Input.mousePosition;
        if (mousePos.x < 0 || mousePos.y < 0 || mousePos.x > Screen.width || mousePos.y > Screen.height) return;

        float scroll = 0f;

        if (!IsPlayerBusyWithUI())
        {
            scroll = Input.GetAxis("Mouse ScrollWheel");
            scroll = Mathf.Clamp(scroll, -0.2f, 0.2f);
        }

        if (scroll != 0)
        {
            targetZoom -= scroll * zoomSpeed;
            targetZoom = Mathf.Clamp(targetZoom, minZoomSize, maxZoomSize);
        }

        cam.orthographicSize = Mathf.SmoothDamp(
            cam.orthographicSize,
            targetZoom,
            ref zoomVelocity,
            zoomSmoothTime,
            Mathf.Infinity,
            Time.deltaTime
        );
    }
}