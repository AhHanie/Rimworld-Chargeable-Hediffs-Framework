using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Chargeable_Hediffs_Framework.Patches
{
    [HarmonyPatch(typeof(PawnCapacityUtility), nameof(PawnCapacityUtility.CalculatePartEfficiency))]
    public static class Patch_PawnCapacityUtility_RechargeableHediff
    {
        public static void Postfix(
            HediffSet diffSet,
            BodyPartRecord part,
            List<PawnCapacityUtility.CapacityImpactor> impactors,
            ref float __result)
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
            __result = Mathf.Max(__result + offset, 0.0001f);
        }
    }
}
