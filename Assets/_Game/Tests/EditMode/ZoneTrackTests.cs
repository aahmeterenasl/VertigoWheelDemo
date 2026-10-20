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
                "Assets/_Game/Prefabs/UI/Board/ZoneTrackView.prefab"));
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

        [TestCase(1, 1)]
        [TestCase(5, 1)]
        [TestCase(29, 25)]
        [TestCase(30, 26)]
        [TestCase(int.MaxValue, int.MaxValue - 8)]
        public void Window_ContainsCurrentZoneWithoutInvalidNumbers(int current, int first)
        {
            _view.Render(current, Rules());
            TMP_Text[] labels = _view.GetComponentsInChildren<TMP_Text>();
            Assert.That(labels.Length, Is.EqualTo(9));
            for (int index = 0; index < labels.Length; index++)
            {
                Assert.That(labels[index].text, Is.EqualTo(((long)first + index).ToString()));
            }
            Assert.That(labels.Count(t => t.fontStyle == FontStyles.Bold), Is.EqualTo(1));
            Assert.That(labels.Single(t => t.fontStyle == FontStyles.Bold).text, Is.EqualTo(current.ToString()));
        }

        [Test]
        public void Refresh_ReusesItemsAndUsesCurrentRulesWithSuperPriority()
        {
            int[] itemIds = _view.GetComponentsInChildren<ZoneItemView>().Select(v => v.GetInstanceID()).ToArray();
            _view.Render(29, Rules());
            TMP_Text[] labels = _view.GetComponentsInChildren<TMP_Text>();
            Assert.That(labels.Single(t => t.text == "30").color, Is.EqualTo(_settings.SuperColor));
            Assert.That(labels.Single(t => t.text == "25").color, Is.EqualTo(_settings.SafeColor));

            _view.Render(5, new ZoneRules(3, 6, CashOutPolicy.AnyZone, 5, 1000000000));
            labels = _view.GetComponentsInChildren<TMP_Text>();
            Assert.That(labels.Single(t => t.text == "3").color, Is.EqualTo(_settings.SafeColor));
            Assert.That(labels.Single(t => t.text == "6").color, Is.EqualTo(_settings.SuperColor));
            Assert.That(_view.GetComponentsInChildren<ZoneItemView>().Select(v => v.GetInstanceID()), Is.EqualTo(itemIds));
        }

        [Test]
        public void InvalidZone_DoesNotPartiallyRebindTrack()
        {
            _view.Render(1, Rules());
            string[] previous = _view.GetComponentsInChildren<TMP_Text>().Select(t => t.text).ToArray();
            Assert.Throws<ArgumentOutOfRangeException>(() => _view.Render(0, Rules()));
            Assert.That(_view.GetComponentsInChildren<TMP_Text>().Select(t => t.text), Is.EqualTo(previous));
        }

        [Test]
        public void IncompleteStyleEdit_KeepsLastValidAppearanceAndAllowsProgressionUntilCorrected()
        {
            _view.Render(29, Rules());
            Sprite background = _settings.BackgroundSprite;
            _settings.BackgroundSprite = null;
            _view.Render(30, Rules());
            TMP_Text[] labels = _view.GetComponentsInChildren<TMP_Text>();
            Assert.That(labels.Single(t => t.fontStyle == FontStyles.Bold).text, Is.EqualTo("30"));
            Assert.That(labels.Single(t => t.text == "30").color, Is.EqualTo(_settings.SuperColor));
            Assert.That(_view.GetComponentsInChildren<UnityEngine.UI.Image>().All(i => i.sprite != null), Is.True);

            _settings.BackgroundSprite = background;
            _settings.SafeColor = Color.cyan;
            _view.Render(29, Rules());
            Assert.That(labels.Single(t => t.text == "25").color, Is.EqualTo(Color.cyan));
        }

        [Test]
        public void InitiallyInvalidStyle_IsReportedInsteadOfDisplayingAnUnconfiguredTrack()
        {
            _settings.BackgroundSprite = null;
            Assert.Throws<InvalidOperationException>(() => _view.Render(1, Rules()));
        }

        private static ZoneRules Rules() => new ZoneRules(5, 30, CashOutPolicy.AnyZone, 5, 1000000000);
    }
}
