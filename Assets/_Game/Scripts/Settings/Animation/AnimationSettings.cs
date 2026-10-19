using System;
using DG.Tweening;
using UnityEngine;

namespace VertigoWheel.Settings
{
    [CreateAssetMenu(fileName = "AnimationSettings", menuName = "Vertigo Wheel/Settings/Animation")]
    public sealed class AnimationSettings : ScriptableObject
    {
        [SerializeField]
        private float _spinDurationSeconds = 4f;

        [SerializeField, Range(1, 20)]
        private int _fullTurns = 5;

        [SerializeField]
        private bool _clockwiseSpin = true;

        [SerializeField]
        private Ease _spinEase = Ease.InOutCubic;

        public float SpinDurationSeconds
        {
            get => _spinDurationSeconds;
            set
            {
                ValidateDuration(value);
                _spinDurationSeconds = value;
            }
        }

        public int FullTurns
        {
            get => _fullTurns;
            set
            {
                ValidateFullTurns(value);
                _fullTurns = value;
            }
        }

        public bool ClockwiseSpin
        {
            get => _clockwiseSpin;
            set => _clockwiseSpin = value;
        }

        public Ease SpinEase
        {
            get => _spinEase;
            set
            {
                ValidateEase(value);
                _spinEase = value;
            }
        }

        /// <summary>Also checks Inspector values, which bypass property setters.</summary>
        public void Validate()
        {
            ValidateDuration(_spinDurationSeconds);
            ValidateFullTurns(_fullTurns);
            ValidateEase(_spinEase);
        }

        private static void ValidateDuration(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Spin duration must be positive and finite.");
            }
        }

        private static void ValidateFullTurns(int value)
        {
            if (value < 1 || value > 20)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Spin turns must be between 1 and 20.");
            }
        }

        private static void ValidateEase(Ease value)
        {
            string name = Enum.GetName(typeof(Ease), value);
            if (name == null || name == "UNSET" || name.StartsWith("INTERNAL_", StringComparison.Ordinal))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Select a public DOTween ease.");
            }
        }
    }
}
