/// <summary>
/// Assembly-safe bridge for local modal input ownership. The quest UI lives in
/// a named assembly and cannot reference presentation classes in Assembly-CSharp.
/// These states are local to the running client and are never network-replicated.
/// </summary>
public static class QuestUIDialogueState
{
    private static bool dialogueActive;
    private static bool repairActive;

    public static bool IsActive => dialogueActive || repairActive;

    public static void SetActive(bool active)
    {
        dialogueActive = active;
    }

    public static void SetRepairActive(bool active) => repairActive = active;
}
