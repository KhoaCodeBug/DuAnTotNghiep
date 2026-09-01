using System;
using System.Collections;
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
    public void MainPlayAuthoring_PilotReferencesAreComplete()
    {
        Type authoring = Type.GetType("IndoorFogMainPlayAuthoring, Assembly-CSharp-Editor");
        Assert.That(authoring, Is.Not.Null);
        authoring.GetMethod("ValidateMainPlay").Invoke(null, null);
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
        string[] labels = { "school", "hospital-large", "hospital-small" };
        string[] roofs = { "__SchoolRoofTrigger_FIXED", "nocnha", "nocnha (1)" };
        Vector2[] positions = { new Vector2(11.36f, 49.93f), new Vector2(-46.646f, 16.413f), new Vector2(-49.584f, 37.427f) };
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
                            Assert.That(candidateCount, Is.GreaterThan(16).And.LessThanOrEqualTo(64),
                                microLabel + " must exercise the former 16-edge overflow boundary.");
                            Assert.That(edgeCount, Is.EqualTo(Mathf.Min(candidateCount, 32)),
                                microLabel + " must retain the strongest silhouette fades instead of reverting to sharp legacy shadows.");
                        }

                        qa.GetMethod("ApplyPoseJson").Invoke(null, new object[] { pose });
                        yield return new WaitForSecondsRealtime(0.4f);
                    }

                    var atlas = (RenderTexture)surfaceType.GetProperty("Atlas").GetValue(siteSurface);
                    Assert.That(atlas != null && atlas.IsCreated(), Is.True, label);
                    if (siteAtlas == null) siteAtlas = atlas;
                    else Assert.That(atlas, Is.SameAs(siteAtlas), "Day/night and flashlight changes must reuse the site atlas.");
                    int surfaceCount = (int)surfaceType.GetProperty("SurfaceCount").GetValue(siteSurface);
                    int scannedCells = (int)surfaceType.GetProperty("ScannedCellCount").GetValue(siteSurface);
                    long atlasBytes = (long)surfaceType.GetProperty("AtlasMemoryBytes").GetValue(siteSurface);
                    double buildMs = (double)surfaceType.GetProperty("LastBuildMilliseconds").GetValue(siteSurface);
                    Assert.That(surfaceCount, Is.GreaterThan(0), label);
                    Assert.That(scannedCells, Is.GreaterThan(0), label);
                    Assert.That(atlasBytes, Is.GreaterThan(0).And.LessThanOrEqualTo(1024L * 1536L * 8L), label);
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
        finally { qa.GetMethod("ClearPoseOverride").Invoke(null, null); }
        yield return new ExitPlayMode();
        Assert.That(Resources.FindObjectsOfTypeAll<RenderTexture>().Any(t => t.name == "Indoor surface projection (local)"), Is.False);
    }

    private static string F(float value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

}
