using System;
using System.Collections;
using Fusion;
using UnityEngine;

/// <summary>Local hold-E interaction for the synchronized escape-vehicle repair.</summary>
public sealed class MilitaryEscapeVehicleRepair : MonoBehaviour
{
    private MilitaryBaseQuestManager manager;
    private SpriteRenderer sprite;
    private float nextProgressRequestTime;
    private bool interruptedUntilKeyRelease;
    private bool readyPresentation;
    private bool isEscaping;

    public static MilitaryEscapeVehicleRepair Create(Transform parent, Vector2 position,
        MilitaryBaseQuestManager targetManager)
    {
        GameObject vehicle = new GameObject("Military Escape Vehicle");
        vehicle.transform.SetParent(parent, true);
        vehicle.transform.position = position;
        MilitaryEscapeVehicleRepair repair = vehicle.AddComponent<MilitaryEscapeVehicleRepair>();
        repair.manager = targetManager;
        repair.sprite = vehicle.AddComponent<SpriteRenderer>();
        repair.sprite.sprite = CreateVehicleSprite();
        repair.sprite.sortingOrder = 18;
        return repair;
    }

    private void Update()
    {
        if (manager == null || !manager.IsNetworkReady || isEscaping) return;
        if (!Input.GetKey(KeyCode.E)) interruptedUntilKeyRelease = false;
        if (!IsLocalPlayerNear() || (AutoUIManager.Instance != null && AutoUIManager.Instance.IsAnyMenuOpen())) return;

        if ((manager.CurrentPhase == MilitaryBaseQuestManager.Phase.NotReached ||
             manager.CurrentPhase == MilitaryBaseQuestManager.Phase.Investigating) && Input.GetKeyDown(KeyCode.E))
        {
            RouteBRadioBroadcastUI.ShowCue(
                RouteBAudioCueId.AlarmPointOfNoReturn,
                () => EscapeRouteDecisionUI.ShowFinaleConfirmation(
                    EscapeEndingRoute.MilitaryEvacuation, manager.RequestTriggerAlarm));
            return;
        }

        if (manager.CurrentPhase == MilitaryBaseQuestManager.Phase.ReadyToEscape && Input.GetKeyDown(KeyCode.E))
        {
            manager.RequestEscape();
            return;
        }

        if (manager.CurrentPhase != MilitaryBaseQuestManager.Phase.SiegeAndRepair) return;
        if (!manager.HasAllParts)
        {
            if (!Input.GetKeyDown(KeyCode.E)) return;
            InventorySystem inventory = PlayerMovement.LocalPlayerInstance != null
                ? PlayerMovement.LocalPlayerInstance.GetComponent<InventorySystem>()
                : null;
            if (inventory == null) return;
            if (!manager.HasBatteryInstalled && inventory.HasItemNamed(MilitaryQuestItemCatalog.BatteryId))
                manager.RequestInstallPart(MilitaryQuestItemKind.Battery);
            else if (!manager.HasFuelInstalled && inventory.HasItemNamed(MilitaryQuestItemCatalog.FuelCanisterId))
                manager.RequestInstallPart(MilitaryQuestItemKind.FuelCanister);
            else if (!manager.HasRepairKitInstalled && inventory.HasItemNamed(MilitaryQuestItemCatalog.RepairKitId))
                manager.RequestInstallPart(MilitaryQuestItemKind.RepairKit);
            return;
        }

        if (interruptedUntilKeyRelease || !Input.GetKey(KeyCode.E) || Time.unscaledTime < nextProgressRequestTime) return;
        nextProgressRequestTime = Time.unscaledTime + 0.1f;
        manager.RequestProgressRepair(0.1f);
    }

    public void InterruptRepairFor(PlayerRef player)
    {
        if (manager != null && manager.Runner != null && manager.Runner.LocalPlayer == player)
            interruptedUntilKeyRelease = true;
    }

    public void SetVehicleReadyPresentation(bool ready)
    {
        readyPresentation = ready;
        RefreshPresentation();
    }

    public void PlayEscapeCutscene(Action onComplete)
    {
        if (isEscaping) return;
        StartCoroutine(EscapeRoutine(onComplete));
    }

    private IEnumerator EscapeRoutine(Action onComplete)
    {
        isEscaping = true;
        Vector3 start = transform.position;
        Vector3 end = new Vector3(manager.EscapeExitPosition.x, manager.EscapeExitPosition.y, start.z);
        const float duration = 2.25f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            transform.position = Vector3.LerpUnclamped(start, end, t);
            yield return null;
        }

