using UnityEngine;

/// <summary>Local presentation and point-of-no-return interaction for route A.</summary>
public sealed class CivilianEscapeRouteController : MonoBehaviour
{
    private MainQuestManager manager;
    private GameObject marker;
    private SpriteRenderer markerSprite;
    private bool localPlayerCanEscape;

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
            SetMarkerVisible(false);
            localPlayerCanEscape = false;
            return;
        }

        bool routeAvailable = manager.IsArrivalCarRepaired && !manager.IsCivilianEscapeComplete &&
                              manager.LockedEscapeRoute != EscapeEndingRoute.MilitaryEvacuation;
        SetMarkerVisible(routeAvailable);
        if (!routeAvailable)
        {
            localPlayerCanEscape = false;
            return;
        }

        EnsureMarker();
        marker.transform.position = manager.CivilianEscapePosition;
        localPlayerCanEscape = IsLocalDriverAtExit();
        if (!localPlayerCanEscape || !Input.GetKeyDown(KeyCode.E) || EscapeRouteDecisionUI.IsVisible ||
            (AutoUIManager.Instance != null && AutoUIManager.Instance.IsAnyMenuOpen()))
            return;

        EscapeRouteDecisionUI.ShowFinaleConfirmation(
            EscapeEndingRoute.CivilianCar, manager.RequestCivilianEscape);
    }

    private bool IsLocalDriverAtExit()
    {
        PlayerMovement player = PlayerMovement.LocalPlayerInstance;
        if (player == null || manager.RepairedArrivalCarObject == null) return false;
        PlayerInteraction interaction = player.GetComponent<PlayerInteraction>();
        if (interaction == null || !interaction.IsInVehicle || !interaction.IsVehicleDriver ||
            interaction.CurrentVehicle != manager.RepairedArrivalCarObject)
            return false;
        return Vector2.Distance(manager.RepairedArrivalCarObject.transform.position,
            manager.CivilianEscapePosition) <= manager.CivilianEscapeTriggerRadius;
    }

    private void EnsureMarker()
    {
        if (marker != null || manager == null) return;
        marker = new GameObject("Civilian Escape Finale Marker");
        marker.transform.position = manager.CivilianEscapePosition;
        markerSprite = marker.AddComponent<SpriteRenderer>();
        markerSprite.sprite = CreateMarkerSprite();
        markerSprite.sortingOrder = 30;
        markerSprite.color = new Color(0.25f, 0.95f, 0.72f, 0.92f);
        marker.SetActive(false);
    }

    private void SetMarkerVisible(bool visible)
    {
        EnsureMarker();
        if (marker != null && marker.activeSelf != visible) marker.SetActive(visible);
    }

    private void OnGUI()
    {
        if (!localPlayerCanEscape || EscapeRouteDecisionUI.IsVisible) return;
        GUIStyle style = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 15,
            fontStyle = FontStyle.Bold
        };
        GUI.Box(new Rect(Screen.width * 0.5f - 290f, Screen.height - 112f, 580f, 42f),
            "[E]  BẮT ĐẦU VƯỢT VÒNG PHONG TỎA  •  ĐIỂM KHÔNG THỂ QUAY LẠI", style);
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
        if (marker != null) Destroy(marker);
    }
}
