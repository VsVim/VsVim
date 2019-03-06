using Vim.Interpreter;

namespace Vim.VisualStudio.Specific
{
#if VS_SPECIFIC_2017 || VS_SPECIFIC_2019

    internal partial class SharedService
    {
        private ICSharpScriptExecutor _cSharpScriptExecutor = new CSharpScriptExecutor();

        private void RunCSharpScript(IVim vim, CallInfo callInfo, bool createEachTime)
        {
            _cSharpScriptExecutor.Execute(vim, callInfo, createEachTime);
        }
    }

#else

    internal partial class SharedService
    {
        private void RunCSharpScript(IVim vim, CallInfo callInfo, bool createEachTime)
        {
           vim.ActiveStatusUtil.OnError("csx not supported");
        }
    }

#endif
}

