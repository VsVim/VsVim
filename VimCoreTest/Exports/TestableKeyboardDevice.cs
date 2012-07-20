using System.ComponentModel.Composition;

namespace Vim.UnitTest.Exports
{
    [Export(typeof(IKeyboardDevice))]
    class TestableKeyboardDevice : IKeyboardDevice
    {
        public bool IsArrowKeyDown
        {
            get { return false; }
        }
    }
}
