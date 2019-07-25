using System;
using System.Linq;
using Microsoft.VisualStudio.Text.Editor;
using Xunit;
using Vim.Extensions;

namespace Vim.UnitTest
{
    public abstract class CommandRunnerTest : VimTestBase
    {
        private ICommandUtil _commandUtil;
        private IRegisterMap _registerMap;
        private IVimTextBuffer _vimTextBuffer;
        private ITextView _textView;
        private CommandRunner _runnerRaw;
        private ICommandRunner _runner;

        private void Create(params string[] lines)
        {
            Create(KeyRemapMode.Normal, lines);
        }

        private void Create(KeyRemapMode countKeyRemapMode, params string[] lines)
        {
            _textView = CreateTextView(lines);
            _vimTextBuffer = Vim.CreateVimTextBuffer(_textView.TextBuffer);
            _registerMap = Vim.RegisterMap;
            var vimBufferData = CreateVimBufferData(
                _vimTextBuffer,
                _textView);
            _commandUtil = CreateCommandUtil(vimBufferData);
            var commonOperations = CommonOperationsFactory.GetCommonOperations(vimBufferData);
            var motionUtil = new MotionUtil(vimBufferData, commonOperations);
            var incrementalSearch = new IncrementalSearch(vimBufferData, commonOperations, motionUtil);
            var motionCapture = new MotionCapture(vimBufferData, incrementalSearch);

            _runnerRaw = new CommandRunner(
                vimBufferData,
                motionCapture,
                _commandUtil,
                VisualKind.Character,
                countKeyRemapMode);
            _runner = _runnerRaw;
        }

        private BindResult<CommandRunData> Run(string command)
        {
            BindResult<CommandRunData> last = null;
            foreach (var c in command)
            {
                last = _runner.Run(KeyInputUtil.CharToKeyInput(c));
            }
            return last;
        }

        public sealed class KeyRemapModeTest : CommandRunnerTest
        {
            [WpfFact]
            public void DefaultIsNone()
            {
                Create("hello world");
                Assert.Equal(_runner.KeyRemapMode, KeyRemapMode.None);
            }

            [WpfFact]
            public void NoneWhileFindingCommand()
            {
                Create("hello world");
                _runner.Add(VimUtil.CreateNormalBinding("cat"));
                _runner.Run('c');
                Assert.Equal(_runner.KeyRemapMode, KeyRemapMode.None);
            }

            [WpfFact]
            public void OperatorPendingWhenWaitingForMotionLegacy()
            {
                Create("hello world");
                _runner.Add(VimUtil.CreateMotionBinding("d"));
                _runner.Run('d');
                Assert.Equal(_runner.KeyRemapMode, KeyRemapMode.OperatorPending);
            }

            [WpfFact]
            public void OperatorPendingWhenWaitingForMotion()
            {
                Create("hello world");
                _runner.Add(VimUtil.CreateMotionBinding("d"));
                _runner.Run('d');
                Assert.Equal(_runner.KeyRemapMode, KeyRemapMode.OperatorPending);
            }

            [WpfFact]
            public void OperatorPendingWhenAmbiguousBetweenMotionAndCommand()
            {
                Create("hello world");
                _runner.Add(VimUtil.CreateMotionBinding("d"));
                _runner.Add(VimUtil.CreateNormalBinding("dd"));
                _runner.Run('d');
                Assert.Equal(_runner.KeyRemapMode, KeyRemapMode.OperatorPending);
            }

            [WpfFact]
            public void LanguageInToArgument()
            {
                Create("hello world");
                _runner.Add(VimUtil.CreateMotionBinding("d"));
                _runner.Run('d');
                _runner.Run('f');
                Assert.Equal(KeyRemapMode.Language, _runner.KeyRemapMode);
            }

            /// <summary>
            /// In a complex binding we need to use the KeyRemapMode specified in that binding
            /// </summary>
            [WpfFact]
            public void LongCommandPropagateMode()
            {
                Create("hello world");
                _runner.Add(VimUtil.CreateComplexNormalBinding("a", x => true, KeyRemapMode.Language));
                _runner.Run('a');
                Assert.Equal(_runner.KeyRemapMode, KeyRemapMode.Language);
                _runner.Run('b');
                Assert.Equal(_runner.KeyRemapMode, KeyRemapMode.Language);
            }

            [WpfFact]
            public void AfterCount()
            {
                Create(KeyRemapMode.Language, "");
                _runner.Run('2');
                Assert.Equal(_runner.KeyRemapMode, KeyRemapMode.Language);
            }

