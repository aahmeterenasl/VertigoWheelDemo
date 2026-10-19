using System;
using System.Collections.Generic;
using NUnit.Framework;
using VertigoWheel.Core.Rewards;
using VertigoWheel.Core.Wheels;
using VertigoWheel.Core.Zones;

namespace VertigoWheel.Tests.EditMode
{
    public sealed class WheelDataTests
    {
        [Test]
        public void Slots_AreDetachedFromTheInputArrayAndCannotBeReplaced()
        {
            WheelSlotData[] slots = CreateSlots();
            WheelData wheel = new WheelData(5, ZoneType.Safe, slots);
            slots[0] = WheelSlotData.CreateBomb();

            Assert.That(wheel.Slots[0].OutcomeKind, Is.EqualTo(SlotOutcomeKind.Reward));
            Assert.That(wheel.Slots[0].RewardId, Is.EqualTo(RewardId.Cash));
            Assert.That(wheel.Slots[0].Quantity, Is.EqualTo(100));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<WheelSlotData>)wheel.Slots)[0] = WheelSlotData.CreateBomb());
        }

        [TestCase(ZoneType.Normal, 0)]
        [TestCase(ZoneType.Normal, 2)]
        [TestCase(ZoneType.Safe, 1)]
        [TestCase(ZoneType.Super, 1)]
        public void InvalidBombCounts_AreRejected(ZoneType zoneType, int bombCount)
        {
            WheelSlotData[] slots = CreateSlots();
            for (int index = 0; index < bombCount; index++)
            {
                slots[index] = WheelSlotData.CreateBomb();
            }

            Assert.Throws<ArgumentException>(() => new WheelData(1, zoneType, slots));
        }

        [TestCase(ZoneType.Normal, 1)]
        [TestCase(ZoneType.Safe, 0)]
        [TestCase(ZoneType.Super, 0)]
        public void CorrectBombCounts_AreAccepted(ZoneType zoneType, int bombCount)
        {
            WheelSlotData[] slots = CreateSlots();
            if (bombCount == 1)
            {
                slots[7] = WheelSlotData.CreateBomb();
            }

            WheelData wheel = new WheelData(30, zoneType, slots);

            Assert.That(wheel.ZoneNumber, Is.EqualTo(30));
            Assert.That(wheel.ZoneType, Is.EqualTo(zoneType));
            Assert.That(wheel.Slots.Count, Is.EqualTo(8));
        }

        [TestCase(7)]
        [TestCase(9)]
        public void IncorrectSlotCount_IsRejected(int slotCount)
        {
            Assert.Throws<ArgumentException>(() =>
                new WheelData(5, ZoneType.Safe, new WheelSlotData[slotCount]));
        }

        [Test]
        public void InvalidWheelArguments_AreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new WheelData(0, ZoneType.Safe, CreateSlots()));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WheelData(1, (ZoneType)99, CreateSlots()));
            Assert.Throws<ArgumentNullException>(() => new WheelData(5, ZoneType.Safe, null));
            Assert.Throws<ArgumentException>(() => new WheelData(5, ZoneType.Safe, new WheelSlotData[8]));
        }

        [Test]
        public void RewardAndBombFactories_EnforceOutcomeData()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => WheelSlotData.CreateReward(RewardId.None, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => WheelSlotData.CreateReward((RewardId)999, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => WheelSlotData.CreateReward(RewardId.Cash, 0));
            WheelSlotData bomb = WheelSlotData.CreateBomb();

            Assert.That(bomb.OutcomeKind, Is.EqualTo(SlotOutcomeKind.Bomb));
            Assert.That(bomb.RewardId, Is.EqualTo(RewardId.None));
            Assert.That(bomb.Quantity, Is.Zero);
        }

        private static WheelSlotData[] CreateSlots()
        {
            WheelSlotData[] slots = new WheelSlotData[WheelData.SlotCount];
            for (int index = 0; index < slots.Length; index++)
            {
                slots[index] = WheelSlotData.CreateReward(RewardId.Cash, 100);
            }

            return slots;
        }
    }
}
