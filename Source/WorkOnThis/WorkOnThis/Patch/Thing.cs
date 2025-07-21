using HarmonyLib;
using System.Collections.Generic;
using Verse;
using WorkOnThis.Tools;

namespace WorkOnThis.Patch.Thing_
{
    [HarmonyPatch(typeof(ThingWithComps), "GetFloatMenuOptions")]
    public class GetFloatMenuOptions_Patch
    {
        static IEnumerable<FloatMenuOption> Postfix(IEnumerable<FloatMenuOption> __result, ThingWithComps __instance, Pawn selPawn)
        {
            FloatMenuOption workOn;
            if (__instance.Spawned && !selPawn.Drafted && WorkFinder.GetWorkOnMenu(__instance, selPawn, out workOn))
                yield return workOn;

            foreach (var option in __result)
                yield return option;
        }
    }
}
