using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GlobalSettingsManager : MonoBehaviour
{
    public static GlobalSettingsManager Instance { get; private set; }

    private Canvas brightnessCanvas;
    private Image brightnessImage;

    // Các biến cho FPS Counter overlay
    private TextMeshProUGUI fpsCounterText;
    private float fpsUpdateInterval = 0.25f;
    private float fpsTimeCounter = 0f;
    private int fpsFrameCounter = 0;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Khởi tạo Canvas phủ màn hình cho Độ sáng
        GameObject canvasGo = new GameObject("BrightnessOverlayCanvas");
        canvasGo.transform.SetParent(transform);
        brightnessCanvas = canvasGo.AddComponent<Canvas>();
        brightnessCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        brightnessCanvas.sortingOrder = 9999; // Luôn nằm trên cùng mọi UI và game object
        canvasGo.AddComponent<CanvasScaler>();

        GameObject imgGo = new GameObject("OverlayImage");
        imgGo.transform.SetParent(canvasGo.transform);
        brightnessImage = imgGo.AddComponent<Image>();
        
        RectTransform rt = brightnessImage.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Bỏ Raycast Target để tấm che không chặn click chuột vào game
        brightnessImage.raycastTarget = false;

        // Khởi tạo FPS Counter Text ở góc trên bên phải
        GameObject fpsGo = new GameObject("FPS_Counter");
        fpsGo.transform.SetParent(canvasGo.transform, false);
        RectTransform fpsRt = fpsGo.AddComponent<RectTransform>();
        fpsRt.anchorMin = new Vector2(1f, 1f);
        fpsRt.anchorMax = new Vector2(1f, 1f);
        fpsRt.pivot = new Vector2(1f, 1f);
        fpsRt.anchoredPosition = new Vector2(-15f, -15f); // Cách góc 15px
        fpsRt.sizeDelta = new Vector2(150, 40);

        fpsCounterText = fpsGo.AddComponent<TextMeshProUGUI>();
        fpsCounterText.alignment = TextAlignmentOptions.Right;
        fpsCounterText.fontSize = 22;
        fpsCounterText.color = Color.green;
        fpsCounterText.text = "--- FPS";
        fpsCounterText.raycastTarget = false;

        ApplyAllSettings();
    }

    public void ApplyAllSettings()
    {
        // 0. Độ phân giải & Chế độ màn hình (Resolution & Display Mode)
        int savedResIndex = PlayerPrefs.GetInt("SelectedResIndex", 3);
        int savedWindowMode = PlayerPrefs.GetInt("GameWindowMode", 0);
        
        Vector2Int[] commonResolutions = new Vector2Int[]
        {
            new Vector2Int(1280, 720),
            new Vector2Int(1366, 768),
            new Vector2Int(1600, 900),
            new Vector2Int(1920, 1080)
        };
        
        int resIndex = Mathf.Clamp(savedResIndex, 0, commonResolutions.Length - 1);
        Vector2Int res = commonResolutions[resIndex];
        
        FullScreenMode mode = FullScreenMode.ExclusiveFullScreen;
        if (savedWindowMode == 1) mode = FullScreenMode.FullScreenWindow;
        else if (savedWindowMode == 2) mode = FullScreenMode.Windowed;
        
        Screen.SetResolution(res.x, res.y, mode);

        // 1. Độ nhạy chuột (Mouse Sensitivity)
        float sensitivity = PlayerPrefs.GetFloat("MouseSensitivity", 1.0f);
        if (PZ_CameraController.Instance != null)
        {
            PZ_CameraController.Instance.UpdateSensitivity();
        }

        // 2. Độ sáng màn hình (Brightness)
        float brightness = PlayerPrefs.GetFloat("GameBrightness", 1.0f); // Tầm chạy: 0.5f (tối) đến 1.5f (sáng)
        ApplyBrightness(brightness);

        // 6. Chất lượng đồ họa (Graphics Quality)
        int qual = PlayerPrefs.GetInt("GameQualityLevel", 2); // 0 = Low, 1 = Medium, 2 = High
        ApplyGraphicsQuality(qual);

        // 7. Chất lượng đổ bóng (Shadow Quality)
        int shad = PlayerPrefs.GetInt("GameShadowQuality", 2); // 0 = Disabled, 1 = Hard Only, 2 = Soft Shadows
        ApplyShadowQuality(shad);

        // 8. Khử răng cưa (Anti-Aliasing)
        int aa = PlayerPrefs.GetInt("GameAntiAliasing", 2); // 0 = Disabled, 1 = 2x, 2 = 4x, 3 = 8x
        ApplyAntiAliasing(aa);

        // 9. Hiện FPS
        int showFps = PlayerPrefs.GetInt("GameShowFPS", 1); // 0 = Off, 1 = On
        ApplyShowFPS(showFps == 1);

        // 3. Giới hạn khung hình (FPS Limit)
        int fps = PlayerPrefs.GetInt("GameFPSLimit", 60); // 30, 60, 120, -1 (unlimited)
        QualitySettings.vSyncCount = 0; // Bắt buộc tắt VSync để giới hạn FPS hoạt động thực tế chính xác
        Application.targetFrameRate = fps;

        // 4. Âm lượng Master
        float volume = PlayerPrefs.GetFloat("GameMasterVolume", 1.0f);
        AudioListener.volume = volume;

        // 5. Cập nhật Âm lượng Nhạc & SFX cho Menu
        if (AutoMainMenuManager.Instance != null)
        {
            AutoMainMenuManager.Instance.UpdateAudioSettings();
        }
    }

    public void ApplyBrightness(float brightness)
    {
        if (brightnessImage != null)
        {
            if (brightness >= 1.0f)
            {
                // Sáng hơn: Phủ màu trắng với Alpha tương ứng
                float alpha = Mathf.Lerp(0f, 0.4f, (brightness - 1.0f) / 0.5f);
                brightnessImage.color = new Color(1f, 1f, 1f, alpha);
            }
            else
            {
                // Tối hơn: Phủ màu đen với Alpha tương ứng
                float alpha = Mathf.Lerp(0f, 0.7f, (1.0f - brightness) / 0.5f);
                brightnessImage.color = new Color(0f, 0f, 0f, alpha);
            }
        }
    }

    public void ApplyGraphicsQuality(int level)
    {
        QualitySettings.SetQualityLevel(level, true);
    }

    public void ApplyShadowQuality(int level)
    {
        if (level == 0)
        {
            QualitySettings.shadows = ShadowQuality.Disable;
        }
        else if (level == 1)
        {
            QualitySettings.shadows = ShadowQuality.HardOnly;
        }
        else
        {
            QualitySettings.shadows = ShadowQuality.All;
        }
    }

    public void ApplyAntiAliasing(int levelIndex)
    {
        int[] aaValues = new int[] { 0, 2, 4, 8 };
        int idx = Mathf.Clamp(levelIndex, 0, aaValues.Length - 1);
        QualitySettings.antiAliasing = aaValues[idx];
    }

    public void ApplyShowFPS(bool show)
    {
        if (fpsCounterText != null)
        {
            fpsCounterText.gameObject.SetActive(show);
        }
    }

    private void Update()
    {
        fpsFrameCounter++;
        fpsTimeCounter += Time.unscaledDeltaTime;

        if (fpsTimeCounter >= fpsUpdateInterval)
        {
            float lastFps = fpsFrameCounter / fpsTimeCounter;
            if (fpsCounterText != null)
            {
                fpsCounterText.text = Mathf.RoundToInt(lastFps) + " FPS";
                if (lastFps >= 55f) fpsCounterText.color = Color.green;
                else if (lastFps >= 30f) fpsCounterText.color = Color.yellow;
                else fpsCounterText.color = Color.red;
            }
            fpsFrameCounter = 0;
            fpsTimeCounter = 0f;
        }
    }
}
