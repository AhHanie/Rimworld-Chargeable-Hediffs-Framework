using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;
using RimWorld.Planet;

namespace Chargeable_Hediffs_Framework
{
    public class Alert_LowHediffCharge : Alert
    {
        private const float LowThreshold = HediffChargeUtility.AutoRechargeThreshold;

        private readonly List<GlobalTargetInfo> _targets = new List<GlobalTargetInfo>();
        private readonly List<string> _targetLabels = new List<string>();

        public Alert_LowHediffCharge()
        {
            defaultLabel = "CHF_Alert_LowCharge".Translate();
        }

        public override string GetLabel()
        {
            if (_targets.Count == 1)
                return defaultLabel + ": " + _targetLabels[0];
            return defaultLabel;
        }

        private void CalculateTargets()
        {
            _targets.Clear();
            _targetLabels.Clear();
            foreach (Pawn pawn in PawnsFinder.AllMapsCaravansAndTravellingTransporters_AliveSpawned)
            {
                if (!HediffChargeUtility.IsAutoRechargeEligible(pawn))
                    continue;
                if (!HediffChargeUtility.NeedsRecharge(pawn, LowThreshold))
                    continue;
                _targets.Add(pawn);
                _targetLabels.Add(pawn.NameShortColored.Resolve());
            }
        }

        public override TaggedString GetExplanation()
        {
            var sb = new StringBuilder();
            sb.AppendLine("CHF_Alert_LowChargeDesc".Translate());
            for (int i = 0; i < _targets.Count; i++)
            {
                if (!(_targets[i].Thing is Pawn pawn))
                    continue;
                sb.Append("  - ");
                sb.Append(_targetLabels[i]);
                sb.Append(": ");
                AppendLowHediffInfo(sb, pawn);
                if (i < _targets.Count - 1)
                    sb.AppendLine();
            }
            return sb.ToString();
        }

        private void AppendLowHediffInfo(StringBuilder sb, Pawn pawn)
        {
            bool first = true;
            foreach (HediffComp_Rechargeable rc in HediffChargeUtility.GetRechargeableComps(pawn))
            {
                if (rc.ChargePercent >= LowThreshold)
                    continue;
                if (!first)
                    sb.Append(", ");
                sb.Append(rc.parent.LabelCap);
                sb.Append(" (");
                sb.Append(rc.ChargePercent.ToStringPercent());
                sb.Append(")");
                first = false;
            }
        }

        public override AlertReport GetReport()
        {
            CalculateTargets();
            return AlertReport.CulpritsAre(_targets);
        }
    }
}
