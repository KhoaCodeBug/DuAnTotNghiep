using Fusion;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class HostModeSpawner : NetworkBehaviour, IPlayerLeft
{
    public static HostModeSpawner Instance { get; private set; }

    [Header("--- Lò Đẻ Kép (Nam & Nữ) ---")]
    public NetworkPrefabRef[] playerPrefabs;

    [Header("--- Điểm Hồi Sinh (Spawn Points) ---")]
    public Transform[] spawnPoints;

    [Header("--- Intro / Tutorial ---")]
    [Tooltip("Only enable this in the solo Intro scene. The tutorial director will explicitly start spawning after the cinematic.")]
    [SerializeField] private bool deferInitialSpawn;

    private Dictionary<PlayerRef, NetworkObject> spawnedPlayers = new Dictionary<PlayerRef, NetworkObject>();
    // A player receives the story-arrival position only once per connection.
    // Death respawns continue to use the scene's original, dispersed house points.
    private HashSet<PlayerRef> playersGivenArrivalSpawn = new HashSet<PlayerRef>();
    // Lives for the current room/session.  A respawn creates a new player
    // NetworkObject, but the same PlayerRef keeps its original starting gun.
    private Dictionary<PlayerRef, string> startingWeaponByPlayer = new Dictionary<PlayerRef, string>();
    private bool spawnRoutineStarted;

    // 🔥 CÁC BIẾN ĐỒNG BỘ MẠNG
    [Networked] public bool IsMatchStarted { get; set; } // Đánh dấu game đã bắt đầu chưa
    private int playersLoadedMap = 0; // (Chỉ Host dùng) Đếm số người đã tải xong Map

    public override void Spawned()
    {
        Instance = this;
        MainArrivalStoryBootstrap.EnsureMainSceneSetup(this);
        if (!deferInitialSpawn)
            BeginInitialSpawn();
    }

    public void BeginInitialSpawn()
    {
        if (spawnRoutineStarted) return;
        spawnRoutineStarted = true;
        StartCoroutine(SpawnRoutine());
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private IEnumerator SpawnRoutine()
    {
        // 🔥 ĐỢI CHO ĐẾN KHI LOCAL PLAYER CÓ ID HỢP LỆ (Tránh lỗi PlayerRef.None làm mất InputAuthority trên Host)
        float timeout = 4f;
        while (Runner != null && Runner.LocalPlayer == PlayerRef.None && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (Runner == null) yield break;

        // The standalone tutorial always uses survivor prefab 0 without
        // overwriting the character that the player chose in the main menu.
        int myCharacterID = TutorialSession.IsActive ? 0 : PlayerPrefs.GetInt("SelectedCharacterID", 0);
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

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestRespawn(PlayerRef player, int characterID, string playerName, RpcInfo info = default)
    {
        if (!Runner.IsServer) return;
        // A client may only request a respawn for its own PlayerRef.
        if (info.Source != PlayerRef.None && info.Source != player)
        {
            Debug.LogWarning($"[SPAWNER] Rejected respawn spoof: {info.Source} requested {player}.");
            return;
        }

        if (spawnedPlayers.TryGetValue(player, out NetworkObject currentObject)
            && currentObject != null && currentObject.IsValid)
        {
            PlayerHealth currentHealth = currentObject.GetComponent<PlayerHealth>();
            if (currentHealth != null && !currentHealth.isDead && !currentHealth.isTransforming)
            {
                Debug.LogWarning($"[SPAWNER] Rejected respawn for living player {player}.");
                return;
            }
        }

        // Despawn old character if it exists
        if (spawnedPlayers.TryGetValue(player, out NetworkObject oldNetObj) && oldNetObj != null)
        {
            Runner.Despawn(oldNetObj);
            spawnedPlayers.Remove(player);
        }

        SpawnCharacter(player, characterID, playerName);
    }

    private void SpawnCharacter(PlayerRef player, int characterID, string playerName)
    {
        if (spawnedPlayers.ContainsKey(player) && spawnedPlayers[player] != null)
        {
            Debug.LogWarning($"[SPAWNER] Người chơi {player} ({playerName}) đã có nhân vật! Bỏ qua đẻ trùng.");
            return;
        }
        if (characterID < 0 || characterID >= playerPrefabs.Length) characterID = 0;

        Vector3 safeSpawnPos = Vector3.zero;
        Quaternion safeSpawnRot = Quaternion.identity;

        bool isFirstSpawn = !playersGivenArrivalSpawn.Contains(player);
        bool useArrivalSpawn = isFirstSpawn &&
            MainArrivalStoryBootstrap.TryGetInitialSpawnPose(playersGivenArrivalSpawn.Count,
                out safeSpawnPos, out safeSpawnRot);

        if (!useArrivalSpawn && spawnPoints != null && spawnPoints.Length > 0)
        {
            int randomIndex = Random.Range(0, spawnPoints.Length);
            safeSpawnPos = spawnPoints[randomIndex].position;
            safeSpawnRot = spawnPoints[randomIndex].rotation;
        }

        NetworkObject netObj = Runner.Spawn(playerPrefabs[characterID], safeSpawnPos, safeSpawnRot, player);

        // 🔥 FIX LỖI 2: Dùng chép đè để tránh Crash nếu người chơi gửi lệnh đẻ 2 lần do lag
        spawnedPlayers[player] = netObj;
        if (isFirstSpawn) playersGivenArrivalSpawn.Add(player);
        Runner.SetPlayerObject(player, netObj);

        // 🔥 LOGIC LATE JOIN (NGƯỜI CHƠI NHẢY DÙ VÀO SAU)
        if (IsMatchStarted)
        {
            RPC_AnnounceLateJoin(playerName); // Báo tin lên Chat
            RPC_PlayBlinkEffect(netObj);      // Cho bất tử chớp nháy 3 giây
        }
    }

    public bool TryGetCachedStartingWeapon(PlayerRef player, out ItemData weapon)
    {
        weapon = null;
        if (!Runner.IsServer || !startingWeaponByPlayer.TryGetValue(player, out string itemId)) return false;

        weapon = ItemDataLoader.LoadItem(itemId);
        if (weapon != null && weapon.category == ItemCategory.Weapon) return true;

        // Do not keep an invalid asset ID in the session cache.
        startingWeaponByPlayer.Remove(player);
        weapon = null;
        return false;
    }

    public void CacheStartingWeapon(PlayerRef player, ItemData weapon)
    {
        if (!Runner.IsServer || weapon == null || weapon.category != ItemCategory.Weapon) return;
        startingWeaponByPlayer[player] = weapon.name;
    }

    public bool TryGetPlayerInventory(PlayerRef player, out InventorySystem inventory)
    {
        inventory = null;
        if (!Runner.IsServer || !spawnedPlayers.TryGetValue(player, out NetworkObject playerObject) || playerObject == null) return false;

        inventory = playerObject.GetComponent<InventorySystem>();
        return inventory != null;
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
        if (AutoChatManager.Instance != null)
        {
            AutoChatManager.Instance.AddMessage("SYSTEM", msg);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_PlayBlinkEffect(NetworkObject netObj)
    {
        if (netObj != null) StartCoroutine(LateJoinProtection(netObj.gameObject));
    }

    private IEnumerator LateJoinProtection(GameObject playerObj)
    {
        if (playerObj == null) yield break;

        // Chỉ nhấp nháy những Renderer vốn đang hiển thị (tránh hiện các renderer ẩn như Muzzle Flash)
        System.Collections.Generic.List<Renderer> meshesToBlink = new System.Collections.Generic.List<Renderer>();
        foreach (var r in playerObj.GetComponentsInChildren<Renderer>(true))
        {
            if (r != null && r.enabled && r.gameObject.name != "MuzzleFlash" && !r.gameObject.name.Contains("Muzzle"))
            {
                meshesToBlink.Add(r);
            }
        }

        float timer = 3f; // 3 Giây bất tử
        bool isVisible = true;

        while (timer > 0)
        {
            // 🔥 FIX LỖI 3: Nếu đứa vô sau thoát game lúc đang chớp nháy -> Dừng lệnh ngay kẻo văng lỗi
            if (playerObj == null) yield break;

            timer -= 0.2f;
            isVisible = !isVisible;
            foreach (var mesh in meshesToBlink)
            {
                if (mesh != null) mesh.enabled = isVisible;
            }
            yield return new WaitForSeconds(0.2f);
        }

        // Bật lại lưới hiển thị bình thường nếu player vẫn còn tồn tại
        if (playerObj != null)
        {
            foreach (var mesh in meshesToBlink)
            {
                if (mesh != null) mesh.enabled = true;
            }
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
            startingWeaponByPlayer.Remove(player);
            playersGivenArrivalSpawn.Remove(player);

            // 🔥 FIX LỖI 1: Kẹt Loading. Nếu có đứa rớt mạng lúc đang ở sảnh chờ load, tự động check và cho những người còn lại vào game!
            if (!IsMatchStarted)
            {
                CheckAndStartGame();
            }
        }
    }
}
