using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerStamina))]
public class PlayerMovement : MonoBehaviour
{
    [Header("--- Movement Settings ---")]
    public float walkSpeed = 4f;
    public float runSpeed = 7f;
    public float aimSpeed = 2f;
    public float crouchSpeed = 2.5f;
    [Tooltip("Tốc độ quay mặt")]
    public float turnSpeed = 12f;

    [Header("--- Aiming & Hardware Cursor ---")]
    // 🔥 SỬA: Thay thế GameObject bằng Texture2D để dùng Hardware Cursor
    public Texture2D crosshairTexture;
    [Tooltip("Tọa độ tâm của tấm hình. Ví dụ hình 32x32 thì tâm là X:16, Y:16")]
    public Vector2 crosshairHotSpot = new Vector2(16, 16);
    private bool isCurrentlyAimingCursor = false;

    [Header("--- Animations ---")]
    public Animator anim;

    private Rigidbody2D rb;
    private Camera mainCam;
    private Vector2 moveInput;
    private bool isAiming;
    private bool isRunning;
    private bool isCrouching;

    private Vector2 lastLookDir = Vector2.down;
    private Vector2 smoothLookDir;

    private PlayerStamina staminaSystem;

    private bool isStunned = false;
    private float stunTimer = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        staminaSystem = GetComponent<PlayerStamina>();
        mainCam = Camera.main;
        rb.freezeRotation = true;
        smoothLookDir = lastLookDir;

        // Đã xóa dòng aimReticle.SetActive ở đây
    }

    void Update()
    {
        if (stunTimer > 0)
        {
            stunTimer -= Time.deltaTime;
            isStunned = stunTimer > 0;
        }

        HandleInputs();

        bool isMovingNow = moveInput.magnitude > 0.1f;
        staminaSystem.UpdateStamina(isRunning, isMovingNow);

        HandleRotationAndAiming(); // Đã đổi tên hàm cho chuẩn xác hơn
        UpdateAnimation(isMovingNow);
    }

    void FixedUpdate()
    {
        HandleMovement();
    }

    private void HandleInputs()
    {
        if (isStunned)
        {
            moveInput = Vector2.zero;
            isAiming = false;
            isRunning = false;
            return;
        }

        float hor = Input.GetAxisRaw("Horizontal");
        float ver = Input.GetAxisRaw("Vertical");
        moveInput = new Vector2(hor, ver).normalized;

        isAiming = Input.GetMouseButton(1);

        if (Input.GetKeyDown(KeyCode.C))
        {
            isCrouching = !isCrouching;
        }

        if (isAiming)
        {
            isCrouching = false;
        }

        if (staminaSystem.IsExhausted || isCrouching)
        {
            isRunning = false;
        }
        else
        {
            isRunning = Input.GetKey(KeyCode.LeftShift) && !isAiming;
        }
    }

    private void HandleMovement()
    {
        float currentSpeed = walkSpeed;

        if (staminaSystem.IsExhausted && !isAiming)
        {
            currentSpeed = walkSpeed * 0.6f;
        }

        if (isAiming)
        {
            currentSpeed = aimSpeed;
        }
        else if (isRunning)
        {
            currentSpeed = runSpeed;
        }
        else if (isCrouching)
        {
            currentSpeed = crouchSpeed;
        }

        if (!isAiming && staminaSystem.CurrentSpeedMultiplier > 1f)
        {
            currentSpeed *= staminaSystem.CurrentSpeedMultiplier;
        }

        if (moveInput == Vector2.zero)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 nextPosition = rb.position + moveInput * currentSpeed * Time.fixedDeltaTime;
        rb.MovePosition(nextPosition);
    }

    // 🔥 SỬA: Cập nhật hàm này để xử lý Crosshair xịn
    private void HandleRotationAndAiming()
    {
        if (isAiming)
        {
            // Tính toán hướng nhìn của nhân vật
            Vector2 mouseHitPoint = GetMouseWorldPosition();
            Vector2 lookVector = mouseHitPoint - (Vector2)transform.position;
            if (lookVector.sqrMagnitude > 0.1f)
            {
                lastLookDir = lookVector.normalized;
            }

            // --- BẬT HARDWARE CURSOR ---
            if (!isCurrentlyAimingCursor)
            {
                Cursor.SetCursor(crosshairTexture, crosshairHotSpot, CursorMode.Auto);
                isCurrentlyAimingCursor = true;
            }
        }
        else
        {
            // Di chuyển thì nhìn theo hướng di chuyển
            if (moveInput != Vector2.zero) lastLookDir = moveInput;

            // --- TẮT HARDWARE CURSOR ---
            if (isCurrentlyAimingCursor)
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                isCurrentlyAimingCursor = false;
            }
        }
    }

    private Vector2 GetMouseWorldPosition()
    {
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = Mathf.Abs(mainCam.transform.position.z);
        return mainCam.ScreenToWorldPoint(mousePos);
    }

    private void UpdateAnimation(bool isMovingNow)
    {
        if (anim == null) return;

        anim.SetBool("IsMoving", isMovingNow);
        anim.SetBool("IsRunning", isRunning);
        anim.SetBool("IsAiming", isAiming);
        anim.SetBool("IsExhausted", staminaSystem.IsExhausted);
        anim.SetBool("IsCrouching", isCrouching);

        float strafeX = 0f;
        float strafeY = 0f;

        if (isAiming && isMovingNow)
        {
            Vector2 forwardDir = lastLookDir.normalized;
            // Đã đổi dấu để X=1 là sang Phải, X=-1 là sang Trái cho khớp Blend Tree
            Vector2 rightDir = new Vector2(forwardDir.y, -forwardDir.x);

            strafeY = Vector2.Dot(moveInput.normalized, forwardDir);
            strafeX = Vector2.Dot(moveInput.normalized, rightDir);
        }

        anim.SetFloat("StrafeX", strafeX);
        anim.SetFloat("StrafeY", strafeY);

        if (isAiming)
        {
            smoothLookDir = Vector3.RotateTowards(smoothLookDir, lastLookDir, turnSpeed * Time.deltaTime, 0f);
        }
        else
        {
            smoothLookDir = lastLookDir;
        }

        anim.SetFloat("MoveX", smoothLookDir.x);
        anim.SetFloat("MoveY", smoothLookDir.y);

        if (staminaSystem.IsExhausted && isMovingNow && !isAiming)
        {
            anim.speed = 0.7f;
        }
        else
        {
            anim.speed = 1f;
        }
    }

    public void LockMovement(float duration)
    {
        stunTimer = duration;
        isStunned = true;
        rb.linearVelocity = Vector2.zero;
        moveInput = Vector2.zero;
    }
}