            /// <summary>
            /// Even though there is a KeyRemapMode for after count there is not one for after register
            /// </summary>
            [WpfFact]
            public void AfterRegister()
            {
                Create(KeyRemapMode.Language, "");
                _runner.Run('"');
                Assert.Equal(_runner.KeyRemapMode, KeyRemapMode.None);
            }
        }

        public sealed class CountTest : CommandRunnerTest
        {
            public CountTest()
            {
                Create("");
                _runner.Add(VimUtil.CreateNormalBinding("dd"));
            }

            [WpfFact]
            public void Default()
            {
                Assert.False(_runner.InCount);
            }

            [WpfFact]
            public void Simple()
            {
                _runner.Run('1');
                Assert.True(_runner.InCount);
                _runner.Run('d');
                Assert.False(_runner.InCount);
                Assert.True(_runner.IsWaitingForMoreInput);
            }

            [WpfFact]
            public void ResetState4()
            {
                _runner.Run('1');
                _runner.ResetState();
                Assert.False(_runner.InCount);
            }

            [WpfFact]
            public void ResetState2()
            {
                _runner.Run("3d");
                _runner.ResetState();
                Assert.False(_runner.HasCount);
            }

            [WpfFact]
            public void Count()
            {
                _runner.Run("3d");
                Assert.True(_runner.HasCount);
                Assert.Equal(3, _runner.Count);
            }
        }

        public sealed class RegisterTest : CommandRunnerTest
        {
            public RegisterTest()
            {
                Create("");
                _runner.Add(VimUtil.CreateNormalBinding("dd"));
            }

            [WpfFact]
            public void AfterRegister()
            {
                _runner.Run("\"a");
                Assert.True(_runner.IsWaitingForMoreInput);
                Assert.Equal(KeyRemapMode.Normal, _runner.KeyRemapMode);
            }

            [WpfFact]
            public void InRegister()
            {
                _runner.Run("\"");
                Assert.True(_runner.IsWaitingForMoreInput);
                Assert.Equal(KeyRemapMode.None, _runner.KeyRemapMode);
            }

            [WpfFact]
            public void Register()
            {
                _runner.Run(@"""a");
                Assert.True(_runner.HasRegisterName);
                Assert.Equal(RegisterName.OfChar('a').Value, _runner.RegisterName);
            }

            [WpfFact]
            public void ResetState3()
            {
                _runner.Run(@"""a");
                _runner.ResetState();
                Assert.False(_runner.HasRegisterName);
            }
        }

        public sealed class RunTest : CommandRunnerTest
        {
            [WpfFact]
            public void CommandMatch1()
            {
                Create(string.Empty);
                var count1 = 0;
                _runner.Add(VimUtil.CreateNormalBinding("a", data => { count1++; return CommandResult.NewCompleted(ModeSwitch.NoSwitch); }));
                Run("a");
                Assert.Equal(1, count1);
            }

            [WpfFact]
            public void CommandMatch2()
            {
                Create(string.Empty);
                var count1 = 0;
                _runner.Add(VimUtil.CreateNormalBinding("a", data => { count1++; return CommandResult.NewCompleted(ModeSwitch.NoSwitch); }));
                Run("b");
                Assert.Equal(0, count1);
            }

            [WpfFact]
            public void CommandMatch3()
            {
                Create(string.Empty);
                var count1 = 0;
                _runner.Add(VimUtil.CreateNormalBinding("a", data => { count1++; return CommandResult.NewCompleted(ModeSwitch.NoSwitch); }));
                var count2 = 0;
                _runner.Add(VimUtil.CreateNormalBinding("b", data => { count2++; return CommandResult.NewCompleted(ModeSwitch.NoSwitch); }));
                Run("b");
                Assert.Equal(0, count1);
                Assert.Equal(1, count2);
            }

            [WpfFact]
            public void CommandMatch4()
            {
                Create(string.Empty);
                var count1 = 0;
                _runner.Add(VimUtil.CreateNormalBinding("ab", data => { count1++; return CommandResult.NewCompleted(ModeSwitch.NoSwitch); }));
                var count2 = 0;
                _runner.Add(VimUtil.CreateNormalBinding("b", data => { count2++; return CommandResult.NewCompleted(ModeSwitch.NoSwitch); }));
                Run("ab");
                Assert.Equal(1, count1);
                Assert.Equal(0, count2);
            }

