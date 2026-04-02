using UnityEngine;
using Fusion;

public class VehicleControllerFusion : NetworkBehaviour
{
    public enum SeatType { Driver, Passenger, None }

    [Header("References")]
    [SerializeField] private Animator _carAnimator;
    public float carSpeed = 5f;

    // Lưu trữ NetworkObject của người chơi thay vì chỉ ID
    [Networked] public NetworkObject DriverObj { get; set; }
    [Networked] public NetworkObject PassengerObj { get; set; }

    // --- LÊN XE ---
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestEnterVehicle(NetworkObject playerObj)
    {
        if (playerObj == DriverObj || playerObj == PassengerObj) return;

        if (DriverObj == null)
        {
            DriverObj = playerObj;
            // QUAN TRỌNG: Cấp quyền điều khiển xe cho người lái
            Object.AssignInputAuthority(playerObj.InputAuthority);

            RPC_ConfirmVehicleAction(playerObj, true, SeatType.Driver);
        }
        else if (PassengerObj == null)
        {
            PassengerObj = playerObj;
            RPC_ConfirmVehicleAction(playerObj, true, SeatType.Passenger);
        }
    }

    // --- XUỐNG XE ---
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestExitVehicle(NetworkObject playerObj)
    {
        if (playerObj == DriverObj)
        {
            DriverObj = null;
            // Rút lại quyền điều khiển xe
            Object.RemoveInputAuthority();
            RPC_ConfirmVehicleAction(playerObj, false, SeatType.Driver);
        }
        else if (playerObj == PassengerObj)
        {
            PassengerObj = null;
            RPC_ConfirmVehicleAction(playerObj, false, SeatType.Passenger);
        }
    }

    // --- ĐỒNG BỘ TRẠNG THÁI LÊN/XUỐNG CHO MỌI NGƯỜI ---
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_ConfirmVehicleAction(NetworkObject playerObj, bool isEntering, SeatType seat)
    {
        if (playerObj == null) return;

        // Gọi hàm để ẩn/hiện nhân vật
        var playerInteraction = playerObj.GetComponent<PlayerInteraction>();
        if (playerInteraction != null)
        {
            playerInteraction.SetInVehicleState(isEntering ? this : null, isEntering);
        }

        // Tùy chọn: Dịch chuyển nhân vật ra cạnh xe khi xuống
        if (!isEntering)
        {
            playerObj.transform.position = transform.position + new Vector3(1.5f, 0, 0); // Văng ra bên cạnh 1 chút
        }
    }

    // --- LOGIC LÁI XE VÀ ANIMATION ---
    public override void FixedUpdateNetwork()
    {
        // Chỉ chạy logic di chuyển nếu có người lái và máy hiện tại có quyền (Server hoặc Driver)
        if (DriverObj != null && (HasStateAuthority || HasInputAuthority))
        {
            // Lấy input (Thay thế bằng hệ thống Input của Fusion nếu bạn có struct NetworkInput)
            float moveX = Input.GetAxisRaw("Horizontal");
            float moveY = Input.GetAxisRaw("Vertical");

            Vector3 moveDirection = new Vector3(moveX, moveY, 0).normalized;

            // Di chuyển xe
            transform.Translate(moveDirection * carSpeed * Runner.DeltaTime);

            // Cập nhật Animation Blend Tree (Hình 4 của bạn)
            if (moveDirection.magnitude > 0.1f)
            {
                _carAnimator.SetFloat("MoveX", moveX);
                _carAnimator.SetFloat("MoveY", moveY);
            }
        }
    }
}