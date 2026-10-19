using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Chargeable_Hediffs_Framework
{
    public class JobDriver_RechargeHediffs : JobDriver
    {
        private const TargetIndex StationInd = TargetIndex.A;

        private Thing Station => job.targetA.Thing;

        private readonly RechargeHediffBiotechFx biotechFx = new RechargeHediffBiotechFx();

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Station, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(StationInd);
            this.FailOn(() => !HediffChargeUtility.IsValidStation(Station));
            // Cancel if the pawn has nothing left this station services: neither rechargeable
            // hediffs nor (when the station opts in) worn charge packs.
            this.FailOn(() => !HediffChargeUtility.HasChargeableByStation(pawn, Station)
                && !HediffChargeUtility.HasWornPackSupportedByStation(pawn, Station));
            // Cancel a colony animal's self-directed job if it loses its self-charge authorization
            // mid-job (training decay, catalyst removal). No-op for non-animal pawns.
            this.FailOn(() => pawn.IsColonyAnimal && !HediffChargeUtility.IsAnimalAuthorizedToSelfCharge(pawn));

            yield return Toils_Goto.GotoThing(StationInd, PathEndMode.InteractionCell)
                .FailOnForbidden(StationInd);

            Toil charge = ToilMaker.MakeToil("MakeNewToils");
            charge.defaultCompleteMode = ToilCompleteMode.Never;
            charge.handlingFacing = true;

            charge.initAction = delegate
            {
                biotechFx.Start(pawn, Station);
            };

            charge.tickIntervalAction = delegate(int delta)
            {
                pawn.rotationTracker.FaceTarget(Station.Position);
                int chargedCount = HediffChargeUtility.ChargeAllFromStation(pawn, Station, delta)
                    + HediffChargeUtility.ChargeWornPacksFromStation(pawn, Station, delta);
                if (chargedCount > 0)
                    biotechFx.Maintain(pawn, Station);
                else
                    biotechFx.End();
                if (!HediffChargeUtility.HasChargeableNeedingCharge(pawn, Station)
                    && !HediffChargeUtility.HasWornPackNeedingStationCharge(pawn, Station))
                    ReadyForNextToil();
            };

            charge.AddFinishAction(delegate
            {
                biotechFx.End();
            });

            charge.WithProgressBar(StationInd,
                () => HediffChargeUtility.AggregateStationChargePercent(pawn, Station));

            yield return charge;
        }
    }
}
