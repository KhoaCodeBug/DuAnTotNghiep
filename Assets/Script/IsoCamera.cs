using UnityEngine;

public class PZ_CameraController : MonoBehaviour
{
    [Header("--- Target Setup ---")]
    public Transform player;
    // Đảm bảo offset có Z là số âm (VD: -10) để camera lùi lại nhìn thấy cảnh 2D
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

        // 🔥 SỬA CHỖ NÀY: Xử lý lia Camera chuẩn 2D
        if (Input.GetMouseButton(1)) // Khi đang đè chuột phải (Aim)
        {
            // 1. Lấy thẳng tọa độ chuột trong thế giới 2D
            Vector3 mouseWorldPos = cam.ScreenToWorldPoint(Input.mousePosition);
            mouseWorldPos.z = player.position.z; // Ép trục Z bằng với Player để tránh lỗi

            // 2. Tính khoảng cách từ chuột tới nhân vật
            Vector3 directionToMouse = mouseWorldPos - player.position;

            // 3. Giới hạn khoảng cách camera vươn ra (không cho camera chạy tuốt luốt ra ngoài mép)
            Vector3 panOffset = Vector3.ClampMagnitude(directionToMouse, maxLookAhead);

            // 4. Cộng dồn offset (Chia 2 để camera nằm ở quãng giữa Player và con trỏ chuột)
            targetPos += (panOffset / 2f);
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
        // 🔥 LỚP BẢO VỆ 1: Chặn đứng mọi hành vi đọc phím/chuột nếu cửa sổ Game không được chọn (Focus)
        if (!Application.isFocused) return;

        // 🔥 LỚP BẢO VỆ 2: Vẫn giữ chặn tọa độ đề phòng
        Vector3 mousePos = Input.mousePosition;
        if (mousePos.x < 0 || mousePos.y < 0 || mousePos.x > Screen.width || mousePos.y > Screen.height)
        {
            return;
        }

        // 🔥 LỚP BẢO VỆ 3: Sửa lỗi "Xả đê" cuộn chuột
        // Dùng GetAxis mượt hơn mouseScrollDelta. 1 nấc lăn trả về khoảng 0.1 hoặc -0.1
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        // Dùng Clamp khóa cứng giá trị. Nếu Unity bị điên xả ra số 5.0, nó sẽ bị gọt về 0.1 ngay lập tức.
        scroll = Mathf.Clamp(scroll, -0.2f, 0.2f);

        if (scroll != 0)
        {
            // Do lúc này scroll là số thập phân nhỏ (0.1), nên nhân với zoomSpeed (15f) sẽ rất mượt, không bị giật cái đùng
            targetZoom -= scroll * zoomSpeed;
            targetZoom = Mathf.Clamp(targetZoom, minZoomSize, maxZoomSize);
        }

        // Làm mượt hình ảnh hiển thị
        cam.orthographicSize = Mathf.SmoothDamp(
            cam.orthographicSize,
            targetZoom,
            ref zoomVelocity,
            zoomSmoothTime
        );
    }
}