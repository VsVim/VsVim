using Vim.Interpreter;

namespace Vim.VisualStudio.Specific
{
#if VS_SPECIFIC_2017 || VS_SPECIFIC_2019

    internal partial class SharedService
    {
        private ICSharpScriptExecutor _cSharpScriptExecutor = new CSharpScriptExecutor();

        private void RunCSharpScript(IVimBuffer vimBuffer, CallInfo callInfo, bool createEachTime)
        {
            _cSharpScriptExecutor.Execute(vimBuffer, callInfo, createEachTime);
        }
    }

#else

    internal partial class SharedService
    {
        private void RunCSharpScript(IVimBuffer vimBuffer, CallInfo callInfo, bool createEachTime)
        {
            vimBuffer.VimBufferData.StatusUtil.OnError("csx not supported");
        }
    }

#endif
}

