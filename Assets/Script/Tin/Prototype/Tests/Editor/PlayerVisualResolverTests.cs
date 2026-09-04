using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class PlayerVisualResolverTests
{
    private const string PlayerPrefabPath = "Assets/Prefab/Player.prefab";
    private const string Player2PrefabPath = "Assets/Prefab/Player2.prefab";

    private static Type ResolverType =>
        Type.GetType("PlayerVisualResolver, Assembly-CSharp") ??
        throw new InvalidOperationException("PlayerVisualResolver was not found in Assembly-CSharp.");

    private static Animator InvokeResolveVisualAnimator(GameObject root)
    {
        MethodInfo method = ResolverType.GetMethod("ResolveVisualAnimator", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(method, "ResolveVisualAnimator method must exist.");
        return (Animator)method.Invoke(null, new object[] { root });
    }

    private static SpriteRenderer InvokeResolveVisualSpriteRenderer(GameObject root)
    {
        MethodInfo method = ResolverType.GetMethod("ResolveVisualSpriteRenderer", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(method, "ResolveVisualSpriteRenderer method must exist.");
        return (SpriteRenderer)method.Invoke(null, new object[] { root });
    }

    private static bool InvokeHasParameter(Animator anim, string name, AnimatorControllerParameterType? type = null)
    {
        MethodInfo method = ResolverType.GetMethod("HasParameter", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(method, "HasParameter method must exist.");
        return (bool)method.Invoke(null, new object[] { anim, name, type });
    }

    [Test]
    public void PlayerPrefab_ResolvesVisualAnimator_NotMuzzleFlash()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        Assert.IsNotNull(prefab, "Player.prefab should exist");

        Animator anim = InvokeResolveVisualAnimator(prefab);
        Assert.IsNotNull(anim, "Visual Animator should be resolved");
        Assert.AreEqual("Visual", anim.gameObject.name, "Resolved animator must belong to 'Visual'");
        Assert.AreNotEqual("MuzzleFlash", anim.gameObject.name, "Must not resolve MuzzleFlash animator");

        Assert.IsTrue(InvokeHasParameter(anim, "GunBash", AnimatorControllerParameterType.Trigger), "Must have GunBash trigger");
        Assert.IsTrue(InvokeHasParameter(anim, "RandomBash", AnimatorControllerParameterType.Int), "Must have RandomBash int");
        Assert.IsTrue(InvokeHasParameter(anim, "TakeDamage", AnimatorControllerParameterType.Trigger), "Must have TakeDamage trigger");
        Assert.IsTrue(InvokeHasParameter(anim, "IsDead", AnimatorControllerParameterType.Bool), "Must have IsDead bool");
    }

    [Test]
    public void Player2Prefab_ResolvesVisualAnimator_NotMuzzleFlash()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Player2PrefabPath);
        Assert.IsNotNull(prefab, "Player2.prefab should exist");

        Animator anim = InvokeResolveVisualAnimator(prefab);
        Assert.IsNotNull(anim, "Visual Animator should be resolved for Player2");
        Assert.AreEqual("Visual", anim.gameObject.name, "Resolved animator must belong to 'Visual'");
        Assert.AreNotEqual("MuzzleFlash", anim.gameObject.name, "Must not resolve MuzzleFlash animator");

        Assert.IsTrue(InvokeHasParameter(anim, "GunBash"), "Must have GunBash");
        Assert.IsTrue(InvokeHasParameter(anim, "RandomBash"), "Must have RandomBash");
        Assert.IsTrue(InvokeHasParameter(anim, "TakeDamage"), "Must have TakeDamage");
        Assert.IsTrue(InvokeHasParameter(anim, "IsDead"), "Must have IsDead");
    }

    [Test]
    public void PlayerPrefabs_ResolveVisualSpriteRenderer()
    {
        GameObject p1 = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        SpriteRenderer sr1 = InvokeResolveVisualSpriteRenderer(p1);
        Assert.IsNotNull(sr1);
        Assert.AreEqual("Visual", sr1.gameObject.name);

        GameObject p2 = AssetDatabase.LoadAssetAtPath<GameObject>(Player2PrefabPath);
        SpriteRenderer sr2 = InvokeResolveVisualSpriteRenderer(p2);
        Assert.IsNotNull(sr2);
        Assert.AreEqual("Visual", sr2.gameObject.name);
    }
}
