using RimWorld;
using Verse;

namespace Chargeable_Hediffs_Framework
{
    [DefOf]
    public static class CHF_TrainableDefOf
    {
        public static TrainableDef CHF_SelfCharge;

        static CHF_TrainableDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(CHF_TrainableDefOf));
        }
    }
}
