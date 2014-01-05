using System;
using System.Collections.Generic;
using Microsoft.FSharp.Core;

namespace Vim.UnitTest.Mock
{
    public class MockNormalMode : INormalMode
    {
        public IVimBuffer VimBufferImpl = null;
        public ICommandRunner CommandRunnerImpl = null;
        public bool InReplaceImpl = false;

        public string Command
        {
            get { throw new NotImplementedException(); }
        }

        public IIncrementalSearch IncrementalSearch
        {
            get { throw new NotImplementedException(); }
        }

        public bool CanProcess(KeyInput value)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<KeyInputSet> CommandNames
        {
            get { throw new NotImplementedException(); }
        }

        public ModeKind ModeKind
        {
            get { throw new NotImplementedException(); }
        }

        public FSharpOption<ModeKind> OneTimeMode
        {
            get { throw new NotImplementedException(); }
        }

        public void OnEnter(ModeArgument arg)
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

        public bool InReplace
        {
            get { return InReplaceImpl; }
        }

        public bool InCount
        {
            get { return false; }
        }

        public ICommandRunner CommandRunner
        {
            get { return CommandRunnerImpl; }
        }

        public void OnClose()
        {
            throw new NotImplementedException();
        }

        public KeyRemapMode KeyRemapMode
        {
            get { throw new NotImplementedException(); }
        }

        public IVimTextBuffer VimTextBuffer
        {
            get { throw new NotImplementedException(); }
        }
    }
}
