using System.Collections.Generic;
using UnityEngine;

/// <summary>Put this on KhuVucNhiemVu to begin the office-map objective.</summary>
[DisallowMultipleComponent]
public sealed class MainQuestStartTrigger : MonoBehaviour
{
    private static readonly Dictionary<int, MainQuestStartTrigger> Registry = new Dictionary<int, MainQuestStartTrigger>();

    [SerializeField, Min(0f)] private float insidePadding = 0.15f;
    private Collider2D triggerCollider;
    private float nextLocalRequestTime;

    public int TriggerId { get; private set; }

    private void Reset()
    {
        Collider2D area = GetComponent<Collider2D>();
        if (area != null) area.isTrigger = true;
    }

    private void Awake()
    {
        triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider == null)
            Debug.LogError("[MAIN QUEST] KhuVucNhiemVu cần một Collider2D bật Is Trigger.", this);
        TriggerId = BuildStableId();
    }

    private void OnEnable()
    {
        if (TriggerId == 0) TriggerId = BuildStableId();
        Registry[TriggerId] = this;
    }

    private void OnDisable()
    {
        if (Registry.TryGetValue(TriggerId, out MainQuestStartTrigger current) && current == this)
            Registry.Remove(TriggerId);
    }

    private void Update()
    {
        MainQuestManager manager = MainQuestManager.Instance;
        if (Time.unscaledTime < nextLocalRequestTime || manager == null || !manager.IsNetworkReady ||
            manager.CurrentStage != MainQuestManager.QuestStage.LocateOffice)
            return;

        PlayerMovement localPlayer = PlayerMovement.LocalPlayerInstance;
        if (localPlayer != null && Contains(localPlayer.transform.position)) RequestStart();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player != null && player.HasInputAuthority)
        {
            PreMilitaryQuestRuntimeBridge.NotifyOfficeEntered(this);
            RequestStart();
        }
    }

    private void RequestStart()
    {
        MainQuestManager manager = MainQuestManager.Instance;
        if (manager == null || !manager.IsNetworkReady ||
            manager.CurrentStage != MainQuestManager.QuestStage.LocateOffice)
            return;

        // A short retry cooldown handles a request arriving while the Host is
        // still resolving the player's authoritative transform.
        nextLocalRequestTime = Time.unscaledTime + 1f;
        manager.RequestStartMapSearch(TriggerId);
    }

    public bool Contains(Vector3 worldPosition)
    {
        if (triggerCollider == null) return false;
        Vector2 nearest = triggerCollider.ClosestPoint(worldPosition);
        return Vector2.Distance(nearest, worldPosition) <= insidePadding;
    }

    public static bool TryGet(int id, out MainQuestStartTrigger trigger)
    {
        return Registry.TryGetValue(id, out trigger) && trigger != null && trigger.isActiveAndEnabled;
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
}
