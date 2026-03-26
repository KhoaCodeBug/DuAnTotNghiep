using UnityEngine;
using Fusion;
using Fusion.Addons.Physics; // Bắt buộc để dùng NetworkRigidbody2D

[RequireComponent(typeof(NetworkRigidbody2D))] // Đã đổi sang mạng
[RequireComponent(typeof(PlayerStamina))]
public class PlayerMovement : NetworkBehaviour // Kế thừa mạng
{
    [Header("--- Movement Settings ---")]
    public float walkSpeed = 4f;
    public float runSpeed = 7f;
    public float aimSpeed = 2f;
    public float crouchSpeed = 2.5f;

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
    private PlayerStamina staminaSystem;

    // ==========================================
    // 🔥 BIẾN ĐỒNG BỘ MẠNG (ĐẢM BẢO MỌI MÁY THẤY GIỐNG NHAU)
    // ==========================================
    [Networked] public Vector2 NetMoveInput { get; set; }
    [Networked] public Vector2 NetLastLookDir { get; set; }
    [Networked] public NetworkBool NetIsMoving { get; set; }
    [Networked] public NetworkBool NetIsRunning { get; set; }
    [Networked] public NetworkBool NetIsAiming { get; set; }
    [Networked] public NetworkBool NetIsCrouching { get; set; }
    [Networked] public NetworkBool NetIsUsingItem { get; set; }

    [Networked] public float NetStunTimer { get; set; }
    [Networked] public float NetAttackLockTimer { get; set; }

    // Giữ nguyên property này để các script UI/Inventory cũ vẫn gọi được bình thường
    public bool isUsingItem
    {
        get => NetIsUsingItem;
        set => NetIsUsingItem = value;
    }

    // Tương đương hàm Awake/Start trong mạng
    public override void Spawned()
    {
        rb = GetComponent<Rigidbody2D>();
        staminaSystem = GetComponent<PlayerStamina>();
        rb.freezeRotation = true;

        if (HasStateAuthority)
        {
            NetLastLookDir = Vector2.down; // Mặc định nhìn xuống
        }

        // Tự động tìm Camera bắt nét cho riêng nhân vật của mình
        if (HasInputAuthority)
        {
            var cameraController = FindAnyObjectByType<PZ_CameraController>();
            if (cameraController != null) cameraController.SetTarget(this.transform);
        }
    }

    // Tương đương FixedUpdate (Trái tim xử lý vật lý và input mạng)
    public override void FixedUpdateNetwork()
    {
        // 1. Chạy thời gian choáng và khóa đòn đánh
        if (NetStunTimer > 0) NetStunTimer -= Runner.DeltaTime;
        if (NetAttackLockTimer > 0) NetAttackLockTimer -= Runner.DeltaTime;

        // 2. Mở bưu kiện Input từ script PlayerInputHandler2D
        if (GetInput(out PlayerNetworkInput input))
        {
            // --- XỬ LÝ KHÓA (STUN / ATTACK LOCK) ---
            if (NetStunTimer > 0)
            {
                // Bị quái cắn: Đứng im, bỏ ngắm, bỏ chạy
                input.moveInput = Vector2.zero;
                input.isAiming = false;
                input.isRunning = false;
            }
            if (NetAttackLockTimer > 0)
            {
                // Đang đập báng súng: Khóa chân, KHÔNG HỦY NGẮM (Fix lỗi GunBash của bạn)
                input.moveInput = Vector2.zero;
            }

            // Hết thể lực hoặc đang ngồi thì không được chạy
            if (staminaSystem.IsExhausted || input.isCrouching)
            {
                input.isRunning = false;
            }

            // Gắn vào biến mạng
            NetIsAiming = input.isAiming;
            NetIsRunning = input.isRunning;
            NetIsCrouching = input.isCrouching;
            NetMoveInput = input.moveInput;
            NetIsMoving = input.moveInput.magnitude > 0.1f;

            // Xử lý góc nhìn (Chuột hoặc Hướng đi)
            if (input.isAiming)
            {
                Vector2 lookVector = input.mouseWorldPos - (Vector2)transform.position;
                if (lookVector.sqrMagnitude > 0.1f)
                {
                    NetLastLookDir = SnapTo8Way(lookVector);
                }
            }
            else if (input.moveInput != Vector2.zero)
            {
                NetLastLookDir = SnapTo8Way(input.moveInput);
            }

            // Tính toán tốc độ
            float currentSpeed = walkSpeed;

            if (NetIsUsingItem) currentSpeed = walkSpeed * 0.35f;
            else if (staminaSystem.IsExhausted && !NetIsAiming) currentSpeed = walkSpeed * 0.6f;
            else if (NetIsAiming) currentSpeed = aimSpeed;
            else if (NetIsRunning) currentSpeed = runSpeed;
            else if (NetIsCrouching) currentSpeed = crouchSpeed;

            if (!NetIsAiming && !NetIsUsingItem && staminaSystem.CurrentSpeedMultiplier > 1f)
            {
                currentSpeed *= staminaSystem.CurrentSpeedMultiplier;
            }

            // Di chuyển nhân vật
            if (input.moveInput == Vector2.zero)
            {
                rb.linearVelocity = Vector2.zero;
            }
            else
            {
                Vector2 nextPosition = rb.position + input.moveInput * currentSpeed * Runner.DeltaTime;
                rb.MovePosition(nextPosition);
            }

            // Xử lý Thể lực và Tiếng ồn
            staminaSystem.UpdateStamina(NetIsRunning, NetIsMoving);
            HandleMovementNoise(NetIsMoving);
        }
    }

