using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using MintStatSort;
using Mono.Cecil;
using Mono.Cecil.Cil;

internal static class PerformanceChecks
{
    // Instrument the iterator, not the scheduler's internals. Cancellation must
    // release third-party enumerators and never resume their stale actions.
    private sealed class Sequence : IEnumerable<Action>, IEnumerator<Action>
    {
        private readonly Action[] actions;
        private int position = -1;
        internal int Moves, Disposals;
        internal Sequence(params Action[] actions) { this.actions = actions; }
        public Action Current => actions[position];
        object IEnumerator.Current => Current;
        public IEnumerator<Action> GetEnumerator() => this;
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public bool MoveNext()
        {
            if (Disposals != 0) throw new InvalidOperationException("Cancelled iterator resumed.");
            Moves++;
            return ++position < actions.Length;
        }
        public void Dispose() { Disposals++; }
        public void Reset() { throw new NotSupportedException(); }
    }

    internal static void Run(Action<bool, string> check)
    {
        using (var work = new BudgetedWork())
        {
            check(!work.Pending && work.Run(1000, 100) == 0, "An idle work queue performs no operations");
            var calls = new List<int>();
            var threads = new List<int>();
            int callingThread = Environment.CurrentManagedThreadId;
            var actions = Enumerable.Range(0, 9).Select(i => (Action)(() =>
            {
                calls.Add(i);
                threads.Add(Environment.CurrentManagedThreadId);
            })).ToArray();
            var sequence = new Sequence(actions);
            work.Replace(sequence);
            check(work.Pending && sequence.Moves == 0 && calls.Count == 0,
                "Scheduling performs no recipe/stat action before a UI work slice");
            check(work.Run(0, 3) == 0 && work.Run(-1, 3) == 0 && work.Run(1000, 0) == 0 && work.Run(1000, -1) == 0,
                "Zero or negative time/operation budgets perform no work");
            check(sequence.Moves == 0 && work.Pending, "An empty budget leaves the iterator untouched and pending");
            check(work.Run(1000, 3) == 3 && calls.SequenceEqual(new[] { 0, 1, 2 }) && sequence.Moves == 3,
                "A work slice honors its operation cap without prefetching the next action");
            check(work.Pending && sequence.Disposals == 0, "A capped slice preserves work for a later frame");
            check(work.Run(1000, 3) == 3 && calls.SequenceEqual(Enumerable.Range(0, 6)),
                "A later frame resumes at the next action");
            check(work.Run(1000, 100) == 3 && calls.SequenceEqual(Enumerable.Range(0, 9)),
                "Every queued action runs once and in recipe order across slices");
            check(threads.All(id => id == callingThread), "Recipe/stat work remains on the calling UI thread");
            check(!work.Pending && sequence.Disposals == 1 && sequence.Moves == 10,
                "Exhaustion closes the enumerator once and clears pending work");
            check(work.Run(1000, 3) == 0 && calls.Count == 9, "An exhausted queue never repeats an action");
            work.Dispose();
            check(sequence.Disposals == 1, "Repeated disposal does not double-dispose an exhausted iterator");
        }

        using (var work = new BudgetedWork())
        {
            int calls = 0;
            var many = new Sequence(Enumerable.Range(0, 100).Select(_ => (Action)(() => calls++)).ToArray());
            work.Replace(many);
            check(work.Run(1000) == 64 && calls == 64 && work.Pending,
                "The default frame cap prevents cheap cached operations from draining a large queue");
            check(work.Run(1000) == 36 && calls == 100 && !work.Pending,
                "The default cap preserves all remaining work across frames");
            var emptySteps = new Sequence(null, null, () => calls++);
            work.Replace(emptySteps);
            check(work.Run(1000, 2) == 2 && emptySteps.Moves == 2 && calls == 100 && work.Pending,
                "Null iterator steps still consume the operation budget without unbounded prefetching");
            check(work.Run(1000, 10) == 1 && calls == 101 && !work.Pending,
                "Null steps do not prevent later work or exhaustion");
        }

        using (var work = new BudgetedWork())
        {
            int oldCalls = 0, replacementCalls = 0;
            var previous = new Sequence(() => oldCalls++, () => oldCalls++);
            var replacement = new Sequence(() => replacementCalls++);
            work.Replace(previous);
            work.Replace(replacement);
            check(previous.Disposals == 1 && previous.Moves == 0, "Changing a category cancels unstarted work and releases its iterator");
            check(work.Run(1000, 10) == 1 && oldCalls == 0 && replacementCalls == 1,
                "A replacement queue executes only the current category's work");
            var interrupted = new Sequence(() => oldCalls++, () => oldCalls++);
            work.Replace(interrupted);
            work.Run(1000, 1);
            work.Replace(null);
            check(interrupted.Disposals == 1 && oldCalls == 1 && !work.Pending && work.Run(1000, 10) == 0,
                "Closing or resetting cancels remaining work after a partial slice");
            var empty = new Sequence();
            work.Replace(empty);
            check(work.Run(1000, 10) == 0 && !work.Pending && empty.Disposals == 1,
                "Empty recipe/stat workloads finish and dispose cleanly");
            var cancelled = new Sequence(() => oldCalls++);
            work.Replace(cancelled);
            work.Dispose();
            work.Dispose();
            check(cancelled.Disposals == 1 && cancelled.Moves == 0 && !work.Pending,
                "Window disposal cancels pending actions and is idempotent");
        }

        using (var work = new BudgetedWork())
        {
            int oldCalls = 0, newCalls = 0;
            var replacement = new Sequence(() => newCalls++);
            var previous = new Sequence(() => { oldCalls++; work.Replace(replacement); }, () => oldCalls++);
            work.Replace(previous);
            check(work.Run(1000, 1) == 1 && oldCalls == 1 && newCalls == 0 && previous.Disposals == 1 && work.Pending,
                "An action can replace its own work without executing a stale next action");
            check(work.Run(1000, 10) == 1 && oldCalls == 1 && newCalls == 1 && !work.Pending && replacement.Disposals == 1,
                "Reentrant replacement survives the old action and resumes normally");
            var sameSliceReplacement = new Sequence(() => newCalls++);
            var sameSlice = new Sequence(() => { oldCalls++; work.Replace(sameSliceReplacement); }, () => oldCalls++);
            work.Replace(sameSlice);
            check(work.Run(1000, 10) == 2 && oldCalls == 2 && newCalls == 2 && sameSlice.Moves == 1 && sameSlice.Disposals == 1,
                "A reentrant replacement can continue in the same budget without resuming the cancelled iterator");
            var selfCancel = new Sequence(() => { oldCalls++; work.Dispose(); }, () => oldCalls++);
            work.Replace(selfCancel);
            check(work.Run(1000, 10) == 1 && oldCalls == 3 && !work.Pending && selfCancel.Disposals == 1,
                "An action may close the window and cancel its remaining work safely");
        }

        using (var work = new BudgetedWork())
        {
            int slowCalls = 0;
            var slow = new Sequence(() => { slowCalls++; Thread.Sleep(150); }, () => slowCalls++);
            work.Replace(slow);
            // A stat worker is indivisible: allow the first operation to overrun
            // the 50ms budget, then ensure no second operation starts. No tight
            // upper wall-time assertion: a busy machine may deschedule this test.
            check(work.Run(50, 100) == 1 && slowCalls == 1 && slow.Moves == 1 && work.Pending,
                "Elapsed time stops a slice after an indivisible slow stat operation");
            check(work.Run(1000, 100) == 1 && slowCalls == 2 && !work.Pending,
                "A time-limited slice retains subsequent recipe work for another frame");
        }
    }

