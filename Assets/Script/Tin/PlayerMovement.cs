using Fusion;
using Fusion.Addons.Physics;
using System.Collections.Generic;
using UnityEngine;

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

    [Header("--- Footstep Audio Settings ---")]
    public AudioSource footstepAudioSource;
    public AudioClip walkGrassSFX;
    public AudioClip walkDirtSFX;
    public AudioClip runGrassSFX;
    public AudioClip runDirtSFX;
    public AudioClip stealthSFX;
    public float walkStepInterval = 0.45f;
    public float runStepInterval = 0.28f;
    public float stealthStepInterval = 0.55f;

    private float footstepTimer = 0f;
    private bool stepToggle = false;

    private Rigidbody2D rb;
    private PlayerStamina staminaSystem;
    private PlayerHealth healthSystem;

    // 🔥 CÔNG TẮC KHÓA LỖI VÀNG KHÈ KHI BỊ ẢO GIÁC
    public bool isParanoiaZombie = false;

    // 🔥 BIẾN CACHE ĐỂ GIẢM LAG MULTIPLAYER
    private Vector3 lastNoisePosition;
    private float lastNoisePositionThreshold = 0.5f; // Không quét lại nếu chưa đi được 0.5 mét
    
    // 🔥 SMOOTH ANIMATION CHO REMOTE PLAYER
    private float smoothMoveX;
    private float smoothMoveY;
    private float smoothStrafeX;
    private float smoothStrafeY;
    private const float ANIM_SMOOTH_SPEED = 10f;

    // ==========================================
    // 🔥 BIẾN ĐỒNG BỘ MẠNG
    // ==========================================
    public static PlayerMovement LocalPlayerInstance;

    [Networked] public Vector2 NetMoveInput { get; set; }
    [Networked] public Vector2 NetLastLookDir { get; set; }
    [Networked] public NetworkBool NetIsMoving { get; set; }
    [Networked] public NetworkBool NetIsRunning { get; set; }
    [Networked] public NetworkBool NetIsAiming { get; set; }
    [Networked] public NetworkBool NetIsCrouching { get; set; }
    [Networked] public NetworkBool NetIsUsingItem { get; set; }
    [Networked] public float NetCameraZoom { get; set; }

    [Networked] public float NetStunTimer { get; set; }
    [Networked] public float NetAttackLockTimer { get; set; }

    [Networked] private NetworkBool PrevInputCrouch { get; set; }

    public bool isUsingItem
    {
        get => NetIsUsingItem;
        set => NetIsUsingItem = value;
    }

    private void Awake()
    {
#if UNITY_EDITOR
        AutoAssignFootstepClips();
#endif
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        AutoAssignFootstepClips();
#endif
    }

    private void AutoAssignFootstepClips()
    {
#if UNITY_EDITOR
        if (walkGrassSFX == null)
            walkGrassSFX = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Footsteps/footstep_walk(grass).mp3");
        if (walkDirtSFX == null)
            walkDirtSFX = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Footsteps/footstep_walk(dirt).mp3");
        if (runGrassSFX == null)
            runGrassSFX = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Footsteps/footstep_run(gass).mp3");
        if (runDirtSFX == null)
            runDirtSFX = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Footsteps/footstep_run(dirt).mp3");
        if (stealthSFX == null)
            stealthSFX = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Footsteps/footstep_steath.mp3");

        if (footstepAudioSource == null)
            footstepAudioSource = GetComponent<AudioSource>();
#endif
    }

    public override void Spawned()
    {
        rb = GetComponent<Rigidbody2D>();
        staminaSystem = GetComponent<PlayerStamina>();
        healthSystem = GetComponent<PlayerHealth>();
        rb.freezeRotation = true;

        if (footstepAudioSource == null)
        {
            footstepAudioSource = GetComponent<AudioSource>();
            if (footstepAudioSource == null)
            {
                footstepAudioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        if (footstepAudioSource != null)
        {
            footstepAudioSource.spatialBlend = 1f; // 3D Spatial Sound
            footstepAudioSource.minDistance = 1f;
            footstepAudioSource.maxDistance = 15f;
            footstepAudioSource.playOnAwake = false;
        }

#if UNITY_EDITOR
        AutoAssignFootstepClips();
#endif

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
            LocalPlayerInstance = this;
            var cameraController = FindAnyObjectByType<PZ_CameraController>();
            if (cameraController != null) 
            {
                cameraController.SetTarget(this.transform);
                // Reset lại trạng thái spectating khi respawn
                cameraController.SpectateTarget(null); // Just to clear if needed
                cameraController.SetTarget(this.transform);
            }
            
            isSpectating = false; // Reset cờ spectate của bản thân
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

    // 🔥 FIX: Reset static reference khi bị Despawn để ván sau tìm lại đúng player
    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (LocalPlayerInstance == this) LocalPlayerInstance = null;
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
            
            // Đồng bộ zoom của camera
            if (PZ_CameraController.Instance != null && !PZ_CameraController.Instance.isSpectatingMode)
            {
                NetCameraZoom = PZ_CameraController.Instance.GetTargetZoom();
            }
        }
    }

    public override void Render()
    {
        bool isTarget = HasInputAuthority || (PZ_CameraController.Instance != null && PZ_CameraController.Instance.isSpectatingMode && PZ_CameraController.Instance.CurrentTarget == this.transform);
        if (flashlightTransform != null)
        {
            flashlightTransform.gameObject.SetActive(isTarget);
        }

        if (healthSystem != null && (healthSystem.isDead || healthSystem.isTransforming))
        {
            if (HasInputAuthority) AutoNoiseMeter.SetMovementNoise(false, false, false);
            if (flashlightTransform != null)
            {
                flashlightTransform.gameObject.SetActive(false);
            }
            return;
        }

        // 🔥 CHỈ CẬP NHẬT ANIMATION VÀ SFX BƯỚC CHÂN NẾU KHÔNG BỊ TRÁO THÀNH ZOMBIE
        if (!isParanoiaZombie)
        {
            UpdateAnimation();
            UpdateFootstepAudio();
        }

        if (flashlightTransform != null && NetLastLookDir != Vector2.zero)
        {
            float targetAngle = Mathf.Atan2(NetLastLookDir.y, NetLastLookDir.x) * Mathf.Rad2Deg;
            Quaternion targetRotation = Quaternion.Euler(0, 0, targetAngle - 90f);
            flashlightTransform.rotation = Quaternion.Lerp(flashlightTransform.rotation, targetRotation, flashlightRotationSpeed * Time.deltaTime);
        }

        if (HasInputAuthority)
        {
            AutoNoiseMeter.SetMovementNoise(NetIsMoving, NetIsRunning, NetIsCrouching);

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

        float targetStrafeX = 0f;
        float targetStrafeY = 0f;

        if (NetIsAiming && isMovingNow)
        {
            Vector2 forwardDir = NetLastLookDir.normalized;
            Vector2 rightDir = new Vector2(forwardDir.y, -forwardDir.x);

            targetStrafeY = Vector2.Dot(NetMoveInput.normalized, forwardDir);
            targetStrafeX = Vector2.Dot(NetMoveInput.normalized, rightDir);
        }

        // 🔥 SMOOTH ANIMATION: Dùng Lerp để animation mượt hơn cho remote player
        float dt = Time.deltaTime;
        smoothStrafeX = Mathf.Lerp(smoothStrafeX, targetStrafeX, ANIM_SMOOTH_SPEED * dt);
        smoothStrafeY = Mathf.Lerp(smoothStrafeY, targetStrafeY, ANIM_SMOOTH_SPEED * dt);
        smoothMoveX = Mathf.Lerp(smoothMoveX, NetLastLookDir.x, ANIM_SMOOTH_SPEED * dt);
        smoothMoveY = Mathf.Lerp(smoothMoveY, NetLastLookDir.y, ANIM_SMOOTH_SPEED * dt);

        anim.SetFloat("StrafeX", smoothStrafeX);
        anim.SetFloat("StrafeY", smoothStrafeY);
        anim.SetFloat("MoveX", smoothMoveX);
        anim.SetFloat("MoveY", smoothMoveY);

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

    private float GetClipStartOffset(AudioClip clip)
    {
        if (clip == null) return 0f;
        string name = clip.name.ToLower();
        if (name.Contains("walk(dirt)")) return 1.21f;
        if (name.Contains("walk(grass)")) return 0.80f;
        if (name.Contains("steath") || name.Contains("stealth")) return 0.45f;
        if (name.Contains("run(gass)") || name.Contains("run(grass)")) return 0.12f;
        if (name.Contains("run(dirt)")) return 0.06f;
        return 0f;
    }

    private float GetClipEndOffset(AudioClip clip)
    {
        if (clip == null) return 0f;
        string name = clip.name.ToLower();
        if (name.Contains("walk(dirt)")) return 8.00f;
        if (name.Contains("walk(grass)")) return 4.95f;
        if (name.Contains("steath") || name.Contains("stealth")) return 4.75f;
        if (name.Contains("run(gass)") || name.Contains("run(grass)")) return 4.05f;
        if (name.Contains("run(dirt)")) return 6.00f;
        return clip.length - 0.1f;
    }

    private void UpdateFootstepAudio()
    {
        if (footstepAudioSource == null) return;

        bool isDeadOrStunned = (healthSystem != null && (healthSystem.isDead || healthSystem.isTransforming)) || NetStunTimer > 0;
        bool isMovingNow = NetIsMoving && !isDeadOrStunned;

        // 🔥 KHI NGỪNG ĐI HOẶC BỊ CHẾT/STUN: Ngắt ngay âm thanh lập tức, không phát dư tiếng!
        if (!isMovingNow)
        {
            if (footstepAudioSource.isPlaying)
            {
                footstepAudioSource.Stop();
            }
            return;
        }

        // 🔥 LIÊN KẾT VỚI AUDIO SETTING: Lấy giá trị âm lượng SFX từ PlayerPrefs
        float sfxVol = PlayerPrefs.GetFloat("GameSFXVolume", 0.8f);

        // 🔥 CỐ ĐỊNH 1 SOUND DUY NHẤT CHO MỖI TRẠNG THÁI (WALK, RUN, STEALTH)
        AudioClip targetClip = null;
        float baseVolume = 0.7f;
        float targetPitch = 1.0f;

        if (NetIsCrouching)
        {
            targetClip = stealthSFX;
            baseVolume = 0.65f; // Đi rón rén âm thanh vừa đủ nghe
            targetPitch = 0.9f;
        }
        else if (NetIsRunning)
        {
            targetClip = runGrassSFX ?? runDirtSFX; // Cố định 1 sound chạy
            baseVolume = 0.95f; // Chạy nhanh âm thanh to
            targetPitch = 0.98f;
        }
        else
        {
            targetClip = walkGrassSFX ?? walkDirtSFX; // Cố định 1 sound đi bộ
            baseVolume = 0.75f; // Đi bộ âm thanh vừa phải
            targetPitch = 1.0f;
        }

        if (targetClip == null)
        {
            if (footstepAudioSource.isPlaying) footstepAudioSource.Stop();
            return;
        }

        // Âm lượng cuối cùng = Âm lượng gốc * Âm lượng SFX trong Audio Settings
        float targetVolume = baseVolume * sfxVol;

        float startOffset = GetClipStartOffset(targetClip);
        float endOffset = GetClipEndOffset(targetClip);

        // 🔥 KHI CHUYỂN TRẠNG THÁI (Walk -> Run / Stealth): Đổi clip và phát mới ngay lập tức
        if (footstepAudioSource.clip != targetClip || !footstepAudioSource.isPlaying)
        {
            footstepAudioSource.clip = targetClip;
            footstepAudioSource.loop = true;
            footstepAudioSource.volume = targetVolume;
            footstepAudioSource.pitch = targetPitch;
            footstepAudioSource.Play();
            
            // Bắt đầu ngay từ âm thanh bước chân đầu tiên (bỏ qua khoảng im lặng đầu file)
            if (startOffset < targetClip.length)
            {
                footstepAudioSource.time = startOffset;
            }
        }
        else
        {
            // 🔥 VÒNG LẶP LIÊN TỤC VÔ TẬN KHÔNG KHOẢNG LẶNG (SEAMLESS LOOP):
            if (footstepAudioSource.time >= endOffset)
            {
                footstepAudioSource.time = startOffset;
            }

            // Điều chỉnh mượt mà pitch và volume khớp với Cài đặt Audio Setting
            float dt = Time.deltaTime;
            footstepAudioSource.volume = Mathf.Lerp(footstepAudioSource.volume, targetVolume, 10f * dt);
            footstepAudioSource.pitch = Mathf.Lerp(footstepAudioSource.pitch, targetPitch, 10f * dt);
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
         
            ZombieAI aiNew = z.GetComponentInParent<ZombieAI>();
            if (aiNew != null)
            {
                aiNew.RPC_HearSound(transform.position);
                continue;
            }

        
            ZOmbieAI_Khoa aiOld = z.GetComponentInParent<ZOmbieAI_Khoa>();
            if (aiOld != null)
            {
                aiOld.RPC_HearSound(transform.position);
            }
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

        // 🔥 GIẢM TẦN SUẤT NOISE: Chạy 0.3s, đi bộ 0.5s (thay vì 0.2s cho cả hai)
        if (NetIsRunning)
        {
            noiseEmitTimer = 0.3f;
        }
        else
        {
            noiseEmitTimer = 0.5f;
        }

        // 🔥 CACHE VỊ TRÍ: Bỏ qua nếu player chưa di chuyển đủ xa
        float distMoved = Vector3.Distance(transform.position, lastNoisePosition);
        if (distMoved < lastNoisePositionThreshold) return;
        lastNoisePosition = transform.position;

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

    // ====================================================
    // 🔥 TÍNH NĂNG SPECTATOR (THEO DÕI KHI CHẾT)
    // ====================================================

    private int spectateIndex = 0;
    private List<PlayerMovement> alivePlayers = new List<PlayerMovement>();
    private bool isSpectating = false;

    // Chạy Local trên máy người chết để chuyển Camera, không cần gửi mạng
    private void Update()
    {
        // Phải đảm bảo file này chưa bị xóa và mình là chủ cái xác
        if (this == null || !HasInputAuthority) return;

        if (healthSystem == null) healthSystem = GetComponent<PlayerHealth>();

        if (healthSystem != null && healthSystem.isDead)
        {
            // Vừa mới chết -> Ép tắt crosshair
            if (isCurrentlyAimingCursor)
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                isCurrentlyAimingCursor = false;
            }

            // Bắt đầu Spectate
            if (!isSpectating)
            {
                isSpectating = true;
                SpectateNext(0); // Tự động lia qua thằng đầu tiên
            }

            // Chặn bấm chuyển Cam nếu rê chuột vào UI (đang bấm Rút Lui)
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

            if (Input.GetKeyDown(KeyCode.A) || Input.GetMouseButtonDown(0))
            {
                SpectateNext(-1);
            }
            else if (Input.GetKeyDown(KeyCode.D) || Input.GetMouseButtonDown(1))
            {
                SpectateNext(1);
            }
        }
    }

    private void SpectateNext(int direction)
    {
        alivePlayers.Clear();
        // Quét hết PlayerMovement đang có trong Map
        var allPlayers = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);

        foreach (var p in allPlayers)
        {
            if (p == null || p.gameObject == null) continue;
            var h = p.GetComponent<PlayerHealth>();
            if (h != null && !h.isDead)
            {
                alivePlayers.Add(p);
            }
        }

        // Chết sạch cả team thì thôi, đứng nhìn xác mình
        if (alivePlayers.Count == 0) return;

        spectateIndex += direction;

        // Cuộn tròn danh sách
        if (spectateIndex < 0) spectateIndex = alivePlayers.Count - 1;
        if (spectateIndex >= alivePlayers.Count) spectateIndex = 0;

        var targetPlayer = alivePlayers[spectateIndex];
        var cameraController = FindFirstObjectByType<PZ_CameraController>();

        if (cameraController != null && targetPlayer != null)
        {
            SpriteRenderer targetSprite = targetPlayer.GetComponentInChildren<SpriteRenderer>();
            if (targetSprite != null)
            {
                // 🔥 Dùng hàm SpectateTarget mới tạo để Camera biết là đang xem ké
                cameraController.SpectateTarget(targetSprite.transform);
            }
            else
            {
                cameraController.SpectateTarget(targetPlayer.transform);
            }
        }
    }
}
