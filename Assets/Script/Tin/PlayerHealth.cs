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

    [Header("Hiệu ứng Hoang Tưởng")]
    // 🔥 CHỈ CẦN DUY NHẤT CÁI NÀY LÀ ĐỦ: File Animator Controller xịn của sếp
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

        if (Input.GetKeyDown(KeyCode.O) && !isBlinking)
        {
            StartCoroutine(ParanoiaBlinkRoutine());
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
        if (!HasStateAuthority || isDead) return;

        if (isBleeding)
        {
            currentHealth -= bleedDamagePerSecond * Runner.DeltaTime;
            if (currentHealth <= 0 && !isDead)
            {
                isDead = true;
                RPC_PlayDeathEffect();
            }
        }

        if (!isBleeding && currentHealth < maxHealth && survivalSystem != null)
        {
            float hungerPct = survivalSystem.currentHunger / survivalSystem.maxHunger;
            float thirstPct = survivalSystem.currentThirst / survivalSystem.maxThirst;

            if (hungerPct >= 0.8f && thirstPct >= 0.8f)
            {
                currentHealth += passiveHealPerSecond * Runner.DeltaTime;
                currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
            }
        }
    }

    public void TakeDamage(float damage, bool isStarving = false)
    {
        if (isDead || !HasStateAuthority) return;

        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        if (currentHealth <= 0)
        {
            isDead = true;
            RPC_PlayDeathEffect();
            return;
        }

        if (!isStarving)
        {
            isBleeding = true;
            isInPain = true;

            RPC_PlayHitEffect();
            if (movementScript != null) movementScript.LockMovement(stunDuration);
        }
    }

    public void SetGlobalBleeding(bool state)
    {
        if (HasStateAuthority) isBleeding = state;
        else RPC_SetGlobalBleeding(state);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetGlobalBleeding(bool state) { isBleeding = state; }

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
        spriteRend.color = originalColor;
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
        gameObject.SetActive(false);
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
        if (zombieAnimatorController == null)
        {
            Debug.LogError("Chưa gắn Zombie_AC vào Inspector kìa sếp!");
            return;
        }

        originalTeammateControllers.Clear();
        hiddenNameTags.Clear();
        lastTeammatePositions.Clear();

        PlayerHealth[] allPlayers = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);

        foreach (var player in allPlayers)
        {
            if (player == this) continue;

            Animator teammateAnim = player.GetComponentInChildren<Animator>();
            PlayerNameTag nameTag = player.GetComponent<PlayerNameTag>();

            if (teammateAnim != null)
            {
                originalTeammateControllers[teammateAnim] = teammateAnim.runtimeAnimatorController;
                teammateAnim.runtimeAnimatorController = zombieAnimatorController;

                // Xóa luôn dòng ép hình cũ vì Animator bây giờ tự lo phần Idle rồi
                lastTeammatePositions[teammateAnim] = teammateAnim.transform.position;
            }

            if (nameTag != null && nameTag.nameText != null)
            {
                nameTag.nameText.gameObject.SetActive(false);
                hiddenNameTags.Add(nameTag);
            }
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
            }
        }
        originalTeammateControllers.Clear();
        lastTeammatePositions.Clear();

        foreach (var tag in hiddenNameTags)
        {
            if (tag != null && tag.nameText != null)
            {
                tag.nameText.gameObject.SetActive(true);
            }
        }
        hiddenNameTags.Clear();
    }

    private void OnGUI()
    {
        // Trống trơn
    }
}