            /// <summary>
            /// Prefix is ambiguous and neither should match
            /// </summary>
            [WpfFact]
            public void CommandMatch5()
            {
                Create(string.Empty);
                var count1 = 0;
                _runner.Add(VimUtil.CreateMotionBinding("aa", data => { count1++; return NormalCommand.NewYank(data); }));
                var count2 = 0;
                _runner.Add(VimUtil.CreateNormalBinding("aab", data => { count2++; return CommandResult.NewCompleted(ModeSwitch.NoSwitch); }));
                Run("aa");
                Assert.Equal(0, count1);
                Assert.Equal(0, count2);
            }

            [WpfFact]
            public void CommandMatch6()
            {
                Create(string.Empty);
                var count1 = 0;
                _runner.Add(VimUtil.CreateMotionBinding("aa", data => { count1++; return NormalCommand.NewYank(data); }));
                var count2 = 0;
                _runner.Add(VimUtil.CreateNormalBinding("aab", data => { count2++; return CommandResult.NewCompleted(ModeSwitch.NoSwitch); }));
                Run("aab");
                Assert.Equal(0, count1);
                Assert.Equal(1, count2);
            }

            [WpfFact]
            public void CommandMatch7()
            {
                Create("foo bar");
                var count1 = 0;
                _runner.Add(VimUtil.CreateMotionBinding("aa", data => { count1++; return NormalCommand.NewYank(data); }));
                var count2 = 0;
                _runner.Add(VimUtil.CreateNormalBinding("aab", data => { count2++; return CommandResult.NewCompleted(ModeSwitch.NoSwitch); }));
                Run("aaw");
                Assert.Equal(1, count1);
                Assert.Equal(0, count2);
            }

            [WpfFact]
            public void Count1()
            {
                Create(string.Empty);
                var didRun = false;
                _runner.Add(VimUtil.CreateNormalBinding("a", data =>
                    {
                        Assert.True(data.Count.IsNone());
                        didRun = true;
                        return CommandResult.NewCompleted(ModeSwitch.NoSwitch);
                    }));
                Run("a");
                Assert.True(didRun);
            }

            [WpfFact]
            public void Count2()
            {
                Create(string.Empty);
                var didRun = false;
                _runner.Add(VimUtil.CreateNormalBinding("a", data =>
                    {
                        Assert.True(data.Count.IsSome());
                        Assert.Equal(1, data.Count.Value);
                        didRun = true;
                        return CommandResult.NewCompleted(ModeSwitch.NoSwitch);
                    }));
                Run("1a");
                Assert.True(didRun);
            }

            [WpfFact]
            public void Count3()
            {
                Create(string.Empty);
                var didRun = false;
                _runner.Add(VimUtil.CreateNormalBinding("a", data =>
                    {
                        Assert.True(data.Count.IsSome());
                        Assert.Equal(42, data.Count.Value);
                        didRun = true;
                        return CommandResult.NewCompleted(ModeSwitch.NoSwitch);
                    }));
                Run("42a");
                Assert.True(didRun);
            }

            /// <summary>
            /// 0 is not a valid command
            /// </summary>
            [WpfFact]
            public void Count4()
            {
                Create(string.Empty);
                var didRun = false;
                _runner.Add(VimUtil.CreateNormalBinding("a", () => { didRun = true; }));
                Assert.True(_runner.Run('0').IsError);
                Assert.False(didRun);
            }

            [WpfFact]
            public void Register1()
            {
                Create(string.Empty);
                var didRun = false;
                _runner.Add(VimUtil.CreateNormalBinding("a", data =>
                    {
                        Assert.Equal(RegisterName.Unnamed, data.GetRegisterNameOrDefault());
                        didRun = true;
                        return CommandResult.NewCompleted(ModeSwitch.NoSwitch);
                    }));
                Run("a");
                Assert.True(didRun);
            }

            [WpfFact]
            public void Register2()
            {
                Create(string.Empty);
                var didRun = false;
                _runner.Add(VimUtil.CreateNormalBinding("a", data =>
                    {
                        Assert.Same(_registerMap.GetRegister('c'), data.GetRegister(_registerMap));
                        didRun = true;
                        return CommandResult.NewCompleted(ModeSwitch.NoSwitch);
                    }));
                Run("\"ca");
                Assert.True(didRun);
            }

