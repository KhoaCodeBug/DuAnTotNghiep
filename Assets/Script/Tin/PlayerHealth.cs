using UnityEngine;
using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.Serialization;

public class PlayerHealth : NetworkBehaviour
{
    public const int BodyPartCount = 16;
    public static readonly string[] BodyPartNames =
    {
        "Head", "Neck", "Upper Torso", "Lower Torso",
        "Left Thigh", "Left Calf", "Left Foot",
        "Right Thigh", "Right Calf", "Right Foot",
        "Left Upper Arm", "Left Forearm", "Left Hand",
        "Right Upper Arm", "Right Forearm", "Right Hand"
    };

    public enum WoundType
    {
        Scratched = 0,
        Laceration = 1,
        Bitten = 2
    }

    public struct NetworkWoundState : INetworkStruct
    {
        public int InjuryMask;
        public NetworkBool IsBandaged;

        public bool HasInjury => InjuryMask != 0;
        public bool HasInjuryType(WoundType type) => (InjuryMask & (1 << (int)type)) != 0;
    }

    public sealed class WoundSnapshot
    {
        public readonly int[] InjuryMasks = new int[BodyPartCount];
        public readonly bool[] Bandaged = new bool[BodyPartCount];
        public bool IsBitten;
        public float InfectionTimer;
    }

    public static PlayerHealth LocalHealthInstance;

    [Header("Chỉ số Máu")]
    public float maxHealth = 100f;
    [Networked] public float currentHealth { get; set; }
    public float CurrentHealthSafe => (Object != null && Object.IsValid) ? currentHealth : maxHealth;
    [Networked] public NetworkBool IsMilitaryOutroProtected { get; private set; }

    [Header("Hiệu ứng khi bị đánh")]
    public float stunDuration = 0.4f;
    public Color hurtColor = Color.red;
    public float flashDuration = 0.1f;

    [Header("Cài đặt Hardcore PZ")]
    public float bleedDamagePerSecond = 1.5f;
    [Tooltip("Tốc độ hồi máu ở cấp No đầu tiên. Mỗi cấp No cao hơn cộng thêm healBonusPerWellFedTier.")]
    public float passiveHealPerSecond = 0.5f;
    public float healBonusPerWellFedTier = 0.1f;

    [Networked] public NetworkBool isBleeding { get; set; }
    [Networked] public NetworkBool isInPain { get; set; }
    [Networked, Capacity(BodyPartCount)] public NetworkArray<NetworkWoundState> Wounds => default;
    [Networked] public int WoundRevision { get; private set; }

    // ==========================================
    // 🔥 HỆ THỐNG NHIỄM TRÙNG & KẺ PHẢN BỘI
    // ==========================================
    [Header("Hệ thống Nhiễm Trùng")]
    [Networked] public float infectionTimer { get; set; } = 600f;
    [Networked] public NetworkBool isBitten { get; set; }

    [Header("Hóa Zombie Khi Chết")]
    [FormerlySerializedAs("traitorBossPrefab")]
    public NetworkPrefabRef zombieTurnPrefab;
    [Networked] public NetworkBool isTransforming { get; set; }
    [Networked] public float transformTimer { get; set; } = 5f;

    [Header("Hiệu ứng Hoang Tưởng")]
    public RuntimeAnimatorController zombieAnimatorController;

    [Header("Blend Tree Parameters")]
    public string paramMoveX = "MoveX";
    public string paramMoveY = "MoveY";
    public string paramSpeed = "Speed";

    private Dictionary<Animator, RuntimeAnimatorController> originalTeammateControllers = new Dictionary<Animator, RuntimeAnimatorController>();
    private List<PlayerNameTag> hiddenNameTags = new List<PlayerNameTag>();

    private Dictionary<Animator, Vector3> lastTeammatePositions = new Dictionary<Animator, Vector3>();
    private bool isFakeZombieVisible = false;

    private PlayerMovement movementScript;
    private Animator anim;
    private SpriteRenderer spriteRend;
    private Color originalColor;
    private bool isFlashing = false;

    private PlayerSurvival survivalSystem;

    [Networked] public NetworkBool isDead { get; set; }
    [Networked] public DeathCause LastDeathCause { get; set; }
    [Networked] public PlayerRef LastAttackerPlayerRef { get; set; }
    private bool hasBroadcastDeathAnnouncement;

    [Header("--- Body State SFX ---")]
    public AudioClip deathSFX;
    public AudioClip[] hurtGruntSFXs = new AudioClip[5];
    private AudioSource bodyStateAudioSource;
    private float lastHurtSoundTime = 0f;
    private int lastHurtIndex = -1;

