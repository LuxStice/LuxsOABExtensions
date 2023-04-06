using HarmonyLib;
using I2.Loc;
using KSP.OAB;
using KSP.Sim.ResourceSystem;
using System;
using System.Reflection;
using UnityEngine;

namespace LuxOABExtensions;

[HarmonyPatch]
public static class Patcher
{
    [HarmonyPatch(typeof(SymmetrySetPositionerRadial), MethodType.Constructor, new Type[] { typeof(int) })]
    static void Prefix(ref int Size, out int __state)
    {
        if (Size == 0 || Size == 1 || Size == 2 || Size == 3 || Size == 4 || Size == 6 || Size == 8)
        {
            __state = -1;
        }
        else
        {
            __state = Size;
            Size = 2;
        }
    }

    [HarmonyPatch(typeof(SymmetrySetPositionerRadial), MethodType.Constructor, new Type[] { typeof(int) })]
    public static void Postfix(ref SymmetrySetPositionerRadial __instance, int __state)
    {
        FieldInfo fieldInfo = typeof(SymmetrySetPositionerRadial).GetField("_size", BindingFlags.Instance | BindingFlags.NonPublic);
        if (fieldInfo != null)
            fieldInfo.SetValue(__instance, __state);
    }

    [HarmonyPatch(typeof(AssemblyPartsPicker.FilterUtils), "GetSizeFullName")]
    private static bool Prefix(ref string __result, AssemblySizeFilterType filterSubType)
    {
        if ((int)filterSubType <= 11)
        {
            return true;
        }

        __result = LuxOABExtensions.GetByID((int)filterSubType).FullName;
        //__result = OABLocalization.GetTranslation("VAB/Size/" + EditorExtensionsPlugin.GetByID((int)filterSubType).FullName);

        return false;
    }

    [HarmonyPatch(typeof(AssemblyPartsPicker.FilterUtils), "GetSizeAbbreviation")]
    public static bool Prefix(ref string __result, AssemblySizeFilterType filterSubType, out string __state)
    {
        if ((int)filterSubType <= 11)
        {
            __state = string.Empty;
            return true;
        }

        __result = LuxOABExtensions.GetByID((int)filterSubType).AbbreviatedName;
        __state = __result;
        return false;
    }

    [HarmonyPatch(typeof(AssemblyPartsPicker.FilterUtils), "GetSizeAbbreviation")]
    public static void Postfix(ref string __result, string __state)
    {
        if (string.IsNullOrEmpty(__state))
            return;
        else
            __result = __state;
    }

    [HarmonyPatch(typeof(ObjectAssemblyPartTracker), "Initialize")]
    public static void Prefix(ref ObjectAssemblyPartTracker __instance)
    {
        __instance.AssemblySizeTypeDiameters.AddRange(LuxOABExtensions.CustomSizes.Select((a) => a.Diameter));
    }
    [HarmonyPatch(typeof(ObjectAssemblyPartTracker), "Initialize")]
    public static void Postfix(ref ObjectAssemblyPartTracker __instance)
    {
        var list = new Dictionary<string, OABSize>();

        __instance._allKnownParts.ForEach(a =>
        {
            OABSize size = LuxOABExtensions.GetByID((int)a.Size);
            if (a.Name.Contains("_m2_"))
            {
                size = LuxOABExtensions.GetByName("Mk2");
                LuxOABExtensions.overrides.Add(a.Name, size);
            }
            if (a.Name.Contains("_m3_"))
            {
                size = LuxOABExtensions.GetByName("Mk3");
                LuxOABExtensions.overrides.Add(a.Name, size);
            }
        });
    }

    [HarmonyPatch(typeof(OABPartData), "Size", MethodType.Getter)]
    private static bool Prefix(ref OABPartData __instance, ref AssemblySizeFilterType __result)
    {
        if (__instance.PartData.sizeCategory == MetaAssemblySizeFilterType.Auto)
        {
            return true;
        }
        if (!LuxOABExtensions.overrides.ContainsKey(__instance.Name))
        {
            return true;
        }

        __result = (AssemblySizeFilterType)LuxOABExtensions.overrides[__instance.Name].ID;
        return false;
    }

