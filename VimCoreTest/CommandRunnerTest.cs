using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Moq;
using Vim;
using Vim.Extensions;
using Microsoft.VisualStudio.Text.Editor;
using VimCoreTest.Utils;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;

namespace VimCoreTest
{
    [TestFixture]
    public class CommandRunnerTest
    {
        private MockFactory _factory;
        private Mock<IStatusUtil> _statusUtil;
        private IRegisterMap _registerMap;
        private ITextView _textView;
        private CommandRunner _runnerRaw;
        private ICommandRunner _runner;

        private void Create(params string[] lines)
        {
            _textView = EditorUtil.CreateView(lines);
            _factory = new MockFactory(MockBehavior.Strict);
            _statusUtil = _factory.Create<IStatusUtil>();
            _registerMap = new RegisterMap();
            _runnerRaw = new CommandRunner(Tuple.Create(
                _textView,
                _registerMap,
                _statusUtil.Object));
            _runner = _runnerRaw;
        }

        private Command CreateRepeatableCommand(string name, Func<FSharpOption<int>, Register, CommandResult> func)
        {
            Converter<FSharpOption<int>, FSharpFunc<Register, CommandResult>> outerFunc = count =>
                {
                    Converter<Register, CommandResult> del = register => func(count, register);
                    return FSharpFuncUtil.Create(del);
                };
            var fsharpFunc = FSharpFuncUtil.Create(outerFunc);
            var list = name.Select(InputUtil.CharToKeyInput).ToFSharpList();
            var commandName = CommandName.NewManyKeyInputs(list);
            return Command.NewRepeatableCommand(commandName, fsharpFunc);
        }

        private Command CreateMotionCommand(string name, Func<FSharpOption<int>, Register, MotionData, CommandResult> func)
        {
            Converter<FSharpOption<int>, FSharpFunc<Register, FSharpFunc<MotionData, CommandResult>>> func1 = count =>
                {
                    Converter<Register, FSharpFunc<MotionData, CommandResult>> func2 = register =>
                    {
                        Converter<MotionData, CommandResult> func3 = data => func(count, register, data);
                        return FSharpFuncUtil.Create(func3);
                    };

                    return FSharpFuncUtil.Create(func2);
                };
            var fsharpFunc = FSharpFuncUtil.Create(func1);
            var list = name.Select(InputUtil.CharToKeyInput).ToFSharpList();
            var commandName = CommandName.NewManyKeyInputs(list);
            return Command.NewMotionCommand(commandName, fsharpFunc);
        }

        private RunKeyInputResult Run(string command)
        {
            RunKeyInputResult last = null;
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
            var command1 = CreateRepeatableCommand("foo", (x, y) => CommandResult.NewCompleted(ModeSwitch.NoSwitch));
            _runner.Add(command1);
            Assert.AreSame(command1, _runner.Commands.Single());
        }

        [Test]
        public void Add2()
        {
            Create(String.Empty);
            var command1 = CreateRepeatableCommand("foo", (x, y) => CommandResult.NewCompleted(ModeSwitch.NoSwitch));
            var command2 = CreateRepeatableCommand("bar", (x, y) => CommandResult.NewCompleted(ModeSwitch.NoSwitch));
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
            _runner.Add(CreateRepeatableCommand("a", (count, reg) => { count1++; return CommandResult.NewCompleted(ModeSwitch.NoSwitch); }));
            Run("a");
            Assert.AreEqual(1, count1);
        }

        [Test]
        public void Run_CommandMatch2()
        {
            Create(String.Empty);
            var count1 = 0;
            _runner.Add(CreateRepeatableCommand("a", (count, reg) => { count1++; return CommandResult.NewCompleted(ModeSwitch.NoSwitch); }));
            Run("b");
            Assert.AreEqual(0, count1);
        }

        [Test]
        public void Run_CommandMatch3()
        {
            Create(String.Empty);
            var count1 = 0;
            _runner.Add(CreateRepeatableCommand("a", (count, reg) => { count1++; return CommandResult.NewCompleted(ModeSwitch.NoSwitch); }));
            var count2 = 0;
            _runner.Add(CreateRepeatableCommand("b", (count, reg) => { count2++; return CommandResult.NewCompleted(ModeSwitch.NoSwitch); }));
            Run("b");
            Assert.AreEqual(0, count1);
            Assert.AreEqual(1, count2);
        }

