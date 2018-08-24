#if VS_SPECIFIC_2017

using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.CodeAnalysis.Scripting;
using System.IO;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using System.Reflection;
using EnvDTE;

namespace Vim.VisualStudio.Specific
{
    [Export(typeof(IVimBufferCreationListener))]
    internal sealed class CSharpScriptRunner : IVimBufferCreationListener, IDisposable
    {
        private readonly IVim _vim;
        private readonly IVimGlobalSettings _globalSettings;
        private readonly CSharpScriptRunner _instance = null;
        private const string ScriptFolder = "vsvimscripts";

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
                    string scriptPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    scriptPath = Path.Combine(scriptPath, ScriptFolder);
                    scriptPath = Path.Combine(scriptPath, $"{e.CallInfo.Name}.csx");

                    if (!File.Exists(scriptPath))
                    {
                        vimBuffer.VimBufferData.StatusUtil.OnError("script file not found.");
                        return;
                    }

                    ScriptOptions options = ScriptOptions.Default
                                    .WithImports("Vim", "EnvDTE", "Microsoft.VisualStudio.Text.Editor")
                                    .WithReferences(Assembly.GetAssembly(typeof(KeyInputEventArgs)), 
                                                    Assembly.GetAssembly(typeof(FileCodeModel)),
                                                    Assembly.GetAssembly(typeof(IWpfTextView)));

                    var param = new CSharpScriptParam(vimBuffer, textView, e.CallInfo);
                    var script = CSharpScript.Create(File.ReadAllText(scriptPath), options, typeof(CSharpScriptParam));
                    script.RunAsync(param);
                }
                catch (CompilationErrorException ex)
                {
                    vimBuffer.VimBufferData.StatusUtil.OnError(ex.Message);
                }
                catch (Exception ex)
                {
                    vimBuffer.VimBufferData.StatusUtil.OnError(ex.Message);
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
