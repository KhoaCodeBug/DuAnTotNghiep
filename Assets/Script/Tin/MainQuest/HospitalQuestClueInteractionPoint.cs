using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum HospitalQuestClueRole
{
    ShiftLog = 1,
    ShiftLog2 = 2
}

public static class HospitalInvestigationRules
{
    public static bool IsClueAvailable(bool networkReady, MainQuestManager.QuestStage questStage,
        MainQuestManager.HospitalInvestigationStage hospitalStage, HospitalQuestClueRole role)
    {
        if (!networkReady || questStage != MainQuestManager.QuestStage.FindCityMap) return false;
        return role == HospitalQuestClueRole.ShiftLog
            ? hospitalStage == MainQuestManager.HospitalInvestigationStage.FindShiftLog
            : hospitalStage == MainQuestManager.HospitalInvestigationStage.FindShiftLog2;
    }

    public static bool IsDoorDiscoverable(bool networkReady, MainQuestManager.QuestStage questStage,
        MainQuestManager.HospitalInvestigationStage hospitalStage, bool isOpen)
    {
        if (!networkReady || isOpen) return false;
        if (questStage == MainQuestManager.QuestStage.LocateOffice) return true;
        return questStage == MainQuestManager.QuestStage.FindCityMap &&
               hospitalStage >= MainQuestManager.HospitalInvestigationStage.FindShiftLog &&
               hospitalStage < MainQuestManager.HospitalInvestigationStage.RadioReady;
    }

    public static bool CanOpenDoor(bool networkReady, MainQuestManager.QuestStage questStage,
        MainQuestManager.HospitalInvestigationStage hospitalStage, bool isOpen, bool hasSharedKey)
    {
        return networkReady && questStage == MainQuestManager.QuestStage.FindCityMap && !isOpen &&
               hasSharedKey &&
               hospitalStage == MainQuestManager.HospitalInvestigationStage.UnlockRadioRoom;
    }
}

/// <summary>
/// Local hold interaction for the two authored hospital logs. State Authority
/// re-resolves the stable ID and validates role, quest stage, distance and LOS.
/// </summary>
[DisallowMultipleComponent]
public sealed class HospitalQuestClueInteractionPoint : MonoBehaviour
{
    private static readonly Dictionary<int, HospitalQuestClueInteractionPoint> Registry =
        new Dictionary<int, HospitalQuestClueInteractionPoint>();

    [SerializeField] private HospitalQuestClueRole role;
    [SerializeField, Min(0.2f)] private float interactionDistance = 0.85f;
    [SerializeField] private PolygonCollider2D interactionZone;
    [SerializeField, Min(0.1f)] private float holdDuration = 0.7f;
    [SerializeField] private LayerMask obstacleMask = 1 << 6;
    [SerializeField] private Vector3 markerOffset = new Vector3(0f, 0.55f, 0f);

    private Coroutine holdRoutine;

    public HospitalQuestClueRole Role => role;
    public int InteractionId { get; private set; }

    private void Awake()
    {
        InteractionId = BuildStableId();
    }

    private void OnEnable()
    {
        if (InteractionId == 0) InteractionId = BuildStableId();
        if (Registry.TryGetValue(InteractionId, out HospitalQuestClueInteractionPoint existing) && existing != this)
            Debug.LogError($"[HOSPITAL H2] Hai tài liệu trùng ID: {existing.name} và {name}.");
        Registry[InteractionId] = this;
    }

    private void OnDisable()
    {
        CancelHold();
        if (Registry.TryGetValue(InteractionId, out HospitalQuestClueInteractionPoint current) && current == this)
            Registry.Remove(InteractionId);
    }

    private void Update()
    {
        if (!IsLocallyAvailable())
        {
            CancelHold();
            return;
        }

        if (holdRoutine != null || !IsClosestForLocalPlayer(out PlayerMovement localPlayer)) return;
        if (LocalGameplayUIState.BlocksWorldInteractionHints) return;
        if (AutoUIManager.Instance != null && AutoUIManager.Instance.isDoingAction) return;
        if (MainQuestSearchCabinet.IsLocalSearchInProgress) return;
        if (Input.GetKeyDown(KeyCode.E)) holdRoutine = StartCoroutine(HoldRoutine(localPlayer));
    }

