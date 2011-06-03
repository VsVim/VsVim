using System;
using System.Collections.Generic;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Vim.UnitTest.Mock
{
    /// <summary>
    /// There is either a bug in F# or Moq which prevents the raising of events through an IMock if 
    /// the event is defined in F#.  This class exists to work around that limitation
    /// </summary>
    public class MockVimBuffer : IVimBuffer
    {
        public ITextBuffer TextBufferImpl;
        public ITextView TextViewImpl;
        public IMode ModeImpl;
        public ModeKind ModeKindImpl;
        public INormalMode NormalModeImpl;
        public IVisualMode VisualBlockModeImpl;
        public IVisualMode VisualCharacterModeImpl;
        public IVisualMode VisualLineModeImpl;
        public ICommandMode CommandModeImpl;
        public IDisabledMode DisabledModeImpl;
        public ISubstituteConfirmMode SubstituteConfirmModeImpl;
        public IIncrementalSearch IncrementalSearchImpl;
        public IInsertMode InsertModeImpl;
        public IInsertMode ReplaceModeImpl;
        public IMode ExternalEditModeImpl;
        public bool IsProcessingInputImpl;
        public PropertyCollection PropertiesImpl;
        public IVimData VimDataImpl;
        public IVim VimImpl;

        public PropertyCollection Properties
        {
            get { return PropertiesImpl; }
        }

        public IEnumerable<IMode> AllModes
        {
            get { throw new NotImplementedException(); }
        }

        public Microsoft.FSharp.Collections.FSharpList<KeyInput> BufferedRemapKeyInputs
        {
            get { throw new NotImplementedException(); }
        }

        public bool CanProcess(KeyInput value)
        {
            throw new NotImplementedException();
        }

        public void Close()
        {
            throw new NotImplementedException();
        }

        public ICommandMode CommandMode
        {
            get { return CommandModeImpl; }
        }

        public IDisabledMode DisabledMode
        {
            get { return DisabledModeImpl; }
        }

        public IMode GetMode(ModeKind value)
        {
            throw new NotImplementedException();
        }

        public Register GetRegister(RegisterName name)
        {
            throw new NotImplementedException();
        }

        public bool IsProcessingInput
        {
            get { return IsProcessingInputImpl; }
        }

        public IJumpList JumpList
        {
            get { throw new NotImplementedException(); }
        }

        public IMarkMap MarkMap
        {
            get { throw new NotImplementedException(); }
        }

        public IMode Mode
        {
            get { return ModeImpl; }
        }

        public ModeKind ModeKind
        {
            get { return ModeKindImpl; }
        }

        public string Name
        {
            get { throw new NotImplementedException(); }
        }

        public IInsertMode InsertMode
        {
            get { return InsertModeImpl; }
        }

        public IInsertMode ReplaceMode
        {
            get { return ReplaceModeImpl; }
        }

        public INormalMode NormalMode
        {
            get { return NormalModeImpl; }
        }

        public bool ProcessChar(char value)
        {
            throw new NotImplementedException();
        }

        public ProcessResult Process(KeyInput value)
        {
            throw new NotImplementedException();
        }

        public IRegisterMap RegisterMap
        {
            get { throw new NotImplementedException(); }
        }

        public IVimLocalSettings LocalSettings
        {
            get { throw new NotImplementedException(); }
        }

        public void RaiseSwitchedMode(IMode mode)
        {
            RaiseSwitchedMode(new SwitchModeEventArgs(FSharpOption<IMode>.None, mode));
        }

        public void RaiseSwitchedMode(SwitchModeEventArgs args)
        {
            if (SwitchedMode != null)
            {
                SwitchedMode(this, args);
            }
        }

        public void RaiseClosed()
        {
            if (Closed != null)
            {
                Closed(this, EventArgs.Empty);
            }
        }

        public void RaiseStatusMessage(string message)
        {
            if (StatusMessage != null)
            {
                StatusMessage(this, message);
            }
        }

        public void RaiseStatusMessageLong(params string[] lines)
        {
            if (StatusMessageLong != null)
            {
                StatusMessageLong(this, lines);
            }
        }

        public void RaiseErrorMessage(string message)
        {
            if (ErrorMessage != null)
            {
                ErrorMessage(this, message);
            }
        }

        public void RaiseKeyInputProcessed(KeyInput ki, ProcessResult result)
        {
            if (KeyInputProcessed != null)
            {
                KeyInputProcessed(this, Tuple.Create(ki, result));
            }
        }

        public void RaiseKeyInputStart(KeyInput ki)
        {
            if (KeyInputStart != null)
            {
                KeyInputStart(this, ki);
            }
        }

        public void RaiseKeyInputEnd(KeyInput ki)
        {
            if (KeyInputEnd != null)
            {
                KeyInputEnd(this, ki);
            }
        }

        public void RaiseKeyInputBuffered(KeyInput ki)
        {
            if (KeyInputBuffered != null)
            {
                KeyInputBuffered(this, ki);
            }
        }

        public event Microsoft.FSharp.Control.FSharpHandler<string> StatusMessage;

        public event Microsoft.FSharp.Control.FSharpHandler<IEnumerable<string>> StatusMessageLong;

        public event Microsoft.FSharp.Control.FSharpHandler<string> ErrorMessage;

        public event Microsoft.FSharp.Control.FSharpHandler<Tuple<KeyInput, ProcessResult>> KeyInputProcessed;

        public event Microsoft.FSharp.Control.FSharpHandler<KeyInput> KeyInputStart;

        public event Microsoft.FSharp.Control.FSharpHandler<KeyInput> KeyInputEnd;

        public event Microsoft.FSharp.Control.FSharpHandler<KeyInput> KeyInputBuffered;

        public event Microsoft.FSharp.Control.FSharpHandler<SwitchModeEventArgs> SwitchedMode;

        public event Microsoft.FSharp.Control.FSharpHandler<EventArgs> Closed;


        public IMode SwitchMode(ModeKind value, ModeArgument arg)
        {
            ModeKindImpl = value;
            return GetMode(value);
        }

        public IMode SwitchPreviousMode()
        {
            throw new NotImplementedException();
        }

        public Microsoft.VisualStudio.Text.ITextBuffer TextBuffer
        {
            get { return TextBufferImpl; }
        }

        public Microsoft.VisualStudio.Text.ITextSnapshot TextSnapshot
        {
            get { return TextBufferImpl.CurrentSnapshot; }
        }

        public Microsoft.VisualStudio.Text.Editor.ITextView TextView
        {
            get { return TextViewImpl; }
        }

        public IVim Vim
        {
            get { return VimImpl; }
        }

        public IVimHost VimHost
        {
            get { throw new NotImplementedException(); }
        }

        public IVisualMode VisualBlockMode
        {
            get { return VisualBlockModeImpl; }
        }

        public IVisualMode VisualCharacterMode
        {
            get { return VisualCharacterModeImpl; }
        }

        public IVisualMode VisualLineMode
        {
            get { return VisualLineModeImpl; }
        }

        public IVimData VimData
        {
            get { return VimDataImpl; }
        }

        public ISubstituteConfirmMode SubstituteConfirmMode
        {
            get { return SubstituteConfirmModeImpl; }
        }

        public IMode ExternalEditMode
        {
            get { return ExternalEditModeImpl; }
        }

        public IIncrementalSearch IncrementalSearch
        {
            get { return IncrementalSearchImpl; }
        }


        public IMotionUtil MotionUtil
        {
            get { throw new NotImplementedException(); }
        }


        public void SimulateProcessed(KeyInput value)
        {
            throw new NotImplementedException();
        }


        public Microsoft.VisualStudio.Text.Operations.ITextStructureNavigator WordNavigator
        {
            get { throw new NotImplementedException(); }
        }

#pragma warning disable 67
        public event Microsoft.FSharp.Control.FSharpHandler<string> WarningMessage;
#pragma warning restore 67


        public IUndoRedoOperations UndoRedoOperations
        {
            get { throw new NotImplementedException(); }
        }


        public VimBufferData VimBufferData
        {
            get { throw new NotImplementedException(); }
        }
    }
}
