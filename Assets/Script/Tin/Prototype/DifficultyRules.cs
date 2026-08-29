using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Canonical Difficulty Rules and Single Source of Truth for game balance.
/// Dictates zombie spawn density, loot rates, incoming damage multipliers, and starter gear.
/// </summary>
public static class DifficultyRules
{
    public enum GameDifficulty
    {
        Easy = 0,
        Normal = 1,
        Hard = 2
    }

    private const string PreferenceKey = "GameDifficulty";

    private static int? sessionDifficultyOverride = null;

    /// <summary>
    /// Current canonical difficulty for the active session.
    /// In multiplayer, this is set authoritatively by the Host and replicated to all peers.
    /// In solo, it is set before loading the gameplay scene.
    /// </summary>
    public static int ActiveDifficulty
    {
        get
        {
            if (sessionDifficultyOverride.HasValue)
                return ClampDifficulty(sessionDifficultyOverride.Value);

            return ClampDifficulty(PlayerPrefs.GetInt(PreferenceKey, (int)GameDifficulty.Normal));
        }
        set
        {
            sessionDifficultyOverride = ClampDifficulty(value);
        }
    }

    public static bool HasSessionOverride => sessionDifficultyOverride.HasValue;

    public static void SetSessionDifficulty(int difficulty)
    {
        sessionDifficultyOverride = ClampDifficulty(difficulty);
    }

    public static void ResetSessionDifficulty()
    {
        sessionDifficultyOverride = null;
    }

    public static int ClampDifficulty(int difficulty)
    {
        return Mathf.Clamp(difficulty, 0, 2);
    }

    /// <summary>
    /// Easy: 0.5x (-50% spawn rate / density, 2.0x respawn cooldown)
    /// Normal: 1.0x (100% spawn rate / density, 1.0x cooldown)
    /// Hard: 2.5x (+150% spawn rate / density, 0.4x cooldown)
    /// </summary>
    public static float GetZombieDensityMultiplier(int difficulty)
    {
        return ClampDifficulty(difficulty) switch
        {
            0 => 0.5f,
            2 => 2.5f,
            _ => 1.0f
        };
    }

    /// <summary>
    /// Easy: 1.5x (150% loot rate)
    /// Normal: 1.0x (100% loot rate)
    /// Hard: 0.4x (40% loot rate)
    /// </summary>
    public static float GetLootRateMultiplier(int difficulty)
    {
        return ClampDifficulty(difficulty) switch
        {
            0 => 1.5f,
            2 => 0.4f,
            _ => 1.0f
        };
    }

    /// <summary>
    /// Easy: 0.7x (-30% damage taken)
    /// Normal: 1.0x (100% normal damage)
    /// Hard: 1.5x (+50% damage taken)
    /// </summary>
    public static float GetIncomingDamageMultiplier(int difficulty)
    {
        return ClampDifficulty(difficulty) switch
        {
            0 => 0.7f,
            2 => 1.5f,
            _ => 1.0f
        };
    }

    [Serializable]
    public struct StarterItem
    {
        public string ItemId;
        public int Amount;
        public bool PreferHotbar;

        public StarterItem(string itemId, int amount, bool preferHotbar = false)
        {
            ItemId = itemId;
            Amount = amount;
            PreferHotbar = preferHotbar;
        }
    }

    /// <summary>
    /// Easy: AK47 + 30 Ammo762 + 1 Meat (Starter assault rifle, 7.62mm ammunition, and sustenance)
    /// Normal: 1 Flashlight + 1 Bandage
    /// Hard: Empty (No starter gear)
    /// </summary>
    public static StarterItem[] GetStarterGearLoadout(int difficulty)
    {
        return ClampDifficulty(difficulty) switch
        {
            0 => new StarterItem[]
            {
                new StarterItem("AK47", 1, preferHotbar: true),
                new StarterItem("Ammo762", 30, preferHotbar: false),
                new StarterItem("Meat", 1, preferHotbar: false)
            },
            1 => new StarterItem[]
            {
                new StarterItem("Flashlight", 1, preferHotbar: false),
                new StarterItem("Bandage", 1, preferHotbar: false)
            },
            2 => Array.Empty<StarterItem>(),
            _ => Array.Empty<StarterItem>()
        };
    }

    public static string GetDifficultyName(int difficulty)
    {
        return ClampDifficulty(difficulty) switch
        {
            0 => "Easy",
            1 => "Normal",
            2 => "Hard",
            _ => "Normal"
        };
    }
}
