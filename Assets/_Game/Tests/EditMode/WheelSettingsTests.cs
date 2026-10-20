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

            WheelData wheel = game.CreateWheelData(zoneNumber, new SequenceRandomSource(0));
            long baseQuantity = expected == ZoneType.Normal ? 100 : expected == ZoneType.Safe ? 500 : 1000;

            Assert.That(wheel.ZoneType, Is.EqualTo(expected));
            Assert.That(wheel.Slots[0].Quantity,
                Is.EqualTo(game.ZoneSettings.CreateRules().GetRewardQuantity(zoneNumber, baseQuantity, 25)));
        }

        [Test]
        public void EditingSlots_ChangesFutureDataWithoutChangingExistingData()
        {
            GameSettings game = CreateGame();
            WheelData previous = game.CreateWheelData(6, new SequenceRandomSource(0));
            game.BronzeWheel.GetSlot(0).BaseQuantity = 400;
            WheelData updated = game.CreateWheelData(6, new SequenceRandomSource(0));

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

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(-0.1f)]
        [TestCase(1.1f)]
        public void SpinButtonColor_RejectsInvalidChannelsFromPropertiesAndInspector(float channel)
        {
            WheelSettings wheel = CreateGame().BronzeWheel;
            Color invalid = new Color(channel, 1f, 1f, 1f);
            Assert.Throws<ArgumentOutOfRangeException>(() => wheel.SpinButtonColor = invalid);

            SerializedObject serialized = new SerializedObject(wheel);
            serialized.FindProperty("_spinButtonColor").colorValue = invalid;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.Throws<ArgumentOutOfRangeException>(() => wheel.Validate(ZoneType.Normal));
            Assert.Throws<ArgumentOutOfRangeException>(() => wheel.ValidatePool());
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
            game.SilverWheel.Pool.GetEntry(3).RewardSlot = CreateBomb();
            Assert.Throws<InvalidOperationException>(() => game.Validate());

            game.SilverWheel = CreateWheel(false);
            AddPool(game.SilverWheel);
            game.GoldenWheel.Pool.GetEntry(4).RewardSlot = CreateBomb();
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

                Assert.That(game.CreateWheelData(1, new SequenceRandomSource(0)).Slots[0].RewardId, Is.EqualTo(rewardId));
            }
        }

        [Test]
        public void Reward_RequiresDisplayNameAndIcon()
        {
            Assert.Throws<ArgumentException>(() => _reward.DisplayName = " ");
            _reward.Icon = null;
            Assert.Throws<InvalidOperationException>(() => _reward.Validate());
        }

        [TestCase(5, 1)]
        [TestCase(6, 2)]
        [TestCase(10, 2)]
        [TestCase(11, 1)]
        public void PoolEntries_UseInclusiveTierBoundsAndZeroWeightDisablesThem(int zone, int expectedCount)
        {
            ZoneSettings zones = Create<ZoneSettings>();
            RewardPoolSettings pool = Create<RewardPoolSettings>();
            pool.SetEntries(new[]
            {
                new RewardPoolEntrySettings { RewardSlot = CreateRewardSlot(100), MinimumTier = 0, MaximumTier = 1 },
                new RewardPoolEntrySettings { RewardSlot = CreateRewardSlot(1000), MinimumTier = 1 },
                new RewardPoolEntrySettings { RewardSlot = CreateRewardSlot(50), PoolWeight = 0 }
            });

            Assert.That(pool.CreateEntries(zone, zones.CreateRules()).Count, Is.EqualTo(expectedCount));
        }

        [Test]
        public void ExactZoneOverride_TakesPriorityAndDoesNotConsumeGenerationRandomness()
        {
            GameSettings game = CreateGame();
            WheelSettings exact = CreateWheel(false);
            exact.GetSlot(0).BaseQuantity = 1000;
            game.SetZoneOverrides(new[] { new ZoneWheelOverrideSettings { ZoneNumber = 30, Wheel = exact } });
            SequenceRandomSource random = new SequenceRandomSource(0);

            WheelData wheel = game.CreateWheelData(30, random);

            Assert.That(wheel.Slots[0].Quantity, Is.EqualTo(1125));
            Assert.That(random.SamplesConsumed, Is.Zero);
            Assert.That(game.GetWheelSettingsForZone(30), Is.SameAs(exact));
            Assert.That(game.GetWheelSettingsForZone(60), Is.SameAs(game.GoldenWheel));
            game.CreateWheelData(60, random);
            Assert.That(random.SamplesConsumed, Is.EqualTo(8));
        }

        [Test]
        public void DuplicateOverridesAndBombsInGoldenOverrides_AreRejected()
        {
            GameSettings game = CreateGame();
            ZoneWheelOverrideSettings exact = new ZoneWheelOverrideSettings { ZoneNumber = 30, Wheel = CreateWheel(false) };
            game.SetZoneOverrides(new[] { exact, exact });
            Assert.Throws<InvalidOperationException>(() => game.Validate());
            game.SetZoneOverrides(new[] { new ZoneWheelOverrideSettings { ZoneNumber = 30, Wheel = CreateWheel(true) } });
            Assert.Throws<InvalidOperationException>(() => game.Validate());
        }

        [Test]
        public void QuantitiesThatConvergeAtTheCap_CannotCreateRepeatedVisibleRewards()
        {
            GameSettings game = CreateGame();
            game.ZoneSettings.MaximumRewardQuantity = 100;

            Assert.Throws<InvalidOperationException>(() => game.CreateWheelData(5, new SequenceRandomSource(0)));
        }

        [Test]
        public void InvalidTierRangesAndLandingWeights_AreRejected()
        {
            RewardPoolEntrySettings entry = new RewardPoolEntrySettings
            {
                RewardSlot = CreateRewardSlot(100), MinimumTier = 3, MaximumTier = 2
            };
            Assert.Throws<InvalidOperationException>(() => entry.Validate());
            Assert.Throws<ArgumentOutOfRangeException>(() => entry.PoolWeight = -1);
            Assert.Throws<ArgumentOutOfRangeException>(() => entry.MinimumTier = -1);
            Assert.Throws<ArgumentOutOfRangeException>(() => entry.MaximumTier = -2);
            Assert.Throws<ArgumentOutOfRangeException>(() => entry.RewardSlot.LandingWeight = 0);
        }

        private WheelSlotSettings CreateRewardSlot(long quantity)
        {
            return new WheelSlotSettings { Reward = _reward, BaseQuantity = quantity, QuantityIncreasePerTier = 0 };
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
                    BaseQuantity = 100 + index,
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
            AddPool(game.BronzeWheel);
            AddPool(game.SilverWheel);
            AddPool(game.GoldenWheel);
            return game;
        }

        private void AddPool(WheelSettings wheel)
        {
            RewardPoolSettings pool = Create<RewardPoolSettings>();
            List<RewardPoolEntrySettings> entries = new List<RewardPoolEntrySettings>();
            for (int index = 0; index < wheel.SlotCount; index++)
            {
                if (wheel.GetSlot(index).OutcomeKind == SlotOutcomeKind.Reward)
                {
                    entries.Add(new RewardPoolEntrySettings { RewardSlot = wheel.GetSlot(index) });
                }
            }

            pool.SetEntries(entries.ToArray());
            wheel.Pool = pool;
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