    // Tương đương Update (Chỉ dùng để vẽ Hình Ảnh, Animation mượt mà trên khung hình)
    public override void Render()
    {
        UpdateAnimation();

        // Chuột chỉ thay đổi trên máy của người đang chơi
        if (HasInputAuthority)
        {
            if (NetIsAiming && !isCurrentlyAimingCursor)
            {
                Cursor.SetCursor(crosshairTexture, crosshairHotSpot, CursorMode.Auto);
                isCurrentlyAimingCursor = true;
            }
            else if (!NetIsAiming && isCurrentlyAimingCursor)
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                isCurrentlyAimingCursor = false;
            }
        }
    }

    // 🔥 PHỤC HỒI 100% LOGIC ANIMATION HOÀN HẢO CỦA BẠN
    private void UpdateAnimation()
    {
        if (anim == null) return;

        bool isMovingNow = NetIsMoving;

        anim.SetBool("IsMoving", isMovingNow);
        anim.SetBool("IsRunning", NetIsRunning);
        anim.SetBool("IsAiming", NetIsAiming);
        anim.SetBool("IsExhausted", staminaSystem.IsExhausted);
        anim.SetBool("IsCrouching", NetIsCrouching);

        float strafeX = 0f;
        float strafeY = 0f;

        // Xử lý bước chân ngang/lùi chuẩn xác
        if (NetIsAiming && isMovingNow)
        {
            Vector2 forwardDir = NetLastLookDir.normalized;
            Vector2 rightDir = new Vector2(forwardDir.y, -forwardDir.x);

            strafeY = Vector2.Dot(NetMoveInput.normalized, forwardDir);
            strafeX = Vector2.Dot(NetMoveInput.normalized, rightDir);
        }

        anim.SetFloat("StrafeX", strafeX);
        anim.SetFloat("StrafeY", strafeY);

        anim.SetFloat("MoveX", NetLastLookDir.x);
        anim.SetFloat("MoveY", NetLastLookDir.y);

        // Chỉnh tốc độ Frame
        if (NetIsUsingItem && isMovingNow)
        {
            anim.speed = 0.5f;
        }
        else if (staminaSystem.IsExhausted && isMovingNow && !NetIsAiming)
        {
            anim.speed = 0.7f;
        }
        else
        {
            anim.speed = 1f;
        }
    }

    // ==========================================
    // CÁC HÀM TIỆN ÍCH GIỮ NGUYÊN BẢN GỐC
    // ==========================================

    public void LockMovement(float duration)
    {
        NetStunTimer = duration;
        rb.linearVelocity = Vector2.zero;
    }

    public void LockMovementForAttack(float duration)
    {
        NetAttackLockTimer = duration;
        rb.linearVelocity = Vector2.zero;
    }

    public void MakeNoise(float radius)
    {
        // TRÁNH LỖI MẠNG: Chỉ có máy chủ (Host) mới được gọi Zombie, 
        // để tránh việc 2 máy cùng gọi làm Zombie nghe thấy âm thanh x2
        if (!HasStateAuthority) return;

        Collider2D[] zombies = Physics2D.OverlapCircleAll(transform.position, radius, zombieLayer);
        foreach (Collider2D z in zombies)
        {
            ZOmbieAI_Khoa ai = z.GetComponentInParent<ZOmbieAI_Khoa>();
            // 🔥 ĐÃ SỬA TÊN HÀM Ở ĐÂY THÀNH RPC_HearSound
            if (ai != null) ai.RPC_HearSound(transform.position);
        }
    }

    private void HandleMovementNoise(bool isMoving)
    {
        if (!isMoving || NetIsCrouching || NetStunTimer > 0) return;

        if (noiseEmitTimer > 0)
        {
            noiseEmitTimer -= Runner.DeltaTime;
            return;
        }
        noiseEmitTimer = 0.2f;

        if (NetIsRunning) MakeNoise(runNoiseRadius);
        else MakeNoise(walkNoiseRadius);
    }

    private Vector2 SnapTo8Way(Vector2 dir)
    {
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        angle = (angle + 360) % 360;

        if (angle < 15f || angle >= 345f) return new Vector2(1, 0); // East
        else if (angle >= 15f && angle < 75f) return new Vector2(1, 1); // NorthEast
        else if (angle >= 75f && angle < 105f) return new Vector2(0, 1); // North
        else if (angle >= 105f && angle < 165f) return new Vector2(-1, 1); // NorthWest
        else if (angle >= 165f && angle < 195f) return new Vector2(-1, 0); // West
        else if (angle >= 195f && angle < 255f) return new Vector2(-1, -1); // SouthWest
        else if (angle >= 255f && angle < 285f) return new Vector2(0, -1); // South
        else if (angle >= 285f && angle < 345f) return new Vector2(1, -1); // SouthEast

        return new Vector2(1, 0);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, walkNoiseRadius);
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, runNoiseRadius);
    }
}