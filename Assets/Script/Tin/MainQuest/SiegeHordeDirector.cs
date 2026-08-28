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

    private readonly List<SiegeZombieObjective> activeObjectives = new();
    private readonly List<NetworkPrefabRef> zombiePrefabs = new();
    private readonly List<Transform> spawnPoints = new();
    private PolygonCollider2D protectedMilitaryArea;
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
        CacheProtectedMilitaryArea();
    }

    public void BeginSiege()
    {
        siegeActive = true;
        releasedToPlayers = false;
        if (manager == null || !manager.HasStateAuthority || siegeRoutine != null) return;
        int adopted = AuthorityAdoptExistingCityZombies();
        Debug.Log($"[MILITARY HORDE] Điều động {adopted} zombie đang sống trong thành phố về cổng.");
        siegeRoutine = StartCoroutine(SiegeRoutine());
    }

    public void StopSiege()
    {
        siegeActive = false;
        if (siegeRoutine != null) StopCoroutine(siegeRoutine);
        siegeRoutine = null;
    }

    public void AuthorityResetAndDespawnAll()
    {
        StopSiege();
        releasedToPlayers = false;
        if (manager == null || !manager.HasStateAuthority || manager.Runner == null)
        {
            activeObjectives.Clear();
            return;
        }

        SiegeZombieObjective[] objectives = FindObjectsByType<SiegeZombieObjective>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < objectives.Length; i++)
        {
            SiegeZombieObjective objective = objectives[i];
            if (objective == null) continue;
            NetworkObject networkObject = objective.GetComponent<NetworkObject>();
            if (objective.ShouldDespawnOnReset && networkObject != null && networkObject.IsValid)
                manager.Runner.Despawn(networkObject);
            else
            {
                objective.RestoreAmbientState();
                Destroy(objective);
            }
        }
        activeObjectives.Clear();
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
               (manager.CurrentPhase == MilitaryBaseQuestManager.Phase.SiegeAndRepair ||
                manager.CurrentPhase == MilitaryBaseQuestManager.Phase.ReadyToEscape))
        {
            for (int i = activeObjectives.Count - 1; i >= 0; i--)
            {
                SiegeZombieObjective objective = activeObjectives[i];
                if (objective == null)
                {
                    activeObjectives.RemoveAt(i);
                    continue;
                }

                if (!objective.IsZombieDead) continue;
                objective.RetireDeadZombie();
                activeObjectives.RemoveAt(i);
            }
            TrySpawnBatch();
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
        int prefabOffset = Random.Range(0, zombiePrefabs.Count);
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
                if (!TryGetExteriorSpawnPosition(spawnBase, out Vector2 position))
                {
                    Debug.LogWarning($"[MILITARY HORDE] Bỏ qua marker '{spawnPoint.name}' vì điểm spawn " +
                                     "nằm trong khu vực trường được bảo vệ.");
                    continue;
                }
                // Cycle through every configured zombie type in each batch so
                // both authored variants receive the same siege lifecycle.
                NetworkPrefabRef prefab = zombiePrefabs[(prefabOffset + spawnedCount) % zombiePrefabs.Count];
                NetworkObject spawned = manager.Runner.Spawn(prefab, position, Quaternion.identity);
                if (spawned == null) continue;
                SiegeZombieObjective objective = spawned.GetComponent<SiegeZombieObjective>();
                if (objective == null) objective = spawned.gameObject.AddComponent<SiegeZombieObjective>();
                objective.Configure(manager, gate, true);
                if (releasedToPlayers || manager.IsGateBroken)
                    objective.ReleaseToPlayers();
                activeObjectives.Add(objective);
                spawnedCount++;
                availableSlots--;
            }
        }
        Debug.Log($"[MILITARY HORDE] Spawn {spawnedCount} zombie từ {spawnPoints.Count} điểm; " +
                  $"gần cổng {nearbyCount}/{MilitaryStoryFlowRules.GetNearbyTarget(playerCount)}, player={playerCount}.");
    }

    private int AuthorityAdoptExistingCityZombies()
    {
        if (manager == null || !manager.HasStateAuthority) return 0;

        NetworkObject[] networkObjects = FindObjectsByType<NetworkObject>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int adopted = 0;
        for (int i = 0; i < networkObjects.Length; i++)
        {
            NetworkObject networkObject = networkObjects[i];
            if (networkObject == null || !networkObject.IsValid || !networkObject.HasStateAuthority) continue;
            GameObject root = networkObject.gameObject;
            bool isZombie = root.GetComponent<ZombieAI>() != null ||
                            root.GetComponent<ZOmbieAI_Khoa>() != null ||
                            root.GetComponent<ZombieAIKhoaRebuilt>() != null;
            if (!isZombie || IsZombieRootDead(root)) continue;

            SiegeZombieObjective objective = root.GetComponent<SiegeZombieObjective>();
            if (objective != null)
            {
                if (!activeObjectives.Contains(objective)) activeObjectives.Add(objective);
                continue;
            }

            objective = root.AddComponent<SiegeZombieObjective>();
            objective.Configure(manager, gate, false);
            activeObjectives.Add(objective);
            adopted++;
        }
        return adopted;
    }

    private static bool IsZombieRootDead(GameObject root)
    {
        ZombieHealth thaiHealth = root.GetComponent<ZombieHealth>();
        if (thaiHealth != null && thaiHealth.Object != null && thaiHealth.Object.IsValid && thaiHealth.isDead)
            return true;
        ZOmbieAI_Khoa khoa = root.GetComponent<ZOmbieAI_Khoa>();
        if (khoa != null && khoa.Object != null && khoa.Object.IsValid && khoa.NetIsDead) return true;
        ZombieAIKhoaRebuilt rebuilt = root.GetComponent<ZombieAIKhoaRebuilt>();
        return rebuilt != null && rebuilt.Object != null && rebuilt.Object.IsValid && rebuilt.NetIsDead;
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

    private bool TryGetExteriorSpawnPosition(Vector2 spawnBase, out Vector2 position)
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            Vector2 candidate = spawnBase + Random.insideUnitCircle * spawnJitterRadius;
            if (protectedMilitaryArea != null && protectedMilitaryArea.OverlapPoint(candidate)) continue;
            position = candidate;
            return true;
        }

        position = default;
        return false;
    }

    private void CacheAuthoredSpawnPoints()
    {
        spawnPoints.Clear();
        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
            if (transforms[i] != null && IsAuthoredSiegeSpawnName(transforms[i].name))
                spawnPoints.Add(transforms[i]);
        spawnPoints.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
        if (spawnPoints.Count != MilitaryStoryFlowRules.SpawnPointCount)
            Debug.LogWarning($"[MILITARY HORDE] Cần đúng 4 marker ViTriSpawnZombie, hiện tìm thấy {spawnPoints.Count}.");
    }

    private static bool IsAuthoredSiegeSpawnName(string objectName) =>
        objectName == "ViTriSpawnZombie" || objectName == "ViTriSpawnZombie (1)" ||
        objectName == "ViTriSpawnZombie (2)" || objectName == "ViTriSpawnZombie (3)";

    private void CacheProtectedMilitaryArea()
    {
        protectedMilitaryArea = null;
        PolygonCollider2D[] polygons = FindObjectsByType<PolygonCollider2D>(FindObjectsSortMode.None);
        for (int i = 0; i < polygons.Length; i++)
        {
            if (polygons[i] == null || polygons[i].name != "KhuVucQuanSu") continue;
            protectedMilitaryArea = polygons[i];
            break;
        }
        if (protectedMilitaryArea == null)
            Debug.LogWarning("[MILITARY HORDE] Không tìm thấy PolygonCollider2D KhuVucQuanSu để chặn spawn trong trường.");
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
    private ZombieHealth thaiZombieHealth;
    private ZOmbieAI_Khoa khoaZombie;
    private ZombieAIKhoaRebuilt rebuiltZombie;
    private MonoBehaviour[] zombieBehaviours;
    private bool released;
    private float gateAttackCooldown;
    private float attackAnimationRemaining;
    private bool? zombieAIEnabled;
    private bool spawnedBySiegeDirector;
    private int attackIndex;
    private int attackSequence;

    public bool ShouldDespawnOnReset => spawnedBySiegeDirector;

    public bool IsZombieDead
    {
        get
        {
            if (thaiZombieHealth != null && IsSpawned(thaiZombieHealth) && thaiZombieHealth.isDead) return true;
            if (khoaZombie != null && IsSpawned(khoaZombie) && khoaZombie.NetIsDead) return true;
            return rebuiltZombie != null && IsSpawned(rebuiltZombie) && rebuiltZombie.NetIsDead;
        }
    }

    public void Configure(MilitaryBaseQuestManager targetManager, MilitaryGateController targetGate,
        bool shouldDespawnOnReset = true)
    {
        manager = targetManager;
        gate = targetGate;
        spawnedBySiegeDirector = shouldDespawnOnReset;
        body = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        thaiZombie = GetComponent<ZombieAI>();
        thaiZombieHealth = GetComponent<ZombieHealth>();
        khoaZombie = GetComponent<ZOmbieAI_Khoa>();
        rebuiltZombie = GetComponent<ZombieAIKhoaRebuilt>();
        if (thaiZombie != null) assaultMoveSpeed = thaiZombie.ChaseMovementSpeed;
        else if (khoaZombie != null) assaultMoveSpeed = khoaZombie.ChaseMovementSpeed;
        else if (rebuiltZombie != null) assaultMoveSpeed = rebuiltZombie.ChaseMovementSpeed;
        zombieBehaviours = GetComponents<MonoBehaviour>();
        zombieAIEnabled = null;
        SetZombieAIEnabled(false);
        int stableId = GetInstanceID();
        attackIndex = StableHash01(stableId, 17) < 0.5f ? 1 : 2;
        gateAttackCooldown = Mathf.Lerp(0.15f, 1.65f, StableHash01(stableId, 29));
    }

    private void FixedUpdate()
    {
        if (manager == null || gate == null || !manager.HasStateAuthority) return;
        if (released)
        {
            if (IsZombieDead) RetireDeadZombie();
            return;
        }
        if (IsZombieDead)
        {
            RetireDeadZombie();
            return;
        }
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
        Vector2 position = body != null ? body.position : transform.position;
        Vector2 target = gate.GetAssaultPosition(GetInstanceID(), position);
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
            gateAttackCooldown = Mathf.Lerp(1.15f, 1.65f,
                StableHash01(GetInstanceID(), ++attackSequence * 31));
            attackAnimationRemaining = 0.62f;
        }
    }

    public void ReleaseToPlayers()
    {
        if (released)
        {
            return;
        }
        if (IsZombieDead)
        {
            RetireDeadZombie();
            return;
        }
        released = true;
        SetGateAttackAnimation(false);
        ApplyAnimationState(Vector2.zero);
        SetZombieAIEnabled(true);
    }

    public void RetireDeadZombie()
    {
        if (released && !enabled) return;
        released = true;
        SetGateAttackAnimation(false);
        ApplyAnimationState(Vector2.zero);
        if (body != null) body.linearVelocity = Vector2.zero;
        // Khoa variants replicate collider/death Animator state from Render(),
        // which stops running when their NetworkBehaviour is disabled. Keep
        // those dead AIs enabled: their own FixedUpdateNetwork exits on
        // NetIsDead, while Render continues to hold the corpse pose.
        if (thaiZombie != null) thaiZombie.enabled = false;
        if (khoaZombie != null) khoaZombie.enabled = true;
        if (rebuiltZombie != null) rebuiltZombie.enabled = true;
        enabled = false;
    }

    public void RestoreAmbientState()
    {
        if (IsZombieDead) return;
        released = false;
        SetGateAttackAnimation(false);
        SetZombieAIEnabled(true);
    }

    private static bool IsSpawned(NetworkBehaviour behaviour)
    {
        return behaviour != null && behaviour.Object != null && behaviour.Object.IsValid;
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
        if (zombieBehaviours == null || zombieAIEnabled == enabled) return;
        zombieAIEnabled = enabled;
        for (int i = 0; i < zombieBehaviours.Length; i++)
        {
            MonoBehaviour behaviour = zombieBehaviours[i];
            if (behaviour is ZombieAI || behaviour is ZOmbieAI_Khoa || behaviour is ZombieAIKhoaRebuilt)
                behaviour.enabled = enabled;
        }
    }

    private static float StableHash01(int value, int salt)
    {
        unchecked
        {
            uint x = (uint)value ^ ((uint)salt * 0x9E3779B9u);
            x ^= x >> 16;
            x *= 0x7FEB352Du;
            x ^= x >> 15;
            x *= 0x846CA68Bu;
            x ^= x >> 16;
            return (x & 0x00FFFFFFu) / 16777215f;
        }
    }
}
