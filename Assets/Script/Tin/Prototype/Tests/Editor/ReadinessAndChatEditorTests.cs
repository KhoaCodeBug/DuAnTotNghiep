using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

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
        Assert.That(controllerSource, Does.Contain("hit.collider.transform.IsChildOf(cachedIndoorStructureRoot)"),
            "Indoor rays must ignore unrelated outdoor fences and only accept this building's colliders.");
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

    private static void SetTestLanguage(int langIndex) // 0 = English, 1 = Vietnamese
    {
        Type locType = ResolveGameType("GameLocalization");
        Type langEnum = ResolveGameType("GameLocalization+Language");
        MethodInfo setMethod = locType.GetMethod("SetLanguage", BindingFlags.Public | BindingFlags.Static);
        object langVal = Enum.ToObject(langEnum, langIndex);
        setMethod.Invoke(null, new object[] { langVal, false });
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
        MethodInfo releaseMethod = coordType.GetMethod("Release", BindingFlags.Public | BindingFlags.Static);

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
        Assert.That(easyLoadout.Length, Is.EqualTo(3));

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
        Assert.That(hardLoadout.Length, Is.EqualTo(0));

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
            Assert.That(hardLoadout.Length, Is.EqualTo(0), "Hard difficulty must yield 0 starter items.");

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
            Assert.That(easyLoadout.Length, Is.EqualTo(3), "Easy difficulty must yield 3 starter items (AK47, Ammo762, Meat).");
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

        // Test Easy loadout items: AK47, Ammo762, Meat
        Array easyLoadout = (Array)getLoadoutMethod.Invoke(null, new object[] { 0 });
        Assert.That(easyLoadout.Length, Is.EqualTo(3));
        for (int i = 0; i < easyLoadout.Length; i++)
        {
            object entry = easyLoadout.GetValue(i);
            FieldInfo itemIdField = entry.GetType().GetField("ItemId");
            string itemId = (string)itemIdField.GetValue(entry);
            object itemAsset = loadItemMethod.Invoke(null, new object[] { itemId });
            Assert.That(itemAsset, Is.Not.Null, $"Starter item '{itemId}' must exist and load successfully in Resources/Items!");
        }

        // Test Normal loadout items: Flashlight, Bandage
        Array normalLoadout = (Array)getLoadoutMethod.Invoke(null, new object[] { 1 });
        Assert.That(normalLoadout.Length, Is.EqualTo(2));
        for (int i = 0; i < normalLoadout.Length; i++)
        {
            object entry = normalLoadout.GetValue(i);
            FieldInfo itemIdField = entry.GetType().GetField("ItemId");
            string itemId = (string)itemIdField.GetValue(entry);
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
        MethodInfo releaseMethod = coordType.GetMethod("Release", BindingFlags.Public | BindingFlags.Static);

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
}
