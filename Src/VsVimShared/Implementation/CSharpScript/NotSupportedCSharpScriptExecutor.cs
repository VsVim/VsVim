using Vim.Interpreter;

namespace Vim.VisualStudio.Implementation.CSharpScript
{
    internal sealed class NotSupportedCSharpScriptExecutor : ICSharpScriptExecutor
    {
        internal static readonly ICSharpScriptExecutor Instance = new NotSupportedCSharpScriptExecutor();

        void ICSharpScriptExecutor.Execute(IVimBuffer vimBuffer, CallInfo callInfo, bool createEachTime)
        {
            vimBuffer.VimBufferData.StatusUtil.OnError("csx not supported");
        }
    }
}
