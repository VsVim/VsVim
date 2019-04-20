using Vim.Interpreter;

namespace Vim.VisualStudio
{
    public interface ICSharpScriptExecutor
    {
        void Execute(IVim vim, IVimBuffer vimBuffer, CallInfo callInfo, bool createEachTime);
    }
}
