using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vim;

namespace VimCore.Test.Mock
{
    public sealed class MockCommandRunner : ICommandRunner
    {
        public void Add(Command value)
        {
            throw new NotImplementedException();
        }

#pragma warning disable 67
        public event Microsoft.FSharp.Control.FSharpHandler<Tuple<CommandRunData, CommandResult>> CommandRan;
#pragma warning restore 67

        public IEnumerable<Command> Commands
        {
            get { throw new NotImplementedException(); }
        }

        public bool IsWaitingForMoreInput
        {
            get { throw new NotImplementedException(); }
        }

        public void Remove(KeyInputSet value)
        {
            throw new NotImplementedException();
        }

        public void ResetState()
        {
            throw new NotImplementedException();
        }

        public RunKeyInputResult Run(KeyInput value)
        {
            throw new NotImplementedException();
        }

        public CommandRunnerState State
        {
            get { throw new NotImplementedException(); }
        }
    }
}
