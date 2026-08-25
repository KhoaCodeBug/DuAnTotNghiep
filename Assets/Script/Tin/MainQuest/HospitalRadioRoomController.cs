using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum HospitalRadioInteractionRole
{
    Door,
    Radio
}

/// <summary>
/// Pure room availability rules. Keeping these separate makes the authority
/// checks deterministic and lets EditMode tests cover the important gate.
/// </summary>
public static class HospitalRadioRoomRules
{
    public const int RestoreSegmentCount = 3;

    public static bool CanUseRadio(bool networkReady, MainQuestManager.QuestStage questStage,
        MainQuestManager.HospitalInvestigationStage hospitalStage, bool isOpen)
    {
        return CanOperateRadio(networkReady, questStage, hospitalStage, isOpen, false);
    }

    public static bool CanOperateRadio(bool networkReady, MainQuestManager.QuestStage questStage,
        MainQuestManager.HospitalInvestigationStage hospitalStage, bool isOpen, bool recovered)
    {
        return networkReady && questStage == MainQuestManager.QuestStage.FindCityMap && isOpen &&
               hospitalStage == MainQuestManager.HospitalInvestigationStage.RadioReady && !recovered;
    }

    public static float AdvanceRestore(float currentSeconds, float deltaSeconds, float durationSeconds)
    {
        if (durationSeconds <= 0f) return 0f;
        return Mathf.Clamp(currentSeconds + Mathf.Max(0f, deltaSeconds), 0f, durationSeconds);
    }

    public static float GetSegmentEndSeconds(int completedSegments, float durationSeconds)
    {
        int nextSegment = Mathf.Clamp(completedSegments + 1, 1, RestoreSegmentCount);
        return Mathf.Max(0f, durationSeconds) * nextSegment / RestoreSegmentCount;
    }

    public static float GetSegmentNormalizedProgress(float currentSeconds, int completedSegments,
        float durationSeconds)
    {
        if (durationSeconds <= 0f) return 0f;
        float segmentStart = durationSeconds * Mathf.Clamp(completedSegments, 0, RestoreSegmentCount - 1) /
                             RestoreSegmentCount;
        float segmentEnd = GetSegmentEndSeconds(completedSegments, durationSeconds);
        return Mathf.InverseLerp(segmentStart, segmentEnd, currentSeconds);
    }

    public static int GetThreatZombiesPerEntry(int difficulty)
    {
        return 3 + Mathf.Clamp(difficulty, 0, 2);
    }

    public static float GetThreatSpawnHorizontalOffset(int spawnIndex, int spawnCount, float spacing)
    {
        int safeCount = Mathf.Max(1, spawnCount);
        int safeIndex = Mathf.Clamp(spawnIndex, 0, safeCount - 1);
        return (safeIndex - (safeCount - 1) * 0.5f) * Mathf.Max(0f, spacing);
    }
}

public static class HospitalRadioMilestonePresentation
{
    private static AudioClip burstClip;

    public static void Play(Vector3 position)
    {
        GameObject host = new GameObject("Hospital Radio Noise Burst");
        host.transform.position = position;
        AudioSource source = host.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0.82f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 2.5f;
        source.maxDistance = 24f;
        source.volume = PlayerPrefs.GetFloat("GameMasterVolume", 1f) *
                        PlayerPrefs.GetFloat("GameSFXVolume", 0.8f) * 0.9f;
        source.clip = burstClip != null ? burstClip : burstClip = CreateBurstClip();
        source.Play();
        Object.Destroy(host, source.clip.length + 0.25f);
    }

    private static AudioClip CreateBurstClip()
    {
        const int sampleRate = 22050;
        // The second cycle is intentional: the first burst announces the
        // milestone and the repeated static remains audible while the last
        // sequential zombie in the difficulty-scaled wave is being spawned.
        const float cycleDuration = 1.35f;
        const int cycleCount = 2;
        const float duration = cycleDuration * cycleCount;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];
        System.Random random = new System.Random(1403);
        float filteredNoise = 0f;
        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)sampleRate;
            float cycleTime = time % cycleDuration;
            float envelope = Mathf.Clamp01(cycleTime / 0.04f) *
                             Mathf.Clamp01((cycleDuration - cycleTime) / 0.18f);
            float white = (float)(random.NextDouble() * 2.0 - 1.0);
            filteredNoise = Mathf.Lerp(filteredNoise, white, 0.38f);
            float warningPulse = Mathf.Sign(Mathf.Sin(time * Mathf.PI * 15f)) * 0.18f;
            samples[i] = Mathf.Clamp((filteredNoise * 0.62f + warningPulse) * envelope, -0.9f, 0.9f);
        }

        AudioClip clip = AudioClip.Create("Hospital Radio Milestone Burst", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}

