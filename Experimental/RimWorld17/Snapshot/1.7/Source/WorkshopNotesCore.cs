using System;

namespace MintStatSort
{
    internal static class WorkshopNotesCore
    {
        internal const int MaximumLength = 16384;
        internal static string Select(string original, string packageId, string version, string notes)
        {
            if (!string.Equals(packageId, "mintstatsort.recipes", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(notes) ||
                notes.Length > MaximumLength || notes.IndexOf('\0') >= 0 ||
                original == null || !original.StartsWith("[Auto-generated text]:", StringComparison.Ordinal)) return original;
            notes = notes.Trim().TrimStart('\uFEFF');
            int lineEnd = notes.IndexOf('\n');
            if (lineEnd < 0 || notes.Substring(0, lineEnd).TrimEnd('\r') != "MintStatSort " + version.Trim() ||
                string.IsNullOrWhiteSpace(notes.Substring(lineEnd + 1))) return original;
            return notes;
        }
    }
}
