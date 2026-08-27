using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public sealed class MilitaryRouteCinematicController : MonoBehaviour
{
    private const float EnergyDrinkRunSpeedMultiplier = 1.5f;
    private readonly List<(Renderer renderer, bool enabled, bool forceRenderingOff)> hiddenRenderers = new();
    private readonly HashSet<Renderer> hiddenRendererSet = new();
    private readonly List<PlayerMovement> suppressedPlayers = new();
    private readonly List<(Canvas canvas, bool enabled)> hiddenCanvases = new();
    private readonly List<(Light2D light, bool enabled)> hiddenPlayerLights = new();
    private readonly List<(PlayerVision vision, bool enabled)> suppressedPlayerVisions = new();
    private readonly List<(GameObject nameTag, bool active)> hiddenNameTags = new();
    private MilitaryBaseQuestManager manager;
    private Coroutine routine;
    private GameObject hostClone;
    private Animator[] hostCloneAnimators = System.Array.Empty<Animator>();
    private PlayerMovement hostMovement;
    private MilitaryCinematicVisionLight cinematicVisionLight;
    private float fadeAlpha;
    private float letterboxAlpha;
    private string subtitle = string.Empty;

    public bool IsPlaying => routine != null;

    public void Configure(MilitaryBaseQuestManager owner) => manager = owner;

    public void Play(PlayerRef hostPlayer, Vector2 stagedStartPosition)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(PlayRoutine(hostPlayer, stagedStartPosition));
    }

    public void StopImmediate()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        RestorePlayers();
        FogVisionController.Instance?.ClearMilitaryCinematicVision();
        RestoreLocalCamera();
        if (hostClone != null) Destroy(hostClone);
        hostClone = null;
        hostCloneAnimators = System.Array.Empty<Animator>();
        hostMovement = null;
        cinematicVisionLight = null;
        fadeAlpha = 0f;
        letterboxAlpha = 0f;
        subtitle = string.Empty;
        AutoUIManager.Instance?.SetQuestOverlayOpen(false);
    }

    private IEnumerator PlayRoutine(PlayerRef hostPlayer, Vector2 stagedStartPosition)
    {
        AutoTabManager.Instance?.ShowTabs(false);
        if (AutoUIManager.Instance != null)
        {
            AutoUIManager.Instance.ForceHideInventoryOnly();
            AutoUIManager.Instance.CloseContainerUI();
            AutoUIManager.Instance.HideTradeWindow();
            AutoUIManager.Instance.SetQuestOverlayOpen(true);
        }
        AutoHealthPanel.Instance?.SetOpenState(false);

        Transform hostVisual = ResolvePlayerTransform(hostPlayer);
        hostMovement = hostVisual != null ? hostVisual.GetComponent<PlayerMovement>() : null;
        Vector2 carPosition = manager != null ? manager.PoliceCarPosition : Vector2.zero;
        Vector2 gatePosition = manager != null ? manager.GateClosingPosition : carPosition + Vector2.left * 4f;
        Debug.Log($"[MILITARY CINEMATIC] Bắt đầu cảnh tại {stagedStartPosition}; xe {carPosition}; cổng {gatePosition}.");

        yield return Fade(0f, 1f, 0.45f);
        hostClone = CreatePlayerVisualClone(hostVisual, hostMovement);
        hostCloneAnimators = hostClone != null
            ? hostClone.GetComponentsInChildren<Animator>(true)
            : System.Array.Empty<Animator>();
        HideAllPlayers();
        if (hostClone != null)
            hostClone.transform.position = new Vector3(stagedStartPosition.x, stagedStartPosition.y,
                hostVisual != null ? hostVisual.position.z : 0f);
        PlayerVision hostVision = hostVisual != null ? hostVisual.GetComponent<PlayerVision>() : null;
        cinematicVisionLight = hostClone != null ? hostClone.GetComponent<MilitaryCinematicVisionLight>() : null;
        FogVisionController.Instance?.SetMilitaryCinematicVision(hostVision,
            hostClone != null ? hostClone.transform : hostVisual, Vector2.down);
        FocusCamera(hostClone != null ? hostClone.transform : hostVisual);
        letterboxAlpha = 1f;
        yield return Fade(1f, 0f, 0.5f);

        Vector2 carApproach = carPosition + new Vector2(-0.8f, -0.15f);
        yield return MoveCloneAtGameplaySpeed(carApproach,
            hostMovement != null ? hostMovement.walkSpeed : 4f, false);
        Debug.Log("[MILITARY CINEMATIC] Host đã đi bộ tới xe.");
        manager?.PoliceVehicle?.PlayCinematicDoorSequence();
        yield return new WaitForSecondsRealtime(1.1f);
        if (hostClone != null) hostClone.SetActive(false);

        manager?.PoliceVehicle?.PlayCinematicFailedStarter();
        yield return new WaitForSecondsRealtime(1.35f);
        manager?.PoliceVehicle?.SetCinematicAlarm(true);
        yield return new WaitForSecondsRealtime(1.15f);

        manager?.PoliceVehicle?.PlayCinematicDoorSequence();
        if (hostClone != null)
        {
            hostClone.transform.position = carPosition + new Vector2(-0.9f, -0.2f);
            hostClone.SetActive(true);
        }
        subtitle = "Chết tiệt... xe hỏng rồi. Phải mau chóng tìm cách sửa lại xe và tẩu thoát khỏi đây!";
        yield return new WaitForSecondsRealtime(2.25f);
        subtitle = string.Empty;

        Vector2 insideGate = gatePosition + new Vector2(0f, 1.15f);
        yield return MoveCloneAtGameplaySpeed(insideGate,
            (hostMovement != null ? hostMovement.runSpeed : 7f) * EnergyDrinkRunSpeedMultiplier, true);
        Debug.Log("[MILITARY CINEMATIC] Host đã chạy tới vị trí đóng cổng.");
        yield return Fade(0f, 1f, 0.5f);

        if (manager != null && manager.HasStateAuthority)
            manager.AuthorityCompleteMilitaryIntroCinematic(hostPlayer);
        yield return new WaitForSecondsRealtime(0.7f);

        manager?.PoliceVehicle?.SetCinematicAlarmBackground();
        RestorePlayers();
        FogVisionController.Instance?.ClearMilitaryCinematicVision();
        RestoreLocalCamera();
        if (hostClone != null) Destroy(hostClone);
        hostClone = null;
        hostCloneAnimators = System.Array.Empty<Animator>();
        hostMovement = null;
        cinematicVisionLight = null;
        yield return Fade(1f, 0f, 0.65f);
        letterboxAlpha = 0f;
        AutoUIManager.Instance?.SetQuestOverlayOpen(false);
        routine = null;

        // Start the siege radio only after the cinematic has restored the
        // gameplay canvas. RouteBRadioBroadcastUI snapshots foreign Canvas
        // states while it owns presentation; starting it earlier captured the
        // intentionally-disabled AutoCanvas and restored it disabled forever.
        if (manager != null && manager.Runner != null && manager.Runner.LocalPlayer == hostPlayer)
            RouteBRadioBroadcastUI.ShowCue(RouteBAudioCueId.SiegeStarted);
    }

    private Transform ResolvePlayerTransform(PlayerRef player)
    {
        if (manager != null && manager.Runner != null && manager.Runner.TryGetPlayerObject(player, out NetworkObject obj) &&
            obj != null)
            return obj.transform;
        return PlayerMovement.LocalPlayerInstance != null ? PlayerMovement.LocalPlayerInstance.transform : null;
    }

    private static GameObject CreatePlayerVisualClone(Transform source, PlayerMovement movement)
    {
        if (source == null) return null;
        GameObject root = CloneVisualNode(source, null, movement, true);
        root.name = "Military Cinematic Host Visual";
        root.transform.position = source.position;
        root.transform.rotation = source.rotation;
        root.transform.localScale = source.lossyScale;
        MilitaryCinematicFootstepAudio footstepAudio = root.AddComponent<MilitaryCinematicFootstepAudio>();
        footstepAudio.Configure(movement);
        MilitaryCinematicFootstepRelay[] relays = root.GetComponentsInChildren<MilitaryCinematicFootstepRelay>(true);
        for (int i = 0; i < relays.Length; i++) relays[i].Configure(footstepAudio);
        MilitaryCinematicVisionLight visionLight = root.AddComponent<MilitaryCinematicVisionLight>();
        visionLight.Configure(source.GetComponent<PlayerVision>());
        return root;
    }

    private static GameObject CloneVisualNode(Transform source, Transform cloneParent,
        PlayerMovement movement, bool isRoot)
    {
        GameObject clone = new GameObject(source.name);
        clone.transform.SetParent(cloneParent, false);
        if (!isRoot)
        {
            clone.transform.localPosition = source.localPosition;
            clone.transform.localRotation = source.localRotation;
            clone.transform.localScale = source.localScale;
        }

        SpriteRenderer originalRenderer = source.GetComponent<SpriteRenderer>();
        if (originalRenderer != null)
        {
            SpriteRenderer copy = clone.AddComponent<SpriteRenderer>();
            copy.sprite = originalRenderer.sprite;
            copy.color = originalRenderer.color;
            copy.flipX = originalRenderer.flipX;
            copy.flipY = originalRenderer.flipY;
            copy.drawMode = originalRenderer.drawMode;
            copy.size = originalRenderer.size;
            copy.maskInteraction = originalRenderer.maskInteraction;
            copy.sortingLayerID = originalRenderer.sortingLayerID;
            copy.sortingOrder = Mathf.Max(originalRenderer.sortingOrder, 200);
            copy.sharedMaterial = originalRenderer.sharedMaterial;
            copy.enabled = originalRenderer.enabled;
        }

        Animator originalAnimator = source.GetComponent<Animator>();
        if (originalAnimator != null && originalAnimator.runtimeAnimatorController != null)
        {
            Animator animator = clone.AddComponent<Animator>();
            animator.runtimeAnimatorController = originalAnimator.runtimeAnimatorController;
            animator.avatar = originalAnimator.avatar;
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.speed = 1f;
            clone.AddComponent<MilitaryCinematicFootstepRelay>();
        }

        for (int i = 0; i < source.childCount; i++)
            CloneVisualNode(source.GetChild(i), clone.transform, movement, false);
        clone.SetActive(source.gameObject.activeSelf);
        return clone;
    }

    private void HideAllPlayers()
    {
        hiddenRenderers.Clear();
        hiddenRendererSet.Clear();
        suppressedPlayers.Clear();
        hiddenCanvases.Clear();
        hiddenPlayerLights.Clear();
        suppressedPlayerVisions.Clear();
        hiddenNameTags.Clear();
        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] == null) continue;
            players[i].SetCinematicPresentationSuppressed(true);
            suppressedPlayers.Add(players[i]);
            PlayerVision vision = players[i].GetComponent<PlayerVision>();
            if (vision != null)
            {
                suppressedPlayerVisions.Add((vision, vision.enabled));
                vision.enabled = false;
            }
            PlayerNameTag playerNameTag = players[i].GetComponent<PlayerNameTag>();
            if (playerNameTag != null && playerNameTag.nameText != null)
            {
                GameObject nameTagObject = playerNameTag.nameText.gameObject;
                hiddenNameTags.Add((nameTagObject, nameTagObject.activeSelf));
                nameTagObject.SetActive(false);
            }
            Renderer[] renderers = players[i].GetComponentsInChildren<Renderer>(true);
            for (int j = 0; j < renderers.Length; j++)
            {
                Renderer renderer = renderers[j];
                if (renderer == null || !hiddenRendererSet.Add(renderer)) continue;
                hiddenRenderers.Add((renderer, renderer.enabled, renderer.forceRenderingOff));
                renderer.forceRenderingOff = true;
                renderer.enabled = false;
            }
            Canvas[] canvases = players[i].GetComponentsInChildren<Canvas>(true);
            for (int j = 0; j < canvases.Length; j++)
            {
                if (canvases[j] == null) continue;
                hiddenCanvases.Add((canvases[j], canvases[j].enabled));
                canvases[j].enabled = false;
            }
            Light2D[] lights = players[i].GetComponentsInChildren<Light2D>(true);
            for (int j = 0; j < lights.Length; j++)
            {
                if (lights[j] == null) continue;
                hiddenPlayerLights.Add((lights[j], lights[j].enabled));
                lights[j].enabled = false;
            }
        }
    }

    private void RestorePlayers()
    {
        for (int i = 0; i < hiddenRenderers.Count; i++)
            if (hiddenRenderers[i].renderer != null)
            {
                hiddenRenderers[i].renderer.forceRenderingOff = hiddenRenderers[i].forceRenderingOff;
                hiddenRenderers[i].renderer.enabled = hiddenRenderers[i].enabled;
            }
        hiddenRenderers.Clear();
        hiddenRendererSet.Clear();
        for (int i = 0; i < suppressedPlayers.Count; i++)
            if (suppressedPlayers[i] != null)
                suppressedPlayers[i].SetCinematicPresentationSuppressed(false);
        suppressedPlayers.Clear();
        for (int i = 0; i < hiddenCanvases.Count; i++)
            if (hiddenCanvases[i].canvas != null)
                hiddenCanvases[i].canvas.enabled = hiddenCanvases[i].enabled;
        hiddenCanvases.Clear();
        for (int i = 0; i < hiddenPlayerLights.Count; i++)
            if (hiddenPlayerLights[i].light != null)
                hiddenPlayerLights[i].light.enabled = hiddenPlayerLights[i].enabled;
        hiddenPlayerLights.Clear();
        for (int i = 0; i < suppressedPlayerVisions.Count; i++)
            if (suppressedPlayerVisions[i].vision != null)
                suppressedPlayerVisions[i].vision.enabled = suppressedPlayerVisions[i].enabled;
        suppressedPlayerVisions.Clear();
        for (int i = 0; i < hiddenNameTags.Count; i++)
            if (hiddenNameTags[i].nameTag != null)
                hiddenNameTags[i].nameTag.SetActive(hiddenNameTags[i].active);
        hiddenNameTags.Clear();
    }

    private void LateUpdate()
    {
        if (routine == null) return;
        for (int i = 0; i < hiddenRenderers.Count; i++)
            if (hiddenRenderers[i].renderer != null)
            {
                hiddenRenderers[i].renderer.forceRenderingOff = true;
                hiddenRenderers[i].renderer.enabled = false;
            }
        for (int i = 0; i < hiddenCanvases.Count; i++)
            if (hiddenCanvases[i].canvas != null) hiddenCanvases[i].canvas.enabled = false;
        for (int i = 0; i < hiddenPlayerLights.Count; i++)
            if (hiddenPlayerLights[i].light != null) hiddenPlayerLights[i].light.enabled = false;
        for (int i = 0; i < hiddenNameTags.Count; i++)
            if (hiddenNameTags[i].nameTag != null) hiddenNameTags[i].nameTag.SetActive(false);
    }

    private static void FocusCamera(Transform target)
    {
        if (target != null) PZ_CameraController.Instance?.SetTarget(target);
    }

    private static void RestoreLocalCamera()
    {
        if (PlayerMovement.LocalPlayerInstance != null)
            PZ_CameraController.Instance?.SetTarget(PlayerMovement.LocalPlayerInstance.transform);
    }

    private IEnumerator MoveCloneAtGameplaySpeed(Vector2 destination, float movementSpeed, bool running)
    {
        if (hostClone == null) yield break;
        Vector3 to = new Vector3(destination.x, destination.y, hostClone.transform.position.z);
        float safeSpeed = Mathf.Max(0.1f, movementSpeed);
        while ((hostClone.transform.position - to).sqrMagnitude > 0.0004f)
        {
            Vector2 direction = ((Vector2)(to - hostClone.transform.position)).normalized;
            UpdateCloneLocomotion(direction, running);
            hostClone.transform.position = Vector3.MoveTowards(hostClone.transform.position, to,
                safeSpeed * Time.unscaledDeltaTime);
            yield return null;
        }
        hostClone.transform.position = to;
        UpdateCloneLocomotion(Vector2.zero, false);
    }

    private void UpdateCloneLocomotion(Vector2 direction, bool running)
    {
        bool moving = direction.sqrMagnitude > 0.001f;
        Vector2 facing = moving ? SnapTo8Way(direction) : Vector2.down;
        FogVisionController.Instance?.UpdateMilitaryCinematicVisionDirection(facing);
        cinematicVisionLight?.SetDirection(facing);
        for (int i = 0; i < hostCloneAnimators.Length; i++)
        {
            Animator animator = hostCloneAnimators[i];
            if (animator == null) continue;
            SetBoolIfPresent(animator, "IsMoving", moving);
            SetBoolIfPresent(animator, "IsRunning", moving && running);
            SetBoolIfPresent(animator, "IsAiming", false);
            SetBoolIfPresent(animator, "IsCrouching", false);
            SetBoolIfPresent(animator, "IsExhausted", false);
            SetFloatIfPresent(animator, "MoveX", facing.x);
            SetFloatIfPresent(animator, "MoveY", facing.y);
            SetFloatIfPresent(animator, "StrafeX", 0f);
            SetFloatIfPresent(animator, "StrafeY", 0f);
        }
    }

    private static Vector2 SnapTo8Way(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.001f) return Vector2.down;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        int octant = Mathf.RoundToInt(angle / 45f);
        float snapped = octant * 45f * Mathf.Deg2Rad;
        Vector2 result = new Vector2(Mathf.Cos(snapped), Mathf.Sin(snapped));
        if (Mathf.Abs(result.x) < 0.01f) result.x = 0f;
        if (Mathf.Abs(result.y) < 0.01f) result.y = 0f;
        return result;
    }

    private static void SetBoolIfPresent(Animator animator, string name, bool value)
    {
        int hash = Animator.StringToHash(name);
        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
            if (parameters[i].nameHash == hash && parameters[i].type == AnimatorControllerParameterType.Bool)
            {
                animator.SetBool(hash, value);
                return;
            }
    }

    private static void SetFloatIfPresent(Animator animator, string name, float value)
    {
        int hash = Animator.StringToHash(name);
        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
            if (parameters[i].nameHash == hash && parameters[i].type == AnimatorControllerParameterType.Float)
            {
                animator.SetFloat(hash, value);
                return;
            }
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            fadeAlpha = Mathf.LerpUnclamped(from, to, Smooth(elapsed / Mathf.Max(0.001f, duration)));
            yield return null;
        }
        fadeAlpha = to;
    }

    private static float Smooth(float value)
    {
        float t = Mathf.Clamp01(value);
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    private void OnGUI()
    {
        if (routine == null && fadeAlpha <= 0.001f && letterboxAlpha <= 0.001f) return;
        int oldDepth = GUI.depth;
        Color oldColor = GUI.color;
        GUI.depth = -5000;

        if (letterboxAlpha > 0.001f)
        {
            float bar = Mathf.Max(52f, Screen.height * 0.085f);
            GUI.color = new Color(0f, 0f, 0f, letterboxAlpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, bar), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0f, Screen.height - bar, Screen.width, bar), Texture2D.whiteTexture);
        }

        if (!string.IsNullOrEmpty(subtitle))
        {
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(Screen.width * 0.15f, Screen.height - 118f, Screen.width * 0.7f, 64f), subtitle, style);
        }

        if (fadeAlpha > 0.001f)
        {
            GUI.color = new Color(0f, 0f, 0f, fadeAlpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        }
        GUI.color = oldColor;
        GUI.depth = oldDepth;
    }

    private void OnDestroy() => StopImmediate();
}