        [Test]
        public void Run_CommandMatch4()
        {
            Create(String.Empty);
            var count1 = 0;
            _runner.Add(CreateRepeatableCommand("ab", (count, reg) => { count1++; return CommandResult.NewCompleted(ModeSwitch.NoSwitch); }));
            var count2 = 0;
            _runner.Add(CreateRepeatableCommand("b", (count, reg) => { count2++; return CommandResult.NewCompleted(ModeSwitch.NoSwitch); }));
            Run("ab");
            Assert.AreEqual(1, count1);
            Assert.AreEqual(0, count2);
        }

        [Test]
        [Description("Prefix is ambiguous and neither should match")]
        public void Run_CommandMatch5()
        {
            Create(String.Empty);
            var count1 = 0;
            _runner.Add(CreateMotionCommand("aa", (count, reg, data) => { count1++; return CommandResult.NewCompleted(ModeSwitch.NoSwitch); }));
            var count2 = 0;
            _runner.Add(CreateRepeatableCommand("aab", (count, reg) => { count2++; return CommandResult.NewCompleted(ModeSwitch.NoSwitch); }));
            Run("aa");
            Assert.AreEqual(0, count1);
            Assert.AreEqual(0, count2);
        }

        [Test]
        public void Run_CommandMatch6()
        {
            Create(String.Empty);
            var count1 = 0;
            _runner.Add(CreateMotionCommand("aa", (count, reg, data) => { count1++; return CommandResult.NewCompleted(ModeSwitch.NoSwitch); }));
            var count2 = 0;
            _runner.Add(CreateRepeatableCommand("aab", (count, reg) => { count2++; return CommandResult.NewCompleted(ModeSwitch.NoSwitch); }));
            Run("aab");
            Assert.AreEqual(0, count1);
            Assert.AreEqual(1, count2);
        }

        [Test]
        public void Run_CommandMatch7()
        {
            Create("foo bar");
            var count1 = 0;
            _runner.Add(CreateMotionCommand("aa", (count, reg, data) => { count1++; return CommandResult.NewCompleted(ModeSwitch.NoSwitch); }));
            var count2 = 0;
            _runner.Add(CreateRepeatableCommand("aab", (count, reg) => { count2++; return CommandResult.NewCompleted(ModeSwitch.NoSwitch); }));
            Run("aaw");
            Assert.AreEqual(1, count1);
            Assert.AreEqual(0, count2);
        }

        [Test]
        public void Run_Count1()
        {
            Create(String.Empty);
            var didRun = false;
            _runner.Add(CreateRepeatableCommand("a", (count, reg) =>
                {
                    Assert.IsTrue(count.IsNone());
                    didRun = true;
                    return CommandResult.NewCompleted(ModeSwitch.NoSwitch);
                }));
            Run("a");
            Assert.IsTrue(didRun);
        }

        [Test]
        public void Run_Count2()
        {
            Create(String.Empty);
            var didRun = false;
            _runner.Add(CreateRepeatableCommand("a", (count, reg) =>
                {
                    Assert.IsTrue(count.IsSome());
                    Assert.AreEqual(1, count.Value);
                    didRun = true;
                    return CommandResult.NewCompleted(ModeSwitch.NoSwitch);
                }));
            Run("1a");
            Assert.IsTrue(didRun);
        }

        [Test]
        public void Run_Count3()
        {
            Create(String.Empty);
            var didRun = false;
            _runner.Add(CreateRepeatableCommand("a", (count, reg) =>
                {
                    Assert.IsTrue(count.IsSome());
                    Assert.AreEqual(42, count.Value);
                    didRun = true;
                    return CommandResult.NewCompleted(ModeSwitch.NoSwitch);
                }));
            Run("42a");
            Assert.IsTrue(didRun);
        }

        [Test]
        public void Run_Register1()
        {
            Create(String.Empty);
            var didRun = false;
            _runner.Add(CreateRepeatableCommand("a", (count, reg) =>
                {
                    Assert.AreSame(_registerMap.DefaultRegister, reg);
                    didRun = true;
                    return CommandResult.NewCompleted(ModeSwitch.NoSwitch);
                }));
            Run("a");
            Assert.IsTrue(didRun);
        }

