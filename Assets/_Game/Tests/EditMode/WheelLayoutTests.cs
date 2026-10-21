using System;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using VertigoWheel.Core.Wheels;
using VertigoWheel.Core.Randomness;
using VertigoWheel.Core.Runs;
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
                "Assets/_Game/Prefabs/UI/Wheel/ui_panel_wheel.prefab");
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

        [Test]
        public void BoundWheel_UsesProfileSpinColorWithoutChangingTheSharedMaterialOrButtonGeometry()
        {
            GameSettings game = AssetDatabase.LoadAssetAtPath<GameSettings>("Assets/_Game/Settings/GameSettings.asset");
            var rewards = AssetDatabase.FindAssets("t:RewardSettings", new[] { "Assets/_Game/Settings/Rewards" })
                .Select(id => AssetDatabase.LoadAssetAtPath<RewardSettings>(AssetDatabase.GUIDToAssetPath(id)))
                .ToDictionary(reward => reward.RewardId);
            var button = _view.transform.Find("ui_button_spin").GetComponent<UnityEngine.UI.Button>();
            Material shared = _prefab.transform.Find("ui_button_spin").GetComponent<UnityEngine.UI.Image>().material;
            Vector2 size = ((RectTransform)button.transform).sizeDelta;

            foreach (int zone in new[] { 1, 5, 30, 60 })
            {
                var run = new RunSession(game, new SystemRandomSource(14), new SystemRandomSource(37), zone);
                WheelSettings profile = game.GetWheelSettingsForZone(zone);
                _view.Bind(run.Wheel, profile, rewards, game.BombIcon);
                _view.SetSpinInteractable(true);

                Assert.That(button.image.color, Is.EqualTo(profile.SpinButtonColor), "Zone " + zone);
                Assert.That(button.image.material, Is.SameAs(shared), "Tinting must not create per-wheel materials.");
                Assert.That(shared.shader.name, Is.EqualTo("VertigoWheel/UI/SpinButton"));
                Assert.That(((RectTransform)button.transform).sizeDelta, Is.EqualTo(size));
                Assert.That(button.interactable, Is.True);
                Assert.That(button.onClick.GetPersistentEventCount(), Is.Zero);
            }
        }

        [TestCase(0f, true)]
        [TestCase(13f, false)]
        public void Slots_FaceOutwardAndInheritWheelRotationAfterResizing(float slotZeroAngle, bool clockwise)
        {
            GameSettings game = AssetDatabase.LoadAssetAtPath<GameSettings>("Assets/_Game/Settings/GameSettings.asset");
            WheelSettings profile = Object.Instantiate(_settings);
            try
            {
                profile.SlotZeroAngleDegrees = slotZeroAngle;
                profile.ClockwiseSlotOrder = clockwise;
                var run = new RunSession(game, new SystemRandomSource(14), new SystemRandomSource(37));
                _view.Bind(run.Wheel, profile, game.CreateRewardCatalog(), game.BombIcon);
                Transform rotationRoot = _view.transform.Find("ui_wheel_rotation");
                Transform button = _view.transform.Find("ui_button_spin");
                Quaternion buttonRotation = button.rotation;
                foreach (float rotation in new[] { 0f, 37f, -719f, 2430f })
                {
                    _view.SetRotation(rotation);
                    ((RectTransform)_view.transform).sizeDelta = new Vector2(500f, 500f);
                    Canvas.ForceUpdateCanvases();
                    for (int index = 0; index < WheelData.SlotCount; index++)
                    {
                        Transform slot = rotationRoot.Find("ui_slot_reward_" + index.ToString("00"));
                        float angle = slotZeroAngle + (clockwise ? -1 : 1) * index * 45f;
                        Quaternion expected = Quaternion.Euler(0, 0, angle);
                        Assert.That(Quaternion.Angle(slot.localRotation, expected), Is.LessThan(.01f));
                        Assert.That(Quaternion.Angle(slot.rotation, rotationRoot.rotation * expected), Is.LessThan(.01f));
                        Vector3 outward = (slot.position - rotationRoot.position).normalized;
                        Assert.That(Vector3.Dot(slot.up, outward), Is.GreaterThan(.999f));
                    }
                    Assert.That(Quaternion.Angle(button.rotation, buttonRotation), Is.LessThan(.01f),
                        "The center Spin button remains stationary.");
                }
            }
            finally { Object.DestroyImmediate(profile); }
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
                Assert.That(quantity.fontSizeMax,
                    Is.EqualTo(diameter * _settings.SlotSizeRatio * 0.19f).Within(0.01f));
            }

            RectTransform pointer = (RectTransform)_view.transform.Find("ui_panel_wheel_pointer");
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
