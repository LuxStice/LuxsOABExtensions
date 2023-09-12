using BepInEx;
using HarmonyLib;
using I2.Loc;
using KSP.OAB;
using KSP.Sim.ResourceSystem;
using KSP.UI;
using System;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace LuxsOABExtensions;

[HarmonyPatch]
static class Patcher
{

    [HarmonyPatch(typeof(ObjectAssemblyPartTracker), "Initialize")]
    private static void Prefix(ref ObjectAssemblyPartTracker __instance)
    {
        __instance.AssemblySizeTypeDiameters.AddRange(LuxsOABExtensions.CustomSizes.Select((a) => a.Diameter));
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ObjectAssemblyPartTracker), "Initialize")]
    private static void UpdateAllMkParts(ref ObjectAssemblyPartTracker __instance)
    {
        if (LuxsOABExtensions.listSet)
            return;
        var list = new Dictionary<string, LOABESize>();
        __instance._allKnownParts.ForEach(a =>
        {
            try
            {
                LOABESize size = LuxsOABExtensions.GetByID((int)a.Size);
                if (a.Name.Contains("m2"))
                {
                    size = LuxsOABExtensions.GetByAbbreviation("Mk2");
                    LuxsOABExtensions.overrides.Add(a.Name, size);
                }
                if (a.Name.Contains("m3"))
                {
                    size = LuxsOABExtensions.GetByAbbreviation("Mk3");
                    LuxsOABExtensions.overrides.Add(a.Name, size);
                }
            }
            catch { }
        });
        LuxsOABExtensions.listSet = true;
    }

    [HarmonyPatch(typeof(OABPartData), "Size", MethodType.Getter)]
    private static bool Prefix(ref OABPartData __instance, ref AssemblySizeFilterType __result)
    {
        if (__instance.PartData.sizeCategory == MetaAssemblySizeFilterType.Auto)
        {
            return true;
        }
        if (!LuxsOABExtensions.overrides.ContainsKey(__instance.Name))
        {
            return true;
        }

        __result = (AssemblySizeFilterType)LuxsOABExtensions.overrides[__instance.Name].ID;
        return false;
    }

    [HarmonyPatch(typeof(AssemblyPartsPicker), "SortPartListByCategory")]
    private static bool Prefix(List<IObjectAssemblyAvailablePart> parts, PartCategories category, ref List<IObjectAssemblyAvailablePart> __result, ref AssemblyPartsPicker __instance)
    {
        List<IObjectAssemblyAvailablePart> list = null;
        SortingType sortingType = SortingType.Alphabetical;
        foreach (PartCategorySortingType partCategorySortingType in __instance.partCategorySortingTypes)
        {
            if (partCategorySortingType.category == category)
            {
                sortingType = partCategorySortingType.sortingType;
                break;
            }
        }
        switch (sortingType)
        {
            case SortingType.Size:
                {
                    parts.Sort(new Comparison<IObjectAssemblyAvailablePart>(__instance.PartComparison));
                    Dictionary<AssemblySizeFilterType, List<IObjectAssemblyAvailablePart>> sizeLookupTable = new Dictionary<AssemblySizeFilterType, List<IObjectAssemblyAvailablePart>>();
                    foreach (IObjectAssemblyAvailablePart part in parts)
                    {
                        AssemblySizeFilterType Size = part.Size;

                        if (LuxsOABExtensions.overrides.ContainsKey(part.Name))
                            Size = (AssemblySizeFilterType)LuxsOABExtensions.overrides[part.Name].ID;

                        if (!sizeLookupTable.TryGetValue(Size, out list))
                        {
                            list = new List<IObjectAssemblyAvailablePart>();
                            sizeLookupTable[Size] = list;
                        }
                        list.Add(part);
                    }
                    parts = new List<IObjectAssemblyAvailablePart>();
                    foreach (LOABESize size in LuxsOABExtensions.Sizes)
                    {
                        list = null;
                        if (sizeLookupTable.TryGetValue((AssemblySizeFilterType)size.ID, out list))
                        {
                            parts.AddRange(list);
                        }
                    }
                    __result = parts;
                    return false;
                }
            default:
                return true;
        }
    }


    [HarmonyPatch(typeof(AssemblyPartsPicker), nameof(AssemblyPartsPicker.SetVisiblePartFilters))]
    private static bool Prefix(ref AssemblyPartsPicker __instance, List<IObjectAssemblyAvailablePart> parts)
    {
        List<AssemblyFilterContainer> partFilterContainers = __instance._partFilterContainers;
        switch (__instance.currentFilter)
        {
            case AssemblyPartFilterType.Size:
                {
                    int count = LuxsOABExtensions.Sizes.Count;
                    __instance.ResizeFilterCount(count);
                    int i = 0;
                    int reverseCount = count - 1;
                    while (i < count)
                    {
                        AssemblyFilterContainer filterContainer = __instance._partFilterContainers[i];
                        int order = (__instance._isSortOrderAscending.GetValue() ? i : reverseCount);
                        filterContainer.SetFilter(AssemblyPartFilterType.Size, LuxsOABExtensions.Sizes[i].ID);
                        string headerName = OABLocalization.GetTranslation("VAB/Size/" + LuxsOABExtensions.Sizes[order].FullName);
                        __instance.SetAssemblyFilterHeaderName(ref filterContainer, headerName);
                        filterContainer.SetFilterColor(AssemblyPartsPicker.GetFilterColorFromFilterEnum(order, __instance.filterColors));
                        filterContainer.SetFilterHighlightColor(AssemblyPartsPicker.GetFilterColorFromFilterEnum(order, __instance.filterHighlightColors));
                        i++;
                        reverseCount--;
                    }
                    break;
                }
            default:
                return true;
        }
        __instance.UpdateFilterButtons();
        return false;
    }



    [HarmonyPatch(typeof(PartPlacementVFX), nameof(PartPlacementVFX.LoadAttachVFX))]
    [HarmonyPrefix]
    internal static bool ShouldShowPlacementVFX() => LuxsOABExtensions.Instance.placementConfig.Value;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ManipulationWidget), nameof(ManipulationWidget.CalculateTranslateLimits))]
    internal static void SetTranslationLimit(ref ManipulationWidget __instance)
    {
        __instance.translateLimits *= LuxsOABExtensions.Instance.partTranslationLimitConfig.Value;
    }

}