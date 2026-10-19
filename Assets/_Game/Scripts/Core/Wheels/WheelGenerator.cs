using System;
using System.Collections.Generic;
using VertigoWheel.Core.Randomness;
using VertigoWheel.Core.Rewards;
using VertigoWheel.Core.Zones;

namespace VertigoWheel.Core.Wheels
{
    public sealed class WheelGenerator
    {
        public WheelData Generate(
            int zoneNumber,
            ZoneType zoneType,
            IReadOnlyList<RewardPoolEntryData> entries,
            IRandomSource random,
            int bombSlotIndex = 7,
            int bombLandingWeight = 1)
        {
            if (zoneNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(zoneNumber));
            }

            if (!Enum.IsDefined(typeof(ZoneType), zoneType))
            {
                throw new ArgumentOutOfRangeException(nameof(zoneType));
            }

            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            if (bombSlotIndex < 0 || bombSlotIndex >= WheelData.SlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(bombSlotIndex));
            }

            if (bombLandingWeight < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(bombLandingWeight));
            }

            List<RewardPoolEntryData> candidates = new List<RewardPoolEntryData>();
            HashSet<(RewardId, long)> distinct = new HashSet<(RewardId, long)>();
            foreach (RewardPoolEntryData entry in entries)
            {
                if (entry == null)
                {
                    throw new ArgumentException("Pool entries cannot be null.", nameof(entries));
                }

                if (entry.PoolWeight > 0)
                {
                    candidates.Add(entry);
                    distinct.Add((entry.Reward.RewardId, entry.Reward.Quantity));
                }
            }

            bool hasBomb = zoneType == ZoneType.Normal;
            int required = hasBomb ? WheelData.SlotCount - 1 : WheelData.SlotCount;
            if (distinct.Count < required)
            {
                throw new InvalidOperationException(
                    $"Zone {zoneNumber} requires {required} distinct eligible reward/amount pairs; found {distinct.Count}.");
            }

            WheelSlotData[] slots = new WheelSlotData[WheelData.SlotCount];
            for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
            {
                if (hasBomb && slotIndex == bombSlotIndex)
                {
                    slots[slotIndex] = WheelSlotData.CreateBomb(bombLandingWeight);
                    continue;
                }

                List<int> weights = new List<int>(candidates.Count);
                foreach (RewardPoolEntryData candidate in candidates)
                {
                    weights.Add(candidate.PoolWeight);
                }

                WheelSlotData selected = candidates[WeightedSelection.SelectIndex(weights, random)].Reward;
                slots[slotIndex] = selected;

                // Remove equivalent final outcomes too, including quantities that converged after clamping.
                candidates.RemoveAll(candidate => candidate.Reward.RewardId == selected.RewardId
                    && candidate.Reward.Quantity == selected.Quantity);
            }

            return new WheelData(zoneNumber, zoneType, slots);
        }
    }
}
