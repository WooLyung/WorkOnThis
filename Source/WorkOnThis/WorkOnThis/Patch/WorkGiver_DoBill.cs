using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.AI;
using WorkOnThis.Tools;

namespace WorkOnThis.Patch.WorkGiver_DoBill_
{
    [HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestBillIngredientsInSet_AllowMix")]
    public class TryFindBestBillIngredientsInSet_AllowMix_Patch
    {
        static bool Prefix(ref bool __result, List<Thing> availableThings, Bill bill, List<ThingCount> chosen, IntVec3 rootCell, List<IngredientCount> missingIngredients)
        {
            if (WorkFinder.ForcedThing == null)
                return true;

            chosen.Clear();
            missingIngredients?.Clear();
            availableThings.SortBy((Thing t) => bill.recipe.IngredientValueGetter.ValuePerUnitOf(t.def), (Thing t) =>
            {
                if (t == WorkFinder.ForcedThing)
                    return -100000;
                return (t.Position - rootCell).LengthHorizontalSquared;
            });

            for (int i = 0; i < bill.recipe.ingredients.Count; i++)
            {
                IngredientCount ingredientCount = bill.recipe.ingredients[i];
                float num = ingredientCount.GetBaseCount();
                for (int j = 0; j < availableThings.Count; j++)
                {
                    Thing thing = availableThings[j];
                    if (ingredientCount.filter.Allows(thing) && (ingredientCount.IsFixedIngredient || bill.ingredientFilter.Allows(thing)))
                    {
                        float num2 = bill.recipe.IngredientValueGetter.ValuePerUnitOf(thing.def);
                        int num3 = Mathf.Min(Mathf.CeilToInt(num / num2), thing.stackCount);
                        ThingCountUtility.AddToList(chosen, thing, num3);
                        num -= (float)num3 * num2;
                        if (num <= 0.0001f)
                            break;
                    }
                }

                if (num > 0.0001f)
                {
                    if (missingIngredients == null)
                    {
                        __result = false;
                        return false;
                    }

                    missingIngredients.Add(ingredientCount);
                }
            }

            if (missingIngredients != null)
            {
                __result = missingIngredients.Count == 0;
                return false;
            }

            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestIngredientsInSet_NoMixHelper")]
    public class TryFindBestIngredientsInSet_NoMixHelper_Patch
    {
        static bool Prefix(ref bool __result, List<Thing> availableThings, List<IngredientCount> ingredients, List<ThingCount> chosen, IntVec3 rootCell, bool alreadySorted, List<IngredientCount> missingIngredients, Bill bill = null)
        {
            if (WorkFinder.ForcedThing == null)
                return true;

            Type DefCountList = typeof(WorkGiver_DoBill).GetNestedType("DefCountList", BindingFlags.NonPublic);
            object availableCounts = Activator.CreateInstance(DefCountList);
            MethodInfo GetDef = DefCountList.GetMethod("GetDef", BindingFlags.Instance | BindingFlags.Public);
            MethodInfo Count = DefCountList.GetMethod("get_Count", BindingFlags.Instance | BindingFlags.Public);
            MethodInfo GetCount = DefCountList.GetMethod("GetCount", BindingFlags.Instance | BindingFlags.Public);
            MethodInfo SetCount = DefCountList.GetMethod("SetCount", BindingFlags.Instance | BindingFlags.Public);
            MethodInfo Clear = DefCountList.GetMethod("Clear", BindingFlags.Instance | BindingFlags.Public);
            MethodInfo GenerateFrom = DefCountList.GetMethod("GenerateFrom", BindingFlags.Instance | BindingFlags.Public);

            if (!alreadySorted)
            {
                Comparison<Thing> comparison = delegate (Thing t1, Thing t2)
                {
                    float num4 = t1 == WorkFinder.ForcedThing ? -100000 : (t1.PositionHeld - rootCell).LengthHorizontalSquared;
                    float value = t2 == WorkFinder.ForcedThing ? -100000 : (t2.PositionHeld - rootCell).LengthHorizontalSquared;
                    return num4.CompareTo(value);
                };
                availableThings.Sort(comparison);
            }

            chosen.Clear();
            Clear.Invoke(availableCounts, null);
            missingIngredients?.Clear();
            GenerateFrom.Invoke(availableCounts, new object[] { availableThings });
            for (int i = 0; i < ingredients.Count; i++)
            {
                IngredientCount ingredientCount = ingredients[i];
                bool flag = false;
                for (int j = 0; j < (int)Count.Invoke(availableCounts, null); j++)
                {
                    float num = ((bill != null) ? ((float)ingredientCount.CountRequiredOfFor(GetDef.Invoke(availableCounts, new object[] { j }) as ThingDef, bill.recipe, bill)) : ingredientCount.GetBaseCount());
                    if ((bill != null && !bill.recipe.ignoreIngredientCountTakeEntireStacks && num > (float)GetCount.Invoke(availableCounts, new object[] { j }) || !ingredientCount.filter.Allows(GetDef.Invoke(availableCounts, new object[] { j }) as ThingDef) || (bill != null && !ingredientCount.IsFixedIngredient && !bill.ingredientFilter.Allows(GetDef.Invoke(availableCounts, new object[] { j }) as ThingDef))))
                        continue;

                    for (int k = 0; k < availableThings.Count; k++)
                    {
                        if (availableThings[k].def != GetDef.Invoke(availableCounts, new object[] { j }) as ThingDef)
                            continue;

                        int num2 = availableThings[k].stackCount - ThingCountUtility.CountOf(chosen, availableThings[k]);
                        if (num2 > 0)
                        {
                            if (bill != null && bill.recipe.ignoreIngredientCountTakeEntireStacks)
                            {
                                ThingCountUtility.AddToList(chosen, availableThings[k], num2);
                                __result = true;
                                return false;
                            }

                            int num3 = Mathf.Min(Mathf.FloorToInt(num), num2);
                            ThingCountUtility.AddToList(chosen, availableThings[k], num3);
                            num -= num3;
                            if (num < 0.001f)
                            {
                                flag = true;
                                float count = (float)GetCount.Invoke(availableCounts, new object[] { j });
                                count -= num;
                                SetCount.Invoke(availableCounts, new object[] { j, count });
                                break;
                            }
                        }
                    }

                    if (flag)
                        break;
                }

                if (!flag)
                {
                    if (missingIngredients == null)
                    {
                        __result = false;
                        return false;
                    }

                    missingIngredients.Add(ingredientCount);
                }
            }

            if (missingIngredients != null)
            {
                __result = missingIngredients.Count == 0;
                return false;
            }

            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestBillIngredientsInSet_NoMix")]
    public class TryFindBestBillIngredientsInSet_NoMix_Patch
    {
        static bool Prefix(ref bool __result, List<Thing> availableThings, Bill bill, List<ThingCount> chosen, IntVec3 rootCell, bool alreadySorted, List<IngredientCount> missingIngredients)
        {
            if (WorkFinder.ForcedThing != null && !availableThings.Contains(WorkFinder.ForcedThing))
                availableThings.Add(WorkFinder.ForcedThing);
            return true;
        }
    }
}
