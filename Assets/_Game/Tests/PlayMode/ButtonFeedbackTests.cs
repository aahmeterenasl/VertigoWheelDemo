using System.Collections;
using System.Reflection;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using VertigoWheel.Settings;
using VertigoWheel.UI.Common;
using Object = UnityEngine.Object;

namespace VertigoWheel.Tests.PlayMode
{
    public sealed class ButtonFeedbackTests
    {
        private GameObject _root;
        private GameObject _eventRoot;
        private RectTransform _visual;
        private UnityEngine.UI.Button _button;
        private ButtonFeedbackView _feedback;
        private AnimationSettings _settings;
        private EventSystem _events;
        private int _clicks;

        [SetUp]
        public void SetUp()
        {
            _events = EventSystem.current;
            if (_events == null)
            {
                _eventRoot = new GameObject("feedback_test_events", typeof(EventSystem));
                _events = _eventRoot.GetComponent<EventSystem>();
            }
            _root = new GameObject("ui_button_test", typeof(RectTransform), typeof(UnityEngine.UI.Image),
                typeof(UnityEngine.UI.Button), typeof(ActionButtonView));
            _root.SetActive(false);
            _visual = (RectTransform)new GameObject("ui_animation_button_content", typeof(RectTransform)).transform;
            _visual.SetParent(_root.transform, false);
            _settings = ScriptableObject.CreateInstance<AnimationSettings>();
            _button = _root.GetComponent<UnityEngine.UI.Button>();
            _feedback = _root.AddComponent<ButtonFeedbackView>();
            typeof(ButtonFeedbackView).GetField("_settings", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_feedback, _settings);
            _clicks = 0;
            _root.GetComponent<ActionButtonView>().Clicked += () => _clicks++;
            _root.SetActive(true);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            Object.Destroy(_root);
            Object.Destroy(_settings);
            if (_eventRoot != null) Object.Destroy(_eventRoot);
            yield return null;
        }

        private PointerEventData Pointer(int id = 7) => new PointerEventData(_events)
        { pointerId = id, button = PointerEventData.InputButton.Left };

        [UnityTest]
        public IEnumerator PointerMotion_UsesCapturedTimingAndUnscaledTime_WithoutMovingTheHitArea()
        {
            Time.timeScale = 0f;
            var root = (RectTransform)_root.transform;
            root.anchoredPosition = new Vector2(80f, 40f);
            Vector2 size = root.sizeDelta;
            _feedback.OnPointerDown(Pointer());
            yield return new WaitForSecondsRealtime(.10f);
            Assert.That(_visual.localScale.x, Is.EqualTo(.96f).Within(.001f));
            _settings.ButtonReleaseSeconds = 60f;
            _settings.ButtonPressedScale = .9f;
            _feedback.OnPointerUp(Pointer());
            ExecuteEvents.Execute(_root, Pointer(), ExecuteEvents.pointerClickHandler);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(_visual.localScale, Is.EqualTo(Vector3.one));
            Assert.That(root.localScale, Is.EqualTo(Vector3.one));
            Assert.That(root.anchoredPosition, Is.EqualTo(new Vector2(80f, 40f)));
            Assert.That(root.sizeDelta, Is.EqualTo(size));
            Assert.That(_clicks, Is.EqualTo(1), "The feedback component must never dispatch another click.");
        }

        [UnityTest]
        public IEnumerator CancelExitFocusAndInteractionLoss_ResetTheVisual_AndIgnoreOtherTouches()
        {
            _settings.ButtonPressSeconds = _settings.ButtonReleaseSeconds = 0f;
            _feedback.OnPointerDown(Pointer());
            _feedback.OnPointerUp(Pointer(8));
            Assert.That(_visual.localScale.x, Is.EqualTo(.96f));
            _feedback.OnPointerExit(Pointer());
            Assert.That(_visual.localScale, Is.EqualTo(Vector3.one));
            _feedback.OnPointerDown(Pointer());
            _feedback.OnCancel(new BaseEventData(_events));
            Assert.That(_visual.localScale, Is.EqualTo(Vector3.one));
            _feedback.OnPointerDown(Pointer());
            _button.interactable = false;
            yield return null;
            Assert.That(_visual.localScale, Is.EqualTo(Vector3.one));
            _feedback.OnPointerDown(Pointer());
            Assert.That(_visual.localScale, Is.EqualTo(Vector3.one));
            _button.interactable = true;
            _feedback.OnPointerDown(Pointer());
            _feedback.SendMessage("OnApplicationFocus", false);
            Assert.That(_visual.localScale, Is.EqualTo(Vector3.one));
            _feedback.OnPointerDown(Pointer());
            _feedback.SendMessage("OnApplicationPause", true);
            Assert.That(_visual.localScale, Is.EqualTo(Vector3.one));
            var group = _root.AddComponent<CanvasGroup>();
            _feedback.OnPointerDown(Pointer());
            group.interactable = false;
            yield return null;
            Assert.That(_visual.localScale, Is.EqualTo(Vector3.one));
            group.interactable = true;
            _feedback.OnPointerDown(Pointer());
            _feedback.enabled = false;
            Assert.That(_visual.localScale, Is.EqualTo(Vector3.one));
            Assert.That(_clicks, Is.Zero);
        }

        [UnityTest]
        public IEnumerator Submit_DispatchesOneAction_AndDisableCancelsItsOwnedTween()
        {
            Time.timeScale = 0f;
            ExecuteEvents.Execute(_root, new BaseEventData(_events), ExecuteEvents.submitHandler);
            yield return new WaitForSecondsRealtime(.08f);
            Assert.That(_visual.localScale.x, Is.LessThan(1f));
            Assert.That(_clicks, Is.EqualTo(1));
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(_visual.localScale, Is.EqualTo(Vector3.one));
            Assert.That(DOTween.IsTweening(_feedback), Is.False);
            _settings.ButtonReleaseSeconds = 1f;
            ExecuteEvents.Execute(_root, new BaseEventData(_events), ExecuteEvents.submitHandler);
            _root.SetActive(false);
            Assert.That(_visual.localScale, Is.EqualTo(Vector3.one));
            Assert.That(DOTween.IsTweening(_feedback), Is.False);
            _root.SetActive(true);
            _settings.ButtonPressSeconds = _settings.ButtonReleaseSeconds = 0f;
            ExecuteEvents.Execute(_root, new BaseEventData(_events), ExecuteEvents.submitHandler);
            Assert.That(_visual.localScale, Is.EqualTo(Vector3.one));
            Assert.That(_clicks, Is.EqualTo(3));
        }
    }
}
