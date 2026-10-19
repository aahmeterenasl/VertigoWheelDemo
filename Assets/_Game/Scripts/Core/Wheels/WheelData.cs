using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using VertigoWheel.Core.Zones;
using VertigoWheel.Core.Rewards;

namespace VertigoWheel.Core.Wheels
{
    /// <summary>A detached snapshot of one zone's eight outcomes.</summary>
    public sealed class WheelData
    {
        private readonly int _zoneNumber;
        private readonly ZoneType _zoneType;
        private readonly ReadOnlyCollection<WheelSlotData> _slots;

        public WheelData(int zoneNumber, ZoneType zoneType, WheelSlotData[] slots)
        {
            if (zoneNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(zoneNumber));
            }

            if (!Enum.IsDefined(typeof(ZoneType), zoneType))
            {
                throw new ArgumentOutOfRangeException(nameof(zoneType));
            }

            if (slots == null)
            {
                throw new ArgumentNullException(nameof(slots));
            }

            if (slots.Length != SlotCount)
            {
                throw new ArgumentException("The supplied wheel art requires eight slots.", nameof(slots));
            }

            WheelSlotData[] copy = (WheelSlotData[])slots.Clone();
            int bombCount = 0;
            HashSet<(RewardId, long)> rewards = new HashSet<(RewardId, long)>();
            foreach (WheelSlotData slot in copy)
            {
                if (slot == null)
                {
                    throw new ArgumentException("Wheel slots cannot be null.", nameof(slots));
                }

                if (slot.OutcomeKind == SlotOutcomeKind.Bomb)
                {
                    bombCount++;
                }
                else if (!rewards.Add((slot.RewardId, slot.Quantity)))
                {
                    throw new ArgumentException("A wheel cannot repeat the same reward and final quantity.", nameof(slots));
                }
            }

            int expectedBombCount = zoneType == ZoneType.Normal ? 1 : 0;
            if (bombCount != expectedBombCount)
            {
                throw new ArgumentException(
                    $"A {zoneType} wheel requires {expectedBombCount} bomb slots; found {bombCount}.",
                    nameof(slots));
            }

            _zoneNumber = zoneNumber;
            _zoneType = zoneType;
            _slots = Array.AsReadOnly(copy);
        }

        public static int SlotCount => 8;
        public int ZoneNumber => _zoneNumber;
        public ZoneType ZoneType => _zoneType;
        public IReadOnlyList<WheelSlotData> Slots => _slots;
    }
}
