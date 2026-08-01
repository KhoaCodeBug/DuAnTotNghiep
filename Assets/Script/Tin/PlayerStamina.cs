using Fusion;
using UnityEngine;

public class PlayerStamina : NetworkBehaviour
{
    [Header("--- Moodle Icons (UI) ---")]
    public Texture2D iconStamina; // Texture2D

    [Header("--- Stamina System ---")]
    [Networked] public float maxStamina { get; set; }
    [Networked] public float currentStamina { get; set; }

    public float staminaDrain = 20f;
    public float staminaRecoverIdle = 15f;
    public float staminaRecoverWalk = 5f;

    [Header("--- Combat Penalty ---")]
    public float bashRegenDelay = 3f;
    [Networked] private float currentRegenDelayTimer { get; set; }

    [Networked] public NetworkBool IsExhausted { get; private set; }
    [Networked] public NetworkBool HasEnergyBuff { get; private set; }
    [Networked] public float CurrentSpeedMultiplier { get; private set; }

    [Networked] private TickTimer buffTimer { get; set; }
    [Networked] private float activeStaminaBoost { get; set; }

    [Header("--- Heavy Breathing SFX ---")]
    public AudioClip heavyBreathingSFX;
    private AudioSource breathingAudioSource;

    // Màu chuẩn Zomboid (Chỉ có Debuff Đỏ cho Stamina)
    private Color red1 = new Color(0.9f, 0.6f, 0.6f, 1f);
    private Color red2 = new Color(0.8f, 0.4f, 0.4f, 1f);
    private Color red3 = new Color(0.7f, 0.2f, 0.2f, 1f);
    private Color red4 = new Color(0.5f, 0.0f, 0.0f, 1f);

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            maxStamina = 100f;
            currentStamina = 100f;
            CurrentSpeedMultiplier = 1f;
            IsExhausted = false;
            HasEnergyBuff = false;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        if (HasEnergyBuff && buffTimer.Expired(Runner))
        {
            HasEnergyBuff = false;
            CurrentSpeedMultiplier = 1f;
            maxStamina -= activeStaminaBoost;

            if (currentStamina > maxStamina) currentStamina = maxStamina;
            activeStaminaBoost = 0f;
        }

