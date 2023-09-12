using BepInEx;
using HarmonyLib;
using KSP.Game;
using KSP.Messages;
using KSP.OAB;
using LuxsOABExtensions.Messages;
using System.Reflection;
using UnityEngine;

namespace LuxsOABExtensions.Patching
{
    [HarmonyPatch]
    internal static class LOABESymmetryMode
    {
        internal static SubscriptionHandle _loabeSymmetryChangedHandle;
        internal static void Initialize()
        {
            _loabeSymmetryChangedHandle = GameManager.Instance.Game.Messages.PersistentSubscribe<LOABESymmetryChangedMessage>(new Action<MessageCenterMessage>(LOABESymmetryChangedMessage));
        }

        internal static void OnDestroy()
        {
            _loabeSymmetryChangedHandle.Release();
        }

        #region Messages

        internal static void LOABESymmetryChangedMessage(MessageCenterMessage msg)
        {
            LOABESymmetryChangedMessage loabeSymmetryChangedMessage = msg as LOABESymmetryChangedMessage;
            NotificationData notificationData = new NotificationData();
            notificationData.Tier = NotificationTier.Passive;
            notificationData.Primary.LocKey = $"Symmetry Mode: {loabeSymmetryChangedMessage.SymmetryMode}x";
            GameManager.Instance.Game.Notifications.ProcessNotification(notificationData);
        }

