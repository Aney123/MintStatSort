using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using MintStatSort;
using Mono.Cecil;
using Mono.Cecil.Cil;

internal static class NativeMenuChecks
{
    private sealed class Recipe
    {
        internal string Name;
        internal Recipe(string name) { Name = name; }
    }
    private class Closure
    {
        internal Recipe Recipe;
        internal Recipe Other;
        internal object Child;
        internal object Child2;
        internal static Recipe StaticRecipe;
        internal int ActionCalls;
        internal int PropertyCalls;
        internal Recipe DangerousProperty
        {
            get { PropertyCalls++; throw new Exception("A property must never run during inspection."); }
        }
        internal void Action() { ActionCalls++; throw new Exception("A menu action must never run during inspection."); }
        internal Delegate Callback => new Action(Action);
    }
    private sealed class EqualClosure : Closure
    {
        public override bool Equals(object other) { return other is EqualClosure; }
        public override int GetHashCode() { return 1; }
    }
    private sealed class ForeignClosure
    {
        internal Recipe Recipe;
        internal void Action() { throw new Exception("Foreign menu action must never run."); }
    }
    private sealed class Option
    {
        internal string Id;
        internal Recipe Recipe;
        internal int Priority;
        internal int Order;
        internal string Style;
        internal Action Action;
    }

