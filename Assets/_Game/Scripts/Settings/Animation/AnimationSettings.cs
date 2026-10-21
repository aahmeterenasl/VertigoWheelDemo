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

        [SerializeField, Min(0f)]
        private float _rewardRevealDurationSeconds = 0.25f;

        [SerializeField]
        private Sprite _rewardGlowSprite;

        [SerializeField]
        private Sprite _rewardShineSprite;

        [Header("Wheel feedback")]
        [SerializeField]
        private bool _wheelFeedbackEnabled = true;

        [SerializeField, Range(0f, 15f)]
        private float _pointerTickAngleDegrees = 6f;

        [SerializeField, Min(0f)]
        private float _pointerTickDurationSeconds = 0.08f;

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
            ValidateTickAngle(_pointerTickAngleDegrees);
            ValidateOptionalDuration(_pointerTickDurationSeconds);
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
