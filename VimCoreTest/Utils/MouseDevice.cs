using System.ComponentModel.Composition;
using Vim;

namespace VimCore.Test.Utils
{
    [Export(typeof(IMouseDevice))]
    public class MouseDevice : IMouseDevice
    {
        public bool IsLeftButtonPressed
        {
            get { return false; }
        }
    }
}
