using System;
using System.Collections.Generic;
using System.Linq;
using MintStatSort;
using Mono.Cecil;
using Mono.Cecil.Cil;

internal static class ComparisonChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var union = CategorySelection.Toggle("kind:armor", "kind:clothing");
        check(CategorySelection.Unpack(union).SequenceEqual(new[] { "kind:armor", "kind:clothing" }), "Two categories coexist in a stable serialized union");
        check(CategorySelection.Toggle(union, "kind:armor") == "kind:clothing", "Toggling one category preserves the other");
        check(CategorySelection.Remove(union, "kind:clothing") == "kind:armor", "Removing a category chip removes only that category");
        check(CategorySelection.Toggle(union, "all") == "all" && CategorySelection.Toggle("kind:armor", "kind:armor") == "all", "All or removing the last category removes only the category restriction");
        var unusual = new[] { "defs:A|B", "label:Uno, dos: 50%", "label:Armaduras ñ" };
        check(CategorySelection.Unpack(CategorySelection.Pack(unusual)).SequenceEqual(unusual.OrderBy(x => x, StringComparer.Ordinal)), "Escaped multi-selection preserves merged category IDs and translated punctuation");
        check(CategorySelection.Pack(new[] { "all", "kind:armor", "kind:armor" }) == "kind:armor", "Repeated category IDs are canonicalized without duplicate recipes");
        check(CategorySelection.Available(union, new[] { "all", "kind:clothing" }) == "kind:clothing", "Unavailable categories are removed without discarding valid selections");
        check(CategorySelection.Available(union, Array.Empty<string>()) == "all", "A workbench with no matching categories falls back to All");
        // Table geometry must keep every selected statistic accessible at small
        // resolutions, including lists wider than the dialog's viewport.
        var narrow = new ComparisonLayout(420f, 5);
        check(narrow.NameWidth >= 220f && narrow.ContentWidth > 420f,
            "At narrow widths five selected stats remain accessible by horizontal scrolling");
        var wide = new ComparisonLayout(1000f, 1);
        check(wide.ContentWidth <= 1000f && wide.NameWidth > narrow.NameWidth,
            "A wide table with one stat gives spare space to names without forcing horizontal scrolling");
        var many = new ComparisonLayout(680f, 12);
        check(many.ContentWidth >= 220f + 12 * ComparisonLayout.StatWidth + ComparisonLayout.TotalWidth,
            "All twelve selected columns and the score remain in the scrollable content");
        check(NumericFilterCore.Matches(5, 5, 5), "Numeric limits are inclusive");
        check(NumericFilterCore.Matches((double)0.6f, null, 0.6), "Float worker rounding does not reject the displayed percentage boundary");
        check(!NumericFilterCore.Matches(null, 0, null) && !NumericFilterCore.Matches(double.NaN, null, 10), "Missing or invalid readings cannot satisfy an active limit");
        check(NumericFilterCore.Matches(null, null, null) && NumericFilterCore.Matches(0, null, 0), "An absent limit and a real zero are distinct");
        check(!NumericFilterCore.Matches(4.9, 5, null) && !NumericFilterCore.Matches(5.1, null, 5), "One-sided numeric limits exclude out-of-range values");
        double? parsed;
        check(NumericFilterCore.TryLimit(" 1,5 ", 1, out parsed) && parsed == 1.5, "Comma decimal inputs do not become grouped integers");
        check(NumericFilterCore.TryLimit("60", 100, out parsed) && parsed == 0.6, "Percentage input is converted to the underlying stat value");
        check(NumericFilterCore.TryLimit("", 1, out parsed) && parsed == null, "Blank limit means unbounded");
        check(!NumericFilterCore.TryLimit("NaN", 1, out parsed) && !NumericFilterCore.TryLimit("1e999", 1, out parsed) && !NumericFilterCore.TryLimit("1,234.5", 1, out parsed), "Nonfinite values and ambiguous grouped numbers are rejected");
        var values = new[] { new double?[] { 0, 0 }, new double?[] { 10, 10 }, new double?[] { 8, 3 }, new double?[] { 3, 8 } };
        var equal = SortingCore.Balanced(values, r => r, r => "", new[] { true, true }, true);
        var explicitEqual = SortingCore.Balanced(values, r => r, r => "", new[] { true, true }, true, new[] { 1, 1 });
        check(equal.Select(r => r.Score).SequenceEqual(explicitEqual.Select(r => r.Score)), "Default importance preserves the previous scores exactly");
        var importantFirst = SortingCore.Balanced(values, r => r, r => "", new[] { true, true }, true, new[] { 5, 1 });
        var importantSecond = SortingCore.Balanced(values, r => r, r => "", new[] { true, true }, true, new[] { 1, 5 });
        check(ReferenceEquals(importantFirst[1].Item, values[2]) && ReferenceEquals(importantSecond[1].Item, values[3]), "Changing priority reverses the appropriate tradeoff");
        check(importantFirst.All(r => r.Score.HasValue && SortingCore.Finite(r.Score.Value)), "Weighted scores remain finite including minimum and maximum candidates");
        var constants = values.Select(r => new double?[] { r[0], r[1], 17 }).ToArray();
        var withConstant = SortingCore.Balanced(constants, r => r, r => "", new[] { true, true, false }, true, new[] { 5, 1, 5 });
        check(importantFirst.Select(r => r.Score).SequenceEqual(withConstant.Select(r => r.Score)), "A constant high-priority column cannot dilute weighted scores");
    }
    internal static void Metadata(ModuleDefinition mod, Action<bool, string> check)
    {
        var dialog = mod.Types.Single(t => t.FullName == "MintStatSort.ComparisonWindow");
        var settings = mod.Types.Single(t => t.FullName == "MintStatSort.SortSettings");
        var copy = settings.Methods.Single(m => m.Name == "CopyComparisonFrom");
        check(copy.Body.Instructions.Any(i => i.OpCode == OpCodes.Ldftn) &&
            settings.NestedTypes.SelectMany(t => t.Methods).Any(m => Calls(m).Any(c => c.Name == "Copy" && c.DeclaringType.Name == "SortCriterion")),
            "Dialog preferences clone each criterion rather than editing persisted criteria by reference");
        check(Calls(dialog.Methods.Single(m => m.IsConstructor && !m.IsStatic)).Any(c => c.Name == "Copy" && c.DeclaringType.Name == "SortSettings"),
            "Opening a dialog takes an independent settings snapshot");
        var apply = dialog.Methods.Single(m => m.Name == "Apply");
        check(Calls(apply).Any(c => c.Name == "CopyComparisonFrom") && Calls(apply).Any(c => c.Name == "Changed"),
            "Apply copies the preview back and persists the preferences");
        check(dialog.Methods.Where(m => m.Name != "Apply").All(m => !Calls(m).Any(c => c.Name == "CopyComparisonFrom" ||
            (c.Name == "Changed" && c.DeclaringType.Name == "RecipeSession"))),
            "Only Apply commits the draft comparison; explicit favourites/profile saves remain independent");
        var toolbar = mod.Types.Single(t => t.FullName == "MintStatSort.Toolbar");
        check(Calls(toolbar.Methods.Single(m => m.Name == "Draw")).Any(c => c.Name == ".ctor" && c.DeclaringType == dialog),
            "The bills toolbar opens the unified comparison window");
        foreach (string name in new[] { "NiceIntegration", "MintIntegration" })
        {
            var integration = mod.Types.Single(t => t.Name == name);
            check(Calls(integration.Methods.Single(m => m.Name == "BeginScroll")).Any(c => c.Name == "Draw" && c.DeclaringType == toolbar),
                name + " embeds the entry point in its recipe area");
        }
        check(!mod.Types.Any(t => t.BaseType?.Name == "MainTabWindow"), "No global bottom-bar tab is introduced");
    }
    private static IEnumerable<MethodReference> Calls(MethodDefinition method)
        => !method.HasBody ? Enumerable.Empty<MethodReference>() : method.Body.Instructions
            .Where(i => i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt || i.OpCode == OpCodes.Newobj)
            .Select(i => i.Operand).OfType<MethodReference>();
}
