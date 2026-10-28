using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace MintStatSort
{
    // One recipe inspector at a time; publish complete passes atomically. Every
    // recipe is independent, since this is a filter, not a production queue.
    internal sealed class RecipeAvailability : IDisposable
    {
        private readonly Map map;
        private readonly Dictionary<IngredientCount, List<ThingDef>> choices;
        private readonly BudgetedWork work = new BudgetedWork();
        private HashSet<RecipeDef> candidates = new HashSet<RecipeDef>();
        private Dictionary<RecipeDef, bool?> results = new Dictionary<RecipeDef, bool?>();
        private ThingDef material;
        private int executions = 1;
        private bool enabled, disposed, mix;
        private float nextRefresh;
        private IngredientInspector current;
        internal bool Ready { get; private set; }
        internal bool Pending => work.Pending;
        internal int Revision { get; private set; }
        internal int Completed { get; private set; }
        internal int Total => candidates.Count;
        internal RecipeAvailability(Map map, Dictionary<IngredientCount, List<ThingDef>> choices)
        { this.map = map; this.choices = choices; }
        internal void Configure(IEnumerable<RecipeDef> recipes, ThingDef material, int executions, bool enabled, bool mix = false)
        {
            if (disposed) return;
            var next = new HashSet<RecipeDef>(recipes.Where(r => r != null)); executions = BatchCount.Clamp(executions);
            if (this.mix == mix && this.enabled == enabled && this.material == material && this.executions == executions && candidates.SetEquals(next)) return;
            work.Dispose(); current?.Dispose(); current = null;
            this.mix = mix; this.enabled = enabled; this.material = material; this.executions = executions; candidates = next;
            Ready = false; results.Clear(); Completed = 0; Revision++;
            if (enabled) work.Replace(Scan());
        }
        internal bool? Result(RecipeDef recipe) => recipe != null && results.TryGetValue(recipe, out var result) ? result : null;
        internal void Update()
        {
            if (disposed || !enabled) return;
            if (!Pending && Time.realtimeSinceStartup >= nextRefresh) work.Replace(Scan());
            work.Run(1.7, 1);
        }
        private IEnumerable<Action> Scan()
        {
            var next = new Dictionary<RecipeDef, bool?>(); Completed = 0;
            try
            {
                foreach (var recipe in candidates)
                {
                    current = new IngredientInspector(recipe, map, material, choices); current.SetExecutions(executions); current.SetMixing(mix);
                    while (current.Pending) yield return () => current.Update();
                    bool? sufficient = current.Rows.All(r => r.Missing.HasValue) ? (bool?)current.Rows.All(r => r.Missing == 0) : null;
                    next[recipe] = sufficient; current.Dispose(); current = null; Completed++;
                    yield return () => { };
                }
                yield return () => { results = next; Ready = true; Revision++; nextRefresh = Time.realtimeSinceStartup + 3f; };
            }
            finally { current?.Dispose(); current = null; }
        }
        public void Dispose() { disposed = true; work.Dispose(); current?.Dispose(); current = null; }
    }
}
