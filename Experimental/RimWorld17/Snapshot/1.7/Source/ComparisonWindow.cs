using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using RimWorld;
using UnityEngine;
using Verse;

namespace MintStatSort
{
    internal sealed class ComparisonRow
    {
        internal RecipeDef Recipe;
        internal string Label;
        internal FloatMenuOption Option;
    }
    internal sealed class ActiveFilter
    {
        internal string Key, Label;
    }

    // The dialog owns its draft and measurements. Drawing the bills tab underneath
    // cannot change the preview or write partially edited settings to disk.
    internal class ComparisonWindow : Window
    {
        private static readonly Color Gold = new Color(0.78f, 0.63f, 0.32f);
        private static readonly Color Muted = new Color(0.66f, 0.73f, 0.77f);
        private static readonly Color ValueColor = new Color(0.62f, 0.82f, 0.94f);
        private readonly List<ComparisonRow> rows;
        private readonly List<RecipeDef> recipes;
        private readonly Map map;
        private readonly Func<bool> valid;
        private readonly string contextName;
        private readonly bool native;
        private readonly SortSettings draft;
        private string benchKey = "", profileId;
        private readonly List<CategoryChoice> categories;
        private readonly Dictionary<string, Dictionary<RecipeDef, Measurement>> readings = new Dictionary<string, Dictionary<RecipeDef, Measurement>>();
        private readonly Dictionary<ComparisonRow, string> scoreDetails = new Dictionary<ComparisonRow, string>();
        private bool compactCriteria, focusSearch;
        private Vector2 compactScroll;
        private readonly Dictionary<ComparisonRow, double?> scores = new Dictionary<ComparisonRow, double?>();
        private List<Metric> choices = new List<Metric>();
        private readonly BudgetedWork discovery = new BudgetedWork();
        private readonly BudgetedWork preparation = new BudgetedWork();
        private readonly BudgetedWork inventory = new BudgetedWork();
        private readonly Dictionary<RecipeDef, ThingDef[]> recipeMaterials = new Dictionary<RecipeDef, ThingDef[]>();
        private readonly Dictionary<IngredientCount, List<ThingDef>> ingredientChoices = new Dictionary<IngredientCount, List<ThingDef>>();
        private Vector2 filterScroll;
        private readonly Dictionary<string, List<Metric>> availableChoices = new Dictionary<string, List<Metric>>();
        private Measurement pendingReading;
        private bool materialsReady;
        private int prepared, preparationTotal;
        private float prepareAfter;
        private readonly List<string> groupOrder = new List<string> { "protection", "attack", "climate", "practical", "other" };
        private List<ThingDef> materials = new List<ThingDef>();
        private Dictionary<ThingDef, long> counts;
        private float nextCountRefresh;
        private Vector2 recipeScroll, goalsScroll, materialScroll, sideScroll;
        private string search = "", goalSearch = "", materialSearch = "";
        private bool advanced, showInactive, closed;
        private IngredientInspector ingredientPreview;
        private ThingDef previewMaterial;
        private string comparisonMode;
        private ComparisonRow selected;
        private List<ComparisonRow> display;
        private List<Metric> goals;
        private List<Metric> tableMetrics = new List<Metric>();
        private readonly List<RecipeDef> pinned = new List<RecipeDef>();
        private readonly BudgetedWork pinnedWork = new BudgetedWork();
        private readonly RecipeAvailability availability;
        private string executionsText = "1";
        private int availabilityRevision;
        private bool dirty = true;
        public override Vector2 InitialSize => new Vector2(Mathf.Min(1480f, UI.screenWidth - 24f), Mathf.Min(910f, UI.screenHeight - 40f));

