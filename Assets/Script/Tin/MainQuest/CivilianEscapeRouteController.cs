using UnityEngine;

/// <summary>Local presentation and point-of-no-return interaction for route A.</summary>
public sealed class CivilianEscapeRouteController : MonoBehaviour
{
    private MainQuestManager manager;
    private GameObject checkpointMarker;
    private GameObject cityExitMarker;
    private bool localDriverAtCheckpoint;
    private bool localTeamReady;
    private float forceGatherTimer = 0f;
    private const float ForceGatherDuration = 30f;
    private bool forceGatherCountdownActive;

    public static CivilianEscapeRouteController Attach(MainQuestManager target)
    {
        if (target == null) return null;
        CivilianEscapeRouteController controller = target.GetComponent<CivilianEscapeRouteController>();
        if (controller == null) controller = target.gameObject.AddComponent<CivilianEscapeRouteController>();
        controller.manager = target;
        controller.EnsureMarker();
        return controller;
    }

    private void Update()
    {
        if (manager == null || !manager.IsNetworkReady)
        {
            SetMarkerVisible(checkpointMarker, false);
            SetMarkerVisible(cityExitMarker, false);
            localDriverAtCheckpoint = false;
            localTeamReady = false;
            ResetForceGatherCountdown();
            return;
        }

        MainQuestManager.CivilianRouteStage stage = manager.CurrentCivilianRouteStage;
        bool routeAvailable = manager.IsArrivalCarRepaired && !manager.IsCivilianEscapeComplete &&
                              manager.LockedEscapeRoute != EscapeEndingRoute.MilitaryEvacuation;
        SetMarkerVisible(checkpointMarker, routeAvailable &&
            stage >= MainQuestManager.CivilianRouteStage.ExploringExits &&
            stage <= MainQuestManager.CivilianRouteStage.AwaitingTeam);
        SetMarkerVisible(cityExitMarker, routeAvailable &&
            stage == MainQuestManager.CivilianRouteStage.EscapeRun);
        if (!routeAvailable)
        {
            localDriverAtCheckpoint = false;
            localTeamReady = false;
            ResetForceGatherCountdown();
            return;
        }

        EnsureMarker();
        checkpointMarker.transform.position = manager.CivilianEscapePosition;
        cityExitMarker.transform.position = manager.CivilianCityExitPosition;
        localDriverAtCheckpoint = stage == MainQuestManager.CivilianRouteStage.AwaitingTeam &&
                                  IsLocalDriverAt(manager.CivilianEscapePosition,
                                      manager.CivilianEscapeTriggerRadius);
        localTeamReady = localDriverAtCheckpoint && manager.AreAllLivingPlayersGatheredForCivilianEscape();
        TickForceGatherCountdown(stage);
        if (!localTeamReady || !Input.GetKeyDown(KeyCode.E) || EscapeRouteDecisionUI.IsVisible ||
            (AutoUIManager.Instance != null && AutoUIManager.Instance.IsAnyMenuOpen()))
            return;

        EscapeRouteDecisionUI.ShowFinaleConfirmation(
            EscapeEndingRoute.CivilianCar, manager.RequestCivilianEscape);
    }

    private void TickForceGatherCountdown(MainQuestManager.CivilianRouteStage stage)
    {
        bool canForceGather = stage == MainQuestManager.CivilianRouteStage.AwaitingTeam &&
                              manager.IsAtLeastHalfOfLivingPlayersGatheredForCivilianEscape();
        if (!canForceGather)
        {
            ResetForceGatherCountdown();
            return;
        }

        if (!forceGatherCountdownActive)
        {
            forceGatherCountdownActive = true;
            forceGatherTimer = ForceGatherDuration;
        }
        else
        {
            forceGatherTimer = Mathf.Max(0f, forceGatherTimer - Time.deltaTime);
        }

        if (forceGatherTimer > 0f || !manager.HasStateAuthority) return;
        if (!manager.AuthorityForceCivilianEscape()) ResetForceGatherCountdown();
    }

