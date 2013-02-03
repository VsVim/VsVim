using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using EditorUtils;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;
using Vim;

namespace VsVim.Implementation.Misc
{
    /// <summary>
    /// Responsible for dealing with the conflicting key bindings inside of Visual Studio
    /// </summary>
    [Export(typeof(IKeyBindingService))]
    [Export(typeof(IVimBufferCreationListener))]
    internal sealed class KeyBindingService : IKeyBindingService, IVimBufferCreationListener
    {
        private readonly _DTE _dte;
        private readonly IVsShell _vsShell;
        private readonly IOptionsDialogService _optionsDialogService;
        private readonly IProtectedOperations _protectedOperations;
        private readonly IVimApplicationSettings _vimApplicationSettings;
        private Lazy<HashSet<string>> _importantScopeSet;
        private ConflictingKeyBindingState _state;
        private CommandKeyBindingSnapshot _snapshot;

        internal CommandKeyBindingSnapshot CommandKeyBindingSnapshotOptional
        {
            get { return _snapshot; }
        }

        [ImportingConstructor]
        internal KeyBindingService(SVsServiceProvider serviceProvider, IOptionsDialogService service, [EditorUtilsImport] IProtectedOperations protectedOperations, IVimApplicationSettings vimApplicationSettings)
        {
            _dte = serviceProvider.GetService<SDTE, _DTE>();
            _vsShell = serviceProvider.GetService<SVsShell, IVsShell>();
            _optionsDialogService = service;
            _protectedOperations = protectedOperations;
            _vimApplicationSettings = vimApplicationSettings;
            _importantScopeSet = new Lazy<HashSet<string>>(CreateImportantScopeSet);
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

        internal void RunConflictingKeyBindingStateCheck(IVimBuffer buffer)
        {
            // Create the set of KeyInput values which are handled by VsVim
            var needed = buffer.AllModes.Select(x => x.CommandNames).SelectMany(x => x).ToList();
            needed.Add(KeyInputSet.NewOneKeyInput(buffer.LocalSettings.GlobalSettings.DisableAllCommand));
            var set = new HashSet<KeyInput>(needed.Select(x => x.KeyInputs.First()));

            // Take a snapshot based on the current state of the DTE commands and store it
            // and the conflicting state
            _snapshot = CreateCommandKeyBindingSnapshot(set);
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
            }
        }

        internal void IgnoreAnyConflicts()
        {
            ConflictingKeyBindingState = ConflictingKeyBindingState.ConflictsIgnoredOrResolved;
        }

        /// <summary>
        /// Compute the set of keys that conflict with and have been already been removed.
        /// </summary>
        internal CommandKeyBindingSnapshot CreateCommandKeyBindingSnapshot(IVimBuffer buffer)
        {
            // Get the list of all KeyInputs that are the first key of a VsVim command
            var hashSet = new HashSet<KeyInput>(
                buffer.AllModes
                .Select(x => x.CommandNames)
                .SelectMany(x => x)
                .Where(x => x.KeyInputs.Length > 0)
                .Select(x => x.KeyInputs.First()));

            // Need to get the custom key bindings in the list.  It's very common for users 
            // to use for example function keys (<F2>, <F3>, etc ...) in their mappings which
            // are often bound to other Visual Studio commands.
            var keyMap = buffer.Vim.KeyMap;
            foreach (var keyRemapMode in KeyRemapMode.All)
            {
                foreach (var keyMapping in keyMap.GetKeyMappingsForMode(keyRemapMode))
                {
                    keyMapping.Left.KeyInputs.ForEach(keyInput => hashSet.Add(keyInput));
                }
            }

            // Include the key used to disable VsVim
            hashSet.Add(buffer.LocalSettings.GlobalSettings.DisableAllCommand);

            return CreateCommandKeyBindingSnapshot(hashSet);
        }

        internal CommandKeyBindingSnapshot CreateCommandKeyBindingSnapshot(HashSet<KeyInput> needed)
        {
            var commandsSnapshot = new CommandsSnapshot(_dte);
            var conflicting = FindConflictingCommandKeyBindings(commandsSnapshot, needed);
            var removed = FindRemovedKeyBindings(commandsSnapshot);
            return new CommandKeyBindingSnapshot(
                commandsSnapshot,
                removed,
                conflicting);
        }

