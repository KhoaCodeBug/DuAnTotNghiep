using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class ReadinessAndChatEditorTests
{
    private const float MinimumMultiplayerConnectionTimeoutSeconds = 120f;

    [Test]
    public void MainScene_InvisibleObstacleRegressionMarker_IsNoLongerCoveredByLegacyFenceCollider()
    {
        string scenePath = Path.Combine(Application.dataPath, "Scenes/Main.unity");
        Assert.That(File.Exists(scenePath), Is.True);

        string sceneYaml = File.ReadAllText(scenePath);
        Assert.That(sceneYaml, Does.Contain("m_Name: Collider_A*TangHinh"),
            "Keep the authored marker as the regression location for the invisible blocker.");
        Assert.That(sceneYaml, Does.Not.Contain("m_Name: HangRao (3)"),
            "HangRao (3) was a solid PolygonCollider2D with no renderer at the marker position.");
    }

    [TestCase("Assets/Prefab/Player.prefab")]
    [TestCase("Assets/Prefab/Player2.prefab")]
    public void PlayerVisionPrefabs_UseConsistentAwarenessFadeAndLocalXRay(string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Assert.That(prefab, Is.Not.Null, prefabPath);

        MonoBehaviour vision = prefab.GetComponents<MonoBehaviour>()
            .FirstOrDefault(component => component != null && component.GetType().Name == "PlayerVision");
        Assert.That(vision, Is.Not.Null, $"{prefabPath} must contain PlayerVision on its root.");

        SerializedObject serializedVision = new SerializedObject(vision);
        Assert.That(serializedVision.FindProperty("passiveVisionRadius").floatValue,
            Is.EqualTo(1.5f).Within(0.001f));
        Assert.That(serializedVision.FindProperty("zombieVisibilityFadeDuration").floatValue,
            Is.GreaterThan(0f));
        Assert.That(serializedVision.FindProperty("zombieAwarenessInitialAlpha").floatValue,
            Is.InRange(0.05f, 0.5f));
        Assert.That(serializedVision.FindProperty("localPlayerXRayAlpha").floatValue,
            Is.InRange(0.1f, 0.6f));
    }

    [Test]
    public void IndoorFog_UsesBuildingScopedPhysicsOcclusion()
    {
        string controllerPath = Path.Combine(Application.dataPath, "Khoa/Code/FogVisionController.cs");
        string shaderPath = Path.Combine(Application.dataPath, "Shader/FogVisionOverlay.shader");
        string controllerSource = File.ReadAllText(controllerPath);
        string shaderSource = File.ReadAllText(shaderPath);

        Assert.That(controllerSource, Does.Contain("ResolveIndoorStructureRoot"));
        Assert.That(controllerSource, Does.Contain("IsIndoorStructuralHit"));
        Assert.That(controllerSource, Does.Contain("indoorCollider.OverlapPoint(pointBeforeWall)"),
            "Large-building walls may live outside the roof trigger hierarchy and must be linked by indoor geometry.");
        Assert.That(shaderSource, Does.Contain("_IndoorOcclusionDistances[180]"));
        Assert.That(shaderSource, Does.Contain("visibleIndoor = insideIndoor * indoorOcclusionVisibility"),
            "Rooms behind an internal wall must retain the dark indoor cover.");
    }

    [Test]
    public void ZombieMovement_UsesObstacleSweep_AndEnemyStillCollidesWithObstacleLayer()
    {
        Assert.That(Physics2D.GetIgnoreLayerCollision(LayerMask.NameToLayer("Enemy"),
            LayerMask.NameToLayer("Obstacle")), Is.False);

        foreach (string typeName in new[] { "ZOmbieAI_Khoa", "ZombieAIKhoaRebuilt" })
        {
            Type zombieType = ResolveGameType(typeName);
            MethodInfo sweep = zombieType.GetMethod("MoveWithObstacleSweep",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(sweep, Is.Not.Null, $"{typeName} must sweep its body before MovePosition.");
            Assert.That(sweep.ReturnType, Is.EqualTo(typeof(float)));
        }

        foreach (string prefabPath in new[]
                 {
                     "Assets/Khoa/Zombie2Khoa.prefab",
                     "Assets/Khoa/ZombieKhoaRebuilt.prefab",
                     "Assets/Khoa/ZombieBossTest.prefab"
                 })
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, prefabPath);
            Assert.That(prefab.layer, Is.EqualTo(LayerMask.NameToLayer("Enemy")), prefabPath);
            Rigidbody2D body = prefab.GetComponent<Rigidbody2D>();
            Assert.That(body, Is.Not.Null, prefabPath);
            Assert.That(body.collisionDetectionMode, Is.EqualTo(CollisionDetectionMode2D.Continuous), prefabPath);
        }
    }

    [Test]
    public void StartingLoadout_OnlyCompletesAfterVerifiedPlacement_AndCanRetry()
    {
        Type inventoryType = ResolveGameType("InventorySystem");
        PropertyInfo resolved = inventoryType.GetProperty("StartingLoadoutResolved",
            BindingFlags.NonPublic | BindingFlags.Instance);
        MethodInfo grant = inventoryType.GetMethod("TryGrantDifficultyStartingLoadout",
            BindingFlags.NonPublic | BindingFlags.Instance);
        MethodInfo placeWeapon = inventoryType.GetMethod("PlaceStartingWeaponInHotbar",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.That(resolved, Is.Not.Null);
        Assert.That(grant, Is.Not.Null);
        Assert.That(grant.ReturnType, Is.EqualTo(typeof(bool)));
        Assert.That(placeWeapon, Is.Not.Null);
        Assert.That(placeWeapon.ReturnType, Is.EqualTo(typeof(bool)),
            "A failed hotbar placement must not be marked as applied.");
    }

    [Test]
    public void FirearmBalance_AkAndS12KMeetCloseRangeDamageAndAccuracyFloor()
    {
        AssertWeaponBalance("AK47", minimumDamage: 42f, maximumSpread: 1f, minimumPellets: 1);
        AssertWeaponBalance("S12K", minimumDamage: 24f, maximumSpread: 8f, minimumPellets: 9);
    }

    private static void AssertWeaponBalance(string weaponId, float minimumDamage,
        float maximumSpread, int minimumPellets)
    {
        string assetPath = Path.Combine(Application.dataPath, $"Resources/Items/{weaponId}.asset");
        Assert.That(File.Exists(assetPath), Is.True, assetPath);
        string yaml = File.ReadAllText(assetPath);

        float damage = ParseYamlFloat(yaml, "weaponDamage");
        float spread = ParseYamlFloat(yaml, "spreadAngle");
        int pellets = Mathf.RoundToInt(ParseYamlFloat(yaml, "pelletCount"));
        Assert.That(damage, Is.GreaterThanOrEqualTo(minimumDamage), $"{weaponId} damage");
        Assert.That(spread, Is.LessThanOrEqualTo(maximumSpread), $"{weaponId} spread");
        Assert.That(pellets, Is.GreaterThanOrEqualTo(minimumPellets), $"{weaponId} pellet count");
    }

    private static float ParseYamlFloat(string yaml, string field)
    {
        Match match = Regex.Match(yaml, $"^  {Regex.Escape(field)}: (?<value>-?[0-9]+(?:\\.[0-9]+)?)\\r?$",
            RegexOptions.Multiline);
        Assert.That(match.Success, Is.True, $"Missing YAML field '{field}'.");
        return float.Parse(match.Groups["value"].Value,
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private static Type ResolveGameType(string name)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name, false))
            .FirstOrDefault(candidate => candidate != null);
        Assert.That(type, Is.Not.Null, $"Could not resolve runtime type '{name}'.");
        return type;
    }

    private static EventSystem ResolveTestEventSystem()
    {
        EventSystem eventSystem = EventSystem.current ??
            Resources.FindObjectsOfTypeAll<EventSystem>()
                .FirstOrDefault(system => system != null && system.gameObject.scene.IsValid());
        Assert.That(eventSystem, Is.Not.Null, "Chat dragging requires an EventSystem.");
        return eventSystem;
    }

    private static void SetTestLanguage(int langIndex) // 0 = English, 1 = Vietnamese
    {
        Type locType = ResolveGameType("GameLocalization");
        Type langEnum = ResolveGameType("GameLocalization+Language");
        MethodInfo setMethod = locType.GetMethod("SetLanguage", BindingFlags.Public | BindingFlags.Static);
        object langVal = Enum.ToObject(langEnum, langIndex);
        setMethod.Invoke(null, new object[] { langVal, false });
    }

    private static string GetLocalization(string key)
    {
        Type locType = ResolveGameType("GameLocalization");
        MethodInfo getMethod = locType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(string), typeof(string) }, null);
        return (string)getMethod.Invoke(null, new object[] { key, null });
    }

    [Test]
    public void FusionConnectionTimeout_CoversConcurrentMainSceneLoading()
    {
        string configPath = Path.Combine(Application.dataPath,
            "Photon/Fusion/Resources/NetworkProjectConfig.fusion");
        Assert.That(File.Exists(configPath), Is.True,
            "Fusion NetworkProjectConfig must exist at the canonical Resources path.");

        string configJson = File.ReadAllText(configPath);
        Match timeoutMatch = Regex.Match(configJson,
            "\\\"ConnectionTimeout\\\"\\s*:\\s*(?<seconds>[0-9]+(?:\\.[0-9]+)?)");
        Assert.That(timeoutMatch.Success, Is.True,
            "Fusion NetworkProjectConfig must declare ConnectionTimeout.");
        Assert.That(float.TryParse(timeoutMatch.Groups["seconds"].Value,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out float timeoutSeconds), Is.True);
        Assert.That(timeoutSeconds, Is.GreaterThanOrEqualTo(MinimumMultiplayerConnectionTimeoutSeconds),
            "Host/client Editors can spend over 30 seconds loading Main concurrently; " +
            "a shorter timeout disconnects a healthy client before readiness completes.");
    }

    [Test]
    public void ChatAuthority_RichTextSanitization_PlayerAndSystemSeparated()
    {
        Type deathContextType = ResolveGameType("PlayerDeathContext");
        MethodInfo sanitizeMethod = deathContextType.GetMethod("SanitizeRichText", BindingFlags.Public | BindingFlags.Static);
        Assert.That(sanitizeMethod, Is.Not.Null);

        // 1. Kiểm tra loại bỏ mã màu và tag HTML/RichText độc hại
        string rawPlayerName = "<color=red><size=50><b>Hacker_Pro</b></size></color>";
        string rawMessage = "<color=#00ff00>Hello <i>World</i></color><script>alert(1)</script>";

        string cleanName = (string)sanitizeMethod.Invoke(null, new object[] { rawPlayerName });
        string cleanMsg = (string)sanitizeMethod.Invoke(null, new object[] { rawMessage });

        Assert.That(cleanName, Is.EqualTo("Hacker_Pro"));
        Assert.That(cleanMsg, Is.EqualTo("Hello Worldalert(1)"));

        // 2. Kiểm tra chuỗi rỗng / khoảng trắng
        Assert.That((string)sanitizeMethod.Invoke(null, new object[] { "   " }), Is.Empty);
        Assert.That((string)sanitizeMethod.Invoke(null, new object[] { null }), Is.Empty);
    }

    [Test]
    public void SystemMessage_UsesUnifiedGoldColor()
    {
        Type deathContextType = ResolveGameType("PlayerDeathContext");
        FieldInfo colorField = deathContextType.GetField("SystemColorHex", BindingFlags.Public | BindingFlags.Static);
        Assert.That(colorField, Is.Not.Null);
        Assert.That((string)colorField.GetValue(null), Is.EqualTo("#FFD54A"));
    }

    [Test]
    public void DeathAnnouncement_MapsAllCausesCorrectly()
    {
        SetTestLanguage(1); // Vietnamese
        Type deathContextType = ResolveGameType("PlayerDeathContext");
        Type deathCauseEnum = ResolveGameType("DeathCause");
        MethodInfo formatMethod = deathContextType.GetMethod("FormatDeathMessage", BindingFlags.Public | BindingFlags.Static);
        Assert.That(formatMethod, Is.Not.Null);

        object zombieCause = Enum.Parse(deathCauseEnum, "ZombieAttack");
        object bleedCause = Enum.Parse(deathCauseEnum, "Bleeding");
        object infectCause = Enum.Parse(deathCauseEnum, "Infection");
        object starveCause = Enum.Parse(deathCauseEnum, "Starvation");
        object thirstCause = Enum.Parse(deathCauseEnum, "Dehydration");
        object pvpCause = Enum.Parse(deathCauseEnum, "PvP");
        object unknownCause = Enum.Parse(deathCauseEnum, "Unknown");

        // Zombie Attack
        string zombieMsg = (string)formatMethod.Invoke(null, new object[] { "Minh", zombieCause, null });
        Assert.That(zombieMsg, Is.EqualTo("Minh đã chết vì bị zombie tấn công."));

        // Bleeding
        string bleedMsg = (string)formatMethod.Invoke(null, new object[] { "Minh", bleedCause, null });
        Assert.That(bleedMsg, Is.EqualTo("Minh đã chết vì mất máu."));

        // Infection
        string infectMsg = (string)formatMethod.Invoke(null, new object[] { "Minh", infectCause, null });
        Assert.That(infectMsg, Is.EqualTo("Minh đã chết vì nhiễm trùng."));

        // Starvation
        string starveMsg = (string)formatMethod.Invoke(null, new object[] { "Minh", starveCause, null });
        Assert.That(starveMsg, Is.EqualTo("Minh đã chết vì đói."));

        // Dehydration
        string thirstMsg = (string)formatMethod.Invoke(null, new object[] { "Minh", thirstCause, null });
        Assert.That(thirstMsg, Is.EqualTo("Minh đã chết vì khát."));

        // PvP with killer name
        string pvpNamed = (string)formatMethod.Invoke(null, new object[] { "Minh", pvpCause, "Khoa" });
        Assert.That(pvpNamed, Is.EqualTo("Minh đã bị Khoa hạ gục."));

        // PvP without killer name
        string pvpAnon = (string)formatMethod.Invoke(null, new object[] { "Minh", pvpCause, null });
        Assert.That(pvpAnon, Is.EqualTo("Minh đã bị người chơi khác hạ gục."));

        // Unknown / Fallback
        string unknownMsg = (string)formatMethod.Invoke(null, new object[] { "Minh", unknownCause, null });
        Assert.That(unknownMsg, Is.EqualTo("Minh đã tử vong."));

        // Fallback victim name when empty
        string fallbackVictim = (string)formatMethod.Invoke(null, new object[] { "   ", zombieCause, null });
        Assert.That(fallbackVictim, Is.EqualTo("Survivor đã chết vì bị zombie tấn công."));
    }

    [Test]
    public void JoinAnnouncement_FormatsCorrectly()
    {
        SetTestLanguage(1); // Vietnamese
        Type deathContextType = ResolveGameType("PlayerDeathContext");
        MethodInfo formatJoinMethod = deathContextType.GetMethod("FormatJoinMessage", BindingFlags.Public | BindingFlags.Static);
        Assert.That(formatJoinMethod, Is.Not.Null);

        string joinMsg = (string)formatJoinMethod.Invoke(null, new object[] { "Khoa" });
        Assert.That(joinMsg, Is.EqualTo("Khoa đã vào trận."));

        string fallbackJoin = (string)formatJoinMethod.Invoke(null, new object[] { "<b></b>" });
        Assert.That(fallbackJoin, Is.EqualTo("Survivor đã vào trận."));
    }

    [Test]
    public void Bilingual_DeathAndJoinAnnouncements_EnglishAndVietnamese()
    {
        Type deathContextType = ResolveGameType("PlayerDeathContext");
        Type deathCauseEnum = ResolveGameType("DeathCause");
        MethodInfo formatMethod = deathContextType.GetMethod("FormatDeathMessage", BindingFlags.Public | BindingFlags.Static);
        MethodInfo formatJoinMethod = deathContextType.GetMethod("FormatJoinMessage", BindingFlags.Public | BindingFlags.Static);
        object zombieCause = Enum.Parse(deathCauseEnum, "ZombieAttack");
        object pvpCause = Enum.Parse(deathCauseEnum, "PvP");

        // English verification
        SetTestLanguage(0); // English
        Assert.That((string)formatJoinMethod.Invoke(null, new object[] { "John" }), Is.EqualTo("John joined the match."));
        Assert.That((string)formatMethod.Invoke(null, new object[] { "John", zombieCause, null }), Is.EqualTo("John died to a zombie attack."));
        Assert.That((string)formatMethod.Invoke(null, new object[] { "John", pvpCause, "Alex" }), Is.EqualTo("John was killed by Alex."));

        // Vietnamese verification
        SetTestLanguage(1); // Vietnamese
        Assert.That((string)formatJoinMethod.Invoke(null, new object[] { "John" }), Is.EqualTo("John đã vào trận."));
        Assert.That((string)formatMethod.Invoke(null, new object[] { "John", zombieCause, null }), Is.EqualTo("John đã chết vì bị zombie tấn công."));
        Assert.That((string)formatMethod.Invoke(null, new object[] { "John", pvpCause, "Alex" }), Is.EqualTo("John đã bị Alex hạ gục."));
    }

    [Test]
    public void ReadinessStateMachine_ProgressMonotonic_AndStages()
    {
        Type coordType = ResolveGameType("GameplayReadinessCoordinator");
        Type stageEnum = ResolveGameType("GameplayReadinessCoordinator+ReadinessStage");

        MethodInfo resetMethod = coordType.GetMethod("ResetCoordinator", BindingFlags.Public | BindingFlags.Static);
        MethodInfo startMethod = coordType.GetMethod("StartLoading", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(string) }, null)
            ?? coordType.GetMethod("StartLoading", BindingFlags.Public | BindingFlags.Static);
        MethodInfo setStageMethod = coordType.GetMethod("SetStage", BindingFlags.Public | BindingFlags.Static, null, new Type[] { stageEnum, typeof(float), typeof(string) }, null);
        MethodInfo releaseMethod = coordType.GetMethod("Release", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);

        PropertyInfo stageProp = coordType.GetProperty("CurrentStage", BindingFlags.Public | BindingFlags.Static);
        PropertyInfo progressProp = coordType.GetProperty("CurrentProgress", BindingFlags.Public | BindingFlags.Static);
        PropertyInfo loadingProp = coordType.GetProperty("IsLoadingActive", BindingFlags.Public | BindingFlags.Static);
        PropertyInfo releasedProp = coordType.GetProperty("IsReleasedToGameplay", BindingFlags.Public | BindingFlags.Static);

        resetMethod.Invoke(null, null);
        Assert.That(stageProp.GetValue(null).ToString(), Is.EqualTo("None"));
        Assert.That((float)progressProp.GetValue(null), Is.EqualTo(0f));
        Assert.That((bool)loadingProp.GetValue(null), Is.False);

        if (startMethod.GetParameters().Length == 1)
            startMethod.Invoke(null, new object[] { "loading.connecting" });
        else
            startMethod.Invoke(null, null);

        Assert.That((bool)loadingProp.GetValue(null), Is.True);
        Assert.That(stageProp.GetValue(null).ToString(), Is.EqualTo("Connecting"));
        float p0 = (float)progressProp.GetValue(null);

        object sceneLoadingStage = Enum.Parse(stageEnum, "SceneLoading");
        setStageMethod.Invoke(null, new object[] { sceneLoadingStage, 0.5f, null });
        float p1 = (float)progressProp.GetValue(null);
        Assert.That(p1, Is.GreaterThan(p0));

        object fusionReadyStage = Enum.Parse(stageEnum, "FusionSceneReady");
        setStageMethod.Invoke(null, new object[] { fusionReadyStage, 0f, null });
        float p2 = (float)progressProp.GetValue(null);
        Assert.That(p2, Is.GreaterThanOrEqualTo(p1));

        object spawnWaitStage = Enum.Parse(stageEnum, "PlayerSpawnWaiting");
        setStageMethod.Invoke(null, new object[] { spawnWaitStage, 0f, null });
        float p3 = (float)progressProp.GetValue(null);
        Assert.That(p3, Is.GreaterThanOrEqualTo(p2));

        object bindingStage = Enum.Parse(stageEnum, "LocalAvatarBinding");
        setStageMethod.Invoke(null, new object[] { bindingStage, 0f, null });
        float p4 = (float)progressProp.GetValue(null);
        Assert.That(p4, Is.GreaterThanOrEqualTo(p3));

        object hudStage = Enum.Parse(stageEnum, "HUDAndSystemsReady");
        setStageMethod.Invoke(null, new object[] { hudStage, 0f, null });
        float p5 = (float)progressProp.GetValue(null);
        Assert.That(p5, Is.GreaterThanOrEqualTo(p4));

        object awaitReleaseStage = Enum.Parse(stageEnum, "AwaitingHostRelease");
        setStageMethod.Invoke(null, new object[] { awaitReleaseStage, 0f, null });
        float p6 = (float)progressProp.GetValue(null);
        Assert.That(p6, Is.GreaterThanOrEqualTo(p5));
        Assert.That(p6, Is.LessThan(1.0f)); // Chưa release thì không được 100%

        // Thử lùi stage -> Máy trạng thái phải chặn không cho tụt tiến độ
        object connectingStage = Enum.Parse(stageEnum, "Connecting");
        setStageMethod.Invoke(null, new object[] { connectingStage, 0f, null });
        Assert.That(stageProp.GetValue(null).ToString(), Is.EqualTo("AwaitingHostRelease"));

        // Release to gameplay
        releaseMethod.Invoke(null, null);
        Assert.That(stageProp.GetValue(null).ToString(), Is.EqualTo("ReleasedToGameplay"));
        Assert.That((float)progressProp.GetValue(null), Is.EqualTo(1.0f));
        Assert.That((bool)releasedProp.GetValue(null), Is.True);
        Assert.That((bool)loadingProp.GetValue(null), Is.False);
    }

    [Test]
    public void GameplayHudLayout_PromptRectNeverOverlapsHotbar_AcrossAllResolutions()
    {
        Type hudLayoutType = ResolveGameType("GameplayHudLayout");
        FieldInfo hotbarFootprintField = hudLayoutType.GetField("CanonicalHotbarFootprint1080p", BindingFlags.Public | BindingFlags.Static);
        FieldInfo safeMarginField = hudLayoutType.GetField("SafeMargin1080p", BindingFlags.Public | BindingFlags.Static);
        Assert.That(hotbarFootprintField, Is.Not.Null);
        Assert.That(safeMarginField, Is.Not.Null);

        float canonicalHotbarFootprint = (float)hotbarFootprintField.GetValue(null);
        float canonicalSafeMargin = (float)safeMarginField.GetValue(null);

        Vector2Int[] testResolutions = new Vector2Int[]
        {
            new Vector2Int(1280, 720),   // 720p
            new Vector2Int(1366, 768),   // 768p
            new Vector2Int(1600, 900),   // 900p
            new Vector2Int(1920, 1080),  // 1080p
            new Vector2Int(2560, 1440),  // 1440p 2K
            new Vector2Int(3840, 2160)   // 2160p 4K
        };

        foreach (var res in testResolutions)
        {
            float width = res.x;
            float height = res.y;

            // Tính scale giả lập theo thuật toán của GameplayHudLayout
            float logWidth = Mathf.Log(width / 1920f, 2f);
            float logHeight = Mathf.Log(height / 1080f, 2f);
            float logWeighted = Mathf.Lerp(logWidth, logHeight, 0.5f);
            float scale = Mathf.Clamp(Mathf.Pow(2f, logWeighted), 0.5f, 2.5f);

            float hotbarHeightPixels = canonicalHotbarFootprint * scale;
            float promptHeight = 42f * scale;
            float margin = canonicalSafeMargin * scale;

            float promptYMax = height - (hotbarHeightPixels + margin);
            float promptYMin = promptYMax - promptHeight;

            // Đáy của prompt box phải nằm phía trên đỉnh của hotbar ít nhất `margin` pixel
            float gapAboveHotbar = (height - hotbarHeightPixels) - promptYMax;
            Assert.That(gapAboveHotbar, Is.GreaterThanOrEqualTo(margin - 0.01f),
                $"Thất bại tại độ phân giải {res.x}x{res.y}: Prompt box đè lấn vùng hotbar!");

            // Prompt box phải nằm hoàn toàn bên trong màn hình
            Assert.That(promptYMin, Is.GreaterThan(0f),
                $"Thất bại tại độ phân giải {res.x}x{res.y}: Prompt box bị đẩy ra ngoài đỉnh màn hình!");
        }
    }

    [Test]
    public void HostModeSpawner_AuthValidation_AndReadyPlayerSet()
    {
        Type spawnerType = ResolveGameType("HostModeSpawner");
        MethodInfo authenticateMethod = spawnerType.GetMethod("TryResolveAuthenticatedPlayer", BindingFlags.Public | BindingFlags.Static);
        MethodInfo registerMethod = spawnerType.GetMethod("RegisterReadyPlayer", BindingFlags.Public | BindingFlags.Static);
        Assert.That(authenticateMethod, Is.Not.Null);
        Assert.That(registerMethod, Is.Not.Null);

        Type playerRefType = ResolveGameType("Fusion.PlayerRef");
        MethodInfo fromIndexMethod = playerRefType.GetMethod("FromIndex", BindingFlags.Public | BindingFlags.Static);
        PropertyInfo noneProp = playerRefType.GetProperty("None", BindingFlags.Public | BindingFlags.Static);
        object nonePlayer = noneProp != null ? noneProp.GetValue(null) : Activator.CreateInstance(playerRefType);

        object realPlayer = fromIndexMethod.Invoke(null, new object[] { 1 });
        object spoofClaim = fromIndexMethod.Invoke(null, new object[] { 2 });

        // 1. Xác thực RpcSource chống spoof PlayerRef
        object[] authArgsValid = new object[] { realPlayer, realPlayer, null };
        Assert.That((bool)authenticateMethod.Invoke(null, authArgsValid), Is.True);
        Assert.That(authArgsValid[2], Is.EqualTo(realPlayer));

        object[] authArgsSpoofed = new object[] { spoofClaim, realPlayer, null };
        Assert.That((bool)authenticateMethod.Invoke(null, authArgsSpoofed), Is.False);

        // 2. Quản lý ready player set: Mỗi player chỉ add đúng 1 lần
        object readySet = Activator.CreateInstance(typeof(HashSet<>).MakeGenericType(playerRefType));
        object[] regArgs1 = new object[] { readySet, realPlayer };
        Assert.That((bool)registerMethod.Invoke(null, regArgs1), Is.True);

        object[] regArgsDuplicate = new object[] { readySet, realPlayer };
        Assert.That((bool)registerMethod.Invoke(null, regArgsDuplicate), Is.False); // Add trùng trả về false

        object[] regArgsNone = new object[] { readySet, nonePlayer };
        Assert.That((bool)registerMethod.Invoke(null, regArgsNone), Is.False); // Player None bị từ chối
    }

    [Test]
    public void DifficultyRules_ContractMultipliers_AndLoadouts()
    {
        Type diffRulesType = ResolveGameType("DifficultyRules");
        MethodInfo getDensityMethod = diffRulesType.GetMethod("GetZombieDensityMultiplier", BindingFlags.Public | BindingFlags.Static);
        MethodInfo getLootMethod = diffRulesType.GetMethod("GetLootRateMultiplier", BindingFlags.Public | BindingFlags.Static);
        MethodInfo getDamageMethod = diffRulesType.GetMethod("GetIncomingDamageMultiplier", BindingFlags.Public | BindingFlags.Static);
        MethodInfo getLoadoutMethod = diffRulesType.GetMethod("GetStarterGearLoadout", BindingFlags.Public | BindingFlags.Static);
        MethodInfo setSessionDiffMethod = diffRulesType.GetMethod("SetSessionDifficulty", BindingFlags.Public | BindingFlags.Static);
        MethodInfo resetSessionDiffMethod = diffRulesType.GetMethod("ResetSessionDifficulty", BindingFlags.Public | BindingFlags.Static);
        PropertyInfo activeDiffProp = diffRulesType.GetProperty("ActiveDifficulty", BindingFlags.Public | BindingFlags.Static);

        // 1. Easy Mode (0)
        Assert.That((float)getDensityMethod.Invoke(null, new object[] { 0 }), Is.EqualTo(0.5f));
        Assert.That((float)getLootMethod.Invoke(null, new object[] { 0 }), Is.EqualTo(1.5f));
        Assert.That((float)getDamageMethod.Invoke(null, new object[] { 0 }), Is.EqualTo(0.7f));
        Array easyLoadout = (Array)getLoadoutMethod.Invoke(null, new object[] { 0 });
        Assert.That(easyLoadout.Length, Is.EqualTo(5));

        // 2. Normal Mode (1)
        Assert.That((float)getDensityMethod.Invoke(null, new object[] { 1 }), Is.EqualTo(1.0f));
        Assert.That((float)getLootMethod.Invoke(null, new object[] { 1 }), Is.EqualTo(1.0f));
        Assert.That((float)getDamageMethod.Invoke(null, new object[] { 1 }), Is.EqualTo(1.0f));
        Array normalLoadout = (Array)getLoadoutMethod.Invoke(null, new object[] { 1 });
        Assert.That(normalLoadout.Length, Is.EqualTo(2));

        // 3. Hard Mode (2)
        Assert.That((float)getDensityMethod.Invoke(null, new object[] { 2 }), Is.EqualTo(2.5f));
        Assert.That((float)getLootMethod.Invoke(null, new object[] { 2 }), Is.EqualTo(0.4f));
        Assert.That((float)getDamageMethod.Invoke(null, new object[] { 2 }), Is.EqualTo(1.5f));
        Array hardLoadout = (Array)getLoadoutMethod.Invoke(null, new object[] { 2 });
        Assert.That(hardLoadout.Length, Is.Zero);

        // 4. Session Difficulty Canonical Override
        resetSessionDiffMethod.Invoke(null, null);
        PlayerPrefs.SetInt("GameDifficulty", 0);
        Assert.That((int)activeDiffProp.GetValue(null), Is.EqualTo(0));

        // Host overrides session difficulty to Hard (2)
        setSessionDiffMethod.Invoke(null, new object[] { 2 });
        Assert.That((int)activeDiffProp.GetValue(null), Is.EqualTo(2)); // PlayerPrefs ignored when session override is active

        resetSessionDiffMethod.Invoke(null, null);
    }

    [Test]
    public void HostHard_ClientEasy_And_HostEasy_ClientHard_AuthoritySync()
    {
        Type diffRulesType = ResolveGameType("DifficultyRules");
        MethodInfo getDensityMethod = diffRulesType.GetMethod("GetZombieDensityMultiplier", BindingFlags.Public | BindingFlags.Static);
        MethodInfo getLootMethod = diffRulesType.GetMethod("GetLootRateMultiplier", BindingFlags.Public | BindingFlags.Static);
        MethodInfo getDamageMethod = diffRulesType.GetMethod("GetIncomingDamageMultiplier", BindingFlags.Public | BindingFlags.Static);
        MethodInfo getLoadoutMethod = diffRulesType.GetMethod("GetStarterGearLoadout", BindingFlags.Public | BindingFlags.Static);
        MethodInfo setSessionDiffMethod = diffRulesType.GetMethod("SetSessionDifficulty", BindingFlags.Public | BindingFlags.Static);
        MethodInfo resetSessionDiffMethod = diffRulesType.GetMethod("ResetSessionDifficulty", BindingFlags.Public | BindingFlags.Static);
        PropertyInfo activeDiffProp = diffRulesType.GetProperty("ActiveDifficulty", BindingFlags.Public | BindingFlags.Static);
        PropertyInfo hasOverrideProp = diffRulesType.GetProperty("HasSessionOverride", BindingFlags.Public | BindingFlags.Static);

        try
        {
            // Scenario 1: Client has local PlayerPrefs = Easy (0), but Host broadcasts Hard (2)
            resetSessionDiffMethod.Invoke(null, null);
            PlayerPrefs.SetInt("GameDifficulty", 0);
            PlayerPrefs.Save();

            // Client joins room and receives Host's session difficulty = 2
            setSessionDiffMethod.Invoke(null, new object[] { 2 });

            Assert.That((bool)hasOverrideProp.GetValue(null), Is.True, "Session override must be active on Client.");
            int effectiveDiff = (int)activeDiffProp.GetValue(null);
            Assert.That(effectiveDiff, Is.EqualTo(2), "Client must adopt Host's Hard difficulty (2), ignoring local PlayerPrefs (0).");
            Assert.That((float)getDensityMethod.Invoke(null, new object[] { effectiveDiff }), Is.EqualTo(2.5f));
            Assert.That((float)getLootMethod.Invoke(null, new object[] { effectiveDiff }), Is.EqualTo(0.4f));
            Assert.That((float)getDamageMethod.Invoke(null, new object[] { effectiveDiff }), Is.EqualTo(1.5f));
            Array hardLoadout = (Array)getLoadoutMethod.Invoke(null, new object[] { effectiveDiff });
            Assert.That(hardLoadout.Length, Is.Zero, "Hard difficulty must not grant a flashlight.");

            // Scenario 2: Client has local PlayerPrefs = Hard (2), but Host broadcasts Easy (0)
            resetSessionDiffMethod.Invoke(null, null);
            PlayerPrefs.SetInt("GameDifficulty", 2);
            PlayerPrefs.Save();

            // Client joins room and receives Host's session difficulty = 0
            setSessionDiffMethod.Invoke(null, new object[] { 0 });

            Assert.That((bool)hasOverrideProp.GetValue(null), Is.True, "Session override must be active on Client.");
            effectiveDiff = (int)activeDiffProp.GetValue(null);
            Assert.That(effectiveDiff, Is.EqualTo(0), "Client must adopt Host's Easy difficulty (0), ignoring local PlayerPrefs (2).");
            Assert.That((float)getDensityMethod.Invoke(null, new object[] { effectiveDiff }), Is.EqualTo(0.5f));
            Assert.That((float)getLootMethod.Invoke(null, new object[] { effectiveDiff }), Is.EqualTo(1.5f));
            Assert.That((float)getDamageMethod.Invoke(null, new object[] { effectiveDiff }), Is.EqualTo(0.7f));
            Array easyLoadout = (Array)getLoadoutMethod.Invoke(null, new object[] { effectiveDiff });
            Assert.That(easyLoadout.Length, Is.EqualTo(5), "Easy difficulty must yield five starter entries.");
        }
        finally
        {
            resetSessionDiffMethod.Invoke(null, null);
        }
    }

    [Test]
    public void HostModeSpawner_TryExtractIntProperty_And_SessionDifficultyReadyGate()
    {
        Type spawnerType = ResolveGameType("HostModeSpawner");
        MethodInfo tryExtractMethod = spawnerType.GetMethod("TryExtractIntProperty", BindingFlags.Public | BindingFlags.Static);
        Assert.That(tryExtractMethod, Is.Not.Null);

        // 1. Int parsing
        object[] argsInt = new object[] { 2, 0 };
        bool successInt = (bool)tryExtractMethod.Invoke(null, argsInt);
        Assert.That(successInt, Is.True);
        Assert.That(argsInt[1], Is.EqualTo(2));

        // 2. Long parsing
        object[] argsLong = new object[] { 0L, 0 };
        bool successLong = (bool)tryExtractMethod.Invoke(null, argsLong);
        Assert.That(successLong, Is.True);
        Assert.That(argsLong[1], Is.EqualTo(0));

        // 3. String numeric parsing
        object[] argsStr = new object[] { "1", 0 };
        bool successStr = (bool)tryExtractMethod.Invoke(null, argsStr);
        Assert.That(successStr, Is.True);
        Assert.That(argsStr[1], Is.EqualTo(1));

        // 4. Null / Invalid parsing
        object[] argsNull = new object[] { null, 0 };
        bool successNull = (bool)tryExtractMethod.Invoke(null, argsNull);
        Assert.That(successNull, Is.False);

        object[] argsInvalid = new object[] { "invalid_text", 0 };
        bool successInvalid = (bool)tryExtractMethod.Invoke(null, argsInvalid);
        Assert.That(successInvalid, Is.False);

        // 5. Clamping out-of-range values
        object[] argsOverflow = new object[] { 99, 0 };
        bool successOverflow = (bool)tryExtractMethod.Invoke(null, argsOverflow);
        Assert.That(successOverflow, Is.True);
        Assert.That(argsOverflow[1], Is.EqualTo(2)); // Clamped to 2 (Hard)

        // 6. Verify IsSessionDifficultyAuthoritativeReady property exists and is public
        PropertyInfo readyProp = spawnerType.GetProperty("IsSessionDifficultyAuthoritativeReady", BindingFlags.Public | BindingFlags.Instance);
        Assert.That(readyProp, Is.Not.Null, "IsSessionDifficultyAuthoritativeReady property must be exposed on HostModeSpawner.");

        // 7. Verify Client IsSessionDifficultyAuthoritativeReady is NOT satisfied by local DifficultyRules override alone
        GameObject testGo = new GameObject("TestHostModeSpawner");
        try
        {
            Component spawnerComp = testGo.AddComponent(spawnerType);
            Type diffRulesType = ResolveGameType("DifficultyRules");
            MethodInfo setSessionDiffMethod = diffRulesType.GetMethod("SetSessionDifficulty", BindingFlags.Public | BindingFlags.Static);
            MethodInfo resetSessionDiffMethod = diffRulesType.GetMethod("ResetSessionDifficulty", BindingFlags.Public | BindingFlags.Static);
            MethodInfo setMetadataReadyMethod = spawnerType.GetMethod("SetSessionDifficultyMetadataReadyForTest", BindingFlags.NonPublic | BindingFlags.Instance);

            // Set local DifficultyRules session override
            setSessionDiffMethod.Invoke(null, new object[] { 2 });

            // On Client (Runner is null in EditMode), local override must NOT make IsSessionDifficultyAuthoritativeReady true
            bool isReady = (bool)readyProp.GetValue(spawnerComp);
            Assert.That(isReady, Is.False, "Local DifficultyRules override alone must NOT make Client IsSessionDifficultyAuthoritativeReady true.");

            // When authoritative metadata/network is marked ready, IsSessionDifficultyAuthoritativeReady becomes true
            setMetadataReadyMethod.Invoke(spawnerComp, new object[] { true });
            isReady = (bool)readyProp.GetValue(spawnerComp);
            Assert.That(isReady, Is.True, "Authoritative metadata ready MUST make Client IsSessionDifficultyAuthoritativeReady true.");

            resetSessionDiffMethod.Invoke(null, null);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(testGo);
        }
    }

    [Test]
    public void StarterGear_ItemAssetsExist_AndLoadSuccessfully()
    {
        Type diffRulesType = ResolveGameType("DifficultyRules");
        Type itemLoaderType = ResolveGameType("ItemDataLoader");
        MethodInfo getLoadoutMethod = diffRulesType.GetMethod("GetStarterGearLoadout", BindingFlags.Public | BindingFlags.Static);
        MethodInfo loadItemMethod = itemLoaderType.GetMethod("LoadItem", BindingFlags.Public | BindingFlags.Static);

        Assert.That(getLoadoutMethod, Is.Not.Null);
        Assert.That(loadItemMethod, Is.Not.Null);

        // Test Easy loadout fixed entries; the weapon entry is resolved by State Authority.
        Array easyLoadout = (Array)getLoadoutMethod.Invoke(null, new object[] { 0 });
        Assert.That(easyLoadout.Length, Is.EqualTo(5));
        for (int i = 0; i < easyLoadout.Length; i++)
        {
            object entry = easyLoadout.GetValue(i);
            FieldInfo itemIdField = entry.GetType().GetField("ItemId");
            FieldInfo preferHotbarField = entry.GetType().GetField("PreferHotbar");
            string itemId = (string)itemIdField.GetValue(entry);
            if (preferHotbarField != null && (bool)preferHotbarField.GetValue(entry))
            {
                Assert.That(itemId, Is.Not.Null.And.Not.Empty,
                    "The random starter weapon entry must have a non-empty resolver ID.");
                continue;
            }
            object itemAsset = loadItemMethod.Invoke(null, new object[] { itemId });
            Assert.That(itemAsset, Is.Not.Null, $"Starter item '{itemId}' must exist and load successfully in Resources/Items!");
        }

        // Test Normal loadout fixed entries; the weapon entry is resolved by State Authority.
        Array normalLoadout = (Array)getLoadoutMethod.Invoke(null, new object[] { 1 });
        Assert.That(normalLoadout.Length, Is.EqualTo(2));
        for (int i = 0; i < normalLoadout.Length; i++)
        {
            object entry = normalLoadout.GetValue(i);
            FieldInfo itemIdField = entry.GetType().GetField("ItemId");
            FieldInfo preferHotbarField = entry.GetType().GetField("PreferHotbar");
            string itemId = (string)itemIdField.GetValue(entry);
            if (preferHotbarField != null && (bool)preferHotbarField.GetValue(entry))
            {
                Assert.That(itemId, Is.Not.Null.And.Not.Empty,
                    "The random starter weapon entry must have a non-empty resolver ID.");
                continue;
            }
            object itemAsset = loadItemMethod.Invoke(null, new object[] { itemId });
            Assert.That(itemAsset, Is.Not.Null, $"Starter item '{itemId}' must exist and load successfully in Resources/Items!");
        }
    }

    [Test]
    public void LocalizationMatrix_AllLoadingStagesAndDifficultyDescriptions_Bilingual()
    {
        Type locType = ResolveGameType("GameLocalization");
        MethodInfo getMethod = locType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(string), typeof(string) }, null)
            ?? locType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);

        string[] requiredKeys = new string[]
        {
            "loading.connecting",
            "loading.scene_loading",
            "loading.fusion_ready",
            "loading.player_spawn_waiting",
            "loading.avatar_binding",
            "loading.hud_ready",
            "loading.awaiting_host",
            "loading.ready_complete",
            "difficulty.easy.title",
            "difficulty.easy.stats",
            "difficulty.easy.desc",
            "difficulty.normal.title",
            "difficulty.normal.stats",
            "difficulty.normal.desc",
            "difficulty.hard.title",
            "difficulty.hard.stats",
            "difficulty.hard.desc",
            "chat.system_prefix",
            "chat.player_joined",
            "chat.death.zombie",
            "chat.death.bleeding",
            "chat.death.infection",
            "chat.death.starvation",
            "chat.death.dehydration",
            "chat.death.pvp_killer",
            "chat.death.pvp_generic",
            "chat.death.unknown"
        };

        foreach (string key in requiredKeys)
        {
            SetTestLanguage(0); // English
            string enVal = (string)getMethod.Invoke(null, getMethod.GetParameters().Length == 2 ? new object[] { key, null } : new object[] { key });
            Assert.That(enVal, Is.Not.Null.And.Not.Empty, $"Key '{key}' is missing English translation!");
            Assert.That(enVal, Is.Not.EqualTo(key), $"Key '{key}' returned fallback key in English!");

            SetTestLanguage(1); // Vietnamese
            string viVal = (string)getMethod.Invoke(null, getMethod.GetParameters().Length == 2 ? new object[] { key, null } : new object[] { key });
            Assert.That(viVal, Is.Not.Null.And.Not.Empty, $"Key '{key}' is missing Vietnamese translation!");
            Assert.That(viVal, Is.Not.EqualTo(key), $"Key '{key}' returned fallback key in Vietnamese!");
        }
    }

    [Test]
    public void SuppressionGate_ControlsReadinessAndPrompts()
    {
        Type coordType = ResolveGameType("GameplayReadinessCoordinator");
        Type hudLayoutType = ResolveGameType("GameplayHudLayout");
        Type localUIStateType = ResolveGameType("LocalGameplayUIState");

        MethodInfo resetMethod = coordType.GetMethod("ResetCoordinator", BindingFlags.Public | BindingFlags.Static);
        MethodInfo startMethod = coordType.GetMethod("StartLoading", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(string) }, null)
            ?? coordType.GetMethod("StartLoading", BindingFlags.Public | BindingFlags.Static);
        MethodInfo releaseMethod = coordType.GetMethod("Release", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);

        PropertyInfo suppressedProp = coordType.GetProperty("IsGameplaySuppressed", BindingFlags.Public | BindingFlags.Static);
        PropertyInfo releasedProp = coordType.GetProperty("IsReleasedToGameplay", BindingFlags.Public | BindingFlags.Static);
        MethodInfo arePromptsSuppressedMethod = hudLayoutType.GetMethod("AreGameplayPromptsSuppressed", BindingFlags.Public | BindingFlags.Static);
        PropertyInfo blocksHintsProp = localUIStateType.GetProperty("BlocksWorldInteractionHints", BindingFlags.Public | BindingFlags.Static);

        resetMethod.Invoke(null, null);
        Assert.That((bool)suppressedProp.GetValue(null), Is.False);

        if (startMethod.GetParameters().Length == 1)
            startMethod.Invoke(null, new object[] { "loading.connecting" });
        else
            startMethod.Invoke(null, null);

        Assert.That((bool)suppressedProp.GetValue(null), Is.True);
        Assert.That((bool)arePromptsSuppressedMethod.Invoke(null, null), Is.True);
        Assert.That((bool)blocksHintsProp.GetValue(null), Is.True);

        releaseMethod.Invoke(null, null);
        Assert.That((bool)suppressedProp.GetValue(null), Is.False);
        Assert.That((bool)releasedProp.GetValue(null), Is.True);
    }

    [Test]
    public void ZombieCorpseLoot_RPC_ShowSearchResult_UsesRpcTargetAndCorrectSignature()
    {
        Type corpseType = ResolveGameType("ZombieCorpseLoot");
        MethodInfo rpcMethod = corpseType.GetMethod("RPC_ShowSearchResult", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(rpcMethod, Is.Not.Null, "RPC_ShowSearchResult method must exist on ZombieCorpseLoot.");

        ParameterInfo[] parameters = rpcMethod.GetParameters();
        Assert.That(parameters.Length, Is.EqualTo(4), "RPC_ShowSearchResult must have exactly 4 parameters (recipient, resultValue, itemId, amount).");

        ParameterInfo recipientParam = parameters[0];
        Assert.That(recipientParam.Name, Is.EqualTo("recipient"));
        Assert.That(recipientParam.ParameterType.Name, Is.EqualTo("PlayerRef"));

        bool hasRpcTarget = recipientParam.GetCustomAttributes(true)
            .Any(attr => attr.GetType().Name == "RpcTargetAttribute" || attr.GetType().Name.Contains("RpcTarget"));
        Assert.That(hasRpcTarget, Is.True, "First parameter 'recipient' must be decorated with [RpcTarget] to ensure unicast delivery over Fusion network transport.");

        Assert.That(parameters[1].Name, Is.EqualTo("resultValue"));
        Assert.That(parameters[2].Name, Is.EqualTo("itemId"));
        Assert.That(parameters[3].Name, Is.EqualTo("amount"));

        PropertyInfo searchedProp = corpseType.GetProperty("HasCorpseBeenSearched", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(searchedProp, Is.Not.Null, "HasCorpseBeenSearched property must exist.");
    }

    [Test]
    public void MilitaryCarPrefab_HasAuthoredInspectionZone_AndComponents()
    {
#if UNITY_EDITOR
        GameObject carPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hau/NewPrefab/Car/Car.prefab");
        Assert.That(carPrefab, Is.Not.Null, "Car.prefab must exist at Assets/Hau/NewPrefab/Car/Car.prefab");

        Transform zoneTransform = carPrefab.transform.Find("VungKiemTraXeCanhSat");
        Assert.That(zoneTransform, Is.Not.Null, "Car.prefab must have child GameObject 'VungKiemTraXeCanhSat'.");

        PolygonCollider2D poly = zoneTransform.GetComponent<PolygonCollider2D>();
        Assert.That(poly, Is.Not.Null, "VungKiemTraXeCanhSat must have PolygonCollider2D attached.");
        Assert.That(poly.isTrigger, Is.True, "VungKiemTraXeCanhSat PolygonCollider2D must be a trigger.");
        Assert.That(poly.pathCount, Is.GreaterThan(0), "PolygonCollider2D must have authored path points.");

        Type authoringType = ResolveGameType("VehicleInspectionZoneAuthoring");
        Component authoring = zoneTransform.GetComponent(authoringType);
        Assert.That(authoring, Is.Not.Null, "VungKiemTraXeCanhSat must have VehicleInspectionZoneAuthoring component.");

        Type stationType = ResolveGameType("RoadsideVehicleRepairStation");
        Component station = carPrefab.GetComponent(stationType);
        Assert.That(station, Is.Not.Null, "Car.prefab root must have RoadsideVehicleRepairStation component.");

        PropertyInfo polyProp = stationType.GetProperty("InspectionPolygon", BindingFlags.Public | BindingFlags.Instance);
        PolygonCollider2D resolvedPoly = polyProp?.GetValue(station) as PolygonCollider2D;
        Assert.That(resolvedPoly, Is.EqualTo(poly), "RoadsideVehicleRepairStation must reference VungKiemTraXeCanhSat polygon.");
#endif
    }

    [Test]
    public void RoadsideVehicleRepairStation_ResolvesAuthoredPolygon_WithoutDuplicateAutoZone()
    {
        Type authoringType = ResolveGameType("VehicleInspectionZoneAuthoring");
        Type stationType = ResolveGameType("RoadsideVehicleRepairStation");

        GameObject carGo = new GameObject("TestCar");
        GameObject zoneGo = new GameObject("VungKiemTraXeCanhSat");
        zoneGo.transform.SetParent(carGo.transform, false);

        PolygonCollider2D authoredPoly = zoneGo.AddComponent<PolygonCollider2D>();
        authoredPoly.SetPath(0, new Vector2[] { new Vector2(-1, 0), new Vector2(1, 0), new Vector2(1, 2), new Vector2(-1, 2) });
        zoneGo.AddComponent(authoringType);

        Component station = carGo.AddComponent(stationType);
        MethodInfo resolveMethod = stationType.GetMethod("ResolveInspectionPolygon", new Type[] { typeof(bool) });
        resolveMethod.Invoke(station, new object[] { true });

        PropertyInfo polyProp = stationType.GetProperty("InspectionPolygon", BindingFlags.Public | BindingFlags.Instance);
        PolygonCollider2D resolvedPoly = polyProp?.GetValue(station) as PolygonCollider2D;

        Assert.That(resolvedPoly, Is.SameAs(authoredPoly), "Station must resolve to authored child polygon.");
        Transform autoChild = carGo.transform.Find("VungKiemTraXeCanhSat [AUTO]");
        Assert.That(autoChild, Is.Null, "Station must not create duplicate [AUTO] zone when authored zone exists.");

        UnityEngine.Object.DestroyImmediate(carGo);
    }

    [Test]
    public void RoadsideVehicleRepairStation_PlayerInsideAndOutsideZoneDetection()
    {
        Type stationType = ResolveGameType("RoadsideVehicleRepairStation");

        GameObject carGo = new GameObject("TestCarDetection");
        GameObject zoneGo = new GameObject("VungKiemTraXeCanhSat");
        zoneGo.transform.SetParent(carGo.transform, false);

        PolygonCollider2D authoredPoly = zoneGo.AddComponent<PolygonCollider2D>();
        authoredPoly.SetPath(0, new Vector2[] { new Vector2(-2, 0), new Vector2(2, 0), new Vector2(2, 4), new Vector2(-2, 4) });

        Component station = carGo.AddComponent(stationType);
        MethodInfo resolveMethod = stationType.GetMethod("ResolveInspectionPolygon", new Type[] { typeof(bool) });
        resolveMethod.Invoke(station, new object[] { true });

        MethodInfo isPlayerInPositionMethod = stationType.GetMethod("IsPlayerInRepairPosition", BindingFlags.Public | BindingFlags.Instance);
        bool inside = (bool)isPlayerInPositionMethod.Invoke(station, new object[] { new Vector3(0f, 2f, 0f) });
        bool outside = (bool)isPlayerInPositionMethod.Invoke(station, new object[] { new Vector3(10f, 10f, 0f) });

        Assert.That(inside, Is.True, "Point (0, 2) inside polygon must return true.");
        Assert.That(outside, Is.False, "Point (10, 10) outside polygon must return false.");

        UnityEngine.Object.DestroyImmediate(carGo);
    }

    [Test]
    public void RoadsideVehicleRepairStation_FallbackAutoGenerationWhenNoAuthoring()
    {
        Type stationType = ResolveGameType("RoadsideVehicleRepairStation");

        GameObject carGo = new GameObject("TestCarUnauthored");
        Component station = carGo.AddComponent(stationType);
        MethodInfo resolveMethod = stationType.GetMethod("ResolveInspectionPolygon", new Type[] { typeof(bool) });
        resolveMethod.Invoke(station, new object[] { true });

        PropertyInfo polyProp = stationType.GetProperty("InspectionPolygon", BindingFlags.Public | BindingFlags.Instance);
        PolygonCollider2D resolvedPoly = polyProp?.GetValue(station) as PolygonCollider2D;

        Assert.That(resolvedPoly, Is.Not.Null, "Station must create fallback polygon when unauthored.");
        Assert.That(resolvedPoly.gameObject.name, Is.EqualTo("VungKiemTraXeCanhSat [AUTO]"));
        Assert.That(resolvedPoly.isTrigger, Is.True);

        // Calling Resolve again must not create another duplicate
        resolveMethod.Invoke(station, new object[] { true });
        int autoCount = 0;
        for (int i = 0; i < carGo.transform.childCount; i++)
        {
            if (carGo.transform.GetChild(i).name == "VungKiemTraXeCanhSat [AUTO]")
                autoCount++;
        }
        Assert.That(autoCount, Is.EqualTo(1), "Subsequent resolve must not create duplicate auto zones.");

        UnityEngine.Object.DestroyImmediate(carGo);
    }

    [Test]
    public void LoadingTips_BilingualLocalization_ValidAcrossAllKeys()
    {
        for (int i = 1; i <= 5; i++)
        {
            string key = $"loading.tip.{i}";

            SetTestLanguage(0); // English
            string enTip = GetLocalization(key);
            Assert.That(enTip, Is.Not.Null.And.Not.Empty);
            Assert.That(enTip, Does.StartWith("Tip:"), $"English tip {i} must start with 'Tip:'");

            SetTestLanguage(1); // Vietnamese
            string viTip = GetLocalization(key);
            Assert.That(viTip, Is.Not.Null.And.Not.Empty);
            Assert.That(viTip, Does.StartWith("Mẹo:"), $"Vietnamese tip {i} must start with 'Mẹo:'");
        }
    }

    [Test]
    public void ReadinessCoordinator_FailedStateIsTerminal_CannotBeReleased()
    {
        Type coordType = ResolveGameType("GameplayReadinessCoordinator");
        MethodInfo resetMethod = coordType.GetMethod("ResetCoordinator", BindingFlags.Public | BindingFlags.Static);
        MethodInfo startMethod = coordType.GetMethod("StartLoading", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(string) }, null)
            ?? coordType.GetMethod("StartLoading", BindingFlags.Public | BindingFlags.Static);
        MethodInfo failMethod = coordType.GetMethod("Fail", BindingFlags.Public | BindingFlags.Static);
        MethodInfo releaseMethod = coordType.GetMethod("Release", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
        PropertyInfo stageProp = coordType.GetProperty("CurrentStage", BindingFlags.Public | BindingFlags.Static);
        PropertyInfo releasedProp = coordType.GetProperty("IsReleasedToGameplay", BindingFlags.Public | BindingFlags.Static);

        resetMethod.Invoke(null, null);
        if (startMethod.GetParameters().Length == 1)
            startMethod.Invoke(null, new object[] { "loading.connecting" });
        else
            startMethod.Invoke(null, null);

        // Fail
        failMethod.Invoke(null, new object[] { "Network dropped" });
        Assert.That(stageProp.GetValue(null).ToString(), Is.EqualTo("Failed"));
        Assert.That((bool)releasedProp.GetValue(null), Is.False);

        // Release attempted while in Failed state
        releaseMethod.Invoke(null, null);
        Assert.That(stageProp.GetValue(null).ToString(), Is.EqualTo("Failed"), "Failed state must not be overwritten by Release().");
        Assert.That((bool)releasedProp.GetValue(null), Is.False);

        // Reset cleans it up
        resetMethod.Invoke(null, null);
        Assert.That(stageProp.GetValue(null).ToString(), Is.EqualTo("None"));
    }

    [Test]
    public void RoadsideVehicleRepairStation_RejectsExternalPolygon_AndResolvesLocally()
    {
        Type stationType = ResolveGameType("RoadsideVehicleRepairStation");

        GameObject carGo = new GameObject("TestCarLocal");
        GameObject otherGo = new GameObject("OtherCarExternal");
        PolygonCollider2D externalPoly = otherGo.AddComponent<PolygonCollider2D>();

        Component station = carGo.AddComponent(stationType);
        FieldInfo polyField = stationType.GetField("inspectionPolygon", BindingFlags.NonPublic | BindingFlags.Instance);
        polyField?.SetValue(station, externalPoly);

        MethodInfo resolveMethod = stationType.GetMethod("ResolveInspectionPolygon", new Type[] { typeof(bool) });
        if (resolveMethod != null)
            resolveMethod.Invoke(station, new object[] { false });
        else
            stationType.GetMethod("ResolveInspectionPolygon", BindingFlags.Public | BindingFlags.Instance).Invoke(station, null);

        PropertyInfo polyProp = stationType.GetProperty("InspectionPolygon", BindingFlags.Public | BindingFlags.Instance);
        PolygonCollider2D resolved = polyProp?.GetValue(station) as PolygonCollider2D;

        Assert.That(resolved, Is.Not.EqualTo(externalPoly), "Station must reject polygon collider from external hierarchy.");

        UnityEngine.Object.DestroyImmediate(carGo);
        UnityEngine.Object.DestroyImmediate(otherGo);
    }

    [Test]
    public void RoadsideVehicleRepairStation_AwakeAndOnValidate_DoNotCreateAutoChildPrematurely()
    {
        Type stationType = ResolveGameType("RoadsideVehicleRepairStation");

        GameObject carGo = new GameObject("TestCarLazy");
        Component station = carGo.AddComponent(stationType);

        MethodInfo resolveNoAutoMethod = stationType.GetMethod("ResolveInspectionPolygon", new Type[] { typeof(bool) });
        if (resolveNoAutoMethod != null)
        {
            resolveNoAutoMethod.Invoke(station, new object[] { false });
            Transform autoChild = carGo.transform.Find("VungKiemTraXeCanhSat [AUTO]");
            Assert.That(autoChild, Is.Null, "Awake/OnValidate must not create AUTO child prematurely.");

            resolveNoAutoMethod.Invoke(station, new object[] { true });
            Transform autoChildAfter = carGo.transform.Find("VungKiemTraXeCanhSat [AUTO]");
            Assert.That(autoChildAfter, Is.Not.Null, "Configure must create AUTO child when allowed.");
        }

        UnityEngine.Object.DestroyImmediate(carGo);
    }

    [Test]
    public void ForceCloseLoadingScreen_EarlyStagesDoNotTransitionToReleased()
    {
        Type coordType = ResolveGameType("GameplayReadinessCoordinator");
        Type menuType = ResolveGameType("AutoMainMenuManager");
        Type stageEnum = ResolveGameType("GameplayReadinessCoordinator+ReadinessStage");

        MethodInfo resetMethod = coordType.GetMethod("ResetCoordinator", BindingFlags.Public | BindingFlags.Static);
        MethodInfo setStageMethod = coordType.GetMethod("SetStage", BindingFlags.Public | BindingFlags.Static, null, new Type[] { stageEnum, typeof(float), typeof(string) }, null);
        PropertyInfo stageProp = coordType.GetProperty("CurrentStage", BindingFlags.Public | BindingFlags.Static);
        PropertyInfo releasedProp = coordType.GetProperty("IsReleasedToGameplay", BindingFlags.Public | BindingFlags.Static);

        GameObject menuGo = new GameObject("TestMainMenu");
        Component menu = menuGo.AddComponent(menuType);
        MethodInfo forceCloseMethod = menuType.GetMethod("ForceCloseLoadingScreen", BindingFlags.Public | BindingFlags.Instance);

        // 1. Stage = Connecting
        resetMethod.Invoke(null, null);
        setStageMethod.Invoke(null, new object[] { Enum.Parse(stageEnum, "Connecting"), 0f, null });
        forceCloseMethod.Invoke(menu, null);
        Assert.That(stageProp.GetValue(null).ToString(), Is.EqualTo("Connecting"), "ForceCloseLoadingScreen must not release Connecting stage.");
        Assert.That((bool)releasedProp.GetValue(null), Is.False);

        // 2. Stage = PlayerSpawnWaiting
        setStageMethod.Invoke(null, new object[] { Enum.Parse(stageEnum, "PlayerSpawnWaiting"), 0f, null });
        forceCloseMethod.Invoke(menu, null);
        Assert.That(stageProp.GetValue(null).ToString(), Is.EqualTo("PlayerSpawnWaiting"), "ForceCloseLoadingScreen must not release PlayerSpawnWaiting stage.");
        Assert.That((bool)releasedProp.GetValue(null), Is.False);

        // 3. Stage = Failed
        MethodInfo failMethod = coordType.GetMethod("Fail", BindingFlags.Public | BindingFlags.Static);
        failMethod.Invoke(null, new object[] { "Timeout test" });
        forceCloseMethod.Invoke(menu, null);
        Assert.That(stageProp.GetValue(null).ToString(), Is.EqualTo("Failed"), "ForceCloseLoadingScreen must not release Failed stage.");
        Assert.That((bool)releasedProp.GetValue(null), Is.False);

        resetMethod.Invoke(null, null);
        UnityEngine.Object.DestroyImmediate(menuGo);
    }

    [Test]
    public void ForceCloseLoadingScreen_LateStagesTransitionToReleased()
    {
        Type coordType = ResolveGameType("GameplayReadinessCoordinator");
        Type menuType = ResolveGameType("AutoMainMenuManager");
        Type stageEnum = ResolveGameType("GameplayReadinessCoordinator+ReadinessStage");

        MethodInfo resetMethod = coordType.GetMethod("ResetCoordinator", BindingFlags.Public | BindingFlags.Static);
        MethodInfo setStageMethod = coordType.GetMethod("SetStage", BindingFlags.Public | BindingFlags.Static, null, new Type[] { stageEnum, typeof(float), typeof(string) }, null);
        PropertyInfo stageProp = coordType.GetProperty("CurrentStage", BindingFlags.Public | BindingFlags.Static);
        PropertyInfo releasedProp = coordType.GetProperty("IsReleasedToGameplay", BindingFlags.Public | BindingFlags.Static);

        GameObject menuGo = new GameObject("TestMainMenu");
        Component menu = menuGo.AddComponent(menuType);
        MethodInfo forceCloseMethod = menuType.GetMethod("ForceCloseLoadingScreen", BindingFlags.Public | BindingFlags.Instance);

        // 1. Stage = HUDAndSystemsReady
        resetMethod.Invoke(null, null);
        setStageMethod.Invoke(null, new object[] { Enum.Parse(stageEnum, "HUDAndSystemsReady"), 0f, null });
        forceCloseMethod.Invoke(menu, null);
        Assert.That(stageProp.GetValue(null).ToString(), Is.EqualTo("ReleasedToGameplay"), "ForceCloseLoadingScreen must release HUDAndSystemsReady stage.");
        Assert.That((bool)releasedProp.GetValue(null), Is.True);

        // 2. Stage = AwaitingHostRelease
        resetMethod.Invoke(null, null);
        setStageMethod.Invoke(null, new object[] { Enum.Parse(stageEnum, "AwaitingHostRelease"), 0f, null });
        forceCloseMethod.Invoke(menu, null);
        Assert.That(stageProp.GetValue(null).ToString(), Is.EqualTo("ReleasedToGameplay"), "ForceCloseLoadingScreen must release AwaitingHostRelease stage.");
        Assert.That((bool)releasedProp.GetValue(null), Is.True);

        resetMethod.Invoke(null, null);
        UnityEngine.Object.DestroyImmediate(menuGo);
    }

    [Test]
    public void ReadinessCoordinator_RequireLocalReadyRelease_GuardsEarlyStages()
    {
        Type coordType = ResolveGameType("GameplayReadinessCoordinator");
        Type stageEnum = ResolveGameType("GameplayReadinessCoordinator+ReadinessStage");

        MethodInfo resetMethod = coordType.GetMethod("ResetCoordinator", BindingFlags.Public | BindingFlags.Static);
        MethodInfo setStageMethod = coordType.GetMethod("SetStage", BindingFlags.Public | BindingFlags.Static, null, new Type[] { stageEnum, typeof(float), typeof(string) }, null);
        MethodInfo releaseMethod = coordType.GetMethod("Release", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(bool) }, null);
        PropertyInfo stageProp = coordType.GetProperty("CurrentStage", BindingFlags.Public | BindingFlags.Static);

        // Stage = Connecting with requireLocalReady = true
        resetMethod.Invoke(null, null);
        setStageMethod.Invoke(null, new object[] { Enum.Parse(stageEnum, "Connecting"), 0f, null });
        releaseMethod.Invoke(null, new object[] { true });
        Assert.That(stageProp.GetValue(null).ToString(), Is.EqualTo("Connecting"), "Release(true) must guard against premature release at Connecting.");

        // Stage = HUDAndSystemsReady with requireLocalReady = true
        setStageMethod.Invoke(null, new object[] { Enum.Parse(stageEnum, "HUDAndSystemsReady"), 0f, null });
        releaseMethod.Invoke(null, new object[] { true });
        Assert.That(stageProp.GetValue(null).ToString(), Is.EqualTo("ReleasedToGameplay"), "Release(true) must release at HUDAndSystemsReady.");

        resetMethod.Invoke(null, null);
    }

    [Test]
    public void AutoMainMenuManager_PauseGate_IgnoresHiddenMainMenuPanel()
    {
        Type menuType = ResolveGameType("AutoMainMenuManager");
        PropertyInfo gateProperty = menuType.GetProperty("IsPauseMenuOrOptionsOpen",
            BindingFlags.Public | BindingFlags.Instance);
        Assert.That(gateProperty, Is.Not.Null);

        Component menu = (Component)System.Runtime.Serialization.FormatterServices
            .GetUninitializedObject(menuType);
        GameObject hiddenMenuCanvasGo = new GameObject("TestHiddenMenuCanvas");
        Canvas hiddenMenuCanvas = hiddenMenuCanvasGo.AddComponent<Canvas>();
        GameObject mainPanel = new GameObject("TestHiddenMainPanel");
        GameObject optionsPanel = new GameObject("TestHiddenOptionsPanel");
        GameObject pausePanel = new GameObject("TestPausePanel");
        GameObject pauseOptions = new GameObject("TestPauseOptions");
        try
        {
            mainPanel.transform.SetParent(hiddenMenuCanvasGo.transform);
            optionsPanel.transform.SetParent(hiddenMenuCanvasGo.transform);
            hiddenMenuCanvasGo.SetActive(false);
            pausePanel.SetActive(false);
            pauseOptions.SetActive(false);

            menuType.GetField("mainCanvas", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(menu, hiddenMenuCanvas);
            menuType.GetField("mainPanel", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(menu, mainPanel);
            menuType.GetField("optionsPanel", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(menu, optionsPanel);
            menuType.GetField("pauseMenuPanel", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(menu, pausePanel);
            menuType.GetField("pauseOptionsPanel", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(menu, pauseOptions);

            Assert.That((bool)gateProperty.GetValue(menu), Is.False,
                "The hidden Main Menu panel must not block gameplay chat; only pause/options panels may do so.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(hiddenMenuCanvasGo);
            UnityEngine.Object.DestroyImmediate(mainPanel);
            UnityEngine.Object.DestroyImmediate(optionsPanel);
            UnityEngine.Object.DestroyImmediate(pausePanel);
            UnityEngine.Object.DestroyImmediate(pauseOptions);
        }
    }

    [Test]
    public void AutoMainMenuManager_StartedClient_DefersLoadingUntilSceneLoadCallback()
    {
        Type menuType = ResolveGameType("AutoMainMenuManager");
        MethodInfo deferMethod = menuType.GetMethod("ShouldDeferClientLoadingUntilSceneLoad",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(deferMethod, Is.Not.Null,
            "A client joining an already-started session must let the actual Fusion scene-load callback own loading initialization.");

        Assert.That((bool)deferMethod.Invoke(null, new object[] { 1 }), Is.True,
            "GameState=1 must defer loading until OnSceneLoadStart to avoid resetting readiness after Spawned().");
        Assert.That((bool)deferMethod.Invoke(null, new object[] { 0 }), Is.False,
            "GameState=0 must not be treated as an already-started session.");
    }

    [Test]
    public void AutoChatManager_IncomingMessage_MakesChatVisibleOnScreen()
    {
        Type chatType = ResolveGameType("AutoChatManager");
        GameObject chatGo = new GameObject("TestAutoChat");
        Component chat = chatGo.AddComponent(chatType);
        MethodInfo buildMethod = chatType.GetMethod("BuildChatUI", BindingFlags.Public | BindingFlags.Instance);
        buildMethod.Invoke(chat, null);

        FieldInfo chatGroupField = chatType.GetField("chatGroup", BindingFlags.NonPublic | BindingFlags.Instance);
        CanvasGroup cg = chatGroupField?.GetValue(chat) as CanvasGroup;
        Assert.That(cg, Is.Not.Null, "Chat panel must have a CanvasGroup.");

        // Simulate chat closed and fully faded out
        cg.alpha = 0f;

        MethodInfo addMsgMethod = chatType.GetMethod("AddPlayerMessage", BindingFlags.Public | BindingFlags.Instance);
        addMsgMethod.Invoke(chat, new object[] { "Survivor", "Enemy approaching!" });

        Assert.That(cg.alpha, Is.EqualTo(1f), "Incoming player message must make chat panel visible (alpha = 1).");

        cg.alpha = 0f;
        MethodInfo addSysMsgMethod = chatType.GetMethod("AddSystemMessage", BindingFlags.Public | BindingFlags.Instance);
        addSysMsgMethod.Invoke(chat, new object[] { "Zone shrinking!" });

        Assert.That(cg.alpha, Is.EqualTo(1f), "Incoming system message must make chat panel visible (alpha = 1).");

        UnityEngine.Object.DestroyImmediate(chatGo);
    }

    [Test]
    public void AutoChatManager_SanitizationAndLimits_ProtectsHistory()
    {
        Type chatType = ResolveGameType("AutoChatManager");
        GameObject chatGo = new GameObject("TestAutoChatSanitize");
        Component chat = chatGo.AddComponent(chatType);
        MethodInfo buildMethod = chatType.GetMethod("BuildChatUI", BindingFlags.Public | BindingFlags.Instance);
        buildMethod.Invoke(chat, null);

        FieldInfo historyField = chatType.GetField("chatHistory", BindingFlags.NonPublic | BindingFlags.Instance);
        UnityEngine.UI.Text historyText = historyField?.GetValue(chat) as UnityEngine.UI.Text;
        Assert.That(historyText, Is.Not.Null);

        MethodInfo addMsgMethod = chatType.GetMethod("AddPlayerMessage", BindingFlags.Public | BindingFlags.Instance);

        // Rich text injection attempt
        addMsgMethod.Invoke(chat, new object[] { "<size=100>Hacker</size>", "<color=red><material=1>Inject</color></material>" });

        Assert.That(historyText.text, Does.Not.Contain("<size="));
        Assert.That(historyText.text, Does.Not.Contain("<material="));
        Assert.That(historyText.text, Does.Contain("Hacker"));
        Assert.That(historyText.text, Does.Contain("Inject"));

        // Long text limit
        string veryLongMessage = new string('A', 300);
        addMsgMethod.Invoke(chat, new object[] { "Survivor", veryLongMessage });
        Assert.That(historyText.text.Length, Is.LessThan(600), "Individual message must be clamped.");

        UnityEngine.Object.DestroyImmediate(chatGo);
    }

    [Test]
    public void AutoChatManager_VietnameseText_PreservedAccurately()
    {
        Type chatType = ResolveGameType("AutoChatManager");
        GameObject chatGo = new GameObject("TestAutoChatVN");
        Component chat = chatGo.AddComponent(chatType);
        MethodInfo buildMethod = chatType.GetMethod("BuildChatUI", BindingFlags.Public | BindingFlags.Instance);
        buildMethod.Invoke(chat, null);

        FieldInfo historyField = chatType.GetField("chatHistory", BindingFlags.NonPublic | BindingFlags.Instance);
        UnityEngine.UI.Text historyText = historyField?.GetValue(chat) as UnityEngine.UI.Text;
        Assert.That(historyText, Is.Not.Null);

        MethodInfo addMsgMethod = chatType.GetMethod("AddPlayerMessage", BindingFlags.Public | BindingFlags.Instance);
        string vnText = "Xin chào đồng đội! Tôi đang ở trạm cứu hộ quân sự.";
        addMsgMethod.Invoke(chat, new object[] { "Chiến Binh", vnText });

        Assert.That(historyText.text, Does.Contain(vnText), "Vietnamese diacritics must be preserved in chat history.");

        UnityEngine.Object.DestroyImmediate(chatGo);
    }

    [Test]
    public void AutoChatManager_CanOpenChat_RespectsReadinessAndTypingState()
    {
        Type chatType = ResolveGameType("AutoChatManager");
        Type coordType = ResolveGameType("GameplayReadinessCoordinator");

        GameObject chatGo = new GameObject("TestAutoChatGates");
        Component chat = chatGo.AddComponent(chatType);
        MethodInfo buildMethod = chatType.GetMethod("BuildChatUI", BindingFlags.Public | BindingFlags.Instance);
        buildMethod.Invoke(chat, null);

        MethodInfo canOpenMethod = chatType.GetMethod("CanOpenChat", BindingFlags.Public | BindingFlags.Instance);
        MethodInfo resetMethod = coordType.GetMethod("ResetCoordinator", BindingFlags.Public | BindingFlags.Static);
        MethodInfo startMethod = coordType.GetMethod("StartLoading", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(string) }, null)
            ?? coordType.GetMethod("StartLoading", BindingFlags.Public | BindingFlags.Static);
        MethodInfo releaseMethod = coordType.GetMethod("Release", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);

        // Start loading -> Cannot open chat
        resetMethod.Invoke(null, null);
        if (startMethod.GetParameters().Length == 1)
            startMethod.Invoke(null, new object[] { "loading.connecting" });
        else
            startMethod.Invoke(null, null);

        bool canOpenConnecting = (bool)canOpenMethod.Invoke(chat, null);
        Assert.That(canOpenConnecting, Is.False, "Should not be able to open chat during loading screen.");

        // Release to gameplay -> Can open chat
        releaseMethod.Invoke(null, null);
        bool canOpenReleased = (bool)canOpenMethod.Invoke(chat, null);
        Assert.That(canOpenReleased, Is.True, "Should be able to open chat when released to gameplay.");

        resetMethod.Invoke(null, null);
        UnityEngine.Object.DestroyImmediate(chatGo);
    }

    [Test]
    public void AutoChatManager_MessageSubmissions_ValidateEmptyWhitespaceAndTrim()
    {
        Type chatType = ResolveGameType("AutoChatManager");
        GameObject chatGo = new GameObject("TestAutoChatSubmit");
        Component chat = chatGo.AddComponent(chatType);
        MethodInfo buildMethod = chatType.GetMethod("BuildChatUI", BindingFlags.Public | BindingFlags.Instance);
        buildMethod.Invoke(chat, null);

        FieldInfo onSendField = chatType.GetField("onSendMessage", BindingFlags.Public | BindingFlags.Instance);
        FieldInfo chatInputField = chatType.GetField("chatInput", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo isTypingField = chatType.GetField("isTyping", BindingFlags.NonPublic | BindingFlags.Instance);
        MethodInfo endEditMethod = chatType.GetMethod("OnChatEndEdit", BindingFlags.NonPublic | BindingFlags.Instance);

        string lastReceivedMsg = null;
        int callCount = 0;
        Action<string> handler = msg =>
        {
            lastReceivedMsg = msg;
            callCount++;
        };
        Delegate del = Delegate.CreateDelegate(onSendField.FieldType, handler.Target, handler.Method);
        onSendField.SetValue(chat, del);

        // 1. Whitespace only -> Should not invoke
        isTypingField.SetValue(chat, true);
        endEditMethod.Invoke(chat, new object[] { "    \t\n   " });
        Assert.That(callCount, Is.EqualTo(0), "Whitespace message must not be sent.");

        // 2. Empty string -> Should not invoke
        isTypingField.SetValue(chat, true);
        endEditMethod.Invoke(chat, new object[] { "" });
        Assert.That(callCount, Is.EqualTo(0), "Empty message must not be sent.");

        // 3. Valid message with padding -> Trimmed and sent exactly once
        isTypingField.SetValue(chat, true);
        endEditMethod.Invoke(chat, new object[] { "   Hello World!   " });
        Assert.That(callCount, Is.EqualTo(1), "Valid message must be sent exactly once.");
        Assert.That(lastReceivedMsg, Is.EqualTo("Hello World!"), "Message must be trimmed.");

        // 4. Typing state reset
        bool isTyping = (bool)isTypingField.GetValue(chat);
        Assert.That(isTyping, Is.False, "isTyping must be false after OnChatEndEdit.");

        UnityEngine.Object.DestroyImmediate(chatGo);
    }

    [Test]
    public void AutoChatManager_OpenAndClose_TogglesTypingAndRaycasts()
    {
        Type chatType = ResolveGameType("AutoChatManager");
        GameObject chatGo = new GameObject("TestAutoChatToggle");
        Component chat = chatGo.AddComponent(chatType);
        MethodInfo buildMethod = chatType.GetMethod("BuildChatUI", BindingFlags.Public | BindingFlags.Instance);
        buildMethod.Invoke(chat, null);

        MethodInfo openMethod = chatType.GetMethod("OpenChat", BindingFlags.Public | BindingFlags.Instance);
        MethodInfo closeMethod = chatType.GetMethod("CloseChat", BindingFlags.Public | BindingFlags.Instance);
        MethodInfo isTypingMethod = chatType.GetMethod("IsTyping", BindingFlags.Public | BindingFlags.Instance);
        FieldInfo chatGroupField = chatType.GetField("chatGroup", BindingFlags.NonPublic | BindingFlags.Instance);
        CanvasGroup cg = chatGroupField.GetValue(chat) as CanvasGroup;

        // Open
        openMethod.Invoke(chat, null);
        Assert.That((bool)isTypingMethod.Invoke(chat, null), Is.True, "IsTyping must be true when chat is open.");
        Assert.That(cg.blocksRaycasts, Is.True, "blocksRaycasts must be true when chat is open.");

        // Close
        closeMethod.Invoke(chat, null);
        Assert.That((bool)isTypingMethod.Invoke(chat, null), Is.False, "IsTyping must be false when chat is closed.");
        Assert.That(cg.blocksRaycasts, Is.False, "blocksRaycasts must be false when chat is closed.");

        UnityEngine.Object.DestroyImmediate(chatGo);
    }

    [Test]
    public void PlayerDeathContext_FormatLeftMessage_LocalizedProperly()
    {
        Type deathContextType = ResolveGameType("PlayerDeathContext");
        MethodInfo formatLeftMethod = deathContextType.GetMethod("FormatLeftMessage", BindingFlags.Public | BindingFlags.Static);
        Assert.That(formatLeftMethod, Is.Not.Null);

        string leftEn = (string)formatLeftMethod.Invoke(null, new object[] { "PlayerOne" });
        Assert.That(leftEn, Does.Contain("PlayerOne"));

        string leftFallback = (string)formatLeftMethod.Invoke(null, new object[] { null });
        Assert.That(leftFallback, Does.Contain("Survivor"));
    }

    [Test]
    public void AutoChatManager_BuildChatUI_AssignsInputTextComponent()
    {
        Type chatType = ResolveGameType("AutoChatManager");
        GameObject chatGo = new GameObject("TestAutoChatInputBinding");
        Component chat = chatGo.AddComponent(chatType);
        MethodInfo buildMethod = chatType.GetMethod("BuildChatUI", BindingFlags.Public | BindingFlags.Instance);
        buildMethod.Invoke(chat, null);

        FieldInfo inputField = chatType.GetField("chatInput", BindingFlags.NonPublic | BindingFlags.Instance);
        UnityEngine.UI.InputField input = inputField?.GetValue(chat) as UnityEngine.UI.InputField;

        Assert.That(input, Is.Not.Null, "Chat InputField must exist.");
        Assert.That(input.textComponent, Is.Not.Null,
            "Chat InputField must bind its child Text component so typed messages render correctly.");

        UnityEngine.Object.DestroyImmediate(chatGo);
    }

    [Test]
    public void AutoChatManager_LoLStyle_ViewportBackgroundTransparentWhenNotTyping_AndHeaderRemoved()
    {
        Type chatType = ResolveGameType("AutoChatManager");
        GameObject chatGo = new GameObject("TestAutoChatLoLStyle");
        Component chat = chatGo.AddComponent(chatType);
        MethodInfo buildMethod = chatType.GetMethod("BuildChatUI", BindingFlags.Public | BindingFlags.Instance);
        buildMethod.Invoke(chat, null);

        FieldInfo vpBgField = chatType.GetField("vpBg", BindingFlags.NonPublic | BindingFlags.Instance);
        UnityEngine.UI.Image vpBg = vpBgField.GetValue(chat) as UnityEngine.UI.Image;
        Assert.That(vpBg, Is.Not.Null, "Viewport image must exist.");

        FieldInfo headerField = chatType.GetField("headerBar", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(headerField, Is.Null, "BoxChat must not create or retain a separate header bar.");

        MethodInfo openMethod = chatType.GetMethod("OpenChat", BindingFlags.Public | BindingFlags.Instance);
        MethodInfo closeMethod = chatType.GetMethod("CloseChat", BindingFlags.Public | BindingFlags.Instance);

        // Ban đầu (khi đóng chat): nền trong suốt (alpha = 0)
        Assert.That(vpBg.color.a, Is.EqualTo(0f), "Viewport background must be 100% transparent when idle (LoL style).");

        // Khi mở chat: nền tối mờ (alpha > 0.5)
        openMethod.Invoke(chat, null);
        Assert.That(vpBg.color.a, Is.GreaterThan(0.5f), "Viewport background must be visible when typing.");

        // Khi đóng chat: nền lập tức trở về trong suốt 100%
        closeMethod.Invoke(chat, null);
        Assert.That(vpBg.color.a, Is.EqualTo(0f), "Viewport background must return to 100% transparent after closing.");

        UnityEngine.Object.DestroyImmediate(chatGo);
    }

    [Test]
    public void AutoChatManager_DragEnd_KeepsTypingAndRestoresInputFocus()
    {
        Type chatType = ResolveGameType("AutoChatManager");
        GameObject chatGo = new GameObject("TestAutoChatDragFocus");
        Component chat = chatGo.AddComponent(chatType);
        MethodInfo buildMethod = chatType.GetMethod("BuildChatUI", BindingFlags.Public | BindingFlags.Instance);
        buildMethod.Invoke(chat, null);

        FieldInfo typingField = chatType.GetField("isTyping", BindingFlags.NonPublic | BindingFlags.Instance);
        typingField.SetValue(chat, true);

        FieldInfo inputField = chatType.GetField("chatInput", BindingFlags.NonPublic | BindingFlags.Instance);
        UnityEngine.UI.InputField input = inputField.GetValue(chat) as UnityEngine.UI.InputField;
        Assert.That(input, Is.Not.Null);
        input.gameObject.SetActive(true);

        Component draggable = Resources.FindObjectsOfTypeAll<MonoBehaviour>()
            .FirstOrDefault(component => component != null &&
                component.GetType().Name == "UIDraggable" &&
                component.gameObject.scene.IsValid() &&
                component.GetType().GetField("targetToDrag", BindingFlags.Public | BindingFlags.Instance)
                    ?.GetValue(component) == chatType.GetField("chatPanelRt", BindingFlags.NonPublic | BindingFlags.Instance)
                        ?.GetValue(chat));
        Assert.That(draggable, Is.Not.Null, "Chat panel must expose a draggable viewport.");
        EventSystem eventSystem = ResolveTestEventSystem();
        MethodInfo onBeginDrag = draggable.GetType().GetMethod("OnBeginDrag", BindingFlags.Public | BindingFlags.Instance);
        MethodInfo onEndDrag = draggable.GetType().GetMethod("OnEndDrag", BindingFlags.Public | BindingFlags.Instance);
        Assert.That(onBeginDrag, Is.Not.Null, "Chat draggable must implement OnBeginDrag.");
        Assert.That(onEndDrag, Is.Not.Null, "Chat draggable must implement OnEndDrag.");
        onBeginDrag.Invoke(draggable, new object[] { new PointerEventData(eventSystem) });
        onEndDrag.Invoke(draggable, new object[] { null });

        Assert.That((bool)typingField.GetValue(chat), Is.True,
            "Dragging the chat panel must not close chat input mode.");
        Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(input.gameObject),
            "After dropping the panel, the input field must be focused without pressing Enter again.");

        UnityEngine.Object.DestroyImmediate(chatGo);
    }

    [Test]
    public void AutoChatManager_VisibleTextOnlyPanel_CanBeginDragWithoutEnteringChat()
    {
        Type chatType = ResolveGameType("AutoChatManager");
        GameObject chatGo = new GameObject("TestAutoChatTextOnlyDrag");
        Component chat = chatGo.AddComponent(chatType);
        MethodInfo buildMethod = chatType.GetMethod("BuildChatUI", BindingFlags.Public | BindingFlags.Instance);
        buildMethod.Invoke(chat, null);

        MethodInfo addMessage = chatType.GetMethod("AddPlayerMessage", BindingFlags.Public | BindingFlags.Instance);
        MethodInfo applyTextOnly = chatType.GetMethod("ApplyTextOnlyVisuals", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo panelField = chatType.GetField("chatPanelRt", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo typingField = chatType.GetField("isTyping", BindingFlags.NonPublic | BindingFlags.Instance);
        RectTransform panel = panelField?.GetValue(chat) as RectTransform;
        Assert.That(panel, Is.Not.Null, "Chat panel must exist before it can be dragged.");

        addMessage.Invoke(chat, new object[] { "Survivor", "Visible without opening chat" });
        applyTextOnly.Invoke(chat, null);
        Assert.That((bool)typingField.GetValue(chat), Is.False,
            "An incoming message must remain in text-only mode until the player opens chat.");

        Component draggable = Resources.FindObjectsOfTypeAll<MonoBehaviour>()
            .FirstOrDefault(component => component != null &&
                component.GetType().Name == "UIDraggable" &&
                component.gameObject.scene.IsValid() &&
                component.GetType().GetField("targetToDrag", BindingFlags.Public | BindingFlags.Instance)
                    ?.GetValue(component) == panel);
        Assert.That(draggable, Is.Not.Null, "The visible chat history must expose a drag handler.");

        EventSystem eventSystem = ResolveTestEventSystem();
        MethodInfo onBeginDrag = draggable.GetType().GetMethod("OnBeginDrag", BindingFlags.Public | BindingFlags.Instance);
        MethodInfo onEndDrag = draggable.GetType().GetMethod("OnEndDrag", BindingFlags.Public | BindingFlags.Instance);
        PropertyInfo isDragging = draggable.GetType().GetProperty("IsDragging", BindingFlags.Public | BindingFlags.Instance);
        Assert.That(onBeginDrag, Is.Not.Null);
        Assert.That(onEndDrag, Is.Not.Null);
        Assert.That(isDragging, Is.Not.Null);

        PointerEventData pointer = new PointerEventData(eventSystem)
        {
            position = new Vector2(200f, 200f)
        };
        onBeginDrag.Invoke(draggable, new object[] { pointer });

        Assert.That((bool)isDragging.GetValue(draggable), Is.True,
            "A visible text-only chat panel must be draggable without reopening text input.");

        onEndDrag.Invoke(draggable, new object[] { null });
        UnityEngine.Object.DestroyImmediate(chatGo);
    }

    [Test]
    public void AutoChatManager_DragEnd_IgnoresDelayedInputFieldEndEdit()
    {
        Type chatType = ResolveGameType("AutoChatManager");
        GameObject chatGo = new GameObject("TestAutoChatDelayedEndEdit");
        Component chat = chatGo.AddComponent(chatType);
        MethodInfo buildMethod = chatType.GetMethod("BuildChatUI", BindingFlags.Public | BindingFlags.Instance);
        buildMethod.Invoke(chat, null);

        MethodInfo openMethod = chatType.GetMethod("OpenChat", BindingFlags.Public | BindingFlags.Instance);
        MethodInfo onEndEdit = chatType.GetMethod("OnChatEndEdit", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo typingField = chatType.GetField("isTyping", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo panelField = chatType.GetField("chatPanelRt", BindingFlags.NonPublic | BindingFlags.Instance);
        RectTransform panel = panelField?.GetValue(chat) as RectTransform;
        Assert.That(panel, Is.Not.Null);

        openMethod.Invoke(chat, null);
        Assert.That((bool)typingField.GetValue(chat), Is.True);

        Component draggable = Resources.FindObjectsOfTypeAll<MonoBehaviour>()
            .FirstOrDefault(component => component != null &&
                component.GetType().Name == "UIDraggable" &&
                component.gameObject.scene.IsValid() &&
                component.GetType().GetField("targetToDrag", BindingFlags.Public | BindingFlags.Instance)
                    ?.GetValue(component) == panel);
        Assert.That(draggable, Is.Not.Null, "Chat panel must expose a drag handler.");

        MethodInfo onBeginDrag = draggable.GetType().GetMethod("OnBeginDrag", BindingFlags.Public | BindingFlags.Instance);
        MethodInfo onEndDrag = draggable.GetType().GetMethod("OnEndDrag", BindingFlags.Public | BindingFlags.Instance);
        PointerEventData pointer = new PointerEventData(ResolveTestEventSystem())
        {
            position = new Vector2(200f, 200f)
        };
        onBeginDrag.Invoke(draggable, new object[] { pointer });
        onEndDrag.Invoke(draggable, new object[] { null });

        // Model Unity delivering the focus-loss callback after the drag guard's frame window.
        FieldInfo dragGuardField = chatType.GetField("dragFocusGuardUntilFrame", BindingFlags.NonPublic | BindingFlags.Instance);
        dragGuardField.SetValue(chat, -1);
        onEndEdit.Invoke(chat, new object[] { "delayed focus-loss callback" });

        Assert.That((bool)typingField.GetValue(chat), Is.True,
            "A delayed InputField.onEndEdit callback from dragging must not close the chat panel.");

        UnityEngine.Object.DestroyImmediate(chatGo);
    }

    [Test]
    public void AutoChatManager_DragHandle_PreservesFocusBeforeDragCallback()
    {
        string chatPath = Path.Combine(Application.dataPath, "Script/Tin/AutoChatManager.cs");
        string source = File.ReadAllText(chatPath);

        Assert.That(source, Does.Contain("IsPointerOverChatDragHandle"),
            "Chat end-edit handling must recognize focus loss caused by the drag handle.");
        Assert.That(source, Does.Match(
                @"IsPointerOverChatDragHandle\(\)[\s\S]{0,260}dragPointerDown\s*=\s*true"),
            "Focus loss over the drag handle must arm the drag guard before Unity raises the drag callback.");
        Assert.That(source, Does.Contain("RectTransformUtility.RectangleContainsScreenPoint"),
            "The drag-handle hit test must use Unity UI coordinates.");
        Assert.That(source, Does.Contain("Input.mousePosition"),
            "The drag-handle hit test must use the current pointer position.");
        Assert.That(source.Contains("Input.GetMouseButton(0)"), Is.True,
            "A focus-loss callback delivered after the pointer moved must still be recognized as a drag.");
        Assert.That(source.Contains("Input.GetAxis(\"Mouse X\")"), Is.True,
            "The drag fallback must detect mouse movement while the left button is held.");
    }

    [Test]
    public void ChatBroadcast_UsesGlobalHostRelay_ForEveryClient()
    {
        string playerPath = Path.Combine(Application.dataPath, "Script/Tin/Multiplayer/PlayerInputHandler2D.cs");
        string spawnerPath = Path.Combine(Application.dataPath, "Script/Tin/Multiplayer/HostModeSpawner.cs");
        string playerSource = File.ReadAllText(playerPath);
        string spawnerSource = File.ReadAllText(spawnerPath);

        Assert.That(playerSource, Does.Match(
                @"HostModeSpawner\s+relay\s*=\s*HostModeSpawner\.Instance[\s\S]{0,320}relay\.RPC_BroadcastChat\(senderName, cleanMsg\);"),
            "The player object should forward validated chat to the room-level relay.");
        Assert.That(playerSource, Does.Not.Match(
                @"\[Rpc\(RpcSources\.StateAuthority, RpcTargets\.All\)\]\s+private void RPC_BroadcastChat"),
            "A player object must not own the fan-out RPC; its visibility/lifecycle is per player.");
        Assert.That(spawnerSource, Does.Match(
                @"\[Rpc\(RpcSources\.StateAuthority, RpcTargets\.All\)\]\s+public void RPC_BroadcastChat\(string senderName, string cleanMessage\)"),
            "HostModeSpawner is the room-level object that must fan chat messages out to all clients.");
    }

    [Test]
    public void AutoChatManager_PersistsManagerWithItsCanvasAcrossSceneTransitions()
    {
        string chatPath = Path.Combine(Application.dataPath, "Script/Tin/AutoChatManager.cs");
        string source = File.ReadAllText(chatPath);

        Assert.That(source, Does.Match(
                @"if\s*\(Application\.isPlaying\)[\s\S]{0,500}DontDestroyOnLoad\(gameObject\);"),
            "The manager and its event subscription must survive the network scene transition together with the canvas.");
    }

    [Test]
    public void AutoChatManager_PositionPersistence_RestoresFromPlayerPrefs()
    {
        PlayerPrefs.SetFloat("Chat_PosX", 123.5f);
        PlayerPrefs.SetFloat("Chat_PosY", 456.7f);
        PlayerPrefs.Save();

        Type chatType = ResolveGameType("AutoChatManager");
        GameObject chatGo = new GameObject("TestAutoChatPrefs");
        Component chat = chatGo.AddComponent(chatType);
        MethodInfo buildMethod = chatType.GetMethod("BuildChatUI", BindingFlags.Public | BindingFlags.Instance);
        buildMethod.Invoke(chat, null);

        FieldInfo panelRtField = chatType.GetField("chatPanelRt", BindingFlags.NonPublic | BindingFlags.Instance);
        RectTransform panelRt = panelRtField.GetValue(chat) as RectTransform;
        Assert.That(panelRt, Is.Not.Null);

        Assert.That(panelRt.anchoredPosition.x, Is.EqualTo(123.5f).Within(0.01f));
        Assert.That(panelRt.anchoredPosition.y, Is.EqualTo(456.7f).Within(0.01f));

        PlayerPrefs.DeleteKey("Chat_PosX");
        PlayerPrefs.DeleteKey("Chat_PosY");
        PlayerPrefs.Save();

        UnityEngine.Object.DestroyImmediate(chatGo);
    }

    [Test]
    public void AutoChatManager_EmptyOrWhitespaceMessage_NotSent()
    {
        Type chatType = ResolveGameType("AutoChatManager");
        GameObject chatGo = new GameObject("TestAutoChatEmptyMsg");
        Component chat = chatGo.AddComponent(chatType);
        MethodInfo buildMethod = chatType.GetMethod("BuildChatUI", BindingFlags.Public | BindingFlags.Instance);
        buildMethod.Invoke(chat, null);

        MethodInfo openMethod = chatType.GetMethod("OpenChat", BindingFlags.Public | BindingFlags.Instance);
        openMethod.Invoke(chat, null);

        bool messageSent = false;
        FieldInfo onSendField = chatType.GetField("onSendMessage", BindingFlags.Public | BindingFlags.Instance);
        Action<string> capture = (msg) => messageSent = true;
        Delegate handler = Delegate.CreateDelegate(onSendField.FieldType, capture.Target, capture.Method);
        onSendField.SetValue(chat, handler);

        MethodInfo onEndEditMethod = chatType.GetMethod("OnChatEndEdit", BindingFlags.NonPublic | BindingFlags.Instance);
        onEndEditMethod.Invoke(chat, new object[] { "    " });

        Assert.That(messageSent, Is.False, "Whitespace message must not be sent.");

        openMethod.Invoke(chat, null);
        onEndEditMethod.Invoke(chat, new object[] { "" });
        Assert.That(messageSent, Is.False, "Empty message must not be sent.");

        UnityEngine.Object.DestroyImmediate(chatGo);
    }

    [Test]
    public void AutoChatManager_PositionPersistence_ClampedWithinCanvasBounds()
    {
        PlayerPrefs.SetFloat("Chat_PosX", 99999f);
        PlayerPrefs.SetFloat("Chat_PosY", -500f);
        PlayerPrefs.Save();

        Type chatType = ResolveGameType("AutoChatManager");
        GameObject chatGo = new GameObject("TestAutoChatClamp");
        Component chat = chatGo.AddComponent(chatType);
        MethodInfo buildMethod = chatType.GetMethod("BuildChatUI", BindingFlags.Public | BindingFlags.Instance);
        buildMethod.Invoke(chat, null);

        FieldInfo panelRtField = chatType.GetField("chatPanelRt", BindingFlags.NonPublic | BindingFlags.Instance);
        RectTransform panelRt = panelRtField.GetValue(chat) as RectTransform;
        Assert.That(panelRt, Is.Not.Null);

        Assert.That(panelRt.anchoredPosition.x, Is.LessThan(99999f));
        Assert.That(panelRt.anchoredPosition.y, Is.GreaterThanOrEqualTo(0f));

        PlayerPrefs.DeleteKey("Chat_PosX");
        PlayerPrefs.DeleteKey("Chat_PosY");
        PlayerPrefs.Save();

        UnityEngine.Object.DestroyImmediate(chatGo);
    }

    [Test]
    public void PlayerSurvival_HasActiveSleepStatusMessage_PropertyWorks()
    {
        Type survivalType = ResolveGameType("PlayerSurvival");
        GameObject go = new GameObject("TestPlayerSurvival");
        Component survival = go.AddComponent(survivalType);

        PropertyInfo hasActiveProp = survivalType.GetProperty("HasActiveSleepStatusMessage", BindingFlags.Public | BindingFlags.Instance);
        Assert.That(hasActiveProp, Is.Not.Null);
        Assert.That((bool)hasActiveProp.GetValue(survival), Is.False);

        FieldInfo msgField = survivalType.GetField("sleepStatusMessage", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo untilField = survivalType.GetField("sleepStatusMessageUntil", BindingFlags.NonPublic | BindingFlags.Instance);

        msgField.SetValue(survival, "You can only sleep from 20:00 to 03:00.");
        untilField.SetValue(survival, Time.unscaledTime + 5f);
        Assert.That((bool)hasActiveProp.GetValue(survival), Is.True);

        untilField.SetValue(survival, Time.unscaledTime - 1f);
        Assert.That((bool)hasActiveProp.GetValue(survival), Is.False);

        UnityEngine.Object.DestroyImmediate(go);
    }

    [Test]
    public void Client_DirectOrLateJoin_ShowLoadingScreen_DoesNotResetReadinessWhenReleasedOrSceneLoaded()
    {
        Type coordType = ResolveGameType("GameplayReadinessCoordinator");
        Type menuType = ResolveGameType("AutoMainMenuManager");
        Type stageEnum = ResolveGameType("GameplayReadinessCoordinator+ReadinessStage");

        MethodInfo resetMethod = coordType.GetMethod("ResetCoordinator", BindingFlags.Public | BindingFlags.Static);
        MethodInfo setStageMethod = coordType.GetMethod("SetStage", BindingFlags.Public | BindingFlags.Static, null, new Type[] { stageEnum, typeof(float), typeof(string) }, null);
        MethodInfo releaseMethod = coordType.GetMethod("Release", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
        PropertyInfo stageProp = coordType.GetProperty("CurrentStage", BindingFlags.Public | BindingFlags.Static);
        PropertyInfo releasedProp = coordType.GetProperty("IsReleasedToGameplay", BindingFlags.Public | BindingFlags.Static);

        GameObject menuGo = new GameObject("TestMainMenuClientOrdering");
        Component menu = menuGo.AddComponent(menuType);
        MethodInfo showLoadingMethod = menuType.GetMethod("ShowLoadingScreen", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo isLocalSceneLoadedField = menuType.GetField("isLocalSceneLoaded", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo isLoadingScreenActiveField = menuType.GetField("isLoadingScreenActive", BindingFlags.NonPublic | BindingFlags.Instance);

        // 1. Simulate Client has completed loading and is ReleasedToGameplay
        resetMethod.Invoke(null, null);
        setStageMethod.Invoke(null, new object[] { Enum.Parse(stageEnum, "HUDAndSystemsReady"), 0.8f, null });
        releaseMethod.Invoke(null, null);
        isLocalSceneLoadedField.SetValue(menu, true);
        isLoadingScreenActiveField.SetValue(menu, false);

        Assert.That((bool)releasedProp.GetValue(null), Is.True, "Gameplay should be released.");

        // 2. Call ShowLoadingScreen again (e.g. from late GameState update or late callback)
        showLoadingMethod.Invoke(menu, null);

        // 3. Must NOT reopen loading or reset stage back to Connecting
        Assert.That(stageProp.GetValue(null).ToString(), Is.EqualTo("ReleasedToGameplay"),
            "ShowLoadingScreen must not reset readiness when scene is loaded and gameplay is released.");
        Assert.That((bool)releasedProp.GetValue(null), Is.True, "Gameplay must remain released.");
        Assert.That((bool)isLoadingScreenActiveField.GetValue(menu), Is.False, "Loading screen must not become active.");

        resetMethod.Invoke(null, null);
        UnityEngine.Object.DestroyImmediate(menuGo);
    }

    [Test]
    public void HostModeSpawner_PerPlayerReadiness_ReleasesOnlyTheAuthenticatedReadyPlayer()
    {
        string spawnerPath = Path.Combine(Application.dataPath,
            "Script/Tin/Multiplayer/HostModeSpawner.cs");
        Assert.That(File.Exists(spawnerPath), Is.True, spawnerPath);

        string source = File.ReadAllText(spawnerPath);
        Assert.That(source, Does.Contain("RPC_ReleaseReadyPlayer(authoritativePlayer)"),
            "Each authenticated readiness report must release that player immediately.");
        Assert.That(source, Does.Contain("[RpcTarget] PlayerRef targetPlayer"),
            "The release RPC must target one ready player instead of the entire room.");
        Assert.That(source, Does.Not.Contain("HostReadinessTimeoutWatchdog"),
            "Ready players must not wait for the former ten-second room watchdog.");
        Assert.That(source, Does.Not.Contain("playersLoadedSet.Count >= currentPlayersInRoom"),
            "Gameplay entry must not be gated by every connected player finishing loading.");
    }

    [Test]
    public void ForceCloseLoadingScreen_EarlyHostSignal_IsConsumedWhenLocalReadinessBecomesSafe()
    {
        Type coordType = ResolveGameType("GameplayReadinessCoordinator");
        Type menuType = ResolveGameType("AutoMainMenuManager");
        Type stageEnum = ResolveGameType("GameplayReadinessCoordinator+ReadinessStage");

        MethodInfo resetMethod = coordType.GetMethod("ResetCoordinator", BindingFlags.Public | BindingFlags.Static);
        MethodInfo startMethod = coordType.GetMethod("StartLoading", BindingFlags.Public | BindingFlags.Static,
            null, new[] { typeof(string) }, null);
        MethodInfo setStageMethod = coordType.GetMethod("SetStage", BindingFlags.Public | BindingFlags.Static,
            null, new[] { stageEnum, typeof(float), typeof(string) }, null);
        PropertyInfo releasedProp = coordType.GetProperty("IsReleasedToGameplay",
            BindingFlags.Public | BindingFlags.Static);
        MethodInfo forceCloseMethod = menuType.GetMethod("ForceCloseLoadingScreen",
            BindingFlags.Public | BindingFlags.Instance);
        MethodInfo consumePendingMethod = menuType.GetMethod("TryReleasePendingHostSignal",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.That(consumePendingMethod, Is.Not.Null,
            "The menu needs a condition-based path that retains and consumes an early host signal.");

        GameObject menuGo = new GameObject("TestPendingPerPlayerRelease");
        Component menu = menuGo.AddComponent(menuType);

        resetMethod.Invoke(null, null);
        startMethod.Invoke(null, new object[] { "loading.connecting" });
        forceCloseMethod.Invoke(menu, null);
        Assert.That((bool)releasedProp.GetValue(null), Is.False,
            "An early host signal must not bypass local avatar/HUD readiness.");

        object hudReadyStage = Enum.Parse(stageEnum, "HUDAndSystemsReady");
        setStageMethod.Invoke(null, new[] { hudReadyStage, (object)0.8f, "loading.hud_ready" });
        bool consumed = (bool)consumePendingMethod.Invoke(menu, null);

        Assert.That(consumed, Is.True, "The retained host signal must be consumed at the first safe local stage.");
        Assert.That((bool)releasedProp.GetValue(null), Is.True,
            "A locally ready player must enter gameplay without waiting for other players.");

        resetMethod.Invoke(null, null);
        UnityEngine.Object.DestroyImmediate(menuGo);
    }
}
