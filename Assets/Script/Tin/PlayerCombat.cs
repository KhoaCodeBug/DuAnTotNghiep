using UnityEngine;
using Fusion;
using System.Collections;
using System.Collections.Generic;

public class PlayerCombat : NetworkBehaviour
{
    public static bool IsTerminalCombatState(bool isDead, bool isTransforming, float currentHealth) =>
        isDead || isTransforming || currentHealth <= 0f;

    public readonly struct MilitaryRespawnCombatSnapshot
    {
        public readonly string EquippedWeaponId;
        public readonly int CurrentAmmo;

        public MilitaryRespawnCombatSnapshot(string equippedWeaponId, int currentAmmo)
        {
            EquippedWeaponId = equippedWeaponId;
            CurrentAmmo = Mathf.Max(0, currentAmmo);
        }
    }

    [Header("--- Hiệu ứng Lửa đạn (Muzzle Flash) ---")]
    public Animator muzzleAnimator;
    public SpriteRenderer muzzleFlashRenderer;
    [Tooltip("Chỉnh số này khớp với thời gian chạy hết 15 frame của bạn (VD: 0.2 hoặc 0.25)")]
    public float muzzleFlashDuration = 0.2f;

    [Header("--- Cài Đặt Tương Tác Bắn ---")]
    public LayerMask enemyLayer;

    [Header("--- Hỗ Trợ Bắn Cự Ly Sát Người ---")]
    [Min(0.25f)] public float pointBlankAssistRadius = 1.2f;
    [Range(10f, 90f)] public float pointBlankAssistHalfAngle = 70f;
    [Range(0f, 0.2f)] public float bulletCastRadius = 0.08f;

    [Header("--- Audio Settings (Cận Chiến) ---")]
    public AudioSource weaponAudioSource;
    public AudioClip swingSFX;
    public AudioClip hitFleshSFX;

    private float lastDryFireTime = 0f;

    [Networked] public int currentAmmo { get; set; } = 30;
    [Networked] public NetworkString<_64> EquippedWeaponId { get; private set; }
    private bool isReloading = false;

    // 🔥 HỆ THỐNG BĂNG ĐẠN RIÊNG CHO TỪNG SÚNG
    // Cache đạn cho từng súng (key = itemName, value = số đạn hiện tại trong băng)
    private Dictionary<string, int> weaponAmmoCache = new Dictionary<string, int>();
    private string lastEquippedWeaponName = "";
    private string lastLocalRequestedWeaponName = "";

    [Header("--- Cận Chiến (Gun Bash) ---")]
    public float bashDamage = 10f;
    public float bashRange = 1f;
    public float bashCooldown = 0.8f;
    public float bashNoiseRadius = 5f;
    public float bashStaminaCost = 15f;
    public float bashDuration = 0.5f;

    [SerializeField] private Animator anim;
    private Camera mainCam;
    private PlayerMovement playerMove;
    private PlayerStamina staminaSystem;
    private InventorySystem invSys;
    private PlayerSurvival survivalSystem;
    private PlayerHealth healthSystem;

    [Networked] private TickTimer nextFireTimer { get; set; }
    [Networked] private TickTimer nextBashTimer { get; set; }
    private float muzzleFlashTimer = 0f;
    private readonly Collider2D[] pointBlankCandidates = new Collider2D[24];

    public override void Spawned()
    {
        if (anim == null) anim = PlayerVisualResolver.ResolveVisualAnimator(gameObject);
        mainCam = Camera.main;
        playerMove = GetComponent<PlayerMovement>();
        staminaSystem = GetComponent<PlayerStamina>();
        invSys = GetComponent<InventorySystem>();
        survivalSystem = GetComponent<PlayerSurvival>();
        healthSystem = GetComponent<PlayerHealth>();

        if (muzzleFlashRenderer != null) muzzleFlashRenderer.enabled = false;

        if (HasStateAuthority && HostModeSpawner.Instance != null &&
            HostModeSpawner.Instance.TryTakeMilitaryCombatSnapshot(Object.InputAuthority, out MilitaryRespawnCombatSnapshot snapshot))
        {
            EquippedWeaponId = snapshot.EquippedWeaponId;
            currentAmmo = snapshot.CurrentAmmo;
            lastEquippedWeaponName = snapshot.EquippedWeaponId;
            if (!string.IsNullOrWhiteSpace(snapshot.EquippedWeaponId))
                weaponAmmoCache[snapshot.EquippedWeaponId] = snapshot.CurrentAmmo;
        }
        else if (HasStateAuthority) currentAmmo = 0;

        AutoAssignAK47AudioClips();
    }

    public MilitaryRespawnCombatSnapshot CaptureMilitaryRespawnCombatSnapshot() =>
        new MilitaryRespawnCombatSnapshot(EquippedWeaponId.ToString(), currentAmmo);

