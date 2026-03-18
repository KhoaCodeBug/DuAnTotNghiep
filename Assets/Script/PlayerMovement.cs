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

    [Header("--- Aiming & Visuals ---")]
    public GameObject aimReticle;

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
        if (aimReticle != null) aimReticle.SetActive(false);
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

        HandleRotationAndReticle();
        UpdateAnimation(isMovingNow);
        HandleCombat();
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
            if (lookVector.sqrMagnitude > 0.1f)
            {
                lastLookDir = lookVector.normalized;
            }
        }
        else
        {
            if (aimReticle != null && aimReticle.activeSelf)
                aimReticle.SetActive(false);

            if (moveInput != Vector2.zero) lastLookDir = moveInput;
        }
    }

    private void HandleCombat()
    {
        // Hàm này đã trống vì code combat nằm ở PlayerCombat.cs
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

        // 🔥 MỚI: Khai báo 2 biến để gửi sang Blend Tree ngắm bắn
        float strafeX = 0f;
        float strafeY = 0f;

        if (isAiming && isMovingNow)
        {
            // === TOÁN VECTOR "NÃO LÒNG" BẮT ĐẦU TỪ ĐÂY ===

            // 1. Lấy hướng súng đang chỉa (lastLookDir) làm "Trục Tới"
            Vector2 forwardDir = lastLookDir.normalized;

            // 2. Tính "Trục Ngang" vuông góc với Trục Tới (Xoay 90 độ)
            // Vector (x, y) xoay 90 độ thành (-y, x)
            Vector2 rightDir = new Vector2(-forwardDir.y, forwardDir.x);

            // 3. Phân tích moveInput (hướng bấm phím WASD) lên 2 trục vừa tìm được
            // Dùng Dot Product để xem moveInput "giống" trục nào hơn
            strafeY = Vector2.Dot(moveInput.normalized, forwardDir); // Giá trị Tiến/Lùi (-1 đến 1)
            strafeX = Vector2.Dot(moveInput.normalized, rightDir);   // Giá trị Ngang Trái/Phải (-1 đến 1)

            // Ví dụ: Súng chỉa lên (0,1). Bấm nút D (1,0). 
            // forwardDir=(0,1), rightDir=(-1,0).
            // strafeY = Dot((1,0), (0,1)) = 0.
            // strafeX = Dot((1,0), (-1,0)) = -1 (Nghĩa là đang đi sang PHẢI so với hướng súng).

            // === TOÁN VECTOR KẾT THÚC ===
        }

        // 🔥 MỚI: Gửi 2 giá trị này sang Animator
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