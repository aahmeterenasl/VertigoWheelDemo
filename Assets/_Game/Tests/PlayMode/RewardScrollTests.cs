using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VertigoWheel.App;
using VertigoWheel.Core.Randomness;
using VertigoWheel.Core.Rewards;
using VertigoWheel.Core.Runs;
using VertigoWheel.Core.Wheels;
using VertigoWheel.Core.Zones;
using VertigoWheel.Settings;
using VertigoWheel.UI.Board;
using VertigoWheel.UI.Common;
using VertigoWheel.UI.Dialogs;
using VertigoWheel.UI.Rewards;
using Object = UnityEngine.Object;

namespace VertigoWheel.Tests.PlayMode
{
    public sealed class RewardScrollTests
    {
        private Scene _scene;
        private GameView _board;
        private RewardCollectionView _collection;
        private UnityEngine.UI.ScrollRect _scroll;
        private Transform _exit;
        private GameBootstrap _bootstrap;
        private Texture2D _texture;
        private Sprite _icon;
        private readonly Dictionary<RewardId, RewardSettings> _catalog = new Dictionary<RewardId, RewardSettings>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Additive);
            _scene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            Assert.That(_scene.name, Is.EqualTo("Game"));
            GameObject[] roots = _scene.GetRootGameObjects();
            _bootstrap = roots.SelectMany(root => root.GetComponentsInChildren<GameBootstrap>(true)).Single();
            _bootstrap.enabled = false;
            _board = roots.SelectMany(root => root.GetComponentsInChildren<GameView>(true)).Single();
            Transform ledgerScroll = _board.transform.Find("ui_panel_run_rewards/ui_scroll_run_rewards");
            Assert.That(ledgerScroll, Is.Not.Null, "The authored board needs its scrolling reward ledger.");
            _collection = ledgerScroll.GetComponent<RewardCollectionView>();
            _scroll = ledgerScroll.GetComponent<UnityEngine.UI.ScrollRect>();
            Assert.That(_collection, Is.Not.Null);
            Assert.That(_scroll, Is.Not.Null);
            _exit = _board.GetComponentsInChildren<ActionButtonView>(true)
                .Single(button => button.name == "ui_button_exit").transform;

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
            _board.SetBoardBlocked(false);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_scene.IsValid() && _scene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            foreach (RewardSettings reward in _catalog.Values)
            {
                Object.Destroy(reward);
            }
            _catalog.Clear();
            if (_icon != null)
            {
                Object.Destroy(_icon);
            }
            if (_texture != null)
            {
                Object.Destroy(_texture);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator FullCatalog_ScrollsReusesRowsAndKeepsExitOutsideMovingContent()
        {
            RewardAmount[] rewards = _catalog.Keys.Select(id => new RewardAmount(id, 10)).ToArray();
            _collection.SetRewards(rewards, _catalog, true);
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(_scroll.content.rect.height, Is.GreaterThan(_scroll.viewport.rect.height));
            Assert.That(_exit.IsChildOf(_scroll.content), Is.False, "Exit must remain fixed above the scroll area.");
            Vector3 exitPosition = _exit.position;
            int firstItemId = _scroll.content.GetChild(0).GetInstanceID();
            float initialScroll = _scroll.verticalNormalizedPosition;
            PointerEventData pointer = new PointerEventData(EventSystem.current)
            {
                scrollDelta = new Vector2(0, -12)
            };

            _scroll.OnScroll(pointer);
            yield return null;
            Assert.That(_scroll.verticalNormalizedPosition, Is.LessThan(initialScroll - 0.001f));
            float movedPosition = _scroll.verticalNormalizedPosition;
            rewards[0] = new RewardAmount(rewards[0].RewardId, 99);
            _collection.SetRewards(rewards, _catalog);
            yield return null;

            Assert.That(_scroll.content.childCount, Is.EqualTo(rewards.Length));
            Assert.That(_scroll.content.GetChild(0).GetInstanceID(), Is.EqualTo(firstItemId));
            Assert.That(_scroll.verticalNormalizedPosition, Is.EqualTo(movedPosition).Within(0.001f));
            Assert.That(Vector3.Distance(_exit.position, exitPosition), Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator BombPanel_ScrollsLostRewardsWhileBoardStaysBlocked_AndClearsOnNextShow()
        {
            var bomb = _scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BombResultView>(true)).Single();
            var animation = ScriptableObject.CreateInstance<AnimationSettings>();
            animation.DialogDurationSeconds = 0;
            animation.BombEntranceSeconds = 0;
            try
            {
                _board.SetBoardBlocked(true);
                bomb.Show(_icon, _catalog.Keys.Select(id => new RewardAmount(id, 10)).ToArray(), _catalog, animation);
                yield return null;
                Canvas.ForceUpdateCanvases();
                var list = bomb.GetComponentInChildren<RewardCollectionView>();
                var scroll = list.GetComponent<UnityEngine.UI.ScrollRect>();
                Assert.That(scroll.enabled, Is.True);
                Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
                Assert.That(bomb.GetComponentsInChildren<ActionButtonView>().Any(b => b.name == "ui_button_exit"), Is.False);
                Assert.That(_scroll.enabled, Is.False);
                Assert.That(_exit.GetComponent<UnityEngine.UI.Button>().IsInteractable(), Is.False);
                float before = scroll.verticalNormalizedPosition;
                scroll.OnScroll(new PointerEventData(EventSystem.current) {scrollDelta = new Vector2(0, -12)});
                yield return null;
                Assert.That(scroll.verticalNormalizedPosition, Is.LessThan(before - .001f));
                bomb.Hide();
                Assert.That(scroll.enabled, Is.False);
                Assert.That(scroll.velocity, Is.EqualTo(Vector2.zero));
                bomb.Show(_icon, Array.Empty<RewardAmount>(), _catalog, animation);
                Assert.That(list.GetComponentsInChildren<RewardItemView>().Length, Is.Zero,
                    "A later empty run must not show the previous run's rewards.");
            }
            finally
            {
                bomb.Hide();
                Object.Destroy(animation);
            }
        }

        [UnityTest]
        public IEnumerator BombEntrance_HideReopenAndResize_RestoreTheAuthoredVisuals()
        {
            var bomb = _scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BombResultView>(true)).Single();
            var animation = ScriptableObject.CreateInstance<AnimationSettings>();
            animation.BombEntranceSeconds = 0.08f;
            try
            {
                var motions = bomb.GetComponentsInChildren<UiMotionView>(true);
                var root = (RectTransform)bomb.transform;
                Vector2 size = root.sizeDelta;
                bomb.Show(_icon, _catalog.Keys.Take(2).Select(id => new RewardAmount(id, 10)).ToArray(), _catalog, animation);
                root.sizeDelta += Vector2.right * 24f;
                yield return new WaitForSecondsRealtime(0.15f);
                Assert.That(bomb.IsReady, Is.True);
                Assert.That(motions.All(m => m.transform.localScale == Vector3.one), Is.True);
                root.sizeDelta = size;
                var tint = bomb.transform.Find("ui_image_bomb_backdrop").GetComponent<UnityEngine.UI.Image>().color;
                Assert.That(tint.a, Is.GreaterThan(0f).And.LessThan(1f));
                animation.BombEntranceSeconds = 0.15f;
                bomb.Show(_icon, Array.Empty<RewardAmount>(), _catalog, animation);
                bomb.Hide();
                Assert.That(motions.All(m => m.transform.localScale == Vector3.one), Is.True);
                animation.BombEntranceSeconds = 0f;
                bomb.Show(_icon, Array.Empty<RewardAmount>(), _catalog, animation);
                yield return new WaitForSecondsRealtime(0.2f);
                Assert.That(bomb.IsReady, Is.True);
                Assert.That(bomb.GetComponentsInChildren<RewardItemView>().Length, Is.Zero);
            }
            finally { bomb.Hide(); Object.Destroy(animation); }
        }

        [UnityTest]
        public IEnumerator CashOutSummary_ScrollsCardsKeepsRestartFixed_AndResetsForLaterRuns()
        {
            CashOutSummaryView summary = _scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CashOutSummaryView>(true)).Single();
            AnimationSettings animation = ScriptableObject.CreateInstance<AnimationSettings>();
            animation.DialogDurationSeconds = 0.05f;
            RewardId[] ids = _catalog.Keys.ToArray();
            RunSummary collected = CreateSummary(ids, ids.Length);
            try
            {
                _board.SetBoardBlocked(true);
                summary.Show(collected, _catalog, animation);
                RewardCollectionView list = summary.GetComponentInChildren<RewardCollectionView>();
                UnityEngine.UI.ScrollRect scroll = list.GetComponent<UnityEngine.UI.ScrollRect>();
                Transform restart = summary.GetComponentsInChildren<ActionButtonView>()
                    .Single(button => button.name == "ui_button_restart").transform;
                Assert.That(summary.IsReady, Is.False);
                Assert.That(scroll.enabled, Is.False, "Cards must wait for the dialog entrance before accepting a drag.");
                yield return new WaitForSecondsRealtime(0.15f);
                Canvas.ForceUpdateCanvases();

                Assert.That(summary.IsReady, Is.True);
                Assert.That(scroll.enabled, Is.True);
                Assert.That(scroll.horizontal, Is.True);
                Assert.That(scroll.vertical, Is.False);
                Assert.That(scroll.content.rect.width, Is.GreaterThan(scroll.viewport.rect.width));
                Assert.That(restart.IsChildOf(scroll.content), Is.False, "Restart must remain outside the moving cards.");
                Assert.That(_scroll.enabled, Is.False, "The gameplay ledger remains blocked beneath the summary.");
                Vector3 restartPosition = restart.position;
                int firstItemId = scroll.content.GetChild(0).GetInstanceID();
                int pooledCount = scroll.content.childCount;
                float before = scroll.horizontalNormalizedPosition;
                scroll.OnScroll(new PointerEventData(EventSystem.current) { scrollDelta = new Vector2(-120, 0) });
                yield return null;
                Assert.That(scroll.horizontalNormalizedPosition, Is.GreaterThan(before + .001f));
                Assert.That(Vector3.Distance(restart.position, restartPosition), Is.LessThan(.01f));

                float movedPosition = scroll.horizontalNormalizedPosition;
                summary.Show(collected, _catalog, animation);
                yield return new WaitForSecondsRealtime(0.15f);
                Assert.That(scroll.horizontalNormalizedPosition, Is.EqualTo(movedPosition).Within(.001f),
                    "Refreshing an open terminal dialog must preserve the cards being inspected.");

                scroll.velocity = new Vector2(600, 0);
                summary.Hide();
                Assert.That(scroll.enabled, Is.False);
                Assert.That(scroll.velocity, Is.EqualTo(Vector2.zero));
                animation.DialogDurationSeconds = 0;
                summary.Show(CreateSummary(ids, 2), _catalog, animation);
                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(list.GetComponentsInChildren<RewardItemView>().Length, Is.EqualTo(2));
                Assert.That(scroll.content.childCount, Is.EqualTo(pooledCount));
                Assert.That(scroll.content.GetChild(0).GetInstanceID(), Is.EqualTo(firstItemId));
                Assert.That(scroll.horizontalNormalizedPosition, Is.EqualTo(0f).Within(.001f));

                summary.Hide();
                summary.Show(CreateSummary(ids, 0), _catalog, animation);
                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(list.GetComponentsInChildren<RewardItemView>().Length, Is.Zero,
                    "A later empty cash-out must not display cards from a previous run.");
                Assert.That(scroll.content.childCount, Is.EqualTo(pooledCount));
                Assert.That(scroll.horizontalNormalizedPosition, Is.EqualTo(0f).Within(.001f));
            }
            finally
            {
                summary.Hide();
                Object.Destroy(animation);
            }
        }

