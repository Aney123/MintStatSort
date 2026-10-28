using System;
using System.Globalization;

namespace MintStatSort
{
    public static class BatchCount
    {
        public const int Maximum = 999;
        public static int Clamp(int value) => Math.Max(1, Math.Min(Maximum, value));
        public static bool TryParse(string value, out int count)
            => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out count) && count >= 1 && count <= Maximum;
        public static int? Required(int? single, int executions)
        {
            if (!single.HasValue || single < 0) return null;
            long result = (long)single.Value * Clamp(executions);
            return result <= int.MaxValue ? (int?)result : null;
        }
    }
}
