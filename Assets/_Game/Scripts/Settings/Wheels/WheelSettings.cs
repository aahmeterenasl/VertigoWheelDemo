using System;
using UnityEngine;
using VertigoWheel.Core.Randomness;
using VertigoWheel.Core.Wheels;
using VertigoWheel.Core.Zones;

namespace VertigoWheel.Settings
{
    [CreateAssetMenu(fileName = "WheelSettings", menuName = "Vertigo Wheel/Settings/Wheel")]
    public sealed class WheelSettings : ScriptableObject
    {
        [SerializeField]
        private Sprite _baseSprite;

        [SerializeField]
        private Sprite _pointerSprite;

        [SerializeField]
        [Tooltip("Angle of slot zero from up; positive angles are counterclockwise.")]
        private float _slotZeroAngleDegrees;

        [SerializeField]
        private bool _clockwiseSlotOrder = true;

        [SerializeField]
        [Tooltip("Angle of the stationary pointer from up.")]
        private float _pointerAngleDegrees;

        [SerializeField]
        [Tooltip("Distance from the wheel center to each slot, relative to the wheel diameter.")]
        private float _slotRadiusRatio = 0.30f;

        [SerializeField]
        [Tooltip("Slot size relative to the wheel diameter.")]
        private float _slotSizeRatio = 0.17f;

        [Header("Fixed slots for exact-zone overrides")]
        [SerializeField]
        private WheelSlotSettings[] _slots = new WheelSlotSettings[WheelData.SlotCount];

        [Header("Pool for fallback generation")]
        [SerializeField]
        private RewardPoolSettings _pool;

        [SerializeField, Range(0, 7)]
        private int _bombSlotIndex = 7;

        [SerializeField, Min(1)]
        private int _bombLandingWeight = 1;

        public RewardPoolSettings Pool
        {
            get => _pool;
            set => _pool = value;
        }

        public int BombSlotIndex
        {
            get => _bombSlotIndex;
            set
            {
                if (value < 0 || value >= WheelData.SlotCount)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }
                _bombSlotIndex = value;
            }
        }

        public int BombLandingWeight
        {
            get => _bombLandingWeight;
            set
            {
                if (value < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }
                _bombLandingWeight = value;
            }
        }

        public Sprite BaseSprite
        {
            get => _baseSprite;
            set => _baseSprite = value;
        }

        public Sprite PointerSprite
        {
            get => _pointerSprite;
            set => _pointerSprite = value;
        }

        public float SlotZeroAngleDegrees
        {
            get => _slotZeroAngleDegrees;
            set
            {
                ValidateFiniteAngle(value);
                _slotZeroAngleDegrees = value;
            }
        }

        public bool ClockwiseSlotOrder
        {
            get => _clockwiseSlotOrder;
            set => _clockwiseSlotOrder = value;
        }

        public float PointerAngleDegrees
        {
            get => _pointerAngleDegrees;
            set
            {
                ValidateFiniteAngle(value);
                _pointerAngleDegrees = value;
            }
        }

        public int SlotCount => _slots?.Length ?? 0;

        public float SlotRadiusRatio
        {
            get => _slotRadiusRatio;
            set
            {
                ValidateSlotRatio(value);
                _slotRadiusRatio = value;
            }
        }

        public float SlotSizeRatio
        {
            get => _slotSizeRatio;
            set
            {
                ValidateSlotRatio(value);
                _slotSizeRatio = value;
            }
        }

        public WheelSlotSettings GetSlot(int index)
        {
            ValidateSlotIndex(index);
            return _slots[index];
        }

        public void SetSlot(int index, WheelSlotSettings slot)
        {
            ValidateSlotIndex(index);
            _slots[index] = slot ?? throw new ArgumentNullException(nameof(slot));
        }

        public void Validate(ZoneType zoneType)
        {
            if (!Enum.IsDefined(typeof(ZoneType), zoneType))
            {
                throw new ArgumentOutOfRangeException(nameof(zoneType));
            }

            if (_baseSprite == null || _pointerSprite == null || SlotCount != WheelData.SlotCount)
            {
                throw new InvalidOperationException($"Wheel '{name}' needs base/pointer sprites and exactly eight slots.");
            }

            ValidateFiniteAngle(_slotZeroAngleDegrees);
            ValidateFiniteAngle(_pointerAngleDegrees);
            ValidateSlotRatio(_slotRadiusRatio);
            ValidateSlotRatio(_slotSizeRatio);
            int bombCount = 0;
            for (int index = 0; index < _slots.Length; index++)
            {
                WheelSlotSettings slot = _slots[index];
                if (slot == null)
                {
                    throw new InvalidOperationException($"Wheel '{name}' is missing slot {index}.");
                }

                slot.Validate();
                if (slot.OutcomeKind == SlotOutcomeKind.Bomb)
                {
                    bombCount++;
                }
            }

            int expectedBombCount = zoneType == ZoneType.Normal ? 1 : 0;
            if (bombCount != expectedBombCount)
            {
                throw new InvalidOperationException(
                    $"Wheel '{name}' for {zoneType} requires {expectedBombCount} bomb slots; found {bombCount}.");
            }
        }

        public WheelData CreateData(int zoneNumber, ZoneRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            ZoneType zoneType = rules.GetZoneType(zoneNumber);
            Validate(zoneType);
            WheelSlotData[] slots = new WheelSlotData[SlotCount];
            for (int index = 0; index < slots.Length; index++)
            {
                slots[index] = _slots[index].CreateData(zoneNumber, rules);
            }

            return new WheelData(zoneNumber, zoneType, slots);
        }

        public void ValidatePool()
        {
            if (_baseSprite == null || _pointerSprite == null || _pool == null
                || _bombSlotIndex < 0 || _bombSlotIndex >= WheelData.SlotCount || _bombLandingWeight < 1)
            {
                throw new InvalidOperationException($"Wheel '{name}' needs sprites, a pool, and valid bomb settings.");
            }

            ValidateFiniteAngle(_slotZeroAngleDegrees);
            ValidateFiniteAngle(_pointerAngleDegrees);
            ValidateSlotRatio(_slotRadiusRatio);
            ValidateSlotRatio(_slotSizeRatio);
            _pool.Validate();
        }

        public WheelData CreateFromPool(int zoneNumber, ZoneRules rules, IRandomSource random)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }
            ValidatePool();
            return new WheelGenerator().Generate(zoneNumber, rules.GetZoneType(zoneNumber),
                _pool.CreateEntries(zoneNumber, rules), random, _bombSlotIndex, _bombLandingWeight);
        }

        private void ValidateSlotIndex(int index)
        {
            if (index < 0 || index >= SlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        private static void ValidateFiniteAngle(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Wheel angles must be finite.");
            }
        }

        private static void ValidateSlotRatio(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0 || value > 0.5f)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Slot ratios must be positive, finite, and at most 0.5.");
            }
        }
    }
}
