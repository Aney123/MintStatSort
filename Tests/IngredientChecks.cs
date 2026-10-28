using System;
using System.Collections.Generic;
using MintStatSort;

internal static class IngredientChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var reservations = new Dictionary<string, long>();
        var first = new IngredientAllocation<string>(7);
        first.Add("pile", 10, reservations);
        check(first.Available == 10 && first.Missing == 0 && reservations["pile"] == 7, "The first ingredient reserves only its requirement inside the preview");
        var second = new IngredientAllocation<string>(7);
        second.Add("pile", 10, reservations);
        check(second.Available == 3 && second.Missing == 4 && reservations["pile"] == 10, "Overlapping ingredients cannot reuse a stack consumed by an earlier requirement");
        second.Add("other", 5, reservations);
        check(second.Available == 8 && second.Missing == 0 && reservations["other"] == 4, "Compatible additional stacks can cover a remaining shortfall");
        var zero = new IngredientAllocation<string>(0);
        zero.Add("other", 5, reservations);
        check(zero.Available == 1 && zero.Missing == 0 && reservations["other"] == 4, "A zero requirement reports remaining stock without reserving any");
        var many = new IngredientAllocation<string>(long.MaxValue);
        var fresh = new Dictionary<string, long>();
        many.Add("a", int.MaxValue, fresh); many.Add("b", int.MaxValue, fresh); many.Add("c", -1, fresh);
        check(many.Available == 2L * int.MaxValue && many.Missing > int.MaxValue, "Large stock counts retain 64-bit precision and ignore nonpositive stacks");
        var empty = new IngredientAllocation<string>(9);
        check(empty.Available == 0 && empty.Missing == 9, "An empty compatible stock produces an explicit shortfall");
    }
}
