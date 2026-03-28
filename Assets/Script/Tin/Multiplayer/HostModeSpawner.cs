using Fusion;
using UnityEngine;
using System.Collections.Generic;

public class HostModeSpawner : NetworkBehaviour, IPlayerLeft
{
    [Header("--- Lò Đẻ Kép (Nam & Nữ) ---")]
    [Tooltip("Kéo thả 2 cái Prefab vào đây (0: Nam, 1: Nữ)")]
    public NetworkPrefabRef[] playerPrefabs;

    private Dictionary<PlayerRef, NetworkObject> spawnedPlayers = new Dictionary<PlayerRef, NetworkObject>();

    // 🔥 BÍ KÍP TỐI THƯỢNG: Dùng hàm Spawned thay vì PlayerJoined
    // Nó đảm bảo 100% Lò Đẻ đã hiện hồn rồi mới bắt đầu đẻ, không bao giờ bị hụt nhịp!
    public override void Spawned()
    {
        // Móc RAM ra xem hồi nãy ở ngoài Menu chọn nhân vật số mấy
        int myCharacterID = CharacterSelectionMenu.LocalSelectedCharacterID;

        if (Runner.IsServer)
        {
            // Nếu TÔI là Host: Tự lấy tay đẻ luôn cho bản thân, khỏi cần gọi điện!
            SpawnCharacter(Runner.LocalPlayer, myCharacterID);
        }
        else
        {
            // Nếu TÔI là Client (Clone): Lò đẻ vừa tải xong, gọi điện réo Host đẻ cho em!
            RPC_RequestSpawn(Runner.LocalPlayer, myCharacterID);
        }
    }

    // Cục phát sóng: Chỉ Host mới có quyền xử lý lệnh này
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestSpawn(PlayerRef player, int characterID)
    {
        if (!Runner.IsServer) return;
        SpawnCharacter(player, characterID);
    }

    // Hàm đẻ thực tế (Chỉ Host mới được chạy hàm này)
    private void SpawnCharacter(PlayerRef player, int characterID)
    {
        // Chống lỗi nhập bậy
        if (characterID < 0 || characterID >= playerPrefabs.Length) characterID = 0;

        Vector3 spawnPos = new Vector3(Random.Range(-2f, 2f), Random.Range(-2f, 2f), 0f);

        // Đẻ ra đúng nhân vật đã chọn và giao quyền điều khiển (Input Authority) cho thằng gọi
        NetworkObject netObj = Runner.Spawn(playerPrefabs[characterID], spawnPos, Quaternion.identity, player);

        spawnedPlayers.Add(player, netObj);
        Debug.Log($"✅ Máy chủ đã đẻ nhân vật số {characterID} cho {player}");
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