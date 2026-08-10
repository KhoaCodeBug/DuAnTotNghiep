using Fusion;
using Pathfinding;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Standalone tutorial flow through the first silent takedown. Each concept
/// begins with a modal explanation and then has one observable objective.
/// </summary>
public sealed class TutorialPhaseOneController : MonoBehaviour
{
    private enum Step
    {
        WaitingForIntro,
        MoveBrief, Move,
        ZoomBrief, Zoom,
        AimBrief, Aim,
        NeedsBrief, NeedsFocus,
        HouseBrief, GoToKitchen,
        CabinetBrief, Loot,
        ConsumeBrief, Consume,
        WeaponBrief, EquipWeapon,
        ReloadBrief, Reload,
        LeaveHouseBrief, LeaveHouse,
        ZombieCinematic,
        NoiseBrief, NoiseFocus,
        SneakBrief, Sneak,
        MeleeBrief, Melee,
        MeleeKillDelay,
        BleedingBrief, OpenHealth, Bandage,
        RangedCinematic, RangedBrief, RangedNoiseBrief, RangedReturn, RangedCombat,
        FinalKillDelay, FinalTaunt, HordeAssault, DeathPause, Ending
    }

    [Header("Targets")]
    [SerializeField] private IntroTutorialDirector introDirector;
    [SerializeField] private Transform kitchenCabinet;
    [SerializeField] private TutorialPhaseOneText tutorialText;
    [SerializeField] private RoofVisibility targetHouseRoof;
    [SerializeField] private Transform tutorialZombieSpawn;
    [SerializeField] private Transform tutorialZombieSpawn2;
    [SerializeField] private NetworkObject tutorialZombiePrefab;

    [Header("Progress tuning")]
    [SerializeField, Min(0.5f)] private float movementSecondsRequired = 3f;
    [SerializeField, Min(0.5f)] private float requiredIndoorSeconds = 2f;
    [SerializeField, Min(0.5f)] private float requiredOutdoorSeconds = 1f;
    [SerializeField, Min(0.5f)] private float aimingSecondsRequired = 1.2f;
    [SerializeField, Min(0.5f)] private float zoomPracticeSecondsRequired = 2f;
    [SerializeField, Range(0.05f, 0.39f)] private float tutorialNeedRatio = 0.35f;
    [SerializeField, Range(0f, 1f)] private float initialZoomInAmount = 0.85f;
    [SerializeField, Min(0.05f)] private float meleeInstructionDistance = 0.25f;
    [SerializeField, Min(0.5f)] private float postMeleeDelaySeconds = 2f;
    [SerializeField, Min(0.5f)] private float postRangedKillDelaySeconds = 1.5f;
    [SerializeField, Range(8, 30)] private int finalHordeCount = 16;
    [SerializeField, Min(3f)] private float finalHordeMinRadius = 7f;
    [SerializeField, Min(4f)] private float finalHordeMaxRadius = 11f;
    [SerializeField, Min(5f)] private float finalHordeFailsafeSeconds = 14f;
    [SerializeField, Min(0.5f)] private float deathScreenDelaySeconds = 2f;

    private Step step = Step.WaitingForIntro;
    private PlayerMovement localPlayer;
    private PlayerSurvival survival;
    private InventorySystem inventory;
    private PlayerCombat combat;
    private PlayerHealth health;
    private RoofDetector roofDetector;
    private IntroCameraFollow introCamera;
    private NetworkRunner runner;
    private NetworkObject firstTutorialZombie;
    private NetworkObject secondTutorialZombie;
    private float movementProgress;
    private float zoomPracticeProgress;
    private float aimingProgress;
    private float indoorProgress;
    private float outdoorProgress;
    private float postMeleeDelayProgress;
    private float finalFlowTimer;
    private bool needsApplied;
    private bool initialZoomApplied;
    private bool lootUiClosed;
    private bool zombiesSpawned;
    private bool cinematicStarted;
    private bool crouchConfirmed;
    private bool tutorialWoundApplied;
    private bool rangedCinematicStarted;
    private bool finalHordeSpawned;
    private bool endingTransitionStarted;
    private string modalTitle;
    private string modalBody;

    private void Awake()
    {
        TutorialSession.Begin();
        TutorialInputGate.SetFireLocked(true);
        TutorialInputGate.SetHealthFloor(true, 0.3f);

        introDirector ??= FindFirstObjectByType<IntroTutorialDirector>();
        introCamera ??= FindFirstObjectByType<IntroCameraFollow>();
        tutorialText ??= Resources.Load<TutorialPhaseOneText>("Tutorial/TutorialPhaseOneText");
        kitchenCabinet ??= FindTransform("Prefab_Kitchen1_E (1)");
        targetHouseRoof ??= FindComponent<RoofVisibility>("Nha8 (1)");
        tutorialZombieSpawn ??= FindTransform("TutorialZombieSpawn");
        tutorialZombieSpawn2 ??= FindTransform("TutorialZombieSpawn2");
    }

    private void OnDisable()
    {
        AutoNoiseMeter.SetTutorialHighlight(false);
        if (FogVisionController.Instance != null) FogVisionController.Instance.ClearTutorialCinematicReveal();
        TutorialInputGate.Clear();
    }

