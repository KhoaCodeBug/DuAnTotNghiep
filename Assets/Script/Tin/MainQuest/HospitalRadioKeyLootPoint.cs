using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-authored candidate for the hospital Radio key. State Authority chooses
/// one stable ID and replicates that choice, so Host, Client and late joiners
/// expose the same polygon, prompt and waypoint.
/// </summary>
[DisallowMultipleComponent]
public sealed class HospitalRadioKeyLootPoint : MonoBehaviour
{
    private static readonly Dictionary<int, HospitalRadioKeyLootPoint> Registry =
        new Dictionary<int, HospitalRadioKeyLootPoint>();

    [SerializeField] private PolygonCollider2D interactionZone;
    [SerializeField, Min(0.2f)] private float fallbackDistance = 0.8f;
    [SerializeField, Min(0.1f)] private float holdDuration = 0.55f;
    [SerializeField] private Vector3 markerOffset = new Vector3(0f, 0.45f, 0f);

    private Coroutine holdRoutine;

    public int InteractionId { get; private set; }
    public PolygonCollider2D InteractionZone => interactionZone;

    private void Awake()
    {
        InteractionId = BuildStableId();
    }

    private void OnEnable()
    {
        if (InteractionId == 0) InteractionId = BuildStableId();
        if (Registry.TryGetValue(InteractionId, out HospitalRadioKeyLootPoint existing) && existing != this)
            Debug.LogError($"[HOSPITAL H5] Hai KeyLoot trùng ID: {existing.name} và {name}.");
        Registry[InteractionId] = this;
    }

    private void OnDisable()
    {
        CancelHold();
        if (Registry.TryGetValue(InteractionId, out HospitalRadioKeyLootPoint current) && current == this)
            Registry.Remove(InteractionId);
    }

    private void Update()
    {
        if (!IsLocallyAvailable())
        {
            CancelHold();
            return;
        }

        PlayerMovement localPlayer = PlayerMovement.LocalPlayerInstance;
        if (holdRoutine != null || localPlayer == null || !CanPlayerInteract(localPlayer.transform.position)) return;
        if (LocalGameplayUIState.BlocksWorldInteractionHints ||
            (AutoUIManager.Instance != null && AutoUIManager.Instance.isDoingAction) ||
            MainQuestSearchCabinet.IsLocalSearchInProgress) return;
        if (Input.GetKeyDown(KeyCode.E)) holdRoutine = StartCoroutine(HoldRoutine(localPlayer));
    }

    private void OnGUI()
    {
        if (!IsLocallyAvailable() || LocalGameplayUIState.BlocksWorldInteractionHints) return;
        PlayerMovement localPlayer = PlayerMovement.LocalPlayerInstance;
        if (localPlayer == null || !CanPlayerInteract(localPlayer.transform.position)) return;
        Camera camera = Camera.main;
        if (camera == null) return;
        Vector3 screenPoint = camera.WorldToScreenPoint(transform.position + markerOffset);
        if (screenPoint.z <= 0f) return;

        GUIStyle markerStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 25,
            fontStyle = FontStyle.Bold
        };
        markerStyle.normal.textColor = new Color(1f, 0.78f, 0.18f);
        GUI.Label(new Rect(screenPoint.x - 20f, Screen.height - screenPoint.y - 20f, 40f, 40f),
            "◆", markerStyle);
        if (holdRoutine != null) return;

