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

        [SerializeField, Min(0f)]
        private float _dialogDurationSeconds = 0.18f;

        [Header("Reward reveal")]
        [SerializeField, Min(0f)]
        private float _rewardRevealDurationSeconds = 0.32f;

        [SerializeField, Range(0f, 90f)]
        private float _rewardShineDegreesPerSecond = 12f;

        [SerializeField, Range(0.5f, 1f)]
        private float _rewardIconStartScale = 0.88f;

        [SerializeField, Range(1f, 1.1f)]
        private float _rewardIconPeakScale = 1.03f;

        [SerializeField, Range(0f, 30f)]
        private float _rewardTextOffset = 10f;

        [SerializeField]
        private Sprite _rewardGlowSprite;

        [SerializeField]
        private Sprite _rewardShineSprite;

        [Header("Reward arrival")]
        [SerializeField]
        private bool _rewardFlightEnabled = true;

        [SerializeField, Min(0f)]
        private float _rewardFlightDurationSeconds = 0.35f;

        [SerializeField, Min(0f)]
        private float _rewardFlightStaggerSeconds = 0.035f;

        [SerializeField, Min(0f)]
        private float _rewardFlightFadeDurationSeconds = 0.14f;

        [SerializeField, Range(0f, 200f)]
        private float _rewardFlightArcHeight = 40f;

        [SerializeField, Range(0.1f, 1f)]
        private float _rewardFlightStartScale = 0.4f;

        [Header("Wheel feedback")]
        [SerializeField]
        private bool _wheelFeedbackEnabled = true;

        [SerializeField, Range(0f, 15f)]
        private float _pointerTickAngleDegrees = 6f;

        [SerializeField, Min(0f)]
        private float _pointerTickDurationSeconds = 0.08f;

        public bool RewardFlightEnabled
        {
            get => _rewardFlightEnabled;
            set => _rewardFlightEnabled = value;
        }

        public float RewardFlightDurationSeconds
        {
            get => _rewardFlightDurationSeconds;
            set
            {
                ValidateOptionalDuration(value);
                _rewardFlightDurationSeconds = value;
            }
        }

        public float RewardFlightStaggerSeconds
        {
            get => _rewardFlightStaggerSeconds;
            set
            {
                ValidateOptionalDuration(value);
                _rewardFlightStaggerSeconds = value;
            }
        }

        public float RewardFlightFadeDurationSeconds
        {
            get => _rewardFlightFadeDurationSeconds;
            set
            {
                ValidateOptionalDuration(value);
                _rewardFlightFadeDurationSeconds = value;
            }
        }

        public float RewardFlightArcHeight
        {
            get => _rewardFlightArcHeight;
            set
            {
                ValidateRange(value, 0f, 200f);
                _rewardFlightArcHeight = value;
            }
        }

        public float RewardFlightStartScale
        {
            get => _rewardFlightStartScale;
            set
            {
                ValidateRange(value, 0.1f, 1f);
                _rewardFlightStartScale = value;
            }
        }

        public float RewardShineDegreesPerSecond
        {
            get => _rewardShineDegreesPerSecond;
            set
            {
                ValidateRange(value, 0f, 90f);
                _rewardShineDegreesPerSecond = value;
            }
        }

        public float RewardIconStartScale
        {
            get => _rewardIconStartScale;
            set
            {
                ValidateRange(value, 0.5f, 1f);
                _rewardIconStartScale = value;
            }
        }

        public float RewardIconPeakScale
        {
            get => _rewardIconPeakScale;
            set
            {
                ValidateRange(value, 1f, 1.1f);
                _rewardIconPeakScale = value;
            }
        }

        public float RewardTextOffset
        {
            get => _rewardTextOffset;
            set
            {
                ValidateRange(value, 0f, 30f);
                _rewardTextOffset = value;
            }
        }

        public bool WheelFeedbackEnabled
        {
            get => _wheelFeedbackEnabled;
            set => _wheelFeedbackEnabled = value;
        }

        public float PointerTickAngleDegrees
        {
            get => _pointerTickAngleDegrees;
            set
            {
                ValidateTickAngle(value);
                _pointerTickAngleDegrees = value;
            }
        }

        public float PointerTickDurationSeconds
        {
            get => _pointerTickDurationSeconds;
            set
            {
                ValidateOptionalDuration(value);
                _pointerTickDurationSeconds = value;
            }
        }

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

        public float DialogDurationSeconds
        {
            get => _dialogDurationSeconds;
            set
            {
                ValidateOptionalDuration(value);
                _dialogDurationSeconds = value;
            }
        }

        public float RewardRevealDurationSeconds
        {
            get => _rewardRevealDurationSeconds;
            set
            {
                ValidateOptionalDuration(value);
                _rewardRevealDurationSeconds = value;
            }
        }

        public Sprite RewardGlowSprite
        {
            get => _rewardGlowSprite;
            set => _rewardGlowSprite = value;
        }

        public Sprite RewardShineSprite
        {
            get => _rewardShineSprite;
            set => _rewardShineSprite = value;
        }

        /// <summary>Also checks Inspector values, which bypass property setters.</summary>
        public void Validate()
        {
            ValidateDuration(_spinDurationSeconds);
            ValidateFullTurns(_fullTurns);
            ValidateEase(_spinEase);
            ValidateOptionalDuration(_dialogDurationSeconds);
            ValidateOptionalDuration(_rewardRevealDurationSeconds);
            ValidateRange(_rewardShineDegreesPerSecond, 0f, 90f);
            ValidateRange(_rewardIconStartScale, 0.5f, 1f);
            ValidateRange(_rewardIconPeakScale, 1f, 1.1f);
            ValidateRange(_rewardTextOffset, 0f, 30f);
            ValidateOptionalDuration(_rewardFlightDurationSeconds);
            ValidateOptionalDuration(_rewardFlightStaggerSeconds);
            ValidateOptionalDuration(_rewardFlightFadeDurationSeconds);
            ValidateRange(_rewardFlightArcHeight, 0f, 200f);
            ValidateRange(_rewardFlightStartScale, 0.1f, 1f);
            ValidateTickAngle(_pointerTickAngleDegrees);
            ValidateOptionalDuration(_pointerTickDurationSeconds);
        }

        private static void ValidateRange(float value, float minimum, float maximum)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < minimum || value > maximum)
            {
                throw new ArgumentOutOfRangeException(nameof(value), $"Animation value must be between {minimum} and {maximum}.");
            }
        }

        private static void ValidateTickAngle(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f || value > 15f)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Pointer deflection must be between zero and 15 degrees.");
            }
        }

        private static void ValidateOptionalDuration(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Optional animation durations must be nonnegative and finite.");
            }
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
