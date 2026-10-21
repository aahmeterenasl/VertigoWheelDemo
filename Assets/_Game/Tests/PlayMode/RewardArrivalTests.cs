#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DG.Tweening;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VertigoWheel.App;
using VertigoWheel.Core.Rewards;
using VertigoWheel.Settings;
using VertigoWheel.UI.Board;
using VertigoWheel.UI.Common;
using VertigoWheel.UI.Dialogs;
using VertigoWheel.UI.Rewards;
using Object = UnityEngine.Object;

namespace VertigoWheel.Tests.PlayMode
{
    public sealed class RewardArrivalTests
    {
        private Scene _scene;
        private Canvas _canvas;
        private RewardArrivalView _arrival;
        private RewardRevealView _reveal;
        private RewardCollectionView _ledger;
        private AnimationSettings _settings;
        private IReadOnlyDictionary<RewardId, RewardSettings> _catalog;
        private Camera _camera;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Additive);
            _scene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            Find<GameBootstrap>().enabled = false;
            var board = Find<GameView>();
            _arrival = board.RewardArrival;
            _ledger = board.GetComponentInChildren<RewardCollectionView>();
            _reveal = Find<RewardRevealView>();
            _canvas = board.GetComponentInParent<Canvas>();
            foreach (var safe in _canvas.GetComponentsInChildren<SafeAreaView>(true)) safe.enabled = false;
            var game = AssetDatabase.LoadAssetAtPath<GameSettings>("Assets/_Game/Settings/GameSettings.asset");
            _catalog = game.CreateRewardCatalog();
            _settings = Object.Instantiate(game.AnimationSettings);
            _settings.RewardRevealDurationSeconds = 0;
            _settings.RewardFlightFadeDurationSeconds = 0;
            _settings.RewardFlightStaggerSeconds = 0;
            _settings.RewardFlightArcHeight = 0;
            _ledger.SetRewards(_catalog.Keys.Select(id => new RewardAmount(id, long.MaxValue)).ToArray(), _catalog, true);
            _camera = new GameObject("arrival_test_camera", typeof(Camera)).GetComponent<Camera>();
            _camera.transform.position = new Vector3(0, 0, -10);
            _camera.orthographic = true;
            _camera.orthographicSize = 540;
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            _arrival.Cancel();
            yield return SceneManager.UnloadSceneAsync(_scene);
            Object.Destroy(_settings);
            Object.Destroy(_camera.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator VisibleTarget_UsesTheMatchingIcon_InOverlayAndCameraCanvases()
        {
            RewardId id = _catalog.Keys.First();
            foreach (bool useCamera in new[] { false, true })
            {
                _canvas.worldCamera = useCamera ? _camera : null;
                _canvas.renderMode = useCamera ? RenderMode.ScreenSpaceCamera : RenderMode.ScreenSpaceOverlay;
                _canvas.planeDistance = 10;
                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(_ledger.TryGetArrivalTarget(id, out RectTransform target, out RectTransform viewport), Is.True);
                int completed = 0;
                Begin(id, () => completed++);
                HoldNearArrival();
                yield return null;
                Vector3 copyCenter = Copy().TransformPoint(Copy().rect.center);
                Vector3 targetCenter = target.TransformPoint(target.rect.center);
                Vector3 localCopy = viewport.InverseTransformPoint(copyCenter);
                Vector3 localTarget = viewport.InverseTransformPoint(targetCenter);
                Assert.That(Vector2.Distance(localCopy, localTarget), Is.LessThan(0.5f), useCamera.ToString());
                Assert.That(completed, Is.Zero);
                Assert.That(_arrival.CompleteImmediately(), Is.True);
                Assert.That(_arrival.CompleteImmediately(), Is.False);
                Assert.That(completed, Is.EqualTo(1));
                AssertCopiesCleared();
            }
        }

        [UnityTest]
        public IEnumerator OffscreenTarget_UsesViewportEdge_PreservesScroll_AndTracksAResize()
        {
            RewardId id = _catalog.Keys.Last();
            var scroll = _ledger.GetComponent<UnityEngine.UI.ScrollRect>();
            Vector2 scrollBefore = scroll.normalizedPosition;
            int completed = 0;
            Begin(id, () => completed++);
            HoldNearArrival();
            _canvas.GetComponent<UnityEngine.UI.CanvasScaler>().enabled = false;
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.worldCamera = _camera;
            ((RectTransform)_canvas.transform).sizeDelta = new Vector2(2400, 1080);
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;
            _ledger.TryGetArrivalTarget(id, out RectTransform target, out RectTransform viewport);
            Vector3 localTarget = viewport.InverseTransformPoint(target.TransformPoint(target.rect.center));
            Vector3 localCopy = viewport.InverseTransformPoint(Copy().TransformPoint(Copy().rect.center));
            Assert.That(localTarget.y, Is.LessThan(viewport.rect.yMin));
            Assert.That(localCopy.y, Is.EqualTo(viewport.rect.yMin).Within(0.5f));
            Assert.That(Vector2.Distance(scroll.normalizedPosition, scrollBefore), Is.LessThan(0.001f));
            Assert.That(_arrival.GetComponentsInChildren<UnityEngine.UI.Image>(true).Length, Is.EqualTo(3));
            Assert.That(completed, Is.Zero);
            _arrival.CompleteImmediately();
            Assert.That(completed, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator MissingDestinationFinishesOnce_WhileCancellationSuppressesCompletion()
        {
            RewardId id = _catalog.Keys.First();
            int completed = 0;
            Begin(id, () => completed++);
            _ledger.SetRewards(new RewardAmount[0], _catalog);
            yield return null;
            Assert.That(_arrival.IsPlaying, Is.False);
            Assert.That(completed, Is.EqualTo(1));
            AssertCopiesCleared();
            _ledger.SetRewards(new[] { new RewardAmount(id, 1) }, _catalog);
            Begin(id, () => completed++);
            _arrival.Cancel();
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(completed, Is.EqualTo(1));
            AssertCopiesCleared();
            _settings.RewardFlightDurationSeconds = 0;
            _reveal.Show(_catalog[id], 1, _settings);
            Assert.That(_arrival.TryCaptureSource(_reveal.RewardIcon), Is.True);
            Assert.That(_arrival.TryPlay(id, _settings, () => completed++), Is.False);
            Assert.That(completed, Is.EqualTo(1));
        }

        private void Begin(RewardId id, System.Action completed)
        {
            _reveal.Show(_catalog[id], long.MaxValue, _settings);
            Canvas.ForceUpdateCanvases();
            Assert.That(_arrival.TryCaptureSource(_reveal.RewardIcon), Is.True);
            _reveal.Hide();
            Assert.That(_arrival.TryPlay(id, _settings, completed), Is.True);
        }

        private void HoldNearArrival()
        {
            var flight = (Tween)typeof(RewardArrivalView).GetField("_flight", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_arrival);
            flight.Goto(_settings.RewardFlightDurationSeconds * 0.99f, false);
        }

        private RectTransform Copy() => _arrival.GetComponentsInChildren<UnityEngine.UI.Image>(true)[0].rectTransform;

        private void AssertCopiesCleared()
        {
            Assert.That(_arrival.GetComponentsInChildren<UnityEngine.UI.Image>(true)
                .All(copy => !copy.gameObject.activeSelf && copy.sprite == null), Is.True);
        }

        private T Find<T>() where T : Component => _scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).First();
    }
}
#endif
