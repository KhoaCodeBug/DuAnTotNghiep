using UnityEngine;
using UnityEngine.EventSystems;

public class MobileInputController : MonoBehaviour
{
    public static MobileInputController Instance;

    [Header("=== Cần Gạt (Joysticks) ===")]
    public Joystick moveJoystick; // Cần gạt bên trái (Đi lại)
    public Joystick aimJoystick;  // Cần gạt bên phải (Xoay mặt & Bắn)

    [Header("=== Trạng Thái Nút Bấm ===")]
    public bool isBashPressed;
    private bool isReloadTriggered;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // 🔥 Dành cho nút Đập Báng Súng (Phải đè/nhấn giữ)
    public void OnBashPointerDown() { isBashPressed = true; }
    public void OnBashPointerUp() { isBashPressed = false; }

    // 🔥 Dành cho nút Nạp Đạn (Chỉ cần chạm 1 cái)
    public void OnReloadClick() { isReloadTriggered = true; }

    // Hàm này để script PlayerCombat gọi ra "ăn" lệnh Reload
    public bool CheckAndConsumeReload()
    {
        if (isReloadTriggered)
        {
            isReloadTriggered = false; // Ăn xong thì reset
            return true;
        }
        return false;
    }
}