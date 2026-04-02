using UnityEngine;
using Fusion;
using System.Collections;
using System.Collections.Generic;

public class PlayerHealth : NetworkBehaviour
{
    [Header("Chỉ số Máu")]
    public float maxHealth = 100f;
    [Networked] public float currentHealth { get; set; }

    [Header("Hiệu ứng khi bị đánh")]
    public float stunDuration = 0.4f;
    public Color hurtColor = Color.red;
    public float flashDuration = 0.1f;

    // ==========================================
    // === THÊM CÀI ĐẶT HARDCORE SINH TỒN ===
    // ==========================================
    [Header("Cài đặt Hardcore PZ")]
    public float bleedDamagePerSecond = 1.5f;
    public float passiveHealPerSecond = 0.5f;

    [Networked] public NetworkBool isBleeding { get; set; }
    [Networked] public NetworkBool isInPain { get; set; }
    // ==========================================

    private PlayerMovement movementScript;
    private Animator anim;
    private SpriteRenderer spriteRend;
    private Color originalColor;
    private bool isFlashing = false;

    // === THÊM BIẾN SURVIVAL ĐỂ CHECK ĐÓI/KHÁT ===
    private PlayerSurvival survivalSystem;

    [Networked] public NetworkBool isDead { get; set; }

    public override void Spawned()
    {
        if (HasStateAuthority) currentHealth = maxHealth;

        movementScript = GetComponent<PlayerMovement>();
        anim = GetComponent<Animator>();
        spriteRend = GetComponentInChildren<SpriteRenderer>();

        survivalSystem = GetComponent<PlayerSurvival>();

        if (spriteRend != null) originalColor = spriteRend.color;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || isDead) return;

        // 1. Xử lý chảy máu
        if (isBleeding)
        {
            currentHealth -= bleedDamagePerSecond * Runner.DeltaTime;
            if (currentHealth <= 0 && !isDead)
            {
                isDead = true;
                RPC_PlayDeathEffect();
            }
        }

        // 2. Xử lý hồi máu thụ động
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

    // ==========================================
    // === THÊM CÁC HÀM XÓA DEBUFF TỪ ITEM ======
    // ==========================================
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
    // ==========================================

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

    // ==========================================
    // 🔥 ĐỒNG BỘ HIỂN THỊ DEBUFF CỦA ZOMBOID & THÊM LẠI ICON
    // ==========================================
    private void OnGUI()
    {
        if (!HasInputAuthority || isDead) return;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 24;
        style.fontStyle = FontStyle.Bold;

        int yPos = 50;
        int xPos = Screen.width - 250;

        // 1. KIỂM TRA ĐAU ĐỚN VÀ CHẢY MÁU CHUNG
        if (isInPain)
        {
            style.normal.textColor = Color.yellow;
            style.hover.textColor = Color.yellow;
            GUI.Label(new Rect(xPos, yPos, 250, 40), "⚡ Pain", style);
            yPos += 40;
        }

        if (isBleeding)
        {
            style.normal.textColor = Color.red;
            style.hover.textColor = Color.red;
            GUI.Label(new Rect(xPos, yPos, 250, 40), "🩸 Bleeding", style);
            yPos += 40;
        }

        // 2. LIÊN KẾT VỚI BẢNG HEALTH PANEL ĐỂ LẤY CÁC VẾT THƯƠNG CỤ THỂ (Scratched, Bitten...)
        if (AutoHealthPanel.Instance != null)
        {
            // Lấy danh sách TẤT CẢ các vết thương hiện có trên toàn thân (đã lược bỏ những chỗ được băng bó)
            List<AutoHealthPanel.InjuryType> activeGlobalInjuries = AutoHealthPanel.Instance.GetActiveGlobalInjuries();

            // Nếu trong danh sách đó có Scratched -> Hiện Scratched 1 lần duy nhất
            if (activeGlobalInjuries.Contains(AutoHealthPanel.InjuryType.Scratched))
            {
                style.normal.textColor = new Color(1f, 0.5f, 0.5f); // Đỏ nhạt
                GUI.Label(new Rect(xPos, yPos, 250, 40), "🩸 Scratched", style);
                yPos += 40;
            }

            // Nếu có Laceration -> Hiện 1 lần duy nhất
            if (activeGlobalInjuries.Contains(AutoHealthPanel.InjuryType.Laceration))
            {
                style.normal.textColor = Color.red; // Đỏ
                GUI.Label(new Rect(xPos, yPos, 250, 40), "🩸 Laceration", style);
                yPos += 40;
            }

            // Nếu có Bitten -> Hiện 1 lần duy nhất (Án tử)
            if (activeGlobalInjuries.Contains(AutoHealthPanel.InjuryType.Bitten))
            {
                style.normal.textColor = new Color(0.6f, 0f, 0f); // Đỏ thẫm
                GUI.Label(new Rect(xPos, yPos, 250, 40), "☠ BITTEN", style);
                yPos += 40;
            }
        }

        // 3. KIỂM TRA HỒI MÁU
        bool isHealing = false;
        if (!isBleeding && currentHealth < maxHealth && survivalSystem != null)
        {
            float hungerPct = survivalSystem.currentHunger / survivalSystem.maxHunger;
            float thirstPct = survivalSystem.currentThirst / survivalSystem.maxThirst;
            if (hungerPct >= 0.8f && thirstPct >= 0.8f) isHealing = true;
        }

        if (isHealing)
        {
            style.normal.textColor = Color.green;
            style.hover.textColor = Color.green;
            GUI.Label(new Rect(xPos, yPos, 250, 40), "💚 Healing...", style);
        }
    }
}