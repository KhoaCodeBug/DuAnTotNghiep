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
        spawnedPlayers.Add(player, netObj);

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
        int currentPlayersInRoom = Runner.SessionInfo.PlayerCount;

        Debug.Log($"[ĐIỂM DANH] Đã có {playersLoadedMap}/{currentPlayersInRoom} người tải xong Map.");

        // NẾU TẤT CẢ ĐÃ TẢI XONG -> PHÁT LỆNH GO!!!
        if (playersLoadedMap >= currentPlayersInRoom)
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
            // Tui dùng lệnh tĩnh, nếu sếp có Instance thì thay bằng AutoChatManager.Instance
            SendMessageToChat($"<color=#00ff00>Viện binh đang đến: {playerName} đã nhảy dù xuống khu vực!</color>");
        }
    }

    private void SendMessageToChat(string msg)
    {
        // Sếp tùy chỉnh lại dòng này gọi đúng vào hàm AddMessage trong AutoChatManager của sếp nhé
        // VD: AutoChatManager.Instance.AddMessage("HỆ THỐNG", msg);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_PlayBlinkEffect(NetworkObject netObj)
    {
        if (netObj != null) StartCoroutine(LateJoinProtection(netObj.gameObject));
    }

    private IEnumerator LateJoinProtection(GameObject playerObj)
    {
        // (Sếp có thể nhét code tắt Damage nhận vào ở đây)

        Renderer[] meshes = playerObj.GetComponentsInChildren<Renderer>();
        float timer = 3f; // 3 Giây bất tử
        bool isVisible = true;

        while (timer > 0)
        {
            timer -= 0.2f;
            isVisible = !isVisible;
            foreach (var mesh in meshes) mesh.enabled = isVisible;
            yield return new WaitForSeconds(0.2f);
        }

        // Bật lại lưới hiển thị bình thường
        foreach (var mesh in meshes) mesh.enabled = true;
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