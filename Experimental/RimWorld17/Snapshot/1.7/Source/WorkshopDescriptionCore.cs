using System;
using System.Text;

namespace MintStatSort
{
    internal static class WorkshopDescriptionCore
    {
        internal const int MaximumBytes = 7999;
        internal static string Select(string original, string packageId, string version, string manifest, string description)
        {
            if (!string.Equals(packageId, "mintstatsort.recipes", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(manifest) ||
                manifest.Trim().TrimStart('\uFEFF') != "MintStatSort " + version.Trim() ||
                string.IsNullOrWhiteSpace(description) || description.IndexOf('\0') >= 0) return original;
            description = description.Trim().TrimStart('\uFEFF');
            return Encoding.UTF8.GetByteCount(description) <= MaximumBytes ? description : original;
        }
    }
}
