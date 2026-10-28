using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
namespace MintStatSort
{
    internal static class RecipeRequirements
    {
        internal static string Describe(RecipeDef recipe, bool available, string originalLabel)
        {
            if (recipe == null) return originalLabel ?? "";
            var lines = new List<string>();
            if (!string.IsNullOrEmpty(originalLabel)) lines.Add(originalLabel);
            var research = (recipe.researchPrerequisites ?? new List<ResearchProjectDef>()).ToList();
            if (recipe.researchPrerequisite != null) research.Add(recipe.researchPrerequisite);
            var missing = research.Distinct().Where(r => r != null && !r.IsFinished).Select(r => r.LabelCap.ToString()).ToList();
            if (missing.Count > 0) lines.Add("MSS.MissingResearch".Translate(string.Join(", ", missing)));
            if (recipe.skillRequirements != null)
                foreach (var skill in recipe.skillRequirements)
                    if (skill?.skill != null)
                    lines.Add("MSS.RequiredSkill".Translate(skill.skill.LabelCap.ToString(), skill.minLevel));
            if (!available && missing.Count == 0) lines.Add("MSS.OtherRestriction".Translate());
            lines.Add("MSS.RequirementsNote".Translate());
            return string.Join("\n", lines);
        }
    }
}
