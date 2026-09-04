using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Client-side detector for a personal story checkpoint. It never mutates
/// progress directly: State Authority re-resolves this trigger and validates
/// the requesting player's authoritative avatar position.
/// </summary>
[DisallowMultipleComponent]
public sealed class StoryCheckpointArrivalTrigger : MonoBehaviour
{
    private static readonly Dictionary<int, StoryCheckpointArrivalTrigger> Registry =
        new Dictionary<int, StoryCheckpointArrivalTrigger>();

    [SerializeField] private StoryCheckpoint checkpoint = StoryCheckpoint.OfficeHospital;
    [SerializeField, Min(0f)] private float insidePadding = 0.15f;

    private Collider2D triggerCollider;
    private float nextLocalRequestTime;
    private bool localArrivalAccepted;

    public int TriggerId { get; private set; }
    public StoryCheckpoint Checkpoint => checkpoint;

    private void Reset()
    {
        Collider2D area = GetComponent<Collider2D>();
        if (area != null) area.isTrigger = true;
    }

    private void Awake()
    {
        triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider == null)
            Debug.LogError("[STORY CHECKPOINT] Arrival trigger requires a Collider2D.", this);
        TriggerId = BuildStableId();
    }

    private void OnEnable()
    {
        if (TriggerId == 0) TriggerId = BuildStableId();
        Registry[TriggerId] = this;
    }

    private void OnDisable()
    {
        if (Registry.TryGetValue(TriggerId, out StoryCheckpointArrivalTrigger current) && current == this)
            Registry.Remove(TriggerId);
    }

    private void Update()
    {
        if (localArrivalAccepted || Time.unscaledTime < nextLocalRequestTime) return;
        PlayerMovement localPlayer = PlayerMovement.LocalPlayerInstance;
        if (localPlayer != null && localPlayer.HasInputAuthority && Contains(localPlayer.transform.position))
            RequestArrival();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerMovement player = other != null ? other.GetComponentInParent<PlayerMovement>() : null;
        if (player != null && player.HasInputAuthority) RequestArrival();
    }

    private void RequestArrival()
    {
        if (localArrivalAccepted) return;
        MainQuestManager manager = MainQuestManager.Instance;
        if (manager == null || !manager.IsNetworkReady) return;
        nextLocalRequestTime = Time.unscaledTime + 1f;
        manager.RequestStoryCheckpointArrival(TriggerId);
    }

    public void ConfirmLocalArrivalAccepted()
    {
        localArrivalAccepted = true;
    }

    public bool Contains(Vector3 worldPosition)
    {
        if (triggerCollider == null) return false;
        Vector2 nearest = triggerCollider.ClosestPoint(worldPosition);
        return Vector2.Distance(nearest, worldPosition) <= insidePadding;
    }

    public static bool TryGet(int id, out StoryCheckpointArrivalTrigger trigger)
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
