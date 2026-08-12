using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Network-authoritative spine for the Main scene story.  Attach this to an
/// existing scene NetworkObject (Day_Night_System is a suitable host), rather
/// than putting it on an ordinary scene GameObject.
/// </summary>
public sealed class MainQuestManager : NetworkBehaviour
{
    public enum QuestStage
    {
        NotStarted,
        FindCityMap,
        CityMapFound
    }

    public static MainQuestManager Instance { get; private set; }

    [Header("Map reward")]
    [Tooltip("Optional point in KhuVuc3. Every client pans its own camera here when the city map is found.")]
    [SerializeField] private Transform khuVuc3Focus;
    [SerializeField, Min(0.1f)] private float focusTravelSeconds = 1.25f;
    [SerializeField, Min(0f)] private float focusHoldSeconds = 1.8f;
    [SerializeField, Min(0.1f)] private float focusReturnSeconds = 1.0f;

    [Header("Quest HUD")]
    [SerializeField] private bool showBuiltInQuestHud = true;

    [Networked] public int NetworkQuestStage { get; set; }
    [Networked] public int MapCabinetId { get; set; }
    [Networked] public NetworkBool IsCityMapUnlocked { get; set; }

    private MapController cachedMapController;
    private Coroutine focusRoutine;

    public QuestStage CurrentStage => (QuestStage)NetworkQuestStage;
    public bool IsMapSearchActive => CurrentStage == QuestStage.FindCityMap;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(this);
    }

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            NetworkQuestStage = (int)QuestStage.NotStarted;
            MapCabinetId = 0;
            IsCityMapUnlocked = false;
        }
    }

    private void Update()
    {
        ApplyMapAccess();
    }

    /// <summary>Called by MainQuestStartTrigger after a local player reaches KhuVucBatDau.</summary>
    public void RequestStartMapSearch(int triggerId)
    {
        if (HasStateAuthority) ServerStartMapSearch(triggerId, Runner.LocalPlayer);
        else RPC_RequestStartMapSearch(triggerId);
    }

    /// <summary>Called by the closest highlighted office cabinet when E is pressed.</summary>
    public void RequestSearchCabinet(int cabinetId)
    {
        if (HasStateAuthority) ServerSearchCabinet(cabinetId, Runner.LocalPlayer);
        else RPC_RequestSearchCabinet(cabinetId);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestStartMapSearch(int triggerId, RpcInfo info = default)
    {
        ServerStartMapSearch(triggerId, info.Source);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestSearchCabinet(int cabinetId, RpcInfo info = default)
    {
        ServerSearchCabinet(cabinetId, info.Source);
    }

    private void ServerStartMapSearch(int triggerId, PlayerRef requester)
    {
        if (!HasStateAuthority || CurrentStage != QuestStage.NotStarted) return;
        if (!MainQuestStartTrigger.TryGet(triggerId, out MainQuestStartTrigger trigger) || trigger == null) return;
        if (!TryGetRequestingPlayer(requester, out PlayerMovement player) || !trigger.Contains(player.transform.position)) return;

        MainQuestSearchCabinet[] allCabinets = FindObjectsByType<MainQuestSearchCabinet>(FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        List<MainQuestSearchCabinet> validCabinets = new List<MainQuestSearchCabinet>(allCabinets.Length);
        for (int i = 0; i < allCabinets.Length; i++)
        {
            if (allCabinets[i] != null && allCabinets[i].CabinetId != 0)
                validCabinets.Add(allCabinets[i]);
        }

        if (validCabinets.Count == 0)
        {
            Debug.LogError("[MAIN QUEST] Không có MainQuestSearchCabinet nào để random bản đồ.");
            RPC_ShowQuestMessage("Chưa thể bắt đầu: văn phòng chưa có tủ nhiệm vụ.");
            return;
        }

        MapCabinetId = validCabinets[Random.Range(0, validCabinets.Count)].CabinetId;
        NetworkQuestStage = (int)QuestStage.FindCityMap;
        RPC_ShowQuestMessage("MỤC TIÊU MỚI: Tìm bản đồ thành phố. Kiểm tra các tủ văn phòng được đánh dấu vàng.");
    }

    private void ServerSearchCabinet(int cabinetId, PlayerRef requester)
    {
        if (!HasStateAuthority || !IsMapSearchActive) return;
        if (!MainQuestSearchCabinet.TryGet(cabinetId, out MainQuestSearchCabinet cabinet) || cabinet == null) return;
        if (!TryGetRequestingPlayer(requester, out PlayerMovement player) || !cabinet.CanPlayerSearch(player.transform.position)) return;

        if (cabinetId != MapCabinetId)
        {
            RPC_ShowQuestMessage("Không có bản đồ trong tủ này. Hãy kiểm tra tủ khác.");
            return;
        }

        IsCityMapUnlocked = true;
        MapCabinetId = 0;
        NetworkQuestStage = (int)QuestStage.CityMapFound;
        RPC_ShowQuestMessage("ĐÃ TÌM THẤY BẢN ĐỒ! Khu quân sự đã được đánh dấu.");
        RPC_PlayKhuVuc3Focus();
    }

    private static bool TryGetRequestingPlayer(PlayerRef requester, out PlayerMovement player)
    {
        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] == null || players[i].Object == null || !players[i].Object.IsValid) continue;
            if (players[i].Object.InputAuthority != requester) continue;
            player = players[i];
            return true;
        }

        player = null;
        return false;
    }

    private void ApplyMapAccess()
    {
        if (cachedMapController == null)
            cachedMapController = FindFirstObjectByType<MapController>();
        if (cachedMapController == null) return;

        if (IsCityMapUnlocked)
        {
            if (!cachedMapController.enabled) cachedMapController.enabled = true;
            return;
        }

        cachedMapController.enabled = false;
        if (cachedMapController.mapUI != null) cachedMapController.mapUI.SetActive(false);
        if (cachedMapController.playerIcon != null) cachedMapController.playerIcon.SetActive(false);
        if (cachedMapController.markers == null) return;
        for (int i = 0; i < cachedMapController.markers.Length; i++)
            if (cachedMapController.markers[i] != null) cachedMapController.markers[i].SetActive(false);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowQuestMessage(string message)
    {
        AutoChatManager.Instance?.AddMessage("NHIỆM VỤ", message);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayKhuVuc3Focus()
    {
        if (khuVuc3Focus == null || PZ_CameraController.Instance == null) return;
        if (focusRoutine != null) StopCoroutine(focusRoutine);
        focusRoutine = StartCoroutine(FocusKhuVuc3Routine());
    }

    private IEnumerator FocusKhuVuc3Routine()
    {
        PZ_CameraController cameraController = PZ_CameraController.Instance;
        Transform localPlayer = PlayerMovement.LocalPlayerInstance != null
            ? PlayerMovement.LocalPlayerInstance.transform
            : null;
        if (cameraController == null || localPlayer == null || khuVuc3Focus == null) yield break;

        cameraController.enabled = false;
        Vector3 from = cameraController.transform.position;
        Vector3 to = khuVuc3Focus.position + cameraController.offset;
        for (float elapsed = 0f; elapsed < focusTravelSeconds; elapsed += Time.unscaledDeltaTime)
        {
            cameraController.transform.position = Vector3.Lerp(from, to, Mathf.Clamp01(elapsed / focusTravelSeconds));
            yield return null;
        }

        cameraController.transform.position = to;
        yield return new WaitForSecondsRealtime(focusHoldSeconds);

        from = cameraController.transform.position;
        to = localPlayer.position + cameraController.offset;
        for (float elapsed = 0f; elapsed < focusReturnSeconds; elapsed += Time.unscaledDeltaTime)
        {
            cameraController.transform.position = Vector3.Lerp(from, to, Mathf.Clamp01(elapsed / focusReturnSeconds));
            yield return null;
        }

        cameraController.transform.position = to;
        cameraController.enabled = true;
        focusRoutine = null;
    }

    private void OnGUI()
    {
        if (!showBuiltInQuestHud || TutorialSession.IsActive) return;

        string objective = CurrentStage switch
        {
            QuestStage.FindCityMap => "NHIỆM VỤ: Tìm bản đồ thành phố trong văn phòng.",
            QuestStage.CityMapFound => "NHIỆM VỤ: Đến khu quân sự được đánh dấu trên bản đồ.",
            _ => string.Empty
        };
        if (string.IsNullOrEmpty(objective)) return;

        GUIStyle style = new GUIStyle(GUI.skin.box)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        GUI.Box(new Rect(Screen.width * 0.5f - 260f, 24f, 520f, 38f), objective, style);
    }
}