        [Test]
        public void Run_Register2()
        {
            Create(String.Empty);
            var didRun = false;
            _runner.Add(CreateRepeatableCommand("a", (count, reg) =>
                {
                    Assert.AreSame(_registerMap.GetRegister('c'), reg);
                    didRun = true;
                    return CommandResult.NewCompleted(ModeSwitch.NoSwitch);
                }));
            Run("\"ca");
            Assert.IsTrue(didRun);
        }

        [Test]
        public void Run_Register3()
        {
            Create(String.Empty);
            var didRun = false;
            _runner.Add(CreateRepeatableCommand("a", (count, reg) =>
                {
                    Assert.AreSame(_registerMap.GetRegister('d'), reg);
                    didRun = true;
                    return CommandResult.NewCompleted(ModeSwitch.NoSwitch);
                }));
            Run("\"da");
            Assert.IsTrue(didRun);
        }

        [Test]
        public void Run_CountAndRegister1()
        {
            Create(String.Empty);
            var didRun = false;
            _runner.Add(CreateRepeatableCommand("a", (count, reg) =>
                {
                    Assert.IsTrue(count.IsSome());
                    Assert.AreEqual(2, count.Value);
                    Assert.AreSame(_registerMap.GetRegister('d'), reg);
                    didRun = true;
                    return CommandResult.NewCompleted(ModeSwitch.NoSwitch);
                }));
            Run("\"d2a");
            Assert.IsTrue(didRun);
        }

        [Test]
        public void Run_CountAndRegister2()
        {
            Create(String.Empty);
            var didRun = false;
            _runner.Add(CreateRepeatableCommand("a", (count, reg) =>
                {
                    Assert.IsTrue(count.IsSome());
                    Assert.AreEqual(2, count.Value);
                    Assert.AreSame(_registerMap.GetRegister('d'), reg);
                    didRun = true;
                    return CommandResult.NewCompleted(ModeSwitch.NoSwitch);
                }));
            Run("2\"da");
            Assert.IsTrue(didRun);
        }

        [Test]
        public void IsWaitingForMoreInput1()
        {
            Create("hello world");
            _runner.Add(CreateRepeatableCommand("cat", (count, reg) => CommandResult.NewCompleted(ModeSwitch.NoSwitch)));
            Assert.IsTrue(Run("c").IsNeedMoreKeyInput);
            Assert.IsTrue(_runner.IsWaitingForMoreInput);
        }

        [Test]
        public void IsWaitingForMoreInput2()
        {
            Create("hello world");
            _runner.Add(CreateRepeatableCommand("cat", (count, reg) => CommandResult.NewCompleted(ModeSwitch.NoSwitch)));
            Assert.IsTrue(Run("ca").IsNeedMoreKeyInput);
            Assert.IsTrue(_runner.IsWaitingForMoreInput);
        }

        [Test]
        public void IsWaitingForMoreInput3()
        {
            Create("hello world");
            _runner.Add(CreateRepeatableCommand("cat", (count, reg) => CommandResult.NewCompleted(ModeSwitch.NoSwitch)));
            Assert.IsTrue(Run("cat").IsCommandRan);
            Assert.IsFalse(_runner.IsWaitingForMoreInput);
        }

        [Test]
        public void IsWaitingForMoreInput4()
        {
            Create("hello world");
            _runner.Add(CreateRepeatableCommand("cat", (count, reg) => CommandResult.NewCompleted(ModeSwitch.NoSwitch)));
            Assert.IsTrue(Run("ca").IsNeedMoreKeyInput);
            Assert.IsTrue(_runner.Run(InputUtil.VimKeyToKeyInput(VimKey.EscapeKey)).IsCommandCancelled);
            Assert.IsFalse(_runner.IsWaitingForMoreInput);
        }

        [Test]
        [Description("Cancel in a motion")]
        public void IsWaitingForMoreInput5()
        {
            Create("hello world");
            _runner.Add(CreateMotionCommand("cat", (count, reg, data) => CommandResult.NewCompleted(ModeSwitch.NoSwitch)));
            Assert.IsTrue(Run("cata").IsNeedMoreKeyInput);
            Assert.IsTrue(_runner.Run(InputUtil.VimKeyToKeyInput(VimKey.EscapeKey)).IsCommandCancelled);
            Assert.IsFalse(_runner.IsWaitingForMoreInput);
        }