    private Canvas paranoiaCanvas;
    private Image paranoiaImage;
    private bool isBlinking = false;
    private bool biteTerminalOverlayActive;

    private bool hasTriggeredSpectate = false;
    private bool terminalLocalSafetyApplied;

    // Body-part actions are committed on State Authority. Request ids only
    // correlate responses; they never grant inventory or wound-state credit.
    private int nextBandageRequestId = 1;
    private readonly Dictionary<int, System.Action<bool>> pendingBandageRequests =
        new Dictionary<int, System.Action<bool>>();

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            currentHealth = maxHealth;
            IsMilitaryOutroProtected = false;
            Wounds.Clear();
            WoundRevision = 0;
            LastDeathCause = DeathCause.Unknown;
            LastAttackerPlayerRef = default;
            RecalculateWoundFlags();
        }
        hasBroadcastDeathAnnouncement = false;

        movementScript = GetComponent<PlayerMovement>();
        anim = GetComponentInChildren<Animator>();
        spriteRend = GetComponentInChildren<SpriteRenderer>();
        survivalSystem = GetComponent<PlayerSurvival>();

        if (spriteRend != null) originalColor = spriteRend.color;

        if (HasInputAuthority)
        {
            LocalHealthInstance = this;
            SetupParanoiaUI();

            if (AutoHealthPanel.Instance != null)
            {
                AutoHealthPanel.Instance.BindLocalPlayer(this);
            }
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        pendingBandageRequests.Clear();
        CleanupParanoia();
        if (paranoiaCanvas != null)
        {
            Destroy(paranoiaCanvas.gameObject);
        }
        // 🔥 FIX: Reset static reference khi bị Despawn để ván sau tìm lại đúng player
        if (LocalHealthInstance == this) LocalHealthInstance = null;
    }

    public override void Render()
    {
        // Death presentation RPCs are not replayed to late joiners. Replicated
        // state must independently guarantee that a corpse cannot keep a local
        // collider alive and block physics/LOS forever on any peer.
        if (!terminalLocalSafetyApplied && (isDead || isTransforming))
            ApplyTerminalLocalSafety();
    }

    private void SetupParanoiaUI()
    {
        GameObject canvasObj = new GameObject("ParanoiaCanvas_" + Object.Id);
        DontDestroyOnLoad(canvasObj);

        paranoiaCanvas = canvasObj.AddComponent<Canvas>();
        paranoiaCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        paranoiaCanvas.sortingOrder = 200;

        GameObject imgObj = new GameObject("ParanoiaOverlay");
        imgObj.transform.SetParent(canvasObj.transform, false);
        paranoiaImage = imgObj.AddComponent<Image>();

        RectTransform rect = paranoiaImage.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        paranoiaImage.color = new Color(0, 0, 0, 0);
        paranoiaImage.raycastTarget = false;
    }

    private void Update()
    {
        if (!HasInputAuthority) return;

        // Death is authored by StateAuthority and reaches the owning client through
        // Fusion replication. Clean local-only hallucination visuals as soon as that
        // replicated state arrives instead of waiting for the old avatar to despawn.
        if (isDead && (isBlinking || isFakeZombieVisible || biteTerminalOverlayActive))
            CleanupParanoia();

        if (isDead && !hasTriggeredSpectate)
        {
            hasTriggeredSpectate = true; // Khóa lại, chỉ gọi 1 lần duy nhất

            // Ép tắt tâm ruồi ngắm bắn (Nếu có)
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

            // Gọi Camera tiến hành chuyển mục tiêu sang người sống sót đầu tiên
            var cam = FindFirstObjectByType<PZ_CameraController>();
            if (cam != null) cam.SpectateNext(0);
        }

        if (isBitten && !isDead)
        {
            if (!isBlinking && !biteTerminalOverlayActive && infectionTimer <= 180f && infectionTimer > 0f)
                StartCoroutine(nameof(BiteTerminalOverlayRoutine));
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        if (MainQuestManager.Instance != null && MainQuestManager.Instance.IsQuestCutsceneActive) return;

        // Survivors who could not occupy one of the four physical police-car
        // seats are carried by the authoritative Route B outro. Freeze lethal
        // survival drains for the few seconds of that canonical extraction.
        if (IsMilitaryOutroProtected)
        {
            if (!isDead && !isTransforming) currentHealth = Mathf.Max(1f, currentHealth);
            return;
        }

        if (isTransforming)
        {
            transformTimer -= Runner.DeltaTime;

            if (transformTimer <= 0)
            {
                isTransforming = false;

                if (zombieTurnPrefab.IsValid)
                {
                    Vector3 spawnPos = transform.position;
                    if (UnityEngine.AI.NavMesh.SamplePosition(spawnPos, out UnityEngine.AI.NavMeshHit navHit, 2f, UnityEngine.AI.NavMesh.AllAreas))
                    {
                        spawnPos = navHit.position;
                    }

                    NetworkObject spawnedZombie = Runner.Spawn(zombieTurnPrefab, spawnPos, Quaternion.identity);
                    if (spawnedZombie != null)
                    {
                        var agent = spawnedZombie.GetComponent<UnityEngine.AI.NavMeshAgent>();
                        if (agent != null)
                        {
                            if (UnityEngine.AI.NavMesh.SamplePosition(spawnedZombie.transform.position, out UnityEngine.AI.NavMeshHit agentHit, 2f, UnityEngine.AI.NavMesh.AllAreas))
                            {
                                agent.enabled = true;
                                agent.Warp(agentHit.position);
                            }
                            else
                            {
                                agent.enabled = false;
                            }
                        }
                    }
                }

                // 👇 THÊM ĐOẠN NÀY VÀO TRƯỚC KHI DESPAWN 👇
                if (HasInputAuthority)
                {
                    var cameraController = FindFirstObjectByType<PZ_CameraController>();
                    if (cameraController != null)
                    {
                        // Giật cam sang người sống sót đầu tiên tìm thấy trước khi xác này bay màu
                        PlayerHealth[] allPlayers = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
                        foreach (var p in allPlayers)
                        {
                            if (p != this && !p.isDead)
                            {
                                cameraController.SpectateTarget(p.transform);
                                break;
                            }
                        }
                    }
                }
                // 👆 KẾT THÚC THÊM 👆

                Runner.Despawn(Object);
            }
            return;
        }

        if (isDead) return;

        // VẾT CẮN RÚT MÁU KHI SẮP HÓA ZOMBIE
        if (isBitten)
        {
            float safeTimer = Mathf.Max(infectionTimer, Runner.DeltaTime);

            if (infectionTimer <= 180f && infectionTimer > 0f)
            {
                float bleedAmount = (currentHealth / safeTimer) * Runner.DeltaTime;
                currentHealth -= bleedAmount;
            }

            infectionTimer -= Runner.DeltaTime;

            if (infectionTimer <= 0)
            {
                infectionTimer = 0;
                currentHealth = 0;
                LastDeathCause = DeathCause.Infection;
                LastAttackerPlayerRef = default;
                TriggerDeathLogic();
                return;
            }
        }

        // TỤT MÁU DO CHẢY MÁU CỦA VẾT THƯƠNG HỞ
        if (isBleeding)
        {
            currentHealth -= bleedDamagePerSecond * Runner.DeltaTime;
        }

        ApplyTutorialHealthFloor();

        bool hasTerminalBiteDrain = isBitten && infectionTimer <= 180f;
        if (!isBleeding && !hasTerminalBiteDrain && currentHealth < maxHealth && survivalSystem != null)
        {
            int wellFedTier = survivalSystem.GetWellFedTier();
            if (wellFedTier > 0)
            {
                currentHealth += GetPassiveHealRate(wellFedTier) * Runner.DeltaTime;
            }
        }

        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        if (currentHealth <= 0 && !isDead)
        {
            if (isBleeding && LastDeathCause == DeathCause.Unknown)
            {
                LastDeathCause = DeathCause.Bleeding;
                LastAttackerPlayerRef = default;
            }
            TriggerDeathLogic();
        }
    }

    public void TakeDamage(float damage, bool isStarving = false, bool isZombieAttack = false)
    {
        TakeDamageWithAttacker(damage, isStarving, isZombieAttack, default);
    }

    public void TakeDamageWithAttacker(float damage, bool isStarving, bool isZombieAttack, PlayerRef attacker)
    {
        if (!HasStateAuthority) return;
        if (MainQuestManager.Instance != null && MainQuestManager.Instance.IsQuestCutsceneActive) return;
        if (IsMilitaryOutroProtected) return;

        // The vehicle body, not an invisible seated player body, receives
        // zombie contact. This authority-side guard also covers attacks that
        // were committed just before the player entered the vehicle.
        if (isZombieAttack && PlayerInteraction.IsProtectedOccupant(this)) return;

        if (isDead && !isTransforming) return;

        if (damage > 0f)
            RPC_CancelCorpseSearchForDamage();

        if (isZombieAttack)
        {
            LastDeathCause = DeathCause.ZombieAttack;
            LastAttackerPlayerRef = default;
        }
        else if (isStarving)
        {
            if (survivalSystem != null && survivalSystem.currentThirst <= 0f && survivalSystem.currentHunger > 0f)
                LastDeathCause = DeathCause.Dehydration;
            else
                LastDeathCause = DeathCause.Starvation;
            LastAttackerPlayerRef = default;
        }
        else if (attacker != default && Object != null && Object.IsValid && attacker != Object.InputAuthority)
        {
            LastDeathCause = DeathCause.PvP;
            LastAttackerPlayerRef = attacker;
        }

        float effectiveDamage = damage;
        if (!isStarving)
        {
            effectiveDamage *= DifficultyRules.GetIncomingDamageMultiplier(DifficultyRules.ActiveDifficulty);
        }

        currentHealth -= effectiveDamage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        ApplyTutorialHealthFloor();

        // Repair interruption is decided by the same State Authority that
        // accepted this damage, so a client cannot hide a zombie hit locally.
        if (Object != null && Object.IsValid)
            MilitaryBaseQuestManager.Instance?.NotifyPlayerDamaged(Object.InputAuthority, isZombieAttack);

        if (isTransforming)
        {
            RPC_PlayHitEffect();
            if (currentHealth <= 0)
            {
                isTransforming = false;
                RPC_PlayDeathEffect();
            }
            return;
        }

        if (currentHealth <= 0)
        {
            TriggerDeathLogic();
            return;
        }

        if (!isStarving)
        {
            isInPain = true;

            RPC_PlayHitEffect();

            if (isZombieAttack)
            {
                AuthorityCreateZombieWound();
            }

            if (movementScript != null) movementScript.LockMovement(stunDuration);
        }
    }

    /// <summary>
    /// Authority-only damage entry point. Attackers must resolve hits on State
    /// Authority; a remote client is never allowed to request arbitrary player
    /// damage through the victim's NetworkObject.
    /// </summary>
    public void TakeDamageNetworked(float damage, bool isStarving = false, bool isZombieAttack = false)
    {
        if (HasStateAuthority) TakeDamage(damage, isStarving, isZombieAttack);
    }

    public void AuthoritySetMilitaryOutroProtected(bool protectedState)
    {
        if (!HasStateAuthority) return;
        IsMilitaryOutroProtected = protectedState;
        if (protectedState && !isDead && !isTransforming)
            currentHealth = Mathf.Max(1f, currentHealth);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_CancelCorpseSearchForDamage()
    {
        AutoUIManager.Instance?.CancelTimedGameplayAction();
    }

    private void ApplyTutorialHealthFloor()
    {
        if (!TutorialSession.IsActive || !TutorialInputGate.HealthFloorEnabled || isDead) return;
        currentHealth = Mathf.Max(currentHealth, maxHealth * TutorialInputGate.MinimumHealthRatio);
    }

    private void AuthorityCreateZombieWound()
    {
        if (!HasStateAuthority) return;

        int targetIndex;
        if (Random.Range(0f, 100f) <= 5f)
        {
            targetIndex = 1; // Neck is always a bite.
        }
        else
        {
            List<int> healthyParts = new List<int>();
            for (int i = 0; i < BodyPartCount; i++)
                if (i != 1 && !Wounds[i].HasInjury) healthyParts.Add(i);

            targetIndex = healthyParts.Count > 0
                ? healthyParts[Random.Range(0, healthyParts.Count)]
                : Random.Range(2, BodyPartCount);
        }

        WoundType type;
        float roll = Random.Range(0f, 100f);
        if (targetIndex == 1 || roll <= 5f) type = WoundType.Bitten;
        else if (roll <= 52.5f) type = WoundType.Laceration;
        else type = WoundType.Scratched;

        AuthorityAddWound(targetIndex, type);
    }

    public void AuthorityAddTutorialWound(int bodyPartIndex)
    {
        if (HasStateAuthority)
            AuthorityAddWound(Mathf.Clamp(bodyPartIndex, 0, BodyPartCount - 1), WoundType.Laceration);
    }

    private void AuthorityAddWound(int bodyPartIndex, WoundType type)
    {
        if (!HasStateAuthority || bodyPartIndex < 0 || bodyPartIndex >= BodyPartCount) return;

        NetworkWoundState wound = Wounds[bodyPartIndex];
        wound.InjuryMask |= 1 << (int)type;
        wound.IsBandaged = false;
        Wounds.Set(bodyPartIndex, wound);
        WoundRevision++;
        RecalculateWoundFlags();
    }

    private void TriggerDeathLogic()
    {
        CleanupParanoia();
        isDead = true;

        if (HasStateAuthority && !hasBroadcastDeathAnnouncement)
        {
            hasBroadcastDeathAnnouncement = true;
            string victimName = HostModeSpawner.Instance != null && Object != null && Object.IsValid
                ? HostModeSpawner.Instance.GetPlayerName(Object.InputAuthority)
                : GetComponent<PlayerNameTag>()?.PlayerName.ToString();

            string killerName = null;
            if (LastDeathCause == DeathCause.PvP && LastAttackerPlayerRef != default && HostModeSpawner.Instance != null)
            {
                killerName = HostModeSpawner.Instance.GetPlayerName(LastAttackerPlayerRef);
            }

            string safeVictim = string.IsNullOrWhiteSpace(victimName) ? string.Empty : victimName;
            string safeKiller = string.IsNullOrWhiteSpace(killerName) ? string.Empty : killerName;

            RPC_BroadcastDeathSystemMessage(safeVictim, (int)LastDeathCause, safeKiller);
        }

        if (isBitten)
        {
            isTransforming = true;
            transformTimer = 5f;
            currentHealth = 100f;
            RPC_PlayConvulseEffect();
        }
        else
        {
            RPC_PlayDeathEffect();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BroadcastDeathSystemMessage(string victimName, int deathCause, string killerName)
    {
        string safeVictim = string.IsNullOrWhiteSpace(victimName) ? "Survivor" : victimName;
        string safeKiller = string.IsNullOrWhiteSpace(killerName) ? null : killerName;
        string deathMessage = PlayerDeathContext.FormatDeathMessage(safeVictim, (DeathCause)deathCause, safeKiller);
        AutoChatManager.Instance?.AddSystemMessage(deathMessage);
    }

    public void SetGlobalBleeding(bool state)
    {
        if (HasStateAuthority) RecalculateWoundFlags();
    }

    public int RequestBandageForWound(int woundIndex, System.Action<bool> onResolved)
    {
        if (!HasInputAuthority || onResolved == null || woundIndex < 0 || woundIndex >= BodyPartCount)
            return 0;

        int requestId = nextBandageRequestId++;
        if (nextBandageRequestId <= 0) nextBandageRequestId = 1;
        pendingBandageRequests[requestId] = onResolved;

        if (HasStateAuthority)
        {
            ResolveBandageForWound(requestId, AuthorityTryBandageWound(woundIndex));
        }
        else
        {
            RPC_RequestBandageForWound(requestId, woundIndex);
        }

        return requestId;
    }

    public int RequestBandageForFirstWound(System.Action<bool> onResolved)
    {
        for (int i = 0; i < BodyPartCount; i++)
        {
            NetworkWoundState wound = Wounds[i];
            if (wound.HasInjury && !wound.IsBandaged)
                return RequestBandageForWound(i, onResolved);
        }

        onResolved?.Invoke(false);
        return 0;
    }

    public int RequestRemoveBandageForWound(int woundIndex, System.Action<bool> onResolved)
    {
        if (!HasInputAuthority || onResolved == null || woundIndex < 0 || woundIndex >= BodyPartCount)
            return 0;

        int requestId = nextBandageRequestId++;
        if (nextBandageRequestId <= 0) nextBandageRequestId = 1;
        pendingBandageRequests[requestId] = onResolved;

        if (HasStateAuthority)
            ResolveBandageForWound(requestId, AuthorityRemoveBandage(woundIndex));
        else
            RPC_RequestRemoveBandageForWound(requestId, woundIndex);

        return requestId;
    }

    public void CancelBandageRequest(int requestId)
    {
        if (requestId != 0) pendingBandageRequests.Remove(requestId);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestBandageForWound(int requestId, int woundIndex)
    {
        bool success = AuthorityTryBandageWound(woundIndex);
        RPC_BandageForWoundResult(requestId, success);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestRemoveBandageForWound(int requestId, int woundIndex)
    {
        bool success = AuthorityRemoveBandage(woundIndex);
        RPC_BandageForWoundResult(requestId, success);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_BandageForWoundResult(int requestId, NetworkBool success)
    {
        ResolveBandageForWound(requestId, success);
    }

    private bool AuthorityTryBandageWound(int woundIndex)
    {
        if (!HasStateAuthority || isDead || woundIndex < 0 || woundIndex >= BodyPartCount)
            return false;

        NetworkWoundState wound = Wounds[woundIndex];
        bool success = TryApplyBandage(ref wound, () => TryConsumeMedicalTreatment("Bandage"));
        if (success)
        {
            Wounds.Set(woundIndex, wound);
            WoundRevision++;
            RecalculateWoundFlags();
        }
        return success;
    }

    public static bool TryApplyBandage(ref NetworkWoundState wound, System.Func<bool> consumeSingleBandage)
    {
        if (!wound.HasInjury || wound.IsBandaged || consumeSingleBandage == null) return false;
        if (!consumeSingleBandage()) return false;
        wound.IsBandaged = true;
        return true;
    }

    private bool AuthorityRemoveBandage(int woundIndex)
    {
        if (!HasStateAuthority || isDead || woundIndex < 0 || woundIndex >= BodyPartCount)
            return false;

        NetworkWoundState wound = Wounds[woundIndex];
        if (!wound.HasInjury || !wound.IsBandaged) return false;

        // Preserve the existing gameplay rule: removing a completed dressing
        // heals scratches/lacerations, while a bite remains an open terminal wound.
        wound.InjuryMask &= 1 << (int)WoundType.Bitten;
        wound.IsBandaged = false;
        Wounds.Set(woundIndex, wound);
        WoundRevision++;
        RecalculateWoundFlags();
        return true;
    }

    private void ResolveBandageForWound(int requestId, bool success)
    {
        if (!pendingBandageRequests.TryGetValue(requestId, out System.Action<bool> callback)) return;

        pendingBandageRequests.Remove(requestId);
        callback(success);
    }

    public void SetBitten()
    {
        if (HasStateAuthority) isBitten = true;
    }

    public int GetBodyPartIndex(string bodyPartName)
    {
        if (string.IsNullOrEmpty(bodyPartName)) return -1;
        for (int i = 0; i < BodyPartNames.Length; i++)
            if (string.Equals(BodyPartNames[i], bodyPartName, System.StringComparison.Ordinal)) return i;
        return -1;
    }

    public NetworkWoundState GetWound(int bodyPartIndex)
    {
        return bodyPartIndex >= 0 && bodyPartIndex < BodyPartCount
            ? Wounds[bodyPartIndex]
            : default;
    }

    public WoundSnapshot CaptureWoundSnapshot()
    {
        if (!HasStateAuthority) return null;

        WoundSnapshot snapshot = new WoundSnapshot
        {
            IsBitten = isBitten,
            InfectionTimer = infectionTimer
        };
        for (int i = 0; i < BodyPartCount; i++)
        {
            NetworkWoundState wound = Wounds[i];
            snapshot.InjuryMasks[i] = wound.InjuryMask;
            snapshot.Bandaged[i] = wound.IsBandaged;
        }
        return snapshot;
    }

    public void RestoreWoundSnapshot(WoundSnapshot snapshot)
    {
        if (!HasStateAuthority || snapshot == null) return;

        for (int i = 0; i < BodyPartCount; i++)
        {
            Wounds.Set(i, new NetworkWoundState
            {
                InjuryMask = snapshot.InjuryMasks[i],
                IsBandaged = snapshot.Bandaged[i]
            });
        }
        isBitten = snapshot.IsBitten;
        infectionTimer = snapshot.InfectionTimer;
        WoundRevision++;
        RecalculateWoundFlags();
    }

    private void RecalculateWoundFlags()
    {
        if (!HasStateAuthority) return;

        bool bleeding = false;
        bool bitten = false;
        for (int i = 0; i < BodyPartCount; i++)
        {
            NetworkWoundState wound = Wounds[i];
            if (wound.HasInjuryType(WoundType.Bitten)) bitten = true;
            if (wound.HasInjury && !wound.IsBandaged) bleeding = true;
        }

        isBleeding = bleeding;
        if (bitten) isBitten = true;
    }

    public void UsePainkiller()
    {
        if (HasStateAuthority) AuthorityStopPain();
        else RPC_StopPain();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_StopPain()
    {
        AuthorityStopPain();
    }

    private void AuthorityStopPain()
    {
        if (!HasStateAuthority || !isInPain) return;
        if (TryConsumeMedicalTreatment("PainKiller")) isInPain = false;
    }

    private bool TryConsumeMedicalTreatment(string itemId)
    {
        InventorySystem inventory = GetComponent<InventorySystem>();
        ItemData treatment = ItemDataLoader.LoadItem(itemId);
        if (inventory == null || treatment == null || inventory.GetItemCount(treatment) < 1)
        {
            Debug.LogWarning($"[HEALTH] Rejected treatment '{itemId}': authoritative inventory has no item.");
            return false;
        }

        int removed = inventory.ConsumeItem(treatment, 1);
        if (removed == 1) return true;

        if (removed > 0) inventory.AddItem(treatment, removed);
        Debug.LogWarning($"[HEALTH] Rejected treatment '{itemId}': authoritative consume failed.");
        return false;
    }

    public float GetSFXVolume()
    {
        float vol = PlayerPrefs.GetFloat("GameSFXVolume", 0.8f);
        if (AutoMainMenuManager.Instance != null)
        {
            vol = AutoMainMenuManager.Instance.sfxVolume;
        }
        return Mathf.Clamp01(vol);
    }

    private void EnsureAudioSource()
    {
        if (bodyStateAudioSource == null)
        {
            bodyStateAudioSource = gameObject.AddComponent<AudioSource>();
            bodyStateAudioSource.playOnAwake = false;
            bodyStateAudioSource.loop = false;
        }

        GameplayAudioSpatializer.Configure(bodyStateAudioSource, GameplayAudioSpatializer.Profile.Body);
    }

    private void PlayHurtGruntSFX()
    {
        EnsureAudioSource();

        // Nạp tự động từ Resources nếu chưa gán Inspector
        for (int i = 0; i < 5; i++)
        {
            if (hurtGruntSFXs[i] == null)
            {
                hurtGruntSFXs[i] = Resources.Load<AudioClip>($"Sound/BodyState/player_hurt_grunt_{i + 1}");
            }
        }

        // Chống spam tiếng kêu đau liên tục dồn dập
        if (Time.time - lastHurtSoundTime < 0.25f) return;

        // Chọn ngẫu nhiên 1 trong 5 biến thể (tránh lặp lại biến thể ngay trước đó)
        List<int> validIndices = new List<int>();
        for (int i = 0; i < 5; i++)
        {
            if (hurtGruntSFXs[i] != null && i != lastHurtIndex)
            {
                validIndices.Add(i);
            }
        }

        if (validIndices.Count == 0 && hurtGruntSFXs[0] != null) validIndices.Add(0);

        if (validIndices.Count > 0)
        {
            int pick = validIndices[Random.Range(0, validIndices.Count)];
            lastHurtIndex = pick;
            lastHurtSoundTime = Time.time;
            float finalVolume = GameplayAudioSpatializer.GetAttenuatedVolume(
                bodyStateAudioSource,
                GameplayAudioSpatializer.Profile.Body,
                GetSFXVolume(),
                HasInputAuthority);
            bodyStateAudioSource.PlayOneShot(hurtGruntSFXs[pick], finalVolume);
        }
    }

    private void PlayDeathSFX()
    {
        if (deathSFX == null) deathSFX = Resources.Load<AudioClip>("Sound/BodyState/player_death");

        if (deathSFX != null)
        {
            float sfxVol = GetSFXVolume();
            GameObject soundObj = new GameObject("Temp_PlayerDeathSFX");
            soundObj.transform.position = transform.position;
            AudioSource aSrc = soundObj.AddComponent<AudioSource>();
            aSrc.clip = deathSFX;
            GameplayAudioSpatializer.Configure(aSrc, GameplayAudioSpatializer.Profile.Body);
            aSrc.volume = GameplayAudioSpatializer.GetAttenuatedVolume(
                aSrc,
                GameplayAudioSpatializer.Profile.Body,
                sfxVol,
                HasInputAuthority);
            aSrc.playOnAwake = false;
            aSrc.Play();
            Destroy(soundObj, deathSFX.length + 0.2f);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_PlayHitEffect()
    {
        if (anim != null) anim.SetTrigger("TakeDamage");
        if (spriteRend != null && !isFlashing) StartCoroutine(FlashHurtRoutine());
        PlayHurtGruntSFX();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_PlayConvulseEffect()
    {
        if (anim != null) anim.SetBool("IsDead", true);

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;
        if (movementScript != null) movementScript.enabled = false;

        ApplyTerminalLocalSafety();

        if (spriteRend != null) spriteRend.color = new Color(0.4f, 0.5f, 0.4f, 1f);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_PlayDeathEffect()
    {
        if (anim != null) anim.SetBool("IsDead", true);

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
        }
        if (movementScript != null) movementScript.enabled = false;

        ApplyTerminalLocalSafety();

        StopAllCoroutines();
        if (spriteRend != null) spriteRend.color = originalColor;

        PlayDeathSFX();
        StartCoroutine(BlinkAndVanishRoutine());
    }

    private void ApplyTerminalLocalSafety()
    {
        terminalLocalSafetyApplied = true;
        foreach (Collider2D coll in GetComponentsInChildren<Collider2D>(true))
            coll.enabled = false;
        if (movementScript != null) movementScript.enabled = false;
    }

    public void Heal(float amount)
    {
        if (isDead) return;
        // Never accept an arbitrary heal amount from Input Authority. Legitimate
        // healing must be initiated and validated by State Authority.
        if (HasStateAuthority) PerformHeal(amount);
    }

    public float GetPassiveHealRate(int wellFedTier)
    {
        wellFedTier = Mathf.Clamp(wellFedTier, 1, 4);
        return passiveHealPerSecond + (wellFedTier - 1) * healBonusPerWellFedTier;
    }

    private void PerformHeal(float amount)
    {
        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
    }

    private IEnumerator FlashHurtRoutine()
    {
        isFlashing = true;
        spriteRend.color = hurtColor;
        yield return new WaitForSeconds(flashDuration);

        spriteRend.color = isTransforming ? new Color(0.4f, 0.5f, 0.4f, 1f) : originalColor;
        isFlashing = false;
    }

    private IEnumerator BlinkAndVanishRoutine()
    {
        yield return new WaitForSeconds(1f);
        for (int i = 0; i < 5; i++)
        {
            if (spriteRend != null) spriteRend.enabled = false;
            yield return new WaitForSeconds(0.15f);
            if (spriteRend != null) spriteRend.enabled = true;
            yield return new WaitForSeconds(0.15f);
        }

        // 👇 XÓA HOẶC COMMENT DÒNG NÀY LẠI
        // gameObject.SetActive(false); 

        // 👇 THAY BẰNG DÒNG NÀY (Chỉ làm hình ảnh tàng hình, giữ lại code)
        if (spriteRend != null) spriteRend.enabled = false;
    }

    private IEnumerator BiteTerminalOverlayRoutine()
    {
        if (paranoiaImage == null) yield break;

        isBlinking = true;
        Color black = new Color(0, 0, 0, 1f);
        Color permanentRed = new Color(0.65f, 0f, 0f, 0.35f);

        yield return StartCoroutine(FadeColor(black, 0.5f));
        yield return new WaitForSeconds(0.1f);
        yield return StartCoroutine(FadeColor(permanentRed, 0.5f));
        biteTerminalOverlayActive = true;
        isBlinking = false;
    }

    private IEnumerator FadeColor(Color targetColor, float duration)
    {
        Color startColor = paranoiaImage.color;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            paranoiaImage.color = Color.Lerp(startColor, targetColor, elapsed / duration);
            yield return null;
        }

        paranoiaImage.color = targetColor;
    }

    private void SwapTeammatesToZombies()
    {
        if (zombieAnimatorController == null) return;
        originalTeammateControllers.Clear();
        hiddenNameTags.Clear();
        lastTeammatePositions.Clear();

        PlayerHealth[] allPlayers = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);

        foreach (var player in allPlayers)
        {
            if (player == this) continue;

            Animator teammateAnim = player.GetComponentInChildren<Animator>();
            PlayerNameTag nameTag = player.GetComponent<PlayerNameTag>();
            PlayerMovement pm = player.GetComponent<PlayerMovement>();

            if (teammateAnim != null)
            {
                originalTeammateControllers[teammateAnim] = teammateAnim.runtimeAnimatorController;
                teammateAnim.runtimeAnimatorController = zombieAnimatorController;
                lastTeammatePositions[teammateAnim] = teammateAnim.transform.position;
            }

            if (nameTag != null && nameTag.nameText != null)
            {
                nameTag.nameText.gameObject.SetActive(false);
                hiddenNameTags.Add(nameTag);
            }

            if (pm != null) pm.isParanoiaZombie = true;
        }
        isFakeZombieVisible = true;
    }

    private void RestoreTeammatesSprites()
    {
        isFakeZombieVisible = false;

        foreach (var kvp in originalTeammateControllers)
        {
            if (kvp.Key != null)
            {
                kvp.Key.runtimeAnimatorController = kvp.Value;
                PlayerMovement pm = kvp.Key.GetComponentInParent<PlayerMovement>();
                if (pm != null) pm.isParanoiaZombie = false;
            }
        }
        originalTeammateControllers.Clear();
        lastTeammatePositions.Clear();

        foreach (var tag in hiddenNameTags)
        {
            if (tag != null && tag.nameText != null) tag.nameText.gameObject.SetActive(true);
        }
        hiddenNameTags.Clear();
    }

    private void CleanupParanoia()
    {
        StopCoroutine(nameof(BiteTerminalOverlayRoutine));
        RestoreTeammatesSprites();
        isBlinking = false;
        biteTerminalOverlayActive = false;
        if (paranoiaImage != null) paranoiaImage.color = Color.clear;
    }

    private void OnDisable()
    {
        CleanupParanoia();
    }

    // ĐỂ TRỐNG THEO LỆNH SẾP (KHÔNG VẼ MOODLE RÁC)
    private void OnGUI() { }
}
