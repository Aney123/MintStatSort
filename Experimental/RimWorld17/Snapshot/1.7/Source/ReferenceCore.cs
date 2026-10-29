using System;
using System.Collections.Generic;

namespace MintStatSort
{
    public sealed class AttackReading
    {
        public string Category;
        public double Damage;
    }
    public static class ReferenceCore
    {
        public static double? HighestDamage(IEnumerable<AttackReading> attacks, string category)
        {
            bool any = false, matching = false, valid = false;
            double highest = 0;
            foreach (var attack in attacks)
            {
                any = true;
                if (category != null && !string.Equals(attack.Category, category, StringComparison.Ordinal)) continue;
                matching = true;
                if (!SortingCore.Finite(attack.Damage) || attack.Damage < 0) continue;
                valid = true;
                highest = Math.Max(highest, attack.Damage);
            }
            return !any || (matching && !valid) ? (double?)null : highest;
        }
        public static long StoredQuantity<T>(IEnumerable<T> stacks, Func<T, int> quantity,
            Func<T, bool> spawned, Func<T, bool> inStorage)
        {
            long count = 0;
            foreach (var stack in stacks)
            {
                int amount = quantity(stack);
                if (amount > 0 && spawned(stack) && inStorage(stack)) count += amount;
            }
            return count;
        }
    }
}
