using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using WorkOnThis.Cache;
using WorkOnThis.Windows;

namespace WorkOnThis.Tools
{
    [StaticConstructorOnStartup]
    public static class WorkFinder
    {
        private static readonly IntRange ReCheckFailedBillTicksRange = new IntRange(500, 600);
        public static Thing ForcedThing
        {
            get;
            private set;
        }

        private static bool CannotDoBillDueToMedicineRestriction(IBillGiver giver, Bill bill, List<IngredientCount> missingIngredients)
        {
            return (bool)typeof(WorkGiver_DoBill).GetMethod(
                "CannotDoBillDueToMedicineRestriction",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
            ).Invoke(null, new object[] { giver, bill, missingIngredients });
        }

        private static bool TryFindBestBillIngredients(Bill bill, Pawn pawn, Thing billGiver, List<ThingCount> chosen, List<IngredientCount> missingIngredients)
        {
            return (bool)typeof(WorkGiver_DoBill).GetMethod(
                "TryFindBestBillIngredients",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
            ).Invoke(null, new object[] { bill, pawn, billGiver, chosen, missingIngredients });
        }

        private static Job WorkOnFormedBill(Thing giver, Bill_Autonomous bill)
        {
            return (Job)typeof(WorkGiver_DoBill).GetMethod(
                "WorkOnFormedBill",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
            ).Invoke(null, new object[] { giver, bill });
        }

        private static UnfinishedThing ClosestUnfinishedThingForBill(Pawn pawn, Bill_ProductionWithUft bill)
        {
            return (UnfinishedThing)typeof(WorkGiver_DoBill).GetMethod(
                "ClosestUnfinishedThingForBill",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
            ).Invoke(null, new object[] { pawn, bill });
        }

        private static Job FinishUftJob(Pawn pawn, UnfinishedThing uft, Bill_ProductionWithUft bill)
        {
            return (Job)typeof(WorkGiver_DoBill).GetMethod(
                "FinishUftJob",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
            ).Invoke(null, new object[] { pawn, uft, bill });
        }

        private static Job StartOrResumeBillJob(Pawn pawn, IBillGiver giver, Bill bill, WorkGiverDef def)
        {
            List<ThingCount> chosenIngThings = new List<ThingCount>();
            List<IngredientCount> missingIngredients = new List<IngredientCount>();
            List<Thing> tmpMissingUniqueIngredients = new List<Thing>();
            bool flag = FloatMenuMakerMap.makingFor == pawn;

            if ((bill.recipe.requiredGiverWorkType != null && bill.recipe.requiredGiverWorkType != def.workType) || (Find.TickManager.TicksGame <= bill.nextTickToSearchForIngredients && FloatMenuMakerMap.makingFor != pawn) || !bill.ShouldDoNow() || !bill.PawnAllowedToStartAnew(pawn))
                return null;

            SkillRequirement skillRequirement = bill.recipe.FirstSkillRequirementPawnDoesntSatisfy(pawn);
            if (skillRequirement != null)
                return null;

            if (bill is Bill_Medical bill_Medical)
            {
                if (bill_Medical.IsSurgeryViolationOnExtraFactionMember(pawn))
                    return null;

                if (!pawn.CanReserve(bill_Medical.GiverPawn, 1, -1, null, true))
                    return null;
            }

            if (bill is Bill_Mech bill_Mech && bill_Mech.Gestator.WasteProducer.Waste != null && bill_Mech.Gestator.GestatingMech == null)
                return null;

            if (bill is Bill_ProductionWithUft bill_ProductionWithUft)
            {
                if (bill_ProductionWithUft.BoundUft != null)
                {
                    if (bill_ProductionWithUft.BoundWorker == pawn && pawn.CanReserveAndReach(bill_ProductionWithUft.BoundUft, PathEndMode.Touch, Danger.Deadly) && !bill_ProductionWithUft.BoundUft.IsForbidden(pawn))
                        return FinishUftJob(pawn, bill_ProductionWithUft.BoundUft, bill_ProductionWithUft);
                    return null;
                }

                UnfinishedThing unfinishedThing = ClosestUnfinishedThingForBill(pawn, bill_ProductionWithUft);
                if (unfinishedThing != null)
                    return FinishUftJob(pawn, unfinishedThing, bill_ProductionWithUft);
            }

            if (bill is Bill_Autonomous bill_Autonomous && bill_Autonomous.State != 0)
                return WorkOnFormedBill((Thing)giver, bill_Autonomous);

            List<IngredientCount> list = null;
            if (flag)
            {
                list = missingIngredients;
                list.Clear();
                tmpMissingUniqueIngredients.Clear();
            }

            Bill_Medical bill_Medical2 = bill as Bill_Medical;
            if (bill_Medical2 != null && bill_Medical2.uniqueRequiredIngredients?.NullOrEmpty() == false)
                foreach (Thing uniqueRequiredIngredient in bill_Medical2.uniqueRequiredIngredients)
                    if (uniqueRequiredIngredient.IsForbidden(pawn) || !pawn.CanReserveAndReach(uniqueRequiredIngredient, PathEndMode.OnCell, Danger.Deadly))
                        tmpMissingUniqueIngredients.Add(uniqueRequiredIngredient);

            if (!TryFindBestBillIngredients(bill, pawn, (Thing)giver, chosenIngThings, list) || !tmpMissingUniqueIngredients.NullOrEmpty())
            {
                if (FloatMenuMakerMap.makingFor != pawn)
                {
                    bill.nextTickToSearchForIngredients = Find.TickManager.TicksGame + ReCheckFailedBillTicksRange.RandomInRange;
                }
                else if (flag)
                {
                    if (CannotDoBillDueToMedicineRestriction(giver, bill, list))
                    {
                        JobFailReason.Is("NoMedicineMatchingCategory".Translate(WorkGiver_DoBill.GetMedicalCareCategory((Thing)giver).GetLabel().Named("CATEGORY")), bill.Label);
                    }
                    else
                    {
                        string text = list.Select((IngredientCount missing) => missing.Summary).Concat(tmpMissingUniqueIngredients.Select((Thing t) => t.Label)).ToCommaList();
                        JobFailReason.Is("MissingMaterials".Translate(text), bill.Label);
                    }

                    flag = false;
                }

                chosenIngThings.Clear();
                return null;
            }

            flag = false;
            if (bill_Medical2 != null && bill_Medical2.uniqueRequiredIngredients?.NullOrEmpty() == false)
            {
                foreach (Thing uniqueRequiredIngredient2 in bill_Medical2.uniqueRequiredIngredients)
                {
                    chosenIngThings.Add(new ThingCount(uniqueRequiredIngredient2, 1));
                }
            }

            Job haulOffJob;
            Job result = WorkGiver_DoBill.TryStartNewDoBillJob(pawn, bill, giver, chosenIngThings, out haulOffJob);
            chosenIngThings.Clear();
            return result;
        }

