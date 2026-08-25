using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Reproducibly bakes the project's Vietnamese TextMesh Pro fallback.
/// The generated atlas is static so builds never depend on machine-local glyph generation.
/// </summary>
public static class VietnameseStaticFontSetup
{
    public const string StaticFontAssetPath = "Assets/Resources/Fonts/Vietnamese Static SDF.asset";

    private const string LegacyDynamicFontAssetPath = "Assets/Resources/Fonts/VietnameseDynamic SDF.asset";
    private const string SourceFontPath = "Assets/TextMesh Pro/Fonts/LiberationSans.ttf";
    private const string LiberationFontAssetPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
    private const int AtlasSize = 2048;
    private const int AtlasPadding = 9;

    // Non-ASCII symbols used by TextMesh Pro runtime UI under Assets/Script/Tin.
    // Symbols used only by IMGUI or debug logs are outside the TMP atlas.
    private const string UsedSymbols = "°·×–—“”…•←↑→↓■□▼●";

    [MenuItem("Tools/TextMesh Pro/Rebuild Vietnamese Static Font")]
    public static void RebuildFromMenu()
    {
        string summary = BuildAndConfigure();
        Debug.Log(summary);
    }

    public static string BuildAndConfigure()
    {
        EnsureStaticAssetPath();

        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(StaticFontAssetPath);
        if (sourceFont == null)
            throw new InvalidOperationException("Missing source font: " + SourceFontPath);
        if (fontAsset == null)
            throw new InvalidOperationException("Missing TMP font asset: " + StaticFontAssetPath);

        ConfigureForBake(fontAsset, sourceFont);
        uint[] requested = BuildBakeCharacterSet().ToArray();
        fontAsset.ClearFontAssetData(false);
        fontAsset.TryAddCharacters(requested, out uint[] missingFromSource, true);

        uint[] missingVietnamese = BuildRequiredVietnameseSet()
            .Where(unicode => !fontAsset.HasCharacter((int)unicode))
            .ToArray();
        if (missingVietnamese.Length > 0)
        {
            throw new InvalidOperationException(
                "Liberation Sans is missing required Vietnamese characters: " + FormatCodePoints(missingVietnamese));
        }

        SetStaticMode(fontAsset);
        ConfigureFixedFallbacks(fontAsset);
        RenameSubAssets(fontAsset);
        EditorUtility.SetDirty(fontAsset);
        foreach (Texture2D atlas in fontAsset.atlasTextures)
            if (atlas != null) EditorUtility.SetDirty(atlas);
        if (fontAsset.material != null) EditorUtility.SetDirty(fontAsset.material);

        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(StaticFontAssetPath, ImportAssetOptions.ForceUpdate);

        int missingOptionalCount = missingFromSource == null ? 0 : missingFromSource.Length;
        return $"Baked {fontAsset.characterTable.Count} characters into a fixed {AtlasSize}x{AtlasSize} " +
               $"Vietnamese SDF atlas. Required Vietnamese missing: 0. Optional/source-missing: {missingOptionalCount}.";
    }

    public static IReadOnlyCollection<uint> BuildRequiredVietnameseSet()
    {
        SortedSet<uint> characters = new SortedSet<uint>();
        AddRange(characters, 0x0020, 0x007E); // Basic Latin used by every UI flow.
        AddRange(characters, 0x00A0, 0x00FF); // Latin-1 punctuation and accented letters.

        uint[] vietnameseBases =
        {
            0x0102, 0x0103, 0x0110, 0x0111, 0x0128, 0x0129,
            0x0168, 0x0169, 0x01A0, 0x01A1, 0x01AF, 0x01B0
        };
        foreach (uint unicode in vietnameseBases) characters.Add(unicode);
        AddRange(characters, 0x1EA0, 0x1EF9); // All precomposed Vietnamese tone characters.

        // Support normalized/decomposed Vietnamese text entered through chat or player names.
        uint[] combiningMarks = { 0x0300, 0x0301, 0x0302, 0x0303, 0x0306, 0x0309, 0x031B, 0x0323 };
        foreach (uint unicode in combiningMarks) characters.Add(unicode);
        return characters;
    }

    public static IReadOnlyCollection<uint> BuildBakeCharacterSet()
    {
        SortedSet<uint> characters = new SortedSet<uint>(BuildRequiredVietnameseSet());
        AddRange(characters, 0x0100, 0x017F); // Latin Extended-A.
        AddRange(characters, 0x1E00, 0x1EFF); // Latin Extended Additional.
        foreach (char symbol in UsedSymbols) characters.Add(symbol);
        return characters;
    }

