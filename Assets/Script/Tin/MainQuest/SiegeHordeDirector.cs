using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>Host-only wave spawning plus the initial gate objective for spawned siege zombies.</summary>
public sealed class SiegeHordeDirector : MonoBehaviour
{
    [SerializeField, Min(1f)] private float secondsBetweenChecks = MilitaryStoryFlowRules.HordeCheckIntervalSeconds;
    [SerializeField, Min(5f)] private float nearGateRadius = 35f;
    [SerializeField, Min(1)] private int multiplayerHardSafetyCap = 72;
    [SerializeField, Min(1)] private int soloHardSafetyCap = 36;
    [SerializeField, Min(0f)] private float spawnJitterRadius = 0.55f;
    [SerializeField, Min(5f)] private float minimumSpawnDistanceFromGate = 18f;
    [SerializeField, Min(0f)] private float clearExistingZombieRadiusAtGate = 7.5f;

    private readonly List<SiegeZombieObjective> activeObjectives = new();
    private readonly List<NetworkPrefabRef> zombiePrefabs = new();
    private readonly List<Transform> spawnPoints = new();
    private MilitaryBaseQuestManager manager;
    private MilitaryGateController gate;
    private Coroutine siegeRoutine;
    private bool siegeActive;
    private bool releasedToPlayers;

    public void Configure(MilitaryBaseQuestManager targetManager, MilitaryGateController targetGate)
    {
        manager = targetManager;
        gate = targetGate;
        CacheZombiePrefabs();
        CacheAuthoredSpawnPoints();
    }

    public void BeginSiege()
    {
        siegeActive = true;
        releasedToPlayers = false;
        if (manager == null || !manager.HasStateAuthority || siegeRoutine != null) return;
        RegisterExistingZombiesNearGate();
        siegeRoutine = StartCoroutine(SiegeRoutine());
    }

    public void StopSiege()
    {
        siegeActive = false;
        if (siegeRoutine != null) StopCoroutine(siegeRoutine);
        siegeRoutine = null;
    }

    public void ReleaseHordeToPlayers()
    {
        releasedToPlayers = true;
        for (int i = activeObjectives.Count - 1; i >= 0; i--)
        {
            if (activeObjectives[i] == null) activeObjectives.RemoveAt(i);
            else activeObjectives[i].ReleaseToPlayers();
        }
    }

    private IEnumerator SiegeRoutine()
    {
        yield return new WaitForSeconds(0.6f);
        while (siegeActive && manager != null && manager.IsNetworkReady &&
               manager.CurrentPhase == MilitaryBaseQuestManager.Phase.SiegeAndRepair)
        {
            activeObjectives.RemoveAll(item => item == null);
            if (!releasedToPlayers) TrySpawnBatch();
            yield return new WaitForSeconds(secondsBetweenChecks);
        }
        siegeRoutine = null;
    }

