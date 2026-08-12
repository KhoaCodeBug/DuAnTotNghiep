using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class SleepInteractable : MonoBehaviour
{
    private static readonly Dictionary<int, SleepInteractable> Registry = new Dictionary<int, SleepInteractable>();

    [Header("Tương tác giường")]
    [Min(0.3f)] public float interactionDistance = 0.7f;
    public string prompt = "NHẤN [E] ĐỂ NGỦ";

    private Collider2D bedCollider;
    public int BedId { get; private set; }

    private void Reset()
    {
        BoxCollider2D boxCollider = GetComponent<BoxCollider2D>();
        if (boxCollider != null) boxCollider.isTrigger = true;
    }

    private void Awake()
    {
        bedCollider = GetComponent<Collider2D>();
        BedId = BuildStableBedId();
    }

    private void OnEnable()
    {
        if (BedId == 0) BedId = BuildStableBedId();
        if (Registry.TryGetValue(BedId, out SleepInteractable existing) && existing != this)
            Debug.LogError($"[SLEEP] Hai giường trùng BedId: {existing.name} và {name}. Hãy đặt chúng ở vị trí/tên khác nhau.");
        Registry[BedId] = this;
    }

    private void OnDisable()
    {
        if (Registry.TryGetValue(BedId, out SleepInteractable current) && current == this)
            Registry.Remove(BedId);
    }

    private void Update()
    {
        PlayerMovement movement = PlayerMovement.LocalPlayerInstance;
        if (movement == null || !IsClosestUsableBed(movement.transform)) return;

        PlayerSurvival survival = movement.GetComponent<PlayerSurvival>();
        if (survival == null || survival.IsSleepInputLocked) return;
        if (AutoUIManager.Instance != null && AutoUIManager.Instance.IsAnyMenuOpen()) return;

        if (Input.GetKeyDown(KeyCode.E))
            survival.TrySleepAtBed(this);
    }

    private void OnGUI()
    {
        PlayerMovement movement = PlayerMovement.LocalPlayerInstance;
        if (movement == null || !IsClosestUsableBed(movement.transform)) return;

        PlayerSurvival survival = movement.GetComponent<PlayerSurvival>();
        if (survival == null || survival.IsSleepInputLocked) return;

        string text = DayNightManager.Instance != null && !DayNightManager.Instance.CanUseBedNow()
            ? "CHỈ CÓ THỂ NGỦ TỪ 20:00 ĐẾN 03:00"
            : prompt;

        GUIStyle style = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 16,
            fontStyle = FontStyle.Bold
        };
        GUI.Box(new Rect(Screen.width * 0.5f - 180f, Screen.height - 105f, 360f, 42f), text, style);
    }

    private bool IsClosestUsableBed(Transform player)
    {
        if (player == null || DistanceTo(player.position) > interactionDistance) return false;

        SleepInteractable closest = null;
        float closestDistance = float.PositiveInfinity;
        foreach (SleepInteractable bed in Registry.Values)
        {
            if (bed == null || !bed.isActiveAndEnabled) continue;
            float distance = bed.DistanceTo(player.position);
            if (distance < closestDistance)
            {
                closest = bed;
                closestDistance = distance;
            }
        }
        return closest == this;
    }

    public float DistanceTo(Vector3 worldPosition)
    {
        Vector2 closest = bedCollider != null ? bedCollider.ClosestPoint(worldPosition) : (Vector2)transform.position;
        return Vector2.Distance(worldPosition, closest);
    }

    public static bool TryGetBed(int id, out SleepInteractable bed)
    {
        return Registry.TryGetValue(id, out bed) && bed != null && bed.isActiveAndEnabled;
    }

    private int BuildStableBedId()
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
        Gizmos.color = new Color(0.35f, 0.65f, 1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
    }
}
