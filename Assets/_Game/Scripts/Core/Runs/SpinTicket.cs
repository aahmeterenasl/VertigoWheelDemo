using VertigoWheel.Core.Wheels;

namespace VertigoWheel.Core.Runs
{
    /// <summary>
    /// One accepted spin. Only its owning session can settle this exact instance;
    /// its detached wheel and selected outcome remain fixed during animation.
    /// </summary>
    public sealed class SpinTicket
    {
        private readonly long _runGeneration;
        private readonly long _spinSequence;
        private readonly WheelData _wheel;
        private readonly int _selectedSlotIndex;

        internal SpinTicket(long runGeneration, long spinSequence, WheelData wheel, int selectedSlotIndex)
        {
            _runGeneration = runGeneration;
            _spinSequence = spinSequence;
            _wheel = wheel;
            _selectedSlotIndex = selectedSlotIndex;
        }

        public long RunGeneration => _runGeneration;
        public long SpinSequence => _spinSequence;
        public int ZoneNumber => _wheel.ZoneNumber;
        public WheelData Wheel => _wheel;
        public int SelectedSlotIndex => _selectedSlotIndex;
        public WheelSlotData Outcome => _wheel.Slots[_selectedSlotIndex];
    }
}
