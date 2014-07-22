using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Vim;
using Vim.Extensions;

namespace Vim.VisualStudio.Implementation.Misc
{
    internal sealed class StandardCommandTarget : ICommandTarget
    {
        private readonly IVimBuffer _vimBuffer;
        private readonly IVimBufferCoordinator _vimBufferCoordinator;
        private readonly ITextBuffer _textBuffer;
        private readonly ITextView _textView;
        private readonly ITextManager _textManager;
        private readonly IDisplayWindowBroker _broker;
        private readonly IOleCommandTarget _nextOleCommandTarget;

        internal StandardCommandTarget(
            IVimBufferCoordinator vimBufferCoordinator,
            ITextManager textManager,
            IDisplayWindowBroker broker,
            IOleCommandTarget nextOleCommandTarget)
        {
            _vimBuffer = vimBufferCoordinator.VimBuffer;
            _vimBufferCoordinator = vimBufferCoordinator;
            _textBuffer = _vimBuffer.TextBuffer;
            _textView = _vimBuffer.TextView;
            _textManager = textManager;
            _broker = broker;
            _nextOleCommandTarget = nextOleCommandTarget;
        }

        /// <summary>
        /// Try and map a KeyInput to a single KeyInput value.  This will only succeed for KeyInput 
        /// values which have no mapping or map to a single KeyInput value
        /// </summary>
        private bool TryGetSingleMapping(KeyInput original, out KeyInput mapped)
        {
            var result = _vimBuffer.GetKeyInputMapping(original);
            if (result.IsNeedsMoreInput || result.IsRecursive || result.IsPartiallyMapped)
            {
                // No single mapping
                mapped = null;
                return false;
            }

            if (result.IsMapped)
            {
                var set = ((KeyMappingResult.Mapped)result).Item;
                if (!set.IsOneKeyInput)
                {
                    mapped = null;
                    return false;
                }

                mapped = set.FirstKeyInput.Value;
                return true;
            }

            // Shouldn't get here because all cases of KeyMappingResult should be
            // handled above
            Contract.Assert(false);
            mapped = null;
            return false;
        }

        /// <summary>
        /// Is this KeyInput intended to be processed by the active display window
        /// </summary>
        private bool IsDisplayWindowKey(KeyInput keyInput)
        {
            // Consider normal completion
            if (_broker.IsCompletionActive)
            {
                return
                    keyInput.IsArrowKey ||
                    keyInput == KeyInputUtil.EnterKey ||
                    keyInput == KeyInputUtil.TabKey ||
                    keyInput.Key == VimKey.Back;
            }

            if (_broker.IsSmartTagSessionActive)
            {
                return
                    keyInput.IsArrowKey ||
                    keyInput == KeyInputUtil.EnterKey;
            }

            if (_broker.IsSignatureHelpActive)
            {
                return keyInput.IsArrowKey;
            }

            return false;
        }

        /// <summary>
        /// Try and process the KeyInput from the Exec method.  This method decides whether or not
        /// a key should be processed directly by IVimBuffer or if should be going through 
        /// IOleCommandTarget.  Generally the key is processed by IVimBuffer but for many intellisense
        /// scenarios we want the key to be routed to Visual Studio directly.  Issues to consider 
        /// here are ...
        /// 
        ///  - How should the KeyInput participate in Macro playback?
        ///  - Does both VsVim and Visual Studio need to process the key (Escape mainly)
        ///  
        /// </summary>
        private bool TryProcessWithBuffer(KeyInput keyInput)
        {
            // If the IVimBuffer can't process it then it doesn't matter
            if (!_vimBuffer.CanProcess(keyInput))
            {
                return false;
            }

            // In the middle of a word completion session let insert mode handle the input.  It's 
            // displaying the intellisense itself and this method is meant to let custom intellisense
            // operate normally
            if (_vimBuffer.ModeKind == ModeKind.Insert && _vimBuffer.InsertMode.ActiveWordCompletionSession.IsSome())
            {
                return _vimBuffer.Process(keyInput).IsAnyHandled;
            }

            // If we are in a peek definition window and normal mode we need to let the Escape key 
            // pass on to the next command target.  This is necessary to close the peek definition
            // window
            if (_vimBuffer.ModeKind == ModeKind.Normal && 
                _textView.Roles.Contains(Constants.TextViewRoleEmbeddedPeekTextView) &&
                keyInput == KeyInputUtil.EscapeKey)
            {
                return false;
            }

            // The only time we actively intercept keys and route them through IOleCommandTarget
            // is when one of the IDisplayWindowBroker windows is active
            //
            // In those cases if the KeyInput is a command which should be handled by the
            // display window we route it through IOleCommandTarget to get the proper 
            // experience for those features
            if (!_broker.IsAnyDisplayActive())
            {
                return _vimBuffer.Process(keyInput).IsAnyHandled;
            }

            // Next we need to consider here are Key mappings.  The CanProcess and Process APIs 
            // will automatically map the KeyInput under the hood at the IVimBuffer level but
            // not at the individual IMode.  Have to manually map here and test against the 
            // mapped KeyInput
            KeyInput mapped;
            if (!TryGetSingleMapping(keyInput, out mapped))
            {
                _broker.DismissDisplayWindows();
                return _vimBuffer.Process(keyInput).IsAnyHandled;
            }

            bool handled;
            if (IsDisplayWindowKey(mapped))
            {
                // If the key which actually needs to be processed is a display window key, say 
                // down, up, etc..., then forward it on to the next IOleCommandTarget.  It is responsible
                // for mapping that key to action against the display window 
                handled = ErrorHandler.Succeeded(_nextOleCommandTarget.Exec(mapped));
            }
            else
            {
                // Intentionally using keyInput here and not mapped.  Process will do mapping on the
                // provided input hence we should be using the original keyInput here not mapped
                handled = _vimBuffer.Process(keyInput).IsAnyHandled;
            }

            // The Escape key should always dismiss the active completion session.  However Vim
            // itself is mostly ignorant of display windows and typically won't dismiss them
            // as part of processing Escape (one exception is insert mode).  Dismiss it here if 
            // it's still active
            if (mapped.Key == VimKey.Escape && _broker.IsAnyDisplayActive())
            {
                _broker.DismissDisplayWindows();
            }

            return handled;
        }

