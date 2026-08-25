using UnityEngine;
using Fusion;
using System.Collections;
using System.Collections.Generic;
using Pathfinding;

public class ZombieSpawnZone : NetworkBehaviour
{
    public enum ZoneLevel { Level1, Level2, Level3, Level4, Level5, Level6 }

    [Header("=== Cấu hình Zone ===")]
    public ZoneLevel level = ZoneLevel.Level1;
    public Vector2 zoneSize = new Vector2(10f, 10f);

    [Header("=== Tự động thiết lập theo Level (Đọc từ bảng mẫu) ===")]
    public bool useAutoConfig = false; // Mặc định false để không đè lên các Zone đã setup tay của bạn

    [Header("=== Cấu hình Zombie ===")]
    public List<NetworkPrefabRef> zombiePrefabs;
    public int minZombies = 1;
    public int maxZombies = 5;

    [Header("=== Cấu hình Hồi sinh (Respawn) ===")]
    public float safeDistance = 7f;
    public float respawnCooldown = 120f;

    private List<ZOmbieAI_Khoa> aliveZombies = new List<ZOmbieAI_Khoa>();
    private List<ZombieAIKhoaRebuilt> aliveRebuiltZombies = new List<ZombieAIKhoaRebuilt>();
    private List<ZombieHealth> aliveThaiZombies = new List<ZombieHealth>();
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

        if (useAutoConfig)
        {
            ApplyLevelConfig(GetEffectiveLevel());
        }

        aliveZombies.RemoveAll(z => z == null || z.NetIsDead);
        aliveRebuiltZombies.RemoveAll(z => z == null || z.NetIsDead);
        aliveThaiZombies.RemoveAll(z => z == null || z.isDead);

        if (aliveZombies.Count == 0 && aliveRebuiltZombies.Count == 0 && aliveThaiZombies.Count == 0)
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
                    if (spawnedObj == null)
                    {
                        // A concurrent story/horde spawn can consume the runner's
                        // available spawn slot during this frame. Retry another
                        // point instead of dereferencing a failed spawn.
                        continue;
                    }

                    if (spawnedObj.TryGetComponent(out ZOmbieAI_Khoa zScript))
                    {
                        aliveZombies.Add(zScript);
                    }
                    else if (spawnedObj.TryGetComponent(out ZombieAIKhoaRebuilt rebuiltScript))
                    {
                        aliveRebuiltZombies.Add(rebuiltScript);
                    }
                    else if (spawnedObj.TryGetComponent(out ZombieHealth thaiHealth))
                    {
                        aliveThaiZombies.Add(thaiHealth);
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

    public ZoneLevel GetEffectiveLevel()
    {
        int currentLevelInt = (int)level;
        
        // Đọc độ khó từ PlayerPrefs (Easy=0, Normal=1, Hardcore=2)
        int difficulty = PlayerPrefs.GetInt("GameDifficulty", 1); 
        
        // Nếu độ khó là Hardcore (2), tăng level lên 1 bậc
        if (difficulty == 2)
        {
            currentLevelInt = Mathf.Min(currentLevelInt + 1, (int)ZoneLevel.Level6);
        }
        // Nếu độ khó là Easy (0), giảm level đi 1 bậc
        else if (difficulty == 0)
        {
            currentLevelInt = Mathf.Max(currentLevelInt - 1, (int)ZoneLevel.Level1);
        }
        
        return (ZoneLevel)currentLevelInt;
    }

    public void ApplyLevelConfig(ZoneLevel targetLevel)
    {
        switch (targetLevel)
        {
            case ZoneLevel.Level1:
                minZombies = 1;
                maxZombies = 3;
                safeDistance = 10f;
                respawnCooldown = 180f;
                break;
            case ZoneLevel.Level2:
                minZombies = 3;
                maxZombies = 6;
                safeDistance = 9f;
                respawnCooldown = 150f;
                break;
            case ZoneLevel.Level3:
                minZombies = 6;
                maxZombies = 12;
                safeDistance = 8f;
                respawnCooldown = 120f;
                break;
            case ZoneLevel.Level4:
                minZombies = 12;
                maxZombies = 20;
                safeDistance = 8f;
                respawnCooldown = 90f;
                break;
            case ZoneLevel.Level5:
                minZombies = 20;
                maxZombies = 35;
                safeDistance = 7f;
                respawnCooldown = 60f;
                break;
            case ZoneLevel.Level6:
                minZombies = 35;
                maxZombies = 50;
                safeDistance = 6f;
                respawnCooldown = 45f;
                break;
        }
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
        if (level == ZoneLevel.Level2) zoneColor = new Color(0.9f, 0.7f, 0f); // Cam vàng
        else if (level == ZoneLevel.Level3) zoneColor = Color.red; // Đỏ
        else if (level == ZoneLevel.Level4) zoneColor = new Color(0.8f, 0f, 0.8f); // Tím
        else if (level == ZoneLevel.Level5) zoneColor = new Color(0.4f, 0f, 0.4f); // Tím đậm
        else if (level == ZoneLevel.Level6) zoneColor = Color.black; // Đen nguy hiểm

        Gizmos.color = new Color(zoneColor.r, zoneColor.g, zoneColor.b, 0.3f);
        Gizmos.DrawCube(transform.position, new Vector3(zoneSize.x, zoneSize.y, 0.1f));
        Gizmos.color = zoneColor;
        Gizmos.DrawWireCube(transform.position, new Vector3(zoneSize.x, zoneSize.y, 0.1f));

        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, safeDistance);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        gameObject.name = "Zone_" + level.ToString();

        if (useAutoConfig)
        {
            ApplyLevelConfig(level);
        }
    }
#endif
}
