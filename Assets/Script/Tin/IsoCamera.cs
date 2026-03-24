using UnityEngine;

public class PZ_CameraController : MonoBehaviour
{
    [Header("--- Target Setup ---")]
    // 🔥 ĐÃ SỬA: Chuyển thành private để không phải kéo thả tay, tránh nhầm lẫn khi chơi mạng
    private Transform player;
    private bool hasTarget = false; // Cờ đánh dấu xem camera đã có mục tiêu chưa

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

    // 🔥 MỚI: Cửa nạp để cục Player (sau khi được sinh ra mạng) gọi và báo "Bám theo tui!"
    public void SetTarget(Transform targetTransform)
    {
        player = targetTransform;
        hasTarget = true;

        // Dịch chuyển camera ngay lập tức tới chỗ nhân vật lúc vừa đẻ ra để khỏi bị khựng
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
        // 🔥 MỚI: Nếu chưa có nhân vật thì không cho phép Zoom
        if (!hasTarget || player == null) return;

        HandleZoom();
    }

    void LateUpdate()
    {
        // 🔥 MỚI: Nếu chưa có nhân vật thì Camera đứng im, không báo lỗi
        if (!hasTarget || player == null) return;

        HandleCameraFollowAndPan();
    }

    private void HandleCameraFollowAndPan()
    {
        Vector3 targetPos = player.position + offset;

        // 🔥 Xử lý lia Camera chuẩn 2D
        // Cấm Camera lia theo chuột nếu túi đồ đang mở
        if (Input.GetMouseButton(1) && !(AutoUIManager.Instance != null && AutoUIManager.Instance.IsInventoryOpen()))
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
        // LỚP BẢO VỆ 1: Chặn đứng mọi hành vi đọc phím/chuột nếu cửa sổ Game không được chọn (Focus)
        if (!Application.isFocused) return;

        // LỚP BẢO VỆ 2: Vẫn giữ chặn tọa độ đề phòng
        Vector3 mousePos = Input.mousePosition;
        if (mousePos.x < 0 || mousePos.y < 0 || mousePos.x > Screen.width || mousePos.y > Screen.height)
        {
            return;
        }

        // LỚP BẢO VỆ 3: Sửa lỗi "Xả đê" cuộn chuột
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