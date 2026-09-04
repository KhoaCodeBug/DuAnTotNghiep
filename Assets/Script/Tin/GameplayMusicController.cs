using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Creates one low-volume, looping 2D music source for Intro_Cinematic and
/// Main. The same source survives the transition between those two scenes.
/// </summary>
public sealed class GameplayMusicController : MonoBehaviour
{
    private const string SettingsResourceName = "GameplayMusicSettings";
    private static GameplayMusicController instance;
    private static bool sceneHookInstalled;

    private AudioSource musicSource;
    private GameplayMusicSettings settings;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        instance = null;
        sceneHookInstalled = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallSceneHook()
    {
        if (sceneHookInstalled) return;
        sceneHookInstalled = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapCurrentScene()
    {
        EnsureForScene(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureForScene(scene);
    }

    private static void EnsureForScene(Scene scene)
    {
        bool isGameplayFlow = scene.name == "Intro_Cinematic" || scene.name == "Main";
        if (!isGameplayFlow)
        {
            if (instance != null)
            {
                GameplayMusicController oldInstance = instance;
                instance = null;
                Destroy(oldInstance.gameObject);
            }
            return;
        }

        if (instance != null) return;

        GameplayMusicSettings musicSettings = Resources.Load<GameplayMusicSettings>(SettingsResourceName);
        if (musicSettings == null || musicSettings.Theme == null)
        {
            Debug.LogWarning("[MUSIC] Không tìm thấy GameplayMusicSettings hoặc clip ThemeGamePlay.");
            return;
        }

        GameObject musicObject = new GameObject("Gameplay Music - ThemeGamePlay");
        instance = musicObject.AddComponent<GameplayMusicController>();
        instance.Initialize(musicSettings);
        DontDestroyOnLoad(musicObject);
    }

    private void Initialize(GameplayMusicSettings musicSettings)
    {
        settings = musicSettings;
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.clip = settings.Theme;
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        musicSource.dopplerLevel = 0f;
        musicSource.volume = 0f;
        musicSource.Play();
    }

    private void Update()
    {
        if (musicSource == null || settings == null) return;
        float playerMusicVolume = GameAudioSettings.MusicVolume;
        float targetVolume = Mathf.Clamp01(playerMusicVolume) * settings.RelativeVolume;
        musicSource.volume = Mathf.MoveTowards(musicSource.volume, targetVolume, Time.unscaledDeltaTime * 0.25f);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
