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
using VertigoWheel.Core.Rewards;
using VertigoWheel.Settings;
using VertigoWheel.UI.Board;
using VertigoWheel.UI.Common;
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
    }
}
