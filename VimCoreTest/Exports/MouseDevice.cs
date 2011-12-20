using System.ComponentModel.Composition;
using Vim;

namespace Vim.UnitTest.Exports
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
