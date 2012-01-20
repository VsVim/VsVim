using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;
using Vim;

namespace VsVim.Implementation
{
    /// <summary>
    /// Responsible for dealing with the conflicting key bindings inside of Visual Studio
    /// </summary>
    [Export(typeof(IKeyBindingService))]
    internal sealed class KeyBindingService : IKeyBindingService
    {
        private readonly _DTE _dte;
        private readonly IVsShell _vsShell;
        private readonly IOptionsDialogService _optionsDialogService;
        private HashSet<string> _importantScopeSet;
        private ConflictingKeyBindingState _state;
        private CommandKeyBindingSnapshot _snapshot;

        [ImportingConstructor]
        internal KeyBindingService(SVsServiceProvider serviceProvider, IOptionsDialogService service)
        {
            _dte = serviceProvider.GetService<SDTE, _DTE>();
            _vsShell = serviceProvider.GetService<SVsShell, IVsShell>();
            _optionsDialogService = service;
        }

        internal void UpdateConflictingState(ConflictingKeyBindingState state, CommandKeyBindingSnapshot snapshot)
        {
            _snapshot = snapshot;
            ConflictingKeyBindingState = state;
        }

        internal ConflictingKeyBindingState ConflictingKeyBindingState
        {
            get { return _state; }
            private set
            {
                if (_state != value)
                {
                    _state = value;
                    var list = ConflictingKeyBindingStateChanged;
                    if (list != null)
                    {
                        list(this, EventArgs.Empty);
                    }
                }
            }
        }

        internal event EventHandler ConflictingKeyBindingStateChanged;

        internal CommandKeyBindingSnapshot CreateCommandKeyBindingSnapshot(IVimBuffer vimBuffer)
        {
            var util = new KeyBindingUtil(_dte, GetOrCreateImportantScopeSet());
            return util.CreateCommandKeyBindingSnapshot(vimBuffer);
        }

        internal void RunConflictingKeyBindingStateCheck(IVimBuffer buffer, Action<ConflictingKeyBindingState, CommandKeyBindingSnapshot> onComplete)
        {
            var needed = buffer.AllModes.Select(x => x.CommandNames).SelectMany(x => x).ToList();
            needed.Add(KeyInputSet.NewOneKeyInput(buffer.LocalSettings.GlobalSettings.DisableAllCommand));
            RunConflictingKeyBindingStateCheck(needed.Select(x => x.KeyInputs.First()), onComplete);
        }

        internal void RunConflictingKeyBindingStateCheck(IEnumerable<KeyInput> neededInputs, Action<ConflictingKeyBindingState, CommandKeyBindingSnapshot> onComplete)
        {
            if (_snapshot != null)
            {
                onComplete(_state, _snapshot);
                return;
            }

            var util = new KeyBindingUtil(_dte, GetOrCreateImportantScopeSet());
            var set = new HashSet<KeyInput>(neededInputs);
            _snapshot = util.CreateCommandKeyBindingSnapshot(set);
            ConflictingKeyBindingState = _snapshot.Conflicting.Any()
                ? ConflictingKeyBindingState.FoundConflicts
                : ConflictingKeyBindingState.ConflictsIgnoredOrResolved;
        }

        internal void ResetConflictingKeyBindingState()
        {
            ConflictingKeyBindingState = ConflictingKeyBindingState.HasNotChecked;
            _snapshot = null;
        }

        internal void ResolveAnyConflicts()
        {
            if (_snapshot == null || _state != ConflictingKeyBindingState.FoundConflicts)
            {
                return;
            }

            if (_optionsDialogService.ShowConflictingKeyBindingsDialog(_snapshot))
            {
                ConflictingKeyBindingState = ConflictingKeyBindingState.ConflictsIgnoredOrResolved;
                _snapshot = null;
            }
        }

        internal void IgnoreAnyConflicts()
        {
            ConflictingKeyBindingState = ConflictingKeyBindingState.ConflictsIgnoredOrResolved;
            _snapshot = null;
        }

