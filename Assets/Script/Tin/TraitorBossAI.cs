using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using Fusion;

public class TraitorBossAI : NetworkBehaviour
{
    [Header("=== Movement (NavMesh 2D) ===")]
    [SerializeField] private float walkSpeed = 3.5f;
    [SerializeField] private float enragedSpeed = 5.5f;
    private NavMeshAgent agent;

    [Header("=== Boss Stats ===")]
    [SerializeField] private float maxHealth = 400f;
    [SerializeField] private float bossDamage = 25f;
    [SerializeField] private float normalStunDuration = 0.5f;

    [Header("=== Vision & Aggro ===")]
    [SerializeField] private LayerMask obstacleMask;
    private PlayerRef currentAggroShooter;
    private float aggroTimer = 0f;

    [Header("=== Special Skill: Blood Dash ===")]
    [SerializeField] private float dashSpeed = 12f;
    [SerializeField] private float dashDuration = 0.4f;
    [SerializeField] private float dashCooldown = 5f;
    private float currentDashCooldown;
    private float currentDashTimer;
    private Vector2 dashDirection;

    [Header("=== Attack ===")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float baseAttackDuration = 1.4f;
    [SerializeField] private float baseAttackCooldown = 1.2f;

    private float currentAttackCooldown;
    private float attackTimer;
    private float cooldownTimer;
    private bool isAttackingLocal;
    private bool hasAppliedDamage;

    // 🔥 CÁC BIẾN ĐỒNG BỘ MẠNG
    [Networked] public float CurrentHealth { get; set; }
    [Networked] public NetworkBool NetIsDead { get; set; }
    [Networked] public NetworkBool NetIsAttacking { get; set; }
    [Networked] public int NetAttackIndex { get; set; }
    [Networked] public float NetSpeed { get; set; }
    [Networked] public Vector2 NetMoveDir { get; set; }

    private bool isEnraged = false;
    private bool isDashing = false;
    private float stunTimer;
    private float spawnPauseTimer = 2f;

    private Transform targetPlayer;
    private Collider2D playerCol;
    private PlayerHealth playerHealth;
    private Collider2D myCol;
    private Rigidbody2D rb;
    private Animator anim;
    private SpriteRenderer spriteRend;
    private Color originalColor;

    private Vector2 lastMoveDirection;
    private float searchTargetTimer = 0f;

    private float smoothMoveX, smoothMoveY, smoothSpeed;
    private bool lastIsAttacking;
    private bool lastIsDead;
    private int lastAttackIndex;

    private void Awake()
    {
        myCol = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        spriteRend = GetComponentInChildren<SpriteRenderer>();

        agent = GetComponent<NavMeshAgent>();
        agent.updateRotation = false;
        agent.updateUpAxis = false;

        if (spriteRend != null) originalColor = spriteRend.color;
    }

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            CurrentHealth = maxHealth;
            currentAttackCooldown = baseAttackCooldown;
            if (agent != null) agent.speed = walkSpeed;
        }
        else
        {
            if (agent != null) agent.enabled = false;
            if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;
        }

        PlayerMovement myLocalPlayer = GetLocalPlayer();

        if (myLocalPlayer == null)
        {
            var cameraController = FindAnyObjectByType<PZ_CameraController>();
            if (cameraController != null)
            {
                cameraController.SetTarget(this.transform);
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || NetIsDead) return;

        if (agent != null && !agent.isOnNavMesh)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 3f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
            return;
        }

        if (spawnPauseTimer > 0)
        {
            spawnPauseTimer -= Runner.DeltaTime;
            agent.isStopped = true;
            NetSpeed = 0f;
            return;
        }

        if (!isEnraged && CurrentHealth <= maxHealth * 0.5f)
        {
            isEnraged = true;
            agent.speed = enragedSpeed;
            currentAttackCooldown = baseAttackCooldown * 0.6f;
        }

        searchTargetTimer -= Runner.DeltaTime;
        if (searchTargetTimer <= 0f)
        {
            FindSmartTarget();
            searchTargetTimer = 0.25f;
        }

        if (stunTimer > 0f)
        {
            stunTimer -= Runner.DeltaTime;
            if (stunTimer > 0f)
            {
                agent.isStopped = true;
                rb.linearVelocity = Vector2.zero;
                NetSpeed = 0f;
                return;
            }
        }

        if (currentDashCooldown > 0f) currentDashCooldown -= Runner.DeltaTime;
        if (cooldownTimer > 0f) cooldownTimer -= Runner.DeltaTime;

        if (isAttackingLocal)
        {
            attackTimer -= Runner.DeltaTime;
            if (attackTimer <= 0f)
            {
                isAttackingLocal = false;
                NetIsAttacking = false;
            }
        }