            [WpfFact]
            public void Register3()
            {
                Create(string.Empty);
                var didRun = false;
                _runner.Add(VimUtil.CreateNormalBinding("a", data =>
                    {
                        Assert.Same(_registerMap.GetRegister('d'), data.GetRegister(_registerMap));
                        didRun = true;
                        return CommandResult.NewCompleted(ModeSwitch.NoSwitch);
                    }));
                Run("\"da");
                Assert.True(didRun);
            }

            [WpfFact]
            public void CountAndRegister1()
            {
                Create(string.Empty);
                var didRun = false;
                _runner.Add(VimUtil.CreateNormalBinding("a", data =>
                    {
                        Assert.True(data.Count.IsSome());
                        Assert.Equal(2, data.Count.Value);
                        Assert.Same(_registerMap.GetRegister('d'), data.GetRegister(_registerMap));
                        didRun = true;
                        return CommandResult.NewCompleted(ModeSwitch.NoSwitch);
                    }));
                Run("\"d2a");
                Assert.True(didRun);
            }

            [WpfFact]
            public void CountAndRegister2()
            {
                Create(string.Empty);
                var didRun = false;
                _runner.Add(VimUtil.CreateNormalBinding("a", data =>
                    {
                        Assert.True(data.Count.IsSome());
                        Assert.Equal(2, data.Count.Value);
                        Assert.Same(_registerMap.GetRegister('d'), data.GetRegister(_registerMap));
                        didRun = true;
                        return CommandResult.NewCompleted(ModeSwitch.NoSwitch);
                    }));
                Run("2\"da");
                Assert.True(didRun);
            }

            [WpfFact]
            public void NoMatchingCommand1()
            {
                Create("hello world");
                _runner.Add(VimUtil.CreateNormalBinding("cat", data => CommandResult.NewCompleted(ModeSwitch.NoSwitch)));
                Assert.True(_runner.Run('b').IsError);
            }

            [WpfFact]
            public void NoMatchingCommand2()
            {
                Create("hello world");
                _runner.Add(VimUtil.CreateNormalBinding("cat", data => CommandResult.NewCompleted(ModeSwitch.NoSwitch)));
                Assert.True(_runner.Run('c').IsNeedMoreInput);
                Assert.True(_runner.Run('b').IsError);
            }

            [WpfFact]
            public void NoMatchingCommand3()
            {
                Create("hello world");
                _runner.Add(VimUtil.CreateNormalBinding("cot", data => CommandResult.NewCompleted(ModeSwitch.NoSwitch)));
                _runner.Add(VimUtil.CreateNormalBinding("cook", data => CommandResult.NewCompleted(ModeSwitch.NoSwitch)));
                Assert.True(_runner.Run('c').IsNeedMoreInput);
                Assert.True(_runner.Run('o').IsNeedMoreInput);
                Assert.True(_runner.Run('b').IsError);
            }

            /// <summary>
            /// A BindData should be passed an Escape if it does set the handle
            /// escape flag
            /// </summary>
            [WpfFact]
            public void RespectDontHandleEscapeFlag()
            {
                Create("hello world");
                var didRun = false;
                var didEscape = false;
                _runner.Add(VimUtil.CreateComplexNormalBinding(
                    "c",
                    ki =>
                    {
                        didRun = true;
                        didEscape = ki == KeyInputUtil.EscapeKey;
                    },
                    flags: CommandFlags.HandlesEscape));
                _runner.Run('c');
                _runner.Run(VimKey.Escape);
                Assert.True(didEscape);
                Assert.True(didRun);
            }

            /// <summary>
            /// A BindData shouldn be passed an Escape if it set the handle escape falg
            /// </summary>
            [WpfFact]
            public void RespectHandleEscapeFlag()
            {
                Create("hello world");
                var didRun = false;
                var didEscape = false;
                _runner.Add(VimUtil.CreateComplexNormalBinding(
                    "c",
                    ki =>
                    {
                        didRun = true;
                        didEscape = ki == KeyInputUtil.EscapeKey;
                    },
                    CommandFlags.HandlesEscape));
                _runner.Run('c');
                _runner.Run(VimKey.Escape);
                Assert.True(didEscape);
                Assert.True(didRun);
            }
        }

        public sealed class MiscTest : CommandRunnerTest
        {
            [WpfFact]
            public void Add1()
            {
                Create(string.Empty);
                var command1 = VimUtil.CreateNormalBinding("foo", data => CommandResult.NewCompleted(ModeSwitch.NoSwitch));
                _runner.Add(command1);
                Assert.Same(command1, _runner.Commands.Single());
            }