        transform.position = end;
        onComplete?.Invoke();
    }

    public void RefreshPresentation()
    {
        if (sprite == null || manager == null || !manager.IsNetworkReady) return;
        bool ready = readyPresentation || manager.CurrentPhase == MilitaryBaseQuestManager.Phase.ReadyToEscape ||
                     manager.CurrentPhase == MilitaryBaseQuestManager.Phase.Escaped;
        sprite.color = ready
            ? new Color(0.25f, 1f, 0.48f)
            : manager.HasAllParts ? new Color(0.95f, 0.78f, 0.2f) : new Color(0.38f, 0.48f, 0.35f);
    }

    private void OnGUI()
    {
        if (GameplayHudLayout.AreGameplayPromptsSuppressed()) return;
        if (manager == null || !manager.IsNetworkReady || isEscaping || !IsLocalPlayerNear()) return;
        string prompt = GetPrompt();
        if (string.IsNullOrEmpty(prompt)) return;

        GUIStyle style = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 15,
            fontStyle = FontStyle.Bold
        };
        Rect promptRect = GameplayHudLayout.GetBottomCenterPromptRect(520f, 42f);
        GUI.Box(promptRect, prompt, style);

        if (manager.CurrentPhase == MilitaryBaseQuestManager.Phase.SiegeAndRepair && manager.HasAllParts)
        {
            Rect bar = GameplayHudLayout.GetProgressBarRectAbovePrompt(promptRect, 420f, 24f);
            GUI.Box(bar, GUIContent.none);
            float width = (bar.width - 6f) * Mathf.Clamp01(manager.VehicleRepairProgress / 100f);
            Color previous = GUI.color;
            GUI.color = new Color(0.22f, 0.85f, 0.42f);
            GUI.DrawTexture(new Rect(bar.x + 3f, bar.y + 3f, width, bar.height - 6f), Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Label(bar, $"SỬA XE  {manager.VehicleRepairProgress:0}%", style);
        }
    }

    private string GetPrompt()
    {
        switch (manager.CurrentPhase)
        {
            case MilitaryBaseQuestManager.Phase.NotReached:
            case MilitaryBaseQuestManager.Phase.Investigating:
                return "[E]  KIỂM TRA XE QUÂN SỰ";
            case MilitaryBaseQuestManager.Phase.SiegeAndRepair:
                if (!manager.HasAllParts)
                    return "[E]  LẮP PHỤ TÙNG  •  " + GetPartStatus();
                return interruptedUntilKeyRelease
                    ? "THẢ [E] RỒI GIỮ LẠI ĐỂ TIẾP TỤC SỬA"
                    : "GIỮ [E]  SỬA XE";
            case MilitaryBaseQuestManager.Phase.ReadyToEscape:
                return "[E]  TẬP HỢP ĐỘI VÀ THOÁT KHỎI CĂN CỨ";
            default:
                return string.Empty;
        }
    }

    private string GetPartStatus() =>
        $"Ắc quy {(manager.HasBatteryInstalled ? "■" : "□")}  " +
        $"Nhiên liệu {(manager.HasFuelInstalled ? "■" : "□")}  " +
        $"Bộ sửa {(manager.HasRepairKitInstalled ? "■" : "□")}";

    private bool IsLocalPlayerNear()
    {
        PlayerMovement player = PlayerMovement.LocalPlayerInstance;
        return player != null && Vector2.Distance(player.transform.position, transform.position) <=
               manager.InteractionDistance;
    }

    private static Sprite CreateVehicleSprite()
    {
        Texture2D texture = new Texture2D(52, 30, TextureFormat.RGBA32, false)
        {
            name = "MILITARY_ESCAPE_VEHICLE_RUNTIME",
            filterMode = FilterMode.Point,
            hideFlags = HideFlags.DontSave
        };
        Color32 clear = new Color32(0, 0, 0, 0);
        Color32 body = new Color32(78, 104, 66, 255);
        Color32 dark = new Color32(22, 28, 23, 255);
        Color32[] pixels = new Color32[52 * 30];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
        for (int y = 5; y < 25; y++)
        for (int x = 4; x < 48; x++) pixels[y * 52 + x] = body;
        for (int y = 1; y < 8; y++)
        for (int x = 8; x < 18; x++) pixels[y * 52 + x] = dark;
        for (int y = 22; y < 29; y++)
        for (int x = 34; x < 44; x++) pixels[y * 52 + x] = dark;
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Sprite result = Sprite.Create(texture, new Rect(0, 0, 52, 30), new Vector2(0.5f, 0.5f), 22f);
        result.hideFlags = HideFlags.DontSave;
        return result;
    }
}
