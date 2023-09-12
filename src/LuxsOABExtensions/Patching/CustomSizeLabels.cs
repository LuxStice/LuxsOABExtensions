using HarmonyLib;
using KSP.OAB;
using KSP.UI;
using UnityEngine;

namespace LuxsOABExtensions.Patching
{
    [HarmonyPatch]
    internal static class CustomSizeLabels
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(PartInfoOverlay), nameof(PartInfoOverlay.ConfigureSizeInfo))]
        internal static bool SetColorAndAbrName(IObjectAssemblyAvailablePart part, ref PartInfoOverlay __instance)
        {
            if ((int)part.Size <= 11)
                return true;

            int partID = (int)part.Size;
            var loabeSize = LuxsOABExtensions.GetByID(partID);

            __instance._overlayContext.PartInfoSizeText.SetValue(loabeSize.AbbreviatedName);
            __instance._overlayContext.PartInfoSizeColor.SetValue(loabeSize.Color);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(AssemblyPartsPicker.FilterUtils), nameof(AssemblyPartsPicker.FilterUtils.GetSizeFullName))]
        private static bool GetLOABEFullName(ref string __result, AssemblySizeFilterType filterSubType)
        {
            if ((int)filterSubType <= 11)
            {
                return true;
            }

            __result = LuxsOABExtensions.GetByID((int)filterSubType).FullName;

            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(AssemblyPartsPicker.FilterUtils), nameof(AssemblyPartsPicker.FilterUtils.GetSizeAbbreviation))]
        private static void GetLOABEAbbreviation(ref string __result, AssemblySizeFilterType filterSubType)
        {
            if ((int)filterSubType <= 11)
            {
                return;
            }

            __result = LuxsOABExtensions.GetByID((int)filterSubType).AbbreviatedName;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(AssemblyPartsPicker), nameof(AssemblyPartsPicker.GetFilterColorFromFilterEnum))]
        private static bool GetLOABEFilterColor(ref Color __result, int filterSubType, PartSizeColorData filterColors)
        {
            if (filterSubType <= 11)
                return true;

            var SizeColor = LuxsOABExtensions.GetByID(filterSubType).TagColor;

            if (SizeColor.useColor)
            {
                __result = SizeColor.tagColor;
                return false;
            }
            else
                return true;
        }
    }
}
