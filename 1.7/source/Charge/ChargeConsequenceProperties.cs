using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Chargeable_Hediffs_Framework
{
    public class ChargeConsequenceProperties
    {
        public List<StatModifier> statOffsets;
        public List<StatModifier> statFactors;
        public float partEfficiencyOffset = 0f;

        public void Normalize()
        {
            if (statOffsets == null)
                statOffsets = new List<StatModifier>();
            if (statFactors == null)
                statFactors = new List<StatModifier>();
        }

        public IEnumerable<string> ConfigErrors(HediffDef parentDef)
        {
            if (statOffsets != null)
                for (int i = 0; i < statOffsets.Count; i++)
                    if (statOffsets[i].stat == null)
                        yield return $"depletedConsequences.statOffsets[{i}].stat is null";
            if (statFactors != null)
                for (int i = 0; i < statFactors.Count; i++)
                {
                    if (statFactors[i].stat == null)
                        yield return $"depletedConsequences.statFactors[{i}].stat is null";
                    if (statFactors[i].value == 0f)
                        yield return $"depletedConsequences.statFactors[{i}].value is 0; zero stat factors cannot be removed from the consequence cache — use a small nonzero value instead";
                }
        }
    }
}
