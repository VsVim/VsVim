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


        public KeyMappingResult GetKeyInputMapping(KeyInput value)
        {
            throw new NotImplementedException();
        }


        public bool CanProcessNotDirectInsert(KeyInput value)
        {
            throw new NotImplementedException();
        }


        public bool CanProcessAsCommand(KeyInput value)
        {
            throw new NotImplementedException();
        }

        IEnumerable<IMode> IVimBuffer.AllModes
        {
            get { throw new NotImplementedException(); }
        }

        Microsoft.FSharp.Collections.FSharpList<KeyInput> IVimBuffer.BufferedRemapKeyInputs
        {
            get { throw new NotImplementedException(); }
        }

        bool IVimBuffer.CanProcess(KeyInput value)
        {
            throw new NotImplementedException();
        }

        bool IVimBuffer.CanProcessAsCommand(KeyInput value)
        {
            throw new NotImplementedException();
        }

        void IVimBuffer.Close()
        {
            throw new NotImplementedException();
        }

        event Microsoft.FSharp.Control.FSharpHandler<EventArgs> IVimBuffer.Closed
        {
            add { throw new NotImplementedException(); }
            remove { throw new NotImplementedException(); }
        }

        ICommandMode IVimBuffer.CommandMode
        {
            get { throw new NotImplementedException(); }
        }

        IDisabledMode IVimBuffer.DisabledMode
        {
            get { throw new NotImplementedException(); }
        }

        event Microsoft.FSharp.Control.FSharpHandler<string> IVimBuffer.ErrorMessage
        {
            add { throw new NotImplementedException(); }
            remove { throw new NotImplementedException(); }
        }

        IMode IVimBuffer.ExternalEditMode
        {
            get { throw new NotImplementedException(); }
        }

        KeyMappingResult IVimBuffer.GetKeyInputMapping(KeyInput value)
        {
            throw new NotImplementedException();
        }

        IMode IVimBuffer.GetMode(ModeKind value)
        {
            throw new NotImplementedException();
        }

        Register IVimBuffer.GetRegister(RegisterName value)
        {
            throw new NotImplementedException();
        }

        IVimGlobalSettings IVimBuffer.GlobalSettings
        {
            get { throw new NotImplementedException(); }
        }

        IIncrementalSearch IVimBuffer.IncrementalSearch
        {
            get { throw new NotImplementedException(); }
        }

        IInsertMode IVimBuffer.InsertMode
        {
            get { throw new NotImplementedException(); }
        }

        bool IVimBuffer.IsProcessingInput
        {
            get { throw new NotImplementedException(); }
        }

        IJumpList IVimBuffer.JumpList
        {
            get { throw new NotImplementedException(); }
        }

        event Microsoft.FSharp.Control.FSharpHandler<KeyInput> IVimBuffer.KeyInputBuffered
        {
            add { throw new NotImplementedException(); }
            remove { throw new NotImplementedException(); }
        }

        event Microsoft.FSharp.Control.FSharpHandler<KeyInput> IVimBuffer.KeyInputEnd
        {
            add { throw new NotImplementedException(); }
            remove { throw new NotImplementedException(); }
        }

        event Microsoft.FSharp.Control.FSharpHandler<Tuple<KeyInput, ProcessResult>> IVimBuffer.KeyInputProcessed
        {
            add { throw new NotImplementedException(); }
            remove { throw new NotImplementedException(); }
        }

        event Microsoft.FSharp.Control.FSharpHandler<KeyInput> IVimBuffer.KeyInputStart
        {
            add { throw new NotImplementedException(); }
            remove { throw new NotImplementedException(); }
        }

        IVimLocalSettings IVimBuffer.LocalSettings
        {
            get { throw new NotImplementedException(); }
        }

        IMarkMap IVimBuffer.MarkMap
        {
            get { throw new NotImplementedException(); }
        }

        IMode IVimBuffer.Mode
        {
            get { throw new NotImplementedException(); }
        }

        ModeKind IVimBuffer.ModeKind
        {
            get { throw new NotImplementedException(); }
        }

        IMotionUtil IVimBuffer.MotionUtil
        {
            get { throw new NotImplementedException(); }
        }

        string IVimBuffer.Name
        {
            get { throw new NotImplementedException(); }
        }

        INormalMode IVimBuffer.NormalMode
        {
            get { throw new NotImplementedException(); }
        }

        ProcessResult IVimBuffer.Process(KeyInput value)
        {
            throw new NotImplementedException();
        }

        IRegisterMap IVimBuffer.RegisterMap
        {
            get { throw new NotImplementedException(); }
        }

        IInsertMode IVimBuffer.ReplaceMode
        {
            get { throw new NotImplementedException(); }
        }

        void IVimBuffer.SimulateProcessed(KeyInput value)
        {
            throw new NotImplementedException();
        }

        event Microsoft.FSharp.Control.FSharpHandler<string> IVimBuffer.StatusMessage
        {
            add { throw new NotImplementedException(); }
            remove { throw new NotImplementedException(); }
        }

        event Microsoft.FSharp.Control.FSharpHandler<IEnumerable<string>> IVimBuffer.StatusMessageLong
        {
            add { throw new NotImplementedException(); }
            remove { throw new NotImplementedException(); }
        }

        ISubstituteConfirmMode IVimBuffer.SubstituteConfirmMode
        {
            get { throw new NotImplementedException(); }
        }

        IMode IVimBuffer.SwitchMode(ModeKind value, ModeArgument argument)
        {
            throw new NotImplementedException();
        }

        IMode IVimBuffer.SwitchPreviousMode()
        {
            throw new NotImplementedException();
        }

        event Microsoft.FSharp.Control.FSharpHandler<SwitchModeEventArgs> IVimBuffer.SwitchedMode
        {
            add { throw new NotImplementedException(); }
            remove { throw new NotImplementedException(); }
        }

        ITextBuffer IVimBuffer.TextBuffer
        {
            get { throw new NotImplementedException(); }
        }

        ITextSnapshot IVimBuffer.TextSnapshot
        {
            get { throw new NotImplementedException(); }
        }

        ITextView IVimBuffer.TextView
        {
            get { throw new NotImplementedException(); }
        }

        IUndoRedoOperations IVimBuffer.UndoRedoOperations
        {
            get { throw new NotImplementedException(); }
        }

        IVim IVimBuffer.Vim
        {
            get { throw new NotImplementedException(); }
        }

        VimBufferData IVimBuffer.VimBufferData
        {
            get { throw new NotImplementedException(); }
        }

        IVimData IVimBuffer.VimData
        {
            get { throw new NotImplementedException(); }
        }

        IVisualMode IVimBuffer.VisualBlockMode
        {
            get { throw new NotImplementedException(); }
        }

        IVisualMode IVimBuffer.VisualCharacterMode
        {
            get { throw new NotImplementedException(); }
        }

        IVisualMode IVimBuffer.VisualLineMode
        {
            get { throw new NotImplementedException(); }
        }

        event Microsoft.FSharp.Control.FSharpHandler<string> IVimBuffer.WarningMessage
        {
            add { throw new NotImplementedException(); }
            remove { throw new NotImplementedException(); }
        }

        Microsoft.VisualStudio.Text.Operations.ITextStructureNavigator IVimBuffer.WordNavigator
        {
            get { throw new NotImplementedException(); }
        }

        PropertyCollection IPropertyOwner.Properties
        {
            get { throw new NotImplementedException(); }
        }
    }
}
