using System;
using System.Collections;
using System.Threading.Tasks;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Full-screen mutually-exclusive victory summary for the extraction finale.</summary>
public sealed class VictorySummaryUI : MonoBehaviour
{
    private static VictorySummaryUI instance;

    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private TMP_Text summaryText;
    private TMP_Text titleText;
    private TMP_Text subtitleText;
    private Button mainMenuButton;
    private TMP_Text buttonLabelText;
    private float cachedSurvivalSeconds;
    private EscapeEndingRoute cachedRoute;

    public bool IsVisible => canvas != null && canvas.enabled && gameObject.activeSelf;
    public static bool IsShowing => instance != null && instance.IsVisible;

    public static void ShowForCurrentMatch(float survivalSeconds)
    {
        ShowForCurrentMatch(survivalSeconds, EscapeEndingRoute.MilitaryEvacuation);
    }

    public static void ShowForCurrentMatch(float survivalSeconds, EscapeEndingRoute route)
    {
        if (instance == null)
        {
            // Build the required UI host before adding VictorySummaryUI. AddComponent
            // invokes Awake immediately, so creating an empty GameObject first made
            // Awake attempt to configure a Canvas that did not exist yet.
            GameObject host = new GameObject("VictorySummaryUI",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(CanvasGroup));
            DontDestroyOnLoad(host);
            instance = host.AddComponent<VictorySummaryUI>();
            instance.Build();
        }
        instance.Show(survivalSeconds, route);
    }

