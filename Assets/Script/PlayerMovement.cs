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
    public Texture2D crosshairTexture;
    [Tooltip("Tọa độ tâm của tấm hình. Ví dụ hình 32x32 thì tâm là X:16, Y:16")]
    public Vector2 crosshairHotSpot = new Vector2(16, 16);
    private bool isCurrentlyAimingCursor = false;

    [Header("--- Noise Generation ---")]
    public LayerMask zombieLayer;
    public float walkNoiseRadius = 4f;
    public float runNoiseRadius = 8f;
    private float noiseEmitTimer = 0f;

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

    // Đang xài đồ (Nhận tín hiệu từ AutoUIManager)
    public bool isUsingItem = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        staminaSystem = GetComponent<PlayerStamina>();
        mainCam = Camera.main;
        rb.freezeRotation = true;
        smoothLookDir = lastLookDir;
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

        HandleRotationAndAiming();
        UpdateAnimation(isMovingNow);

        HandleMovementNoise(isMovingNow);
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

        // 🔥 ĐÃ SỬA: Chặn chức năng ngắm nếu đang bật UI
        if (AutoUIManager.Instance != null && AutoUIManager.Instance.IsInventoryOpen())
        {
            isAiming = false; // Cấm ngắm
        }
        else
        {
            isAiming = Input.GetMouseButton(1); // Bình thường thì cho phép ngắm
        }

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
            // PUBG: Cấm chạy nhanh (Shift) khi đang dùng đồ
            isRunning = Input.GetKey(KeyCode.LeftShift) && !isAiming && !isUsingItem;
        }
    }

    private void HandleMovement()
    {
        float currentSpeed = walkSpeed;

        // Ưu tiên 1: Đang dùng đồ là lết chậm 35%
        if (isUsingItem)
        {
            currentSpeed = walkSpeed * 0.35f;
        }
        else if (staminaSystem.IsExhausted && !isAiming)
        {
            currentSpeed = walkSpeed * 0.6f;
        }
        else if (isAiming)
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

        if (!isAiming && !isUsingItem && staminaSystem.CurrentSpeedMultiplier > 1f)
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

    private void HandleRotationAndAiming()
    {
        if (isAiming)
        {
            Vector2 mouseHitPoint = GetMouseWorldPosition();
            Vector2 lookVector = mouseHitPoint - (Vector2)transform.position;
            if (lookVector.sqrMagnitude > 0.1f)
            {
                lastLookDir = lookVector.normalized;
            }

            if (!isCurrentlyAimingCursor)
            {
                Cursor.SetCursor(crosshairTexture, crosshairHotSpot, CursorMode.Auto);
                isCurrentlyAimingCursor = true;
            }
        }
        else
        {
            if (moveInput != Vector2.zero) lastLookDir = moveInput;

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

        // 🔥 ĐÃ SỬA: Tinh chỉnh lại tốc độ Animation cho mượt mắt
        if (isUsingItem && isMovingNow)
        {
            anim.speed = 0.5f; // Chân bước chậm lại cho khớp với tốc độ lết
        }
        else if (staminaSystem.IsExhausted && isMovingNow && !isAiming)
        {
            anim.speed = 0.7f; // Thở dốc thì bước chậm
        }
        else
        {
            anim.speed = 1f; // Chạy bộ/Đi bộ bình thường
        }
    }

    private void HandleMovementNoise(bool isMoving)
    {
        if (!isMoving || isCrouching || isStunned) return;

        if (noiseEmitTimer > 0)
        {
            noiseEmitTimer -= Time.deltaTime;
            return;
        }
        noiseEmitTimer = 0.2f;

        if (isRunning) MakeNoise(runNoiseRadius);
        else MakeNoise(walkNoiseRadius);
    }

    public void LockMovement(float duration)
    {
        stunTimer = duration;
        isStunned = true;
        rb.linearVelocity = Vector2.zero;
        moveInput = Vector2.zero;
    }

    public void MakeNoise(float radius)
    {
        Collider2D[] zombies = Physics2D.OverlapCircleAll(transform.position, radius, zombieLayer);
        foreach (Collider2D z in zombies)
        {
            ZOmbieAI_Khoa ai = z.GetComponentInParent<ZOmbieAI_Khoa>();
            if (ai != null) ai.HearSound(transform.position);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, walkNoiseRadius);
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, runNoiseRadius);
    }
}