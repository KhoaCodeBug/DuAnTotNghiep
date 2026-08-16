using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>Host-only wave spawning plus the initial gate objective for spawned siege zombies.</summary>
public sealed class SiegeHordeDirector : MonoBehaviour
{
    [SerializeField, Min(1f)] private float secondsBetweenWaves = 10f;
    [SerializeField, Min(1)] private int maxAliveSiegeZombies = 24;
    [SerializeField, Min(1f)] private float spawnRadius = 5f;

    private readonly List<SiegeZombieObjective> activeObjectives = new();
    private readonly List<NetworkPrefabRef> zombiePrefabs = new();
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
    }

    public void BeginSiege()
    {
        siegeActive = true;
        releasedToPlayers = false;
        if (manager == null || !manager.HasStateAuthority || siegeRoutine != null) return;
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
        yield return new WaitForSeconds(1f);
        while (siegeActive && manager != null && manager.IsNetworkReady &&
               manager.CurrentPhase == MilitaryBaseQuestManager.Phase.SiegeAndRepair)
        {
            activeObjectives.RemoveAll(item => item == null);
            if (!releasedToPlayers && activeObjectives.Count < maxAliveSiegeZombies) SpawnWave();
            yield return new WaitForSeconds(secondsBetweenWaves);
        }
        siegeRoutine = null;
    }

    private void SpawnWave()
    {
        if (manager.Runner == null || zombiePrefabs.Count == 0)
        {
            Debug.LogWarning("[MILITARY QUEST] Không tìm thấy NetworkPrefabRef zombie từ các ZombieSpawnZone.");
            return;
        }

        int difficulty = PlayerPrefs.GetInt("GameDifficulty", 1);
        int requested = difficulty switch { 0 => 3, 2 => 7, _ => 5 };
        int count = Mathf.Min(requested, maxAliveSiegeZombies - activeObjectives.Count);
        Vector2 gatePosition = manager.GetInteractionPosition(MilitaryBaseQuestManager.InteractionKind.Gate);
        for (int i = 0; i < count; i++)
        {
            float x = count == 1 ? 0f : Mathf.Lerp(-spawnRadius, spawnRadius, i / (float)(count - 1));
            Vector2 position = gatePosition + new Vector2(x + Random.Range(-0.6f, 0.6f), -spawnRadius - 2f);
            NetworkPrefabRef prefab = zombiePrefabs[Random.Range(0, zombiePrefabs.Count)];
            NetworkObject spawned = manager.Runner.Spawn(prefab, position, Quaternion.identity);
            if (spawned == null) continue;
            SiegeZombieObjective objective = spawned.gameObject.AddComponent<SiegeZombieObjective>();
            objective.Configure(manager, gate);
            activeObjectives.Add(objective);
        }
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
    private MilitaryBaseQuestManager manager;
    private MilitaryGateController gate;
    private Rigidbody2D body;
    private MonoBehaviour[] zombieBehaviours;
    private float stunUntil;
    private float nextShockTime;
    private bool released;

    public void Configure(MilitaryBaseQuestManager targetManager, MilitaryGateController targetGate)
    {
        manager = targetManager;
        gate = targetGate;
        body = GetComponent<Rigidbody2D>();
        zombieBehaviours = GetComponents<MonoBehaviour>();
        SetZombieAIEnabled(false);
    }

    private void FixedUpdate()
    {
        if (released || manager == null || gate == null || !manager.HasStateAuthority) return;
        if (manager.IsGateBroken)
        {
            ReleaseToPlayers();
            return;
        }
        if (Time.time < stunUntil)
        {
            SetVelocity(Vector2.zero);
            return;
        }

        Vector2 target = manager.GetInteractionPosition(MilitaryBaseQuestManager.InteractionKind.Gate);
        Vector2 position = body != null ? body.position : transform.position;
        float distance = Vector2.Distance(position, target);
        if (distance > 0.9f)
        {
            Vector2 velocity = (target - position).normalized * 1.65f;
            SetVelocity(velocity);
            if (body == null) transform.position += (Vector3)(velocity * Time.fixedDeltaTime);
            return;
        }

        SetVelocity(Vector2.zero);
        gate.ApplyHordePressure(Time.fixedDeltaTime, 1);
        if (manager.IsGeneratorActive && Time.time >= nextShockTime)
        {
            nextShockTime = Time.time + 1.5f;
            gate.ElectrifyZombie(gameObject);
        }
    }

    public void ApplyElectricStun(float seconds)
    {
        stunUntil = Mathf.Max(stunUntil, Time.time + Mathf.Max(0f, seconds));
        SetVelocity(Vector2.zero);
    }

    public void ReleaseToPlayers()
    {
        if (released) return;
        released = true;
        SetZombieAIEnabled(true);
    }

    private void SetVelocity(Vector2 velocity)
    {
        if (body != null) body.linearVelocity = velocity;
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