        UpdateBreathingAudio();
    }

    public void UpdateStamina(bool isRunning, bool isMovingNow)
    {
        if (currentRegenDelayTimer > 0)
        {
            currentRegenDelayTimer -= Runner.DeltaTime;
        }

        if (isRunning && isMovingNow)
        {
            if (!HasEnergyBuff)
            {
                currentStamina -= staminaDrain * Runner.DeltaTime;
                if (currentStamina <= 0)
                {
                    currentStamina = 0;
                    IsExhausted = true;
                }
            }
        }
        else
        {
            if (currentRegenDelayTimer > 0) return;

            float currentRecoverRate = isMovingNow ? staminaRecoverWalk : staminaRecoverIdle;
            if (HasEnergyBuff) currentRecoverRate *= 3f;

            currentStamina += currentRecoverRate * Runner.DeltaTime;

            if (currentStamina >= maxStamina)
            {
                currentStamina = maxStamina;
                IsExhausted = false;
            }
        }
    }

    public void ConsumeStamina(float amount)
    {
        if (!HasEnergyBuff)
        {
            currentStamina -= amount;
            currentRegenDelayTimer = bashRegenDelay;

            if (currentStamina <= 0)
            {
                currentStamina = 0;
                IsExhausted = true;
            }
        }
    }

    public void RestoreStamina(float amount)
    {
        currentStamina += amount;
        if (currentStamina >= maxStamina) currentStamina = maxStamina;
        if (currentStamina > 0) IsExhausted = false;
    }

    public void ApplyEnergyBuff(float duration, float speedBoost, float staminaBoost)
    {
        HasEnergyBuff = true;
        IsExhausted = false;
        CurrentSpeedMultiplier = speedBoost;
        activeStaminaBoost = staminaBoost;
        maxStamina += staminaBoost;
        currentStamina += staminaBoost;
        buffTimer = TickTimer.CreateFromSeconds(Runner, duration);
    }

    public float GetSFXVolume()
    {
        float vol = PlayerPrefs.GetFloat("GameSFXVolume", 0.8f);
        if (AutoMainMenuManager.Instance != null)
        {
            vol = AutoMainMenuManager.Instance.sfxVolume;
        }
        return Mathf.Clamp01(vol);
    }

    private void UpdateBreathingAudio()
    {
        if (!HasInputAuthority) return;

        PlayerHealth health = GetComponent<PlayerHealth>();
        bool isDead = health != null && health.isDead;

        float staminaRatio = maxStamina > 0 ? (currentStamina / maxStamina) : 1f;

        if (breathingAudioSource == null)
        {
            breathingAudioSource = gameObject.AddComponent<AudioSource>();
            breathingAudioSource.playOnAwake = false;
            breathingAudioSource.loop = true;
        }

        if (heavyBreathingSFX == null)
        {
            heavyBreathingSFX = Resources.Load<AudioClip>("Sound/BodyState/heavy_breathing");
        }

        if (isDead || staminaRatio >= 0.45f)
        {
            if (breathingAudioSource.isPlaying)
            {
                breathingAudioSource.volume = Mathf.MoveTowards(breathingAudioSource.volume, 0f, 2f * Time.deltaTime);
                if (breathingAudioSource.volume <= 0.01f)
                {
                    breathingAudioSource.Stop();
                }
            }
        }
        else if (staminaRatio <= 0.40f && heavyBreathingSFX != null)
        {
            // 🔥 TIẾNG THỞ DỐC TĂNG DẦN THEO ĐỘ TỤT THỂ LỰC (staminaRatio: 0.40 -> 0.00)
            float factor = Mathf.Clamp01((0.40f - staminaRatio) / 0.40f); // 0.0 (bắt đầu mệt) -> 1.0 (kiệt sức)
            float masterSFX = GetSFXVolume();

            float targetVolume = Mathf.Lerp(0.20f, 0.95f, factor) * masterSFX;
            float targetPitch = Mathf.Lerp(0.92f, 1.10f, factor);

            if (!breathingAudioSource.isPlaying)
            {
                breathingAudioSource.clip = heavyBreathingSFX;
                breathingAudioSource.volume = targetVolume;
                breathingAudioSource.pitch = targetPitch;
                breathingAudioSource.Play();
            }
            else
            {
                float dt = Time.deltaTime;
                breathingAudioSource.volume = Mathf.MoveTowards(breathingAudioSource.volume, targetVolume, 3f * dt);
                breathingAudioSource.pitch = Mathf.MoveTowards(breathingAudioSource.pitch, targetPitch, 1f * dt);
            }
        }
    }

    private void Update()
    {
        UpdateBreathingAudio();
    }

    // =========================================================
    // 🔥 VẼ OnGUI CHUẨN: TOP-RIGHT, ICON TRƯỚC, CHỮ SAU
    // =========================================================
    private void OnGUI()
    {
        if (!HasInputAuthority) return;

        float staminaRatio = currentStamina / maxStamina;

        // Nếu khỏe re (> 60%) thì không vẽ gì hết
        if (staminaRatio >= 0.60f) return;

        // --- Cài đặt font chữ (Tựa vào cái style mẫu của sếp) ---
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 22; // To rõ
        style.fontStyle = FontStyle.Bold;
        style.alignment = TextAnchor.MiddleRight; // Căn phải chữ để nó nằm bên trái Icon

        // --- TỌA ĐỘ GÓC TRÊN BÊN PHẢI (Giống sếp) ---
        float iconSize = 40f;
        float xPosRoot = Screen.width - 60f; // Cách lề phải 60px để căn lề phải icon
        float yPos = 80f; // Bắt đầu từ độ cao 80px (để chừa chỗ cho Máu/Chảy máu nếu có)

        string statusText = "";

        // --- Xác định màu sắc và chữ dựa trên độ mệt ---
        if (staminaRatio > 0.40f)
        {
            style.normal.textColor = red1;
            statusText = "Moderate Exertion";
        }
        else if (staminaRatio > 0.25f)
        {
            style.normal.textColor = red2;
            statusText = "High Exertion";
        }
        else if (staminaRatio > 0f)
        {
            style.normal.textColor = red3;
            statusText = "Excessive Exertion";
        }
        else
        {
            style.normal.textColor = red4;
            statusText = "Exhausted";
        }

        // --- VẼ: CHỈ ICON, HOVER MỚI HIỆN CHỮ ---
        Rect iconRect = new Rect(xPosRoot, yPos, iconSize, iconSize);
        if (iconStamina != null)
        {
            GUI.DrawTexture(iconRect, iconStamina);
        }

        // Kiểm tra di chuột (hover)
        Vector2 mousePos = Event.current.mousePosition;
        if (iconRect.Contains(mousePos))
        {
            GUI.Label(new Rect(xPosRoot - 240f, yPos, 230f, iconSize), statusText, style);
        }
    }
}