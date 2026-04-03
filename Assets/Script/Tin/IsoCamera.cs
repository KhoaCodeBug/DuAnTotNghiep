using UnityEngine;

public class PZ_CameraController : MonoBehaviour
{
    [Header("--- Target Setup ---")]
    private Transform player;
    private bool hasTarget = false;
    [SerializeField] private float fastFollow = 0.02f;

    private bool isInVehicle = false;
    public Vector3 offset = new Vector3(0, 0, -10f);

    [Header("--- PZ Camera Panning ---")]
    public float smoothFollow = 0.12f;
    public float maxLookAhead = 6f;

    [Header("--- Orthographic Zoom ---")]
    public float zoomSpeed = 15f;
    public float zoomSmoothTime = 0.05f;
    public float minZoomSize = 5f;
    public float maxZoomSize = 14f;

    private Rigidbody2D targetRb;
    private Camera cam;
    private Vector3 velocity;
    private float targetZoom;
    private float zoomVelocity;

    // 🔥 FIX 1: lấy Rigidbody từ PARENT (xe)
    public void SetTarget(Transform targetTransform)
    {
        player = targetTransform;

        // 🔥 QUAN TRỌNG: lấy Rigidbody của xe
        targetRb = targetTransform.GetComponentInParent<Rigidbody2D>();

        hasTarget = true;

        // snap ngay lập tức
        transform.position = player.position + offset;
    }

    void Start()
    {
        cam = GetComponentInChildren<Camera>();
        if (!cam.orthographic) cam.orthographic = true;
        targetZoom = cam.orthographicSize;
    }

    // 🔥 FIX 2: dùng Rigidbody nếu có
    void LateUpdate()
    {
        if (player == null) return;

        Vector3 targetPos;

        if (targetRb != null)
            targetPos = (Vector3)targetRb.position + offset;
        else
            targetPos = player.position + offset;

        if (isInVehicle)
        {
            // 🔥 dính 100%
            transform.position = targetPos;
        }
        else
        {
            transform.position = Vector3.SmoothDamp(
                transform.position,
                targetPos,
                ref velocity,
                smoothFollow
            );
        }
    }

    public void SetVehicleMode(bool value)
    {
        isInVehicle = value;
    }

    private void HandleZoom()
    {
        if (!Application.isFocused) return;

        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (scroll != 0)
        {
            targetZoom -= scroll * zoomSpeed;
            targetZoom = Mathf.Clamp(targetZoom, minZoomSize, maxZoomSize);
        }

        cam.orthographicSize = Mathf.SmoothDamp(
            cam.orthographicSize,
            targetZoom,
            ref zoomVelocity,
            zoomSmoothTime
        );
    }
}