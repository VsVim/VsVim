using Vim.Interpreter;

namespace Vim.VisualStudio.Specific
{
    internal sealed class NotSupportedCSharpScriptExecutor : ICSharpScriptExecutor
    {
        internal static readonly ICSharpScriptExecutor Instance = new NotSupportedCSharpScriptExecutor();

        void ICSharpScriptExecutor.Execute(IVim vim, CallInfo callInfo, bool createEachTime)
        {
            vim.ActiveStatusUtil.OnError("csx not supported");
        }
    }
}
