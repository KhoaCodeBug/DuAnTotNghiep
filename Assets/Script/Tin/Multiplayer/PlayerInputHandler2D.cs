using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using Photon.Voice.Unity;
using UnityEngine;

public class PlayerInputHandler2D : NetworkBehaviour, INetworkRunnerCallbacks
{
    [Header("--- HỆ THỐNG VOICE CHAT ---")]
    private Recorder globalRecorder;

    public override void Spawned()
    {
        if (HasInputAuthority)
        {
            Runner.AddCallbacks(this);

            // ==========================================
            // 🔥 ĐÃ FIX THEO CHUẨN MỚI CỦA UNITY (FindFirstObjectByType)
            // ==========================================
            globalRecorder = FindFirstObjectByType<Recorder>();

            if (globalRecorder != null)
            {
                globalRecorder.TransmitEnabled = false;
            }
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (HasInputAuthority)
            runner.RemoveCallbacks(this);
    }

    void Update()
    {
        if (HasInputAuthority == false || globalRecorder == null) return;

        if (Input.GetKeyDown(KeyCode.V))
        {
            globalRecorder.TransmitEnabled = true;
            Debug.Log("🎙️ [Fragments of Survival] Đang phát sóng...");
        }
        else if (Input.GetKeyUp(KeyCode.V))
        {
            globalRecorder.TransmitEnabled = false;
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

        bool isInvOpen = AutoUIManager.Instance != null && AutoUIManager.Instance.IsInventoryOpen();
        data.isAiming = isInvOpen ? false : Input.GetMouseButton(1);
        data.isRunning = Input.GetKey(KeyCode.LeftShift);
        data.isCrouching = Input.GetKey(KeyCode.C);

        data.isShooting = Input.GetMouseButton(0);
        data.isBashing = Input.GetKey(KeyCode.Space);

        input.Set(data);
    }

    #region Ẩn các hàm bắt buộc của INetworkRunnerCallbacks để tránh lỗi Unity
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