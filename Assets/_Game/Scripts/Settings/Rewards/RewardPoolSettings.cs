using System;
using System.Collections.Generic;
using UnityEngine;
using VertigoWheel.Core.Wheels;
using VertigoWheel.Core.Zones;

namespace VertigoWheel.Settings
{
    [CreateAssetMenu(fileName = "RewardPoolSettings", menuName = "Vertigo Wheel/Settings/Reward Pool")]
    public sealed class RewardPoolSettings : ScriptableObject
    {
        [SerializeField]
        private RewardPoolEntrySettings[] _entries = Array.Empty<RewardPoolEntrySettings>();

        public int EntryCount => _entries?.Length ?? 0;

        public RewardPoolEntrySettings GetEntry(int index)
        {
            if (index < 0 || index >= EntryCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return _entries[index];
        }

        public void SetEntries(RewardPoolEntrySettings[] entries)
        {
            _entries = entries == null
                ? throw new ArgumentNullException(nameof(entries))
                : (RewardPoolEntrySettings[])entries.Clone();
        }

        public void Validate()
        {
            if (EntryCount == 0)
            {
                throw new InvalidOperationException($"Reward pool '{name}' has no entries.");
            }

            foreach (RewardPoolEntrySettings entry in _entries)
            {
                if (entry == null)
                {
                    throw new InvalidOperationException($"Reward pool '{name}' has a missing entry.");
                }
                entry.Validate();
            }
        }

        public IReadOnlyList<RewardPoolEntryData> CreateEntries(int zoneNumber, ZoneRules rules, int quantityMultiplier = 1)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }
            if (quantityMultiplier < 1) throw new ArgumentOutOfRangeException(nameof(quantityMultiplier));
            Validate();
            int tier = rules.GetRewardTier(zoneNumber);
            List<RewardPoolEntryData> entries = new List<RewardPoolEntryData>();
            foreach (RewardPoolEntrySettings entry in _entries)
            {
                if (entry.IsEligible(tier))
                {
                    entries.Add(new RewardPoolEntryData(entry.RewardSlot.CreateData(zoneNumber, rules, quantityMultiplier), entry.PoolWeight));
                }
            }

            return entries.AsReadOnly();
        }
    }
}
