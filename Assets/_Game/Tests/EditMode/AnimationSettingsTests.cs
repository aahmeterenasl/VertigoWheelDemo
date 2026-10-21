using System;
using System.Collections.Generic;
using DG.Tweening;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VertigoWheel.Core.Rewards;
using VertigoWheel.Core.Wheels;
using VertigoWheel.Core.Zones;
using VertigoWheel.Settings;

namespace VertigoWheel.Tests.EditMode
{
    public sealed class AnimationSettingsTests
    {
        private readonly List<UnityEngine.Object> _objects = new List<UnityEngine.Object>();
        private AnimationSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _settings = Create<AnimationSettings>();
        }

        [TearDown]
        public void TearDown()
        {
            for (int index = _objects.Count - 1; index >= 0; index--)
            {
                UnityEngine.Object.DestroyImmediate(_objects[index]);
            }
            _objects.Clear();
        }

        [Test]
        public void Defaults_ProvideAValidClockwiseSpin()
        {
            Assert.That(_settings.SpinDurationSeconds, Is.EqualTo(4f));
            Assert.That(_settings.FullTurns, Is.EqualTo(5));
            Assert.That(_settings.ClockwiseSpin, Is.True);
            Assert.That(_settings.SpinEase, Is.EqualTo(Ease.InOutCubic));
            Assert.That(_settings.DialogDurationSeconds, Is.EqualTo(0.18f));
            Assert.That(_settings.RewardRevealDurationSeconds, Is.EqualTo(0.32f));
            Assert.DoesNotThrow(() => _settings.Validate());
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidZoneTransitionValues_RejectSetterAndInspectorPaths(float value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _settings.ZoneTransitionSeconds = value);
            Assert.Throws<ArgumentOutOfRangeException>(() => _settings.WheelCrossfadeSeconds = value);
            using (var data = new SerializedObject(_settings))
            {
                data.FindProperty("_zoneTransitionSeconds").floatValue = value;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => _settings.Validate());
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidExitDurations_RejectSetterAndInspectorPaths(float duration)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _settings.ExitOpenSeconds = duration);
            Assert.Throws<ArgumentOutOfRangeException>(() => _settings.ExitCloseSeconds = duration);
            using (var data = new SerializedObject(_settings))
            {
                data.FindProperty("_exitCloseSeconds").floatValue = duration;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => _settings.Validate());
        }

        [Test]
        public void DialogDurations_SupportImmediateAndAnimatedReveals()
        {
            _settings.DialogDurationSeconds = 0f;
            _settings.RewardRevealDurationSeconds = 0.5f;
            Assert.That(_settings.DialogDurationSeconds, Is.Zero);
            Assert.That(_settings.RewardRevealDurationSeconds, Is.EqualTo(0.5f));
            Assert.DoesNotThrow(() => _settings.Validate());
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidDialogDurations_DoNotReplaceAuthoredValues(float duration)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _settings.DialogDurationSeconds = duration);
            Assert.Throws<ArgumentOutOfRangeException>(() => _settings.RewardRevealDurationSeconds = duration);
            Assert.That(_settings.DialogDurationSeconds, Is.EqualTo(0.18f));
            Assert.That(_settings.RewardRevealDurationSeconds, Is.EqualTo(0.32f));
        }

        [TestCase("_dialogDurationSeconds", -1f)]
        [TestCase("_dialogDurationSeconds", float.NaN)]
        [TestCase("_dialogDurationSeconds", float.PositiveInfinity)]
        [TestCase("_rewardRevealDurationSeconds", -1f)]
        [TestCase("_rewardRevealDurationSeconds", float.NaN)]
        [TestCase("_rewardRevealDurationSeconds", float.PositiveInfinity)]
        [TestCase("_pointerTickDurationSeconds", -1f)]
        [TestCase("_pointerTickDurationSeconds", float.NaN)]
        public void SerializedInvalidDialogDurations_AreValidated(string field, float duration)
        {
            SerializedObject serialized = new SerializedObject(_settings);
            serialized.FindProperty(field).floatValue = duration;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.Throws<ArgumentOutOfRangeException>(() => _settings.Validate());
        }

        [TestCase(-1f)]
        [TestCase(16f)]
        [TestCase(float.NaN)]
        public void InvalidFeedbackAngle_DoesNotReplaceAuthoredValue(float angle)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _settings.PointerTickAngleDegrees = angle);
            Assert.That(_settings.PointerTickAngleDegrees, Is.EqualTo(6f));
        }

        [TestCase("_rewardShineDegreesPerSecond", -1f)]
        [TestCase("_rewardShineDegreesPerSecond", float.NaN)]
        [TestCase("_rewardIconStartScale", 0.49f)]
        [TestCase("_rewardIconStartScale", 1.01f)]
        [TestCase("_rewardIconPeakScale", 0.99f)]
        [TestCase("_rewardIconPeakScale", float.PositiveInfinity)]
        [TestCase("_rewardTextOffset", -1f)]
        [TestCase("_rewardTextOffset", 31f)]
        [TestCase("_rewardFlightDurationSeconds", -1f)]
        [TestCase("_rewardFlightStaggerSeconds", float.NaN)]
        [TestCase("_rewardFlightFadeDurationSeconds", float.PositiveInfinity)]
        [TestCase("_rewardFlightArcHeight", 201f)]
        [TestCase("_rewardFlightStartScale", 0f)]
        public void InvalidSerializedRevealMotion_IsRejectedBeforeItCanBeApplied(string field, float value)
        {
            using (var serialized = new SerializedObject(_settings))
            {
                serialized.FindProperty(field).floatValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => _settings.Validate());
        }

        [Test]
        public void AuthoredSpinValues_AreAvailableToTheAnimator()
        {
            _settings.SpinDurationSeconds = 0.5f;
            _settings.FullTurns = 20;
            _settings.ClockwiseSpin = false;
            _settings.SpinEase = Ease.OutBounce;

            Assert.DoesNotThrow(() => _settings.Validate());
            Assert.That(_settings.SpinDurationSeconds, Is.EqualTo(0.5f));
            Assert.That(_settings.FullTurns, Is.EqualTo(20));
            Assert.That(_settings.ClockwiseSpin, Is.False);
            Assert.That(_settings.SpinEase, Is.EqualTo(Ease.OutBounce));
            _settings.FullTurns = 1;
            Assert.DoesNotThrow(() => _settings.Validate());
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidDuration_DoesNotReplaceValidValue(float duration)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _settings.SpinDurationSeconds = duration);
            Assert.That(_settings.SpinDurationSeconds, Is.EqualTo(4f));
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(21)]
        [TestCase(int.MaxValue)]
        public void InvalidTurnCount_DoesNotReplaceValidValue(int turns)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _settings.FullTurns = turns);
            Assert.That(_settings.FullTurns, Is.EqualTo(5));
        }

        [TestCase((Ease)(-1))]
        [TestCase((Ease)int.MaxValue)]
        [TestCase(Ease.INTERNAL_Custom)]
        [TestCase(Ease.INTERNAL_Zero)]
        public void InvalidEase_DoesNotReplaceValidValue(Ease ease)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _settings.SpinEase = ease);
            Assert.That(_settings.SpinEase, Is.EqualTo(Ease.InOutCubic));
        }

        [Test]
        public void EveryPresentReservedEase_IsRejected()
        {
            foreach (string name in new[] { "INTERNAL_Custom", "INTERNAL_Zero", "INTERNAL_Direct", "UNSET" })
            {
                if (Enum.TryParse(name, out Ease ease))
                {
                    Assert.Throws<ArgumentOutOfRangeException>(() => _settings.SpinEase = ease, name);
                }
            }
        }

        [TestCase(0f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void SerializedInvalidDuration_IsValidated(float duration)
        {
            SerializedObject serialized = new SerializedObject(_settings);
            serialized.FindProperty("_spinDurationSeconds").floatValue = duration;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.Throws<ArgumentOutOfRangeException>(() => _settings.Validate());
        }

        [TestCase("_fullTurns", 0)]
        [TestCase("_fullTurns", 21)]
        [TestCase("_spinEase", -1)]
        [TestCase("_spinEase", (int)Ease.INTERNAL_Custom)]
        public void SerializedInvalidTurnCountAndEase_AreValidated(string field, int value)
        {
            SerializedObject serialized = new SerializedObject(_settings);
            serialized.FindProperty(field).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.Throws<ArgumentOutOfRangeException>(() => _settings.Validate());
        }

        [Test]
        public void WheelSlotRatios_DefaultsAndUpperBoundaryAreValid()
        {
            WheelSettings wheel = CreateWheel();
            Assert.That(wheel.SlotRadiusRatio, Is.EqualTo(0.30f));
            Assert.That(wheel.SlotSizeRatio, Is.EqualTo(0.17f));

            wheel.SlotRadiusRatio = 0.5f;
            wheel.SlotSizeRatio = 0.5f;
            Assert.DoesNotThrow(() => wheel.Validate(ZoneType.Safe));
            Assert.DoesNotThrow(() => wheel.ValidatePool());
        }

        [TestCase(0f)]
        [TestCase(-0.1f)]
        [TestCase(0.5001f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidWheelSlotRatios_DoNotReplaceValidValues(float ratio)
        {
            WheelSettings wheel = Create<WheelSettings>();
            Assert.Throws<ArgumentOutOfRangeException>(() => wheel.SlotRadiusRatio = ratio);
            Assert.Throws<ArgumentOutOfRangeException>(() => wheel.SlotSizeRatio = ratio);
            Assert.That(wheel.SlotRadiusRatio, Is.EqualTo(0.30f));
            Assert.That(wheel.SlotSizeRatio, Is.EqualTo(0.17f));
        }

        [TestCase("_slotRadiusRatio", 0f)]
        [TestCase("_slotRadiusRatio", 0.5001f)]
        [TestCase("_slotRadiusRatio", float.NaN)]
        [TestCase("_slotSizeRatio", 0f)]
        [TestCase("_slotSizeRatio", 0.5001f)]
        [TestCase("_slotSizeRatio", float.PositiveInfinity)]
        public void SerializedInvalidWheelSlotRatios_AreValidatedForBothWheelSources(string field, float ratio)
        {
            WheelSettings wheel = CreateWheel();
            SerializedObject serialized = new SerializedObject(wheel);
            serialized.FindProperty(field).floatValue = ratio;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.Throws<ArgumentOutOfRangeException>(() => wheel.Validate(ZoneType.Safe));
            Assert.Throws<ArgumentOutOfRangeException>(() => wheel.ValidatePool());
        }

        private T Create<T>() where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            _objects.Add(instance);
            return instance;
        }

        private WheelSettings CreateWheel()
        {
            Texture2D texture = new Texture2D(2, 2);
            _objects.Add(texture);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
            _objects.Add(sprite);
            RewardSettings reward = Create<RewardSettings>();
            reward.RewardId = RewardId.Cash;
            reward.DisplayName = "Cash";
            reward.Icon = sprite;

            WheelSettings wheel = Create<WheelSettings>();
            wheel.BaseSprite = sprite;
            wheel.PointerSprite = sprite;
            RewardPoolEntrySettings[] entries = new RewardPoolEntrySettings[WheelData.SlotCount];
            for (int index = 0; index < entries.Length; index++)
            {
                WheelSlotSettings slot = new WheelSlotSettings { Reward = reward, BaseQuantity = index + 1 };
                wheel.SetSlot(index, slot);
                entries[index] = new RewardPoolEntrySettings { RewardSlot = slot };
            }

            RewardPoolSettings pool = Create<RewardPoolSettings>();
            pool.SetEntries(entries);
            wheel.Pool = pool;
            return wheel;
        }
    }
}
