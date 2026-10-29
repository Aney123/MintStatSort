using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RimWorld;
using Verse;

namespace MintStatSort
{
    internal sealed class IngredientDetail
    {
        internal IngredientCount Ingredient;
        internal string Label;
        internal List<ThingDef> Choices;
        internal ThingDef Reference;
        internal int? Required;
        internal long? Available, Missing;
    }
    internal sealed class IngredientInspector : IDisposable
    {
        internal readonly RecipeDef Recipe;
        internal readonly Map Map;
        internal List<IngredientDetail> Rows = new List<IngredientDetail>();
        internal bool Ready;
        internal bool MixSubstitutes { get; private set; }
        internal int Executions { get; private set; } = 1;
        internal bool Pending => work.Pending;
        private readonly BudgetedWork work = new BudgetedWork();
        private readonly ThingDef preferred;
        private readonly Dictionary<IngredientCount, List<ThingDef>> choiceCache;
        private readonly List<IngredientCount> fixedIngredients = new List<IngredientCount>();
        private readonly HashSet<ThingDef> fixedDefinitions = new HashSet<ThingDef>();
        private readonly Dictionary<IngredientCount, Dictionary<ThingDef, int?>> requirementCache = new Dictionary<IngredientCount, Dictionary<ThingDef, int?>>();
        private float nextRefresh;
        private bool disposed;
        internal IngredientInspector(RecipeDef recipe, Map map, ThingDef preferred,
            Dictionary<IngredientCount, List<ThingDef>> choiceCache)
        {
            Recipe = recipe; Map = map; this.preferred = preferred; this.choiceCache = choiceCache;
            work.Replace(Discover());
        }
        internal void Update()
        {
            if (disposed) return;
            if (Ready && !Pending && Time.realtimeSinceStartup >= nextRefresh) Refresh();
            work.Run(1.5, 64);
        }
        internal void SetExecutions(int value)
        {
            value = BatchCount.Clamp(value); if (disposed || value == Executions) return;
            Executions = value;
            foreach (var row in Rows) { row.Required = null; row.Available = null; row.Missing = null; }
            if (Ready) Refresh();
        }
        internal void SetMixing(bool value)
        {
            if (disposed || MixSubstitutes == value) return;
            MixSubstitutes = value; foreach (var row in Rows) { row.Available = null; row.Missing = null; }
            if (Ready) Refresh();
        }
        internal void Refresh()
        {
            if (disposed || !Ready) return;
            // A mod may adjust recipe quantities while this view is open. Cache
            // within one pass, but let refresh see the current game calculation.
            requirementCache.Clear();
            work.Replace(MixSubstitutes ? CountSubstitutes() : Count()); nextRefresh = float.PositiveInfinity;
        }
        internal void Choose(int index, ThingDef reference)
        {
            if (disposed || !Ready || index < 0 || index >= Rows.Count || reference == null || !Rows[index].Choices.Contains(reference)) return;
            Rows[index].Reference = reference; Rows[index].Required = null;
            foreach (var row in Rows) { row.Available = null; row.Missing = null; }
            Refresh();
        }
        private IEnumerable<Action> Discover()
        {
            var next = new List<IngredientDetail>();
            // Bill.IsFixedOrAllowedIngredient accepts fixed requirements before
            // consulting the recipe-wide filter. Some crafting recipes have an
            // empty recipe-wide filter and entirely fixed ingredients.
            foreach (var ingredient in Recipe.ingredients ?? new List<IngredientCount>())
            {
                var captured = ingredient;
                yield return () =>
                {
                    if (captured?.filter != null && captured.IsFixedIngredient)
                    { fixedIngredients.Add(captured); if (captured.FixedIngredient != null) fixedDefinitions.Add(captured.FixedIngredient); }
                };
            }
            foreach (var ingredient in Recipe.ingredients ?? new List<IngredientCount>())
            {
                if (ingredient?.filter == null) continue;
                List<ThingDef> choices;
                if (!choiceCache.TryGetValue(ingredient, out choices))
                {
                    choices = new List<ThingDef>();
                    foreach (var definition in ingredient.filter.AllowedThingDefs)
                    {
                        var candidate = definition;
                        yield return () =>
                        {
                            if (candidate != null && (fixedDefinitions.Contains(candidate) || Recipe.fixedIngredientFilter == null || Recipe.fixedIngredientFilter.Allows(candidate))) choices.Add(candidate);
                        };
                    }
                    yield return () =>
                    {
                        choices.Sort((a, b) => string.Compare(a.LabelCap.ToString(), b.LabelCap.ToString(), StringComparison.CurrentCultureIgnoreCase));
                        choiceCache[ingredient] = choices;
                    };
                }
                yield return () =>
                {
                    var defaultStuff = Recipe.ProducedThingDef?.MadeFromStuff == true ? GenStuff.DefaultStuffFor(Recipe.ProducedThingDef) : null;
                    var reference = preferred != null && choices.Contains(preferred) ? preferred :
                        defaultStuff != null && choices.Contains(defaultStuff) ? defaultStuff : choices.FirstOrDefault();
                    next.Add(new IngredientDetail { Ingredient = ingredient, Choices = choices,
                        Reference = reference, Label = ingredient.filter.Summary });
                };
            }
            yield return () => { Rows = next; Ready = true; Refresh(); };
        }
        private int? Required(IngredientDetail row)
        {
            if (row.Reference == null || Recipe.ignoreIngredientCountTakeEntireStacks) return null;
            if (!requirementCache.TryGetValue(row.Ingredient, out var values))
                requirementCache[row.Ingredient] = values = new Dictionary<ThingDef, int?>();
            if (!values.TryGetValue(row.Reference, out var required))
            {
                try
                {
                    int count = row.Ingredient.CountRequiredOfFor(row.Reference, Recipe, null);
                    required = count >= 0 ? (int?)count : null;
                }
                catch (Exception) { required = null; }
                values[row.Reference] = required;
            }
            return BatchCount.Required(required, Executions);
        }
        private IEnumerable<Action> Count()
        {
            var next = new List<IngredientDetail>();
            var reservations = new Dictionary<Thing, long>();
            foreach (var original in Rows)
            {
                var row = new IngredientDetail { Ingredient = original.Ingredient, Label = original.Label,
                    Choices = original.Choices, Reference = original.Reference };
                var stacks = new Thing[0];
                bool validCount = true;
                IngredientAllocation<Thing> allocation = null;
                yield return () =>
                {
                    row.Required = Required(row);
                    allocation = new IngredientAllocation<Thing>(row.Required.GetValueOrDefault());
                    if (Map != null && row.Reference != null) stacks = Map.listerThings.ThingsOfDef(row.Reference).ToArray();
                };
                // Snapshot only this chosen definition. Storage/filter checks are
                // sliced per stack; no map-wide search or pawn pathfinding.
                foreach (var stack in stacks)
                {
                    var captured = stack;
                    yield return () =>
                    {
                        try
                        {
                            if (captured.Spawned && captured.Map == Map && captured.IsInAnyStorage() &&
                                row.Ingredient.filter.Allows(captured) && (fixedIngredients.Any(i => i.filter.Allows(captured)) || Recipe.fixedIngredientFilter == null || Recipe.fixedIngredientFilter.Allows(captured)))
                                allocation.Add(captured, captured.stackCount, reservations);
                        }
                        catch (Exception) { validCount = false; }
                    };
                }
                yield return () =>
                {
                    if (Map != null && row.Reference != null && validCount)
                    { row.Available = allocation.Available; if (row.Required.HasValue) row.Missing = allocation.Missing; }
                    next.Add(row);
                };
            }
            yield return () => { Rows = next; nextRefresh = Time.realtimeSinceStartup + 1.5f; };
        }
        private IEnumerable<Action> CountSubstitutes()
        {
            var next = new List<IngredientDetail>();
            var reservations = new Dictionary<Thing, long>();
            // Evaluate the most restricted requirements first. This remains a
            // conservative estimate, not an exhaustive manufacturing solver.
            foreach (var original in Rows.OrderBy(r => r.Choices.Count))
            {
                var row = new IngredientDetail { Ingredient = original.Ingredient, Label = original.Label,
                    Choices = original.Choices, Reference = original.Reference };
                var quantities = new Dictionary<ThingDef, int>();
                var stacks = new List<Thing>();
                bool valid = true;
                yield return () => row.Required = Required(row);
                if (Map != null && row.Required.HasValue)
                {
                    bool stuffProduct = Recipe.ProducedThingDef?.MadeFromStuff == true;
                    foreach (var definition in row.Choices)
                    {
                        if (stuffProduct && definition.stuffProps != null && definition != row.Reference) continue;
                        var def = definition;
                        Thing[] snapshot = null;
                        yield return () =>
                        {
                            try
                            {
                                int count = row.Ingredient.CountRequiredOfFor(def, Recipe, null);
                                var total = count >= 0 ? BatchCount.Required(count, Executions) : null;
                                if (total.HasValue && total > 0) quantities[def] = total.Value;
                                else if (row.Required > 0) valid = false;
                                snapshot = Map.listerThings.ThingsOfDef(def).ToArray();
                            }
                            catch (Exception) { valid = false; snapshot = new Thing[0]; }
                        };
                        foreach (var item in snapshot)
                        {
                            var stack = item;
                            yield return () =>
                            {
                                try
                                {
                                    if (stack.Spawned && stack.Map == Map && stack.IsInAnyStorage() &&
                                        row.Ingredient.filter.Allows(stack) &&
                                        (fixedIngredients.Any(i => i.filter.Allows(stack)) || Recipe.fixedIngredientFilter == null || Recipe.fixedIngredientFilter.Allows(stack)))
                                        stacks.Add(stack);
                                }
                                catch (Exception) { valid = false; }
                            };
                        }
                    }
                    ThingDef single = null;
                    if (!Recipe.allowMixingIngredients)
                    {
                        // A recipe that forbids mixing can still use a different
                        // single substitute. Never sum two kinds for it.
                        double best = -1;
                        foreach (var pair in quantities)
                        {
                            double free = 0;
                            foreach (var item in stacks.Where(s => s.def == pair.Key))
                            {
                                var stack = item;
                                yield return () => { reservations.TryGetValue(stack, out long used); free += Math.Max(0, stack.stackCount - used); };
                            }
                            yield return () => { if (free / pair.Value > best) { best = free / pair.Value; single = pair.Key; } };
                        }
                    }
                    var allocation = new SubstituteAllocation<Thing>();
                    foreach (var item in stacks)
                    {
                        var stack = item;
                        yield return () =>
                        {
                            if ((Recipe.allowMixingIngredients || stack.def == single) && quantities.TryGetValue(stack.def, out int required))
                                allocation.Add(stack, stack.stackCount, required, reservations);
                        };
                    }
                    yield return () =>
                    {
                        if (valid)
                        {
                            row.Available = (long)Math.Min(long.MaxValue, Math.Floor(allocation.Available * row.Required.Value + 1e-9));
                            row.Missing = row.Required == 0 || allocation.Complete ? 0 : (long)Math.Ceiling(allocation.Remaining * row.Required.Value - 1e-9);
                        }
                    };
                }
                yield return () => next.Add(row);
            }
            yield return () => { Rows = next.OrderBy(r => Recipe.ingredients.IndexOf(r.Ingredient)).ToList(); nextRefresh = Time.realtimeSinceStartup + 1.5f; };
        }
        public void Dispose() { disposed = true; work.Dispose(); }
    }
}
