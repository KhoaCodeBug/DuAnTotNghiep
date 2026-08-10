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
    public Texture2D iconSleepy;

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

    [Header("--- Ngủ & Mệt mỏi ---")]
    [Networked] public NetworkBool IsWaitingForSleep { get; set; }
    [Networked] public NetworkBool RestedThisNight { get; set; }
    [Networked] public int SleepBedId { get; set; }
    [Networked] public float SleepRequestedAtHour { get; set; }

    private string sleepStatusMessage;
    private float sleepStatusMessageUntil;

    public bool IsSleepInputLocked => IsWaitingForSleep ||
                                      (DayNightManager.Instance != null && DayNightManager.Instance.IsSleepTransitionActive);

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            currentHunger = maxHunger;
            currentThirst = maxThirst;
            IsWaitingForSleep = false;
            RestedThisNight = false;
            SleepBedId = 0;
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

    public void TrySleepAtBed(SleepInteractable bed)
    {
        if (!HasInputAuthority || bed == null) return;
        if (HasStateAuthority) ServerTrySleepAtBed(bed.BedId);
        else RPC_RequestSleepAtBed(bed.BedId);
    }

    public void CancelWaitingForSleep()
    {
        if (!HasInputAuthority) return;
        if (HasStateAuthority) ServerCancelSleep();
        else RPC_RequestCancelSleep();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestSleepAtBed(int bedId)
    {
        ServerTrySleepAtBed(bedId);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestCancelSleep()
    {
        ServerCancelSleep();
    }

    private void ServerTrySleepAtBed(int bedId)
    {
        if (!HasStateAuthority || IsWaitingForSleep) return;

        DayNightManager manager = DayNightManager.Instance;
        if (manager == null)
        {
            RPC_ShowSleepMessage("Không tìm thấy hệ thống ngày và đêm.");
            return;
        }

        if (!manager.CanUseBedNow())
        {
            RPC_ShowSleepMessage("Chỉ có thể ngủ từ 20:00 đến 03:00.");
            return;
        }

        if (!SleepInteractable.TryGetBed(bedId, out SleepInteractable bed) ||
            bed.DistanceTo(transform.position) > bed.interactionDistance + 0.4f)
        {
            RPC_ShowSleepMessage("Bạn đang đứng quá xa giường.");
            return;
        }

        PlayerSurvival[] players = FindObjectsByType<PlayerSurvival>(FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] == null || players[i] == this) continue;
            if (players[i].IsWaitingForSleep && players[i].SleepBedId == bedId)
            {
                RPC_ShowSleepMessage("Giường này đã có người sử dụng.");
                return;
            }
        }

        PlayerHealth health = GetComponent<PlayerHealth>();
        if (health == null || health.isDead || health.isTransforming) return;
        PlayerInteraction interaction = GetComponent<PlayerInteraction>();
        if (interaction != null && interaction.IsInVehicle)
        {
            RPC_ShowSleepMessage("Hãy xuống xe trước khi sử dụng giường.");
            return;
        }

        IsWaitingForSleep = true;
        SleepBedId = bedId;
        SleepRequestedAtHour = manager.CurrentTime;
        RPC_ShowSleepMessage("Đã nằm xuống. Đang đợi những người chơi khác...");
    }

    private void ServerCancelSleep()
    {
        if (!HasStateAuthority || !IsWaitingForSleep) return;
        if (DayNightManager.Instance != null && DayNightManager.Instance.IsSleepTransitionActive) return;

        IsWaitingForSleep = false;
        SleepBedId = 0;
        RPC_ShowSleepMessage("Bạn đã rời khỏi giường.");
    }

    public void ServerFinishSleep()
    {
        if (!HasStateAuthority) return;
        IsWaitingForSleep = false;
        SleepBedId = 0;
        RestedThisNight = true;
    }

    public void ServerResetRestForNextNight(float currentHour)
    {
        if (!HasStateAuthority) return;
        if (currentHour >= 12f && currentHour < 20f)
            RestedThisNight = false;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    public void RPC_ShowSleepMessage(string message)
    {
        sleepStatusMessage = message;
        sleepStatusMessageUntil = Time.unscaledTime + 3.5f;
    }

    public float GetSleepiness01()
    {
        if (RestedThisNight || DayNightManager.Instance == null || TutorialSession.IsActive) return 0f;
        float elapsed = GetNightElapsedSince22(DayNightManager.Instance.CurrentTime);
        return elapsed < 0f ? 0f : Mathf.Clamp01((elapsed + 0.01f) / 5f);
    }

    public int GetSleepinessTier()
    {
        float sleepiness = GetSleepiness01();
        if (sleepiness <= 0f) return 0;
        return Mathf.Clamp(Mathf.CeilToInt(sleepiness * 4f), 1, 4);
    }

    public float GetFatigueMovementMultiplier()
    {
        float debuff = GetFatigueDebuff01();
        return debuff <= 0f ? 1f : Mathf.Lerp(0.85f, 0.60f, debuff);
    }

    public float GetFatigueMeleeDamageMultiplier()
    {
        float debuff = GetFatigueDebuff01();
        return debuff <= 0f ? 1f : Mathf.Lerp(0.85f, 0.60f, debuff);
    }

    public float GetFatigueMeleeSpeedMultiplier()
    {
        float debuff = GetFatigueDebuff01();
        return debuff <= 0f ? 1f : Mathf.Lerp(0.85f, 0.60f, debuff);
    }

    private float GetFatigueDebuff01()
    {
        if (RestedThisNight || DayNightManager.Instance == null) return 0f;
        float elapsed = GetNightElapsedSince22(DayNightManager.Instance.CurrentTime);
        // 01:00 là 3 giờ sau 22:00; debuff nặng dần đến lúc bất tỉnh lúc 03:00.
        return elapsed <= 3f ? 0f : Mathf.InverseLerp(3f, 5f, elapsed);
    }

    private static float GetNightElapsedSince22(float hour)
    {
        if (hour >= 22f) return hour - 22f;
        if (hour < 3f) return hour + 2f;
        return -1f;
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

        int sleepTier = GetSleepinessTier();
        if (sleepTier > 0)
        {
            string[] names = { "", "Hơi buồn ngủ", "Buồn ngủ", "Rất buồn ngủ", "Kiệt sức" };
            string effect = DayNightManager.Instance != null && DayNightManager.Instance.CurrentTime >= 1f &&
                            DayNightManager.Instance.CurrentTime < 3f
                ? $"Di chuyển và cận chiến đang bị suy giảm. Bạn sẽ bất tỉnh lúc 03:00."
                : "Hãy tìm một chiếc giường an toàn trước 03:00.";
            string tooltip = $"<b>{names[sleepTier]}</b>\n{effect}";
            DrawMoodle(iconSleepy, GetBackground(badBackgrounds, sleepTier),
                new Rect(xPos, yPos, MoodleIconSize, MoodleIconSize), tooltip, new Color(0.78f, 0.84f, 1f));
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

        DrawSleepOverlay();
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
        if (GetSleepinessTier() > 0)
            yPos += MoodleSpacingY;

        rect = new Rect(Screen.width - MoodleRightMargin, yPos, MoodleIconSize, MoodleIconSize);
        return true;
    }

    private void DrawSleepOverlay()
    {
        DayNightManager manager = DayNightManager.Instance;
        bool waiting = IsWaitingForSleep && (manager == null || !manager.IsSleepTransitionActive);
        bool transitioning = manager != null && manager.IsSleepTransitionActive;

        if (waiting || transitioning)
        {
            int previousDepth = GUI.depth;
            GUI.depth = -1000;
            Color previousColor = GUI.color;
            float alpha = transitioning ? manager.GetSleepOverlayAlpha() : 0.72f;
            GUI.color = transitioning ? new Color(0f, 0f, 0f, alpha) : new Color(0.16f, 0.18f, 0.22f, alpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 26,
                fontStyle = FontStyle.Bold
            };
            titleStyle.normal.textColor = Color.white;

            if (waiting)
            {
                int sleeping = 0;
                int total = 0;
                if (manager != null) manager.GetSleepCounts(out sleeping, out total);
                GUI.Label(new Rect(0f, Screen.height * 0.42f, Screen.width, 48f),
                    $"ĐANG ĐỢI NGƯỜI CHƠI KHÁC  ({sleeping}/{total})", titleStyle);
                if (GUI.Button(new Rect(Screen.width * 0.5f - 90f, Screen.height * 0.54f, 180f, 44f), "RỜI KHỎI GIƯỜNG"))
                    CancelWaitingForSleep();
            }
            else if (alpha > 0.08f)
            {
                string label = manager.CurrentSleepPhase == DayNightManager.SleepPhase.ForcedFade
                    ? "BẠN ĐÃ KIỆT SỨC..."
                    : "ĐANG NGỦ...";
                GUI.Label(new Rect(0f, Screen.height * 0.46f, Screen.width, 48f), label, titleStyle);
            }

            GUI.color = previousColor;
            GUI.depth = previousDepth;
        }

        if (!string.IsNullOrEmpty(sleepStatusMessage) && Time.unscaledTime < sleepStatusMessageUntil)
        {
            GUIStyle messageStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            GUI.Box(new Rect(Screen.width * 0.5f - 230f, 55f, 460f, 48f), sleepStatusMessage, messageStyle);
        }
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
