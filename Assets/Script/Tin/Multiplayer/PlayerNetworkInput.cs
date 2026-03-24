using Fusion;
using UnityEngine;

public struct PlayerNetworkInput : INetworkInput
{
    public Vector2 moveInput;
    public Vector2 mouseWorldPos;
    public NetworkBool isAiming;
    public NetworkBool isRunning;
    public NetworkBool isCrouching;

    // 🔥 MỚI: Thêm 2 nút phục vụ chiến đấu
    public NetworkBool isShooting;
    public NetworkBool isBashing;
}