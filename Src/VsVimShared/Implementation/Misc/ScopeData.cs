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
    /// <summary>
    /// Contains the information about the main scope information in Visual Studio key bindings.  Every 
    /// key binding is associated with a scope, the main one being Global in 1033.  These values are 
    /// localized though and this class abstracts away the process of getting their values
    /// </summary>
    internal sealed class ScopeData
    {
        internal const string DefaultGlobalScopeName = "Global";
        internal const string DefaultTextEditorScopeName = "Text Editor";

        internal static ScopeData Default
        {
            get { return new ScopeData(DefaultGlobalScopeName, DefaultTextEditorScopeName); }
        }

        private readonly string _globalScopeName;
        private readonly string _textEditorScopeName;
        private readonly HashSet<string> _importantScopeSet;

        internal string GlobalScopeName
        {
            get { return _globalScopeName; }
        }

        internal ScopeData(string globalScopeName, string textEditorScopeName = null)
        {
            _globalScopeName = globalScopeName ?? DefaultGlobalScopeName;
            _textEditorScopeName = textEditorScopeName ?? DefaultTextEditorScopeName;
            _importantScopeSet = CreateImportantScopeSet(_globalScopeName, _textEditorScopeName);
        }

        internal ScopeData(IVsShell vsShell)
        {
            if (!TryGetMainScopeNames(vsShell, out _globalScopeName, out _textEditorScopeName))
            {
                _globalScopeName = DefaultGlobalScopeName;
                _textEditorScopeName = DefaultTextEditorScopeName;
            }

            _importantScopeSet = CreateImportantScopeSet(_globalScopeName, _textEditorScopeName);
        }

        internal bool IsImportantScope(string name)
        {
            return _importantScopeSet.Contains(name);
        }

        private static HashSet<string> CreateImportantScopeSet(string globalScopeName, string textEditorScopeName)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            set.Add(globalScopeName);
            set.Add(textEditorScopeName);

            // No scope is considered interesting as well
            set.Add("");
            return set;
        }

        /// <summary>
        /// Get the localized names of "Global" and "Text Editor" scope.  I wish there was a prettier way of doing 
        /// this.  However there is no API available which provides this information.  We need to directly 
        /// query some registry values here
        /// </summary>
        private static bool TryGetMainScopeNames(IVsShell vsShell, out string globalScopeName, out string textEditorScopeName)
        {
            globalScopeName = null;
            textEditorScopeName = null;
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
                            textEditorScopeName = "Text Editor";
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

        private static bool TryGetKeyBindingScopeName(IVsShell vsShell, RegistryKey keyBindingsKey, string subKeyName, uint? id, out string localizedScopeName)
        {
            try
            {
                using (var subKey = keyBindingsKey.OpenSubKey(subKeyName, writable: false))
                {
                    uint resourceId;
                    if (id.HasValue)
                    {
                        resourceId = id.Value;
                    }
                    else
                    {
                        if (!UInt32.TryParse((string)subKey.GetValue(null), out resourceId))
                        {
                            localizedScopeName = null;
                            return false;
                        }
                    }

                    var package = Guid.Parse((string)subKey.GetValue("Package"));
                    return 
                        ErrorHandler.Succeeded(vsShell.LoadPackageString(ref package, resourceId, out localizedScopeName)) &&
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
