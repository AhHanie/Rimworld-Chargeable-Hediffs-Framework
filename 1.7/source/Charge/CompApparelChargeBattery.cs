using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Chargeable_Hediffs_Framework
{
    // Why a recipient cannot be recharged by a pack right now. Cheap to compute, so the job driver
    // can poll it every tick; translation only happens in CanRechargePawn.
    public enum PackRechargeDenial
    {
        None,
        NotWorn,
        Self,
        NotSpawned,
        WrongMap,
        NotColonyOwned,
        Hostile,
        NoDeficit,
        MentalState,
        NotInterruptible,
        AlreadyServiced,
        Reserved,
        Unreachable
    }

    public class CompApparelChargeBattery : CompApparelReloadable
    {
        // Fractional pack uses received from charge stations; whole units move into remainingCharges.
        private float stationRechargeProgress;

        private TargetingParameters targetingParams;

        public new CompProperties_ApparelChargeBattery Props => (CompProperties_ApparelChargeBattery)props;

        public float StationRechargeProgress => stationRechargeProgress;

        public bool IsFull => remainingCharges >= MaxCharges;

        // Fill level including the fractional station progress, 0..1.
        public float FillPercent => MaxCharges > 0
            ? Mathf.Clamp01((remainingCharges + stationRechargeProgress) / MaxCharges)
            : 0f;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref stationRechargeProgress, "stationRechargeProgress", 0f);
        }

        // ── Station refilling ─────────────────────────────────────────────────────────────────────

        // Accumulates a (possibly tiny) amount of pack uses. Completed units move into
        // remainingCharges; progress beyond full capacity is discarded. Returns whole uses added.
        public int AddStationCharge(float uses)
        {
            if (!(uses > 0f) || float.IsInfinity(uses))
                return 0;
            if (IsFull)
            {
                stationRechargeProgress = 0f;
                return 0;
            }

            stationRechargeProgress += uses;
            int whole = Mathf.FloorToInt(stationRechargeProgress);
            if (whole <= 0)
                return 0;

            int before = remainingCharges;
            remainingCharges = Mathf.Min(MaxCharges, remainingCharges + whole);
            stationRechargeProgress -= whole;
            if (IsFull)
                stationRechargeProgress = 0f;
            return remainingCharges - before;
        }

        // ── Recipient rules ───────────────────────────────────────────────────────────────────────

        // A recipient that is already immobile can be serviced in place without replacing its job.
        public static bool IsRecipientStationary(Pawn recipient)
        {
            return recipient.Downed || !recipient.Awake() || recipient.IsDeactivated() || recipient.IsSelfShutdown();
        }

        public PackRechargeDenial GetRecipientDenial(Pawn recipient, bool checkReach = true)
        {
            Pawn wearer = Wearer;
            if (wearer == null)
                return PackRechargeDenial.NotWorn;
            if (recipient == wearer)
                return PackRechargeDenial.Self;
            if (recipient == null || recipient.Dead || !recipient.Spawned)
                return PackRechargeDenial.NotSpawned;
            if (recipient.Map != wearer.Map)
                return PackRechargeDenial.WrongMap;
            if (!HediffChargeUtility.IsAutoRechargeEligible(recipient))
                return PackRechargeDenial.NotColonyOwned;
            if (recipient.HostileTo(wearer))
                return PackRechargeDenial.Hostile;
            if (!HasEligibleChargeDeficit(recipient))
                return PackRechargeDenial.NoDeficit;
            if (recipient.InMentalState)
                return PackRechargeDenial.MentalState;

            Job recipientJob = recipient.CurJob;
            if (recipientJob != null && recipientJob.def == CHF_JobDefOf.CHF_WaitForPackRecharge
                && !JobDriver_WaitForPackRecharge.IsWaitFor(recipientJob, wearer, parent))
                return PackRechargeDenial.AlreadyServiced;

            if (!IsRecipientStationary(recipient))
            {
                if (!recipient.CanCasuallyInteractNow(twoWayInteraction: false, canInteractWhileSleeping: true))
                    return PackRechargeDenial.NotInterruptible;
                if (recipientJob != null && !recipientJob.def.casualInterruptible)
                    return PackRechargeDenial.NotInterruptible;
            }

            if (checkReach)
            {
                if (!wearer.CanReach(recipient, PathEndMode.Touch, wearer.NormalMaxDanger()))
                    return PackRechargeDenial.Unreachable;
                if (!wearer.CanReserve(recipient))
                    return PackRechargeDenial.Reserved;
            }
            return PackRechargeDenial.None;
        }

        public bool CanRechargePawn(Pawn recipient, out string reason)
        {
            PackRechargeDenial denial = GetRecipientDenial(recipient);
            reason = denial == PackRechargeDenial.None ? null : DenialText(denial);
            return denial == PackRechargeDenial.None;
        }

        public static string DenialText(PackRechargeDenial denial)
        {
            return ("CHF_PackInvalid_" + denial).Translate();
        }

        // True if the recipient has at least one hediff this pack would charge.
        public bool HasEligibleChargeDeficit(Pawn recipient)
        {
            if (!HediffChargeUtility.IsPawnEligible(recipient))
                return false;
            List<Hediff> hediffs = recipient.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (!(hediffs[i] is HediffWithComps hwc))
                    continue;
                for (int j = 0; j < hwc.comps.Count; j++)
                {
                    if (hwc.comps[j] is HediffComp_Rechargeable rc && IsEligibleTarget(rc))
                        return true;
                }
            }
            return false;
        }

        private bool IsEligibleTarget(HediffComp_Rechargeable rc)
        {
            if (!rc.NeedsCharge || !Props.SupportsHediff(rc.Def))
                return false;
            // Same rule as the implant battery: battery hediffs are never recharge targets.
            return rc.parent.comps.Find(c => c is HediffComp_ChargeBattery) == null;
        }

        // ── Wearer rules ──────────────────────────────────────────────────────────────────────────

        public bool CanOrderRecharge(out string reason)
        {
            reason = null;
            Pawn wearer = Wearer;
            if (wearer == null)
            {
                reason = DenialText(PackRechargeDenial.NotWorn);
                return false;
            }
            if (!wearer.IsPlayerControlled)
            {
                reason = "CannotOrderNonControlled".Translate();
                return false;
            }
            if (!wearer.Spawned)
            {
                reason = "CHF_PackWearerNotOnMap".Translate();
                return false;
            }
            if (wearer.Downed || !wearer.health.capacities.CapableOf(PawnCapacityDefOf.Moving))
            {
                reason = "CHF_PackWearerCannotMove".Translate(wearer.LabelShort);
                return false;
            }
            if (remainingCharges <= 0)
            {
                reason = "CommandReload_NoCharges".Translate(Props.ChargeNounArgument);
                return false;
            }
            if (!CanBeUsed(out reason))
            {
                if (reason.NullOrEmpty())
                    reason = "CHF_PackUnusable".Translate();
                return false;
            }
            return true;
        }

        // ── Transfer ──────────────────────────────────────────────────────────────────────────────

        // Moves up to chargePerUse into the recipient's eligible hediffs, lowest ChargePercent
        // first (ties: larger deficit, then hediff list order). Returns the charge actually added.
        // Does not spend a pack use; the job driver calls UsedOnce after a positive result.
        public float TryRechargePawn(Pawn recipient)
        {
            if (!HediffChargeUtility.IsPawnEligible(recipient))
                return 0f;

            var targets = new List<HediffComp_Rechargeable>();
            List<Hediff> hediffs = recipient.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (!(hediffs[i] is HediffWithComps hwc))
                    continue;
                for (int j = 0; j < hwc.comps.Count; j++)
                {
                    if (hwc.comps[j] is HediffComp_Rechargeable rc && IsEligibleTarget(rc))
                        InsertByPriority(targets, rc);
                }
            }

            float budget = Props.chargePerUse;
            float added = 0f;
            for (int i = 0; i < targets.Count && budget > 0f; i++)
            {
                HediffComp_Rechargeable rc = targets[i];
                float before = rc.CurrentCharge;
                rc.AddCharge(Mathf.Min(budget, rc.MaxCharge - before));
                float gained = rc.CurrentCharge - before;
                budget -= gained;
                added += gained;
            }
            return added;
        }

        // Stable insertion: a candidate goes before an existing entry only if strictly better,
        // so equal entries keep hediff list order.
        private static void InsertByPriority(List<HediffComp_Rechargeable> sorted, HediffComp_Rechargeable rc)
        {
            int index = sorted.Count;
            while (index > 0 && IsBetterTarget(rc, sorted[index - 1]))
                index--;
            sorted.Insert(index, rc);
        }

        private static bool IsBetterTarget(HediffComp_Rechargeable candidate, HediffComp_Rechargeable current)
        {
            if (candidate.ChargePercent != current.ChargePercent)
                return candidate.ChargePercent < current.ChargePercent;
            return candidate.MaxCharge - candidate.CurrentCharge > current.MaxCharge - current.CurrentCharge;
        }

        // ── Gizmo ─────────────────────────────────────────────────────────────────────────────────

        public override IEnumerable<Gizmo> CompGetWornGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetWornGizmosExtra())
                yield return gizmo;

            Pawn wearer = Wearer;
            if (wearer == null)
                yield break;
            if (wearer.Drafted ? !Props.displayGizmoWhileDrafted : !Props.displayGizmoWhileUndrafted)
                yield break;

            var command = new Command_ChargePackTarget
            {
                pack = this,
                defaultLabel = "CHF_PackCommandLabel".Translate(),
                defaultDesc = "CHF_PackCommandDesc".Translate(
                    Props.chargePerUse.ToString("F1"),
                    (Props.rechargeDurationTicks / 60f).ToString("F1")),
                icon = Props.CommandIcon ?? parent.def.uiIcon,
                iconAngle = parent.def.uiIconAngle,
                iconOffset = parent.def.uiIconOffset,
                defaultIconColor = parent.DrawColor,
                hotKey = Props.hotKey,
                targetingParams = TargetingParams,
                action = OrderRecharge
            };
            if (!CanOrderRecharge(out string reason))
                command.Disable(reason);
            yield return command;
        }

        private TargetingParameters TargetingParams => targetingParams ?? (targetingParams = new TargetingParameters
        {
            canTargetPawns = true,
            canTargetAnimals = true,
            canTargetHumans = true,
            canTargetMechs = true,
            canTargetSubhumans = true,
            canTargetBuildings = false,
            canTargetItems = false,
            canTargetSelf = false,
            neverTargetHostileFaction = true,
            // Hover only filters out pawns that can never be recipients. State-dependent failures
            // (no deficit, mental state, unreachable, ...) are accepted here so that clicking
            // them explains the reason in OrderRecharge instead of silently doing nothing.
            validator = target => target.Thing is Pawn pawn
                && pawn != Wearer
                && HediffChargeUtility.IsAutoRechargeEligible(pawn)
        });

        // Full re-check: the target or pack may have changed between targeting and this click.
        private void OrderRecharge(LocalTargetInfo target)
        {
            Pawn wearer = Wearer;
            if (wearer == null)
                return;
            if (!CanOrderRecharge(out string reason))
            {
                Messages.Message(reason, wearer, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }
            Pawn recipient = target.Thing as Pawn;
            if (recipient == null || !CanRechargePawn(recipient, out reason))
            {
                Messages.Message(reason ?? DenialText(PackRechargeDenial.NotSpawned), wearer,
                    MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            Job job = JobMaker.MakeJob(CHF_JobDefOf.CHF_RechargePawnFromPack, recipient, parent);
            job.playerForced = true;
            wearer.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        public override string CompInspectStringExtra()
        {
            string text = base.CompInspectStringExtra();
            if (stationRechargeProgress > 0f && !IsFull)
                text += " (+" + stationRechargeProgress.ToStringPercent("F0") + ")";
            return text;
        }
    }

    // Command_Target that shows the remaining use count in the corner like vanilla reloadable gizmos.
    public class Command_ChargePackTarget : Command_Target
    {
        public CompApparelChargeBattery pack;

        public override string TopRightLabel => pack.GizmoExtraLabel;
    }
}
