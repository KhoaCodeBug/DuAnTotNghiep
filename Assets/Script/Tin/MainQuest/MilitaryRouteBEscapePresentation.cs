using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Local presentation for the authoritative Route B vehicle escape. Networked
/// state stays in MilitaryBaseQuestManager; this component only draws route
/// guidance and moves/fades the local gameplay camera.
/// </summary>
[DisallowMultipleComponent]
public sealed class MilitaryRouteBEscapePresentation : MonoBehaviour
{
    private const float LetterboxCloseSeconds = 1.15f;
    private const float LetterboxHeight01 = 0.105f;
    private const float FadeSeconds = 1.35f;
    private const float BlackHoldSeconds = 0.35f;

    public static bool BlocksGameplayInput { get; private set; }

    private readonly List<SpriteRenderer> markers = new();
    private MilitaryBaseQuestManager manager;
    private Coroutine outroRoutine;
    private Sprite arrowSprite;
    private Canvas fadeCanvas;
    private Image fadeImage;
    private Image topLetterbox;
    private Image bottomLetterbox;
    private Camera gameplayCamera;
    private PZ_CameraController cameraController;
    private bool cameraControllerWasEnabled;
    private bool outroStarted;

    public void Configure(MilitaryBaseQuestManager targetManager)
    {
        manager = targetManager;
        BuildGuidanceMarkers();
        RefreshPresentation();
    }

    private void Update()
    {
        RefreshPresentation();
        if (manager != null && manager.IsNetworkReady && manager.IsEscapeOutroActive && !outroStarted)
        {
            PlayOutro(() => VictorySummaryUI.ShowForCurrentMatch(
                manager.SurvivalSeconds, EscapeEndingRoute.MilitaryEvacuation));
        }
    }

    public void RefreshPresentation()
    {
        if (manager == null) return;
        if (markers.Count != manager.EscapeGuidanceWaypointCount) BuildGuidanceMarkers();

        bool visible = manager.IsEscapeGuidanceActive;
        int activeIndex = Mathf.Clamp(manager.EscapeWaypointIndex, 0,
            Mathf.Max(0, markers.Count - 1));
        float pulse = 1f + Mathf.Sin(Time.unscaledTime * 4.2f) * 0.08f;
        for (int i = 0; i < markers.Count; i++)
        {
            SpriteRenderer marker = markers[i];
            if (marker == null) continue;
            bool show = visible && i >= activeIndex;
            marker.gameObject.SetActive(show);
            if (!show) continue;

            if (manager.TryGetEscapeGuidanceWaypoint(i, out Vector2 position, out Vector2 direction))
            {
                marker.transform.position = new Vector3(position.x, position.y, -0.12f);
                marker.transform.rotation = Quaternion.Euler(0f, 0f,
                    Vector2.SignedAngle(Vector2.up, direction));
            }

            bool current = i == activeIndex;
            marker.transform.localScale = Vector3.one * (current ? pulse : 0.88f);
            marker.color = current
                ? new Color(1f, 0.83f, 0.12f, 0.98f)
                : new Color(1f, 0.72f, 0.08f, 0.62f);
        }
    }

    public void PlayOutro(Action onComplete)
    {
        if (outroStarted) return;
        outroStarted = true;
        outroRoutine = StartCoroutine(OutroRoutine(onComplete));
    }

    public void StopImmediate()
    {
        if (outroRoutine != null) StopCoroutine(outroRoutine);
        outroRoutine = null;
        outroStarted = false;
        BlocksGameplayInput = false;
        RestoreCameraController();
        if (fadeCanvas != null) Destroy(fadeCanvas.gameObject);
        fadeCanvas = null;
        fadeImage = null;
        topLetterbox = null;
        bottomLetterbox = null;
        for (int i = 0; i < markers.Count; i++)
            if (markers[i] != null) Destroy(markers[i].gameObject);
        markers.Clear();
    }