    private void Update()
    {
        CachePlayerReferences();
        if (tutorialText == null) return;

        switch (step)
        {
            case Step.WaitingForIntro:
                if (introDirector != null && introDirector.IsComplete && localPlayer != null)
                {
                    SetInitialTutorialZoom();
                    TutorialInputGate.SetCameraZoomLocked(true);
                    ShowModal(Step.MoveBrief, tutorialText.moveTitle, tutorialText.moveBrief);
                }
                break;

            case Step.MoveBrief:
                if (ContinueModalPressed())
                {
                    step = Step.Move;
                    SetTutorialMovement(false);
                }
                break;

            case Step.Move:
                if (localPlayer != null && localPlayer.NetIsMoving)
                    movementProgress += Time.unscaledDeltaTime;
                if (movementProgress >= movementSecondsRequired)
                    ShowModal(Step.ZoomBrief, tutorialText.zoomTitle, tutorialText.zoomBrief);
                break;

            case Step.ZoomBrief:
                if (ContinueModalPressed())
                {
                    step = Step.Zoom;
                    SetTutorialMovement(true);
                    TutorialInputGate.SetCameraZoomLocked(false);
                }
                break;

            case Step.Zoom:
                if (Mathf.Abs(Input.GetAxis("Mouse ScrollWheel")) > 0.001f)
                    zoomPracticeProgress = Mathf.Max(zoomPracticeProgress, Time.unscaledDeltaTime);
                if (zoomPracticeProgress > 0f)
                    zoomPracticeProgress += Time.unscaledDeltaTime;
                if (zoomPracticeProgress >= zoomPracticeSecondsRequired)
                    ShowModal(Step.AimBrief, tutorialText.aimTitle, tutorialText.aimBrief);
                break;

            case Step.AimBrief:
                if (ContinueModalPressed())
                {
                    step = Step.Aim;
                    SetTutorialMovement(true);
                }
                break;

            case Step.Aim:
                if (localPlayer != null && localPlayer.NetIsAiming)
                    aimingProgress += Time.unscaledDeltaTime;
                if (aimingProgress >= aimingSecondsRequired)
                {
                    ApplyTutorialNeeds();
                    ShowModal(Step.NeedsBrief, tutorialText.needsTitle, tutorialText.needsBrief);
                }
                break;

            case Step.NeedsBrief:
                if (ContinueModalPressed()) step = Step.NeedsFocus;
                break;

            case Step.NeedsFocus:
                if (Input.GetMouseButtonDown(0))
                    ShowModal(Step.HouseBrief, tutorialText.houseTitle, tutorialText.houseBrief);
                break;

            case Step.HouseBrief:
                if (ContinueModalPressed())
                {
                    step = Step.GoToKitchen;
                    SetTutorialMovement(false);
                }
                break;

            case Step.GoToKitchen:
                if (HasStayedInsideTargetHouse())
                    ShowModal(Step.CabinetBrief, tutorialText.cabinetTitle, tutorialText.cabinetBrief);
                break;

            case Step.CabinetBrief:
                if (ContinueModalPressed())
                {
                    step = Step.Loot;
                    SetTutorialMovement(false);
                }
                break;

            case Step.Loot:
                TryOpenTutorialCabinet();
                if (TutorialLootCount() >= 5)
                {
                    CloseLootUi();
                    ShowModal(Step.ConsumeBrief, tutorialText.consumeTitle, tutorialText.consumeBrief);
                }
                break;

            case Step.ConsumeBrief:
                if (ContinueModalPressed())
                {
                    step = Step.Consume;
                    SetTutorialMovement(true);
                }
                break;

            case Step.Consume:
                if (survival != null && survival.currentHunger >= survival.maxHunger * 0.50f && survival.currentThirst >= survival.maxThirst * 0.50f)
                {
                    // The needs highlight disappears immediately and the
                    // survivor can move again before the next lesson starts.
                    ShowModal(Step.WeaponBrief, tutorialText.weaponTitle, tutorialText.weaponBrief, false);
                }
                break;

            case Step.WeaponBrief:
                if (ContinueModalPressed())
                {
                    step = Step.EquipWeapon;
                    SetTutorialMovement(true);
                }
                break;

            case Step.EquipWeapon:
                if (HotbarHUDManager.Instance != null && HotbarHUDManager.Instance.HasGunEquipped())
                    ShowModal(Step.ReloadBrief, tutorialText.reloadTitle, tutorialText.reloadBrief);
                break;

            case Step.ReloadBrief:
                if (ContinueModalPressed())
                {
                    step = Step.Reload;
                    SetTutorialMovement(true);
                }
                break;

            case Step.Reload:
                if (combat != null && combat.currentAmmo > 0)
                {
                    SpawnTutorialZombies();
                    if (firstTutorialZombie != null)
                        ShowModal(Step.LeaveHouseBrief, tutorialText.leaveHouseTitle, tutorialText.leaveHouseBrief);
                }
                break;

            case Step.LeaveHouseBrief:
                if (ContinueModalPressed())
                {
                    step = Step.LeaveHouse;
                    SetTutorialMovement(false);
                }
                break;

            case Step.LeaveHouse:
                if (HasStayedOutsideTargetHouse())
                {
                    SetTutorialMovement(true);
                    step = Step.ZombieCinematic;
                }
                break;

            case Step.ZombieCinematic:
                RunZombieCinematic();
                break;

            case Step.NoiseBrief:
                if (ContinueModalPressed())
                {
                    ShowModal(Step.SneakBrief, tutorialText.sneakTitle, tutorialText.sneakBrief);
                }
                break;

            case Step.SneakBrief:
                if (ContinueModalPressed())
                {
                    step = Step.Sneak;
                    crouchConfirmed = false;
                    // The player must consciously enter crouch before being
                    // allowed to move toward the zombie.
                    SetTutorialMovement(true);
                }
                break;

            case Step.Sneak:
                if (!crouchConfirmed)
                {
                    if (localPlayer != null && localPlayer.NetIsCrouching)
                    {
                        crouchConfirmed = true;
                        SetTutorialMovement(false);
                    }
                    break;
                }

                if (IsInMeleeRange())
                    ShowModal(Step.MeleeBrief, tutorialText.meleeTitle, tutorialText.meleeBrief);
                break;

            case Step.MeleeBrief:
                if (ContinueModalPressed())
                {
                    step = Step.Melee;
                    SetTutorialMovement(false);
                }
                break;

            case Step.Melee:
                if (IsFirstTutorialZombieDead())
                {
                    step = Step.MeleeKillDelay;
                    postMeleeDelayProgress = 0f;
                    SetTutorialMovement(true);
                }
                break;

            case Step.MeleeKillDelay:
                postMeleeDelayProgress += Time.unscaledDeltaTime;
                if (postMeleeDelayProgress >= postMeleeDelaySeconds)
                {
                    EnsureTutorialWound();
                    ShowModal(Step.BleedingBrief, tutorialText.bleedingTitle, tutorialText.bleedingBrief);
                }
                break;

            case Step.BleedingBrief:
                if (ContinueModalPressed())
                {
                    step = Step.OpenHealth;
                    SetTutorialMovement(true);
                }
                break;

            case Step.OpenHealth:
                if (IsHealthTabOpen()) step = Step.Bandage;
                break;

            case Step.Bandage:
                if (IsTutorialWoundBandaged())
                {
                    CloseTutorialPanels();
                    rangedCinematicStarted = false;
                    step = Step.RangedCinematic;
                    SetTutorialMovement(true);
                }
                break;

            case Step.RangedCinematic:
                RunRangedCinematic();
                break;

            case Step.RangedBrief:
                if (ContinueModalPressed())
                    ShowModal(Step.RangedNoiseBrief, tutorialText.rangedNoiseTitle, tutorialText.rangedNoiseBrief);
                break;

            case Step.RangedNoiseBrief:
                if (ContinueModalPressed())
                {
                    step = Step.RangedReturn;
                    introCamera?.ReturnTutorialFocus();
                }
                break;

            case Step.RangedReturn:
                if (introCamera == null || !introCamera.IsTutorialFocusPlaying)
                {
                    if (FogVisionController.Instance != null) FogVisionController.Instance.ClearTutorialCinematicReveal();
                    // Zombie B now returns to its normal AI. It will acquire
                    // the survivor through vision or react to the first gunshot.
                    secondTutorialZombie?.GetComponent<ZOmbieAI_Khoa>()?.ReleaseTutorialStationary();
                    step = Step.RangedCombat;
                    SetTutorialMovement(true);
                    TutorialInputGate.SetFireLocked(false);
                }
                break;

            case Step.RangedCombat:
                if (IsSecondTutorialZombieDead())
                {
                    TutorialInputGate.SetFireLocked(true);
                    secondTutorialZombie?.GetComponent<ZOmbieAI_Khoa>()?.SetTutorialForceVisible(false);
                    step = Step.FinalKillDelay;
                    finalFlowTimer = 0f;
                    SetTutorialMovement(true);
                }
                break;

            case Step.FinalKillDelay:
                finalFlowTimer += Time.unscaledDeltaTime;
                if (finalFlowTimer >= postRangedKillDelaySeconds)
                    ShowModal(Step.FinalTaunt, tutorialText.finalTauntTitle, tutorialText.finalTauntBrief);
                break;

            case Step.FinalTaunt:
                if (ContinueModalPressed())
                {
                    BeginFinalHorde();
                }
                break;

            case Step.HordeAssault:
                finalFlowTimer += Time.unscaledDeltaTime;
                if (finalFlowTimer >= 2f) AutoNoiseMeter.SetTutorialHighlight(false);
                if (health != null && health.isDead)
                {
                    step = Step.DeathPause;
                    finalFlowTimer = 0f;
                    TutorialInputGate.SetFireLocked(true);
                    TutorialInputGate.Configure(true, true);
                }
                else if (finalFlowTimer >= finalHordeFailsafeSeconds && health != null)
                {
                    // The horde should earn the kill naturally. This only
                    // prevents a broken pathfinding node from soft-locking the ending.
                    health.TakeDamage(health.maxHealth * 10f, false, true);
                }
                break;

            case Step.DeathPause:
                finalFlowTimer += Time.unscaledDeltaTime;
                if (finalFlowTimer >= deathScreenDelaySeconds)
                {
                    step = Step.Ending;
                    SetTutorialMovement(false);
                    TutorialInputGate.Configure(true, true);
                    TutorialInputGate.SetFireLocked(true);
                }
                break;
        }
    }

