using System;
using System.ComponentModel.Composition;

namespace Vim.Mac
{
    [Export(typeof(IKeyboardDevice))]
    internal sealed class KeyboardDevice : IKeyboardDevice
    {
        public bool IsArrowKeyDown => throw new NotImplementedException();

        public VimKeyModifiers KeyModifiers => throw new NotImplementedException();
    }
}
