﻿using BepInEx;
using HarmonyLib;
using I2.Loc;
using KSP.OAB;
using KSP.Sim.ResourceSystem;
using System;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace LuxsOABExtensions;

[HarmonyPatch]
static class Patcher
{
    [HarmonyPatch(typeof(AssemblyPartsPicker.FilterUtils), "GetSizeFullName")]
    private static bool Prefix(ref string __result, AssemblySizeFilterType filterSubType)
    {
        if ((int)filterSubType <= 11)
        {
            return true;
        }

        __result = LuxsOABExtensions.GetByID((int)filterSubType).FullName;
        //__result = OABLocalization.GetTranslation("VAB/Size/" + EditorExtensionsPlugin.GetByID((int)filterSubType).FullName);

        return false;
    }

    [HarmonyPatch(typeof(AssemblyPartsPicker.FilterUtils), "GetSizeAbbreviation")]
    private static void Postfix(ref string __result, AssemblySizeFilterType filterSubType)
    {
        if ((int)filterSubType <= 11)
        {
            return;
        }

        __result = LuxsOABExtensions.GetByID((int)filterSubType).AbbreviatedName;
    }

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

    [HarmonyPatch(typeof(AssemblyPartsPicker), "GetFilterColorFromFilterEnum")]
    [HarmonyPatch(typeof(AssemblyPartsPicker), "GetFilterHighlightColorFromFilterEnum")]
    private static bool Prefix(ref Color __result, int filterSubType)
    {
        if (filterSubType <= 11)
            return true;

        (bool useColor, Color tagColor) SizeColor = LuxsOABExtensions.GetByID(filterSubType).TagColor;

        if (SizeColor.useColor)
        {
            __result = SizeColor.tagColor;
            return false;
        }
        else
            return true;
    }

    [HarmonyPatch(typeof(AssemblyPartsPicker), "SetVisiblePartFilters")]
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
                        filterContainer.SetFilterColor(__instance.GetFilterColorFromFilterEnum(order));
                        filterContainer.SetFilterHighlightColor(__instance.GetFilterHighlightColorFromFilterEnum(order));
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

    [HarmonyPatch(typeof(ObjectAssemblyFlexibleModal), "ConfigureSizeInfo")]
    private static bool Prefix(IObjectAssemblyAvailablePart part, ref ObjectAssemblyFlexibleModal __instance)
    {
        if ((int)part.Size <= 11)
            return true;
#pragma warning disable CS0012
        __instance.sizeText.text = LuxsOABExtensions.GetByID((int)part.Size).AbbreviatedName;
        //__instance.sizeText.text = __instance.TryToLocalizeToolTipString(EditorExtensionsPlugin.GetByID((int)part.Size).AbbreviatedName);
        __instance.sizeText.color = LuxsOABExtensions.GetByID((int)part.Size).Color;
#pragma warning restore CS0012
        return false;
    }


    [HarmonyPatch(typeof(ObjectAssemblyPlacementTool), nameof(ObjectAssemblyPlacementTool.PerformSurfaceOffsetAdjustment))]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> ChangeSnapAngle(IEnumerable<CodeInstruction> instructions)
    {

        var enumerator = instructions.GetEnumerator();
        int index = -1;

        while (enumerator.MoveNext())
        {
            var instruction = enumerator.Current;
            index++;
            if (instruction.opcode == OpCodes.Ldc_R4 && index == 0)
            {
                yield return new CodeInstruction(OpCodes.Ldsfld, typeof(LuxsOABExtensions).GetField("AngleSnap", AccessTools.all));
                continue;
            }
            yield return instruction;
        }
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