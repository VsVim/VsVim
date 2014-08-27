using System;
using System.Collections.Generic;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
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
        public ISelectMode SelectBlockModeImpl;
        public ISelectMode SelectCharacterModeImpl;
        public ISelectMode SelectLineModeImpl;
        public ICommandUtil CommandUtilImpl;
        public IMode ExternalEditModeImpl;
        public bool IsProcessingInputImpl;
        public PropertyCollection PropertiesImpl;
        public IVimData VimDataImpl;
        public IVim VimImpl;
        public FSharpOption<ModeKind> InOneTimeCommandImpl;
        public IVimGlobalSettings GlobalSettingsImpl;

        public PropertyCollection Properties
        {
            get { return PropertiesImpl; }
        }

        public IEnumerable<IMode> AllModes
        {
            get { throw new NotImplementedException(); }
        }

        public FSharpList<KeyInput> BufferedKeyInputs
        {
            get { throw new NotImplementedException(); }
        }

        event EventHandler<SwitchModeEventArgs> IVimBuffer.SwitchedMode
        {
            add
            {
                throw new NotImplementedException();
            }

            remove
            {
                throw new NotImplementedException();
            }
        }

        event EventHandler<KeyInputStartEventArgs> IVimBuffer.KeyInputStart
        {
            add
            {
                throw new NotImplementedException();
            }

            remove
            {
                throw new NotImplementedException();
            }
        }

        event EventHandler<KeyInputStartEventArgs> IVimBuffer.KeyInputProcessing
        {
            add
            {
                throw new NotImplementedException();
            }

            remove
            {
                throw new NotImplementedException();
            }
        }

        event EventHandler<KeyInputProcessedEventArgs> IVimBuffer.KeyInputProcessed
        {
            add
            {
                throw new NotImplementedException();
            }

            remove
            {
                throw new NotImplementedException();
            }
        }

        event EventHandler<KeyInputSetEventArgs> IVimBuffer.KeyInputBuffered
        {
            add
            {
                throw new NotImplementedException();
            }

            remove
            {
                throw new NotImplementedException();
            }
        }

        event EventHandler<KeyInputEventArgs> IVimBuffer.KeyInputEnd
        {
            add
            {
                throw new NotImplementedException();
            }

            remove
            {
                throw new NotImplementedException();
            }
        }

        event EventHandler<StringEventArgs> IVimBuffer.WarningMessage
        {
            add
            {
                throw new NotImplementedException();
            }

            remove
            {
                throw new NotImplementedException();
            }
        }

        event EventHandler<StringEventArgs> IVimBuffer.ErrorMessage
        {
            add
            {
                throw new NotImplementedException();
            }

            remove
            {
                throw new NotImplementedException();
            }
        }

        event EventHandler<StringEventArgs> IVimBuffer.StatusMessage
        {
            add
            {
                throw new NotImplementedException();
            }

            remove
            {
                throw new NotImplementedException();
            }
        }

        event EventHandler IVimBuffer.Closing
        {
            add
            {
                throw new NotImplementedException();
            }

            remove
            {
                throw new NotImplementedException();
            }
        }

        event EventHandler IVimBuffer.Closed
        {
            add
            {
                throw new NotImplementedException();
            }

            remove
            {
                throw new NotImplementedException();
            }
        }

        public bool CanProcess(KeyInput value)
        {
            throw new NotImplementedException();
        }

        public bool IsClosed
        {
            get { throw new NotImplementedException(); }
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

        public void ProcessBufferedKeyInputs()
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

        public IVimGlobalSettings GlobalSettings
        {
            get { return GlobalSettingsImpl; }
        }

        public void RaiseSwitchedMode(IMode mode)
        {
            RaiseSwitchedMode(new SwitchModeEventArgs(mode, mode));
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
                StatusMessage(this, new StringEventArgs(message));
            }
        }

        public void RaiseErrorMessage(string message)
        {
            if (ErrorMessage != null)
            {
                ErrorMessage(this, new StringEventArgs(message));
            }
        }

        public void RaiseKeyInputProcessed(KeyInput ki, ProcessResult result)
        {
            if (KeyInputProcessed != null)
            {
                KeyInputProcessed(this, new KeyInputProcessedEventArgs(ki, result));
            }
        }

        public void RaiseKeyInputStart(KeyInput ki)
        {
            if (KeyInputStart != null)
            {
                KeyInputStart(this, new KeyInputStartEventArgs(ki));
            }
        }

        public void RaiseKeyInputEnd(KeyInput ki)
        {
            if (KeyInputEnd != null)
            {
                KeyInputEnd(this, new KeyInputEventArgs(ki));
            }
        }

        public void RaiseKeyInputBuffered(KeyInputSet keyInputSet)
        {
            if (KeyInputBuffered != null)
            {
                KeyInputBuffered(this, new KeyInputSetEventArgs(keyInputSet));
            }
        }

        public event EventHandler<StringEventArgs> StatusMessage;

        public event EventHandler<StringEventArgs> ErrorMessage;

        public event EventHandler<KeyInputProcessedEventArgs> KeyInputProcessed;

        public event EventHandler<KeyInputStartEventArgs> KeyInputStart;

        public event EventHandler<KeyInputEventArgs> KeyInputEnd;

        public event EventHandler<KeyInputSetEventArgs> KeyInputBuffered;

        public event EventHandler<SwitchModeEventArgs> SwitchedMode;

