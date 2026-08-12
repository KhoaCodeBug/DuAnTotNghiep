using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>Client-local language service. Network messages should carry keys/data, not translated text.</summary>
public static class GameLocalization
{
    public enum Language { English = 0, Vietnamese = 1 }

    private const string PreferenceKey = "GameLanguage";
    public static event Action LanguageChanged;
    public static Language Current { get; private set; } = ReadInitialLanguage();
    public static bool IsVietnamese => Current == Language.Vietnamese;
    public static string CurrentLabel => IsVietnamese ? "TIẾNG VIỆT" : "ENGLISH";

    private static readonly Dictionary<string, string[]> Text = new Dictionary<string, string[]>
    {
        { "spectator.dead", new[] { "YOU DIED. SPECTATING TEAMMATES.\nUse A/D or click to cycle.", "BẠN ĐÃ CHẾT. ĐANG THEO DÕI ĐỒNG ĐỘI.\nDùng A/D hoặc nhấp chuột để chuyển." } },
        { "respawn.action", new[] { "RESPAWN", "HỒI SINH" } },
        { "respawn.waiting", new[] { "RESPAWNING...", "ĐANG HỒI SINH..." } },
        { "respawn.failed", new[] { "RESPAWN FAILED - TRY AGAIN", "HỒI SINH THẤT BẠI - THỬ LẠI" } },
        { "noise.title", new[] { "NOISE", "ĐỘ ỒN" } },
        { "noise.silent", new[] { "SILENT", "YÊN LẶNG" } },
        { "noise.running", new[] { "RUNNING", "CHẠY" } },
        { "noise.footsteps", new[] { "FOOTSTEPS", "BƯỚC CHÂN" } },
        { "noise.voice", new[] { "NEARBY VOICE", "GIỌNG NÓI LÂN CẬN" } },
        { "intro.next", new[] { "[E] Next", "[E] Tiếp" } },
        { "intro.leave", new[] { "[E] Leave vehicle", "[E] Rời xe" } },
        { "intro.fallback", new[] { "The car died. I need to inspect it.", "Xe đã chết máy. Phải xuống kiểm tra thôi." } },
        { "settings.language", new[] { "LANGUAGE:", "NGÔN NGỮ:" } },
        { "settings.english", new[] { "ENGLISH", "TIẾNG ANH" } },
        { "settings.vietnamese", new[] { "VIETNAMESE", "TIẾNG VIỆT" } },
        { "inventory.title", new[] { "INVENTORY", "TÚI ĐỒ" } },
        { "loot.title", new[] { "LOOT CONTAINER", "VẬT PHẨM TRONG THÙNG" } },
        { "trade.title", new[] { "TRADE", "BÀN GIAO DỊCH" } },
        { "trade.choosing", new[] { "Choosing...", "Đang chọn..." } },
        { "trade.lock", new[] { "LOCK", "KHÓA LẠI" } },
        { "trade.unlock", new[] { "UNLOCK", "MỞ KHÓA" } },
    };

    private static readonly Dictionary<string, string[]> LiteralText = CreateLiteralTable();

    public static string Get(string key, string fallback = null)
    {
        if (Text.TryGetValue(key, out string[] values))
            return values[(int)Current];
        return fallback ?? key;
    }

    public static string TranslateLiteral(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        const string dropdownSuffix = "  ▼";
        bool hasDropdownSuffix = value.EndsWith(dropdownSuffix, StringComparison.Ordinal);
        string coreValue = hasDropdownSuffix
            ? value.Substring(0, value.Length - dropdownSuffix.Length)
            : value;

        foreach (KeyValuePair<string, string[]> pair in LiteralText)
        {
            if (coreValue == pair.Value[0] || coreValue == pair.Value[1])
                return pair.Value[(int)Current] + (hasDropdownSuffix ? dropdownSuffix : string.Empty);
        }
        return value;
    }

    public static void SetLanguage(Language language, bool save = true)
    {
        if (Current == language) return;
        Current = language;
        if (save)
        {
            PlayerPrefs.SetInt(PreferenceKey, (int)language);
            PlayerPrefs.Save();
        }
        LanguageChanged?.Invoke();
    }

    public static TMP_FontAsset GetRuntimeFont(TMP_FontAsset preferred = null)
    {
        TMP_FontAsset vietnameseFallback = Resources.Load<TMP_FontAsset>("Fonts/VietnameseDynamic SDF");
        if (preferred == null) return vietnameseFallback;

        // Keep the project's visual font, but let the dynamic fallback supply
        // Vietnamese glyphs that are absent from the static VCR atlas.
        if (vietnameseFallback != null && !preferred.fallbackFontAssetTable.Contains(vietnameseFallback))
            preferred.fallbackFontAssetTable.Add(vietnameseFallback);
        return preferred;
    }

    private static Language ReadInitialLanguage()
    {
        int fallback = Application.systemLanguage == SystemLanguage.Vietnamese ? 1 : 0;
        return (Language)Mathf.Clamp(PlayerPrefs.GetInt(PreferenceKey, fallback), 0, 1);
    }

