using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Moq;
using Vim;
using Microsoft.VisualStudio.Text.Editor;
using VimCoreTest.Utils;
using Microsoft.FSharp.Core;

namespace VimCoreTest
{
    [TestFixture]
    public class CommandRunnerTest
    {
        private MockFactory _factory;
        private Mock<IStatusUtil> _statusUtil;
        private RegisterMap _registerMap;
        private ITextView _textView;
        private CommandRunner _runnerRaw;
        private ICommandRunner _runner;

        private void Create(params string[] lines)
        {
            _textView = EditorUtil.CreateView(lines);
            _factory = new MockFactory(MockBehavior.Strict);
            _statusUtil = _factory.Create<IStatusUtil>();
            _registerMap = new RegisterMap();
            _runnerRaw = new CommandRunner(
                _textView,
                _registerMap,
                _statusUtil.Object);
            _runner = _runnerRaw;
        }

        private Command CreateSimpleCommand(string name, Func<FSharpOption<int>, Register, CommandResult> func)
        {
            Converter<FSharpOption<int>, FSharpFunc<Register, CommandResult>> outerFunc = count =>
                {
                    Converter<Register,CommandResult> del = register => func(count,register);
                    return FSharpFuncUtil.Create(del);
                };
            var fsharpFunc = FSharpFuncUtil.Create(outerFunc);
            return Command.NewSimpleCommand(name, fsharpFunc);
        }

        private Command CreateMotionCommand(string name, Func<FSharpOption<int>, Register, MotionData, CommandResult> func)
        {
            Converter<FSharpOption<int>, FSharpFunc<Register, FSharpFunc<MotionData,CommandResult>>> func1 = count =>
                {
                    Converter<Register, FSharpFunc<MotionData, CommandResult>> func2 = register =>
                    {
                        Converter<MotionData, CommandResult> func3 = data => func(count, register, data);
                        return FSharpFuncUtil.Create(func3);
                    };

                    return FSharpFuncUtil.Create(func2);
                };
            var fsharpFunc = FSharpFuncUtil.Create(func1);
            return Command.NewMotionCommand(name, fsharpFunc);
        }

        private CommandResult Run(string command)
        {
            CommandResult last = null;
            foreach (var c in command)
            {
                last = _runner.Run(InputUtil.CharToKeyInput(c));
            }
            return last;
        }

        [Test]
        public void Add1()
        {
            Create(String.Empty);
            var command1 = CreateSimpleCommand("foo", (x, y) => CommandResult.CommandCancelled);
            _runner.Add(command1);
            Assert.AreSame(command1, _runner.Commands.Single());
        }

        [Test]
        public void Add2()
        {
            Create(String.Empty);
            var command1 = CreateSimpleCommand("foo", (x, y) => CommandResult.CommandCancelled);
            var command2 = CreateSimpleCommand("bar", (x, y) => CommandResult.CommandCancelled);
            _runner.Add(command1);
            _runner.Add(command2);
            Assert.AreEqual(2, _runner.Commands.Count());
            Assert.IsTrue(_runner.Commands.Contains(command1));
            Assert.IsTrue(_runner.Commands.Contains(command2));
        }

        [Test]
        public void Run_CommandMatch1()
        {
            Create(String.Empty);
            var count1 = 0;
            _runner.Add(CreateSimpleCommand("a", (count, reg) => { count1++; return CommandResult.CommandCompleted; }));
            Run("a");
            Assert.AreEqual(1, count1);
        }

        [Test]
        public void Run_CommandMatch2()
        {
            Create(String.Empty);
            var count1 = 0;
            _runner.Add(CreateSimpleCommand("a", (count, reg) => { count1++; return CommandResult.CommandCompleted; }));
            Run("b");
            Assert.AreEqual(0, count1);
        }

        [Test]
        public void Run_CommandMatch3()
        {
            Create(String.Empty);
            var count1 = 0;
            _runner.Add(CreateSimpleCommand("a", (count, reg) => { count1++; return CommandResult.CommandCompleted; }));
            var count2 = 0;
            _runner.Add(CreateSimpleCommand("b", (count, reg) => { count2++; return CommandResult.CommandCompleted; }));
            Run("b");
            Assert.AreEqual(0, count1);
            Assert.AreEqual(1, count2);
        }

        [Test]
        public void Run_CommandMatch4()
        {
            Create(String.Empty);
            var count1 = 0;
            _runner.Add(CreateSimpleCommand("ab", (count, reg) => { count1++; return CommandResult.CommandCompleted; }));
            var count2 = 0;
            _runner.Add(CreateSimpleCommand("b", (count, reg) => { count2++; return CommandResult.CommandCompleted; }));
            Run("ab");
            Assert.AreEqual(1, count1);
            Assert.AreEqual(0, count2);
        }
    }
}
