using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VertigoWheel.Core.Zones;
using VertigoWheel.Settings;

namespace VertigoWheel.Tests.EditMode
{
    public sealed class ZoneSettingsTests
    {
        private ZoneSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<ZoneSettings>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_settings);
        }

        [Test]
        public void Defaults_AllowCashOutInNormalZones()
        {
            Assert.That(_settings.CashOutPolicy, Is.EqualTo(CashOutPolicy.AnyZone));
            Assert.That(_settings.CreateRules().AllowsCashOut(1), Is.True);
        }

        [Test]
        public void ChangingSettings_UpdatesFreshRulesWithoutMutatingAnExistingSnapshot()
        {
            ZoneRules previousRules = _settings.CreateRules();
            _settings.CashOutPolicy = CashOutPolicy.SafeAndSuperZonesOnly;
            _settings.SafeZoneInterval = 3;

            ZoneRules updatedRules = _settings.CreateRules();

            Assert.That(previousRules.AllowsCashOut(1), Is.True);
            Assert.That(updatedRules.AllowsCashOut(1), Is.False);
            Assert.That(previousRules.GetZoneType(3), Is.EqualTo(ZoneType.Normal));
            Assert.That(updatedRules.GetZoneType(3), Is.EqualTo(ZoneType.Safe));
        }

        [Test]
        public void InvalidSetterValues_DoNotReplaceValidSettings()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _settings.SafeZoneInterval = 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => _settings.SuperZoneInterval = 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => _settings.RewardTierInterval = 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => _settings.MaximumRewardQuantity = 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => _settings.CashOutPolicy = (CashOutPolicy)99);

            Assert.That(_settings.SafeZoneInterval, Is.EqualTo(5));
            Assert.That(_settings.SuperZoneInterval, Is.EqualTo(30));
            Assert.That(_settings.RewardTierInterval, Is.EqualTo(5));
            Assert.That(_settings.MaximumRewardQuantity, Is.EqualTo(1000000000));
            Assert.That(_settings.CashOutPolicy, Is.EqualTo(CashOutPolicy.AnyZone));
        }

        [Test]
        public void SerializedInvalidData_IsRejectedEvenWhenSettersAreBypassed()
        {
            SerializedObject serializedSettings = new SerializedObject(_settings);
            serializedSettings.FindProperty("_safeZoneInterval").intValue = 0;
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();

            Assert.Throws<ArgumentOutOfRangeException>(() => _settings.CreateRules());
        }

        [Test]
        public void AuthoredProgressionValues_ArePassedToCore()
        {
            _settings.RewardTierInterval = 2;
            _settings.MaximumRewardQuantity = 150;

            Assert.That(_settings.CreateRules().GetRewardQuantity(5, 100, 100), Is.EqualTo(150));
        }
    }
}
