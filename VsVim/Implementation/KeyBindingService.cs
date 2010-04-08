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
    public sealed class KeyBindingService : IKeyBindingService
    {
        private readonly _DTE _dte;
        private ConflictingKeyBindingState _state;
        private CommandKeyBindingSnapshot _snapshot;

        [ImportingConstructor]
        public KeyBindingService(SVsServiceProvider sp)
        {
            _dte = sp.GetService<SDTE, _DTE>();
        }

        public ConflictingKeyBindingState ConflictingKeyBindingState
        {
            get { return _state; }
        }

        public event EventHandler ConflictingKeyBindingStateChanged;

        public void RunConflictingKeyBindingStateCheck(IVimBuffer buffer, Action<ConflictingKeyBindingState, CommandKeyBindingSnapshot> onComplete)
        {
            if (_snapshot != null)
            {
                onComplete(_state, _snapshot);
                return;
            }

            var util = new KeyBindingUtil(_dte);
            _snapshot = util.CreateCommandKeyBindingSnapshot(buffer);
            if (_snapshot.Conflicting.Any())
            {
                _state = ConflictingKeyBindingState.FoundConflicts;
            }
            else
            {
                _state = ConflictingKeyBindingState.NoConflicts;
            }
        }

        public void ResetConflictingKeyBindingState()
        {
            _state = ConflictingKeyBindingState.HasNotChecked;
            _snapshot = null;
        }

        public void ResolveAnyConflicts()
        {
            if (_snapshot != null || _state != ConflictingKeyBindingState.FoundConflicts)
            {
                return;
            }

            UI.ConflictingKeyBindingDialog.DoShow(_snapshot);
            _state = ConflictingKeyBindingState.NoConflicts;
            _snapshot = null;
        }
    }
}
