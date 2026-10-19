using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Chargeable_Hediffs_Framework
{
    // Worn utility battery. The wearer walks up to another pawn and recharges its rechargeable
    // hediffs. Reuses the vanilla reloadable apparel comp for the saved integer use count, but
    // needs no verb: the comp builds its own targeting gizmo and issues an ordered job.
    // The item must be nonstacking apparel; see Docs/ApparelChargePack.md.
    public class CompProperties_ApparelChargeBattery : CompProperties_ApparelReloadable
    {
        // Maximum hediff charge delivered at the end of one successful job, summed across all
        // target hediffs. maxCharges (inherited) is the number of jobs the pack can complete.
        public float chargePerUse;

        // Length of the adjacent service step and its progress bar. 240 ticks is about four
        // seconds at normal speed.
        public int rechargeDurationTicks = 240;

        // Optional whitelist of recipient HediffDefs. Null/empty means any eligible hediff.
        public List<HediffDef> supportedHediffs;

        // Optional blacklist of recipient HediffDefs. Takes priority over supportedHediffs.
        public List<HediffDef> excludedHediffs;

        // Optional gizmo icon. Defaults to the item's own icon.
        [NoTranslate]
        public string commandIconPath;

        [Unsaved]
        private Texture2D commandIcon;

        public Texture2D CommandIcon
        {
            get
            {
                if (commandIcon == null && !commandIconPath.NullOrEmpty())
                    commandIcon = ContentFinder<Texture2D>.Get(commandIconPath);
                return commandIcon;
            }
        }

        public CompProperties_ApparelChargeBattery()
        {
            compClass = typeof(CompApparelChargeBattery);
        }

        public bool SupportsHediff(HediffDef def)
        {
            if (excludedHediffs != null && excludedHediffs.Contains(def))
                return false;
            return supportedHediffs == null || supportedHediffs.Count == 0 || supportedHediffs.Contains(def);
        }

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string err in base.ConfigErrors(parentDef))
                yield return err;

            string owner = $"{nameof(CompProperties_ApparelChargeBattery)} on '{parentDef.defName}'";

            if (maxCharges <= 0)
                yield return $"{owner}: maxCharges must be positive (got {maxCharges})";
            if (!(chargePerUse > 0f) || float.IsInfinity(chargePerUse))
                yield return $"{owner}: {nameof(chargePerUse)} must be a positive finite number (got {chargePerUse})";
            if (rechargeDurationTicks <= 0)
                yield return $"{owner}: {nameof(rechargeDurationTicks)} must be positive (got {rechargeDurationTicks})";

            if (ammoDef != null)
                yield return $"{owner}: ammoDef is not supported; refill happens at a charge station";
            if (replenishAfterCooldown)
                yield return $"{owner}: replenishAfterCooldown is not supported";
            if (destroyOnEmpty)
                yield return $"{owner}: destroyOnEmpty is not supported";

            if (parentDef.Verbs.Count > 0)
                yield return $"{owner}: must not declare verbs; the comp creates its own targeting gizmo";
            if (parentDef.stackLimit > 1)
                yield return $"{owner}: must be nonstacking (stackLimit 1) so each pack keeps its own use count";

            if (parentDef.comps != null)
            {
                int packComps = 0;
                for (int i = 0; i < parentDef.comps.Count; i++)
                {
                    if (parentDef.comps[i] is CompProperties_ApparelChargeBattery)
                        packComps++;
                }
                if (packComps > 1)
                    yield return $"{owner}: only one {nameof(CompProperties_ApparelChargeBattery)} is allowed per item";
            }

            foreach (string err in NullEntryErrors(supportedHediffs, nameof(supportedHediffs), owner))
                yield return err;
            foreach (string err in NullEntryErrors(excludedHediffs, nameof(excludedHediffs), owner))
                yield return err;
        }

        private static IEnumerable<string> NullEntryErrors(List<HediffDef> list, string fieldName, string owner)
        {
            if (list == null)
                yield break;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null)
                    yield return $"{owner}: null entry in {fieldName} at index {i} — check that all HediffDef names are spelled correctly";
            }
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
        {
            foreach (StatDrawEntry entry in base.SpecialDisplayStats(req))
                yield return entry;

            yield return new StatDrawEntry(StatCategoryDefOf.Apparel,
                "CHF_Stat_PackChargePerUse_Name".Translate(),
                chargePerUse.ToString("F1"),
                "CHF_Stat_PackChargePerUse_Desc".Translate(ChargeNounArgument),
                2748);
            yield return new StatDrawEntry(StatCategoryDefOf.Apparel,
                "CHF_Stat_PackDuration_Name".Translate(),
                rechargeDurationTicks.ToStringTicksToPeriod(),
                "CHF_Stat_PackDuration_Desc".Translate(),
                2747);
        }
    }
}