/// <summary>
/// Applies the replicated H1 state to the authored hospital room. Awake closes
/// the visual and enables the blocker before the first rendered frame, so the
/// original hospital prefab asset remains untouched.
/// </summary>
[DisallowMultipleComponent]
public sealed class HospitalRadioRoomController : MonoBehaviour
{
    [SerializeField] private Tilemap doorTilemap;
    [SerializeField] private Vector3Int doorCell;
    [SerializeField] private TileBase closedDoorTile;
    [SerializeField] private TileBase openDoorTile;
    [SerializeField] private Collider2D doorBlocker;
    [SerializeField] private HospitalRadioInteractionPoint doorInteraction;
    [SerializeField] private HospitalRadioInteractionPoint radioInteraction;

    private bool? lastAppliedOpenState;

    private void Awake()
    {
        ApplyState(false, true);
    }

    private void Update()
    {
        MainQuestManager manager = MainQuestManager.Instance;
        bool isOpen = manager != null && manager.IsHospitalRadioDoorOpenState;
        ApplyState(isOpen, false);
    }

    private void ApplyState(bool isOpen, bool force)
    {
        if (!force && lastAppliedOpenState.HasValue && lastAppliedOpenState.Value == isOpen) return;
        lastAppliedOpenState = isOpen;

        if (doorTilemap != null)
            doorTilemap.SetTile(doorCell, isOpen ? openDoorTile : closedDoorTile);
        if (doorBlocker != null)
            doorBlocker.enabled = !isOpen;

        doorInteraction?.SetRoomStateAvailable(!isOpen);
        radioInteraction?.SetRoomStateAvailable(isOpen);
    }

#if UNITY_EDITOR
    public void EditorConfigure(Tilemap tilemap, Vector3Int cell, TileBase closedTile, TileBase openTile,
        Collider2D blocker, HospitalRadioInteractionPoint door, HospitalRadioInteractionPoint radio)
    {
        doorTilemap = tilemap;
        doorCell = cell;
        closedDoorTile = closedTile;
        openDoorTile = openTile;
        doorBlocker = blocker;
        doorInteraction = door;
        radioInteraction = radio;
        lastAppliedOpenState = null;
    }
#endif
}

/// <summary>
/// Local hold interaction plus a stable scene registry used by State Authority
/// to reject spoofed IDs and out-of-range requests.
/// </summary>
[DisallowMultipleComponent]
public sealed class HospitalRadioInteractionPoint : MonoBehaviour
{
    private static readonly Dictionary<int, HospitalRadioInteractionPoint> Registry =
        new Dictionary<int, HospitalRadioInteractionPoint>();

    [SerializeField] private HospitalRadioInteractionRole role;
    [SerializeField, Min(0.2f)] private float interactionDistance = 0.65f;
    [SerializeField] private PolygonCollider2D interactionZone;
    [SerializeField, Min(0.1f)] private float holdDuration = 0.6f;
    [SerializeField] private LayerMask obstacleMask = 1 << 6;
    [SerializeField] private Collider2D ignoredObstacle;
    [SerializeField] private Vector3 markerOffset = new Vector3(0f, 0.6f, 0f);

    private Coroutine holdRoutine;
    private bool roomStateAvailable;
    private bool radioRequestActive;
    private int requestedRadioCheckpoint = -1;

    public HospitalRadioInteractionRole Role => role;
    public int InteractionId { get; private set; }

    private void Awake()
    {
        InteractionId = BuildStableId();
    }

    private void OnEnable()
    {
        if (InteractionId == 0) InteractionId = BuildStableId();
        if (Registry.TryGetValue(InteractionId, out HospitalRadioInteractionPoint existing) && existing != this)
            Debug.LogError($"[HOSPITAL H1] Hai điểm tương tác trùng ID: {existing.name} và {name}.");
        Registry[InteractionId] = this;
    }

    private void OnDisable()
    {
        CancelHold();
        CancelRadioOperation();
        if (Registry.TryGetValue(InteractionId, out HospitalRadioInteractionPoint current) && current == this)
            Registry.Remove(InteractionId);
    }

