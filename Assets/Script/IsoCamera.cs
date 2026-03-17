using UnityEngine;

public class PZ_CameraController : MonoBehaviour
{
    [Header("--- Target Setup ---")]
    public Transform player;
    public Vector3 offset = new Vector3(0, 0, 0);

    [Header("--- PZ Camera Panning ---")]
    public float smoothFollow = 0.12f;
    public float maxLookAhead = 6f;

    [Header("--- Orthographic Zoom ---")]
    public float zoomSpeed = 15f;
    public float zoomSmoothTime = 0.05f; // Thời gian trễ nhỏ để phản hồi tức thì
    public float minZoomSize = 5f;
    public float maxZoomSize = 14f;

    private Camera cam;
    private Vector3 velocity;
    private float targetZoom;
    private float zoomVelocity;

    void Start()
    {
        cam = GetComponentInChildren<Camera>();
        if (!cam.orthographic) cam.orthographic = true;
        targetZoom = cam.orthographicSize;

        if (player != null)
        {
            transform.position = player.position + offset;
        }
    }

    // Update dùng để bắt các input nhanh như Cuộn chuột
    void Update()
    {
        HandleZoom();
    }
    void LateUpdate()
    {
        if (player == null) return;
        HandleCameraFollowAndPan();
    }

    private void HandleCameraFollowAndPan()
    {
        Vector3 targetPos = player.position + offset;

        if (Input.GetMouseButton(1))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

            if (groundPlane.Raycast(ray, out float rayLength))
            {
                Vector3 mouseWorldPos = ray.GetPoint(rayLength);
                Vector3 directionToMouse = mouseWorldPos - player.position;

                Vector3 panOffset = Vector3.ClampMagnitude(directionToMouse, maxLookAhead);

                targetPos += (panOffset / 2f);
            }
        }

        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPos,
            ref velocity,
            smoothFollow
        );
    }

    private void HandleZoom()
    {
        float scroll = Input.mouseScrollDelta.y;

        if (scroll != 0)
        {
            // Tác động trực tiếp vào targetZoom trong Update giúp bắt ngay lập tức mọi lần lăn
            targetZoom -= scroll * zoomSpeed;
            targetZoom = Mathf.Clamp(targetZoom, minZoomSize, maxZoomSize);
        }

        // Làm mượt hình ảnh hiển thị
        cam.orthographicSize = Mathf.SmoothDamp(
            cam.orthographicSize,
            targetZoom,
            ref zoomVelocity,
            zoomSmoothTime // PHẢI dùng zoomSmoothTime thay vì smoothFollow để bớt delay
        );
    }
}