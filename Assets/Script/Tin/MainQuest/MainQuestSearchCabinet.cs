using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A clue-search interaction for a quest point. It can sit on an empty marker
/// GameObject or beside a normal LootContainer without changing normal loot.
/// </summary>
[DisallowMultipleComponent]
public sealed class MainQuestSearchCabinet : MonoBehaviour
{
    private static readonly Dictionary<int, MainQuestSearchCabinet> Registry = new Dictionary<int, MainQuestSearchCabinet>();

    [Header("Điểm nhiệm vụ: tìm bản đồ")]
    [Min(0.2f)] public float interactionDistance = 0.7f;
    [Min(0.2f)] public float searchDuration = 1.8f;
    public LayerMask obstacleMask = 1 << 6;
    public Vector3 markerOffset = new Vector3(0f, 0.7f, 0f);

    private Collider2D cabinetCollider;
    private Coroutine searchRoutine;
    public int CabinetId { get; private set; }
    public static bool IsLocalSearchInProgress { get; private set; }

    private void Awake()
    {
        cabinetCollider = GetComponent<Collider2D>();
        CabinetId = BuildStableId();
    }

    private void OnEnable()
    {
        if (CabinetId == 0) CabinetId = BuildStableId();
        if (Registry.TryGetValue(CabinetId, out MainQuestSearchCabinet existing) && existing != this)
            Debug.LogError($"[MAIN QUEST] Hai tủ nhiệm vụ trùng ID: {existing.name} và {name}.");
        Registry[CabinetId] = this;
    }

    private void OnDisable()
    {
        CancelLocalSearch();
        if (Registry.TryGetValue(CabinetId, out MainQuestSearchCabinet current) && current == this)
            Registry.Remove(CabinetId);
    }

    private void Update()
    {
        MainQuestManager manager = MainQuestManager.Instance;
        if (manager == null || !manager.IsCurrentOfficeObjective(CabinetId) || manager.IsCabinetChecked(CabinetId))
        {
            CancelLocalSearch();
            return;
        }

        if (searchRoutine != null)
            return;
        if (!IsClosestCabinetForLocalPlayer(out PlayerMovement localPlayer)) return;
        if (LocalGameplayUIState.BlocksWorldInteractionHints) return;
        if (AutoUIManager.Instance != null && AutoUIManager.Instance.isDoingAction) return;
        if (Input.GetKeyDown(KeyCode.E)) searchRoutine = StartCoroutine(SearchRoutine(localPlayer));
    }

    private void OnGUI()
    {
        MainQuestManager manager = MainQuestManager.Instance;
        if (manager == null || !manager.IsCurrentOfficeObjective(CabinetId) || manager.IsQuestCutsceneActive) return;
        if (manager.IsCabinetChecked(CabinetId)) return;
        if (LocalGameplayUIState.BlocksWorldInteractionHints) return;

        Camera camera = Camera.main;
        if (camera == null) return;
        Vector3 screenPoint = camera.WorldToScreenPoint(transform.position + markerOffset);
        if (screenPoint.z <= 0f) return;

        GUIStyle markerStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 28,
            fontStyle = FontStyle.Bold
        };
        markerStyle.normal.textColor = new Color(1f, 0.88f, 0.1f, 1f);
        GUI.Label(new Rect(screenPoint.x - 18f, Screen.height - screenPoint.y - 18f, 36f, 36f), "●", markerStyle);

        if (searchRoutine != null || !IsClosestCabinetForLocalPlayer(out _)) return;
        // Option C: keep the hint near the objective area, but give it its own
        // interaction-card language so it cannot be mistaken for a quest toast.
        const float cardWidth = 500f;
        const float cardHeight = 60f;
        Rect card = new Rect(Screen.width * 0.5f - cardWidth * 0.5f, 72f, cardWidth, cardHeight);
        DrawSolidRect(card, new Color(0.025f, 0.045f, 0.043f, 0.96f));
        DrawSolidRect(new Rect(card.x, card.y, 4f, card.height), new Color(1f, 0.62f, 0.08f, 1f));
        DrawCardBorder(card, new Color(0.28f, 0.39f, 0.36f, 0.95f));