        #endregion
        #region Essentials
        [HarmonyPrefix]
        [HarmonyPatch(typeof(SymmetrySetPositionerRadial), MethodType.Constructor, new Type[] { typeof(int) })]
        private static void OverrideSymmetrySetPositionerCreation(ref int size, out int __state)
        {
            //The original method excludes all sizes that aren't on the regular symmetry modes

            //Case a custom symmetry, pass the custom value, and set size to 2
            if (LuxsOABExtensions.CustomSymmetryMode)
            {
                __state = LuxsOABExtensions.CustomSymmetryValue;
                size = 2;
            }
            //Case is a LOABE Symmetry, pass the index of the LOABE Symmetry
            else if (LuxsOABExtensions.LOABESymmetryMode)
            {
                __state = size;
                size = 2;
            }
            //Else its a stock symmetry, so leave it alone
            else
            {
                __state = -1;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SymmetrySetPositionerRadial), MethodType.Constructor, new Type[] { typeof(int) })]
        private static void ApplyNewSymmetrySetSize(ref SymmetrySetPositionerRadial __instance, int __state)
        {
            if (__state != -1)
            {
                //It sets the actual size which was carried over with the __state argument
                FieldInfo fieldInfo = typeof(SymmetrySetPositionerRadial).GetField("_size", BindingFlags.Instance | BindingFlags.NonPublic);
                if (fieldInfo != null)
                    fieldInfo.SetValue(
                        __instance,
                        (LuxsOABExtensions.CustomSymmetryMode)
                            ? __state
                            : LuxsOABExtensions.RegularSymmetryModes[__state] //Turn index into actual value
                        );
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SymmetrySet), "CreatePositionerFromMode")]
        private static bool AllowForAllSymmetrySizes(ref ISymmetrySetPositioner __result, BuilderSymmetryMode mode)
        {
            //The original method excludes anything that isn't on the Enum
            if (LuxsOABExtensions.LOABESymmetryMode || LuxsOABExtensions.CustomSymmetryMode)
            {
                __result = new SymmetrySetPositionerRadial((int)mode);
                return false;
            }
            else
                return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SymmetrySet), "Create", new Type[] { typeof(ObjectAssemblyPartTracker), typeof(OABSessionInformation), typeof(IObjectAssemblyPart), typeof(BuilderSymmetryMode) })]
        private static bool AllowForSymmetryValueOf7OnCreate(ref ObjectAssemblyPartTracker partTracker, OABSessionInformation sessionInfo, ref IObjectAssemblyPart anchor, BuilderSymmetryMode symmetryMode)
        {
            //This is needed because the original method excludes the COUNT (7) symmetry
            if (symmetryMode != BuilderSymmetryMode.COUNT)
                return true;

            SymmetrySet symmetrySet = new SymmetrySet(partTracker, sessionInfo, anchor, symmetryMode);
            anchor.SymmetrySet = symmetrySet;
            partTracker.RegisterSymmetrySet(symmetrySet);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ObjectAssemblyBuilder.ObjectAssemblyBuilderEventsManager), "SymmetryMode_OnChanged")]
        private static bool AllowForSymmetryValueOf7OnChange(ref ObjectAssemblyBuilder.ObjectAssemblyBuilderEventsManager __instance)
        {
            //This is needed so that i can have a symmetry of value 7 (BuildSymmetryMode.COUNT)
            BuilderSymmetryMode value = __instance.builder.watcherStats.SymmetryMode.GetValue();
            if (value != BuilderSymmetryMode.COUNT)
                return true;

            IObjectAssemblyPart partGrabbed = __instance.builder._activePartTracker.partGrabbed;
            if (partGrabbed != null)
            {
                if (partGrabbed.SymmetrySet != null)
                {
                    partGrabbed.SymmetrySet.Dispose();
                }
                SymmetrySet.Create(__instance.builder._activePartTracker, __instance.builder.Stats, __instance.builder._activePartTracker.partGrabbed, value);
            }
            return false;
        }
        #endregion

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ObjectAssemblyToolbar), nameof(ObjectAssemblyToolbar.NextSymmetryMode))]
        private static bool OverrideNextSymmetryMode(ref ObjectAssemblyToolbar __instance)
        {
            if (__instance.OAB.ActivePartTracker.partGrabbed is not null && __instance.OAB.ActivePartTracker.PotentialParentPart is not null)
            {
                if (LuxsOABExtensions.TryFindConnectedNode(__instance.OAB.ActivePartTracker.partGrabbed, __instance.OAB.ActivePartTracker.PotentialParentPart, out var pair))
                {

                    if (!pair.targetNode.NodeSymmetryGroupID.IsNullOrWhiteSpace())
                    {
                        int count = 0;
                        foreach (var node in pair.targetNode.Owner.Nodes)
                        {
                            if (node.NodeSymmetryGroupID == pair.targetNode.NodeSymmetryGroupID)
                                count++;
                        }

                        if (LuxsOABExtensions.StockSymmetryModes.Contains(count))
                        {
                            LuxsOABExtensions.LOABESymmetryMode = false;
                            LuxsOABExtensions.CustomSymmetryMode = false;
                            count = LuxsOABExtensions.StockSymmetryModes.IndexOf(count);
                        }
                        else if (LuxsOABExtensions.RegularSymmetryModes.Contains(count))
                        {
                            LuxsOABExtensions.LOABESymmetryMode = true;
                            LuxsOABExtensions.CustomSymmetryMode = false;
                            count = LuxsOABExtensions.RegularSymmetryModes.IndexOf(count);
                        }
                        else
                        {
                            LuxsOABExtensions.CustomSymmetryMode = true;
                            LuxsOABExtensions.CustomSymmetryValue = count;
                        }

                        __instance.SetSymmetryToolByEnum((BuilderSymmetryMode)count);

                        return false; //Always skip original method, else changes will be overwritten
                    }
                }
            }
            if (!__instance.justHold)
            {
                if (Input.GetKey(KeyCode.LeftControl) || LuxsOABExtensions.LOABESymmetryMode || LuxsOABExtensions.CustomSymmetryMode)
                {
                    //Get current symmetry
                    int nextSymmetry = (int)__instance.OAB.Stats.SymmetryMode.GetValue() + 1;

                    //If it is in a custom symmetry mode (any value that ain't a regular symmetry)
                    if (LuxsOABExtensions.CustomSymmetryMode)
                    {
                        LuxsOABExtensions.CustomSymmetryMode = false;

                        int indexOfRegularSymmetry = -1;
                        //Find next regular symmetry
                        //This has to be done ie, if we have a 9way symmetry, we gotta find what's the next regular one
                        int difrence = int.MaxValue;

                        for (int i = LuxsOABExtensions.RegularSymmetryModes.Length - 1; i >= 0; i--)
                        {
                            int curSymmetry = LuxsOABExtensions.RegularSymmetryModes[i];
                            int currentDiffrence = curSymmetry - nextSymmetry;

                            if (currentDiffrence < 0)
                                break;

                            if (currentDiffrence < difrence)
                            {
                                difrence = currentDiffrence;
                                indexOfRegularSymmetry = i;

                                if (difrence == 0)
                                    break;
                            }
                        }

                        if (indexOfRegularSymmetry <= 6)
                        {
                            LuxsOABExtensions.LOABESymmetryMode = false;
                            return true;
                        }
                        else
                        {
                            LuxsOABExtensions.LOABESymmetryMode = true;
                            __instance.SetSymmetryToolByEnum((BuilderSymmetryMode)indexOfRegularSymmetry);
                            LuxsOABExtensions.UpdateSprite();
                            return false;
                        }
                    }

                    //If its on a stock symmetry, allow for stock behaviour
                    if (nextSymmetry >= 0 && nextSymmetry <= 6)
                    {
                        LuxsOABExtensions.LOABESymmetryMode = false;
                        return true;
                    }

                    //If its an LOABE Symmetry mode, set symmetry mode
                    if (nextSymmetry < LuxsOABExtensions.RegularSymmetryModes.Length)
                    {
                        LuxsOABExtensions.LOABESymmetryMode = true;
                        __instance.SetSymmetryToolByEnum((BuilderSymmetryMode)nextSymmetry);
                        GameManager.Instance.Game.Messages.Publish<LOABESymmetryChangedMessage>(new Messages.LOABESymmetryChangedMessage(LuxsOABExtensions.RegularSymmetryModes[nextSymmetry]));
                        LuxsOABExtensions.UpdateSprite();
                        //Has to be skiped since original method check if its between 0 and 6, and LOABE's modes are >6
                        __instance.justHold = false;
                        return false;
                    }

                    //If its greater than the loabe list, go to start
                    if (nextSymmetry >= LuxsOABExtensions.RegularSymmetryModes.Length)
                    {
                        LuxsOABExtensions.LOABESymmetryMode = false;
                        return true;
                    }
                }
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ObjectAssemblyToolbar), nameof(ObjectAssemblyToolbar.PreviousSymmetryMode))]
        private static bool OverridePreviousSymmetryMode(ref ObjectAssemblyToolbar __instance)
        {
            if (Input.GetKey(KeyCode.LeftControl) || LuxsOABExtensions.LOABESymmetryMode || LuxsOABExtensions.CustomSymmetryMode)
            {
                //Get current symmetry
                int previousSymmetry = (int)__instance.OAB.Stats.SymmetryMode.GetValue() - 1;

                //If it is in a custom symmetry mode (any value that ain't a regular symmetry)
                if (LuxsOABExtensions.CustomSymmetryMode)
                {
                    LuxsOABExtensions.CustomSymmetryMode = false;

                    int indexOfRegularSymmetry = LuxsOABExtensions.RegularSymmetryModes.Length - 1;
                    //Find next regular symmetry
                    //This has to be done ie, if we have a 9way symmetry, we gotta find what's the next regular one
                    int difrence = int.MaxValue;

                    for (int i = 0; i < LuxsOABExtensions.RegularSymmetryModes.Length; i++)
                    {
                        int curSymmetry = LuxsOABExtensions.RegularSymmetryModes[i];
                        int currentDiffrence = previousSymmetry - curSymmetry;

                        if (currentDiffrence < 0)
                            break;

                        if (currentDiffrence < difrence)
                        {
                            difrence = currentDiffrence;
                            indexOfRegularSymmetry = i;

                            if (difrence == 0)
                                break;
                        }
                    }

                    if (indexOfRegularSymmetry <= 6)
                    {
                        LuxsOABExtensions.LOABESymmetryMode = false;
                        return true;
                    }
                    else
                    {
                        LuxsOABExtensions.LOABESymmetryMode = true;
                        __instance.SetSymmetryToolByEnum((BuilderSymmetryMode)indexOfRegularSymmetry);
                        LuxsOABExtensions.UpdateSprite();
                        return false;
                    }
                }

                //If its on a stock symmetry, allow for stock behaviour
                if (previousSymmetry >= 0 && previousSymmetry <= 6)
                {
                    LuxsOABExtensions.LOABESymmetryMode = false;
                    return true;
                }

                //If its an LOABE Symmetry mode, set symmetry mode
                if (previousSymmetry > 6 && previousSymmetry < LuxsOABExtensions.RegularSymmetryModes.Length)
                {
                    LuxsOABExtensions.LOABESymmetryMode = true;
                    __instance.SetSymmetryToolByEnum((BuilderSymmetryMode)previousSymmetry);
                    GameManager.Instance.Game.Messages.Publish<LOABESymmetryChangedMessage>(new Messages.LOABESymmetryChangedMessage(LuxsOABExtensions.RegularSymmetryModes[previousSymmetry]));
                    LuxsOABExtensions.UpdateSprite();
                    //Has to be skiped since original method check if its between 0 and 6, and LOABE's modes are >6
                    return false;
                }

                //If its greater than the loabe list, go to start
                if (previousSymmetry < 0)
                {
                    LuxsOABExtensions.LOABESymmetryMode = true;
                    __instance.SetSymmetryToolByEnum((BuilderSymmetryMode)LuxsOABExtensions.RegularSymmetryModes.Length - 1);
                    LuxsOABExtensions.UpdateSprite();
                    return false;
                }
            }
            return true;
        }
    }
}
