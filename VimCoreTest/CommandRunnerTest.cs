using System;
using System.Linq;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using VimCore.Test.Utils;

namespace VimCore.Test
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
            var capture = new MotionCapture(_textView, new MotionUtil(_textView, new Vim.GlobalSettings()));
            _runnerRaw = new CommandRunner(
                _textView,
                _registerMap,
                (IMotionCapture)capture,
                _statusUtil.Object);
            _runner = _runnerRaw;
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
            var command1 = VimUtil.CreateSimpleCommand("foo", (x, y) => CommandResult.NewCompleted(ModeSwitch.NoSwitch));
            _runner.Add(command1);
            Assert.AreSame(command1, _runner.Commands.Single());
        }

        [Test]
        public void Add2()
        {
            Create(String.Empty);
            var command1 = VimUtil.CreateSimpleCommand("foo", (x, y) => CommandResult.NewCompleted(ModeSwitch.NoSwitch));
            var command2 = VimUtil.CreateSimpleCommand("bar", (x, y) => CommandResult.NewCompleted(ModeSwitch.NoSwitch));
            _runner.Add(command1);
            _runner.Add(command2);
            Assert.AreEqual(2, _runner.Commands.Count());
            Assert.IsTrue(_runner.Commands.Contains(command1));
            Assert.IsTrue(_runner.Commands.Contains(command2));
        }

        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void Add3()
        {
            Create(String.Empty);
            var command1 = VimUtil.CreateSimpleCommand("foo", (x, y) => CommandResult.NewCompleted(ModeSwitch.NoSwitch));
            _runner.Add(command1);
            _runner.Add(command1);
        }

        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void Add4()
        {
            Create(String.Empty);
            var command1 = VimUtil.CreateSimpleCommand("foo", (x, y) => CommandResult.NewCompleted(ModeSwitch.NoSwitch));
            var command2 = VimUtil.CreateMotionCommand("foo", (x, y, z) => CommandResult.NewCompleted(ModeSwitch.NoSwitch));
            _runner.Add(command1);
            _runner.Add(command2);
        }

        [Test]
        public void Remove1()
        {
            Create(String.Empty);
            var command1 = VimUtil.CreateSimpleCommand("foo", (x, y) => CommandResult.NewCompleted(ModeSwitch.NoSwitch));
            var command2 = VimUtil.CreateSimpleCommand("bar", (x, y) => CommandResult.NewCompleted(ModeSwitch.NoSwitch));
            _runner.Add(command1);
            _runner.Remove(command1.KeyInputSet);
            Assert.AreEqual(0, _runner.Commands.Count());
        }

        [Test]
        [Description("Don't throw when removing a command that's not present")]
        public void Remove2()
        {
            Create(String.Empty);
            _runner.Remove(KeyNotationUtil.StringToKeyInputSet("foo"));
            Assert.AreEqual(0, _runner.Commands.Count());
        }

        [Test]
        public void Run_CommandMatch1()
        {
            Create(String.Empty);
            var count1 = 0;
            _runner.Add(VimUtil.CreateSimpleCommand("a", (count, reg) => { count1++; return CommandResult.NewCompleted(ModeSwitch.NoSwitch); }));
            Run("a");
            Assert.AreEqual(1, count1);
        }

        [Test]
        public void Run_CommandMatch2()
        {
            Create(String.Empty);
            var count1 = 0;
            _runner.Add(VimUtil.CreateSimpleCommand("a", (count, reg) => { count1++; return CommandResult.NewCompleted(ModeSwitch.NoSwitch); }));
            Run("b");
            Assert.AreEqual(0, count1);
        }

        [Test]
        public void Run_CommandMatch3()
        {
            Create(String.Empty);
            var count1 = 0;
            _runner.Add(VimUtil.CreateSimpleCommand("a", (count, reg) => { count1++; return CommandResult.NewCompleted(ModeSwitch.NoSwitch); }));
            var count2 = 0;
            _runner.Add(VimUtil.CreateSimpleCommand("b", (count, reg) => { count2++; return CommandResult.NewCompleted(ModeSwitch.NoSwitch); }));
            Run("b");
            Assert.AreEqual(0, count1);
            Assert.AreEqual(1, count2);
        }

        [Test]
        public void Run_CommandMatch4()
        {
            Create(String.Empty);
            var count1 = 0;
            _runner.Add(VimUtil.CreateSimpleCommand("ab", (count, reg) => { count1++; return CommandResult.NewCompleted(ModeSwitch.NoSwitch); }));
            var count2 = 0;
            _runner.Add(VimUtil.CreateSimpleCommand("b", (count, reg) => { count2++; return CommandResult.NewCompleted(ModeSwitch.NoSwitch); }));
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
            _runner.Add(VimUtil.CreateMotionCommand("aa", (count, reg, data) => { count1++; return CommandResult.NewCompleted(ModeSwitch.NoSwitch); }));
            var count2 = 0;
            _runner.Add(VimUtil.CreateSimpleCommand("aab", (count, reg) => { count2++; return CommandResult.NewCompleted(ModeSwitch.NoSwitch); }));
            Run("aa");
            Assert.AreEqual(0, count1);
            Assert.AreEqual(0, count2);
        }

        [Test]
        public void Run_CommandMatch6()
        {
            Create(String.Empty);
            var count1 = 0;
            _runner.Add(VimUtil.CreateMotionCommand("aa", (count, reg, data) => { count1++; return CommandResult.NewCompleted(ModeSwitch.NoSwitch); }));
            var count2 = 0;
            _runner.Add(VimUtil.CreateSimpleCommand("aab", (count, reg) => { count2++; return CommandResult.NewCompleted(ModeSwitch.NoSwitch); }));
            Run("aab");
            Assert.AreEqual(0, count1);
            Assert.AreEqual(1, count2);
        }

        [Test]
        public void Run_CommandMatch7()
        {
            Create("foo bar");
            var count1 = 0;
            _runner.Add(VimUtil.CreateMotionCommand("aa", (count, reg, data) => { count1++; return CommandResult.NewCompleted(ModeSwitch.NoSwitch); }));
            var count2 = 0;
            _runner.Add(VimUtil.CreateSimpleCommand("aab", (count, reg) => { count2++; return CommandResult.NewCompleted(ModeSwitch.NoSwitch); }));
            Run("aaw");
            Assert.AreEqual(1, count1);
            Assert.AreEqual(0, count2);
        }

        [Test]
        public void Run_Count1()
        {
            Create(String.Empty);
            var didRun = false;
            _runner.Add(VimUtil.CreateSimpleCommand("a", (count, reg) =>
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
            _runner.Add(VimUtil.CreateSimpleCommand("a", (count, reg) =>
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
            _runner.Add(VimUtil.CreateSimpleCommand("a", (count, reg) =>
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
        [Description("0 is not a valid count")]
        public void Run_Count4()
        {
            Create(string.Empty);
            var didRun = false;
            _runner.Add(VimUtil.CreateSimpleCommand("a", (count, reg) => { didRun = true; }));
            Assert.IsTrue(_runner.Run('0').IsNoMatchingCommand);
            Assert.IsFalse(didRun);
        }

        [Test]
        public void Run_Register1()
        {
            Create(String.Empty);
            var didRun = false;
            _runner.Add(VimUtil.CreateSimpleCommand("a", (count, reg) =>
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
            _runner.Add(VimUtil.CreateSimpleCommand("a", (count, reg) =>
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
            _runner.Add(VimUtil.CreateSimpleCommand("a", (count, reg) =>
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
            _runner.Add(VimUtil.CreateSimpleCommand("a", (count, reg) =>
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
            _runner.Add(VimUtil.CreateSimpleCommand("a", (count, reg) =>
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
        public void Run_NoMatchingCommand1()
        {
            Create("hello world");
            _runner.Add(VimUtil.CreateSimpleCommand("cat", (count, reg) => CommandResult.NewCompleted(ModeSwitch.NoSwitch)));
            Assert.IsTrue(_runner.Run('b').IsNoMatchingCommand);
        }

        [Test]
        public void Run_NoMatchingCommand2()
        {
            Create("hello world");
            _runner.Add(VimUtil.CreateSimpleCommand("cat", (count, reg) => CommandResult.NewCompleted(ModeSwitch.NoSwitch)));
            Assert.IsTrue(_runner.Run('c').IsNeedMoreKeyInput);
            Assert.IsTrue(_runner.Run('b').IsNoMatchingCommand);
        }

        [Test]
        public void Run_NoMatchingCommand3()
        {
            Create("hello world");
            _runner.Add(VimUtil.CreateSimpleCommand("cot", (count, reg) => CommandResult.NewCompleted(ModeSwitch.NoSwitch)));
            _runner.Add(VimUtil.CreateSimpleCommand("cook", (count, reg) => CommandResult.NewCompleted(ModeSwitch.NoSwitch)));
            Assert.IsTrue(_runner.Run('c').IsNeedMoreKeyInput);
            Assert.IsTrue(_runner.Run('o').IsNeedMoreKeyInput);
            Assert.IsTrue(_runner.Run('b').IsNoMatchingCommand);
        }

        [Test]
        public void Run_Escape1()
        {
            Create("hello world");
            var didSee = false;
            _runner.Add(VimUtil.CreateLongCommand(
                "c",
                ki =>
                {
                    if (ki.Key == VimKey.Escape) { didSee = true; return true; }
                    else { return false; }
                },
                CommandFlags.None));
            _runner.Run('c');
            Assert.IsTrue(_runner.Run(InputUtil.VimKeyToKeyInput(VimKey.Escape)).IsCommandCancelled);
            Assert.IsFalse(didSee);
        }

        [Test]
        public void Run_Escape2()
        {
            Create("hello world");
            var didSee = false;
            _runner.Add(VimUtil.CreateLongCommand(
                "c",
                ki =>
                {
                    if (ki.Key == VimKey.Escape) { didSee = true; }
                    return false;
                },
                CommandFlags.HandlesEscape));
            _runner.Run('c');
            Assert.IsTrue(_runner.Run(InputUtil.VimKeyToKeyInput(VimKey.Escape)).IsNeedMoreKeyInput);
            Assert.IsTrue(didSee);
        }

        [Test]
        public void Run_Escape3()
        {
            Create("hello world");
            var didSee = false;
            _runner.Add(VimUtil.CreateLongCommand(
                "c",
                ki =>
                {
                    if (ki.Key == VimKey.Escape) { didSee = true; return true; }
                    return false;
                },
                CommandFlags.HandlesEscape));
            _runner.Run('c');
            Assert.IsTrue(_runner.Run(InputUtil.VimKeyToKeyInput(VimKey.Escape)).IsCommandRan);
            Assert.IsTrue(didSee);
        }

        [Test]
        public void IsWaitingForMoreInput1()
        {
            Create("hello world");
            _runner.Add(VimUtil.CreateSimpleCommand("cat", (count, reg) => CommandResult.NewCompleted(ModeSwitch.NoSwitch)));
            Assert.IsTrue(Run("c").IsNeedMoreKeyInput);
            Assert.IsTrue(_runner.IsWaitingForMoreInput);
        }

        [Test]
        public void IsWaitingForMoreInput2()
        {
            Create("hello world");
            _runner.Add(VimUtil.CreateSimpleCommand("cat", (count, reg) => CommandResult.NewCompleted(ModeSwitch.NoSwitch)));
            Assert.IsTrue(Run("ca").IsNeedMoreKeyInput);
            Assert.IsTrue(_runner.IsWaitingForMoreInput);
        }

        [Test]
        public void IsWaitingForMoreInput3()
        {
            Create("hello world");
            _runner.Add(VimUtil.CreateSimpleCommand("cat", (count, reg) => CommandResult.NewCompleted(ModeSwitch.NoSwitch)));
            Assert.IsTrue(Run("cat").IsCommandRan);
            Assert.IsFalse(_runner.IsWaitingForMoreInput);
        }

        [Test]
        public void IsWaitingForMoreInput4()
        {
            Create("hello world");
            _runner.Add(VimUtil.CreateSimpleCommand("cat", (count, reg) => CommandResult.NewCompleted(ModeSwitch.NoSwitch)));
            Assert.IsTrue(Run("ca").IsNeedMoreKeyInput);
            Assert.IsTrue(_runner.Run(InputUtil.VimKeyToKeyInput(VimKey.Escape)).IsCommandCancelled);
            Assert.IsFalse(_runner.IsWaitingForMoreInput);
        }

        [Test]
        [Description("Cancel in a motion")]
        public void IsWaitingForMoreInput5()
        {
            Create("hello world");
            _runner.Add(VimUtil.CreateMotionCommand("cat", (count, reg, data) => CommandResult.NewCompleted(ModeSwitch.NoSwitch)));
            Assert.IsTrue(Run("cata").IsNeedMoreKeyInput);
            Assert.IsTrue(_runner.Run(InputUtil.VimKeyToKeyInput(VimKey.Escape)).IsCommandCancelled);
            Assert.IsFalse(_runner.IsWaitingForMoreInput);
        }

        [Test]
        public void Run_Motion1()
        {
            Create("foo bar");
            var didRun = false;
            _runner.Add(VimUtil.CreateMotionCommand("a", (count, reg, data) =>
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
            _runner.Add(VimUtil.CreateMotionCommand("a", (count, reg, data) =>
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
            _runner.Add(VimUtil.CreateSimpleCommand("abc", (x, y) => CommandResult.NewCompleted(ModeSwitch.NoSwitch)));
            Run("a");
            Assert.IsTrue(_runner.IsWaitingForMoreInput);
            _runner.ResetState();
            Assert.IsFalse(_runner.IsWaitingForMoreInput);
        }

        [Test]
        public void LongCommand1()
        {
            Create("hello world");
            var isDone = false;
            var seen = string.Empty;
            FSharpFunc<KeyInput, LongCommandResult> repeat = null;
            Converter<KeyInput, LongCommandResult> func = ki =>
            {
                seen += ki.Char.ToString();
                return isDone
                    ? LongCommandResult.NewFinished(CommandResult.NewError("foo"))
                    : LongCommandResult.NewNeedMoreInput(repeat);
            };
            repeat = FSharpFunc<KeyInput, LongCommandResult>.FromConverter(func);
            _runner.Add(VimUtil.CreateLongCommand("f", (x, y) => LongCommandResult.NewNeedMoreInput(repeat)));
            Assert.IsTrue(_runner.Run(InputUtil.CharToKeyInput('f')).IsNeedMoreKeyInput);
            Assert.IsTrue(_runner.Run(InputUtil.CharToKeyInput('o')).IsNeedMoreKeyInput);
            Assert.IsTrue(_runner.Run(InputUtil.CharToKeyInput('d')).IsNeedMoreKeyInput);
            isDone = true;
            Assert.IsTrue(_runner.Run(InputUtil.CharToKeyInput('d')).IsCommandErrored);
            Assert.AreEqual("odd", seen);
        }

        [Test]
        public void LongCommand2()
        {
            Create("hello world");
            var isDone = false;
            var seen = string.Empty;
            FSharpFunc<KeyInput, LongCommandResult> repeat = null;
            Converter<KeyInput, LongCommandResult> func = ki =>
            {
                seen += ki.Char.ToString();
                return isDone
                    ? LongCommandResult.Cancelled
                    : LongCommandResult.NewNeedMoreInput(repeat);
            };
            repeat = FSharpFunc<KeyInput, LongCommandResult>.FromConverter(func);
            _runner.Add(VimUtil.CreateLongCommand("f", (x, y) => LongCommandResult.NewNeedMoreInput(repeat)));
            Assert.IsTrue(_runner.Run(InputUtil.CharToKeyInput('f')).IsNeedMoreKeyInput);
            Assert.IsTrue(_runner.Run(InputUtil.CharToKeyInput('o')).IsNeedMoreKeyInput);
            Assert.IsTrue(_runner.Run(InputUtil.CharToKeyInput('d')).IsNeedMoreKeyInput);
            isDone = true;
            Assert.IsTrue(_runner.Run(InputUtil.CharToKeyInput('d')).IsCommandCancelled);
            Assert.AreEqual("odd", seen);
        }

        [Test]
        public void LongCommand3()
        {
            Create("hello world");
            var seen = string.Empty;
            FSharpFunc<KeyInput, LongCommandResult> repeat = null;
            Converter<KeyInput, LongCommandResult> func = ki =>
            {
                seen += ki.Char.ToString();
                return LongCommandResult.NewNeedMoreInput(repeat);
            };
            repeat = FSharpFunc<KeyInput, LongCommandResult>.FromConverter(func);
            _runner.Add(VimUtil.CreateLongCommand("f", (x, y) => LongCommandResult.NewNeedMoreInput(repeat)));
            Assert.IsTrue(_runner.Run(InputUtil.CharToKeyInput('f')).IsNeedMoreKeyInput);
            Assert.IsTrue(_runner.Run(InputUtil.CharToKeyInput('o')).IsNeedMoreKeyInput);
            Assert.IsTrue(_runner.Run(InputUtil.CharToKeyInput('d')).IsNeedMoreKeyInput);
            Assert.IsTrue(_runner.Run(InputUtil.VimKeyToKeyInput(VimKey.Escape)).IsCommandCancelled);
        }

        [Test]
        public void NestedRun1()
        {
            Create("hello world");
            _runner.Add(VimUtil.CreateSimpleCommand("a", (x,y) =>
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
            _runner.Add(VimUtil.CreateSimpleCommand("a", (x,y) =>
                {
                    var res = _runner.Run(InputUtil.CharToKeyInput('a'));
                    Assert.IsTrue(res.IsNestedRunDetected);
                    return CommandResult.NewCompleted(ModeSwitch.NoSwitch);
                }));

            var res2 = _runner.Run(InputUtil.CharToKeyInput('a'));
            Assert.IsTrue(res2.IsCommandRan);
        }

        [Test]
        public void State1()
        {
            Create("hello world");
            Assert.IsTrue(_runner.State.IsNoInput);
        }

        [Test]
        public void State2()
        {
            Create("hello world");
            Assert.IsTrue(_runner.Run('c').IsNoMatchingCommand);
            Assert.IsTrue(_runner.State.IsNoInput);
        }

        [Test]
        public void State3()
        {
            Create("hello world");
            _runner.Add(VimUtil.CreateSimpleCommand("cat", (count, reg) => CommandResult.NewCompleted(ModeSwitch.NoSwitch)));
            _runner.Run('c');
            Assert.IsTrue(_runner.State.IsNotEnoughInput);
        }

        [Test]
        public void State4()
        {
            Create("hello world");
            _runner.Add(VimUtil.CreateSimpleCommand("cat", (count, reg) => CommandResult.NewCompleted(ModeSwitch.NoSwitch)));
            _runner.Run('c');
            _runner.Run(InputUtil.VimKeyToKeyInput(VimKey.Escape));
            Assert.IsTrue(_runner.State.IsNoInput);
        }

        [Test]
        public void State5()
        {
            Create("hello world");
            var command1 = VimUtil.CreateMotionCommand("c", (x, y, z) => CommandResult.NewCompleted(ModeSwitch.NoSwitch));
            _runner.Add(command1);
            _runner.Run('c');
            Assert.IsTrue(_runner.State.IsNotFinishWithCommand);
            Assert.AreSame(command1, _runner.State.AsNotFinishedWithCommand().Item);
        }

        [Test]
        public void State6()
        {
            Create("hello world");
            var command1 = VimUtil.CreateMotionCommand("c", (x, y, z) => CommandResult.NewCompleted(ModeSwitch.NoSwitch));
            _runner.Add(command1);
            _runner.Run('c');
            _runner.Run('w');
            Assert.IsTrue(_runner.State.IsNoInput);
        }

        [Test]
        public void State7()
        {
            Create("hello world");
            var seen = string.Empty;
            FSharpFunc<KeyInput, LongCommandResult> repeat = null;
            Converter<KeyInput, LongCommandResult> func = ki =>
            {
                seen += ki.Char.ToString();
                return LongCommandResult.NewNeedMoreInput(repeat);
            };
            repeat = FSharpFunc<KeyInput, LongCommandResult>.FromConverter(func);
            var command1 = VimUtil.CreateLongCommand("f", (x, y) => LongCommandResult.NewNeedMoreInput(repeat));
            _runner.Add(command1);
            _runner.Run('f');
            Assert.IsTrue(_runner.State.IsNotFinishWithCommand);
            Assert.AreSame(command1, _runner.State.AsNotFinishedWithCommand().Item);
        }

        [Test]
        public void State8()
        {
            Create("hello world");
            var command1 = VimUtil.CreateSimpleCommand("cc", (x,y) => {});
            var command2 = VimUtil.CreateMotionCommand("c", (x, y, z) => { });
            _runner.Add(command1);
            _runner.Add(command2);
            _runner.Run('c');
            Assert.IsTrue(_runner.State.IsNotEnoughMatchingPrefix);
            Assert.AreSame(command2, _runner.State.AsNotEnoughMatchingPrefix().Item1);
            Assert.IsTrue(_runner.State.AsNotEnoughMatchingPrefix().Item2.Contains(command1));
        }

        [Test]
        public void CommandRan1()
        {
            Create("hello world");
            var didSee = false;
            var command1 = VimUtil.CreateSimpleCommand("c", (x, y) => CommandResult.NewCompleted(ModeSwitch.NoSwitch));
            _runner.Add(command1);
            _runner.CommandRan += (notUsed,tuple) =>
                {
                    Assert.AreSame(command1, tuple.Item1.Command);
                    Assert.IsTrue(tuple.Item2.IsCompleted);
                    didSee = true;
                };
            _runner.Run('c');
            Assert.IsTrue(didSee);
        }

        [Test]
        public void CommandRan2()
        {
            Create("hello world");
            var didSee = false;
            var command1 = VimUtil.CreateSimpleCommand("c", (x, y) => CommandResult.NewCompleted(ModeSwitch.NoSwitch));
            _runner.Add(command1);
            _runner.CommandRan += (notUsed,tuple) =>
                {
                    Assert.AreSame(command1, tuple.Item1.Command);
                    Assert.AreEqual(2, tuple.Item1.Count.Value);
                    Assert.IsTrue(tuple.Item2.IsCompleted);
                    didSee = true;
                };
            _runner.Run('2');
            _runner.Run('c');
            Assert.IsTrue(didSee);
        }

        [Test]
        public void CommandRan3()
        {
            Create("hello world");
            var didSee = false;
            var command1 = VimUtil.CreateSimpleCommand("cat", (x, y) => CommandResult.NewCompleted(ModeSwitch.NoSwitch));
            _runner.Add(command1);
            _runner.CommandRan += (notUsed,tuple) =>
                {
                    didSee = true;
                };
            _runner.Run('c');
            _runner.Run(InputUtil.VimKeyToKeyInput(VimKey.Escape));
            Assert.IsFalse(didSee);
        }

    }
}
