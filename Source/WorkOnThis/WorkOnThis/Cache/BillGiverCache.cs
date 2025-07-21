using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace WorkOnThis.Cache
{
    public static class BillGiverCache
    {
        private class CacheData
        {
            public List<(Job, Thing, WorkGiverDef)> data = new List<(Job, Thing, WorkGiverDef)>();
            public int age;
        }

        private static Pawn curPawn = null;
        private static Map curMap = null;
        private static Dictionary<Thing, CacheData> cache = new Dictionary<Thing, CacheData>();

        private static void ClearAll(Pawn pawn)
        {
            curPawn = pawn;
            curMap = pawn.Map;
            cache.Clear();
        }

        public static bool IsUpdateNow(Thing thing, Pawn pawn)
        {
            Map map = thing.Map;
            if (pawn != curPawn || map != curMap || cache.Max(pair => pair.Value.age) >= 30)
                ClearAll(pawn);

            if (!cache.ContainsKey(thing))
            {
                cache.Add(thing, new CacheData());
                return true;
            }

            cache[thing].age++;
            return false;
        }

        public static void Insert(Thing thing, Job job, Thing workSpot, WorkGiverDef def)
        {
            cache[thing].data.Add((job, workSpot, def));
        }

        public static IEnumerable<(Job, Thing, WorkGiverDef)> GetValues(Thing thing)
        {
            foreach ((Job, Thing, WorkGiverDef) data in cache[thing].data)
                yield return data;
        }
    }
}
