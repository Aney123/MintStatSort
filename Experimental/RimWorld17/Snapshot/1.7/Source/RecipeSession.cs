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
        private static readonly Queue<Action> measurementQueue = new Queue<Action>();
        private static readonly Dictionary<string, HashSet<RecipeDef>> queuedMeasurements = new Dictionary<string, HashSet<RecipeDef>>();
        private static readonly BudgetedWork measurements = new BudgetedWork();
        private static int measurementRevision, lastMeasurementFrame = -1;
        private static Measurement pendingReading;
        private static string materialKey = "", ammoKey = "";
        private static RecipeAvailability stockAvailability;
        private static int lastStockFrame = -1;
        private static readonly Dictionary<IngredientCount, List<ThingDef>> stockChoices = new Dictionary<IngredientCount, List<ThingDef>>();
        private static QualityCategory qualityKey = QualityCategory.Normal;
        private static readonly Dictionary<RecipeDef, double?> combinedScores = new Dictionary<RecipeDef, double?>();
        private static List<Metric> selectedMetrics;
        public static SortSettings Options => MintStatSortMod.Instance.Options;
        public static List<Metric> SelectedMetrics => selectedMetrics ?? (selectedMetrics = (Options.Criteria ?? new List<SortCriterion>())
            .Select(c => Metrics.Resolve(c.Id)).Where(m => m != null).GroupBy(m => m.Id).Select(g => g.First()).ToList());
        public static Metric CurrentMetric => SelectedMetrics.FirstOrDefault() ?? Metrics.Resolve(Options.Metric == "name" ? "name" : "original");
        public static bool HasComparison => SelectedMetrics.Count != 0;
        public static IReadOnlyList<RecipeDef> Recipes => recipes;
        public static Map ActiveMap;
        private static string activeBenchKey;
        internal static void SetBench(string key)
        {
            key = key ?? "";
            if (activeBenchKey == key) return;
            activeBenchKey = key;
            var remembered = Options.Bench(key);
            if (remembered != null) { remembered.Values.ApplyTo(Options); Invalidate(); }
        }
        public static void SetMap(Map map)
        {
            if (ReferenceEquals(ActiveMap, map)) return;
            ActiveMap = map;
            Invalidate();
        }
        public static ThingDef Material => string.IsNullOrEmpty(Options.Material) ? null : DefDatabase<ThingDef>.GetNamedSilentFail(Options.Material);
        public static void SetRecipes(List<RecipeDef> incoming)
        {
            incoming = incoming ?? new List<RecipeDef>();
            if (!recipes.SequenceEqual(incoming)) { recipes = incoming.ToList(); Invalidate(); }
            if (Options.Category != "all" && !recipes.Any(r => Matches(r, Options.Category))) Options.Category = "all";
            EnsureContext();
        }
        private static void EnsureContext()
        {
            string current = Options.Material ?? "";
            if (materialKey == current && ammoKey == Options.Ammo && qualityKey == Options.Quality) return;
            materialKey = current; ammoKey = Options.Ammo; qualityKey = Options.Quality;
            Invalidate();
        }
        public static void Invalidate()
        {
            measurementRevision++; stockAvailability?.Dispose(); stockAvailability = null; stockChoices.Clear(); lastStockFrame = -1;
            measurements.Dispose(); measurementQueue.Clear(); queuedMeasurements.Clear();
            cache.Clear(); combinedScores.Clear(); selectedMetrics = null; pendingReading = null;
        }
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
        internal static void ImportMeasurements(Dictionary<string, Dictionary<RecipeDef, Measurement>> values, ThingDef material, QualityCategory quality = QualityCategory.Normal, string ammo = "")
        {
            if (Options.Ammo != ammo) return;
            if (Material != material || Options.Quality != quality || Options.Ammo != ammo) return;
            EnsureContext();
            foreach (var entry in values)
            {
                if (!cache.TryGetValue(entry.Key, out var readings))
                    cache[entry.Key] = readings = new Dictionary<RecipeDef, Measurement>();
                foreach (var reading in entry.Value) readings[reading.Key] = reading.Value;
            }
        }
        public static Measurement Get(RecipeDef recipe, Metric metric)
        {
            if (recipe == null || metric?.Id == null) return Measurement.Missing();
            EnsureContext();
            if (!cache.TryGetValue(metric.Id, out var values)) cache[metric.Id] = values = new Dictionary<RecipeDef, Measurement>();
            if (values.TryGetValue(recipe, out var value)) return value;
            if (!queuedMeasurements.TryGetValue(metric.Id, out var queued))
                queuedMeasurements[metric.Id] = queued = new HashSet<RecipeDef>();
            if (queued.Add(recipe))
            {
                var material = Material;
                var quality = Options.Quality; var ammo = Options.Ammo;
                int revision = measurementRevision;
                measurementQueue.Enqueue(() =>
                {
                    try
                    {
                        if (revision != measurementRevision || Material != material || Options.Quality != quality || Options.Ammo != ammo) return;
                        // The comparison dialog may have imported this pair while
                        // it was waiting. Reuse that reading instead of measuring it.
                        if (!cache.TryGetValue(metric.Id, out var readings))
                            cache[metric.Id] = readings = new Dictionary<RecipeDef, Measurement>();
                        if (readings.ContainsKey(recipe)) return;
                        Measurement reading;
                        try { reading = Metrics.MeasureAtQuality(recipe, metric, material, quality, ammo); }
                        catch (Exception) { reading = Measurement.Missing("MSS.StatUnavailable".Translate()); }
                        if (revision == measurementRevision && Material == material && Options.Quality == quality && Options.Ammo == ammo) readings[recipe] = reading;
                    }
                    finally
                    {
                        if (revision == measurementRevision && queuedMeasurements.TryGetValue(metric.Id, out var pending))
                            pending.Remove(recipe);
                    }
                });
                if (!measurements.Pending) measurements.Replace(QueuedWork());
            }
            return pendingReading ?? (pendingReading = new Measurement { Display = "…", Detail = "MSS.Preparing".Translate() });
        }
        private static IEnumerable<Action> QueuedWork()
        {
            while (measurementQueue.Count > 0) yield return measurementQueue.Dequeue();
        }
        private static void PumpMeasurements()
        {
            if (lastMeasurementFrame == Time.frameCount) return;
            lastMeasurementFrame = Time.frameCount;
            measurements.Run(3, 512);
        }
        public static List<RecipeDef> SortForDisplay(List<RecipeDef> incoming)
        {
            return SortForDisplay(incoming, r => r, r => r.LabelCap.ToString());
        }
        public static List<T> SortForDisplay<T>(List<T> incoming, Func<T, RecipeDef> recipe, Func<T, string> label)
        {
            if ((!VanillaIntegration.Ready && !MintIntegration.Ready && !NiceIntegration.Ready) || !Options.Enabled) return incoming;
            PumpMeasurements();
            var selected = incoming.Where(r => Matches(recipe(r), Options.Category) &&
                (!Options.FavoritesOnly || recipe(r) == null || Options.IsFavorite(recipe(r)))).ToList();
            if (Options.OnlyStockReady)
            {
                if (stockAvailability == null) stockAvailability = new RecipeAvailability(ActiveMap, stockChoices);
                stockAvailability.Configure(selected.Select(recipe), Material, Options.Executions, true, Options.MixSubstitutes);
                if (lastStockFrame != Time.frameCount) { lastStockFrame = Time.frameCount; stockAvailability.Update(); }
                if (stockAvailability.Ready) selected = selected.Where(r => recipe(r) == null || stockAvailability.Result(recipe(r)) == true).ToList();
            }
            var metrics = SelectedMetrics;
            if (metrics.Count == 0)
                return Options.OrderMode == "name" ? SortingCore.Alphabetical(selected, label, Options.Descending) : selected;
            var directions = metrics.Select(m => Options.Criteria.First(c => c.Id == m.Id).HigherIsBetter).ToList();
            var criteria = metrics.Select(m => Options.Criteria.First(c => c.Id == m.Id)).ToList();
            selected = selected.Where(r =>
            {
                var definition = recipe(r);
                if (definition == null) return true;
                for (int i = 0; i < metrics.Count; i++)
                {
                    if (!criteria[i].HasMinimum && !criteria[i].HasMaximum) continue;
                    var reading = Get(definition, metrics[i]);
                    // Pending rows remain available until a real value (or a
                    // completed missing result) is present in the shared cache.
                    if (!ReferenceEquals(reading, pendingReading) && !criteria[i].Matches(reading.Value)) return false;
                }
                return true;
            }).ToList();
            if (Options.OrderMode != "criteria")
            {
                if (metrics.Count > 1)
                {
                    var preview = SortingCore.Balanced(selected, r => metrics.Select(m => Get(recipe(r), m).Value).ToArray(), label, directions, true, criteria.Select(c => c.Weight).ToList());
                    combinedScores.Clear();
                    foreach (var item in preview) if (recipe(item.Item) != null) combinedScores[recipe(item.Item)] = item.Score;
                }
                return Options.OrderMode == "name" ? SortingCore.Alphabetical(selected, label, Options.Descending) : selected;
            }
            if (metrics.Count == 1)
                return SortingCore.Numeric(selected, r => Get(recipe(r), metrics[0]).Value, label, Options.Descending == directions[0]);
            var ranked = SortingCore.Balanced(selected, r => metrics.Select(m => Get(recipe(r), m).Value).ToArray(), label, directions, Options.Descending, criteria.Select(c => c.Weight).ToList());
            combinedScores.Clear();
            foreach (var item in ranked) combinedScores[recipe(item.Item)] = item.Score;
            return ranked.Select(r => r.Item).ToList();
        }
        public static Measurement Comparison(RecipeDef recipe)
        {
            var metrics = SelectedMetrics;
            if (metrics.Count == 0) return Measurement.Missing();
            if (metrics.Count == 1) return Get(recipe, metrics[0]);
            if (metrics.Any(metric => ReferenceEquals(Get(recipe, metric), pendingReading))) return pendingReading;
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
            // The comparison window discovers applicability incrementally. This
            // legacy fallback must not hide candidates simply because they await
            // a reading; it has no active UI callers.
            return Metrics.Candidates(advanced).Concat(SelectedMetrics).GroupBy(m => m.Id).Select(g => g.First())
                .OrderBy(m => Array.IndexOf(new[] { "attack", "protection", "climate", "practical", "other" }, m.GroupKey))
                .ThenBy(m => m.Name).ToList();
        }
        public static bool Matches(RecipeDef recipe, string category)
        {
            var selections = CategorySelection.Unpack(category);
            return selections.Count == 0 || selections.Any(id => MatchesOne(recipe, id));
        }
        private static bool MatchesOne(RecipeDef recipe, string category)
        {
            if (recipe == null) return false;
            if (string.IsNullOrEmpty(category) || category == "all") return true;
            var product = recipe.ProducedThingDef;
            if (category == "kind:weapons") return product != null && product.IsWeapon;
            if (category == "kind:armor") return IsArmor(product);
            if (category == "kind:clothing") return product != null && product.IsApparel && !IsArmor(product);
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
        private static bool IsArmor(ThingDef product)
            => product != null && product.IsApparel && product.thingCategories != null &&
                product.thingCategories.SelectMany(Parents).Any(c => c.defName == "ApparelArmor" || c.defName == "ArmorHeadgear");
        public static void SelectCategory(string category)
        {
            Options.Category = category;
            Invalidate();
            // Pending values are not missing data. Keep resolvable selections
            // across categories, just as the comparison dialog does.
            Options.Criteria = Options.Criteria.Where(c => Metrics.Resolve(c.Id) != null).ToList();
            Changed();
        }
        private static IEnumerable<ThingCategoryDef> Parents(ThingCategoryDef category)
        {
            var visited = new HashSet<ThingCategoryDef>();
            while (category != null && visited.Add(category)) { yield return category; category = category.parent; }
        }
        public static List<CategoryChoice> Categories()
        {
            // Index each product's ancestors once. Counts stay distinct by recipe,
            // and icons retain the first matching non-null product in menu order.
            var matching = new Dictionary<string, HashSet<RecipeDef>>(StringComparer.Ordinal);
            var icons = new Dictionary<string, ThingDef>(StringComparer.Ordinal);
            var iconPositions = new Dictionary<string, int>(StringComparer.Ordinal);
            var productIds = new Dictionary<ThingDef, List<string>>();
            var seenCategories = new HashSet<ThingCategoryDef>();
            var orderedCategories = new List<ThingCategoryDef>();
            void Add(string id, RecipeDef recipe, ThingDef product, int position)
            {
                if (!matching.TryGetValue(id, out var set)) matching[id] = set = new HashSet<RecipeDef>();
                set.Add(recipe);
                if (product != null && !icons.ContainsKey(id))
                { icons[id] = product; iconPositions[id] = position; }
            }
            for (int position = 0; position < recipes.Count; position++)
            {
                var recipe = recipes[position];
                var product = recipe.ProducedThingDef;
                Add("all", recipe, product, position);
                if (product == null) { Add("kind:other", recipe, null, position); continue; }
                if (!productIds.TryGetValue(product, out var ids))
                {
                    ids = new List<string>();
                    var parents = new List<ThingCategoryDef>();
                    var seenParents = new HashSet<ThingCategoryDef>();
                    if (product.thingCategories != null)
                        foreach (var category in product.thingCategories)
                            foreach (var parent in Parents(category))
                                if (seenParents.Add(parent))
                                {
                                    parents.Add(parent);
                                    ids.Add("def:" + parent.defName);
                                    if (parent.parent != null && seenCategories.Add(parent)) orderedCategories.Add(parent);
                                }
                    bool weapon = product.IsWeapon, apparel = product.IsApparel;
                    bool armor = apparel && parents.Any(parent => parent.defName == "ApparelArmor" || parent.defName == "ArmorHeadgear");
                    if (weapon) ids.Add("kind:weapons");
                    if (apparel) { ids.Add("kind:apparel"); ids.Add(armor ? "kind:armor" : "kind:clothing"); }
                    if (product.IsRangedWeapon) ids.Add("kind:ranged");
                    if (product.IsMeleeWeapon) ids.Add("kind:melee");
                    if (!apparel && !weapon) ids.Add("kind:other");
                    productIds[product] = ids;
                }
                foreach (var id in ids) Add(id, recipe, product, position);
            }
            bool Has(string id) => matching.TryGetValue(id, out var set) && set.Count > 0;
            var choices = new List<CategoryChoice> { new CategoryChoice { Id = "all", Name = "MSS.All".Translate(), Primary = true } };
            foreach (var entry in new[] { new[] { "kind:weapons", "MSS.Weapons" }, new[] { "kind:armor", "MSS.Armor" },
                new[] { "kind:clothing", "MSS.Clothing" } })
                if (Has(entry[0])) choices.Add(new CategoryChoice { Id = entry[0], Name = entry[1].Translate(), Primary = true });
            foreach (var entry in new[] { new[] { "kind:apparel", "MSS.Apparel" }, new[] { "kind:ranged", "MSS.Ranged" },
                new[] { "kind:melee", "MSS.Melee" }, new[] { "kind:other", "MSS.Other" } })
                if (Has(entry[0])) choices.Add(new CategoryChoice { Id = entry[0], Name = entry[1].Translate() });
            foreach (var group in orderedCategories.GroupBy(c => c.LabelCap.ToString(), StringComparer.CurrentCultureIgnoreCase)
                .Where(g => !choices.Any(c => string.Equals(c.Name, g.Key, StringComparison.CurrentCultureIgnoreCase)))
                .OrderBy(g => g.Key))
            {
                string id;
                if (group.Count() == 1) id = "def:" + group.First().defName;
                else
                {
                    id = "defs:" + string.Join("|", group.Select(c => c.defName).OrderBy(n => n, StringComparer.Ordinal));
                    var merged = new HashSet<RecipeDef>();
                    int firstPosition = int.MaxValue;
                    foreach (var category in group)
                    {
                        string categoryId = "def:" + category.defName;
                        merged.UnionWith(matching[categoryId]);
                        if (iconPositions.TryGetValue(categoryId, out var position) && position < firstPosition)
                        { firstPosition = position; icons[id] = icons[categoryId]; }
                    }
                    matching[id] = merged;
                }
                choices.Add(new CategoryChoice { Id = id, Name = group.Key });
            }
            foreach (var choice in choices)
            {
                choice.Count = matching.TryGetValue(choice.Id, out var set) ? set.Count : 0;
                if (icons.TryGetValue(choice.Id, out var icon)) choice.Icon = icon;
            }
            return choices;
        }
        public static string CategoryName()
        {
            var selections = CategorySelection.Unpack(Options.Category);
            if (selections.Count > 1)
            {
                var choices = Categories();
                return string.Join(" + ", selections.Select(selected => choices.FirstOrDefault(c => c.Id == selected)?.Name ?? selected));
            }
            string id = selections.FirstOrDefault() ?? "all";
            if (id.StartsWith("defs:", StringComparison.Ordinal))
                return id.Substring(5).Split('|').Select(n => DefDatabase<ThingCategoryDef>.GetNamedSilentFail(n))
                    .FirstOrDefault(c => c != null)?.LabelCap.ToString() ?? "MSS.All".Translate();
            if (id.StartsWith("label:", StringComparison.Ordinal)) return id.Substring(6);
            if (id.StartsWith("def:", StringComparison.Ordinal))
                return DefDatabase<ThingCategoryDef>.GetNamedSilentFail(id.Substring(4))?.LabelCap.ToString() ?? "MSS.All".Translate();
            string key;
            switch (id)
            {
                case "kind:weapons": key = "MSS.Weapons"; break;
                case "kind:armor": key = "MSS.Armor"; break;
                case "kind:clothing": key = "MSS.Clothing"; break;
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