#pragma warning disable 67
        public event EventHandler<KeyInputStartEventArgs> KeyInputProcessing;

        public event EventHandler Closing;
#pragma warning restore 67

        public event EventHandler Closed;

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

        public ISelectMode SelectBlockMode
        {
            get { return SelectBlockModeImpl; }
        }

        public ISelectMode SelectCharacterMode
        {
            get { return SelectCharacterModeImpl; }
        }

        public ISelectMode SelectLineMode
        {
            get { return SelectLineModeImpl; }
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
        public event EventHandler<StringEventArgs> WarningMessage;
#pragma warning restore 67

        public IUndoRedoOperations UndoRedoOperations
        {
            get { throw new NotImplementedException(); }
        }


        public IVimBufferData VimBufferData
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

        Register IVimBuffer.GetRegister(RegisterName value)
        {
            throw new NotImplementedException();
        }

        IMode IVimBuffer.GetMode(ModeKind value)
        {
            throw new NotImplementedException();
        }

        KeyMappingResult IVimBuffer.GetKeyInputMapping(KeyInput value)
        {
            throw new NotImplementedException();
        }

        ProcessResult IVimBuffer.Process(KeyInput value)
        {
            throw new NotImplementedException();
        }

        void IVimBuffer.ProcessBufferedKeyInputs()
        {
            throw new NotImplementedException();
        }

        void IVimBuffer.ProcessProvisional(KeyInput value)
        {
            throw new NotImplementedException();
        }

        bool IVimBuffer.CanProcess(KeyInput value)
        {
            throw new NotImplementedException();
        }

        bool IVimBuffer.CanProcessAsCommand(KeyInput value)
        {
            throw new NotImplementedException();
        }

        bool IVimBuffer.CanProcessProvisional(KeyInput value)
        {
            throw new NotImplementedException();
        }

        IMode IVimBuffer.SwitchMode(ModeKind value1, ModeArgument value2)
        {
            throw new NotImplementedException();
        }

        IMode IVimBuffer.SwitchPreviousMode()
        {
            throw new NotImplementedException();
        }

        void IVimBuffer.SimulateProcessed(KeyInput value)
        {
            throw new NotImplementedException();
        }

        void IVimBuffer.Close()
        {
            throw new NotImplementedException();
        }

        public IVimTextBuffer VimTextBuffer
        {
            get { throw new NotImplementedException(); }
        }

        public IVimWindowSettings WindowSettings
        {
            get { throw new NotImplementedException(); }
        }

        public FSharpOption<string> CurrentDirectory
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public FSharpOption<ModeKind> InOneTimeCommand
        {
            get { return InOneTimeCommandImpl; }
        }

        public ICommandUtil CommandUtil
        {
            get { return CommandUtilImpl; }
        }

        IEnumerable<IMode> IVimBuffer.AllModes
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        FSharpList<KeyInput> IVimBuffer.BufferedKeyInputs
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        FSharpOption<string> IVimBuffer.CurrentDirectory
        {
            get
            {
                throw new NotImplementedException();
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        ICommandUtil IVimBuffer.CommandUtil
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        IVimGlobalSettings IVimBuffer.GlobalSettings
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        IIncrementalSearch IVimBuffer.IncrementalSearch
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        bool IVimBuffer.IsProcessingInput
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        bool IVimBuffer.IsClosed
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        IJumpList IVimBuffer.JumpList
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        IVimLocalSettings IVimBuffer.LocalSettings
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        IMarkMap IVimBuffer.MarkMap
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        IMode IVimBuffer.Mode
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        ModeKind IVimBuffer.ModeKind
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        string IVimBuffer.Name
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        FSharpOption<ModeKind> IVimBuffer.InOneTimeCommand
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        IRegisterMap IVimBuffer.RegisterMap
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        ITextBuffer IVimBuffer.TextBuffer
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        ITextSnapshot IVimBuffer.TextSnapshot
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        ITextView IVimBuffer.TextView
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        IMotionUtil IVimBuffer.MotionUtil
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        IUndoRedoOperations IVimBuffer.UndoRedoOperations
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        IVim IVimBuffer.Vim
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        IVimTextBuffer IVimBuffer.VimTextBuffer
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        IVimBufferData IVimBuffer.VimBufferData
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        ITextStructureNavigator IVimBuffer.WordNavigator
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        IVimWindowSettings IVimBuffer.WindowSettings
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        IVimData IVimBuffer.VimData
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        INormalMode IVimBuffer.NormalMode
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        ICommandMode IVimBuffer.CommandMode
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        IDisabledMode IVimBuffer.DisabledMode
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        IVisualMode IVimBuffer.VisualCharacterMode
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        IVisualMode IVimBuffer.VisualLineMode
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        IVisualMode IVimBuffer.VisualBlockMode
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        IInsertMode IVimBuffer.InsertMode
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        IInsertMode IVimBuffer.ReplaceMode
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        ISelectMode IVimBuffer.SelectCharacterMode
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        ISelectMode IVimBuffer.SelectLineMode
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        ISelectMode IVimBuffer.SelectBlockMode
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        ISubstituteConfirmMode IVimBuffer.SubstituteConfirmMode
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        IMode IVimBuffer.ExternalEditMode
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        PropertyCollection IPropertyOwner.Properties
        {
            get
            {
                throw new NotImplementedException();
            }
        }
    }
}
