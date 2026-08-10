using Fusion;
using UnityEngine;

public class PlayerSurvival : NetworkBehaviour
{
    private const float MoodleIconSize = 40f;
    private const float MoodleRightMargin = 60f;
    private const float MoodleTop = 130f;
    private const float MoodleSpacingY = 50f;

    [Header("--- Moodle Icons (UI) ---")]
    public Texture2D iconHunger;
    public Texture2D iconThirst;
    public Texture2D iconBleeding;

    [Header("--- Moodle Backgrounds ---")]
    public Texture2D[] badBackgrounds = new Texture2D[4];
    public Texture2D[] goodBackgrounds = new Texture2D[4];

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
        if (TutorialSession.IsActive && TutorialInputGate.SurvivalFrozen) return;

        currentHunger = Mathf.Max(currentHunger - hungerDrainRate * Runner.DeltaTime, 0f);
        currentThirst = Mathf.Max(currentThirst - thirstDrainRate * Runner.DeltaTime, 0f);

        if (currentHunger <= 0f || currentThirst <= 0f)
        {
            healthScript?.TakeDamage(damageOverTime * Runner.DeltaTime, true);
        }
    }

    public void RestoreHunger(float amount)
    {
        if (HasStateAuthority) PerformRestoreHunger(amount);
        else RPC_RequestRestoreHunger(amount);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestRestoreHunger(float amount) => PerformRestoreHunger(amount);

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
    private void RPC_RequestRestoreThirst(float amount) => PerformRestoreThirst(amount);

    private void PerformRestoreThirst(float amount)
    {
        currentThirst = Mathf.Min(currentThirst + amount, maxThirst);
    }

    public void SetTutorialNeeds(float hungerRatio, float thirstRatio)
    {
        hungerRatio = Mathf.Clamp01(hungerRatio);
        thirstRatio = Mathf.Clamp01(thirstRatio);
        if (HasStateAuthority) PerformSetTutorialNeeds(hungerRatio, thirstRatio);
        else RPC_RequestSetTutorialNeeds(hungerRatio, thirstRatio);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestSetTutorialNeeds(float hungerRatio, float thirstRatio)
    {
        PerformSetTutorialNeeds(hungerRatio, thirstRatio);
    }

    private void PerformSetTutorialNeeds(float hungerRatio, float thirstRatio)
    {
        currentHunger = maxHunger * hungerRatio;
        currentThirst = maxThirst * thirstRatio;
    }

    /// <summary>
    /// 0 = không có hiệu ứng No, 1..4 = bốn cấp nền xanh từ 80% đến 100%.
    /// Cả thức ăn và nước đều phải đạt 80% để cơ thể có thể hồi phục.
    /// </summary>
    public int GetWellFedTier()
    {
        float hungerRatio = SafeRatio(currentHunger, maxHunger);
        float thirstRatio = SafeRatio(currentThirst, maxThirst);
        if (hungerRatio < 0.8f || thirstRatio < 0.8f) return 0;

        if (hungerRatio < 0.85f) return 1;
        if (hungerRatio < 0.90f) return 2;
        if (hungerRatio < 0.95f) return 3;
        return 4;
    }

    private static float SafeRatio(float current, float maximum)
    {
        return maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
    }

    private void OnGUI()
    {
        if (!HasInputAuthority || (healthScript != null && healthScript.isDead)) return;

        float hungerRatio = SafeRatio(currentHunger, maxHunger);
        float thirstRatio = SafeRatio(currentThirst, maxThirst);
        float xPos = Screen.width - MoodleRightMargin;
        float yPos = MoodleTop;

        int wellFedTier = GetWellFedTier();
        if (wellFedTier > 0)
        {
            float healRate = healthScript != null
                ? healthScript.GetPassiveHealRate(wellFedTier)
                : 0.4f + wellFedTier * 0.1f;
            string[] names = { "", "Hơi no", "No", "Rất no", "No căng bụng" };
            string tooltip = $"<b>{names[wellFedTier]}</b> — {Mathf.RoundToInt(hungerRatio * 100f)}%\n" +
                             $"Hồi {healRate:0.0} HP/giây khi không chảy máu.\n" +
                             $"Nước hiện tại: {Mathf.RoundToInt(thirstRatio * 100f)}%.";
            DrawMoodle(iconHunger, GetBackground(goodBackgrounds, wellFedTier),
                new Rect(xPos, yPos, MoodleIconSize, MoodleIconSize), tooltip, new Color(0.6f, 1f, 0.45f));
            yPos += MoodleSpacingY;
        }
        else if (hungerRatio < 0.4f)
        {
            int tier = GetBadTier(hungerRatio);
            string[] names = { "", "Hơi đói", "Đói", "Rất đói", "Đói kiệt sức" };
            string effect = tier == 4
                ? $"Đang mất {damageOverTime:0.#} HP/giây. Hãy ăn ngay."
                : "Mức nguy hiểm sẽ tăng nếu không ăn.";
            string tooltip = $"<b>{names[tier]}</b> — {Mathf.RoundToInt(hungerRatio * 100f)}%\n{effect}";
            DrawMoodle(iconHunger, GetBackground(badBackgrounds, tier),
                new Rect(xPos, yPos, MoodleIconSize, MoodleIconSize), tooltip, Color.white);
            yPos += MoodleSpacingY;
        }

        if (thirstRatio < 0.4f)
        {
            int tier = GetBadTier(thirstRatio);
            string[] names = { "", "Hơi khát", "Khát", "Khát nghiêm trọng", "Khát kiệt sức" };
            string effect = tier == 4
                ? $"Đang mất {damageOverTime:0.#} HP/giây. Hãy uống ngay."
                : "Mức nguy hiểm sẽ tăng nếu không uống nước.";
            string tooltip = $"<b>{names[tier]}</b> — {Mathf.RoundToInt(thirstRatio * 100f)}%\n{effect}";
            DrawMoodle(iconThirst, GetBackground(badBackgrounds, tier),
                new Rect(xPos, yPos, MoodleIconSize, MoodleIconSize), tooltip, Color.white);
            yPos += MoodleSpacingY;
        }

        if (healthScript != null && healthScript.isBleeding)
        {
            string tooltip = $"<b>Đang chảy máu</b>\n" +
                             $"Mất {healthScript.bleedDamagePerSecond:0.#} HP/giây.\n" +
                             "Mở Health Status và băng bó mọi vết thương.";
            DrawMoodle(iconBleeding, GetBackground(badBackgrounds, 4),
                new Rect(xPos, yPos, MoodleIconSize, MoodleIconSize), tooltip, new Color(1f, 0.75f, 0.75f));
        }
    }

    /// <summary>
    /// Returns the exact IMGUI rectangle used by the bleeding Moodle. Tutorial
    /// overlays use this instead of duplicating the dynamic Moodle layout.
    /// </summary>
    public bool TryGetBleedingMoodleRect(out Rect rect)
    {
        rect = default;
        if (healthScript == null) healthScript = GetComponent<PlayerHealth>();
        if (healthScript == null || !healthScript.isBleeding || healthScript.isDead) return false;

        float yPos = MoodleTop;
        float hungerRatio = SafeRatio(currentHunger, maxHunger);
        float thirstRatio = SafeRatio(currentThirst, maxThirst);

        if (GetWellFedTier() > 0 || hungerRatio < 0.4f)
            yPos += MoodleSpacingY;
        if (thirstRatio < 0.4f)
            yPos += MoodleSpacingY;

        rect = new Rect(Screen.width - MoodleRightMargin, yPos, MoodleIconSize, MoodleIconSize);
        return true;
    }

    private static int GetBadTier(float ratio)
    {
        if (ratio > 0.25f) return 1;
        if (ratio > 0.15f) return 2;
        if (ratio > 0f) return 3;
        return 4;
    }

    private static Texture2D GetBackground(Texture2D[] backgrounds, int tier)
    {
        int index = tier - 1;
        return backgrounds != null && index >= 0 && index < backgrounds.Length
            ? backgrounds[index]
            : null;
    }

    private static void DrawMoodle(Texture2D icon, Texture2D background, Rect iconRect,
        string tooltip, Color tooltipColor)
    {
        if (background != null)
            GUI.DrawTexture(iconRect, background, ScaleMode.ScaleToFit, true);
        if (icon != null)
            GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);

        if (!iconRect.Contains(Event.current.mousePosition)) return;

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleLeft,
            wordWrap = true,
            richText = true
        };
        labelStyle.normal.textColor = tooltipColor;

        const float tooltipWidth = 310f;
        float tooltipHeight = Mathf.Max(iconRect.height, labelStyle.CalcHeight(new GUIContent(tooltip), tooltipWidth - 20f) + 16f);
        Rect tooltipRect = new Rect(iconRect.x - tooltipWidth - 10f,
            iconRect.y + (iconRect.height - tooltipHeight) * 0.5f, tooltipWidth, tooltipHeight);
        GUI.Box(tooltipRect, GUIContent.none);
        GUI.Label(new Rect(tooltipRect.x + 10f, tooltipRect.y + 8f,
            tooltipRect.width - 20f, tooltipRect.height - 16f), tooltip, labelStyle);
    }
}
