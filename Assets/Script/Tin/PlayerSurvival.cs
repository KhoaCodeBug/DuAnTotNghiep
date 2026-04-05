using UnityEngine;
using Fusion;

public class PlayerSurvival : NetworkBehaviour
{
    [Header("--- Moodle Icons (UI) ---")]
    public Texture2D iconHunger; // Texture2D
    public Texture2D iconThirst; // Texture2D

    [Header("--- Chỉ số Đói (Hunger) ---")]
    public float maxHunger = 100f;
    [Networked] public float currentHunger { get; set; }
    public float hungerDrainRate = 0.5f;

    [Header("--- Chỉ số Khát (Thirst) ---")]
    public float maxThirst = 100f;
    [Networked] public float currentThirst { get; set; }
    public float thirstDrainRate = 0.8f;

    [Header("--- Sức khỏe ---")]
    public float damageOverTime = 2f;
    private PlayerHealth healthScript;

    // Màu chuẩn Zomboid (Chỉ dùng màu Debuff Đỏ, giấu Buff xanh)
    private Color red1 = new Color(0.9f, 0.6f, 0.6f, 1f);
    private Color red2 = new Color(0.8f, 0.4f, 0.4f, 1f);
    private Color red3 = new Color(0.7f, 0.2f, 0.2f, 1f);
    private Color red4 = new Color(0.5f, 0.0f, 0.0f, 1f);

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            currentHunger = maxHunger;
            currentThirst = maxThirst;
        }
        healthScript = GetComponent<PlayerHealth>();
    }

    public override void FixedUpdateNetwork()
    {
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
                healthScript.TakeDamage(damageOverTime * Runner.DeltaTime, true);
            }
        }
    }

    public void RestoreHunger(float amount)
    {
        if (HasStateAuthority) PerformRestoreHunger(amount);
        else RPC_RequestRestoreHunger(amount);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestRestoreHunger(float amount) { PerformRestoreHunger(amount); }

    private void PerformRestoreHunger(float amount)
    {
        currentHunger = Mathf.Min(currentHunger + amount, maxHunger);
    }

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

    // =========================================================
    // 🔥 VẼ OnGUI CHUẨN: TOP-RIGHT, CỘT DỌC, ICON TRƯỚC, CHỮ SAU
    // =========================================================
    private void OnGUI()
    {
        if (!HasInputAuthority || (healthScript != null && healthScript.isDead)) return;

        float hungerRatio = currentHunger / maxHunger;
        float thirstRatio = currentThirst / maxThirst;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 22; // To rõ giống Stamina
        style.fontStyle = FontStyle.Bold;
        style.alignment = TextAnchor.MiddleLeft;

        // --- TỌA ĐỘ GÓC TRÊN BÊN PHẢI (Giống sếp) ---
        float iconSize = 40f;
        float xPosRoot = Screen.width - 280f; // Điểm bắt đầu vẽ
        float yPos = 130f; // Bắt đầu thấp hơn Stamina một chút để tạo cột
        float spacingY = 50f; // Khoảng cách giữa Đ đói và Khát

        // ==========================
        // 1. VẼ MOODLE ĐÓI (Icon trước, Chữ sau, Chỉ vẽ Debuff)
        // ==========================
        // 🔥 LÓGIC ĐÃ SỬA: Chỉ vẽ khi đói (< 40%). Khi no (> 40%) thì tàng hình hoàn toàn.
        if (hungerRatio < 0.40f)
        {
            string hungerText = "";
            if (hungerRatio > 0.25f) { style.normal.textColor = red1; hungerText = "Peckish"; }
            else if (hungerRatio > 0.15f) { style.normal.textColor = red2; hungerText = "Hungry"; }
            else if (hungerRatio > 0f) { style.normal.textColor = red3; hungerText = "Very Hungry"; }
            else { style.normal.textColor = red4; hungerText = "Starving"; }

            // 1. Vẽ Icon Đói bên trái
            if (iconHunger != null) GUI.DrawTexture(new Rect(xPosRoot, yPos, iconSize, iconSize), iconHunger);
            // 2. Vẽ Chữ Đói nối đuôi bên phải
            GUI.Label(new Rect(xPosRoot + iconSize + 10f, yPos, 230, iconSize), hungerText, style);

            yPos += spacingY; // Đẩy tọa độ Y xuống để chuẩn bị vẽ Khát (nếu có)
        }

        // ==========================
        // 2. VẼ MOODLE KHÁT (Icon trước, Chữ sau, Chỉ vẽ Debuff)
        // ==========================
        // Chỉ vẽ khi khát (< 40%)
        if (thirstRatio < 0.40f)
        {
            string thirstText = "";
            if (thirstRatio > 0.25f) { style.normal.textColor = red1; thirstText = "Slightly Thirsty"; }
            else if (thirstRatio > 0.15f) { style.normal.textColor = red2; thirstText = "Thirsty"; }
            else if (thirstRatio > 0f) { style.normal.textColor = red3; thirstText = "Parched"; }
            else { style.normal.textColor = red4; thirstText = "Dying of Thirst"; }

            // 1. Vẽ Icon Khát bên trái
            if (iconThirst != null) GUI.DrawTexture(new Rect(xPosRoot, yPos, iconSize, iconSize), iconThirst);
            // 2. Vẽ Chữ Khát nối đuôi bên phải
            GUI.Label(new Rect(xPosRoot + iconSize + 10f, yPos, 230, iconSize), thirstText, style);
        }
    }
}