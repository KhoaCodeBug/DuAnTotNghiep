using Fusion;
using UnityEngine;

public class Skill_StealthCrouch : NetworkBehaviour
{
    [Header("--- Chỉ số Kỹ năng ---")]
    public float activeDuration = 5f; // Tàng hình 5s
    public float cooldownTime = 30f; // Hồi chiêu 30s

    [Networked] public float ActiveTimer { get; set; }
    [Networked] public float CooldownTimer { get; set; }

    // 🔥 Biến này cực quan trọng: Báo cho Zombie biết đừng cắn con này!
    [Networked] public NetworkBool IsInvisible { get; set; }

    private PlayerMovement movementScript;

    public override void Spawned()
    {
        // Lấy cái script di chuyển để biết lúc nào người chơi đang ngồi
        movementScript = GetComponent<PlayerMovement>();
    }

    public override void FixedUpdateNetwork()
    {
        if (CooldownTimer > 0) CooldownTimer -= Runner.DeltaTime;

        if (ActiveTimer > 0)
        {
            ActiveTimer -= Runner.DeltaTime;
            if (ActiveTimer <= 0)
            {
                IsInvisible = false;
                Debug.Log("Hết 5s tàng hình, Zombie thấy mặt rồi!");
            }
        }
        else
        {
            // NẾU: Kỹ năng đã sẵn sàng (cooldown <= 0) VÀ Người chơi bấm nút Ngồi (NetIsCrouching)
            if (CooldownTimer <= 0 && movementScript.NetIsCrouching && !IsInvisible)
            {
                ActivateSkill();
            }
        }
    }

    private void ActivateSkill()
    {
        IsInvisible = true;
        ActiveTimer = activeDuration;
        CooldownTimer = cooldownTime;
        Debug.Log("👻 KÍCH HOẠT TÀNG HÌNH! Zombie bị mù 5s!");
    }
}