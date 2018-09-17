#if VS_SPECIFIC_2017

using System;
using System.ComponentModel.Composition;
using Vim.Interpreter;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;

namespace Vim.VisualStudio.Specific
{
    [Export(typeof(ICSharpScriptExecutor))]
    internal sealed class CSharpScriptExecutor : ICSharpScriptExecutor, IDisposable
    {
        [Import(typeof(IVim))]
        private Lazy<IVim> Vim { get; set; }

        private const string ScriptFolder = "vsvimscripts";
        private Dictionary<string, Script<object>> _scripts = new Dictionary<string, Script<object>>(StringComparer.OrdinalIgnoreCase);
        private ScriptOptions _scriptOptions = null;

        private void Dispose()
        {
        }

        private void Execute(CallInfo callInfo, bool createEachTime)
        {
            IVim vim = Vim.Value;
            try
            {
                Script<object> script;
                if (!TryGetScript(vim, callInfo.Name, createEachTime, out script))
                    return;

                var globals = new CSharptScripGlobals(callInfo, vim);
                script.RunAsync(globals).Wait();
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
        private ScriptOptions GetScriptOptions(string scriptPath)
        {
            var ssr = ScriptSourceResolver.Default
                .WithBaseDirectory(scriptPath);

            var searchPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            var smr = ScriptMetadataResolver.Default
                .WithBaseDirectory(scriptPath)
                .WithSearchPaths(searchPath);

            var asm = new List<Assembly>();

            asm.Add(typeof(EnvDTE.AddIn).Assembly); //EnvDTE.dll
            asm.Add(typeof(EnvDTE80.Breakpoint2).Assembly); //EnvDTE80.dll
            asm.Add(typeof(EnvDTE90.Module).Assembly); //EnvDTE90.dll
            asm.Add(typeof(EnvDTE100.Solution4).Assembly); //EnvDTE100.dll

            //asm.Add(typeof(Microsoft.VisualStudio.Shell.Package).Assembly); //Microsoft.VisualStudil.Shell.15.0 is imported. 
            //CS0433 error occurs when multiple versions of Microsoft.VisualStudil.Shell are imported.
            //So commented out.

            asm.Add(typeof(Microsoft.VisualStudio.Text.ITextSnapshot).Assembly); //Microsoft.VisualStudio.Text.Data.dll
            asm.Add(typeof(Microsoft.VisualStudio.Text.Document.ChangeTag).Assembly); //Microsoft.VisualStudio.Text.Logic.dll
            asm.Add(typeof(Microsoft.VisualStudio.Text.Editor.IWpfTextView).Assembly); //Microsoft.VisualStudio.Text.UI.Wpf.dll

            asm.Add(typeof(Vim.IVim).Assembly); //VimCore.dll
            asm.Add(typeof(Vim.UI.Wpf.IBlockCaret).Assembly); //VimWpf.dll
            asm.Add(typeof(Vim.VisualStudio.ISharedService).Assembly); //Vim.VisualStudio.VsInterfaces.dll,Microsoft.VisualStudil.Shell.11.0 is imported.
            asm.Add(typeof(Vim.VisualStudio.Extensions).Assembly); //Vim.VisualStudio.Shared.dll,Microsoft.VisualStudil.Shell.11.0 is imported.

            var so = ScriptOptions.Default
                  .WithSourceResolver(ssr)
                  .WithMetadataResolver(smr)
                  .WithReferences(asm);

            return so;
        }
        private bool TryGetScript(IVim vim, string scriptName, bool createEachTime, out Script<object> script)
        {
            if (!createEachTime && _scripts.ContainsKey(scriptName))
            {
                script = _scripts[scriptName];
                return true;
            }
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

            script = CSharpScript.Create(File.ReadAllText(scriptFilePath), _scriptOptions, typeof(CSharptScripGlobals));
            _scripts[scriptName] = script;
            return true;
        }

        #region ICSharpScriptExecutor

        void ICSharpScriptExecutor.Execute(CallInfo callInfo, bool createEachTime)
        {
            Execute(callInfo, createEachTime);
            VimTrace.TraceInfo("CSharptScript:Execute {0}", callInfo.Name);
        }
        #endregion

        #region IDispose

        void IDisposable.Dispose()
        {
            Dispose();
        }

        #endregion
    }
}
#endif
