using System;
using System.Linq;
using System.Collections.Generic;

namespace MintStatSort
{
    // Shared geometry for the fixed header and horizontally scrollable data rows.
    internal sealed class ComparisonLayout
    {
        internal const float StatWidth = 134f;
        internal const float TotalWidth = 86f;
        internal readonly float NameWidth;
        internal readonly float ContentWidth;
        private readonly float[] widths;
        internal ComparisonLayout(float viewportWidth, int columns)
            : this(viewportWidth, columns < 0 ? throw new ArgumentOutOfRangeException(nameof(columns)) : Enumerable.Repeat(StatWidth, columns)) { }
        internal ComparisonLayout(float viewportWidth, IEnumerable<float> columnWidths)
        {
            widths = columnWidths.Select(w => float.IsNaN(w) || float.IsInfinity(w) ? StatWidth : Math.Max(80f, Math.Min(260f, w))).ToArray();
            NameWidth = Math.Max(220f, viewportWidth - 18f - widths.Sum() - TotalWidth);
            ContentWidth = NameWidth + widths.Sum() + TotalWidth;
        }
        internal float Width(int column) => widths[column];
        internal float X(int column) => NameWidth + widths.Take(column).Sum();
    }
}
