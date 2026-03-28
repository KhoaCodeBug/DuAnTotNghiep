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

    public override void Spawned()
    {
        anim = GetComponent<Animator>();
        mainCam = Camera.main;
        playerMove = GetComponent<PlayerMovement>();
        staminaSystem = GetComponent<PlayerStamina>();
        invSys = FindAnyObjectByType<InventorySystem>();

        if (muzzleFlashRenderer != null) muzzleFlashRenderer.enabled = false;
        if (HasStateAuthority) currentAmmo = magazineSize;
    }

    void Update()
    {
        if (!HasInputAuthority) return;

        UpdateAmmoHUD();

        if (AutoUIManager.Instance != null && AutoUIManager.Instance.IsInventoryOpen()) return;

        if (Input.GetKeyDown(KeyCode.R) && currentAmmo < magazineSize && !isReloading)
        {
            StartCoroutine(ReloadRoutine());
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (GetInput(out PlayerNetworkInput input))
        {
            if (isReloading) return;
            if (!input.isAiming) return;

            bool isMeleeAttacking = playerMove != null && playerMove.NetAttackLockTimer > 0;

            // ====================================================
            // 🔥 HỎI THĂM SKILL: Lục xem có Bậc Thầy Vũ Khí không và đang bật không?
            // ====================================================
            bool isWeaponMasterActive = false;
            if (TryGetComponent(out Skill_WeaponMaster skillWM) && skillWM.IsWeaponMasterActive)
            {
                isWeaponMasterActive = true;
            }

            // 1. XỬ LÝ BẮN SÚNG
            if (input.isShooting && nextFireTimer.ExpiredOrNotRunning(Runner) && !isMeleeAttacking)
            {
                // 🔥 ĐÃ SỬA: Băng đạn > 0 HOẶC đang bật Bậc Thầy Vũ Khí (dù hết đạn vẫn cho bắn)
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
                        Debug.Log("Súng rỗng! Bấm R để nạp đạn.");
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
        // 1. Máy chủ (Host) trừ đạn và gọi quái nghe tiếng súng
        if (HasStateAuthority)
        {
            // ====================================================
            // 🔥 TRỪ ĐẠN THÔNG MINH
            // ====================================================
            bool consumeAmmo = true;
            if (TryGetComponent(out Skill_WeaponMaster skillWM) && skillWM.IsWeaponMasterActive)
            {
                consumeAmmo = false; // Phá luật, không trừ đạn!
            }

            if (consumeAmmo)
            {
                currentAmmo--;
            }
            // ====================================================

            if (playerMove != null) playerMove.MakeNoise(shootNoiseRadius);
        }

        Vector2 shootDirection = (mouseWorldPos - (Vector2)transform.position).normalized;
        RPC_ShowMuzzleFlash(shootDirection);

        if (HasStateAuthority)
        {
            RaycastHit2D hit = Physics2D.Raycast(transform.position, shootDirection, weaponRange, enemyLayer);
            if (hit.collider != null)
            {
                ZOmbieAI_Khoa enemy = hit.collider.GetComponentInParent<ZOmbieAI_Khoa>();
                if (enemy != null)
                {
                    float finalGunDamage = gunDamage;
                    PlayerHealth health = GetComponent<PlayerHealth>();

                    if (health != null && health.isInPain)
                    {
                        finalGunDamage *= 0.7f;
                    }

                    enemy.RPC_TakeDamage(finalGunDamage, Object.InputAuthority);
                }
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
            List<ZOmbieAI_Khoa> alreadyHitZombies = new List<ZOmbieAI_Khoa>();

            foreach (Collider2D enemy in hitEnemies)
            {
                ZOmbieAI_Khoa enemyStats = enemy.GetComponentInParent<ZOmbieAI_Khoa>();
                if (enemyStats != null && !alreadyHitZombies.Contains(enemyStats))
                {
                    float finalBashDamage = bashDamage;
                    PlayerHealth health = GetComponent<PlayerHealth>();

                    if (health != null && health.isInPain)
                    {
                        finalBashDamage *= 0.7f;
                    }

                    enemyStats.RPC_TakeDamage(finalBashDamage, Object.InputAuthority);
                    alreadyHitZombies.Add(enemyStats);
                }
            }
        }
    }

    [Rpc(RpcSources.StateAuthority | RpcSources.InputAuthority, RpcTargets.All)]
    public void RPC_PlayBashAnimation(int randomAttack)
    {
        if (anim != null)
        {
            anim.SetInteger("RandomBash", randomAttack);
            anim.SetTrigger("GunBash");
        }
    }

    [Rpc(RpcSources.StateAuthority | RpcSources.InputAuthority, RpcTargets.All)]
    public void RPC_ShowMuzzleFlash(Vector2 direction)
    {
        if (muzzleAnimator != null && muzzleFlashRenderer != null)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            string directionString = DetermineDirectionFromAngle(angle);
            string animName = "Gunfire" + directionString;

            muzzleFlashRenderer.enabled = true;

            AnimatorStateInfo stateInfo = muzzleAnimator.GetCurrentAnimatorStateInfo(0);
            if (!stateInfo.IsName(animName))
            {
                muzzleAnimator.Play(animName, -1, 0f);
            }

            StopCoroutine("HideMuzzleFlash");
            StartCoroutine("HideMuzzleFlash");
        }
    }

    public void UpdateAmmoHUD()
    {
        int reserve = (invSys != null && ammoType != null) ? invSys.GetItemCount(ammoType) : 0;
        if (AutoUIManager.Instance != null) AutoUIManager.Instance.UpdateAmmoUI(currentAmmo, reserve);
    }

    private IEnumerator HideMuzzleFlash()
    {
        yield return new WaitForSeconds(muzzleFlashDuration);
        if (muzzleFlashRenderer != null) muzzleFlashRenderer.enabled = false;
    }

    private string DetermineDirectionFromAngle(float angle)
    {
        angle = (angle + 360) % 360;
        if (angle < 15f || angle >= 345f) return "East";
        else if (angle >= 15f && angle < 75f) return "NorthEast";
        else if (angle >= 75f && angle < 105f) return "North";
        else if (angle >= 105f && angle < 165f) return "NorthWest";
        else if (angle >= 165f && angle < 195f) return "West";
        else if (angle >= 195f && angle < 255f) return "SouthWest";
        else if (angle >= 255f && angle < 285f) return "South";
        else if (angle >= 285f && angle < 345f) return "SouthEast";
        return "East";
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, bashRange);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, shootNoiseRadius);
    }
}