    private void ResetForceGatherCountdown()
    {
        forceGatherCountdownActive = false;
        forceGatherTimer = 0f;
    }

    private bool IsLocalDriverAt(Vector2 position, float radius)
    {
        PlayerMovement player = PlayerMovement.LocalPlayerInstance;
        if (player == null || manager.RepairedArrivalCarObject == null) return false;
        PlayerInteraction interaction = player.GetComponent<PlayerInteraction>();
        if (interaction == null || !interaction.IsInVehicle || !interaction.IsVehicleDriver ||
            interaction.CurrentVehicle != manager.RepairedArrivalCarObject)
            return false;
        return Vector2.Distance(manager.RepairedArrivalCarObject.transform.position, position) <= radius;
    }

    private void EnsureMarker()
    {
        if (manager == null) return;
        if (checkpointMarker == null)
            checkpointMarker = CreateWorldMarker("Civilian Escape Regroup Marker",
                manager.CivilianEscapePosition, new Color(0.25f, 0.95f, 0.72f, 0.92f));
        if (cityExitMarker == null)
            cityExitMarker = CreateWorldMarker("Civilian City Exit Marker",
                manager.CivilianCityExitPosition, new Color(1f, 0.72f, 0.18f, 0.95f));
    }

    private void SetMarkerVisible(GameObject target, bool visible)
    {
        EnsureMarker();
        if (target != null && target.activeSelf != visible) target.SetActive(visible);
    }

    private void OnGUI()
    {
        if (GameplayHudLayout.AreGameplayPromptsSuppressed()) return;

        if (forceGatherCountdownActive && manager != null && manager.IsNetworkReady &&
            manager.CurrentCivilianRouteStage == MainQuestManager.CivilianRouteStage.AwaitingTeam)
        {
            GUIStyle countdownStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };
            string countdown = $"Bắt đầu đếm ngược rời thành phố: {Mathf.CeilToInt(forceGatherTimer)}s...";
            GUI.Box(new Rect(Screen.width * 0.5f - 260f, 72f, 520f, 42f), countdown, countdownStyle);
        }

        if ((!localDriverAtCheckpoint && !localTeamReady) || EscapeRouteDecisionUI.IsVisible) return;
        GUIStyle style = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 15,
            fontStyle = FontStyle.Bold
        };
        string prompt = localTeamReady
            ? "[E]  BẮT ĐẦU VƯỢT VÒNG PHONG TỎA  •  ĐIỂM KHÔNG THỂ QUAY LẠI"
            : "CHỜ CÁC THÀNH VIÊN CÒN SỐNG TẬP KẾT GẦN XE";
        Rect promptRect = GameplayHudLayout.GetBottomCenterPromptRect(620f, 42f);
        GUI.Box(promptRect, prompt, style);
    }

    private static GameObject CreateWorldMarker(string name, Vector2 position, Color color)
    {
        GameObject result = new GameObject(name);
        result.transform.position = position;
        SpriteRenderer sprite = result.AddComponent<SpriteRenderer>();
        sprite.sprite = CreateMarkerSprite();
        sprite.sortingOrder = 30;
        sprite.color = color;
        result.SetActive(false);
        return result;
    }

    private static Sprite CreateMarkerSprite()
    {
        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "CIVILIAN_ESCAPE_MARKER_RUNTIME",
            filterMode = FilterMode.Point,
            hideFlags = HideFlags.DontSave
        };
        Color32 clear = new Color32(0, 0, 0, 0);
        Color32 bright = new Color32(75, 245, 185, 255);
        Color32[] pixels = new Color32[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
        for (int y = 2; y < size - 2; y++)
        for (int x = 2; x < size - 2; x++)
        {
            int edgeDistance = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
            if (edgeDistance < 3 || Mathf.Abs(x - y) <= 1 || Mathf.Abs((size - 1 - x) - y) <= 1)
                pixels[y * size + x] = bright;
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 16f);
    }

    private void OnDestroy()
    {
        if (checkpointMarker != null) Destroy(checkpointMarker);
        if (cityExitMarker != null) Destroy(cityExitMarker);
    }
}