    private void TrySpawnBatch()
    {
        if (manager.Runner == null || zombiePrefabs.Count == 0 || spawnPoints.Count == 0)
        {
            Debug.LogWarning("[MILITARY QUEST] Thiếu prefab zombie hoặc bốn marker ViTriSpawnZombie.");
            return;
        }

        int playerCount = GetActivePlayerCount();
        int nearbyCount = CountSiegeZombiesNearGate();
        if (!MilitaryStoryFlowRules.ShouldSpawnBatch(playerCount, nearbyCount)) return;

        int hardCap = playerCount <= 1 ? soloHardSafetyCap : multiplayerHardSafetyCap;
        int availableSlots = Mathf.Max(0, hardCap - activeObjectives.Count);
        if (availableSlots <= 0) return;

        int perPoint = MilitaryStoryFlowRules.GetSpawnPerPoint(playerCount);
        int spawnedCount = 0;
        for (int pointIndex = 0; pointIndex < spawnPoints.Count && availableSlots > 0; pointIndex++)
        {
            Transform spawnPoint = spawnPoints[pointIndex];
            for (int localIndex = 0; localIndex < perPoint && availableSlots > 0; localIndex++)
            {
                Vector2 gatePosition = manager.GetInteractionPosition(MilitaryBaseQuestManager.InteractionKind.Gate);
                Vector2 spawnBase = spawnPoint.position;
                Vector2 fromGate = spawnBase - gatePosition;
                if (fromGate.magnitude < minimumSpawnDistanceFromGate && fromGate.sqrMagnitude > 0.001f)
                    spawnBase = gatePosition + fromGate.normalized * minimumSpawnDistanceFromGate;
                Vector2 position = spawnBase + Random.insideUnitCircle * spawnJitterRadius;
                NetworkPrefabRef prefab = zombiePrefabs[Random.Range(0, zombiePrefabs.Count)];
                NetworkObject spawned = manager.Runner.Spawn(prefab, position, Quaternion.identity);
                if (spawned == null) continue;
                SiegeZombieObjective objective = spawned.GetComponent<SiegeZombieObjective>();
                if (objective == null) objective = spawned.gameObject.AddComponent<SiegeZombieObjective>();
                objective.Configure(manager, gate);
                activeObjectives.Add(objective);
                spawnedCount++;
                availableSlots--;
            }
        }
        Debug.Log($"[MILITARY HORDE] Spawn {spawnedCount} zombie từ {spawnPoints.Count} điểm; " +
                  $"gần cổng {nearbyCount}/{MilitaryStoryFlowRules.GetNearbyTarget(playerCount)}, player={playerCount}.");
    }

    private int CountSiegeZombiesNearGate()
    {
        Vector2 gatePosition = manager.GetInteractionPosition(MilitaryBaseQuestManager.InteractionKind.Gate);
        int count = 0;
        for (int i = 0; i < activeObjectives.Count; i++)
            if (activeObjectives[i] != null &&
                Vector2.Distance(activeObjectives[i].transform.position, gatePosition) <= nearGateRadius)
                count++;
        return count;
    }

    private int GetActivePlayerCount()
    {
        int count = 0;
        foreach (PlayerRef _ in manager.Runner.ActivePlayers) count++;
        return Mathf.Max(1, count);
    }

