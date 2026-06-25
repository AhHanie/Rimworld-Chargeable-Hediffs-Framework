using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Chargeable_Hediffs_Framework
{
    public class WorkGiver_RechargeHediffs : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.InteractionCell;

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            if (!HediffChargeUtility.IsAutoRechargeEligible(pawn))
                return true;
            if (!forced && !HediffChargeUtility.NeedsRecharge(pawn, HediffChargeUtility.AutoRechargeThreshold))
                return true;
            return false;
        }

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            if (pawn.Map == null)
                yield break;
            IReadOnlyList<ThingDef> defs = HediffChargeUtility.StationDefs;
            for (int i = 0; i < defs.Count; i++)
            {
                List<Thing> things = pawn.Map.listerThings.ThingsOfDef(defs[i]);
                for (int j = 0; j < things.Count; j++)
                    yield return things[j];
            }
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!HediffChargeUtility.IsValidStation(t))
                return false;
            if (!HediffChargeUtility.HasChargeableNeedingCharge(pawn, t))
                return false;
            if (t.IsForbidden(pawn))
                return false;
            if (!pawn.CanReserve(t, 1, -1, null, forced))
                return false;
            if (!pawn.CanReach(t, PathEndMode.InteractionCell, pawn.NormalMaxDanger()))
                return false;
            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!HasJobOnThing(pawn, t, forced))
                return null;
            return JobMaker.MakeJob(CHF_JobDefOf.CHF_RechargeHediffs, t);
        }
    }
}
