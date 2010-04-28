using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vim;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace VimCoreTest.Mock
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

        public void RaiseSwitchedMode(IMode mode)
        {
            if ( SwitchedMode != null )
            {
                SwitchedMode(this, mode);
            }
        }

        public IEnumerable<IMode> AllModes
        {
            get { throw new NotImplementedException(); }
        }

        public Microsoft.FSharp.Collections.FSharpList<KeyInput> BufferedRemapKeyInputs
        {
            get { throw new NotImplementedException(); }
        }

        public bool CanProcessInput(KeyInput value)
        {
            throw new NotImplementedException();
        }

        public void Close()
        {
            throw new NotImplementedException();
        }

        public ICommandMode CommandMode
        {
            get { throw new NotImplementedException(); }
        }

        public IDisabledMode DisabledMode
        {
            get { throw new NotImplementedException(); }
        }

        public IMode GetMode(ModeKind value)
        {
            throw new NotImplementedException();
        }

        public Register GetRegister(char value)
        {
            throw new NotImplementedException();
        }

        public bool IsProcessingInput
        {
            get { throw new NotImplementedException(); }
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

        public INormalMode NormalMode
        {
            get { return (INormalMode)ModeImpl; }
        }

        public bool ProcessChar(char value)
        {
            throw new NotImplementedException();
        }

        public bool ProcessInput(KeyInput value)
        {
            throw new NotImplementedException();
        }

        public IRegisterMap RegisterMap
        {
            get { throw new NotImplementedException(); }
        }

        public IVimLocalSettings Settings
        {
            get { throw new NotImplementedException(); }
        }

#pragma warning disable 67
        public event Microsoft.FSharp.Control.FSharpHandler<string> StatusMessage;

        public event Microsoft.FSharp.Control.FSharpHandler<IEnumerable<string>> StatusMessageLong;

        public event Microsoft.FSharp.Control.FSharpHandler<string> ErrorMessage;

        public event Microsoft.FSharp.Control.FSharpHandler<Tuple<KeyInput, ProcessResult>> KeyInputProcessed;

        public event Microsoft.FSharp.Control.FSharpHandler<KeyInput> KeyInputReceived;

        public event Microsoft.FSharp.Control.FSharpHandler<KeyInput> KeyInputBuffered;

#pragma warning restore 67

        public event Microsoft.FSharp.Control.FSharpHandler<IMode> SwitchedMode;


        public IMode SwitchMode(ModeKind value)
        {
            throw new NotImplementedException();
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
            get { throw new NotImplementedException(); }
        }

        public IVimHost VimHost
        {
            get { throw new NotImplementedException(); }
        }




        public IVisualMode VisualBlockMode
        {
            get { throw new NotImplementedException(); }
        }

        public IVisualMode VisualCharacterMode
        {
            get { throw new NotImplementedException(); }
        }

        public IVisualMode VisualLineMode
        {
            get { throw new NotImplementedException(); }
        }
    }
}