    private void RegisterExistingZombiesNearGate()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        Vector2 gatePosition = manager.GetInteractionPosition(MilitaryBaseQuestManager.InteractionKind.Gate);
        int adopted = 0;
        int cleared = 0;
        HashSet<GameObject> handledRoots = new HashSet<GameObject>();
        for (int i = 0; i < enemies.Length; i++)
        {
            GameObject enemy = enemies[i];
            if (enemy == null) continue;
            NetworkObject networkObject = enemy.GetComponentInParent<NetworkObject>();
            GameObject root = networkObject != null ? networkObject.gameObject : enemy;
            if (!handledRoots.Add(root)) continue;
            float gateDistance = Vector2.Distance(root.transform.position, gatePosition);
            if (gateDistance > nearGateRadius) continue;
            if (root.GetComponent<PlayerMovement>() != null) continue;
            if (root.GetComponent<ZombieAI>() == null && root.GetComponent<ZOmbieAI_Khoa>() == null &&
                root.GetComponent<ZombieAIKhoaRebuilt>() == null)
                continue;

            if (gateDistance < clearExistingZombieRadiusAtGate)
            {
                if (networkObject != null && networkObject.IsValid && networkObject.HasStateAuthority)
                    manager.Runner.Despawn(networkObject);
                else if (networkObject == null)
                    Destroy(root);
                cleared++;
                continue;
            }

            SiegeZombieObjective objective = root.GetComponent<SiegeZombieObjective>();
            if (objective == null) objective = root.AddComponent<SiegeZombieObjective>();
            if (activeObjectives.Contains(objective)) continue;
            objective.Configure(manager, gate);
            activeObjectives.Add(objective);
            adopted++;
        }
        Debug.Log($"[MILITARY HORDE] Dọn {cleared} zombie cũ sát cổng; chuyển {adopted} zombie " +
                  "ở vành ngoài sang mục tiêu công thành.");
    }

    private void CacheAuthoredSpawnPoints()
    {
        spawnPoints.Clear();
        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
            if (transforms[i] != null && transforms[i].name.StartsWith("ViTriSpawnZombie"))
                spawnPoints.Add(transforms[i]);
        spawnPoints.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
        if (spawnPoints.Count != MilitaryStoryFlowRules.SpawnPointCount)
            Debug.LogWarning($"[MILITARY HORDE] Cần đúng 4 marker ViTriSpawnZombie, hiện tìm thấy {spawnPoints.Count}.");
    }

    private void CacheZombiePrefabs()
    {
        zombiePrefabs.Clear();
        ZombieSpawnZone[] zones = FindObjectsByType<ZombieSpawnZone>(FindObjectsSortMode.None);
        float closest = float.PositiveInfinity;
        ZombieSpawnZone selected = null;
        Vector2 basePosition = manager != null
            ? manager.GetInteractionPosition(MilitaryBaseQuestManager.InteractionKind.Vehicle)
            : Vector2.zero;
        for (int i = 0; i < zones.Length; i++)
        {
            if (zones[i] == null || zones[i].zombiePrefabs == null || zones[i].zombiePrefabs.Count == 0) continue;
            float distance = Vector2.Distance(zones[i].transform.position, basePosition);
            if (distance >= closest) continue;
            closest = distance;
            selected = zones[i];
        }
        if (selected != null) zombiePrefabs.AddRange(selected.zombiePrefabs);
    }
}

/// <summary>Authority-side temporary objective added to zombies spawned by the siege director.</summary>
public sealed class SiegeZombieObjective : MonoBehaviour
{
    private float assaultMoveSpeed = 2.5f;
    private MilitaryBaseQuestManager manager;
    private MilitaryGateController gate;
    private Rigidbody2D body;
    private Animator animator;
    private ZombieAI thaiZombie;
    private ZOmbieAI_Khoa khoaZombie;
    private ZombieAIKhoaRebuilt rebuiltZombie;
    private MonoBehaviour[] zombieBehaviours;
    private bool released;
    private float gateAttackCooldown;
    private float attackAnimationRemaining;
    private int attackIndex;

    public void Configure(MilitaryBaseQuestManager targetManager, MilitaryGateController targetGate)
    {
        manager = targetManager;
        gate = targetGate;
        body = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        thaiZombie = GetComponent<ZombieAI>();
        khoaZombie = GetComponent<ZOmbieAI_Khoa>();
        rebuiltZombie = GetComponent<ZombieAIKhoaRebuilt>();
        if (thaiZombie != null) assaultMoveSpeed = thaiZombie.ChaseMovementSpeed;
        else if (khoaZombie != null) assaultMoveSpeed = khoaZombie.ChaseMovementSpeed;
        else if (rebuiltZombie != null) assaultMoveSpeed = rebuiltZombie.ChaseMovementSpeed;
        zombieBehaviours = GetComponents<MonoBehaviour>();
        SetZombieAIEnabled(false);
        gateAttackCooldown = Mathf.Abs(GetInstanceID() % 100) / 100f;
    }