        internal ComparisonWindow(List<ComparisonRow> rows, Map map, Func<bool> valid, string contextName, bool native = false)
        {
            this.rows = rows;
            this.map = map;
            this.valid = valid;
            this.contextName = contextName;
            this.native = native;
            availability = new RecipeAvailability(map, ingredientChoices);
            recipes = rows.Where(r => r.Recipe != null).Select(r => r.Recipe).Distinct().ToList();
            draft = RecipeSession.Options.Copy(); draft.Migrate(); executionsText = draft.Executions.ToString();
            comparisonMode = draft.OrderMode;
            RecipeSession.SetRecipes(recipes);
            categories = RecipeSession.Categories();
            doCloseX = true;
            closeOnAccept = false;
            absorbInputAroundWindow = true;
            forcePause = true;
            RefreshChoices();
            RefreshDisplay();
        }
        protected virtual bool ContextValid() => valid == null || valid();
        protected virtual string RequirementText(ComparisonRow row) => RecipeRequirements.Describe(row?.Recipe, row != null && RecipeAvailable(row.Recipe) && (row.Option == null || !row.Option.Disabled), row?.Label);
        protected virtual bool RecipeAvailable(RecipeDef recipe) => recipe == null || recipe.AvailableNow;
        internal void SetBenchContext(string key)
        {
            benchKey = key ?? "";
            var remembered = RecipeSession.Options.Bench(benchKey);
            if (remembered != null) UsePreferences(remembered.Values, remembered.ProfileId);
            else NormalizeCategory();
        }
        private void NormalizeCategory()
        {
            string available = CategorySelection.Available(draft.Category, categories.Where(c => c.Count > 0).Select(c => c.Id));
            if (available != draft.Category) { draft.Category = available; dirty = true; RefreshChoices(); RefreshDisplay(); }
        }
        private void UsePreferences(ComparisonPreferences preferences, string id)
        {
            string previousMaterial = draft.Material; var previousQuality = draft.Quality; string previousAmmo = draft.Ammo;
            preferences.ApplyTo(draft); profileId = id; executionsText = draft.Executions.ToString();
            draft.Migrate();
            if (!string.IsNullOrEmpty(draft.Material) && DefDatabase<ThingDef>.GetNamedSilentFail(draft.Material) == null) draft.Material = "";
            draft.Category = CategorySelection.Available(draft.Category, categories.Where(c => c.Count > 0).Select(c => c.Id));
            comparisonMode = draft.OrderMode;
            if (previousMaterial != draft.Material || previousQuality != draft.Quality || previousAmmo != draft.Ammo) readings.Clear();
            recipeScroll = goalsScroll = materialScroll = Vector2.zero; prepareAfter = 0;
            RefreshChoices(); RefreshDisplay();
        }
        private void LoadProfile(string id)
        {
            var profile = RecipeSession.Options.Profiles.FirstOrDefault(p => p.Id == id);
            if (profile != null) UsePreferences(profile.Values, id);
        }
        private void SaveProfile(string name)
        {
            var profile = RecipeSession.Options.SaveProfile(name, draft);
            if (profile == null) return;
            profileId = profile.Id;
            MintStatSortMod.Instance.WriteSettings();
            Messages.Message("MSS.ProfileSaved".Translate(profile.Name), MessageTypeDefOf.TaskCompletion, false);
        }
        private void ToggleFavorite(RecipeDef recipe)
        {
            RecipeSession.Options.ToggleFavorite(recipe);
            MintStatSortMod.Instance.WriteSettings();
            dirty = true; prepareAfter = 0; RefreshDisplay();
        }
        private void SetFavoritesOnly(bool value)
        {
            draft.FavoritesOnly = value; recipeScroll = Vector2.zero;
            dirty = true; prepareAfter = 0; RefreshDisplay();
        }
        private ThingDef Material => string.IsNullOrEmpty(draft.Material) ? null : DefDatabase<ThingDef>.GetNamedSilentFail(draft.Material);
        private Measurement Measure(RecipeDef recipe, Metric metric)
        {
            if (!readings.TryGetValue(metric.Id, out var values)) readings[metric.Id] = values = new Dictionary<RecipeDef, Measurement>();
            if (!values.TryGetValue(recipe, out var reading))
            {
                try { reading = Metrics.MeasureAtQuality(recipe, metric, Material, draft.Quality, draft.Ammo); }
                catch (Exception ex) { reading = Measurement.Missing("MSS.StatUnavailable".Translate() + " (" + ex.GetType().Name + ")"); }
                values[recipe] = reading;
            }
            return reading;
        }
        private Measurement Probe(RecipeDef recipe, Metric metric)
        {
            if (TryReading(recipe, metric, out var cached)) return cached;
            Measurement reading;
            try { reading = Metrics.MeasureAtQuality(recipe, metric, Material, draft.Quality, draft.Ammo); }
            catch (Exception ex) { reading = Measurement.Missing("MSS.StatUnavailable".Translate() + " (" + ex.GetType().Name + ")"); }
            // Discovery only needs one example. Retaining every absent pawn stat
            // for every recipe can consume hundreds of MB on large mod lists.
            if (reading.Value.HasValue)
            {
                if (!readings.TryGetValue(metric.Id, out var values)) readings[metric.Id] = values = new Dictionary<RecipeDef, Measurement>();
                values[recipe] = reading;
            }
            return reading;
        }
        private bool TryReading(RecipeDef recipe, Metric metric, out Measurement reading)
        {
            reading = null;
            return recipe != null && readings.TryGetValue(metric.Id, out var values) && values.TryGetValue(recipe, out reading);
        }
        private Measurement Reading(RecipeDef recipe, Metric metric)
        {
            if (TryReading(recipe, metric, out var reading)) return reading;
            return pendingReading ?? (pendingReading = new Measurement { Display = "…", Detail = "MSS.Preparing".Translate() });
        }
        private void RefreshChoices()
        {
            var relevant = recipes.Where(r => RecipeSession.Matches(r, draft.Category)).ToList();
            var candidates = Metrics.Candidates(advanced).Concat(draft.Criteria.Select(c => Metrics.Resolve(c.Id)).Where(m => m != null))
                .GroupBy(m => m.Id).Select(g => g.First()).ToList();
            choices = candidates.Where(m => draft.Criteria.Any(c => c.Id == m.Id)).ToList();
            string key = draft.Category + "\n" + draft.Material + "\n" + draft.Quality + "\n" + draft.Ammo + "\n" + advanced;
            bool scan = !availableChoices.TryGetValue(key, out var cachedChoices);
            if (!scan) choices = choices.Concat(cachedChoices).GroupBy(m => m.Id).Select(g => g.First())
                .OrderBy(m => groupOrder.IndexOf(m.GroupKey)).ThenBy(m => m.Name).ToList();
            materials = new List<ThingDef>();
            materialsReady = false;
            counts = new Dictionary<ThingDef, long>();
            inventory.Dispose();
            discovery.Replace(Discover(relevant, candidates, key, scan));
            dirty = true;
        }
        private IEnumerable<Action> Discover(List<RecipeDef> relevant, List<Metric> candidates, string key, bool scan)
        {
            var allowed = new HashSet<ThingDef>();
            foreach (var recipe in relevant.Where(r => r.ProducedThingDef != null && r.ProducedThingDef.MadeFromStuff))
                yield return () =>
                {
                    if (!recipeMaterials.TryGetValue(recipe, out var stuff))
                        recipeMaterials[recipe] = stuff = GenStuff.AllowedStuffsFor(recipe.ProducedThingDef).Where(s => Metrics.RecipeAllowsStuff(recipe, s)).ToArray();
                    allowed.UnionWith(stuff);
                };
            yield return () =>
            {
                materials = allowed.OrderBy(s => s.LabelCap.ToString()).ToList();
                materialsReady = true;
                nextCountRefresh = 0;
            };
            if (!scan) yield break;
            var available = new List<Metric>();
            foreach (var metric in candidates)
            {
                bool found = false;
                foreach (var recipe in relevant)
                {
                    yield return () =>
                    {
                        if (Probe(recipe, metric).Value.HasValue)
                        {
                            available.Add(metric);
                            if (!choices.Any(m => m.Id == metric.Id)) choices.Add(metric);
                            choices = choices.OrderBy(m => groupOrder.IndexOf(m.GroupKey)).ThenBy(m => m.Name).ToList();
                            found = true;
                        }
                    };
                    if (found) break;
                }
            }
            yield return () => availableChoices[key] = available;
        }
        private void RefreshDisplay()
        {
            if (!dirty) return;
            preparation.Dispose();
            goals = draft.Criteria.Select(c => Metrics.Resolve(c.Id)).Where(m => m != null).GroupBy(m => m.Id).Select(g => g.First()).ToList();
            var visible = rows.Where(r => PickerTiles.Matches(r.Label, search)).ToList();
            var known = visible.Where(r => r.Recipe != null && RecipeSession.Matches(r.Recipe, draft.Category) &&
                (!draft.FavoritesOnly || RecipeSession.Options.IsFavorite(r.Recipe))).ToList();
            tableMetrics = DisplayMetrics();
            availability.Configure(known.Select(r => r.Recipe), Material, draft.Executions, draft.OnlyStockReady, draft.MixSubstitutes);
            known = FilterStock(known);
            pinnedWork.Replace(PreparePinned());
            scores.Clear(); scoreDetails.Clear();
            if (goals.Count == 0 && comparisonMode == "name") known = SortingCore.Alphabetical(known, r => r.Label, draft.Descending);
            display = NativeMenuCore.MergeKnown(visible, r => r.Recipe != null, known);
            if (selected != null && !display.Contains(selected)) selected = null;
            prepared = 0;
            preparationTotal = known.Count * goals.Count;
            if (goals.Count > 0)
                preparation.Replace(PrepareComparison(visible, known, goals.ToList(), draft.Descending,
                    goals.Select(m => draft.Criteria.First(c => c.Id == m.Id).HigherIsBetter).ToList()));
            dirty = false;
        }
        private IEnumerable<Action> PrepareComparison(List<ComparisonRow> visible, List<ComparisonRow> known, List<Metric> selectedGoals,
            bool descending, List<bool> directions)
        {
            foreach (var row in known)
                foreach (var metric in selectedGoals)
                    yield return () => { Measure(row.Recipe, metric); prepared++; };
            yield return () =>
            {
                var criteria = selectedGoals.Select(m => draft.Criteria.First(c => c.Id == m.Id)).ToList();
                known = FilterStock(known.Where(r => selectedGoals.Select((m, i) => criteria[i].Matches(Reading(r.Recipe, m).Value)).All(v => v)).ToList());
                if (selectedGoals.Count == 1)
                {
                    known = SortingCore.Numeric(known, r => Reading(r.Recipe, selectedGoals[0]).Value, r => r.Label, descending == directions[0]);
                    foreach (var row in known) scores[row] = Reading(row.Recipe, selectedGoals[0]).Value;
                }
                else
                {
                    var ranked = SortingCore.Balanced(known, r => selectedGoals.Select(m => Reading(r.Recipe, m).Value).ToArray(), r => r.Label, directions, descending, criteria.Select(c => c.Weight).ToList());
                    known = ranked.Select(r => r.Item).ToList();
                    foreach (var result in ranked)
                    {
                        scores[result.Item] = result.Score;
                        var lines = new List<string> { "MSS.ScoreExplanation".Translate() };
                        for (int i = 0; i < selectedGoals.Count; i++)
                            lines.Add(result.Desirabilities == null ? selectedGoals[i].Name + ": " + "MSS.NoData".Translate() :
                                result.Desirabilities[i].HasValue ? "MSS.ScorePart".Translate(selectedGoals[i].Name,
                                    Reading(result.Item.Recipe, selectedGoals[i]).Display, (result.Desirabilities[i].Value * 100).ToString("0.#"),
                                    (result.WeightShares[i] * 100).ToString("0.#"), criteria[i].Weight).ToString() :
                                    "MSS.ScoreConstant".Translate(selectedGoals[i].Name).ToString());
                        scoreDetails[result.Item] = string.Join("\n\n", lines);
                    }
                }
                // Scores/limits still use all criteria, but ordering is independent.
                if (comparisonMode == "name") known = SortingCore.Alphabetical(known, r => r.Label, descending);
                else if (comparisonMode == "original")
                {
                    var allowed = new HashSet<ComparisonRow>(known);
                    known = visible.Where(allowed.Contains).ToList();
                }
                display = NativeMenuCore.MergeKnown(visible, r => r.Recipe != null, known);
                if (selected != null && !display.Contains(selected)) selected = null;
            };
        }
        private void SetCriterion(SortCriterion value)
        {
            int index = draft.Criteria.FindIndex(c => c.Id == value.Id);
            if (index < 0) draft.Criteria.Add(value.Copy());
            else draft.Criteria[index] = value.Copy();
            draft.Metric = "original"; draft.OrderMode = comparisonMode = "criteria";
            dirty = true; recipeScroll = Vector2.zero;
        }
        private void OpenCriterion(string id)
        {
            var metric = Metrics.Resolve(id);
            if (metric == null) return;
            var value = draft.Criteria.FirstOrDefault(c => c.Id == id) ?? new SortCriterion { Id = id, HigherIsBetter = metric.HigherIsBetter };
            Find.WindowStack.Add(new CriterionDialog(metric, value, SetCriterion));
        }
        private List<ActiveFilter> ActiveFilters()
        {
            var result = new List<ActiveFilter>();
            if (!string.IsNullOrWhiteSpace(search)) result.Add(new ActiveFilter { Key = "search", Label = "MSS.FilterSearch".Translate(search) });
            foreach (var id in CategorySelection.Unpack(draft.Category))
                result.Add(new ActiveFilter { Key = "category:" + id, Label = categories.FirstOrDefault(c => c.Id == id)?.Name ?? id });
            if (draft.OnlyStockReady) result.Add(new ActiveFilter { Key = "stock", Label = "MSS.StockReady".Translate() });
            if (draft.FavoritesOnly) result.Add(new ActiveFilter { Key = "favorites", Label = "MSS.Favorites".Translate() });
            foreach (var criterion in draft.Criteria)
            {
                if (!criterion.HasMinimum && !criterion.HasMaximum) continue;
                var metric = Metrics.Resolve(criterion.Id);
                if (metric == null) continue;
                double scale = Metrics.LimitScale(metric);
                string unit = Metrics.LimitUnit(metric);
                if (criterion.HasMinimum) result.Add(new ActiveFilter { Key = "min:" + criterion.Id,
                    Label = metric.Name + " ≥ " + (criterion.Minimum * scale).ToString("G6") + " " + unit });
                if (criterion.HasMaximum) result.Add(new ActiveFilter { Key = "max:" + criterion.Id,
                    Label = metric.Name + " ≤ " + (criterion.Maximum * scale).ToString("G6") + " " + unit });
            }
            return result;
        }
        private void RemoveActiveFilter(string key)
        {
            if (key == "search") search = "";
            else if (key == "category") Category("all");
            else if (key != null && key.StartsWith("category:", StringComparison.Ordinal)) Category(CategorySelection.Remove(draft.Category, key.Substring(9)));
            else if (key == "favorites") draft.FavoritesOnly = false;
            else if (key == "stock") draft.OnlyStockReady = false;
            else if (key != null && (key.StartsWith("min:") || key.StartsWith("max:")))
            {
                var criterion = draft.Criteria.FirstOrDefault(c => c.Id == key.Substring(4));
                if (criterion == null) return;
                if (key.StartsWith("min:")) criterion.HasMinimum = false; else criterion.HasMaximum = false;
            }
            else return;
            dirty = true; prepareAfter = 0; recipeScroll = Vector2.zero; RefreshDisplay();
        }
        private void ClearActiveFilters()
        {
            search = ""; draft.FavoritesOnly = false; draft.OnlyStockReady = false;
            foreach (var criterion in draft.Criteria) { criterion.HasMinimum = false; criterion.HasMaximum = false; }
            Category("all"); dirty = true; prepareAfter = 0; filterScroll = recipeScroll = Vector2.zero; RefreshDisplay();
        }
        private void OpenIngredients()
        {
            if (selected?.Recipe == null || !ContextValid()) return;
            var dialog = new IngredientDialog(selected.Recipe, map, Material, ingredientChoices, ContextValid);
            dialog.SetExecutions(draft.Executions); dialog.SetMixing(draft.MixSubstitutes); Find.WindowStack.Add(dialog);
        }
        private void DrawActiveFilters(Rect rect)
        {
            var filters = ActiveFilters();
            Text.Font = GameFont.Tiny;
            Label(new Rect(rect.x, rect.y + 3f, 104f, 22f), "MSS.ActiveFilters".Translate(filters.Count), Muted, GameFont.Tiny);
            var clear = new Rect(rect.xMax - 94f, rect.y, 94f, 27f);
            bool before = GUI.enabled; GUI.enabled = before && filters.Count > 0;
            bool clearClicked = Button(clear, "MSS.ClearFilters".Translate()); GUI.enabled = before;
            var viewport = new Rect(rect.x + 108f, rect.y, Mathf.Max(1, rect.width - 210f), 44f);
            float width = filters.Sum(f => Mathf.Min(230f, Text.CalcSize(f.Label).x + 26f) + 4f);
            var content = new Rect(0, 0, Mathf.Max(viewport.width - 1f, width), 27f);
            Widgets.BeginScrollView(viewport, ref filterScroll, content);
            float x = 0;
            string remove = null;
            foreach (var filter in filters)
            {
                var chip = new Rect(x, 0, Mathf.Min(230f, Text.CalcSize(filter.Label).x + 26f), 27f);
                if (Button(chip, "× " + filter.Label.Truncate(chip.width - 26f))) remove = filter.Key;
                TooltipHandler.TipRegion(chip, filter.Label + "\n" + "MSS.RemoveFilterTip".Translate()); x = chip.xMax + 4f;
            }
            if (filters.Count == 0) Label(new Rect(0, 4f, content.width, 20f), "MSS.NoActiveFilters".Translate(), Muted, GameFont.Tiny);
            Widgets.EndScrollView();
            // Apply after closing the scroll group, retaining the UI snapshot.
            if (clearClicked) ClearActiveFilters(); else if (remove != null) RemoveActiveFilter(remove);
        }
        public override void WindowUpdate()
        {
            base.WindowUpdate();
            if (closed) return;
            if (!ContextValid()) { Close(); return; }
            PumpPreparation();
            UpdateIngredientPreview();
            pinnedWork.Run(1, 64); availability.Update();
            if (availability.Revision != availabilityRevision) { availabilityRevision = availability.Revision; dirty = true; }
        }
        private void PumpPreparation()
        {
            RefreshDisplay();
            var watch = Stopwatch.StartNew();
            if (Time.realtimeSinceStartup >= prepareAfter) preparation.Run(2, 512);
            if (materialsReady && !inventory.Pending && Time.realtimeSinceStartup >= nextCountRefresh)
            {
                inventory.Replace(CountMaterials(materials.ToList()));
                nextCountRefresh = Time.realtimeSinceStartup + 1.5f;
            }
            inventory.Run(Math.Max(0, 2.5 - watch.Elapsed.TotalMilliseconds), 32);
            discovery.Run(Math.Max(0, 4 - watch.Elapsed.TotalMilliseconds), 512);
        }
        private IEnumerable<Action> CountMaterials(List<ThingDef> snapshot)
        {
            var next = new Dictionary<ThingDef, long>();
            foreach (var material in snapshot)
                yield return () => next[material] = MaterialInventory.Count(map, new[] { material })[material];
            yield return () => counts = next;
        }
        public override void PreClose()
        {
            closed = true; pinnedWork.Dispose(); availability.Dispose();
            ingredientPreview?.Dispose(); ingredientPreview = null;
            discovery.Dispose(); preparation.Dispose(); inventory.Dispose();
            base.PreClose();
        }
        private void Apply()
        {
            RecipeSession.Options.CopyComparisonFrom(draft);
            RecipeSession.Options.RememberBench(benchKey, draft, profileId);
            RecipeSession.Changed();
            RecipeSession.ImportMeasurements(readings, Material, draft.Quality, draft.Ammo);
        }
        private void Category(string id)
        {
            draft.Category = CategorySelection.Pack(CategorySelection.Unpack(id));
            // Keep the user's checked goals visible, even if a category has no data.
            // Missing measurements remain explicit rather than becoming zero.
            recipeScroll = goalsScroll = materialScroll = Vector2.zero;
            prepareAfter = 0;
            RefreshChoices();
        }
        private void ToggleCategory(string id) { Category(CategorySelection.Toggle(draft.Category, id)); RefreshDisplay(); }
        private void SelectOrder(string mode)
        {
            if (mode != "criteria" && mode != "name" && mode != "original") return;
            draft.OrderMode = comparisonMode = mode;
            draft.Metric = mode == "name" ? "name" : "original";
            draft.Descending = mode != "name";
            dirty = true; recipeScroll = Vector2.zero; RefreshDisplay();
        }
        private void UpdateIngredientPreview()
        {
            if (closed) return;
            var recipe = selected?.Recipe;
            if (ingredientPreview?.Recipe != recipe || previewMaterial != Material)
            {
                ingredientPreview?.Dispose(); ingredientPreview = null; previewMaterial = Material;
                if (recipe != null) ingredientPreview = new IngredientInspector(recipe, map, Material, ingredientChoices);
            }
            ingredientPreview?.SetMixing(draft.MixSubstitutes); ingredientPreview?.SetExecutions(draft.Executions); ingredientPreview?.Update();
        }
        private List<Metric> DisplayMetrics()
        {
            var result = new List<Metric>();
            foreach (var column in draft.Columns)
            { var metric = goals.FirstOrDefault(m => m.Id == column.Id); if (metric != null && column.Visible) result.Add(metric); }
            result.AddRange(goals.Where(m => !draft.Columns.Any(c => c.Id == m.Id)));
            return result;
        }
        private int ColumnWidth(string id) => draft.Columns.FirstOrDefault(c => c.Id == id)?.Width ?? 134;
        private List<ComparisonRow> FilterStock(List<ComparisonRow> source)
            => !draft.OnlyStockReady || !availability.Ready ? source : source.Where(r => availability.Result(r.Recipe) == true).ToList();
        private void SetExecutions(int count)
        { count = BatchCount.Clamp(count); if (count == draft.Executions) return; draft.Executions = count; executionsText = count.ToString(); dirty = true; }
        private void TogglePin(RecipeDef recipe)
        {
            if (recipe == null || closed) return;
            if (!pinned.Remove(recipe))
            {
                if (pinned.Count >= 3) { Messages.Message("MSS.PinLimit".Translate(), MessageTypeDefOf.RejectInput, false); return; }
                pinned.Add(recipe);
            }
            pinnedWork.Replace(PreparePinned());
        }
        private IEnumerable<Action> PreparePinned()
        {
            foreach (var recipe in pinned.ToList()) foreach (var metric in goals.ToList())
            { var r = recipe; var m = metric; yield return () => Measure(r, m); }
        }
        private void OpenPinned()
        { if (pinned.Count > 0) Find.WindowStack.Add(new PinnedComparisonDialog(pinned, goals, Reading, () => !closed && ContextValid())); }
        private void CommitColumns(List<ComparisonColumn> columns)
        { draft.Columns = ComparisonColumn.Normalize(columns); dirty = true; RefreshDisplay(); }
        private void OpenColumns() => Find.WindowStack.Add(new ColumnsDialog(goals, draft.Columns, CommitColumns, () => !closed && ContextValid()));
        private void DrawRecipeTools(Rect rect)
        {
            Text.Font = GameFont.Tiny;
            if (Button(new Rect(rect.x, rect.y, 94f, 26f), "MSS.Columns".Translate())) OpenColumns();
            bool previous = GUI.enabled; GUI.enabled = previous && pinned.Count > 0;
            if (Button(new Rect(rect.x + 100f, rect.y, 126f, 26f), "MSS.PinnedCount".Translate(pinned.Count))) OpenPinned(); GUI.enabled = previous;
            Label(new Rect(rect.x + 234f, rect.y + 3f, 92f, 23f), "MSS.Executions".Translate(), null, GameFont.Tiny);
            string next = Widgets.TextField(new Rect(rect.x + 329f, rect.y, 50f, 26f), executionsText);
            if (next != executionsText) { executionsText = next; if (BatchCount.TryParse(next, out var count)) SetExecutions(count); }
            TooltipHandler.TipRegion(new Rect(rect.x + 234f, rect.y, 145f, 26f), "MSS.ExecutionsTip".Translate());
            bool before = draft.OnlyStockReady;
            var stockRect = rect.width < 640f ? new Rect(rect.x, rect.y + 29f, rect.width, 26f) : new Rect(rect.x + 388f, rect.y, rect.width - 388f, 26f);
            Widgets.CheckboxLabeled(stockRect, "MSS.StockReady".Translate(), ref draft.OnlyStockReady);
            if (before != draft.OnlyStockReady) { dirty = true; recipeScroll = Vector2.zero; }
            TooltipHandler.TipRegion(stockRect, "MSS.StockReadyTip".Translate());
            if (draft.OnlyStockReady && availability.Pending) TooltipHandler.TipRegion(rect, "MSS.StockProgress".Translate(availability.Completed, availability.Total));
        }
        private void ManageName(ComparisonProfile profile, bool duplicate)
        {
            string initial = duplicate ? RecipeSession.Options.UniqueProfileName(profile.Name) : profile.Name;
            var dialog = new ProfileNameDialog(initial, name =>
            {
                if (duplicate)
                { var copy = RecipeSession.Options.DuplicateProfile(profile.Id, name); if (copy != null) profileId = copy.Id; }
                else RecipeSession.Options.RenameProfile(profile.Id, name);
                MintStatSortMod.Instance.WriteSettings();
            });
            dialog.AllowExisting = false; dialog.TitleKey = duplicate ? "MSS.ProfileDuplicate" : "MSS.ProfileRename"; Find.WindowStack.Add(dialog);
        }
        private void ExportProfile(ComparisonProfile profile)
        {
            try { Messages.Message("MSS.FileSaved".Translate(ProfileExchange.Export(profile)), MessageTypeDefOf.TaskCompletion, false); }
            catch (Exception ex) { Messages.Message("MSS.FileError".Translate(ex.Message), MessageTypeDefOf.RejectInput, false); }
        }
        private void ImportProfiles()
        {
            try
            {
            Directory.CreateDirectory(ProfileExchange.DirectoryPath);
            var files = Directory.GetFiles(ProfileExchange.DirectoryPath, "*.xml").OrderBy(f => f).Take(100).ToList();
            if (files.Count == 0) { Messages.Message("MSS.ProfileImportHelp".Translate(ProfileExchange.DirectoryPath), MessageTypeDefOf.TaskCompletion, false); return; }
            Find.WindowStack.Add(new FloatMenu(files.Select(file => new FloatMenuOption(Path.GetFileName(file), () => ImportProfile(file))).ToList()));
            }
            catch (Exception ex) { Messages.Message("MSS.FileError".Translate(ex.Message), MessageTypeDefOf.RejectInput, false); }
        }
        private void ImportProfile(string file)
        {
            try
            { var profile = ProfileExchange.Import(file, RecipeSession.Options); MintStatSortMod.Instance.WriteSettings(); LoadProfile(profile.Id); Messages.Message("MSS.ProfileSaved".Translate(profile.Name), MessageTypeDefOf.TaskCompletion, false); }
            catch (Exception ex) { Messages.Message("MSS.FileError".Translate(ex.Message), MessageTypeDefOf.RejectInput, false); }
        }
        private void OpenDiagnostics() => Find.WindowStack.Add(new DiagnosticDialog("Context: " + contextName + "\nRecipes: " + recipes.Count +
            "\nCategories: " + draft.Category + "\nOrder: " + comparisonMode + "\nCriteria: " + string.Join(", ", draft.Criteria.Select(c => c.Id)) +
            "\nMaterial: " + draft.Material + "\nQuality: " + draft.Quality + "\nExecutions: " + draft.Executions +
            "\nStored estimate filter: " + draft.OnlyStockReady + "\nCached measurements: " + readings.Values.Sum(v => v.Count)));
        private static void Label(Rect rect, string text, Color? color = null, GameFont font = GameFont.Small)
        {
            Text.Font = font; GUI.color = color ?? Color.white;
            Widgets.Label(rect, text);
            GUI.color = Color.white;
        }
        private static bool Button(Rect rect, string text, bool primary = false, bool selected = false)
        {
            var background = primary ? new Color(0.43f, 0.33f, 0.16f) : selected ? new Color(0.28f, 0.25f, 0.17f) : new Color(0.13f, 0.16f, 0.19f);
            if (!GUI.enabled) background.a = 0.5f;
            Widgets.DrawBoxSolid(rect, background);
            var previous = GUI.color; GUI.color = primary || selected ? Gold : new Color(0.38f, 0.43f, 0.46f);
            Widgets.DrawBox(rect, 1); GUI.color = previous;
            var previousAnchor = Text.Anchor;
            bool clicked = Widgets.ButtonText(rect, "", drawBackground: false);
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect.ContractedBy(3f), text);
            Text.Anchor = previousAnchor;
            return clicked;
        }
        private static bool Tab(Rect rect, string label, bool selected) => Button(rect, label, selected: selected);
        public override void DoWindowContents(Rect rect)
        {
            if (!RecipeSession.Options.Enabled || !ContextValid()) { Close(); return; }
            using (new UiScope())
            {
                Text.Anchor = TextAnchor.UpperLeft; Text.WordWrap = false; GUI.color = Color.white;
                HandleShortcuts();
                bool compact = rect.width < 1000f;
                Label(new Rect(0, 0, Mathf.Max(180f, rect.width - 540f), 32f), "MintStatSort", null, GameFont.Medium);
                if (rect.width >= 1000f) Label(new Rect(0, 34f, rect.width, 22f), "MSS.ComparisonSubtitle".Translate(contextName), Muted);
                DrawProfiles(compact ? new Rect(0, 34f, Mathf.Min(480f, rect.width - 40f), 32f) : new Rect(Mathf.Max(180f, rect.width - 520f), 0, Mathf.Min(480f, rect.width - 220f), 32f));
                if (compact)
                {
                    if (Tab(new Rect(0, 70f, rect.width / 2f - 3f, 28f), "MSS.RecipesTab".Translate(), !compactCriteria)) compactCriteria = false;
                    if (Tab(new Rect(rect.width / 2f + 3f, 70f, rect.width / 2f - 3f, 28f), "MSS.CriteriaTab".Translate(), compactCriteria)) compactCriteria = true;
                }
                float actionHeight = selected?.Option == null || selected.Recipe != null ? 36f : Mathf.Max(36f, selected.Option.RequiredHeight);
                float footer = native ? 80f + actionHeight : 52f;
                float bodyY = compact ? 106f : 66f;
                var body = new Rect(0, bodyY, rect.width, Mathf.Max(60f, rect.height - bodyY - footer));
                float sideWidth = Mathf.Min(400f, Mathf.Max(320f, body.width * 0.31f));
                var left = new Rect(body.x, body.y, Mathf.Max(150f, body.width - sideWidth - 16f), body.height);
                var right = new Rect(left.xMax + 16f, body.y, sideWidth, body.height);
                RefreshDisplay();
                if (compact)
                {
                    if (compactCriteria) DrawSidebar(body);
                    else if (body.height < 430f)
                    {
                        var content = new Rect(0, 0, body.width - 18f, 480f);
                        Widgets.BeginScrollView(body, ref compactScroll, content);
                        DrawRecipes(content);
                        Widgets.EndScrollView();
                    }
                    else DrawRecipes(body);
                }
                else { DrawSidebar(right); DrawRecipes(left); }
                if (native) DrawNativeAction(new Rect(0, body.yMax + 6f, rect.width, actionHeight + 20f));
                float buttonY = rect.height - 34f;
                float buttonWidth = Mathf.Min(140f, rect.width / 4f);
                var apply = new Rect(rect.width - buttonWidth * 3f - 16f, buttonY, buttonWidth, 32f);
                if (Button(apply, "MSS.ApplyComparison".Translate(), primary: true)) { Apply(); if (!native) Close(); }
                if (Button(new Rect(apply.xMax + 8f, buttonY, buttonWidth, 32f), "MSS.ResetComparison".Translate()))
                {
                    draft.Reset(); executionsText = "1"; comparisonMode = "original"; ingredientPreview?.Dispose(); ingredientPreview = null; readings.Clear(); selected = null; profileId = null;
                    search = goalSearch = materialSearch = "";
                    recipeScroll = goalsScroll = materialScroll = Vector2.zero;
                    RefreshChoices();
                }
                if (Button(new Rect(rect.width - buttonWidth, buttonY, buttonWidth, 32f), "MSS.Close".Translate())) Close();
                var note = new Rect(0, buttonY, Mathf.Max(0, apply.x - 8f), 32f);
                Label(note, "MSS.PreviewHint".Translate().ToString().Truncate(note.width), Muted, GameFont.Tiny);
                TooltipHandler.TipRegion(note, "MSS.PreviewHint".Translate());
            }
        }
        private void DrawRecipes(Rect rect)
        {
            Text.Font = GameFont.Small;
            float favoriteWidth = Mathf.Min(124f, rect.width * 0.38f);
            var searchRect = new Rect(rect.x, rect.y, rect.width - favoriteWidth - 5f, 28f);
            GUI.SetNextControlName("MSSRecipeSearch");
            string next = Widgets.TextField(searchRect, search);
            if (focusSearch) { GUI.FocusControl("MSSRecipeSearch"); focusSearch = false; }
            if (next != search) { search = next; dirty = true; prepareAfter = Time.realtimeSinceStartup + 0.12f; recipeScroll = Vector2.zero; RefreshDisplay(); }
            if (search.Length == 0) Label(new Rect(rect.x + 8f, rect.y + 4f, searchRect.width - 16f, 22f), "MSS.SearchRecipes".Translate(), Muted, GameFont.Tiny);
            var favoriteFilter = new Rect(searchRect.xMax + 5f, rect.y, favoriteWidth, 28f);
            Text.Font = GameFont.Tiny;
            if (Tab(favoriteFilter, "MSS.Favorites".Translate(), draft.FavoritesOnly)) SetFavoritesOnly(!draft.FavoritesOnly);
            TooltipHandler.TipRegion(favoriteFilter, "MSS.FavoritesTip".Translate());
            var primary = categories.Where(c => c.Primary).ToList();
            float tabWidth = (rect.width - 4f * primary.Count) / (primary.Count + 1);
            for (int i = 0; i < primary.Count; i++)
            {
                var category = primary[i];
                var tab = new Rect(rect.x + i * (tabWidth + 4f), rect.y + 35f, tabWidth, 28f);
                Text.Font = GameFont.Tiny;
                if (Tab(tab, category.Name.Truncate(tab.width - 8f), CategorySelection.Contains(draft.Category, category.Id))) ToggleCategory(category.Id);
                TooltipHandler.TipRegion(tab, category.Name + " · " + "MSS.RecipeCount".Translate(category.Count) + "\n" + "MSS.MultiCategoryTip".Translate());
            }
            var more = new Rect(rect.x + primary.Count * (tabWidth + 4f), rect.y + 35f, tabWidth, 28f);
            if (Button(more, "MSS.MoreCategoriesShort".Translate()))
                Find.WindowStack.Add(new FloatMenu(categories.Select(c => new FloatMenuOption((CategorySelection.Contains(draft.Category, c.Id) ? "✓ " : "") + c.Name + " (" + c.Count + ")", () => ToggleCategory(c.Id))).ToList()));
            TooltipHandler.TipRegion(more, categories.FirstOrDefault(c => c.Id == draft.Category)?.Name ?? draft.Category);
            DrawActiveFilters(new Rect(rect.x, rect.y + 70f, rect.width, 44f));
            DrawRecipeTools(new Rect(rect.x, rect.y + 116f, rect.width, 28f));
            float y = rect.y + (rect.width < 640f ? 180f : 151f);
            float previewHeight = selected?.Recipe == null ? 34f : 86f;
            var previewRect = new Rect(rect.x, rect.yMax - previewHeight, rect.width, previewHeight);
            DrawIngredientPreview(previewRect);
            var viewport = new Rect(rect.x, y + 49f, rect.width, Mathf.Max(1, previewRect.y - y - 54f));
            var layout = new ComparisonLayout(rect.width, tableMetrics.Select(m => (float)ColumnWidth(m.Id)));
            var content = new Rect(0, 0, layout.ContentWidth, Mathf.Max(1, display.Count * 58f));
            GUI.BeginGroup(new Rect(rect.x, y, rect.width - 18f, 47f));
            DrawHeaders(layout, -recipeScroll.x);
            GUI.EndGroup();
            Widgets.BeginScrollView(viewport, ref recipeScroll, content);
            for (int i = 0; i < display.Count; i++)
            {
                var row = display[i];
                var tile = new Rect(0, i * 58f, content.width, 55f);
                if (tile.yMax < recipeScroll.y || tile.y > recipeScroll.y + viewport.height) continue;
                if (selected == row) { Widgets.DrawBoxSolid(tile, new Color(0.5f, 0.4f, 0.2f, 0.32f)); Widgets.DrawBoxSolid(new Rect(tile.x, tile.y, 4f, tile.height), Gold); }
                else if (Mouse.IsOver(tile)) Widgets.DrawHighlight(tile);
                // Consume the star's click before the native selection area, so
                // marking a favourite cannot invoke/select a bill action.
                if (row.Recipe != null)
                {
                    var star = new Rect(3f, tile.y + 11f, 27f, 30f);
                    bool favorite = RecipeSession.Options.IsFavorite(row.Recipe);
                    Text.Font = GameFont.Small; GUI.color = favorite ? Gold : Muted;
                    if (Button(star, favorite ? "★" : "☆")) ToggleFavorite(row.Recipe);
                    GUI.color = Color.white;
                    TooltipHandler.TipRegion(star, (favorite ? "MSS.Unfavorite" : "MSS.Favorite").Translate());
                }
                if (Widgets.ButtonInvisible(new Rect(32f, tile.y, tile.width - 32f, tile.height))) selected = row;
                Widgets.DrawLineHorizontal(0, tile.yMax, tile.width);
                var product = row.Recipe?.ProducedThingDef;
                if (product != null) Widgets.ThingIcon(new Rect(34f, tile.y + 6f, 40f, 40f), product,
                    product.MadeFromStuff ? (Material != null && GenStuff.AllowedStuffsFor(product).Contains(Material) ? Material : GenStuff.DefaultStuffFor(product)) : null);
                bool enabled = RecipeAvailable(row.Recipe) && (row.Option == null || !row.Option.Disabled);
                var name = new Rect(80f, tile.y + 6f, layout.NameWidth - 88f, 25f);
                Label(name, row.Label.Truncate(name.width), enabled ? Color.white : Color.gray);
                Label(new Rect(80f, tile.y + 30f, name.width, 20f), product?.LabelCap.ToString() ?? "MSS.NativeOption".Translate().ToString(), Muted, GameFont.Tiny);
                TooltipHandler.TipRegion(new Rect(32f, tile.y, layout.NameWidth - 32f, tile.height), RequirementText(row));
                for (int column = 0; column < tableMetrics.Count; column++)
                {
                    var cell = new Rect(layout.X(column), tile.y + 14f, layout.Width(column), 28f);
                    var value = row.Recipe == null ? Measurement.Missing() : Reading(row.Recipe, tableMetrics[column]);
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Label(cell, value.Display.Truncate(cell.width - 6f), value.Value.HasValue ? ValueColor : Color.gray, GameFont.Tiny);
                    TooltipHandler.TipRegion(cell, value.Detail);
                    Text.Anchor = TextAnchor.UpperLeft;
                }
                var total = new Rect(layout.ContentWidth - ComparisonLayout.TotalWidth, tile.y + 14f, ComparisonLayout.TotalWidth, 28f);
                Text.Anchor = TextAnchor.MiddleCenter;
                Label(total, ScoreText(row), new Color(0.5f, 0.95f, 0.58f), GameFont.Tiny);
                Text.Anchor = TextAnchor.UpperLeft;
                TooltipHandler.TipRegion(total, scoreDetails.TryGetValue(row, out var explanation) ? explanation : (goals.Count > 1 ? "MSS.BalanceTip" : "MSS.SingleValueTip").Translate().ToString());
            }
            if (display.Count == 0)
            {
                Text.WordWrap = true;
                Label(new Rect(5f, 5f, content.width - 10f, 65f), (draft.OnlyStockReady ? "MSS.NoStockReady" : draft.Criteria.Any(c => c.HasMinimum || c.HasMaximum) ? "MSS.NoLimitMatches" : draft.FavoritesOnly ? "MSS.NoFavorites" : "MSS.NoMatches").Translate(), Muted);
                Text.WordWrap = false;
            }
            Widgets.EndScrollView();
        }
        private string ScoreText(ComparisonRow row)
        {
            if (!scores.TryGetValue(row, out var score) || !score.HasValue) return preparation.Pending ? "…" : "MSS.NoData".Translate();
            return goals.Count == 1 ? Reading(row.Recipe, goals[0]).Display : "MSS.ScoreValue".Translate(score.Value.ToString("0.#")).ToString();
        }
        private void DrawHeaders(ComparisonLayout layout, float x)
        {
            Text.Font = GameFont.Tiny;
            if (Button(new Rect(x, 0, layout.NameWidth - 4f, 44f), "MSS.ProductRecipe".Translate() + (comparisonMode == "name" ? draft.Descending ? " ↓" : " ↑" : "")))
            {
                bool descending = comparisonMode == "name" && !draft.Descending;
                SelectOrder("name"); draft.Descending = descending; dirty = true;
            }
            TooltipHandler.TipRegion(new Rect(x, 0, layout.NameWidth - 4f, 44f), "MSS.NameOrderTip".Translate());
            for (int i = 0; i < tableMetrics.Count; i++)
            {
                var cell = new Rect(x + layout.X(i), 0, layout.Width(i) - 4f, 44f);
                var goal = tableMetrics[i];
                // A visible metric is not necessarily an active sorting criterion.
                // The fallback describes the column without modifying the user's draft.
                var criterion = draft.Criteria.FirstOrDefault(c => c.Id == goal.Id)
                    ?? new SortCriterion { Id = goal.Id, HigherIsBetter = goal.HigherIsBetter };
                Widgets.DrawBoxSolid(cell, new Color(0.14f, 0.17f, 0.19f, 0.8f));
                Text.WordWrap = true; Text.Anchor = TextAnchor.MiddleCenter;
                Label(cell.ContractedBy(3f), goal.Name, ValueColor, GameFont.Tiny);
                Text.WordWrap = false; Text.Anchor = TextAnchor.UpperLeft;
                TooltipHandler.TipRegion(cell, goal.Name + "\n" + (criterion.HigherIsBetter ? "MSS.PreferHigher" : "MSS.PreferLower").Translate() + " · " + "MSS.Importance".Translate(criterion.Weight));
            }
            var total = new Rect(x + layout.ContentWidth - ComparisonLayout.TotalWidth, 0, ComparisonLayout.TotalWidth, 44f);
            if (Button(total, (goals.Count > 1 ? "MSS.Total" : "MSS.Value").Translate() + (comparisonMode == "criteria" ? draft.Descending ? " ↓" : " ↑" : "")))
            { bool descending = comparisonMode != "criteria" || !draft.Descending; SelectOrder("criteria"); draft.Descending = descending; dirty = true; }
            TooltipHandler.TipRegion(total, "MSS.RelativeScoreTip".Translate());
        }
        private void DrawIngredientPreview(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.12f, 0.15f, 0.17f, 0.8f));
            if (selected?.Recipe == null)
            { Label(rect.ContractedBy(6f), "MSS.SelectForIngredients".Translate(), Muted, GameFont.Tiny); return; }
            var detail = new Rect(rect.xMax - 112f, rect.y + 5f, 106f, 27f);
            if (Button(detail, "MSS.IngredientDetails".Translate())) OpenIngredients();
            if (Button(new Rect(detail.x - 98f, detail.y, 92f, 27f), (pinned.Contains(selected.Recipe) ? "MSS.Unpin" : "MSS.Pin").Translate())) TogglePin(selected.Recipe);
            Label(new Rect(rect.x + 8f, rect.y + 5f, Mathf.Max(0, rect.width - 232f), 24f), selected.Label.Truncate(Mathf.Max(0, rect.width - 238f)), null, GameFont.Tiny);
            string summary = "MSS.PreparingIngredients".Translate();
            if (ingredientPreview?.Recipe == selected.Recipe && ingredientPreview.Ready)
            {
                summary = ingredientPreview.Rows.Count == 0 ? "MSS.NoIngredients".Translate().ToString() : string.Join(" · ", ingredientPreview.Rows.Take(3).Select(r =>
                    "MSS.IngredientCompact".Translate(r.Required?.ToString() ?? "—", r.Reference?.LabelCap.ToString() ?? r.Label,
                    r.Available?.ToString() ?? "—", r.Missing?.ToString() ?? "—").ToString()));
                if (ingredientPreview.Rows.Count > 3) summary += " · " + "MSS.IngredientMore".Translate(ingredientPreview.Rows.Count - 3);
            }
            Text.WordWrap = true;
            Label(new Rect(rect.x + 8f, rect.y + 34f, rect.width - 16f, 46f), summary, Muted, GameFont.Tiny);
            Text.WordWrap = false;
            TooltipHandler.TipRegion(rect, "MSS.IngredientsStorageNote".Translate());
        }
        private void DrawSidebar(Rect outer)
        {
            float goalHeight = Mathf.Clamp(outer.height * 0.6f, 310f, 470f);
            var content = new Rect(0, 0, outer.width - 18f, Mathf.Max(goalHeight + 245f, outer.height - 1f));
            Widgets.BeginScrollView(outer, ref sideScroll, content);
            DrawGoals(new Rect(0, 0, content.width, goalHeight));
            DrawMaterials(new Rect(0, goalHeight + 10f, content.width, content.height - goalHeight - 10f));
            Widgets.EndScrollView();
        }
        private void DrawProfiles(Rect rect)
        {
            var profile = RecipeSession.Options.Profiles.FirstOrDefault(p => p.Id == profileId);
            string name = profile == null ? "MSS.ProfileCustom".Translate().ToString() : profile.Name + (profile.Values.Matches(draft) ? "" : " *");
            Text.Font = GameFont.Tiny;
            var pick = new Rect(rect.x, rect.y, rect.width - 166f, 30f);
            if (Button(pick, "MSS.ProfilePick".Translate(name).ToString().Truncate(pick.width - 12f)))
            {
                var profiles = StarterProfiles.Names.Select(id => new FloatMenuOption("MSS.StarterPrefix".Translate(("MSS.Starter." + id).Translate()),
                    () => UsePreferences(StarterProfiles.Create(id, draft), null))).ToList();
                profiles.AddRange(RecipeSession.Options.Profiles.OrderBy(p => p.Name).Select(p => new FloatMenuOption(p.Name, () => LoadProfile(p.Id))));
                Find.WindowStack.Add(new FloatMenu(profiles));
            }
            TooltipHandler.TipRegion(pick, "MSS.ProfilePickTip".Translate());
            if (Button(new Rect(pick.xMax + 6f, rect.y, 118f, 30f), "MSS.ProfileSave".Translate()))
                Find.WindowStack.Add(new ProfileNameDialog(profile?.Name, SaveProfile));
            if (Button(new Rect(rect.xMax - 36f, rect.y, 36f, 30f), "…"))
            {
                var actions = new List<FloatMenuOption>
                {
                    new FloatMenuOption("MSS.ProfileRename".Translate(), profile == null ? (Action)null : () => ManageName(profile, false)),
                    new FloatMenuOption("MSS.ProfileDuplicate".Translate(), profile == null ? (Action)null : () => ManageName(profile, true)),
                    new FloatMenuOption("MSS.ProfileExport".Translate(), profile == null ? (Action)null : () => ExportProfile(profile)),
                    new FloatMenuOption("MSS.ProfileImport".Translate(), ImportProfiles),
                    new FloatMenuOption("MSS.Diagnostics".Translate(), OpenDiagnostics),
                    new FloatMenuOption((draft.MixSubstitutes ? "✓ " : "") + "MSS.MixSubstitutes".Translate(), () => { draft.MixSubstitutes = !draft.MixSubstitutes; dirty = true; }),
                    new FloatMenuOption("MSS.Shortcuts".Translate(), () => Messages.Message("MSS.ShortcutsTip".Translate(), MessageTypeDefOf.TaskCompletion, false))
                };
                if (CombatExtendedIntegration.Active) actions.Insert(0, new FloatMenuOption("MSS.CE.ChooseAmmo".Translate(), ChooseAmmo));
                actions.AddRange(RecipeSession.Options.Profiles.OrderBy(p => p.Name).Select(p => new FloatMenuOption("MSS.ProfileDeleteNamed".Translate(p.Name), () =>
                { RecipeSession.Options.DeleteProfile(p.Id); if (profileId == p.Id) profileId = null; MintStatSortMod.Instance.WriteSettings(); })));
                Find.WindowStack.Add(new FloatMenu(actions));
            }
        }
        private void ChooseAmmo()
        {
            var options = new List<FloatMenuOption> { new FloatMenuOption("MSS.CE.DefaultAmmo".Translate(), () => SelectAmmo("")) };
            var product = selected?.Recipe?.ProducedThingDef;
            var ammo = CombatExtendedIntegration.Ammunition(product);
            if (ammo.Count == 0) Messages.Message("MSS.CE.SelectWeapon".Translate(), MessageTypeDefOf.RejectInput, false);
            else options.AddRange(ammo.Select(a => new FloatMenuOption(a.LabelCap.ToString(), () => SelectAmmo(a.defName))));
            Find.WindowStack.Add(new FloatMenu(options));
        }
        private void SelectAmmo(string id)
        {
            if (draft.Ammo == id) return;
            draft.Ammo = id; readings.Clear(); availableChoices.Clear(); dirty = true; prepareAfter = 0;
            RefreshChoices(); RefreshDisplay();
        }
        private void HandleShortcuts()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown) return;
            if (e.control && e.keyCode == KeyCode.F) { compactCriteria = false; focusSearch = true; e.Use(); }
            else if (e.control && e.keyCode == KeyCode.P) { if (selected?.Recipe != null) TogglePin(selected.Recipe); e.Use(); }
            else if (e.control && e.keyCode == KeyCode.Return) { Apply(); e.Use(); }
            else if (e.control && e.keyCode == KeyCode.Tab) { compactCriteria = !compactCriteria; e.Use(); }
            else if (e.alt && (e.keyCode == KeyCode.UpArrow || e.keyCode == KeyCode.DownArrow))
            {
                if (display.Count > 0)
                {
                    int index = Mathf.Clamp(display.IndexOf(selected) + (e.keyCode == KeyCode.DownArrow ? 1 : -1), 0, display.Count - 1);
                    selected = display[index]; recipeScroll.y = Mathf.Max(0, index * 58f - 58f);
                }
                e.Use();
            }
        }
        private void SelectQuality(QualityCategory quality)
        {
            if (draft.Quality == quality) return;
            draft.Quality = quality;
            readings.Clear(); dirty = true; prepareAfter = 0;
            recipeScroll = Vector2.zero;
            RefreshChoices(); RefreshDisplay();
        }
        private void DrawGoals(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.08f, 0.10f, 0.11f, 0.5f));
            Label(new Rect(rect.x + 8f, rect.y + 5f, rect.width - 16f, 25f), "MSS.SortBy".Translate());
            Text.Font = GameFont.Tiny;
            float modeWidth = (rect.width - 24f) / 3f;
            var modes = new[] { "criteria", "name", "original" };
            var names = new[] { "MSS.Characteristics", "MSS.Name", "MSS.Original" };
            for (int i = 0; i < 3; i++)
                if (Tab(new Rect(rect.x + 8f + i * (modeWidth + 4f), rect.y + 32f, modeWidth, 25f), names[i].Translate(), comparisonMode == modes[i])) SelectOrder(modes[i]);
            var quality = new Rect(rect.x + 8f, rect.y + 63f, rect.width - 16f, 28f);
            if (Button(quality, "MSS.Quality".Translate(draft.Quality.GetLabel().CapitalizeFirst())))
                Find.WindowStack.Add(new FloatMenu(Enum.GetValues(typeof(QualityCategory)).Cast<QualityCategory>()
                    .Select(q => new FloatMenuOption(q.GetLabel().CapitalizeFirst(), () => SelectQuality(q))).ToList()));
            TooltipHandler.TipRegion(quality, "MSS.QualityTip".Translate());
            Label(new Rect(rect.x + 8f, rect.y + 103f, rect.width - 16f, 23f), "MSS.ComparisonCriteria".Translate(draft.Criteria.Count), null, GameFont.Tiny);
            var field = new Rect(rect.x + 8f, rect.y + 132f, rect.width - 114f, 26f);
            string next = Widgets.TextField(field, goalSearch);
            if (next != goalSearch) { goalSearch = next; goalsScroll = Vector2.zero; if (goalSearch.Length > 0 && !advanced) { advanced = true; RefreshChoices(); } }
            if (goalSearch.Length == 0) Label(field.ContractedBy(4f), "MSS.SearchStats".Translate(), Muted, GameFont.Tiny);
            if (Button(new Rect(field.xMax + 4f, field.y, 94f, 26f), (showInactive ? "MSS.ActiveOnly" : "MSS.AddStatistics").Translate()))
            { showInactive = !showInactive; advanced = true; goalsScroll = Vector2.zero; RefreshChoices(); }
            var filtered = choices.Where(m => (showInactive || goalSearch.Length > 0 || draft.Criteria.Any(c => c.Id == m.Id)) && PickerTiles.Matches(m.Name + " " + m.Group, goalSearch))
                .OrderByDescending(m => draft.Criteria.Any(c => c.Id == m.Id)).ThenBy(m => m.Name).ToList();
            var viewport = new Rect(rect.x + 8f, rect.y + 168f, rect.width - 16f, rect.height - 224f);
            var content = new Rect(0, 0, viewport.width - 18f, Mathf.Max(1, filtered.Count * 83f));
            Widgets.BeginScrollView(viewport, ref goalsScroll, content);
            for (int i = 0; i < filtered.Count; i++)
            {
                var metric = filtered[i]; var tile = new Rect(0, i * 83f, content.width, 80f);
                if (tile.yMax < goalsScroll.y || tile.y > goalsScroll.y + viewport.height) continue;
                var criterion = draft.Criteria.FirstOrDefault(c => c.Id == metric.Id);
                bool enabled = criterion != null, before = enabled;
                Text.WordWrap = true; Text.Font = GameFont.Tiny;
                Widgets.Checkbox(new Vector2(0, tile.y + 4f), ref enabled, 22f);
                Label(new Rect(30f, tile.y, tile.width - 30f, 34f), metric.Name, null, GameFont.Tiny);
                Text.WordWrap = false;
                if (enabled != before)
                {
                    if (enabled) { criterion = new SortCriterion { Id = metric.Id, HigherIsBetter = metric.HigherIsBetter }; draft.Criteria.Add(criterion); }
                    else { draft.Criteria.Remove(criterion); criterion = null; }
                    draft.Metric = "original"; draft.OrderMode = comparisonMode = "criteria"; dirty = true;
                }
                TooltipHandler.TipRegion(new Rect(0, tile.y, tile.width, 34f), metric.Name + "\n" + metric.Group + "\n" + (metric.Stat?.description ?? "MSS.CompareTip".Translate().ToString()));
                float importanceWidth = Mathf.Min(94f, tile.width * 0.31f);
                var importance = new Rect(0, tile.y + 40f, importanceWidth, 28f);
                if (Button(importance, "MSS.ImportanceShort".Translate(criterion?.Weight ?? 1)))
                {
                    var captured = criterion?.Copy() ?? new SortCriterion { Id = metric.Id, HigherIsBetter = metric.HigherIsBetter };
                    Find.WindowStack.Add(new FloatMenu(Enumerable.Range(1, 5).Select(w => new FloatMenuOption("MSS.Importance".Translate(w), () => { captured.Weight = w; SetCriterion(captured); })).ToList()));
                }
                var direction = new Rect(importance.xMax + 4f, importance.y, Mathf.Max(60f, tile.width - importanceWidth - 78f), 28f);
                if (Button(direction, ((criterion?.HigherIsBetter ?? metric.HigherIsBetter) ? "MSS.PreferHigher" : "MSS.PreferLower").Translate()))
                {
                    var captured = criterion?.Copy() ?? new SortCriterion { Id = metric.Id };
                    Find.WindowStack.Add(new FloatMenu(new[] { true, false }.Select(higher => new FloatMenuOption((higher ? "MSS.PreferHigher" : "MSS.PreferLower").Translate(), () => { captured.HigherIsBetter = higher; SetCriterion(captured); })).ToList()));
                }
                var limits = new Rect(tile.width - 66f, importance.y, 66f, 28f);
                if (Button(limits, "MSS.LimitsShort".Translate() + (criterion != null && (criterion.HasMinimum || criterion.HasMaximum) ? " *" : ""))) OpenCriterion(metric.Id);
                TooltipHandler.TipRegion(limits, "MSS.CriterionSettings".Translate());
                Widgets.DrawLineHorizontal(0, tile.yMax, tile.width);
            }
            if (filtered.Count == 0)
            { Text.WordWrap = true; Label(new Rect(0, 0, content.width, 65f), (draft.Criteria.Count == 0 && !showInactive && goalSearch.Length == 0 ? "MSS.AddStatisticsHint" : discovery.Pending ? "MSS.PreparingStats" : "MSS.NoCharacteristics").Translate(), Muted, GameFont.Tiny); Text.WordWrap = false; }
            Widgets.EndScrollView();
            Text.WordWrap = true;
            Label(new Rect(rect.x + 8f, rect.yMax - 46f, rect.width - 16f, 42f),
                draft.OnlyStockReady && availability.Pending ? "MSS.StockProgress".Translate(availability.Completed, availability.Total).ToString() : preparation.Pending ? "MSS.PreparingComparison".Translate(prepared, preparationTotal).ToString() : discovery.Pending ? "MSS.PreparingStats".Translate().ToString() : "MSS.RelativeScoreTip".Translate().ToString(), Muted, GameFont.Tiny);
            Text.WordWrap = false;
        }
        private void DrawMaterials(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.08f, 0.10f, 0.11f, 0.5f));
            Label(new Rect(rect.x + 8f, rect.y + 5f, rect.width - 16f, 25f), "MSS.MaterialTitle".Translate());
            TooltipHandler.TipRegion(new Rect(rect.x, rect.y, rect.width, 30f), "MSS.MaterialTip".Translate());
            Text.Font = GameFont.Tiny;
            string nextMaterialSearch = Widgets.TextField(new Rect(rect.x + 8f, rect.y + 33f, rect.width - 16f, 23f), materialSearch);
            if (nextMaterialSearch != materialSearch) { materialSearch = nextMaterialSearch; materialScroll = Vector2.zero; }
            TooltipHandler.TipRegion(new Rect(rect.x + 8f, rect.y + 33f, rect.width - 16f, 23f), "MSS.SearchMaterials".Translate());
            var currentCounts = counts;
            var filtered = materials.Where(m => (!draft.StoredMaterialsOnly || (currentCounts.TryGetValue(m, out var quantity) && quantity > 0) || m.defName == draft.Material) && PickerTiles.Matches(m.LabelCap.ToString(), materialSearch)).ToList();
            var viewport = new Rect(rect.x + 8f, rect.y + 64f, rect.width - 16f, rect.height - 99f);
            var content = new Rect(0, 0, viewport.width - 18f, (filtered.Count + 1) * 39f + (filtered.Count == 0 ? 65f : 0));
            Widgets.BeginScrollView(viewport, ref materialScroll, content);
            DrawMaterial(new Rect(0, 0, content.width, 36f), null, "MSS.AutoMaterial".Translate(), "MSS.AutoMaterialHelp".Translate());
            for (int i = 0; i < filtered.Count; i++)
            {
                var material = filtered[i];
                var tile = new Rect(0, (i + 1) * 39f, content.width, 36f);
                if (tile.yMax < materialScroll.y || tile.y > materialScroll.y + viewport.height) continue;
                DrawMaterial(tile, material, material.LabelCap, currentCounts.TryGetValue(material, out var quantity) ? "MSS.StoredCount".Translate(quantity).ToString() : "MSS.Preparing".Translate().ToString());
            }
            if (filtered.Count == 0)
            {
                Text.WordWrap = true;
                Label(new Rect(0, 42f, content.width, 62f), (!materialsReady || inventory.Pending ? "MSS.PreparingMaterials" : draft.StoredMaterialsOnly ? "MSS.NoStoredMaterials" : "MSS.NoMatches").Translate(), Muted, GameFont.Tiny);
                Text.WordWrap = false;
            }
            Widgets.EndScrollView();
            bool before = draft.StoredMaterialsOnly;
            Widgets.CheckboxLabeled(new Rect(rect.x + 8f, rect.yMax - 28f, rect.width - 16f, 25f), "MSS.StoredOnly".Translate(), ref draft.StoredMaterialsOnly);
            if (before != draft.StoredMaterialsOnly) materialScroll = Vector2.zero;
        }
        private void DrawMaterial(Rect rect, ThingDef material, string name, string detail)
        {
            bool active = (material?.defName ?? "") == draft.Material;
            bool clicked = Tab(rect, "", active);
            if (material != null) Widgets.ThingIcon(new Rect(rect.x + 4f, rect.y + 3f, 30f, 30f), material);
            float indent = material == null ? 6f : 38f;
            Label(new Rect(rect.x + indent, rect.y + 1f, rect.width - indent - 4f, 18f), name.Truncate(rect.width - indent - 4f), null, GameFont.Tiny);
            Label(new Rect(rect.x + indent, rect.y + 18f, rect.width - indent - 4f, 18f), detail.Truncate(rect.width - indent - 4f), Muted, GameFont.Tiny);
            TooltipHandler.TipRegion(rect, name + "\n" + detail + "\n" + "MSS.MaterialTip".Translate());
            if (clicked && !active)
            {
                draft.Material = material?.defName ?? "";
                readings.Clear(); dirty = true;
                RefreshChoices();
            }
        }
        private void DrawNativeAction(Rect rect)
        {
            Text.Font = GameFont.Tiny;
            Label(new Rect(rect.x, rect.y, rect.width, 18f), "MSS.SelectThenAdd".Translate(), Muted, GameFont.Tiny);
            if (selected?.Recipe != null || selected == null)
            {
                var button = new Rect(rect.xMax - 220f, rect.y + 22f, 220f, 34f);
                bool canAdd = selected?.Option != null && !selected.Option.Disabled && selected.Option.action != null
                    && ContextValid() && RecipeAvailable(selected.Recipe);
                Label(new Rect(rect.x, button.y + 5f, Mathf.Max(0, rect.width - 232f), 24f),
                    selected?.Label ?? "MSS.SelectRecipe".Translate().ToString(), canAdd ? Color.white : Muted);
                bool oldEnabled = GUI.enabled;
                GUI.enabled = oldEnabled && canAdd;
                bool clicked = Button(button, "MSS.AddBill".Translate());
                GUI.enabled = oldEnabled;
                if (clicked && canAdd)
                {
                    try
                    {
                        // Invoke the game's existing callback, including its checks,
                        // style choice and warnings; never construct a bill ourselves.
                        if (NativeBillAction.TryExecute(ContextValid(), RecipeAvailable(selected.Recipe), selected.Option.Disabled, selected.Option.action))
                        { Apply(); Close(); }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("[Mint Stat Sort] Native bill action failed: " + ex);
                        Messages.Message("MSS.AddBillFailed".Translate(), MessageTypeDefOf.RejectInput, false);
                    }
                }
                TooltipHandler.TipRegion(button, canAdd ? "MSS.AddBillTip".Translate().ToString() : selected == null ? "MSS.SelectRecipe".Translate().ToString() : RequirementText(selected));
                return;
            }
            if (selected.Option == null) return;
            if (!ContextValid() || !RecipeAvailable(selected.Recipe))
            { Label(new Rect(rect.x, rect.y + 20f, rect.width, 30f), "MSS.RecipeUnavailable".Translate(), Color.gray); return; }
            // Use the real option, including disabled state, ideology style, info
            // button, warnings and the original bill callback. Never recreate bills.
            Text.Font = GameFont.Small; Text.WordWrap = true;
            if (selected.Option.DoGUI(new Rect(rect.x, rect.y + 20f, rect.width, Mathf.Max(32f, selected.Option.RequiredHeight)), false, null))
            { Apply(); Close(); }
            Text.WordWrap = false;
        }
    }
}
