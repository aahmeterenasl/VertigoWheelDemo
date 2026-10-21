using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using VertigoWheel.Core.Randomness;
using VertigoWheel.Core.Rewards;
using VertigoWheel.Core.Runs;
using VertigoWheel.Settings;
using VertigoWheel.UI.Board;
using VertigoWheel.UI.Common;
using VertigoWheel.UI.Rewards;
using VertigoWheel.UI.Wheel;
using Object = UnityEngine.Object;

namespace VertigoWheel.Tests.EditMode
{
    public sealed class RewardCollectionTests
    {
        private GameObject _canvasRoot;
        private GameObject _templateRoot;
        private Texture2D _texture;
        private Sprite _icon;
        private RewardCollectionView _view;
        private UnityEngine.UI.ScrollRect _scroll;
        private RectTransform _content;
        private TMP_Text _emptyLabel;
        private WheelSettings _wheelProfile;
        private GameSettings _boardSettings;
        private ZoneSettings _boardZones;
        private readonly Dictionary<RewardId, RewardSettings> _catalog = new Dictionary<RewardId, RewardSettings>();

        [SetUp]
        public void SetUp()
        {
            _texture = new Texture2D(2, 2);
            _icon = Sprite.Create(_texture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f);
            foreach (RewardId id in Enum.GetValues(typeof(RewardId)))
            {
                if (id == RewardId.None)
                {
                    continue;
                }
                RewardSettings reward = ScriptableObject.CreateInstance<RewardSettings>();
                reward.RewardId = id;
                reward.DisplayName = id.ToString();
                reward.Icon = _icon;
                _catalog.Add(id, reward);
            }

            _canvasRoot = new GameObject("RewardCollectionTestCanvas", typeof(RectTransform), typeof(Canvas));
            GameObject scrollObject = Child("ui_scroll_rewards", _canvasRoot.transform);
            ((RectTransform)scrollObject.transform).sizeDelta = new Vector2(300f, 200f);
            _scroll = scrollObject.AddComponent<UnityEngine.UI.ScrollRect>();
            _scroll.horizontal = false;
            _scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            RectTransform viewport = (RectTransform)Child("ui_viewport_rewards", scrollObject.transform).transform;
            Stretch(viewport);
            viewport.gameObject.AddComponent<UnityEngine.UI.Image>();
            viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
            _content = (RectTransform)Child("ui_content_rewards", viewport).transform;
            _content.anchorMin = new Vector2(0, 1);
            _content.anchorMax = Vector2.one;
            _content.pivot = new Vector2(0.5f, 1);
            _content.sizeDelta = new Vector2(0, 0);
            UnityEngine.UI.VerticalLayoutGroup layout = _content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            _content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            _scroll.viewport = viewport;
            _scroll.content = _content;
            _emptyLabel = AddText("ui_text_rewards_empty", scrollObject.transform);

            _templateRoot = Child("RewardItemTemplate", null);
            _templateRoot.SetActive(false);
            _templateRoot.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 120;
            RewardItemView template = _templateRoot.AddComponent<RewardItemView>();
            SetField(template, "_icon", Child("ui_image_reward", _templateRoot.transform).AddComponent<UnityEngine.UI.Image>());
            SetField(template, "_name", AddText("ui_text_reward_name", _templateRoot.transform));
            SetField(template, "_quantity", AddText("ui_text_reward_quantity", _templateRoot.transform));

            _view = scrollObject.AddComponent<RewardCollectionView>();
            SetField(_view, "_scrollRect", _scroll);
            SetField(_view, "_content", _content);
            SetField(_view, "_itemPrefab", template);
            SetField(_view, "_emptyLabel", _emptyLabel);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_wheelProfile);
            Object.DestroyImmediate(_boardSettings);
            Object.DestroyImmediate(_boardZones);
            Object.DestroyImmediate(_canvasRoot);
            Object.DestroyImmediate(_templateRoot);
            foreach (RewardSettings reward in _catalog.Values)
            {
                Object.DestroyImmediate(reward);
            }
            _catalog.Clear();
            Object.DestroyImmediate(_icon);
            Object.DestroyImmediate(_texture);
        }

