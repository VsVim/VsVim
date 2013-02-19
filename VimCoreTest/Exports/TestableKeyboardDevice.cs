using System.ComponentModel.Composition;

namespace Vim.UnitTest.Exports
{
    [Export(typeof(IKeyboardDevice))]
    public sealed class TestableKeyboardDevice : IKeyboardDevice
    {
        public bool IsArrowKeyDown
        {
            get { return false; }
        }
    }
}
