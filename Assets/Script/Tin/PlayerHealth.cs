using UnityEngine;
using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.Serialization;

public class PlayerHealth : NetworkBehaviour
{
    public static PlayerHealth LocalHealthInstance;

    [Header("Chỉ số Máu")]
    public float maxHealth = 100f;
    [Networked] public float currentHealth { get; set; }
    public float CurrentHealthSafe => (Object != null && Object.IsValid) ? currentHealth : maxHealth;

    [Header("Hiệu ứng khi bị đánh")]
    public float stunDuration = 0.4f;
    public Color hurtColor = Color.red;
    public float flashDuration = 0.1f;

    [Header("Cài đặt Hardcore PZ")]
    public float bleedDamagePerSecond = 1.5f;
    [Tooltip("Tốc độ hồi máu ở cấp No đầu tiên. Mỗi cấp No cao hơn cộng thêm healBonusPerWellFedTier.")]
    public float passiveHealPerSecond = 0.5f;
    public float healBonusPerWellFedTier = 0.1f;

    [Networked] public NetworkBool isBleeding { get; set; }
    [Networked] public NetworkBool isInPain { get; set; }

    // ==========================================
    // 🔥 HỆ THỐNG NHIỄM TRÙNG & KẺ PHẢN BỘI
    // ==========================================
    [Header("Hệ thống Nhiễm Trùng")]
    [Networked] public float infectionTimer { get; set; } = 600f;
    [Networked] public NetworkBool isBitten { get; set; }

    private float blinkCooldown = 0f;

    [Header("Hóa Zombie Khi Chết")]
    [FormerlySerializedAs("traitorBossPrefab")]
    public NetworkPrefabRef zombieTurnPrefab;
    [Networked] public NetworkBool isTransforming { get; set; }
    [Networked] public float transformTimer { get; set; } = 5f;

    [Header("Hiệu ứng Hoang Tưởng")]
    public RuntimeAnimatorController zombieAnimatorController;

    [Header("Blend Tree Parameters")]
    public string paramMoveX = "MoveX";
    public string paramMoveY = "MoveY";
    public string paramSpeed = "Speed";

    private Dictionary<Animator, RuntimeAnimatorController> originalTeammateControllers = new Dictionary<Animator, RuntimeAnimatorController>();
    private List<PlayerNameTag> hiddenNameTags = new List<PlayerNameTag>();

    private Dictionary<Animator, Vector3> lastTeammatePositions = new Dictionary<Animator, Vector3>();
    private bool isFakeZombieVisible = false;

    private PlayerMovement movementScript;
    private Animator anim;
    private SpriteRenderer spriteRend;
    private Color originalColor;
    private bool isFlashing = false;

    private PlayerSurvival survivalSystem;

    [Networked] public NetworkBool isDead { get; set; }

    [Header("--- Body State SFX ---")]
    public AudioClip deathSFX;
    public AudioClip[] hurtGruntSFXs = new AudioClip[5];
    private AudioSource bodyStateAudioSource;
    private float lastHurtSoundTime = 0f;
    private int lastHurtIndex = -1;

    private Canvas paranoiaCanvas;
    private Image paranoiaImage;
    private bool isBlinking = false;

    private bool hasTriggeredSpectate = false;

