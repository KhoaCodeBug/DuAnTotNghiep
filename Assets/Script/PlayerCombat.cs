using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    [Header("--- Vũ khí Tầm Xa ---")]
    public float gunDamage = 20f;
    public float fireRate = 0.2f;
    public float weaponRange = 15f;
    public LayerMask enemyLayer;
    // 🔥 MỚI: Bán kính tiếng súng nổ
    public float shootNoiseRadius = 20f;

    [Header("--- Cận Chiến (Gun Bash) ---")]
    public float bashDamage = 10f;
    public float bashRange = 1f;
    public float bashCooldown = 0.8f;
    // 🔥 MỚI: Đập báng súng cũng có tiếng động nhẹ
    public float bashNoiseRadius = 5f;

    private Animator anim;
    private Camera mainCam;
    private PlayerMovement playerMove; // Lấy script Movement để mượn hàm tiếng ồn

    private float nextFireTime = 0f;
    private float nextBashTime = 0f;

    void Start()
    {
        anim = GetComponent<Animator>();
        mainCam = Camera.main;
        playerMove = GetComponent<PlayerMovement>();
    }

    void Update()
    {
        if (AutoUIManager.Instance != null && AutoUIManager.Instance.IsInventoryOpen()) return;

        if (!Input.GetMouseButton(1)) return;

        if (Input.GetMouseButtonDown(0) && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + fireRate;
            Shoot();
        }

        if (Input.GetKeyDown(KeyCode.Space) && Time.time >= nextBashTime)
        {
            nextBashTime = Time.time + bashCooldown;
            Bash();
        }
    }

    private void Shoot()
    {
        anim.SetTrigger("Shoot");

        // 🔥 MỚI: Phát ra tiếng súng vang dội gọi nguyên bầy zombie tới
        if (playerMove != null) playerMove.MakeNoise(shootNoiseRadius);

        Vector3 mousePos = mainCam.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;
        Vector2 shootDirection = (mousePos - transform.position).normalized;

        RaycastHit2D hit = Physics2D.Raycast(transform.position, shootDirection, weaponRange, enemyLayer);

        if (hit.collider != null)
        {
            ZOmbieAI_Khoa enemy = hit.collider.GetComponentInParent<ZOmbieAI_Khoa>();
            if (enemy != null) enemy.TakeDamage(gunDamage);
        }
    }

    private void Bash()
    {
        int randomAttack = Random.Range(2, 5);
        anim.SetInteger("RandomBash", randomAttack);
        anim.SetTrigger("GunBash");

        // 🔥 MỚI: Phát tiếng động nhỏ khi cận chiến
        if (playerMove != null) playerMove.MakeNoise(bashNoiseRadius);

        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(transform.position, bashRange, enemyLayer);

        foreach (Collider2D enemy in hitEnemies)
        {
            ZOmbieAI_Khoa enemyStats = enemy.GetComponentInParent<ZOmbieAI_Khoa>();
            if (enemyStats != null)
            {
                enemyStats.TakeDamage(bashDamage);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Vẽ vòng cận chiến (Màu đỏ)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, bashRange);

        // 🔥 MỚI: Vẽ vòng tiếng súng (Màu tím cánh sen cho dễ nhìn từ xa)
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, shootNoiseRadius);
    }
}