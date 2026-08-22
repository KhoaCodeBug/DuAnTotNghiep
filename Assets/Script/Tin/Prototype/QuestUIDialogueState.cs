/// <summary>
/// Assembly-safe bridge for local dialogue input ownership. The quest UI lives
/// in a named assembly and cannot reference presentation classes in Assembly-CSharp.
/// This state is local to the running client and is never network-replicated.
/// </summary>
public static class QuestUIDialogueState
{
    public static bool IsActive { get; private set; }

    public static void SetActive(bool active)
    {
        IsActive = active;
    }
}
