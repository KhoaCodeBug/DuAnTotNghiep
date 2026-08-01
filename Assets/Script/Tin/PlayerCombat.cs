using UnityEngine;
using Fusion;
using System.Collections;
using System.Collections.Generic;

public class PlayerCombat : NetworkBehaviour
{
    [Header("--- Hiệu ứng Lửa đạn (Muzzle Flash) ---")]
    public Animator muzzleAnimator;
    public SpriteRenderer muzzleFlashRenderer;
    [Tooltip("Chỉnh số này khớp với thời gian chạy hết 15 frame của bạn (VD: 0.2 hoặc 0.25)")]
    public float muzzleFlashDuration = 0.2f;

    [Header("--- Vũ khí Tầm Xa ---")]
    public float gunDamage = 20f;
    public float fireRate = 0.2f;
    public float weaponRange = 15f;
    public LayerMask enemyLayer;
    public float shootNoiseRadius = 20f;

    [Header("--- AK47 & Melee Weapon Audio Settings ---")]
    public AudioSource weaponAudioSource;
    public AudioClip singleShotSFX;
    public AudioClip autoShotSFX;
    public AudioClip reloadSFX;
    public AudioClip dryFireSFX;
    public AudioClip swingSFX;
    public AudioClip hitFleshSFX;

    private float lastDryFireTime = 0f;

    [Header("--- Quản Lý Đạn (Ammunition) ---")]
    public ItemData ammoType;
    public int magazineSize = 30;

    [Networked] public int currentAmmo { get; set; } = 30;
    private bool isReloading = false;

    [Header("--- Cận Chiến (Gun Bash) ---")]
    public float bashDamage = 10f;
    public float bashRange = 1f;
    public float bashCooldown = 0.8f;
    public float bashNoiseRadius = 5f;
    public float bashStaminaCost = 15f;
    public float bashDuration = 0.5f;

    private Animator anim;
    private Camera mainCam;
    private PlayerMovement playerMove;
    private PlayerStamina staminaSystem;
    private InventorySystem invSys;

    [Networked] private TickTimer nextFireTimer { get; set; }
    [Networked] private TickTimer nextBashTimer { get; set; }
    private float muzzleFlashTimer = 0f;

    public override void Spawned()
    {
        anim = GetComponent<Animator>();
        mainCam = Camera.main;
        playerMove = GetComponent<PlayerMovement>();
        staminaSystem = GetComponent<PlayerStamina>();
        invSys = FindAnyObjectByType<InventorySystem>();

        if (muzzleFlashRenderer != null) muzzleFlashRenderer.enabled = false;
        if (HasStateAuthority) currentAmmo = magazineSize;
        AutoAssignAK47AudioClips();
    }

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

        UpdateAmmoHUD();

        if (AutoUIManager.Instance != null && AutoUIManager.Instance.IsInventoryOpen()) return;

        // 🔥 FIX NẠP ĐẠN: Bấm R trên phím HOẶC Bấm nút Reload trên điện thoại
        bool wantToReload = Input.GetKeyDown(KeyCode.R) || (MobileInputController.Instance != null && MobileInputController.Instance.CheckAndConsumeReload());