            [WpfFact]
            public void Add2()
            {
                Create(string.Empty);
                var command1 = VimUtil.CreateNormalBinding("foo", data => CommandResult.NewCompleted(ModeSwitch.NoSwitch));
                var command2 = VimUtil.CreateNormalBinding("bar", data => CommandResult.NewCompleted(ModeSwitch.NoSwitch));
                _runner.Add(command1);
                _runner.Add(command2);
                Assert.Equal(2, _runner.Commands.Count());
                Assert.Contains(command1, _runner.Commands);
                Assert.Contains(command2, _runner.Commands);
            }

            [WpfFact]
            public void Add3()
            {
                Create(string.Empty);
                var command1 = VimUtil.CreateNormalBinding("foo", data => CommandResult.NewCompleted(ModeSwitch.NoSwitch));
                _runner.Add(command1);
                Assert.Throws<ArgumentException>(() => _runner.Add(command1));
            }

            [WpfFact]
            public void Add4()
            {
                Create(string.Empty);
                var command1 = VimUtil.CreateNormalBinding("foo", data => CommandResult.NewCompleted(ModeSwitch.NoSwitch));
                var command2 = VimUtil.CreateNormalBinding("foo");
                _runner.Add(command1);
                Assert.Throws<ArgumentException>(() => _runner.Add(command2));
            }

            [WpfFact]
            public void Remove1()
            {
                Create(string.Empty);
                var command1 = VimUtil.CreateNormalBinding("foo", data => CommandResult.NewCompleted(ModeSwitch.NoSwitch));
                _runner.Add(command1);
                _runner.Remove(command1.KeyInputSet);
                Assert.Empty(_runner.Commands);
            }

            /// <summary>
            /// Don't throw when removing a command that's not present
            /// </summary>
            [WpfFact]
            public void Remove2()
            {
                Create(string.Empty);
                _runner.Remove(KeyNotationUtil.StringToKeyInputSet("foo"));
                Assert.Empty(_runner.Commands);
            }

            [WpfFact]
            public void IsWaitingForMoreInput1()
            {
                Create("hello world");
                _runner.Add(VimUtil.CreateNormalBinding("cat", data => CommandResult.NewCompleted(ModeSwitch.NoSwitch)));
                Assert.True(Run("c").IsNeedMoreInput);
                Assert.True(_runner.IsWaitingForMoreInput);
            }

            [WpfFact]
            public void IsWaitingForMoreInput2()
            {
                Create("hello world");
                _runner.Add(VimUtil.CreateNormalBinding("cat", data => CommandResult.NewCompleted(ModeSwitch.NoSwitch)));
                Assert.True(Run("ca").IsNeedMoreInput);
                Assert.True(_runner.IsWaitingForMoreInput);
            }

            [WpfFact]
            public void IsWaitingForMoreInput3()
            {
                Create("hello world");
                _runner.Add(VimUtil.CreateNormalBinding("cat", data => CommandResult.NewCompleted(ModeSwitch.NoSwitch)));
                Assert.True(Run("cat").IsComplete);
                Assert.False(_runner.IsWaitingForMoreInput);
            }

            [WpfFact]
            public void IsWaitingForMoreInput4()
            {
                Create("hello world");
                _runner.Add(VimUtil.CreateNormalBinding("cat", data => CommandResult.NewCompleted(ModeSwitch.NoSwitch)));
                Assert.True(Run("ca").IsNeedMoreInput);
                Assert.True(_runner.Run(KeyInputUtil.EscapeKey).IsCancelled);
                Assert.False(_runner.IsWaitingForMoreInput);
            }

            /// <summary>
            /// Cancel in a motion
            /// </summary>
            [WpfFact]
            public void IsWaitingForMoreInput5()
            {
                Create("hello world");
                _runner.Add(VimUtil.CreateMotionBinding("cat"));
                Assert.True(Run("cata").IsNeedMoreInput);
                Assert.True(_runner.Run(KeyInputUtil.EscapeKey).IsCancelled);
                Assert.False(_runner.IsWaitingForMoreInput);
            }

