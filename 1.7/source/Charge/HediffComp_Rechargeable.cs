using System.Collections.Generic;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Chargeable_Hediffs_Framework
{
    public class HediffComp_Rechargeable : HediffComp
    {
        private float currentCharge;

        public HediffCompProperties_Rechargeable Props => (HediffCompProperties_Rechargeable)props;

        public float MaxCharge => Props.maxCharge;

        public float CurrentCharge => currentCharge;

        public float ChargePercent => Props.maxCharge > 0f ? currentCharge / Props.maxCharge : 0f;

        public bool IsDepleted => currentCharge <= 0f;

        public bool NeedsCharge => currentCharge < Props.maxCharge;

        // Decay only while alive and on a map or in a caravan.
        public bool CanDecayNow => !Pawn.Dead && (Pawn.Spawned || Pawn.GetCaravan() != null);

        // Shows "74%" in the Health tab label: "Bionic Arm (74%)".
        public override string CompLabelInBracketsExtra => ChargePercent.ToStringPercent();

        // Shown in the hediff tooltip.
        public override string CompTipStringExtra => GetChargeInspectString();

        public override void CompPostMake()
        {
            base.CompPostMake();
            float start = Props.startingCharge >= 0f ? Props.startingCharge : Props.maxCharge;
            currentCharge = Mathf.Clamp(start, 0f, Props.maxCharge);
        }

        public override void CompExposeData()
        {
            Scribe_Values.Look(ref currentCharge, "currentCharge", Props.maxCharge);
        }

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            if (!IsDepleted)
                return;
            Pawn pawn = Pawn;
            if (pawn.Dead)
                return;
            HediffChargeUtility.GetChargeCache(pawn)?.RegisterDepleted(this);
        }

        public override void CompPostPostRemoved()
        {
            HediffChargeUtility.GetChargeCache(Pawn)?.Unregister(this);
        }

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            if (CanDecayNow && Props.chargeDecayPerTick > 0f)
                DrainCharge(Props.chargeDecayPerTick * delta);
        }

        public void AddCharge(float amount)
        {
            bool wasDepleted = IsDepleted;
            currentCharge = Mathf.Clamp(currentCharge + amount, 0f, Props.maxCharge);
            if (wasDepleted && !IsDepleted)
                NotifyDepletionChanged(nowDepleted: false);
        }

        public void DrainCharge(float amount)
        {
            bool wasDepleted = IsDepleted;
            currentCharge = Mathf.Clamp(currentCharge - amount, 0f, Props.maxCharge);
            if (!wasDepleted && IsDepleted)
                NotifyDepletionChanged(nowDepleted: true);
        }

        public void SetCharge(float amount)
        {
            bool wasDepleted = IsDepleted;
            currentCharge = Mathf.Clamp(amount, 0f, Props.maxCharge);
            if (wasDepleted != IsDepleted)
                NotifyDepletionChanged(nowDepleted: IsDepleted);
        }

        private void NotifyDepletionChanged(bool nowDepleted)
        {
            Pawn pawn = Pawn;
            if (pawn.Dead)
                return;

            CompChargeConsequencesCache cache = HediffChargeUtility.GetChargeCache(pawn);
            if (cache != null)
            {
                if (nowDepleted)
                    cache.RegisterDepleted(this);
                else
                    cache.Unregister(this);
            }

            pawn.health.Notify_HediffChanged(parent);
            pawn.health.capacities?.Notify_CapacityLevelsDirty();
        }

        // Reusable inspect text for callers that need charge info in inspect strings.
        public string GetChargeInspectString()
        {
            var sb = new StringBuilder();
            sb.Append("CHF_Charge".Translate());
            sb.Append(": ");
            sb.Append(currentCharge.ToString("F1"));
            sb.Append(" / ");
            sb.Append(Props.maxCharge.ToString("F1"));
            sb.Append(" (");
            sb.Append(ChargePercent.ToStringPercent());
            sb.Append(")");
            if (Props.chargeDecayPerTick > 0f)
            {
                sb.AppendLine();
                sb.Append("CHF_DecayRate".Translate());
                sb.Append(": ");
                sb.Append((Props.chargeDecayPerTick * GenDate.TicksPerDay).ToString("F1"));
                sb.Append(" / ");
                sb.Append("CHF_PerDay".Translate());
            }
            if (IsDepleted)
            {
                sb.AppendLine();
                sb.Append("CHF_ChargeDepleted".Translate().Colorize(ColoredText.WarningColor));
                AppendDepletedConsequencesTo(sb);
            }
            return sb.ToString();
        }

        private void AppendDepletedConsequencesTo(StringBuilder sb)
        {
            ChargeConsequenceProperties cons = Props.depletedConsequences;
            if (cons == null)
                return;
            bool hasEffects = cons.statOffsets.Count > 0
                           || cons.statFactors.Count > 0
                           || cons.partEfficiencyOffset != 0f;
            if (!hasEffects)
                return;

            sb.AppendLine();
            sb.AppendLine("CHF_DepletedEffects".Translate());

            for (int i = 0; i < cons.statOffsets.Count; i++)
            {
                StatModifier sm = cons.statOffsets[i];
                sb.AppendLine("  " + sm.stat.LabelCap + ": "
                    + sm.stat.Worker.ValueToString(sm.value, finalized: false, ToStringNumberSense.Offset));
            }

            for (int i = 0; i < cons.statFactors.Count; i++)
            {
                StatModifier sm = cons.statFactors[i];
                sb.AppendLine("  " + sm.stat.LabelCap + ": "
                    + sm.stat.Worker.ValueToString(sm.value, finalized: false, ToStringNumberSense.Factor));
            }

            if (cons.partEfficiencyOffset != 0f)
            {
                sb.Append("  " + "CHF_PartEfficiency".Translate() + ": "
                    + cons.partEfficiencyOffset.ToStringPercent());
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmos()
        {
            if (!DebugSettings.ShowDevGizmos)
                yield break;

            yield return new Command_Action
            {
                defaultLabel = "CHF_DebugSetChargeZero".Translate(parent.LabelCap),
                defaultDesc = "CHF_DebugSetChargeZeroDesc".Translate(parent.LabelCap, CurrentCharge.ToString("F1"), MaxCharge.ToString("F1")),
                action = delegate
                {
                    SetCharge(0f);
                }
            };

            yield return new Command_Action
            {
                defaultLabel = "CHF_DebugSetChargeMax".Translate(parent.LabelCap),
                defaultDesc = "CHF_DebugSetChargeMaxDesc".Translate(parent.LabelCap, CurrentCharge.ToString("F1"), MaxCharge.ToString("F1")),
                action = delegate
                {
                    SetCharge(MaxCharge);
                }
            };

            yield return new Command_Action
            {
                defaultLabel = "CHF_DebugDrainChargeTen".Translate(parent.LabelCap),
                defaultDesc = "CHF_DebugDrainChargeTenDesc".Translate(parent.LabelCap, CurrentCharge.ToString("F1"), MaxCharge.ToString("F1")),
                action = delegate
                {
                    DrainCharge(Props.maxCharge * 0.1f);
                }
            };
        }

        public override string CompDebugString()
        {
            return $"charge: {currentCharge:F3} / {Props.maxCharge:F3}  ({ChargePercent:P0})  depleted={IsDepleted}";
        }
    }
}
