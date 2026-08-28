using System.Text.RegularExpressions;
using Fusion;

public enum DeathCause
{
    Unknown = 0,
    ZombieAttack = 1,
    Bleeding = 2,
    Infection = 3,
    Starvation = 4,
    Dehydration = 5,
    PvP = 6
}

public static class PlayerDeathContext
{
    public const string SystemColorHex = "#FFD54A";

    public static string SanitizeRichText(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        return Regex.Replace(input, "<.*?>", string.Empty).Trim();
    }

    public static string FormatDeathMessage(string victimName, DeathCause cause, string killerName = null)
    {
        string safeVictim = SanitizeRichText(victimName);
        if (string.IsNullOrWhiteSpace(safeVictim)) safeVictim = "Survivor";

        string safeKiller = SanitizeRichText(killerName);

        return cause switch
        {
            DeathCause.ZombieAttack => $"{safeVictim} đã chết vì bị zombie tấn công.",
            DeathCause.Bleeding     => $"{safeVictim} đã chết vì mất máu.",
            DeathCause.Infection    => $"{safeVictim} đã chết vì nhiễm trùng.",
            DeathCause.Starvation   => $"{safeVictim} đã chết vì đói.",
            DeathCause.Dehydration  => $"{safeVictim} đã chết vì khát.",
            DeathCause.PvP          => !string.IsNullOrWhiteSpace(safeKiller)
                                       ? $"{safeVictim} đã bị {safeKiller} hạ gục."
                                       : $"{safeVictim} đã bị người chơi khác hạ gục.",
            _                       => $"{safeVictim} đã tử vong."
        };
    }

    public static string FormatJoinMessage(string playerName)
    {
        string safeName = SanitizeRichText(playerName);
        if (string.IsNullOrWhiteSpace(safeName)) safeName = "Survivor";
        return $"{safeName} đã vào trận.";
    }
}
