using UnityEngine;
using UnityEngine.UI;

public class GlobalSettingsManager : MonoBehaviour
{
    public static GlobalSettingsManager Instance { get; private set; }

    private Canvas brightnessCanvas;
    private Image brightnessImage;

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
}
