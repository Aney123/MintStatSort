using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace MintStatSort
{
    internal sealed class CategoryChoice
    {
        public string Id;
        public string Name;
        public ThingDef Icon;
        public int Count;
        public bool Primary;
    }
    internal static class RecipeSession
    {
        private static List<RecipeDef> recipes = new List<RecipeDef>();
        private static readonly Dictionary<string, Dictionary<RecipeDef, Measurement>> cache = new Dictionary<string, Dictionary<RecipeDef, Measurement>>();
        private static string materialKey = "";
        private static readonly Dictionary<RecipeDef, double?> combinedScores = new Dictionary<RecipeDef, double?>();
        private static List<Metric> selectedMetrics;
        public static SortSettings Options => MintStatSortMod.Instance.Options;
        public static List<Metric> SelectedMetrics => selectedMetrics ?? (selectedMetrics = (Options.Criteria ?? new List<SortCriterion>())
            .Select(c => Metrics.Resolve(c.Id)).Where(m => m != null).GroupBy(m => m.Id).Select(g => g.First()).ToList());
        public static Metric CurrentMetric => SelectedMetrics.FirstOrDefault() ?? Metrics.Resolve(Options.Metric == "name" ? "name" : "original");
        public static bool HasComparison => SelectedMetrics.Count != 0;
        public static Map ActiveMap;
        public static void SetMap(Map map) { ActiveMap = map; }
        public static ThingDef Material => string.IsNullOrEmpty(Options.Material) ? null : DefDatabase<ThingDef>.GetNamedSilentFail(Options.Material);
        public static void SetRecipes(List<RecipeDef> incoming)
        {
            incoming = incoming ?? new List<RecipeDef>();
            if (!recipes.SequenceEqual(incoming)) { recipes = incoming.ToList(); Invalidate(); }
            string current = Options.Material ?? "";
            if (materialKey != current) { materialKey = current; Invalidate(); }
        }
        public static void Invalidate() { cache.Clear(); combinedScores.Clear(); selectedMetrics = null; }
        public static void Changed(bool resetScroll = true)
        {
            Invalidate();
            if (resetScroll)
            {
                MintIntegration.ScrollField?.SetValue(null, Vector2.zero);
                NiceIntegration.ResetScroll();
            }
            MintStatSortMod.Instance.WriteSettings();
        }
        public static Measurement Get(RecipeDef recipe, Metric metric)
        {
            if (!cache.TryGetValue(metric.Id, out var values)) cache[metric.Id] = values = new Dictionary<RecipeDef, Measurement>();
            if (!values.TryGetValue(recipe, out var value))
            {
                try { value = Metrics.Measure(recipe, metric, Material); }
                catch (Exception) { value = Measurement.Missing("MSS.StatUnavailable".Translate()); }
                values[recipe] = value;
            }
            return value;
        }
        public static List<RecipeDef> SortForDisplay(List<RecipeDef> incoming)
        {
            return SortForDisplay(incoming, r => r, r => r.LabelCap.ToString());
        }
        public static List<T> SortForDisplay<T>(List<T> incoming, Func<T, RecipeDef> recipe, Func<T, string> label)
        {
            if ((!MintIntegration.Ready && !NiceIntegration.Ready) || !Options.Enabled) return incoming;
            var selected = incoming.Where(r => Matches(recipe(r), Options.Category)).ToList();
            var metrics = SelectedMetrics;
            if (metrics.Count == 0)
                return Options.Metric == "name" ? SortingCore.Alphabetical(selected, label, Options.Descending) : selected;
            var directions = metrics.Select(m => Options.Criteria.First(c => c.Id == m.Id).HigherIsBetter).ToList();
            if (metrics.Count == 1)
                return SortingCore.Numeric(selected, r => Get(recipe(r), metrics[0]).Value, label, Options.Descending == directions[0]);
            var ranked = SortingCore.Balanced(selected, r => metrics.Select(m => Get(recipe(r), m).Value).ToArray(), label, directions, Options.Descending);
            combinedScores.Clear();
            foreach (var item in ranked) combinedScores[recipe(item.Item)] = item.Score;
            return ranked.Select(r => r.Item).ToList();
        }
        public static Measurement Comparison(RecipeDef recipe)
        {
            var metrics = SelectedMetrics;
            if (metrics.Count == 0) return Measurement.Missing();
            if (metrics.Count == 1) return Get(recipe, metrics[0]);
            double? score;
            if (!combinedScores.TryGetValue(recipe, out score)) score = null;
            return new Measurement
            {
                Value = score,
                Display = score.HasValue ? "MSS.ScoreValue".Translate(score.Value.ToString("0.#")) : "MSS.NoData".Translate(),
                Detail = "MSS.BalanceTip".Translate().ToString() + "\n\n" + string.Join("\n\n", metrics.Select(m =>
                    (Options.Criteria.First(c => c.Id == m.Id).HigherIsBetter ? "MSS.More" : "MSS.Less").Translate().ToString() + " · " + Get(recipe, m).Detail))
            };
        }
        public static string SortSummary()
        {
            var metrics = SelectedMetrics;
            return metrics.Count == 0 ? CurrentMetric.Name : metrics.Count == 1 ? metrics[0].Name :
                "MSS.CriteriaCount".Translate(metrics.Count).ToString();
        }
        public static List<Metric> AvailableMetrics(bool advanced = false)
        {
            var relevant = recipes.Where(r => Matches(r, Options.Category)).ToList();
            return Metrics.Candidates(advanced).Concat(SelectedMetrics).GroupBy(m => m.Id).Select(g => g.First())
                .Where(m => SelectedMetrics.Any(s => s.Id == m.Id) || relevant.Any(r =>
                    Get(r, m).Value.HasValue && (!m.Id.StartsWith("weapon.melee.", StringComparison.Ordinal) || Get(r, m).Value > 0)))
                .OrderBy(m => Array.IndexOf(new[] { "attack", "protection", "climate", "practical", "other" }, m.GroupKey))
                .ThenBy(m => m.Name).ToList();
        }
        public static bool Matches(RecipeDef recipe, string category)
        {
            if (string.IsNullOrEmpty(category) || category == "all") return true;
            var product = recipe.ProducedThingDef;
            if (category == "kind:apparel") return product != null && product.IsApparel;
            if (category == "kind:ranged") return product != null && product.IsRangedWeapon;
            if (category == "kind:melee") return product != null && product.IsMeleeWeapon;
            if (category == "kind:other") return product == null || (!product.IsApparel && !product.IsWeapon);
            if (product?.thingCategories == null) return false;
            var merged = category.StartsWith("defs:", StringComparison.Ordinal) ? category.Substring(5).Split('|') : null;
            return product.thingCategories.Any(c => Parents(c).Any(p => "def:" + p.defName == category ||
                (merged != null && merged.Contains(p.defName)) ||
                (category.StartsWith("label:", StringComparison.Ordinal) &&
                 string.Equals(p.LabelCap.ToString(), category.Substring(6), StringComparison.CurrentCultureIgnoreCase))));
        }
        public static void SelectCategory(string category)
        {
            Options.Category = category;
            Invalidate();
            var relevant = recipes.Where(r => Matches(r, category)).ToList();
            // Keep useful comparisons when switching between related categories,
            // dropping goals that have no data for anything in the new category.
            Options.Criteria = Options.Criteria.Where(c =>
            {
                var metric = Metrics.Resolve(c.Id);
                return metric != null && relevant.Any(r => Get(r, metric).Value.HasValue);
            }).ToList();
            Changed();
        }
        private static IEnumerable<ThingCategoryDef> Parents(ThingCategoryDef category)
        {
            var visited = new HashSet<ThingCategoryDef>();
            while (category != null && visited.Add(category)) { yield return category; category = category.parent; }
        }
        public static List<CategoryChoice> Categories()
        {
            var choices = new List<CategoryChoice> { new CategoryChoice { Id = "all", Name = "MSS.All".Translate(), Primary = true } };
            foreach (var entry in new[] { new[] { "kind:apparel", "MSS.Apparel" }, new[] { "kind:ranged", "MSS.Ranged" },
                new[] { "kind:melee", "MSS.Melee" }, new[] { "kind:other", "MSS.Other" } })
                if (recipes.Any(r => Matches(r, entry[0]))) choices.Add(new CategoryChoice { Id = entry[0], Name = entry[1].Translate(), Primary = true });
            choices.AddRange(recipes.Select(r => r.ProducedThingDef).Where(p => p?.thingCategories != null)
                .SelectMany(p => p.thingCategories).SelectMany(Parents).Where(c => c.parent != null)
                .Distinct().GroupBy(c => c.LabelCap.ToString(), StringComparer.CurrentCultureIgnoreCase)
                .Where(g => !choices.Any(c => string.Equals(c.Name, g.Key, StringComparison.CurrentCultureIgnoreCase)))
                .OrderBy(g => g.Key).Select(g => new CategoryChoice
                { Id = g.Count() == 1 ? "def:" + g.First().defName :
                    "defs:" + string.Join("|", g.Select(c => c.defName).OrderBy(n => n, StringComparer.Ordinal)), Name = g.Key }));
            foreach (var choice in choices)
            {
                var matching = recipes.Where(r => Matches(r, choice.Id)).Distinct().ToList();
                choice.Count = matching.Count;
                choice.Icon = matching.Select(r => r.ProducedThingDef).FirstOrDefault(p => p != null);
            }
            return choices;
        }
        public static string CategoryName()
        {
            string id = Options.Category ?? "all";
            if (id.StartsWith("defs:", StringComparison.Ordinal))
                return id.Substring(5).Split('|').Select(n => DefDatabase<ThingCategoryDef>.GetNamedSilentFail(n))
                    .FirstOrDefault(c => c != null)?.LabelCap.ToString() ?? "MSS.All".Translate();
            if (id.StartsWith("label:", StringComparison.Ordinal)) return id.Substring(6);
            if (id.StartsWith("def:", StringComparison.Ordinal))
                return DefDatabase<ThingCategoryDef>.GetNamedSilentFail(id.Substring(4))?.LabelCap.ToString() ?? "MSS.All".Translate();
            string key;
            switch (id)
            {
                case "kind:apparel": key = "MSS.Apparel"; break;
                case "kind:ranged": key = "MSS.Ranged"; break;
                case "kind:melee": key = "MSS.Melee"; break;
                case "kind:other": key = "MSS.Other"; break;
                default: key = "MSS.All"; break;
            }
            return key.Translate();
        }
        public static IEnumerable<ThingDef> Materials()
        {
            return recipes.Where(r => Matches(r, Options.Category) && r.ProducedThingDef != null && r.ProducedThingDef.MadeFromStuff)
                .SelectMany(r => GenStuff.AllowedStuffsFor(r.ProducedThingDef).Where(s => Metrics.RecipeAllowsStuff(r, s)))
                .Distinct().OrderBy(s => s.LabelCap.ToString());
        }
    }
}