        /// <summary>
        /// This intercepts the Paste command in Visual Studio and tries to make it work for VsVim. This is 
        /// only possible in a subset of states like command line mode.  Otherwise we default to Visual Studio
        /// behavior
        /// </summary>
        private bool Paste()
        {
            if (_vimBuffer.ModeKind != ModeKind.Command)
            {
                return false;
            }

            try
            {
                var text = Clipboard.GetText();
                var command = _vimBuffer.CommandMode.Command;
                _vimBuffer.CommandMode.Command = command + text;
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal bool Exec(EditCommand editCommand, out Action action)
        {
            action = null;

            // If the KeyInput was already handled then pretend we handled it here 
            if (editCommand.HasKeyInput && _vimBufferCoordinator.IsDiscarded(editCommand.KeyInput))
            {
                return true;
            }

            switch (editCommand.EditCommandKind)
            {
                case EditCommandKind.Undo:
                    // The user hit the undo button.  Don't attempt to map anything here and instead just 
                    // run a single Vim undo operation
                    _vimBuffer.UndoRedoOperations.Undo(1);
                    return true;

                case EditCommandKind.Redo:
                    // The user hit the redo button.  Don't attempt to map anything here and instead just 
                    // run a single Vim redo operation
                    _vimBuffer.UndoRedoOperations.Redo(1);
                    return true;

                case EditCommandKind.Paste:
                    return Paste();

                case EditCommandKind.GoToDefinition:
                    // The GoToDefinition command will often cause a selection to occur in the 
                    // buffer.  We don't want that to cause us to enter Visual Mode so clear it
                    // out.  This command can cause the active document to switch if the target
                    // of the goto def is in another file.  This file won't be registered as the
                    // active file yet so just clear out the active selections
                    action = () =>
                        {
                            _textManager.GetDocumentTextViews(DocumentLoad.RespectLazy)
                                .Where(x => !x.Selection.IsEmpty)
                                .ForEach(x => x.Selection.Clear());
                        };
                    return false;

                case EditCommandKind.Comment:
                case EditCommandKind.Uncomment:
                    // The comment / uncomment command will often induce a selection on the 
                    // editor even if there was no selection before the command was run (single line
                    // case).  
                    if (_textView.Selection.IsEmpty)
                    {
                        action = () => { _textView.Selection.Clear(); };
                    }
                    return false;

                case EditCommandKind.UserInput:
                case EditCommandKind.VisualStudioCommand:
                    if (editCommand.HasKeyInput)
                    {
                        var keyInput = editCommand.KeyInput;

                        // Discard the input if it's been flagged by a previous QueryStatus
                        if (_vimBufferCoordinator.IsDiscarded(keyInput))
                        {
                            return true;
                        }

                        // Try and process the command with the IVimBuffer
                        if (TryProcessWithBuffer(keyInput))
                        {
                            return true;
                        }
                    }
                    return false;
                default:
                    Debug.Assert(false);
                    return false;
            }
        }

        private CommandStatus QueryStatus(EditCommand editCommand)
        {
            var action = CommandStatus.PassOn;
            switch (editCommand.EditCommandKind)
            {
                case EditCommandKind.Undo:
                case EditCommandKind.Redo:
                    action = CommandStatus.Enable;
                    break;
                case EditCommandKind.Paste:
                    action = _vimBuffer.ModeKind == ModeKind.Command
                        ? CommandStatus.Enable
                        : CommandStatus.PassOn;
                    break;
                default:
                    if (editCommand.HasKeyInput && _vimBuffer.CanProcess(editCommand.KeyInput))
                    {
                        action = CommandStatus.Enable;
                    }
                    break;
            }

            return action;
        }

        #region ICommandTarget

        CommandStatus ICommandTarget.QueryStatus(EditCommand editCommand)
        {
            return QueryStatus(editCommand);
        }

        bool ICommandTarget.Exec(EditCommand editCommand, out Action action)
        {
            return Exec(editCommand, out action);
        }

        #endregion
    }

    [Export(typeof(ICommandTargetFactory))]
    [Name(Constants.StandardCommandTargetName)]
    [Order]
    internal sealed class StandardCommandTargetFactory : ICommandTargetFactory
    {
        private readonly ITextManager _textManager;
        private readonly IDisplayWindowBrokerFactoryService _displayWindowBrokerFactory;

        [ImportingConstructor]
        internal StandardCommandTargetFactory(ITextManager textManager, IDisplayWindowBrokerFactoryService displayWindowBrokerFactory)
        {
            _textManager = textManager;
            _displayWindowBrokerFactory = displayWindowBrokerFactory;
        }

        ICommandTarget ICommandTargetFactory.CreateCommandTarget(IOleCommandTarget nextCommandTarget, IVimBufferCoordinator vimBufferCoordinator)
        {
            var displayWindowBroker = _displayWindowBrokerFactory.GetDisplayWindowBroker(vimBufferCoordinator.VimBuffer.TextView);
            return new StandardCommandTarget(vimBufferCoordinator, _textManager, displayWindowBroker, nextCommandTarget);
        }
    }
}
