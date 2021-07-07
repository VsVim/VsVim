#if VS_UNIT_TEST_HOST
using System.ComponentModel.Composition;

namespace Vim.UnitTest
{
    [Export(typeof(IKeyboardDevice))]
    public sealed class TestableKeyboardDevice : IKeyboardDevice
    {
        public bool IsArrowKeyDown
        {
            get { return false; }
        }

        public VimKeyModifiers KeyModifiers
        {
            get { return VimKeyModifiers.None; }
        }
    }
}
#endif