using HarmonyLib;
using RimWorld;
using Verse;

namespace Chargeable_Hediffs_Framework.Patches
{
    [HarmonyPatch(typeof(PawnCapacitiesHandler), nameof(PawnCapacitiesHandler.GetLevel), typeof(PawnCapacityDef))]
    internal static class Patch_PawnCapacitiesHandler_GetLevel
    {
        private static void Prefix(Pawn ___pawn)
        {
            HediffChargeUtility.GetChargeCache(___pawn)?.RefreshActivation();
        }
    }

    [HarmonyPatch(typeof(StatWorker), nameof(StatWorker.GetValue), typeof(Thing), typeof(bool), typeof(int))]
    internal static class Patch_StatWorker_GetValue
    {
        private static void Prefix(Thing thing, StatDef ___stat)
        {
            if (thing is Pawn pawn)
                HediffChargeUtility.GetChargeCache(pawn)?.RefreshActivation(___stat);
        }
    }
}
