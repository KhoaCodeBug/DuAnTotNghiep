using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using Photon.Voice.Unity;
using UnityEngine;

public class PlayerInputHandler2D : NetworkBehaviour, INetworkRunnerCallbacks
{
    [Header("--- UI VOICE ---")]
    public GameObject voiceIcon;

    [Networked]
    public NetworkBool IsSpeaking { get; set; }

    [Header("--- HỆ THỐNG VOICE CHAT ---")]
    private Recorder globalRecorder;

    // 🔥 THÊM: Bộ đếm thời gian để chống spam gửi lệnh lên Server liên tục
    private float nextVoiceNoiseTime = 0f;
    // 🔥 THÊM BIẾN NÀY DƯỚI CHỖ globalRecorder:
    private float currentVoiceRadius = 0f;

    public override void Spawned()
    {
        if (HasInputAuthority)
        {
            Runner.AddCallbacks(this);
            globalRecorder = FindFirstObjectByType<Recorder>();
            if (globalRecorder != null) globalRecorder.TransmitEnabled = false;

            if (AutoChatManager.Instance != null)
                AutoChatManager.Instance.onSendMessage += HandleSendMessage;
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (HasInputAuthority)
        {
            runner.RemoveCallbacks(this);
            if (AutoChatManager.Instance != null)
                AutoChatManager.Instance.onSendMessage -= HandleSendMessage;
        }
    }

    void Update()
    {
        if (voiceIcon != null)
        {
            voiceIcon.SetActive(IsSpeaking);
        }

        if (HasInputAuthority == false || globalRecorder == null) return;

        // BẬT / TẮT MIC
        if (Input.GetKeyDown(KeyCode.V))
        {
            globalRecorder.TransmitEnabled = true;
            IsSpeaking = true;
            Debug.Log("🎙️ [Fragments of Survival] Đang phát sóng...");
        }
        else if (Input.GetKeyUp(KeyCode.V))
        {
            globalRecorder.TransmitEnabled = false;
            IsSpeaking = false;
            Debug.Log("🔇 [Fragments of Survival] Đã ngắt liên lạc.");
        }

        // ==========================================
        // 🔥 ĐO ÂM LƯỢNG VÀ TẠO TIẾNG ỒN DỤ ZOMBIE (ĐÃ FIX HỆ SỐ THỰC TẾ)
        // ==========================================
        if (IsSpeaking && globalRecorder.LevelMeter != null)
        {
            float voiceVolume = globalRecorder.LevelMeter.CurrentPeakAmp;

            // 🔥 1. Hạ ngưỡng lọc tạp âm xuống cực thấp (0.01) vì Mic thường thu âm lượng rất nhỏ
            if (voiceVolume > 0.01f)
            {
                // 🔥 2. TĂNG HỆ SỐ NHÂN LÊN 50! 
                // Ví dụ: Bạn nói nhỏ (0.05) * 50 = 2.5 mét. La to (0.16) * 50 = 8 mét.
                float noiseRadius = voiceVolume * 80f;

                // Ép giới hạn tối đa là 8m (Theo đúng ý bạn)
                noiseRadius = Mathf.Clamp(noiseRadius, 0f, 10f);

                currentVoiceRadius = noiseRadius; // Lưu lại để vẽ Gizmos

                if (Time.time >= nextVoiceNoiseTime)
                {
                    // 🔥 In thẳng ra Console để đạo diễn thấy thực tế Mic mình đang kêu to bao nhiêu mét!
                    Debug.Log($"[TEST MIC] Âm lượng thật: {voiceVolume:F3} | Bán kính gọi Zombie: {noiseRadius:F1} mét");

                    RPC_MakeVoiceNoise(noiseRadius);
                    nextVoiceNoiseTime = Time.time + 0.05f;
                }
            }
            else
            {
                currentVoiceRadius = 0f;
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

        data.moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;

        if (Camera.main != null)
        {
            Vector3 mousePos = Input.mousePosition;
            mousePos.z = Mathf.Abs(Camera.main.transform.position.z);
            data.mouseWorldPos = Camera.main.ScreenToWorldPoint(mousePos);
        }

        bool isTyping = AutoChatManager.Instance != null && AutoChatManager.Instance.IsTyping();
        bool isInvOpen = AutoUIManager.Instance != null && AutoUIManager.Instance.IsInventoryOpen();

        // 🔥 THÊM CỜ KIỂM TRA BẢNG MÁU
        bool isHealthOpen = AutoHealthPanel.Instance != null && AutoHealthPanel.Instance.IsOpen;

        bool isDead = false;
        PlayerHealth health = GetComponent<PlayerHealth>();
        if (health != null)
        {
            isDead = health.currentHealth <= 0;
        }

        // 🔥 CHẶN DI CHUYỂN NẾU MỞ BẤT KỲ UI NÀO
        if (isTyping || isInvOpen || isHealthOpen || isDead)
        {
            input.Set(new PlayerNetworkInput());
            return;
        }

        // 🔥 CHẶN NGẮM BẮN (AIM) NẾU CHỈ CHUỘT LÊN BẤT KỲ UI NÀO
        bool pointerOnUI = UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

        data.isAiming = pointerOnUI ? false : Input.GetMouseButton(1);
        data.isRunning = Input.GetKey(KeyCode.LeftShift);
        data.isCrouching = Input.GetKey(KeyCode.C);

        data.isShooting = Input.GetMouseButton(0);
        data.isBashing = Input.GetKey(KeyCode.Space);

        input.Set(data);
    }

    private void HandleSendMessage(string msg)
    {
        Rpc_SendChat("Vô Danh", msg);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    public void Rpc_SendChat(string playerName, string message)
    {
        if (AutoChatManager.Instance != null)
        {
            AutoChatManager.Instance.AddMessage(playerName, message);
        }
    }

    // ==========================================
    // 🔥 CÁI ĐIỆN THOẠI ĐỂ MÁY KHÁCH GỌI MÁY CHỦ BÁO ĐỘNG
    // ==========================================
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_MakeVoiceNoise(float radius)
    {
        // Khi Máy Chủ nghe máy, nó sẽ tự động thò tay lấy hàm MakeNoise ra để vẽ vòng tròn
        PlayerMovement moveScript = GetComponent<PlayerMovement>();
        if (moveScript != null)
        {
            moveScript.MakeNoise(radius);
        }
    }

    // ==========================================
    // 🔥 VẼ VÒNG TRÒN ÂM THANH RA SCENE ĐỂ TEST
    // ==========================================
    private void OnDrawGizmos()
    {
        // Chỉ vẽ khi có âm lượng phát ra
        if (currentVoiceRadius > 0f)
        {
            Gizmos.color = Color.cyan; // Màu xanh lơ cho khác với súng/chạy bộ
            Gizmos.DrawWireSphere(transform.position, currentVoiceRadius);
        }
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