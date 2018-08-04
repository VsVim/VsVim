using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.Text;
using Vim;

namespace Vim.Plugin.Implementation.CSharpScriptRunner
{
    public class CSharpScriptParam
    {
        public IVimBuffer VimBuffer { get; } = null;
        public IWpfTextView TextView { get; } = null;

        public CSharpScriptParam(IVimBuffer _vimBuffer,IWpfTextView _textView)
        {
            VimBuffer = _vimBuffer;
            TextView = _textView;
        }

        public void DisplayStatus(string input)
        {
            VimBuffer.VimBufferData.StatusUtil.OnStatus(input);
        }

        public void Process(KeyInput keyInput)
        {
            VimBuffer.ProcessFromScript(keyInput);
        }

        public void Process(string input, bool enter = false)
        {
            foreach (var c in input)
            {
                var i = KeyInputUtil.CharToKeyInput(c);
                Process(i);
            }

            if (enter)
            {
                Process(KeyInputUtil.EnterKey);
            }
        }

    }
}
