using System.Collections;
using UnityEngine;

/// <summary>Physical/presentation gate whose canonical health is owned by MilitaryBaseQuestManager.</summary>
[RequireComponent(typeof(BoxCollider2D))]
public sealed class MilitaryGateController : MonoBehaviour
{
    [SerializeField] private float hordeDamagePerSecond = 22f;
    [SerializeField] private float electricStunSeconds = 1.25f;

    private MilitaryBaseQuestManager manager;
    private BoxCollider2D gateCollider;
    private SpriteRenderer sprite;

    public static MilitaryGateController Create(Transform parent, Vector2 position,
        MilitaryBaseQuestManager targetManager)
    {
        GameObject gate = new GameObject("Military Iron Gate");
        gate.transform.SetParent(parent, true);
        gate.transform.position = position;
        MilitaryGateController controller = gate.AddComponent<MilitaryGateController>();
        controller.manager = targetManager;
        controller.gateCollider = gate.GetComponent<BoxCollider2D>();
        controller.gateCollider.size = new Vector2(5.5f, 0.65f);
        controller.sprite = gate.AddComponent<SpriteRenderer>();
        controller.sprite.sprite = CreateGateSprite();
        controller.sprite.sortingOrder = 20;
        return controller;
    }

    public void TakeGateDamage(float damage) => manager?.TakeGateDamage(damage);

    public void ApplyHordePressure(float deltaSeconds, int attackerCount)
    {
        if (manager == null || !manager.HasStateAuthority || attackerCount <= 0) return;
        TakeGateDamage(hordeDamagePerSecond * Mathf.Min(attackerCount, 8) * deltaSeconds);
    }

    public void RefreshPresentation()
    {
        if (manager == null || !manager.IsNetworkReady || sprite == null) return;
        if (manager.IsGateBroken)
        {
            BreakGate();
            return;
        }

        bool shouldBeClosed = manager.CurrentPhase == MilitaryBaseQuestManager.Phase.SiegeAndRepair ||
                              manager.CurrentPhase == MilitaryBaseQuestManager.Phase.ReadyToEscape;
        if (gateCollider != null) gateCollider.enabled = shouldBeClosed;
        if (!shouldBeClosed)
        {
            sprite.color = new Color(0.55f, 0.62f, 0.55f, 0.22f);
            transform.localRotation = Quaternion.Euler(0f, 0f, 82f);
            return;
        }

        transform.localRotation = Quaternion.identity;

        float ratio = manager.GateMaxHealth > 0f ? manager.GateCurrentHealth / manager.GateMaxHealth : 0f;
        sprite.color = manager.IsGeneratorActive
            ? Color.Lerp(new Color(0.1f, 0.4f, 0.65f), new Color(0.25f, 0.95f, 1f), ratio)
            : Color.Lerp(new Color(0.5f, 0.08f, 0.05f), new Color(0.55f, 0.62f, 0.55f), ratio);
    }

    public void BreakGate()
    {
        if (gateCollider != null) gateCollider.enabled = false;
        if (sprite != null)
        {
            sprite.color = new Color(0.25f, 0.2f, 0.16f, 0.75f);
            transform.localRotation = Quaternion.Euler(0f, 0f, -12f);
        }
    }

    public void ElectrifyZombie(GameObject zombie)
    {
        if (manager == null || !manager.HasStateAuthority || !manager.IsGeneratorActive || zombie == null) return;
        SiegeZombieObjective objective = zombie.GetComponent<SiegeZombieObjective>();
        if (objective != null) objective.ApplyElectricStun(electricStunSeconds);
        else StartCoroutine(TemporarilyDisableZombieAI(zombie, electricStunSeconds));
    }

    private static IEnumerator TemporarilyDisableZombieAI(GameObject zombie, float duration)
    {
        MonoBehaviour[] behaviours = zombie.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
            if (IsZombieAI(behaviours[i])) behaviours[i].enabled = false;
        yield return new WaitForSeconds(duration);
        if (zombie == null) yield break;
        for (int i = 0; i < behaviours.Length; i++)
            if (behaviours[i] != null && IsZombieAI(behaviours[i])) behaviours[i].enabled = true;
    }

    private static bool IsZombieAI(MonoBehaviour behaviour) =>
        behaviour is ZombieAI || behaviour is ZOmbieAI_Khoa || behaviour is ZombieAIKhoaRebuilt;

    private static Sprite CreateGateSprite()
    {
        Texture2D texture = new Texture2D(96, 14, TextureFormat.RGBA32, false)
        {
            name = "MILITARY_GATE_RUNTIME",
            filterMode = FilterMode.Point,
            hideFlags = HideFlags.DontSave
        };
        Color32[] pixels = new Color32[96 * 14];
        Color32 clear = new Color32(0, 0, 0, 0);
        Color32 metal = new Color32(135, 148, 138, 255);
        for (int y = 0; y < 14; y++)
        for (int x = 0; x < 96; x++)
            pixels[y * 96 + x] = y < 2 || y > 11 || x % 12 < 3 ? metal : clear;
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Sprite result = Sprite.Create(texture, new Rect(0, 0, 96, 14), new Vector2(0.5f, 0.5f), 18f);
        result.hideFlags = HideFlags.DontSave;
        return result;
    }
}