        if (targetPlayer == null)
        {
            if (agent.isOnNavMesh) agent.isStopped = true;
            NetSpeed = 0f;
            return;
        }

        Vector2 myPos = myCol.bounds.center;
        Vector2 targetPos = playerCol.bounds.center;
        Vector2 dirToPlayer = (targetPos - myPos).normalized;

        // 🔥 ĐÃ FIX (Bệnh 1): Đo khoảng cách MÉP ĐỤNG MÉP thay vì TÂM ĐẾN TÂM
        ColliderDistance2D collDist = Physics2D.Distance(myCol, playerCol);
        float distance = Mathf.Max(collDist.distance, 0f);

        if (isDashing)
        {
            currentDashTimer -= Runner.DeltaTime;
            agent.isStopped = true;

            transform.position += (Vector3)dashDirection * dashSpeed * Runner.DeltaTime;
            NetSpeed = dashSpeed;

            if (currentDashTimer <= 0f)
            {
                isDashing = false;
                if (agent.isOnNavMesh) agent.Warp(transform.position);
            }
            return;
        }

        if (distance <= attackRange && !isAttackingLocal && cooldownTimer <= 0f)
        {
            int attackIndex = Random.Range(1, 3);
            NetAttackIndex = attackIndex;
            NetIsAttacking = true;

            isAttackingLocal = true;
            hasAppliedDamage = false;
            attackTimer = baseAttackDuration;
            cooldownTimer = currentAttackCooldown;

            agent.isStopped = true;
            rb.linearVelocity = Vector2.zero;
            lastMoveDirection = dirToPlayer;
        }
        else if (distance > 3.5f && distance < 8f && currentDashCooldown <= 0f && !isAttackingLocal)
        {
            RaycastHit2D wallCheck = Physics2D.Raycast(myPos, dirToPlayer, distance, obstacleMask);
            bool hasLineOfSight = wallCheck.collider == null || wallCheck.collider.gameObject == targetPlayer.gameObject;

            if (hasLineOfSight)
            {
                isDashing = true;
                currentDashTimer = dashDuration;
                currentDashCooldown = dashCooldown;
                dashDirection = dirToPlayer;
                lastMoveDirection = dirToPlayer;
                agent.isStopped = true;
            }
        }
        else if (!isAttackingLocal)
        {
            agent.isStopped = false;
            agent.SetDestination(targetPos);

            if (agent.hasPath)
            {
                Vector2 nextWaypointDir = ((Vector2)agent.steeringTarget - myPos).normalized;
                if (nextWaypointDir != Vector2.zero)
                {
                    lastMoveDirection = Vector2.Lerp(lastMoveDirection, nextWaypointDir, 15f * Runner.DeltaTime);
                }
            }
        }

