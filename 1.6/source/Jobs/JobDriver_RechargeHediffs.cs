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
            // Cancel if the pawn's rechargeable hediffs that this station services all disappear.
            this.FailOn(() => !HediffChargeUtility.HasChargeableByStation(pawn, Station));

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
                int chargedCount = HediffChargeUtility.ChargeAllFromStation(pawn, Station, delta);
                if (chargedCount > 0)
                    biotechFx.Maintain(pawn, Station);
                else
                    biotechFx.End();
                if (!HediffChargeUtility.HasChargeableNeedingCharge(pawn, Station))
                    ReadyForNextToil();
            };

            charge.AddFinishAction(delegate
            {
                biotechFx.End();
            });

            charge.WithProgressBar(StationInd,
                () => HediffChargeUtility.AggregateChargePercent(pawn, Station));

            yield return charge;
        }
    }
}
