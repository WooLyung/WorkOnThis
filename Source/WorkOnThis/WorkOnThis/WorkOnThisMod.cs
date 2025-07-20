using HarmonyLib;
using System.Reflection;
using Verse;

namespace WorkOnThis
{
    [StaticConstructorOnStartup]
    public class WorkOnThisMod
    {
        public static Harmony Harmony { get; private set; }

        static WorkOnThisMod()
        {
            Harmony = new Harmony("ng.lyu.workonthis");
            Harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }
}