    private static Dictionary<string, string[]> CreateLiteralTable()
    {
        return new Dictionary<string, string[]>
        {
            { "new_game", new[] { "NEW GAME", "CHƠI MỚI" } },
            { "tutorial", new[] { "TUTORIAL", "HƯỚNG DẪN" } },
            { "multiplayer", new[] { "MULTIPLAYER", "NHIỀU NGƯỜI CHƠI" } },
            { "options", new[] { "OPTIONS", "TÙY CHỌN" } },
            { "credits", new[] { "CREDITS", "GIỚI THIỆU" } },
            { "quit", new[] { "QUIT", "THOÁT" } },
            { "back", new[] { "BACK", "QUAY LẠI" } },
            { "save", new[] { "SAVE", "LƯU" } },
            { "display", new[] { "DISPLAY", "HIỂN THỊ" } },
            { "controls", new[] { "CONTROLS", "ĐIỀU KHIỂN" } },
            { "audio", new[] { "AUDIO", "ÂM THANH" } },
            { "resolution", new[] { "RESOLUTION:", "ĐỘ PHÂN GIẢI:" } },
            { "display_mode", new[] { "DISPLAY MODE:", "CHẾ ĐỘ MÀN HÌNH:" } },
            { "graphics", new[] { "GRAPHICS QUALITY:", "CHẤT LƯỢNG ĐỒ HỌA:" } },
            { "shadows", new[] { "SHADOW QUALITY:", "CHẤT LƯỢNG BÓNG:" } },
            { "brightness", new[] { "BRIGHTNESS:", "ĐỘ SÁNG:" } },
            { "fps_limit", new[] { "FPS LIMIT:", "GIỚI HẠN FPS:" } },
            { "show_fps", new[] { "SHOW FPS:", "HIỆN FPS:" } },
            { "fps_position", new[] { "FPS POSITION:", "VỊ TRÍ FPS:" } },
            { "aim_sensitivity", new[] { "AIM SENSITIVITY:", "ĐỘ NHẠY NGẮM:" } },
            { "zoom_sensitivity", new[] { "ZOOM SENSITIVITY:", "ĐỘ NHẠY ZOOM:" } },
            { "master_volume", new[] { "MASTER VOLUME:", "ÂM LƯỢNG TỔNG:" } },
            { "music_volume", new[] { "MUSIC VOLUME:", "ÂM LƯỢNG NHẠC:" } },
            { "sfx_volume", new[] { "SFX VOLUME:", "ÂM LƯỢNG HIỆU ỨNG:" } },
            { "inventory", new[] { "INVENTORY", "TÚI ĐỒ" } },
            { "loot", new[] { "LOOT CONTAINER", "VẬT PHẨM TRONG THÙNG" } },
            { "health", new[] { "HEALTH STATUS", "TÌNH TRẠNG SỨC KHỎE" } },
            { "pause", new[] { "PAUSED", "TẠM DỪNG" } },
            { "resume", new[] { "RESUME", "TIẾP TỤC" } },
            { "host", new[] { "HOST", "CHỦ PHÒNG" } },
            { "teammate", new[] { "TEAMMATE", "ĐỒNG ĐỘI" } },
            { "empty", new[] { "EMPTY SLOT", "Ô TRỐNG" } },
            { "start", new[] { "START", "BẮT ĐẦU" } },
            { "create_room", new[] { "CREATE ROOM", "TẠO PHÒNG" } },
            { "join_room", new[] { "JOIN ROOM", "VÀO PHÒNG" } },
            { "refresh", new[] { "REFRESH", "LÀM MỚI" } },
            { "yes", new[] { "[ YES ]", "[ CÓ ]" } },
            { "no", new[] { "[ NO ]", "[ KHÔNG ]" } },
            { "low", new[] { "LOW", "THẤP" } },
            { "medium", new[] { "MEDIUM", "TRUNG BÌNH" } },
            { "high", new[] { "HIGH", "CAO" } },
            { "disabled", new[] { "DISABLED", "TẮT" } },
            { "off", new[] { "OFF", "TẮT" } },
            { "on", new[] { "ON", "BẬT" } },
            { "fullscreen", new[] { "FULLSCREEN", "TOÀN MÀN HÌNH" } },
            { "borderless", new[] { "BORDERLESS", "KHÔNG VIỀN" } },
            { "windowed", new[] { "WINDOWED", "CỬA SỔ" } },
            { "unlimited", new[] { "UNLIMITED", "KHÔNG GIỚI HẠN" } },
        };
    }
}

public sealed class RuntimeLocalizationDriver : MonoBehaviour
{
    private float nextRefresh;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<RuntimeLocalizationDriver>() != null) return;
        GameObject go = new GameObject("--- RUNTIME LOCALIZATION ---");
        DontDestroyOnLoad(go);
        go.AddComponent<RuntimeLocalizationDriver>();
    }

    private void OnEnable() => GameLocalization.LanguageChanged += RefreshAll;
    private void OnDisable() => GameLocalization.LanguageChanged -= RefreshAll;

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.4f;
        RefreshAll();
    }

    private void RefreshAll()
    {
        foreach (TMP_Text label in Resources.FindObjectsOfTypeAll<TMP_Text>())
        {
            if (label == null || !label.gameObject.scene.IsValid()) continue;
            label.font = GameLocalization.GetRuntimeFont(label.font);
            label.text = GameLocalization.TranslateLiteral(label.text);
        }

        foreach (UnityEngine.UI.Text label in Resources.FindObjectsOfTypeAll<UnityEngine.UI.Text>())
        {
            if (label == null || !label.gameObject.scene.IsValid()) continue;
            label.text = GameLocalization.TranslateLiteral(label.text);
        }
    }
}
