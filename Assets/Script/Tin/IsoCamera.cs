using UnityEngine;
using UnityEngine.EventSystems; // 🔥 Thêm thư viện này để check chuột trên UI

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

    public void SetTarget(Transform targetTransform)
    {
        // Chốt đơn: Bám thẳng vào cục mục tiêu, không lùng sục rườm rà nữa!
        player = targetTransform;
        hasTarget = true;
        transform.position = player.position + offset;
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

    // ==========================================
    // 🔥 HÀM TỔNG QUẢN: CHECK XEM CÓ ĐANG BẬN DÙNG UI KHÔNG
    // ==========================================
    private bool IsPlayerBusyWithUI()
    {
        // 1. Chuột có đang nằm trên bất kỳ UI nào không? (Bảng máu, Nút bấm, Bảng Trade...)
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return true;

        // 2. Có đang mở túi đồ không?
        if (AutoUIManager.Instance != null && AutoUIManager.Instance.IsInventoryOpen()) return true;

        // 3. Có đang mở bảng Máu không?
        if (AutoHealthPanel.Instance != null && AutoHealthPanel.Instance.IsOpen) return true;

        // 4. Có đang gõ phím trong khung Chat không?
        if (AutoChatManager.Instance != null && AutoChatManager.Instance.IsTyping()) return true;

        // Nếu không dính cái nào ở trên -> Cho phép Camera hoạt động bình thường!
        return false;
    }

    private void HandleCameraFollowAndPan()
    {
        Vector3 targetPos = player.position + offset;

        // 🔥 NẾU ĐANG BẤM CHUỘT PHẢI VÀ KHÔNG BẬN DÙNG UI -> CHO PHÉP LOOKAHEAD
        if (Input.GetMouseButton(1) && !IsPlayerBusyWithUI())
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

        // 🔥 NẾU KHÔNG BẬN DÙNG UI (Không chat, không mở bảng, không rê chuột lên UI) -> CHO PHÉP ZOOM
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