using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class RouteBCarAndTradeRegressionTests
{
    private const string PlayerPrefabPath = "Assets/Prefab/Player.prefab";

    [Test]
    public void PlayerInteraction_UsesVisualChildForVehiclePresentation()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        Assert.IsNotNull(prefab, $"Player prefab must exist at {PlayerPrefabPath}");

        Assert.IsNotNull(prefab.transform.Find("Visual"), "Player prefab must include a Visual child.");

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string sourcePath = Path.Combine(projectRoot, "Assets", "Hau", "Script", "PlayerInteraction.cs");
        string source = File.ReadAllText(sourcePath);

        StringAssert.Contains("PlayerVisualResolver.ResolveVisualSpriteRenderer(gameObject)", source,
            "Vehicle presentation must resolve the character renderer from Visual.");
        StringAssert.Contains("PlayerVisualResolver.ResolveVisualAnimator(gameObject)", source,
            "Vehicle presentation must resolve the character Animator from Visual.");
    }

    [Test]
    public void PlayerTrade_HasSharedDistanceRuleForBothPeers()
    {
        System.Type tradeType = System.Type.GetType("PlayerTrade, Assembly-CSharp");
        Assert.IsNotNull(tradeType, "PlayerTrade must be available in Assembly-CSharp.");
        MethodInfo method = tradeType.GetMethod(
            "IsWithinTradeRadius",
            BindingFlags.Public | BindingFlags.Static);

        Assert.IsNotNull(method,
            "PlayerTrade must expose one shared distance rule used by local target selection and state-authority validation.");

        bool within = (bool)method.Invoke(null, new object[]
        {
            new Vector2(0f, 0f), 2f,
            new Vector2(1.9f, 0f), 2f
        });
        bool outside = (bool)method.Invoke(null, new object[]
        {
            new Vector2(0f, 0f), 2f,
            new Vector2(2.1f, 0f), 2f
        });

        Assert.IsTrue(within, "Players inside the smaller configured radius must be eligible.");
        Assert.IsFalse(outside, "Players outside the smaller configured radius must be rejected.");
    }

}
