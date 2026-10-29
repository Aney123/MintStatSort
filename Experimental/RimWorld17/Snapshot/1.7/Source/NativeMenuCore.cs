using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace MintStatSort
{
    // Read captured data only. Never run a menu action to identify its recipe.
    internal static class NativeMenuCore
    {
        internal static T ResolveRecipe<T>(IEnumerable<Delegate> actions, IEnumerable<T> allowed,
            Func<Type, bool> nativeClosure) where T : class
        {
            var candidates = allowed as HashSet<T> ?? new HashSet<T>(allowed ?? Enumerable.Empty<T>());
            var found = new HashSet<T>();
            var visited = new HashSet<object>(ReferenceIdentity.Instance);
            bool failed = false;
            Action<object, int> inspect = null;
            inspect = (target, depth) =>
            {
                if (target == null || visited.Contains(target)) return;
                var type = target.GetType();
                if (!nativeClosure(type)) return;
                if (depth > 6 || visited.Count >= 32) { failed = true; return; }
                visited.Add(target);
                try
                {
                    foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        object value = field.GetValue(target);
                        if (value is T recipe && candidates.Contains(recipe)) found.Add(recipe);
                        else if (value != null && nativeClosure(value.GetType())) inspect(value, depth + 1);
                    }
                }
                catch { failed = true; }
            };
            foreach (var action in actions ?? Enumerable.Empty<Delegate>())
                foreach (var callback in action?.GetInvocationList() ?? Array.Empty<Delegate>()) inspect(callback.Target, 0);
            return !failed && found.Count == 1 ? found.First() : null;
        }

        internal static List<T> OriginalOrder<T>(IEnumerable<T> options, Func<T, int> priority, Func<T, int> order)
            => options.OrderByDescending(priority).ThenByDescending(order).ToList();

        // Keep opaque options and submenu actions in their original relative places.
        // Only identified recipe slots participate in comparison/category filtering.
        internal static List<T> MergeKnown<T>(IEnumerable<T> original, Func<T, bool> known, IEnumerable<T> sorted)
        {
            var remaining = new Queue<T>(sorted);
            var result = new List<T>();
            foreach (var option in original)
                if (!known(option)) result.Add(option);
                else if (remaining.Count != 0) result.Add(remaining.Dequeue());
            return result;
        }

        private sealed class ReferenceIdentity : IEqualityComparer<object>
        {
            internal static readonly ReferenceIdentity Instance = new ReferenceIdentity();
            public new bool Equals(object a, object b) => ReferenceEquals(a, b);
            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }
    }
}
