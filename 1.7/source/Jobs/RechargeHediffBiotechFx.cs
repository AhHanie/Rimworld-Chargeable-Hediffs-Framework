using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Chargeable_Hediffs_Framework
{
    internal sealed class RechargeHediffBiotechFx
    {
        private Sustainer chargingSustainer;
        private Mote pawnGlowMote;
        private Mote cablePulseMote;

        public void Start(Pawn pawn, Thing station)
        {
            if (!CanUse(pawn, station))
                return;

            SoundDefOf.MechChargerStart.PlayOneShot(SoundInfo.InMap(station));
        }

        public void Maintain(Pawn pawn, Thing station)
        {
            if (!CanUse(pawn, station))
            {
                End();
                return;
            }

            if (chargingSustainer == null || chargingSustainer.Ended)
                chargingSustainer = SoundDefOf.MechChargerCharging.TrySpawnSustainer(SoundInfo.InMap(station, MaintenanceType.PerTick));
            chargingSustainer?.Maintain();

            if (pawnGlowMote == null || pawnGlowMote.Destroyed)
                pawnGlowMote = MoteMaker.MakeAttachedOverlay(pawn, ThingDefOf.Mote_MechCharging, Vector3.zero);
            pawnGlowMote?.Maintain();

            if (ThingDefOf.Mote_ChargingCablesPulse != null && (cablePulseMote == null || cablePulseMote.Destroyed))
                cablePulseMote = MoteMaker.MakeInteractionOverlay(ThingDefOf.Mote_ChargingCablesPulse, station, pawn);
            cablePulseMote?.Maintain();
        }

        public void End()
        {
            chargingSustainer?.End();
            chargingSustainer = null;
            pawnGlowMote = null;
            cablePulseMote = null;
        }

        private static bool CanUse(Pawn pawn, Thing station)
        {
            return ModsConfig.BiotechActive
                && pawn != null
                && station != null
                && !pawn.Destroyed
                && !station.Destroyed
                && pawn.Spawned
                && station.Spawned
                && pawn.Map == station.Map
                && SoundDefOf.MechChargerStart != null
                && SoundDefOf.MechChargerCharging != null
                && ThingDefOf.Mote_MechCharging != null;
        }
    }
}