    private void FixedUpdate()
    {
        if (released || manager == null || gate == null || !manager.HasStateAuthority) return;
        if (manager.IsGateBroken)
        {
            ReleaseToPlayers();
            return;
        }
        if (gateAttackCooldown > 0f) gateAttackCooldown -= Time.fixedDeltaTime;
        if (attackAnimationRemaining > 0f)
        {
            attackAnimationRemaining -= Time.fixedDeltaTime;
            if (attackAnimationRemaining <= 0f) SetGateAttackAnimation(false);
        }
        Vector2 target = gate.GetAssaultPosition(GetInstanceID());
        Vector2 position = body != null ? body.position : transform.position;
        float distance = Vector2.Distance(position, target);
        if (distance > 0.9f)
        {
            SetGateAttackAnimation(false);
            Vector2 velocity = (target - position).normalized * assaultMoveSpeed;
            MoveWithChaseSpeed(velocity, distance - 0.9f);
            return;
        }

        ApplyAnimationState(Vector2.zero);
        if (body != null) body.linearVelocity = Vector2.zero;
        if (gateAttackCooldown <= 0f)
        {
            attackIndex = attackIndex % 2 + 1;
            SetGateAttackAnimation(true);
            gate.TryApplyHordeHit();
            gateAttackCooldown = 1.25f + Mathf.Abs(GetInstanceID() % 7) * 0.04f;
            attackAnimationRemaining = 0.62f;
        }
    }

    public void ReleaseToPlayers()
    {
        if (released) return;
        released = true;
        SetGateAttackAnimation(false);
        ApplyAnimationState(Vector2.zero);
        SetZombieAIEnabled(true);
    }

    private void SetGateAttackAnimation(bool active)
    {
        if (thaiZombie != null)
        {
            thaiZombie.NetIsAttacking = active;
            if (active && animator != null)
            {
                animator.ResetTrigger("Atk1");
                animator.ResetTrigger("Atk2");
                animator.SetTrigger("Atk" + attackIndex);
            }
        }
        if (khoaZombie != null)
        {
            khoaZombie.NetIsAttacking = active;
            if (active) khoaZombie.NetAttackIndex = attackIndex;
        }
        if (rebuiltZombie != null)
        {
            rebuiltZombie.NetIsAttacking = active;
            if (active) rebuiltZombie.NetAttackIndex = attackIndex;
        }
        if (animator != null && thaiZombie == null)
        {
            animator.SetInteger("AttackIndex", attackIndex);
            animator.SetBool("IsAttacking", active);
        }
    }

    private void MoveWithChaseSpeed(Vector2 velocity, float remainingDistance)
    {
        Vector2 step = velocity.normalized * Mathf.Min(velocity.magnitude * Time.fixedDeltaTime,
            Mathf.Max(0f, remainingDistance));
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.MovePosition(body.position + step);
        }
        else transform.position += (Vector3)step;
        ApplyAnimationState(velocity);
    }

    private void ApplyAnimationState(Vector2 velocity)
    {
        float speed = velocity.magnitude;
        Vector2 direction = speed > 0.01f ? velocity / speed : Vector2.zero;
        if (thaiZombie != null)
        {
            thaiZombie.NetMoveDir = direction;
            thaiZombie.NetMoveSpeed = speed;
            thaiZombie.NetIsRunning = speed > 0.01f;
        }
        if (khoaZombie != null)
        {
            khoaZombie.NetMoveDir = direction;
            khoaZombie.NetSpeed = speed;
        }
        if (rebuiltZombie != null)
        {
            rebuiltZombie.NetMoveDir = direction;
            rebuiltZombie.NetSpeed = speed;
        }

        if (animator == null) return;
        if (thaiZombie != null)
        {
            animator.SetFloat("DirX", direction.x);
            animator.SetFloat("DirY", direction.y);
        }
        else
        {
            animator.SetFloat("MoveX", direction.x);
            animator.SetFloat("MoveY", direction.y);
        }
        animator.SetFloat("Speed", speed);
    }

    private void SetZombieAIEnabled(bool enabled)
    {
        if (zombieBehaviours == null) return;
        for (int i = 0; i < zombieBehaviours.Length; i++)
        {
            MonoBehaviour behaviour = zombieBehaviours[i];
            if (behaviour is ZombieAI || behaviour is ZOmbieAI_Khoa || behaviour is ZombieAIKhoaRebuilt)
                behaviour.enabled = enabled;
        }
    }
}