        [UnityTest]
        public IEnumerator OpeningModal_CancelsCapturedDragAndMomentumUntilBoardUnblocks()
        {
            RewardAmount[] rewards = _catalog.Keys.Select(id => new RewardAmount(id, 10)).ToArray();
            _collection.SetRewards(rewards, _catalog, true);
            yield return null;
            Canvas.ForceUpdateCanvases();
            _scroll.verticalNormalizedPosition = 0.5f;
            Vector2 screenCenter = RectTransformUtility.WorldToScreenPoint(null, _scroll.viewport.position);
            PointerEventData pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = screenCenter
            };
            _scroll.OnBeginDrag(pointer);
            _scroll.velocity = new Vector2(0, 600);

            _board.SetBoardBlocked(true);
            Vector2 blockedContentPosition = _scroll.content.anchoredPosition;
            pointer.position += Vector2.up * 100;
            _scroll.OnDrag(pointer);
            yield return null;

            Assert.That(_scroll.enabled, Is.False);
            Assert.That(_scroll.velocity, Is.EqualTo(Vector2.zero));
            Assert.That(Vector2.Distance(_scroll.content.anchoredPosition, blockedContentPosition), Is.LessThan(0.01f));
            Assert.That(_exit.GetComponent<UnityEngine.UI.Button>().IsInteractable(), Is.False);

