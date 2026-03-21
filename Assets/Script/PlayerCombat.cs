using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
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

    [Tooltip("Khóa chân bao nhiêu giây khi đập súng (Nên nhỏ hơn Cooldown)")]
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
            if (staminaSystem != null && staminaSystem.IsExhausted)
            {
                Debug.Log("Kiệt sức rồi, không đập báng súng nổi!");
                return;
            }

            nextBashTime = Time.time + bashCooldown;
            Bash();
        }
    }

    private void Shoot()
    {
        anim.SetTrigger("Shoot");

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

        if (playerMove != null)
        {
            playerMove.MakeNoise(bashNoiseRadius);

            // 🔥 ĐÃ SỬA: Gọi đúng hàm khóa chân tấn công mới
            playerMove.LockMovementForAttack(bashDuration);
        }

        if (staminaSystem != null)
        {
            staminaSystem.ConsumeStamina(bashStaminaCost);
        }

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
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, bashRange);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, shootNoiseRadius);
    }
}