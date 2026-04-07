using UnityEngine;
using UnityEngine.AI;
using Fusion;

public class ZombieAI : NetworkBehaviour
{
    [Header("Mục tiêu & Tốc độ")]
    public float moveSpeed = 3.5f;
    public float chaseRange = 10f;
    public float attackRange = 1.5f;
    public float damageRadius = 1.8f;

    [Header("Sát thương & Cooldown")]
    public float attackCooldown = 1.5f;
    public float damageAtk1 = 10f;
    public float damageAtk2 = 15f;
    public float damageAtk3 = 20f;
    public float damageAtk4 = 30f;

    private Transform player;
    private NavMeshAgent agent;
    private Animator anim;
    private ZombieHealth healthScript;

    private float attackTimer = 0f;
    private float searchTimer = 0f;

    // BIẾN MỚI: Bộ đếm thời gian bị đứng hình khi trúng đạn
    private float stunTimer = 0f;

    private int currentAttackIndex = 1;

    [Networked] public Vector2 NetMoveDir { get; set; }
    [Networked] public NetworkBool NetIsRunning { get; set; }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.updateRotation = false;
            agent.updateUpAxis = false;
        }
    }

    public override void Spawned()
    {
        transform.rotation = Quaternion.identity;
        transform.position = new Vector3(transform.position.x, transform.position.y, 0f);

        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
        healthScript = GetComponent<ZombieHealth>();

        if (!HasStateAuthority)
        {
            if (agent != null) agent.enabled = false;
            return;
        }

        if (agent != null)
        {
            agent.updateRotation = false;
            agent.updateUpAxis = false;
            agent.speed = moveSpeed;
            agent.Warp(transform.position);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || (healthScript != null && healthScript.isDead))
        {
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
            NetIsRunning = false;
            return;
        }

        // ==========================================
        // CƠ CHẾ ĐỨNG HÌNH (STUN) TỪ KHOA
        // ==========================================
        if (stunTimer > 0)
        {
            stunTimer -= Runner.DeltaTime;
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
            NetIsRunning = false;
            return; // Khóa toàn bộ AI khi đang bị khựng
        }

        searchTimer -= Runner.DeltaTime;
        if (searchTimer <= 0)
        {
            FindClosestPlayer();
            searchTimer = 0.5f;
        }

        if (player == null || agent == null || !agent.isOnNavMesh)
        {
            NetIsRunning = false;
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
            return;
        }

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        if (attackTimer > 0) attackTimer -= Runner.DeltaTime;

        if (distanceToPlayer > chaseRange)
        {
            agent.isStopped = true;
            NetIsRunning = false;
        }
        else if (distanceToPlayer > attackRange)
        {
            agent.isStopped = false;
            agent.speed = moveSpeed;
            agent.SetDestination(player.position);

            NetIsRunning = true;
            NetMoveDir = (agent.steeringTarget - transform.position).normalized;
        }
        else
        {
            agent.isStopped = true;
            NetIsRunning = false;
            NetMoveDir = (player.position - transform.position).normalized;

            if (attackTimer <= 0)
            {
                int randomAtk = Random.Range(1, 5);
                currentAttackIndex = randomAtk;

                RPC_TriggerAttack(randomAtk);
                attackTimer = attackCooldown;
            }
        }
    }

    public override void Render()
    {
        if (anim == null) return;

        anim.SetBool("isRunning", NetIsRunning);
        if (NetMoveDir != Vector2.zero)
        {
            anim.SetFloat("DirX", NetMoveDir.x);
            anim.SetFloat("DirY", NetMoveDir.y);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_TriggerAttack(int atkIndex)
    {
        if (anim != null)
        {
            anim.ResetTrigger("Atk1");
            anim.ResetTrigger("Atk2");
            anim.ResetTrigger("Atk3");
            anim.ResetTrigger("Atk4");
            anim.SetTrigger("Atk" + atkIndex);
        }

        Debug.Log($"<color=orange><b>[BÁO ĐỘNG] Zombie đang tung chiêu: ĐÒN SỐ {atkIndex}</b></color>");
    }

    void FindClosestPlayer()
    {
        GameObject[] allPlayers = GameObject.FindGameObjectsWithTag("Player");
        float closestDistance = chaseRange;
        player = null;

        foreach (GameObject p in allPlayers)
        {
            float dist = Vector2.Distance(transform.position, p.transform.position);
            if (dist < closestDistance)
            {
                closestDistance = dist;
                player = p.transform;
            }
        }
    }

    public void DealDamage()
    {
        if (currentAttackIndex == 1) ExecuteDamage(damageAtk1);
        else if (currentAttackIndex == 2) ExecuteDamage(damageAtk2);
        else if (currentAttackIndex == 3) ExecuteDamage(damageAtk3);
        else if (currentAttackIndex == 4) ExecuteDamage(damageAtk4);
    }

    private void ExecuteDamage(float damageAmount)
    {
        if (!HasStateAuthority || player == null) return;

        if (Vector2.Distance(transform.position, player.position) <= damageRadius)
        {
            PlayerHealth pHealth = player.GetComponent<PlayerHealth>();
            if (pHealth != null) pHealth.TakeDamage(damageAmount);
        }
    }

    // HÀM MỚI: Bị gọi từ ZombieHealth để bắt AI đứng hình
    public void ApplyStun(float duration)
    {
        stunTimer = duration;
        attackTimer = duration; // Bị bắn thì cũng bị chậm nhịp ra đòn
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, chaseRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, damageRadius);
    }
}