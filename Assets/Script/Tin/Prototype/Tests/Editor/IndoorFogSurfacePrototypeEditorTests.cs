using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class IndoorFogSurfacePrototypeEditorTests
{
    private const string Folder = "QA_Artifacts/IndoorFogPrototype_20260831";
    private static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    [TestCase("continuous-wall", 0)]
    [TestCase("two-silhouettes", 2)]
    [TestCase("overflow", 0)]
    public void ShadowEdges_GradeSilhouettesNotWholeWall_AndOverflowConservatively(string scenario, int expected)
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
                    scenario == "two-silhouettes" && i >= 20 && i < 60 ? 15f : 5f;
            float[] original = (float[])rays.Clone();
            type.GetMethod("BuildIndoorShadowEdges", Private).Invoke(fog, new object[] { rays.Length });
            Assert.That((int)type.GetField("indoorShadowEdgeCount", Private).GetValue(fog), Is.EqualTo(expected));
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
    public IEnumerator SurfaceProjection_RealSolo_CachesAtlasAndStaysInsideOptInHouse()
    {
        Directory.CreateDirectory(Folder);
        string posePath = Folder + "/pose.json";
        string previousPose = File.Exists(posePath) ? File.ReadAllText(posePath) : null;
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        yield return new EnterPlayMode(false);
        Type qa = Type.GetType("IndoorFogPrototypeQA, Assembly-CSharp-Editor");
        Type movement = Type.GetType("PlayerMovement, Assembly-CSharp");
        Type fogType = Type.GetType("FogVisionController, Assembly-CSharp");
        Type surfaceType = Type.GetType("IndoorFogSurfaceMap, Assembly-CSharp");
        Type readiness = Type.GetType("GameplayReadinessCoordinator, Assembly-CSharp");
        Assert.That(qa, Is.Not.Null);
        qa.GetMethod("StartSolo").Invoke(null, null);
        float deadline = Time.realtimeSinceStartup + 75;
        while (Time.realtimeSinceStartup < deadline &&
            (movement.GetField("LocalPlayerInstance").GetValue(null) == null ||
             !(bool)readiness.GetProperty("IsReleasedToGameplay").GetValue(null))) yield return null;
        Assert.That(movement.GetField("LocalPlayerInstance").GetValue(null), Is.Not.Null);
        Assert.That((bool)readiness.GetProperty("IsReleasedToGameplay").GetValue(null), Is.True);

        try
        {
            RenderTexture cached = null;
            string[] names = { "v3-baseline-day-on", "v3-day-on", "v3-night-off", "v3-day-off", "v3-night-on", "v3-look-right", "v3-look-left", "v3-outdoor", "v3-other-house", "v3-return", "v3-disabled" };
            for (int i = 0; i < names.Length; i++)
            {
                bool active = i != 0 && i != 10;
                bool torch = i != 2 && i != 3;
                float hour = i == 2 || i == 4 ? 21 : 13.5f;
                float x = i == 7 ? -62.29f : i == 8 ? -68 : -39.2f;
                float y = i == 7 ? 30.77f : i == 8 ? -0.7f : 44.3f;
                int dx = i == 5 ? 1 : i == 6 ? -1 : 0;
                int dy = dx == 0 ? 1 : 0;
                names[i] = names[i].Replace("v3-", "shadow-regression-");
                string pose = "{\"house\":\"nhachinhxaydautien (12)\",\"flashlightConeFeather\":0.2,\"flashlightBoundaryFadeDistance\":0.65,\"x\":" + F(x) + ",\"y\":" + F(y) +
                    ",\"hour\":" + F(hour) + ",\"directionX\":" + dx + ",\"directionY\":" + dy +
                    ",\"zoom\":3,\"cameraUp\":1.2,\"flashlight\":" + torch.ToString().ToLowerInvariant() +
                    ",\"prototype\":" + active.ToString().ToLowerInvariant() + ",\"label\":\"" + names[i] + "\"}";
                File.WriteAllText(posePath, pose);
                qa.GetMethod("ApplyPose").Invoke(null, null);
                yield return new WaitForSecondsRealtime(0.4f); // Hotbar equip must publish before its first toggle.
                qa.GetMethod("ApplyPose").Invoke(null, null);
                yield return new WaitForSecondsRealtime(1f);
                Component fog = fogType.GetProperty("Instance").GetValue(null) as Component;
                Assert.That(fog, Is.Not.Null);
                var material = (Material)fogType.GetField("overlayMaterial", Private).GetValue(fog);
                bool expectedSurface = active && i != 7 && i != 8;
                Assert.That(material.GetFloat("_IndoorSurfaceActive"), Is.EqualTo(expectedSurface ? 1 : 0), names[i]);
                Assert.That(material.GetFloat("_IndoorActive"), Is.EqualTo(i == 7 ? 0 : 1), names[i]);
                Assert.That(material.GetFloat("_FlashlightActive"), Is.EqualTo(torch ? 1 : 0), names[i]);
                Assert.That(material.GetFloat("_IndoorFlashlightBoundaryFade"),
                    Is.EqualTo(expectedSurface ? 0.65f : 0f).Within(0.01f), names[i]);
                if (!expectedSurface || !torch)
                    Assert.That(material.GetFloat("_IndoorShadowEdgeCount"), Is.Zero,
                        "No shadow grading may survive OFF, another house, outside or disable.");
                else
                    Assert.That(material.GetFloat("_IndoorShadowEdgeCount"), Is.GreaterThan(0f),
                        "The real sample must actually exercise shadow-edge grading, not pass with it disabled.");
                Assert.That(material.shader.isSupported, Is.True);
                var localPlayer = (Component)movement.GetField("LocalPlayerInstance").GetValue(null);
                Component vision = localPlayer.GetComponent("PlayerVision");
                object light = vision.GetType().GetField("playerLight").GetValue(vision);
                float innerAngle = (float)light.GetType().GetProperty("pointLightInnerAngle").GetValue(light);
                float outerAngle = (float)light.GetType().GetProperty("pointLightOuterAngle").GetValue(light);
                float sightAngle = (float)vision.GetType().GetProperty("CurrentVisionAngle").GetValue(vision);
                Assert.That(outerAngle, Is.EqualTo(torch ? 145f : 140f).Within(0.1f), "Outer beam must not expand.");
                Assert.That(sightAngle, Is.EqualTo(torch ? 145f : 140f), "Gameplay sight angle stays unchanged.");
                Assert.That(innerAngle, Is.EqualTo(torch ? 105f : 100f).Within(0.1f),
                    "Keep the accepted bright core, including lit wall and decor in the sample house.");
                if (expectedSurface)
                {
                    var atlas = material.GetTexture("_IndoorSurfaceAtlas") as RenderTexture;
                    Assert.That(atlas != null && atlas.IsCreated(), Is.True);
                    if (cached == null) { cached = atlas; AssertAtlasHasSurfaceAndTransparentPixels(atlas); }
                    else Assert.That(atlas, Is.SameAs(cached), "No atlas rebuild for look, light, time or re-entry.");
                }
                qa.GetMethod("Capture").Invoke(null, null);
                yield return new WaitForSecondsRealtime(0.4f);
                Assert.That(File.Exists(Folder + "/" + names[i] + ".png"), Is.True);
            }
            Assert.That(cached == null || !cached.IsCreated(), Is.True, "Disabling the opt-in component must release its GPU allocation.");
        }
        finally
        {
            if (previousPose != null) File.WriteAllText(posePath, previousPose);
        }
        yield return new ExitPlayMode();
        Assert.That(Resources.FindObjectsOfTypeAll<RenderTexture>().Any(t => t.name == "Indoor surface projection (local)"), Is.False,
            "No transient surface atlas may survive ExitPlayMode.");
    }

    private static string F(float value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static void AssertAtlasHasSurfaceAndTransparentPixels(RenderTexture atlas)
    {
        RenderTexture previous = RenderTexture.active;
        var texture = new Texture2D(atlas.width, atlas.height, TextureFormat.RGBAFloat, false, true);
        try
        {
            RenderTexture.active = atlas;
            texture.ReadPixels(new Rect(0, 0, atlas.width, atlas.height), 0, 0);
            texture.Apply();
            Color[] pixels = texture.GetPixels();
            Assert.That(pixels.Count(p => p.a > 0.5f), Is.GreaterThan(1000), "GPU atlas must contain actual sprite surfaces.");
            Assert.That(pixels.Count(p => p.a < 0.5f), Is.GreaterThan(1000), "Transparent area must not clear arbitrary room rectangles.");
            Assert.That(pixels.Where(p => p.a > 0.5f).All(p => p.r >= 0 && p.r <= 1 && p.g >= 0 && p.g <= 1), Is.True);
        }
        finally { RenderTexture.active = previous; UnityEngine.Object.Destroy(texture); }
    }
}
