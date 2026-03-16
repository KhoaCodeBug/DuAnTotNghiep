using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PZ_PlayerController2D : MonoBehaviour
{
    [Header("--- Movement Settings ---")]
    public float walkSpeed = 4f;
    public float runSpeed = 7f;
    public float aimSpeed = 2f;

    [Header("--- Aiming & Visuals ---")]
    public GameObject aimReticle;
    [Tooltip("Tốc độ chuyển đổi giữa các hướng trong Animator")]
    public float turnSpeed = 12f; // Thêm biến này để chỉnh độ mượt khi xoay hướng

    [Header("--- Animations ---")]
    public Animator anim;

    private Rigidbody2D rb;
    private Camera mainCam;
    private Vector2 moveInput;
    private bool isAiming;
    private bool isRunning;

    private Vector2 lastLookDir = Vector2.down;
    private Vector2 smoothLookDir; // Biến lưu giá trị mượt để gửi vào Blend Tree

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        mainCam = Camera.main;
        rb.freezeRotation = true;

        smoothLookDir = lastLookDir; // Khởi tạo giá trị ban đầu

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

    private void HandleInputs()
    {
        float hor = Input.GetAxisRaw("Horizontal");
        float ver = Input.GetAxisRaw("Vertical");

        moveInput = new Vector2(hor, ver).normalized;

        isAiming = Input.GetMouseButton(1);
        isRunning = Input.GetKey(KeyCode.LeftShift) && !isAiming;
    }

    private void HandleMovement()
    {
        float currentSpeed = walkSpeed;
        if (isAiming) currentSpeed = aimSpeed;
        else if (isRunning) currentSpeed = runSpeed;

        if (moveInput == Vector2.zero)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 nextPosition = rb.position + moveInput * currentSpeed * Time.fixedDeltaTime;
        rb.MovePosition(nextPosition);
    }

    private void HandleRotationAndReticle()
    {
        if (isAiming)
        {
            Vector2 mouseHitPoint = GetMouseWorldPosition();

            if (aimReticle != null)
            {
                if (!aimReticle.activeSelf) aimReticle.SetActive(true);
                aimReticle.transform.position = mouseHitPoint;
            }

            Vector2 lookVector = mouseHitPoint - (Vector2)transform.position;

            // --- FIX LỖI GIẬT HƯỚNG MẶT (DEADZONE) ---
            // Chỉ cập nhật hướng nhìn nếu chuột cách nhân vật một khoảng tối thiểu (0.1f)
            // Tránh việc nhân vật lùi chạm vào trỏ chuột rồi xoay vòng tròn
            if (lookVector.sqrMagnitude > 0.1f)
            {
                lastLookDir = lookVector.normalized;
            }
        }
        else
        {
            if (aimReticle != null && aimReticle.activeSelf)
                aimReticle.SetActive(false);

            if (moveInput != Vector2.zero)
            {
                lastLookDir = moveInput;
            }
        }
    }

    private Vector2 GetMouseWorldPosition()
    {
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = Mathf.Abs(mainCam.transform.position.z);
        return mainCam.ScreenToWorldPoint(mousePos);
    }

    private void UpdateAnimation()
    {
        if (anim == null) return;

        bool isMovingNow = moveInput.magnitude > 0.1f;
        anim.SetBool("IsRunning", isMovingNow);
        anim.SetBool("IsAiming", isAiming);

        // --- TÍNH TOÁN ĐI LÙI AN TOÀN HƠN ---
        bool isMovingBackwards = false;
        if (isAiming && isMovingNow)
        {
            float dotProduct = Vector2.Dot(lastLookDir.normalized, moveInput.normalized);
            // Nới rộng ngưỡng một chút để không bị kẹt khi đi chéo
            if (dotProduct < -0.05f)
            {
                isMovingBackwards = true;
            }
        }
        anim.SetBool("IsMovingBackwards", isMovingBackwards);

        // --- LÀM MƯỢT GÓC QUAY (SMOOTH ROTATION) ---
        // Lerp từ từ giá trị hiện tại tới lastLookDir để Blend Tree chuyển đổi mềm mại
        smoothLookDir = Vector2.Lerp(smoothLookDir, lastLookDir, turnSpeed * Time.deltaTime);

        anim.SetFloat("MoveX", smoothLookDir.x);
        anim.SetFloat("MoveY", smoothLookDir.y);

        if (isMovingNow)
        {
            if (isAiming) anim.speed = 0.5f;
            else if (isRunning) anim.speed = 1.3f;
            else anim.speed = 1f;
        }
        else
        {
            anim.speed = 1f;
        }
    }
}