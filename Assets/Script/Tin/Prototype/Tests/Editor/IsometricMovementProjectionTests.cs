using NUnit.Framework;
using UnityEngine;

public sealed class IsometricMovementProjectionTests
{
    private const float Tolerance = 0.0001f;

    [Test]
    public void ZeroInputRemainsZero()
    {
        Assert.That(IsometricMovementProjection.ProjectInput(Vector2.zero), Is.EqualTo(Vector2.zero));
    }

    [TestCase(1f, 0f, 1f, 0f)]
    [TestCase(-1f, 0f, -1f, 0f)]
    [TestCase(0f, 1f, 0f, 1f)]
    [TestCase(0f, -1f, 0f, -1f)]
    public void CardinalInputKeepsItsScreenDirection(
        float inputX,
        float inputY,
        float expectedX,
        float expectedY)
    {
        Vector2 projected = IsometricMovementProjection.ProjectInput(new Vector2(inputX, inputY));

        Assert.That(projected.x, Is.EqualTo(expectedX).Within(Tolerance));
        Assert.That(projected.y, Is.EqualTo(expectedY).Within(Tolerance));
        Assert.That(projected.magnitude, Is.EqualTo(1f).Within(Tolerance));
    }

    [TestCase(1f, 1f, 1f, 0.5f)]
    [TestCase(-1f, 1f, -1f, 0.5f)]
    [TestCase(1f, -1f, 1f, -0.5f)]
    [TestCase(-1f, -1f, -1f, -0.5f)]
    public void DiagonalInputFollowsTwoToOneIsometricRoadAxis(
        float inputX,
        float inputY,
        float expectedX,
        float expectedY)
    {
        Vector2 raw = new Vector2(inputX, inputY).normalized;
        Vector2 projected = IsometricMovementProjection.ProjectInput(raw);
        Vector2 expected = new Vector2(expectedX, expectedY).normalized;

        Assert.That(Vector2.Distance(projected, expected), Is.LessThan(Tolerance));
        Assert.That(projected.magnitude, Is.EqualTo(1f).Within(Tolerance));
    }

    [Test]
    public void AnalogInputPreservesMagnitudeAfterProjection()
    {
        Vector2 input = new Vector2(0.35f, 0.35f);
        Vector2 projected = IsometricMovementProjection.ProjectInput(input);

        Assert.That(projected.magnitude, Is.EqualTo(input.magnitude).Within(Tolerance));
        Assert.That(Mathf.Abs(projected.y / projected.x), Is.EqualTo(0.5f).Within(Tolerance));
    }
}