    private void CachePlayerReferences()
    {
        if (localPlayer == null) localPlayer = PlayerMovement.LocalPlayerInstance;
        if (localPlayer == null) return;
        survival ??= localPlayer.GetComponent<PlayerSurvival>();
        inventory ??= localPlayer.GetComponent<InventorySystem>();
        combat ??= localPlayer.GetComponent<PlayerCombat>();
        health ??= localPlayer.GetComponent<PlayerHealth>();
        roofDetector ??= localPlayer.GetComponentInChildren<RoofDetector>();
        introCamera ??= FindFirstObjectByType<IntroCameraFollow>();
        runner ??= FindAnyObjectByType<NetworkRunner>();
    }

    private void SetTutorialMovement(bool locked)
    {
        TutorialInputGate.Configure(locked, true);
        TutorialInputGate.SetFireLocked(true);
    }

    private void ApplyTutorialNeeds()
    {
        if (needsApplied || survival == null) return;
        needsApplied = true;
        survival.SetTutorialNeeds(tutorialNeedRatio, tutorialNeedRatio);
    }

    private void SetInitialTutorialZoom()
    {
        if (initialZoomApplied) return;
        initialZoomApplied = true;
        introCamera ??= FindFirstObjectByType<IntroCameraFollow>();
        introCamera?.SetZoomInAmount(initialZoomInAmount);
    }

