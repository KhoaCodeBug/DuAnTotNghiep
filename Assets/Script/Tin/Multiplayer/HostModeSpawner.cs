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
    // Last known look/name per player so the authority can auto-respawn them
    // at the military checkpoint without asking the (dead) client first.
    private Dictionary<PlayerRef, int> characterIdByPlayer = new Dictionary<PlayerRef, int>();
    private Dictionary<PlayerRef, string> playerNameByPlayer = new Dictionary<PlayerRef, string>();
    private readonly Dictionary<PlayerRef, string> persistenceKeyByPlayer = new Dictionary<PlayerRef, string>();
    private readonly Dictionary<string, PlayerHealth.WoundSnapshot> woundSnapshotByUser =
        new Dictionary<string, PlayerHealth.WoundSnapshot>();
    private Dictionary<PlayerRef, InventorySystem.MilitaryRespawnSnapshot> militaryInventoryByPlayer =
        new Dictionary<PlayerRef, InventorySystem.MilitaryRespawnSnapshot>();
    private Dictionary<PlayerRef, PlayerCombat.MilitaryRespawnCombatSnapshot> militaryCombatByPlayer =
        new Dictionary<PlayerRef, PlayerCombat.MilitaryRespawnCombatSnapshot>();
    private readonly Dictionary<PlayerRef, InventorySystem.MilitaryRespawnSnapshot> soloMilitaryCheckpointInventory =
        new Dictionary<PlayerRef, InventorySystem.MilitaryRespawnSnapshot>();
    private readonly Dictionary<PlayerRef, PlayerCombat.MilitaryRespawnCombatSnapshot> soloMilitaryCheckpointCombat =
        new Dictionary<PlayerRef, PlayerCombat.MilitaryRespawnCombatSnapshot>();
    private bool spawnRoutineStarted;

    // 🔥 CÁC BIẾN ĐỒNG BỘ MẠNG
    [Networked] public bool IsMatchStarted { get; set; } // Đánh dấu game đã bắt đầu chưa
    private readonly HashSet<PlayerRef> playersLoadedSet = new HashSet<PlayerRef>();

    public int ReadyPlayerCount => playersLoadedSet.Count;

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
    public void RPC_RequestSpawn(PlayerRef player, int characterID, string playerName, RpcInfo info = default)
    {
        if (!Runner.IsServer) return;
        if (!TryResolveAuthenticatedPlayer(player, info.Source, out PlayerRef authoritativePlayer))
        {
            Debug.LogWarning($"[SPAWNER] Rejected spoofed request from {info.Source} for {player}.");
            return;
        }

        SpawnCharacter(authoritativePlayer, characterID, playerName);
    }

    public static bool TryResolveAuthenticatedPlayer(PlayerRef claimedPlayer, PlayerRef rpcSource,
        out PlayerRef authoritativePlayer)
    {
        authoritativePlayer = rpcSource != PlayerRef.None ? rpcSource : claimedPlayer;
        return authoritativePlayer != PlayerRef.None &&
               (rpcSource == PlayerRef.None || rpcSource == claimedPlayer);
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

        // During the military finale the authority auto-respawn governs deaths;
        // manual requests would bypass the 10s delay and team charge pool.
        MilitaryBaseQuestManager questManager = MilitaryBaseQuestManager.Instance;
        if (questManager != null && (questManager.GovernsRespawn || questManager.CanOfferSoloRetry))
        {
            Debug.Log("[SPAWNER] Từ chối respawn thủ công: hệ thống hồi sinh quân sự đang điều phối.");
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

        Vector2? storyCheckpoint = TryResolveStoryRespawnPosition(out Vector2 checkpointPosition)
            ? checkpointPosition : null;
        SpawnCharacter(player, characterID, playerName, storyCheckpoint);
    }

    /// <summary>
    /// Server-side respawn used by the military auto-respawn system. Spawns at
    /// the given checkpoint position instead of a random scene spawn point.
    /// Returns false when the player already has a living avatar.
    /// </summary>
    public bool AuthorityRespawnAtCheckpoint(PlayerRef player, Vector2 position)
    {
        if (!Runner.IsServer) return false;
        if (spawnedPlayers.TryGetValue(player, out NetworkObject currentObject)
            && currentObject != null && currentObject.IsValid)
        {
            PlayerHealth currentHealth = currentObject.GetComponent<PlayerHealth>();
            if (currentHealth != null && !currentHealth.isDead && !currentHealth.isTransforming)
                return false;
        }

        if (spawnedPlayers.TryGetValue(player, out NetworkObject oldNetObj) && oldNetObj != null)
        {
            Runner.Despawn(oldNetObj);
            spawnedPlayers.Remove(player);
        }

        characterIdByPlayer.TryGetValue(player, out int characterID);
        string playerName = playerNameByPlayer.TryGetValue(player, out string cachedName)
            ? cachedName : PlayerPrefs.GetString("MyPlayerName", "Survivor");
        SpawnCharacter(player, characterID, playerName, position);
        return true;
    }

    /// <summary>Captures the authoritative avatar state before its death-transform despawns it.</summary>
    public void CaptureMilitaryRespawnState(PlayerRef player)
    {
        if (!Runner.IsServer || player == PlayerRef.None) return;
        if (!spawnedPlayers.TryGetValue(player, out NetworkObject playerObject) ||
            playerObject == null || !playerObject.IsValid)
            return;

        InventorySystem inventory = playerObject.GetComponent<InventorySystem>();
        if (inventory != null)
            militaryInventoryByPlayer[player] = inventory.CaptureMilitaryRespawnSnapshot();

        PlayerCombat combat = playerObject.GetComponent<PlayerCombat>();
        if (combat != null)
            militaryCombatByPlayer[player] = combat.CaptureMilitaryRespawnCombatSnapshot();
    }

    /// <summary>Persists the Solo loadout exactly as it was when Route B committed.</summary>
    public bool CaptureSoloMilitaryCheckpoint(PlayerRef player)
    {
        if (!Runner.IsServer || player == PlayerRef.None ||
            !spawnedPlayers.TryGetValue(player, out NetworkObject playerObject) ||
            playerObject == null || !playerObject.IsValid)
            return false;

        InventorySystem inventory = playerObject.GetComponent<InventorySystem>();
        PlayerCombat combat = playerObject.GetComponent<PlayerCombat>();
        if (inventory == null || combat == null) return false;
        soloMilitaryCheckpointInventory[player] = inventory.CaptureMilitaryRespawnSnapshot();
        soloMilitaryCheckpointCombat[player] = combat.CaptureMilitaryRespawnCombatSnapshot();
        return true;
    }

    /// <summary>Copies the persistent checkpoint into the one-shot spawn restore queues.</summary>
    public bool PrepareSoloMilitaryCheckpointRespawn(PlayerRef player)
    {
        if (!Runner.IsServer ||
            !soloMilitaryCheckpointInventory.TryGetValue(player, out InventorySystem.MilitaryRespawnSnapshot inventory) ||
            !soloMilitaryCheckpointCombat.TryGetValue(player, out PlayerCombat.MilitaryRespawnCombatSnapshot combat))
            return false;
        militaryInventoryByPlayer[player] = inventory;
        militaryCombatByPlayer[player] = combat;
        return true;
    }

    public bool TryTakeMilitaryInventorySnapshot(PlayerRef player,
        out InventorySystem.MilitaryRespawnSnapshot snapshot)
    {
        if (militaryInventoryByPlayer.TryGetValue(player, out snapshot))
        {
            militaryInventoryByPlayer.Remove(player);
            return true;
        }
        snapshot = null;
        return false;
    }

    public bool TryTakeMilitaryCombatSnapshot(PlayerRef player,
        out PlayerCombat.MilitaryRespawnCombatSnapshot snapshot)
    {
        if (militaryCombatByPlayer.TryGetValue(player, out snapshot))
        {
            militaryCombatByPlayer.Remove(player);
            return true;
        }
        snapshot = default;
        return false;
    }

    private void SpawnCharacter(PlayerRef player, int characterID, string playerName,
        Vector2? forcedPosition = null)
    {
        if (spawnedPlayers.ContainsKey(player) && spawnedPlayers[player] != null)
        {
            Debug.LogWarning($"[SPAWNER] Người chơi {player} ({playerName}) đã có nhân vật! Bỏ qua đẻ trùng.");
            return;
        }
        if (characterID < 0 || characterID >= playerPrefabs.Length) characterID = 0;

        Vector3 safeSpawnPos = Vector3.zero;
        Quaternion safeSpawnRot = Quaternion.identity;

        // The first avatar created in Main is the survivor arriving from Intro,
        // so place it beside the broken car. Only death respawns are allowed to
        // use the scene's four random spawn points.
        bool isInitialArrival = !playersGivenArrivalSpawn.Contains(player);
        if (forcedPosition.HasValue)
        {
            safeSpawnPos = forcedPosition.Value;
        }
        else
        {
            bool useArrivalSpawn = isInitialArrival &&
                MainArrivalStoryBootstrap.TryGetInitialSpawnPose(playersGivenArrivalSpawn.Count,
                    out safeSpawnPos, out safeSpawnRot);

            if (!useArrivalSpawn && spawnPoints != null && spawnPoints.Length > 0)
            {
                int randomIndex = Random.Range(0, spawnPoints.Length);
                safeSpawnPos = spawnPoints[randomIndex].position;
                safeSpawnRot = spawnPoints[randomIndex].rotation;
            }
        }

        NetworkObject netObj = Runner.Spawn(playerPrefabs[characterID], safeSpawnPos, safeSpawnRot, player);

        // 🔥 FIX LỖI 2: Dùng chép đè để tránh Crash nếu người chơi gửi lệnh đẻ 2 lần do lag
        spawnedPlayers[player] = netObj;
        characterIdByPlayer[player] = characterID;
        playerNameByPlayer[player] = playerName;
        string persistenceKey = GetPersistenceKey(player, playerName);
        persistenceKeyByPlayer[player] = persistenceKey;
        if (woundSnapshotByUser.TryGetValue(persistenceKey, out PlayerHealth.WoundSnapshot woundSnapshot))
        {
            PlayerHealth spawnedHealth = netObj.GetComponent<PlayerHealth>();
            if (spawnedHealth != null)
                spawnedHealth.RestoreWoundSnapshot(woundSnapshot);
        }
        if (isInitialArrival) playersGivenArrivalSpawn.Add(player);
        Runner.SetPlayerObject(player, netObj);

        // 🔥 LOGIC LATE JOIN (NGƯỜI CHƠI NHẢY DÙ VÀO SAU)
        if (IsMatchStarted)
        {
            RPC_AnnounceLateJoin(playerName); // Báo tin lên Chat
            RPC_PlayBlinkEffect(netObj);      // Cho bất tử chớp nháy 3 giây
        }
    }

    private static bool TryResolveStoryRespawnPosition(out Vector2 position)
    {
        position = default;
        MainQuestManager quest = MainQuestManager.Instance;
        if (quest == null || !quest.IsNetworkReady) return false;

        string checkpointName = quest.IsHospitalRadioRecoveredState
            ? "Save-Respawn 2"
            : quest.HasMapFragment1 ? "Save-Respawn" : string.Empty;
        if (string.IsNullOrEmpty(checkpointName)) return false;

        GameObject checkpoint = GameObject.Find(checkpointName);
        if (checkpoint == null)
        {
            Debug.LogError($"[SPAWNER] Tuyến nhiệm vụ đã mở nhưng Main.unity thiếu '{checkpointName}'.");
            return false;
        }

        position = checkpoint.transform.position;
        Debug.Log($"[SPAWNER] Respawn theo tiến độ nhiệm vụ tại '{checkpointName}' {position}.");
        return true;
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

    public string GetPlayerName(PlayerRef player)
    {
        if (playerNameByPlayer.TryGetValue(player, out string cachedName) &&
            !string.IsNullOrWhiteSpace(cachedName))
            return cachedName;

        if (spawnedPlayers.TryGetValue(player, out NetworkObject playerObject) && playerObject != null)
        {
            PlayerNameTag nameTag = playerObject.GetComponent<PlayerNameTag>();
            if (nameTag != null && !string.IsNullOrWhiteSpace(nameTag.PlayerName.ToString()))
                return nameTag.PlayerName.ToString();
        }

        return "Survivor";
    }

    // ========================================================
    // 🔥 HỆ THỐNG ĐIỂM DANH & ĐỒNG BỘ LOADING (CHỐT CHẶN 95%)
    // ========================================================

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_PlayerFinishedLoadingMap(PlayerRef playerRef, RpcInfo info = default)
    {
        if (!Runner.IsServer) return;
        if (!TryResolveAuthenticatedPlayer(playerRef, info.Source, out PlayerRef authoritativePlayer))
        {
            Debug.LogWarning($"[SPAWNER] Rejected spoofed request from {info.Source} for {playerRef}.");
            return;
        }

        // Nhánh 1: Nếu game đã bắt đầu từ lâu, đây là người đi trễ (Nhảy dù)
        if (IsMatchStarted)
        {
            RPC_OpenEyesForLateJoiner(authoritativePlayer); // Gọi riêng nó mở mắt lập tức
            return;
        }

        // Nhánh 2: Game chưa bắt đầu, đang ở đoạn Đồng Bộ Đầu Trận
        if (RegisterReadyPlayer(playersLoadedSet, authoritativePlayer))
            CheckAndStartGame();
    }

    public static bool RegisterReadyPlayer(ISet<PlayerRef> readyPlayers, PlayerRef player)
    {
        return readyPlayers != null && player != PlayerRef.None && readyPlayers.Add(player);
    }

    private string GetPersistenceKey(PlayerRef player, string fallbackName)
    {
        string userId = Runner != null ? Runner.GetPlayerUserId(player) : string.Empty;
        if (!string.IsNullOrWhiteSpace(userId)) return "fusion:" + userId;
        return "name:" + (string.IsNullOrWhiteSpace(fallbackName) ? player.ToString() : fallbackName.Trim());
    }

    private void CheckAndStartGame()
    {
        if (!Runner.IsServer || IsMatchStarted) return;

        int currentPlayersInRoom = Runner.SessionInfo.PlayerCount;
        Debug.Log($"[ĐIỂM DANH] Đã có {playersLoadedSet.Count}/{currentPlayersInRoom} người tải xong Map.");

        // NẾU TẤT CẢ ĐÃ TẢI XONG -> PHÁT LỆNH GO!!!
        if (playersLoadedSet.Count >= currentPlayersInRoom && playersLoadedSet.Count > 0)
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
                PlayerHealth health = netObj != null ? netObj.GetComponent<PlayerHealth>() : null;
                if (health != null && persistenceKeyByPlayer.TryGetValue(player, out string persistenceKey))
                {
                    PlayerHealth.WoundSnapshot snapshot = health.CaptureWoundSnapshot();
                    if (snapshot != null) woundSnapshotByUser[persistenceKey] = snapshot;
                }
                Runner.Despawn(netObj);
                spawnedPlayers.Remove(player);
            }
            startingWeaponByPlayer.Remove(player);
            playersGivenArrivalSpawn.Remove(player);
            characterIdByPlayer.Remove(player);
            playerNameByPlayer.Remove(player);
            persistenceKeyByPlayer.Remove(player);
            militaryInventoryByPlayer.Remove(player);
            militaryCombatByPlayer.Remove(player);
            soloMilitaryCheckpointInventory.Remove(player);
            soloMilitaryCheckpointCombat.Remove(player);
            playersLoadedSet.Remove(player);

            // 🔥 FIX LỖI 1: Kẹt Loading. Nếu có đứa rớt mạng lúc đang ở sảnh chờ load, tự động check và cho những người còn lại vào game!
            if (!IsMatchStarted)
            {
                CheckAndStartGame();
            }
        }
    }
}
