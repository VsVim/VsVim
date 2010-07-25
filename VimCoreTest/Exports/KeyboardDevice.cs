using System.ComponentModel.Composition;
using Vim;

namespace VimCore.Test.Exports
{
    [Export(typeof(IKeyboardDevice))]
    class KeyboardDevice : IKeyboardDevice
    {
        public bool IsKeyDown(KeyInput value)
        {
            return false;
        }
    }
}
