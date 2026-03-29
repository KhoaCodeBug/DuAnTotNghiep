using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using Photon.Voice.Unity;
using UnityEngine;

public class PlayerInputHandler2D : NetworkBehaviour, INetworkRunnerCallbacks
{
    [Header("--- UI VOICE ---")]
    public GameObject voiceIcon; // Chỉ cần đúng 1 cục này để bật/tắt

    [Networked]
    public NetworkBool IsSpeaking { get; set; }

    [Header("--- HỆ THỐNG VOICE CHAT ---")]
    private Recorder globalRecorder;

    public override void Spawned()
    {
        if (HasInputAuthority)
        {
            Runner.AddCallbacks(this);
            globalRecorder = FindFirstObjectByType<Recorder>();
            if (globalRecorder != null) globalRecorder.TransmitEnabled = false;

            // 🔥 MỚI: Đăng ký kết nối mạng cho khung chat
            if (AutoChatManager.Instance != null)
                AutoChatManager.Instance.onSendMessage += HandleSendMessage;
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (HasInputAuthority)
        {
            runner.RemoveCallbacks(this);
            // 🔥 MỚI: Ngắt kết nối chat khi chết hoặc thoát
            if (AutoChatManager.Instance != null)
                AutoChatManager.Instance.onSendMessage -= HandleSendMessage;
        }
    }

    void Update()
    {
        // 1. Tự động bật/tắt Icon cho TẤT CẢ mọi người dựa theo biến IsSpeaking
        if (voiceIcon != null)
        {
            voiceIcon.SetActive(IsSpeaking);
        }

        // 2. Chỉ máy của chính mình mới được gửi tín hiệu
        if (HasInputAuthority == false || globalRecorder == null) return;

        // BẤM V -> MỞ MIC & BẬT ICON
        if (Input.GetKeyDown(KeyCode.V))
        {
            globalRecorder.TransmitEnabled = true;
            IsSpeaking = true;
            Debug.Log("🎙️ [Fragments of Survival] Đang phát sóng...");
        }
        // BUÔNG V -> TẮT MIC & TẮT ICON
        else if (Input.GetKeyUp(KeyCode.V))
        {
            globalRecorder.TransmitEnabled = false;
            IsSpeaking = false;
            Debug.Log("🔇 [Fragments of Survival] Đã ngắt liên lạc.");
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
        // 🔥 MỚI: Khóa cứng nhân vật nếu đang gõ Chat
        bool isTyping = AutoChatManager.Instance != null && AutoChatManager.Instance.IsTyping();
        bool isInvOpen = AutoUIManager.Instance != null && AutoUIManager.Instance.IsInventoryOpen();

        if (isTyping || isInvOpen)
        {
            // Trả về Input rỗng (Đứng im, không bắn, không chém)
            input.Set(new PlayerNetworkInput());
            return;
        }

        data.isAiming = isInvOpen ? false : Input.GetMouseButton(1);
        data.isRunning = Input.GetKey(KeyCode.LeftShift);
        data.isCrouching = Input.GetKey(KeyCode.C);

        data.isShooting = Input.GetMouseButton(0);
        data.isBashing = Input.GetKey(KeyCode.Space);

        input.Set(data);
    }

    // ==========================================
    // 🔥 HỆ THỐNG GỬI CHAT QUA MẠNG
    // ==========================================
    private void HandleSendMessage(string msg)
    {
        // Lấy tên thật của nhân vật nếu có, hiện tại tạm để là "Vô Danh"
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