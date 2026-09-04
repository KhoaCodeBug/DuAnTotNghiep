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

    [Header("--- Isometric Movement Projection ---")]
    [SerializeField, Range(0.25f, 1f)]
    [Tooltip("Vertical compression of Main's 2:1 isometric Grid. Keep at 0.5 unless the map projection changes.")]
    private float isometricVerticalScale = IsometricMovementProjection.DefaultVerticalScale;

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
    [Header("--- Thai Zombie Hearing Balance ---")]
    public float thaiZombieWalkNoiseRadius = 2f;
    public int thaiZombieWalkResponderLimit = 1;
    public int thaiZombieRunResponderLimit = 3;
    public int thaiZombieLoudResponderLimit = 5;
    [Range(0f, 1f)] public float thaiZombieWalkNoiseUrgency = 0.35f;
    [Range(0f, 1f)] public float thaiZombieRunNoiseUrgency = 0.85f;
    [Range(0f, 1f)] public float thaiZombieLoudNoiseUrgency = 1f;
    private float noiseEmitTimer = 0f;

    [Header("--- Animations ---")]
    public Animator anim;

    [Header("--- Footstep Audio Settings (Animation Events) ---")]
    public AudioSource footstepAudioSource;
    public AudioClip walkSFX;
    public AudioClip runSFX;
    private bool cinematicPresentationSuppressed;

    private Rigidbody2D rb;
    private PlayerStamina staminaSystem;
    private PlayerHealth healthSystem;
    private PlayerSurvival survivalSystem;

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
    private struct ThaiZombieNoiseCandidate
    {
        public ZombieAI ai;
        public float distance;

        public ThaiZombieNoiseCandidate(ZombieAI ai, float distance)
        {
            this.ai = ai;
            this.distance = distance;
        }
    }

    private struct RebuiltZombieNoiseCandidate
    {
        public ZombieAIKhoaRebuilt ai;
        public float distance;

        public RebuiltZombieNoiseCandidate(ZombieAIKhoaRebuilt ai, float distance)
        {
            this.ai = ai;
            this.distance = distance;
        }
    }

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
    [Networked] public NetworkBool NetIsVehicleBraking { get; set; }
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
        if (walkSFX == null)
            walkSFX = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Sound/Footsteps/player_walk.wav");
        if (runSFX == null)
            runSFX = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Sound/Footsteps/player_run.wav");

        if (footstepAudioSource == null)
            footstepAudioSource = GetComponent<AudioSource>();
#endif
    }

    public override void Spawned()
    {
        rb = GetComponent<Rigidbody2D>();
        staminaSystem = GetComponent<PlayerStamina>();
        healthSystem = GetComponent<PlayerHealth>();
        survivalSystem = GetComponent<PlayerSurvival>();
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
            GameplayAudioSpatializer.Configure(footstepAudioSource, GameplayAudioSpatializer.Profile.Footstep);
        }

#if UNITY_EDITOR
        AutoAssignFootstepClips();
