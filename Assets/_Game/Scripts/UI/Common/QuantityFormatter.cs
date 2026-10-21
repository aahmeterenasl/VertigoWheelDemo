using System;
using System.Globalization;

namespace VertigoWheel.UI.Common
{
    /// <summary>Display-only abbreviations; reward arithmetic always retains the original long.</summary>
    public static class QuantityFormatter
    {
        public static string Format(long quantity, bool compact = false)
        {
            if (quantity < 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            if (quantity < (compact ? 1000L : 1000000L))
                return quantity.ToString(compact ? "0" : "N0", CultureInfo.InvariantCulture);

            long divisor;
            string suffix;
            if (quantity < 1000000L) { divisor = 1000L; suffix = "K"; }
            else if (quantity < 1000000000L) { divisor = 1000000L; suffix = "M"; }
            else if (quantity < 1000000000000L) { divisor = 1000000000L; suffix = "B"; }
            else if (quantity < 1000000000000000L) { divisor = 1000000000000L; suffix = "T"; }
            else if (quantity < 1000000000000000000L) { divisor = 1000000000000000L; suffix = "Qa"; }
            else { divisor = 1000000000000000000L; suffix = "Qi"; }

            decimal scaled = (decimal)quantity / divisor;
            // Bound the label length and never advertise an amount above the actual reward.
            decimal displayed = scaled >= 100m ? decimal.Floor(scaled) : decimal.Floor(scaled * 10m) / 10m;
            return displayed.ToString("0.#", CultureInfo.InvariantCulture) + suffix;
        }
    }
}
