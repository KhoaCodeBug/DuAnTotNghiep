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
        // 1. Độ nhạy chuột (Mouse Sensitivity)
        float sensitivity = PlayerPrefs.GetFloat("MouseSensitivity", 1.0f);
        if (PZ_CameraController.Instance != null)
        {
            PZ_CameraController.Instance.UpdateSensitivity();
        }

        // 2. Độ sáng màn hình (Brightness)
        float brightness = PlayerPrefs.GetFloat("GameBrightness", 1.0f); // Tầm chạy: 0.5f (tối) đến 1.5f (sáng)
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
        Application.targetFrameRate = fps;

        // 4. Âm lượng Master
        float volume = PlayerPrefs.GetFloat("GameMasterVolume", 1.0f);
        AudioListener.volume = volume;
    }
}
