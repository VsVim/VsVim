using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vim;

namespace VimCoreTest.Utils
{
    internal class MockNormalMode : INormalMode
    {
        internal IVimBuffer VimBufferImpl = null;
        internal bool IsOperatorPendingImpl = false;
        internal bool IsWaitingForInputImpl= false;

        void RaisCommandexecuted(NormalModeCommand command)
        {
            var e = CommandExecuted;
            if (e != null)
            {
                e(this, command);
            }
        }

        public string Command
        {
            get { throw new NotImplementedException(); }
        }

        public event Microsoft.FSharp.Control.FSharpHandler<NormalModeCommand> CommandExecuted;

        public IIncrementalSearch IncrementalSearch
        {
            get { throw new NotImplementedException(); }
        }

        public bool IsOperatorPending
        {
            get { return IsOperatorPendingImpl; }
        }

        public bool IsWaitingForInput
        {
            get { return IsWaitingForInputImpl; }
        }

        public bool CanProcess(KeyInput value)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<KeyInput> Commands
        {
            get { throw new NotImplementedException(); }
        }

        public ModeKind ModeKind
        {
            get { throw new NotImplementedException(); }
        }

        public void OnEnter()
        {
            throw new NotImplementedException();
        }

        public void OnLeave()
        {
            throw new NotImplementedException();
        }

        public ProcessResult Process(KeyInput value)
        {
            throw new NotImplementedException();
        }

        public IVimBuffer VimBuffer
        {
            get { return VimBufferImpl; }
        }
    }
}
