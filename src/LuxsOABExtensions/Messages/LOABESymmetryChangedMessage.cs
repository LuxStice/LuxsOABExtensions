using KSP.Messages;

namespace LuxsOABExtensions.Messages
{
    internal class LOABESymmetryChangedMessage : SettingsMessageBase
    {
        internal int SymmetryMode;

        public LOABESymmetryChangedMessage(int SymmetryMode)
        {
            this.SymmetryMode = SymmetryMode;
        }
    }
}
