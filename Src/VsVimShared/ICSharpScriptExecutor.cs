using Vim.Interpreter;

namespace Vim.VisualStudio
{
    public interface ICSharpScriptExecutor
    {
        void Execute(CallInfo callInfo, bool createEachTime);
    }
}
