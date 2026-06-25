using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Chargeable_Hediffs_Framework
{
    public static class HediffChargeUtility
    {
        public const float AutoRechargeThreshold = 0.2f;

        // Shared scratch list for allocation-free hot-path iteration. RimWorld is single-threaded;
        // do not hold a reference to this list across calls.
        private static readonly List<HediffComp_Rechargeable> s_tempComps = new List<HediffComp_Rechargeable>();

        // Station extension cache keyed by ThingDef; built lazily on first use after defs load.
        private static Dictionary<ThingDef, ChargeStationExtension> s_stationCache;

        // Parallel list of keys for allocation-free iteration in PotentialWorkThingsGlobal.
        private static List<ThingDef> s_stationDefs;

        // ── Cache comp helpers ────────────────────────────────────────────────────────────────────

        public static CompChargeConsequencesCache GetChargeCache(Pawn pawn) =>
            pawn?.GetComp<CompChargeConsequencesCache>();

        public static void InjectChargeCacheCompIntoRaceDefs()
        {
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def.category != ThingCategory.Pawn || def.race == null)
                    continue;
                if (def.HasComp<CompChargeConsequencesCache>())
                    continue;
                def.comps.Add(new CompProperties_ChargeConsequencesCache());
            }
        }

        // ── Station cache ─────────────────────────────────────────────────────────────────────────

        // Pre-warms the cache. Call from a mod startup hook if you want eager initialization;
        // otherwise the first GetStationExtension call triggers it automatically.
        public static void BuildStationCache()
        {
            s_stationCache = new Dictionary<ThingDef, ChargeStationExtension>();
            s_stationDefs = new List<ThingDef>();
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                ChargeStationExtension ext = def.GetModExtension<ChargeStationExtension>();
                if (ext != null)
                {
                    s_stationCache[def] = ext;
                    s_stationDefs.Add(def);
                }
            }
        }

        public static ChargeStationExtension GetStationExtension(ThingDef def)
        {
            if (s_stationCache == null)
                BuildStationCache();
            s_stationCache.TryGetValue(def, out ChargeStationExtension ext);
            return ext;
        }

        // Read-only list of every ThingDef that has a ChargeStationExtension.
        // Used by WorkGiver_RechargeHediffs to iterate spawned stations without allocating.
        public static IReadOnlyList<ThingDef> StationDefs
        {
            get
            {
                if (s_stationCache == null)
                    BuildStationCache();
                return s_stationDefs;
            }
        }

        // ── Eligibility helpers ───────────────────────────────────────────────────────────────────

        // Basic null/dead/hediff-set guard used by all query helpers.
        public static bool IsPawnEligible(Pawn pawn)
        {
            return pawn != null && !pawn.Dead && pawn.health?.hediffSet != null;
        }

        // True if the pawn is a player-owned colonist, slave, or mechanoid that can be assigned
        // auto-recharge work. Used by WorkGiver and Alert; not required for raw query helpers.
        public static bool IsAutoRechargeEligible(Pawn pawn)
        {
            if (!IsPawnEligible(pawn))
                return false;
            if (pawn.Faction == null || !pawn.Faction.IsPlayer)
                return false;
            if (pawn.IsPrisoner || pawn.RaceProps.Animal)
                return false;
            return true;
        }

        // True if the thing is a registered charge station (extension present), ignoring powered state.
        public static bool IsChargeStation(Thing thing)
        {
            return thing != null && !thing.Destroyed && GetStationExtension(thing.def) != null;
        }

        // True if the thing's power comp reports it is on. Returns false when no power comp exists.
        public static bool IsStationPowered(Thing thing)
        {
            if (thing == null || thing.Destroyed)
                return false;
            CompPowerTrader power = thing.TryGetComp<CompPowerTrader>();
            return power != null && power.PowerOn;
        }

        // True if the station is a powered, registered charge station that is currently on.
        public static bool IsValidStation(Thing station)
        {
            return IsChargeStation(station) && IsStationPowered(station);
        }

        // True if the station supports charging the given rechargeable comp.
        // Does not check powered state or whether the comp currently needs charge.
        public static bool CanStationCharge(Thing station, HediffComp_Rechargeable comp)
        {
            if (comp == null || !IsChargeStation(station))
                return false;
            return GetStationExtension(station.def).SupportsDef(comp.Def);
        }

        // Charge units per tick this station delivers. Returns 0 when the station is not registered.
        public static float GetChargeRatePerTick(Thing station)
        {
            if (station == null || station.Destroyed)
                return 0f;
            ChargeStationExtension ext = GetStationExtension(station.def);
            return ext != null ? ext.EffectiveChargeRate(station.def) : 0f;
        }

        // ── Core query helpers ────────────────────────────────────────────────────────────────────

        // Fills outComps with every HediffComp_Rechargeable on the pawn; clears the list first.
        // Returns the count filled. Zero allocation when the caller supplies a reusable list.
        public static int GetRechargeableComps(Pawn pawn, List<HediffComp_Rechargeable> outComps)
        {
            outComps.Clear();
            if (!IsPawnEligible(pawn))
                return 0;
            AddRechargeableCompsFrom(pawn, outComps);
            return outComps.Count;
        }

        // Allocating overload for non-hot-path callers such as UI or one-off checks.
        public static IEnumerable<HediffComp_Rechargeable> GetRechargeableComps(Pawn pawn)
        {
            if (!IsPawnEligible(pawn))
                yield break;
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (!(hediffs[i] is HediffWithComps hwc))
                    continue;
                for (int j = 0; j < hwc.comps.Count; j++)
                {
                    if (hwc.comps[j] is HediffComp_Rechargeable rc)
                        yield return rc;
                }
            }
        }

        public static bool HasRechargeableHediffs(Pawn pawn)
        {
            if (!IsPawnEligible(pawn))
                return false;
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (!(hediffs[i] is HediffWithComps hwc))
                    continue;
                for (int j = 0; j < hwc.comps.Count; j++)
                {
                    if (hwc.comps[j] is HediffComp_Rechargeable)
                        return true;
                }
            }
            return false;
        }

        // Sets percent to the lowest ChargePercent found. Returns false when the pawn has none.
        public static bool TryGetLowestChargePercent(Pawn pawn, out float percent)
        {
            percent = 1f;
            bool found = false;
            if (!IsPawnEligible(pawn))
                return false;
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (!(hediffs[i] is HediffWithComps hwc))
                    continue;
                for (int j = 0; j < hwc.comps.Count; j++)
                {
                    if (!(hwc.comps[j] is HediffComp_Rechargeable rc))
                        continue;
                    float p = rc.ChargePercent;
                    if (!found || p < percent)
                    {
                        percent = p;
                        found = true;
                    }
                }
            }
            return found;
        }

        private static void AddRechargeableCompsFrom(Pawn pawn, List<HediffComp_Rechargeable> outComps)
        {
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i] is HediffWithComps hwc)
                {
                    for (int j = 0; j < hwc.comps.Count; j++)
                    {
                        if (hwc.comps[j] is HediffComp_Rechargeable rc)
                            outComps.Add(rc);
                    }
                }
            }
        }

        // True if any rechargeable hediff is below the given threshold (default 20 %).
        public static bool NeedsRecharge(Pawn pawn, float threshold = 0.2f)
        {
            return TryGetLowestChargePercent(pawn, out float pct) && pct < threshold;
        }

        // True if the pawn has at least one rechargeable hediff the station supports that still needs charge.
        // Does not check powered state; use FailOn(IsValidStation) separately for power.
        public static bool HasChargeableNeedingCharge(Pawn pawn, Thing station)
        {
            if (!IsPawnEligible(pawn) || !IsChargeStation(station))
                return false;
            ChargeStationExtension ext = GetStationExtension(station.def);
            GetRechargeableComps(pawn, s_tempComps);
            for (int i = 0; i < s_tempComps.Count; i++)
            {
                HediffComp_Rechargeable rc = s_tempComps[i];
                if (ext.SupportsDef(rc.Def) && rc.NeedsCharge)
                    return true;
            }
            return false;
        }

        // True if the pawn has at least one rechargeable hediff the given station can service.
        public static bool HasChargeableByStation(Pawn pawn, Thing station)
        {
            if (!IsPawnEligible(pawn) || !IsValidStation(station))
                return false;
            ChargeStationExtension ext = GetStationExtension(station.def);
            GetRechargeableComps(pawn, s_tempComps);
            for (int i = 0; i < s_tempComps.Count; i++)
            {
                if (ext.SupportsDef(s_tempComps[i].Def))
                    return true;
            }
            return false;
        }

        // Charges every eligible hediff on the pawn from this station for delta ticks.
        // Returns the number of hediff comps that received charge.
        public static int ChargeAllFromStation(Pawn pawn, Thing station, int delta)
        {
            if (!IsPawnEligible(pawn) || !IsValidStation(station))
                return 0;
            ChargeStationExtension ext = GetStationExtension(station.def);
            float rate = ext.EffectiveChargeRate(station.def) * delta;
            if (rate <= 0f)
                return 0;
            GetRechargeableComps(pawn, s_tempComps);
            int count = 0;
            for (int i = 0; i < s_tempComps.Count; i++)
            {
                HediffComp_Rechargeable rc = s_tempComps[i];
                if (!ext.SupportsDef(rc.Def) || !rc.NeedsCharge)
                    continue;
                rc.AddCharge(rate);
                count++;
            }
            return count;
        }

        // Mean charge percent across rechargeable hediffs eligible for the given station.
        // Pass null for station to include all rechargeable hediffs regardless of station support.
        public static float AggregateChargePercent(Pawn pawn, Thing station = null)
        {
            if (!IsPawnEligible(pawn))
                return 0f;
            ChargeStationExtension ext = station != null ? GetStationExtension(station.def) : null;
            GetRechargeableComps(pawn, s_tempComps);
            float sum = 0f;
            int count = 0;
            for (int i = 0; i < s_tempComps.Count; i++)
            {
                HediffComp_Rechargeable rc = s_tempComps[i];
                if (ext != null && !ext.SupportsDef(rc.Def))
                    continue;
                sum += rc.ChargePercent;
                count++;
            }
            return count > 0 ? sum / count : 0f;
        }
    }
}
