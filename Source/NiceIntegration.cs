using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace MintStatSort
{
    // Resolve the optional mod at runtime; its DLL is never distributed here.
    internal static class NiceIntegration
    {
        internal static bool Ready;
        private static Type holderType;
        private static Building_WorkTable currentTable;
        private static FieldInfo recipeField, labelField, availableField, scrollField, animationField, modeField;

        internal static void Install(Harmony harmony)
        {
            var drawer = AccessTools.TypeByName("NiceBillTab.TabBillsDrawer");
            holderType = AccessTools.TypeByName("NiceBillTab.RecipeHolder");
            var settings = AccessTools.TypeByName("NiceBillTab.Settings");
            if (drawer == null || holderType == null || settings == null)
                throw new InvalidOperationException("Nice Bill Tab: no se encuentran sus tipos de interfaz.");

            recipeField = RequiredField(holderType, "recipe", typeof(RecipeDef), false);
            labelField = RequiredField(holderType, "label", typeof(string), false);
            var listType = typeof(List<>).MakeGenericType(holderType);
            availableField = RequiredField(drawer, "availableRecipes", listType, true);
            scrollField = RequiredField(drawer, "scrollPosition", typeof(Vector2), true);
            animationField = RequiredField(drawer, "desiredYtoScrollRecipes", typeof(float?), true);
            modeField = RequiredField(settings, "RecipeListMode", typeof(int), true);
            var left = RequiredMethod(drawer, "DrawLeftPart", typeof(void), typeof(Rect), typeof(Building_WorkTable));
            var list = RequiredMethod(drawer, "DrawRecipeList", typeof(float), listType, typeof(float), typeof(float), typeof(float));
            var full = RequiredMethod(drawer, "DrawRecipePreview", typeof(float), typeof(Rect), holderType, typeof(bool));
            var compact = RequiredMethod(drawer, "DrawRecipePreviewCompact", typeof(float), typeof(Rect), holderType, typeof(bool));
            var click = RequiredMethod(drawer, "HandleRecipeClick", typeof(void), typeof(Rect), holderType);
            try
            {
                harmony.Patch(left, prefix: new HarmonyMethod(typeof(NiceIntegration), nameof(BeforeLeftPart)),
                    transpiler: new HarmonyMethod(typeof(NiceIntegration), nameof(MenuTranspiler)));
                harmony.Patch(list, transpiler: new HarmonyMethod(typeof(NiceIntegration), nameof(ListTranspiler)));
                harmony.Patch(full, transpiler: new HarmonyMethod(typeof(NiceIntegration), nameof(RowTranspiler)));
                harmony.Patch(compact, transpiler: new HarmonyMethod(typeof(NiceIntegration), nameof(RowTranspiler)));
                harmony.Patch(click, prefix: new HarmonyMethod(typeof(NiceIntegration), nameof(BeforeClickArea)));
                Ready = true;
                Log.Message("[Mint Stat Sort] Integración de recetas conectada a Nice Bill Tab.");
            }
            catch
            {
                Ready = false;
                harmony.UnpatchAll(harmony.Id);
                throw;
            }
        }

        private static FieldInfo RequiredField(Type type, string name, Type expected, bool isStatic)
        {
            var field = AccessTools.Field(type, name);
            if (field == null || field.FieldType != expected || field.IsStatic != isStatic)
                throw new InvalidOperationException("Nice Bill Tab: campo incompatible " + name + ".");
            return field;
        }
        private static MethodInfo RequiredMethod(Type type, string name, Type result, params Type[] arguments)
        {
            var method = AccessTools.Method(type, name, arguments);
            if (method == null || !method.IsStatic || method.ReturnType != result)
                throw new InvalidOperationException("Nice Bill Tab: método incompatible " + name + ".");
            return method;
        }
        internal static void ResetScroll()
        {
            if (!Ready) return;
            scrollField.SetValue(null, Vector2.zero);
            animationField.SetValue(null, null);
        }
        private static RecipeDef Recipe(object holder) { return (RecipeDef)recipeField.GetValue(holder); }
        private static string Label(object holder) { return (string)labelField.GetValue(holder); }
        private static void BeforeLeftPart(Building_WorkTable __1)
        {
            currentTable = __1;
            RecipeSession.SetMap(__1?.Map ?? Find.CurrentMap);
            RecipeSession.SetBench(__1?.def.defName);
        }

        private static IEnumerable<CodeInstruction> MenuTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            int count = 0;
            foreach (var code in codes)
            {
                if ((code.opcode == OpCodes.Call || code.opcode == OpCodes.Callvirt) && code.operand is MethodInfo method &&
                    method.DeclaringType == typeof(Widgets) && method.Name == "BeginScrollView" &&
                    method.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[]
                    { typeof(Rect), typeof(Vector2).MakeByRefType(), typeof(Rect), typeof(bool) }))
                {
                    code.opcode = OpCodes.Call;
                    code.operand = AccessTools.Method(typeof(NiceIntegration), nameof(BeginScroll));
                    count++;
                }
            }
            if (count != 1) throw new InvalidOperationException("Nice Bill Tab: área de recetas incompatible.");
            return codes;
        }
        private static void BeginScroll(Rect viewport, ref Vector2 scroll, Rect content, bool showScrollbars)
        {
            if (Ready && RecipeSession.Options.Enabled && viewport.height > Toolbar.Height + 40f)
            {
                var holders = (IEnumerable)availableField.GetValue(null);
                RecipeSession.SetRecipes(holders.Cast<object>().Select(Recipe).ToList());
                var table = currentTable;
                Toolbar.Draw(new Rect(viewport.x, viewport.y, viewport.width - 16f, Toolbar.Height),
                    () => table != null && table.Spawned && !table.Destroyed && table.Map == Find.CurrentMap &&
                        ReferenceEquals(Find.Selector.SingleSelectedThing, table), table?.LabelCap.ToString() ?? "Nice Bill Tab", table?.def.defName);
                viewport.y += Toolbar.Height;
                viewport.height -= Toolbar.Height;
            }
            Widgets.BeginScrollView(viewport, ref scroll, content, showScrollbars);
        }

        private static IEnumerable<CodeInstruction> ListTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            // Replace only the argument for this draw call, keeping Nice's cached
            // filtered list, holders, style identities and bill queue intact.
            var result = new List<CodeInstruction>
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(NiceIntegration), nameof(SortHolders)).MakeGenericMethod(holderType)),
                new CodeInstruction(OpCodes.Starg_S, (byte)0)
            };
            int count = 0;
            foreach (var code in instructions)
            {
                result.Add(code);
                if (code.opcode == OpCodes.Ldsfld && Equals(code.operand, modeField))
                {
                    result.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(NiceIntegration), nameof(EffectiveMode))));
                    count++;
                }
            }
            if (count != 3) throw new InvalidOperationException("Nice Bill Tab: modos de lista incompatibles.");
            return result;
        }
        private static List<T> SortHolders<T>(List<T> incoming)
        {
            if (!Ready || !RecipeSession.Options.Enabled) return incoming;
            return RecipeSession.SortForDisplay(incoming, h => Recipe(h), h => Label(h));
        }
        private static int EffectiveMode(int original)
        {
            // A category tree cannot express a global highest-to-lowest order.
            // Use Nice's own compact cards temporarily, without changing settings.
            bool active = RecipeSession.CurrentMetric.Id != "original" ||
                (!string.IsNullOrEmpty(RecipeSession.Options.Category) && RecipeSession.Options.Category != "all") || RecipeSession.Options.FavoritesOnly;
            return Ready && RecipeSession.Options.Enabled && active && original == 2 ? 1 : original;
        }
        private static IEnumerable<CodeInstruction> RowTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            int count = 0;
            foreach (var code in codes)
            {
                if (code.opcode != OpCodes.Ldfld || !Equals(code.operand, labelField)) continue;
                code.opcode = OpCodes.Call;
                code.operand = AccessTools.Method(typeof(NiceIntegration), nameof(ComparisonLabel));
                count++;
            }
            // Both CalcHeight and Label receive the same text, avoiding clipped
            // multi-line names in either of Nice's card layouts.
            if (count != 1) throw new InvalidOperationException("Nice Bill Tab: etiqueta de tarjeta incompatible.");
            return codes;
        }
        private static string ComparisonLabel(object holder)
        {
            string label = Label(holder);
            if (!Ready || !RecipeSession.Options.Enabled) return label;
            return !RecipeSession.HasComparison ? label : RecipeSession.Comparison(Recipe(holder)).Display + " · " + label;
        }
        private static void BeforeClickArea(Rect __0, object __1)
        {
            if (!Ready || !RecipeSession.Options.Enabled) return;
            if (RecipeSession.HasComparison)
                TooltipHandler.TipRegion(__0, RecipeSession.Comparison(Recipe(__1)).Detail);
        }
    }
}