        if (wantToReload && currentAmmo < magazineSize && !isReloading)
        {
            StartCoroutine(ReloadRoutine());
        }
    }

    public override void FixedUpdateNetwork()
    {
        bool isDead = false;
        PlayerHealth health = GetComponent<PlayerHealth>();
        if (health != null)
        {
            isDead = health.currentHealth <= 0;
        }

        if (isDead)
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
            if (input.isShooting && nextFireTimer.ExpiredOrNotRunning(Runner) && !isMeleeAttacking)
            {
                if (currentAmmo > 0 || isWeaponMasterActive)
                {
                    nextFireTimer = TickTimer.CreateFromSeconds(Runner, fireRate);
                    Shoot(input.mouseWorldPos);
                }
                else
                {
                    if (HasInputAuthority)
                    {
                        nextFireTimer = TickTimer.CreateFromSeconds(Runner, fireRate);
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

                nextBashTimer = TickTimer.CreateFromSeconds(Runner, bashCooldown);
                Bash();
            }
        }
    }

    private IEnumerator ReloadRoutine()
    {
        if (invSys == null || ammoType == null) yield break;

        int reserveAmmo = invSys.GetItemCount(ammoType);
        if (reserveAmmo <= 0)
        {
            Debug.Log("Không có đạn dự trữ trong túi!");
            yield break;
        }

        isReloading = true;
        RPC_PlayReloadSFX();
        float duration = ammoType.useTime;
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

        int ammoNeeded = magazineSize - currentAmmo;
        int ammoExtracted = invSys.ConsumeItem(ammoType, ammoNeeded);

        if (HasStateAuthority)
        {
            currentAmmo += ammoExtracted;
        }
        else
        {
            RPC_RequestReload(ammoExtracted);
        }

        isReloading = false;
        Debug.Log("Nạp đạn xong!");
        UpdateAmmoHUD();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_RequestReload(int amountAdded)
    {
        currentAmmo += amountAdded;
    }

    private void Shoot(Vector2 mouseWorldPos)
    {
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
            }

            if (playerMove != null) playerMove.MakeNoise(shootNoiseRadius);
        }

        Vector2 shootDirection = (mouseWorldPos - (Vector2)transform.position).normalized;
        RPC_ShowMuzzleFlash(shootDirection);

        if (HasStateAuthority)
        {
            // 🔥 ĐỔI SANG RAYCAST ALL: Đạn bay xuyên thấu để lọc mục tiêu
            RaycastHit2D[] hits = Physics2D.RaycastAll(transform.position, shootDirection, weaponRange, enemyLayer);

            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider == null) continue;

                // 1. TRÁNH TỰ TỬ: Nếu đạn đụng phải chính cơ thể người bắn -> Bỏ qua, bay tiếp!
                if (hit.collider.transform.root == this.transform.root || hit.collider.gameObject == this.gameObject)
                    continue;

                float finalGunDamage = gunDamage;
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
                        targetPlayer.TakeDamage(finalGunDamage);
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
                ZOmbieAI_Khoa enemy = hit.collider.GetComponentInParent<ZOmbieAI_Khoa>();
                if (enemy != null)
                {
                    enemy.RPC_TakeDamage(finalGunDamage, Object.InputAuthority);
                    break; // Đạn ghim vào Zombie, kết thúc tia đạn
                }

                ZombieHealth zombie = hit.collider.GetComponentInParent<ZombieHealth>();   
                if(zombie != null)
                {
                    zombie.RPC_TakeDamage(finalGunDamage, Object.InputAuthority);
                    break;
                }    

                // Nếu sếp có layer Tường chắn đạn nằm trong enemyLayer, thêm điều kiện break ở đây
            }
        }
    }

    private void Bash()
    {
        int randomAttack = Random.Range(2, 5);
        RPC_PlayBashAnimation(randomAttack);

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
                            targetPlayer.TakeDamage(finalBashDamage);
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

    [Rpc(RpcSources.StateAuthority | RpcSources.InputAuthority, RpcTargets.All)]
    public void RPC_PlayBashAnimation(int randomAttack)
    {
        if (HasInputAuthority) AutoNoiseMeter.ReportTransientNoise(0.48f, "CẬN CHIẾN");

        if (anim != null)
        {
            anim.SetInteger("RandomBash", randomAttack);
            anim.SetTrigger("GunBash");
        }
    }

    [Rpc(RpcSources.StateAuthority | RpcSources.InputAuthority, RpcTargets.All)]
    public void RPC_ShowMuzzleFlash(Vector2 direction)
    {
        if (!gameObject.activeInHierarchy) return;

        // 🔥 PHÁT ÂM THANH BẮN SÚNG AK47
        PlayAK47ShootSFX();

        if (HasInputAuthority) AutoNoiseMeter.ReportTransientNoise(0.92f, "SÚNG NỔ");

        if (muzzleAnimator != null && muzzleFlashRenderer != null)
        {
            // 🔥 THIẾT LẬP SORTING ORDER NỔI LÊN PHÍA TRƯỚC PLAYER Ở BẤT KỲ HƯỚNG NÀO (KỂ CẢ HƯỚNG NAM / SOUTH)
            SpriteRenderer playerSr = GetComponent<SpriteRenderer>();
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

            // Gán lại timer đếm ngược
            muzzleFlashTimer = muzzleFlashDuration;
        }
    }

    public void UpdateAmmoHUD()
    {
        int reserve = (invSys != null && ammoType != null) ? invSys.GetItemCount(ammoType) : 0;
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
            weaponAudioSource.spatialBlend = 0f; // 2D Sound (Phát 100% âm lượng Max, không bị suy hao)
            weaponAudioSource.playOnAwake = false;
        }

        if (singleShotSFX == null) singleShotSFX = Resources.Load<AudioClip>("Sound/Weapons/AK47/ak47_single");
        if (autoShotSFX == null) autoShotSFX = Resources.Load<AudioClip>("Sound/Weapons/AK47/ak47_auto");
        if (reloadSFX == null) reloadSFX = Resources.Load<AudioClip>("Sound/Weapons/AK47/ak47_reload");
        if (dryFireSFX == null) dryFireSFX = Resources.Load<AudioClip>("Sound/Weapons/AK47/ak47_dry_fire");

        if (swingSFX == null) swingSFX = Resources.Load<AudioClip>("Sound/Melee/melee_swing");
        if (hitFleshSFX == null) hitFleshSFX = Resources.Load<AudioClip>("Sound/Melee/melee_hit_flesh");
    }

    private void PlayAK47ShootSFX()
    {
        AutoAssignAK47AudioClips();
        if (singleShotSFX == null) singleShotSFX = Resources.Load<AudioClip>("Sound/Weapons/AK47/ak47_single");
        if (singleShotSFX == null || weaponAudioSource == null)
        {
            Debug.LogError("[PlayerCombat] Không tìm thấy file âm thanh súng ak47_single!");
            return;
        }

        float sfxVol = PlayerPrefs.GetFloat("GameSFXVolume", 0.8f);

        // 🔥 PHÁT ÂM THANH BẮN SÚNG VANG TO, UY LỰC TRÊN WEAPON AUDIO SOURCE ĐỘC LẬP
        weaponAudioSource.pitch = Random.Range(0.98f, 1.02f);
        weaponAudioSource.PlayOneShot(singleShotSFX, 1.0f * sfxVol);
    }

    [Rpc(RpcSources.StateAuthority | RpcSources.InputAuthority, RpcTargets.All)]
    public void RPC_PlayDryFireSFX()
    {
        if (!gameObject.activeInHierarchy) return;
        AutoAssignAK47AudioClips();
        if (dryFireSFX == null) dryFireSFX = Resources.Load<AudioClip>("Sound/Weapons/AK47/ak47_dry_fire");
        if (dryFireSFX == null || weaponAudioSource == null) return;

        float sfxVol = PlayerPrefs.GetFloat("GameSFXVolume", 0.8f);
        weaponAudioSource.pitch = 1.0f;

        // 🔥 PHÁT TIẾNG CLICK TẠCH NỔI BẬT ĐANH RÕ (ÂM LƯỢNG 1.20f)
        weaponAudioSource.PlayOneShot(dryFireSFX, 1.20f * sfxVol);
    }

    [Rpc(RpcSources.StateAuthority | RpcSources.InputAuthority, RpcTargets.All)]
    public void RPC_PlayReloadSFX()
    {
        if (!gameObject.activeInHierarchy) return;
        AutoAssignAK47AudioClips();
        if (reloadSFX == null) reloadSFX = Resources.Load<AudioClip>("Sound/Weapons/AK47/ak47_reload");
        if (reloadSFX == null || weaponAudioSource == null) return;

        float sfxVol = PlayerPrefs.GetFloat("GameSFXVolume", 0.8f);
        weaponAudioSource.pitch = 1.0f;
        weaponAudioSource.PlayOneShot(reloadSFX, 0.90f * sfxVol);
    }

    [Rpc(RpcSources.StateAuthority | RpcSources.InputAuthority, RpcTargets.All)]
    public void RPC_StopReloadSFX()
    {
        if (weaponAudioSource != null && weaponAudioSource.isPlaying)
        {
            weaponAudioSource.Stop();
        }
    }

    public void OnMeleeSwing()
    {
        RPC_PlayMeleeSwingSFX();
    }

    [Rpc(RpcSources.StateAuthority | RpcSources.InputAuthority, RpcTargets.All)]
    public void RPC_PlayMeleeSwingSFX()
    {
        if (!gameObject.activeInHierarchy) return;
        AutoAssignAK47AudioClips();
        if (swingSFX == null) swingSFX = Resources.Load<AudioClip>("Sound/Melee/melee_swing");
        if (swingSFX == null || weaponAudioSource == null) return;

        float sfxVol = PlayerPrefs.GetFloat("GameSFXVolume", 0.8f);
        weaponAudioSource.pitch = Random.Range(0.97f, 1.03f);
        weaponAudioSource.PlayOneShot(swingSFX, 0.90f * sfxVol);
    }

    [Rpc(RpcSources.StateAuthority | RpcSources.InputAuthority, RpcTargets.All)]
    public void RPC_PlayMeleeHitFleshSFX()
    {
        if (!gameObject.activeInHierarchy) return;
        AutoAssignAK47AudioClips();
        if (hitFleshSFX == null) hitFleshSFX = Resources.Load<AudioClip>("Sound/Melee/melee_hit_flesh");
        if (hitFleshSFX == null || weaponAudioSource == null) return;

        float sfxVol = PlayerPrefs.GetFloat("GameSFXVolume", 0.8f);
        weaponAudioSource.pitch = Random.Range(0.97f, 1.03f);
        weaponAudioSource.PlayOneShot(hitFleshSFX, 1.00f * sfxVol);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, bashRange);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, shootNoiseRadius);
    }
}
