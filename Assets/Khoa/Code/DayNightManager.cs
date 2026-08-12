using System.Collections.Generic;
using Fusion;
using Pathfinding;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class DayNightManager : NetworkBehaviour
{
    public enum SleepPhase
    {
        None,
        VoluntaryFade,
        ForcedFade,
        Waking
    }

    public static DayNightManager Instance { get; private set; }

    [Header("=== Cài đặt Thời gian ===")]
    [Tooltip("Bao nhiêu phút ngoài đời = 1 ngày trong game?")]
    public float realMinutesPerDay = 10f;

    [Networked] public float CurrentTime { get; set; }

    [Header("=== Môi trường (Global Light) ===")]
    public Light2D globalLight;
    public AnimationCurve globalIntensityCurve;
    public Gradient skyColorCurve;

    [Header("=== Giao diện UI ===")]
    public TextMeshProUGUI clockText;

    [Header("=== Ngủ & phục kích ===")]
    [Tooltip("Zombie dùng cho sự kiện bị đánh thức. Có thể để trống khi chưa setup giường, nhưng sự kiện sẽ chỉ gây đòn đánh bảo đảm.")]
    public NetworkObject ambushZombiePrefab;
    [Tooltip("Chỉ zombie trong vòng tròn nhỏ này mới làm tăng nguy cơ phục kích khi ngủ trên giường.")]
    [Min(0.5f)] public float ambushSafetyRadius = 3f;
    [Tooltip("Tường/vật cản chắn giữa Player và zombie sẽ làm zombie đó không được tính vào nguy cơ phục kích.")]
    public LayerMask ambushObstacleMask = 1 << 6;
    [Min(0.1f)] public float ambushSpawnDistance = 0.9f;
    [Min(0f)] public float ambushGuaranteedDamage = 10f;
    [Min(0.25f)] public float sleepFadeSeconds = 1.4f;
    [Min(0.25f)] public float wakeFadeSeconds = 1.2f;

    [SerializeField, HideInInspector] private int sleepSystemVersion;

    [Networked] public int NetworkSleepPhase { get; set; }
    [Networked] public TickTimer SleepPhaseTimer { get; set; }
    [Networked] public float SleepStartHour { get; set; }
    [Networked] public float PendingWakeHour { get; set; }

    private readonly HashSet<PlayerRef> pendingAmbushPlayers = new HashSet<PlayerRef>();
    private bool isSpawned;
    private int lastAnnouncedSleeping = -1;
    private int lastAnnouncedTotal = -1;

    private const float SleepSequenceSeconds = 6f;
    private const float ForcedFadeToBlackSeconds = 0.65f;

    public SleepPhase CurrentSleepPhase => (SleepPhase)NetworkSleepPhase;
    public bool IsSleepTransitionActive => CurrentSleepPhase != SleepPhase.None;

    private void Awake()
    {
        ApplySleepSystemDefaultsIfNeeded();
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void OnValidate()
    {
        if (!ApplySleepSystemDefaultsIfNeeded()) return;

#if UNITY_EDITOR
        if (ambushZombiePrefab == null)
        {
            GameObject zombiePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Khoa/Zombie2Khoa.prefab");
            if (zombiePrefab != null) ambushZombiePrefab = zombiePrefab.GetComponent<NetworkObject>();
        }

        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    private bool ApplySleepSystemDefaultsIfNeeded()
    {
        if (sleepSystemVersion >= 1) return false;

        realMinutesPerDay = 10f;
        ambushSafetyRadius = 3f;
        ambushObstacleMask = 1 << 6;
        ambushSpawnDistance = 0.9f;
        ambushGuaranteedDamage = 10f;
        sleepFadeSeconds = 1.4f;
        wakeFadeSeconds = 1.2f;
        sleepSystemVersion = 1;
        return true;
    }

    public override void Spawned()
    {
        base.Spawned();
        isSpawned = true;

        if (HasStateAuthority)
        {
            CurrentTime = 12f;
            NetworkSleepPhase = (int)SleepPhase.None;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        if (TutorialSession.IsActive)
        {
            CurrentTime = 12f;
            return;
        }

        if (IsSleepTransitionActive)
        {
            UpdateSleepTransition();
            return;
        }

        float previousTime = CurrentTime;
        float safeMinutesPerDay = Mathf.Max(0.1f, realMinutesPerDay);
        float timeSpeed = 24f / (safeMinutesPerDay * 60f);
        CurrentTime += Runner.DeltaTime * timeSpeed;
        if (CurrentTime >= 24f) CurrentTime -= 24f;

        List<PlayerSurvival> alivePlayers = GetAlivePlayers();
        for (int i = 0; i < alivePlayers.Count; i++)
            alivePlayers[i].ServerResetRestForNextNight(CurrentTime);

        AnnounceSleepCountIfChanged(alivePlayers);

        int sleepingCount = CountWaitingPlayers(alivePlayers);
        if (alivePlayers.Count > 0 && sleepingCount == alivePlayers.Count)
        {
            BeginGroupSleep(alivePlayers, false);
            return;
        }

        if (CrossedHour(previousTime, CurrentTime, 3f))
            BeginGroupSleep(alivePlayers, true);
    }

    private void UpdateSleepTransition()
    {
        SleepPhase phase = CurrentSleepPhase;
        if (phase == SleepPhase.VoluntaryFade || phase == SleepPhase.ForcedFade)
        {
            float remaining = SleepPhaseTimer.RemainingTime(Runner) ?? 0f;
            float progress = 1f - Mathf.Clamp01(remaining / SleepSequenceSeconds);
            CurrentTime = LerpClockForward(SleepStartHour, PendingWakeHour, progress);

            if (!SleepPhaseTimer.Expired(Runner)) return;

            CurrentTime = PendingWakeHour;
            List<PlayerSurvival> alivePlayers = GetAlivePlayers();

            for (int i = 0; i < alivePlayers.Count; i++)
                alivePlayers[i].ServerFinishSleep();

            SpawnPendingAmbushes(alivePlayers);
            NetworkSleepPhase = (int)SleepPhase.Waking;
            SleepPhaseTimer = TickTimer.CreateFromSeconds(Runner, wakeFadeSeconds);
            AnnounceSleepCountIfChanged(alivePlayers, true);
            return;
        }

        if (!SleepPhaseTimer.Expired(Runner)) return;

        if (phase == SleepPhase.Waking)
        {
            pendingAmbushPlayers.Clear();
            NetworkSleepPhase = (int)SleepPhase.None;
            SleepPhaseTimer = TickTimer.None;
        }
    }

    private void BeginGroupSleep(List<PlayerSurvival> alivePlayers, bool forced)
    {
        if (alivePlayers == null || alivePlayers.Count == 0 || IsSleepTransitionActive) return;

        pendingAmbushPlayers.Clear();
        SleepStartHour = CurrentTime;

        if (forced)
        {
            for (int i = 0; i < alivePlayers.Count; i++)
                pendingAmbushPlayers.Add(alivePlayers[i].Object.InputAuthority);

            PendingWakeHour = 4f;
            NetworkSleepPhase = (int)SleepPhase.ForcedFade;
            RPC_AnnounceSleepEvent("03:00 — Cả đội đã kiệt sức và bất tỉnh ngoài ý muốn!");
        }
        else
        {
            float totalBedtime = 0f;
            for (int i = 0; i < alivePlayers.Count; i++)
            {
                PlayerSurvival player = alivePlayers[i];
                totalBedtime += NightElapsedFrom20(player.SleepRequestedAtHour);

                float nearestZombie = FindNearestZombieDistance(player.transform.position, ambushSafetyRadius);
                float chance = GetAmbushChance(nearestZombie);
                if (chance > 0f && Random.value < chance)
                    pendingAmbushPlayers.Add(player.Object.InputAuthority);
            }

            bool anyAmbush = pendingAmbushPlayers.Count > 0;
            PendingWakeHour = anyAmbush
                ? 4f
                : RollSharedWakeHour(totalBedtime / Mathf.Max(1, alivePlayers.Count));
            NetworkSleepPhase = (int)SleepPhase.VoluntaryFade;
            RPC_AnnounceSleepEvent(anyAmbush
                ? "Cả đội đã ngủ. Có gì đó đang di chuyển rất gần nơi trú ẩn..."
                : "Tất cả người chơi đã lên giường. Đang tua đến sáng...");
        }

        SleepPhaseTimer = TickTimer.CreateFromSeconds(Runner, SleepSequenceSeconds);
    }

    private void SpawnPendingAmbushes(List<PlayerSurvival> alivePlayers)
    {
        if (pendingAmbushPlayers.Count == 0) return;

        for (int i = 0; i < alivePlayers.Count; i++)
        {
            PlayerSurvival survival = alivePlayers[i];
            if (!pendingAmbushPlayers.Contains(survival.Object.InputAuthority)) continue;

            PlayerHealth health = survival.GetComponent<PlayerHealth>();
            if (health == null || health.isDead) continue;

            if (ambushZombiePrefab != null)
            {
                Vector3 spawnPosition = FindAmbushSpawnPosition(survival.transform.position);
                Vector2 facing = ((Vector2)survival.transform.position - (Vector2)spawnPosition).normalized;
                NetworkObject zombie = Runner.Spawn(ambushZombiePrefab, spawnPosition, Quaternion.identity, null,
                    (runner, spawned) =>
                    {
                        ZOmbieAI_Khoa oldAI = spawned.GetComponent<ZOmbieAI_Khoa>();
                        if (oldAI != null) oldAI.ConfigureTutorialSpawn(facing, 100f, false);
                        else spawned.GetComponent<ZombieAIKhoaRebuilt>()?.ConfigureTutorialSpawn(facing, 100f, false);
                    });
                if (zombie != null)
                {
                    ZOmbieAI_Khoa oldAI = zombie.GetComponent<ZOmbieAI_Khoa>();
                    if (oldAI != null) oldAI.ReleaseTutorialStationary(survival.transform.position);
                    else zombie.GetComponent<ZombieAIKhoaRebuilt>()?.ReleaseTutorialStationary(survival.transform.position);
                }
            }
            else
            {
                Debug.LogError("[SLEEP] Chưa gán Ambush Zombie Prefab trên Day_Night_System.");
            }

            // Không có sàn máu: người chơi ngủ ở vị trí nguy hiểm phải chịu hậu quả.
            health.TakeDamage(ambushGuaranteedDamage, false, true);
            survival.RPC_ShowSleepMessage("BẠN BỊ ZOMBIE ĐÁNH THỨC!");
        }
    }

    private Vector3 FindAmbushSpawnPosition(Vector3 playerPosition)
    {
        for (int attempt = 0; attempt < 12; attempt++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector2 candidate = (Vector2)playerPosition +
                                new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ambushSpawnDistance;
            if (ambushObstacleMask.value != 0 && Physics2D.Linecast(playerPosition, candidate, ambushObstacleMask))
                continue;
            if (AstarPath.active == null) return candidate;

            GraphNode node = AstarPath.active.GetNearest(candidate).node;
            if (node != null && node.Walkable)
            {
                Vector3 nodePosition = (Vector3)node.position;
                if (ambushObstacleMask.value == 0 || !Physics2D.Linecast(playerPosition, nodePosition, ambushObstacleMask))
                    return nodePosition;
            }
        }

        return playerPosition + Vector3.right * ambushSpawnDistance;
    }

    private float FindNearestZombieDistance(Vector3 playerPosition, float radius)
    {
        float nearest = float.PositiveInfinity;

        ZOmbieAI_Khoa[] khoaZombies = FindObjectsByType<ZOmbieAI_Khoa>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < khoaZombies.Length; i++)
        {
            if (khoaZombies[i] == null || khoaZombies[i].NetIsDead) continue;
            float distance = Vector2.Distance(playerPosition, khoaZombies[i].transform.position);
            if (distance <= radius && !IsAmbushPathBlocked(playerPosition, khoaZombies[i].transform.position))
                nearest = Mathf.Min(nearest, distance);
        }

        ZombieAIKhoaRebuilt[] rebuiltZombies = FindObjectsByType<ZombieAIKhoaRebuilt>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < rebuiltZombies.Length; i++)
        {
            if (rebuiltZombies[i] == null || rebuiltZombies[i].NetIsDead) continue;
            float distance = Vector2.Distance(playerPosition, rebuiltZombies[i].transform.position);
            if (distance <= radius && !IsAmbushPathBlocked(playerPosition, rebuiltZombies[i].transform.position))
                nearest = Mathf.Min(nearest, distance);
        }

        ZombieAI[] thaiZombies = FindObjectsByType<ZombieAI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < thaiZombies.Length; i++)
        {
            if (thaiZombies[i] == null) continue;
            float distance = Vector2.Distance(playerPosition, thaiZombies[i].transform.position);
            if (distance <= radius && !IsAmbushPathBlocked(playerPosition, thaiZombies[i].transform.position))
                nearest = Mathf.Min(nearest, distance);
        }

        return nearest <= radius ? nearest : float.PositiveInfinity;
    }

    private bool IsAmbushPathBlocked(Vector3 from, Vector3 to)
    {
        return ambushObstacleMask.value != 0 && Physics2D.Linecast(from, to, ambushObstacleMask);
    }

    private float GetAmbushChance(float nearestDistance)
    {
        if (float.IsPositiveInfinity(nearestDistance) || nearestDistance > ambushSafetyRadius) return 0f;

        float ratio = 1f - Mathf.Clamp01(nearestDistance / Mathf.Max(0.1f, ambushSafetyRadius));
        // Zombie sát giường: tối đa 85%. Zombie ở rìa vòng an toàn: gần 0%.
        return Mathf.Lerp(0.05f, 0.85f, ratio * ratio);
    }

    private float RollSharedWakeHour(float averageNightElapsed)
    {
        float lateness = Mathf.InverseLerp(0f, 7f, averageNightElapsed);
        float biasPower = Mathf.Lerp(2.5f, 0.65f, lateness);
        return Mathf.Lerp(5f, 7f, Mathf.Pow(Random.value, biasPower));
    }

    private static float NightElapsedFrom20(float hour)
    {
        return hour >= 20f ? hour - 20f : hour + 4f;
    }

    private static float LerpClockForward(float startHour, float endHour, float progress)
    {
        float forwardHours = Mathf.Repeat(endHour - startHour, 24f);
        return Mathf.Repeat(startHour + forwardHours * Mathf.Clamp01(progress), 24f);
    }

    private static bool CrossedHour(float previous, float current, float target)
    {
        if (previous <= current) return previous < target && current >= target;
        return previous < target || current >= target;
    }

    private List<PlayerSurvival> GetAlivePlayers()
    {
        PlayerSurvival[] all = FindObjectsByType<PlayerSurvival>(FindObjectsSortMode.None);
        List<PlayerSurvival> alive = new List<PlayerSurvival>(all.Length);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null || all[i].Object == null || !all[i].Object.IsValid) continue;
            PlayerHealth health = all[i].GetComponent<PlayerHealth>();
            if (health != null && !health.isDead && !health.isTransforming)
                alive.Add(all[i]);
        }
        return alive;
    }

    private static int CountWaitingPlayers(List<PlayerSurvival> players)
    {
        int count = 0;
        for (int i = 0; i < players.Count; i++)
            if (players[i].IsWaitingForSleep) count++;
        return count;
    }

    private void AnnounceSleepCountIfChanged(List<PlayerSurvival> players, bool force = false)
    {
        int sleeping = CountWaitingPlayers(players);
        int total = players.Count;
        if (!force && sleeping == lastAnnouncedSleeping && total == lastAnnouncedTotal) return;

        bool hadPreviousState = lastAnnouncedSleeping >= 0;
        int previousSleeping = lastAnnouncedSleeping;
        lastAnnouncedSleeping = sleeping;
        lastAnnouncedTotal = total;

        if (sleeping > 0 || (hadPreviousState && previousSleeping > 0) || force)
            RPC_AnnounceSleepCount(sleeping, total);
    }

    public void GetSleepCounts(out int sleeping, out int total)
    {
        PlayerSurvival[] all = FindObjectsByType<PlayerSurvival>(FindObjectsSortMode.None);
        sleeping = 0;
        total = 0;
        for (int i = 0; i < all.Length; i++)
        {
            PlayerHealth health = all[i].GetComponent<PlayerHealth>();
            if (health == null || health.isDead || health.isTransforming) continue;
            total++;
            if (all[i].IsWaitingForSleep) sleeping++;
        }
    }

    public float GetSleepOverlayAlpha()
    {
        if (!IsSleepTransitionActive || Runner == null) return 0f;
        float remaining = SleepPhaseTimer.RemainingTime(Runner) ?? 0f;
        if (CurrentSleepPhase == SleepPhase.Waking)
            return Mathf.Clamp01(remaining / Mathf.Max(0.01f, wakeFadeSeconds));
        if (CurrentSleepPhase == SleepPhase.VoluntaryFade) return 1f;

        float elapsed = SleepSequenceSeconds - remaining;
        return Mathf.Clamp01(elapsed / ForcedFadeToBlackSeconds);
    }

    public bool ShouldShowFastForwardClock()
    {
        return CurrentSleepPhase == SleepPhase.VoluntaryFade ||
               (CurrentSleepPhase == SleepPhase.ForcedFade && GetSleepOverlayAlpha() >= 0.95f);
    }

    public bool CanUseBedNow()
    {
        if (IsSleepTransitionActive) return false;
        return CurrentTime >= 20f || CurrentTime < 3f;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_AnnounceSleepCount(int sleeping, int total)
    {
        AutoChatManager.Instance?.AddMessage("SYSTEM", $"Đang ngủ: {sleeping}/{total} người.");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_AnnounceSleepEvent(string message)
    {
        AutoChatManager.Instance?.AddMessage("SYSTEM", message);
    }

    private void Update()
    {
        if (!isSpawned) return;
        UpdateLighting();
        UpdateUI();
    }

    private void UpdateLighting()
    {
        if (globalLight == null) return;
        float timePercent = CurrentTime / 24f;
        float authoredIntensity = globalIntensityCurve != null && globalIntensityCurve.length > 0
            ? globalIntensityCurve.Evaluate(timePercent)
            : 1f;
        globalLight.intensity = Mathf.Min(authoredIntensity, EvaluateNightIntensityCap(CurrentTime));

        Color authoredColor = skyColorCurve != null ? skyColorCurve.Evaluate(timePercent) : Color.white;
        float nightBlend = EvaluateNightBlend(CurrentTime);
        globalLight.color = Color.Lerp(authoredColor, new Color(0.12f, 0.18f, 0.34f, 1f), nightBlend * 0.72f);
    }

    private static float EvaluateNightIntensityCap(float hour)
    {
        hour = Mathf.Repeat(hour, 24f);
        if (hour >= 18.5f) return Mathf.Lerp(0.55f, 0.055f, Mathf.InverseLerp(18.5f, 23f, hour));
        if (hour < 5f) return 0.055f;
        if (hour < 7.5f) return Mathf.Lerp(0.055f, 1.5f, Mathf.InverseLerp(5f, 7.5f, hour));
        return 1.5f;
    }

    public static float EvaluateNightBlend(float hour)
    {
        hour = Mathf.Repeat(hour, 24f);
        if (hour >= 18.5f) return Mathf.InverseLerp(18.5f, 22f, hour);
        if (hour < 5.5f) return 1f;
        if (hour < 8f) return 1f - Mathf.InverseLerp(5.5f, 8f, hour);
        return 0f;
    }

    private void UpdateUI()
    {
        if (AutoUIManager.Instance == null || AutoUIManager.Instance.clockText == null) return;
        int hours = Mathf.FloorToInt(CurrentTime);
        int minutes = Mathf.FloorToInt((CurrentTime - hours) * 60f);
        AutoUIManager.Instance.clockText.text = $"{hours:00}:{minutes:00}";
    }

    public float GetTimePercent() => CurrentTime / 24f;
}
