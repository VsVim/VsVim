using System;
using System.Runtime.CompilerServices;
using Vim.Interpreter;

namespace Vim.VisualStudio.Specific
{
#if VS_SPECIFIC_2017 || VS_SPECIFIC_2019

    internal partial class SharedService
    {
        private Lazy<ICSharpScriptExecutor> _lazyExecutor = new Lazy<ICSharpScriptExecutor>(CreateExecutor);

        private void RunCSharpScript(IVim vim, CallInfo callInfo, bool createEachTime)
        {
            _lazyExecutor.Value.Execute(vim, callInfo, createEachTime);
        }

        private static ICSharpScriptExecutor CreateExecutor()
        {
            try
            {
                return CreateCSharpExecutor();
            }
            catch
            {
                // Failure is expected here in certain cases. 
            }

            return NotSupportedCSharpScriptExecutor.Instance;
        }

        /// <summary>
        /// The creation of <see cref="CSharpScriptExecutor"/> will load the Microsoft.CodeAnalysis
        /// assemblies. This method deliberately has inlining disabled so that the attempted load 
        /// will happen in this method call and not be inlined into the caller. This lets us better
        /// trap failure. 
        /// 
        /// The majority of VS workloads will have these assemblies and hence this will be safe. But
        /// there are workloads, Python and C++ for example, which will not install them. In that 
        /// case C# script execution won't be supported and this method will fail.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ICSharpScriptExecutor CreateCSharpExecutor() => new CSharpScriptExecutor();
    }

#else

    internal partial class SharedService
    {
        private void RunCSharpScript(IVim vim, CallInfo callInfo, bool createEachTime)
        {
            NotSupportedCSharpScriptExecutor.Instance.Execute(vim, callInfo, createEachTime);
        }
    }

#endif
}

