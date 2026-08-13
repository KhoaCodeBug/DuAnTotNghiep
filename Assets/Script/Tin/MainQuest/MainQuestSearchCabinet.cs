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
    public LayerMask obstacleMask = 1 << 6;
    public Vector3 markerOffset = new Vector3(0f, 0.7f, 0f);

    private Collider2D cabinetCollider;
    public int CabinetId { get; private set; }

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
        if (Registry.TryGetValue(CabinetId, out MainQuestSearchCabinet current) && current == this)
            Registry.Remove(CabinetId);
    }

    private void Update()
    {
        if (!IsClosestCabinetForLocalPlayer(out PlayerMovement localPlayer)) return;
        if (AutoUIManager.Instance != null && AutoUIManager.Instance.IsAnyMenuOpen()) return;
        if (Input.GetKeyDown(KeyCode.E)) MainQuestManager.Instance.RequestSearchCabinet(CabinetId);
    }

    private void OnGUI()
    {
        MainQuestManager manager = MainQuestManager.Instance;
        if (manager == null || !manager.IsMapSearchActive || manager.IsQuestCutsceneActive) return;

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

        if (!IsClosestCabinetForLocalPlayer(out _)) return;
        GUIStyle promptStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 16,
            fontStyle = FontStyle.Bold
        };
        GUI.Box(new Rect(Screen.width * 0.5f - 185f, Screen.height - 105f, 370f, 42f),
            GameLocalization.TranslateLiteral("PRESS [E] TO SEARCH AREA"), promptStyle);
    }

    private bool IsClosestCabinetForLocalPlayer(out PlayerMovement localPlayer)
    {
        localPlayer = PlayerMovement.LocalPlayerInstance;
        MainQuestManager manager = MainQuestManager.Instance;
        if (localPlayer == null || manager == null || !manager.IsMapSearchActive ||
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