    internal static void Run(Action<bool, string> check)
    {
        var first = new Recipe("Sword");
        var second = new Recipe("Armor");
        var foreign = new Recipe("Not on this table");
        var allowed = new[] { first, second };
        Func<Type, bool> native = type => typeof(Closure).IsAssignableFrom(type);
        Func<IEnumerable<Delegate>, Recipe> resolve = actions => NativeMenuCore.ResolveRecipe(actions, allowed, native);
        var direct = new Closure { Recipe = first };
        check(ReferenceEquals(resolve(new[] { direct.Callback }), first), "A native captured recipe resolves without running its action");
        check(direct.ActionCalls == 0 && direct.PropertyCalls == 0, "Inspect only fields, never action delegates or properties");
        check(resolve(null) == null, "Absent callbacks remain opaque");
        check(resolve(new Delegate[] { null }) == null, "Null callbacks remain opaque");
        check(NativeMenuCore.ResolveRecipe(new[] { direct.Callback }, (Recipe[])null, native) == null, "Absent table recipes cannot match captured data");
        check(NativeMenuCore.ResolveRecipe(new[] { direct.Callback }, Array.Empty<Recipe>(), native) == null, "An empty table cannot resolve a foreign menu recipe");
        var child = new Closure { Recipe = first };
        var outer = new Closure { Child = child };
        check(ReferenceEquals(resolve(new[] { outer.Callback }), first), "Nested native display-class fields resolve the recipe");
        check(outer.ActionCalls == 0 && child.ActionCalls == 0, "Nested inspection never executes callbacks");
        var ambiguous = new Closure { Recipe = first, Other = second };
        check(resolve(new[] { ambiguous.Callback }) == null, "Two table recipes in one callback are ambiguous");
        var repeated = new Closure { Recipe = first, Other = first, Child = child };
        check(ReferenceEquals(resolve(new[] { repeated.Callback }), first), "Repeated references to the same recipe are unambiguous");
        var outsiders = new Closure { Recipe = first, Other = foreign };
        check(ReferenceEquals(resolve(new[] { outsiders.Callback }), first), "Captured recipes absent from this table cannot influence matching");
        var foreignTarget = new ForeignClosure { Recipe = first };
        check(resolve(new[] { new Action(foreignTarget.Action) }) == null, "Foreign display classes are not traversed");
        var foreignChild = new Closure { Child = foreignTarget };
        check(resolve(new[] { foreignChild.Callback }) == null, "Foreign nested objects remain opaque even when they contain recipes");
        Closure.StaticRecipe = first;
        var empty = new Closure();
        check(resolve(new[] { empty.Callback }) == null, "Static fields cannot identify a menu option");
        Closure.StaticRecipe = null;
        var cycle = new Closure { Recipe = first };
        cycle.Child = cycle;
        check(ReferenceEquals(resolve(new[] { cycle.Callback }), first), "A closure cycle terminates while retaining its unique recipe");
        var cycleOther = new Closure { Recipe = second, Child = cycle };
        cycle.Child2 = cycleOther;
        check(resolve(new[] { cycle.Callback }) == null, "A cycle containing two recipes is ambiguous");
        var equalityTrap = new EqualClosure { Recipe = first, Child = new EqualClosure { Recipe = second } };
        check(resolve(new[] { equalityTrap.Callback }) == null, "Visited closures use reference identity, not overridden equality");
        var secondCallback = new Closure { Recipe = second };
        var multicast = Delegate.Combine(direct.Callback, secondCallback.Callback);
        check(resolve(new[] { multicast }) == null, "Every invocation in a multicast delegate participates in ambiguity detection");
        var sameMulticast = Delegate.Combine(direct.Callback, child.Callback);
        check(ReferenceEquals(resolve(new[] { sameMulticast }), first), "Multicast callbacks referring to one recipe remain resolvable");
        check(resolve(new[] { direct.Callback, secondCallback.Callback }) == null, "Action and extra GUI callbacks cannot disagree about a recipe");

        var depthSix = Chain(6, first);
        check(ReferenceEquals(resolve(new[] { depthSix.Callback }), first), "The supported closure depth resolves its leaf");
        var tooDeep = Chain(7, first);
        check(resolve(new[] { tooDeep.Callback }) == null, "Data beyond the traversal depth remains opaque");
        var clippedAmbiguity = Chain(7, second);
        clippedAmbiguity.Recipe = first;
        check(resolve(new[] { clippedAmbiguity.Callback }) == null, "A shallow match cannot hide an uninspected deep recipe");
        var boundedCallbacks = Enumerable.Range(0, 32).Select(_ => new Closure { Recipe = first }).ToList();
        check(ReferenceEquals(resolve(boundedCallbacks.Select(c => c.Callback)), first), "The supported object count accepts a consistent match");
        boundedCallbacks.Add(new Closure { Recipe = second });
        check(resolve(boundedCallbacks.Select(c => c.Callback)) == null, "An exhausted object budget cannot produce a false unique match");
        check(boundedCallbacks.All(c => c.ActionCalls == 0 && c.PropertyCalls == 0), "Budget checks also avoid executing callbacks or getters");

        var opaqueFirst = new Option { Id = "warning", Priority = 2 };
        var baseStyle = new Option { Id = "base", Recipe = first, Priority = 0, Style = "base", Action = () => throw new Exception("Do not execute.") };
        var submenu = new Option { Id = "style-menu", Priority = 0 };
        var otherStyle = new Option { Id = "style", Recipe = first, Priority = 0, Style = "ideology" };
        var armor = new Option { Id = "armor", Recipe = second, Priority = 0 };
        var opaqueLast = new Option { Id = "tail", Priority = -1 };
        var options = new[] { opaqueFirst, baseStyle, submenu, otherStyle, armor, opaqueLast };
        var snapshot = options.ToArray();
        Func<Option, bool> known = option => option.Recipe != null;
        var merged = NativeMenuCore.MergeKnown(options, known, new[] { armor, baseStyle, otherStyle });
        check(Ids(merged) == "warning,armor,style-menu,base,style,tail", "Sorting identified recipes keeps opaque options in their original relative slots");
        check(merged[0] == opaqueFirst && merged[2] == submenu && merged[5] == opaqueLast, "Opaque warnings and submenu identities are preserved");
        check(merged.Contains(baseStyle) && merged.Contains(otherStyle), "Distinct style options for one recipe are preserved");
        check(merged.Single(o => o.Id == "base").Action == baseStyle.Action, "Original option action identity is retained");
        check(options.SequenceEqual(snapshot), "Merging does not mutate the native option source");
        var filtered = NativeMenuCore.MergeKnown(options, known, new[] { armor });
        check(Ids(filtered) == "warning,armor,style-menu,tail", "Category filtering removes only identified recipes");
        check(Ids(NativeMenuCore.MergeKnown(options, known, Array.Empty<Option>())) == "warning,style-menu,tail", "An empty category preserves native opaque options");
        check(NativeMenuCore.MergeKnown(Array.Empty<Option>(), known, Array.Empty<Option>()).Count == 0, "Empty native option menus remain empty");
        check(NativeMenuCore.MergeKnown(new[] { opaqueFirst, submenu }, known, Array.Empty<Option>()).SequenceEqual(new[] { opaqueFirst, submenu }), "All-opaque menus retain every original option");
        check(Ids(NativeMenuCore.MergeKnown(options, known, options.Where(known))) == Ids(options), "Original ordering restores every recipe and opaque option");
        var ranked = new[]
        {
            new Option { Id = "tie-a", Priority = 1, Order = 2 },
            new Option { Id = "low", Priority = -1, Order = 99 },
            new Option { Id = "high-order", Priority = 1, Order = 3 },
            new Option { Id = "top", Priority = 2, Order = -100 },
            new Option { Id = "tie-b", Priority = 1, Order = 2 }
        };
        var ordered = NativeMenuCore.OriginalOrder(ranked, o => o.Priority, o => o.Order);
        check(Ids(ordered) == "top,high-order,tie-a,tie-b,low", "Native priority and orderInPriority sort descending, with stable ties");
        check(Ids(ranked) == "tie-a,low,high-order,top,tie-b", "Native priority ordering does not mutate its source");
        check(NativeMenuCore.OriginalOrder(Array.Empty<Option>(), o => o.Priority, o => o.Order).Count == 0, "Original order supports empty menus");
    }

