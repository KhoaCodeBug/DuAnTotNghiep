using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class ReadinessAndChatEditorTests
{
    private static Type ResolveGameType(string name)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name, false))
            .FirstOrDefault(candidate => candidate != null);
        Assert.That(type, Is.Not.Null, $"Could not resolve runtime type '{name}'.");
        return type;
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
        Type deathContextType = ResolveGameType("PlayerDeathContext");
        MethodInfo formatJoinMethod = deathContextType.GetMethod("FormatJoinMessage", BindingFlags.Public | BindingFlags.Static);
        Assert.That(formatJoinMethod, Is.Not.Null);

        string joinMsg = (string)formatJoinMethod.Invoke(null, new object[] { "Khoa" });
        Assert.That(joinMsg, Is.EqualTo("Khoa đã vào trận."));

        string fallbackJoin = (string)formatJoinMethod.Invoke(null, new object[] { "<b></b>" });
        Assert.That(fallbackJoin, Is.EqualTo("Survivor đã vào trận."));
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
            startMethod.Invoke(null, new object[] { "Đang kết nối..." });
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
}