    private bool HasStayedInsideTargetHouse()
    {
        bool isInside = IsInsideTargetHouse();
        indoorProgress = isInside ? indoorProgress + Time.unscaledDeltaTime : 0f;
        return indoorProgress >= requiredIndoorSeconds;
    }

    private bool HasStayedOutsideTargetHouse()
    {
        bool isOutside = roofDetector != null && !IsInsideTargetHouse();
        outdoorProgress = isOutside ? outdoorProgress + Time.unscaledDeltaTime : 0f;
        return outdoorProgress >= requiredOutdoorSeconds;
    }

    private bool IsInsideTargetHouse() => roofDetector != null && targetHouseRoof != null && roofDetector.CurrentRoof == targetHouseRoof;

    private int TutorialLootCount()
    {
        if (inventory == null) return 0;
        int count = 0;
        if (inventory.HasItemNamed("Meat")) count++;
        if (inventory.HasItemNamed("Water")) count++;
        if (inventory.HasItemNamed("Bandage")) count++;
        if (inventory.HasItemNamed("Ammo12Gauge")) count++;
        if (inventory.HasItemNamed("S12K")) count++;
        return count;
    }

    private void CloseLootUi()
    {
        if (lootUiClosed) return;
        lootUiClosed = true;
        if (AutoUIManager.Instance == null) return;
        AutoUIManager.Instance.CloseContainerUI();
        AutoUIManager.Instance.ForceHideInventoryOnly();
    }

    private void TryOpenTutorialCabinet()
    {
        if (!Input.GetMouseButtonDown(0) || !ResolveKitchenCabinet()) return;
        LootContainer container = kitchenCabinet.GetComponent<LootContainer>();
        if (container == null) container = kitchenCabinet.GetComponentInChildren<LootContainer>();
        container?.TryOpenForLocalPlayer();
    }

    /// <summary>
    /// The kitchen prefab can finish instantiating after this director's Awake.
    /// Resolve lazily as well as at startup, so an Inspector reference is an
    /// optional convenience and never a tutorial-breaking requirement.
    /// </summary>
    private bool ResolveKitchenCabinet()
    {
        if (kitchenCabinet != null) return true;

        kitchenCabinet = FindTransform("Prefab_Kitchen1_E (1)");
        if (kitchenCabinet != null) return true;

        foreach (LootContainer candidate in FindObjectsByType<LootContainer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate != null && candidate.name.Contains("Kitchen1_E"))
            {
                kitchenCabinet = candidate.transform;
                return true;
            }
        }

