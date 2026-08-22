using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using Photon.Voice.Unity;
using Photon.Voice.Fusion;
using UnityEngine;

public class PlayerInputHandler2D : NetworkBehaviour, INetworkRunnerCallbacks
{
    [Header("--- UI VOICE ---")]
    public GameObject voiceIcon;

    [Networked]
    public NetworkBool IsSpeaking { get; set; }

    [Header("--- HỆ THỐNG VOICE CHAT ---")]
    private Recorder globalRecorder;
    private bool isChatSubscribed = false;

    private float nextVoiceNoiseTime = 0f;
    private float currentVoiceRadius = 0f;
    private float nextVoiceDiagTime = 0f;
    private bool pushToTalkHeld = false;
    private float pushToTalkStartedAt = -1f;
    private bool recoveryAttempted = false;
    [Header("--- NOISE METER ---")]
    [SerializeField] private float uiVoiceHearDistance = 12f;

    private void SetPushToTalk(bool enabled)
    {
        if (!HasInputAuthority) return;

        pushToTalkHeld = enabled;
        if (enabled)
        {
            pushToTalkStartedAt = Time.time;
            recoveryAttempted = false;
        }

        IsSpeaking = enabled;
        RPC_SetSpeaking(enabled);

        if (globalRecorder != null)
        {
            globalRecorder.RecordingEnabled = true;
            globalRecorder.TransmitEnabled = enabled;
        }
    }

    private void UpdatePushToTalk()
    {
        bool shouldTransmit = Input.GetKey(KeyCode.V);

        if (shouldTransmit != pushToTalkHeld)
        {
            SetPushToTalk(shouldTransmit);
            Debug.Log(shouldTransmit
                ? "🎙️ [VOICE] Push-to-talk enabled."
                : "🔇 [VOICE] Push-to-talk disabled.");
        }

        // Photon Voice có thể tắt TransmitEnabled trong lúc reconnect hoặc đổi
        // scene. Khi V vẫn được giữ, luôn khôi phục lại cờ này trên chính stream
        // cục bộ thay vì để biểu tượng loa và trạng thái gửi bị lệch nhau.
        if (pushToTalkHeld && globalRecorder != null)
        {
            globalRecorder.RecordingEnabled = true;
            globalRecorder.TransmitEnabled = true;

            // Chỉ thử khởi tạo lại đúng một lần nếu đã vào room nhưng sau 2 giây
            // vẫn chưa có frame nào được gửi. Không restart ngay lúc nhấn V.
            if (!recoveryAttempted && Time.time - pushToTalkStartedAt >= 2f &&
                !globalRecorder.IsCurrentlyTransmitting)
            {
                recoveryAttempted = true;
                globalRecorder.RestartRecording();
                Debug.LogWarning("[VOICE] Recorder chưa gửi frame sau 2 giây; đang khởi tạo lại microphone một lần.");
            }
        }
    }

