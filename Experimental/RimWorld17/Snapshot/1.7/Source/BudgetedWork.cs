using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MintStatSort
{
    // Runs game API work on the calling (UI) thread. Both limits are needed:
    // cached operations are cheap, while third-party stat workers can be slow.
    internal sealed class BudgetedWork : IDisposable
    {
        private IEnumerator<Action> steps;
        internal bool Pending => steps != null;
        internal void Replace(IEnumerable<Action> work)
        {
            Dispose();
            steps = work?.GetEnumerator();
        }
        internal int Run(double milliseconds, int maxOperations = 64)
        {
            if (milliseconds <= 0 || maxOperations <= 0) return 0;
            var watch = Stopwatch.StartNew();
            int completed = 0;
            while (steps != null && completed < maxOperations && watch.Elapsed.TotalMilliseconds < milliseconds)
            {
                var current = steps;
                if (!current.MoveNext()) { if (steps == current) Dispose(); break; }
                current.Current?.Invoke();
                completed++;
            }
            return completed;
        }
        public void Dispose()
        {
            var previous = steps;
            steps = null;
            previous?.Dispose();
        }
    }
}
