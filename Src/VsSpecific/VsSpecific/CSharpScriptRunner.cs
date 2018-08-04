#if VS_SPECIFIC_2017

using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.CodeAnalysis.Scripting;
using System.IO;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using System.Reflection;

namespace Vim.Plugin.Implementation.CSharpScriptRunner
{
    [Export(typeof(IVimBufferCreationListener))]
    internal sealed class CSharpScriptRunner : IVimBufferCreationListener, IDisposable
    {
        private readonly IVim _vim;
        private readonly IVimGlobalSettings _globalSettings;
        private readonly CSharpScriptRunner _instance = null;

        [ImportingConstructor]
        internal CSharpScriptRunner(IVim vim)
        {
            if (_instance != null)
                return;

            _instance = this;
            _vim = vim;
            _globalSettings = _vim.GlobalSettings;
        }

        private void Dispose()
        {
        }

        private void VimBufferCreated(IVimBuffer vimBuffer)
        {
            if ((vimBuffer.TextView is IWpfTextView textView))
            {
                vimBuffer.CallCSharpScript += OnCallCSharpScript;
                vimBuffer.Closed += OnBufferClosed;
            }
        }
        private void OnBufferClosed(object sender, EventArgs e)
        {
            if (sender is IVimBuffer vimBuffer && vimBuffer.TextView is IWpfTextView textView)
            {
                vimBuffer.CallCSharpScript -= OnCallCSharpScript;
                vimBuffer.Closed -= OnBufferClosed;
           }
        }
        private void OnCallCSharpScript(object sender, CallCSharpScriptEventArgs e)
        {
            if (sender is IVimBuffer vimBuffer && vimBuffer.TextView is IWpfTextView textView)
            {
                try
                {
                    string scriptPath = Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), System.String.Format("{0}.csx", e.CallInfo.Name));

                    ScriptOptions options = ScriptOptions.Default
                                    .WithImports("Vim")
                                    .WithReferences(Assembly.GetAssembly(typeof(KeyInputEventArgs)));

                    var param = new CSharpScriptParam(vimBuffer, textView);
                    var script = CSharpScript.Create(File.ReadAllText(scriptPath), options, typeof(CSharpScriptParam));
                    script.RunAsync(param);
                }
                catch (CompilationErrorException ex)
                {
                    Console.WriteLine("[Compile Error]");
                    Console.WriteLine(ex.Message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
           }
        }
        #region IVimBufferCreationListener

        void IVimBufferCreationListener.VimBufferCreated(IVimBuffer vimBuffer)
        {
            VimBufferCreated(vimBuffer);
            VimTrace.TraceInfo("CSharptScript");
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
