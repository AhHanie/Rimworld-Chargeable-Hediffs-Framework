using RimWorld;
using Verse;

namespace Chargeable_Hediffs_Framework
{
    [DefOf]
    public static class CHF_JobDefOf
    {
        public static JobDef CHF_RechargeHediffs;

        static CHF_JobDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(CHF_JobDefOf));
        }
    }
}
