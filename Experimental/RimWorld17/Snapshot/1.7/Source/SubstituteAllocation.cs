using System;
using System.Collections.Generic;
namespace MintStatSort
{
    // Quantities converted to fractions of a full ingredient requirement.
    // Reservations belong only to one recipe preview and are never game orders.
    public sealed class SubstituteAllocation<T>
    {
        public double Available { get; private set; }
        private double remaining = 1;
        public bool Complete => remaining <= 1e-9;
        public double Remaining => Complete ? 0 : remaining;
        public void Add(T stack, int count, int fullRequirement, Dictionary<T, long> reservations)
        {
            if (fullRequirement <= 0 || count <= 0) return;
            reservations.TryGetValue(stack, out long used);
            long free = Math.Max(0, (long)count - used);
            Available += (double)free / fullRequirement;
            long take = Math.Min(free, (long)Math.Ceiling(Math.Max(0, remaining * fullRequirement - 1e-9)));
            if (take > 0)
            {
                reservations[stack] = used + take;
                remaining = Math.Max(0, remaining - (double)take / fullRequirement);
            }
        }
    }
}
