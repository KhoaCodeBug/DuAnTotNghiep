using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Deterministic resolver for character Visual Animator components on player prefabs.
/// Avoids grabbing non-character animators such as MuzzleFlash or accessory animators.
/// </summary>
public static class PlayerVisualResolver
{
    private static readonly HashSet<string> ExcludedNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
    {
        "MuzzleFlash",
        "Muzzle",
        "Flash"
    };

    public static Animator ResolveVisualAnimator(GameObject root)
    {
        if (root == null) return null;

        // 1. Prioritize child transform named "Visual"
        Transform visualTransform = root.transform.Find("Visual");
        if (visualTransform != null)
        {
            Animator visualAnim = visualTransform.GetComponent<Animator>();
            if (visualAnim != null && IsValidCharacterAnimator(visualAnim))
            {
                return visualAnim;
            }
        }

        // 2. Scan all children animators, filtering out excluded names & muzzle controllers
        Animator[] allAnimators = root.GetComponentsInChildren<Animator>(true);
        foreach (Animator candidate in allAnimators)
        {
            if (candidate == null) continue;
            if (ExcludedNames.Contains(candidate.gameObject.name)) continue;

            RuntimeAnimatorController rac = candidate.runtimeAnimatorController;
            if (rac != null && rac.name.IndexOf("Muzzle", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            if (IsValidCharacterAnimator(candidate))
            {
                return candidate;
            }
        }

        // 3. Fallback: return the first non-muzzle animator, or visual transform animator if exists
        if (visualTransform != null)
        {
            Animator fallback = visualTransform.GetComponent<Animator>();
            if (fallback != null) return fallback;
        }

        foreach (Animator candidate in allAnimators)
        {
            if (candidate != null && !ExcludedNames.Contains(candidate.gameObject.name))
            {
                return candidate;
            }
        }

        return root.GetComponentInChildren<Animator>();
    }

    public static SpriteRenderer ResolveVisualSpriteRenderer(GameObject root)
    {
        if (root == null) return null;

        Transform visualTransform = root.transform.Find("Visual");
        if (visualTransform != null)
        {
            SpriteRenderer visualSr = visualTransform.GetComponent<SpriteRenderer>();
            if (visualSr != null) return visualSr;
        }

        SpriteRenderer[] allRenderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer candidate in allRenderers)
        {
            if (candidate == null) continue;
            if (ExcludedNames.Contains(candidate.gameObject.name)) continue;
            return candidate;
        }

        return root.GetComponentInChildren<SpriteRenderer>();
    }

    public static bool IsValidCharacterAnimator(Animator animator)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return false;
        // Verify key parameters exist
        return HasParameter(animator, "IsDead") || HasParameter(animator, "GunBash");
    }

    public static bool HasParameter(Animator animator, string parameterName, AnimatorControllerParameterType? expectedType = null)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return false;

        RuntimeAnimatorController rac = animator.runtimeAnimatorController;
        if (rac is AnimatorOverrideController aoc && aoc.runtimeAnimatorController != null)
        {
            rac = aoc.runtimeAnimatorController;
        }

        #if UNITY_EDITOR
        if (rac is UnityEditor.Animations.AnimatorController ac)
        {
            foreach (var param in ac.parameters)
            {
                if (param.name == parameterName)
                {
                    if (expectedType.HasValue && param.type != expectedType.Value) return false;
                    return true;
                }
            }
        }
        #endif

        if (animator.parameters != null)
        {
            foreach (var param in animator.parameters)
            {
                if (param.name == parameterName)
                {
                    if (expectedType.HasValue && param.type != expectedType.Value) return false;
                    return true;
                }
            }
        }
        return false;
    }

    public static bool SafeTrigger(Animator animator, string triggerName)
    {
        if (animator == null)
        {
            #if UNITY_EDITOR
            Debug.LogWarning($"[PlayerVisualResolver] Cannot trigger '{triggerName}': Animator is null.");
            #endif
            return false;
        }

        if (!HasParameter(animator, triggerName, AnimatorControllerParameterType.Trigger))
        {
            #if UNITY_EDITOR
            Debug.LogWarning($"[PlayerVisualResolver] Animator '{animator.name}' (controller: {animator.runtimeAnimatorController?.name}) lacks Trigger parameter '{triggerName}'.");
            #endif
            return false;
        }

        animator.SetTrigger(triggerName);
        return true;
    }

    public static bool SafeSetInteger(Animator animator, string paramName, int value)
    {
        if (animator == null)
        {
            #if UNITY_EDITOR
            Debug.LogWarning($"[PlayerVisualResolver] Cannot set int '{paramName}': Animator is null.");
            #endif
            return false;
        }

        if (!HasParameter(animator, paramName, AnimatorControllerParameterType.Int))
        {
            #if UNITY_EDITOR
            Debug.LogWarning($"[PlayerVisualResolver] Animator '{animator.name}' lacks Int parameter '{paramName}'.");
            #endif
            return false;
        }

        animator.SetInteger(paramName, value);
        return true;
    }

    public static bool SafeSetBool(Animator animator, string paramName, bool value)
    {
        if (animator == null)
        {
            #if UNITY_EDITOR
            Debug.LogWarning($"[PlayerVisualResolver] Cannot set bool '{paramName}': Animator is null.");
            #endif
            return false;
        }

        if (!HasParameter(animator, paramName, AnimatorControllerParameterType.Bool))
        {
            #if UNITY_EDITOR
            Debug.LogWarning($"[PlayerVisualResolver] Animator '{animator.name}' lacks Bool parameter '{paramName}'.");
            #endif
            return false;
        }

        animator.SetBool(paramName, value);
        return true;
    }
}
