using UnityEngine;
using Fusion;
using System.Collections;
using System.Collections.Generic;
using Pathfinding;

public class ZombieSpawnZone : NetworkBehaviour
{
    public enum ZoneLevel { Level1, Level2, Level3 }

    [Header("=== Cấu hình Zone ===")]
    public ZoneLevel level = ZoneLevel.Level1;
    public Vector2 zoneSize = new Vector2(10f, 10f);

    [Header("=== Cấu hình Zombie ===")]
    public List<NetworkPrefabRef> zombiePrefabs;
    public int minZombies = 1;
    public int maxZombies = 5;

    [Header("=== Cấu hình Hồi sinh (Respawn) ===")]
    public float safeDistance = 7f;
    public float respawnCooldown = 120f;

    private List<ZOmbieAI_Khoa> aliveZombies = new List<ZOmbieAI_Khoa>();
    private float currentCooldown;
    private float checkPlayerTimer = 0f;
    private bool isSpawning = false;

    // [MỚI] Biến để đánh dấu lần đẻ đầu tiên
    private bool isFirstWave = true;

    public override void Spawned()
    {
        if (!HasStateAuthority) return;
        currentCooldown = 0f;
        isFirstWave = true; // Vừa vào game là tính đợt 1
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || isSpawning) return;

        aliveZombies.RemoveAll(z => z == null || z.NetIsDead);

        if (aliveZombies.Count == 0)
        {
            // Chỉ bắt đầu đếm Cooldown và Check Player nếu KHÔNG PHẢI lần đẻ đầu tiên
            if (!isFirstWave)
            {
                currentCooldown -= Runner.DeltaTime;

                checkPlayerTimer -= Runner.DeltaTime;
                if (checkPlayerTimer <= 0f)
                {
                    checkPlayerTimer = 1f;
                    if (IsPlayerNearZone())
                    {
                        currentCooldown = respawnCooldown;
                    }
                }
            }
            else
            {
                currentCooldown = 0f; // Ép cooldown về 0 cho lần đẻ đầu
            }

            if (currentCooldown <= 0f)
            {
                StartCoroutine(SpawnZombiesRoutine());
            }
        }
    }

    private bool IsPlayerNearZone()
    {
        GameObject[] allPlayers = GameObject.FindGameObjectsWithTag("Player");
        Vector2 myPos = transform.position;

        foreach (GameObject p in allPlayers)
        {
            float dist = Vector2.Distance(myPos, p.transform.position);
            if (dist <= safeDistance) return true;
        }
        return false;
    }

    private IEnumerator SpawnZombiesRoutine()
    {
        if (zombiePrefabs == null || zombiePrefabs.Count == 0)
        {
            Debug.LogError($"<color=red>[LỖI] Zone {gameObject.name} chưa gán Prefab!</color>");
            yield break;
        }

        isSpawning = true;
        int spawnCount = Random.Range(minZombies, maxZombies + 1);

        for (int i = 0; i < spawnCount; i++)
        {
            bool hasSpawnedThisZombie = false;

            // [MỚI] Cho nó cơ hội thử 10 lần nếu random trúng tường
            for (int attempt = 0; attempt < 10; attempt++)
            {
                Vector2 randomPoint = GetRandomPointInZone();
                GraphNode node = AstarPath.active.GetNearest(randomPoint).node;

                if (node != null && node.Walkable)
                {
                    int randomPrefabIndex = Random.Range(0, zombiePrefabs.Count);
                    Vector3 spawnPos = (Vector3)node.position;

                    NetworkObject spawnedObj = Runner.Spawn(zombiePrefabs[randomPrefabIndex], spawnPos, Quaternion.identity);

                    if (spawnedObj.TryGetComponent(out ZOmbieAI_Khoa zScript))
                    {
                        aliveZombies.Add(zScript);
                    }

                    hasSpawnedThisZombie = true;
                    break; // Đẻ thành công thì thoát vòng lặp "cố chấp" này
                }
            }

            // Chỉ chờ 0.2s nếu con đó đẻ thành công, không thì qua con khác luôn
            if (hasSpawnedThisZombie) yield return new WaitForSeconds(0.2f);
        }

        currentCooldown = respawnCooldown;
        isFirstWave = false; // Xong đợt 1 rồi, tắt đi để đợt sau phải chờ Cooldown
        isSpawning = false;
    }

    private Vector2 GetRandomPointInZone()
    {
        float halfWidth = zoneSize.x / 2f;
        float halfHeight = zoneSize.y / 2f;
        float randomX = Random.Range(-halfWidth, halfWidth);
        float randomY = Random.Range(-halfHeight, halfHeight);
        return (Vector2)transform.position + new Vector2(randomX, randomY);
    }

    private void OnDrawGizmos()
    {
        Color zoneColor = Color.green;
        if (level == ZoneLevel.Level2) zoneColor = Color.yellow;
        else if (level == ZoneLevel.Level3) zoneColor = Color.red;

        Gizmos.color = new Color(zoneColor.r, zoneColor.g, zoneColor.b, 0.3f);
        Gizmos.DrawCube(transform.position, new Vector3(zoneSize.x, zoneSize.y, 0.1f));
        Gizmos.color = zoneColor;
        Gizmos.DrawWireCube(transform.position, new Vector3(zoneSize.x, zoneSize.y, 0.1f));

        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, safeDistance);
    }
}