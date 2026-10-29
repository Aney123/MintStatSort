using System;
using System.Collections.Generic;
using System.Linq;

namespace MintStatSort
{
    public sealed class Ranked<T>
    {
        public T Item;
        public double? Score;
        internal int Index;
        internal string Label;
        internal double?[] Values;
        internal double TieStrength;
        public double?[] Desirabilities;
        public double[] WeightShares;
    }
    public static class SortingCore
    {
        public static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
        // Invalid/missing values always go last, in either direction. Equal values
        // use display name then original position so the order never flickers.
        public static List<T> Numeric<T>(IEnumerable<T> source, Func<T, double?> measure,
            Func<T, string> label, bool descending)
        {
            return source.Select((item, index) => new
            {
                Item = item, Index = index, Value = measure(item), Label = label(item) ?? ""
            }).Select(row => new
            {
                row.Item, row.Index, row.Label,
                Valid = row.Value.HasValue && Finite(row.Value.Value),
                Value = row.Value.GetValueOrDefault()
            }).OrderBy(row => row.Valid ? 0 : 1)
                .ThenBy(row => row.Valid ? (descending ? -row.Value : row.Value) : 0)
                .ThenBy(row => row.Label, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(row => row.Index).Select(row => row.Item).ToList();
        }
        public static List<T> Alphabetical<T>(IEnumerable<T> source, Func<T, string> label, bool descending)
        {
            return descending
                ? source.OrderByDescending(label, StringComparer.CurrentCultureIgnoreCase).ToList()
                : source.OrderBy(label, StringComparer.CurrentCultureIgnoreCase).ToList();
        }
        // Give equally important goals a common scale. Missing any chosen value
        // means no combined score; constant columns cannot influence the order.
        public static List<Ranked<T>> Balanced<T>(IEnumerable<T> source, Func<T, double?[]> measure,
            Func<T, string> label, IReadOnlyList<bool> higherIsBetter, bool bestFirst, IReadOnlyList<int> weights = null)
        {
            var rows = source.Select((item, index) => new Ranked<T>
            { Item = item, Index = index, Label = label(item) ?? "", Values = measure(item) }).ToList();
            if (higherIsBetter.Count == 0)
                return rows;
            var complete = rows.Where(r => r.Values != null && r.Values.Length == higherIsBetter.Count &&
                r.Values.All(v => v.HasValue && Finite(v.Value))).ToList();
            var lows = new double[higherIsBetter.Count];
            var highs = new double[higherIsBetter.Count];
            var active = new List<int>();
            Func<int, int> weight = i => weights != null && i < weights.Count ? NumericFilterCore.Importance(weights[i]) : 1;
            if (complete.Count != 0)
            {
                for (int i = 0; i < higherIsBetter.Count; i++)
                {
                    lows[i] = complete.Min(r => r.Values[i].Value);
                    highs[i] = complete.Max(r => r.Values[i].Value);
                    if (lows[i] != highs[i]) active.Add(i);
                }
                foreach (var row in complete)
                {
                    row.Desirabilities = new double?[higherIsBetter.Count];
                    row.WeightShares = new double[higherIsBetter.Count];
                    double activeWeight = active.Sum(i => (double)weight(i));
                    double sum = 0, logSum = 0;
                    double totalWeight = 0;
                    bool weakest = false;
                    foreach (int i in active)
                    {
                        double range = highs[i] - lows[i];
                        double relative = Finite(range) ? (row.Values[i].Value - lows[i]) / range :
                            (row.Values[i].Value / 2 - lows[i] / 2) / (highs[i] / 2 - lows[i] / 2);
                        relative = Math.Max(0, Math.Min(1, relative));
                        double desirability = higherIsBetter[i] ? relative : 1 - relative;
                        int importance = weight(i);
                        row.Desirabilities[i] = desirability;
                        row.WeightShares[i] = importance / activeWeight;
                        totalWeight += importance;
                        sum += desirability * importance;
                        if (desirability == 0) weakest = true;
                        else logSum += Math.Log(desirability) * importance;
                    }
                    // Geometric mean rewards balance: one excellent property
                    // cannot hide a minimum value in another chosen property.
                    row.Score = active.Count == 0 ? 100 : weakest ? 0 : Math.Exp(logSum / totalWeight) * 100;
                    row.TieStrength = active.Count == 0 ? 1 : sum / totalWeight;
                }
            }
            return rows.OrderBy(r => r.Score.HasValue ? 0 : 1)
                .ThenBy(r => r.Score.HasValue ? (bestFirst ? -r.Score.Value : r.Score.Value) : 0)
                .ThenBy(r => r.Score.HasValue ? (bestFirst ? -r.TieStrength : r.TieStrength) : 0)
                .ThenBy(r => r.Label, StringComparer.CurrentCultureIgnoreCase).ThenBy(r => r.Index).ToList();
        }
    }
}
