using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Chargeable_Hediffs_Framework
{
    public class HediffCompProperties_ChargeBattery : HediffCompProperties
    {
        // Max charge drained from the battery's own rechargeable store per tick, before efficiency.
        public float chargeRate = 0f;

        // Fraction of drained source charge actually delivered to the target. 0.75 means draining
        // 1 from the battery adds 0.75 to the target; the remainder is lost.
        public float transferEfficiency = 1f;

        // Optional whitelist of target HediffDefs. Null/empty means any non-battery rechargeable
        // hediff on the pawn is a valid target.
        public List<HediffDef> supportedHediffs;

        // Optional blacklist of target HediffDefs. Takes priority over supportedHediffs.
        public List<HediffDef> excludedHediffs;

        public bool showInspectString = true;

        public HediffCompProperties_ChargeBattery()
        {
            compClass = typeof(HediffComp_ChargeBattery);
        }

        public bool SupportsTarget(HediffDef def)
        {
            if (excludedHediffs != null && excludedHediffs.Contains(def))
                return false;
            return supportedHediffs == null || supportedHediffs.Count == 0 || supportedHediffs.Contains(def);
        }

        public override IEnumerable<string> ConfigErrors(HediffDef parentDef)
        {
            foreach (string err in base.ConfigErrors(parentDef))
                yield return err;

            if (parentDef.comps == null || !parentDef.comps.Any(c => c is HediffCompProperties_Rechargeable))
                yield return $"{nameof(HediffCompProperties_ChargeBattery)}: '{parentDef.defName}' must also have a {nameof(HediffCompProperties_Rechargeable)} comp";

            if (chargeRate <= 0f)
                yield return $"{nameof(chargeRate)} must be positive (got {chargeRate})";

            if (transferEfficiency <= 0f || transferEfficiency > 1f)
                yield return $"{nameof(transferEfficiency)} must be > 0 and <= 1 (got {transferEfficiency})";

            if (supportedHediffs != null)
            {
                for (int i = 0; i < supportedHediffs.Count; i++)
                {
                    if (supportedHediffs[i] == null)
                        yield return $"{nameof(HediffCompProperties_ChargeBattery)}: '{parentDef.defName}' has a null entry in {nameof(supportedHediffs)} at index {i} — check that all HediffDef names are spelled correctly";
                }
            }

            if (excludedHediffs != null)
            {
                for (int i = 0; i < excludedHediffs.Count; i++)
                {
                    if (excludedHediffs[i] == null)
                        yield return $"{nameof(HediffCompProperties_ChargeBattery)}: '{parentDef.defName}' has a null entry in {nameof(excludedHediffs)} at index {i} — check that all HediffDef names are spelled correctly";
                }
            }
        }
    }
}
