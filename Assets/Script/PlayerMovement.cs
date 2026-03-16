using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PZ_PlayerController2D : MonoBehaviour
{
    [Header("--- Movement Settings ---")]
    public float walkSpeed = 4f;
    public float runSpeed = 7f;
    public float aimSpeed = 2f;
    [Tooltip("Tốc độ quay mặt")]
    public float turnSpeed = 12f;

    [Header("--- Stamina System ---")]
    public float maxStamina = 100f;
    public float currentStamina = 100f;
    public float staminaDrain = 20f;       // Mất bao nhiêu Stamina mỗi giây khi chạy

    [Tooltip("Tốc độ hồi Stamina khi ĐỨNG YÊN thở")]
    public float staminaRecoverIdle = 15f;

    [Tooltip("Tốc độ hồi Stamina khi ĐI BỘ/LẾT")]
    public float staminaRecoverWalk = 5f;

    private bool isExhausted = false;      // Trạng thái mệt mỏi

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

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        mainCam = Camera.main;
        rb.freezeRotation = true;
        smoothLookDir = lastLookDir;
        if (aimReticle != null) aimReticle.SetActive(false);
    }

    void Update()
    {
        HandleInputs();
        HandleStamina(); // Quản lý thể lực
        HandleRotationAndReticle();
        UpdateAnimation();
        HandleCombat();
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

        // NẾU ĐANG MỆT (EXHAUSTED) THÌ CẤM CHẠY
        if (isExhausted)
        {
            isRunning = false;
        }
        else
        {
            isRunning = Input.GetKey(KeyCode.LeftShift) && !isAiming;
        }
    }

    private void HandleStamina()
    {
        // 1. Trừ thể lực khi đang chạy
        if (isRunning && moveInput.magnitude > 0.1f)
        {
            currentStamina -= staminaDrain * Time.deltaTime;
            if (currentStamina <= 0)
            {
                currentStamina = 0;
                isExhausted = true; // Cạn kiệt thể lực
            }
        }
        // 2. Hồi thể lực khi không chạy (Đi bộ hoặc Đứng yên)
        else
        {
            // Kiểm tra xem nhân vật có đang di chuyển không
            bool isMovingNow = moveInput.magnitude > 0.1f;

            // Chọn tốc độ hồi phục tương ứng
            float currentRecoverRate = isMovingNow ? staminaRecoverWalk : staminaRecoverIdle;

            // Cộng thể lực
            currentStamina += currentRecoverRate * Time.deltaTime;

            if (currentStamina >= maxStamina)
            {
                currentStamina = maxStamina;
                isExhausted = false; // Đầy thể lực, xốc lại súng và sẵn sàng chạy tiếp
            }
        }
    }

    private void HandleMovement()
    {
        float currentSpeed = walkSpeed;

        // Nếu mệt, đi bộ chậm lại (còn 60% tốc độ gốc)
        if (isExhausted && !isAiming)
        {
            currentSpeed = walkSpeed * 0.6f;
        }

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
        // TẤT CẢ hành động tấn công CHỈ ĐƯỢC PHÉP khi đang đè chuột phải ngắm (Aiming)
        if (isAiming)
        {
            // 1. BẮN SÚNG (Chuột Trái)
            if (Input.GetMouseButtonDown(0))
            {
                anim.SetTrigger("Shoot");
                // Sau này viết thêm code trừ đạn, sinh tia lửa ở đây...
            }

            // 2. ĐẬP BẰNG SÚNG (Phím Space)
            if (Input.GetKeyDown(KeyCode.Space))
            {
                // Random ra 1 số từ 2 đến 4 (tương ứng với Attack 2, 3, 4)
                int randomAttack = Random.Range(2, 5);

                // Gửi số random vào Animator để chọn đòn đánh
                anim.SetInteger("RandomBash", randomAttack);

                // Kích hoạt đánh
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

    private void UpdateAnimation()
    {
        if (anim == null) return;

        bool isMovingNow = moveInput.magnitude > 0.1f;
        anim.SetBool("IsMoving", isMovingNow);
        anim.SetBool("IsRunning", isRunning);
        anim.SetBool("IsAiming", isAiming);

        // Báo cho Animator biết nhân vật đang thở dốc
        anim.SetBool("IsExhausted", isExhausted);

        bool isMovingBackwards = false;
        if (isAiming && isMovingNow)
        {
            float dotProduct = Vector2.Dot(lastLookDir.normalized, moveInput.normalized);
            if (dotProduct < -0.05f) isMovingBackwards = true;
        }
        anim.SetBool("IsMovingBackwards", isMovingBackwards);

        smoothLookDir = Vector2.Lerp(smoothLookDir, lastLookDir, turnSpeed * Time.deltaTime);
        anim.SetFloat("MoveX", smoothLookDir.x);
        anim.SetFloat("MoveY", smoothLookDir.y);

        // Phát chậm animation đi bộ nếu đang mệt
        if (isExhausted && isMovingNow && !isAiming)
        {
            anim.speed = 0.7f;
        }
        else
        {
            anim.speed = 1f;
        }
    }
}