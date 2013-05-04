using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vim;

namespace VimApp
{
    internal static class Extensions
    {
        internal static void Process(this IVimBuffer vimBuffer, string input, bool enter = false)
        {
            foreach (var c in input)
            {
                var keyInput = KeyInputUtil.CharToKeyInput(c);
                vimBuffer.Process(keyInput);
            }

            if (enter)
            {
                vimBuffer.Process(KeyInputUtil.EnterKey);
            }
        }
    }
}