    private static Closure Chain(int depth, Recipe recipe)
    {
        var result = new Closure { Recipe = recipe };
        for (int index = 0; index < depth; index++) result = new Closure { Child = result };
        return result;
    }
    private static string Ids(IEnumerable<Option> options) => string.Join(",", options.Select(o => o.Id));

    internal static void Metadata(string rimWorldPath, string modPath, Action<bool, string> check)
    {
        using var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(rimWorldPath)));
        resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(modPath)));
        var read = new ReaderParameters { AssemblyResolver = resolver };
        using var game = ModuleDefinition.ReadModule(rimWorldPath, read);
        using var mod = ModuleDefinition.ReadModule(modPath, read);
        var billStack = game.Types.Single(t => t.FullName == "RimWorld.BillStack");
        var listing = billStack.Methods.Single(m => m.Name == "DoListing");
        check(!listing.IsStatic && listing.ReturnType.FullName == "RimWorld.Bill" && listing.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(new[]
            { "UnityEngine.Rect", "System.Func`1<System.Collections.Generic.List`1<Verse.FloatMenuOption>>", "UnityEngine.Vector2&", "System.Single&" }), "RimWorld 1.6 native bill listing signature matches the patch target");
        var nativeOption = game.Types.Single(t => t.FullName == "Verse.FloatMenuOption");
        var draw = nativeOption.Methods.Single(m => m.Name == "DoGUI");
        check(!draw.IsStatic && draw.ReturnType.FullName == "System.Boolean" && draw.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(new[]
            { "UnityEngine.Rect", "System.Boolean", "Verse.FloatMenu" }), "Native option GUI supports preserved selection, disabled states and extra actions");
        check(nativeOption.Fields.Any(f => f.Name == "action" && f.FieldType.FullName == "System.Action") &&
            nativeOption.Fields.Any(f => f.Name == "extraPartOnGUI" && f.FieldType.FullName == "System.Func`2<UnityEngine.Rect,System.Boolean>") &&
            nativeOption.Fields.Any(f => f.Name == "mouseoverGuiAction" && f.FieldType.FullName == "System.Action`1<UnityEngine.Rect>"), "Recipe metadata can be read from the native option's existing callbacks");
        check(!mod.AssemblyReferences.Any(a => a.Name.StartsWith("DubsMintMenus", StringComparison.OrdinalIgnoreCase) || a.Name.StartsWith("NiceBillTab", StringComparison.OrdinalIgnoreCase)), "The compiled mod has no mandatory Dubs or Nice assembly references");
        var types = AllTypes(mod.Types).ToList();
        var integration = types.Single(t => t.FullName == "MintStatSort.VanillaIntegration");
        var install = integration.Methods.Single(m => m.Name == "Install");
        check(install.HasBody && install.Body.Instructions.Any(i => i.OpCode == OpCodes.Ldstr && Equals(i.Operand, "DoListing")) &&
            install.Body.Instructions.Any(i => i.OpCode == OpCodes.Ldtoken && (i.Operand as TypeReference)?.FullName == billStack.FullName), "Vanilla installation targets native BillStack.DoListing");
        var create = integration.Methods.Single(m => m.Name == "CreateMenu");
        check(create.IsStatic && create.ReturnType.FullName == "Verse.Window", "The native menu replacement is a Window compatible with WindowStack.Add");
        check(Calls(create).Any(m => m.Name == ".ctor" && m.DeclaringType.FullName == "Verse.FloatMenu"), "CreateMenu retains the native FloatMenu fallback");
        var window = types.Single(t => t.FullName == "MintStatSort.NativeRecipeWindow");
        var comparison = types.Single(t => t.FullName == "MintStatSort.ComparisonWindow");
        check(window.BaseType.FullName == comparison.FullName && comparison.BaseType.FullName == "Verse.Window",
            "The native selector uses the same game window as Nice and Dubs");
        var valid = window.Methods.Single(m => m.Name == "ValidTable");
        check(valid.ReturnType.FullName == "System.Boolean" && Calls(valid).Any(m => m.DeclaringType.Name == "Selector" && m.Name == "get_SingleSelectedThing") &&
            Calls(valid).Any(m => m.Name == "get_Spawned") && Calls(valid).Any(m => m.Name == "get_Destroyed"), "The selector validates the current table selection and existence before interacting");
        var contents = comparison.Methods.Single(m => m.Name == "DrawNativeAction");
        var guiCall = contents.Body.Instructions.Single(i => (i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt) &&
            (i.Operand as MethodReference)?.FullName == draw.FullName);
        check(guiCall.Previous?.OpCode == OpCodes.Ldnull && guiCall.Previous.Previous?.OpCode == OpCodes.Ldc_I4_0,
            "Recipe rows delegate to original FloatMenuOption.DoGUI with colonistOrdering=false and no FloatMenu");
        check(Calls(contents).Any(m => m.Name == "ContextValid") && Calls(contents).Any(m => m.Name == "RecipeAvailable"),
            "Native actions recheck table validity and recipe availability immediately before using the original option");
        check(Calls(window.Methods.Single(m => m.Name == "ContextValid")).Any(m => m.Name == "ValidTable"),
            "Native context guard validates the table");
        ComparisonChecks.Metadata(mod, check);
        var session = types.Single(t => t.FullName == "MintStatSort.RecipeSession");
        var sort = session.Methods.Single(m => m.Name == "SortForDisplay" && m.HasGenericParameters);
        check(sort.Body.Instructions.Any(i => i.OpCode == OpCodes.Ldsfld && (i.Operand as FieldReference)?.FullName == "System.Boolean MintStatSort.VanillaIntegration::Ready"), "Native readiness enables the shared sorter without Nice or Dubs");
        var settings = types.Single(t => t.FullName == "MintStatSort.MintStatSortMod");
        check(AllTypes(new[] { settings }).SelectMany(t => t.Methods).Any(m => Calls(m).Any(c => c.Name == "Install" && c.DeclaringType.FullName == integration.FullName)), "Mod startup connects the native integration");
        var connectNative = AllTypes(new[] { settings }).SelectMany(t => t.Methods).Single(m => m.HasBody &&
            m.Body.Instructions.Any(i => i.OpCode == OpCodes.Ldstr && Equals(i.Operand, "MSS.NativeMenu")));
        int firstConnection = connectNative.Body.Instructions.ToList().FindIndex(i => (i.Operand as MethodReference)?.Name == "Connect");
        int optionalDiscovery = connectNative.Body.Instructions.ToList().FindIndex(i => (i.Operand as MethodReference)?.Name == "TypeByName");
        check(firstConnection >= 0 && optionalDiscovery > firstConnection, "Native startup connects before discovery of optional menu mods");
        var core = types.Single(t => t.FullName == "MintStatSort.NativeMenuCore");
        check(AllTypes(new[] { core }).SelectMany(t => t.Methods).All(m => !Calls(m).Any(c => c.Name == "DynamicInvoke" ||
            (c.Name == "Invoke" && c.DeclaringType.FullName == "System.Action"))), "Compiled recipe discovery never invokes menu actions");
        var root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(modPath), ".."));
        var dependencies = XDocument.Load(Path.Combine(root, "About/About.xml")).Root.Element("modDependencies")?.Elements("li").Select(li => li.Element("packageId")?.Value).ToArray() ?? Array.Empty<string>();
        check(dependencies.SequenceEqual(new[] { "brrainz.harmony" }), "Mod metadata requires Harmony only");
        Console.WriteLine("Native menu API and compiled mod checked: " + game.Assembly.Name.Version + " / " + mod.Assembly.Name.Version);
    }

    private static IEnumerable<MethodReference> Calls(MethodDefinition method)
    {
        if (!method.HasBody) return Enumerable.Empty<MethodReference>();
        return method.Body.Instructions.Where(i => i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt || i.OpCode == OpCodes.Newobj)
            .Select(i => i.Operand).OfType<MethodReference>();
    }
    private static IEnumerable<TypeDefinition> AllTypes(IEnumerable<TypeDefinition> types)
    {
        foreach (var type in types)
        {
            yield return type;
            foreach (var nested in AllTypes(type.NestedTypes)) yield return nested;
        }
    }
}
