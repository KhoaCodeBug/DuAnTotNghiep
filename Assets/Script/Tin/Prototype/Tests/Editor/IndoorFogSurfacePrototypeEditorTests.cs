using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class IndoorFogSurfacePrototypeEditorTests
{
    private static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;

    [Test]
    public void SurfaceAtlasGpu_DiagonalAlphaRetainsSubTexelCoverageAndNormalizesFootprint()
    {
        Shader shader = Shader.Find("Hidden/IndoorFogSurfaceAtlas");
        Assert.That(shader != null && shader.isSupported, Is.True);
        var source = new Texture2D(8, 8, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Point };
        var pixels = new Color[64];
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 8; x++) pixels[y * 8 + x] = x >= y ? Color.white : Color.clear;
        source.SetPixels(pixels); source.Apply();
        var mesh = new Mesh();
        mesh.vertices = new[] { new Vector3(0, 0), new Vector3(1, 0), new Vector3(0, 1), new Vector3(1, 1) };
        mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
        mesh.uv2 = new[] { new Vector2(0, .1f), new Vector2(1, .1f), new Vector2(0, .1f), new Vector2(1, .1f) };
        mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        var material = new Material(shader);
        material.SetTexture("_MainTex", source);
        material.SetVector("_AtlasBounds", new Vector4(0, 0, 1, 1));
        var atlas = new RenderTexture(4, 4, 0, RenderTextureFormat.RGHalf, RenderTextureReadWrite.Linear);
        var readback = new Texture2D(4, 4, TextureFormat.RGBAFloat, false, true);
        RenderTexture previous = RenderTexture.active;
        try
        {
            atlas.Create();
            using (var commands = new UnityEngine.Rendering.CommandBuffer())
            {
                commands.SetRenderTarget(atlas);
                commands.ClearRenderTarget(false, true, Color.clear);
                commands.DrawMesh(mesh, Matrix4x4.identity, material);
                Graphics.ExecuteCommandBuffer(commands);
            }
            RenderTexture.active = atlas;
            readback.ReadPixels(new Rect(0, 0, 4, 4), 0, 0); readback.Apply();
            Color[] baked = readback.GetPixels();
            Assert.That(baked.Count(p => p.g > .01f && p.g < .99f), Is.GreaterThanOrEqualTo(3),
                "Diagonal alpha must survive as fractional coverage, not binary atlas-sized squares.");
            foreach (Color pixel in baked.Where(p => p.g > .01f))
                Assert.That(pixel.r / pixel.g, Is.EqualTo(.1f).Within(.001f),
                    "Alpha-weighted coordinates must decode to the same footprint even at a partial edge.");
        }
        finally
        {
            RenderTexture.active = previous;
            atlas.Release();
            foreach (UnityEngine.Object item in new UnityEngine.Object[] { source, mesh, material, atlas, readback })
                UnityEngine.Object.DestroyImmediate(item);
        }
    }

    [Test]
    public void SurfaceProjection_SparseTilemapScanIsClippedToIndoorBounds()
    {
        var gridObject = new GameObject("Indoor Fog sparse-grid test", typeof(Grid));
        var mapObject = new GameObject("Sparse surface", typeof(Tilemap), typeof(TilemapRenderer));
        mapObject.transform.SetParent(gridObject.transform, false);
        var tile = ScriptableObject.CreateInstance<Tile>();
        try
        {
            var map = mapObject.GetComponent<Tilemap>();
            map.SetTile(new Vector3Int(-100000, -100000, 0), tile);
            map.SetTile(Vector3Int.zero, tile);
            MethodInfo clip = Type.GetType("IndoorFogSurfaceMap, Assembly-CSharp")
                .GetMethod("GetClippedCellBounds", BindingFlags.NonPublic | BindingFlags.Static);
            var clipped = (BoundsInt)clip.Invoke(null, new object[] { map, new Bounds(Vector3.zero, new Vector3(10, 10, 1)) });
            long cellCount = (long)clipped.size.x * clipped.size.y * clipped.size.z;
            Assert.That(clipped.Contains(Vector3Int.zero), Is.True);
            Assert.That(cellCount, Is.LessThan(20000), "A sparse global tilemap must not force a scene-wide scan.");
            Assert.That(clipped.size.x, Is.LessThan(map.cellBounds.size.x));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(tile);
            UnityEngine.Object.DestroyImmediate(gridObject);
        }
    }

    [Test]
    public void MainPlayAuthoring_AllBuildingReferencesAreComplete()
    {
        Type authoring = Type.GetType("IndoorFogMainPlayAuthoring, Assembly-CSharp-Editor");
        Assert.That(authoring, Is.Not.Null);
        authoring.GetMethod("ValidateMainPlay").Invoke(null, null);

        Type surfaceType = Type.GetType("IndoorFogSurfaceMap, Assembly-CSharp");
        Component[] maps = Resources.FindObjectsOfTypeAll(surfaceType).OfType<Component>()
            .Where(component => component.gameObject.scene.isLoaded &&
                component.gameObject.scene.path == "Assets/Scenes/Main.unity").ToArray();
        authoring.GetMethod("ValidateAllMainBuildings").Invoke(null,
            new object[] { UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/Main.unity") });
        Assert.That(maps, Has.Length.EqualTo(78), "All 77 Main buildings / 78 roof volumes must be covered.");
        Assert.That(maps.Select(map => map.gameObject).Distinct().Count(), Is.EqualTo(maps.Length),
            "No rollout root may contain duplicate IndoorFogSurfaceMap components.");
        foreach (Component map in maps)
        {
            Assert.That(surfaceType.GetField("indoorVolume").GetValue(map), Is.Not.Null, map.name);
            var surfaces = (Tilemap[])surfaceType.GetField("surfaces").GetValue(map);
            Assert.That(surfaces, Is.Not.Null.And.Not.Empty, map.name);
            Assert.That(surfaces, Has.None.Null, map.name);
            Assert.That((int)surfaceType.GetField("atlasResolution").GetValue(map), Is.EqualTo(1024), map.name);
            var aliases = (Collider2D[])surfaceType.GetField("additionalIndoorVolumes").GetValue(map);
            Assert.That(aliases, Has.Length.EqualTo(map.name == "cuahang_FIX" ? 1 : 0), map.name);
            foreach (var alias in aliases)
                Assert.That((bool)surfaceType.GetMethod("MatchesIndoorVolume").Invoke(map, new object[] { alias }), Is.True);
        }
    }

    [TestCase("continuous-wall", 0)]
    [TestCase("two-silhouettes", 2)]
    [TestCase("school-scale", 18)]
    [TestCase("dense-valid", 32)]
    [TestCase("overflow", 0)]
    public void ShadowEdges_GradeSilhouettesWithoutTogglingAtTheOldSixteenEdgeLimit(string scenario, int expected)
    {
        // Keep the object inactive: only exercise classification of cached rays,
        // without creating the live overlay or changing its singleton.
        var fixture = new GameObject("Shadow edge classifier test");
        fixture.SetActive(false);
        try
        {
            Type type = Type.GetType("FogVisionController, Assembly-CSharp");
            var fog = fixture.AddComponent(type);
            var rays = (float[])type.GetField("indoorOcclusionDistances", Private).GetValue(fog);
            for (int i = 0; i < rays.Length; i++)
                rays[i] = scenario == "overflow" ? (i % 2 == 0 ? 5f : 15f) :
                    scenario == "school-scale" ? (i % 20 >= 2 && i % 20 < 8 ? 15f : 5f) :
                    scenario == "dense-valid" ? (i % 9 >= 2 && i % 9 < 5 ? 15f : 5f) :
                    scenario == "two-silhouettes" && i >= 20 && i < 60 ? 15f : 5f;
            float[] original = (float[])rays.Clone();
            type.GetMethod("BuildIndoorShadowEdges", Private).Invoke(fog, new object[] { rays.Length });
            Assert.That((int)type.GetField("indoorShadowEdgeCount", Private).GetValue(fog), Is.EqualTo(expected));
            int candidateCount = (int)type.GetField("indoorShadowCandidateCount", Private).GetValue(fog);
            if (scenario == "school-scale")
                Assert.That(candidateCount, Is.EqualTo(18), "School-sized geometry must not fall back to sharp V2 shadows.");
            if (scenario == "dense-valid")
                Assert.That(candidateCount, Is.GreaterThan(32), "The strongest 32 edges should be retained instead of disabling fade.");
            if (scenario == "overflow")
                Assert.That(candidateCount, Is.GreaterThan(64), "Only malformed/noisy ray data should disable optional grading.");
            CollectionAssert.AreEqual(original, rays, "Optional grading must not alter the accepted occlusion rays.");
            var edges = (Vector4[])type.GetField("indoorShadowEdges", Private).GetValue(fog);
            for (int i = 0; i < expected; i++)
            {
                Assert.That(new Vector2(edges[i].x, edges[i].y).magnitude, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(edges[i].z, Is.EqualTo(5f), "Do not start grading before the near blocker.");
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(fixture); }
    }

    [Test]
    public void ShadowEdges_TemporalTracksPreserveIdentityUseHysteresisAndResetOnTeleport()
    {
        var fixture = new GameObject("Shadow edge temporal stability test");
        fixture.SetActive(false);
        try
        {
            Type type = Type.GetType("FogVisionController, Assembly-CSharp");
            var fog = fixture.AddComponent(type);
            var rays = (float[])type.GetField("indoorOcclusionDistances", Private).GetValue(fog);
            FieldInfo originField = type.GetField("lastOcclusionOrigin", Private);
            FieldInfo countField = type.GetField("indoorShadowEdgeCount", Private);
            FieldInfo idsField = type.GetField("indoorShadowEdgeIds", Private);
            FieldInfo edgesField = type.GetField("indoorShadowEdges", Private);
            FieldInfo targetsField = type.GetField("indoorShadowTargetEdges", Private);
            FieldInfo weightsField = type.GetField("indoorShadowTargetWeights", Private);
            MethodInfo build = type.GetMethod("BuildIndoorShadowEdges", Private);
            MethodInfo update = type.GetMethod("UpdateIndoorShadowEdgePresentation", Private);

            SetTwoSilhouetteRays(rays, 20, 60);
            originField.SetValue(fog, Vector2.zero);
            build.Invoke(fog, new object[] { rays.Length });
            int count = (int)countField.GetValue(fog);
            Assert.That(count, Is.EqualTo(2));
            var originalIds = ((int[])idsField.GetValue(fog)).Take(count).ToArray();

            SetTwoSilhouetteRays(rays, 21, 61);
            originField.SetValue(fog, new Vector2(0.04f, 0f));
            build.Invoke(fog, new object[] { rays.Length });
            CollectionAssert.AreEquivalent(originalIds, ((int[])idsField.GetValue(fog)).Take(count).ToArray(),
                "A one-ray micro shift must keep the same temporal edge identities.");
            var before = ((Vector4[])edgesField.GetValue(fog))[0];
            var target = ((Vector4[])targetsField.GetValue(fog))[0];
            update.Invoke(fog, new object[] { 1f / 60f });
            var after = ((Vector4[])edgesField.GetValue(fog))[0];
            float beforeError = Vector2.Angle(new Vector2(before.x, before.y), new Vector2(target.x, target.y));
            float afterError = Vector2.Angle(new Vector2(after.x, after.y), new Vector2(target.x, target.y));
            Assert.That(afterError, Is.LessThan(beforeError), "Presentation should converge toward the new scan.");
            Assert.That(afterError, Is.GreaterThan(0.001f), "One frame must not snap fully to the new scan.");

            for (int i = 0; i < rays.Length; i++) rays[i] = 5f;
            build.Invoke(fog, new object[] { rays.Length });
            build.Invoke(fog, new object[] { rays.Length });
            Assert.That(((float[])weightsField.GetValue(fog)).Take(count), Is.All.EqualTo(1f),
                "Exit grace must absorb two missing scans.");
            build.Invoke(fog, new object[] { rays.Length });
            Assert.That(((float[])weightsField.GetValue(fog)).Take(count), Is.All.EqualTo(0f),
                "The third missing scan should start a smooth fade-out.");

            SetTwoSilhouetteRays(rays, 20, 60);
            originField.SetValue(fog, new Vector2(3f, 0f));
            build.Invoke(fog, new object[] { rays.Length });
            var teleportedIds = ((int[])idsField.GetValue(fog)).Take((int)countField.GetValue(fog)).ToArray();
            Assert.That(originalIds.Intersect(teleportedIds), Is.Empty,
                "A teleport must reset history instead of dragging old geometry across the map.");
        }
        finally { UnityEngine.Object.DestroyImmediate(fixture); }
    }

    [UnityTest, Timeout(180000)]
    public IEnumerator SurfaceProjection_MainPlayStressSites_BuildReuseCaptureAndRelease()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        yield return new EnterPlayMode(false);
        Type qa = Type.GetType("IndoorFogPrototypeQA, Assembly-CSharp-Editor");
        Type movement = Type.GetType("PlayerMovement, Assembly-CSharp");
        Type fogType = Type.GetType("FogVisionController, Assembly-CSharp");
        Type surfaceType = Type.GetType("IndoorFogSurfaceMap, Assembly-CSharp");
        Type readiness = Type.GetType("GameplayReadinessCoordinator, Assembly-CSharp");
        qa.GetMethod("StartSolo").Invoke(null, null);
        float deadline = Time.realtimeSinceStartup + 75;
        while (Time.realtimeSinceStartup < deadline &&
            (movement.GetField("LocalPlayerInstance").GetValue(null) == null ||
             !(bool)readiness.GetProperty("IsReleasedToGameplay").GetValue(null))) yield return null;
        Assert.That(movement.GetField("LocalPlayerInstance").GetValue(null), Is.Not.Null);
        Assert.That((bool)readiness.GetProperty("IsReleasedToGameplay").GetValue(null), Is.True);

        string runId = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        string[] labels = { "school", "hospital-large", "hospital-small", "house-main" };
        string[] roofs = { "__SchoolRoofTrigger_FIXED", "nocnha", "nocnha (1)", "nhachinhxaydautien (12)" };
        Vector2[] positions = { new Vector2(11.36f, 49.93f), new Vector2(-46.646f, 16.413f),
            new Vector2(-49.584f, 37.427f), new Vector2(-39.2f, 44.3f) };
        Type cheatType = Type.GetType("DevCheatManager, Assembly-CSharp");
        object cheat = cheatType.GetProperty("Instance").GetValue(null);
        FieldInfo godMode = cheatType.GetField("isGodMode");
        bool previousGodMode = (bool)godMode.GetValue(cheat);
        // Deterministic visual fixture: hostile AI must not end the authority player
        // while the four capture sites are exercised. This is restored in finally.
        godMode.SetValue(cheat, true);
        try
        {
            Component previousSurface = null;
            for (int site = 0; site < labels.Length; site++)
            {
                RenderTexture siteAtlas = null;
                Component siteSurface = null;
                for (int variant = 0; variant < 4; variant++)
                {
                    bool night = variant >= 2;
                    bool torch = variant % 2 == 0;
                    string mode = (night ? "night" : "day") + (torch ? "-on" : "-off");
                    string label = "rollout-" + runId + "-" + labels[site] + "-" + mode;
                    string pose = "{\"house\":\"" + roofs[site] + "\",\"flashlightConeFeather\":0.2," +
                        "\"flashlightBoundaryFadeDistance\":0.65,\"x\":" + F(positions[site].x) + ",\"y\":" + F(positions[site].y) +
                        ",\"hour\":" + (night ? "21" : "13.5") + ",\"directionX\":0,\"directionY\":1,\"zoom\":3," +
                        "\"cameraUp\":1.2,\"flashlight\":" + torch.ToString().ToLowerInvariant() +
                        ",\"prototype\":true,\"label\":\"" + label + "\"}";
                    qa.GetMethod("ApplyPoseJson").Invoke(null, new object[] { pose });
                    yield return new WaitForSecondsRealtime(0.4f);
                    qa.GetMethod("ApplyPoseJson").Invoke(null, new object[] { pose });
                    yield return new WaitForSecondsRealtime(1f);

                    var localPlayer = (Component)movement.GetField("LocalPlayerInstance").GetValue(null);
                    Component vision = localPlayer.GetComponent("PlayerVision");
                    var activeCollider = (Collider2D)vision.GetType().GetProperty("ActiveIndoorCollider").GetValue(vision);
                    Assert.That(activeCollider, Is.Not.Null, label);
                    siteSurface = activeCollider.GetComponentInParent(surfaceType);
                    Assert.That(siteSurface, Is.Not.Null, label);
                    Assert.That(surfaceType.GetField("indoorVolume").GetValue(siteSurface), Is.SameAs(activeCollider), label);
                    var fog = (Component)fogType.GetProperty("Instance").GetValue(null);
                    var material = (Material)fogType.GetField("overlayMaterial", Private).GetValue(fog);
                    Assert.That(material.GetFloat("_IndoorActive"), Is.EqualTo(1f), label);
                    Assert.That(material.GetFloat("_IndoorSurfaceActive"), Is.EqualTo(1f), label);
                    Assert.That(material.GetFloat("_FlashlightActive"), Is.EqualTo(torch ? 1f : 0f), label);

                    if (site == 0 && variant == 0)
                    {
                        float[] microOffsets = { -0.08f, -0.04f, 0f, 0.04f, 0.08f };
                        HashSet<int> previousTrackIds = null;
                        foreach (float offset in microOffsets)
                        {
                            string microLabel = label + "-micro-" + F(offset);
                            string microPose = "{\"house\":\"" + roofs[site] + "\",\"flashlightConeFeather\":0.2," +
                                "\"flashlightBoundaryFadeDistance\":0.65,\"x\":" + F(positions[site].x + offset) +
                                ",\"y\":" + F(positions[site].y) + ",\"hour\":13.5,\"directionX\":0,\"directionY\":1,\"zoom\":3," +
                                "\"cameraUp\":1.2,\"flashlight\":true,\"prototype\":true,\"label\":\"" + microLabel + "\"}";
                            qa.GetMethod("ApplyPoseJson").Invoke(null, new object[] { microPose });
                            yield return new WaitForSecondsRealtime(0.25f);

                            int candidateCount = (int)fogType.GetField("indoorShadowCandidateCount", Private).GetValue(fog);
                            int edgeCount = Mathf.RoundToInt(material.GetFloat("_IndoorShadowEdgeCount"));
                            int[] allTrackIds = (int[])fogType.GetField("indoorShadowEdgeIds", Private).GetValue(fog);
                            var trackIds = new HashSet<int>(allTrackIds.Take(edgeCount));
                            Assert.That(candidateCount, Is.GreaterThan(16).And.LessThanOrEqualTo(64),
                                microLabel + " must exercise the former 16-edge overflow boundary.");
                            Assert.That(edgeCount, Is.GreaterThan(0).And.LessThanOrEqualTo(32),
                                microLabel + " must keep bounded temporal silhouette tracks.");
                            if (previousTrackIds != null)
                            {
                                int overlap = previousTrackIds.Count(trackIds.Contains);
                                int required = Mathf.Max(1, Mathf.Min(previousTrackIds.Count, trackIds.Count) / 2);
                                Assert.That(overlap, Is.GreaterThanOrEqualTo(required),
                                    microLabel + " must preserve at least half of the edge identities across a 4 cm move.");
                            }
                            previousTrackIds = trackIds;
                            qa.GetMethod("Capture").Invoke(null, null);
                            yield return new WaitForSecondsRealtime(0.15f);
                        }

                        qa.GetMethod("ApplyPoseJson").Invoke(null, new object[] { pose });
                        yield return new WaitForSecondsRealtime(0.4f);
                        qa.GetMethod("Profile").Invoke(null, null);
                        float profileDeadline = Time.realtimeSinceStartup + 20f;
                        while (qa.GetField("recorders", PrivateStatic).GetValue(null) != null &&
                               Time.realtimeSinceStartup < profileDeadline) yield return null;
                        Assert.That(qa.GetField("recorders", PrivateStatic).GetValue(null), Is.Null,
                            "Indoor fog profile should complete within 20 seconds.");
                        foreach (float offset in new[] { 0f, 2.4f, 4.8f })
                        {
                            string closePose = AllMainPose(roofs[site], positions[site], 13.5f, true,
                                "rollout-" + runId + "-school-close-" + F(offset), 1.5f, offset);
                            qa.GetMethod("ApplyPoseJson").Invoke(null, new object[] { closePose });
                            yield return new WaitForSecondsRealtime(0.5f);
                            qa.GetMethod("Capture").Invoke(null, null);
                            yield return new WaitForSecondsRealtime(0.2f);
                        }
                        qa.GetMethod("ApplyPoseJson").Invoke(null, new object[] { pose });
                        yield return new WaitForSecondsRealtime(0.3f);
                    }

                    var atlas = (RenderTexture)surfaceType.GetProperty("Atlas").GetValue(siteSurface);
                    Assert.That(atlas != null && atlas.IsCreated(), Is.True, label);
                    Assert.That(atlas.filterMode, Is.EqualTo(FilterMode.Bilinear),
                        label + " must coverage-filter the surface atlas instead of exposing atlas-sized point steps.");
                    Assert.That(atlas.format, Is.EqualTo(RenderTextureFormat.RGHalf), label);
                    Assert.That(atlas.wrapMode, Is.EqualTo(TextureWrapMode.Clamp), label);
                    Assert.That(atlas.useMipMap, Is.False, label);
                    if (siteAtlas == null) siteAtlas = atlas;
                    else Assert.That(atlas, Is.SameAs(siteAtlas), "Day/night and flashlight changes must reuse the site atlas.");
                    int surfaceCount = (int)surfaceType.GetProperty("SurfaceCount").GetValue(siteSurface);
                    int scannedCells = (int)surfaceType.GetProperty("ScannedCellCount").GetValue(siteSurface);
                    long atlasBytes = (long)surfaceType.GetProperty("AtlasMemoryBytes").GetValue(siteSurface);
                    double buildMs = (double)surfaceType.GetProperty("LastBuildMilliseconds").GetValue(siteSurface);
                    Assert.That(surfaceCount, Is.GreaterThan(0), label);
                    Assert.That(scannedCells, Is.GreaterThan(0), label);
                    Assert.That(atlasBytes, Is.GreaterThan(0).And.LessThanOrEqualTo(3072L * 3072L * 4L), label);
                    Debug.Log("[IndoorFogStress] " + label + " surfaces=" + surfaceCount + " scannedCells=" + scannedCells +
                        " atlasBytes=" + atlasBytes + " buildMs=" + buildMs.ToString("F3"));
                    qa.GetMethod("Capture").Invoke(null, null);
                    yield return new WaitForSecondsRealtime(0.4f);
                }

                Assert.That(siteSurface, Is.Not.SameAs(previousSurface), "Each stress volume must own an explicit surface map.");
                previousSurface = siteSurface;
                string outdoor = "{\"house\":\"" + roofs[site] + "\",\"x\":-62.29,\"y\":30.77,\"hour\":13.5," +
                    "\"directionX\":0,\"directionY\":1,\"zoom\":3,\"cameraUp\":1.2,\"flashlight\":false," +
                    "\"prototype\":true,\"label\":\"rollout-" + runId + "-" + labels[site] + "-outdoor\"}";
                qa.GetMethod("ApplyPoseJson").Invoke(null, new object[] { outdoor });
                yield return new WaitForSecondsRealtime(1f);
                Assert.That(siteAtlas == null || !siteAtlas.IsCreated(), Is.True, "Outdoor transition must release " + labels[site] + " atlas.");
            }
        }
        finally
        {
            godMode.SetValue(cheat, previousGodMode);
            qa.GetMethod("ClearPoseOverride").Invoke(null, null);
        }
        yield return new ExitPlayMode();
        Assert.That(Resources.FindObjectsOfTypeAll<RenderTexture>().Any(t => t.name == "Indoor surface projection (local)"), Is.False);
    }

    [UnityTest, Timeout(360000)]
    public IEnumerator SurfaceProjection_AllMainVolumes_ActivateReuseReleaseAndCaptureFamilies()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        yield return new EnterPlayMode(false);
        Type qa = Type.GetType("IndoorFogPrototypeQA, Assembly-CSharp-Editor");
        Type movement = Type.GetType("PlayerMovement, Assembly-CSharp");
        Type surfaceType = Type.GetType("IndoorFogSurfaceMap, Assembly-CSharp");
        Type fogType = Type.GetType("FogVisionController, Assembly-CSharp");
        Type readiness = Type.GetType("GameplayReadinessCoordinator, Assembly-CSharp");
        qa.GetMethod("StartSolo").Invoke(null, null);
        float deadline = Time.realtimeSinceStartup + 90;
        while (Time.realtimeSinceStartup < deadline &&
            !(bool)readiness.GetProperty("IsReleasedToGameplay").GetValue(null)) yield return null;
        Assert.That((bool)readiness.GetProperty("IsReleasedToGameplay").GetValue(null), Is.True);
        var player = (Component)movement.GetField("LocalPlayerInstance").GetValue(null);
        Component vision = player.GetComponent("PlayerVision");
        int obstacleMask = ((LayerMask)vision.GetType().GetField("obstacleLayer").GetValue(vision)).value;
        Type cheatType = Type.GetType("DevCheatManager, Assembly-CSharp");
        object cheat = cheatType.GetProperty("Instance").GetValue(null);
        FieldInfo godMode = cheatType.GetField("isGodMode");
        bool previousGodMode = (bool)godMode.GetValue(cheat);
        godMode.SetValue(cheat, true);
        var families = new HashSet<string>();
        string runId = "allmain-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var report = new System.Text.StringBuilder("building,point,atlas,bytes,buildMs,familyCapture\n");
        try
        {
            var maps = UnityEngine.Object.FindObjectsByType(surfaceType, FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .OfType<Component>().OrderBy(m => m.name).ToArray();
            Assert.That(maps, Has.Length.EqualTo(78));
            foreach (Component map in maps)
            {
                var volume = (Collider2D)surfaceType.GetField("indoorVolume").GetValue(map);
                string family = System.Text.RegularExpressions.Regex.Replace(map.name, @" \(\d+\)$", "");
                bool captureFamily = families.Add(family);
                Vector2 point = FindClearIndoorPoint(volume, obstacleMask);
                string roofName = map.name == "school" ? "__SchoolRoofTrigger_FIXED" :
                    map.name == "hospital" ? "nocnha" : map.name == "Hospital_Small_FIXED" ? "nocnha (1)" : map.name;
                // Preserve the user's exact School repro, and audited Hospital poses.
                if (map.name == "school") point = new Vector2(11.36f, 49.93f);
                if (map.name == "hospital") point = new Vector2(-46.646f, 16.413f);
                if (map.name == "Hospital_Small_FIXED") point = new Vector2(-49.584f, 37.427f);
                RenderTexture firstAtlas = null;
                int variants = captureFamily ? 4 : 2;
                for (int variant = 0; variant < variants; variant++)
                {
                    bool torch = variant % 2 == 0;
                    string label = runId + "-" + family + "-" + variant;
                    string pose = AllMainPose(roofName, point, variant >= 2 ? 21 : 13.5f, torch, label);
                    qa.GetMethod("ApplyPoseJson").Invoke(null, new object[] { pose });
                    yield return new WaitForSecondsRealtime(0.16f);
                    qa.GetMethod("ApplyPoseJson").Invoke(null, new object[] { pose });
                    yield return new WaitForSecondsRealtime(0.35f);
                    var actual = (Collider2D)vision.GetType().GetProperty("ActiveIndoorCollider").GetValue(vision);
                    Assert.That((bool)surfaceType.GetMethod("MatchesIndoorVolume").Invoke(map, new object[] { actual }),
                        Is.True, map.name + " must naturally enter an explicitly authored roof volume.");
                    Assert.That(actual.GetComponentInParent(surfaceType), Is.SameAs(map), map.name);
                    var fog = (Component)fogType.GetProperty("Instance").GetValue(null);
                    var material = (Material)fogType.GetField("overlayMaterial", Private).GetValue(fog);
                    Assert.That(material.GetFloat("_IndoorSurfaceActive"), Is.EqualTo(1), label);
                    Assert.That(material.GetFloat("_FlashlightActive"), Is.EqualTo(torch ? 1 : 0), label);
                    var atlas = (RenderTexture)surfaceType.GetProperty("Atlas").GetValue(map);
                    Assert.That(atlas != null && atlas.IsCreated(), Is.True, label);
                    Assert.That(atlas.format, Is.EqualTo(RenderTextureFormat.RGHalf), label);
                    Assert.That(atlas.filterMode, Is.EqualTo(FilterMode.Bilinear), label);
                    Assert.That(atlas.width, Is.LessThanOrEqualTo(3072));
                    Assert.That(atlas.height, Is.LessThanOrEqualTo(3072));
                    if (firstAtlas == null) firstAtlas = atlas;
                    else Assert.That(atlas, Is.SameAs(firstAtlas), label + " should reuse its atlas.");
                    Assert.That((int)surfaceType.GetProperty("SurfaceCount").GetValue(map), Is.GreaterThan(0), label);
                    if (captureFamily)
                    {
                        qa.GetMethod("Capture").Invoke(null, null);
                        yield return new WaitForSecondsRealtime(0.1f);
                    }
                }
                report.AppendLine(map.name + ",\"" + point + "\"," + firstAtlas.width + "x" + firstAtlas.height + "," +
                    surfaceType.GetProperty("AtlasMemoryBytes").GetValue(map) + "," +
                    surfaceType.GetProperty("LastBuildMilliseconds").GetValue(map) + "," + captureFamily);
                if (map.name == "school")
                {
                    foreach (float offset in new[] { 0f, 4.8f, 8f })
                    {
                        string pose = AllMainPose(roofName, point, 13.5f, true, runId + "-school-close-" + F(offset), 1.5f, offset);
                        qa.GetMethod("ApplyPoseJson").Invoke(null, new object[] { pose });
                        yield return new WaitForSecondsRealtime(0.2f);
                        qa.GetMethod("ApplyPoseJson").Invoke(null, new object[] { pose });
                        yield return new WaitForSecondsRealtime(0.6f);
                        qa.GetMethod("Capture").Invoke(null, null);
                        yield return new WaitForSecondsRealtime(0.1f);
                    }
                }
                qa.GetMethod("ApplyPoseJson").Invoke(null, new object[] {
                    AllMainPose(roofName, new Vector2(-62.29f, 30.77f), 13.5f, false, runId + "-outdoor") });
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.That(firstAtlas == null || !firstAtlas.IsCreated(), Is.True, map.name + " must release on exit.");
            }
            Assert.That(families.Count, Is.EqualTo(12));
        }
        finally
        {
            Directory.CreateDirectory("QA_Artifacts/IndoorFogAllMain_20260902");
            File.WriteAllText("QA_Artifacts/IndoorFogAllMain_20260902/" + runId + "-runtime.csv", report.ToString());
            godMode.SetValue(cheat, previousGodMode);
            qa.GetMethod("ClearPoseOverride").Invoke(null, null);
        }
        yield return new ExitPlayMode();
        Assert.That(Resources.FindObjectsOfTypeAll<RenderTexture>().Any(t => t.name == "Indoor surface projection (local)"), Is.False);
    }

    private static Vector2 FindClearIndoorPoint(Collider2D volume, int obstacleMask)
    {
        Bounds bounds = volume.bounds;
        var candidates = new List<Vector2>();
        for (int y = 1; y < 20; y++)
        for (int x = 1; x < 20; x++)
        {
            var point = new Vector2(Mathf.Lerp(bounds.min.x, bounds.max.x, x / 20f), Mathf.Lerp(bounds.min.y, bounds.max.y, y / 20f));
            if (volume.OverlapPoint(point) && !Physics2D.OverlapCircleAll(point, 0.25f, obstacleMask).Any(c => !c.isTrigger))
                candidates.Add(point);
        }
        Assert.That(candidates, Is.Not.Empty, volume.name + " has no clear interior sample.");
        return candidates.OrderBy(p => Vector2.SqrMagnitude(p - (Vector2)bounds.center)).First();
    }

    private static string AllMainPose(string roof, Vector2 point, float hour, bool torch, string label, float zoom = 3f, float right = 0f)
    {
        return "{\"house\":\"" + roof + "\",\"x\":" + F(point.x) + ",\"y\":" + F(point.y) +
            ",\"hour\":" + F(hour) + ",\"directionX\":0,\"directionY\":1,\"zoom\":" + F(zoom) +
            ",\"cameraUp\":1.2,\"cameraRight\":" + F(right) + ",\"flashlight\":" + torch.ToString().ToLowerInvariant() +
            ",\"prototype\":true,\"label\":\"" + label + "\"}";
    }

    private static string F(float value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static void SetTwoSilhouetteRays(float[] rays, int startInclusive, int endExclusive)
    {
        for (int i = 0; i < rays.Length; i++)
            rays[i] = i >= startInclusive && i < endExclusive ? 15f : 5f;
    }

}
