namespace VertigoWheel.Core.Randomness
{
    public interface IRandomSource
    {
        /// <summary>Returns a value greater than or equal to zero and less than one.</summary>
        double NextDouble();
    }
}
