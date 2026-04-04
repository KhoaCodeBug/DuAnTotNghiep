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

    private float nextVoiceNoiseTime = 0f;
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

        if (IsSpeaking && globalRecorder.LevelMeter != null)
        {
            float voiceVolume = globalRecorder.LevelMeter.CurrentPeakAmp;

            if (voiceVolume > 0.01f)
            {
                float noiseRadius = voiceVolume * 80f;
                noiseRadius = Mathf.Clamp(noiseRadius, 0f, 10f);

                currentVoiceRadius = noiseRadius;

                if (Time.time >= nextVoiceNoiseTime)
                {
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

        bool isTyping = AutoChatManager.Instance != null && AutoChatManager.Instance.IsTyping();
        bool isInvOpen = AutoUIManager.Instance != null && AutoUIManager.Instance.IsInventoryOpen();
        bool isHealthOpen = AutoHealthPanel.Instance != null && AutoHealthPanel.Instance.IsOpen;

        bool isDead = false;
        PlayerHealth health = GetComponent<PlayerHealth>();
        if (health != null)
        {
            isDead = health.currentHealth <= 0;
        }

        // 🔥 CHẶN TẤT CẢ INPUT NẾU ĐANG MỞ UI HOẶC ĐÃ CHẾT
        if (isTyping || isInvOpen || isHealthOpen || isDead)
        {
            input.Set(new PlayerNetworkInput());
            return;
        }

        // ===============================================
        // 🔥 SONG KIẾM HỢP BÍCH: KIỂM TRA MÁY ĐANG CHẠY
        // ===============================================
        // ĐÃ SỬA: Ép nó nhận Joystick luôn cả khi sếp đang ngồi test trên Unity máy tính
        if ((Application.isMobilePlatform || Application.isEditor) && MobileInputController.Instance != null && MobileInputController.Instance.gameObject.activeInHierarchy)
        {
            var mobileUI = MobileInputController.Instance;

            // 1. Di chuyển bằng Joystick trái
            data.moveInput = mobileUI.moveJoystick.Direction;

            // 2. Chạy bộ: Kéo Joystick đi chuyển ra xa (> 0.7) thì tự động chạy
            data.isRunning = data.moveInput.magnitude > 0.7f;
            data.isCrouching = false; // Điện thoại tạm thời chưa có nút ngồi

            // 3. Ngắm & Bắn bằng Joystick phải (Twin-stick shooter)
            if (mobileUI.aimJoystick.Direction.magnitude > 0.1f)
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

            // 4. Cận chiến: Nhận lệnh từ nút Bash trên màn hình
            data.isBashing = mobileUI.isBashPressed;
        }
        else
        {
            // ===============================================
            // 🔥 CHẾ ĐỘ PC: CHUỘT VÀ BÀN PHÍM (GIỮ NGUYÊN GỐC)
            // ===============================================
            data.moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;

            if (Camera.main != null)
            {
                Vector3 mousePos = Input.mousePosition;
                mousePos.z = Mathf.Abs(Camera.main.transform.position.z);
                data.mouseWorldPos = Camera.main.ScreenToWorldPoint(mousePos);
            }

            bool pointerOnUI = UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

            data.isAiming = pointerOnUI ? false : Input.GetMouseButton(1);
            data.isRunning = Input.GetKey(KeyCode.LeftShift);
            data.isCrouching = Input.GetKey(KeyCode.C);

            data.isShooting = Input.GetMouseButton(0);
            data.isBashing = Input.GetKey(KeyCode.Space);
        }

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