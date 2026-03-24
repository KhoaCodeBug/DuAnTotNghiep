using UnityEngine;
using Fusion; // Cần dùng mạng

public class PlayerSurvival : NetworkBehaviour // Kế thừa mạng
{
    [Header("--- Chỉ số Đói (Hunger) ---")]
    public float maxHunger = 100f;
    [Networked] public float currentHunger { get; set; } // Biến mạng
    public float hungerDrainRate = 0.5f;

    [Header("--- Chỉ số Khát (Thirst) ---")]
    public float maxThirst = 100f;
    [Networked] public float currentThirst { get; set; } // Biến mạng
    public float thirstDrainRate = 0.8f;

    [Header("--- Sức khỏe ---")]
    public float damageOverTime = 2f;
    private PlayerHealth healthScript;

    public override void Spawned()
    {
        // Máy chủ thiết lập chỉ số ban đầu
        if (HasStateAuthority)
        {
            currentHunger = maxHunger;
            currentThirst = maxThirst;
        }
        healthScript = GetComponent<PlayerHealth>();
    }

    public override void FixedUpdateNetwork()
    {
        // 🔥 CHỈ MÁY CHỦ mới có quyền làm tụt đói/khát để đảm bảo công bằng
        if (!HasStateAuthority) return;

        if (healthScript != null && healthScript.isDead) return;

        currentHunger -= hungerDrainRate * Runner.DeltaTime;
        currentThirst -= thirstDrainRate * Runner.DeltaTime;

        currentHunger = Mathf.Max(currentHunger, 0);
        currentThirst = Mathf.Max(currentThirst, 0);

        if (currentHunger <= 0 || currentThirst <= 0)
        {
            if (healthScript != null)
            {
                // Gọi trừ máu nhưng báo là "Do đói/khát" để không bị chớp đỏ liên tục
                healthScript.TakeDamage(damageOverTime * Runner.DeltaTime, true);
            }
        }
    }

    // --- HÀM ĂN UỐNG (TỪ TÚI ĐỒ GỌI) ---
    public void RestoreHunger(float amount)
    {
        if (HasStateAuthority) PerformRestoreHunger(amount);
        else RPC_RequestRestoreHunger(amount); // Nếu là máy con thì xin phép Server
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestRestoreHunger(float amount) { PerformRestoreHunger(amount); }

    private void PerformRestoreHunger(float amount)
    {
        currentHunger = Mathf.Min(currentHunger + amount, maxHunger);
    }

    // Tương tự cho uống nước
    public void RestoreThirst(float amount)
    {
        if (HasStateAuthority) PerformRestoreThirst(amount);
        else RPC_RequestRestoreThirst(amount);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestRestoreThirst(float amount) { PerformRestoreThirst(amount); }

    private void PerformRestoreThirst(float amount)
    {
        currentThirst = Mathf.Min(currentThirst + amount, maxThirst);
    }
}