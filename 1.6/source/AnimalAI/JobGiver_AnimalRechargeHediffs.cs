using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Chargeable_Hediffs_Framework
{
    // Autonomous counterpart to WorkGiver_RechargeHediffs for authorized colony animals.
    // Authorization is already gated by ThinkNode_ConditionalAnimalSelfCharge higher in the
    // subtree; this node only decides whether a station is currently needed and reachable.
    public class JobGiver_AnimalRechargeHediffs : ThinkNode_JobGiver
    {
        // Reused across calls; RimWorld's simulation is single-threaded.
        private static readonly List<Thing> s_candidateStations = new List<Thing>();

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!pawn.Spawned)
                return null;
            if (!HediffChargeUtility.NeedsRecharge(pawn, HediffChargeUtility.AutoRechargeThreshold))
                return null;

            Thing station = FindReachableStation(pawn);
            if (station == null)
                return null;

            return JobMaker.MakeJob(CHF_JobDefOf.CHF_RechargeHediffs, station);
        }

        private static Thing FindReachableStation(Pawn pawn)
        {
            s_candidateStations.Clear();
            try
            {
                IReadOnlyList<ThingDef> defs = HediffChargeUtility.StationDefs;
                for (int i = 0; i < defs.Count; i++)
                {
                    List<Thing> things = pawn.Map.listerThings.ThingsOfDef(defs[i]);
                    for (int j = 0; j < things.Count; j++)
                        s_candidateStations.Add(things[j]);
                }
                if (s_candidateStations.Count == 0)
                    return null;

                return GenClosest.ClosestThingReachable(
                    pawn.Position,
                    pawn.Map,
                    ThingRequest.ForGroup(ThingRequestGroup.Undefined),
                    PathEndMode.InteractionCell,
                    TraverseParms.For(pawn, pawn.NormalMaxDanger()),
                    9999f,
                    t => IsUsableStation(pawn, t),
                    s_candidateStations,
                    0,
                    -1,
                    forceAllowGlobalSearch: true);
            }
            finally
            {
                s_candidateStations.Clear();
            }
        }

        private static bool IsUsableStation(Pawn pawn, Thing station)
        {
            if (!HediffChargeUtility.IsValidStation(station))
                return false;
            if (!HediffChargeUtility.HasChargeableNeedingCharge(pawn, station))
                return false;
            if (station.IsForbidden(pawn))
                return false;
            if (!pawn.CanReserve(station))
                return false;
            return true;
        }
    }
}