            /// <summary>
            /// Make sure we are properly able to distinguish between motion and non-motion commands
            /// which have similar prefix matches
            /// </summary>
            [WpfFact]
            public void Run_MotionMixedWithNonMotion()
            {
                Create("the dog chased the ball");
                var simple = new[] { "g~g~", "g~~", "gugu", "guu", "gUgU", "gUU" };
                var motion = new[] { "g~", "gu", "gU" };

                foreach (var cur in simple)
                {
                    _runner.Add(VimUtil.CreateNormalBinding(cur));
                }

                foreach (var cur in motion)
                {
                    _runner.Add(VimUtil.CreateMotionBinding(cur));
                }

                foreach (var cur in simple)
                {
                    // Make sure we can run them all
                    Assert.True(_runner.Run(cur).IsComplete);
                }

                foreach (var cur in motion)
                {
                    // Make sure we can run them all
                    Assert.True(_runner.Run(cur).IsNeedMoreInput);
                    _runner.ResetState();
                }
            }

            [WpfFact]
            public void Reset1()
            {
                Create("hello world");
                _runner.Add(VimUtil.CreateNormalBinding("abc", data => CommandResult.NewCompleted(ModeSwitch.NoSwitch)));
                Run("a");
                Assert.True(_runner.IsWaitingForMoreInput);
                _runner.ResetState();
                Assert.False(_runner.IsWaitingForMoreInput);
            }

            [WpfFact]
            public void ComplexCommand_EnsureItLoops()
            {
                Create("hello world");
                var seen = string.Empty;
                bool func(KeyInput ki)
                {
                    seen += ki.Char.ToString();
                    return seen != "ood";
                }

                _runner.Add(VimUtil.CreateComplexNormalBinding("f", func));
                Assert.True(_runner.Run("food").IsComplete);
                Assert.Equal("ood", seen);
            }

            /// <summary>
            /// Disallow Run calls during a bind operation
            /// </summary>
            [WpfFact]
            public void NestedRun_DontAllowRunDuringBind()
            {
                Create("hello world");
                _runner.Add(VimUtil.CreateNormalBinding("b"));
                _runner.Add(VimUtil.CreateComplexNormalBinding(
                    "a",
                    _ =>
                    {
                        var res = _runner.Run(KeyInputUtil.CharToKeyInput('b'));
                        Assert.True(res.IsError);
                    }));

                var res2 = _runner.Run("ab");
                Assert.True(res2.IsComplete);
            }

            /// <summary>
            /// It's OK for a Command to call back into the ICommandRunner::Run which ran it
            /// so long as the binding is complete.  This is necessary for Macros
            /// </summary>
            [WpfFact]
            public void NestedRun_AllowMultipleRuns()
            {
                Create("");
                var ran1 = false;
                var ran2 = false;
                _runner.Add(VimUtil.CreateNormalBinding("a", command: VimUtil.CreatePing(
                    _ =>
                    {
                        ran1 = true;
                        Assert.True(_runner.Run('b').IsComplete);
                    })));
                _runner.Add(VimUtil.CreateNormalBinding("b", command: VimUtil.CreatePing(
                    _ =>
                    {
                        ran2 = true;
                    })));
                Assert.True(_runner.Run('a').IsComplete);
                Assert.True(ran1);
                Assert.True(ran2);
            }

            [WpfFact]
            public void CommandRan1()
            {
                Create("hello world");
                var didSee = false;
                var command1 = VimUtil.CreateNormalBinding("c", data => CommandResult.NewCompleted(ModeSwitch.NoSwitch));
                _runner.Add(command1);
                _runner.CommandRan += (notUsed, args) =>
                    {
                        Assert.Same(command1, args.CommandRunData.CommandBinding);
                        Assert.True(args.CommandRunData.CommandResult.IsCompleted);
                        didSee = true;
                    };
                _runner.Run('c');
                Assert.True(didSee);
            }

            [WpfFact]
            public void CommandRan2()
            {
                Create("hello world");
                var didSee = false;
                var command1 = VimUtil.CreateNormalBinding("c", data => CommandResult.NewCompleted(ModeSwitch.NoSwitch));
                _runner.Add(command1);
                _runner.CommandRan += (notUsed, args) =>
                    {
                        Assert.Same(command1, args.CommandRunData.CommandBinding);
                        didSee = true;
                    };
                _runner.Run('2');
                _runner.Run('c');
                Assert.True(didSee);
            }

            [WpfFact]
            public void CommandRan3()
            {
                Create("hello world");
                var didSee = false;
                var command1 = VimUtil.CreateNormalBinding("cat", data => CommandResult.NewCompleted(ModeSwitch.NoSwitch));
                _runner.Add(command1);
                _runner.CommandRan += (notUsed, tuple) =>
                    {
                        didSee = true;
                    };
                _runner.Run('c');
                _runner.Run(KeyInputUtil.EscapeKey);
                Assert.False(didSee);
            }
        }
    }
}
