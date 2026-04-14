using UnityEngine;
using Fusion;

[RequireComponent(typeof(Rigidbody2D))]
public class VehicleControllerFusion : NetworkBehaviour
{
    public float moveSpeed = 8f;

    public Transform driverSeat;
    public Transform passengerSeat;
    public Transform exitPoint;

    public Camera vehicleCamera;
    public Animator animator;

    [Networked] private NetworkObject Driver { get; set; }
    [Networked] private NetworkObject Passenger { get; set; }

    private Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0;
        rb.freezeRotation = true;

        if (vehicleCamera != null)
            vehicleCamera.gameObject.SetActive(false);
    }

    // ================= ENTER =================
    public void RequestEnter(NetworkObject player)
    {
        RPC_Enter(player);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    void RPC_Enter(NetworkObject player)
    {
        if (Driver == null)
        {
            Driver = player;
            Attach(player, driverSeat, true);
            return;
        }

        if (Passenger == null)
        {
            Passenger = player;
            Attach(player, passengerSeat, false);
        }
    }

    void Attach(NetworkObject player, Transform seat, bool isDriver)
    {
        // 🔥 Tắt NetworkTransform
        var net = player.GetComponent<NetworkTransform>();
        if (net != null) net.enabled = false;

        // 🔥 Tắt Rigidbody
        var rb = player.GetComponent<Rigidbody2D>();
        if (rb != null) rb.simulated = false;

        // 🔥 Đặt đúng vị trí ngay
        player.transform.position = seat.position;

        RPC_SetState(player, true, isDriver);
    }

    // ================= EXIT =================
    public void RequestExit(NetworkObject player)
    {
        RPC_Exit(player);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    void RPC_Exit(NetworkObject player)
    {
        if (Driver == player) Driver = null;
        else if (Passenger == player) Passenger = null;
        else return;

        Vector3 exitPos = exitPoint.position;

        var net = player.GetComponent<NetworkTransform>();
        var rb = player.GetComponent<Rigidbody2D>();

        // 🔥 Bật lại
        if (net != null) net.enabled = true;
        if (rb != null) rb.simulated = true;

        // 🔥 Teleport chuẩn
        if (net != null)
            net.Teleport(exitPos, Quaternion.identity);

        player.transform.position = exitPos;

        if (rb != null)
        {
            rb.position = exitPos;
            rb.linearVelocity = Vector2.zero;
        }

        RPC_SetState(player, false, false);
    }

    // ================= SYNC =================
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_SetState(NetworkObject player, bool enter, bool isDriver)
    {
        var p = player.GetComponent<PlayerInteraction>();
        if (p != null)
            p.SetVehicle(this, enter, isDriver);
    }

    // ================= FOLLOW SEAT =================
    public override void FixedUpdateNetwork()
    {
        // 🔥 ép player theo ghế (CỰC QUAN TRỌNG)
        if (Driver != null)
            Driver.transform.position = driverSeat.position;

        if (Passenger != null)
            Passenger.transform.position = passengerSeat.position;

        if (Driver == null) return;
        if (!Driver.HasInputAuthority) return;

        float x = Input.GetKey(KeyCode.A) ? -1 : Input.GetKey(KeyCode.D) ? 1 : 0;
        float y = Input.GetKey(KeyCode.S) ? -1 : Input.GetKey(KeyCode.W) ? 1 : 0;

        Vector2 dir = new Vector2(x, y).normalized;
        rb.linearVelocity = dir * moveSpeed;

        if (animator != null)
        {
            animator.SetFloat("MoveX", dir.x);
            animator.SetFloat("MoveY", dir.y);
        }
    }

    // ================= CAMERA =================
    public void SetCamera(bool enable)
    {
        if (vehicleCamera == null) return;

        vehicleCamera.gameObject.SetActive(enable);

        foreach (var l in FindObjectsOfType<AudioListener>())
            l.enabled = false;

        if (enable)
            vehicleCamera.GetComponent<AudioListener>().enabled = true;
    }
}