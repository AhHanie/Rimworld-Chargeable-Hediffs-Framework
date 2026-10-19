using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Chargeable_Hediffs_Framework
{
    // The wearer of a charge pack walks up to a recipient, stands beside it, and recharges its
    // hediffs while a progress bar fills. TargetA = recipient, TargetB = the worn pack.
    // Charge is transferred and a pack use spent only when the service step completes.
    public class JobDriver_RechargePawnFromPack : JobDriver
    {
        private const TargetIndex RecipientInd = TargetIndex.A;
        private const TargetIndex PackInd = TargetIndex.B;
        private const TargetIndex StandCellInd = TargetIndex.C;

        // Finite chase so a constantly moving or unreachable recipient cannot hold the worker forever.
        private const int ApproachTimeoutTicks = 3000;

        // The recipient's wait outlives the service step slightly; it normally ends explicitly.
        private const int WaitExpiryMargin = 30;

        private Pawn Recipient => job.GetTarget(RecipientInd).Thing as Pawn;

        private Thing Pack => job.GetTarget(PackInd).Thing;

        private CompApparelChargeBattery PackComp => Pack?.TryGetComp<CompApparelChargeBattery>();

        private int ServiceTicks => PackComp?.Props.rechargeDurationTicks ?? 1;

        // Cheap per-tick validity check; does not test reachability.
        private bool IsOrderStillValid()
        {
            CompApparelChargeBattery comp = PackComp;
            if (comp == null || comp.Wearer != pawn || comp.RemainingCharges <= 0)
                return false;
            Pawn recipient = Recipient;
            if (recipient == null)
                return false;
            return comp.GetRecipientDenial(recipient, checkReach: false) == PackRechargeDenial.None;
        }

        private bool ApproachTimedOut()
        {
            return Find.TickManager.TicksGame - job.startTick > ApproachTimeoutTicks;
        }

        private bool IsAdjacentToRecipient()
        {
            Pawn recipient = Recipient;
            return recipient != null && recipient.Spawned && pawn.Position.AdjacentTo8Way(recipient.Position);
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            Pawn recipient = Recipient;
            CompApparelChargeBattery comp = PackComp;
            if (recipient == null || comp == null || comp.Wearer != pawn || comp.RemainingCharges <= 0)
                return false;
            if (comp.GetRecipientDenial(recipient) != PackRechargeDenial.None)
                return false;
            return pawn.Reserve(recipient, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(RecipientInd);
            this.FailOn(() => !IsOrderStillValid());
            AddFinishAction(delegate { ReleaseRecipientWait(); });

            Toil approach = Toils_Goto.GotoThing(RecipientInd, PathEndMode.Touch)
                .FailOn(ApproachTimedOut);

            // RimWorld evaluates a toil's fail conditions before its initAction runs, so the
            // recipient's wait must be started by a toil of its own, ahead of the service toil
            // whose fail conditions require that wait to exist.
            Toil startWait = ToilMaker.MakeToil("HoldRecipientInPlace");
            startWait.defaultCompleteMode = ToilCompleteMode.Instant;
            startWait.initAction = delegate
            {
                pawn.pather.StopDead();
                Pawn recipient = Recipient;
                if (recipient != null && !CompApparelChargeBattery.IsRecipientStationary(recipient))
                    StartRecipientWait(recipient);
            };

            Toil service = MakeServiceToil();

            // Touch arrival can leave the worker inside the recipient's cell or have the recipient
            // wander off; step aside to a free adjacent cell, then re-check.
            Toil position = ToilMaker.MakeToil("PositionBesideRecipient");
            position.defaultCompleteMode = ToilCompleteMode.Instant;
            position.initAction = delegate
            {
                if (IsAdjacentToRecipient())
                {
                    JumpToToil(startWait);
                    return;
                }
                if (TryFindAdjacentStandCell(out IntVec3 cell))
                    job.SetTarget(StandCellInd, cell);
                else
                    EndJobWith(JobCondition.Incompletable);
            };

            Toil stepAside = Toils_Goto.GotoCell(StandCellInd, PathEndMode.OnCell)
                .FailOn(ApproachTimedOut);

            yield return approach;
            yield return position;
            yield return stepAside;
            yield return Toils_Jump.Jump(position);
            yield return startWait;
            yield return service;
            yield return Toils_General.Do(CompleteRecharge);
        }

        private Toil MakeServiceToil()
        {
            Toil service = ToilMaker.MakeToil("RechargePawnFromPack");
            service.defaultCompleteMode = ToilCompleteMode.Delay;
            service.defaultDuration = ServiceTicks;
            service.handlingFacing = true;

            service.initAction = delegate
            {
                pawn.pather.StopDead();
            };
            service.tickAction = delegate
            {
                Pawn recipient = Recipient;
                if (recipient != null)
                    pawn.rotationTracker.FaceTarget(recipient);
            };
            service.WithProgressBarToilDelay(RecipientInd, ServiceTicks);

            service.FailOn(() => !IsAdjacentToRecipient());
            service.FailOn(() =>
            {
                Pawn recipient = Recipient;
                return recipient == null
                    || (!CompApparelChargeBattery.IsRecipientStationary(recipient) && !IsRecipientWaitingForUs(recipient));
            });
            return service;
        }

        private void StartRecipientWait(Pawn recipient)
        {
            Job waitJob = JobMaker.MakeJob(CHF_JobDefOf.CHF_WaitForPackRecharge, pawn, Pack);
            waitJob.expiryInterval = ServiceTicks + WaitExpiryMargin;
            recipient.jobs.StartJob(waitJob, JobCondition.InterruptForced, null, resumeCurJobAfterwards: true);
        }

        private bool IsRecipientWaitingForUs(Pawn recipient)
        {
            return recipient?.jobs != null && JobDriver_WaitForPackRecharge.IsWaitFor(recipient.CurJob, pawn, Pack);
        }

        // Ends the recipient's wait only if it is the one this worker/pack created; never an
        // unrelated wait or a newer order. RimWorld resumes the suspended job afterwards.
        private void ReleaseRecipientWait()
        {
            Pawn recipient = Recipient;
            if (IsRecipientWaitingForUs(recipient))
                recipient.jobs.EndCurrentJob(JobCondition.InterruptForced);
        }

        private bool TryFindAdjacentStandCell(out IntVec3 best)
        {
            best = IntVec3.Invalid;
            Pawn recipient = Recipient;
            if (recipient == null || !recipient.Spawned)
                return false;

            Map map = recipient.Map;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < GenAdj.AdjacentCells.Length; i++)
            {
                IntVec3 cell = recipient.Position + GenAdj.AdjacentCells[i];
                if (!cell.InBounds(map) || !cell.Standable(map) || cell.IsForbidden(pawn))
                    continue;
                if (!ReachabilityImmediate.CanReachImmediate(cell, recipient, map, PathEndMode.Touch, pawn))
                    continue;
                if (!pawn.CanReach(cell, PathEndMode.OnCell, pawn.NormalMaxDanger()))
                    continue;
                int distance = (cell - pawn.Position).LengthHorizontalSquared;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = cell;
                }
            }
            return best.IsValid;
        }

        private void CompleteRecharge()
        {
            CompApparelChargeBattery comp = PackComp;
            Pawn recipient = Recipient;
            if (comp == null || recipient == null || comp.Wearer != pawn || comp.RemainingCharges <= 0
                || comp.GetRecipientDenial(recipient, checkReach: false) != PackRechargeDenial.None
                || !IsAdjacentToRecipient())
            {
                EndJobWith(JobCondition.Incompletable);
                return;
            }

            float added = comp.TryRechargePawn(recipient);
            if (added <= 0f)
            {
                EndJobWith(JobCondition.Incompletable);
                return;
            }

            comp.UsedOnce();
            MoteMaker.ThrowText(recipient.DrawPos, recipient.Map, "+" + added.ToString("F1"), Color.white);
            ReleaseRecipientWait();
        }
    }
}
