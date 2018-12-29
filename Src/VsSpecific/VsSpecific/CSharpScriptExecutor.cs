#if VS_SPECIFIC_2017 || VS_SPECIFIC_2019

using System;
using Vim.Interpreter;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Vim.VisualStudio.Specific
{
    internal sealed class CSharpScriptExecutor : ICSharpScriptExecutor
    {
        private const string ScriptFolder = "vsvimscripts";
        private Dictionary<string, object> _scripts = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        private MethodInfo _cSharpScriptCreate = null;
        private dynamic _scriptOptions = null;
        private dynamic _defaultScriptSourceResolver = null;
        private dynamic _defaultScriptMetadataResolver = null;
        private dynamic _defaultScriptOptions = null;

        private void Execute(IVim vim, CallInfo callInfo, bool createEachTime)
        {
            try
            {
                string assemblyPath;
                string baseDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"CommonExtensions\Microsoft\ManagedLanguages\VBCSharp\InteractiveComponents");

                string assemblyName;
                if (_cSharpScriptCreate == null)
                {
                    assemblyName = "Microsoft.CodeAnalysis.CSharp.Scripting.dll";
                    assemblyPath = Path.Combine(baseDirectory, assemblyName);
                    if (!File.Exists(assemblyPath))
                    {
                        vim.ActiveStatusUtil.OnError($"{assemblyName} not found.");
                        return;
                    }
                    var asm = Assembly.LoadFile(assemblyPath);
                    var t = asm.GetType("Microsoft.CodeAnalysis.CSharp.Scripting.CSharpScript");
                    foreach (MethodInfo mi in t.GetMethods())
                    {

                        if (!mi.IsGenericMethod && mi.Name == "Create")
                        {
                            if (mi.GetParameters()[0].ParameterType == typeof(string))
                            {
                                _cSharpScriptCreate = mi;
                                break;
                            }
                        }
                    }
                    if(_cSharpScriptCreate == null)
                    {
                        vim.ActiveStatusUtil.OnError($"method 'CSharpScript.Create' not found.");
                        return;
                    }
                }
                if (_defaultScriptSourceResolver == null)
                {
                    assemblyName = "Microsoft.CodeAnalysis.Scripting.dll";
                    assemblyPath = Path.Combine(baseDirectory, assemblyName);
                    if (!File.Exists(assemblyPath))
                    {
                        vim.ActiveStatusUtil.OnError($"{assemblyName} not found.");
                        return;
                    }
                    var asm = Assembly.LoadFile(assemblyPath);
                    var t = asm.GetType("Microsoft.CodeAnalysis.Scripting.ScriptSourceResolver");
                    _defaultScriptSourceResolver = t.GetProperty("Default", BindingFlags.Static | BindingFlags.Public).GetValue(null, null);

                    t = asm.GetType("Microsoft.CodeAnalysis.Scripting.ScriptMetadataResolver");
                    _defaultScriptMetadataResolver = t.GetProperty("Default", BindingFlags.Static | BindingFlags.Public).GetValue(null, null);

                    t = asm.GetType("Microsoft.CodeAnalysis.Scripting.ScriptOptions");
                    _defaultScriptOptions = t.GetProperty("Default", BindingFlags.Static | BindingFlags.Public).GetValue(null, null);
                }

                object script;
                if (!TryGetScript(vim, callInfo.Name, createEachTime, out script))
                    return;

                var globals = new CSharpScriptGlobals(callInfo, vim);
                dynamic sc = script;
                dynamic runner = sc.RunAsync(globals);
                runner.Wait();
            }
            catch (Exception ex)
            {
                if (ex.GetType().Name == "CompilationErrorException")
                {
                    if (_scripts.ContainsKey(callInfo.Name))
                        _scripts.Remove(callInfo.Name);

                    vim.ActiveStatusUtil.OnError(string.Join(Environment.NewLine, ((dynamic)ex).Diagnostics));
                }
                else
                {
                    vim.ActiveStatusUtil.OnError(ex.Message);
                }
            }
        }
        private dynamic GetScriptOptions(string scriptPath)
        {
            var ssr = _defaultScriptSourceResolver
                .WithBaseDirectory(scriptPath);

            List<string> searchPaths = new List<string>();
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

            searchPaths.Add(Path.Combine(baseDirectory, "PublicAssemblies"));
            searchPaths.Add(Path.Combine(baseDirectory, "PrivateAssemblies"));
            searchPaths.Add(Path.Combine(baseDirectory, @"CommonExtensions\Microsoft\Editor"));
            searchPaths.Add(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

            var smr = _defaultScriptMetadataResolver
                .WithBaseDirectory(scriptPath)
                .WithSearchPaths(searchPaths.ToArray());

            var asm = new List<Assembly>();

            asm.Add(typeof(Vim.IVim).Assembly); //VimCore.dll
            asm.Add(typeof(Vim.UI.Wpf.IBlockCaret).Assembly); //VimWpf.dll
            asm.Add(typeof(Vim.VisualStudio.ISharedService).Assembly); //Vim.VisualStudio.VsInterfaces.dll
            asm.Add(typeof(Vim.VisualStudio.Extensions).Assembly); //Vim.VisualStudio.Shared.dll

            var so = _defaultScriptOptions
                  .WithSourceResolver(ssr)
                  .WithMetadataResolver(smr)
                  .WithReferences(asm);

            return so;
        }
        private bool TryGetScript(IVim vim, string scriptName, bool createEachTime, out object script)
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

            script = _cSharpScriptCreate.Invoke(null, new object[] { File.ReadAllText(scriptFilePath), _scriptOptions, typeof(CSharpScriptGlobals), null });
            _scripts[scriptName] = script;
            return true;
        }

#region ICSharpScriptExecutor

        void ICSharpScriptExecutor.Execute(IVim vim, CallInfo callInfo, bool createEachTime)
        {
            Execute(vim, callInfo, createEachTime);
            VimTrace.TraceInfo("CSharptScript:Execute {0}", callInfo.Name);
        }
#endregion

    }
}
#endif
