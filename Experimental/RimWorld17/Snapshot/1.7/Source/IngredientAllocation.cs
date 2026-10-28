using System;
using System.Collections.Generic;

namespace MintStatSort
{
    // Shared reservations exist only in the preview, never in the actual map.
    public sealed class IngredientAllocation<T>
    {
        private readonly long required;
        private long allocated;
        public long Available { get; private set; }
        public long Missing => Math.Max(0, required - Available);
        public IngredientAllocation(long required) { this.required = Math.Max(0, required); }
        public void Add(T stack, int quantity, Dictionary<T, long> reservations)
        {
            if (quantity <= 0) return;
            long used;
            reservations.TryGetValue(stack, out used);
            long remaining = Math.Max(0, (long)quantity - used);
            Available += remaining;
            long take = Math.Min(remaining, required - allocated);
            if (take > 0) { reservations[stack] = used + take; allocated += take; }
        }
    }
}