    [HarmonyPatch(typeof(AssemblyPartsPicker), "SortPartListByCategory")]
    public static bool Prefix(List<IObjectAssemblyAvailablePart> parts, PartCategories category, ref List<IObjectAssemblyAvailablePart> __result, ref AssemblyPartsPicker __instance)
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

                        if (LuxOABExtensions.overrides.ContainsKey(part.Name))
                            Size = (AssemblySizeFilterType)LuxOABExtensions.overrides[part.Name].ID;

                        if (!sizeLookupTable.TryGetValue(Size, out list))
                        {
                            list = new List<IObjectAssemblyAvailablePart>();
                            sizeLookupTable[Size] = list;
                        }
                        list.Add(part);
                    }
                    parts = new List<IObjectAssemblyAvailablePart>();
                    foreach (OABSize size in LuxOABExtensions.Sizes)
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

        (bool useColor, Color tagColor) SizeColor = LuxOABExtensions.GetByID(filterSubType).TagColor;

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
                    int count = LuxOABExtensions.Sizes.Count;
                    __instance.ResizeFilterCount(count);
                    int i = 0;
                    int reverseCount = count - 1;
                    while (i < count)
                    {
                        AssemblyFilterContainer filterContainer = __instance._partFilterContainers[i];
                        int order = (__instance._isSortOrderAscending.GetValue() ? i : reverseCount);
                        filterContainer.SetFilter(AssemblyPartFilterType.Size, LuxOABExtensions.Sizes[i].ID);
                        string headerName = OABLocalization.GetTranslation("VAB/Size/" + LuxOABExtensions.Sizes[order].FullName);
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
        __instance.sizeText.text = LuxOABExtensions.GetByID((int)part.Size).AbbreviatedName;
        //__instance.sizeText.text = __instance.TryToLocalizeToolTipString(EditorExtensionsPlugin.GetByID((int)part.Size).AbbreviatedName);
        __instance.sizeText.color = LuxOABExtensions.GetByID((int)part.Size).Color;
#pragma warning restore CS0012
        return false;
    }

    [HarmonyPatch]
    private class NextSymmetryModePatch
    {
        [HarmonyPatch(typeof(ObjectAssemblyToolbar), "NextSymmetryMode")]
        private static bool Prefix(ref ObjectAssemblyToolbar __instance)
        {
            if (!__instance.justHold)
            {
                int index = (int)__instance.OAB.Stats.SymmetryMode.GetValue();

                index++;

                if (index >= LuxOABExtensions.RegularSymmetryModes.Length)
                {
                    index = 0;
                }
                int symmetry = LuxOABExtensions.RegularSymmetryModes[index];
                LuxOABExtensions.SymmetryValue = symmetry;
                Debug.Log($"Set symmetry to x{symmetry}");
                __instance.SetSymmetryToolByEnum((BuilderSymmetryMode)index);
            }
            __instance.justHold = false;
            return false;
        }
    }


    [HarmonyPatch]
    private class PreviousSymmetryModePatch
    {
        [HarmonyPatch(typeof(ObjectAssemblyToolbar), "PreviousSymmetryMode")]
        private static bool Prefix(ref ObjectAssemblyToolbar __instance)
        {
            int index = (int)__instance.OAB.Stats.SymmetryMode.GetValue();
            index--;

            if (index < 0)
            {
                index = LuxOABExtensions.RegularSymmetryModes.Length-1;
            }
            int symmetry = LuxOABExtensions.RegularSymmetryModes[index];
            LuxOABExtensions.SymmetryValue = symmetry;
            Debug.Log($"Set symmetry to x{symmetry}");
            __instance.SetSymmetryToolByEnum((BuilderSymmetryMode)index);
            return false;
        }
    }
}