using System;
using System.Collections;
using DG.Tweening;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using VertigoWheel.Core.Randomness;
using VertigoWheel.Core.Runs;
using VertigoWheel.Core.Wheels;
using VertigoWheel.Settings;
using VertigoWheel.UI.Wheel;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object = UnityEngine.Object;

namespace VertigoWheel.Tests.PlayMode
{
    public sealed class WheelAnimationTests
    {
        private GameObject _canvasRoot;
        private GameSettings _gameSettings;
        private AnimationSettings _animationSettings;
        private RunSession _session;
        private WheelView _view;
        private WheelAnimator _animator;
        private UnityEngine.UI.Button _spinButton;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
#if UNITY_EDITOR
            Assert.That(Application.isPlaying, Is.True, "These tests must run in PlayMode.");
            _gameSettings = AssetDatabase.LoadAssetAtPath<GameSettings>("Assets/_Game/Settings/GameSettings.asset");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/UI/Wheel/WheelView.prefab");
            Assert.That(_gameSettings, Is.Not.Null, "The authored GameSettings asset is required.");
            Assert.That(prefab, Is.Not.Null, "The authored wheel prefab is required.");
            Assert.That(_gameSettings.AnimationSettings, Is.Not.Null);
            Assert.That(_gameSettings.BombIcon, Is.Not.Null);

            _animationSettings = Object.Instantiate(_gameSettings.AnimationSettings);
            _animationSettings.SpinDurationSeconds = 0.12f;
            _animationSettings.FullTurns = 1;
            _animationSettings.SpinEase = Ease.InOutCubic;

            _canvasRoot = new GameObject("WheelAnimationTestCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster));
            _canvasRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            GameObject instance = Object.Instantiate(prefab, _canvasRoot.transform, false);
            _view = instance.GetComponent<WheelView>();
            _animator = instance.GetComponent<WheelAnimator>();
            _spinButton = instance.transform.Find("ui_button_spin")?.GetComponent<UnityEngine.UI.Button>();
            Assert.That(_view, Is.Not.Null);
            Assert.That(_animator, Is.Not.Null);
            Assert.That(_spinButton, Is.Not.Null);

            _session = new RunSession(_gameSettings, new SystemRandomSource(123), new SystemRandomSource(456));
            BindSessionWheel();
#else
            Assert.Ignore("Authored prefab loading for these tests currently requires the Unity Editor.");
#endif
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_animator != null)
            {
                _animator.Cancel();
            }
            if (_canvasRoot != null)
            {
                Object.Destroy(_canvasRoot);
            }
            if (_animationSettings != null)
            {
                Object.Destroy(_animationSettings);
            }
            _canvasRoot = null;
            _animationSettings = null;
            _gameSettings = null;
            _session = null;
            _view = null;
            _animator = null;
            _spinButton = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator NaturalCompletion_PreservesTheAcceptedTicketAndCapturedSettings_AndRejectsBusyReplay()
        {
            _view.SetRotation(17f);
            Assert.That(_session.TryStartSpin(out SpinTicket ticket), Is.True);
            float expectedTarget = GetExpectedTarget(ticket);
            SpinTicket completedTicket = null;
            int completions = 0;
            bool resolved = false;

            Assert.That(_animator.TryPlay(ticket, _animationSettings, completed =>
            {
                completedTicket = completed;
                completions++;
                resolved = _session.TryResolveSpin(completed);
            }), Is.True);
            Assert.That(_animator.IsSpinning, Is.True);
            Assert.That(_spinButton.interactable, Is.False);
            Assert.That(_animator.TryPlay(ticket, _animationSettings, _ => completions += 100), Is.False);

            _animationSettings.SpinDurationSeconds = 60f;
            _animationSettings.FullTurns = 20;
            _animationSettings.ClockwiseSpin = !_animationSettings.ClockwiseSpin;
            _animationSettings.SpinEase = Ease.Linear;

            yield return WaitUntilSpinStops();

            Assert.That(completions, Is.EqualTo(1));
            Assert.That(completedTicket, Is.SameAs(ticket));
            Assert.That(resolved, Is.True);
            Assert.That(_session.State, Is.EqualTo(RunState.Resolving));
            AssertLanding(expectedTarget);
            Assert.That(_animator.CompleteImmediately(), Is.False);
        }

        [UnityTest]
        public IEnumerator CompleteImmediately_LandsAndSettlesOnlyOnce()
        {
            Assert.That(_session.TryStartSpin(out SpinTicket ticket), Is.True);
            float expectedTarget = GetExpectedTarget(ticket);
            int completions = 0;
            SpinTicket completedTicket = null;
            bool resolved = false;
            Assert.That(_animator.TryPlay(ticket, _animationSettings, completed =>
            {
                completedTicket = completed;
                completions++;
                resolved = _session.TryResolveSpin(completed);
            }), Is.True);

            Assert.That(_animator.CompleteImmediately(), Is.True);
            Assert.That(_animator.CompleteImmediately(), Is.False);
            Assert.That(_animator.IsSpinning, Is.False);
            yield return new WaitForSecondsRealtime(0.2f);

            Assert.That(completions, Is.EqualTo(1));
            Assert.That(completedTicket, Is.SameAs(ticket));
            Assert.That(resolved, Is.True);
            Assert.That(_session.TryResolveSpin(ticket), Is.False);
            AssertLanding(expectedTarget);
        }