            _board.SetBoardBlocked(false);
            Assert.That(_scroll.enabled, Is.True);
            pointer.scrollDelta = new Vector2(0, -10);
            _scroll.OnScroll(pointer);
            Assert.That(Vector2.Distance(_scroll.content.anchoredPosition, blockedContentPosition), Is.GreaterThan(0.01f));
        }

        private static RunSummary CreateSummary(RewardId[] ids, int spinCount)
        {
            RunSession run = new RunSession(new SummarySettingsSource(ids), new ZeroRandomSource(), new ZeroRandomSource());
            for (int index = 0; index < spinCount; index++)
            {
                Assert.That(run.TryStartSpin(out SpinTicket ticket), Is.True);
                Assert.That(run.TryResolveSpin(ticket), Is.True);
                Assert.That(run.TryCompleteResolution(ticket), Is.True);
            }
            Assert.That(run.TryCashOut(out RunSummary summary), Is.True);
            return summary;
        }

        private sealed class ZeroRandomSource : IRandomSource
        {
            public double NextDouble() => 0;
        }

        private sealed class SummarySettingsSource : IRunSettingsSource
        {
            private readonly RewardId[] _ids;

            public SummarySettingsSource(RewardId[] ids) => _ids = ids;

            public ZoneRules CreateZoneRules() => new ZoneRules(5, 30, CashOutPolicy.AnyZone, 5, long.MaxValue);

            public WheelData CreateWheelData(int zoneNumber, IRandomSource random)
            {
                ZoneType type = CreateZoneRules().GetZoneType(zoneNumber);
                WheelSlotData[] slots = new WheelSlotData[WheelData.SlotCount];
                for (int index = 0; index < slots.Length; index++)
                {
                    slots[index] = type == ZoneType.Normal && index == slots.Length - 1
                        ? WheelSlotData.CreateBomb(1)
                        : WheelSlotData.CreateReward(_ids[(zoneNumber - 1 + index) % _ids.Length], 10, 1);
                }
                return new WheelData(zoneNumber, type, slots);
            }
        }
    }
}
