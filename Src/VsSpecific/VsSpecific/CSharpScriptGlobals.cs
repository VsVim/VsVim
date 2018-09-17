#if VS_SPECIFIC_2017

using Vim.Interpreter;

namespace Vim.VisualStudio.Specific
{
    public class CSharptScripGlobals
    {
        public string Name { get; } = string.Empty;
        public string Arguments { get; } = string.Empty;
        public LineRangeSpecifier LineRange { get; }
        public bool IsScriptLocal { get; } = false;
        public IVim Vim { get; } = null;

        public CSharptScripGlobals(CallInfo callInfo, IVim vim)
        {
            Name = callInfo.Name;
            Arguments = callInfo.Arguments;
            LineRange = callInfo.LineRange;
            IsScriptLocal = callInfo.IsScriptLocal;
            Vim = vim;
        }
    }
}
#endif