        [Test]
        public void Refresh_ReusesRowsUpdatesQuantityAndPreservesScroll()
        {
            RewardAmount[] rewards = AllRewards();
            _view.SetRewards(rewards, _catalog, true);
            Canvas.ForceUpdateCanvases();
            Assert.That(_content.rect.height, Is.GreaterThan(_scroll.viewport.rect.height));
            int firstItemId = _content.GetChild(0).GetInstanceID();
            int itemCount = _content.childCount;
            _scroll.verticalNormalizedPosition = 0.37f;
            rewards[0] = new RewardAmount(rewards[0].RewardId, 1234567);

            _view.SetRewards(rewards, _catalog);

            Assert.That(_content.childCount, Is.EqualTo(itemCount));
            Assert.That(_content.GetChild(0).GetInstanceID(), Is.EqualTo(firstItemId));
            Assert.That(QuantityAt(0), Is.EqualTo("x1.2M"));
            Assert.That(_scroll.verticalNormalizedPosition, Is.EqualTo(0.37f).Within(0.001f));
            Assert.That(_emptyLabel.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void EmptyAndRefill_ReusesExistingRowsAndResetStopsMomentum()
        {
            RewardAmount[] rewards = AllRewards();
            _view.SetRewards(rewards, _catalog, true);
            int firstItemId = _content.GetChild(0).GetInstanceID();
            int itemCount = _content.childCount;
            _scroll.verticalNormalizedPosition = 0.2f;
            _scroll.velocity = new Vector2(0, -500);
            _view.StopScrolling();
            Assert.That(_scroll.velocity, Is.EqualTo(Vector2.zero));

            _view.SetRewards(Array.Empty<RewardAmount>(), _catalog, true);
            Assert.That(_emptyLabel.gameObject.activeSelf, Is.True);
            for (int index = 0; index < _content.childCount; index++)
            {
                Assert.That(_content.GetChild(index).gameObject.activeSelf, Is.False);
            }
            _view.SetRewards(rewards, _catalog, true);

            Assert.That(_content.childCount, Is.EqualTo(itemCount));
            Assert.That(_content.GetChild(0).GetInstanceID(), Is.EqualTo(firstItemId));
            Assert.That(_scroll.verticalNormalizedPosition, Is.EqualTo(1f).Within(0.001f));
            Assert.That(_emptyLabel.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void InvalidCatalogEntry_DoesNotPartiallyUpdateOrCreateRows()
        {
            _view.SetRewards(new[] { new RewardAmount(RewardId.Gold, 10) }, _catalog);
            Dictionary<RewardId, RewardSettings> incompleteCatalog = new Dictionary<RewardId, RewardSettings>
            {
                { RewardId.Gold, _catalog[RewardId.Gold] }
            };

            Assert.Throws<InvalidOperationException>(() => _view.SetRewards(new[]
            {
                new RewardAmount(RewardId.Gold, 999),
                new RewardAmount(RewardId.Cash, 100)
            }, incompleteCatalog));

            Assert.That(_content.childCount, Is.EqualTo(1));
            Assert.That(QuantityAt(0), Is.EqualTo("x10"));
            Assert.That(_emptyLabel.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void InvalidItemBind_PreservesPreviouslyDisplayedReward()
        {
            _view.SetRewards(new[] { new RewardAmount(RewardId.Gold, 10) }, _catalog);
            RewardItemView item = _content.GetChild(0).GetComponent<RewardItemView>();
            Assert.Throws<ArgumentNullException>(() => item.Bind(null, 5));
            Assert.Throws<ArgumentOutOfRangeException>(() => item.Bind(_catalog[RewardId.Cash], 0));
            Assert.That(QuantityAt(0), Is.EqualTo("x10"));
            Assert.That(_content.GetChild(0).Find("ui_text_reward_name").GetComponent<TMP_Text>().text,
                Is.EqualTo("Gold"));
        }

        [TestCase("ui_panel_reward_row", "1,463", false)]
        [TestCase("ui_panel_reward_card", "x1,463", true)]
        public void AuthoredRewardItems_BindTheirOwnQuantityFormat(string prefabName, string expected, bool hasName)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Prefabs/UI/Rewards/" + prefabName + ".prefab");
            Assert.That(prefab, Is.Not.Null);
            GameObject instance = Object.Instantiate(prefab, _canvasRoot.transform, false);
            RewardItemView item = instance.GetComponent<RewardItemView>();

            item.Bind(_catalog[RewardId.Cash], 1463);

            Assert.That(instance.transform.Find("ui_animation_reward_content/" + "ui_text_reward_quantity_value").GetComponent<TMP_Text>().text,
                Is.EqualTo(expected));
            Transform name = instance.transform.Find("ui_animation_reward_content/ui_text_reward_name_value");
            Assert.That(name != null, Is.EqualTo(hasName));
            if (hasName)
            {
                Assert.That(name.GetComponent<TMP_Text>().text, Is.EqualTo("Cash"));
            }
        }

        [TestCase(29, "35", "30")]
        [TestCase(5, "10", "30")]
        [TestCase(30, "35", "60")]
        [TestCase(2147483640, "2147483645", "—")]
        [TestCase(int.MaxValue, "—", "—")]
        public void BoardRender_UsesPreparedZoneAndBlocksControlsAndMomentumBehindModal(
            int zoneNumber, string nextSafe, string nextSuper)
        {
            GameSettings settings = AssetDatabase.LoadAssetAtPath<GameSettings>("Assets/_Game/Settings/GameSettings.asset");
            GameObject wheelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/UI/Wheel/ui_panel_wheel.prefab");
            Assert.That(settings, Is.Not.Null);
            Assert.That(wheelPrefab, Is.Not.Null);
            // Developer Play-mode tuning of the shared asset must not change this test's policy.
            _boardSettings = Object.Instantiate(settings);
            _boardZones = Object.Instantiate(settings.ZoneSettings);
            _boardZones.CashOutPolicy = VertigoWheel.Core.Zones.CashOutPolicy.AnyZone;
            _boardSettings.ZoneSettings = _boardZones;
            settings = _boardSettings;
            WheelView wheel = Object.Instantiate(wheelPrefab, _canvasRoot.transform, false).GetComponent<WheelView>();
            GameObject boardObject = Child("ui_board", _canvasRoot.transform);
            CanvasGroup boardGroup = boardObject.AddComponent<CanvasGroup>();
            GameView board = boardObject.AddComponent<GameView>();
            ActionButtonView exit = Child("ui_button_exit", boardObject.transform).AddComponent<ActionButtonView>();
            TMP_Text zone = AddText("ui_text_zone", boardObject.transform);
            TMP_Text safe = AddText("ui_text_safe", boardObject.transform);
            TMP_Text super = AddText("ui_text_super", boardObject.transform);
            SetField(board, "_wheel", wheel);
            SetField(board, "_wheelAnimator", wheel.GetComponent<WheelAnimator>());
            SetField(board, "_exitButton", exit);
            SetField(board, "_rewards", _view);
            SetField(board, "_zoneLabel", zone);
            SetField(board, "_nextSafeLabel", safe);
            SetField(board, "_nextSuperLabel", super);
            TMP_Text status = AddText("ui_text_status", boardObject.transform);
            SetField(board, "_statusLabel", status);
            SetField(board, "_boardGroup", boardGroup);
            GameObject trackPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Prefabs/UI/Board/ui_panel_zone_track.prefab");
            Assert.That(trackPrefab, Is.Not.Null);
            SetField(board, "_zoneTrack", Object.Instantiate(trackPrefab, boardObject.transform, false).GetComponent<ZoneTrackView>());
            RunSession session = new RunSession(settings, new SystemRandomSource(12), new SystemRandomSource(34), zoneNumber);
            _wheelProfile = Object.Instantiate(settings.GetWheelSettingsForZone(zoneNumber));
            _wheelProfile.HeadingText = "CUSTOM HEADING";
            _wheelProfile.StatusText = "Custom wheel caption";
            wheel.Bind(session.Wheel, _wheelProfile, _catalog, settings.BombIcon);

            board.Render(session.CreateSnapshot(), settings.CreateZoneRules(), _catalog, false);
            Assert.That(zone.text, Is.EqualTo("CUSTOM HEADING"));
            Assert.That(zone.color, Is.EqualTo(settings.GetWheelSettingsForZone(zoneNumber).HeadingColor));
            Assert.That(status.text, Is.EqualTo("Custom wheel caption"));
            board.SetStatus("Temporary gameplay message");
            _wheelProfile.HeadingText = "LIVE HEADING";
            _wheelProfile.HeadingColor = Color.cyan;
            _wheelProfile.StatusText = "Live wheel caption";
            typeof(GameView).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(board, null);
            Assert.That(zone.text, Is.EqualTo("LIVE HEADING"));
            Assert.That(zone.color, Is.EqualTo(Color.cyan));
            Assert.That(status.text, Is.EqualTo("Temporary gameplay message"));
            board.SetStatus(string.Empty);
            Assert.That(status.text, Is.EqualTo("Live wheel caption"));
            _wheelProfile.StatusText = string.Empty;
            typeof(GameView).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(board, null);
            Assert.That(status.gameObject.activeSelf, Is.False);
            Assert.That(safe.text, Is.EqualTo(nextSafe), "Super zones replace safe zones.");
            Assert.That(super.text, Is.EqualTo(nextSuper));
            Assert.That(exit.GetComponent<UnityEngine.UI.Button>().interactable, Is.True);
            UnityEngine.UI.Button spin = wheel.transform.Find("ui_button_spin").GetComponent<UnityEngine.UI.Button>();
            Assert.That(spin.interactable, Is.True);
            _scroll.velocity = new Vector2(0, 400);

            board.Render(session.CreateSnapshot(), settings.CreateZoneRules(), _catalog, true);
            Assert.That(boardGroup.interactable, Is.False);
            Assert.That(boardGroup.blocksRaycasts, Is.False);
            Assert.That(spin.interactable, Is.False);
            Assert.That(exit.GetComponent<UnityEngine.UI.Button>().interactable, Is.False);
            Assert.That(_scroll.velocity, Is.EqualTo(Vector2.zero));
            Assert.That(_scroll.enabled, Is.False);

            Assert.That(session.TryStartSpin(out _), Is.True);
            board.Render(session.CreateSnapshot(), settings.CreateZoneRules(), _catalog, false);
            Assert.That(boardGroup.interactable, Is.True);
            Assert.That(_scroll.enabled, Is.True);
            Assert.That(spin.interactable, Is.False);
            Assert.That(exit.GetComponent<UnityEngine.UI.Button>().interactable, Is.False);
        }

        private RewardAmount[] AllRewards()
        {
            List<RewardAmount> rewards = new List<RewardAmount>();
            foreach (RewardId id in _catalog.Keys)
            {
                rewards.Add(new RewardAmount(id, 10));
            }
            return rewards.ToArray();
        }

        private string QuantityAt(int index)
        {
            return _content.GetChild(index).Find("ui_text_reward_quantity").GetComponent<TMP_Text>().text;
        }

        private static GameObject Child(string name, Transform parent)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child;
        }

        private static TMP_Text AddText(string name, Transform parent)
        {
            TMP_Text text = Child(name, parent).AddComponent<TextMeshProUGUI>();
            text.enableAutoSizing = false;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetField(object instance, string fieldName, object value)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(instance, value);
        }
    }
}
