using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using VertigoWheel.Core.Zones;
using VertigoWheel.Settings;
using VertigoWheel.UI.Board;
using Object = UnityEngine.Object;

namespace VertigoWheel.Tests.EditMode
{
    public sealed class ZoneTrackTests
    {
        private GameObject _instance;
        private ZoneTrackView _view;
        private ZoneTrackSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Prefabs/UI/Board/ui_panel_zone_track.prefab"));
            _view = _instance.GetComponent<ZoneTrackView>();
            _settings = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ZoneTrackSettings>(
                "Assets/_Game/Settings/UI/ZoneTrackSettings.asset"));
            typeof(ZoneTrackView).GetField("_settings", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_view, _settings);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_instance);
            Object.DestroyImmediate(_settings);
        }

        [TestCase(1)]
        [TestCase(3)]
        [TestCase(5)]
        [TestCase(7)]
        [TestCase(15)]
        [TestCase(29)]
        [TestCase(30)]
        [TestCase(int.MaxValue)]
        public void Window_KeepsCurrentCenteredAndHidesOutOfRangeNumbers(int current)
        {
            _view.Render(current, Rules());
            ZoneItemView[] items = _view.GetComponentsInChildren<ZoneItemView>(true);
            Assert.That(items.Length, Is.EqualTo(13));
            for (int index = 0; index < items.Length; index++)
            {
                long zone = (long)current - 6 + index;
                bool valid = zone >= 1 && zone <= int.MaxValue;
                Assert.That(items[index].gameObject.activeSelf, Is.EqualTo(valid));
                if (valid)
                {
                    Assert.That(items[index].GetComponentInChildren<TMP_Text>().text, Is.EqualTo(zone.ToString()));
                }
            }
            TMP_Text[] labels = Labels();
            Assert.That(labels.Count(t => t.fontStyle == FontStyles.Bold), Is.EqualTo(1));
            Assert.That(labels.Single(t => t.fontStyle == FontStyles.Bold).text, Is.EqualTo(current.ToString()));
            Assert.That(((RectTransform)items[6].transform).anchorMin.x, Is.EqualTo(6f / 13f).Within(.0001f));
            Assert.That(((RectTransform)items[6].transform).anchorMax.x, Is.EqualTo(7f / 13f).Within(.0001f));
        }

        [Test]
        public void Refresh_ReusesItemsAndUsesCurrentRulesWithSuperPriority()
        {
            int[] itemIds = _view.GetComponentsInChildren<ZoneItemView>(true).Select(v => v.GetInstanceID()).ToArray();
            _view.Render(29, Rules());
            TMP_Text[] labels = Labels();
            Assert.That(labels.Single(t => t.text == "30").color, Is.EqualTo(_settings.SuperColor));
            Assert.That(labels.Single(t => t.text == "25").color, Is.EqualTo(Completed(_settings.SafeColor)));

            _view.Render(5, new ZoneRules(3, 6, CashOutPolicy.AnyZone, 5, 1000000000));
            labels = Labels();
            Assert.That(labels.Single(t => t.text == "3").color, Is.EqualTo(Completed(_settings.SafeColor)));
            Assert.That(labels.Single(t => t.text == "6").color, Is.EqualTo(_settings.SuperColor));
            Assert.That(_view.GetComponentsInChildren<ZoneItemView>(true).Select(v => v.GetInstanceID()), Is.EqualTo(itemIds));
        }

        [Test]
        public void InvalidZone_DoesNotPartiallyRebindTrack()
        {
            _view.Render(1, Rules());
            string[] previous = Labels().Select(t => t.text).ToArray();
            Assert.Throws<ArgumentOutOfRangeException>(() => _view.Render(0, Rules()));
            Assert.That(Labels().Select(t => t.text), Is.EqualTo(previous));
        }

        [Test]
        public void IncompleteStyleEdit_KeepsLastValidAppearanceAndAllowsProgressionUntilCorrected()
        {
            _view.Render(29, Rules());
            Sprite background = _settings.BackgroundSprite;
            _settings.BackgroundSprite = null;
            _view.Render(30, Rules());
            TMP_Text[] labels = Labels();
            Assert.That(labels.Single(t => t.fontStyle == FontStyles.Bold).text, Is.EqualTo("30"));
            Assert.That(labels.Single(t => t.text == "30").color, Is.EqualTo(_settings.CurrentNumberColor));
            Assert.That(_view.GetComponentsInChildren<UnityEngine.UI.Image>().All(i => i.sprite != null), Is.True);

            _settings.BackgroundSprite = background;
            _settings.SafeColor = Color.cyan;
            _view.Render(29, Rules());
            Assert.That(labels.Single(t => t.text == "25").color, Is.EqualTo(Completed(Color.cyan)));
        }

        [TestCase(3, ZoneType.Normal)]
        [TestCase(5, ZoneType.Safe)]
        [TestCase(30, ZoneType.Super)]
        public void CurrentMarker_UsesZoneStyleAndCompletedNumbersAreDimmed(int current, ZoneType type)
        {
            _view.Render(current, Rules());
            var marker = _view.transform.Find("ui_animation_zone_marker/ui_image_zone_current_value").GetComponent<UnityEngine.UI.Image>();
            Sprite expected = type == ZoneType.Super ? _settings.SuperCurrentBackgroundSprite
                : type == ZoneType.Safe ? _settings.SafeCurrentBackgroundSprite : _settings.CurrentBackgroundSprite;
            Assert.That(marker.sprite, Is.SameAs(expected));
            Assert.That(marker.color, Is.EqualTo(type == ZoneType.Super ? _settings.SuperColor : Color.white));
            TMP_Text[] labels = Labels();
            Assert.That(labels.Single(t => t.text == (current - 1).ToString()).color.a,
                Is.EqualTo(_settings.NormalColor.a * _settings.CompletedOpacity));
            Assert.That(labels.Single(t => t.text == (current + 1).ToString()).color.a, Is.EqualTo(1f));
            if (type != ZoneType.Safe)
            {
                Assert.That(labels.Single(t => t.text == current.ToString()).color, Is.EqualTo(_settings.CurrentNumberColor));
            }
        }

        [Test]
        public void InitiallyInvalidStyle_IsReportedInsteadOfDisplayingAnUnconfiguredTrack()
        {
            _settings.BackgroundSprite = null;
            Assert.Throws<InvalidOperationException>(() => _view.Render(1, Rules()));
        }

        [Test]
        public void Motion_OnlyConsumesAConsecutiveAdvance_AndCadenceChangesSnap()
        {
            _view.Render(1, Rules());
            Assert.That(_view.AnimateAdvance(0f, 1f), Is.False);
            _view.Render(5, Rules());
            Assert.That(_view.AnimateAdvance(0f, 1f), Is.False);
            _view.Render(6, Rules());
            Assert.That(_view.AnimateAdvance(0f, 1f), Is.True);
            _view.Render(6, Rules());
            Assert.That(_view.AnimateAdvance(0f, 1f), Is.False);
            _view.Render(7, new ZoneRules(7, 30, CashOutPolicy.AnyZone, 5, 1000000000));
            Assert.That(_view.AnimateAdvance(0f, 1f), Is.False);
            _view.Render(1, Rules());
            Assert.That(_view.AnimateAdvance(0f, 1f), Is.False);
        }

        private static ZoneRules Rules() => new ZoneRules(5, 30, CashOutPolicy.AnyZone, 5, 1000000000);

        private TMP_Text[] Labels() => _view.GetComponentsInChildren<ZoneItemView>()
            .Select(item => item.GetComponentInChildren<TMP_Text>()).ToArray();

        private Color Completed(Color color)
        {
            color.a *= _settings.CompletedOpacity;
            return color;
        }
    }
}
