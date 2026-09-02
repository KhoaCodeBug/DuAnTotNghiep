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
            DeathCause.ZombieAttack => string.Format(GameLocalization.Get("chat.death.zombie"), safeVictim),
            DeathCause.Bleeding     => string.Format(GameLocalization.Get("chat.death.bleeding"), safeVictim),
            DeathCause.Infection    => string.Format(GameLocalization.Get("chat.death.infection"), safeVictim),
            DeathCause.Starvation   => string.Format(GameLocalization.Get("chat.death.starvation"), safeVictim),
            DeathCause.Dehydration  => string.Format(GameLocalization.Get("chat.death.dehydration"), safeVictim),
            DeathCause.PvP          => !string.IsNullOrWhiteSpace(safeKiller)
                                       ? string.Format(GameLocalization.Get("chat.death.pvp_killer"), safeVictim, safeKiller)
                                       : string.Format(GameLocalization.Get("chat.death.pvp_generic"), safeVictim),
            _                       => string.Format(GameLocalization.Get("chat.death.unknown"), safeVictim)
        };
    }

    public static string FormatJoinMessage(string playerName)
    {
        string safeName = SanitizeRichText(playerName);
        if (string.IsNullOrWhiteSpace(safeName)) safeName = "Survivor";
        return string.Format(GameLocalization.Get("chat.player_joined"), safeName);
    }

    public static string FormatLeftMessage(string playerName)
    {
        string safeName = SanitizeRichText(playerName);
        if (string.IsNullOrWhiteSpace(safeName)) safeName = "Survivor";
        return string.Format(GameLocalization.Get("chat.player_left"), safeName);
    }
}
