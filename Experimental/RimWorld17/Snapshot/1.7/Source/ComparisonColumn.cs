using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace MintStatSort
{
    public sealed class ComparisonColumn : IExposable
    {
        public string Id;
        public int Width = 134;
        public bool Visible = true;
        public void ExposeData()
        {
            Scribe_Values.Look(ref Id, "id"); Scribe_Values.Look(ref Width, "width", 134);
            Scribe_Values.Look(ref Visible, "visible", true);
        }
        public ComparisonColumn Copy() => new ComparisonColumn { Id = Id, Width = Width, Visible = Visible };
        internal static List<ComparisonColumn> Normalize(IEnumerable<ComparisonColumn> values)
            => (values ?? Enumerable.Empty<ComparisonColumn>()).Where(c => c != null && !string.IsNullOrEmpty(c.Id))
                .GroupBy(c => c.Id, StringComparer.Ordinal).Select(g => new ComparisonColumn { Id = g.Key,
                    Width = Math.Max(80, Math.Min(260, g.Last().Width)), Visible = g.Last().Visible }).ToList();
    }
}
