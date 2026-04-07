using UnityEngine;
using Fusion;

[RequireComponent(typeof(Rigidbody2D))]
public class VehicleControllerFusion : NetworkBehaviour
{
    [Header("Vehicle")]
    public float moveSpeed = 8f;
    public float turnSpeed = 200f;

    [Header("Camera")]
    public Camera vehicleCamera;

    [Networked] private NetworkObject Driver { get; set; }

    private Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        rb.gravityScale = 0;
        rb.freezeRotation = false;

        if (vehicleCamera != null)
            vehicleCamera.gameObject.SetActive(false);
    }

    // ================= ENTER =================
    public void RequestEnter(NetworkObject player)
    {
        if (!Object.HasInputAuthority) return;

        RPC_Enter(player);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_Enter(NetworkObject player)
    {
        if (Driver != null) return;

        Driver = player;

        var interaction = player.GetComponent<PlayerInteraction>();
        if (interaction != null)
        {
            interaction.SetVehicle(this, true);
        }
    }

    // ================= EXIT =================
    public void RequestExit(NetworkObject player)
    {
        if (!Object.HasInputAuthority) return;

        RPC_Exit(player);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_Exit(NetworkObject player)
    {
        if (Driver != player) return;

        var interaction = player.GetComponent<PlayerInteraction>();
        if (interaction != null)
        {
            interaction.SetVehicle(this, false);
        }

        Driver = null;
    }

    // ================= CONTROL =================
    public override void FixedUpdateNetwork()
    {
        if (Driver == null) return;
        if (!Object.HasInputAuthority) return;

        float move = Input.GetKey(KeyCode.W) ? 1 :
               Input.GetKey(KeyCode.S) ? -1 : 0;

        float turn = Input.GetKey(KeyCode.A) ? 1 :
                     Input.GetKey(KeyCode.D) ? -1 : 0;

        rb.linearVelocity = transform.up * move * moveSpeed;
        rb.MoveRotation(rb.rotation + turn * turnSpeed * Runner.DeltaTime);
        // di chuyển
        rb.linearVelocity = transform.up * move * moveSpeed;

        // xoay (FIX lỗi không quay)
        rb.MoveRotation(rb.rotation + turn * turnSpeed * Runner.DeltaTime);
    }

    // ================= CAMERA =================
    public void SetCamera(bool enable)
    {
        if (vehicleCamera == null) return;

        vehicleCamera.gameObject.SetActive(enable);

        // đảm bảo chỉ 1 AudioListener
        AudioListener[] listeners = FindObjectsOfType<AudioListener>();
        foreach (var l in listeners)
            l.enabled = false;

        if (enable)
        {
            var listener = vehicleCamera.GetComponent<AudioListener>();
            if (listener != null)
                listener.enabled = true;
        }
    }
}