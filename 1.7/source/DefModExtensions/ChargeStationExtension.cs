using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Chargeable_Hediffs_Framework
{
    public class ChargeStationExtension : DefModExtension
    {
        // Charge units added per tick to each eligible hediff while a pawn occupies this station.
        // When zero, falls back to CompProperties_Power.PowerConsumption / 60000 so modders can
        // set one number in XML and let charge rate match power draw (1 day of power = 1 day of charge).
        public float chargeRate = 0f;

        // Restrict which HediffDefs this station can service. Empty/null means all rechargeable hediffs.
        public List<HediffDef> supportedHediffs;

        // Complete charge pack uses added per tick to each supported worn CompApparelChargeBattery.
        // Zero (default) means this station never refills packs. May be fractional; the pack
        // accumulates the remainder.
        public float apparelChargeRate = 0f;

        // Restrict which pack ThingDefs this station refills. Empty/null means any pack when
        // apparelChargeRate is positive.
        public List<ThingDef> supportedBatteryApparel;

        // Cached by ResolveReferences for use in ConfigErrors.
        private ThingDef parentDef;

        public float EffectiveChargeRate(ThingDef def)
        {
            if (chargeRate > 0f)
                return chargeRate;
            CompProperties_Power power = def?.GetCompProperties<CompProperties_Power>();
            return power != null ? power.PowerConsumption / 60000f : 0f;
        }

        public bool SupportsDef(HediffDef hediffDef)
        {
            return supportedHediffs == null || supportedHediffs.Count == 0 || supportedHediffs.Contains(hediffDef);
        }

        public bool ChargesApparel => apparelChargeRate > 0f;

        public bool SupportsApparel(ThingDef apparelDef)
        {
            return ChargesApparel
                && (supportedBatteryApparel == null || supportedBatteryApparel.Count == 0 || supportedBatteryApparel.Contains(apparelDef));
        }

        public override void ResolveReferences(Def def)
        {
            base.ResolveReferences(def);
            parentDef = def as ThingDef;
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string err in base.ConfigErrors())
                yield return err;

            if (parentDef == null)
            {
                yield return $"{nameof(ChargeStationExtension)} may only be added to ThingDefs";
                yield break;
            }
            if (parentDef.GetCompProperties<CompProperties_Power>() == null)
                yield return $"{nameof(ChargeStationExtension)}: '{parentDef.defName}' must have CompProperties_Power";
            if (chargeRate < 0f)
                yield return $"{nameof(chargeRate)} must not be negative (got {chargeRate})";
            if (supportedHediffs != null)
            {
                for (int i = 0; i < supportedHediffs.Count; i++)
                {
                    if (supportedHediffs[i] == null)
                        yield return $"{nameof(ChargeStationExtension)}: '{parentDef.defName}' has a null entry in supportedHediffs at index {i} — check that all HediffDef names are spelled correctly";
                }
            }
            if (float.IsNaN(apparelChargeRate) || float.IsInfinity(apparelChargeRate) || apparelChargeRate < 0f)
                yield return $"{nameof(apparelChargeRate)} must be a finite, nonnegative number (got {apparelChargeRate})";
            if (supportedBatteryApparel != null)
            {
                for (int i = 0; i < supportedBatteryApparel.Count; i++)
                {
                    ThingDef apparelDef = supportedBatteryApparel[i];
                    if (apparelDef == null)
                        yield return $"{nameof(ChargeStationExtension)}: '{parentDef.defName}' has a null entry in supportedBatteryApparel at index {i} — check that all ThingDef names are spelled correctly";
                    else if (apparelDef.GetCompProperties<CompProperties_ApparelChargeBattery>() == null)
                        yield return $"{nameof(ChargeStationExtension)}: '{parentDef.defName}' lists '{apparelDef.defName}' in supportedBatteryApparel, but it has no {nameof(CompProperties_ApparelChargeBattery)}";
                }
            }
        }
    }
}
