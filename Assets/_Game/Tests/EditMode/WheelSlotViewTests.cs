using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VertigoWheel.UI.Wheel;
using Object = UnityEngine.Object;

namespace VertigoWheel.Tests.EditMode
{
    public sealed class WheelSlotViewTests
    {
        private WheelSlotView _view;
        private RectTransform _icon;
        private TMP_Text _quantity;

        [SetUp]
        public void SetUp()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Prefabs/UI/Wheel/ui_panel_wheel_slot.prefab");
            Assert.That(prefab, Is.Not.Null);
            _view = Object.Instantiate(prefab).GetComponent<WheelSlotView>();
            _icon = _view.GetComponentInChildren<Image>(true).rectTransform;
            _quantity = _view.GetComponentInChildren<TMP_Text>(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (_view != null)
            {
                Object.DestroyImmediate(_view.gameObject);
            }
        }

        [Test]
        public void BindBomb_CentersIconAndHidesQuantityWithoutChangingIconSize()
        {
            Vector2 originalSize = _icon.rect.size;
            float originalAnchorMinX = _icon.anchorMin.x;
            float originalAnchorMaxX = _icon.anchorMax.x;

            _view.Bind(null, 0, true);

            AssertBombLayout(_view);
            AssertVector(_icon.rect.size, originalSize);
            Assert.That(_icon.anchorMin.x, Is.EqualTo(originalAnchorMinX));
            Assert.That(_icon.anchorMax.x, Is.EqualTo(originalAnchorMaxX));
        }

        [Test]
        public void BindRewardAfterBomb_RestoresAuthoredIconAndQuantityLayout()
        {
            Vector2 originalAnchorMin = _icon.anchorMin;
            Vector2 originalAnchorMax = _icon.anchorMax;
            Vector2 originalPosition = _icon.anchoredPosition;
            Vector2 originalSizeDelta = _icon.sizeDelta;

            _view.Bind(null, 0, true);
            _view.Bind(null, 125, false);

            AssertVector(_icon.anchorMin, originalAnchorMin);
            AssertVector(_icon.anchorMax, originalAnchorMax);
            AssertVector(_icon.anchoredPosition, originalPosition);
            AssertVector(_icon.sizeDelta, originalSizeDelta);
            Assert.That(_quantity.gameObject.activeSelf, Is.True);
            Assert.That(_quantity.text, Is.EqualTo("x125"));
        }

        [TestCase(64f)]
        [TestCase(256f)]
        public void SetLayout_BombRemainsCenteredAtDifferentSlotSizes(float size)
        {
            _view.Bind(null, 0, true);

            _view.SetLayout(new Vector2(90f, -40f), size, 45f);
            ((RectTransform)_view.transform).ForceUpdateRectTransforms();

            AssertBombLayout(_view);
            Assert.That(_icon.rect.height, Is.EqualTo(size * 0.48f).Within(0.01f));
        }

        [Test]
        public void Reenable_RestoresBombLayoutFromSerializedState()
        {
            _view.Bind(null, 0, true);
            _view.gameObject.SetActive(false);
            ResetToRewardLayout(_view);

            _view.gameObject.SetActive(true);

            AssertBombLayout(_view);
        }

        [Test]
        public void FreshInstance_RestoresBombLayoutWithoutAnotherBind()
        {
            _view.Bind(null, 0, true);
            _view.gameObject.SetActive(false);
            ResetToRewardLayout(_view);
            WheelSlotView copy = Object.Instantiate(_view);
            try
            {
                copy.gameObject.SetActive(true);

                AssertBombLayout(copy);
            }
            finally
            {
                Object.DestroyImmediate(copy.gameObject);
            }
        }

        [TestCase(64f, 1L)]
        [TestCase(129f, 100000L)]
        [TestCase(256f, long.MaxValue)]
        [TestCase(64f, 999999L)]
        [TestCase(129f, 99999999999999999L)]
        [TestCase(256f, 999999999999999999L)]
        public void RewardContent_FitsInsideCircularOpeningIncludingLargeQuantities(float size, long amount)
        {
            var canvas = new GameObject("SlotFitTestCanvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            try
            {
                _view.transform.SetParent(canvas.transform, false);
                _view.Bind(null, amount, false);
                _view.SetLayout(Vector2.zero, size, 45f);
                Canvas.ForceUpdateCanvases();
                _quantity.ForceMeshUpdate();

                Assert.That(_quantity.enableAutoSizing, Is.True);
                Assert.That(_quantity.isTextOverflowing, Is.False);
                foreach (RectTransform content in new[] { _icon, _quantity.rectTransform })
                {
                    // Test visible text, rather than the unused corners of its rectangular box.
                    Bounds bounds = content == _quantity.rectTransform
                        ? _quantity.textBounds : new Bounds(content.rect.center, content.rect.size);
                    foreach (Vector3 corner in new[]
                    {
                        new Vector3(bounds.min.x, bounds.min.y), new Vector3(bounds.min.x, bounds.max.y),
                        new Vector3(bounds.max.x, bounds.min.y), new Vector3(bounds.max.x, bounds.max.y)
                    })
                    {
                        Vector3 local = _view.transform.InverseTransformPoint(content.TransformPoint(corner));
                        Assert.That(local.magnitude, Is.LessThanOrEqualTo(size * .45f + .01f), content.name);
                    }
                    Assert.That(_view.transform.InverseTransformPoint(content.TransformPoint(content.rect.center)).x,
                        Is.EqualTo(0).Within(.01f), "Content must be horizontally centered.");
                }
            }
            finally
            {
                _view.transform.SetParent(null, false);
                Object.DestroyImmediate(canvas);
            }
        }

        private static void ResetToRewardLayout(WheelSlotView view)
        {
            RectTransform icon = view.GetComponentInChildren<Image>(true).rectTransform;
            icon.anchorMin = new Vector2(icon.anchorMin.x, 0.335f);
            icon.anchorMax = new Vector2(icon.anchorMax.x, 0.815f);
            view.GetComponentInChildren<TMP_Text>(true).gameObject.SetActive(true);
        }

        private static void AssertBombLayout(WheelSlotView view)
        {
            RectTransform icon = view.GetComponentInChildren<Image>(true).rectTransform;
            Vector3 localCenter = view.transform.InverseTransformPoint(icon.TransformPoint(icon.rect.center));
            AssertVector(localCenter, Vector2.zero);
            Assert.That(view.GetComponentInChildren<TMP_Text>(true).gameObject.activeSelf, Is.False);
        }

        private static void AssertVector(Vector2 actual, Vector2 expected)
        {
            Assert.That(Vector2.Distance(actual, expected), Is.LessThan(0.01f));
        }
    }
}
