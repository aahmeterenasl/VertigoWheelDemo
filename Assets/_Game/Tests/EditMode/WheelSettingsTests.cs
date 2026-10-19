using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VertigoWheel.Core.Rewards;
using VertigoWheel.Core.Wheels;
using VertigoWheel.Core.Zones;
using VertigoWheel.Settings;

namespace VertigoWheel.Tests.EditMode
{
    public sealed class WheelSettingsTests
    {
        private readonly List<UnityEngine.Object> _objects = new List<UnityEngine.Object>();
        private Sprite _sprite;
        private RewardSettings _reward;

        [SetUp]
        public void SetUp()
        {
            Texture2D texture = new Texture2D(2, 2);
            _objects.Add(texture);
            _sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
            _objects.Add(_sprite);
            _reward = CreateReward(RewardId.Cash);
        }

        [TearDown]
        public void TearDown()
        {
            for (int index = _objects.Count - 1; index >= 0; index--)
            {
                UnityEngine.Object.DestroyImmediate(_objects[index]);
            }

            _objects.Clear();
        }

        [TestCase(1, ZoneType.Normal)]
        [TestCase(5, ZoneType.Safe)]
        [TestCase(30, ZoneType.Super)]
        [TestCase(31, ZoneType.Normal)]
        [TestCase(60, ZoneType.Super)]
        public void GameSettings_SelectsTheWheelAndCalculatesItsQuantities(int zoneNumber, ZoneType expected)
        {
            GameSettings game = CreateGame();
            game.SilverWheel.GetSlot(0).BaseQuantity = 500;
            game.GoldenWheel.GetSlot(0).BaseQuantity = 1000;

            WheelData wheel = game.CreateWheelData(zoneNumber);
            long baseQuantity = expected == ZoneType.Normal ? 100 : expected == ZoneType.Safe ? 500 : 1000;

            Assert.That(wheel.ZoneType, Is.EqualTo(expected));
            Assert.That(wheel.Slots[0].Quantity,
                Is.EqualTo(game.ZoneSettings.CreateRules().GetRewardQuantity(zoneNumber, baseQuantity, 25)));
        }

        [Test]
        public void EditingSlots_ChangesFutureDataWithoutChangingExistingData()
        {
            GameSettings game = CreateGame();
            WheelData previous = game.CreateWheelData(6);
            game.BronzeWheel.GetSlot(0).BaseQuantity = 400;
            WheelData updated = game.CreateWheelData(6);

            Assert.That(previous.Slots[0].Quantity, Is.EqualTo(125));
            Assert.That(updated.Slots[0].Quantity, Is.EqualTo(425));
        }

        [Test]
        public void SharedRewardAssets_AreAllowedButDuplicateIdsOnDifferentAssetsAreRejected()
        {
            GameSettings game = CreateGame();
            Assert.DoesNotThrow(() => game.Validate());

            game.GoldenWheel.GetSlot(0).Reward = CreateReward(RewardId.Cash);

            Assert.Throws<InvalidOperationException>(() => game.Validate());
        }

        [Test]
        public void MissingGameReferences_AreRejected()
        {
            Assert.Throws<InvalidOperationException>(() => Create<GameSettings>().Validate());
        }

        [Test]
        public void MissingWheelSprites_AreRejected()
        {
            WheelSettings wheel = CreateWheel(false);
            wheel.PointerSprite = null;

            Assert.Throws<InvalidOperationException>(() => wheel.Validate(ZoneType.Safe));
        }

        [Test]
        public void SerializedSlotCountAndMissingSlot_AreValidated()
        {
            WheelSettings wheel = CreateWheel(false);
            SerializedObject serializedWheel = new SerializedObject(wheel);
            serializedWheel.FindProperty("_slots").arraySize = 7;
            serializedWheel.ApplyModifiedPropertiesWithoutUndo();
            Assert.Throws<InvalidOperationException>(() => wheel.Validate(ZoneType.Safe));

            WheelSettings emptyWheel = Create<WheelSettings>();
            emptyWheel.BaseSprite = _sprite;
            emptyWheel.PointerSprite = _sprite;
            Assert.Throws<InvalidOperationException>(() => emptyWheel.Validate(ZoneType.Safe));
        }

        [Test]
        public void Bombs_AreRejectedOnSafeAndSuperProfiles()
        {
            GameSettings game = CreateGame();
            game.SilverWheel.SetSlot(3, CreateBomb());
            Assert.Throws<InvalidOperationException>(() => game.Validate());

            game.SilverWheel = CreateWheel(false);
            game.GoldenWheel.SetSlot(4, CreateBomb());
            Assert.Throws<InvalidOperationException>(() => game.Validate());
        }

        [Test]
        public void NormalProfile_RequiresExactlyOneBomb()
        {
            WheelSettings wheel = CreateWheel(false);
            Assert.Throws<InvalidOperationException>(() => wheel.Validate(ZoneType.Normal));
            wheel.SetSlot(0, CreateBomb());
            wheel.SetSlot(1, CreateBomb());
            Assert.Throws<InvalidOperationException>(() => wheel.Validate(ZoneType.Normal));
        }

        [Test]
        public void BombSlot_CannotCarryARewardOrQuantities()
        {
            WheelSlotSettings slot = CreateBomb();
            slot.Reward = _reward;
            Assert.Throws<InvalidOperationException>(() => slot.Validate());
            slot.Reward = null;
            slot.BaseQuantity = 1;
            Assert.Throws<InvalidOperationException>(() => slot.Validate());
        }

