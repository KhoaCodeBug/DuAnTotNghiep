using UnityEngine;
using Fusion;
using System.Collections;

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

        // Lấy component sinh tồn
        survivalSystem = GetComponent<PlayerSurvival>();

        if (spriteRend != null) originalColor = spriteRend.color;
    }

    // ==========================================================
    // === THÊM FIXEDUPDATENETWORK ĐỂ XỬ LÝ MÁU THEO THỜI GIAN ===
    // ==========================================================
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

        // 2. Xử lý hồi máu thụ động (Không chảy máu + Ăn uống > 80%)
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
    // ==========================================================

    // Có thêm biến isStarving để chặn hiệu ứng giật mình nếu bị trừ máu do đói khát
    public void TakeDamage(float damage, bool isStarving = false)
    {
        // Chết rồi hoặc không phải Server thì nghỉ
        if (isDead || !HasStateAuthority) return;

        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        if (currentHealth <= 0)
        {
            isDead = true;
            RPC_PlayDeathEffect(); // Báo cả làng tui chết rồi
            return;
        }

        // Nếu bị chém thật (không phải do đói) thì báo cả làng bật hiệu ứng chớp đỏ
        if (!isStarving)
        {
            // === THÊM DEBUFF KHI BỊ ZOMBIE ĐÁNH ===
            isBleeding = true;
            isInPain = true;
            // ======================================

            RPC_PlayHitEffect();
            if (movementScript != null) movementScript.LockMovement(stunDuration);
        }
    }

    // ==========================================
    // === THÊM CÁC HÀM XÓA DEBUFF TỪ ITEM ======
    // ==========================================
    public void UseBandage()
    {
        if (HasStateAuthority) isBleeding = false;
        else RPC_StopBleeding();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_StopBleeding() { isBleeding = false; }

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
    // === THÊM: HIỆN TRẠNG THÁI KIỂU ZOMBOID ===
    // ==========================================
    private void OnGUI()
    {
        // Chỉ vẽ UI cho máy của người đang chơi
        if (!HasInputAuthority || isDead) return;

        // Chỉnh font chữ to, in đậm
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 24;
        style.fontStyle = FontStyle.Bold;

        int yPos = 50; // Góc trên bên phải
        int xPos = Screen.width - 250;

        // 1. Nếu đang chảy máu -> Hiện chữ ĐỎ
        if (isBleeding)
        {
            style.normal.textColor = Color.red;
            style.hover.textColor = Color.red;

            GUI.Label(new Rect(xPos, yPos, 250, 40), "🩸 CHẢY MÁU!", style);
            yPos += 40; // Đẩy dòng tiếp theo xuống dưới
        }

        // 2. Nếu đang bị đau -> Hiện chữ VÀNG
        if (isInPain)
        {
            style.normal.textColor = Color.yellow;
            style.hover.textColor = Color.yellow;

            GUI.Label(new Rect(xPos, yPos, 250, 40), "⚡ ĐAU ĐỚN!", style);
            yPos += 40; // Đẩy dòng tiếp theo xuống dưới
        }

        // 3. KIỂM TRA ĐIỀU KIỆN HỒI MÁU THỤ ĐỘNG
        bool isHealing = false;
        if (!isBleeding && currentHealth < maxHealth && survivalSystem != null)
        {
            float hungerPct = survivalSystem.currentHunger / survivalSystem.maxHunger;
            float thirstPct = survivalSystem.currentThirst / survivalSystem.maxThirst;

            // Đói và Khát phải trên 80% thì mới đang hồi máu
            if (hungerPct >= 0.8f && thirstPct >= 0.8f)
            {
                isHealing = true;
            }
        }

        // Nếu đủ điều kiện hồi máu -> Hiện chữ XANH LÁ
        if (isHealing)
        {
            style.normal.textColor = Color.green;
            style.hover.textColor = Color.green;

            GUI.Label(new Rect(xPos, yPos, 250, 40), "💚 ĐANG HỒI MÁU...", style);
        }
    }
}