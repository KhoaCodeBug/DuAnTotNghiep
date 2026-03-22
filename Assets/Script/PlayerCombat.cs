using UnityEngine;
using System.Collections;

public class PlayerCombat : MonoBehaviour
{
    [Header("--- Hiệu ứng Lửa đạn (Muzzle Flash) ---")]
    public Animator muzzleAnimator;
    public SpriteRenderer muzzleFlashRenderer;

    // 🔥 MỚI: Cho phép bạn chỉnh thời gian tia lửa chạy
    [Tooltip("Chỉnh số này khớp với thời gian chạy hết 15 frame của bạn (VD: 0.2 hoặc 0.25)")]
    public float muzzleFlashDuration = 0.2f;

    [Header("--- Vũ khí Tầm Xa ---")]
    public float gunDamage = 20f;
    public float fireRate = 0.2f;
    public float weaponRange = 15f;
    public LayerMask enemyLayer;
    public float shootNoiseRadius = 20f;

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

    private float nextFireTime = 0f;
    private float nextBashTime = 0f;

    void Start()
    {
        anim = GetComponent<Animator>();
        mainCam = Camera.main;
        playerMove = GetComponent<PlayerMovement>();
        staminaSystem = GetComponent<PlayerStamina>();

        if (muzzleFlashRenderer != null) muzzleFlashRenderer.enabled = false;
    }

    void Update()
    {
        if (AutoUIManager.Instance != null && AutoUIManager.Instance.IsInventoryOpen()) return;

        // Phải giữ chuột phải để ngắm
        if (!Input.GetMouseButton(1)) return;

        // 🔥 ĐÃ SỬA: Đổi từ GetMouseButtonDown sang GetMouseButton để "Sấy" (Bắn liên thanh)
        if (Input.GetMouseButton(0) && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + fireRate;
            Shoot();
        }

        if (Input.GetKeyDown(KeyCode.Space) && Time.time >= nextBashTime)
        {
            if (staminaSystem != null && staminaSystem.IsExhausted) return;

            nextBashTime = Time.time + bashCooldown;
            Bash();
        }
    }

    private void Shoot()
    {
        if (playerMove != null) playerMove.MakeNoise(shootNoiseRadius);

        Vector3 mousePos = mainCam.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;
        Vector2 shootDirection = (mousePos - transform.position).normalized;

        if (muzzleAnimator != null && muzzleFlashRenderer != null)
        {
            float angle = Mathf.Atan2(shootDirection.y, shootDirection.x) * Mathf.Rad2Deg;
            string directionString = DetermineDirectionFromAngle(angle);
            string animName = "Gunfire" + directionString; // Tên Animation cần chạy

            muzzleFlashRenderer.enabled = true;

            // 🔥 MỚI: Kiểm tra xem Muzzle có đang chiếu đúng hướng này không
            AnimatorStateInfo stateInfo = muzzleAnimator.GetCurrentAnimatorStateInfo(0);

            // Nếu bạn đổi hướng bắn, hệ thống mới ép chạy lại từ frame 0.
            // Nếu xả đạn liên thanh cùng 1 hướng, cứ để nó chiếu trọn vẹn 15 frame!
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

    private IEnumerator HideMuzzleFlash()
    {
        // 🔥 ĐÃ SỬA: Đợi đủ thời gian user cài đặt thì mới tắt tia lửa đạn
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
        foreach (Collider2D enemy in hitEnemies)
        {
            ZOmbieAI_Khoa enemyStats = enemy.GetComponentInParent<ZOmbieAI_Khoa>();
            if (enemyStats != null) enemyStats.TakeDamage(bashDamage);
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