        [Test]
        public void Run_Motion1()
        {
            Create("foo bar");
            var didRun = false;
            _runner.Add(CreateMotionCommand("a", (count, reg, data) =>
                {
                    Assert.AreEqual(new SnapshotSpan(_textView.GetLine(0).Start, 4), data.Span);
                    didRun = true;
                    return CommandResult.NewCompleted(ModeSwitch.NoSwitch);
                }));
            Run("aw");
            Assert.IsTrue(didRun);
        }

        [Test]
        public void Run_Motion2()
        {
            Create("foo bar");
            var didRun = false;
            _runner.Add(CreateMotionCommand("a", (count, reg, data) =>
                {
                    Assert.AreEqual(new SnapshotSpan(_textView.GetLine(0).Start, 4), data.Span);
                    didRun = true;
                    return CommandResult.NewCompleted(ModeSwitch.NoSwitch);
                }));
            Run("aaw");
            Assert.IsTrue(didRun);
        }

        [Test]
        public void Reset1()
        {
            Create("hello world");
            _runner.Add(CreateRepeatableCommand("abc", (x, y) => CommandResult.NewCompleted(ModeSwitch.NoSwitch)));
            Run("a");
            Assert.IsTrue(_runner.IsWaitingForMoreInput);
            _runner.Reset();
            Assert.IsFalse(_runner.IsWaitingForMoreInput);
        }

        //[Test]
        //public void CommandWithAdditionalInput1()
        //{
        //    Create("hello world");
        //    var isDone = false;
        //    var seen = String.Empty;
        //    FSharpFunc<KeyInput,CommandResult> repeat = null;
        //    Converter<KeyInput,CommandResult> func = ki =>
        //    {
        //        seen = seen + ki.Char.ToString();
        //        return isDone ? CommandResult.Completed : CommandResult.NewNeedMoreKeyInput(repeat);
        //    };
        //    repeat = FSharpFunc<KeyInput,CommandResult>.FromConverter(func);
        //    _runner.Add(CreateRepeatableCommand("f", (x, y) => CommandResult.NewNeedMoreKeyInput(repeat)));

        //    Run("food");
        //    Assert.AreEqual("ood", seen);
        //}

        //[Test]
        //public void CommandWithAdditionalInput2()
        //{
        //    Create("hello world");
        //    var isDone = false;
        //    var seen = String.Empty;
        //    FSharpFunc<KeyInput,CommandResult> repeat = null;
        //    Converter<KeyInput,CommandResult> func = ki =>
        //    {
        //        seen = seen + ki.Char.ToString();
        //        return isDone ? CommandResult.Completed : CommandResult.NewNeedMoreKeyInput(repeat);
        //    };
        //    repeat = FSharpFunc<KeyInput,CommandResult>.FromConverter(func);
        //    _runner.Add(CreateRepeatableCommand("f", (x, y) => CommandResult.NewNeedMoreKeyInput(repeat)));

        //    Run("food");
        //    isDone = true;
        //    Run("bar");
        //    Assert.AreEqual("oodb", seen);
        //}

        [Test]
        public void NestedRun1()
        {
            Create("hello world");
            _runner.Add(CreateRepeatableCommand("a", (x,y) =>
                {
                    var res = _runner.Run(InputUtil.CharToKeyInput('a'));
                    Assert.IsTrue(res.IsNestedRunDetected);
                    return CommandResult.NewCompleted(ModeSwitch.NoSwitch);
                }));

            var res2 = _runner.Run(InputUtil.CharToKeyInput('a'));
            Assert.IsTrue(res2.IsCommandRan);
        }

        [Test]
        public void NestedRun2()
        {
            Create("hello world");
            _runner.Add(CreateRepeatableCommand("a", (x,y) =>
                {
                    var res = _runner.Run(InputUtil.CharToKeyInput('a'));
                    Assert.IsTrue(res.IsNestedRunDetected);
                    return CommandResult.NewCompleted(ModeSwitch.NoSwitch);
                }));

            var res2 = _runner.Run(InputUtil.CharToKeyInput('a'));
            Assert.IsTrue(res2.IsCommandRan);
        }

    }
}