        /// <summary>
        /// Find all of the Command instances (which represent Visual Studio commands) which would conflict with any
        /// VsVim commands that use the keys in neededInputs.
        /// </summary>
        internal List<CommandKeyBinding> FindConflictingCommandKeyBindings(CommandsSnapshot commandsSnapshot, HashSet<KeyInput> neededInputs)
        {
            var list = new List<CommandKeyBinding>();
            var all = commandsSnapshot.CommandKeyBindings.Where(x => !ShouldSkip(x));
            foreach (var binding in all)
            {
                var input = binding.KeyBinding.FirstKeyStroke.AggregateKeyInput;
                if (neededInputs.Contains(input))
                {
                    list.Add(binding);
                }
            }

            return list;
        }

        /// <summary>
        /// Returns the list of commands that were previously removed by the user and are no longer currently active.
        /// </summary>
        internal List<CommandKeyBinding> FindRemovedKeyBindings(CommandsSnapshot commandsSnapshot)
        {
            return _vimApplicationSettings
                .RemovedBindings
                .Where(x => !commandsSnapshot.IsKeyBindingActive(x.KeyBinding))
                .ToList();
        }

        /// <summary>
        /// Should this be skipped when removing conflicting bindings?
        /// </summary>
        internal bool ShouldSkip(CommandKeyBinding binding)
        {
            var scope = binding.KeyBinding.Scope;
            var importantScopeSet = _importantScopeSet.Value;
            if (!importantScopeSet.Contains(scope))
            {
                return true;
            }

            if (!binding.KeyBinding.KeyStrokes.Any())
            {
                return true;
            }

            var first = binding.KeyBinding.FirstKeyStroke;

            // We don't want to remove any mappings which don't include a modifier key 
            // because it removes too many mappings.  Without this check we would for
            // example remove Delete in insert mode, arrow keys for intellisense and 
            // general navigation, space bar for completion, etc ...
            //
            // One exception is function keys.  They are only bound in Vim to key 
            // mappings and should win over VS commands since users explicitly 
            // want them to occur
            if (first.KeyModifiers == KeyModifiers.None && !first.KeyInput.IsFunctionKey)
            {
                return true;
            }

            // In Vim ctlr+shift+f is exactly the same command as ctrl+f.  Vim simply ignores the 
            // shift key when processing a control command with an alpha character.  Visual Studio
            // though does differentiate.  Ctrl+f is differente than Ctrl+Shift+F.  So don't 
            // process any alpha commands which have both Ctrl and Shift as Vim wouldn't 
            // ever hit them
            if (Char.IsLetter(first.Char) && first.KeyModifiers == (KeyModifiers.Shift | KeyModifiers.Control))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Get the localized names of the scopes who's key bindings we find interesting.  This is 
        /// a fairly unpretty way of doing this.  Yet it's the only known way to achieve this in 
        /// Vs 2010.  
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

                        // For "Global".  The id in the registry here is incorrect for Vs 2010
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
                return CreateDefaultImportantScopeSet();
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
        internal static HashSet<string> CreateDefaultImportantScopeSet()
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

        void IKeyBindingService.RunConflictingKeyBindingStateCheck(IVimBuffer buffer)
        {
            RunConflictingKeyBindingStateCheck(buffer);
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

        #region IVimBufferCreationListener

        void IVimBufferCreationListener.VimBufferCreated(IVimBuffer vimBuffer)
        {
            Action doCheck = () =>
            {
                if (ConflictingKeyBindingState == ConflictingKeyBindingState.HasNotChecked)
                {
                    if (_vimApplicationSettings.IgnoredConflictingKeyBinding)
                    {
                        IgnoreAnyConflicts();
                    }
                    else
                    {
                        RunConflictingKeyBindingStateCheck(vimBuffer);
                    }
                }
            };

            // Don't block startup by immediately running a key binding check.  Schedule it 
            // for the future
            _protectedOperations.BeginInvoke(doCheck);
        }

        #endregion
    }
}
