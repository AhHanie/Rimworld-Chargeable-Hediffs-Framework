using RimWorld;
using Verse;
using Verse.AI;

namespace Chargeable_Hediffs_Framework
{
    public class FloatMenuOptionProvider_RechargeHediff : FloatMenuOptionProvider
    {
        protected override bool Drafted => true;
        protected override bool Undrafted => true;
        protected override bool Multiselect => false;
        protected override bool MechanoidCanDo => true;

        protected override FloatMenuOption GetSingleOptionFor(Thing clickedThing, FloatMenuContext context)
        {
            if (!HediffChargeUtility.IsChargeStation(clickedThing))
                return null;

            Pawn pawn = context.FirstSelectedPawn;
            if (pawn == null)
                return null;

            if (!HediffChargeUtility.IsAutoRechargeEligible(pawn))
                return null;

            if (!HediffChargeUtility.HasRechargeableHediffs(pawn))
                return new FloatMenuOption(
                    "CHF_CannotRecharge".Translate() + ": " + "CHF_NoRechargeableHediffs".Translate(),
                    null);

            ChargeStationExtension ext = HediffChargeUtility.GetStationExtension(clickedThing.def);
            bool anySupported = false;
            foreach (HediffComp_Rechargeable rc in HediffChargeUtility.GetRechargeableComps(pawn))
            {
                if (ext.SupportsDef(rc.Def))
                {
                    anySupported = true;
                    break;
                }
            }
            if (!anySupported)
                return new FloatMenuOption(
                    "CHF_CannotRecharge".Translate() + ": " + "CHF_StationNotCompatible".Translate(),
                    null);

            if (!HediffChargeUtility.IsStationPowered(clickedThing))
                return new FloatMenuOption(
                    "CHF_CannotRecharge".Translate() + ": " + "CHF_StationNoPower".Translate(),
                    null);

            if (!HediffChargeUtility.HasChargeableNeedingCharge(pawn, clickedThing))
                return new FloatMenuOption(
                    "CHF_CannotRecharge".Translate() + ": " + "CHF_AlreadyFullyCharged".Translate(),
                    null);

            if (!pawn.CanReach(clickedThing, PathEndMode.InteractionCell, pawn.NormalMaxDanger()))
                return new FloatMenuOption(
                    "CHF_CannotRecharge".Translate() + ": " + "NoPath".Translate().CapitalizeFirst(),
                    null);

            return FloatMenuUtility.DecoratePrioritizedTask(
                new FloatMenuOption("CHF_RechargeAt".Translate(clickedThing.LabelCap), delegate
                {
                    Job job = JobMaker.MakeJob(CHF_JobDefOf.CHF_RechargeHediffs, clickedThing);
                    pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                }),
                pawn,
                clickedThing);
        }
    }
}
