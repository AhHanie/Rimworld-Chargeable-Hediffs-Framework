using UnityEngine;
using Verse;

namespace Chargeable_Hediffs_Framework
{
    public class PlaceWorker_ShowWirelessChargeRadius : PlaceWorker
    {
        public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
        {
            if (thing != null)
                return;
            CompProperties_WirelessCharge props = def.GetCompProperties<CompProperties_WirelessCharge>();
            if (props != null)
                GenDraw.DrawRadiusRing(center, props.radius);
        }
    }
}
