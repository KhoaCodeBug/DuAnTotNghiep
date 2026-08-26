/// <summary>
/// One local, non-networked policy for world-space interaction hints. A hint
/// must disappear while another UI owns attention, then naturally reappear if
/// the player is still inside its collider after that UI closes.
/// </summary>
public static class LocalGameplayUIState
{
    public static bool BlocksWorldInteractionHints
    {
        get
        {
            if (RouteBRadioBroadcastUI.BlocksLocalGameplayInput || EscapeRouteDecisionUI.IsVisible ||
                VehicleRepairSkillCheckUI.BlocksGameplayInput ||
                CivilianRoutePresentationController.BlocksGameplayInput)
                return true;

            if (AutoUIManager.Instance != null && AutoUIManager.Instance.IsAnyMenuOpen()) return true;
            if (QuestFlowUIPrototype.Instance != null && QuestFlowUIPrototype.Instance.IsQuestOverlayOpen) return true;
            if (AutoHealthPanel.Instance != null && AutoHealthPanel.Instance.IsOpen) return true;
            if (AutoChatManager.Instance != null && AutoChatManager.Instance.IsTyping()) return true;
            if (AutoMainMenuManager.Instance != null &&
                (AutoMainMenuManager.Instance.IsPauseMenuOpen || AutoMainMenuManager.Instance.IsPauseOptionsOpen))
                return true;

            return false;
        }
    }
}
