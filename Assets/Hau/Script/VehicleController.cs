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

    private Vector2 lastDir = Vector2.down;
    private float stopTimer = 0f;

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

        // 🔥 STOP XE
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        stopTimer = 0.1f;

        player.transform.position = exitPos;

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

    // ================= MOVE =================
    public override void FixedUpdateNetwork()
    {
        if (stopTimer > 0)
        {
            stopTimer -= Runner.DeltaTime;
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (Driver == null)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (!Driver.HasInputAuthority) return;

        float x = Input.GetKey(KeyCode.A) ? -1 :
                  Input.GetKey(KeyCode.D) ? 1 : 0;

        float y = Input.GetKey(KeyCode.S) ? -1 :
                  Input.GetKey(KeyCode.W) ? 1 : 0;

        Vector2 dir = new Vector2(x, y).normalized;

        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            dir.y = 0;
        else
            dir.x = 0;

        rb.linearVelocity = dir * moveSpeed;

        UpdateAnimation(dir);

        if (Driver != null)
            Driver.transform.position = driverSeat.position;

        if (Passenger != null)
            Passenger.transform.position = passengerSeat.position;
    }

    // ================= ANIMATION =================
    void UpdateAnimation(Vector2 dir)
    {
        if (animator == null) return;

        if (dir != Vector2.zero)
            lastDir = dir;

        animator.SetFloat("MoveX", lastDir.x);
        animator.SetFloat("MoveY", lastDir.y);
        animator.SetBool("isMoving", dir != Vector2.zero);
    }

    // ================= CAMERA =================
    public void SetCamera(bool enable)
    {
        if (vehicleCamera == null) return;

        // 🔥 FIX: chỉ local mới được bật camera
        if (!Object.HasInputAuthority && !Object.HasStateAuthority)
            return;

        vehicleCamera.gameObject.SetActive(enable);

        foreach (var l in FindObjectsOfType<AudioListener>())
            l.enabled = false;

        if (enable)
            vehicleCamera.GetComponent<AudioListener>().enabled = true;
    }
}