        [UnityTest]
        public IEnumerator CancellationAndDisable_SuppressOldCallbacks_AndAllowAReboundRestartedTicket()
        {
            int obsoleteCompletions = 0;
            Assert.That(_session.TryStartSpin(out SpinTicket cancelledTicket), Is.True);
            Assert.That(_animator.TryPlay(cancelledTicket, _animationSettings, _ => obsoleteCompletions++), Is.True);
            _animator.Cancel();
            Assert.That(_animator.IsSpinning, Is.False);
            Assert.That(_animator.CompleteImmediately(), Is.False);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(obsoleteCompletions, Is.Zero);

            _session.Restart();
            BindSessionWheel();
            Assert.That(_session.TryStartSpin(out SpinTicket disabledTicket), Is.True);
            Assert.That(_animator.TryPlay(disabledTicket, _animationSettings, _ => obsoleteCompletions++), Is.True);
            _view.gameObject.SetActive(false);
            Assert.That(_animator.IsSpinning, Is.False);
            Assert.That(_animator.TryPlay(disabledTicket, _animationSettings, _ => obsoleteCompletions++), Is.False);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(obsoleteCompletions, Is.Zero);

            _session.Restart();
            _view.gameObject.SetActive(true);
            BindSessionWheel();
            Assert.That(_session.TryStartSpin(out SpinTicket currentTicket), Is.True);
            Assert.That(currentTicket.RunGeneration, Is.GreaterThan(disabledTicket.RunGeneration));
            float expectedTarget = GetExpectedTarget(currentTicket);
            int currentCompletions = 0;
            bool resolved = false;
            Assert.That(_animator.TryPlay(currentTicket, _animationSettings, completed =>
            {
                currentCompletions++;
                resolved = ReferenceEquals(completed, currentTicket) && _session.TryResolveSpin(completed);
            }), Is.True);
            Assert.That(_animator.CompleteImmediately(), Is.True);
            yield return new WaitForSecondsRealtime(0.2f);

            Assert.That(currentCompletions, Is.EqualTo(1));
            Assert.That(obsoleteCompletions, Is.Zero);
            Assert.That(resolved, Is.True);
            Assert.That(_session.TryResolveSpin(cancelledTicket), Is.False);
            Assert.That(_session.TryResolveSpin(disabledTicket), Is.False);
            AssertLanding(expectedTarget);
        }

        [UnityTest]
        public IEnumerator RepeatedEnableDisable_KeepsOneRuntimeSpinListenerAndNoPersistentCallbacks()
        {
            int requests = 0;
            _view.SpinRequested += () => requests++;
            Assert.That(_spinButton.onClick.GetPersistentEventCount(), Is.Zero);

            for (int index = 0; index < 4; index++)
            {
                _view.gameObject.SetActive(false);
                _spinButton.onClick.Invoke();
                Assert.That(requests, Is.EqualTo(index));
                _view.gameObject.SetActive(true);
                _view.SetSpinInteractable(true);
                _spinButton.onClick.Invoke();
                Assert.That(requests, Is.EqualTo(index + 1));
                yield return null;
            }

            _view.SetSpinInteractable(false);
            _spinButton.onClick.Invoke();
            Assert.That(requests, Is.EqualTo(4));
            Assert.That(_spinButton.onClick.GetPersistentEventCount(), Is.Zero);
        }

