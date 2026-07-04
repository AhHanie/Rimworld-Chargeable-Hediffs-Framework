using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Chargeable_Hediffs_Framework
{
    public class CompProperties_WirelessCharge : CompProperties
    {
        // Cells around the parent building that are scanned for eligible pawns.
        public float radius = 6.9f;

        // How often the comp scans for pawns to charge. 250 ticks is 1 in-game hour.
        public int scanIntervalTicks = 250;

        // Controls the selected radius overlay.
        public bool drawRadius = true;

        // Controls the inspect string lines this comp adds.
        public bool showInspectString = true;

        public CompProperties_WirelessCharge()
        {
            compClass = typeof(CompWirelessCharge);
        }

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string err in base.ConfigErrors(parentDef))
                yield return err;

            if (parentDef.GetCompProperties<CompProperties_Power>() == null)
                yield return $"{nameof(CompProperties_WirelessCharge)}: '{parentDef.defName}' must have CompProperties_Power";
            if (parentDef.GetModExtension<ChargeStationExtension>() == null)
                yield return $"{nameof(CompProperties_WirelessCharge)}: '{parentDef.defName}' must have {nameof(ChargeStationExtension)}";
            if (radius <= 0f)
                yield return $"{nameof(radius)} must be positive (got {radius})";
            else if (radius >= GenRadial.MaxRadialPatternRadius)
                yield return $"{nameof(radius)} {radius} on '{parentDef.defName}' is >= GenRadial.MaxRadialPatternRadius ({GenRadial.MaxRadialPatternRadius}); the radius ring cannot be drawn";
            if (scanIntervalTicks <= 0)
                yield return $"{nameof(scanIntervalTicks)} must be positive (got {scanIntervalTicks})";
        }
    }
}
