#if VS_SPECIFIC_2017 || VS_SPECIFIC_2019

using Vim.Interpreter;

namespace Vim.VisualStudio.Specific
{
    public class CSharpScriptGlobals
    {
        public string Name { get; } = string.Empty;
        public string Arguments { get; } = string.Empty;
        public LineRangeSpecifier LineRange { get; }
        public bool IsScriptLocal { get; } = false;
        public IVim Vim { get; } = null;
        public IVimBuffer VimBuffer { get; } = null;

        public CSharpScriptGlobals(CallInfo callInfo, IVimBuffer vimBuffer)
        {
            Name = callInfo.Name;
            Arguments = callInfo.Arguments;
            LineRange = callInfo.LineRange;
            IsScriptLocal = callInfo.IsScriptLocal;
            Vim = vimBuffer.Vim;
            VimBuffer = vimBuffer;
        }
    }
}
#endif
