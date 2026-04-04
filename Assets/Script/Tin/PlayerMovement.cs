using UnityEngine;
using Fusion;
using Fusion.Addons.Physics;

[RequireComponent(typeof(NetworkRigidbody2D))]
[RequireComponent(typeof(PlayerStamina))]
public class PlayerMovement : NetworkBehaviour
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

    [Header("--- Line of Sight (Đèn pin) ---")]
    public Transform flashlightTransform;
    public float flashlightRotationSpeed = 20f;

    [Header("--- Noise Generation ---")]
    public LayerMask zombieLayer;
    public float walkNoiseRadius = 4f;
    public float runNoiseRadius = 8f;
    private float noiseEmitTimer = 0f;

    [Header("--- Animations ---")]
    public Animator anim;

    private Rigidbody2D rb;
    private PlayerStamina staminaSystem;
    private PlayerHealth healthSystem;

    // 🔥 CÔNG TẮC KHÓA LỖI VÀNG KHÈ KHI BỊ ẢO GIÁC
    public bool isParanoiaZombie = false;

    // ==========================================
    // 🔥 BIẾN ĐỒNG BỘ MẠNG
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

    [Networked] private NetworkBool PrevInputCrouch { get; set; }

    public bool isUsingItem
    {
        get => NetIsUsingItem;
        set => NetIsUsingItem = value;
    }

    public override void Spawned()
    {
        rb = GetComponent<Rigidbody2D>();
        staminaSystem = GetComponent<PlayerStamina>();
        rb.freezeRotation = true;

        if (HasStateAuthority)
        {
            NetLastLookDir = Vector2.down;
        }

        Fusion.Addons.Physics.NetworkRigidbody2D netRb = GetComponent<Fusion.Addons.Physics.NetworkRigidbody2D>();
        SpriteRenderer sprite = GetComponentInChildren<SpriteRenderer>();

        if (netRb != null && sprite != null && sprite.transform != this.transform)
        {
            netRb.InterpolationTarget = sprite.transform;
        }

        // Nếu nhân vật này LÀ CỦA MÌNH (Mình có quyền bấm nút điều khiển nó)
        if (HasInputAuthority)
        {
            var cameraController = FindAnyObjectByType<PZ_CameraController>();
            if (cameraController != null) cameraController.SetTarget(this.transform);

            // (Đèn pin vẫn giữ nguyên không làm gì cả -> Sáng bình thường)
        }
        else
        {
            // 🔥 NẾU NHÂN VẬT NÀY LÀ CỦA THẰNG BẠN (Hoặc người lạ): Tắt cầu dao đèn pin của nó đi!
            if (flashlightTransform != null)
            {
                flashlightTransform.gameObject.SetActive(false);
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (healthSystem != null && (healthSystem.isDead || healthSystem.isTransforming))
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (NetStunTimer > 0) NetStunTimer -= Runner.DeltaTime;
        if (NetAttackLockTimer > 0) NetAttackLockTimer -= Runner.DeltaTime;

        if (GetInput(out PlayerNetworkInput input))
        {
            if (NetStunTimer > 0)
            {
                input.moveInput = Vector2.zero;
                input.isAiming = false;
                input.isRunning = false;
            }
            if (NetAttackLockTimer > 0)
            {
                input.moveInput = Vector2.zero;
            }

            NetIsAiming = input.isAiming;
            NetMoveInput = input.moveInput;
            NetIsMoving = input.moveInput.magnitude > 0.1f;
            NetIsRunning = input.isRunning && NetIsMoving;

            if (input.isCrouching && !PrevInputCrouch)
            {
                NetIsCrouching = !NetIsCrouching;
            }
            PrevInputCrouch = input.isCrouching;

            if (staminaSystem.IsExhausted || NetIsCrouching)
            {
                NetIsRunning = false;
            }

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

            PlayerHealth health = GetComponent<PlayerHealth>();
            if (health != null && health.isInPain)
            {
                currentSpeed *= 0.6f;
            }

            rb.linearVelocity = input.moveInput * currentSpeed;

            staminaSystem.UpdateStamina(NetIsRunning, NetIsMoving);
            HandleMovementNoise(NetIsMoving);
        }
    }

    public override void Render()
    {
        if (healthSystem != null && (healthSystem.isDead || healthSystem.isTransforming))
        {
            return;
        }

        // 🔥 CHỈ CẬP NHẬT ANIMATION NẾU KHÔNG BỊ TRÁO THÀNH ZOMBIE
        if (!isParanoiaZombie)
        {
            UpdateAnimation();
        }

        if (flashlightTransform != null && NetLastLookDir != Vector2.zero)
        {
            float targetAngle = Mathf.Atan2(NetLastLookDir.y, NetLastLookDir.x) * Mathf.Rad2Deg;
            Quaternion targetRotation = Quaternion.Euler(0, 0, targetAngle - 90f);
            flashlightTransform.rotation = Quaternion.Lerp(flashlightTransform.rotation, targetRotation, flashlightRotationSpeed * Time.deltaTime);
        }

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
        if (!HasStateAuthority) return;

        Collider2D[] zombies = Physics2D.OverlapCircleAll(transform.position, radius, zombieLayer);
        foreach (Collider2D z in zombies)
        {
            ZOmbieAI_Khoa ai = z.GetComponentInParent<ZOmbieAI_Khoa>();
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

        if (angle < 15f || angle >= 345f) return new Vector2(1, 0);
        else if (angle >= 15f && angle < 75f) return new Vector2(1, 1);
        else if (angle >= 75f && angle < 105f) return new Vector2(0, 1);
        else if (angle >= 105f && angle < 165f) return new Vector2(-1, 1);
        else if (angle >= 165f && angle < 195f) return new Vector2(-1, 0);
        else if (angle >= 195f && angle < 255f) return new Vector2(-1, -1);
        else if (angle >= 255f && angle < 285f) return new Vector2(0, -1);
        else if (angle >= 285f && angle < 345f) return new Vector2(1, -1);

        return new Vector2(1, 0);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, walkNoiseRadius);
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, runNoiseRadius);
    }
}