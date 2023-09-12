using HarmonyLib;
using KSP.Game;
using KSP.Messages;
using KSP.OAB;
using LuxsOABExtensions.Messages;
using System.Reflection.Emit;
using UnityEngine;

namespace LuxsOABExtensions.Patching
{
    [HarmonyPatch]
    internal class LOABEAngleSnap
    {
        internal static SubscriptionHandle _loabeAngleSnapChangedHandle;
        internal static void Initialize()
        {
            _loabeAngleSnapChangedHandle = GameManager.Instance.Game.Messages.PersistentSubscribe<LOABEAngleSnapChangedMessage>(new Action<MessageCenterMessage>(LOABEAngleSnapChangedMessage));
        }

        internal static void OnDestroy()
        {
            _loabeAngleSnapChangedHandle.Release();
        }

        #region Messages

        internal static void LOABEAngleSnapChangedMessage(MessageCenterMessage msg)
        {
            LOABEAngleSnapChangedMessage loabeAngleSnapChangedMessage = msg as LOABEAngleSnapChangedMessage;
            NotificationData notificationData = new NotificationData();
            notificationData.Tier = NotificationTier.Passive;
            if (loabeAngleSnapChangedMessage.AngleSnap > 0)
                notificationData.Primary.LocKey = $"Angle Snap: {loabeAngleSnapChangedMessage.AngleSnap}º";
            else
                notificationData.Primary.LocKey = $"Angle Snap: Free";
            LuxsOABExtensions.AngleSnap = loabeAngleSnapChangedMessage.AngleSnap;
            GameManager.Instance.Game.Notifications.ProcessNotification(notificationData);
        }

        #endregion

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
                    //yield return new CodeInstruction(OpCodes.Ldsfld, typeof(LuxsOABExtensions).GetField(nameof(LuxsOABExtensions.AngleSnap), AccessTools.all));
                    yield return new CodeInstruction(OpCodes.Callvirt, typeof(LOABEAngleSnap).GetMethod(nameof(GetAngleSnap), AccessTools.all));
                    continue;
                }
                yield return instruction;
            }
        }

        internal static float GetAngleSnap()
        {

            return LuxsOABExtensions.AngleSnap;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ObjectAssemblyToolbar), nameof(ObjectAssemblyToolbar.ToggleAngleSnap))]
        internal static bool HandleLOABEAngleSnapMode(ref ObjectAssemblyToolbar __instance)
        {
            //with left control pressed, will cycle through free>1>5>...>60>90>free>
            if (Input.GetKey(KeyCode.LeftControl))
            {
                if (Input.GetKey(KeyCode.LeftShift))
                {
                    LuxsOABExtensions.LOABEAngleSnapIndex--;
                    if (LuxsOABExtensions.LOABEAngleSnapIndex < 0)
                    {
                        LuxsOABExtensions.LOABEAngleSnapIndex = LuxsOABExtensions.RegularAngleSnapModes.Length - 1;
                    }
                }
                else
                {
                    LuxsOABExtensions.LOABEAngleSnapIndex++;
                    if (LuxsOABExtensions.LOABEAngleSnapIndex >= LuxsOABExtensions.RegularAngleSnapModes.Length)
                    {
                        LuxsOABExtensions.LOABEAngleSnapIndex = 0;
                    }
                }

                __instance.toggleOABSnapProp.SetValueInternal(LuxsOABExtensions.LOABEAngleSnapIndex > 0);

                float angleSnap = LuxsOABExtensions.RegularAngleSnapModes[LuxsOABExtensions.LOABEAngleSnapIndex];
                GameManager.Instance.Game.Messages.Publish(new LOABEAngleSnapChangedMessage(angleSnap));
                return false;
            }
            return true;
        }
    }
}
