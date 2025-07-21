using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace WorkOnThis.Cache
{
    public static class BillGiverCache
    {
        private static Pawn curPawn = null;
        private static Map curMap = null;
        private static Thing curThing = null;
        private static int frames = 0;
        private static List<(Job, Thing, WorkGiverDef)> cache = new List<(Job, Thing, WorkGiverDef)>();

        private static void Clear(Thing thing, Pawn pawn)
        {
            curPawn = pawn;
            curMap = thing.Map;
            curThing = thing;
            frames = 0;
            cache.Clear();
        }

        public static bool IsUpdateNow(Thing thing, Pawn pawn)
        {
            Map map = thing.Map;
            if (pawn != curPawn || map != curMap || thing != curThing || frames >= 60)
            {
                Clear(thing, pawn);
                return true;
            }
            return false;
        }

        public static void Insert(Job job, Thing thing, WorkGiverDef def)
        {
            cache.Add((job, thing, def));
        }

        public static IEnumerable<(Job, Thing, WorkGiverDef)> GetValues()
        {
            foreach ((Job, Thing, WorkGiverDef) data in cache)
                yield return data;
        }
    }
}
