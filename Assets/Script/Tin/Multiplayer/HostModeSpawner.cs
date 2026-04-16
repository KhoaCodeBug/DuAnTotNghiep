using Fusion;
using UnityEngine;
using System.Collections.Generic;

public class HostModeSpawner : NetworkBehaviour, IPlayerLeft
{
    [Header("--- Lò Đẻ Kép (Nam & Nữ) ---")]
    [Tooltip("Kéo thả 2 cái Prefab vào đây (0: Nam, 1: Nữ)")]
    public NetworkPrefabRef[] playerPrefabs;

    // 🔥 THÊM VÀO: Danh sách các điểm an toàn để đẻ người chơi
    [Header("--- Điểm Hồi Sinh (Spawn Points) ---")]
    [Tooltip("Tạo các GameObject trống trên Map, đặt ở chỗ an toàn rồi kéo vào đây")]
    public Transform[] spawnPoints;

    private Dictionary<PlayerRef, NetworkObject> spawnedPlayers = new Dictionary<PlayerRef, NetworkObject>();

    public override void Spawned()
    {
        int myCharacterID = PlayerPrefs.GetInt("SelectedCharacterID", 0);
        string myPlayerName = PlayerPrefs.GetString("MyPlayerName", "Survivor");

        Debug.Log($"[SPAWNER] Tui là {myPlayerName}, tui muốn đẻ nhân vật số: {myCharacterID}");

        if (Runner.IsServer)
        {
            SpawnCharacter(Runner.LocalPlayer, myCharacterID);
        }
        else
        {
            RPC_RequestSpawn(Runner.LocalPlayer, myCharacterID);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestSpawn(PlayerRef player, int characterID)
    {
        if (!Runner.IsServer) return;
        SpawnCharacter(player, characterID);
    }

    private void SpawnCharacter(PlayerRef player, int characterID)
    {
        if (characterID < 0 || characterID >= playerPrefabs.Length)
        {
            characterID = 0;
        }

        // 🔥 LOGIC MỚI: TÌM VỊ TRÍ ĐẺ AN TOÀN
        Vector3 safeSpawnPos = Vector3.zero;
        Quaternion safeSpawnRot = Quaternion.identity;

        // Kiểm tra xem sếp đã gắn các điểm Spawn Point vào chưa
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            // Bốc ngẫu nhiên 1 điểm trong danh sách sếp đã đánh dấu
            int randomIndex = Random.Range(0, spawnPoints.Length);
            safeSpawnPos = spawnPoints[randomIndex].position;
            safeSpawnRot = spawnPoints[randomIndex].rotation; // Lấy luôn hướng xoay cho chuẩn
        }
        else
        {
            // Báo lỗi nhẹ nếu sếp quên gắn Spawn Points, tạm đẻ ở giữa tâm vũ trụ
            Debug.LogWarning("⚠️ Lò Đẻ chưa có Spawn Points! Nhân vật bị rớt xuống tọa độ (0,0,0).");
        }

        // Đẻ ra đúng nhân vật tại vị trí an toàn
        NetworkObject netObj = Runner.Spawn(playerPrefabs[characterID], safeSpawnPos, safeSpawnRot, player);

        spawnedPlayers.Add(player, netObj);
        Debug.Log($"✅ Máy chủ đã đẻ nhân vật số {characterID} cho người chơi: {player} tại {safeSpawnPos}");
    }

    public void PlayerLeft(PlayerRef player)
    {
        if (Runner.IsServer && spawnedPlayers.TryGetValue(player, out NetworkObject netObj))
        {
            Runner.Despawn(netObj);
            spawnedPlayers.Remove(player);
        }
    }
}