    private static void EnsureStaticAssetPath()
    {
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(StaticFontAssetPath) != null) return;

        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LegacyDynamicFontAssetPath) != null)
        {
            string moveError = AssetDatabase.MoveAsset(LegacyDynamicFontAssetPath, StaticFontAssetPath);
            if (!string.IsNullOrEmpty(moveError))
                throw new InvalidOperationException("Could not rename legacy TMP asset: " + moveError);
            return;
        }

        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        if (sourceFont == null)
            throw new InvalidOperationException("Missing source font: " + SourceFontPath);

        TMP_FontAsset created = TMP_FontAsset.CreateFontAsset(
            sourceFont, 90, AtlasPadding, GlyphRenderMode.SDFAA,
            AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, false);
        created.name = "Vietnamese Static SDF";
        AssetDatabase.CreateAsset(created, StaticFontAssetPath);
        foreach (Texture2D atlas in created.atlasTextures)
            AssetDatabase.AddObjectToAsset(atlas, created);
        AssetDatabase.AddObjectToAsset(created.material, created);
        AssetDatabase.SaveAssets();
    }

    private static void ConfigureForBake(TMP_FontAsset fontAsset, Font sourceFont)
    {
        SerializedObject serialized = new SerializedObject(fontAsset);
        SetInteger(serialized, "m_AtlasPopulationMode", (int)AtlasPopulationMode.Dynamic);
        SetObject(serialized, "m_SourceFontFile", sourceFont);
        SetObject(serialized, "m_SourceFontFile_EditorRef", sourceFont, false);
        SetInteger(serialized, "m_AtlasWidth", AtlasSize);
        SetInteger(serialized, "m_AtlasHeight", AtlasSize);
        SetInteger(serialized, "m_AtlasPadding", AtlasPadding);
        SetInteger(serialized, "m_AtlasRenderMode", (int)GlyphRenderMode.SDFAA);
        SetBoolean(serialized, "m_IsMultiAtlasTexturesEnabled", false);
        SetBoolean(serialized, "m_ClearDynamicDataOnBuild", false);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        fontAsset.ReadFontAssetDefinition();
    }

    private static void SetStaticMode(TMP_FontAsset fontAsset)
    {
        fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
        fontAsset.isMultiAtlasTexturesEnabled = false;

        SerializedObject serialized = new SerializedObject(fontAsset);
        SetBoolean(serialized, "m_ClearDynamicDataOnBuild", false);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        fontAsset.ReadFontAssetDefinition();
    }

    private static void ConfigureFixedFallbacks(TMP_FontAsset staticFont)
    {
        TMP_FontAsset liberation = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LiberationFontAssetPath);
        if (liberation == null)
            throw new InvalidOperationException("Missing primary TMP font: " + LiberationFontAssetPath);

        liberation.fallbackFontAssetTable.Clear();
        liberation.fallbackFontAssetTable.Add(staticFont);
        EditorUtility.SetDirty(liberation);

        TMP_Settings settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
        if (settings == null)
            throw new InvalidOperationException("Missing TMP Settings: " + TmpSettingsPath);

        SerializedObject serializedSettings = new SerializedObject(settings);
        SetObject(serializedSettings, "m_defaultFontAsset", staticFont);
        SerializedProperty fallbacks = serializedSettings.FindProperty("m_fallbackFontAssets");
        if (fallbacks == null)
            throw new InvalidOperationException("TMP Settings has no m_fallbackFontAssets property.");
        // The static asset is already the default. Keep the global fallback list
        // empty to avoid a self-reference; legacy Liberation uses a serialized
        // one-way fallback to this asset instead.
        fallbacks.arraySize = 0;
        serializedSettings.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
    }

    private static void RenameSubAssets(TMP_FontAsset fontAsset)
    {
        fontAsset.name = "Vietnamese Static SDF";
        for (int i = 0; i < fontAsset.atlasTextures.Length; i++)
        {
            Texture2D atlas = fontAsset.atlasTextures[i];
            if (atlas != null) atlas.name = i == 0 ? "Vietnamese Static SDF Atlas" : $"Vietnamese Static SDF Atlas {i}";
        }
        if (fontAsset.material != null) fontAsset.material.name = "Vietnamese Static SDF Material";
    }

    private static void AddRange(ISet<uint> set, uint first, uint last)
    {
        for (uint unicode = first; unicode <= last; unicode++) set.Add(unicode);
    }

    private static string FormatCodePoints(IEnumerable<uint> unicodes)
    {
        return string.Join(", ", unicodes.Select(unicode => "U+" + unicode.ToString("X4")));
    }

    private static void SetInteger(SerializedObject serialized, string propertyName, int value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null) throw new InvalidOperationException("Missing serialized property: " + propertyName);
        property.intValue = value;
    }

    private static void SetBoolean(SerializedObject serialized, string propertyName, bool value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null) throw new InvalidOperationException("Missing serialized property: " + propertyName);
        property.boolValue = value;
    }

    private static void SetObject(SerializedObject serialized, string propertyName, UnityEngine.Object value, bool required = true)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            if (required) throw new InvalidOperationException("Missing serialized property: " + propertyName);
            return;
        }
        property.objectReferenceValue = value;
    }
}