    private void Awake()
    {
        if (instance == null) instance = this;
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }
        if (canvas == null) Build();
    }

    private void OnEnable()
    {
        GameLocalization.LanguageChanged += OnLanguageChanged;
    }

    private void OnDisable()
    {
        GameLocalization.LanguageChanged -= OnLanguageChanged;
    }

    private void OnDestroy()
    {
        GameLocalization.LanguageChanged -= OnLanguageChanged;
        if (instance == this) instance = null;
    }

    private void OnLanguageChanged()
    {
        if (IsVisible)
        {
            RefreshTexts();
        }
    }

    private void Build()
    {
        if (canvas != null) return;
        canvas = gameObject.GetComponent<Canvas>() ?? gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        if (gameObject.GetComponent<CanvasScaler>() == null) gameObject.AddComponent<CanvasScaler>();
        if (gameObject.GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
        canvasGroup = gameObject.GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

        Image backdrop = CreateImage("Victory Backdrop", transform, new Color(0.015f, 0.025f, 0.02f, 0.98f));
        Stretch(backdrop.rectTransform);

        RectTransform panel = CreateImage("Victory Panel", transform, new Color(0.08f, 0.12f, 0.09f, 0.98f)).rectTransform;
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(620f, 430f);

        titleText = CreateText("Victory Title", panel, GameLocalization.Get("victory.title.military"), 40,
            new Color(0.35f, 1f, 0.55f));
        SetRect(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -62f), new Vector2(560f, 70f));
        titleText.fontStyle = FontStyles.Bold;

        subtitleText = CreateText("Victory Subtitle", panel, GameLocalization.Get("victory.subtitle.military"), 18,
            new Color(0.8f, 0.9f, 0.82f));
        SetRect(subtitleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -116f), new Vector2(560f, 36f));

        summaryText = CreateText("Victory Statistics", panel, string.Empty, 22, Color.white);
        SetRect(summaryText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -5f), new Vector2(480f, 150f));
        summaryText.alignment = TextAlignmentOptions.MidlineLeft;

        GameObject buttonObject = new GameObject("Return To MainMenu", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(panel, false);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        SetRect(buttonRect, new Vector2(0.5f, 0f), new Vector2(0f, 58f), new Vector2(310f, 56f));
        buttonObject.GetComponent<Image>().color = new Color(0.2f, 0.65f, 0.34f, 1f);
        mainMenuButton = buttonObject.GetComponent<Button>();
        mainMenuButton.onClick.AddListener(ReturnToMainMenu);
        buttonLabelText = CreateText("Button Label", buttonRect, GameLocalization.Get("victory.return_menu"), 20, Color.white);
        Stretch(buttonLabelText.rectTransform);
        buttonLabelText.fontStyle = FontStyles.Bold;

        canvas.enabled = false;
    }

    private void RefreshTexts()
    {
        if (titleText != null)
        {
            titleText.text = cachedRoute == EscapeEndingRoute.CivilianCar
                ? GameLocalization.Get("victory.title.civilian")
                : GameLocalization.Get("victory.title.military");
        }

        if (subtitleText != null)
        {
            subtitleText.text = cachedRoute == EscapeEndingRoute.CivilianCar
                ? GameLocalization.Get("victory.subtitle.civilian")
                : GameLocalization.Get("victory.subtitle.military");
        }

        if (buttonLabelText != null)
        {
            buttonLabelText.text = GameLocalization.Get("victory.return_menu");
        }

        int killCount = 0;
        PlayerMovement localPlayer = PlayerMovement.LocalPlayerInstance;
        Skill_WeaponMaster skill = localPlayer != null ? localPlayer.GetComponent<Skill_WeaponMaster>() : null;
        if (skill != null && skill.Object != null && skill.Object.IsValid) killCount = skill.CurrentKills;

        string difficulty = DifficultyRules.ActiveDifficulty switch
        {
            0 => GameLocalization.Get("difficulty.name.easy"),
            2 => GameLocalization.Get("difficulty.name.hardcore"),
            _ => GameLocalization.Get("difficulty.name.normal")
        };
        TimeSpan duration = TimeSpan.FromSeconds(Mathf.Max(0f, cachedSurvivalSeconds));
        if (summaryText != null)
        {
            summaryText.text =
                $"{GameLocalization.Get("victory.stat.survival_time")}     {duration.Hours:00}:{duration.Minutes:00}:{duration.Seconds:00}\n\n" +
                $"{GameLocalization.Get("victory.stat.zombies_killed")}           {killCount}\n\n" +
                $"{GameLocalization.Get("victory.stat.difficulty")}                 {difficulty}";
        }
    }

    private void Show(float survivalSeconds, EscapeEndingRoute route)
    {
        CloseBlockingGameplayUI();
        cachedSurvivalSeconds = survivalSeconds;
        cachedRoute = route;
        gameObject.SetActive(true);
        canvas.enabled = true;
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = true;

        RefreshTexts();

        StopAllCoroutines();
        StartCoroutine(FadeInRoutine());
    }

    private IEnumerator FadeInRoutine()
    {
        const float duration = 0.65f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }
        canvasGroup.alpha = 1f;
    }

    public static void CloseBlockingGameplayUI()
    {
        QuestFlowUIPrototype questUI = FindFirstObjectByType<QuestFlowUIPrototype>(FindObjectsInactive.Include);
        if (questUI != null)
        {
            questUI.CloseRouteClueReading();
            questUI.SetMapOpenForPreview(false);
            questUI.SetJournalOpenForPreview(false);
        }
        AutoUIManager.Instance?.SetQuestOverlayOpen(true);
        AutoTabManager.Instance?.ShowTabs(false);
        AutoHealthPanel.Instance?.SetOpenState(false);
        HotbarHUDManager.Instance?.SetHUDVisible(false);
        AutoNoiseMeter.SetHUDVisible(false);
    }

    private async void ReturnToMainMenu()
    {
        if (mainMenuButton != null) mainMenuButton.interactable = false;
        NetworkRunner runner = FindFirstObjectByType<NetworkRunner>();
        if (runner != null && runner.IsRunning)
        {
            try { await runner.Shutdown(); }
            catch (Exception exception) { Debug.LogWarning("[VICTORY] Runner shutdown: " + exception.Message); }
        }
        await Task.Yield();
        SceneManager.LoadScene(0);
        Destroy(gameObject);
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject child = new GameObject(name, typeof(RectTransform), typeof(Image));
        child.transform.SetParent(parent, false);
        Image image = child.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static TMP_Text CreateText(string name, Transform parent, string value, int size, Color color)
    {
        GameObject child = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        child.transform.SetParent(parent, false);
        TMP_Text text = child.GetComponent<TMP_Text>();
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
