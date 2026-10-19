using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Chargeable_Hediffs_Framework
{
    // Holds a mobile recipient in place while a pack wearer recharges it. TargetA = the worker,
    // TargetB = the pack. The wait ends by itself as soon as the worker is no longer running the
    // matching recharge job, so a missed cleanup can never leave the recipient stuck.
    public class JobDriver_WaitForPackRecharge : JobDriver
    {
        private const TargetIndex WorkerInd = TargetIndex.A;
        private const TargetIndex PackInd = TargetIndex.B;

        // True if the job is a wait created by this worker for this pack.
        public static bool IsWaitFor(Job waitJob, Pawn worker, Thing pack)
        {
            return waitJob != null
                && waitJob.def == CHF_JobDefOf.CHF_WaitForPackRecharge
                && waitJob.GetTarget(WorkerInd).Thing == worker
                && waitJob.GetTarget(PackInd).Thing == pack;
        }

        // True if the worker is currently running the recharge job that owns this wait.
        private bool WorkerStillServicing()
        {
            Pawn worker = job.GetTarget(WorkerInd).Thing as Pawn;
            if (worker == null || worker.Dead || !worker.Spawned || worker.jobs == null)
                return false;
            Job workerJob = worker.CurJob;
            return workerJob != null
                && workerJob.def == CHF_JobDefOf.CHF_RechargePawnFromPack
                && workerJob.GetTarget(TargetIndex.A).Thing == pawn
                && workerJob.GetTarget(TargetIndex.B).Thing == job.GetTarget(PackInd).Thing;
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        // Keep the current posture, e.g. a recipient resting in bed stays lying.
        public override void SetInitialPosture()
        {
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil wait = ToilMaker.MakeToil("WaitForPackRecharge");
            wait.defaultCompleteMode = ToilCompleteMode.Never;
            wait.handlingFacing = true;
            wait.initAction = delegate
            {
                pawn.pather?.StopDead();
                Map.pawnDestinationReservationManager.Reserve(pawn, job, pawn.Position);
            };
            wait.tickAction = delegate
            {
                if (job.GetTarget(WorkerInd).Thing is Pawn worker && worker.Spawned)
                    pawn.rotationTracker.FaceTarget(worker);
            };
            wait.AddEndCondition(() => WorkerStillServicing() ? JobCondition.Ongoing : JobCondition.Succeeded);
            yield return wait;
        }
    }
}