    void Update()
    {
        // Fix Late Join & Respawn Muzzle Flash Stuck: Dùng biến đếm thời gian thực tế thay vì dựa vào Coroutine/Animator
        if (muzzleFlashRenderer != null && muzzleFlashRenderer.enabled)
        {
            if (muzzleFlashTimer > 0)
            {
                muzzleFlashTimer -= Time.deltaTime;
            }
            
            if (muzzleFlashTimer <= 0)
            {
                muzzleFlashRenderer.enabled = false;
            }
        }

        if (!HasInputAuthority) return;

        if (IsCombatUnavailable())
        {
            CancelReloadForTerminalState();
            return;
        }

        // 🔥 Kiểm tra chuyển súng mỗi frame để save/restore đạn riêng từng cây
        CheckWeaponSwitch();

        UpdateAmmoHUD();

        if (AutoUIManager.Instance != null && AutoUIManager.Instance.IsAnyMenuOpen()) return;
        if (QuestFlowUIPrototype.Instance != null && QuestFlowUIPrototype.Instance.IsQuestOverlayOpen) return;
        if (MainQuestSearchCabinet.IsLocalSearchInProgress) return;

        bool hasWeapon = HotbarHUDManager.Instance != null && HotbarHUDManager.Instance.HasGunEquipped();

        // 🔥 FIX NẠP ĐẠN: Bấm R trên phím HOẶC Bấm nút Reload trên điện thoại
        bool wantToReload = Input.GetKeyDown(KeyCode.R) || (MobileInputController.Instance != null && MobileInputController.Instance.CheckAndConsumeReload());

        ItemData equipped = GetEquippedWeapon();
        int currentMagCapacity = (equipped != null && equipped.magazineCapacity > 0) ? equipped.magazineCapacity : 30;

        if (hasWeapon && wantToReload && currentAmmo < currentMagCapacity && !isReloading)
        {
            StartCoroutine(ReloadRoutine());
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (IsCombatUnavailable())
        {
            if (muzzleFlashRenderer != null) muzzleFlashRenderer.enabled = false;
            CancelReloadForTerminalState();
            return;
        }

        if (survivalSystem != null && survivalSystem.IsSleepInputLocked) return;

        PlayerInteraction vehicleInteraction = GetComponent<PlayerInteraction>();
        if (vehicleInteraction != null && vehicleInteraction.IsInVehicle)
        {
            if (muzzleFlashRenderer != null) muzzleFlashRenderer.enabled = false;
            return;
        }

        if (GetInput(out PlayerNetworkInput input))
        {
            if (isReloading) return;
            if (!input.isAiming) return;

            bool isMeleeAttacking = playerMove != null && playerMove.NetAttackLockTimer > 0;

            bool isWeaponMasterActive = false;
            if (TryGetComponent(out Skill_WeaponMaster skillWM) && skillWM.IsWeaponMasterActive)
            {
                isWeaponMasterActive = true;
            }

            // 1. XỬ LÝ BẮN SÚNG
            ItemData equippedWeapon = GetEquippedWeapon();
            float currentFireRate = (equippedWeapon != null && equippedWeapon.fireRate > 0) ? equippedWeapon.fireRate : 0.1f;

            bool tutorialFireLocked = TutorialSession.IsActive && TutorialInputGate.FireLocked;
            if (input.isShooting && !tutorialFireLocked && nextFireTimer.ExpiredOrNotRunning(Runner) && !isMeleeAttacking)
            {
                if (HasInputAuthority)
                    AutoUIManager.Instance?.CancelTimedGameplayAction();

                if (currentAmmo > 0 || isWeaponMasterActive)
                {
                    nextFireTimer = TickTimer.CreateFromSeconds(Runner, currentFireRate);
                    Shoot(input.mouseWorldPos);
                }
                else
                {
                    if (HasInputAuthority)
                    {
                        nextFireTimer = TickTimer.CreateFromSeconds(Runner, currentFireRate);
                        if (Time.time - lastDryFireTime > 0.4f)
                        {
                            lastDryFireTime = Time.time;
                            RPC_PlayDryFireSFX();
                        }
                    }
                }
            }

            // 2. XỬ LÝ ĐẬP BÁNG SÚNG
            if (input.isBashing && nextBashTimer.ExpiredOrNotRunning(Runner) && !isMeleeAttacking)
            {
                if (staminaSystem != null && staminaSystem.currentStamina < bashStaminaCost) return;

                float meleeSpeed = survivalSystem != null ? survivalSystem.GetFatigueMeleeSpeedMultiplier() : 1f;
                nextBashTimer = TickTimer.CreateFromSeconds(Runner, bashCooldown / Mathf.Max(0.1f, meleeSpeed));
                Bash();
            }
        }
    }

    private IEnumerator ReloadRoutine()
    {
        ItemData equipped = GetEquippedWeapon();
        ItemData requiredAmmo = (equipped != null) ? equipped.ammoTypeRequired : null;

        if (invSys == null || requiredAmmo == null) yield break;

        int reserveAmmo = invSys.GetItemCount(requiredAmmo);
        if (reserveAmmo <= 0)
        {
            Debug.Log("Không có đạn dự trữ trong túi!");
            yield break;
        }

        isReloading = true;
        RPC_PlayReloadSFX();

        float duration = (requiredAmmo != null && requiredAmmo.useTime > 0) ? requiredAmmo.useTime : 4.0f;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;

            if (AutoUIManager.Instance != null)
                AutoUIManager.Instance.ShowReloadUI(timer, duration);

            if (Input.GetKey(KeyCode.LeftShift))
            {
                isReloading = false;
                RPC_StopReloadSFX();
                if (AutoUIManager.Instance != null) AutoUIManager.Instance.HideReloadUI();
                Debug.Log("Đã hủy nạp đạn để bỏ chạy!");
                yield break;
            }

            yield return null;
        }

        if (AutoUIManager.Instance != null) AutoUIManager.Instance.HideReloadUI();

        int maxMag = (equipped != null && equipped.magazineCapacity > 0) ? equipped.magazineCapacity : 30;
        int ammoNeeded = maxMag - currentAmmo;
        if (HasStateAuthority)
        {
            int ammoExtracted = invSys.ConsumeItem(requiredAmmo, ammoNeeded);
            currentAmmo = Mathf.Min(maxMag, currentAmmo + ammoExtracted);
            SyncAmmoCache();
        }
        else
        {
            RPC_RequestReload(equipped.name);
        }

        isReloading = false;
        Debug.Log("Nạp đạn xong!");
        UpdateAmmoHUD();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_RequestReload(string weaponId)
    {
        if (IsCombatUnavailable()) return;
        if (string.IsNullOrWhiteSpace(weaponId) || EquippedWeaponId.ToString() != weaponId) return;
        ItemData weapon = ItemDataLoader.LoadItem(weaponId);
        ItemData requiredAmmo = weapon != null ? weapon.ammoTypeRequired : null;
        if (weapon == null || requiredAmmo == null || invSys == null) return;

        int maxMag = Mathf.Max(1, weapon.magazineCapacity);
        int ammoNeeded = Mathf.Max(0, maxMag - currentAmmo);
        int ammoExtracted = invSys.ConsumeItem(requiredAmmo, ammoNeeded);
        currentAmmo = Mathf.Min(maxMag, currentAmmo + ammoExtracted);
        SyncAmmoCache();
    }

    private void Shoot(Vector2 mouseWorldPos)
    {
        if (IsCombatUnavailable()) return;
        if (HasInputAuthority)
            AutoUIManager.Instance?.CancelTimedGameplayAction();

        ItemData equipped = GetEquippedWeapon();

        if (HasStateAuthority)
        {
            bool consumeAmmo = true;
            if (TryGetComponent(out Skill_WeaponMaster skillWM) && skillWM.IsWeaponMasterActive)
            {
                consumeAmmo = false;
            }

            if (consumeAmmo)
            {
                currentAmmo--;
                SyncAmmoCache();
            }

            float currentNoiseRadius = (equipped != null && equipped.shootNoiseRadius > 0) ? equipped.shootNoiseRadius : 20f;
            if (playerMove != null) playerMove.MakeNoise(currentNoiseRadius);
        }

        Vector2 shootDirection = mouseWorldPos - (Vector2)transform.position;
        if (shootDirection.sqrMagnitude <= 0.0001f)
        {
            shootDirection = playerMove != null && playerMove.NetLastLookDir.sqrMagnitude > 0.0001f
                ? playerMove.NetLastLookDir
                : Vector2.up;
        }
        shootDirection.Normalize();

        if (HasStateAuthority)
        {
            shootDirection = ResolvePointBlankShootDirection(shootDirection);
            if (Runner.IsForward) RPC_ShowMuzzleFlash(shootDirection);
        }
        else if (HasInputAuthority && Runner.IsForward)
        {
            // Predict only the owner's audio. State Authority is the sole RPC
            // fan-out, and its echo is suppressed for this owner below.
            PlayAK47ShootSFX();
        }

        if (HasStateAuthority)
        {
            int pellets = (equipped != null && equipped.pelletCount > 0) ? equipped.pelletCount : 1;
            float spreadAngle = (equipped != null) ? equipped.spreadAngle : 2f;
            float currentDamage = (equipped != null && equipped.weaponDamage > 0) ? equipped.weaponDamage : 34f;
            float currentRange = (equipped != null && equipped.weaponRange > 0) ? equipped.weaponRange : 15f;

            for (int i = 0; i < pellets; i++)
            {
                Vector2 spreadDir = shootDirection;
                if (pellets > 1)
                {
                    float angleOffset = Random.Range(-spreadAngle, spreadAngle);
                    spreadDir = Quaternion.Euler(0, 0, angleOffset) * shootDirection;
                }

                // Cast bán kính rất nhỏ giúp đạn không lọt qua khe collider khi zombie
                // đang chồng sát Player, nhưng vẫn giữ độ chính xác của ray ở tầm xa.
                RaycastHit2D[] hits = bulletCastRadius > 0.001f
                    ? Physics2D.CircleCastAll(transform.position, bulletCastRadius, spreadDir, currentRange, enemyLayer)
                    : Physics2D.RaycastAll(transform.position, spreadDir, currentRange, enemyLayer);

                foreach (RaycastHit2D hit in hits)
                {
                    if (hit.collider == null) continue;

                    // 1. TRÁNH TỰ TỬ: Nếu đạn đụng phải chính cơ thể người bắn -> Bỏ qua, bay tiếp!
                    if (hit.collider.transform.root == this.transform.root || hit.collider.gameObject == this.gameObject)
                        continue;

                    float finalGunDamage = currentDamage;
                    PlayerHealth myHealth = GetComponent<PlayerHealth>();

                    if (myHealth != null && myHealth.isInPain)
                    {
                        finalGunDamage *= 0.7f;
                    }

                    // ========================================================
                    // 🔥 HỆ THỐNG FRIENDLY FIRE
                    // ========================================================
                    PlayerHealth targetPlayer = hit.collider.GetComponentInParent<PlayerHealth>();
                    if (targetPlayer != null)
                    {
                        if (targetPlayer.isBitten)
                        {
                            Debug.Log("⚠️ Đã bắn trúng người chơi bị nhiễm bệnh!");
                            targetPlayer.TakeDamageWithAttacker(finalGunDamage, false, false, Object.InputAuthority);
                            break; // Bắn trúng cơ thể thịt -> Đạn ghim lại, không bay xuyên táo nữa
                        }
                        else
                        {
                            Debug.Log("❌ Đạn bay xuyên qua người chơi khỏe mạnh!");
                            continue; // Người khỏe mạnh tàng hình với đạn -> Đạn bay tiếp tìm Zombie phía sau
                        }
                    }
                // ========================================================

                // XỬ LÝ SÁT THƯƠNG ZOMBIE THƯỜNG
                ZombieAIKhoaRebuilt rebuiltEnemy = hit.collider.GetComponentInParent<ZombieAIKhoaRebuilt>();
                if (rebuiltEnemy != null)
                {
                    if (rebuiltEnemy.NetIsDead) continue;
                    rebuiltEnemy.RPC_TakeDamage(finalGunDamage, Object.InputAuthority);
                    break;
                }

                ZOmbieAI_Khoa enemy = hit.collider.GetComponentInParent<ZOmbieAI_Khoa>();
                if (enemy != null)
                {
                    if (enemy.NetIsDead) continue;
                    enemy.RPC_TakeDamage(finalGunDamage, Object.InputAuthority);
                    break; // Đạn ghim vào Zombie, kết thúc tia đạn
                }

                ZombieHealth zombie = hit.collider.GetComponentInParent<ZombieHealth>();   
                if(zombie != null)
                {
                    if (zombie.isDead) continue;
                    zombie.RPC_TakeDamage(finalGunDamage, Object.InputAuthority);
                    break;
                }    

                // Nếu sếp có layer Tường chắn đạn nằm trong enemyLayer, thêm điều kiện break ở đây
                } // Đóng foreach
            } // Đóng for (pellets)
        }
    }

    private Vector2 ResolvePointBlankShootDirection(Vector2 requestedDirection)
    {
        int count = Physics2D.OverlapCircleNonAlloc(transform.position, pointBlankAssistRadius,
            pointBlankCandidates, enemyLayer);
        float minimumDot = Mathf.Cos(pointBlankAssistHalfAngle * Mathf.Deg2Rad);
        float bestScore = float.NegativeInfinity;
        Vector2 bestDirection = requestedDirection;

        for (int i = 0; i < count; i++)
        {
            Collider2D candidate = pointBlankCandidates[i];
            if (candidate == null || candidate.transform.root == transform.root || !IsLivingZombie(candidate))
                continue;

            Vector2 closest = candidate.ClosestPoint(transform.position);
            Vector2 toTarget = closest - (Vector2)transform.position;
            if (toTarget.sqrMagnitude <= 0.0001f)
                toTarget = (Vector2)candidate.bounds.center - (Vector2)transform.position;
            if (toTarget.sqrMagnitude <= 0.0001f)
                toTarget = requestedDirection;

            float distance = toTarget.magnitude;
            Vector2 direction = toTarget / Mathf.Max(distance, 0.0001f);
            float alignment = Vector2.Dot(requestedDirection, direction);
            if (alignment < minimumDot) continue;

            float score = alignment * 2f - distance / Mathf.Max(pointBlankAssistRadius, 0.01f);
            if (score <= bestScore) continue;
            bestScore = score;
            bestDirection = direction;
        }

        return bestDirection;
    }

    private static bool IsLivingZombie(Collider2D collider)
    {
        ZombieAIKhoaRebuilt rebuilt = collider.GetComponentInParent<ZombieAIKhoaRebuilt>();
        if (rebuilt != null) return !rebuilt.NetIsDead;

        ZOmbieAI_Khoa legacy = collider.GetComponentInParent<ZOmbieAI_Khoa>();
        if (legacy != null) return !legacy.NetIsDead;

        ZombieHealth health = collider.GetComponentInParent<ZombieHealth>();
        return health != null && !health.isDead;
    }

    private void Bash()
    {
        if (HasInputAuthority)
            AutoUIManager.Instance?.CancelTimedGameplayAction();

        int randomAttack = Random.Range(2, 5);
        // Bash presentation:
        // Local owner predicts and plays immediately on forward tick to guarantee responsive feedback.
        // StateAuthority broadcasts RPC so remote peers replicate the exact same animation.
        if (Runner.IsForward)
        {
            if (HasInputAuthority)
            {
                TriggerBashVisual(randomAttack);
            }

            if (HasStateAuthority)
            {
                RPC_PlayBashAnimation(randomAttack);
            }
        }

        if (playerMove != null) playerMove.LockMovementForAttack(bashDuration);
        if (staminaSystem != null) staminaSystem.ConsumeStamina(bashStaminaCost);

        if (HasStateAuthority)
        {
            if (playerMove != null) playerMove.MakeNoise(bashNoiseRadius);

            Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(transform.position, bashRange, enemyLayer);
            List<int> alreadyHitIDs = new List<int>(); // Dùng ID để lọc trùng hiệu quả hơn

            foreach (Collider2D enemy in hitEnemies)
            {
                float finalBashDamage = bashDamage;
                PlayerHealth myHealth = GetComponent<PlayerHealth>();

                if (myHealth != null && myHealth.isInPain)
                {
                    finalBashDamage *= 0.7f;
                }

                if (survivalSystem != null)
                {
                    finalBashDamage *= survivalSystem.GetFatigueMeleeDamageMultiplier();
                }

                // ========================================================
                // 🔥 HỆ THỐNG FRIENDLY FIRE (ÁP DỤNG KHI ĐẬP BÁNG SÚNG)
                // ========================================================
                PlayerHealth targetPlayer = enemy.GetComponentInParent<PlayerHealth>();
                if (targetPlayer != null && !alreadyHitIDs.Contains(targetPlayer.gameObject.GetInstanceID()))
                {
                    if (targetPlayer.Object != null && targetPlayer.Object.InputAuthority != Runner.LocalPlayer)
                    {
                        if (targetPlayer.isBitten)
                        {
                            Debug.Log("⚠️ Đã đập trúng người chơi bị nhiễm bệnh!");
                            targetPlayer.TakeDamageWithAttacker(finalBashDamage, false, false, Object.InputAuthority);
                            alreadyHitIDs.Add(targetPlayer.gameObject.GetInstanceID());
                        }
                        else
                        {
                            Debug.Log("❌ Người chơi này đang khỏe mạnh! Không đập được.");
                        }
                    }
                    continue; // Dừng lại ở đây vì đã xử lý xong phần đập người chơi
                }
                // ========================================================

                // XỬ LÝ ĐẬP ZOMBIE THƯỜNG
                ZombieAIKhoaRebuilt rebuiltStats = enemy.GetComponentInParent<ZombieAIKhoaRebuilt>();
                if (rebuiltStats != null && !alreadyHitIDs.Contains(rebuiltStats.GetInstanceID()))
                {
                    rebuiltStats.RPC_TakeDamage(finalBashDamage, Object.InputAuthority);
                    alreadyHitIDs.Add(rebuiltStats.GetInstanceID());
                    RPC_PlayMeleeHitFleshSFX();
                }

                ZOmbieAI_Khoa enemyStats = enemy.GetComponentInParent<ZOmbieAI_Khoa>();
                if (enemyStats != null && !alreadyHitIDs.Contains(enemyStats.GetInstanceID()))
                {
                    enemyStats.RPC_TakeDamage(finalBashDamage, Object.InputAuthority);
                    alreadyHitIDs.Add(enemyStats.GetInstanceID());
                    RPC_PlayMeleeHitFleshSFX();
                }

                //Thai 
                ZombieHealth newZombieStats = enemy.GetComponentInParent<ZombieHealth>();
                if (newZombieStats != null && !alreadyHitIDs.Contains(newZombieStats.gameObject.GetInstanceID()))
                {
                    newZombieStats.RPC_TakeDamage(finalBashDamage, Object.InputAuthority, true);
                    alreadyHitIDs.Add(newZombieStats.gameObject.GetInstanceID());
                    RPC_PlayMeleeHitFleshSFX();
                }
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_PlayBashAnimation(int randomAttack)
    {
        // Suppress echo on the predicting owner (Host or Client)
        if (HasInputAuthority) return;

        TriggerBashVisual(randomAttack);
    }

    private void TriggerBashVisual(int randomAttack)
    {
        if (anim == null) anim = PlayerVisualResolver.ResolveVisualAnimator(gameObject);
        if (HasInputAuthority) AutoNoiseMeter.ReportTransientNoise(0.48f, "CẬN CHIẾN");

        if (anim != null)
        {
            PlayerVisualResolver.SafeSetInteger(anim, "RandomBash", randomAttack);
            PlayerVisualResolver.SafeTrigger(anim, "GunBash");
        }
    }

    [Rpc(RpcSources.StateAuthority | RpcSources.InputAuthority, RpcTargets.All)]
    public void RPC_ShowMuzzleFlash(Vector2 direction)
    {
        if (!gameObject.activeInHierarchy) return;

        if (HasInputAuthority)
            AutoUIManager.Instance?.CancelTimedGameplayAction();

        // A remote teammate and a Host owner play the authoritative event.
        // A client owner already played the predicted cue in Shoot().
        if (GameplayAudioSpatializer.ShouldPlayAuthoritativeCue(
                GameplayAudioSpatializer.PlayerCue.Gunshot,
                HasInputAuthority,
                HasStateAuthority))
            PlayAK47ShootSFX();

        if (HasInputAuthority) AutoNoiseMeter.ReportTransientNoise(0.92f, "SÚNG NỔ");

        if (muzzleAnimator != null && muzzleFlashRenderer != null)
        {
            // 🔥 THIẾT LẬP SORTING ORDER NỔI LÊN PHÍA TRƯỚC PLAYER Ở BẤT KỲ HƯỚNG NÀO (KỂ CẢ HƯỚNG NAM / SOUTH)
            SpriteRenderer playerSr = PlayerVisualResolver.ResolveVisualSpriteRenderer(gameObject);
            if (playerSr != null)
            {
                muzzleFlashRenderer.sortingLayerID = playerSr.sortingLayerID;
                muzzleFlashRenderer.sortingOrder = playerSr.sortingOrder + 10;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            string directionString = DetermineDirectionFromAngle(angle);
            string animName = "Gunfire" + directionString;

            muzzleFlashRenderer.enabled = true;

            AnimatorStateInfo stateInfo = muzzleAnimator.GetCurrentAnimatorStateInfo(0);
            if (!stateInfo.IsName(animName))
            {
                muzzleAnimator.Play(animName, -1, 0f);
            }

            // Dynamic Flash Duration: Súng bắn càng nhanh (fireRate nhỏ) thì flash nháy càng nhanh tương thích
            ItemData equipped = GetEquippedWeapon();
            float currentFireRate = (equipped != null && equipped.fireRate > 0) ? equipped.fireRate : 0.1f;
            float dynamicFlashDuration = Mathf.Min(muzzleFlashDuration, currentFireRate * 0.75f);

            // Gán lại timer đếm ngược
            muzzleFlashTimer = dynamicFlashDuration;
        }
    }

    public ItemData GetEquippedWeapon()
    {
        if (!Application.isPlaying) return null;
        if (HasInputAuthority && HotbarHUDManager.Instance != null)
            return HotbarHUDManager.Instance.GetSelectedWeapon();

        string weaponId = EquippedWeaponId.ToString();
        return string.IsNullOrWhiteSpace(weaponId) ? null : ItemDataLoader.LoadItem(weaponId);
    }

    // =========================================================
    // 🔥 HỆ THỐNG CHUYỂN SÚNG: Save/Restore đạn riêng từng cây
    // =========================================================
    private void CheckWeaponSwitch()
    {
        ItemData equipped = HotbarHUDManager.Instance != null
            ? HotbarHUDManager.Instance.GetSelectedWeapon()
            : null;
        string currentWeaponId = equipped != null ? equipped.name : string.Empty;
        if (currentWeaponId == lastLocalRequestedWeaponName) return;
        lastLocalRequestedWeaponName = currentWeaponId;

        AutoUIManager.Instance?.CancelTimedGameplayAction();

        if (HasStateAuthority) ApplyAuthoritativeWeaponSwitch(currentWeaponId);
        else RPC_RequestEquipWeapon(currentWeaponId);

        // Hủy reload cục bộ nếu đổi súng giữa chừng.
        if (isReloading)
        {
            isReloading = false;
            StopAllCoroutines();
            RPC_StopReloadSFX();
            if (AutoUIManager.Instance != null) AutoUIManager.Instance.HideReloadUI();
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestEquipWeapon(string weaponId)
    {
        if (IsCombatUnavailable()) return;
        ApplyAuthoritativeWeaponSwitch(weaponId);
    }

    private bool IsCombatUnavailable() => healthSystem != null &&
        IsTerminalCombatState(healthSystem.isDead, healthSystem.isTransforming, healthSystem.currentHealth);

    private void CancelReloadForTerminalState()
    {
        if (muzzleFlashRenderer != null) muzzleFlashRenderer.enabled = false;
        if (!isReloading) return;
        isReloading = false;
        StopAllCoroutines();
        RPC_StopReloadSFX();
        AutoUIManager.Instance?.HideReloadUI();
    }

    private void ApplyAuthoritativeWeaponSwitch(string weaponId)
    {
        ItemData equipped = string.IsNullOrWhiteSpace(weaponId) ? null : ItemDataLoader.LoadItem(weaponId);
        if (equipped != null)
        {
            if (equipped.category != ItemCategory.Weapon || invSys == null || !invSys.HasItemNamed(weaponId))
                return;
            weaponId = equipped.name;
        }

        if (weaponId == lastEquippedWeaponName) return;

        // 1. SAVE đạn cho súng CŨ vào cache
        if (!string.IsNullOrEmpty(lastEquippedWeaponName))
        {
            weaponAmmoCache[lastEquippedWeaponName] = currentAmmo;
        }

        // 2. LOAD đạn cho súng MỚI từ cache
        if (equipped != null && !string.IsNullOrEmpty(weaponId))
        {
            if (weaponAmmoCache.ContainsKey(weaponId))
            {
                // Súng này đã bắn trước đó → khôi phục đạn còn lại
                currentAmmo = weaponAmmoCache[weaponId];
            }
            else
            {
                // Súng mới lần đầu cầm → đổ đầy băng đạn
                // The tutorial deliberately starts its recovered S12K empty,
                // so the player has to learn the reload step before firing.
                int fullMag = TutorialSession.IsActive ? 0 :
                    ((equipped.magazineCapacity > 0) ? equipped.magazineCapacity : 30);
                weaponAmmoCache[weaponId] = fullMag;
                currentAmmo = fullMag;
            }
        }
        else
        {
            // Không cầm súng nào → ammo = 0
            currentAmmo = 0;
        }

        lastEquippedWeaponName = weaponId;
        EquippedWeaponId = weaponId;
    }

    // Đồng bộ cache sau mỗi lần bắn hoặc nạp đạn
    private void SyncAmmoCache()
    {
        if (!string.IsNullOrEmpty(lastEquippedWeaponName))
        {
            weaponAmmoCache[lastEquippedWeaponName] = currentAmmo;
        }
    }

    public void UpdateAmmoHUD()
    {
        ItemData equipped = GetEquippedWeapon();
        ItemData requiredAmmo = (equipped != null) ? equipped.ammoTypeRequired : null;
        int reserve = (invSys != null && requiredAmmo != null) ? invSys.GetItemCount(requiredAmmo) : 0;
        if (AutoUIManager.Instance != null) AutoUIManager.Instance.UpdateAmmoUI(currentAmmo, reserve);
    }

    private string DetermineDirectionFromAngle(float angle)
    {
        angle = (angle + 360) % 360;
        if (angle < 22.5f || angle >= 337.5f) return "East";
        else if (angle >= 22.5f && angle < 67.5f) return "NorthEast";
        else if (angle >= 67.5f && angle < 112.5f) return "North";
        else if (angle >= 112.5f && angle < 157.5f) return "NorthWest";
        else if (angle >= 157.5f && angle < 202.5f) return "West";
        else if (angle >= 202.5f && angle < 247.5f) return "SouthWest";
        else if (angle >= 247.5f && angle < 292.5f) return "South";
        else if (angle >= 292.5f && angle < 337.5f) return "SouthEast";
        return "East";
    }

    // =========================================================
    // 🔥 QUẢN LÝ ÂM THANH SÚNG AK47
    // =========================================================

    private void AutoAssignAK47AudioClips()
    {
        if (weaponAudioSource == null)
        {
            // 🔥 TẠO GAMEOBJECT CON "WeaponAudio" CHUYÊN TRÁCH ÂM THANH VŨ KHÍ
            // Giải quyết triệt để lỗi dùng chung AudioSource với PlayerMovement (bị Stop() nhầm)
            Transform audioChild = transform.Find("WeaponAudio");
            if (audioChild == null)
            {
                GameObject go = new GameObject("WeaponAudio");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.zero;
                weaponAudioSource = go.AddComponent<AudioSource>();
            }
            else
            {
                weaponAudioSource = audioChild.GetComponent<AudioSource>();
                if (weaponAudioSource == null) weaponAudioSource = audioChild.gameObject.AddComponent<AudioSource>();
            }
        }

        if (weaponAudioSource != null)
        {
            GameplayAudioSpatializer.Configure(weaponAudioSource, GameplayAudioSpatializer.Profile.Gunshot);
        }

        if (swingSFX == null) swingSFX = Resources.Load<AudioClip>("Sound/Melee/melee_swing");
        if (hitFleshSFX == null) hitFleshSFX = Resources.Load<AudioClip>("Sound/Melee/melee_hit_flesh");
    }

    private void PlayAK47ShootSFX()
    {
        if (!GameplayAudioSpatializer.ShouldPlayPlayerCue(
                GameplayAudioSpatializer.PlayerCue.Gunshot,
                HasInputAuthority)) return;

        AutoAssignAK47AudioClips();
        ItemData equipped = GetEquippedWeapon();
        AudioClip shootClip = (equipped != null && equipped.customSingleShootSFX != null) ? equipped.customSingleShootSFX : null;
        if (shootClip == null) shootClip = Resources.Load<AudioClip>("Sound/Weapons/AK47/ak47_single");
        if (shootClip == null || weaponAudioSource == null) return;
        GameplayAudioSpatializer.Configure(weaponAudioSource, GameplayAudioSpatializer.Profile.Gunshot);

        float volMultiplier = (equipped != null && equipped.soundVolumeMultiplier > 0) ? equipped.soundVolumeMultiplier : 1.0f;
        float sfxVol = PlayerPrefs.GetFloat("GameSFXVolume", 0.8f);
        float finalVolume = GameplayAudioSpatializer.GetAttenuatedVolume(
            weaponAudioSource,
            GameplayAudioSpatializer.Profile.Gunshot,
            volMultiplier * sfxVol,
            HasInputAuthority);

        weaponAudioSource.pitch = Random.Range(0.98f, 1.02f);
        weaponAudioSource.PlayOneShot(shootClip, finalVolume);
    }

    [Rpc(RpcSources.StateAuthority | RpcSources.InputAuthority, RpcTargets.All)]
    public void RPC_PlayDryFireSFX()
    {
        if (!gameObject.activeInHierarchy) return;
        if (HasInputAuthority)
            AutoUIManager.Instance?.CancelTimedGameplayAction();
        if (!GameplayAudioSpatializer.ShouldPlayPlayerCue(
                GameplayAudioSpatializer.PlayerCue.DryFire,
                HasInputAuthority)) return;

        AutoAssignAK47AudioClips();
        ItemData equipped = GetEquippedWeapon();
        AudioClip dryClip = (equipped != null && equipped.customDryFireSFX != null) ? equipped.customDryFireSFX : null;
        if (dryClip == null) dryClip = Resources.Load<AudioClip>("Sound/Weapons/AK47/ak47_dry_fire");
        if (dryClip == null || weaponAudioSource == null) return;
        GameplayAudioSpatializer.Configure(weaponAudioSource, GameplayAudioSpatializer.Profile.Melee);

        float sfxVol = PlayerPrefs.GetFloat("GameSFXVolume", 0.8f);
        float finalVolume = GameplayAudioSpatializer.GetAttenuatedVolume(
            weaponAudioSource,
            GameplayAudioSpatializer.Profile.Melee,
            1.80f * sfxVol,
            HasInputAuthority);
        weaponAudioSource.pitch = 1.0f;
        weaponAudioSource.PlayOneShot(dryClip, finalVolume);
    }

    [Rpc(RpcSources.StateAuthority | RpcSources.InputAuthority, RpcTargets.All)]
    public void RPC_PlayReloadSFX()
    {
        if (!gameObject.activeInHierarchy) return;
        if (!GameplayAudioSpatializer.ShouldPlayPlayerCue(
                GameplayAudioSpatializer.PlayerCue.Reload,
                HasInputAuthority)) return;

        AutoAssignAK47AudioClips();
        ItemData equipped = GetEquippedWeapon();
        AudioClip customReload = (equipped != null && equipped.customReloadSFX != null) ? equipped.customReloadSFX : null;
        if (customReload == null) customReload = Resources.Load<AudioClip>("Sound/Weapons/AK47/ak47_reload");
        if (customReload == null || weaponAudioSource == null) return;
        GameplayAudioSpatializer.Configure(weaponAudioSource, GameplayAudioSpatializer.Profile.Melee);

        float sfxVol = PlayerPrefs.GetFloat("GameSFXVolume", 0.8f);
        float finalVolume = GameplayAudioSpatializer.GetAttenuatedVolume(
            weaponAudioSource,
            GameplayAudioSpatializer.Profile.Melee,
            1.50f * sfxVol,
            HasInputAuthority);
        weaponAudioSource.pitch = 1.0f;
        weaponAudioSource.PlayOneShot(customReload, finalVolume);
    }

    [Rpc(RpcSources.StateAuthority | RpcSources.InputAuthority, RpcTargets.All)]
    public void RPC_StopReloadSFX()
    {
        if (!GameplayAudioSpatializer.ShouldPlayPlayerCue(
                GameplayAudioSpatializer.PlayerCue.Reload,
                HasInputAuthority)) return;

        if (weaponAudioSource != null && weaponAudioSource.isPlaying)
        {
            weaponAudioSource.Stop();
        }
    }

    public void BroadcastMeleeSwingSFX()
    {
        if (!HasStateAuthority) return;
        RPC_PlayMeleeSwingSFX();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_PlayMeleeSwingSFX()
    {
        if (!gameObject.activeInHierarchy) return;
        if (!GameplayAudioSpatializer.ShouldPlayPlayerCue(
                GameplayAudioSpatializer.PlayerCue.Melee,
                HasInputAuthority)) return;

        AutoAssignAK47AudioClips();
        if (swingSFX == null) swingSFX = Resources.Load<AudioClip>("Sound/Melee/melee_swing");
        if (swingSFX == null || weaponAudioSource == null) return;
        GameplayAudioSpatializer.Configure(weaponAudioSource, GameplayAudioSpatializer.Profile.Melee);

        float sfxVol = PlayerPrefs.GetFloat("GameSFXVolume", 0.8f);
        float finalVolume = GameplayAudioSpatializer.GetAttenuatedVolume(
            weaponAudioSource,
            GameplayAudioSpatializer.Profile.Melee,
            0.90f * sfxVol,
            HasInputAuthority);
        weaponAudioSource.pitch = Random.Range(0.97f, 1.03f);
        weaponAudioSource.PlayOneShot(swingSFX, finalVolume);
    }

    [Rpc(RpcSources.StateAuthority | RpcSources.InputAuthority, RpcTargets.All)]
    public void RPC_PlayMeleeHitFleshSFX()
    {
        if (!gameObject.activeInHierarchy) return;
        if (!GameplayAudioSpatializer.ShouldPlayPlayerCue(
                GameplayAudioSpatializer.PlayerCue.Melee,
                HasInputAuthority)) return;

        AutoAssignAK47AudioClips();
        if (hitFleshSFX == null) hitFleshSFX = Resources.Load<AudioClip>("Sound/Melee/melee_hit_flesh");
        if (hitFleshSFX == null || weaponAudioSource == null) return;
        GameplayAudioSpatializer.Configure(weaponAudioSource, GameplayAudioSpatializer.Profile.Melee);

        float sfxVol = PlayerPrefs.GetFloat("GameSFXVolume", 0.8f);
        float finalVolume = GameplayAudioSpatializer.GetAttenuatedVolume(
            weaponAudioSource,
            GameplayAudioSpatializer.Profile.Melee,
            1.00f * sfxVol,
            HasInputAuthority);
        weaponAudioSource.pitch = Random.Range(0.97f, 1.03f);
        weaponAudioSource.PlayOneShot(hitFleshSFX, finalVolume);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, bashRange);
        Gizmos.color = Color.magenta;
        ItemData equipped = GetEquippedWeapon();
        float currentNoiseRadius = (equipped != null && equipped.shootNoiseRadius > 0) ? equipped.shootNoiseRadius : 20f;
        Gizmos.DrawWireSphere(transform.position, currentNoiseRadius);
    }
}
