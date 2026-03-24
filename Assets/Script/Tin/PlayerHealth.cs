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

    private PlayerMovement movementScript;
    private Animator anim;
    private SpriteRenderer spriteRend;
    private Color originalColor;
    private bool isFlashing = false;

    [Networked] public NetworkBool isDead { get; set; }

    public override void Spawned()
    {
        if (HasStateAuthority) currentHealth = maxHealth;

        movementScript = GetComponent<PlayerMovement>();
        anim = GetComponent<Animator>();
        spriteRend = GetComponentInChildren<SpriteRenderer>();

        if (spriteRend != null) originalColor = spriteRend.color;
    }

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
            RPC_PlayHitEffect();
            if (movementScript != null) movementScript.LockMovement(stunDuration);
        }
    }

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
}