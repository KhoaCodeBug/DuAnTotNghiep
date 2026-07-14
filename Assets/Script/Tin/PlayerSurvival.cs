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
        style.alignment = TextAnchor.MiddleRight; // Căn phải để chữ hiển thị bên trái Icon

        // --- TỌA ĐỘ GÓC TRÊN BÊN PHẢI (Giống sếp) ---
        float iconSize = 40f;
        float xPosRoot = Screen.width - 60f; // Cách lề phải 60px để căn lề phải icon
        float yPos = 130f; // Bắt đầu thấp hơn Stamina một chút để tạo cột
        float spacingY = 50f; // Khoảng cách giữa Đ đói và Khát

        // ==========================
        // 1. VẼ MOODLE ĐÓI (Chỉ vẽ icon, hover hiện chữ)
        // ==========================
        if (hungerRatio < 0.40f)
        {
            string hungerText = "";
            if (hungerRatio > 0.25f) { style.normal.textColor = red1; hungerText = "Peckish"; }
            else if (hungerRatio > 0.15f) { style.normal.textColor = red2; hungerText = "Hungry"; }
            else if (hungerRatio > 0f) { style.normal.textColor = red3; hungerText = "Very Hungry"; }
            else { style.normal.textColor = red4; hungerText = "Starving"; }

            Rect iconRect = new Rect(xPosRoot, yPos, iconSize, iconSize);
            if (iconHunger != null) GUI.DrawTexture(iconRect, iconHunger);

            Vector2 mousePos = Event.current.mousePosition;
            if (iconRect.Contains(mousePos))
            {
                GUI.Label(new Rect(xPosRoot - 240f, yPos, 230f, iconSize), hungerText, style);
            }

            yPos += spacingY; // Đẩy tọa độ Y xuống để chuẩn bị vẽ Khát (nếu có)
        }

        // ==========================
        // 2. VẼ MOODLE KHÁT (Chỉ vẽ icon, hover hiện chữ)
        // ==========================
        if (thirstRatio < 0.40f)
        {
            string thirstText = "";
            if (thirstRatio > 0.25f) { style.normal.textColor = red1; thirstText = "Slightly Thirsty"; }
            else if (thirstRatio > 0.15f) { style.normal.textColor = red2; thirstText = "Thirsty"; }
            else if (thirstRatio > 0f) { style.normal.textColor = red3; thirstText = "Parched"; }
            else { style.normal.textColor = red4; thirstText = "Dying of Thirst"; }

            Rect iconRect = new Rect(xPosRoot, yPos, iconSize, iconSize);
            if (iconThirst != null) GUI.DrawTexture(iconRect, iconThirst);

            Vector2 mousePos = Event.current.mousePosition;
            if (iconRect.Contains(mousePos))
            {
                GUI.Label(new Rect(xPosRoot - 240f, yPos, 230f, iconSize), thirstText, style);
            }
        }
    }
}