    public override void Spawned()
    {
        if (HasStateAuthority) currentHealth = maxHealth;

        movementScript = GetComponent<PlayerMovement>();
        anim = GetComponent<Animator>();
        spriteRend = GetComponentInChildren<SpriteRenderer>();
        survivalSystem = GetComponent<PlayerSurvival>();

        if (spriteRend != null) originalColor = spriteRend.color;

        if (HasInputAuthority)
        {
            LocalHealthInstance = this;
            SetupParanoiaUI();

            if (AutoHealthPanel.Instance != null)
            {
                AutoHealthPanel.Instance.ResetAllInjuries();
            }
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        CleanupParanoia();
        if (paranoiaCanvas != null)
        {
            Destroy(paranoiaCanvas.gameObject);
        }
        // 🔥 FIX: Reset static reference khi bị Despawn để ván sau tìm lại đúng player
        if (LocalHealthInstance == this) LocalHealthInstance = null;
    }

    private void SetupParanoiaUI()
    {
        GameObject canvasObj = new GameObject("ParanoiaCanvas_" + Object.Id);
        DontDestroyOnLoad(canvasObj);

        paranoiaCanvas = canvasObj.AddComponent<Canvas>();
        paranoiaCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        paranoiaCanvas.sortingOrder = 200;

        GameObject imgObj = new GameObject("ParanoiaOverlay");
        imgObj.transform.SetParent(canvasObj.transform, false);
        paranoiaImage = imgObj.AddComponent<Image>();

        RectTransform rect = paranoiaImage.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        paranoiaImage.color = new Color(0, 0, 0, 0);
        paranoiaImage.raycastTarget = false;
    }

    private void Update()
    {
        if (!HasInputAuthority) return;

        // Death is authored by StateAuthority and reaches the owning client through
        // Fusion replication. Clean local-only hallucination visuals as soon as that
        // replicated state arrives instead of waiting for the old avatar to despawn.
        if (isDead && (isBlinking || isFakeZombieVisible))
            CleanupParanoia();

        if (isDead && !hasTriggeredSpectate)
        {
            hasTriggeredSpectate = true; // Khóa lại, chỉ gọi 1 lần duy nhất

            // Ép tắt tâm ruồi ngắm bắn (Nếu có)
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

            // Gọi Camera tiến hành chuyển mục tiêu sang người sống sót đầu tiên
            var cam = FindFirstObjectByType<PZ_CameraController>();
            if (cam != null) cam.SpectateNext(0);
        }

        if (isBitten && !isDead)
        {
            if (blinkCooldown > 0) blinkCooldown -= Time.deltaTime;

            if (!isBlinking && blinkCooldown <= 0)
            {
                if (infectionTimer <= 420f && infectionTimer > 240f)
                {
                    StartCoroutine(nameof(ParanoiaBlinkRoutine));
                    blinkCooldown = 20f;
                }
                else if (infectionTimer <= 240f && infectionTimer > 180f)
                {
                    StartCoroutine(nameof(ParanoiaBlinkRoutine));
                    blinkCooldown = 8f;
                }
                else if (infectionTimer <= 180f && infectionTimer > 0f)
                {
                    StartCoroutine(nameof(ParanoiaBlinkRoutine));
                    blinkCooldown = Random.Range(5f, 7f);
                }
            }
        }

        if (isFakeZombieVisible && originalTeammateControllers.Count > 0)
        {
            foreach (var kvp in originalTeammateControllers)
            {
                Animator teammateAnim = kvp.Key;
                if (teammateAnim != null)
                {
                    Vector3 currentPos = teammateAnim.transform.position;
                    Vector3 lastPos = lastTeammatePositions.ContainsKey(teammateAnim) ? lastTeammatePositions[teammateAnim] : currentPos;
                    Vector3 movementDelta = currentPos - lastPos;
                    Vector3 velocity = movementDelta / Time.deltaTime;

                    lastTeammatePositions[teammateAnim] = currentPos;
                    float speed = velocity.magnitude;
                    teammateAnim.SetFloat(paramSpeed, speed);

                    if (speed > 0.1f)
                    {
                        teammateAnim.SetFloat(paramMoveX, velocity.normalized.x);
                        teammateAnim.SetFloat(paramMoveY, velocity.normalized.y);
                    }
                }
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        if (MainQuestManager.Instance != null && MainQuestManager.Instance.IsQuestCutsceneActive) return;

        if (isTransforming)
        {
            transformTimer -= Runner.DeltaTime;

            if (transformTimer <= 0)
            {
                isTransforming = false;

                if (zombieTurnPrefab.IsValid)
                {
                    Runner.Spawn(zombieTurnPrefab, transform.position, Quaternion.identity);
                }

                // 👇 THÊM ĐOẠN NÀY VÀO TRƯỚC KHI DESPAWN 👇
                if (HasInputAuthority)
                {
                    var cameraController = FindFirstObjectByType<PZ_CameraController>();
                    if (cameraController != null)
                    {
                        // Giật cam sang người sống sót đầu tiên tìm thấy trước khi xác này bay màu
                        PlayerHealth[] allPlayers = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
                        foreach (var p in allPlayers)
                        {
                            if (p != this && !p.isDead)
                            {
                                cameraController.SpectateTarget(p.transform);
                                break;
                            }
                        }
                    }
                }
                // 👆 KẾT THÚC THÊM 👆

                Runner.Despawn(Object);
            }
            return;
        }

        if (isDead) return;

        // VẾT CẮN RÚT MÁU KHI SẮP HÓA ZOMBIE
        if (isBitten)
        {
            float safeTimer = Mathf.Max(infectionTimer, Runner.DeltaTime);

            if (infectionTimer <= 180f && infectionTimer > 0f)
            {
                float bleedAmount = (currentHealth / safeTimer) * Runner.DeltaTime;
                currentHealth -= bleedAmount;
            }

            infectionTimer -= Runner.DeltaTime;

            if (infectionTimer <= 0)
            {
                infectionTimer = 0;
                currentHealth = 0;
                TriggerDeathLogic();
                return;
            }
        }

        // TỤT MÁU DO CHẢY MÁU CỦA VẾT THƯƠNG HỞ
        if (isBleeding)
        {
            currentHealth -= bleedDamagePerSecond * Runner.DeltaTime;
        }

        ApplyTutorialHealthFloor();

        if (!isBleeding && currentHealth < maxHealth && survivalSystem != null)
        {
            int wellFedTier = survivalSystem.GetWellFedTier();
            if (wellFedTier > 0)
            {
                currentHealth += GetPassiveHealRate(wellFedTier) * Runner.DeltaTime;
            }
        }

        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        if (currentHealth <= 0 && !isDead)
        {
            TriggerDeathLogic();
        }
    }

    public void TakeDamage(float damage, bool isStarving = false, bool isZombieAttack = false)
    {
        if (!HasStateAuthority) return;
        if (MainQuestManager.Instance != null && MainQuestManager.Instance.IsQuestCutsceneActive) return;

        // The vehicle body, not an invisible seated player body, receives
        // zombie contact. This authority-side guard also covers attacks that
        // were committed just before the player entered the vehicle.
        if (isZombieAttack && PlayerInteraction.IsProtectedOccupant(this)) return;

        if (isDead && !isTransforming) return;

        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        ApplyTutorialHealthFloor();

        // Repair interruption is decided by the same State Authority that
        // accepted this damage, so a client cannot hide a zombie hit locally.
        if (Object != null && Object.IsValid)
            MilitaryBaseQuestManager.Instance?.NotifyPlayerDamaged(Object.InputAuthority, isZombieAttack);

        if (isTransforming)
        {
            RPC_PlayHitEffect();
            if (currentHealth <= 0)
            {
                isTransforming = false;
                RPC_PlayDeathEffect();
            }
            return;
        }

        if (currentHealth <= 0)
        {
            TriggerDeathLogic();
            return;
        }

        if (!isStarving)
        {
            isBleeding = true;
            isInPain = true;

            RPC_PlayHitEffect();

            if (isZombieAttack)
            {
                RPC_TriggerUIInjury();
            }

            if (movementScript != null) movementScript.LockMovement(stunDuration);
        }
    }

    /// <summary>
    /// Authority-safe damage entry point for server-owned AI. In Host/Single it
    /// applies immediately; in other Fusion topologies it reaches this player's
    /// State Authority instead of being silently discarded.
    /// </summary>
    public void TakeDamageNetworked(float damage, bool isStarving = false, bool isZombieAttack = false)
    {
        if (HasStateAuthority)
        {
            TakeDamage(damage, isStarving, isZombieAttack);
            return;
        }

        RPC_RequestTakeDamage(damage, isStarving, isZombieAttack);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestTakeDamage(float damage, NetworkBool isStarving, NetworkBool isZombieAttack)
    {
        TakeDamage(damage, isStarving, isZombieAttack);
    }

    private void ApplyTutorialHealthFloor()
    {
        if (!TutorialSession.IsActive || !TutorialInputGate.HealthFloorEnabled || isDead) return;
        currentHealth = Mathf.Max(currentHealth, maxHealth * TutorialInputGate.MinimumHealthRatio);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    public void RPC_TriggerUIInjury()
    {
        if (AutoHealthPanel.Instance != null)
        {
            AutoHealthPanel.Instance.TakeRandomZombieAttack("");
        }
    }

    private void TriggerDeathLogic()
    {
        CleanupParanoia();
        isDead = true;

        if (isBitten)
        {
            isTransforming = true;
            transformTimer = 5f;
            currentHealth = 100f;
            RPC_PlayConvulseEffect();
        }
        else
        {
            RPC_PlayDeathEffect();
        }
    }

    public void SetGlobalBleeding(bool state)
    {
        if (HasStateAuthority) isBleeding = state;
        else RPC_SetGlobalBleeding(state);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetGlobalBleeding(bool state) { isBleeding = state; }

    public void SetBitten()
    {
        if (HasStateAuthority) isBitten = true;
        else RPC_SetBitten();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetBitten() { isBitten = true; }

    public void UsePainkiller()
    {
        if (HasStateAuthority) isInPain = false;
        else RPC_StopPain();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_StopPain() { isInPain = false; }

    public float GetSFXVolume()
    {
        float vol = PlayerPrefs.GetFloat("GameSFXVolume", 0.8f);
        if (AutoMainMenuManager.Instance != null)
        {
            vol = AutoMainMenuManager.Instance.sfxVolume;
        }
        return Mathf.Clamp01(vol);
    }

    private void EnsureAudioSource()
    {
        if (bodyStateAudioSource == null)
        {
            bodyStateAudioSource = gameObject.AddComponent<AudioSource>();
            bodyStateAudioSource.playOnAwake = false;
            bodyStateAudioSource.loop = false;
        }

        GameplayAudioSpatializer.Configure(bodyStateAudioSource, GameplayAudioSpatializer.Profile.Body);
    }

    private void PlayHurtGruntSFX()
    {
        EnsureAudioSource();

        // Nạp tự động từ Resources nếu chưa gán Inspector
        for (int i = 0; i < 5; i++)
        {
            if (hurtGruntSFXs[i] == null)
            {
                hurtGruntSFXs[i] = Resources.Load<AudioClip>($"Sound/BodyState/player_hurt_grunt_{i + 1}");
            }
        }

        // Chống spam tiếng kêu đau liên tục dồn dập
        if (Time.time - lastHurtSoundTime < 0.25f) return;

        // Chọn ngẫu nhiên 1 trong 5 biến thể (tránh lặp lại biến thể ngay trước đó)
        List<int> validIndices = new List<int>();
        for (int i = 0; i < 5; i++)
        {
            if (hurtGruntSFXs[i] != null && i != lastHurtIndex)
            {
                validIndices.Add(i);
            }
        }

        if (validIndices.Count == 0 && hurtGruntSFXs[0] != null) validIndices.Add(0);

        if (validIndices.Count > 0)
        {
            int pick = validIndices[Random.Range(0, validIndices.Count)];
            lastHurtIndex = pick;
            lastHurtSoundTime = Time.time;
            float finalVolume = GameplayAudioSpatializer.GetAttenuatedVolume(
                bodyStateAudioSource,
                GameplayAudioSpatializer.Profile.Body,
                GetSFXVolume(),
                HasInputAuthority);
            bodyStateAudioSource.PlayOneShot(hurtGruntSFXs[pick], finalVolume);
        }
    }

    private void PlayDeathSFX()
    {
        if (deathSFX == null) deathSFX = Resources.Load<AudioClip>("Sound/BodyState/player_death");

        if (deathSFX != null)
        {
            float sfxVol = GetSFXVolume();
            GameObject soundObj = new GameObject("Temp_PlayerDeathSFX");
            soundObj.transform.position = transform.position;
            AudioSource aSrc = soundObj.AddComponent<AudioSource>();
            aSrc.clip = deathSFX;
            GameplayAudioSpatializer.Configure(aSrc, GameplayAudioSpatializer.Profile.Body);
            aSrc.volume = GameplayAudioSpatializer.GetAttenuatedVolume(
                aSrc,
                GameplayAudioSpatializer.Profile.Body,
                sfxVol,
                HasInputAuthority);
            aSrc.playOnAwake = false;
            aSrc.Play();
            Destroy(soundObj, deathSFX.length + 0.2f);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_PlayHitEffect()
    {
        if (anim != null) anim.SetTrigger("TakeDamage");
        if (spriteRend != null && !isFlashing) StartCoroutine(FlashHurtRoutine());
        PlayHurtGruntSFX();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_PlayConvulseEffect()
    {
        if (anim != null) anim.SetBool("IsDead", true);

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;
        if (movementScript != null) movementScript.enabled = false;

        if (spriteRend != null) spriteRend.color = new Color(0.4f, 0.5f, 0.4f, 1f);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_PlayDeathEffect()
    {
        if (anim != null) anim.SetBool("IsDead", true);

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
        }
        if (movementScript != null) movementScript.enabled = false;

        foreach (Collider2D coll in GetComponentsInChildren<Collider2D>(true))
            coll.enabled = false;

        StopAllCoroutines();
        if (spriteRend != null) spriteRend.color = originalColor;

        PlayDeathSFX();
        StartCoroutine(BlinkAndVanishRoutine());
    }

    public void Heal(float amount)
    {
        if (isDead) return;
        if (HasStateAuthority) PerformHeal(amount);
        else RPC_RequestHeal(amount);
    }

    public float GetPassiveHealRate(int wellFedTier)
    {
        wellFedTier = Mathf.Clamp(wellFedTier, 1, 4);
        return passiveHealPerSecond + (wellFedTier - 1) * healBonusPerWellFedTier;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestHeal(float amount) { PerformHeal(amount); }

    private void PerformHeal(float amount)
    {
        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
    }

    private IEnumerator FlashHurtRoutine()
    {
        isFlashing = true;
        spriteRend.color = hurtColor;
        yield return new WaitForSeconds(flashDuration);

        spriteRend.color = isTransforming ? new Color(0.4f, 0.5f, 0.4f, 1f) : originalColor;
        isFlashing = false;
    }

    private IEnumerator BlinkAndVanishRoutine()
    {
        yield return new WaitForSeconds(1f);
        for (int i = 0; i < 5; i++)
        {
            if (spriteRend != null) spriteRend.enabled = false;
            yield return new WaitForSeconds(0.15f);
            if (spriteRend != null) spriteRend.enabled = true;
            yield return new WaitForSeconds(0.15f);
        }

        // 👇 XÓA HOẶC COMMENT DÒNG NÀY LẠI
        // gameObject.SetActive(false); 

        // 👇 THAY BẰNG DÒNG NÀY (Chỉ làm hình ảnh tàng hình, giữ lại code)
        if (spriteRend != null) spriteRend.enabled = false;
    }

    private IEnumerator ParanoiaBlinkRoutine()
    {
        if (paranoiaImage == null) yield break;

        isBlinking = true;

        Color clear = new Color(0, 0, 0, 0f);
        Color black = new Color(0, 0, 0, 1f);
        Color bloodRed = new Color(0.6f, 0f, 0f, 0.2f);

        yield return StartCoroutine(FadeColor(black, 0.5f));
        yield return new WaitForSeconds(0.1f);

        SwapTeammatesToZombies();

        yield return new WaitForSeconds(0.2f);

        yield return StartCoroutine(FadeColor(bloodRed, 0.5f));
        yield return new WaitForSeconds(4.5f);

        yield return StartCoroutine(FadeColor(black, 0.5f));
        yield return new WaitForSeconds(0.1f);

        RestoreTeammatesSprites();

        yield return new WaitForSeconds(0.2f);

        yield return StartCoroutine(FadeColor(clear, 0.55f));

        isBlinking = false;
    }

    private IEnumerator FadeColor(Color targetColor, float duration)
    {
        Color startColor = paranoiaImage.color;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            paranoiaImage.color = Color.Lerp(startColor, targetColor, elapsed / duration);
            yield return null;
        }

        paranoiaImage.color = targetColor;
    }

    private void SwapTeammatesToZombies()
    {
        if (zombieAnimatorController == null) return;
        originalTeammateControllers.Clear();
        hiddenNameTags.Clear();
        lastTeammatePositions.Clear();

        PlayerHealth[] allPlayers = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);

        foreach (var player in allPlayers)
        {
            if (player == this) continue;

            Animator teammateAnim = player.GetComponentInChildren<Animator>();
            PlayerNameTag nameTag = player.GetComponent<PlayerNameTag>();
            PlayerMovement pm = player.GetComponent<PlayerMovement>();

            if (teammateAnim != null)
            {
                originalTeammateControllers[teammateAnim] = teammateAnim.runtimeAnimatorController;
                teammateAnim.runtimeAnimatorController = zombieAnimatorController;
                lastTeammatePositions[teammateAnim] = teammateAnim.transform.position;
            }

            if (nameTag != null && nameTag.nameText != null)
            {
                nameTag.nameText.gameObject.SetActive(false);
                hiddenNameTags.Add(nameTag);
            }

            if (pm != null) pm.isParanoiaZombie = true;
        }
        isFakeZombieVisible = true;
    }

    private void RestoreTeammatesSprites()
    {
        isFakeZombieVisible = false;

        foreach (var kvp in originalTeammateControllers)
        {
            if (kvp.Key != null)
            {
                kvp.Key.runtimeAnimatorController = kvp.Value;
                PlayerMovement pm = kvp.Key.GetComponentInParent<PlayerMovement>();
                if (pm != null) pm.isParanoiaZombie = false;
            }
        }
        originalTeammateControllers.Clear();
        lastTeammatePositions.Clear();

        foreach (var tag in hiddenNameTags)
        {
            if (tag != null && tag.nameText != null) tag.nameText.gameObject.SetActive(true);
        }
        hiddenNameTags.Clear();
    }

    private void CleanupParanoia()
    {
        StopCoroutine(nameof(ParanoiaBlinkRoutine));
        RestoreTeammatesSprites();
        isBlinking = false;
        blinkCooldown = 0f;
        if (paranoiaImage != null) paranoiaImage.color = Color.clear;
    }

    private void OnDisable()
    {
        CleanupParanoia();
    }

    // ĐỂ TRỐNG THEO LỆNH SẾP (KHÔNG VẼ MOODLE RÁC)
    private void OnGUI() { }
}