[DisallowMultipleComponent]
public sealed class MilitaryCinematicFootstepRelay : MonoBehaviour
{
    private MilitaryCinematicFootstepAudio output;

    public void Configure(MilitaryCinematicFootstepAudio target) => output = target;

    public void OnFootstep() => output?.PlayAuto();
    public void OnWalkFootstep() => output?.PlayWalk();
    public void OnRunFootstep() => output?.PlayRun();
}

[DisallowMultipleComponent]
public sealed class MilitaryCinematicFootstepAudio : MonoBehaviour
{
    private AudioSource source;
    private AudioClip walkClip;
    private AudioClip runClip;
    private float lastBeatAt = float.NegativeInfinity;

    public void Configure(PlayerMovement movement)
    {
        if (movement == null) return;
        walkClip = movement.walkSFX;
        runClip = movement.runSFX;
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        GameplayAudioSpatializer.Configure(source, GameplayAudioSpatializer.Profile.Footstep);
    }

    public void PlayAuto() => Play(runClip != null ? runClip : walkClip, 0.9f);
    public void PlayWalk() => Play(walkClip, 0.75f);
    public void PlayRun() => Play(runClip, 0.95f);

    private void Play(AudioClip clip, float volume)
    {
        if (source == null || clip == null || Time.unscaledTime - lastBeatAt < 0.18f) return;
        lastBeatAt = Time.unscaledTime;
        source.Stop();
        source.clip = clip;
        source.volume = volume * PlayerPrefs.GetFloat("GameSFXVolume", 0.8f);
        source.pitch = 1f;
        source.Play();
    }
}