    private void OnGUI()
    {
        if (!IsLocallyAvailable() || LocalGameplayUIState.BlocksWorldInteractionHints) return;
        if (!IsClosestForLocalPlayer(out _)) return;

        Camera camera = Camera.main;
        if (camera == null) return;
        Vector3 screenPoint = camera.WorldToScreenPoint(transform.position + markerOffset);
        if (screenPoint.z <= 0f) return;

        GUIStyle markerStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 24,
            fontStyle = FontStyle.Bold
        };
        markerStyle.normal.textColor = new Color(1f, 0.68f, 0.12f);
        GUI.Label(new Rect(screenPoint.x - 18f, Screen.height - screenPoint.y - 18f, 36f, 36f), "●", markerStyle);

        if (holdRoutine != null) return;
        const float width = 460f;
        Rect card = new Rect(Screen.width * 0.5f - width * 0.5f, 72f, width, 54f);
        DrawRect(card, new Color(0.025f, 0.045f, 0.043f, 0.96f));
        DrawRect(new Rect(card.x, card.y, 4f, card.height), markerStyle.normal.textColor);
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 16,
            fontStyle = FontStyle.Bold
        };
        style.normal.textColor = new Color(0.94f, 0.97f, 0.96f);
        GUI.Label(card, QuestUILocalization.IsVietnamese
            ? "GIỮ [E] ĐỂ ĐỌC SỔ TRỰC"
            : "HOLD [E] TO READ SHIFT LOG", style);
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
                QuestUILocalization.IsVietnamese ? "ĐANG ĐỌC SỔ TRỰC..." : "READING SHIFT LOG...");
            yield return null;
        }

        EndHoldPresentation();
        holdRoutine = null;
        MainQuestManager.Instance?.RequestHospitalQuestClue(InteractionId);
    }

    private bool IsLocallyAvailable()
    {
        MainQuestManager manager = MainQuestManager.Instance;
        return manager != null && !manager.IsQuestCutsceneActive &&
               HospitalInvestigationRules.IsClueAvailable(manager.IsNetworkReady, manager.CurrentStage,
                   manager.CurrentHospitalInvestigationStage, role);
    }

    private bool IsClosestForLocalPlayer(out PlayerMovement localPlayer)
    {
        localPlayer = PlayerMovement.LocalPlayerInstance;
        if (localPlayer == null || !CanPlayerInteract(localPlayer.transform.position)) return false;

        HospitalQuestClueInteractionPoint closest = null;
        float closestDistance = float.PositiveInfinity;
        foreach (HospitalQuestClueInteractionPoint point in Registry.Values)
        {
            if (point == null || !point.isActiveAndEnabled || !point.IsLocallyAvailable() ||
                !point.CanPlayerInteract(localPlayer.transform.position)) continue;
            float distance = Vector2.Distance(localPlayer.transform.position, point.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = point;
            }
        }
        return closest == this;
    }

    public bool CanPlayerInteract(Vector3 playerPosition)
    {
        // The authored polygon is shared by local prompt checks and State
        // Authority validation. This avoids fragile radius/LOS checks around
        // deep counters while still keeping a fallback for old scenes.
        if (interactionZone != null && interactionZone.enabled)
            return interactionZone.OverlapPoint(playerPosition);
        if (Vector2.Distance(playerPosition, transform.position) > interactionDistance) return false;
        if (obstacleMask.value == 0) return true;
        Vector2 from = playerPosition;
        Vector2 target = transform.position;
        Vector2 direction = target - from;
        // Stop just before the authored paper anchor so the counter/furniture
        // holding the document is not mistaken for a wall between player and clue.
        Vector2 lineEnd = direction.sqrMagnitude > 0.02f
            ? target - direction.normalized * 0.12f
            : target;
        return !Physics2D.Linecast(from, lineEnd, obstacleMask);
    }

    public static bool TryGet(int id, out HospitalQuestClueInteractionPoint point)
    {
        return Registry.TryGetValue(id, out point) && point != null && point.isActiveAndEnabled;
    }

    public static bool TryGetForRole(HospitalQuestClueRole targetRole, out HospitalQuestClueInteractionPoint point)
    {
        foreach (HospitalQuestClueInteractionPoint candidate in Registry.Values)
        {
            if (candidate != null && candidate.isActiveAndEnabled && candidate.role == targetRole)
            {
                point = candidate;
                return true;
            }
        }
        point = null;
        return false;
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
            string key = gameObject.scene.path + "/" + BuildHierarchyPath(transform) + "/" + role;
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
    public void EditorConfigure(HospitalQuestClueRole configuredRole, float distance, float duration,
        LayerMask obstacles, PolygonCollider2D zone)
    {
        role = configuredRole;
        interactionDistance = distance;
        holdDuration = duration;
        obstacleMask = obstacles;
        interactionZone = zone;
        InteractionId = BuildStableId();
    }
#endif
}
