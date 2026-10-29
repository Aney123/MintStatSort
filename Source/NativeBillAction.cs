using System;

namespace MintStatSort
{
    internal static class NativeBillAction
    {
        internal static bool TryExecute(bool validContext, bool recipeAvailable, bool disabled, Action action)
        {
            if (!validContext || !recipeAvailable || disabled || action == null) return false;
            action();
            return true;
        }
    }
}
