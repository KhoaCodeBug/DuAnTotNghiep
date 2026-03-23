using UnityEngine;
using System.Collections;

public class PlayerCombat : MonoBehaviour
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
    public int currentAmmo = 30;
    private bool isReloading = false;
    // ĐÃ XÓA: public float reloadTime = 1.5f;

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

    // 🔥 MỚI: Lưu trữ túi đồ để cập nhật UI siêu mượt
    private InventorySystem invSys;

    private float nextFireTime = 0f;
    private float nextBashTime = 0f;

    void Start()
    {
        anim = GetComponent<Animator>();
        mainCam = Camera.main;
        playerMove = GetComponent<PlayerMovement>();
        staminaSystem = GetComponent<PlayerStamina>();

        // Tìm túi đồ ngay khi vào game
        invSys = FindAnyObjectByType<InventorySystem>();

        if (muzzleFlashRenderer != null) muzzleFlashRenderer.enabled = false;
    }

    void Update()
    {
        UpdateAmmoHUD();

        if (AutoUIManager.Instance != null && AutoUIManager.Instance.IsInventoryOpen()) return;

        // 🔥 ĐÃ FIX: Dời phím R lên TRƯỚC lệnh ngắm súng. Giờ bạn đi bộ vẫn bấm R nạp đạn được!
        if (Input.GetKeyDown(KeyCode.R) && currentAmmo < magazineSize && !isReloading)
        {
            StartCoroutine(ReloadRoutine());
            return;
        }

        // Đang nạp đạn thì khóa tay, không cho ngắm bắn hay đập báng súng
        if (isReloading) return;

        // TỪ ĐÂY TRỞ XUỐNG BẮT BUỘC PHẢI GIỮ CHUỘT PHẢI ĐỂ NGẮM
        if (!Input.GetMouseButton(1)) return;

        if (Input.GetMouseButton(0) && Time.time >= nextFireTime)
        {
            if (currentAmmo > 0)
            {
                nextFireTime = Time.time + fireRate;
                Shoot();
            }
            else
            {
                if (Time.time >= nextFireTime)
                {
                    nextFireTime = Time.time + fireRate;
                    Debug.Log("Súng rỗng! Bấm R để nạp đạn.");
                }
            }
        }

        if (Input.GetKeyDown(KeyCode.Space) && Time.time >= nextBashTime)
        {
            if (staminaSystem != null && staminaSystem.currentStamina < bashStaminaCost) return;

            nextBashTime = Time.time + bashCooldown;
            Bash();
        }
    }

    // 🔥 HÀM NẠP ĐẠN (Đã lấy useTime từ ItemData)
    // 🔥 HÀM NẠP ĐẠN ĐÃ TÍCH HỢP THANH ACTION BAR
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

        // Vòng lặp đếm thời gian và cập nhật UI liên tục
        while (timer < duration)
        {
            timer += Time.deltaTime;

            // Gửi số liệu cho UIManager vẽ thanh nạp đạn
            if (AutoUIManager.Instance != null)
            {
                AutoUIManager.Instance.ShowReloadUI(timer, duration);
            }

            // 🔥 TÙY CHỌN HAY: Nếu đang nạp đạn mà bấm chạy (Shift), sẽ hủy nạp đạn ngay lập tức!
            if (Input.GetKey(KeyCode.LeftShift))
            {
                isReloading = false;
                if (AutoUIManager.Instance != null) AutoUIManager.Instance.HideReloadUI();
                Debug.Log("Đã hủy nạp đạn để bỏ chạy!");
                yield break;
            }

            yield return null;
        }

        // Khi nạp xong, tắt thanh UI đi
        if (AutoUIManager.Instance != null) AutoUIManager.Instance.HideReloadUI();

        // Rút đạn từ túi đắp vào súng
        int ammoNeeded = magazineSize - currentAmmo;
        int ammoExtracted = invSys.ConsumeItem(ammoType, ammoNeeded);

        currentAmmo += ammoExtracted;
        isReloading = false;

        Debug.Log("Nạp đạn xong!");
        UpdateAmmoHUD();
    }

    private void Shoot()
    {
        currentAmmo--;

        if (playerMove != null) playerMove.MakeNoise(shootNoiseRadius);

        Vector3 mousePos = mainCam.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;
        Vector2 shootDirection = (mousePos - transform.position).normalized;

        if (muzzleAnimator != null && muzzleFlashRenderer != null)
        {
            float angle = Mathf.Atan2(shootDirection.y, shootDirection.x) * Mathf.Rad2Deg;
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

        RaycastHit2D hit = Physics2D.Raycast(transform.position, shootDirection, weaponRange, enemyLayer);
        if (hit.collider != null)
        {
            ZOmbieAI_Khoa enemy = hit.collider.GetComponentInParent<ZOmbieAI_Khoa>();
            if (enemy != null) enemy.TakeDamage(gunDamage);
        }
    }

    // 🔥 CẬP NHẬT GIAO DIỆN CỰC MƯỢT
    public void UpdateAmmoHUD()
    {
        int reserve = (invSys != null && ammoType != null) ? invSys.GetItemCount(ammoType) : 0;

        if (AutoUIManager.Instance != null)
        {
            AutoUIManager.Instance.UpdateAmmoUI(currentAmmo, reserve);
        }
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

    private void Bash()
    {
        int randomAttack = Random.Range(2, 5);
        anim.SetInteger("RandomBash", randomAttack);
        anim.SetTrigger("GunBash");

        if (playerMove != null)
        {
            playerMove.MakeNoise(bashNoiseRadius);
            playerMove.LockMovementForAttack(bashDuration);
        }

        if (staminaSystem != null) staminaSystem.ConsumeStamina(bashStaminaCost);

        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(transform.position, bashRange, enemyLayer);

        // 🔥 MỚI: Tạo một danh sách để nhớ mặt những con Zombie đã dính đòn
        System.Collections.Generic.List<ZOmbieAI_Khoa> alreadyHitZombies = new System.Collections.Generic.List<ZOmbieAI_Khoa>();

        foreach (Collider2D enemy in hitEnemies)
        {
            ZOmbieAI_Khoa enemyStats = enemy.GetComponentInParent<ZOmbieAI_Khoa>();

            // Nếu quét trúng Zombie VÀ con Zombie này CHƯA nằm trong danh sách đã bị đập
            if (enemyStats != null && !alreadyHitZombies.Contains(enemyStats))
            {
                enemyStats.TakeDamage(bashDamage);          // 1. Trừ máu
                alreadyHitZombies.Add(enemyStats);          // 2. Ghi tên vào sổ để vòng lặp sau không trừ nữa
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, bashRange);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, shootNoiseRadius);
    }
}