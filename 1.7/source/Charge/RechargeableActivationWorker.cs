using Verse;

namespace Chargeable_Hediffs_Framework
{
    public abstract class RechargeableActivationWorker
    {
        // Return the current activation state without modifying the pawn or its charge.
        // The worker is shared by implants using the same definition and its answer may change.
        public abstract bool IsActiveFor(Pawn pawn);
    }
}
