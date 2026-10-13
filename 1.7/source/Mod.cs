using HarmonyLib;
using Verse;

namespace Chargeable_Hediffs_Framework
{
    public class Mod : Verse.Mod
    {
        public Mod(ModContentPack content) : base(content)
        {
            new Harmony("sk.chargehediff").PatchAll();
            LongEventHandler.QueueLongEvent(Init, "CHF_LoadingLabel", doAsynchronously: true, null);
        }

        public void Init()
        {
            HediffChargeUtility.InjectChargeCacheCompIntoRaceDefs();
        }
    }
}