        Rect keyCap = new Rect(card.x + 18f, card.y + 13f, 52f, 34f);
        DrawSolidRect(keyCap, new Color(1f, 0.62f, 0.08f, 1f));
        GUIStyle keyStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 17,
            fontStyle = FontStyle.Bold
        };
        keyStyle.normal.textColor = new Color(0.045f, 0.04f, 0.025f, 1f);
        GUI.Label(keyCap, "[E]", keyStyle);

        GUIStyle categoryStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.UpperLeft,
            fontSize = 10,
            fontStyle = FontStyle.Bold
        };
        categoryStyle.normal.textColor = new Color(1f, 0.69f, 0.2f, 1f);
        GUI.Label(new Rect(card.x + 86f, card.y + 8f, card.width - 104f, 18f),
            GameLocalization.Get("quest.interaction_hold"), categoryStyle);

        GUIStyle actionStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.UpperLeft,
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            clipping = TextClipping.Clip
        };
        actionStyle.normal.textColor = new Color(0.94f, 0.97f, 0.96f, 1f);
        GUI.Label(new Rect(card.x + 86f, card.y + 25f, card.width - 104f, 26f),
            manager.GetCurrentOfficeInteractionActionLabel(), actionStyle);
    }

    private static void DrawSolidRect(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    private static void DrawCardBorder(Rect rect, Color color)
    {
        const float thickness = 1f;
        DrawSolidRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
        DrawSolidRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        DrawSolidRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
        DrawSolidRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
    }

    private IEnumerator SearchRoutine(PlayerMovement localPlayer)
    {
        IsLocalSearchInProgress = true;
        if (AutoUIManager.Instance != null)
            AutoUIManager.Instance.isDoingAction = true;

        float elapsed = 0f;
        while (elapsed < searchDuration)
        {
            MainQuestManager manager = MainQuestManager.Instance;
            if (localPlayer == null || manager == null || !manager.IsMapSearchActive ||
                manager.IsCabinetChecked(CabinetId) || !CanPlayerSearch(localPlayer.transform.position))
            {
                EndLocalSearchPresentation();
                searchRoutine = null;
                yield break;
            }

            elapsed = Mathf.Min(searchDuration, elapsed + Time.unscaledDeltaTime);
            AutoUIManager.Instance?.ShowReloadUI(elapsed, searchDuration, manager.GetCurrentOfficeProgressLabel());
            yield return null;
        }

        EndLocalSearchPresentation();
        searchRoutine = null;
        MainQuestManager.Instance?.RequestSearchCabinet(CabinetId);
    }

    private void CancelLocalSearch()
    {
        if (searchRoutine == null)
            return;

        StopCoroutine(searchRoutine);
        searchRoutine = null;
        EndLocalSearchPresentation();
    }

    private static void EndLocalSearchPresentation()
    {
        IsLocalSearchInProgress = false;
        if (AutoUIManager.Instance != null)
        {
            AutoUIManager.Instance.HideReloadUI();
            AutoUIManager.Instance.isDoingAction = false;
        }
    }

    private bool IsClosestCabinetForLocalPlayer(out PlayerMovement localPlayer)
    {
        localPlayer = PlayerMovement.LocalPlayerInstance;
        MainQuestManager manager = MainQuestManager.Instance;
        if (localPlayer == null || manager == null || !manager.IsCurrentOfficeObjective(CabinetId) ||
            manager.IsCabinetChecked(CabinetId) ||
            !CanPlayerSearch(localPlayer.transform.position))
            return false;

        MainQuestSearchCabinet closest = null;
        float closestDistance = float.PositiveInfinity;
        foreach (MainQuestSearchCabinet cabinet in Registry.Values)
        {
            if (cabinet == null || !cabinet.isActiveAndEnabled || !cabinet.CanPlayerSearch(localPlayer.transform.position)) continue;
            float distance = cabinet.DistanceTo(localPlayer.transform.position);
            if (distance < closestDistance)
            {
                closest = cabinet;
                closestDistance = distance;
            }
        }
        return closest == this;
    }

    public bool CanPlayerSearch(Vector3 playerPosition)
    {
        if (DistanceTo(playerPosition) > interactionDistance) return false;
        Vector2 searchPoint = cabinetCollider != null
            ? cabinetCollider.ClosestPoint(playerPosition)
            : (Vector2)transform.position;
        return obstacleMask.value == 0 || !Physics2D.Linecast(playerPosition, searchPoint, obstacleMask);
    }

    private float DistanceTo(Vector3 worldPosition)
    {
        Vector2 closest = cabinetCollider != null ? cabinetCollider.ClosestPoint(worldPosition) : transform.position;
        return Vector2.Distance(worldPosition, closest);
    }

    public static bool TryGet(int id, out MainQuestSearchCabinet cabinet)
    {
        return Registry.TryGetValue(id, out cabinet) && cabinet != null && cabinet.isActiveAndEnabled;
    }

    private int BuildStableId()
    {
        unchecked
        {
            uint hash = 2166136261;
            string key = gameObject.scene.path + "/" + BuildHierarchyPath(transform) + "/" +
                         Mathf.RoundToInt(transform.position.x * 100f) + "/" +
                         Mathf.RoundToInt(transform.position.y * 100f);
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.88f, 0.1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
    }
}
