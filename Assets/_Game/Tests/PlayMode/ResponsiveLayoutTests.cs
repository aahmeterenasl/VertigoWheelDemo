using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
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
    public sealed class ResponsiveLayoutTests
    {
        private Scene _scene;
        private Canvas _canvas;
        private GameView _board;
        private CashOutSummaryView _summary;
        private AnimationSettings _animation;
        private readonly Dictionary<RewardId, RewardSettings> _catalog = new Dictionary<RewardId, RewardSettings>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Additive);
            _scene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            GameObject[] roots = _scene.GetRootGameObjects();
            GameBootstrap bootstrap = Find<GameBootstrap>(roots);
            bootstrap.enabled = false;
            _canvas = Find<Canvas>(roots);
            _board = Find<GameView>(roots);
            _summary = Find<CashOutSummaryView>(roots);
            foreach (SafeAreaView safeArea in _canvas.GetComponentsInChildren<SafeAreaView>(true))
            {
                safeArea.enabled = false;
            }
            // Drive the authored hierarchy at deterministic logical sizes without changing the Editor Game view.
            _canvas.GetComponent<UnityEngine.UI.CanvasScaler>().enabled = false;
            _canvas.renderMode = RenderMode.WorldSpace;
            Sprite icon = _board.Wheel.GetComponentsInChildren<UnityEngine.UI.Image>().First(i => i.sprite != null).sprite;
            foreach (RewardId id in Enum.GetValues(typeof(RewardId)))
            {
                if (id == RewardId.None) continue;
                RewardSettings reward = ScriptableObject.CreateInstance<RewardSettings>();
                reward.RewardId = id;
                reward.DisplayName = id == RewardId.Gold ? "Submachine Gun Upgrade Points" : id.ToString();
                reward.Icon = icon;
                _catalog.Add(id, reward);
            }
            _animation = ScriptableObject.CreateInstance<AnimationSettings>();
            _animation.DialogDurationSeconds = 0;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_scene.IsValid() && _scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene);
            foreach (RewardSettings reward in _catalog.Values) Object.Destroy(reward);
            _catalog.Clear();
            Object.Destroy(_animation);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AuthoredLayouts_FitThreeAspectsAndBothCutoutSides_WithFullLedgerAndLargeTotals()
        {
            RewardAmount[] rewards = _catalog.Keys.Select(id => new RewardAmount(id, long.MaxValue)).ToArray();
            RunSummary collected = CreateFullSummary(_catalog.Keys.ToArray());
            RewardCollectionView ledger = _board.GetComponentInChildren<RewardCollectionView>();
            ledger.SetRewards(rewards, _catalog, true);
            _summary.Show(collected, _catalog, _animation);
            SafeAreaView[] safeAreas = _canvas.GetComponentsInChildren<SafeAreaView>(true);
            Assert.That(safeAreas.Length, Is.EqualTo(5), "Board and all four dialogs need safe-area containers.");

            foreach (Vector2Int size in new[] { new Vector2Int(2400, 1080), new Vector2Int(1920, 1080), new Vector2Int(1440, 1080) })
            {
                float scale = Mathf.Min(size.x / 1920f, size.y / 1080f);
                RectTransform canvasRect = (RectTransform)_canvas.transform;
                canvasRect.sizeDelta = new Vector2(size.x / scale, size.y / scale);
                foreach (float leftInset in new[] { 0f, .05f, .02f })
                {
                    float rightInset = leftInset == .05f ? .02f : leftInset == .02f ? .05f : 0f;
                    Rect safePixels = new Rect(size.x * leftInset, size.y * .025f,
                        size.x * (1f - leftInset - rightInset), size.y * .935f);
                    Rect anchors = SafeAreaView.CalculateNormalizedArea(safePixels, size);
                    foreach (SafeAreaView safeArea in safeAreas)
                    {
                        RectTransform rect = (RectTransform)safeArea.transform;
                        rect.anchorMin = anchors.min;
                        rect.anchorMax = anchors.max;
                        rect.offsetMin = Vector2.zero;
                        rect.offsetMax = Vector2.zero;
                    }
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    yield return null;
                    Canvas.ForceUpdateCanvases();

                    RectTransform boardSafe = (RectTransform)_board.transform.parent;
                    foreach (string name in new[] { "ui_panel_run_rewards", "ui_panel_zone_track", "ui_panel_zone_header", "ui_panel_wheel", "ui_panel_next_zones" })
                        AssertInside((RectTransform)_board.transform.Find(name), boardSafe);
                    RectTransform wheel = (RectTransform)_board.Wheel.transform;
                    Assert.That(wheel.rect.width, Is.EqualTo(wheel.rect.height).Within(.1f), "Wheel must stay square.");
                    Assert.That(Bounds((RectTransform)_board.transform.Find("ui_panel_zone_header")).yMin,
                        Is.GreaterThanOrEqualTo(Bounds(wheel).yMax - .1f), "Zone header must not overlap the wheel.");
                    foreach (DialogView dialog in _canvas.GetComponentsInChildren<DialogView>(true))
                    {
                        Assert.That(dialog.transform.parent, Is.EqualTo(_canvas.transform), "Dimmer stays full-screen.");
                        RectTransform safe = (RectTransform)dialog.transform.GetChild(0);
                        Assert.That(safe.GetComponent<SafeAreaView>(), Is.Not.Null);
                        AssertInside((RectTransform)safe.GetChild(0), safe);
                    }
                    foreach (RewardItemView item in _canvas.GetComponentsInChildren<RewardItemView>())
                    {
                        foreach (TMP_Text text in item.GetComponentsInChildren<TMP_Text>())
                        {
                            text.ForceMeshUpdate();
                            Assert.That(text.isTextOverflowing, Is.False, size + ": " + text.text);
                        }
                    }
                    foreach (ActionButtonView button in _canvas.GetComponentsInChildren<ActionButtonView>(true))
                    {
                        UnityEngine.UI.Image background = button.GetComponent<UnityEngine.UI.Image>();
                        Assert.That(background.type, Is.EqualTo(UnityEngine.UI.Image.Type.Sliced), button.name);
                        Assert.That(background.sprite.border.x, Is.GreaterThan(0), button.name);
                        Assert.That(background.preserveAspect, Is.False, "Background must fill the clickable area.");
                        foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true))
                        {
                            label.ForceMeshUpdate(true);
                            Assert.That(label.isTextOverflowing, Is.False, size + ": " + label.text);
                            AssertInside(label.rectTransform, (RectTransform)button.transform);
                        }
                    }
                    UnityEngine.UI.ScrollRect scroll = ledger.GetComponent<UnityEngine.UI.ScrollRect>();
                    Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
                    UnityEngine.UI.ScrollRect summaryScroll = _summary.GetComponentInChildren<UnityEngine.UI.ScrollRect>();
                    Assert.That(summaryScroll.horizontal, Is.True);
                    Assert.That(summaryScroll.vertical, Is.False);
                    Assert.That(summaryScroll.content.rect.width, Is.GreaterThan(summaryScroll.viewport.rect.width));
                    Assert.That(_board.transform.Find("ui_panel_zone_track").GetComponentsInChildren<ZoneItemView>(true).Length, Is.EqualTo(13));
                }
            }
        }

        private static T Find<T>(GameObject[] roots) where T : Component => roots.SelectMany(g => g.GetComponentsInChildren<T>(true)).Single();

        private static Rect Bounds(RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static void AssertInside(RectTransform child, RectTransform parent)
        {
            Rect inner = Bounds(child), outer = Bounds(parent);
            Assert.That(inner.xMin, Is.GreaterThanOrEqualTo(outer.xMin - .1f), child.name);
            Assert.That(inner.yMin, Is.GreaterThanOrEqualTo(outer.yMin - .1f), child.name);
            Assert.That(inner.xMax, Is.LessThanOrEqualTo(outer.xMax + .1f), child.name);
            Assert.That(inner.yMax, Is.LessThanOrEqualTo(outer.yMax + .1f), child.name);
        }

        private static RunSummary CreateFullSummary(RewardId[] ids)
        {
            RunSession run = new RunSession(new SummarySettingsSource(ids), new ZeroRandomSource(), new ZeroRandomSource());
            for (int index = 0; index < ids.Length; index++)
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
                    slots[index] = type == ZoneType.Normal && index == slots.Length - 1
                        ? WheelSlotData.CreateBomb(1)
                        : WheelSlotData.CreateReward(_ids[(zoneNumber - 1 + index) % _ids.Length], long.MaxValue, 1);
                return new WheelData(zoneNumber, type, slots);
            }
        }
    }
}
