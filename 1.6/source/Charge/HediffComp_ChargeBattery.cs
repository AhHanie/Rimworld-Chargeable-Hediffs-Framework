using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

namespace Chargeable_Hediffs_Framework
{
    // Drains a HediffComp_Rechargeable on the same hediff and transfers the charge into other
    // rechargeable hediffs on the pawn. Runs unconditionally on tick: no spawned/awake/faction/
    // caravan eligibility checks, since this is an internal hediff-to-hediff transfer.
    public class HediffComp_ChargeBattery : HediffComp
    {
        private HediffComp_Rechargeable source;

        // Last-transfer stats for debug/tooltip only; not saved.
        private string lastTargetLabel;
        private float lastSourceDrained;
        private float lastTargetChargeAdded;
        private int lastTargetsCharged;

        public HediffCompProperties_ChargeBattery Props => (HediffCompProperties_ChargeBattery)props;

        // Lazy rather than cached only in CompPostMake: loaded comps are reconstructed before
        // CompExposeData runs, so CompPostMake never fires for them.
        private HediffComp_Rechargeable Source => source ?? (source = parent.GetComp<HediffComp_Rechargeable>());

        public override string CompTipStringExtra => Props.showInspectString ? GetBatteryInspectString() : null;

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            HediffComp_Rechargeable src = Source;
            if (src == null)
                return;
            Pawn pawn = Pawn;
            float budget = Mathf.Min(src.CurrentCharge, Props.chargeRate * delta);
            if (budget <= 0f)
                return;

            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;

            lastTargetsCharged = 0;
            lastSourceDrained = 0f;
            lastTargetChargeAdded = 0f;

            while (budget > 0f)
            {
                HediffComp_Rechargeable target = FindBestTarget(hediffs, src);
                if (target == null)
                    break;

                float targetMissing = target.MaxCharge - target.CurrentCharge;
                float sourceNeededForTarget = targetMissing / Props.transferEfficiency;
                float drain = Mathf.Min(Mathf.Min(budget, sourceNeededForTarget), src.CurrentCharge);
                if (drain <= 0f)
                    break;

                float gain = drain * Props.transferEfficiency;
                target.AddCharge(gain);
                src.DrainCharge(drain);
                budget -= drain;

                lastTargetLabel = target.parent.LabelCap;
                lastSourceDrained += drain;
                lastTargetChargeAdded += gain;
                lastTargetsCharged++;
            }
        }

        private HediffComp_Rechargeable FindBestTarget(List<Hediff> hediffs, HediffComp_Rechargeable src)
        {
            HediffComp_Rechargeable best = null;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (!(hediffs[i] is HediffWithComps hwc))
                    continue;

                HediffComp_Rechargeable candidate = null;
                bool isBattery = false;
                for (int j = 0; j < hwc.comps.Count; j++)
                {
                    if (hwc.comps[j] is HediffComp_Rechargeable rc)
                        candidate = rc;
                    else if (hwc.comps[j] is HediffComp_ChargeBattery)
                        isBattery = true;
                }

                // Battery hediffs are never targets, even if XML filters would otherwise allow them.
                if (candidate == null || isBattery || candidate == src)
                    continue;
                if (!candidate.NeedsCharge)
                    continue;
                if (!Props.SupportsTarget(candidate.Def))
                    continue;

                if (best == null || IsBetterTarget(candidate, best))
                    best = candidate;
            }
            return best;
        }

        private static bool IsBetterTarget(HediffComp_Rechargeable candidate, HediffComp_Rechargeable current)
        {
            if (candidate.ChargePercent < current.ChargePercent)
                return true;
            if (candidate.ChargePercent > current.ChargePercent)
                return false;
            float candidateMissing = candidate.MaxCharge - candidate.CurrentCharge;
            float currentMissing = current.MaxCharge - current.CurrentCharge;
            return candidateMissing > currentMissing;
        }

        private string GetBatteryInspectString()
        {
            var sb = new StringBuilder();
            sb.Append("CHF_BatteryTransferRate".Translate());
            sb.Append(": ");
            sb.Append(Props.chargeRate.ToString("F2"));
            sb.Append(" / tick");

            sb.AppendLine();
            sb.Append("CHF_BatteryTransferEfficiency".Translate());
            sb.Append(": ");
            sb.Append(Props.transferEfficiency.ToStringPercent());

            sb.AppendLine();
            sb.Append("CHF_BatteryTargets".Translate());
            sb.Append(": ");
            bool hasWhitelist = Props.supportedHediffs != null && Props.supportedHediffs.Count > 0;
            bool hasBlacklist = Props.excludedHediffs != null && Props.excludedHediffs.Count > 0;
            if (!hasWhitelist && !hasBlacklist)
                sb.Append("CHF_BatteryTargetsAll".Translate());
            else
                sb.Append("CHF_BatteryTargetsFiltered".Translate(
                    hasWhitelist ? Props.supportedHediffs.Count : 0,
                    hasBlacklist ? Props.excludedHediffs.Count : 0));

            return sb.ToString();
        }

        public override string CompDebugString()
        {
            HediffComp_Rechargeable src = Source;
            if (src == null)
                return $"{nameof(HediffComp_ChargeBattery)}: no {nameof(HediffComp_Rechargeable)} found on parent hediff";

            var sb = new StringBuilder();
            sb.Append($"source charge: {src.CurrentCharge:F3} / {src.MaxCharge:F3}");
            sb.Append($"  rate: {Props.chargeRate:F3}/tick  efficiency: {Props.transferEfficiency:P0}");
            if (lastTargetsCharged > 0)
                sb.Append($"\nlast tick: drained {lastSourceDrained:F3}, added {lastTargetChargeAdded:F3} to {lastTargetsCharged} target(s), last={lastTargetLabel}");
            return sb.ToString();
        }
    }
}
