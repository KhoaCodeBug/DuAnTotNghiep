using UnityEngine;

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

        // (Đã xóa hết mấy dòng Debug lải nhải vụ giật lag)
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

    // Camera bám đuôi thì bắt buộc phải để ở LateUpdate để chạy sau cùng
    void LateUpdate()
    {
        if (!hasTarget || player == null) return;
        HandleCameraFollowAndPan();
    }

    private void HandleCameraFollowAndPan()
    {
        Vector3 targetPos = player.position + offset;

        if (Input.GetMouseButton(1) && !(AutoUIManager.Instance != null && AutoUIManager.Instance.IsInventoryOpen()))
        {
            Vector3 mouseWorldPos = cam.ScreenToWorldPoint(Input.mousePosition);
            mouseWorldPos.z = player.position.z;

            Vector3 directionToMouse = mouseWorldPos - player.position;
            Vector3 panOffset = Vector3.ClampMagnitude(directionToMouse, maxLookAhead);

            targetPos += (panOffset / 2f);
        }

        // Ép buộc dùng Time.deltaTime để tránh Camera bị lạc nhịp với khung hình
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

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        scroll = Mathf.Clamp(scroll, -0.2f, 0.2f);

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