using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    [Header("--- Vũ khí Tầm Xa ---")]
    public float gunDamage = 20f; // Sát thương mỗi viên
    public float fireRate = 0.2f; // Thời gian giữa 2 lần bắn (giây)
    public float weaponRange = 15f; // Tầm bắn tối đa
    public LayerMask enemyLayer; // Layer của quái vật

    [Header("--- Cận Chiến (Gun Bash) ---")]
    public float bashDamage = 10f; // Sát thương đập báng súng
    public float bashRange = 1f; // Bán kính vòng đập quanh nhân vật
    public float bashCooldown = 0.8f; // Đợi 0.8s mới được đập tiếp

    private Animator anim;
    private Camera mainCam;

    // Biến đếm thời gian hồi chiêu
    private float nextFireTime = 0f;
    private float nextBashTime = 0f;

    void Start()
    {
        anim = GetComponent<Animator>();
        mainCam = Camera.main;
    }

    void Update()
    {
        // 🔥 CHỐT CHẶN 1: KHI NÀO ĐÈ CHUỘT PHẢI (AIM) MỚI ĐƯỢC COMBAT
        if (!Input.GetMouseButton(1))
        {
            return; // Lập tức thoát hàm, cấm bấm bắn hay đập khi đi bộ bình thường
        }

        // --- Xử lý Bắn Súng ---
        // Nhấp chuột trái và đã qua thời gian Delay
        if (Input.GetMouseButtonDown(0) && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + fireRate;
            Shoot();
        }

        // --- Xử lý Đập Báng Súng ---
        // Nhấn Space và đã qua thời gian hồi chiêu
        if (Input.GetKeyDown(KeyCode.Space) && Time.time >= nextBashTime)
        {
            nextBashTime = Time.time + bashCooldown;
            Bash();
        }
    }

    private void Shoot()
    {
        anim.SetTrigger("Shoot");

        // 1. Lấy vị trí dấu chấm Aim (chính là vị trí con trỏ chuột trên màn hình)
        Vector3 mousePos = mainCam.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0; // Ép về 2D

        // 2. Tính hướng bắn: Kéo 1 đường thẳng tắp TỪ TÂM NHÂN VẬT tới DẤU CHẤM AIM
        Vector2 shootDirection = (mousePos - transform.position).normalized;

        // 3. Phóng tia Raycast quét xem có trúng con nào thuộc EnemyLayer không
        RaycastHit2D hit = Physics2D.Raycast(transform.position, shootDirection, weaponRange, enemyLayer);

        if (hit.collider != null)
        {
            Debug.Log("🔫 Bắn trúng ngay đầu: " + hit.collider.name);

            // Mở comment 2 dòng này khi bạn code xong EnemyHealth nhé
            ZOmbieAI_Khoa enemy = hit.collider.GetComponent<ZOmbieAI_Khoa>();
            if (enemy != null) enemy.TakeDamage(gunDamage);
        }
    }

    private void Bash()
    {
        // Chạy Animation random đập báng súng
        int randomAttack = Random.Range(2, 5);
        anim.SetInteger("RandomBash", randomAttack);
        anim.SetTrigger("GunBash");

        // Quét một vòng tròn BAO QUANH nhân vật, con nào đứng gần là ăn đòn hết
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(transform.position, bashRange, enemyLayer);

        // Đã đồng nhất tên biến là "enemy"
        foreach (Collider2D enemy in hitEnemies)
        {
            Debug.Log("💥 Đập báng súng trúng: " + enemy.name);

            // Gọi script ZOmbieAI_Khoa từ biến "enemy"
            ZOmbieAI_Khoa enemyStats = enemy.GetComponent<ZOmbieAI_Khoa>();
            if (enemyStats != null)
            {
                enemyStats.TakeDamage(bashDamage);
            }
        }
    }

    // Hàm này vẽ vòng tròn đỏ trong Scene để bạn dễ hình dung tầm đánh cận chiến
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, bashRange);
    }
}