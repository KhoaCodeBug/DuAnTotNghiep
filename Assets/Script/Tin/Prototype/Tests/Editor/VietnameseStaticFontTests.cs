using System;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;

public sealed class VietnameseStaticFontTests
{
    private const string StaticFontPath = "Assets/Resources/Fonts/Vietnamese Static SDF.asset";
    private const string LiberationFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

    [Test]
    public void StaticFontContainsEveryVietnameseCodePointAndUsesNoDynamicAtlas()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(StaticFontPath);
        Assert.That(font, Is.Not.Null);
        Assert.That(font.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Static));
        Assert.That(font.isMultiAtlasTexturesEnabled, Is.False);
        Assert.That(font.atlasTextureCount, Is.EqualTo(1));

        uint[] required = BuildRequiredVietnameseSet();
        uint[] missing = required.Where(unicode => !font.HasCharacter((int)unicode)).ToArray();
        Assert.That(missing, Is.Empty, "Missing Vietnamese code points: " + FormatCodePoints(missing));
    }

    [Test]
    public void TmpSettingsAndLiberationUseSerializedStaticFallback()
    {
        TMP_FontAsset staticFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(StaticFontPath);
        TMP_FontAsset liberation = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LiberationFontPath);
        TMP_Settings settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);

        Assert.That(staticFont, Is.Not.Null);
        Assert.That(liberation, Is.Not.Null);
        Assert.That(settings, Is.Not.Null);
        Assert.That(TMP_Settings.defaultFontAsset, Is.SameAs(staticFont));
        Assert.That(TMP_Settings.fallbackFontAssets, Is.Empty,
            "The default static font must not point back to itself through global fallbacks.");
        Assert.That(liberation.fallbackFontAssetTable, Does.Contain(staticFont));
    }

    [Test]
    public void StaticFontContainsNonEmojiSymbolsUsedByGameUi()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(StaticFontPath);
        const string symbols = "°·×–—“”…•←↑→↓■□▼●";
        char[] missing = symbols.Where(symbol => !font.HasCharacter(symbol)).ToArray();
        Assert.That(missing, Is.Empty, "Missing UI symbols: " + string.Join(", ", missing));
    }

    private static uint[] BuildRequiredVietnameseSet()
    {
        System.Collections.Generic.SortedSet<uint> characters = new System.Collections.Generic.SortedSet<uint>();
        AddRange(characters, 0x0020, 0x007E);
        AddRange(characters, 0x00A0, 0x00FF);
        uint[] bases = { 0x0102, 0x0103, 0x0110, 0x0111, 0x0128, 0x0129, 0x0168, 0x0169, 0x01A0, 0x01A1, 0x01AF, 0x01B0 };
        foreach (uint unicode in bases) characters.Add(unicode);
        AddRange(characters, 0x1EA0, 0x1EF9);
        uint[] marks = { 0x0300, 0x0301, 0x0302, 0x0303, 0x0306, 0x0309, 0x031B, 0x0323 };
        foreach (uint unicode in marks) characters.Add(unicode);
        return characters.ToArray();
    }

    private static void AddRange(System.Collections.Generic.ISet<uint> set, uint first, uint last)
    {
        for (uint unicode = first; unicode <= last; unicode++) set.Add(unicode);
    }

    private static string FormatCodePoints(uint[] unicodes)
    {
        return string.Join(", ", unicodes.Select(unicode => "U+" + unicode.ToString("X4")));
    }
}
