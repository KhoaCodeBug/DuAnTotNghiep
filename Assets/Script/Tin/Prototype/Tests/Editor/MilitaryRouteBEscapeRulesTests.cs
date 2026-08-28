using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class MilitaryRouteBEscapeRulesTests
{
    [Test]
    public void GateAccelerationAddsDamageWithoutReplacingNaturalDamage()
    {
        Assert.That(MilitaryStoryFlowRules.GetEscapeGateDamagePerSecond(5000f, 8f),
            Is.EqualTo(625f).Within(0.001f));
        Assert.That(MilitaryStoryFlowRules.GetSoloGateElapsedRate(false, 300f, 8f),
            Is.EqualTo(1f).Within(0.001f));
        Assert.That(MilitaryStoryFlowRules.GetSoloGateElapsedRate(true, 300f, 8f),
            Is.EqualTo(38.5f).Within(0.001f));
    }

    [Test]
    public void OutroCameraCurveStartsAtRestPeaksEarlyAndSettlesAtTheEnd()
    {
        Assert.That(MilitaryStoryFlowRules.EndingMapCameraTravelSeconds, Is.EqualTo(6f));
        Assert.That(MilitaryStoryFlowRules.EndingMapCameraHoldSeconds, Is.EqualTo(2f));
        Assert.That(MilitaryStoryFlowRules.EvaluateEndingMapCameraTravel(0f), Is.EqualTo(0f));
        Assert.That(MilitaryStoryFlowRules.EvaluateEndingMapCameraTravel(1f), Is.EqualTo(1f));

        const int samples = 100;
        float previous = 0f;
        float peakStep = 0f;
        int peakIndex = 0;
        float firstStep = 0f;
        float finalStep = 0f;
        for (int i = 1; i <= samples; i++)
        {
            float current = MilitaryStoryFlowRules.EvaluateEndingMapCameraTravel(i / (float)samples);
            float step = current - previous;
            Assert.That(step, Is.GreaterThanOrEqualTo(0f));
            if (i == 1) firstStep = step;
            if (i == samples) finalStep = step;
            if (step > peakStep)
            {
                peakStep = step;
                peakIndex = i;
            }
            previous = current;
        }

        Assert.That(peakIndex, Is.InRange(40, 47));
        Assert.That(firstStep, Is.LessThan(peakStep * 0.001f));
        Assert.That(finalStep, Is.LessThan(peakStep * 0.02f));
    }

    [Test]
    public void LargeSiegeHordeGetsContinuousDistinctGatePositions()
    {
        GameObject gateObject = new GameObject("Gate Distribution Test");
        try
        {
            System.Type gateType = System.Type.GetType("MilitaryGateController, Assembly-CSharp");
            Assert.That(gateType, Is.Not.Null);
            Component gate = gateObject.AddComponent(gateType);
            BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            gateType.GetField("gateCenter", fields)?.SetValue(gate, Vector2.zero);
            gateType.GetField("gateDirection", fields)?.SetValue(gate, Vector2.right);
            gateType.GetField("gateLength", fields)?.SetValue(gate, 4.4f);
            MethodInfo getPosition = gateType.GetMethod("GetAssaultPosition");
            Assert.That(getPosition, Is.Not.Null);

            HashSet<Vector2> positions = new HashSet<Vector2>();
            for (int stableId = 1; stableId <= 128; stableId++)
            {
                Vector2 position = (Vector2)getPosition.Invoke(gate,
                    new object[] { stableId, new Vector2(0f, 5f) });
                Assert.That(position.x, Is.InRange(-1.99f, 1.99f));
                Assert.That(position.y, Is.InRange(0.11f, 0.69f));
                positions.Add(position);
            }

            Assert.That(positions.Count, Is.EqualTo(128),
                "A large horde must not share the old 13 exact transform slots.");
        }
        finally
        {
            Object.DestroyImmediate(gateObject);
        }
    }

    [Test]
    public void DeadOrTransformingPlayerCannotReopenCombatWhenHealthIsTemporarilyPositive()
    {
        System.Type combatType = System.Type.GetType("PlayerCombat, Assembly-CSharp");
        MethodInfo terminalRule = combatType?.GetMethod("IsTerminalCombatState",
            BindingFlags.Public | BindingFlags.Static);
        Assert.That(terminalRule, Is.Not.Null);
        Assert.That(terminalRule.Invoke(null, new object[] { true, false, 100f }), Is.True);
        Assert.That(terminalRule.Invoke(null, new object[] { false, true, 100f }), Is.True);
        Assert.That(terminalRule.Invoke(null, new object[] { false, false, 0f }), Is.True);
        Assert.That(terminalRule.Invoke(null, new object[] { false, false, 1f }), Is.False);
    }

    [Test]
    public void TerminalPlayerSafetyDisablesEveryBodyCollider()
    {
        GameObject player = new GameObject("Terminal Player Safety Test");
        try
        {
            CircleCollider2D rootCollider = player.AddComponent<CircleCollider2D>();
            GameObject child = new GameObject("Child Collider");
            child.transform.SetParent(player.transform);
            BoxCollider2D childCollider = child.AddComponent<BoxCollider2D>();
            System.Type healthType = System.Type.GetType("PlayerHealth, Assembly-CSharp");
            Assert.That(healthType, Is.Not.Null);
            Component health = player.AddComponent(healthType);
            MethodInfo applySafety = healthType.GetMethod("ApplyTerminalLocalSafety",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(applySafety, Is.Not.Null);

            applySafety.Invoke(health, null);

            Assert.That(rootCollider.enabled, Is.False);
            Assert.That(childCollider.enabled, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(player);
        }
    }
}
