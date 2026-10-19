using System;
using UnityEngine;

namespace VertigoWheel.Settings
{
    [Serializable]
    public sealed class ZoneWheelOverrideSettings
    {
        [SerializeField, Min(1)]
        private int _zoneNumber = 1;

        [SerializeField]
        private WheelSettings _wheel;

        public int ZoneNumber
        {
            get => _zoneNumber;
            set
            {
                if (value < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }
                _zoneNumber = value;
            }
        }

        public WheelSettings Wheel
        {
            get => _wheel;
            set => _wheel = value;
        }

        public void Validate()
        {
            if (_zoneNumber < 1 || _wheel == null)
            {
                throw new InvalidOperationException("A zone override needs a positive zone number and a fixed wheel.");
            }
        }
    }
}
