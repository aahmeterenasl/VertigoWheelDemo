using System;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using VertigoWheel.Core.Wheels;
using VertigoWheel.Settings;
using VertigoWheel.UI.Wheel;
using Object = UnityEngine.Object;

namespace VertigoWheel.Tests.EditMode
{
    public sealed class WheelLayoutTests
    {
        private GameObject _canvasRoot;
        private GameObject _prefab;
        private WheelSettings _settings;
        private WheelView _view;

        [SetUp]
        public void SetUp()
        {
            _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Prefabs/UI/Wheel/WheelView.prefab");
            _settings = AssetDatabase.LoadAssetAtPath<WheelSettings>(
                "Assets/_Game/Settings/Wheels/BronzeWheelSettings.asset");
            Assert.That(_prefab, Is.Not.Null);
            Assert.That(_settings, Is.Not.Null);
            _canvasRoot = new GameObject("WheelLayoutTestCanvas", typeof(RectTransform), typeof(Canvas));
            _canvasRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            CreateUnboundView();
        }

        [TearDown]
        public void TearDown()
        {
            if (_canvasRoot != null)
            {
                Object.DestroyImmediate(_canvasRoot);
            }
            _canvasRoot = null;
            _view = null;
        }

        [Test]
        public void UnboundPrefab_ResizesSlotsPointerButtonAndTextFromAuthoredGeometry()
        {
            ResizeAndAssert(new Vector2(960f, 960f));
            ResizeAndAssert(new Vector2(640f, 640f));
        }

        [Test]
        public void UnboundPrefab_UsesTheShorterDimensionWithoutStretchingItsLayout()
        {
            ResizeAndAssert(new Vector2(800f, 500f));
            ResizeAndAssert(new Vector2(500f, 800f));
        }

        [Test]
        public void UnboundPrefab_RebuildsLayoutAfterReenableAndFreshInstantiation()
        {
            _view.gameObject.SetActive(false);
            ((RectTransform)_view.transform).sizeDelta = new Vector2(420f, 420f);
            _view.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            AssertLayout(420f);

            Object.DestroyImmediate(_view.gameObject);
            CreateUnboundView();
            ResizeAndAssert(new Vector2(580f, 580f));
        }

        private void CreateUnboundView()
        {
            GameObject instance = Object.Instantiate(_prefab, _canvasRoot.transform, false);
            _view = instance.GetComponent<WheelView>();
            Assert.That(_view, Is.Not.Null);
            Assert.That(_view.BoundWheel, Is.Null,
                "Editor layout must work before gameplay binds a prepared wheel.");
        }

        private void ResizeAndAssert(Vector2 dimensions)
        {
            RectTransform root = (RectTransform)_view.transform;
            root.sizeDelta = dimensions;
            root.ForceUpdateRectTransforms();
            Canvas.ForceUpdateCanvases();
            AssertLayout(Mathf.Min(dimensions.x, dimensions.y));
        }

        private void AssertLayout(float diameter)
        {
            Assert.That(_view.BoundWheel, Is.Null);
            Transform rotationRoot = _view.transform.Find("ui_wheel_rotation");
            Assert.That(rotationRoot, Is.Not.Null);
            for (int index = 0; index < WheelData.SlotCount; index++)
            {
                Transform slot = rotationRoot.Find("ui_slot_reward_" + index.ToString("00"));
                Assert.That(slot, Is.Not.Null);
                RectTransform slotRect = (RectTransform)slot;
                double angle = _settings.SlotZeroAngleDegrees
                    + (_settings.ClockwiseSlotOrder ? -1 : 1) * index * 360.0 / WheelData.SlotCount;
                AssertVector(slotRect.anchoredPosition,
                    PositionAt(angle, diameter * _settings.SlotRadiusRatio), "Slot " + index + " position");
                AssertVector(slotRect.sizeDelta, Vector2.one * diameter * _settings.SlotSizeRatio,
                    "Slot " + index + " size");
                TMP_Text quantity = slot.GetComponentInChildren<TMP_Text>(true);
                Assert.That(quantity, Is.Not.Null);
                Assert.That(quantity.fontSize,
                    Is.EqualTo(diameter * _settings.SlotSizeRatio * 0.19f).Within(0.01f));
            }

            RectTransform pointer = (RectTransform)_view.transform.Find("ui_image_wheel_pointer_value");
            Assert.That(pointer, Is.Not.Null);
            AssertVector(pointer.anchoredPosition, PositionAt(_settings.PointerAngleDegrees, diameter * 0.43f),
                "Pointer position");
            AssertVector(pointer.sizeDelta, new Vector2(diameter * 0.085f, diameter * 0.13f), "Pointer size");
            RectTransform spin = (RectTransform)_view.transform.Find("ui_button_spin");
            Assert.That(spin, Is.Not.Null);
            AssertVector(spin.sizeDelta, Vector2.one * diameter * 0.28f, "Spin button size");
        }

        private static Vector2 PositionAt(double degrees, float radius)
        {
            double radians = degrees * Math.PI / 180.0;
            return new Vector2(-(float)Math.Sin(radians), (float)Math.Cos(radians)) * radius;
        }

        private static void AssertVector(Vector2 actual, Vector2 expected, string description)
        {
            Assert.That(Vector2.Distance(actual, expected), Is.LessThan(0.01f), description);
        }
    }
}
