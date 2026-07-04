using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;

namespace Chargeable_Hediffs_Framework
{
    public class CompWirelessCharge : ThingComp
    {
        private CompPowerTrader powerComp;
        private int ticksUntilScan;

        // Stats from the most recent scan, for inspect text only.
        private int lastPawnsCharged;
        private int lastHediffsCharged;

        public CompProperties_WirelessCharge Props => (CompProperties_WirelessCharge)props;

        private bool PowerOn => powerComp != null && powerComp.PowerOn;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            powerComp = parent.GetComp<CompPowerTrader>();
            if (!respawningAfterLoad)
                ticksUntilScan = Props.scanIntervalTicks;
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref ticksUntilScan, "ticksUntilScan", Props.scanIntervalTicks);
        }

        public override void CompTickInterval(int delta)
        {
            if (!PowerOn)
                return;
            ticksUntilScan -= delta;
            if (ticksUntilScan > 0)
                return;
            int elapsed = Props.scanIntervalTicks - ticksUntilScan;
            ticksUntilScan = Props.scanIntervalTicks;
            ChargeNearby(elapsed);
        }

        private void ChargeNearby(int elapsedTicks)
        {
            Map map = parent.Map;
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            int pawnsCharged = 0;
            int hediffsCharged = 0;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (!pawn.Position.InHorDistOf(parent.Position, Props.radius))
                    continue;
                if (!HediffChargeUtility.IsAutoRechargeEligible(pawn))
                    continue;

                int charged = HediffChargeUtility.ChargeAllFromStation(pawn, parent, elapsedTicks);
                if (charged <= 0)
                    continue;
                pawnsCharged++;
                hediffsCharged += charged;
            }

            lastPawnsCharged = pawnsCharged;
            lastHediffsCharged = hediffsCharged;
        }

        public override string CompInspectStringExtra()
        {
            if (!Props.showInspectString)
                return null;

            var sb = new StringBuilder();
            sb.Append("CHF_WirelessChargeRadius".Translate());
            sb.Append(": ");
            sb.Append(Props.radius.ToString("F1"));
            sb.AppendLine();
            sb.Append("CHF_WirelessChargeInterval".Translate());
            sb.Append(": ");
            sb.Append(Props.scanIntervalTicks);
            sb.Append(" ");
            sb.Append("CHF_Ticks".Translate());

            if (PowerOn)
            {
                sb.AppendLine();
                sb.Append("CHF_WirelessChargeLastResult".Translate());
                sb.Append(": ");
                sb.Append(lastPawnsCharged);
                sb.Append(" ");
                sb.Append("CHF_WirelessChargeTargets".Translate());
                sb.Append(", ");
                sb.Append(lastHediffsCharged);
                sb.Append(" ");
                sb.Append("CHF_WirelessChargeHediffs".Translate());
            }

            return sb.ToString();
        }

        public override void PostDrawExtraSelectionOverlays()
        {
            if (Props.drawRadius)
                GenDraw.DrawRadiusRing(parent.Position, Props.radius);
        }
    }
}
