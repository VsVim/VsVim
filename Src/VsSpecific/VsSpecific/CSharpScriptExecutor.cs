#if VS_SPECIFIC_2017 || VS_SPECIFIC_2019

using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Vim.Interpreter;

namespace Vim.VisualStudio.Specific
{
    internal sealed partial class CSharpScriptExecutor : ICSharpScriptExecutor
    {
        private const string ScriptFolder = "vsvimscripts";
        private Dictionary<string, Script<object>> _scripts = new Dictionary<string, Script<object>>(StringComparer.OrdinalIgnoreCase);
        private ScriptOptions _scriptOptions = null;

        private async Task ExecuteAsync(IVim vim, CallInfo callInfo, bool createEachTime)
        {
            try
            {
                Script<object> script;
                if (!TryGetScript(vim, callInfo.Name, createEachTime, out script))
                    return;

                var globals = new CSharpScriptGlobals(callInfo, vim);
                var scriptState = await script.RunAsync(globals);
            }
            catch (CompilationErrorException ex)
            {
                if (_scripts.ContainsKey(callInfo.Name))
                    _scripts.Remove(callInfo.Name);

                vim.ActiveStatusUtil.OnError(string.Join(Environment.NewLine, ex.Diagnostics));
            }
            catch (Exception ex)
            {
                vim.ActiveStatusUtil.OnError(ex.Message);
            }
        }

        private static ScriptOptions GetScriptOptions(string scriptPath)
        {
            var ssr = ScriptSourceResolver.Default.WithBaseDirectory(scriptPath);
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

            var searchPaths = new string[]
            {
                Path.Combine(baseDirectory, "PublicAssemblies"),
                Path.Combine(baseDirectory, "PrivateAssemblies"),
                Path.Combine(baseDirectory, @"CommonExtensions\Microsoft\Editor"),
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            };

            var smr = ScriptMetadataResolver.Default
                .WithBaseDirectory(scriptPath)
                .WithSearchPaths(searchPaths);

            var asm = new Assembly[]
            {
                typeof(Vim.IVim).Assembly,                        // VimCore.dll
                typeof(Vim.UI.Wpf.IBlockCaret).Assembly,          // VimWpf.dll
                typeof(Vim.VisualStudio.ISharedService).Assembly, // Vim.VisualStudio.VsInterfaces.dll
                typeof(Vim.VisualStudio.Extensions).Assembly      // Vim.VisualStudio.Shared.dll
            };

            var so = ScriptOptions.Default
                  .WithSourceResolver(ssr)
                  .WithMetadataResolver(smr)
                  .WithReferences(asm);

            return so;
        }

        private bool TryGetScript(IVim vim, string scriptName, bool createEachTime, out Script<object> script)
        {
            if (!createEachTime && _scripts.TryGetValue(scriptName, out script))
                return true;

            string scriptPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            scriptPath = Path.Combine(scriptPath, ScriptFolder);

            string scriptFilePath = Path.Combine(scriptPath, $"{scriptName}.csx");

            if (!File.Exists(scriptFilePath))
            {
                vim.ActiveStatusUtil.OnError("script file not found.");
                script = null;
                return false;
            }

            if (_scriptOptions == null)
                _scriptOptions = GetScriptOptions(scriptPath);

            script = CSharpScript.Create(File.ReadAllText(scriptFilePath), _scriptOptions, typeof(CSharpScriptGlobals));
            _scripts[scriptName] = script;
            return true;
        }

        #region ICSharpScriptExecutor

        void ICSharpScriptExecutor.Execute(IVim vim, CallInfo callInfo, bool createEachTime)
        {
            var task = ExecuteAsync(vim, callInfo, createEachTime);
            VimTrace.TraceInfo("CSharptScript:Execute {0}", callInfo.Name);
        }

        #endregion

    }
}
#endif
