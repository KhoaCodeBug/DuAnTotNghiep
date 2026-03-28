using System.Collections.Generic;
using Fusion;
using UnityEngine;

// 🔥 NÂNG CẤP 1: Đổi thành NetworkBehaviour để có thể dùng bộ đàm (RPC) gọi cho Host
public class HostModeSpawner : NetworkBehaviour, IPlayerJoined, IPlayerLeft
{
    [Header("--- Lò Đẻ 2.0 (Danh sách Tướng) ---")]
    [Tooltip("Kéo thả các Prefab nhân vật vào đây (0 = Nam, 1 = Nữ...)")]
    [SerializeField] private NetworkPrefabRef[] playerPrefabs;

    private readonly Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new();

    public void PlayerJoined(PlayerRef player)
    {
        // Hàm này tự động chạy trên mọi máy khi có người bước vào phòng
        // TÔI chỉ quan tâm khi TÔI là người vừa vào phòng (Chính chủ)
        if (player == Runner.LocalPlayer)
        {
            // Tự móc túi lấy cái tờ giấy ghi nhớ lúc nãy chọn ai ở Menu
            int myCharacterID = PlayerPrefs.GetInt("SelectedCharacterID", 0);

            // Gọi điện thoại thẳng cho máy Host báo cáo số ID
            RPC_RequestSpawn(player, myCharacterID);
        }
    }

    // 🔥 NÂNG CẤP 2: Cục phát sóng RPC (Mọi người đều gọi được, nhưng chỉ Host nghe và làm)
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestSpawn(PlayerRef player, int characterID)
    {
        // Chốt chặn an toàn: Chỉ máy Host mới được quyền đẻ
        if (!Runner.IsServer) return;

        // Chống hack/lỗi: Nếu ID gửi lên xạo xạo (không có trong danh sách) thì ép về 0
        if (characterID < 0 || characterID >= playerPrefabs.Length)
        {
            characterID = 0;
            Debug.LogWarning("⚠️ Có người đòi đẻ nhân vật không tồn tại. Đã ép về mặc định!");
        }

        // Chọn vị trí đẻ ngẫu nhiên (Ép Z = 0 cho game 2D)
        var spawnPosition = new Vector3(UnityEngine.Random.Range(-2f, 2f), UnityEngine.Random.Range(-2f, 2f), 0f);

        // 🔥 ĐÚC NHÂN VẬT TỪ ĐÚNG CÁI KHUÔN ĐÃ CHỌN
        var networkObject = Runner.Spawn(playerPrefabs[characterID], spawnPosition, Quaternion.identity, player);

        _spawnedCharacters[player] = networkObject;
        Debug.Log($"✅ Đã giao hàng: Đẻ nhân vật số {characterID} thành công cho người chơi {player}");
    }

    public void PlayerLeft(PlayerRef player)
    {
        if (!Runner.IsServer) return;

        if (_spawnedCharacters.TryGetValue(player, out var networkObject))
        {
            Runner.Despawn(networkObject);
            _spawnedCharacters.Remove(player);
            Debug.Log($"👋 Người chơi {player} đã thoát, dọn dẹp nhân vật.");
        }
    }
}