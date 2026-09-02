using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Small language bridge owned by the QuestUI assembly. Assembly-CSharp pushes
/// changes from GameLocalization into this bridge, while EditMode tests can use
/// the journal without creating an assembly dependency cycle.
/// </summary>
public static class QuestUILocalization
{
    private const string PreferenceKey = "GameLanguage";

    public static event Action LanguageChanged;
    public static bool IsVietnamese { get; private set; } = ReadInitialLanguage();

    public static void SetVietnamese(bool value)
    {
        if (IsVietnamese == value) return;
        IsVietnamese = value;
        LanguageChanged?.Invoke();
    }

    private static Func<string, string, string> stringLookup;

    public static void SetStringLookup(Func<string, string, string> lookup)
    {
        stringLookup = lookup;
    }

    public static string Get(string key, string fallback = null)
    {
        if (stringLookup != null)
        {
            return stringLookup(key, fallback);
        }

        Type locType = Type.GetType("GameLocalization, Assembly-CSharp");
        if (locType != null)
        {
            MethodInfo getMethod = locType.GetMethod("Get", new[] { typeof(string), typeof(string) });
            if (getMethod != null)
            {
                return (string)getMethod.Invoke(null, new object[] { key, fallback });
            }
        }

        return fallback ?? key;
    }

    private static bool ReadInitialLanguage()
    {
        int fallback = Application.systemLanguage == SystemLanguage.Vietnamese ? 1 : 0;
        return Mathf.Clamp(PlayerPrefs.GetInt(PreferenceKey, fallback), 0, 1) == 1;
    }
}
