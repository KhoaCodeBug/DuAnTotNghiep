using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PZ_PlayerController : MonoBehaviour
{
    [Header("--- Movement Settings ---")]
    public float walkSpeed = 4f;
    public float runSpeed = 7f;      // Tốc độ khi giữ Shift
    public float aimSpeed = 2f;      // Đi chậm khi giữ Chuột phải (PZ chuẩn)
    public float rotationSpeed = 15f;

    [Header("--- Aiming & Visuals ---")]
    [Tooltip("Kéo một GameObject hình tròn (Sprite/Decal) vào đây làm tâm ngắm")]
    public GameObject aimReticle;    // Tâm ngắm (bóng) dưới chuột của game PZ

    [Header("--- Animations ---")]
    public Animator anim; // Kéo Animator của nhân vật vào đây

    private Rigidbody rb;
    private Camera mainCam;
    private Vector3 moveInput;
    private bool isAiming;
    private bool isRunning;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        mainCam = Camera.main;

        // Khóa hoàn toàn xoay vật lý để nhân vật KHÔNG BAO GIỜ bị đổ hay nghiêng làm mất Shadow
        rb.freezeRotation = true;

        if (aimReticle != null) aimReticle.SetActive(false);
    }

    void Update()
    {
        HandleInputs();
        HandleRotationAndReticle();

        UpdateAnimation();
    }

    void FixedUpdate()
    {
        HandleMovement();
    }

    // 1. Lấy và Xử lý Input theo góc Camera (Isometric)
    private void HandleInputs()
    {
        float hor = Input.GetAxisRaw("Horizontal");
        float ver = Input.GetAxisRaw("Vertical");

        // Lấy hướng của Camera, ép trục Y về 0 để nhân vật chỉ đi trên mặt phẳng
        Vector3 camForward = mainCam.transform.forward;
        Vector3 camRight = mainCam.transform.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        moveInput = (camForward * ver + camRight * hor).normalized;

        // Ưu tiên Aiming hơn Running giống PZ
        isAiming = Input.GetMouseButton(1);
        isRunning = Input.GetKey(KeyCode.LeftShift) && !isAiming;
    }

    // 2. Di chuyển vật lý
    private void HandleMovement()
    {
        float currentSpeed = walkSpeed;
    if (isAiming) currentSpeed = aimSpeed;
    else if (isRunning) currentSpeed = runSpeed;

    if (moveInput == Vector3.zero) 
    {
        // Khi không bấm nút, cho vận tốc về 0 nhưng giữ nguyên vận tốc rơi (Y)
        rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
        return;
    }

    // Dùng MovePosition để di chuyển vật lý mượt mà nhất
    Vector3 nextPosition = rb.position + moveInput * currentSpeed * Time.fixedDeltaTime;
    rb.MovePosition(nextPosition);
    }

    // 3. Xử lý xoay người và hiển thị Bóng ngắm (Reticle)
    private void HandleRotationAndReticle()
    {
        if (isAiming)
        {
            // --- CHẾ ĐỘ THỦ THẾ (AIM MODE) ---
            Vector3 mouseHitPoint = GetMouseWorldPosition();

            // Cập nhật vị trí bóng ngắm (Aim Reticle) tại chuột
            if (aimReticle != null)
            {
                if (!aimReticle.activeSelf) aimReticle.SetActive(true);
                aimReticle.transform.position = mouseHitPoint + Vector3.up * 0.6f; // Nâng nhẹ lên để không chìm xuống đất
            }

            // Xoay nhân vật về phía chuột
            Vector3 lookDir = mouseHitPoint - transform.position;
            lookDir.y = 0f; // CHỐNG NGHIÊNG MODEL: Ép cứng Y = 0 để model không chìm làm mất bóng

            if (lookDir != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }
        }
        else
        {
            // --- CHẾ ĐỘ BÌNH THƯỜNG (NORMAL MODE) ---
            if (aimReticle != null && aimReticle.activeSelf)
                aimReticle.SetActive(false); // Tắt bóng ngắm khi nhả chuột

            // Xoay theo hướng di chuyển
            if (moveInput != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveInput);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }
        }
    }

    // Hàm lấy vị trí chuột trên mặt đất (Y=0) cực kỳ chính xác
    private Vector3 GetMouseWorldPosition()
    {
        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero); // Mặt phẳng ảo tại Y=0
        if (groundPlane.Raycast(ray, out float enter))
        {
            return ray.GetPoint(enter);
        }
        return transform.position; // Trả về vị trí hiện tại nếu lỗi (hiếm gặp)
    }

    private void UpdateAnimation()
    {
        if (anim == null) return;

        float targetSpeed = 0f;

        // Nếu có bấm di chuyển
        if (moveInput.magnitude > 0.1f)
        {
            if (isAiming)
                targetSpeed = 0.3f; // Dáng đi chậm khi nhắm
            else if (isRunning)
                targetSpeed = 1.0f; // Chạy nhanh (Sprint)
            else
                targetSpeed = 0.5f; // Đi bộ bình thường
        }
        else
        {
            targetSpeed = 0f; // Đứng yên
        }

        // Làm mượt con số Speed để Animation không bị đổi đột ngột
        float currentAnimSpeed = anim.GetFloat("Speed");
        anim.SetFloat("Speed", Mathf.Lerp(currentAnimSpeed, targetSpeed, Time.deltaTime * 8f));
    }
}