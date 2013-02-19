using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vim.UI.Wpf.Implementation.Keyboard
{
    internal sealed class VimKeyData
    {
        internal static readonly VimKeyData DeadKey = new VimKeyData();

        internal readonly KeyInput KeyInputOptional;
        internal readonly string TextOptional;
        internal readonly bool IsDeadKey;

        internal VimKeyData(KeyInput keyInput, string text)
        {
            Contract.Assert(keyInput != null);
            KeyInputOptional = keyInput;
            TextOptional = text;
            IsDeadKey = false;
        }

        private VimKeyData()
        {
            IsDeadKey = true;
        }

        public override string ToString()
        {
            if (IsDeadKey)
            {
                return "<dead key>";
            }

            return String.Format("{0} - {1}", KeyInputOptional, TextOptional);
        }
    }
}