    private void Update()
    {
        if (role == HospitalRadioInteractionRole.Radio)
        {
            UpdateRadioOperation();
            return;
        }

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
        markerStyle.normal.textColor = role == HospitalRadioInteractionRole.Door
            ? new Color(1f, 0.68f, 0.12f)
            : new Color(0.2f, 0.9f, 0.82f);
        GUI.Label(new Rect(screenPoint.x - 18f, Screen.height - screenPoint.y - 18f, 36f, 36f), "●", markerStyle);

        if (holdRoutine != null) return;
        const float width = 460f;
        float height = role == HospitalRadioInteractionRole.Radio ? 82f : 54f;
        Rect card = new Rect(Screen.width * 0.5f - width * 0.5f, 72f, width, height);
        DrawRect(card, new Color(0.025f, 0.045f, 0.043f, 0.96f));
        DrawRect(new Rect(card.x, card.y, 4f, card.height), markerStyle.normal.textColor);

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 16,
            fontStyle = FontStyle.Bold
        };
        style.normal.textColor = new Color(0.94f, 0.97f, 0.96f);
        Rect labelRect = role == HospitalRadioInteractionRole.Radio
            ? new Rect(card.x + 12f, card.y + 4f, card.width - 24f, 43f)
            : card;
        GUI.Label(labelRect, GetInteractionLabel(), style);
        if (role == HospitalRadioInteractionRole.Radio)
            DrawRadioSegments(card);
    }

    private IEnumerator HoldRoutine(PlayerMovement localPlayer)
    {
        if (AutoUIManager.Instance != null) AutoUIManager.Instance.isDoingAction = true;
        float elapsed = 0f;
        while (elapsed < holdDuration)
        {
            if (localPlayer == null || !IsLocallyAvailable() ||
                !CanPlayerInteract(localPlayer.transform.position) ||
                !Input.GetKey(KeyCode.E) ||
                LocalGameplayUIState.BlocksWorldInteractionHints)
            {
                EndHoldPresentation();
                holdRoutine = null;
                yield break;
            }

            elapsed = Mathf.Min(holdDuration, elapsed + Time.unscaledDeltaTime);
            AutoUIManager.Instance?.ShowReloadUI(elapsed, holdDuration, GetProgressLabel());
            yield return null;
        }

        EndHoldPresentation();
        holdRoutine = null;
        MainQuestManager.Instance?.RequestHospitalRadioInteraction(InteractionId);
    }

    private void UpdateRadioOperation()
    {
        MainQuestManager manager = MainQuestManager.Instance;
        bool available = IsLocallyAvailable();
        PlayerMovement localPlayer = null;
        bool closest = available && IsClosestForLocalPlayer(out localPlayer);
        bool blocked = LocalGameplayUIState.BlocksWorldInteractionHints ||
                       MainQuestSearchCabinet.IsLocalSearchInProgress;

        if (radioRequestActive)
        {
            bool anotherOperator = manager != null && manager.HasHospitalRadioOperator &&
                                   !manager.IsLocalPlayerHospitalRadioOperator;
            bool milestoneReached = manager != null &&
                                    manager.HospitalRadioCheckpointCountState > requestedRadioCheckpoint;
            if (!closest || localPlayer == null || blocked || !Input.GetKey(KeyCode.E) || anotherOperator ||
                milestoneReached)
            {
                CancelRadioOperation();
                return;
            }
            return;
        }

        if (!closest || localPlayer == null || blocked || manager == null || manager.HasHospitalRadioOperator)
            return;
        if (AutoUIManager.Instance != null && AutoUIManager.Instance.isDoingAction) return;
        if (!Input.GetKeyDown(KeyCode.E)) return;

        radioRequestActive = true;
        requestedRadioCheckpoint = manager.HospitalRadioCheckpointCountState;
        if (AutoUIManager.Instance != null) AutoUIManager.Instance.isDoingAction = true;
        manager.RequestSetHospitalRadioOperating(InteractionId, true);
    }

    private bool IsLocallyAvailable()
    {
        if (!roomStateAvailable) return false;
        MainQuestManager manager = MainQuestManager.Instance;
        if (manager == null || !manager.IsNetworkReady || manager.IsQuestCutsceneActive) return false;
        if (role == HospitalRadioInteractionRole.Radio)
            return HospitalRadioRoomRules.CanOperateRadio(true, manager.CurrentStage,
                manager.CurrentHospitalInvestigationStage, manager.IsHospitalRadioDoorOpenState,
                manager.IsHospitalRadioRecoveredState);
        return HospitalInvestigationRules.IsDoorDiscoverable(true, manager.CurrentStage,
            manager.CurrentHospitalInvestigationStage, manager.IsHospitalRadioDoorOpenState);
    }

    private bool IsClosestForLocalPlayer(out PlayerMovement localPlayer)
    {
        localPlayer = PlayerMovement.LocalPlayerInstance;
        if (localPlayer == null || !CanPlayerInteract(localPlayer.transform.position)) return false;

        HospitalRadioInteractionPoint closest = null;
        float closestDistance = float.PositiveInfinity;
        foreach (HospitalRadioInteractionPoint point in Registry.Values)
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
        if (interactionZone != null && interactionZone.enabled)
            return interactionZone.OverlapPoint(playerPosition);
        if (Vector2.Distance(playerPosition, transform.position) > interactionDistance) return false;
        if (obstacleMask.value == 0) return true;

        RaycastHit2D[] hits = Physics2D.LinecastAll(playerPosition, transform.position, obstacleMask);
        for (int i = 0; i < hits.Length; i++)
            if (hits[i].collider != null && hits[i].collider != ignoredObstacle) return false;
        return true;
    }

    public void SetRoomStateAvailable(bool available)
    {
        roomStateAvailable = available;
        if (!available)
        {
            CancelHold();
            CancelRadioOperation();
        }
    }

    public static bool TryGet(int id, out HospitalRadioInteractionPoint point)
    {
        return Registry.TryGetValue(id, out point) && point != null && point.isActiveAndEnabled;
    }

    public static bool TryGetForRole(HospitalRadioInteractionRole targetRole,
        out HospitalRadioInteractionPoint point)
    {
        foreach (HospitalRadioInteractionPoint candidate in Registry.Values)
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

    private void CancelRadioOperation()
    {
        if (!radioRequestActive) return;
        radioRequestActive = false;
        requestedRadioCheckpoint = -1;
        MainQuestManager manager = MainQuestManager.Instance;
        if (manager != null && manager.IsNetworkReady && !manager.IsHospitalRadioRecoveredState)
            manager.RequestSetHospitalRadioOperating(InteractionId, false);
        EndHoldPresentation();
    }

    private static void EndHoldPresentation()
    {
        if (AutoUIManager.Instance == null) return;
        AutoUIManager.Instance.HideReloadUI();
        AutoUIManager.Instance.isDoingAction = false;
    }

    private string GetInteractionLabel()
    {
        bool vietnamese = QuestUILocalization.IsVietnamese;
        if (role == HospitalRadioInteractionRole.Door)
            return vietnamese ? "GIỮ [E] ĐỂ MỞ CỬA PHÒNG RADIO" : "HOLD [E] TO OPEN RADIO ROOM";
        MainQuestManager manager = MainQuestManager.Instance;
        int percent = manager != null ? Mathf.RoundToInt(manager.HospitalRadioRestoreNormalized * 100f) : 0;
        int segment = manager != null ? Mathf.Clamp(manager.HospitalRadioCheckpointCountState + 1, 1, 3) : 1;
        if (radioRequestActive)
            return vietnamese ? $"ĐANG SỬA RADIO  •  CHẶNG {segment}/3" : $"REPAIRING RADIO  •  STAGE {segment}/3";
        if (manager != null && manager.HasHospitalRadioOperator && !manager.IsLocalPlayerHospitalRadioOperator)
            return vietnamese ? $"ĐỒNG ĐỘI ĐANG SỬA  •  CHẶNG {segment}/3" : $"TEAMMATE REPAIRING  •  STAGE {segment}/3";
        if (percent > 0)
            return vietnamese ? $"GIỮ [E] ĐỂ SỬA CHẶNG {segment}/3  •  {percent}%" : $"HOLD [E] FOR STAGE {segment}/3  •  {percent}%";
        return vietnamese ? "GIỮ [E] ĐỂ SỬA RADIO  •  CHẶNG 1/3" : "HOLD [E] TO REPAIR RADIO  •  STAGE 1/3";
    }

    private void DrawRadioSegments(Rect card)
    {
        MainQuestManager manager = MainQuestManager.Instance;
        float progress = manager != null ? manager.HospitalRadioRestoreNormalized : 0f;
        const float gap = 8f;
        float totalWidth = card.width - 44f;
        float segmentWidth = (totalWidth - gap * 2f) / 3f;
        float y = card.yMax - 24f;
        for (int i = 0; i < HospitalRadioRoomRules.RestoreSegmentCount; i++)
        {
            Rect background = new Rect(card.x + 22f + i * (segmentWidth + gap), y, segmentWidth, 9f);
            DrawRect(background, new Color(0.18f, 0.16f, 0.08f, 1f));
            float segmentProgress = Mathf.Clamp01(progress * 3f - i);
            if (segmentProgress <= 0f) continue;
            DrawRect(new Rect(background.x, background.y, background.width * segmentProgress, background.height),
                new Color(1f, 0.72f, 0.12f, 1f));
        }
    }

    private string GetProgressLabel()
    {
        bool vietnamese = QuestUILocalization.IsVietnamese;
        if (role == HospitalRadioInteractionRole.Door)
            return vietnamese ? "ĐANG MỞ CỬA..." : "OPENING DOOR...";
        return vietnamese ? "ĐANG KIỂM TRA RADIO..." : "CHECKING RADIO...";
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
    public void EditorConfigure(HospitalRadioInteractionRole configuredRole, float distance, float duration,
        LayerMask obstacles, Collider2D obstacleToIgnore, PolygonCollider2D zone)
    {
        role = configuredRole;
        interactionDistance = distance;
        holdDuration = duration;
        obstacleMask = obstacles;
        ignoredObstacle = obstacleToIgnore;
        interactionZone = zone;
        InteractionId = BuildStableId();
    }
#endif
}