        NetMoveDir = lastMoveDirection;
        NetSpeed = isAttackingLocal ? 0f : agent.velocity.magnitude;
    }

    private void FindSmartTarget()
    {
        if (aggroTimer > 0)
        {
            aggroTimer -= Runner.DeltaTime;
            if (aggroTimer <= 0) currentAggroShooter = PlayerRef.None;
        }

        PlayerHealth[] allPlayers = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        float minDist = Mathf.Infinity;
        PlayerHealth bestTarget = null;
        PlayerHealth aggroTarget = null;

        foreach (var p in allPlayers)
        {
            if (p.isDead || p.isTransforming) continue;

            if (!isEnraged && p.TryGetComponent(out Skill_StealthCrouch stealth) && stealth.IsInvisible) continue;

            float dist = Vector2.Distance(transform.position, p.transform.position);

            if (currentAggroShooter != PlayerRef.None && p.Object.InputAuthority == currentAggroShooter)
            {
                aggroTarget = p;
            }

            if (dist < minDist)
            {
                minDist = dist;
                bestTarget = p;
            }
        }

        PlayerHealth finalTarget = (aggroTarget != null) ? aggroTarget : bestTarget;

        if (finalTarget != null)
        {
            targetPlayer = finalTarget.transform;
            playerCol = finalTarget.GetComponent<Collider2D>();
            playerHealth = finalTarget;
        }
        else
        {
            targetPlayer = null;
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_TakeDamage(float damage, PlayerRef shooter = default)
    {
        if (NetIsDead) return;

        CurrentHealth -= damage;

        if (shooter != PlayerRef.None)
        {
            currentAggroShooter = shooter;
            aggroTimer = 5f;
        }

        if (CurrentHealth <= 0f)
        {
            CurrentHealth = 0f;
            Die(shooter);
            return;
        }

        if (!isEnraged && !isDashing)
        {
            stunTimer = normalStunDuration;
            isAttackingLocal = false;
            NetIsAttacking = false;
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
        }

        RPC_PlayHitEffect();
    }

    private void Die(PlayerRef shooter)
    {
        if (NetIsDead) return;
        NetIsDead = true;

        rb.linearVelocity = Vector2.zero;
        myCol.enabled = false;
        if (agent != null) agent.enabled = false;

        if (shooter != PlayerRef.None)
        {
            Skill_WeaponMaster[] allWeaponMasters = FindObjectsByType<Skill_WeaponMaster>(FindObjectsSortMode.None);
            foreach (var master in allWeaponMasters)
            {
                if (master.Object != null && master.Object.InputAuthority == shooter)
                {
                    master.AddKill();
                    break;
                }
            }
        }
        StartCoroutine(VanishRoutine());
    }

    public void TriggerAttackDamage()
    {
        if (!HasStateAuthority || NetIsDead || playerHealth == null || playerCol == null) return;
        if (hasAppliedDamage) return;

        // 🔥 ĐÃ FIX: Đồng bộ thước đo Mép chạm Mép cho cú đấm
        float currentDist = Mathf.Max(Physics2D.Distance(myCol, playerCol).distance, 0f);

        if (currentDist <= attackRange + 0.5f)
        {
            playerHealth.TakeDamage(bossDamage, false, true);
            hasAppliedDamage = true;
        }
    }

    public override void Render()
    {
        if (anim != null)
        {
            smoothMoveX = Mathf.Lerp(smoothMoveX, NetMoveDir.x, Time.deltaTime * 12f);
            smoothMoveY = Mathf.Lerp(smoothMoveY, NetMoveDir.y, Time.deltaTime * 12f);
            smoothSpeed = Mathf.Lerp(smoothSpeed, NetSpeed, Time.deltaTime * 15f);

            anim.SetFloat("MoveX", smoothMoveX);
            anim.SetFloat("MoveY", smoothMoveY);
            anim.SetFloat("Speed", smoothSpeed);

            if (lastIsAttacking != NetIsAttacking)
            {
                anim.SetBool("IsAttacking", NetIsAttacking);
                lastIsAttacking = NetIsAttacking;
            }

            if (lastIsDead != NetIsDead)
            {
                anim.SetBool("IsDead", NetIsDead);
                lastIsDead = NetIsDead;
            }

            if (NetIsAttacking && lastAttackIndex != NetAttackIndex)
            {
                anim.SetInteger("AttackIndex", NetAttackIndex);
                lastAttackIndex = NetAttackIndex;
            }

            anim.speed = (isDashing || isEnraged) ? 1.5f : 1f;
        }

        if (spriteRend != null && !NetIsDead)
        {
            float targetScale = isEnraged ? 1.2f : 1f;
            transform.localScale = Vector3.Lerp(transform.localScale, new Vector3(targetScale, targetScale, 1f), Time.deltaTime * 5f);
        }

        if (spriteRend != null)
        {
            PlayerMovement myLocalPlayer = GetLocalPlayer();
            if (myLocalPlayer != null)
            {
                spriteRend.enabled = CheckVisibilityForLocalPlayer(myLocalPlayer);
            }
            else
            {
                spriteRend.enabled = true;
            }
        }
    }

    private PlayerMovement GetLocalPlayer()
    {
        PlayerMovement[] allPlayers = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        foreach (var p in allPlayers) { if (p.HasInputAuthority) return p; }
        return null;
    }

    private bool CheckVisibilityForLocalPlayer(PlayerMovement localPlayer)
    {
        Vector2 myPos = myCol.bounds.center;
        Vector2 playerPos = localPlayer.GetComponent<Collider2D>().bounds.center;
        float distance = Vector2.Distance(myPos, playerPos);

        // 🔥 ĐÃ FIX (Bệnh 2): Phá bỏ luật tàng hình. Mở rộng bán kính hiện hình lên 15m.
        if (distance > 15f) return false;

        return true;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_PlayHitEffect()
    {
        if (anim != null) anim.SetTrigger("TakeDamage");
        if (spriteRend != null && !NetIsDead)
        {
            StopCoroutine(FlashRedRoutine());
            StartCoroutine(FlashRedRoutine());
        }
    }

    private IEnumerator FlashRedRoutine()
    {
        spriteRend.color = Color.red;
        yield return new WaitForSeconds(0.12f);
        if (!NetIsDead) spriteRend.color = originalColor;
    }

    private IEnumerator VanishRoutine()
    {
        yield return new WaitForSeconds(5f);
        if (HasStateAuthority) Runner.Despawn(Object);
    }
}