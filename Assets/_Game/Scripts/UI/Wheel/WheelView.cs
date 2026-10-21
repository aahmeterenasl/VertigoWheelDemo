using System;
using System.Collections.Generic;
using UnityEngine;
using VertigoWheel.Core.Rewards;
using VertigoWheel.Core.Wheels;
using VertigoWheel.Settings;

namespace VertigoWheel.UI.Wheel
{
    [ExecuteAlways]
    public sealed class WheelView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Layout source for an unbound wheel and Editor preview. Bind captures this profile for gameplay.")]
        private WheelSettings _wheelSettings;

        [SerializeField]
        private RectTransform _rotationRoot;

        [SerializeField]
        private UnityEngine.UI.Image _baseImage;

        [SerializeField]
        private UnityEngine.UI.Image _pointer;

        [SerializeField]
        private RectTransform _pointerLayout;

        [SerializeField]
        private UnityEngine.UI.Button _spinButton;

        [SerializeField]
        private WheelSlotView[] _slots = new WheelSlotView[8];

        private WheelData _wheel;
        private float _rotationDegrees;
        private double _slotZeroAngle;
        private bool _clockwiseSlotOrder;
        private float _pointerAngle;
        private float _slotRadiusRatio;
        private float _slotSizeRatio;

        public event Action SpinRequested;

        public WheelData BoundWheel => _wheel;
        public float RotationDegrees => _rotationDegrees;
        public double SlotZeroAngleDegrees => _slotZeroAngle;
        public bool ClockwiseSlotOrder => _clockwiseSlotOrder;
        public float PointerAngleDegrees => _pointerAngle;
        public Color HeadingColor => _wheelSettings != null ? _wheelSettings.HeadingColor : Color.white;
        public string HeadingText => _wheelSettings != null ? _wheelSettings.HeadingText : string.Empty;
        public string StatusText => _wheelSettings != null ? _wheelSettings.StatusText : string.Empty;

        public void Bind(
            WheelData wheel,
            WheelSettings settings,
            IReadOnlyDictionary<RewardId, RewardSettings> rewards,
            Sprite bombIcon)
        {
            if (wheel == null || settings == null || rewards == null)
            {
                throw new ArgumentNullException("Wheel data, visual settings, and reward catalog are required.");
            }
            CacheButton();
            if (_rotationRoot == null || _baseImage == null || _pointer == null || _spinButton == null || _spinButton.image == null
                || _slots == null || _slots.Length != WheelData.SlotCount)
            {
                throw new InvalidOperationException("The wheel prefab is missing required view references.");
            }
            // Validate before changing the displayed wheel. Fixed overrides have no pool.
            if (settings.Pool == null)
            {
                settings.Validate(wheel.ZoneType);
            }
            else
            {
                settings.ValidatePool();
            }
            for (int index = 0; index < _slots.Length; index++)
            {
                WheelSlotData slot = wheel.Slots[index];
                if (_slots[index] == null || (slot.OutcomeKind == SlotOutcomeKind.Bomb && bombIcon == null))
                {
                    throw new InvalidOperationException("A wheel slot view or bomb icon is missing.");
                }
                if (slot.OutcomeKind == SlotOutcomeKind.Reward)
                {
                    if (!rewards.TryGetValue(slot.RewardId, out RewardSettings reward) || reward == null)
                    {
                        throw new InvalidOperationException("No visual reward exists for " + slot.RewardId);
                    }
                    reward.Validate();
                }
            }

            _wheel = wheel;
            _wheelSettings = settings;
            CaptureGeometry(settings);
            _baseImage.sprite = settings.BaseSprite;
            _pointer.sprite = settings.PointerSprite;
            _spinButton.image.color = settings.SpinButtonColor;
            for (int index = 0; index < _slots.Length; index++)
            {
                WheelSlotData slot = wheel.Slots[index];
                bool bomb = slot.OutcomeKind == SlotOutcomeKind.Bomb;
                _slots[index].Bind(bomb ? bombIcon : rewards[slot.RewardId].Icon, slot.Quantity, bomb);
            }
            SetRotation(_rotationDegrees % 360f);
            SetSpinInteractable(false);
            RefreshLayout();
        }

