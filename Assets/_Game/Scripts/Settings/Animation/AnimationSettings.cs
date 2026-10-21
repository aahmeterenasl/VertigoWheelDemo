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
        private float _rewardFlightDurationSeconds = 0.18f;

        [SerializeField, Min(0f)]
        private float _rewardFlightStaggerSeconds = 0.015f;

        [SerializeField, Min(0f)]
        private float _rewardFlightFadeDurationSeconds = 0.08f;

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

        [Header("Ledger feedback")]
        [SerializeField, Min(0f)] private float _ledgerDurationSeconds = 0.24f;
        [SerializeField, Range(1f, 1.1f)] private float _ledgerPulseScale = 1.06f;

        [Header("Zone transition")]
        [SerializeField, Min(0f)] private float _zoneTransitionSeconds = 0.20f;
        [SerializeField, Min(0f)] private float _wheelCrossfadeSeconds = 0.16f;
        [SerializeField, Range(1f, 1.1f)] private float _milestonePulseScale = 1.04f;

        [Header("Exit confirmation")]
        [SerializeField, Min(0f)] private float _exitOpenSeconds = 0.28f;
        [SerializeField, Min(0f)] private float _exitCloseSeconds = 0.12f;
        [SerializeField, Range(0.9f, 1f)] private float _exitStartScale = 0.97f;

        [Header("Bomb result")]
        [SerializeField, Min(0f)] private float _bombEntranceSeconds = 0.32f;
        [SerializeField, Range(0.9f, 1f)] private float _bombStartScale = 0.96f;
        [SerializeField, Range(1f, 1.1f)] private float _bombHaloScale = 1.05f;

        [Header("Cash-out summary")]
        [SerializeField, Min(0f)] private float _summaryEntranceSeconds = 0.40f;
        [SerializeField, Min(0f)] private float _summaryStaggerSeconds = 0.035f;
        [SerializeField, Range(0f, 30f)] private float _summaryCardLift = 14f;

        [Header("Button feedback")]
        [SerializeField, Min(0f)] private float _buttonPressSeconds = 0.06f;
        [SerializeField, Min(0f)] private float _buttonReleaseSeconds = 0.10f;
        [SerializeField, Range(0.9f, 1f)] private float _buttonPressedScale = 0.96f;

        public float ButtonPressSeconds
        {
            get => _buttonPressSeconds;
            set { ValidateOptionalDuration(value); _buttonPressSeconds = value; }
        }

        public float ButtonReleaseSeconds
        {
            get => _buttonReleaseSeconds;
            set { ValidateOptionalDuration(value); _buttonReleaseSeconds = value; }
        }

        public float ButtonPressedScale
        {
            get => _buttonPressedScale;
            set { ValidateRange(value, 0.9f, 1f); _buttonPressedScale = value; }
        }

        public float SummaryEntranceSeconds
        {
            get => _summaryEntranceSeconds;
            set { ValidateOptionalDuration(value); _summaryEntranceSeconds = value; }
        }

        public float SummaryStaggerSeconds
        {
            get => _summaryStaggerSeconds;
            set { ValidateOptionalDuration(value); _summaryStaggerSeconds = value; }
        }

        public float SummaryCardLift
        {
            get => _summaryCardLift;
            set { ValidateRange(value, 0f, 30f); _summaryCardLift = value; }
        }

        public float BombEntranceSeconds
        {
            get => _bombEntranceSeconds;
            set { ValidateOptionalDuration(value); _bombEntranceSeconds = value; }
        }

        public float BombStartScale
        {
            get => _bombStartScale;
            set { ValidateRange(value, 0.9f, 1f); _bombStartScale = value; }
        }

        public float BombHaloScale
        {
            get => _bombHaloScale;
            set { ValidateRange(value, 1f, 1.1f); _bombHaloScale = value; }
        }

        public float ExitOpenSeconds
        {
            get => _exitOpenSeconds;
            set { ValidateOptionalDuration(value); _exitOpenSeconds = value; }
        }

        public float ExitCloseSeconds
        {
            get => _exitCloseSeconds;
            set { ValidateOptionalDuration(value); _exitCloseSeconds = value; }
        }

        public float ExitStartScale
        {
            get => _exitStartScale;
            set { ValidateRange(value, 0.9f, 1f); _exitStartScale = value; }
        }

        public float ZoneTransitionSeconds
        {
            get => _zoneTransitionSeconds;
            set { ValidateOptionalDuration(value); _zoneTransitionSeconds = value; }
        }

        public float WheelCrossfadeSeconds
        {
            get => _wheelCrossfadeSeconds;
            set { ValidateOptionalDuration(value); _wheelCrossfadeSeconds = value; }
        }

        public float MilestonePulseScale
        {
            get => _milestonePulseScale;
            set { ValidateRange(value, 1f, 1.1f); _milestonePulseScale = value; }
        }

        public float LedgerDurationSeconds
        {
            get => _ledgerDurationSeconds;
            set { ValidateOptionalDuration(value); _ledgerDurationSeconds = value; }
        }

        public float LedgerPulseScale
        {
            get => _ledgerPulseScale;
            set { ValidateRange(value, 1f, 1.1f); _ledgerPulseScale = value; }
        }

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
            ValidateOptionalDuration(_buttonPressSeconds);
            ValidateOptionalDuration(_buttonReleaseSeconds);
            ValidateRange(_buttonPressedScale, 0.9f, 1f);
            ValidateOptionalDuration(_summaryEntranceSeconds);
            ValidateOptionalDuration(_summaryStaggerSeconds);
            ValidateRange(_summaryCardLift, 0f, 30f);
            ValidateOptionalDuration(_bombEntranceSeconds);
            ValidateRange(_bombStartScale, 0.9f, 1f);
            ValidateRange(_bombHaloScale, 1f, 1.1f);
            ValidateOptionalDuration(_exitOpenSeconds);
            ValidateOptionalDuration(_exitCloseSeconds);
            ValidateRange(_exitStartScale, 0.9f, 1f);
            ValidateOptionalDuration(_zoneTransitionSeconds);
            ValidateOptionalDuration(_wheelCrossfadeSeconds);
            ValidateRange(_milestonePulseScale, 1f, 1.1f);
            ValidateOptionalDuration(_ledgerDurationSeconds);
            ValidateRange(_ledgerPulseScale, 1f, 1.1f);
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
