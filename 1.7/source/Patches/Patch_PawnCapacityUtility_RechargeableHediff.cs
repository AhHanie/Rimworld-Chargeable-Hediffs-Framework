using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Chargeable_Hediffs_Framework.Patches
{
    [HarmonyPatch(typeof(PawnCapacityUtility), nameof(PawnCapacityUtility.CalculatePartEfficiency))]
    public static class Patch_PawnCapacityUtility_RechargeableHediff
    {
        [HarmonyPrefix]
        [HarmonyInfix(typeof(Mathf), nameof(Mathf.Max), typeof(float), typeof(float), Positions = new[] { -1 })]
        public static void Prefix(
            ref float a,
            [HarmonyOuter] HediffSet diffSet,
            [HarmonyOuter] BodyPartRecord part,
            [HarmonyOuter] List<PawnCapacityUtility.CapacityImpactor> impactors)
        {
            Pawn pawn = diffSet.pawn;
            if (pawn.Dead)
                return;

            CompChargeConsequencesCache cache = HediffChargeUtility.GetChargeCache(pawn);
            if (cache == null)
                return;

            if (!cache.TryGetPartEfficiencyOffset(part, out float offset))
                return;

            cache.TryGetPartEfficiencyImpactors(part, impactors);
            a = Mathf.Max(Mathf.Max(a, 0f) + offset, 0.0001f);
        }
    }
}
