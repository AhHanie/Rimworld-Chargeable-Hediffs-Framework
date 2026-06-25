using System.Collections.Generic;
using Verse;

namespace Chargeable_Hediffs_Framework
{
    public class HediffCompProperties_Rechargeable : HediffCompProperties
    {
        // Max stored charge. Modders scale this to whatever unit suits their hediff.
        public float maxCharge = 1f;

        // Initial charge when the hediff is first created. Negative means start at maxCharge.
        public float startingCharge = -1f;

        // Charge lost per game tick while the pawn is alive and active.
        public float chargeDecayPerTick = 0f;

        public ChargeConsequenceProperties depletedConsequences;

        public HediffCompProperties_Rechargeable()
        {
            compClass = typeof(HediffComp_Rechargeable);
        }

        public override void ResolveReferences(HediffDef parent)
        {
            base.ResolveReferences(parent);
            depletedConsequences?.Normalize();
        }

        public override IEnumerable<string> ConfigErrors(HediffDef parentDef)
        {
            foreach (string err in base.ConfigErrors(parentDef))
                yield return err;
            if (maxCharge <= 0f)
                yield return $"{nameof(maxCharge)} must be positive (got {maxCharge})";
            if (chargeDecayPerTick < 0f)
                yield return $"{nameof(chargeDecayPerTick)} cannot be negative (got {chargeDecayPerTick})";
            if (depletedConsequences != null)
                foreach (string err in depletedConsequences.ConfigErrors(parentDef))
                    yield return err;
        }
    }
}
