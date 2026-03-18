using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerStamina))]
public class PlayerMovement : MonoBehaviour
{
    [Header("--- Movement Settings ---")]
    public float walkSpeed = 4f;
    public float runSpeed = 7f;
    public float aimSpeed = 2f;
    public float crouchSpeed = 2.5f; // 🔥 MỚI: Tốc độ khi ngồi xổm
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
    private bool isCrouching; // 🔥 MỚI: Biến kiểm tra xem có đang ngồi không

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
        //HandleCombat();
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

        // 🔥 MỚI: Nhấn nút C để bật/tắt ngồi xổm
        if (Input.GetKeyDown(KeyCode.C))
        {
            isCrouching = !isCrouching;
        }

        // Tự động hủy ngồi xổm nếu ngắm súng
        if (isAiming)
        {
            isCrouching = false;
        }

        if (staminaSystem.IsExhausted || isCrouching) // Đang mệt HOẶC đang ngồi thì KHÔNG được chạy
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

        // 🔥 SỬA LẠI CHỖ CHỌN TỐC ĐỘ: Thêm ưu tiên cho tốc độ ngồi xổm
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
            currentSpeed = crouchSpeed; // Áp dụng tốc độ ngồi
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

    /*private void HandleCombat()
    {
        if (isAiming)
        {
            if (Input.GetMouseButtonDown(0)) anim.SetTrigger("Shoot");

            if (Input.GetKeyDown(KeyCode.Space))
            {
                int randomAttack = Random.Range(2, 5);
                anim.SetInteger("RandomBash", randomAttack);
                anim.SetTrigger("GunBash");
            }
        }
    }*/

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

        bool isMovingBackwards = false;
        if (isAiming && isMovingNow)
        {
            float dotProduct = Vector2.Dot(lastLookDir.normalized, moveInput.normalized);
            if (dotProduct < -0.05f) isMovingBackwards = true;
        }
        anim.SetBool("IsMovingBackwards", isMovingBackwards);

        // --- 🔥 SỬA CHÍNH LÀ Ở ĐÂY NÈ ---
        if (isAiming)
        {
            // Chỉ xoay mượt theo chuột khi đang ngắm súng
            smoothLookDir = Vector3.RotateTowards(smoothLookDir, lastLookDir, turnSpeed * Time.deltaTime, 0f);
        }
        else
        {
            // Đi bình thường thì gán thẳng luôn, đổi phím A sang D là lật mặt ngay lập tức!
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