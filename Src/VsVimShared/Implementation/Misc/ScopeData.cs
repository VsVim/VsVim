using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;

namespace VsVim.Implementation.Misc
{
    internal enum ScopeKind
    {
        Global,
        TextEditor,
        SolutionExplorer,

        /// <summary>
        /// This is different than Unknown because it's possible for a key binding to simply have 
        /// no scope at all
        /// </summary>
        EmptyName,

        Unknown
    }

    /// <summary>
    /// Contains the information about the main scope information in Visual Studio key bindings.  Every 
    /// key binding is associated with a scope, the main one being Global in 1033.  These values are 
    /// localized though and this class abstracts away the process of getting their values
    /// </summary>
    internal sealed class ScopeData
    {
        private const string DefaultGlobalScopeName = "Global";
        private const string DefaultTextEditorScopeName = "Text Editor";
        private const string DefaultSolutionExplorerScopeName = "Solution Explorer";

        internal static ScopeData Default
        {
            get { return new ScopeData(DefaultGlobalScopeName, DefaultTextEditorScopeName, DefaultSolutionExplorerScopeName); }
        }

        private readonly string _globalScopeName;
        private readonly string _textEditorScopeName;
        private readonly string _solutionExplorerScopeName;
        private readonly Dictionary<string, ScopeKind> _scopeKindMap;

        internal string GlobalScopeName
        {
            get { return _globalScopeName; }
        }

        internal ScopeData(string globalScopeName = null, string textEditorScopeName = null, string solutionExplorerScopeName = null)
        {
            _globalScopeName = globalScopeName ?? DefaultGlobalScopeName;
            _textEditorScopeName = textEditorScopeName ?? DefaultTextEditorScopeName;
            _solutionExplorerScopeName = solutionExplorerScopeName ?? DefaultSolutionExplorerScopeName;
            _scopeKindMap = BuildScopeKindMap();
        }

        internal ScopeData(IVsShell vsShell)
        {
            if (!TryGetMainScopeNames(vsShell, out _globalScopeName, out _textEditorScopeName, out _solutionExplorerScopeName))
            {
                _globalScopeName = DefaultGlobalScopeName;
                _textEditorScopeName = DefaultTextEditorScopeName;
                _solutionExplorerScopeName = DefaultSolutionExplorerScopeName;
            }

            _scopeKindMap = BuildScopeKindMap();
        }

        internal ScopeKind GetScopeKind(string name)
        {
            ScopeKind kind;
            if (!_scopeKindMap.TryGetValue(name, out kind))
            {
                kind = ScopeKind.Unknown;
            }

            return kind;
        }

        private Dictionary<string, ScopeKind> BuildScopeKindMap()
        {
            var map = new Dictionary<string, ScopeKind>(StringComparer.OrdinalIgnoreCase);
            map[_globalScopeName] = ScopeKind.Global;
            map[_textEditorScopeName] = ScopeKind.TextEditor;
            map[_solutionExplorerScopeName] = ScopeKind.SolutionExplorer;
            map[""] = ScopeKind.EmptyName;
            return map;
        }

        /// <summary>
        /// Get the localized names of "Global", "Text Editor" and "Solution Explorer" scope.  I wish there was a 
        /// prettier way of doing this.  However there is no API available which provides this information.  We need to directly 
        /// query some registry values here
        /// </summary>
        private static bool TryGetMainScopeNames(IVsShell vsShell, out string globalScopeName, out string textEditorScopeName, out string solutionExplorerScopeName)
        {
            globalScopeName = null;
            textEditorScopeName = null;
            solutionExplorerScopeName = null;
            try
            {
                using (var rootKey = VSRegistry.RegistryRoot(__VsLocalRegistryType.RegType_Configuration, writable: false))
                {
                    using (var keyBindingsKey = rootKey.OpenSubKey("KeyBindingTables"))
                    {
                        // For "Global".  The id in the registry here is incorrect for Vs 2010
                        // so hard code the known value
                        if (!TryGetKeyBindingScopeName(vsShell, keyBindingsKey, "{5efc7975-14bc-11cf-9b2b-00aa00573819}", 13018, out globalScopeName))
                        {
                            return false;
                        }

                        // For "Text Editor".  Many locals don't define this key so it's still considered a success 
                        // if we get only the global name.  Just fill in the default here
                        if (!TryGetKeyBindingScopeName(vsShell, keyBindingsKey, "{8B382828-6202-11D1-8870-0000F87579D2}", null, out textEditorScopeName))
                        {
                            textEditorScopeName = DefaultTextEditorScopeName;
                        }

                        // The "Solution Explorer" scope is only relevant starting in VS2013.  By default it contains a lot of 
                        // key bindings for '['.  If we can't find the real name just use the default.  We don't want its abscence
                        // to screw up other scopes
                        if (!TryGetKeyBindingScopeName(vsShell, keyBindingsKey, "{3AE79031-E1BC-11D0-8F78-00A0C9110057}", null, out solutionExplorerScopeName))
                        {
                            solutionExplorerScopeName = DefaultSolutionExplorerScopeName;
                        }
                    }
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// The scopes, including well known ones like "Text Editor", in Visual Studio key bindings are localized 
        /// strings.  This scope attempts to read the localized string out of the correct package by digging
        /// through the registry and getting the correct resource id.
        /// </summary>
        /// <returns></returns>
        private static bool TryGetKeyBindingScopeName(IVsShell vsShell, RegistryKey keyBindingsKey, string subKeyName, uint? id, out string localizedScopeName)
        {
            try
            {
                using (var subKey = keyBindingsKey.OpenSubKey(subKeyName, writable: false))
                {
                    if (!id.HasValue)
                    {
                        // If we don't already have a resource ID then need to reed it from the KeyBindingTables sub-registry 
                        // key.  It will be in the default value position
                        var idString = (string)subKey.GetValue(null) ?? "";

                        // The id string often is prefixed with a '#' so remove that 
                        if (idString.Length > 0 && idString[0] == '#')
                        {
                            idString = idString.Substring(1);
                        }

                        uint resourceId;
                        if (UInt32.TryParse(idString, out resourceId))
                        {
                            id = resourceId;
                        }

                    }

                    if (!id.HasValue)
                    {
                        localizedScopeName = null;
                        return false;
                    }

                    var package = Guid.Parse((string)subKey.GetValue("Package"));
                    return 
                        ErrorHandler.Succeeded(vsShell.LoadPackageString(ref package, id.Value, out localizedScopeName)) &&
                        !string.IsNullOrEmpty(localizedScopeName);
                }
            }
            catch (Exception)
            {
                localizedScopeName = null;
                return false;
            }
        }
    }
}