        [UnityTest]
        public IEnumerator ResizeDuringSpin_PreservesCapturedGeometryTicketAndOnceOnlyCompletion()
        {
            WheelSettings visualSettings = Object.Instantiate(
                _gameSettings.GetWheelSettingsForZone(_session.ZoneNumber));
            try
            {
                const float slotZeroAngle = 13f;
                const float pointerAngle = 27f;
                const float radiusRatio = 0.26f;
                const float sizeRatio = 0.15f;
                visualSettings.SlotZeroAngleDegrees = slotZeroAngle;
                visualSettings.ClockwiseSlotOrder = false;
                visualSettings.PointerAngleDegrees = pointerAngle;
                visualSettings.SlotRadiusRatio = radiusRatio;
                visualSettings.SlotSizeRatio = sizeRatio;
                _view.Bind(_session.Wheel, visualSettings,
                    _gameSettings.CreateRewardCatalog(), _gameSettings.BombIcon);
                _view.SetRotation(37f);
                _animationSettings.SpinDurationSeconds = 2f;
                Assert.That(_session.TryStartSpin(out SpinTicket ticket), Is.True);
                float expectedTarget = GetExpectedTarget(ticket);
                SpinTicket completedTicket = null;
                int completions = 0;
                bool resolved = false;
                Assert.That(_animator.TryPlay(ticket, _animationSettings, completed =>
                {
                    completedTicket = completed;
                    completions++;
                    resolved = _session.TryResolveSpin(completed);
                }), Is.True);
                yield return null;
                Assert.That(_animator.IsSpinning, Is.True);

                visualSettings.SlotZeroAngleDegrees = 90f;
                visualSettings.ClockwiseSlotOrder = true;
                visualSettings.PointerAngleDegrees = 180f;
                visualSettings.SlotRadiusRatio = 0.4f;
                visualSettings.SlotSizeRatio = 0.25f;
                float rotationBeforeResize = _view.RotationDegrees;
                RectTransform wheelRect = (RectTransform)_view.transform;
                wheelRect.sizeDelta = new Vector2(400f, 600f);
                wheelRect.ForceUpdateRectTransforms();
                Canvas.ForceUpdateCanvases();

                Assert.That(_view.RotationDegrees, Is.EqualTo(rotationBeforeResize));
                Assert.That(_view.BoundWheel, Is.SameAs(ticket.Wheel));
                Assert.That(_view.SlotZeroAngleDegrees, Is.EqualTo(slotZeroAngle));
                Assert.That(_view.ClockwiseSlotOrder, Is.False);
                Assert.That(_view.PointerAngleDegrees, Is.EqualTo(pointerAngle));
                Assert.That(_spinButton.interactable, Is.False);
                Assert.That(completions, Is.Zero);
                Transform rotationRoot = _view.transform.Find("ui_wheel_rotation");
                for (int index = 0; index < WheelData.SlotCount; index++)
                {
                    RectTransform slot = (RectTransform)rotationRoot.Find(
                        "ui_slot_reward_" + index.ToString("00"));
                    double radians = (slotZeroAngle + index * 360.0 / WheelData.SlotCount)
                        * Math.PI / 180.0;
                    Vector2 expectedPosition = new Vector2(-(float)Math.Sin(radians),
                        (float)Math.Cos(radians)) * 400f * radiusRatio;
                    Assert.That(Vector2.Distance(slot.anchoredPosition, expectedPosition), Is.LessThan(0.01f));
                    Assert.That(Vector2.Distance(slot.sizeDelta, Vector2.one * 400f * sizeRatio),
                        Is.LessThan(0.01f));
                    Assert.That(slot.GetComponentInChildren<TMP_Text>(true).fontSize,
                        Is.EqualTo(400f * sizeRatio * 0.19f).Within(0.01f));
                    Assert.That(Quaternion.Angle(slot.localRotation,
                        Quaternion.Euler(0, 0, -_view.RotationDegrees)), Is.LessThan(0.01f));
                }
                RectTransform pointer = (RectTransform)_view.transform.Find("ui_image_wheel_pointer_value");
                Vector2 pointerPosition = new Vector2(-Mathf.Sin(pointerAngle * Mathf.Deg2Rad),
                    Mathf.Cos(pointerAngle * Mathf.Deg2Rad)) * 400f * 0.43f;
                Assert.That(Vector2.Distance(pointer.anchoredPosition, pointerPosition), Is.LessThan(0.01f));
                Assert.That(Vector2.Distance(pointer.sizeDelta, new Vector2(400f * 0.085f, 400f * 0.13f)),
                    Is.LessThan(0.01f));
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(pointer.localEulerAngles.z, pointerAngle)),
                    Is.LessThan(0.01f));
                Assert.That(Vector2.Distance(((RectTransform)_spinButton.transform).sizeDelta,
                    Vector2.one * 400f * 0.28f), Is.LessThan(0.01f));

                Assert.That(_animator.CompleteImmediately(), Is.True);
                Assert.That(_animator.CompleteImmediately(), Is.False);
                yield return null;
                Assert.That(completions, Is.EqualTo(1));
                Assert.That(completedTicket, Is.SameAs(ticket));
                Assert.That(resolved, Is.True);
                Assert.That(_session.State, Is.EqualTo(RunState.Resolving));
                Assert.That(_session.TryResolveSpin(ticket), Is.False);
                AssertLanding(expectedTarget);
            }
            finally
            {
                Object.Destroy(visualSettings);
            }
        }

        private void BindSessionWheel()
        {
            _view.Bind(_session.Wheel, _gameSettings.GetWheelSettingsForZone(_session.ZoneNumber),
                _gameSettings.CreateRewardCatalog(), _gameSettings.BombIcon);
            _view.SetSpinInteractable(_session.CanSpin);
        }

        private float GetExpectedTarget(SpinTicket ticket)
        {
            return (float)WheelAngleCalculator.GetTargetRotationDegrees(
                _view.RotationDegrees, ticket.SelectedSlotIndex, _view.SlotZeroAngleDegrees,
                _view.ClockwiseSlotOrder, _view.PointerAngleDegrees,
                _animationSettings.FullTurns, _animationSettings.ClockwiseSpin);
        }

        private void AssertLanding(float expectedTarget)
        {
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(_view.RotationDegrees, expectedTarget)), Is.LessThan(0.01f));
            Assert.That(_animator.IsSpinning, Is.False);
        }

        private IEnumerator WaitUntilSpinStops()
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (_animator.IsSpinning && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.That(_animator.IsSpinning, Is.False, "The spin must use its captured 0.12 second duration.");
        }
    }
}