    public override void Spawned()
    {
        if (HasInputAuthority)
        {
            Runner.AddCallbacks(this);

            FindVoiceRecorder();

            if (AutoChatManager.Instance != null)
            {
                AutoChatManager.Instance.onSendMessage -= HandleSendMessage;
                AutoChatManager.Instance.onSendMessage += HandleSendMessage;
                isChatSubscribed = true;
                Debug.Log($"[CHAT] ✅ Đã đăng ký nhận sự kiện gửi chat thành công lúc Spawned");
            }
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (HasInputAuthority)
        {
            runner.RemoveCallbacks(this);
            // 🔥 FIX: Luôn reset flag, vì Instance có thể đã bị Destroy trước
            if (isChatSubscribed)
            {
                if (AutoChatManager.Instance != null)
                {
                    AutoChatManager.Instance.onSendMessage -= HandleSendMessage;
                }
                isChatSubscribed = false; // Luôn reset để lần sau đăng ký lại
            }
        }
    }

    private void FindVoiceRecorder()
    {
        if (globalRecorder != null) return;

        // 1. Ưu tiên tìm qua VoiceNetworkObject gắn trên chính player
        var voiceNetObj = GetComponent<VoiceNetworkObject>();
        if (voiceNetObj != null && voiceNetObj.RecorderInUse != null)
        {
            globalRecorder = voiceNetObj.RecorderInUse;
            Debug.Log($"[VOICE] ✅ Tìm thấy Recorder từ VoiceNetworkObject gắn trên nhân vật");
            return;
        }

        // 2. Fallback: tìm trong các object con
        globalRecorder = GetComponentInChildren<Recorder>();

        if (globalRecorder == null && Runner != null)
        {
            var voiceClient = Runner.GetComponent<FusionVoiceClient>();
            if (voiceClient != null)
            {
                globalRecorder = voiceClient.PrimaryRecorder;
                if (globalRecorder != null)
                {
                    Debug.Log($"[VOICE] ✅ Đã tìm thấy Recorder từ FusionVoiceClient trên Runner");
                }
            }
        }

        if (globalRecorder == null)
        {
            globalRecorder = FindAnyObjectByType<Recorder>();
            if (globalRecorder != null)
                Debug.Log($"[VOICE] ⚠️ Dùng Recorder tìm bừa trong Scene (fallback)");
        }

        if (globalRecorder != null)
        {
            globalRecorder.MicrophoneType = Recorder.MicType.Unity;
            globalRecorder.UseMicrophoneTypeFallback = true;
            globalRecorder.RecordingEnabled = true;
            globalRecorder.TransmitEnabled = false;
            globalRecorder.UserData = Object.Id;
            Debug.Log($"[VOICE] ✅ Recorder sẵn sàng! TransmitEnabled = false (chờ bấm V)");
        }
    }

    void Update()
    {
        if (voiceIcon != null)
        {
            voiceIcon.SetActive(IsSpeaking);
        }

        if (HasInputAuthority == false)
        {
            // Voice từ người khác không làm người chơi tự phát tiếng, chỉ nháy viền cyan để báo đang nghe thấy.
            PlayerMovement localPlayer = PlayerMovement.LocalPlayerInstance;
            if (IsSpeaking && localPlayer != null)
            {
                float distance = Vector2.Distance(transform.position, localPlayer.transform.position);
                if (distance <= uiVoiceHearDistance)
                    AutoNoiseMeter.ReportHeardVoice(1f - distance / uiVoiceHearDistance);
            }
            return;
        }

        // 🔥 ĐĂNG KÝ CHAT ĐỘNG NẾU TRƯỚC ĐÓ CHƯA ĐĂNG KÝ ĐƯỢC
        if (!isChatSubscribed && AutoChatManager.Instance != null)
        {
            AutoChatManager.Instance.onSendMessage -= HandleSendMessage;
            AutoChatManager.Instance.onSendMessage += HandleSendMessage;
            isChatSubscribed = true;
            Debug.Log($"[CHAT] ✅ Đã đăng ký nhận sự kiện gửi chat động thành công lúc Update");
        }

        // 🔥 TÌM MICROPHONE ĐỘNG NẾU TRƯỚC ĐÓ CHƯA TÌM THẤY
        if (globalRecorder == null)
        {
            FindVoiceRecorder();
        }

        UpdatePushToTalk();

        // Dùng cờ local thay vì NetworkBool IsSpeaking: client có InputAuthority nhận mic ngay,
        // không phải chờ StateAuthority gửi trạng thái về rồi meter mới phản hồi.
        if (pushToTalkHeld)
        {
            // 🔥 LOG CHẨN ĐOÁN HỆ THỐNG VOICE CHAT (1.5 giây một lần khi đang đè V - Chạy độc lập kể cả khi globalRecorder bị null)
            if (Time.time >= nextVoiceDiagTime)
            {
                var voiceClient = Runner.GetComponent<FusionVoiceClient>();
                string clientState = voiceClient != null ? voiceClient.ClientState.ToString() : "Not Found";
                string receiveState = voiceClient != null
                    ? $"Rx: {voiceClient.FramesReceivedPerSecond:F1} fps | Lost: {voiceClient.FramesLostPercent:F1}%"
                    : "Rx: unavailable";
                int micCount = Microphone.devices != null ? Microphone.devices.Length : 0;

                if (globalRecorder != null)
                {
                    float currentAmp = globalRecorder.LevelMeter != null ? globalRecorder.LevelMeter.CurrentPeakAmp : -1f;
                    bool isTransmitting = globalRecorder.IsCurrentlyTransmitting;
                    string micName = globalRecorder.MicrophoneDevice != null ? globalRecorder.MicrophoneDevice.ToString() : "Default/None";
                    string recorderState = $"RecordingEnabled: {globalRecorder.RecordingEnabled} | TransmitEnabled: {globalRecorder.TransmitEnabled}";

                    Debug.Log($"<color=#55ff55>[VOICE DIAGNOSTIC]</color> State: <b>{clientState}</b> | PeakAmp: <b>{currentAmp:F4}</b> | Transmitting: <b>{isTransmitting}</b> | {recorderState} | {receiveState} | MicCount: <b>{micCount}</b> | ActiveMic: <b>{micName}</b>");
                }
                else
                {
                    Debug.LogError($"<color=#ff5555>[VOICE DIAGNOSTIC]</color> State: <b>{clientState}</b> | <b>❌ ERROR: globalRecorder is NULL!</b> | MicCount: <b>{micCount}</b>");
                }
                nextVoiceDiagTime = Time.time + 1.5f;
            }

            if (globalRecorder != null && globalRecorder.LevelMeter != null)
            {
                float voiceVolume = globalRecorder.LevelMeter.CurrentPeakAmp;

                // Nhiều mic có peak nhỏ hơn 0.01; ngưỡng cũ làm voice hợp lệ bị coi là im lặng.
                if (voiceVolume > 0.001f)
                {
                    float noiseRadius = voiceVolume * 80f;
                    noiseRadius = Mathf.Clamp(noiseRadius, 0f, 10f);

                    currentVoiceRadius = noiseRadius;
                    AutoNoiseMeter.ReportTransientNoise(Mathf.Lerp(0.3f, 0.85f, noiseRadius / 10f), "VOICE");

                    if (Time.time >= nextVoiceNoiseTime)
                    {
                        // 🔥 GIẢM TẦN SUẤT RPC: Từ 20/giây → 2/giây để giảm lag mạng
                        RPC_MakeVoiceNoise(noiseRadius);
                        nextVoiceNoiseTime = Time.time + 0.5f;
                    }
                }
                else
                {
                    currentVoiceRadius = 0f;
                    // Mic đang mở nhưng peak chưa tới ngưỡng đo: vẫn báo trạng thái nhẹ, không báo zombie nghe thấy.
                    AutoNoiseMeter.ReportTransientNoise(0.12f, "MIC ĐANG MỞ");
                }
            }
            else
            {
                currentVoiceRadius = 0f;
                AutoNoiseMeter.ReportTransientNoise(0.12f, "MIC ĐANG MỞ");
            }
        }
        else
        {
            currentVoiceRadius = 0f;
        }
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        var data = new PlayerNetworkInput();

        bool isTyping = AutoChatManager.Instance != null && AutoChatManager.Instance.IsTyping();

        // 🔥 SỬ DỤNG CẦU DAO TỔNG Ở ĐÂY: Bao gồm Balo, Bảng Trade, Tủ Đồ Loot
        bool isUIMenuOpen = AutoUIManager.Instance != null && AutoUIManager.Instance.IsAnyMenuOpen();
        bool isQuestOverlayOpen = QuestFlowUIPrototype.Instance != null &&
                                  QuestFlowUIPrototype.Instance.IsQuestOverlayOpen;

        bool isHealthOpen = AutoHealthPanel.Instance != null && AutoHealthPanel.Instance.IsOpen;

        bool isDead = false;
        PlayerHealth health = GetComponent<PlayerHealth>();
        if (health != null)
        {
            isDead = health.currentHealth <= 0;
        }

        PlayerSurvival survival = GetComponent<PlayerSurvival>();
        bool isSleepLocked = survival != null && survival.IsSleepInputLocked;

        // 🔥 CHẶN TẤT CẢ INPUT NẾU ĐANG MỞ UI HOẶC ĐÃ CHẾT
        // Khi trả về 1 input rỗng, nhân vật sẽ đứng im, không bấm chuột phải bắn súng được luôn!
        if (isTyping || isUIMenuOpen || isQuestOverlayOpen || isHealthOpen || isDead || isSleepLocked ||
            VehicleRepairSkillCheckUI.BlocksGameplayInput ||
            MainQuestSearchCabinet.IsLocalSearchInProgress)
        {
            input.Set(new PlayerNetworkInput());
            return;
        }

        // ===============================================
        // 🔥 SONG KIẾM HỢP BÍCH: KIỂM TRA MÁY ĐANG CHẠY
        // ===============================================
        bool hasWeapon = HotbarHUDManager.Instance != null && HotbarHUDManager.Instance.HasGunEquipped();

        if ((Application.isMobilePlatform || Application.isEditor) && MobileInputController.Instance != null && MobileInputController.Instance.gameObject.activeInHierarchy)
        {
            var mobileUI = MobileInputController.Instance;

            // 1. Di chuyển bằng Joystick trái
            data.moveInput = mobileUI.moveJoystick.Direction;

            // 2. Chạy bộ: Kéo Joystick đi chuyển ra xa (> 0.7) thì tự động chạy
            data.isRunning = data.moveInput.magnitude > 0.7f;
            data.isCrouching = false; // Điện thoại tạm thời chưa có nút ngồi

            // 3. Ngắm & Bắn bằng Joystick phải (Twin-stick shooter) - CHỈ KHI CÓ SÚNG
            if (hasWeapon && mobileUI.aimJoystick.Direction.magnitude > 0.1f)
            {
                data.isAiming = true;
                data.isShooting = true; // Cứ kéo cần phải là xả đạn

                // Giả lập tọa độ chuột cách nhân vật 5 mét theo hướng vuốt Joystick
                Vector3 aimDir = new Vector3(mobileUI.aimJoystick.Direction.x, mobileUI.aimJoystick.Direction.y, 0);
                data.mouseWorldPos = transform.position + aimDir * 5f;
            }
            else
            {
                data.isAiming = false;
                data.isShooting = false;
                data.mouseWorldPos = transform.position;
            }

            // 4. Cận chiến: Nhận lệnh từ nút Bash trên màn hình - CHỈ KHI CÓ SÚNG
            data.isBashing = hasWeapon ? mobileUI.isBashPressed : false;
        }
        else
        {
            // ===============================================
            // 🔥 CHẾ ĐỘ PC: CHUỘT VÀ BÀN PHÍM
            // ===============================================
            data.moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;

            if (Camera.main != null)
            {
                Vector3 mousePos = Input.mousePosition;
                if (IsFinite(mousePos))
                {
                    mousePos.z = Mathf.Abs(Camera.main.transform.position.z);
                    Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(mousePos);
                    data.mouseWorldPos = IsFinite(mouseWorld) ? mouseWorld : transform.position;
                }
                else
                {
                    data.mouseWorldPos = transform.position;
                }
            }
            else data.mouseWorldPos = transform.position;

            bool pointerOnUI = UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

            // Phase-one tutorial teaches looking around before asking the
            // player to explicitly equip a gun. It still cannot shoot here.
            bool canAim = hasWeapon || TutorialSession.IsActive;
            data.isAiming = (pointerOnUI || !canAim) ? false : Input.GetMouseButton(1);
            data.isRunning = Input.GetKey(KeyCode.LeftShift);
            data.isCrouching = Input.GetKey(KeyCode.C);
            data.isVehicleBraking = Input.GetKey(KeyCode.Space);

            data.isShooting = hasWeapon ? Input.GetMouseButton(0) : false;
            data.isBashing = hasWeapon ? Input.GetKey(KeyCode.Space) : false;
        }

        input.Set(data);
    }

    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
               !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
               !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }

