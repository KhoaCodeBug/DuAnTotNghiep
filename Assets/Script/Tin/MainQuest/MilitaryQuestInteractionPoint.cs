using UnityEngine;

/// <summary>Local input/presentation for authority-validated military interactions.</summary>
public sealed class MilitaryQuestInteractionPoint : MonoBehaviour
{
    private MilitaryBaseQuestManager manager;
    private MilitaryBaseQuestManager.InteractionKind kind;
    private string displayLabel;
    private Color color;

    public void Configure(MilitaryBaseQuestManager targetManager,
        MilitaryBaseQuestManager.InteractionKind targetKind, string label, Color markerColor)
    {
        manager = targetManager;
        kind = targetKind;
        displayLabel = label;
        color = markerColor;
    }

    private void Update()
    {
        if (!IsAvailable() || !IsLocalPlayerNear()) return;
        if (AutoUIManager.Instance != null && AutoUIManager.Instance.IsAnyMenuOpen()) return;
        if (!Input.GetKeyDown(KeyCode.E)) return;

        switch (kind)
        {
            case MilitaryBaseQuestManager.InteractionKind.Generator:
                manager.RequestActivateGenerator();
                break;
            case MilitaryBaseQuestManager.InteractionKind.Armory:
                manager.RequestUnlockArmory();
                break;
            case MilitaryBaseQuestManager.InteractionKind.BatteryCache:
                manager.RequestCollectPart(MilitaryQuestItemKind.Battery);
                break;
            case MilitaryBaseQuestManager.InteractionKind.FuelCache:
                manager.RequestCollectPart(MilitaryQuestItemKind.FuelCanister);
                break;
            case MilitaryBaseQuestManager.InteractionKind.RepairKitCache:
                manager.RequestCollectPart(MilitaryQuestItemKind.RepairKit);
                break;
            case MilitaryBaseQuestManager.InteractionKind.OfficeSafe:
                manager.RequestClaimOfficeSafe();
                break;
        }
    }

    private void OnGUI()
    {
        if (GameplayHudLayout.AreGameplayPromptsSuppressed()) return;
        if (!IsAvailable()) return;
        Camera camera = Camera.main;
        if (camera == null) return;
        Vector3 screen = camera.WorldToScreenPoint(transform.position);
        if (screen.z <= 0f) return;

        GUIStyle marker = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 24,
            fontStyle = FontStyle.Bold
        };
        marker.normal.textColor = color;
        GUI.Label(new Rect(screen.x - 18f, Screen.height - screen.y - 20f, 36f, 36f), "◆", marker);

        if (!IsLocalPlayerNear()) return;
        GUIStyle prompt = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 15,
            fontStyle = FontStyle.Bold
        };
        Rect promptRect = GameplayHudLayout.GetBottomCenterPromptRect(450f, 42f);
        string label = GameLocalization.Get(displayLabel, GameLocalization.TranslateLiteral(displayLabel));
        GUI.Box(promptRect, $"[E]  {GetPrompt()}  •  {label}", prompt);
    }

    private bool IsAvailable()
    {
        if (manager == null || !manager.IsNetworkReady) return false;
        switch (kind)
        {
            case MilitaryBaseQuestManager.InteractionKind.Generator:
                return manager.CurrentPhase == MilitaryBaseQuestManager.Phase.SiegeAndRepair &&
                       !manager.IsGeneratorActive;
            case MilitaryBaseQuestManager.InteractionKind.Armory:
                return manager.CurrentPhase >= MilitaryBaseQuestManager.Phase.SiegeAndRepair &&
                       manager.CurrentPhase < MilitaryBaseQuestManager.Phase.Escaped && !manager.IsArmoryUnlocked;
            case MilitaryBaseQuestManager.InteractionKind.BatteryCache:
                return manager.CurrentPhase == MilitaryBaseQuestManager.Phase.SiegeAndRepair &&
                       !manager.IsBatteryCacheClaimed;
            case MilitaryBaseQuestManager.InteractionKind.FuelCache:
                return manager.CurrentPhase == MilitaryBaseQuestManager.Phase.SiegeAndRepair &&
                       !manager.IsFuelCacheClaimed;
            case MilitaryBaseQuestManager.InteractionKind.RepairKitCache:
                return manager.CurrentPhase == MilitaryBaseQuestManager.Phase.SiegeAndRepair &&
                       !manager.IsRepairKitCacheClaimed;
            case MilitaryBaseQuestManager.InteractionKind.OfficeSafe:
                return MainQuestManager.Instance != null && MainQuestManager.Instance.IsCityMapUnlocked &&
                       !manager.IsOfficeSafeClaimed;
            case MilitaryBaseQuestManager.InteractionKind.ExitPoint:
                return manager.CurrentPhase == MilitaryBaseQuestManager.Phase.ReadyToEscape;
            default:
                return false;
        }
    }

    private bool IsLocalPlayerNear()
    {
        PlayerMovement player = PlayerMovement.LocalPlayerInstance;
        return player != null && Vector2.Distance(player.transform.position, transform.position) <=
               manager.InteractionDistance;
    }

    private string GetPrompt() => kind switch
    {
        MilitaryBaseQuestManager.InteractionKind.Generator => GameLocalization.Get("quest.military.interact_generator"),
        MilitaryBaseQuestManager.InteractionKind.Armory => GameLocalization.Get("quest.military.interact_armory"),
        MilitaryBaseQuestManager.InteractionKind.OfficeSafe => GameLocalization.Get("quest.military.interact_safe"),
        _ => GameLocalization.Get("quest.military.interact_collect")
    };
}