        private HashSet<string> GetOrCreateImportantScopeSet()
        {
            if (_importantScopeSet != null)
            {
                return _importantScopeSet;
            }

            _importantScopeSet = CreateImportantScopeSet();
            return _importantScopeSet;
        }

        /// <summary>
        /// Get the localized names of the scopes who's key bindings we find interesting.  This is 
        /// a fairly unpretty way of doing this.  Yet it's the only known way to achieve this in 
        /// Dev10.  
        /// </summary>
        private HashSet<string> CreateImportantScopeSet()
        {
            try
            {
                using (var rootKey = VSRegistry.RegistryRoot(__VsLocalRegistryType.RegType_Configuration, writable: false))
                {
                    using (var keyBindingsKey = rootKey.OpenSubKey("KeyBindingTables"))
                    {
                        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                        // For "Global".  The id in the registry here is incorrect for Dev10 
                        // so hard code the known value
                        set.Add(GetKeyBindingScopeName(
                            keyBindingsKey,
                            "{5efc7975-14bc-11cf-9b2b-00aa00573819}",
                            13018,
                            "Global"));

                        // For "Text Editor"
                        set.Add(GetKeyBindingScopeName(
                            keyBindingsKey,
                            "{8B382828-6202-11D1-8870-0000F87579D2}",
                            null,
                            "Text Editor"));

                        // No scope is considered interesting as well
                        set.Add("");
                        return set;
                    }
                }
            }
            catch (Exception)
            {
                return GetDefaultImportantScopeSet();
            }
        }

        private string GetKeyBindingScopeName(RegistryKey keyBindingsKey, string subKeyName, uint? id, string defaultValue)
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
                        resourceId = UInt32.Parse((string)subKey.GetValue(null));
                    }

                    var package = Guid.Parse((string)subKey.GetValue("Package"));
                    string value;
                    ErrorHandler.ThrowOnFailure(_vsShell.LoadPackageString(ref package, resourceId, out value));
                    return !String.IsNullOrEmpty(value) ? value : defaultValue;
                }
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Get the default English version of the scopes we care about.  This is a fallback from
        /// getting any errors in calculating them
        /// </summary>
        internal static HashSet<string> GetDefaultImportantScopeSet()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            set.Add("Global");
            set.Add("Text Editor");
            set.Add("");
            return set;
        }

        #region IKeyBindingService

        ConflictingKeyBindingState IKeyBindingService.ConflictingKeyBindingState
        {
            get { return ConflictingKeyBindingState; }
        }

        event EventHandler IKeyBindingService.ConflictingKeyBindingStateChanged
        {
            add { ConflictingKeyBindingStateChanged += value; }
            remove { ConflictingKeyBindingStateChanged -= value; }
        }

        CommandKeyBindingSnapshot IKeyBindingService.CreateCommandKeyBindingSnapshot(IVimBuffer vimBuffer)
        {
            return CreateCommandKeyBindingSnapshot(vimBuffer);
        }

        void IKeyBindingService.RunConflictingKeyBindingStateCheck(IVimBuffer buffer, Action<ConflictingKeyBindingState, CommandKeyBindingSnapshot> onComplete)
        {
            RunConflictingKeyBindingStateCheck(buffer, onComplete);
        }

        void IKeyBindingService.RunConflictingKeyBindingStateCheck(IEnumerable<KeyInput> neededInputs, Action<ConflictingKeyBindingState, CommandKeyBindingSnapshot> onComplete)
        {
            RunConflictingKeyBindingStateCheck(neededInputs, onComplete);
        }

        void IKeyBindingService.ResetConflictingKeyBindingState()
        {
            ResetConflictingKeyBindingState();
        }

        void IKeyBindingService.ResolveAnyConflicts()
        {
            ResolveAnyConflicts();
        }

        void IKeyBindingService.IgnoreAnyConflicts()
        {
            IgnoreAnyConflicts();
        }

        #endregion
    }
}
