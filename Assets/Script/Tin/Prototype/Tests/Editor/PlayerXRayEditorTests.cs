using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class PlayerXRayEditorTests
{
    [TestCase("Assets/Prefab/Player.prefab")]
    [TestCase("Assets/Prefab/Player2.prefab")]
    public void PlayerPrefab_ReferencesDedicatedXRayMaterial(string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Assert.That(prefab, Is.Not.Null, prefabPath);

        MonoBehaviour vision = prefab.GetComponents<MonoBehaviour>()
            .FirstOrDefault(component => component != null && component.GetType().Name == "PlayerVision");
        Assert.That(vision, Is.Not.Null, $"{prefabPath} must contain PlayerVision on its root.");

        SerializedProperty materialProperty =
            new SerializedObject(vision).FindProperty("localPlayerXRayMaterial");
        Assert.That(materialProperty, Is.Not.Null);

        Material xrayMaterial = materialProperty.objectReferenceValue as Material;
        Assert.That(xrayMaterial, Is.Not.Null,
            $"{prefabPath} must keep a serialized X-Ray material so its shader is included in Player builds.");
        Assert.That(xrayMaterial.shader, Is.Not.Null);
        Assert.That(xrayMaterial.shader.name, Is.EqualTo("ProjectZomboid/LocalPlayerXRay"));
        Assert.That(xrayMaterial.shader.isSupported, Is.True,
            "The dedicated X-Ray shader must compile for the active render pipeline/platform.");
    }

    [TestCase("Assets/Prefab/Player.prefab")]
    [TestCase("Assets/Prefab/Player2.prefab")]
    public void PlayerOcclusionProbe_RequiresVisibleEnvironmentDrawnInFront(string prefabPath)
    {
        Scene originalScene = SceneManager.GetActiveScene();
        Scene testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

        try
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, prefabPath);

            GameObject player = Object.Instantiate(prefab, new Vector3(50000f, 50000f), Quaternion.identity);
            SceneManager.MoveGameObjectToScene(player, testScene);

            MonoBehaviour vision = player.GetComponents<MonoBehaviour>()
                .FirstOrDefault(component => component != null && component.GetType().Name == "PlayerVision");
            Assert.That(vision, Is.Not.Null);

            SpriteRenderer body = player.GetComponentsInChildren<SpriteRenderer>(true)
                .FirstOrDefault(renderer => renderer.gameObject.name == "Visual");
            Assert.That(body, Is.Not.Null);
            Assert.That(body.sprite, Is.Not.Null);

            MethodInfo occlusionProbe = vision.GetType().GetMethod(
                "IsLocalPlayerVisuallyOccluded",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(occlusionProbe, Is.Not.Null);

            Physics2D.SyncTransforms();
            Assert.That(InvokeOcclusionProbe(vision, occlusionProbe), Is.False,
                "An unobstructed Player must not show the X-Ray silhouette.");

            GameObject coverObject = new GameObject("XRay test environment cover");
            SceneManager.MoveGameObjectToScene(coverObject, testScene);
            coverObject.transform.position = body.bounds.center;

            SpriteRenderer coverRenderer = coverObject.AddComponent<SpriteRenderer>();
            coverRenderer.sprite = body.sprite;
            coverRenderer.sortingLayerName = "Foreground";
            coverRenderer.sortingOrder = body.sortingOrder + 1;

            BoxCollider2D coverCollider = coverObject.AddComponent<BoxCollider2D>();
            coverCollider.size = new Vector2(
                Mathf.Max(0.1f, body.bounds.size.x * 0.5f),
                Mathf.Max(0.1f, body.bounds.size.y * 0.5f));

            Physics2D.SyncTransforms();
            Assert.That(InvokeOcclusionProbe(vision, occlusionProbe), Is.True,
                "Visible environmental geometry drawn in front must activate the local X-Ray silhouette.");

            Color transparent = coverRenderer.color;
            transparent.a = 0f;
            coverRenderer.color = transparent;
            Assert.That(InvokeOcclusionProbe(vision, occlusionProbe), Is.False,
                "A fully transparent renderer must not count as visual cover.");

            coverRenderer.color = Color.white;
            coverRenderer.sortingLayerID = body.sortingLayerID;
            coverRenderer.sortingOrder = body.sortingOrder - 1;
            Assert.That(InvokeOcclusionProbe(vision, occlusionProbe), Is.False,
                "Geometry drawn behind the Player must not activate X-Ray.");
        }
        finally
        {
            if (originalScene.IsValid())
                SceneManager.SetActiveScene(originalScene);
            EditorSceneManager.CloseScene(testScene, true);
        }
    }

    [Test]
    public void XRayShader_DrawsUnlitAndIgnoresSceneDepth()
    {
        string shaderPath = Path.Combine(Application.dataPath, "Shader/LocalPlayerXRay.shader");
        string source = File.ReadAllText(shaderPath);

        Assert.That(source, Does.Contain("ZTest Always"));
        Assert.That(source, Does.Contain("ZWrite Off"));
        Assert.That(source, Does.Contain("Blend SrcAlpha OneMinusSrcAlpha"));
        Assert.That(source, Does.Contain("\"LightMode\"=\"Universal2D\""),
            "The X-Ray pass must be routed through the active URP 2D renderer.");
        Assert.That(source, Does.Not.Contain("CombinedShapeLightShared"),
            "The local silhouette must not depend on scene lighting or Indoor Fog light setup.");
    }

    private static bool InvokeOcclusionProbe(MonoBehaviour vision, MethodInfo probe)
    {
        return (bool)probe.Invoke(vision, null);
    }
}
