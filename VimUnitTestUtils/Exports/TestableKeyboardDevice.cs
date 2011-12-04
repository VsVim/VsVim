using System.ComponentModel.Composition;

namespace Vim.UnitTest.Exports
{
    [Export(typeof(IKeyboardDevice))]
    class TestableKeyboardDevice : IKeyboardDevice
    {
        public bool IsKeyDown(VimKey value)
        {
            return false;
        }
    }
}
