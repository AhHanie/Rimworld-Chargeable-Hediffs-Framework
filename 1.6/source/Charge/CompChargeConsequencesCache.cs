using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace Chargeable_Hediffs_Framework
{
    public class CompChargeConsequencesCache : ThingComp
    {
        private struct CachedEntry
        {
            public BodyPartRecord part;
            public List<StatModifier> statOffsets;
            public List<StatModifier> statFactors;
            public float partEfficiencyOffset;
        }

        private readonly Dictionary<Hediff, CachedEntry> activeByHediff =
            new Dictionary<Hediff, CachedEntry>();

        private readonly Dictionary<StatDef, float> cachedStatOffsets =
            new Dictionary<StatDef, float>();

        private readonly Dictionary<StatDef, float> cachedStatFactors =
            new Dictionary<StatDef, float>();

        private readonly Dictionary<BodyPartRecord, float> partEfficiencyOffsets =
            new Dictionary<BodyPartRecord, float>();

        private readonly Dictionary<BodyPartRecord, List<Hediff>> partEfficiencySources =
            new Dictionary<BodyPartRecord, List<Hediff>>();

        private bool dirty;

        public Pawn Pawn => parent as Pawn;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            if (dirty)
                RebuildFromPawnHediffs();
        }

        public override void PostExposeData()
        {
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                dirty = true;
        }

        public override float GetStatOffset(StatDef stat)
        {
            if (dirty)
                RebuildFromPawnHediffs();
            return cachedStatOffsets.TryGetValue(stat, out float value) ? value : 0f;
        }

        public override float GetStatFactor(StatDef stat)
        {
            if (dirty)
                RebuildFromPawnHediffs();
            return cachedStatFactors.TryGetValue(stat, out float value) ? value : 1f;
        }

        public override void GetStatsExplanation(StatDef stat, StringBuilder sb, string whitespace = "")
        {
            if (dirty)
                RebuildFromPawnHediffs();

            StringBuilder lines = null;
            foreach (KeyValuePair<Hediff, CachedEntry> pair in activeByHediff)
            {
                AppendStatExplanationLines(stat, pair.Key, pair.Value, ref lines, whitespace);
            }

            if (lines == null || lines.Length == 0)
                return;

            sb.AppendLine(whitespace + "CHF_StatsReport_DepletedCharge".Translate() + ":");
            sb.Append(lines.ToString());
        }

        public void RegisterDepleted(HediffComp_Rechargeable comp)
        {
            if (comp?.parent == null || Pawn == null)
                return;
            if (!comp.IsDepleted)
                return;
            ChargeConsequenceProperties cons = comp.Props.depletedConsequences;
            if (cons == null)
                return;

            bool hasOffsets = cons.statOffsets.Count > 0;
            bool hasFactors = cons.statFactors.Count > 0;
            bool hasPartEff = cons.partEfficiencyOffset != 0f;
            if (!hasOffsets && !hasFactors && !hasPartEff)
                return;

            Hediff hediff = comp.parent;

            // Remove stale entry first to prevent duplicate aggregation
            if (activeByHediff.ContainsKey(hediff))
                Unregister(comp);

            activeByHediff[hediff] = new CachedEntry
            {
                part = hediff.Part,
                statOffsets = cons.statOffsets,
                statFactors = cons.statFactors,
                partEfficiencyOffset = cons.partEfficiencyOffset
            };

            for (int i = 0; i < cons.statOffsets.Count; i++)
            {
                StatModifier sm = cons.statOffsets[i];
                if (sm.stat == null) continue;
                cachedStatOffsets[sm.stat] = cachedStatOffsets.TryGetValue(sm.stat, out float cur)
                    ? cur + sm.value : sm.value;
            }

            for (int i = 0; i < cons.statFactors.Count; i++)
            {
                StatModifier sm = cons.statFactors[i];
                if (sm.stat == null) continue;
                cachedStatFactors[sm.stat] = cachedStatFactors.TryGetValue(sm.stat, out float cur)
                    ? cur * sm.value : sm.value;
            }

            BodyPartRecord part = hediff.Part;
            if (part != null && cons.partEfficiencyOffset != 0f)
            {
                partEfficiencyOffsets[part] = partEfficiencyOffsets.TryGetValue(part, out float cur)
                    ? cur + cons.partEfficiencyOffset : cons.partEfficiencyOffset;

                if (!partEfficiencySources.TryGetValue(part, out List<Hediff> sources))
                {
                    sources = new List<Hediff>();
                    partEfficiencySources[part] = sources;
                }
                sources.Add(hediff);

                NotifyCapacityDirty();
            }
        }

        public void Unregister(HediffComp_Rechargeable comp)
        {
            if (comp?.parent == null)
                return;

            Hediff hediff = comp.parent;
            if (!activeByHediff.TryGetValue(hediff, out CachedEntry cached))
                return;

            activeByHediff.Remove(hediff);

            for (int i = 0; i < cached.statOffsets.Count; i++)
            {
                StatModifier sm = cached.statOffsets[i];
                if (sm.stat == null) continue;
                if (!cachedStatOffsets.TryGetValue(sm.stat, out float cur)) continue;
                float newVal = cur - sm.value;
                if (Mathf.Abs(newVal) < 1e-5f)
                    cachedStatOffsets.Remove(sm.stat);
                else
                    cachedStatOffsets[sm.stat] = newVal;
            }

            for (int i = 0; i < cached.statFactors.Count; i++)
            {
                StatModifier sm = cached.statFactors[i];
                if (sm.stat == null) continue;
                if (!cachedStatFactors.TryGetValue(sm.stat, out float cur)) continue;

                if (Mathf.Abs(sm.value) < 1e-5f)
                {
                    // Zero factor cannot be divided out safely; full rebuild instead
                    RebuildFromPawnHediffs();
                    return;
                }
                float newVal = cur / sm.value;
                if (Mathf.Abs(newVal - 1f) < 1e-5f)
                    cachedStatFactors.Remove(sm.stat);
                else
                    cachedStatFactors[sm.stat] = newVal;
            }

            BodyPartRecord part = cached.part;
            if (part != null && cached.partEfficiencyOffset != 0f)
            {
                if (partEfficiencyOffsets.TryGetValue(part, out float cur))
                {
                    float newVal = cur - cached.partEfficiencyOffset;
                    if (Mathf.Abs(newVal) < 1e-5f)
                        partEfficiencyOffsets.Remove(part);
                    else
                        partEfficiencyOffsets[part] = newVal;
                }

                if (partEfficiencySources.TryGetValue(part, out List<Hediff> sources))
                {
                    sources.Remove(hediff);
                    if (sources.Count == 0)
                        partEfficiencySources.Remove(part);
                }

                NotifyCapacityDirty();
            }
        }

        public bool TryGetPartEfficiencyOffset(BodyPartRecord part, out float offset)
        {
            if (dirty)
                RebuildFromPawnHediffs();
            if (!partEfficiencyOffsets.TryGetValue(part, out offset))
                return false;
            if (Mathf.Abs(offset) < 1e-5f)
            {
                offset = 0f;
                return false;
            }
            return true;
        }

        public bool TryGetPartEfficiencyImpactors(BodyPartRecord part, List<PawnCapacityUtility.CapacityImpactor> impactors)
        {
            if (impactors == null)
                return false;
            if (!partEfficiencySources.TryGetValue(part, out List<Hediff> sources))
                return false;

            Pawn pawn = Pawn;
            bool added = false;
            for (int i = 0; i < sources.Count; i++)
            {
                Hediff source = sources[i];
                if (pawn != null && !pawn.health.hediffSet.hediffs.Contains(source))
                    continue;
                impactors.Add(new PawnCapacityUtility.CapacityImpactorHediff { hediff = source });
                added = true;
            }
            return added;
        }

        public bool HasPartEfficiencyOffset(BodyPartRecord part)
        {
            if (dirty)
                RebuildFromPawnHediffs();
            return partEfficiencyOffsets.ContainsKey(part);
        }

        public void MarkDirty() => dirty = true;

        public void RebuildFromPawnHediffs()
        {
            activeByHediff.Clear();
            cachedStatOffsets.Clear();
            cachedStatFactors.Clear();
            partEfficiencyOffsets.Clear();
            foreach (List<Hediff> list in partEfficiencySources.Values)
                list.Clear();
            partEfficiencySources.Clear();
            // Mark clean before the loop so re-entrant stat queries do not recurse
            dirty = false;

            Pawn pawn = Pawn;
            if (!HediffChargeUtility.IsPawnEligible(pawn))
                return;

            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (!(hediffs[i] is HediffWithComps hwc))
                    continue;
                for (int j = 0; j < hwc.comps.Count; j++)
                {
                    if (hwc.comps[j] is HediffComp_Rechargeable rc && rc.IsDepleted)
                        RegisterDepleted(rc);
                }
            }
        }

        private static void AppendStatExplanationLines(
            StatDef stat,
            Hediff hediff,
            CachedEntry cached,
            ref StringBuilder lines,
            string whitespace)
        {
            for (int i = 0; i < cached.statOffsets.Count; i++)
            {
                StatModifier sm = cached.statOffsets[i];
                if (sm.stat == null || sm.stat != stat) continue;
                if (Mathf.Approximately(sm.value, 0f)) continue;
                AppendStatExplanationLine(stat, hediff, cached.part, sm.value, ToStringNumberSense.Offset, ref lines, whitespace);
            }

            for (int i = 0; i < cached.statFactors.Count; i++)
            {
                StatModifier sm = cached.statFactors[i];
                if (sm.stat == null || sm.stat != stat) continue;
                if (Mathf.Approximately(sm.value, 1f)) continue;
                AppendStatExplanationLine(stat, hediff, cached.part, sm.value, ToStringNumberSense.Factor, ref lines, whitespace);
            }
        }

        private static void AppendStatExplanationLine(
            StatDef stat,
            Hediff hediff,
            BodyPartRecord part,
            float value,
            ToStringNumberSense sense,
            ref StringBuilder lines,
            string whitespace)
        {
            if (lines == null)
                lines = new StringBuilder();

            lines.AppendLine(whitespace + "    " + SourceLabel(hediff, part) + ": "
                + stat.Worker.ValueToString(value, finalized: false, sense));
        }

        private static string SourceLabel(Hediff hediff, BodyPartRecord part)
        {
            string label = hediff != null ? hediff.LabelBaseCap : "Unknown".Translate().ToString();
            if (part != null)
                return label + " (" + part.LabelCap + ")";
            return label;
        }

        private void NotifyCapacityDirty()
        {
            Pawn?.health?.capacities?.Notify_CapacityLevelsDirty();
        }
    }
}