#endif

        if (HasStateAuthority)
        {
            NetLastLookDir = Vector2.down;
        }

        if (anim == null)
        {
            anim = GetComponentInChildren<Animator>();
        }

        Fusion.Addons.Physics.NetworkRigidbody2D netRb = GetComponent<Fusion.Addons.Physics.NetworkRigidbody2D>();
        if (netRb != null && netRb.InterpolationTarget == null)
        {
            Transform visualTransform = transform.Find("Visual");
            if (visualTransform != null)
            {
                netRb.InterpolationTarget = visualTransform;
            }
            else
            {
                SpriteRenderer sprite = GetComponentInChildren<SpriteRenderer>();
                if (sprite != null && sprite.transform != this.transform)
                {
                    netRb.InterpolationTarget = sprite.transform;
                }
            }
        }

        // Nếu nhân vật này LÀ CỦA MÌNH (Mình có quyền bấm nút điều khiển nó)
        if (HasInputAuthority)
        {
            LocalPlayerInstance = this;
            var cameraController = FindAnyObjectByType<PZ_CameraController>();
            if (cameraController != null) 
            {
                Transform targetToFollow = netRb != null && netRb.InterpolationTarget != null
                    ? netRb.InterpolationTarget
                    : transform;
                cameraController.SetTarget(targetToFollow);
                // Reset lại trạng thái spectating khi respawn
                cameraController.SpectateTarget(null); // Just to clear if needed
                cameraController.SetTarget(targetToFollow);
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
            NetIsVehicleBraking = false;
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (survivalSystem != null && survivalSystem.IsSleepInputLocked)
        {
            NetMoveInput = Vector2.zero;
            NetIsMoving = false;
            NetIsRunning = false;
            NetIsAiming = false;
            NetIsVehicleBraking = false;
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (NetStunTimer > 0) NetStunTimer -= Runner.DeltaTime;
        if (NetAttackLockTimer > 0) NetAttackLockTimer -= Runner.DeltaTime;

        if (GetInput(out PlayerNetworkInput input))
        {
            // Keep this behaviour active while seated so the driver's input is
            // still replicated to the authoritative vehicle. The player body
            // itself must never move independently of the vehicle.
            PlayerInteraction interaction = GetComponent<PlayerInteraction>();
            bool isInVehicle = interaction != null && interaction.IsInVehicle;

            if (isInVehicle)
            {
                NetMoveInput = input.moveInput;
                NetIsVehicleBraking = input.isVehicleBraking;
                NetIsMoving = false;
                NetIsRunning = false;
                rb.linearVelocity = Vector2.zero;
                return;
            }

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

            // Tutorial overlays freeze locomotion without freezing Fusion's
            // network simulation. Aiming stays available for its own lesson.
            if (TutorialSession.IsActive && TutorialInputGate.MovementLocked)
            {
                input.moveInput = Vector2.zero;
                input.isRunning = false;
            }

            Vector2 worldMoveInput = IsometricMovementProjection.ProjectInput(
                input.moveInput,
                isometricVerticalScale);

            NetIsAiming = input.isAiming;
            NetIsVehicleBraking = false;
            NetMoveInput = worldMoveInput;
            NetIsMoving = worldMoveInput.magnitude > 0.1f;
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
            else if (worldMoveInput != Vector2.zero)
            {
                NetLastLookDir = SnapTo8Way(worldMoveInput);
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

            if (survivalSystem != null)
            {
                currentSpeed *= survivalSystem.GetFatigueMovementMultiplier();
            }

            rb.linearVelocity = worldMoveInput * currentSpeed;

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
        bool isTarget = false;
        if (PZ_CameraController.Instance != null && PZ_CameraController.Instance.isSpectatingMode)
        {
            Transform camTarget = PZ_CameraController.Instance.CurrentTarget;
            if (camTarget != null)
            {
                isTarget = (camTarget == transform || camTarget.IsChildOf(transform) || transform.IsChildOf(camTarget));
            }
        }
        else
        {
            isTarget = HasInputAuthority;
        }
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
            if (!cinematicPresentationSuppressed) UpdateFootstepAudio();
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

    private void UpdateFootstepAudio()
    {
        // 🔥 VÌ ĐÃ DÙNG ANIMATION EVENT NÊN ÂM THANH DO CÁC EVENT TRÊN CLIP TỰ KÍCH HOẠT.
        // Hàm này chỉ đảm bảo ngắt âm thanh lập tức khi nhân vật đứng yên / bị chết / bị stun / rón rén.
        if (footstepAudioSource == null) return;
        if (!GameplayAudioSpatializer.ShouldPlayPlayerCue(
                GameplayAudioSpatializer.PlayerCue.Footstep,
                HasInputAuthority))
        {
            if (footstepAudioSource.isPlaying) footstepAudioSource.Stop();
            return;
        }

        bool isDeadOrStunned = (healthSystem != null && (healthSystem.isDead || healthSystem.isTransforming)) || NetStunTimer > 0;
        if (!NetIsMoving || isDeadOrStunned || NetIsCrouching)
        {
            if (footstepAudioSource.isPlaying) footstepAudioSource.Stop();
        }
    }

    // =========================================================
    // 🔥 HÀM KÍCH HOẠT TỪ ANIMATION EVENT (ANIMATION WINDOW)
    // =========================================================
    
    private float lastFootstepAudioTime = 0f;
    private int lastFootstepFrame = -1;

    /// <summary>
    /// Gọi hàm này từ Animation Event tại đúng frame chân chạm đất (tự chọn sound Walk/Run)
    /// </summary>
    public void OnFootstep()
    {
        PlaySingleFootstepBeat();
    }

    /// <summary>
    /// Gọi hàm này từ Animation Event dành riêng cho clip Đi bộ (Walk)
    /// </summary>
    public void OnWalkFootstep()
    {
        PlaySpecificFootstep(walkSFX, 0.75f);
    }

    /// <summary>
    /// Gọi hàm này từ Animation Event dành riêng cho clip Chạy (Run)
    /// </summary>
    public void OnRunFootstep()
    {
        PlaySpecificFootstep(runSFX, 0.95f);
    }

    /// <summary>
    /// Gọi hàm này từ Animation Event khi vung vũ khí chém cận chiến (Attack2, Attack3, Attack4)
    /// </summary>
    public void OnMeleeSwing()
    {
        // Animation events also run on predicted/local proxy animators. Only
        // State Authority may fan this sound out, otherwise Fusion rejects the
        // RPC and Host/Client can each emit a duplicate swing.
        if (!HasStateAuthority) return;
        if (TryGetComponent(out PlayerCombat combat))
        {
            combat.BroadcastMeleeSwingSFX();
        }
    }

    public void PlaySingleFootstepBeat()
    {
        AudioClip clip = NetIsRunning ? runSFX : walkSFX;
        float baseVol = NetIsRunning ? 0.95f : 0.75f;
        PlaySpecificFootstep(clip, baseVol);
    }

    private void PlaySpecificFootstep(AudioClip clip, float baseVol)
    {
        if (cinematicPresentationSuppressed) return;
        if (!GameplayAudioSpatializer.ShouldPlayPlayerCue(
                GameplayAudioSpatializer.PlayerCue.Footstep,
                HasInputAuthority)) return;

        // 🔥 CHỈ PHÁT ÂM THANH CHO PLAYER BẢN THÂN (Tránh máy khách/proxy phát đúp 2 lần)
        if (footstepAudioSource == null) footstepAudioSource = GetComponent<AudioSource>();
        if (footstepAudioSource == null) return;

        bool isDeadOrStunned = (healthSystem != null && (healthSystem.isDead || healthSystem.isTransforming)) || NetStunTimer > 0;
        // 🔥 KHI AIMING (NHẮM), RÓN RÉN (CROUCH), CHẾT, HOẶC ĐỨNG YÊN => TẮT HOÀN TOÀN ÂM THANH (0 SOUND)
        if (!NetIsMoving || isDeadOrStunned || NetIsCrouching || NetIsAiming) return;

        // 🔥 TRIỆT TIÊU LỖI TRỒNG 3-4 TẦNG SOUND:
        // 1. Kiểm tra không cho phát trùng Frame trong 2D Blend Tree
        if (Time.frameCount == lastFootstepFrame) return;
        // 2. Khoảng cách tối thiểu giữa 2 bước chân >= 0.20s
        if (Time.time - lastFootstepAudioTime < 0.20f) return;

        lastFootstepFrame = Time.frameCount;
        lastFootstepAudioTime = Time.time;

        if (clip == null)
        {
            if (NetIsRunning) clip = runSFX ?? Resources.Load<AudioClip>("Sound/Footsteps/player_run");
            else clip = walkSFX ?? Resources.Load<AudioClip>("Sound/Footsteps/player_walk");
        }
        if (clip == null) return;

        float sfxVol = PlayerPrefs.GetFloat("GameSFXVolume", 0.8f);
        float finalVol = GameplayAudioSpatializer.GetAttenuatedVolume(
            footstepAudioSource,
            GameplayAudioSpatializer.Profile.Footstep,
            baseVol * sfxVol,
            HasInputAuthority);

        // 🔥 TRIỆT TIÊU 100% LỖI TRỒNG TẦNG SOUND: Dừng ngay âm đuôi bước cũ trước khi phát bước mới!
        // Không dùng PlayOneShot vì PlayOneShot làm các âm đuôi chồng lên nhau thành 3-4 tầng sound.
        footstepAudioSource.Stop();
        footstepAudioSource.clip = clip;
        footstepAudioSource.volume = finalVol;
        footstepAudioSource.pitch = 1.0f;
        footstepAudioSource.Play();
    }

    public void SetCinematicPresentationSuppressed(bool suppressed)
    {
        cinematicPresentationSuppressed = suppressed;
        if (suppressed && footstepAudioSource != null && footstepAudioSource.isPlaying)
            footstepAudioSource.Stop();
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

    public void MakeNoise(float radius, bool useThaiEnhancedHearing = true, int thaiResponderLimit = -1, float thaiBaseRadiusOverride = -1f, float thaiNoiseUrgency = -1f)
    {
        if (!HasStateAuthority) return;

        const float maxThaiZombieHearingMultiplier = 2f;
        float thaiBaseRadius = thaiBaseRadiusOverride > 0f ? thaiBaseRadiusOverride : radius;
        float thaiScanRadius = useThaiEnhancedHearing ? thaiBaseRadius * maxThaiZombieHearingMultiplier : thaiBaseRadius;
        float scanRadius = Mathf.Max(radius, thaiScanRadius);
        int maxThaiResponders = thaiResponderLimit < 0 ? thaiZombieLoudResponderLimit : thaiResponderLimit;
        float urgency = thaiNoiseUrgency < 0f ? thaiZombieLoudNoiseUrgency : Mathf.Clamp01(thaiNoiseUrgency);

        Collider2D[] zombies = Physics2D.OverlapCircleAll(transform.position, scanRadius, zombieLayer);
        HashSet<int> notifiedZombies = new HashSet<int>();
        List<ThaiZombieNoiseCandidate> thaiCandidates = new List<ThaiZombieNoiseCandidate>();
        List<RebuiltZombieNoiseCandidate> rebuiltCandidates = new List<RebuiltZombieNoiseCandidate>();

        foreach (Collider2D z in zombies)
        {
         
            ZombieAI aiNew = z.GetComponentInParent<ZombieAI>();
            if (aiNew != null)
            {
                int id = aiNew.GetInstanceID();
                // ZombieThai2 has its own deliberately tuned hearing profile.
                // Keep legacy Thai zombies and rebuilt Khoa zombies at the
                // shared base radius when walking, while V2 can be adjusted
                // independently without changing the player's audible SFX.
                float hearingRadius = aiNew.UsesBlindV2Rules
                    ? thaiBaseRadius * aiNew.HearingRangeMultiplier
                    : (useThaiEnhancedHearing ? thaiBaseRadius * aiNew.HearingRangeMultiplier : thaiBaseRadius);
                float distance = Vector2.Distance(transform.position, aiNew.transform.position);
                if (!notifiedZombies.Contains(id) && distance <= hearingRadius)
                {
                    notifiedZombies.Add(id);
                    thaiCandidates.Add(new ThaiZombieNoiseCandidate(aiNew, distance));
                }
                continue;
            }

        
            ZombieAIKhoaRebuilt aiRebuilt = z.GetComponentInParent<ZombieAIKhoaRebuilt>();
            if (aiRebuilt != null)
            {
                int id = aiRebuilt.GetInstanceID();
                float distance = Vector2.Distance(transform.position, aiRebuilt.transform.position);
                // The rebuilt AI uses the same balanced hearing radius/responder
                // budget as the newer zombie implementation. Walking therefore
                // alerts one nearby zombie at 2m, not every Khoa zombie at 4m.
                float hearingRadius = thaiBaseRadius;
                if (!notifiedZombies.Contains(id) && distance <= hearingRadius)
                {
                    notifiedZombies.Add(id);
                    rebuiltCandidates.Add(new RebuiltZombieNoiseCandidate(aiRebuilt, distance));
                }
                continue;
            }

            ZOmbieAI_Khoa aiOld = z.GetComponentInParent<ZOmbieAI_Khoa>();
            if (aiOld != null)
            {
                int id = aiOld.GetInstanceID();
                if (!notifiedZombies.Contains(id) && Vector2.Distance(transform.position, aiOld.transform.position) <= radius)
                {
                    notifiedZombies.Add(id);
                    aiOld.RPC_HearSound(transform.position);
                }
            }
        }

        thaiCandidates.Sort((a, b) => a.distance.CompareTo(b.distance));
        int notifyCount = Mathf.Min(Mathf.Max(maxThaiResponders, 0), thaiCandidates.Count);
        for (int i = 0; i < notifyCount; i++)
        {
            if (thaiCandidates[i].ai != null)
            {
                thaiCandidates[i].ai.RPC_HearSoundWithUrgency(transform.position, urgency, Object.InputAuthority);
            }
        }

        rebuiltCandidates.Sort((a, b) => a.distance.CompareTo(b.distance));
        int rebuiltNotifyCount = Mathf.Min(Mathf.Max(maxThaiResponders, 0), rebuiltCandidates.Count);
        for (int i = 0; i < rebuiltNotifyCount; i++)
        {
            if (rebuiltCandidates[i].ai != null)
                rebuiltCandidates[i].ai.RPC_HearSoundWithUrgency(transform.position, urgency);
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

        if (NetIsRunning)
        {
            MakeNoise(runNoiseRadius, true, thaiZombieRunResponderLimit, -1f, thaiZombieRunNoiseUrgency);
        }
        else
        {
            MakeNoise(walkNoiseRadius, false, thaiZombieWalkResponderLimit, thaiZombieWalkNoiseRadius, thaiZombieWalkNoiseUrgency);
        }
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