[DisallowMultipleComponent]
public sealed class MilitaryCinematicVisionLight : MonoBehaviour
{
    private Light2D light2D;

    public void Configure(PlayerVision sourceVision)
    {
        Light2D source = sourceVision != null ? sourceVision.playerLight : null;
        if (source == null) return;

        GameObject lightObject = new GameObject("Military Cinematic Vision Light");
        lightObject.transform.SetParent(transform, false);
        light2D = lightObject.AddComponent<Light2D>();
        light2D.lightType = source.lightType;
        light2D.intensity = source.intensity;
        light2D.color = source.color;
        light2D.falloffIntensity = source.falloffIntensity;
        light2D.pointLightInnerRadius = source.pointLightInnerRadius;
        light2D.pointLightOuterRadius = source.pointLightOuterRadius;
        light2D.pointLightInnerAngle = source.pointLightInnerAngle;
        light2D.pointLightOuterAngle = source.pointLightOuterAngle;
        light2D.targetSortingLayers = source.targetSortingLayers;
        light2D.shadowsEnabled = source.shadowsEnabled;
        light2D.shadowSoftness = source.shadowSoftness;
        light2D.enabled = source.enabled;
        SetDirection(sourceVision.VisionWorldDirection);
    }

    public void SetDirection(Vector2 direction)
    {
        if (light2D == null || direction.sqrMagnitude < 0.001f) return;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        light2D.transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
    }
}
