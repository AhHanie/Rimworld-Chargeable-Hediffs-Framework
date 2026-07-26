using Verse;
using Verse.AI;

namespace Chargeable_Hediffs_Framework
{
    // Gates the autonomous self-charge subtree to colony animals authorized via SentienceCatalyst
    // or the CHF_SelfCharge training. Does not check charge level; JobGiver_AnimalRechargeHediffs
    // handles that so the condition reflects authorization only.
    public class ThinkNode_ConditionalAnimalSelfCharge : ThinkNode_Conditional
    {
        protected override bool Satisfied(Pawn pawn)
        {
            return HediffChargeUtility.IsAnimalAuthorizedToSelfCharge(pawn);
        }
    }
}
