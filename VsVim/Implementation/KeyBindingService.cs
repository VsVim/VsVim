using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vim;
using System.Windows.Input;
using EnvDTE;
using System.ComponentModel.Composition.Primitives;
using System.ComponentModel.Composition;
using System.Windows;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using System.Collections.ObjectModel;
using Microsoft.Internal.VisualStudio.PlatformUI;
using System.Threading;
using VsVim.UI;

namespace VsVim.Implementation
{

    /// <summary>
    /// Responsible for dealing with the conflicting key bindings inside of Visual Studio
    /// </summary>
    [Export(typeof(IKeyBindingService))]
    internal sealed class KeyBindingService : IKeyBindingService
    {
        private readonly _DTE _dte;
        private readonly IOptionsDialogService _optionsDialogService;
        private ConflictingKeyBindingState _state;
        private CommandKeyBindingSnapshot _snapshot;

        [ImportingConstructor]
        internal KeyBindingService(SVsServiceProvider sp, IOptionsDialogService service)
        {
            _dte = sp.GetService<SDTE, _DTE>();
            _optionsDialogService = service;
        }

        internal void UpdateConflictingState(ConflictingKeyBindingState state, CommandKeyBindingSnapshot snapshot )
        {
            _snapshot = snapshot;
            ConflictingKeyBindingState = state;
        }

        public ConflictingKeyBindingState ConflictingKeyBindingState
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

        public event EventHandler ConflictingKeyBindingStateChanged;

        public void RunConflictingKeyBindingStateCheck(IVimBuffer buffer, Action<ConflictingKeyBindingState, CommandKeyBindingSnapshot> onComplete)
        {
            var needed = buffer.AllModes.Select(x => x.Commands).SelectMany(x => x).ToList();
            needed.Add(buffer.Settings.GlobalSettings.DisableCommand);
            RunConflictingKeyBindingStateCheck(needed, onComplete);
        }

        public void RunConflictingKeyBindingStateCheck(IEnumerable<KeyInput> neededInputs, Action<ConflictingKeyBindingState, CommandKeyBindingSnapshot> onComplete)
        {
            if (_snapshot != null)
            {
                onComplete(_state, _snapshot);
                return;
            }

            var util = new KeyBindingUtil(_dte);
            var set = new HashSet<KeyInput>(neededInputs);
            _snapshot = util.CreateCommandKeyBindingSnapshot(set);
            if (_snapshot.Conflicting.Any())
            {
                ConflictingKeyBindingState = ConflictingKeyBindingState.FoundConflicts;
            }
            else
            {
                ConflictingKeyBindingState = ConflictingKeyBindingState.ConflictsIgnoredOrResolved;
            }
        }

        public void ResetConflictingKeyBindingState()
        {
            ConflictingKeyBindingState = ConflictingKeyBindingState.HasNotChecked;
            _snapshot = null;
        }

        public void ResolveAnyConflicts()
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

        public void IgnoreAnyConflicts()
        {
            ConflictingKeyBindingState = ConflictingKeyBindingState.ConflictsIgnoredOrResolved;
            _snapshot = null;
        }
    }
}
