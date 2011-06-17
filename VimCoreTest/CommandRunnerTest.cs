using System;
using System.Linq;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.UnitTest;
using Vim.UnitTest.Mock;
using GlobalSettings = Vim.GlobalSettings;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class CommandRunnerTest
    {
        private MockRepository _factory;
        private Mock<IVimHost> _host;
        private Mock<IStatusUtil> _statusUtil;
        private ICommandUtil _commandUtil;
        private IVimData _vimData;
        private IRegisterMap _registerMap;
        private ITextView _textView;
        private CommandRunner _runnerRaw;
        private ICommandRunner _runner;

        private void Create(params string[] lines)
        {
            _textView = EditorUtil.CreateTextView(lines);
            _factory = new MockRepository(MockBehavior.Strict);
            _host = _factory.Create<IVimHost>();
            _statusUtil = _factory.Create<IStatusUtil>();
            _registerMap = VimUtil.CreateRegisterMap(MockObjectFactory.CreateClipboardDevice(_factory).Object);
            _vimData = new VimData();
            var settings = new GlobalSettings();
            var localSettings = new LocalSettings(settings, EditorUtil.GetEditorOptions(_textView), _textView);
            var motionUtil = VimUtil.CreateTextViewMotionUtil(
                _textView,
                settings: localSettings,
                vimData: _vimData);
            var capture = new MotionCapture(
                _host.Object,
                _textView,
                MockObjectFactory.CreateIncrementalSearch(factory: _factory).Object,
                localSettings);
            _commandUtil = VimUtil.CreateCommandUtil(
                _textView,
                motionUtil: motionUtil,
                statusUtil: _statusUtil.Object,
                registerMap: _registerMap,
                vimData: _vimData);
            _runnerRaw = new CommandRunner(
                _textView,
                _registerMap,
                capture,
                _commandUtil,
                _statusUtil.Object,
                VisualKind.Character);
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

        [Test]
        public void Add1()
        {
            Create(String.Empty);
            var command1 = VimUtil.CreateNormalBinding("foo", data => CommandResult.NewCompleted(ModeSwitch.NoSwitch));
            _runner.Add(command1);
            Assert.AreSame(command1, _runner.Commands.Single());
        }

        [Test]
        public void Add2()
        {
            Create(String.Empty);
            var command1 = VimUtil.CreateNormalBinding("foo", data => CommandResult.NewCompleted(ModeSwitch.NoSwitch));
            var command2 = VimUtil.CreateNormalBinding("bar", data => CommandResult.NewCompleted(ModeSwitch.NoSwitch));
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
            var command1 = VimUtil.CreateNormalBinding("foo", data => CommandResult.NewCompleted(ModeSwitch.NoSwitch));
            _runner.Add(command1);
            _runner.Add(command1);
        }

        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void Add4()
        {
            Create(String.Empty);
            var command1 = VimUtil.CreateNormalBinding("foo", data => CommandResult.NewCompleted(ModeSwitch.NoSwitch));
            var command2 = VimUtil.CreateNormalBinding("foo");
            _runner.Add(command1);
            _runner.Add(command2);
        }

        [Test]
        public void Remove1()
        {
            Create(String.Empty);
            var command1 = VimUtil.CreateNormalBinding("foo", data => CommandResult.NewCompleted(ModeSwitch.NoSwitch));
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
            _runner.Add(VimUtil.CreateNormalBinding("a", data => { count1++; return CommandResult.NewCompleted(ModeSwitch.NoSwitch); }));
            Run("a");
            Assert.AreEqual(1, count1);
        }

        [Test]
        public void Run_CommandMatch2()
        {
            Create(String.Empty);
            var count1 = 0;
            _runner.Add(VimUtil.CreateNormalBinding("a", data => { count1++; return CommandResult.NewCompleted(ModeSwitch.NoSwitch); }));
            Run("b");
            Assert.AreEqual(0, count1);
        }

        [Test]
        public void Run_CommandMatch3()
        {
            Create(String.Empty);
            var count1 = 0;
            _runner.Add(VimUtil.CreateNormalBinding("a", data => { count1++; return CommandResult.NewCompleted(ModeSwitch.NoSwitch); }));
            var count2 = 0;
            _runner.Add(VimUtil.CreateNormalBinding("b", data => { count2++; return CommandResult.NewCompleted(ModeSwitch.NoSwitch); }));
            Run("b");
            Assert.AreEqual(0, count1);
            Assert.AreEqual(1, count2);
        }

        [Test]
        public void Run_CommandMatch4()
        {
            Create(String.Empty);
            var count1 = 0;
            _runner.Add(VimUtil.CreateNormalBinding("ab", data => { count1++; return CommandResult.NewCompleted(ModeSwitch.NoSwitch); }));
            var count2 = 0;
            _runner.Add(VimUtil.CreateNormalBinding("b", data => { count2++; return CommandResult.NewCompleted(ModeSwitch.NoSwitch); }));
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
            _runner.Add(VimUtil.CreateMotionBinding("aa", data => { count1++; return NormalCommand.NewYank(data); }));
            var count2 = 0;
            _runner.Add(VimUtil.CreateNormalBinding("aab", data => { count2++; return CommandResult.NewCompleted(ModeSwitch.NoSwitch); }));
            Run("aa");
            Assert.AreEqual(0, count1);
            Assert.AreEqual(0, count2);
        }

        [Test]
        public void Run_CommandMatch6()
        {
            Create(String.Empty);
            var count1 = 0;
            _runner.Add(VimUtil.CreateMotionBinding("aa", data => { count1++; return NormalCommand.NewYank(data); }));
            var count2 = 0;
            _runner.Add(VimUtil.CreateNormalBinding("aab", data => { count2++; return CommandResult.NewCompleted(ModeSwitch.NoSwitch); }));
            Run("aab");
            Assert.AreEqual(0, count1);
            Assert.AreEqual(1, count2);
        }

        [Test]
        public void Run_CommandMatch7()
        {
            Create("foo bar");
            var count1 = 0;
            _runner.Add(VimUtil.CreateMotionBinding("aa", data => { count1++; return NormalCommand.NewYank(data); }));
            var count2 = 0;
            _runner.Add(VimUtil.CreateNormalBinding("aab", data => { count2++; return CommandResult.NewCompleted(ModeSwitch.NoSwitch); }));
            Run("aaw");
            Assert.AreEqual(1, count1);
            Assert.AreEqual(0, count2);
        }

        [Test]
        public void Run_Count1()
        {
            Create(String.Empty);
            var didRun = false;
            _runner.Add(VimUtil.CreateNormalBinding("a", data =>
                {
                    Assert.IsTrue(data.Count.IsNone());
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
            _runner.Add(VimUtil.CreateNormalBinding("a", data =>
                {
                    Assert.IsTrue(data.Count.IsSome());
                    Assert.AreEqual(1, data.Count.Value);
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
            _runner.Add(VimUtil.CreateNormalBinding("a", data =>
                {
                    Assert.IsTrue(data.Count.IsSome());
                    Assert.AreEqual(42, data.Count.Value);
                    didRun = true;
                    return CommandResult.NewCompleted(ModeSwitch.NoSwitch);
                }));
            Run("42a");
            Assert.IsTrue(didRun);
        }

        /// <summary>
        /// 0 is not a valid command
        /// </summary>
        [Test]
        public void Run_Count4()
        {
            Create(string.Empty);
            var didRun = false;
            _runner.Add(VimUtil.CreateNormalBinding("a", () => { didRun = true; }));
            Assert.IsTrue(_runner.Run('0').IsError);
            Assert.IsFalse(didRun);
        }

        [Test]
        public void Run_Register1()
        {
            Create(String.Empty);
            var didRun = false;
            _runner.Add(VimUtil.CreateNormalBinding("a", data =>
                {
                    Assert.AreEqual(RegisterName.Unnamed, data.RegisterNameOrDefault);
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
            _runner.Add(VimUtil.CreateNormalBinding("a", data =>
                {
                    Assert.AreSame(_registerMap.GetRegister('c'), data.GetRegister(_registerMap));
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
            _runner.Add(VimUtil.CreateNormalBinding("a", data =>
                {
                    Assert.AreSame(_registerMap.GetRegister('d'), data.GetRegister(_registerMap));
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
            _runner.Add(VimUtil.CreateNormalBinding("a", data =>
                {
                    Assert.IsTrue(data.Count.IsSome());
                    Assert.AreEqual(2, data.Count.Value);
                    Assert.AreSame(_registerMap.GetRegister('d'), data.GetRegister(_registerMap));
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
            _runner.Add(VimUtil.CreateNormalBinding("a", data =>
                {
                    Assert.IsTrue(data.Count.IsSome());
                    Assert.AreEqual(2, data.Count.Value);
                    Assert.AreSame(_registerMap.GetRegister('d'), data.GetRegister(_registerMap));
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
            _runner.Add(VimUtil.CreateNormalBinding("cat", data => CommandResult.NewCompleted(ModeSwitch.NoSwitch)));
            Assert.IsTrue(_runner.Run('b').IsError);
        }

        [Test]
        public void Run_NoMatchingCommand2()
        {
            Create("hello world");
            _runner.Add(VimUtil.CreateNormalBinding("cat", data => CommandResult.NewCompleted(ModeSwitch.NoSwitch)));
            Assert.IsTrue(_runner.Run('c').IsNeedMoreInput);
            Assert.IsTrue(_runner.Run('b').IsError);
        }

        [Test]
        public void Run_NoMatchingCommand3()
        {
            Create("hello world");
            _runner.Add(VimUtil.CreateNormalBinding("cot", data => CommandResult.NewCompleted(ModeSwitch.NoSwitch)));
            _runner.Add(VimUtil.CreateNormalBinding("cook", data => CommandResult.NewCompleted(ModeSwitch.NoSwitch)));
            Assert.IsTrue(_runner.Run('c').IsNeedMoreInput);
            Assert.IsTrue(_runner.Run('o').IsNeedMoreInput);
            Assert.IsTrue(_runner.Run('b').IsError);
        }

        /// <summary>
        /// A BindData should be passed an Escape if it does set the handle
        /// escape flag
        /// </summary>
        [Test]
        public void Run_RespectDontHandleEscapeFlag()
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
            Assert.IsTrue(didEscape);
            Assert.IsTrue(didRun);
        }

        /// <summary>
        /// A BindData shouldn be passed an Escape if it set the handle escape falg
        /// </summary>
        [Test]
        public void Run_RespectHandleEscapeFlag()
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
            Assert.IsTrue(didEscape);
            Assert.IsTrue(didRun);
        }

        [Test]
        public void IsWaitingForMoreInput1()
        {
            Create("hello world");
            _runner.Add(VimUtil.CreateNormalBinding("cat", data => CommandResult.NewCompleted(ModeSwitch.NoSwitch)));
            Assert.IsTrue(Run("c").IsNeedMoreInput);
            Assert.IsTrue(_runner.IsWaitingForMoreInput);
        }

        [Test]
        public void IsWaitingForMoreInput2()
        {
            Create("hello world");
            _runner.Add(VimUtil.CreateNormalBinding("cat", data => CommandResult.NewCompleted(ModeSwitch.NoSwitch)));
            Assert.IsTrue(Run("ca").IsNeedMoreInput);
            Assert.IsTrue(_runner.IsWaitingForMoreInput);
        }

        [Test]
        public void IsWaitingForMoreInput3()
        {
            Create("hello world");
            _runner.Add(VimUtil.CreateNormalBinding("cat", data => CommandResult.NewCompleted(ModeSwitch.NoSwitch)));
            Assert.IsTrue(Run("cat").IsComplete);
            Assert.IsFalse(_runner.IsWaitingForMoreInput);
        }

        [Test]
        public void IsWaitingForMoreInput4()
        {
            Create("hello world");
            _runner.Add(VimUtil.CreateNormalBinding("cat", data => CommandResult.NewCompleted(ModeSwitch.NoSwitch)));
            Assert.IsTrue(Run("ca").IsNeedMoreInput);
            Assert.IsTrue(_runner.Run(KeyInputUtil.EscapeKey).IsCancelled);
            Assert.IsFalse(_runner.IsWaitingForMoreInput);
        }

        [Test]
        [Description("Cancel in a motion")]
        public void IsWaitingForMoreInput5()
        {
            Create("hello world");
            _runner.Add(VimUtil.CreateMotionBinding("cat"));
            Assert.IsTrue(Run("cata").IsNeedMoreInput);
            Assert.IsTrue(_runner.Run(KeyInputUtil.EscapeKey).IsCancelled);
            Assert.IsFalse(_runner.IsWaitingForMoreInput);
        }

        /// <summary>
        /// Make sure we are properly able to distinguish between motion and non-motion commands
        /// which have similar prefix matches
        /// </summary>
        [Test]
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
                Assert.IsTrue(_runner.Run(cur).IsComplete);
            }

            foreach (var cur in motion)
            {
                // Make sure we can run them all
                Assert.IsTrue(_runner.Run(cur).IsNeedMoreInput);
                _runner.ResetState();
            }

        }

        [Test]
        public void Reset1()
        {
            Create("hello world");
            _runner.Add(VimUtil.CreateNormalBinding("abc", data => CommandResult.NewCompleted(ModeSwitch.NoSwitch)));
            Run("a");
            Assert.IsTrue(_runner.IsWaitingForMoreInput);
            _runner.ResetState();
            Assert.IsFalse(_runner.IsWaitingForMoreInput);
        }

        [Test]
        public void ComplexCommand_EnsureItLoops()
        {
            Create("hello world");
            var seen = string.Empty;
            Func<KeyInput, bool> func = ki =>
            {
                seen += ki.Char.ToString();
                return seen != "ood";
            };

            _runner.Add(VimUtil.CreateComplexNormalBinding("f", func));
            Assert.IsTrue(_runner.Run("food").IsComplete);
            Assert.AreEqual("ood", seen);
        }

        /// <summary>
        /// Disallow Run calls during a bind operation
        /// </summary>
        [Test]
        public void NestedRun_DontAllowRunDuringBind()
        {
            Create("hello world");
            _runner.Add(VimUtil.CreateNormalBinding("b"));
            _runner.Add(VimUtil.CreateComplexNormalBinding(
                "a",
                _ =>
                {
                    var res = _runner.Run(KeyInputUtil.CharToKeyInput('b'));
                    Assert.IsTrue(res.IsError);
                }));

            var res2 = _runner.Run("ab");
            Assert.IsTrue(res2.IsComplete);
        }

        /// <summary>
        /// It's OK for a Command to call back into the ICommandRunner::Run which ran it
        /// so long as the binding is complete.  This is necessary for Macros
        /// </summary>
        [Test]
        public void NestedRun_AllowMultipleRuns()
        {
            Create("");
            var ran1 = false;
            var ran2 = false;
            _runner.Add(VimUtil.CreateNormalBinding("a", command: VimUtil.CreatePing(
                _ =>
                {
                    ran1 = true;
                    Assert.IsTrue(_runner.Run('b').IsComplete);
                })));
            _runner.Add(VimUtil.CreateNormalBinding("b", command: VimUtil.CreatePing(
                _ =>
                {
                    ran2 = true;
                })));
            Assert.IsTrue(_runner.Run('a').IsComplete);
            Assert.IsTrue(ran1);
            Assert.IsTrue(ran2);
        }

        [Test]
        public void CommandRan1()
        {
            Create("hello world");
            var didSee = false;
            var command1 = VimUtil.CreateNormalBinding("c", data => CommandResult.NewCompleted(ModeSwitch.NoSwitch));
            _runner.Add(command1);
            _runner.CommandRan += (notUsed, tuple) =>
                {
                    Assert.AreSame(command1, tuple.CommandBinding);
                    Assert.IsTrue(tuple.CommandResult.IsCompleted);
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
            var command1 = VimUtil.CreateNormalBinding("c", data => CommandResult.NewCompleted(ModeSwitch.NoSwitch));
            _runner.Add(command1);
            _runner.CommandRan += (notUsed, tuple) =>
                {
                    Assert.AreSame(command1, tuple.CommandBinding);
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
            var command1 = VimUtil.CreateNormalBinding("cat", data => CommandResult.NewCompleted(ModeSwitch.NoSwitch));
            _runner.Add(command1);
            _runner.CommandRan += (notUsed, tuple) =>
                {
                    didSee = true;
                };
            _runner.Run('c');
            _runner.Run(KeyInputUtil.EscapeKey);
            Assert.IsFalse(didSee);
        }

        [Test]
        public void KeyRemapMode_DefaultIsNone()
        {
            Create("hello world");
            Assert.IsTrue(_runner.KeyRemapMode.IsNone());
        }

        [Test]
        public void KeyRemapMode_NoneWhileFindingCommand()
        {
            Create("hello world");
            _runner.Add(VimUtil.CreateNormalBinding("cat"));
            _runner.Run('c');
            Assert.IsTrue(_runner.KeyRemapMode.IsNone());
        }

        [Test]
        public void KeyRemapMode_OperatorPendingWhenWaitingForMotionLegacy()
        {
            Create("hello world");
            _runner.Add(VimUtil.CreateMotionBinding("d"));
            _runner.Run('d');
            Assert.IsTrue(_runner.KeyRemapMode.IsSome(KeyRemapMode.OperatorPending));
        }

        [Test]
        public void KeyRemapMode_OperatorPendingWhenWaitingForMotion()
        {
            Create("hello world");
            _runner.Add(VimUtil.CreateMotionBinding("d"));
            _runner.Run('d');
            Assert.IsTrue(_runner.KeyRemapMode.IsSome(KeyRemapMode.OperatorPending));
        }

        [Test]
        public void KeyRemapMode_OperatorPendingWhenAmbiguousBetweenMotionAndCommand()
        {
            Create("hello world");
            _runner.Add(VimUtil.CreateMotionBinding("d"));
            _runner.Add(VimUtil.CreateNormalBinding("dd"));
            _runner.Run('d');
            Assert.IsTrue(_runner.KeyRemapMode.IsSome(KeyRemapMode.OperatorPending));
        }

        [Test]
        public void KeyRemapMode_LanguageInToArgument()
        {
            Create("hello world");
            _runner.Add(VimUtil.CreateMotionBinding("d"));
            _runner.Run('d');
            _runner.Run('f');
            Assert.AreEqual(KeyRemapMode.Language, _runner.KeyRemapMode.Value);
        }

        /// <summary>
        /// In a complex binding we need to use the KeyRemapMode specified in that binding
        /// </summary>
        [Test]
        public void KeyRemapMode_LongCommandPropagateMode()
        {
            Create("hello world");
            _runner.Add(VimUtil.CreateComplexNormalBinding("a", x => true, KeyRemapMode.Language));
            _runner.Run('a');
            Assert.IsTrue(_runner.KeyRemapMode.IsSome(KeyRemapMode.Language));
            _runner.Run('b');
            Assert.IsTrue(_runner.KeyRemapMode.IsSome(KeyRemapMode.Language));
        }
    }
}
