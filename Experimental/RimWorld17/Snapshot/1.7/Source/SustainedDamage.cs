using System;
namespace MintStatSort
{
    public static class SustainedDamage
    {
        // Repeated full magazines, default burst, one complete reload per cycle.
        public static double? Calculate(double damage, double pellets, int shots, int burst,
            double warmup, double cooldown, double interval, double reload)
        {
            foreach (double value in new[] { damage, pellets, warmup, cooldown, interval, reload })
                if (!SortingCore.Finite(value) || value < 0) return null;
            if (shots <= 0 || burst <= 0 || pellets <= 0) return null;
            double cycles = Math.Ceiling((double)shots / burst);
            double seconds = cycles * (warmup + cooldown) + (shots - cycles) * interval + reload;
            if (seconds <= 0 || !SortingCore.Finite(seconds)) return null;
            double valueDps = damage * pellets * shots / seconds;
            return SortingCore.Finite(valueDps) ? (double?)valueDps : null;
        }
    }
}
