using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class IndoorFogSurfacePrototypeEditorTests
{
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

}
