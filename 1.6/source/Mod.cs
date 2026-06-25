using HarmonyLib;
using Verse;

namespace Chargeable_Hediffs_Framework
{
    public class Mod : Verse.Mod
    {
        public Mod(ModContentPack content) : base(content)
        {
            LongEventHandler.QueueLongEvent(Init, "CHF_LoadingLabel", doAsynchronously: true, null);
        }

        public void Init()
        {
            HediffChargeUtility.InjectChargeCacheCompIntoRaceDefs();
            new Harmony("sk.chargehediff").PatchAll();
        }
    }
}
