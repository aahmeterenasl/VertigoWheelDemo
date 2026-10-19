using VertigoWheel.Core.Randomness;
using VertigoWheel.Core.Wheels;
using VertigoWheel.Core.Zones;

namespace VertigoWheel.Core.Runs
{
    /// <summary>Creates detached rule and wheel data without exposing Unity assets to Core.</summary>
    public interface IRunSettingsSource
    {
        ZoneRules CreateZoneRules();
        WheelData CreateWheelData(int zoneNumber, IRandomSource random);
    }
}
