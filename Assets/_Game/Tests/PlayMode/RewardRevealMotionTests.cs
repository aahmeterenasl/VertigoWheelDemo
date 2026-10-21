#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using DG.Tweening;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using VertigoWheel.Settings;
using VertigoWheel.UI.Common;
using VertigoWheel.UI.Dialogs;
using Object = UnityEngine.Object;

namespace VertigoWheel.Tests.PlayMode
{
    public sealed class RewardRevealMotionTests
    {
        private GameObject _canvas;
        private RewardRevealView _view;
        private AnimationSettings _settings;
        private RewardSettings _reward;
        private UnityEngine.UI.Button _button;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var game = AssetDatabase.LoadAssetAtPath<GameSettings>("Assets/_Game/Settings/GameSettings.asset");
            _settings = Object.Instantiate(game.AnimationSettings);
            _reward = game.CreateRewardCatalog().Values.First();
            _canvas = new GameObject("reveal_motion_test", typeof(RectTransform), typeof(Canvas));
            _canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/UI/Dialogs/ui_panel_reward_reveal.prefab");
            _view = Object.Instantiate(prefab, _canvas.transform, false).GetComponent<RewardRevealView>();
            _button = _view.GetComponentInChildren<ActionButtonView>(true).GetComponent<UnityEngine.UI.Button>();
            _view.Hide();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            _view.Hide();
            Object.Destroy(_canvas);
            Object.Destroy(_settings);
            yield return null;
        }

        [UnityTest]
        public IEnumerator StagedEntrance_CapturesTuning_UsesUnscaledTime_AndIdleDoesNotGateContinue()
        {
            Time.timeScale = 0f;
            _settings.RewardRevealDurationSeconds = 0.12f;
            int requests = 0;
            _view.ContinueRequested += () => requests++;
            _view.Show(_reward, 123456789, _settings);
            var icon = Read<UnityEngine.UI.Image>(_view, "_icon");
            var shine = Read<UnityEngine.UI.Image>(_view, "_shine");
            var backdrop = Read<CanvasGroup>(_view.GetComponent<DialogView>(), "_backdrop");
            var text = Read<CanvasGroup>(_view, "_textGroup");
            Assert.That(_view.GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
            Assert.That(_view.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            Assert.That(backdrop.alpha, Is.Zero);
            Assert.That(Read<CanvasGroup>(_view, "_iconGroup").alpha, Is.Zero);
            Assert.That(text.alpha, Is.Zero);
            Assert.That(icon.transform.localScale.x, Is.EqualTo(0.88f).Within(0.001f));
            Assert.That(icon.transform.parent.localScale, Is.EqualTo(Vector3.one));
            _button.onClick.Invoke();
            Assert.That(requests, Is.Zero);

            _settings.RewardRevealDurationSeconds = 60f;
            _settings.RewardIconPeakScale = 1.1f;
            _settings.RewardShineDegreesPerSecond = 0f;
            _settings.RewardTextOffset = 30f;
            yield return WaitForReady();
            Assert.That(backdrop.alpha, Is.EqualTo(1f));
            Assert.That(text.alpha, Is.EqualTo(1f));
            Assert.That(((RectTransform)text.transform).anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(icon.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(Read<TMPro.TMP_Text>(_view, "_quantity").text, Is.EqualTo("x123M"));
            Assert.That(Read<TMPro.TMP_Text>(_view, "_name").text, Is.EqualTo(_reward.DisplayName));
            Quaternion rotation = shine.transform.localRotation;
            yield return new WaitForSecondsRealtime(0.08f);
            Assert.That(Quaternion.Angle(rotation, shine.transform.localRotation), Is.GreaterThan(0.1f));
            Assert.That(Read<Tween>(_view, "_shineTween").IsActive(), Is.True);
            _button.onClick.Invoke();
            _button.onClick.Invoke();
            Assert.That(requests, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator HideDuringEntrance_ThenReopenImmediately_DropsObsoleteMotion()
        {
            _settings.RewardRevealDurationSeconds = 0.12f;
            _view.Show(_reward, 2, _settings);
            Tween oldEntrance = Read<Sequence>(_view, "_entrance");
            Tween oldShine = Read<Tween>(_view, "_shineTween");
            _view.Hide();
            Assert.That(oldEntrance.IsActive(), Is.False);
            Assert.That(oldShine.IsActive(), Is.False);
            Assert.That(_view.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            AssertReset();
            _settings.RewardRevealDurationSeconds = 0f;
            _settings.RewardShineDegreesPerSecond = 0f;
            _view.Show(_reward, 7, _settings);
            Assert.That(_view.IsReady, Is.True);
            AssertReset();
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(_view.IsReady, Is.True);
            Assert.That(Read<TMPro.TMP_Text>(_view, "_quantity").text, Is.EqualTo("x7"));
            Assert.That(Read<Sequence>(_view, "_entrance"), Is.Null);
            Assert.That(Read<Tween>(_view, "_shineTween"), Is.Null);
        }

        [UnityTest]
        public IEnumerator DisableWhileIdle_ClearsMotion_AndReenableRequiresANewShow()
        {
            _settings.RewardRevealDurationSeconds = 0f;
            _view.Show(_reward, 1, _settings);
            yield return new WaitForSecondsRealtime(0.05f);
            Tween idle = Read<Tween>(_view, "_shineTween");
            Assert.That(idle.IsActive(), Is.True);
            _view.gameObject.SetActive(false);
            Assert.That(idle.IsActive(), Is.False);
            AssertReset();
            int requests = 0;
            _view.ContinueRequested += () => requests++;
            _view.gameObject.SetActive(true);
            _button.onClick.Invoke();
            Assert.That(_view.IsReady, Is.False);
            Assert.That(requests, Is.Zero);
            _view.Show(_reward, 1, _settings);
            _button.onClick.Invoke();
            Assert.That(requests, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator MissingOptionalEffects_DoNotBlockTheEntranceOrCreateAnIdleTween()
        {
            _settings.RewardGlowSprite = null;
            _settings.RewardShineSprite = null;
            _settings.RewardRevealDurationSeconds = 0.06f;
            _view.Show(_reward, 1, _settings);
            yield return WaitForReady();
            Assert.That(Read<UnityEngine.UI.Image>(_view, "_glow").enabled, Is.False);
            Assert.That(Read<UnityEngine.UI.Image>(_view, "_shine").enabled, Is.False);
            Assert.That(Read<Tween>(_view, "_shineTween"), Is.Null);
            Assert.That(_button.IsInteractable(), Is.True);
        }

        private IEnumerator WaitForReady()
        {
            float deadline = Time.realtimeSinceStartup + 2f;
            while (!_view.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(_view.IsReady, Is.True);
        }

        private void AssertReset()
        {
            foreach (string name in new[] { "_iconGroup", "_textGroup", "_effectsGroup", "_continueGroup" })
                Assert.That(Read<CanvasGroup>(_view, name).alpha, Is.EqualTo(1f));
            Assert.That(Read<UnityEngine.UI.Image>(_view, "_icon").transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(Quaternion.Angle(Read<UnityEngine.UI.Image>(_view, "_shine").transform.localRotation,
                Quaternion.identity), Is.LessThan(0.001f));
        }

        private static T Read<T>(object owner, string field) =>
            (T)owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    }
}
#endif
