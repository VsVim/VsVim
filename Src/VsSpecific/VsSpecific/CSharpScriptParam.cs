#if VS_SPECIFIC_2017
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.Text;
using Vim;
using Vim.Interpreter;

namespace Vim.VisualStudio.Specific
{
    public class CSharpScriptParam
    {
        public string Name { get; } = string.Empty;
        public string Arguments { get; } = string.Empty;
        public LineRangeSpecifier LineRange { get; }
        public bool IsScriptLocal { get; } = false;
        public IVimBuffer VimBuffer { get; } = null;
        public IWpfTextView TextView { get; } = null;
        public DTE2 DTE { get; }

        public CSharpScriptParam(IVimBuffer _vimBuffer,IWpfTextView _textView,CallInfo _callInfo)
        {
            VimBuffer = _vimBuffer;
            TextView = _textView;
            Name = _callInfo.Name;
            Arguments = _callInfo.Arguments;
            LineRange = _callInfo.LineRange;
            IsScriptLocal = _callInfo.IsScriptLocal;
            DTE = Package.GetGlobalService(typeof(DTE)) as DTE2;

        }

        public void DisplayStatus(string input)
        {
            VimBuffer.VimBufferData.StatusUtil.OnStatus(input);
        }
        public void DisplayStatusLong(IEnumerable<string> value)
        {
            VimBuffer.VimBufferData.StatusUtil.OnStatusLong(value);
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
#endif