#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VertigoWheel.App;
using VertigoWheel.Core.Rewards;
using VertigoWheel.Settings;
using VertigoWheel.UI.Board;
using VertigoWheel.UI.Rewards;

namespace VertigoWheel.Tests.PlayMode
{
    public sealed class LedgerFeedbackTests
    {
        private Scene _scene;
        private RewardCollectionView _ledger;
        private AnimationSettings _settings;
        private IReadOnlyDictionary<RewardId, RewardSettings> _catalog;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Additive);
            _scene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            Find<GameBootstrap>().enabled = false;
            _ledger = Find<GameView>().GetComponentInChildren<RewardCollectionView>();
            _settings = ScriptableObject.CreateInstance<AnimationSettings>();
            _settings.LedgerDurationSeconds = 0.12f;
            _catalog = AssetDatabase.LoadAssetAtPath<GameSettings>("Assets/_Game/Settings/GameSettings.asset").CreateRewardCatalog();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            Object.Destroy(_settings);
            yield return SceneManager.UnloadSceneAsync(_scene);
        }

        [UnityTest]
        public IEnumerator ExistingRow_HoldsPreviousValue_UntilArrival_AndRebindDoesNotReplay()
        {
            Set(10);
            _ledger.StageArrival(RewardId.Cash, 5);
            Set(15);
            Assert.That(Quantity(), Is.EqualTo("10"));
            Time.timeScale = 0f;
            _ledger.PlayArrival(_settings);
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(Quantity(), Is.EqualTo("15"));
            Set(15);
            _ledger.PlayArrival(_settings);
            Assert.That(Quantity(), Is.EqualTo("15"), "Ordinary renders and repeated callbacks cannot replay the count.");
            Assert.That(Row().Motion.transform.localScale, Is.EqualTo(Vector3.one));
        }

        [UnityTest]
        public IEnumerator MaximumTotal_IsExact_AfterCompletion_AndInterruption()
        {
            _ledger.StageArrival(RewardId.Cash, long.MaxValue);
            Set(long.MaxValue);
            _ledger.PlayArrival(_settings);
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(Quantity(), Is.EqualTo(long.MaxValue.ToString("N0", CultureInfo.InvariantCulture)));
            _ledger.StageArrival(RewardId.Cash, 100);
            Set(long.MaxValue);
            _ledger.PlayArrival(_settings);
            _ledger.CompleteFeedback();
            Assert.That(Quantity(), Is.EqualTo(long.MaxValue.ToString("N0", CultureInfo.InvariantCulture)));
        }

        [UnityTest]
        public IEnumerator ReusedRowAndZeroDuration_RestoreAuthoritativeNumbers()
        {
            _ledger.StageArrival(RewardId.Cash, 10);
            Set(10);
            _ledger.PlayArrival(_settings);
            _ledger.SetRewards(new[] { new RewardAmount(RewardId.Gold, 7) }, _catalog);
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(Quantity(), Is.EqualTo("7"));
            _settings.LedgerDurationSeconds = 0f;
            _ledger.StageArrival(RewardId.Cash, 3);
            Set(3);
            _ledger.PlayArrival(_settings);
            Assert.That(Quantity(), Is.EqualTo("3"));
        }

        [UnityTest]
        public IEnumerator OffscreenArrival_KeepsScrollPosition_AndShowsExactTotal()
        {
            var amounts = _catalog.Keys.Select(id => new RewardAmount(id, id == RewardId.Cash ? 20 : 1)).ToArray();
            _ledger.SetRewards(amounts, _catalog, true);
            var scroll = _ledger.GetComponent<UnityEngine.UI.ScrollRect>();
            scroll.verticalNormalizedPosition = 0.5f;
            Canvas.ForceUpdateCanvases();
            Vector2 position = scroll.normalizedPosition;
            _ledger.StageArrival(RewardId.Cash, 10);
            _ledger.SetRewards(amounts, _catalog);
            _ledger.PlayArrival(_settings);
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(scroll.normalizedPosition.y, Is.EqualTo(position.y).Within(0.001f));
            Assert.That(Quantity(), Is.EqualTo("20"));
        }

        private void Set(long value) => _ledger.SetRewards(new[] { new RewardAmount(RewardId.Cash, value) }, _catalog);
        private RewardItemView Row() => _ledger.GetComponentsInChildren<RewardItemView>().First();
        private string Quantity() => _ledger.GetComponentsInChildren<RewardItemView>()
            .First(item => item.RewardId == RewardId.Cash || _ledger.GetComponentsInChildren<RewardItemView>().Length == 1)
            .GetComponentsInChildren<TMP_Text>().Single(t => t.name == "ui_text_reward_quantity_value").text;
        private T Find<T>() where T : Component => _scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).First();
    }
}
#endif
