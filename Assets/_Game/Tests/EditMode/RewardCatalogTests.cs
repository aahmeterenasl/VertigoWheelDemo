using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VertigoWheel.Core.Rewards;
using VertigoWheel.Core.Wheels;
using VertigoWheel.Settings;

namespace VertigoWheel.Tests.EditMode
{
    public sealed class RewardCatalogTests
    {
        private readonly List<UnityEngine.Object> _objects = new List<UnityEngine.Object>();
        private Sprite _sprite;
        private RewardSettings _cash;

        [SetUp]
        public void SetUp()
        {
            Texture2D texture = new Texture2D(2, 2);
            _objects.Add(texture);
            _sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
            _objects.Add(_sprite);
            _cash = CreateReward(RewardId.Cash);
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

        [Test]
        public void Catalog_MapsReferencedPoolAndExactOverrideAssetsByRewardId()
        {
            GameSettings game = CreateGame();
            RewardSettings gold = CreateReward(RewardId.Gold);
            RewardSettings grenade = CreateReward(RewardId.M67Grenade);
            game.SilverWheel.Pool.GetEntry(0).RewardSlot.Reward = gold;
            game.SetZoneOverrides(new[]
            {
                new ZoneWheelOverrideSettings { ZoneNumber = 30, Wheel = CreateFixedWheel(grenade) }
            });

            IReadOnlyDictionary<RewardId, RewardSettings> catalog = game.CreateRewardCatalog();

            Assert.That(catalog.Count, Is.EqualTo(3));
            Assert.That(catalog[RewardId.Cash], Is.SameAs(_cash));
            Assert.That(catalog[RewardId.Gold], Is.SameAs(gold));
            Assert.That(catalog[RewardId.M67Grenade], Is.SameAs(grenade));
        }

        [Test]
        public void Catalog_RejectsDifferentAssetsSharingTheSameRewardId()
        {
            GameSettings game = CreateGame();
            game.GoldenWheel.Pool.GetEntry(0).RewardSlot.Reward = CreateReward(RewardId.Cash);

            Assert.Throws<InvalidOperationException>(() => game.CreateRewardCatalog());
        }

        [Test]
        public void Catalog_CannotExposeOrMutateItsBackingDictionary()
        {
            IReadOnlyDictionary<RewardId, RewardSettings> catalog = CreateGame().CreateRewardCatalog();

            Assert.That(catalog as Dictionary<RewardId, RewardSettings>, Is.Null);
            IDictionary<RewardId, RewardSettings> mutableView = (IDictionary<RewardId, RewardSettings>)catalog;
            Assert.That(mutableView.IsReadOnly, Is.True);
            Assert.Throws<NotSupportedException>(() => mutableView.Add(RewardId.Gold, _cash));
            Assert.Throws<NotSupportedException>(() => mutableView[RewardId.Cash] = _cash);
            Assert.Throws<NotSupportedException>(() => mutableView.Remove(RewardId.Cash));
            Assert.Throws<NotSupportedException>(() => mutableView.Clear());
            Assert.That(catalog.Count, Is.EqualTo(1));
        }

        [Test]
        public void Catalog_MappingStaysDetachedWhenAuthoringSourcesChange()
        {
            GameSettings game = CreateGame();
            IReadOnlyDictionary<RewardId, RewardSettings> previous = game.CreateRewardCatalog();
            RewardSettings gold = CreateReward(RewardId.Gold);
            game.BronzeWheel.Pool.GetEntry(0).RewardSlot.Reward = gold;

            IReadOnlyDictionary<RewardId, RewardSettings> updated = game.CreateRewardCatalog();

            Assert.That(previous.Count, Is.EqualTo(1));
            Assert.That(previous.ContainsKey(RewardId.Gold), Is.False);
            Assert.That(updated.Count, Is.EqualTo(2));
            Assert.That(updated[RewardId.Gold], Is.SameAs(gold));
        }

        [Test]
        public void Catalog_IgnoresUnusedFixedSlotsOnFallbackWheelProfiles()
        {
            GameSettings game = CreateGame();
            RewardSettings unused = CreateReward(RewardId.Gold);
            game.BronzeWheel.SetSlot(0, new WheelSlotSettings { Reward = unused });
            // Even a duplicate asset in an unused fixed slot must not enter the fallback pool catalog.
            game.SilverWheel.SetSlot(0, new WheelSlotSettings { Reward = CreateReward(RewardId.Cash) });

            IReadOnlyDictionary<RewardId, RewardSettings> catalog = game.CreateRewardCatalog();

            Assert.That(catalog.Count, Is.EqualTo(1));
            Assert.That(catalog[RewardId.Cash], Is.SameAs(_cash));
            Assert.That(catalog.ContainsKey(RewardId.Gold), Is.False);
        }

        [Test]
        public void GameplaySettingsValidation_DoesNotRequireUiReferences()
        {
            GameSettings game = CreateGame();

            Assert.That(game.AnimationSettings, Is.Null);
            Assert.That(game.BombIcon, Is.Null);
            Assert.DoesNotThrow(() => game.Validate());
            Assert.DoesNotThrow(() => game.CreateZoneRules());
            Assert.DoesNotThrow(() => game.CreateRewardCatalog());
        }

        private GameSettings CreateGame()
        {
            GameSettings game = Create<GameSettings>();
            game.ZoneSettings = Create<ZoneSettings>();
            game.BronzeWheel = CreatePoolWheel();
            game.SilverWheel = CreatePoolWheel();
            game.GoldenWheel = CreatePoolWheel();
            return game;
        }

        private WheelSettings CreatePoolWheel()
        {
            WheelSettings wheel = Create<WheelSettings>();
            wheel.BaseSprite = _sprite;
            wheel.PointerSprite = _sprite;
            RewardPoolSettings pool = Create<RewardPoolSettings>();
            RewardPoolEntrySettings[] entries = new RewardPoolEntrySettings[WheelData.SlotCount];
            for (int index = 0; index < entries.Length; index++)
            {
                entries[index] = new RewardPoolEntrySettings
                {
                    RewardSlot = new WheelSlotSettings { Reward = _cash, BaseQuantity = 100 + index }
                };
            }

            pool.SetEntries(entries);
            wheel.Pool = pool;
            return wheel;
        }

        private WheelSettings CreateFixedWheel(RewardSettings reward)
        {
            WheelSettings wheel = Create<WheelSettings>();
            wheel.BaseSprite = _sprite;
            wheel.PointerSprite = _sprite;
            for (int index = 0; index < wheel.SlotCount; index++)
            {
                wheel.SetSlot(index, new WheelSlotSettings { Reward = reward, BaseQuantity = 100 + index });
            }

            return wheel;
        }

        private RewardSettings CreateReward(RewardId rewardId)
        {
            RewardSettings reward = Create<RewardSettings>();
            reward.RewardId = rewardId;
            reward.DisplayName = rewardId.ToString();
            reward.Icon = _sprite;
            return reward;
        }

        private T Create<T>() where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            _objects.Add(instance);
            return instance;
        }
    }
}