        public void SetSpinInteractable(bool interactable)
        {
            CacheButton();
            if (_spinButton != null)
            {
                _spinButton.interactable = interactable && _wheel != null;
            }
        }

        public void SetRotation(float degrees)
        {
            if (float.IsNaN(degrees) || float.IsInfinity(degrees))
            {
                throw new ArgumentOutOfRangeException(nameof(degrees));
            }
            _rotationDegrees = degrees;
            _rotationRoot.localRotation = Quaternion.Euler(0, 0, degrees % 360f);
        }

        private void OnEnable()
        {
            CacheButton();
            RefreshLayout();
            if (Application.IsPlaying(gameObject) && _spinButton != null)
            {
                _spinButton.onClick.AddListener(HandleSpin);
            }
        }

        private void OnDisable()
        {
            if (_spinButton != null)
            {
                _spinButton.onClick.RemoveListener(HandleSpin);
            }
        }

        private void OnValidate() => CacheButton();

        private void CacheButton()
        {
            if (_spinButton == null)
            {
                Transform button = transform.Find("ui_button_spin");
                _spinButton = button != null ? button.GetComponent<UnityEngine.UI.Button>() : null;
            }
        }

        private void HandleSpin()
        {
            if (_spinButton.interactable && _wheel != null)
            {
                SpinRequested?.Invoke();
            }
        }

        private void OnRectTransformDimensionsChange()
        {
            RefreshLayout();
        }

        private void RefreshLayout()
        {
            // Layout callbacks also run while the prefab is being authored or loaded.
            if (_rotationRoot == null || _pointer == null || _spinButton == null
                || _slots == null || _slots.Length != WheelData.SlotCount)
            {
                return;
            }
            foreach (WheelSlotView slot in _slots)
            {
                if (slot == null)
                {
                    return;
                }
            }
            if (_wheel == null)
            {
                if (_wheelSettings == null)
                {
                    return;
                }
                CaptureGeometry(_wheelSettings);
                if (_spinButton.image != null)
                {
                    _spinButton.image.color = _wheelSettings.SpinButtonColor;
                }
            }

            Rect rect = ((RectTransform)transform).rect;
            float size = Mathf.Min(rect.width, rect.height);
            for (int index = 0; index < _slots.Length; index++)
            {
                double angle = WheelAngleCalculator.GetSlotAngleDegrees(index, _slotZeroAngle, _clockwiseSlotOrder);
                _slots[index].SetLayout(PositionAt(angle, size * _slotRadiusRatio),
                    size * _slotSizeRatio, (float)angle);
            }
            RectTransform pointerLayout = _pointerLayout != null ? _pointerLayout : _pointer.rectTransform;
            pointerLayout.anchoredPosition = PositionAt(_pointerAngle, size * 0.43f);
            pointerLayout.sizeDelta = new Vector2(size * 0.085f, size * 0.13f);
            pointerLayout.localRotation = Quaternion.Euler(0, 0, _pointerAngle);
            ((RectTransform)_spinButton.transform).sizeDelta = Vector2.one * size * 0.28f;
        }

        private void CaptureGeometry(WheelSettings settings)
        {
            _slotZeroAngle = settings.SlotZeroAngleDegrees;
            _clockwiseSlotOrder = settings.ClockwiseSlotOrder;
            _pointerAngle = settings.PointerAngleDegrees;
            _slotRadiusRatio = settings.SlotRadiusRatio;
            _slotSizeRatio = settings.SlotSizeRatio;
        }

        private static Vector2 PositionAt(double degrees, float radius)
        {
            double radians = degrees * Math.PI / 180;
            return new Vector2(-(float)Math.Sin(radians), (float)Math.Cos(radians)) * radius;
        }
    }
}