        const float width = 440f;
        Rect card = new Rect(Screen.width * 0.5f - width * 0.5f, 72f, width, 54f);
        DrawRect(card, new Color(0.025f, 0.045f, 0.043f, 0.96f));
        DrawRect(new Rect(card.x, card.y, 4f, card.height), markerStyle.normal.textColor);
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 16,
            fontStyle = FontStyle.Bold
        };
        labelStyle.normal.textColor = new Color(0.94f, 0.97f, 0.96f);
        GUI.Label(card, QuestUILocalization.IsVietnamese
            ? "GIỮ [E] ĐỂ NHẶT CHÌA KHÓA RADIO"
            : "HOLD [E] TO TAKE THE RADIO KEY", labelStyle);
    }

    private IEnumerator HoldRoutine(PlayerMovement localPlayer)
    {
        if (AutoUIManager.Instance != null) AutoUIManager.Instance.isDoingAction = true;
        float elapsed = 0f;
        while (elapsed < holdDuration)
        {
            if (localPlayer == null || !IsLocallyAvailable() ||
                !CanPlayerInteract(localPlayer.transform.position) || !Input.GetKey(KeyCode.E) ||
                LocalGameplayUIState.BlocksWorldInteractionHints)
            {
                EndHoldPresentation();
                holdRoutine = null;
                yield break;
            }

            elapsed = Mathf.Min(holdDuration, elapsed + Time.unscaledDeltaTime);
            AutoUIManager.Instance?.ShowReloadUI(elapsed, holdDuration,
                QuestUILocalization.IsVietnamese ? "ĐANG NHẶT CHÌA KHÓA..." : "TAKING RADIO KEY...");
            yield return null;
        }

        EndHoldPresentation();
        holdRoutine = null;
        MainQuestManager.Instance?.RequestHospitalRadioKeyLoot(InteractionId);
    }

    private bool IsLocallyAvailable()
    {
        MainQuestManager manager = MainQuestManager.Instance;
        return manager != null && manager.IsNetworkReady && !manager.IsQuestCutsceneActive &&
               manager.CurrentStage == MainQuestManager.QuestStage.FindCityMap &&
               manager.CurrentHospitalInvestigationStage == MainQuestManager.HospitalInvestigationStage.FindRadioKey &&
               !manager.HasHospitalRadioKeyState &&
               manager.SelectedHospitalRadioKeyLootIdState == InteractionId;
    }

    public bool CanPlayerInteract(Vector3 playerPosition)
    {
        if (interactionZone != null && interactionZone.enabled)
            return interactionZone.OverlapPoint(playerPosition);
        return Vector2.Distance(playerPosition, transform.position) <= fallbackDistance;
    }

    public static bool TryGet(int id, out HospitalRadioKeyLootPoint point)
    {
        return Registry.TryGetValue(id, out point) && point != null && point.isActiveAndEnabled;
    }

    public static void GetAll(List<HospitalRadioKeyLootPoint> result)
    {
        if (result == null) return;
        result.Clear();
        foreach (HospitalRadioKeyLootPoint point in Registry.Values)
            if (point != null && point.isActiveAndEnabled)
                result.Add(point);
        result.Sort((left, right) => left.InteractionId.CompareTo(right.InteractionId));
    }

    private void CancelHold()
    {
        if (holdRoutine == null) return;
        StopCoroutine(holdRoutine);
        holdRoutine = null;
        EndHoldPresentation();
    }

    private static void EndHoldPresentation()
    {
        if (AutoUIManager.Instance == null) return;
        AutoUIManager.Instance.HideReloadUI();
        AutoUIManager.Instance.isDoingAction = false;
    }

    private static void DrawRect(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    private int BuildStableId()
    {
        unchecked
        {
            uint hash = 2166136261;
            string key = gameObject.scene.path + "/" + BuildHierarchyPath(transform) + "/HospitalRadioKeyLoot";
            for (int i = 0; i < key.Length; i++)
            {
                hash ^= key[i];
                hash *= 16777619;
            }
            return hash == 0 ? 1 : (int)hash;
        }
    }

    private static string BuildHierarchyPath(Transform target)
    {
        string path = string.Empty;
        while (target != null)
        {
            path = target.name + "[" + target.GetSiblingIndex() + "]/" + path;
            target = target.parent;
        }
        return path;
    }

#if UNITY_EDITOR
    public void EditorConfigure(PolygonCollider2D zone)
    {
        interactionZone = zone;
        InteractionId = BuildStableId();
    }
#endif
}
