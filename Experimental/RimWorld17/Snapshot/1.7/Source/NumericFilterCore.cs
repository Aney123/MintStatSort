using System;
using System.Globalization;

namespace MintStatSort
{
    public static class NumericFilterCore
    {
        public static bool Matches(double? value, double? minimum, double? maximum)
        {
            if (!minimum.HasValue && !maximum.HasValue) return true;
            return value.HasValue && SortingCore.Finite(value.Value) &&
                (!minimum.HasValue || value.Value >= minimum.Value - Math.Abs(minimum.Value) * 1e-7) &&
                (!maximum.HasValue || value.Value <= maximum.Value + Math.Abs(maximum.Value) * 1e-7);
        }
        // No grouping separators: "1,5" is a decimal, never silently 15.
        public static bool TryLimit(string text, double scale, out double? value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(text)) return true;
            double number;
            if (!double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out number) ||
                !SortingCore.Finite(number) || !SortingCore.Finite(scale) || scale <= 0) return false;
            number /= scale;
            if (!SortingCore.Finite(number)) return false;
            value = number;
            return true;
        }
        public static int Importance(int value) => Math.Max(1, Math.Min(5, value));
    }
}
