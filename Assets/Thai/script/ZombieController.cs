using UnityEngine;
using UnityEngine.AI; // Thư viện để dùng NavMesh (Tìm đường)

public class ZombieController : MonoBehaviour
{
    [Header("Mục tiêu & Di chuyển")]
    public Transform player;          // Kéo nhân vật Player vào đây
    private NavMeshAgent agent;
    private Animator anim;

    [Header("Chỉ số Chiến đấu")]
    public int maxHealth = 100;
    private int currentHealth;
    public float attackRange = 1.2f;  // Khoảng cách Zombie dừng lại để bắt đầu đánh
    public float damageRadius = 1.5f; // Tầm tay thực tế vung tới (thường to hơn attackRange một chút)
    public float attackCooldown = 1.5f; // Thời gian nghỉ giữa các đòn đánh

    private bool isDead = false;
    private float attackTimer = 0f;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
        currentHealth = maxHealth;

        // Quan trọng cho game 2D: Tắt tự động xoay của NavMesh để không làm hỏng Sprite
        agent.updateRotation = false;
        agent.updateUpAxis = false;
    }

    void Update()
    {
        // Nếu đã chết hoặc không có Player thì không làm gì cả
        if (isDead || player == null) return;

        // Đo khoảng cách tới Player
        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        // Trừ thời gian chờ đánh
        if (attackTimer > 0) attackTimer -= Time.deltaTime;

        // 1. CHẠY THEO PLAYER (Khi ở ngoài tầm đánh)
        if (distanceToPlayer > attackRange)
        {
            agent.isStopped = false;
            agent.SetDestination(player.position);

            anim.SetBool("isRunning", true);

            // Báo cho Animator biết đang chạy hướng nào để bật clip 8 hướng
            UpdateAnimatorDirection(agent.velocity.normalized);
        }
        // 2. TẤN CÔNG PLAYER (Khi đã vào trong tầm đánh)
        else
        {
            agent.isStopped = true;
            anim.SetBool("isRunning", false);

            // Xoay mặt nhìn thẳng vào Player kể cả khi đang đứng im để đánh
            Vector2 dirToPlayer = (player.position - transform.position).normalized;
            UpdateAnimatorDirection(dirToPlayer);

            // Nếu đã hồi chiêu xong thì quất ngẫu nhiên 1 trong 4 đòn
            if (attackTimer <= 0)
            {
                TriggerRandomAttack();
            }
        }
    }

    // Hàm phụ trợ giúp truyền DirX, DirY vào Animator
    void UpdateAnimatorDirection(Vector2 direction)
    {
        if (direction != Vector2.zero)
        {
            anim.SetFloat("DirX", direction.x);
            anim.SetFloat("DirY", direction.y);
        }
    }

    // Chọn ngẫu nhiên Atk1, Atk2, Atk3 hoặc Atk4
    void TriggerRandomAttack()
    {
        int randomAtk = Random.Range(1, 5); // Lấy số ngẫu nhiên từ 1 đến 4
        anim.SetTrigger("Atk" + randomAtk);

        // Đặt lại thời gian chờ cho đòn tiếp theo
        attackTimer = attackCooldown;
    }

    // -------------------------------------------------------------------
    // HÀM NÀY ĐỂ GỌI TỪ ANIMATION EVENT (LÚC VUNG TAY TRÚNG ĐÍCH)
    // -------------------------------------------------------------------
    public void DealDamage(int damageAmount)
    {
        if (isDead || player == null) return;

        // Đo lại khoảng cách xem lúc vung tay Player có né ra ngoài chưa
        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        if (distanceToPlayer <= damageRadius)
        {
            Debug.Log("Zombie cào trúng Player! Sát thương: " + damageAmount);

            // Xóa dấu // ở dòng dưới để trừ máu Player khi bạn đã viết script cho Player
            // player.GetComponent<PlayerHealth>().TakeDamage(damageAmount);
        }
        else
        {
            Debug.Log("Player đã lùi ra ngoài tầm với, né thành công!");
        }
    }

    // -------------------------------------------------------------------
    // HÀM NÀY GỌI KHI PLAYER CHÉM TRÚNG ZOMBIE
    // -------------------------------------------------------------------
    public void TakeDamage(int damageTaken)
    {
        if (isDead) return;

        currentHealth -= damageTaken;

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            // Bị chém thì khựng lại một nhịp
            anim.SetTrigger("TakeDamage");
            attackTimer = 1f; // Tạm dừng đánh 1 giây do bị choáng
        }
    }

    // Xử lý cái chết của quái vật
    void Die()
    {
        isDead = true;
        agent.isStopped = true; // Dừng NavMesh

        // Bật trạng thái chết ở Animator
        anim.SetBool("isDead", true);

        // Random ra kiểu chết DIE (0) hoặc DIE2 (1)
        int randomDeath = Random.Range(0, 2);
        anim.SetInteger("DeathType", randomDeath);

        // Tắt Box/Capsule Collider để Player không bị vấp vào xác
        GetComponent<Collider2D>().enabled = false;

        // Vô hiệu hóa NavMesh để tránh lỗi văng log
        agent.enabled = false;
    }

    // Vẽ vòng đỏ trong Scene để dễ căn chỉnh
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, damageRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}