        [Test]
        public void RewardSlot_RequiresAValidRewardAndPositiveQuantity()
        {
            WheelSlotSettings slot = new WheelSlotSettings();
            Assert.Throws<InvalidOperationException>(() => slot.Validate());
            slot.Reward = _reward;
            slot.BaseQuantity = 0;
            Assert.Throws<InvalidOperationException>(() => slot.Validate());
        }

        [Test]
        public void InvalidSlotSettersAndIndices_AreRejected()
        {
            WheelSlotSettings slot = new WheelSlotSettings();
            Assert.Throws<ArgumentOutOfRangeException>(() => slot.OutcomeKind = (SlotOutcomeKind)99);
            Assert.Throws<ArgumentOutOfRangeException>(() => slot.BaseQuantity = -1);
            Assert.Throws<ArgumentOutOfRangeException>(() => slot.QuantityIncreasePerTier = -1);
            WheelSettings wheel = CreateWheel(false);
            Assert.Throws<ArgumentOutOfRangeException>(() => wheel.GetSlot(8));
            Assert.Throws<ArgumentOutOfRangeException>(() => wheel.SetSlot(-1, slot));
            Assert.Throws<ArgumentNullException>(() => wheel.SetSlot(0, null));
        }

        [Test]
        public void InvalidSerializedAmounts_AreRejectedEvenWhenSettersAreBypassed()
        {
            WheelSettings wheel = CreateWheel(false);
            SerializedObject serializedWheel = new SerializedObject(wheel);
            serializedWheel.FindProperty("_slots").GetArrayElementAtIndex(0)
                .FindPropertyRelative("_baseQuantity").longValue = -1;
            serializedWheel.ApplyModifiedPropertiesWithoutUndo();

            Assert.Throws<InvalidOperationException>(() => wheel.Validate(ZoneType.Safe));
        }

        [Test]
        public void NonFiniteAngles_AreRejected()
        {
            WheelSettings wheel = CreateWheel(false);
            Assert.Throws<ArgumentOutOfRangeException>(() => wheel.SlotZeroAngleDegrees = float.NaN);
            Assert.Throws<ArgumentOutOfRangeException>(() => wheel.PointerAngleDegrees = float.PositiveInfinity);
        }

        [TestCase(RewardId.None)]
        [TestCase((RewardId)(-1))]
        [TestCase((RewardId)999)]
        public void RewardId_RejectsUnsetAndUndefinedValues(RewardId rewardId)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _reward.RewardId = rewardId);
            Assert.That(_reward.RewardId, Is.EqualTo(RewardId.Cash));
        }

        [TestCase(RewardId.None)]
        [TestCase((RewardId)999)]
        public void SerializedInvalidRewardId_IsRejectedWhenSettersAreBypassed(RewardId rewardId)
        {
            SerializedObject serializedReward = new SerializedObject(_reward);
            serializedReward.FindProperty("_rewardId").intValue = (int)rewardId;
            serializedReward.ApplyModifiedPropertiesWithoutUndo();

            Assert.Throws<InvalidOperationException>(() => _reward.Validate());
        }

        [Test]
        public void EveryDefinedReward_CanBeUsedWithoutChangingWheelLogic()
        {
            foreach (RewardId rewardId in Enum.GetValues(typeof(RewardId)))
            {
                if (rewardId == RewardId.None)
                {
                    continue;
                }

                GameSettings game = CreateGame();
                game.BronzeWheel.GetSlot(0).Reward = rewardId == RewardId.Cash ? _reward : CreateReward(rewardId);

                Assert.That(game.CreateWheelData(1).Slots[0].RewardId, Is.EqualTo(rewardId));
            }
        }

        [Test]
        public void Reward_RequiresDisplayNameAndIcon()
        {
            Assert.Throws<ArgumentException>(() => _reward.DisplayName = " ");
            _reward.Icon = null;
            Assert.Throws<InvalidOperationException>(() => _reward.Validate());
        }

        private T Create<T>() where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            _objects.Add(instance);
            return instance;
        }

        private RewardSettings CreateReward(RewardId rewardId)
        {
            RewardSettings reward = Create<RewardSettings>();
            reward.RewardId = rewardId;
            reward.DisplayName = "Cash";
            reward.Icon = _sprite;
            return reward;
        }

        private WheelSettings CreateWheel(bool includeBomb)
        {
            WheelSettings wheel = Create<WheelSettings>();
            wheel.BaseSprite = _sprite;
            wheel.PointerSprite = _sprite;
            for (int index = 0; index < wheel.SlotCount; index++)
            {
                wheel.SetSlot(index, new WheelSlotSettings
                {
                    Reward = _reward,
                    BaseQuantity = 100,
                    QuantityIncreasePerTier = 25
                });
            }

            if (includeBomb)
            {
                wheel.SetSlot(7, CreateBomb());
            }

            return wheel;
        }

        private GameSettings CreateGame()
        {
            GameSettings game = Create<GameSettings>();
            game.ZoneSettings = Create<ZoneSettings>();
            game.BronzeWheel = CreateWheel(true);
            game.SilverWheel = CreateWheel(false);
            game.GoldenWheel = CreateWheel(false);
            return game;
        }

        private static WheelSlotSettings CreateBomb()
        {
            return new WheelSlotSettings
            {
                OutcomeKind = SlotOutcomeKind.Bomb,
                BaseQuantity = 0,
                QuantityIncreasePerTier = 0
            };
        }
    }
}
