using Fusion;
using UnityEngine;

public class Skill_WeaponMaster : NetworkBehaviour
{
    [Header("--- Chỉ số Kỹ năng ---")]
    public int killsRequired = 10; // Giết đủ 10 con thì kích hoạt
    public float activeDuration = 5f; // Bắn không tốn đạn 5s
    public float cooldownTime = 50f; // Hồi chiêu 50s

    // 🔥 Biến mạng đồng bộ
    [Networked] public int CurrentKills { get; set; }
    [Networked] public float ActiveTimer { get; set; }
    [Networked] public float CooldownTimer { get; set; }
    [Networked] public NetworkBool IsWeaponMasterActive { get; set; }

    public override void FixedUpdateNetwork()
    {
        // 1. Chạy thời gian hồi chiêu
        if (CooldownTimer > 0) CooldownTimer -= Runner.DeltaTime;

        // 2. Chạy thời gian hiệu lực của skill
        if (ActiveTimer > 0)
        {
            ActiveTimer -= Runner.DeltaTime;
            if (ActiveTimer <= 0)
            {
                IsWeaponMasterActive = false;
                Debug.Log("Hết 5 giây Bậc Thầy Vũ Khí. Đạn lại tốn bình thường!");
            }
        }
    }

    // Hàm này gọi khi giết được 1 con Zombie
    public void AddKill()
    {
        if (CooldownTimer > 0 || IsWeaponMasterActive) return; // Đang hồi chiêu hoặc đang bật thì ko tích điểm

        CurrentKills++;
        if (CurrentKills >= killsRequired)
        {
            ActivateSkill();
        }
    }

    private void ActivateSkill()
    {
        IsWeaponMasterActive = true;
        CurrentKills = 0; // Reset số kill
        ActiveTimer = activeDuration;
        CooldownTimer = cooldownTime;
        Debug.Log("🔥 KÍCH HOẠT BẬC THẦY VŨ KHÍ! Tới công chuyện luôn!");
    }
}