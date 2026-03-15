using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))] // Bắt buộc dùng Rigidbody2D thay vì 3D
public class PZ_PlayerController2D : MonoBehaviour
{
    [Header("--- Movement Settings ---")]
    public float walkSpeed = 4f;
    public float runSpeed = 7f;
    public float aimSpeed = 2f;
    public float rotationSpeed = 15f;

    [Header("--- 2.5D Setup ---")]
    [Tooltip("Kéo GameObject Con (chứa mô hình 3D) vào đây")]
    public Transform modelTransform;

    [Header("--- Aiming & Visuals ---")]
    public GameObject aimReticle;

    [Header("--- Animations ---")]
    public Animator anim;

    private Rigidbody2D rb;
    private Camera mainCam;
    private Vector2 moveInput; // Chuyển sang dùng Vector2 cho hệ trục X-Y
    private bool isAiming;
    private bool isRunning;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        mainCam = Camera.main;

        // Khóa hoàn toàn xoay vật lý để nhân vật KHÔNG BAO GIỜ bị lộn vòng
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

    // 1. Lấy và Xử lý Input cho mặt phẳng 2D
    private void HandleInputs()
    {
        // Trong 2D X-Y, Input ánh xạ thẳng lên trục X và Y
        moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;

        isAiming = Input.GetMouseButton(1);
        isRunning = Input.GetKey(KeyCode.LeftShift) && !isAiming;
    }

    // 2. Di chuyển vật lý bằng Rigidbody2D
    private void HandleMovement()
    {
        float currentSpeed = walkSpeed;
        if (isAiming) currentSpeed = aimSpeed;
        else if (isRunning) currentSpeed = runSpeed;

        // Tính toán vị trí tiếp theo và di chuyển
        Vector2 nextPosition = rb.position + moveInput * currentSpeed * Time.fixedDeltaTime;
        rb.MovePosition(nextPosition);
    }

    // 3. Xoay MÔ HÌNH 3D (Con) và hiển thị Bóng ngắm
    private void HandleRotationAndReticle()
    {
        if (modelTransform == null) return; // Nếu quên kéo mô hình 3D vào thì bỏ qua

        if (isAiming)
        {
            Vector3 mouseWorldPos = GetMouseWorldPosition2D();

            if (aimReticle != null)
            {
                if (!aimReticle.activeSelf) aimReticle.SetActive(true);
                aimReticle.transform.position = new Vector3(mouseWorldPos.x, mouseWorldPos.y, 0f);
            }

            // Hướng mặt về phía chuột
            Vector3 lookDir = mouseWorldPos - modelTransform.position;
            lookDir.z = 0f;

            if (lookDir != Vector3.zero)
            {
                // Dùng LookRotation: Trục Z hướng về chuột, trục Y hướng về Camera (Vector3.back)
                Quaternion targetRot = Quaternion.LookRotation(lookDir, Vector3.back);
                modelTransform.rotation = Quaternion.Slerp(modelTransform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }
        }
        else
        {
            if (aimReticle != null && aimReticle.activeSelf)
                aimReticle.SetActive(false);

            // Hướng mặt theo hướng di chuyển
            if (moveInput != Vector2.zero)
            {
                Vector3 moveDir = new Vector3(moveInput.x, moveInput.y, 0f);
                Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.back);
                modelTransform.rotation = Quaternion.Slerp(modelTransform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }
        }
    }

    // Hàm lấy vị trí chuột chuẩn cho 2D
    private Vector3 GetMouseWorldPosition2D()
    {
        Vector3 worldPos = mainCam.ScreenToWorldPoint(Input.mousePosition);
        worldPos.z = 0f; // Ép cứng tọa độ Z về 0 để ngang bằng với mặt đất
        return worldPos;
    }

    private void UpdateAnimation()
    {
        if (anim == null) return;

        float targetSpeed = 0f;

        if (moveInput.magnitude > 0.1f)
        {
            if (isAiming) targetSpeed = 0.3f;
            else if (isRunning) targetSpeed = 1.0f;
            else targetSpeed = 0.5f;
        }

        float currentAnimSpeed = anim.GetFloat("Speed");
        anim.SetFloat("Speed", Mathf.Lerp(currentAnimSpeed, targetSpeed, Time.deltaTime * 8f));
    }
}