    private void HandleSendMessage(string msg)
    {
        PlayerHealth health = GetComponent<PlayerHealth>();
        if (health != null && health.isDead) return;

        string myPlayerName = PlayerPrefs.GetString("MyPlayerName", "Survivor");
        Rpc_SendChat(myPlayerName, msg);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    public void Rpc_SendChat(string playerName, string message)
    {
        if (AutoChatManager.Instance != null)
        {
            AutoChatManager.Instance.AddMessage(playerName, message);
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetSpeaking(NetworkBool speaking)
    {
        IsSpeaking = speaking;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_MakeVoiceNoise(float radius)
    {
        PlayerMovement moveScript = GetComponent<PlayerMovement>();
        if (moveScript != null)
        {
            moveScript.MakeNoise(radius);
        }
    }

    private void OnDrawGizmos()
    {
        if (currentVoiceRadius > 0f)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, currentVoiceRadius);
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) SetPushToTalk(false);
    }

    private void OnDisable()
    {
        SetPushToTalk(false);
    }

    #region Ẩn các hàm bắt buộc của INetworkRunnerCallbacks
    void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner) { }
    void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    void INetworkRunnerCallbacks.OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    void INetworkRunnerCallbacks.OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    void INetworkRunnerCallbacks.OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner) { }
    void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner runner) { }
    #endregion
}