        private static Job JobOnBill(Pawn pawn, Thing thing, Thing workSpot, Bill bill, WorkGiverDef def)
        {
            if (def.Worker is WorkGiver_DoBill worker)
            {
                if (worker.JobOnThing(pawn, workSpot, true) == null)
                    return null;

                IBillGiver billGiver = workSpot as IBillGiver;
                ForcedThing = thing;
                Job job = null;
                try
                {
                    job = StartOrResumeBillJob(pawn, billGiver, bill, def);
                }
                catch (Exception e)
                {
                    Log.Error(e.StackTrace);
                }
                ForcedThing = null;
                return job;
            }

            return null;
        }

        private static IEnumerable<(Job, Thing, WorkGiverDef)> GetAllAvailableJobs(ThingWithComps thing, Pawn pawn)
        {
            if (!BillGiverCache.IsUpdateNow(thing, pawn))
            {
                foreach ((Job, Thing, WorkGiverDef) value in BillGiverCache.GetValues(thing))
                    yield return value;
                yield break;
            }
            List<Thing> list = pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.PotentialBillGiver);

            for (int i = 0; i < list.Count; i++)
            {
                Thing workSpot = list[i];
                if (workSpot is IBillGiver billGiver && billGiver != pawn)
                {
                    foreach (Bill bill in billGiver.BillStack)
                    {
                        WorkGiverDef workGiverDef = billGiver.GetWorkgiver();
                        Job job = JobOnBill(pawn, thing, workSpot, bill, workGiverDef);
                        if (job == null)
                             continue;

                        if (GetIngredients(job).Any(thingCount => thingCount.Thing == thing))
                        {
                            BillGiverCache.Insert(thing, job, workSpot, workGiverDef);
                            yield return (job, workSpot, workGiverDef);
                            break;
                        }
                    }
                }
            }
        }

        private static List<ThingCount> GetIngredients(Job job)
        {
            List<ThingCount> ingredients = new List<ThingCount>();

            if (job.targetQueueB != null)
            {
                for (int i = 0; i < job.targetQueueB.Count; i++)
                {
                    Thing thing = job.targetQueueB[i].Thing;
                    int count = job.countQueue[i];
                    ingredients.Add(new ThingCount(thing, count));
                }
            }

            return ingredients;
        }

        public static bool GetWorkOnMenu(ThingWithComps thing, Pawn pawn, out FloatMenuOption option)
        {
            var subs = new List<FloatMenuOption>();
            foreach ((Job, Thing, WorkGiverDef) pair in GetAllAvailableJobs(thing, pawn))
            {
                Job job = pair.Item1;
                Thing workSpot = pair.Item2;
                WorkGiverDef workGiverDef = pair.Item3;

                string text = "PrioritizeGeneric".Translate((workGiverDef.Worker as WorkGiver_DoBill).PostProcessedGerund(job), workSpot.Label).CapitalizeFirst();
                FloatMenuOption sub = new FloatMenuOption(text, () =>
                {
                    bool shiftHeld = (Event.current != null && Event.current.shift);
                    pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc, shiftHeld);
                }, iconThing: workSpot, iconColor: Color.white);

                subs.Add(sub);
            }

            if (subs.Count == 0)
            {
                option = null;
                return false;
            }

            var workOn = new FloatMenuOption("WorkOnThis.UI.WorkOnThis".Translate(), () =>
            {
                Rect existingRect = new Rect();
                foreach (Window win in Find.WindowStack.Windows)
                {
                    if (win is FloatMenu existingMenu)
                    {
                        existingRect = existingMenu.windowRect;
                        break;
                    }
                }

                PositionedFloatMenu subMenu = new PositionedFloatMenu(subs, thing, existingRect.x, existingRect.y);
                Find.WindowStack.Add(subMenu);
            });
            workOn.tooltip = "WorkOnThis.UI.WorkOnThis.Desc".Translate();

            option = workOn;
            return true;
        }
    }
}
