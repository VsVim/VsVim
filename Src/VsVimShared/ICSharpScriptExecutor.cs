using Vim.Interpreter;

namespace Vim.VisualStudio
{
    public interface ICSharpScriptExecutor
    {
        void Execute(IVim vim, CallInfo callInfo, bool createEachTime);
    }
}
