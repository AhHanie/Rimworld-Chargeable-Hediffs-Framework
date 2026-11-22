using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace Chargeable_Hediffs_Framework
{
    public class CompChargeConsequencesCache : ThingComp
    {
        private sealed class CachedEntry
        {
            public readonly HediffComp_Rechargeable Comp;
            public readonly BodyPartRecord part;
            public readonly List<StatModifier> statOffsets;
            public readonly List<StatModifier> statFactors;
            public readonly float partEfficiencyOffset;
            public bool Active;

            public CachedEntry(HediffComp_Rechargeable comp, ChargeConsequenceProperties effects)
            {
                Comp = comp;
                part = comp.parent.Part;
                statOffsets = effects.statOffsets;
                statFactors = effects.statFactors;
                partEfficiencyOffset = part != null ? effects.partEfficiencyOffset : 0f;
                Active = comp.IsChargeActive;
            }

            public bool CanAffectStat(StatDef stat)
            {
                return partEfficiencyOffset != 0f
                    || ContainsStat(statOffsets, stat)
                    || ContainsStat(statFactors, stat);
            }

            private static bool ContainsStat(List<StatModifier> modifiers, StatDef stat)
            {
                foreach (StatModifier modifier in modifiers)
                    if (modifier.stat == stat)
                        return true;
                return false;
            }
        }

        private struct CacheInvalidation
        {
            private bool capacitiesChanged;
            private HashSet<StatDef> affectedStats;

            public void Include(CachedEntry entry)
            {
                if (entry.partEfficiencyOffset != 0f)
                    capacitiesChanged = true;

                IncludeStats(entry.statOffsets);
                IncludeStats(entry.statFactors);
            }

            private void IncludeStats(List<StatModifier> modifiers)
            {
                foreach (StatModifier modifier in modifiers)
                {
                    if (modifier.stat != null)
                        (affectedStats ??= new HashSet<StatDef>()).Add(modifier.stat);
                }
            }

            public void Apply(Pawn pawn)
            {
                if (capacitiesChanged)
                {
                    pawn.health.capacities?.Notify_CapacityLevelsDirty();
                    // Any cached stat on this pawn could use the changed capacity.
                    foreach (StatDef stat in DefDatabase<StatDef>.AllDefsListForReading)
                    {
                        if ((stat.cacheable || stat.immutable) && affectedStats?.Contains(stat) != true)
                            stat.Worker.ClearCacheForThing(pawn);
                    }
                }

                if (affectedStats != null)
                {
                    foreach (StatDef stat in affectedStats)
                        stat.Worker.ClearCacheForThing(pawn);
                }
            }
        }

        private Dictionary<HediffComp_Rechargeable, CachedEntry> depleted = new();
        private readonly List<CachedEntry> conditional = new();
        private HashSet<HediffComp_Rechargeable> pendingHealthNotifications;

        private readonly Dictionary<StatDef, float> cachedStatOffsets = new();
        private readonly Dictionary<StatDef, float> cachedStatFactors = new();
        private readonly Dictionary<BodyPartRecord, float> partEfficiencyOffsets = new();
        private readonly Dictionary<BodyPartRecord, List<Hediff>> partEfficiencySources = new();

        private bool dirty;

        public Pawn Pawn => parent as Pawn;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            RefreshActivation();
        }

        public override void PostExposeData()
        {
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                dirty = true;
        }

        public override float GetStatOffset(StatDef stat)
        {
            RefreshActivation(stat);
            return cachedStatOffsets.TryGetValue(stat, out float value) ? value : 0f;
        }

        public override float GetStatFactor(StatDef stat)
        {
            RefreshActivation(stat);
            return cachedStatFactors.TryGetValue(stat, out float value) ? value : 1f;
        }

        public override void GetStatsExplanation(StatDef stat, StringBuilder sb, string whitespace = "")
        {
            RefreshActivation(stat);

            StringBuilder lines = null;
            foreach (CachedEntry entry in depleted.Values)
            {
                if (entry.Active)
                    AppendStatExplanationLines(stat, entry.Comp.parent, entry, ref lines, whitespace);
            }

            if (lines == null || lines.Length == 0)
                return;

            sb.AppendLine(whitespace + "CHF_StatsReport_DepletedCharge".Translate() + ":");
            sb.Append(lines);
        }

        public void RegisterDepleted(HediffComp_Rechargeable comp)
        {
            if (!HediffChargeUtility.IsPawnEligible(Pawn))
                return;
            if (dirty)
                RebuildFromPawnHediffs();

            CachedEntry entry = TrackDepleted(comp);
            if (entry?.Active != true)
                return;

            AddActiveEffects(entry);
            var invalidation = new CacheInvalidation();
            invalidation.Include(entry);
            invalidation.Apply(Pawn);
        }

        public void Unregister(HediffComp_Rechargeable comp)
        {
            if (comp == null)
                return;

            pendingHealthNotifications?.Remove(comp);
            if (!depleted.Remove(comp, out CachedEntry entry))
                return;

            if (comp.Props.activationWorker != null)
                conditional.Remove(entry);
            if (!entry.Active)
                return;

            RebuildAggregates();
            var invalidation = new CacheInvalidation();
            invalidation.Include(entry);
            invalidation.Apply(Pawn);
        }

        private CachedEntry TrackDepleted(HediffComp_Rechargeable comp)
        {
            if (comp?.parent == null || !comp.IsDepleted || depleted.ContainsKey(comp))
                return null;

            ChargeConsequenceProperties effects = comp.Props.depletedConsequences;
            if (effects == null || (effects.statOffsets.Count == 0 && effects.statFactors.Count == 0
                && (comp.parent.Part == null || effects.partEfficiencyOffset == 0f)))
                return null;

            var entry = new CachedEntry(comp, effects);
            depleted.Add(comp, entry);
            if (comp.Props.activationWorker != null)
                conditional.Add(entry);

            return entry;
        }

        internal void RefreshActivation(StatDef stat = null)
        {
            if (dirty)
            {
                if (HediffChargeUtility.IsPawnEligible(Pawn))
                    RebuildFromPawnHediffs();
                return;
            }

            if (conditional.Count == 0 || (stat != null && !CanAffectStat(stat)))
                return;

            bool changed = false;
            var invalidation = new CacheInvalidation();
            foreach (CachedEntry entry in conditional)
            {
                bool active = entry.Comp.IsChargeActive;
                if (entry.Active == active)
                    continue;

                entry.Active = active;
                QueueHealthNotification(entry.Comp);
                invalidation.Include(entry);
                changed = true;
            }

            if (changed)
            {
                RebuildAggregates();
                invalidation.Apply(Pawn);
            }
        }

        private bool CanAffectStat(StatDef stat)
        {
            foreach (CachedEntry entry in conditional)
                if (entry.CanAffectStat(stat))
                    return true;
            return false;
        }

        private void QueueHealthNotification(HediffComp_Rechargeable comp)
        {
            (pendingHealthNotifications ??= new HashSet<HediffComp_Rechargeable>()).Add(comp);
        }

        internal void CheckActivationOnTick(HediffComp_Rechargeable comp)
        {
            if (dirty)
                RebuildFromPawnHediffs();

            // The usual interval only needs to evaluate this implant. A transition refreshes
            // every affected contribution before any health notification runs.
            if (depleted.TryGetValue(comp, out CachedEntry entry) && entry.Active != comp.IsChargeActive)
                RefreshActivation();

            if (pendingHealthNotifications?.Remove(comp) == true && !Pawn.Dead)
                Pawn.health.Notify_HediffChanged(comp.parent);
        }

        internal void ClearOnDeath()
        {
            if (depleted.Count > 0)
            {
                var invalidation = new CacheInvalidation();
                foreach (CachedEntry entry in depleted.Values)
                    if (entry.Active)
                        invalidation.Include(entry);

                depleted.Clear();
                conditional.Clear();
                pendingHealthNotifications?.Clear();
                RebuildAggregates();
                invalidation.Apply(Pawn);
            }

            // Reconstruct the transient entries if this pawn is resurrected.
            dirty = true;
        }

        public bool TryGetPartEfficiencyOffset(BodyPartRecord part, out float offset)
        {
            RefreshActivation();
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

            RefreshActivation();
            if (!partEfficiencySources.TryGetValue(part, out List<Hediff> sources))
                return false;

            foreach (Hediff source in sources)
                impactors.Add(new PawnCapacityUtility.CapacityImpactorHediff { hediff = source });
            return true;
        }

        public bool HasPartEfficiencyOffset(BodyPartRecord part)
        {
            RefreshActivation();
            return partEfficiencyOffsets.ContainsKey(part);
        }

        public void MarkDirty() => dirty = true;

        public void RebuildFromPawnHediffs()
        {
            Pawn pawn = Pawn;
            if (!HediffChargeUtility.IsPawnEligible(pawn))
                return;

            var previous = depleted;
            depleted = new Dictionary<HediffComp_Rechargeable, CachedEntry>();
            conditional.Clear();
            dirty = false;

            var invalidation = new CacheInvalidation();
            foreach (CachedEntry entry in previous.Values)
                if (entry.Active)
                    invalidation.Include(entry);

            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                if (!(hediff is HediffWithComps { comps: not null } withComps))
                    continue;

                foreach (HediffComp hediffComp in withComps.comps)
                {
                    if (!(hediffComp is HediffComp_Rechargeable comp))
                        continue;

                    CachedEntry entry = TrackDepleted(comp);
                    if (entry == null)
                        continue;

                    if (entry.Active)
                        invalidation.Include(entry);
                    if (previous.TryGetValue(comp, out CachedEntry oldEntry) && oldEntry.Active != entry.Active)
                        QueueHealthNotification(comp);
                }
            }

            pendingHealthNotifications?.RemoveWhere(comp => !depleted.ContainsKey(comp));
            RebuildAggregates();
            invalidation.Apply(pawn);
        }

        private void RebuildAggregates()
        {
            cachedStatOffsets.Clear();
            cachedStatFactors.Clear();
            partEfficiencyOffsets.Clear();
            partEfficiencySources.Clear();

            foreach (CachedEntry entry in depleted.Values)
                if (entry.Active)
                    AddActiveEffects(entry);
        }

        private void AddActiveEffects(CachedEntry entry)
        {
            foreach (StatModifier modifier in entry.statOffsets)
            {
                if (modifier.stat != null)
                    cachedStatOffsets[modifier.stat] = cachedStatOffsets.GetValueOrDefault(modifier.stat) + modifier.value;
            }

            foreach (StatModifier modifier in entry.statFactors)
            {
                if (modifier.stat != null)
                    cachedStatFactors[modifier.stat] = cachedStatFactors.GetValueOrDefault(modifier.stat, 1f) * modifier.value;
            }

            if (entry.partEfficiencyOffset == 0f)
                return;

            BodyPartRecord part = entry.part;
            partEfficiencyOffsets[part] = partEfficiencyOffsets.GetValueOrDefault(part) + entry.partEfficiencyOffset;
            if (!partEfficiencySources.TryGetValue(part, out List<Hediff> sources))
            {
                sources = new List<Hediff>();
                partEfficiencySources[part] = sources;
            }
            if (!sources.Contains(entry.Comp.parent))
                sources.Add(entry.Comp.parent);
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
    }
}
