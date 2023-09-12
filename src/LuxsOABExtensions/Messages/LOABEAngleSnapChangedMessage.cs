using KSP.Messages;

namespace LuxsOABExtensions.Messages
{
    internal class LOABEAngleSnapChangedMessage : SettingsMessageBase
    {
        internal float AngleSnap;

        public LOABEAngleSnapChangedMessage(float AngleSnap)
        {
            this.AngleSnap = AngleSnap;
        }
    }
}
