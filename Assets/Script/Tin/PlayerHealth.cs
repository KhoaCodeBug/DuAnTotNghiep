using UnityEngine;
using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

public class PlayerHealth : NetworkBehaviour
{
    [Header("Chỉ số Máu")]
    public float maxHealth = 100f;
    [Networked] public float currentHealth { get; set; }

    [Header("Hiệu ứng khi bị đánh")]
    public float stunDuration = 0.4f;
    public Color hurtColor = Color.red;
    public float flashDuration = 0.1f;

    [Header("Cài đặt Hardcore PZ")]
    public float bleedDamagePerSecond = 1.5f;
    public float passiveHealPerSecond = 0.5f;

    [Networked] public NetworkBool isBleeding { get; set; }
    [Networked] public NetworkBool isInPain { get; set; }

    // ==========================================
    // 🔥 HỆ THỐNG NHIỄM TRÙNG & KẺ PHẢN BỘI
    // ==========================================
    [Header("Hệ thống Nhiễm Trùng")]
    [Networked] public float infectionTimer { get; set; } = 600f;
    [Networked] public NetworkBool isBitten { get; set; }

    private float blinkCooldown = 0f;

    [Header("Kẻ Phản Bội (Boss)")]
    public NetworkPrefabRef traitorBossPrefab;
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

    private Canvas paranoiaCanvas;
    private Image paranoiaImage;
    private bool isBlinking = false;

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
            SetupParanoiaUI();
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (paranoiaCanvas != null)
        {
            Destroy(paranoiaCanvas.gameObject);
        }
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

        if (isBitten && !isDead)
        {
            if (blinkCooldown > 0) blinkCooldown -= Time.deltaTime;

            if (!isBlinking && blinkCooldown <= 0)
            {
                if (infectionTimer <= 420f && infectionTimer > 240f)
                {
                    StartCoroutine(ParanoiaBlinkRoutine());
                    blinkCooldown = 20f;
                }
                else if (infectionTimer <= 240f && infectionTimer > 180f)
                {
                    StartCoroutine(ParanoiaBlinkRoutine());
                    blinkCooldown = 8f;
                }
                else if (infectionTimer <= 180f && infectionTimer > 0f)
                {
                    StartCoroutine(ParanoiaBlinkRoutine());
                    blinkCooldown = 20f;
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

        if (isTransforming)
        {
            transformTimer -= Runner.DeltaTime;

            if (transformTimer <= 0)
            {
                isTransforming = false;

                if (traitorBossPrefab.IsValid)
                {
                    Runner.Spawn(traitorBossPrefab, transform.position, Quaternion.identity);
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

        if (!isBleeding && currentHealth < maxHealth && survivalSystem != null)
        {
            float hungerPct = survivalSystem.currentHunger / survivalSystem.maxHunger;
            float thirstPct = survivalSystem.currentThirst / survivalSystem.maxThirst;

            if (hungerPct >= 0.8f && thirstPct >= 0.8f)
            {
                currentHealth += passiveHealPerSecond * Runner.DeltaTime;
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

        if (isDead && !isTransforming) return;

        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

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

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_PlayHitEffect()
    {
        if (anim != null) anim.SetTrigger("TakeDamage");
        if (spriteRend != null && !isFlashing) StartCoroutine(FlashHurtRoutine());
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
        if (rb != null) rb.linearVelocity = Vector2.zero;
        if (movementScript != null) movementScript.enabled = false;

        Collider2D coll = GetComponent<Collider2D>();
        if (coll != null) coll.enabled = false;

        StopAllCoroutines();
        if (spriteRend != null) spriteRend.color = originalColor;

        StartCoroutine(BlinkAndVanishRoutine());
    }

    public void Heal(float amount)
    {
        if (isDead) return;
        if (HasStateAuthority) PerformHeal(amount);
        else RPC_RequestHeal(amount);
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

    // ĐỂ TRỐNG THEO LỆNH SẾP (KHÔNG VẼ MOODLE RÁC)
    private void OnGUI() { }
}