    private IEnumerator OutroRoutine(Action onComplete)
    {
        BlocksGameplayInput = true;
        VictorySummaryUI.CloseBlockingGameplayUI();
        SetMarkersVisible(false);
        EnsureFadeCanvas();
        SetFade(0f);
        SetLetterbox(0f);

        gameplayCamera = Camera.main;
        Vector3 cameraStart = gameplayCamera != null ? gameplayCamera.transform.position : Vector3.zero;
        Vector2 target2D = manager != null ? manager.EscapeCameraTargetPosition : (Vector2)cameraStart;
        Vector3 cameraEnd = new Vector3(target2D.x, target2D.y, cameraStart.z);
        float startZoom = gameplayCamera != null ? gameplayCamera.orthographicSize : 6f;
        float targetZoom = Mathf.Clamp(Mathf.Max(20f, startZoom * 3.2f), 20f, 32f);

        if (gameplayCamera != null)
        {
            cameraController = gameplayCamera.GetComponent<PZ_CameraController>();
            if (cameraController != null)
            {
                cameraControllerWasEnabled = cameraController.enabled;
                cameraController.enabled = false;
            }
        }

        float elapsed = 0f;
        while (elapsed < MilitaryStoryFlowRules.EndingMapCameraTravelSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / MilitaryStoryFlowRules.EndingMapCameraTravelSeconds);
            float travel = MilitaryStoryFlowRules.EvaluateEndingMapCameraTravel(t);
            float zoom = Mathf.SmoothStep(0f, 1f, t);
            SetLetterbox(Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01(elapsed / LetterboxCloseSeconds)));
            if (gameplayCamera != null)
            {
                gameplayCamera.transform.position = Vector3.LerpUnclamped(cameraStart, cameraEnd, travel);
                gameplayCamera.orthographicSize = Mathf.LerpUnclamped(startZoom, targetZoom, zoom);
            }
            yield return null;
        }

        if (gameplayCamera != null)
        {
            gameplayCamera.transform.position = cameraEnd;
            gameplayCamera.orthographicSize = targetZoom;
        }
        SetLetterbox(1f);

        elapsed = 0f;
        while (elapsed < MilitaryStoryFlowRules.EndingMapCameraHoldSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < FadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            SetFade(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / FadeSeconds)));
            yield return null;
        }
        SetFade(1f);

        elapsed = 0f;
        while (elapsed < BlackHoldSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Keep the completed black frame as the backdrop, but place it below
        // VictorySummaryUI (sorting order 5000) so the result panel is visible.
        if (fadeCanvas != null) fadeCanvas.sortingOrder = 4990;
        if (fadeImage != null) fadeImage.raycastTarget = false;
        outroRoutine = null;
        onComplete?.Invoke();
    }

    private void BuildGuidanceMarkers()
    {
        for (int i = 0; i < markers.Count; i++)
            if (markers[i] != null) Destroy(markers[i].gameObject);
        markers.Clear();
        if (manager == null) return;
        if (arrowSprite == null) arrowSprite = CreateArrowSprite();

        for (int i = 0; i < manager.EscapeGuidanceWaypointCount; i++)
        {
            GameObject markerObject = new GameObject($"Route B Direction Arrow {i + 1}");
            markerObject.transform.SetParent(transform, true);
            SpriteRenderer marker = markerObject.AddComponent<SpriteRenderer>();
            marker.sprite = arrowSprite;
            marker.sortingOrder = 86;
            markers.Add(marker);
        }
    }

    private void SetMarkersVisible(bool visible)
    {
        for (int i = 0; i < markers.Count; i++)
            if (markers[i] != null) markers[i].gameObject.SetActive(visible);
    }

    private void EnsureFadeCanvas()
    {
        if (fadeCanvas != null) return;
        GameObject root = new GameObject("Route B Ending Fade", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        fadeCanvas = root.GetComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 6000;

        GameObject imageObject = new GameObject("Fade", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(root.transform, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        fadeImage = imageObject.GetComponent<Image>();
        fadeImage.color = Color.black;
        fadeImage.raycastTarget = true;

        topLetterbox = CreateLetterboxImage(root.transform, "Top Letterbox");
        bottomLetterbox = CreateLetterboxImage(root.transform, "Bottom Letterbox");
    }

    private void SetFade(float alpha)
    {
        if (fadeImage == null) return;
        Color color = fadeImage.color;
        color.a = Mathf.Clamp01(alpha);
        fadeImage.color = color;
    }

    private void SetLetterbox(float progress)
    {
        float height = LetterboxHeight01 * Mathf.Clamp01(progress);
        if (topLetterbox != null)
        {
            RectTransform rect = topLetterbox.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f - height);
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
        if (bottomLetterbox != null)
        {
            RectTransform rect = bottomLetterbox.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1f, height);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }

    private static Image CreateLetterboxImage(Transform parent, string objectName)
    {
        GameObject barObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        barObject.transform.SetParent(parent, false);
        Image image = barObject.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
        return image;
    }

    private void RestoreCameraController()
    {
        if (cameraController != null) cameraController.enabled = cameraControllerWasEnabled;
        cameraController = null;
        gameplayCamera = null;
    }

    private static Sprite CreateArrowSprite()
    {
        const int size = 48;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "ROUTE_B_DIRECTION_ARROW",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        Color32[] pixels = new Color32[size * size];
        Color32 clear = new Color32(0, 0, 0, 0);
        Color32 glow = new Color32(255, 174, 16, 90);
        Color32 core = new Color32(255, 219, 48, 255);
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

        for (int y = 5; y <= 29; y++)
        for (int x = 18; x <= 29; x++)
            pixels[y * size + x] = core;
        for (int y = 19; y <= 43; y++)
        {
            int halfWidth = Mathf.RoundToInt((43 - y) * 0.78f);
            for (int x = 24 - halfWidth; x <= 24 + halfWidth; x++)
                if (x >= 0 && x < size) pixels[y * size + x] = core;
        }

        Color32[] source = (Color32[])pixels.Clone();
        for (int y = 1; y < size - 1; y++)
        for (int x = 1; x < size - 1; x++)
        {
            int index = y * size + x;
            if (source[index].a != 0) continue;
            bool neighbour = false;
            for (int oy = -1; oy <= 1 && !neighbour; oy++)
            for (int ox = -1; ox <= 1; ox++)
                if (source[(y + oy) * size + x + ox].a > 0) { neighbour = true; break; }
            if (neighbour) pixels[index] = glow;
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f), 30f);
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    private void OnDisable()
    {
        BlocksGameplayInput = false;
        RestoreCameraController();
    }

    private void OnDestroy()
    {
        BlocksGameplayInput = false;
        RestoreCameraController();
    }
}