        return false;
    }

    private void SpawnTutorialZombies()
    {
        if (zombiesSpawned || runner == null || !runner.IsServer || tutorialZombiePrefab == null ||
            tutorialZombieSpawn == null || tutorialZombieSpawn2 == null || localPlayer == null)
            return;

        zombiesSpawned = true;
        Vector2 firstFacing = FacingAwayFromPlayer(tutorialZombieSpawn.position);
        Vector2 secondFacing = FacingAwayFromPlayer(tutorialZombieSpawn2.position);

        // Zombie sprites face by Animator MoveX/MoveY, never by rotating their
        // root Transform. Rotating this 2D object makes the sprite look as if
        // it is lying on the road.
        firstTutorialZombie = runner.Spawn(tutorialZombiePrefab, tutorialZombieSpawn.position,
            Quaternion.identity, null, (_, obj) =>
            {
                obj.GetComponent<ZOmbieAI_Khoa>()?.ConfigureTutorialSpawn(firstFacing, 10f, true);
            });
        secondTutorialZombie = runner.Spawn(tutorialZombiePrefab, tutorialZombieSpawn2.position,
            Quaternion.identity, null, (_, obj) =>
            {
                // Reserved, hidden by fog, for the next gunfire lesson.
                obj.GetComponent<ZOmbieAI_Khoa>()?.ConfigureTutorialSpawn(secondFacing, 100f, true);
            });
    }

    private Vector2 FacingAwayFromPlayer(Vector3 spawnPosition)
    {
        Vector2 away = (Vector2)spawnPosition - (Vector2)localPlayer.transform.position;
        return away.sqrMagnitude > 0.001f ? away.normalized : Vector2.down;
    }

    private void RunZombieCinematic()
    {
        if (firstTutorialZombie == null) return;

        if (!cinematicStarted)
        {
            cinematicStarted = true;
            firstTutorialZombie.GetComponent<ZOmbieAI_Khoa>()?.SetTutorialForceVisible(true);
            if (FogVisionController.Instance != null)
                FogVisionController.Instance.SetTutorialCinematicReveal(firstTutorialZombie.transform);
            introCamera ??= FindFirstObjectByType<IntroCameraFollow>();
            introCamera?.PlayTutorialFocus(firstTutorialZombie.transform);
            return;
        }

        if (introCamera != null && introCamera.IsTutorialFocusPlaying) return;

        if (FogVisionController.Instance != null) FogVisionController.Instance.ClearTutorialCinematicReveal();
        firstTutorialZombie.GetComponent<ZOmbieAI_Khoa>()?.SetTutorialForceVisible(false);
        ShowModal(Step.NoiseBrief, tutorialText.noiseTitle, tutorialText.noiseBrief);
    }

    private void EnsureTutorialWound()
    {
        if (tutorialWoundApplied) return;
        tutorialWoundApplied = true;

        AutoHealthPanel panel = AutoHealthPanel.Instance;
        if (panel != null && !panel.HasAnyUnbandagedInjury())
            panel.ApplyTutorialWound("Right Forearm");
    }

    private bool IsHealthTabOpen()
    {
        return AutoTabManager.Instance != null &&
               AutoTabManager.Instance.IsTabCanvasActive() &&
               AutoTabManager.Instance.CurrentTab == AutoTabManager.TabType.Health &&
               AutoHealthPanel.Instance != null && AutoHealthPanel.Instance.IsOpen;
    }

    private bool IsTutorialWoundBandaged()
    {
        AutoHealthPanel panel = AutoHealthPanel.Instance;
        return panel != null && panel.HasAnyBandagedInjury() &&
               health != null && !health.isBleeding;
    }

    private static void CloseTutorialPanels()
    {
        if (AutoTabManager.Instance != null) AutoTabManager.Instance.ShowTabs(false);
        if (AutoHealthPanel.Instance != null) AutoHealthPanel.Instance.SetOpenState(false);
        if (AutoUIManager.Instance != null) AutoUIManager.Instance.ForceHideInventoryOnly();
    }

    private void RunRangedCinematic()
    {
        if (secondTutorialZombie == null) return;

        if (!rangedCinematicStarted)
        {
            rangedCinematicStarted = true;
            SetTutorialMovement(true);
            TutorialInputGate.SetFireLocked(true);
            secondTutorialZombie.GetComponent<ZOmbieAI_Khoa>()?.SetTutorialForceVisible(true);
            if (FogVisionController.Instance != null)
                FogVisionController.Instance.SetTutorialCinematicReveal(secondTutorialZombie.transform);
            introCamera ??= FindFirstObjectByType<IntroCameraFollow>();
            introCamera?.HoldTutorialFocus(secondTutorialZombie.transform);
            return;
        }

        if (introCamera != null && !introCamera.IsTutorialFocusHeld) return;
        ShowModal(Step.RangedBrief, tutorialText.rangedTitle, tutorialText.rangedBrief);
    }

    private void BeginFinalHorde()
    {
        step = Step.HordeAssault;
        finalFlowTimer = 0f;

        // From this point onward death is intentional. All regular controls
        // return so the last stand still feels playable rather than scripted.
        TutorialInputGate.SetHealthFloor(false);
        TutorialInputGate.Configure(false, false);
        TutorialInputGate.SetFireLocked(false);
        TutorialInputGate.SetCameraZoomLocked(false);
        AutoNoiseMeter.SetTutorialHighlight(true);
        AutoNoiseMeter.ReportTransientNoise(1f, "TIẾNG SÚNG VANG KHẮP KHU PHỐ");

        SpawnFinalHorde();
        if (localPlayer != null)
            localPlayer.MakeNoise(60f, true, 100, 60f, 1f);
    }

    private void SpawnFinalHorde()
    {
        if (finalHordeSpawned || runner == null || !runner.IsServer ||
            tutorialZombiePrefab == null || localPlayer == null) return;

        finalHordeSpawned = true;
        Vector3 playerPosition = localPlayer.transform.position;
        float outerRadius = Mathf.Max(finalHordeMaxRadius, finalHordeMinRadius + 0.5f);

        for (int i = 0; i < finalHordeCount; i++)
        {
            float angle = (360f / finalHordeCount) * i + Random.Range(-9f, 9f);
            float radius = Random.Range(finalHordeMinRadius, outerRadius);
            Vector2 direction = Quaternion.Euler(0f, 0f, angle) * Vector2.right;
            Vector3 candidate = playerPosition + (Vector3)(direction * radius);
            Vector3 spawnPosition = ClosestWalkablePosition(candidate);

            // A nearest-node query can fold an invalid point too close to the
            // survivor. Skip that actor instead of visibly spawning it on top.
            if (Vector2.Distance(spawnPosition, playerPosition) < 3.5f) continue;

            Vector2 facing = ((Vector2)playerPosition - (Vector2)spawnPosition).normalized;
            NetworkObject spawned = runner.Spawn(tutorialZombiePrefab, spawnPosition,
                Quaternion.identity, null, (_, obj) =>
                {
                    obj.GetComponent<ZOmbieAI_Khoa>()?.ConfigureTutorialSpawn(facing, 100f, false);
                });

            spawned?.GetComponent<ZOmbieAI_Khoa>()?.ReleaseTutorialStationary(playerPosition);
        }
    }

    private static Vector3 ClosestWalkablePosition(Vector3 candidate)
    {
        if (AstarPath.active == null) return candidate;
        NNInfo nearest = AstarPath.active.GetNearest(candidate, NNConstraint.Default);
        return nearest.node != null && nearest.node.Walkable ? (Vector3)nearest.position : candidate;
    }

    private async void StartMainGame()
    {
        if (endingTransitionStarted) return;
        endingTransitionStarted = true;
        TutorialSession.End();
        TutorialInputGate.Clear();

        int sceneIndex = AutoMainMenuManager.Instance != null
            ? AutoMainMenuManager.Instance.mainSceneIndex
            : 1;
        if (runner != null)
            await runner.LoadScene(SceneRef.FromIndex(sceneIndex));
        else
            SceneManager.LoadScene(sceneIndex);
    }

    private async void ReplayTutorial()
    {
        if (endingTransitionStarted) return;
        endingTransitionStarted = true;
        TutorialSession.Begin();
        TutorialInputGate.Clear();

        int sceneIndex = AutoMainMenuManager.Instance != null
            ? AutoMainMenuManager.Instance.tutorialSceneIndex
            : SceneManager.GetActiveScene().buildIndex;
        if (runner != null)
            await runner.LoadScene(SceneRef.FromIndex(sceneIndex));
        else
            SceneManager.LoadScene(sceneIndex);
    }

    private void ReturnToMenu()
    {
        if (endingTransitionStarted) return;
        endingTransitionStarted = true;
        TutorialSession.End();
        TutorialInputGate.Clear();

        if (runner != null) runner.Shutdown();
        else SceneManager.LoadScene(0);
    }

    private bool IsInMeleeRange()
    {
        if (localPlayer == null || firstTutorialZombie == null || combat == null) return false;
        Collider2D playerCollider = localPlayer.GetComponent<Collider2D>();
        Collider2D zombieCollider = firstTutorialZombie.GetComponent<Collider2D>();
        if (playerCollider == null || zombieCollider == null) return false;
        float distance = Mathf.Max(Physics2D.Distance(playerCollider, zombieCollider).distance, 0f);
        // Deliberately stricter than the weapon's actual hit radius: this is
        // a teaching beat, so the prompt only appears when the survivor is
        // visibly right behind the target.
        return distance <= meleeInstructionDistance;
    }

    private bool IsFirstTutorialZombieDead()
    {
        return firstTutorialZombie != null && firstTutorialZombie.TryGetComponent(out ZOmbieAI_Khoa zombie) && zombie.NetIsDead;
    }

    private bool IsSecondTutorialZombieDead()
    {
        return secondTutorialZombie != null &&
               secondTutorialZombie.TryGetComponent(out ZOmbieAI_Khoa zombie) && zombie.NetIsDead;
    }

    private void ShowModal(Step nextStep, string title, string body, bool lockMovement = true)
    {
        step = nextStep;
        modalTitle = title;
        modalBody = body;
        SetTutorialMovement(lockMovement);
    }

    private bool ContinueModalPressed() => Input.GetMouseButtonDown(0);

    private Transform FindTransform(string name)
    {
        GameObject found = GameObject.Find(name);
        return found != null ? found.transform : null;
    }

    private T FindComponent<T>(string name) where T : Component
    {
        GameObject found = GameObject.Find(name);
        return found != null ? found.GetComponent<T>() : null;
    }

    private void OnGUI()
    {
        if (step == Step.WaitingForIntro) return;

        GUI.depth = -100;

        if (step == Step.Ending)
        {
            DrawEndingScreen();
            return;
        }

        if (step == Step.DeathPause)
        {
            DrawDeathFade();
            return;
        }

        if (step == Step.BleedingBrief) DrawBleedingFocusModal();
        else if (step == Step.NoiseBrief || step == Step.RangedNoiseBrief) DrawNoiseFocusModal();
        else if (IsModalStep()) DrawModal();
        else if (step == Step.NeedsFocus) DrawNeedsFocus();
        else
        {
            DrawObjective();
            if (step == Step.Consume) DrawNeedsIndicatorHighlight();
        }
    }

    private bool IsModalStep()
    {
        return step == Step.MoveBrief || step == Step.AimBrief || step == Step.NeedsBrief ||
               step == Step.ZoomBrief || step == Step.HouseBrief || step == Step.CabinetBrief ||
               step == Step.ConsumeBrief || step == Step.WeaponBrief || step == Step.ReloadBrief ||
               step == Step.LeaveHouseBrief || step == Step.SneakBrief ||
               step == Step.MeleeBrief || step == Step.RangedBrief || step == Step.FinalTaunt;
    }

    private void DrawModal()
    {
        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.72f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = old;

        float width = Mathf.Min(720f, Screen.width - 80f);
        GUIStyle bodyStyle = BodyStyle();
        float bodyHeight = bodyStyle.CalcHeight(new GUIContent(modalBody), width - 56f);
        float boxHeight = Mathf.Clamp(98f + bodyHeight, 205f, Screen.height - 70f);
        Rect box = new Rect((Screen.width - width) * 0.5f, (Screen.height - boxHeight) * 0.5f, width, boxHeight);
        GUI.Box(box, string.Empty);
        GUI.Label(new Rect(box.x + 28, box.y + 28, box.width - 56, 36), modalTitle, TitleStyle());
        GUI.Label(new Rect(box.x + 28, box.y + 75, box.width - 56, box.height - 92), modalBody, bodyStyle);
    }

    private void DrawNeedsFocus()
    {
        Rect hole = NeedsIndicatorRect();
        DrawDimWithHole(hole);
        DrawSpotlightOutline(hole);

        float messageWidth = Mathf.Min(610f, Screen.width - 160f);
        GUIStyle bodyStyle = BodyStyle();
        float messageBodyHeight = bodyStyle.CalcHeight(new GUIContent(tutorialText.needsFocusBody), messageWidth - 44f);
        float messageHeight = Mathf.Clamp(86f + messageBodyHeight, 165f, Screen.height - 70f);
        Rect message = new Rect(40f, (Screen.height - messageHeight) * 0.5f, messageWidth, messageHeight);
        GUI.Box(message, string.Empty);
        GUI.Label(new Rect(message.x + 22, message.y + 18, message.width - 44, 38), tutorialText.needsFocusTitle, TitleStyle());
        GUI.Label(new Rect(message.x + 22, message.y + 65, message.width - 44, message.height - 78), tutorialText.needsFocusBody, bodyStyle);
    }

    private void DrawNeedsIndicatorHighlight() => DrawSpotlightOutline(NeedsIndicatorRect());

    private static Rect NeedsIndicatorRect() => new Rect(Screen.width - 75f, 121f, 68f, 110f);

    private void DrawBleedingFocusModal()
    {
        if (survival == null || !survival.TryGetBleedingMoodleRect(out Rect hole))
        {
            DrawModal();
            return;
        }

        DrawDimWithHole(hole);
        DrawSpotlightOutline(hole, true);

        float width = Mathf.Min(690f, Screen.width - 210f);
        GUIStyle bodyStyle = BodyStyle();
        float bodyHeight = bodyStyle.CalcHeight(new GUIContent(modalBody), width - 56f);
        float boxHeight = Mathf.Clamp(98f + bodyHeight, 215f, Screen.height - 90f);
        Rect box = new Rect(38f, (Screen.height - boxHeight) * 0.5f, width, boxHeight);
        GUI.Box(box, string.Empty);
        GUI.Label(new Rect(box.x + 28f, box.y + 28f, box.width - 56f, 36f), modalTitle, TitleStyle());
        GUI.Label(new Rect(box.x + 28f, box.y + 75f, box.width - 56f, box.height - 92f), modalBody, bodyStyle);
    }

    private static Rect NoiseMeterRect()
    {
        // Mirrors AutoNoiseMeter's 1920x1080 bottom-left layout. The meter
        // remains highlighted correctly at every camera zoom and game view size.
        float scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
        float width = 350f * scale;
        float height = 82f * scale;
        return new Rect(38f * scale, Screen.height - (36f * scale) - height, width, height);
    }

    private void DrawNoiseFocusModal()
    {
        Rect hole = NoiseMeterRect();
        DrawDimWithHole(hole);
        DrawSpotlightOutline(hole, true);

        float width = Mathf.Min(720f, Screen.width - 80f);
        GUIStyle bodyStyle = BodyStyle();
        float bodyHeight = bodyStyle.CalcHeight(new GUIContent(modalBody), width - 56f);
        float boxHeight = Mathf.Clamp(98f + bodyHeight, 205f, Screen.height - 185f);
        Rect box = new Rect((Screen.width - width) * 0.5f,
            Mathf.Clamp((Screen.height - boxHeight) * 0.5f - 45f, 25f, Screen.height - boxHeight - 125f),
            width, boxHeight);
        GUI.Box(box, string.Empty);
        GUI.Label(new Rect(box.x + 28, box.y + 28, box.width - 56, 36), modalTitle, TitleStyle());
        GUI.Label(new Rect(box.x + 28, box.y + 75, box.width - 56, box.height - 92), modalBody, bodyStyle);
    }

    private void DrawDimWithHole(Rect hole)
    {
        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.70f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, hole.y), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0, hole.yMax, Screen.width, Screen.height - hole.yMax), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0, hole.y, hole.x, hole.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(hole.xMax, hole.y, Screen.width - hole.xMax, hole.height), Texture2D.whiteTexture);
        GUI.color = old;
    }

    private void DrawSpotlightOutline(Rect hole, bool arrowAbove = false)
    {
        float pulse = 1f + Mathf.PingPong(Time.unscaledTime * 2f, 1f);
        Color old = GUI.color;
        GUI.color = new Color(1f, 0.84f, 0.12f, 0.95f);
        DrawOutline(new Rect(hole.x - 4f - pulse, hole.y - 4f - pulse, hole.width + 8f + pulse * 2f, hole.height + 8f + pulse * 2f), 2f);
        GUI.Label(arrowAbove
                ? new Rect(hole.center.x - 28f, hole.y - 43f, 56f, 42f)
                : new Rect(hole.center.x - 28f, hole.yMax + 6f, 56f, 42f),
            arrowAbove ? "↓" : "↑", PointerStyle());
        GUI.color = old;
    }

    private static void DrawOutline(Rect rect, float thickness)
    {
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
    }

    private void DrawObjective()
    {
        string text = step switch
        {
            Step.Move => tutorialText.moveObjective,
            Step.Zoom => tutorialText.zoomObjective,
            Step.Aim => tutorialText.aimObjective,
            Step.GoToKitchen => tutorialText.houseObjective,
            Step.Loot => tutorialText.lootObjective,
            Step.Consume => tutorialText.consumeObjective,
            Step.EquipWeapon => tutorialText.weaponObjective,
            Step.Reload => tutorialText.reloadObjective,
            Step.LeaveHouse => tutorialText.leaveHouseObjective,
            Step.Sneak => crouchConfirmed ? tutorialText.sneakObjective : "NHẤN [C] ĐỂ NGỒI XUỐNG",
            Step.Melee => tutorialText.meleeObjective,
            Step.OpenHealth => tutorialText.openHealthObjective,
            Step.Bandage => tutorialText.bandageObjective,
            Step.RangedCombat => tutorialText.rangedObjective,
            Step.HordeAssault => tutorialText.hordeObjective,
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(text)) return;
        float width = Mathf.Min(760f, Screen.width - 80f);
        GUIStyle objectiveStyle = ObjectiveStyle();
        float labelHeight = objectiveStyle.CalcHeight(new GUIContent(text), width - 30f);
        float boxHeight = Mathf.Max(55f, labelHeight + 22f);
        Rect box = new Rect((Screen.width - width) * 0.5f, 30f, width, boxHeight);
        GUI.Box(box, string.Empty);
        GUI.Label(new Rect(box.x + 15f, box.y + 11f, box.width - 30f, labelHeight), text, objectiveStyle);

        if (step == Step.GoToKitchen) DrawHouseRoute();
        else if (step == Step.Loot && ResolveKitchenCabinet())
            DrawWorldMarker(kitchenCabinet.position + Vector3.up * 0.8f, tutorialText.cabinetMarker);
    }

    private void DrawHouseRoute()
    {
        targetHouseRoof ??= FindComponent<RoofVisibility>("Nha8 (1)");
        if (targetHouseRoof == null || Camera.main == null) return;
        // A dotted, screen-space breadcrumb line leads to the house, while
        // the final arrow is clamped to the viewport edge when off-screen.
        // This deliberately targets Nha8 itself, not its furniture: the
        // marker exists before the kitchen prefab has finished instantiating.
        DrawWorldMarker(targetHouseRoof.transform.position, tutorialText.houseMarker);
    }

    private void DrawWorldMarker(Vector3 worldPosition, string label)
    {
        if (Camera.main == null) return;
        Vector3 point = Camera.main.WorldToScreenPoint(worldPosition);
        if (point.z <= 0f) return;

        Vector2 screenPoint = new Vector2(point.x, Screen.height - point.y);
        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Rect safeArea = new Rect(44f, 105f, Screen.width - 88f, Screen.height - 220f);
        bool onScreen = safeArea.Contains(screenPoint);
        Vector2 marker = onScreen ? screenPoint : ClampToScreenEdge(center, screenPoint, safeArea);

        DrawBreadcrumbs(center, marker, onScreen ? 7 : 9);
        float pulse = 24f + Mathf.PingPong(Time.unscaledTime * 30f, 13f);
        Color old = GUI.color;
        GUI.color = new Color(1f, 0.82f, 0.1f, 0.95f);
        GUI.Box(new Rect(marker.x - pulse, marker.y - pulse, pulse * 2f, pulse * 2f), string.Empty);
        GUI.color = old;
        GUI.Label(new Rect(marker.x - 105f, marker.y - pulse - 30f, 210f, 28f), label, MarkerStyle());
        if (!onScreen)
            GUI.Label(new Rect(marker.x - 30f, marker.y - 23f, 60f, 46f), DirectionArrow(center, marker), PointerStyle());
    }

    private static Vector2 ClampToScreenEdge(Vector2 from, Vector2 to, Rect bounds)
    {
        Vector2 direction = (to - from).normalized;
        float tx = direction.x > 0f ? (bounds.xMax - from.x) / direction.x : direction.x < 0f ? (bounds.xMin - from.x) / direction.x : float.PositiveInfinity;
        float ty = direction.y > 0f ? (bounds.yMax - from.y) / direction.y : direction.y < 0f ? (bounds.yMin - from.y) / direction.y : float.PositiveInfinity;
        return from + direction * Mathf.Min(Mathf.Abs(tx), Mathf.Abs(ty));
    }

    private static string DirectionArrow(Vector2 from, Vector2 to)
    {
        Vector2 d = (to - from).normalized;
        if (Mathf.Abs(d.x) > Mathf.Abs(d.y)) return d.x > 0 ? "→" : "←";
        return d.y > 0 ? "↓" : "↑";
    }

    private static void DrawBreadcrumbs(Vector2 from, Vector2 to, int count)
    {
        Color old = GUI.color;
        GUI.color = new Color(1f, 0.83f, 0.12f, 0.88f);
        for (int i = 1; i < count; i++)
        {
            float t = i / (float)count;
            Vector2 p = Vector2.Lerp(from, to, t);
            float size = 5f + t * 5f;
            GUI.DrawTexture(new Rect(p.x - size * 0.5f, p.y - size * 0.5f, size, size), Texture2D.whiteTexture);
        }
        GUI.color = old;
    }

    private GUIStyle TitleStyle() => new GUIStyle(GUI.skin.label)
    {
        fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
        normal = { textColor = new Color(1f, 0.83f, 0.22f) }
    };

    private GUIStyle BodyStyle() => new GUIStyle(GUI.skin.label)
    {
        fontSize = 19, alignment = TextAnchor.UpperCenter, wordWrap = true,
        normal = { textColor = Color.white }
    };

    private GUIStyle ObjectiveStyle() => new GUIStyle(GUI.skin.label)
    {
        fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true,
        normal = { textColor = Color.white }
    };

    private void DrawEndingScreen()
    {
        Color old = GUI.color;
        GUI.color = new Color(0.015f, 0.01f, 0.01f, 0.96f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = old;

        float width = Mathf.Min(720f, Screen.width - 80f);
        float height = Mathf.Min(470f, Screen.height - 70f);
        Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        GUI.Box(panel, string.Empty);

        GUI.Label(new Rect(panel.x + 30f, panel.y + 42f, panel.width - 60f, 55f),
            tutorialText.endingTitle, EndingTitleStyle());
        GUI.Label(new Rect(panel.x + 50f, panel.y + 112f, panel.width - 100f, 90f),
            tutorialText.endingBody, BodyStyle());

        float buttonWidth = Mathf.Min(430f, panel.width - 100f);
        float buttonX = panel.center.x - buttonWidth * 0.5f;
        GUI.enabled = !endingTransitionStarted;
        if (GUI.Button(new Rect(buttonX, panel.y + 225f, buttonWidth, 48f), tutorialText.startMainButton))
            StartMainGame();
        if (GUI.Button(new Rect(buttonX, panel.y + 287f, buttonWidth, 48f), tutorialText.replayButton))
            ReplayTutorial();
        if (GUI.Button(new Rect(buttonX, panel.y + 349f, buttonWidth, 48f), tutorialText.returnMenuButton))
            ReturnToMenu();
        GUI.enabled = true;
    }

    private void DrawDeathFade()
    {
        float progress = Mathf.Clamp01(finalFlowTimer / Mathf.Max(deathScreenDelaySeconds, 0.01f));
        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, progress * 0.92f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = old;
    }

    private GUIStyle EndingTitleStyle() => new GUIStyle(GUI.skin.label)
    {
        fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
        normal = { textColor = new Color(0.85f, 0.12f, 0.10f) }
    };

    private GUIStyle MarkerStyle() => new GUIStyle(GUI.skin.label)
    {
        fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
        normal = { textColor = new Color(1f, 0.88f, 0.2f) }
    };

    private GUIStyle PointerStyle() => new GUIStyle(GUI.skin.label)
    {
        fontSize = 38, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
        normal = { textColor = new Color(1f, 0.86f, 0.15f) }
    };

}