    internal static void Metadata(ModuleDefinition mod, Action<bool, string> check)
    {
        var dialog = mod.Types.Single(t => t.FullName == "MintStatSort.ComparisonWindow");
        var constructor = dialog.Methods.Single(m => m.IsConstructor && !m.IsStatic);
        var reachable = ImmediateMethods(mod, constructor).ToList();
        check(!reachable.SelectMany(m => Calls(m)).Any(c => c.DeclaringType.FullName == "MintStatSort.Metrics" && (c.Name == "Measure" || c.Name == "MeasureAtQuality")),
            "Opening the comparator cannot synchronously measure stats, including immediate refresh helpers and lambdas");
        check(!reachable.SelectMany(m => Calls(m)).Any(c => c.DeclaringType.FullName == "MintStatSort.BudgetedWork" && c.Name == "Run"),
            "The constructor schedules work without consuming a frame's measurement budget");
        foreach (string name in new[] { "DrawRecipes", "DrawMaterials", "DrawSidebar", "SelectQuality", "LoadProfile", "ToggleFavorite", "SetFavoritesOnly", "SetBenchContext", "DrawActiveFilters", "RemoveActiveFilter", "ClearActiveFilters", "OpenIngredients", "SelectOrder", "ToggleCategory", "DrawIngredientPreview" })
        {
            var draw = dialog.Methods.Single(m => m.Name == name);
            check(!ImmediateMethods(mod, draw).SelectMany(m => Calls(m)).Any(c => c.DeclaringType.FullName == "MintStatSort.Metrics" && (c.Name == "Measure" || c.Name == "MeasureAtQuality") ||
                c.DeclaringType.FullName == dialog.FullName && c.Name == "Measure"),
                name + " paints cached measurements without invoking a stat worker directly");
        }
        check(dialog.Fields.Any(f => f.FieldType.FullName == "MintStatSort.BudgetedWork"),
            "The comparator owns cancellable UI-thread work rather than creating background stat tasks");
        var ingredients = mod.Types.Single(t => t.Name == "IngredientDialog");
        var inspector = mod.Types.Single(t => t.Name == "IngredientInspector");
        foreach (var entry in ingredients.Methods.Where(m => m.Name == "DoWindowContents" || m.IsConstructor && !m.IsStatic))
            check(!ImmediateMethods(mod, entry).SelectMany(m => Calls(m)).Any(c => c.Name == "CountRequiredOfFor" ||
                c.Name == "ThingsOfDef" || c.Name == "IsInAnyStorage" || c.DeclaringType.FullName == "MintStatSort.BudgetedWork" && c.Name == "Run"),
                "Ingredient dialog construction/drawing schedules work without scanning stock or estimating requirements");
        check(inspector.Fields.Any(f => f.FieldType.FullName == "MintStatSort.BudgetedWork") &&
            Calls(ingredients.Methods.Single(m => m.Name == "PreClose")).Any(c => c.Name == "Dispose"),
            "Closing the ingredient dialog cancels its budgeted work");

        foreach (string name in new[] { "PinnedComparisonDialog", "ColumnsDialog", "DiagnosticDialog" })
        {
            var type = mod.Types.Single(t => t.Name == name);
            foreach (var entry in type.Methods.Where(m => m.Name == "DoWindowContents" || m.IsConstructor && !m.IsStatic))
                check(!ImmediateMethods(mod, entry).SelectMany(m => Calls(m)).Any(c =>
                    c.Name == "CountRequiredOfFor" || c.Name == "ThingsOfDef" || c.Name == "IsInAnyStorage" ||
                    c.DeclaringType.FullName == "MintStatSort.Metrics" && (c.Name == "Measure" || c.Name == "MeasureAtQuality")),
                    name + " opens and draws without stock scans or statistic workers");
        }
        var stock = mod.Types.Single(t => t.Name == "RecipeAvailability");
        check(!ImmediateMethods(mod, stock.Methods.Single(m => m.Name == "Configure")).SelectMany(m => Calls(m)).Any(c => c.Name == "CountRequiredOfFor" || c.Name == "ThingsOfDef"),
            "Enabling the stock filter only schedules a gradual scan");
        check(Calls(stock.Methods.Single(m => m.Name == "Update")).Any(c => c.DeclaringType.Name == "BudgetedWork" && c.Name == "Run") &&
            Calls(stock.Methods.Single(m => m.Name == "Dispose")).Any(c => c.Name == "Dispose"),
            "Stock filtering has an explicit update budget and cancellation");
        var session = mod.Types.Single(t => t.FullName == "MintStatSort.RecipeSession");
        var get = session.Methods.Single(m => m.Name == "Get");
        // Follow eager lambdas but exclude precisely the Action constructor
        // handed directly to Queue<Action>.Enqueue. That callback is the stat
        // worker dispatched later by PumpMeasurements, not by Get itself.
        var enqueued = get.Body.Instructions.Where(i => i.OpCode == OpCodes.Ldftn &&
            (i.Next?.Operand as MethodReference)?.DeclaringType.FullName == "System.Action" &&
            i.Next.OpCode == OpCodes.Newobj &&
            (i.Next.Next?.Operand as MethodReference)?.Name == "Enqueue" &&
            (i.Next.Next.Operand as MethodReference)?.DeclaringType.FullName == "System.Collections.Generic.Queue`1<System.Action>")
            .Select(i => (MethodReference)i.Operand).ToList();
        var deferred = new HashSet<string>(enqueued.Select(m => m.FullName));
        var immediateGet = ImmediateMethods(mod, get, deferred).ToList();
        check(!immediateGet.SelectMany(m => Calls(m)).Any(c => c.DeclaringType.FullName == "MintStatSort.Metrics" && (c.Name == "Measure" || c.Name == "MeasureAtQuality")),
            "RecipeSession.Get and its immediate helpers/lambdas return cached or pending readings without measuring stats");
        check(enqueued.Count != 0 && !immediateGet.SelectMany(m => Calls(m)).Any(c =>
            c.DeclaringType.FullName == "MintStatSort.BudgetedWork" && c.Name == "Run" ||
            c.DeclaringType.FullName == "System.Action" && c.Name == "Invoke"),
            "RecipeSession.Get queues its stat action without consuming or invoking it synchronously");
        var genericSort = session.Methods.Single(m => m.Name == "SortForDisplay" && m.HasGenericParameters);
        check(Calls(genericSort).Any(c => c.DeclaringType.FullName == session.FullName && c.Name == "PumpMeasurements"),
            "The optional recipe lists pump deferred measurements when sorting for display");
        var pump = session.Methods.Single(m => m.Name == "PumpMeasurements");
        check(Calls(pump).Any(c => c.DeclaringType.FullName == "MintStatSort.BudgetedWork" && c.Name == "Run") &&
            Calls(pump).Any(c => c.DeclaringType.FullName == "UnityEngine.Time" && c.Name == "get_frameCount") &&
            pump.Body.Instructions.Any(i => i.OpCode == OpCodes.Stsfld && (i.Operand as FieldReference)?.Name == "lastMeasurementFrame"),
            "Deferred optional-list measurements consume a budget with a per-frame guard");
        var import = session.Methods.Single(m => m.Name == "ImportMeasurements");
        var context = session.Methods.Single(m => m.Name == "EnsureContext");
        check(Calls(import).Any(c => c.Name == "EnsureContext") &&
            new[] { "materialKey", "qualityKey" }.All(name => context.Body.Instructions.Any(i => i.OpCode == OpCodes.Stsfld && (i.Operand as FieldReference)?.Name == name)),
            "Imported dialog readings update both material and quality context before reuse");
        check(Calls(session.Methods.Single(m => m.Name == "SetMap")).Any(c =>
            c.DeclaringType.FullName == session.FullName && c.Name == "Invalidate"),
            "Changing map invalidates pending and cached recipe measurements");

        foreach (string assembly in new[] { "ColonyWardrobe", "CreativeMode", "MaterialTracker", "RitualSoundscapes" })
            check(!mod.AssemblyReferences.Any(r => r.Name == assembly),
                "MintStatSort has no hard assembly dependency on " + assembly);
        string aboutPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(mod.FileName), "../About/About.xml"));
        var dependencies = XDocument.Load(aboutPath).Root.Element("modDependencies")?.Elements("li")
            .Select(li => li.Element("packageId")?.Value).ToArray() ?? Array.Empty<string>();
        check(!dependencies.Intersect(new[] { "aney123.colonywardrobe", "aney123.creativemode", "aney123.materialtracker", "ritualsoundscapes.music" }).Any(),
            "The four audited mods remain optional in MintStatSort metadata");
    }

    private static IEnumerable<MethodDefinition> ImmediateMethods(ModuleDefinition mod, MethodDefinition start, ISet<string> deferredDelegates = null)
    {
        var methods = AllTypes(mod.Types).SelectMany(t => t.Methods).ToDictionary(m => m.FullName);
        var queue = new Queue<MethodDefinition>();
        var seen = new HashSet<string>();
        queue.Enqueue(start);
        while (queue.Count != 0)
        {
            var method = queue.Dequeue();
            if (!seen.Add(method.FullName)) continue;
            yield return method;
            // Follow normal calls and captured lambdas. Iterator MoveNext is
            // deliberately absent: creating an iterator does not execute it.
            foreach (var instruction in method.HasBody ? method.Body.Instructions : Enumerable.Empty<Instruction>())
            {
                if (!(instruction.Operand is MethodReference call) || !(instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt ||
                    instruction.OpCode == OpCodes.Newobj || instruction.OpCode == OpCodes.Ldftn || instruction.OpCode == OpCodes.Ldvirtftn)) continue;
                string key = call is GenericInstanceMethod generic ? generic.ElementMethod.FullName : call.FullName;
                if ((instruction.OpCode == OpCodes.Ldftn || instruction.OpCode == OpCodes.Ldvirtftn) && deferredDelegates?.Contains(key) == true) continue;
                if (methods.TryGetValue(key, out var child)) queue.Enqueue(child);
            }
        }
    }
    private static IEnumerable<TypeDefinition> AllTypes(IEnumerable<TypeDefinition> types)
    {
        foreach (var type in types)
        {
            yield return type;
            foreach (var nested in AllTypes(type.NestedTypes)) yield return nested;
        }
    }
    private static IEnumerable<MethodReference> Calls(MethodDefinition method, bool delegates = false)
        => !method.HasBody ? Enumerable.Empty<MethodReference>() : method.Body.Instructions
            .Where(i => i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt || i.OpCode == OpCodes.Newobj ||
                delegates && (i.OpCode == OpCodes.Ldftn || i.OpCode == OpCodes.Ldvirtftn))
            .Select(i => i.Operand).OfType<MethodReference>();
}
