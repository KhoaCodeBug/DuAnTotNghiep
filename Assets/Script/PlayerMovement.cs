using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerStamina))] // Đảm bảo luôn có script Stamina đi kèm
public class PlayerMovement : MonoBehaviour
{
    [Header("--- Movement Settings ---")]
    public float walkSpeed = 4f;
    public float runSpeed = 7f;
    public float aimSpeed = 2f;
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

    private Vector2 lastLookDir = Vector2.down;
    private Vector2 smoothLookDir;

    // Thêm tham chiếu đến hệ thống Stamina
    private PlayerStamina staminaSystem;

    // 🔥 MỚI: Các biến dùng để xử lý khi bị Zombie cào
    private bool isStunned = false;
    private float stunTimer = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        staminaSystem = GetComponent<PlayerStamina>(); // Khởi tạo kết nối
        mainCam = Camera.main;
        rb.freezeRotation = true;
        smoothLookDir = lastLookDir;
        if (aimReticle != null) aimReticle.SetActive(false);
    }

    void Update()
    {
        // 🔥 MỚI: Đếm lùi thời gian bị choáng
        if (stunTimer > 0)
        {
            stunTimer -= Time.deltaTime;
            isStunned = stunTimer > 0;
        }

        HandleInputs();

        bool isMovingNow = moveInput.magnitude > 0.1f;
        // Báo cho Stamina biết trạng thái di chuyển để nó tự tính toán
        staminaSystem.UpdateStamina(isRunning, isMovingNow);

        HandleRotationAndReticle();
        UpdateAnimation(isMovingNow); // Tui truyền isMovingNow vào luôn cho gọn
        HandleCombat();
    }

    void FixedUpdate()
    {
        HandleMovement();
    }

    private void HandleInputs()
    {
        // 🔥 MỚI: NẾU ĐANG BỊ CHOÁNG -> KHÔNG CHO LÀM GÌ HẾT
        if (isStunned)
        {
            moveInput = Vector2.zero;
            isAiming = false;
            isRunning = false;
            return; // Thoát hàm sớm, không đọc nút bấm nữa
        }

        float hor = Input.GetAxisRaw("Horizontal");
        float ver = Input.GetAxisRaw("Vertical");
        moveInput = new Vector2(hor, ver).normalized;

        isAiming = Input.GetMouseButton(1);

        // NẾU ĐANG MỆT (Đọc từ PlayerStamina) THÌ CẤM CHẠY
        if (staminaSystem.IsExhausted)
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

        // Nếu mệt, đi bộ chậm lại (còn 60% tốc độ gốc)
        if (staminaSystem.IsExhausted && !isAiming)
        {
            currentSpeed = walkSpeed * 0.6f;
        }

        // Chọn tốc độ cơ bản tùy trạng thái
        if (isAiming)
        {
            currentSpeed = aimSpeed;
        }
        else if (isRunning)
        {
            currentSpeed = runSpeed;
        }

        // Áp dụng bùa chú (Buff) nhân tốc độ cho cả lúc ĐI BỘ và CHẠY (Miễn là không ngắm súng)
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

    // =========================================================================
    // 🔥 DƯỚI ĐÂY LÀ 4 CÁI HÀM CŨ CỦA BẠN MÌNH ĐÃ LẤY LẠI ĐỂ KHÔNG BỊ LỖI
    // =========================================================================

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
        if (isAiming)
        {
            if (Input.GetMouseButtonDown(0))
            {
                anim.SetTrigger("Shoot");
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                int randomAttack = Random.Range(2, 5);
                anim.SetInteger("RandomBash", randomAttack);
                anim.SetTrigger("GunBash");
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

        // Báo cho Animator biết nhân vật đang thở dốc dựa trên Stamina
        anim.SetBool("IsExhausted", staminaSystem.IsExhausted);

        bool isMovingBackwards = false;
        if (isAiming && isMovingNow)
        {
            float dotProduct = Vector2.Dot(lastLookDir.normalized, moveInput.normalized);
            if (dotProduct < -0.05f) isMovingBackwards = true;
        }
        anim.SetBool("IsMovingBackwards", isMovingBackwards);

        smoothLookDir = Vector3.Lerp(smoothLookDir, lastLookDir, turnSpeed * Time.deltaTime);
        anim.SetFloat("MoveX", smoothLookDir.x);
        anim.SetFloat("MoveY", smoothLookDir.y);

        // Phát chậm animation đi bộ nếu đang mệt
        if (staminaSystem.IsExhausted && isMovingNow && !isAiming)
        {
            anim.speed = 0.7f;
        }
        else
        {
            anim.speed = 1f;
        }
    }

    // 🔥 MỚI: Hàm để PlayerHealth gọi khi bị ăn tát
    public void LockMovement(float duration)
    {
        stunTimer = duration;
        isStunned = true;
        rb.linearVelocity = Vector2.zero; // Ép dừng lại ngay lập tức
        moveInput = Vector2.zero;
    }
}