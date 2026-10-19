using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VertigoWheel.Settings;
using VertigoWheel.UI.Common;
using VertigoWheel.UI.Dialogs;
using Object = UnityEngine.Object;

namespace VertigoWheel.Tests.PlayMode
{
    public sealed class DialogViewTests
    {
        private GameObject _root;
        private CanvasGroup _group;
        private RectTransform _content;
        private DialogView _dialog;
        private AnimationSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("dialog_test", typeof(RectTransform), typeof(CanvasGroup));
            _root.SetActive(false);
            _group = _root.GetComponent<CanvasGroup>();
            _dialog = _root.AddComponent<DialogView>();
            _content = (RectTransform)new GameObject("dialog_content", typeof(RectTransform)).transform;
            _content.SetParent(_root.transform, false);
            SetReference(_dialog, "_group", _group);
            SetReference(_dialog, "_content", _content);
            _settings = ScriptableObject.CreateInstance<AnimationSettings>();
            _settings.DialogDurationSeconds = 0f;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            if (_root != null)
            {
                Object.Destroy(_root);
            }
            Object.Destroy(_settings);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Opening_BlocksTheBoardBeforeAcceptingActions_AndUsesUnscaledTime()
        {
            int completions = 0;
            Time.timeScale = 0f;
            _dialog.Show(0.06f, () => completions++);
            Assert.That(_dialog.IsOpen, Is.True);
            Assert.That(_dialog.IsReady, Is.False);
            Assert.That(_group.blocksRaycasts, Is.True);
            Assert.That(_group.interactable, Is.False);
            Assert.That(completions, Is.Zero);

            yield return WaitUntilReady();

            Assert.That(_group.interactable, Is.True);
            Assert.That(_group.alpha, Is.EqualTo(1f));
            Assert.That(_content.localScale, Is.EqualTo(Vector3.one));
            Assert.That(completions, Is.EqualTo(1));
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.That(completions, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator HideAndReopen_CancelTheObsoleteOpeningCallback()
        {
            int obsoleteCompletions = 0;
            int currentCompletions = 0;
            _dialog.Show(0.1f, () => obsoleteCompletions++);
            _dialog.Hide();
            Assert.That(_dialog.IsOpen, Is.False);
            Assert.That(_dialog.IsReady, Is.False);
            Assert.That(_group.blocksRaycasts, Is.False);
            Assert.That(_group.interactable, Is.False);
            Assert.That(_root.activeSelf, Is.False);

            _dialog.Show(0f, () => currentCompletions++);
            Assert.That(_dialog.IsReady, Is.True);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(obsoleteCompletions, Is.Zero);
            Assert.That(currentCompletions, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DisableWhileOpening_DropsTheCallbackAndReleasesInput()
        {
            int completions = 0;
            _dialog.Show(0.05f, () => completions++);
            _root.SetActive(false);
            yield return new WaitForSecondsRealtime(0.15f);

            Assert.That(completions, Is.Zero);
            Assert.That(_dialog.IsOpen, Is.False);
            Assert.That(_dialog.IsReady, Is.False);
            Assert.That(_group.blocksRaycasts, Is.False);
            Assert.That(_group.interactable, Is.False);
            Assert.That(_content.localScale, Is.EqualTo(Vector3.one));
        }

        [UnityTest]
        public IEnumerator ExitActions_RejectEntranceClicksAndDoubleClicks_AndRearmOnShow()
        {
            UnityEngine.UI.Button collect = CreateButton("collect", out ActionButtonView collectView);
            UnityEngine.UI.Button keepPlaying = CreateButton("keep_playing", out ActionButtonView keepPlayingView);
            ExitConfirmationView exit = _root.AddComponent<ExitConfirmationView>();
            SetReference(exit, "_dialog", _dialog);
            SetReference(exit, "_collectButton", collectView);
            SetReference(exit, "_keepPlayingButton", keepPlayingView);
            int collections = 0;
            int cancellations = 0;
            exit.CollectRequested += () => collections++;
            exit.KeepPlayingRequested += () => cancellations++;

            _settings.DialogDurationSeconds = 0.06f;
            exit.Show(_settings);
            collect.onClick.Invoke();
            keepPlaying.onClick.Invoke();
            Assert.That(collections + cancellations, Is.Zero);
            yield return WaitUntilReady();
            collect.onClick.Invoke();
            collect.onClick.Invoke();
            keepPlaying.onClick.Invoke();
            Assert.That(collections, Is.EqualTo(1));
            Assert.That(cancellations, Is.Zero);

            for (int index = 0; index < 3; index++)
            {
                exit.Hide();
                keepPlaying.onClick.Invoke();
                Assert.That(cancellations, Is.EqualTo(index));
                _settings.DialogDurationSeconds = 0f;
                exit.Show(_settings);
                keepPlaying.onClick.Invoke();
                keepPlaying.onClick.Invoke();
                Assert.That(cancellations, Is.EqualTo(index + 1));
                Assert.That(collections, Is.EqualTo(1));
                yield return null;
            }
            Assert.That(collect.onClick.GetPersistentEventCount(), Is.Zero);
            Assert.That(keepPlaying.onClick.GetPersistentEventCount(), Is.Zero);
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidOpeningDuration_LeavesTheExistingDialogUnchanged(float duration)
        {
            _dialog.Show(0f);
            Assert.Throws<ArgumentOutOfRangeException>(() => _dialog.Show(duration));
            Assert.That(_dialog.IsOpen, Is.True);
            Assert.That(_dialog.IsReady, Is.True);
        }

        private UnityEngine.UI.Button CreateButton(string name, out ActionButtonView action)
        {
            GameObject child = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image),
                typeof(UnityEngine.UI.Button));
            child.transform.SetParent(_content, false);
            action = child.AddComponent<ActionButtonView>();
            return child.GetComponent<UnityEngine.UI.Button>();
        }

        private IEnumerator WaitUntilReady()
        {
            float deadline = Time.realtimeSinceStartup + 3f;
            while (!_dialog.IsReady && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.That(_dialog.IsReady, Is.True, "The dialog must finish its captured unscaled duration.");
        }

        private static void SetReference(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }
    }
}
