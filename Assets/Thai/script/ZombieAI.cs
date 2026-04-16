using UnityEngine;
using UnityEngine.AI;
using Fusion;

public class ZombieAI : NetworkBehaviour
{
    [Header("--- Phạm vi Phát hiện (Detection) ---")]
    public float detectionRange = 10f;

    [Header("--- Tấn công & Tốc độ ---")]
    public float moveSpeed = 3.5f;
    public float attackRange = 1.5f;
    public float damageRadius = 1.8f;
    public float attackCooldown = 1.5f;

    [Header("--- Sát thương các chiêu ---")]
    public float damageAtk1 = 10f;
    public float damageAtk2 = 15f;
    public float damageAtk3 = 20f;
    public float damageAtk4 = 30f;

    // 🔊 SOUND SYSTEM
    private Vector3 lastHeardPosition;
    private bool hasHeardSound = false;
    private float hearMemoryTimer = 0f;
    public float hearMemoryDuration = 3f;

    private Transform player;
    private NavMeshAgent agent;
    private Animator anim;
    private ZombieHealth healthScript;

    private float attackTimer = 0f;
    private float searchTimer = 0f;
    private float stunTimer = 0f;
    private float pathUpdateTimer = 0f;

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
            agent.stoppingDistance = 0f;
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
            agent.stoppingDistance = 0f;
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

        // STUN
        if (stunTimer > 0)
        {
            stunTimer -= Runner.DeltaTime;
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
            NetIsRunning = false;
            return;
        }

        // 🔊 SOUND MEMORY
        if (hasHeardSound)
        {
            hearMemoryTimer -= Runner.DeltaTime;
            if (hearMemoryTimer <= 0)
            {
                hasHeardSound = false;
            }
        }

        // FIND PLAYER
        searchTimer -= Runner.DeltaTime;
        if (searchTimer <= 0)
        {
            FindClosestPlayerInRange();
            searchTimer = 0.5f;
        }

        // ❗ KHÔNG có player và cũng không có sound → idle
        if ((player == null && !hasHeardSound) || agent == null || !agent.isOnNavMesh)
        {
            NetIsRunning = false;
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
            return;
        }

        // 🔊 ƯU TIÊN: NGHE ÂM THANH (khi chưa thấy player)
        if (player == null && hasHeardSound)
        {
            agent.isStopped = false;
            agent.speed = moveSpeed * 0.8f;

            pathUpdateTimer -= Runner.DeltaTime;
            if (pathUpdateTimer <= 0)
            {
                agent.SetDestination(lastHeardPosition);
                pathUpdateTimer = 0.2f;
            }

            NetIsRunning = true;
            NetMoveDir = (agent.steeringTarget - transform.position).normalized;

            if (Vector2.Distance(transform.position, lastHeardPosition) < 0.5f)
            {
                hasHeardSound = false;
            }

            return;
        }

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        if (attackTimer > 0) attackTimer -= Runner.DeltaTime;

        // CHASE
        if (distanceToPlayer > attackRange)
        {
            agent.isStopped = false;
            agent.speed = moveSpeed;

            pathUpdateTimer -= Runner.DeltaTime;
            if (pathUpdateTimer <= 0)
            {
                agent.SetDestination(player.position);
                pathUpdateTimer = 0.2f;
            }

            NetIsRunning = true;
            NetMoveDir = (agent.steeringTarget - transform.position).normalized;
        }
        // ATTACK
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

    // 🔊 NHẬN ÂM THANH
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_HearSound(Vector3 pos)
    {
        if (!HasStateAuthority) return;

        // Nếu đang thấy player thì bỏ qua
        if (player != null) return;

        lastHeardPosition = pos;
        hasHeardSound = true;
        hearMemoryTimer = hearMemoryDuration;
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
    }

    void FindClosestPlayerInRange()
    {
        GameObject[] allPlayers = GameObject.FindGameObjectsWithTag("Player");
        float closestDistance = detectionRange;
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
        if (currentAttackIndex == 1) ExecuteDamage(damageAtk1, 1);
        else if (currentAttackIndex == 2) ExecuteDamage(damageAtk2, 2);
        else if (currentAttackIndex == 3) ExecuteDamage(damageAtk3, 3);
        else if (currentAttackIndex == 4) ExecuteDamage(damageAtk4, 4);
    }

    private void ExecuteDamage(float damageAmount, int attackIndex)
    {
        if (!HasStateAuthority || player == null) return;

        if (Vector2.Distance(transform.position, player.position) <= damageRadius)
        {
            PlayerHealth pHealth = player.GetComponent<PlayerHealth>();
            if (pHealth != null)
            {
                pHealth.TakeDamage(damageAmount, false, true);

                if (attackIndex == 2)
                {
                    pHealth.SetBitten();
                }
            }
        }
    }

    public void ApplyStun(float duration)
    {
        stunTimer = duration;
        attackTimer = duration;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, damageRadius);

        // 🔊 debug sound
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(lastHeardPosition, 0.3f);
    }
}