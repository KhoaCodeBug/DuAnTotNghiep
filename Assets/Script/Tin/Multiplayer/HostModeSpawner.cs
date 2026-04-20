using Fusion;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class HostModeSpawner : NetworkBehaviour, IPlayerLeft
{
    [Header("--- Lò Đẻ Kép (Nam & Nữ) ---")]
    public NetworkPrefabRef[] playerPrefabs;

    [Header("--- Điểm Hồi Sinh (Spawn Points) ---")]
    public Transform[] spawnPoints;

    private Dictionary<PlayerRef, NetworkObject> spawnedPlayers = new Dictionary<PlayerRef, NetworkObject>();

    // 🔥 CÁC BIẾN ĐỒNG BỘ MẠNG
    [Networked] public bool IsMatchStarted { get; set; } // Đánh dấu game đã bắt đầu chưa
    private int playersLoadedMap = 0; // (Chỉ Host dùng) Đếm số người đã tải xong Map

    public override void Spawned()
    {
        int myCharacterID = PlayerPrefs.GetInt("SelectedCharacterID", 0);
        string myPlayerName = PlayerPrefs.GetString("MyPlayerName", "Survivor");

        // 1. Gửi lệnh đẻ nhân vật
        if (Runner.IsServer)
        {
            SpawnCharacter(Runner.LocalPlayer, myCharacterID, myPlayerName);
        }
        else
        {
            RPC_RequestSpawn(Runner.LocalPlayer, myCharacterID, myPlayerName);
        }

        // 2. Báo cáo cho Host: "Sếp ơi em đã tải Map xong và đang ở vị trí!"
        RPC_PlayerFinishedLoadingMap(Runner.LocalPlayer);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestSpawn(PlayerRef player, int characterID, string playerName)
    {
        if (!Runner.IsServer) return;
        SpawnCharacter(player, characterID, playerName);
    }

    private void SpawnCharacter(PlayerRef player, int characterID, string playerName)
    {
        if (characterID < 0 || characterID >= playerPrefabs.Length) characterID = 0;

        Vector3 safeSpawnPos = Vector3.zero;
        Quaternion safeSpawnRot = Quaternion.identity;

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            int randomIndex = Random.Range(0, spawnPoints.Length);
            safeSpawnPos = spawnPoints[randomIndex].position;
            safeSpawnRot = spawnPoints[randomIndex].rotation;
        }

        NetworkObject netObj = Runner.Spawn(playerPrefabs[characterID], safeSpawnPos, safeSpawnRot, player);

        // 🔥 FIX LỖI 2: Dùng chép đè để tránh Crash nếu người chơi gửi lệnh đẻ 2 lần do lag
        spawnedPlayers[player] = netObj;

        // 🔥 LOGIC LATE JOIN (NGƯỜI CHƠI NHẢY DÙ VÀO SAU)
        if (IsMatchStarted)
        {
            RPC_AnnounceLateJoin(playerName); // Báo tin lên Chat
            RPC_PlayBlinkEffect(netObj);      // Cho bất tử chớp nháy 3 giây
        }
    }

    // ========================================================
    // 🔥 HỆ THỐNG ĐIỂM DANH & ĐỒNG BỘ LOADING (CHỐT CHẶN 95%)
    // ========================================================

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_PlayerFinishedLoadingMap(PlayerRef playerRef)
    {
        // Nhánh 1: Nếu game đã bắt đầu từ lâu, đây là người đi trễ (Nhảy dù)
        if (IsMatchStarted)
        {
            RPC_OpenEyesForLateJoiner(playerRef); // Gọi riêng nó mở mắt lập tức
            return;
        }

        // Nhánh 2: Game chưa bắt đầu, đang ở đoạn Đồng Bộ Đầu Trận
        playersLoadedMap++;
        CheckAndStartGame(); // Tách ra thành hàm riêng để check ở nhiều chỗ
    }

    private void CheckAndStartGame()
    {
        if (!Runner.IsServer || IsMatchStarted) return;

        int currentPlayersInRoom = Runner.SessionInfo.PlayerCount;
        Debug.Log($"[ĐIỂM DANH] Đã có {playersLoadedMap}/{currentPlayersInRoom} người tải xong Map.");

        // NẾU TẤT CẢ ĐÃ TẢI XONG -> PHÁT LỆNH GO!!!
        if (playersLoadedMap >= currentPlayersInRoom && playersLoadedMap > 0)
        {
            IsMatchStarted = true;
            RPC_OpenEyesForAll();
        }
    }

    // Lệnh phát thanh cho TOÀN BỘ SERVER cùng mở mắt (Đẩy Loading lên 100%)
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_OpenEyesForAll()
    {
        if (AutoMainMenuManager.Instance != null)
        {
            AutoMainMenuManager.Instance.ForceCloseLoadingScreen();
        }
    }

    // Lệnh gọi điện riêng cho thằng đi trễ (Late Joiner) bảo nó mở mắt
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_OpenEyesForLateJoiner([RpcTarget] PlayerRef targetPlayer)
    {
        if (AutoMainMenuManager.Instance != null)
        {
            AutoMainMenuManager.Instance.ForceCloseLoadingScreen();
        }
    }

    // ========================================================
    // 🔥 HIỆU ỨNG NHẢY DÙ CỨU VIỆN
    // ========================================================

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_AnnounceLateJoin(string playerName)
    {
        // Bắn dòng chữ lên AutoChatManager của mọi người
        if (GameObject.Find("--- AUTO CHAT MANAGER ---") != null)
        {
            SendMessageToChat($"<color=#00ff00>Viện binh đang đến: {playerName} đã thâm nhập khu vực!</color>");
        }
    }

    private void SendMessageToChat(string msg)
    {
        // Sếp tùy chỉnh lại dòng này gọi đúng vào hàm AddMessage trong AutoChatManager của sếp nhé
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_PlayBlinkEffect(NetworkObject netObj)
    {
        if (netObj != null) StartCoroutine(LateJoinProtection(netObj.gameObject));
    }

    private IEnumerator LateJoinProtection(GameObject playerObj)
    {
        Renderer[] meshes = playerObj.GetComponentsInChildren<Renderer>();
        float timer = 3f; // 3 Giây bất tử
        bool isVisible = true;

        while (timer > 0)
        {
            // 🔥 FIX LỖI 3: Nếu đứa vô sau thoát game lúc đang chớp nháy -> Dừng lệnh ngay kẻo văng lỗi
            if (playerObj == null) yield break;

            timer -= 0.2f;
            isVisible = !isVisible;
            foreach (var mesh in meshes) mesh.enabled = isVisible;
            yield return new WaitForSeconds(0.2f);
        }

        // Bật lại lưới hiển thị bình thường nếu player vẫn còn tồn tại
        if (playerObj != null)
        {
            foreach (var mesh in meshes) mesh.enabled = true;
        }
    }

    public void PlayerLeft(PlayerRef player)
    {
        if (Runner.IsServer)
        {
            // Xóa xác nhân vật
            if (spawnedPlayers.TryGetValue(player, out NetworkObject netObj))
            {
                Runner.Despawn(netObj);
                spawnedPlayers.Remove(player);
            }

            // 🔥 FIX LỖI 1: Kẹt Loading. Nếu có đứa rớt mạng lúc đang ở sảnh chờ load, tự động check và cho những người còn lại vào game!
            if (!IsMatchStarted)
            {
                CheckAndStartGame();
            }
        }
    }
}