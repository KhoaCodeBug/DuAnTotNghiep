using NUnit.Framework;
using UnityEngine;

public sealed class EightWayDirectionTests
{
    private const float Tolerance = 0.0001f;

    [TestCase(0, "N", 0f, 1f)]
    [TestCase(1, "NE", 1f, 0.5f)]
    [TestCase(2, "E", 1f, 0f)]
    [TestCase(3, "SE", 1f, -0.5f)]
    [TestCase(4, "S", 0f, -1f)]
    [TestCase(5, "SW", -1f, -0.5f)]
    [TestCase(6, "W", -1f, 0f)]
    [TestCase(7, "NW", -1f, 0.5f)]
    public void IndexUsesClockwiseSpriteOrderAndIsometricRoadAxis(
        int index,
        string expectedLabel,
        float expectedX,
        float expectedY)
    {
        Vector2 actual = EightWayDirection.IndexToIsometricDirection(index);
        Vector2 expected = new Vector2(expectedX, expectedY).normalized;

        Assert.That(EightWayDirection.IndexToLabel(index), Is.EqualTo(expectedLabel));
        Assert.That(Vector2.Distance(actual, expected), Is.LessThan(Tolerance));
    }

    [TestCase(0f, 0)]
    [TestCase(22.4f, 0)]
    [TestCase(22.6f, 1)]
    [TestCase(89f, 2)]
    [TestCase(181f, 4)]
    [TestCase(315f, 7)]
    [TestCase(359f, 0)]
    public void HeadingSnapsToNearestClockwiseDirection(float heading, int expectedIndex)
    {
        Assert.That(EightWayDirection.HeadingDegreesToIndex(heading), Is.EqualTo(expectedIndex));
    }

    [Test]
    public void OppositeDirectionsAreFourIndicesApart()
    {
        for (int index = 0; index < EightWayDirection.Count; index++)
        {
            Vector2 direction = EightWayDirection.IndexToIsometricDirection(index);
            Vector2 opposite = EightWayDirection.IndexToIsometricDirection(index + 4);
            Assert.That(Vector2.Distance(direction, -opposite), Is.LessThan(Tolerance));
        }
    }
}
