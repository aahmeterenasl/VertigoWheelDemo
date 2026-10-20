using NUnit.Framework;
using UnityEngine;
using VertigoWheel.UI.Common;

namespace VertigoWheel.Tests.EditMode
{
    public sealed class SafeAreaTests
    {
        [TestCase(2400, 1080)]
        [TestCase(1920, 1080)]
        [TestCase(1440, 1080)]
        public void FullDisplay_HasNoInset(int width, int height)
        {
            Assert.That(SafeAreaView.CalculateNormalizedArea(new Rect(0, 0, width, height),
                new Vector2Int(width, height)), Is.EqualTo(new Rect(0, 0, 1, 1)));
        }

        [TestCase(96, 24, 2208, 1008, .04f, .96f)]
        [TestCase(120, 24, 2232, 1008, .05f, .98f)]
        [TestCase(48, 24, 2232, 1008, .02f, .95f)]
        public void Insets_RespectAsymmetricAndMirroredLandscapeCutouts(float x, float y, float width,
            float height, float left, float right)
        {
            Rect result = SafeAreaView.CalculateNormalizedArea(new Rect(x, y, width, height), new Vector2Int(2400, 1080));
            Assert.That(result.xMin, Is.EqualTo(left).Within(.0001f));
            Assert.That(result.xMax, Is.EqualTo(right).Within(.0001f));
            Assert.That(result.yMin, Is.EqualTo(24f / 1080).Within(.0001f));
            Assert.That(result.yMax, Is.EqualTo(1032f / 1080).Within(.0001f));
        }

        [Test]
        public void OversizedArea_IsClampedToDisplay()
        {
            Assert.That(SafeAreaView.CalculateNormalizedArea(new Rect(-20, -10, 2440, 1100),
                new Vector2Int(2400, 1080)), Is.EqualTo(new Rect(0, 0, 1, 1)));
        }

        [TestCase(0, 0, 0, 0)]
        [TestCase(0, 0, -10, 100)]
        [TestCase(2500, 0, 100, 100)]
        [TestCase(float.NaN, 0, 100, 100)]
        [TestCase(0, 0, float.PositiveInfinity, 100)]
        public void TransientInvalidArea_LeavesContentVisible(float x, float y, float width, float height)
        {
            Assert.That(SafeAreaView.CalculateNormalizedArea(new Rect(x, y, width, height),
                new Vector2Int(2400, 1080)), Is.EqualTo(new Rect(0, 0, 1, 1)));
        }

        [Test]
        public void DisplayNotReady_LeavesContentVisible()
        {
            Assert.That(SafeAreaView.CalculateNormalizedArea(new Rect(0, 0, 100, 100),
                Vector2Int.zero), Is.EqualTo(new Rect(0, 0, 1, 1)));
        }
    }
}
