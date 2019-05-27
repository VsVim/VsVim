using Vim.Interpreter;

namespace Vim.VisualStudio
{
    public interface ICSharpScriptExecutor
    {
        void Execute(IVimBuffer vimBuffer, CallInfo callInfo, bool createEachTime);
    }
}
