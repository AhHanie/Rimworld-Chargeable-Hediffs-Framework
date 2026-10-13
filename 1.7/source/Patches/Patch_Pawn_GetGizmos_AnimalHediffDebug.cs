using HarmonyLib;
using System.Collections.Generic;
using Verse;

namespace Chargeable_Hediffs_Framework.Patches
{
    // Vanilla Pawn.GetGizmos() only calls Pawn_HealthTracker.GetGizmos() - the source of every
    // HediffComp's CompGetGizmos(), including this framework's debug charge tools - for
    // IsColonistPlayerControlled, IsColonyMech, or IsPrisonerOfColony pawns. Colony animals and
    // player-owned ghouls are never included, so a rechargeable hediff's debug gizmos are
    // unreachable on them even with dev mode on. This restores health gizmos for both in dev
    // mode only. IsColonySubhuman is checked alongside IsGhoul because IsGhoul alone does not
    // require player ownership (a hostile ghoul would otherwise also qualify).
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class Patch_Pawn_GetGizmos_AnimalHediffDebug
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach (Gizmo gizmo in __result)
                yield return gizmo;

            if (!DebugSettings.ShowDevGizmos)
                yield break;
            bool isDebugGizmoEligible = __instance.IsColonyAnimal
                || (__instance.IsGhoul && __instance.IsColonySubhuman);
            if (!isDebugGizmoEligible || __instance.health == null)
                yield break;

            foreach (Gizmo gizmo in __instance.health.GetGizmos())
                yield return gizmo;
        }
    }
}
