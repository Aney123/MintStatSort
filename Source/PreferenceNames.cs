using System;
using System.Linq;

namespace MintStatSort
{
    internal static class PreferenceNames
    {
        internal const int MaximumLength = 64;
        internal static string Normalize(string name)
        {
            if (name == null) return "";
            string cleaned = new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();
            return cleaned.Length > MaximumLength ? cleaned.Substring(0, MaximumLength).TrimEnd() : cleaned;
        }
        internal static bool Same(string a, string b)
            => string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);
    }
}
