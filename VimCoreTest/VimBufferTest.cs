using System;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;
using Vim.Extensions;

namespace VimCore.UnitTest
{
    [TestFixture]
    public sealed class VimBufferTest : VimTestBase
    {
        private ITextView _textView;
        private VimBuffer _vimBufferRaw;
        private IVimBuffer _vimBuffer;
        private MockRepository _factory;
        private IKeyMap _keyMap;

        [SetUp]
        public void Setup()
        {
            _textView = CreateTextView("here we go");
            _textView.MoveCaretTo(0);
            _vimBuffer = Vim.CreateVimBuffer(_textView);
            _vimBuffer.SwitchMode(ModeKind.Command, ModeArgument.None);
            _keyMap = _vimBuffer.Vim.KeyMap;
            _vimBufferRaw = (VimBuffer)_vimBuffer;
            _factory = new MockRepository(MockBehavior.Strict);
        }

        private Mock<INormalMode> CreateAndAddNormalMode(MockBehavior behavior = MockBehavior.Strict)
        {
            var mode = _factory.Create<INormalMode>(behavior);
            mode.SetupGet(x => x.ModeKind).Returns(ModeKind.Normal);
            mode.SetupGet(x => x.KeyRemapMode).Returns(KeyRemapMode.Normal);
            _vimBufferRaw.RemoveMode(_vimBufferRaw.NormalMode);
            _vimBufferRaw.AddMode(mode.Object);
            return mode;
        }

        private Mock<IInsertMode> CreateAndAddInsertMode(MockBehavior behavior = MockBehavior.Strict)
        {
            var mode = _factory.Create<IInsertMode>(behavior);
            mode.SetupGet(x => x.ModeKind).Returns(ModeKind.Insert);
            _vimBufferRaw.RemoveMode(_vimBuffer.InsertMode);
            _vimBufferRaw.AddMode(mode.Object);
            return mode;
        }

        private Mock<IVisualMode> CreateAndAddVisualLineMode(MockBehavior behavior = MockBehavior.Strict)
        {
            var mode = _factory.Create<IVisualMode>(behavior);
            mode.SetupGet(x => x.ModeKind).Returns(ModeKind.VisualLine);
            _vimBufferRaw.RemoveMode(_vimBuffer.VisualLineMode);
            _vimBufferRaw.AddMode(mode.Object);
            return mode;
        }

        /// <summary>
        /// Make sure the SwitchdMode event fires when switching modes.
        /// </summary>
        [Test]
        public void SwitchedMode_Event()
        {
            var ran = false;
            _vimBuffer.SwitchedMode += (s, m) => { ran = true; };
            _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            Assert.IsTrue(ran);
        }

        /// <summary>
        /// Make sure the SwitchdMode event fires even when switching to the
        /// same mode
        /// </summary>
        [Test]
        public void SwitchedMode_SameModeEvent()
        {
            var ran = false;
            _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            _vimBuffer.SwitchedMode += (s, m) => { ran = true; };
            _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            Assert.IsTrue(ran);
        }

        /// <summary>
        /// Ensure switching to the previous mode operates correctly
        /// </summary>
        [Test]
        public void SwitchPreviousMode_EnterLeaveOrder()
        {
            var normal = CreateAndAddNormalMode();
            var insert = CreateAndAddInsertMode();
            normal.Setup(x => x.OnEnter(ModeArgument.None)).Verifiable();
            _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            normal.Verify();
            normal.Setup(x => x.OnLeave());
            insert.Setup(x => x.OnEnter(ModeArgument.None));
            _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            normal.Verify();
            insert.Verify();

            insert.Setup(x => x.OnLeave()).Verifiable();
            var prev = _vimBuffer.SwitchPreviousMode();
            Assert.AreSame(normal.Object, prev);
            insert.Verify();

            // On tear down all IVimBuffer instances are closed as well as their active
            // IMode.  Need a setup here
            insert.Setup(x => x.OnClose());
            normal.Setup(x => x.OnClose());
        }

        /// <summary>
        /// SwitchPreviousMode should raise the SwitchedMode event
        /// </summary>
        [Test]
        public void SwitchPreviousMode_RaiseSwitchedMode()
        {
            _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            var ran = false;
            _vimBuffer.SwitchedMode += (s, m) => { ran = true; };
            _vimBuffer.SwitchPreviousMode();
            Assert.IsTrue(ran);
        }

        /// <summary>
        /// When a mode returns the SwitchModeOneTimeCommand value it should cause the 
        /// InOneTimeCommand value to be set
        /// </summary>
        [Test]
        public void SwitchModeOneTimeCommand_SetProperty()
        {
            var mode = CreateAndAddInsertMode(MockBehavior.Loose);
            mode.Setup(x => x.Process(It.IsAny<KeyInput>())).Returns(ProcessResult.NewHandled(ModeSwitch.SwitchModeOneTimeCommand));
            _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            _vimBuffer.Process('c');
            Assert.IsTrue(_vimBuffer.InOneTimeCommand.IsSome(ModeKind.Insert));
        }

        /// <summary>
        /// Make sure the processed event is raised during a key process
        /// </summary>
        [Test]
        public void KeyInputEvents_Processed()
        {
            _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            _textView.SetText("hello world");

            var ran = false;
            _vimBuffer.KeyInputProcessed += delegate { ran = true; };
            _vimBuffer.Process('l');
            Assert.IsTrue(ran);
        }

        /// <summary>
        /// Make sure the events are raised in order
        /// </summary>
        [Test]
        public void KeyInputEvents_InOrder()
        {
            _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            _textView.SetText("hello world");

            var start = false;
            var processed = false;
            var end = false;
            _vimBuffer.KeyInputStart +=
                (b, keyInput) =>
                {
                    Assert.AreEqual('l', keyInput.Char);
                    Assert.IsFalse(processed || end);
                    start = true;
                };
            _vimBuffer.KeyInputProcessed +=
                (b, tuple) =>
                {
                    var keyInput = tuple.Item1;
                    Assert.AreEqual('l', keyInput.Char);
                    Assert.IsTrue(start && !end);
                    processed = true;
                };
            _vimBuffer.KeyInputEnd +=
                (b, keyInput) =>
                {
                    Assert.AreEqual('l', keyInput.Char);
                    Assert.IsTrue(start && processed);
                    end = true;
                };
            _vimBuffer.Process('l');
            Assert.IsTrue(start && processed && end);
        }

        /// <summary>
        /// Start and End events should fire even if there is an exception thrown
        /// </summary>
        [Test]
        public void KeyInputEvents_ExceptionDuringProcessing()
        {
            var normal = CreateAndAddNormalMode(MockBehavior.Loose);
            normal.Setup(x => x.Process(It.IsAny<KeyInput>())).Throws(new Exception());
            _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            _textView.SetText("hello world");

            var start = false;
            var end = false;
            _vimBuffer.KeyInputStart +=
                (b, keyInput) =>
                {
                    Assert.AreEqual('l', keyInput.Char);
                    Assert.IsTrue(!end);
                    start = true;
                };
            _vimBuffer.KeyInputEnd +=
                (b, keyInput) =>
                {
                    Assert.AreEqual('l', keyInput.Char);
                    Assert.IsTrue(start);
                    end = true;
                };
            var caught = false;
            try
            {
                _vimBuffer.Process('l');
            }
            catch (Exception)
            {
                caught = true;
            }
            Assert.IsTrue(start && end && caught);
        }

        /// <summary>
        /// Start, Buffered and End should fire if the KeyInput is buffered due to 
        /// a mapping
        /// </summary>
        [Test]
        public void KeyInputEvents_BufferedInput()
        {
            _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            _vimBuffer.Vim.KeyMap.MapWithNoRemap("lad", "rad", KeyRemapMode.Normal);
            _textView.SetText("hello world");

            var start = false;
            var processed = false;
            var end = false;
            var buffered = false;
            _vimBuffer.KeyInputStart +=
                (b, keyInput) =>
                {
                    Assert.AreEqual('l', keyInput.Char);
                    Assert.IsTrue(!buffered && !end);
                    start = true;
                };
            _vimBuffer.KeyInputEnd +=
                (b, keyInput) =>
                {
                    Assert.AreEqual('l', keyInput.Char);
                    Assert.IsTrue(start && buffered);
                    end = true;
                };
            _vimBuffer.KeyInputBuffered +=
                (b, keyInput) =>
                {
                    Assert.AreEqual('l', keyInput.Char);
                    Assert.IsTrue(start && !end);
                    buffered = true;
                };
            _vimBuffer.KeyInputProcessed += delegate { processed = true; };
            _vimBuffer.Process('l');
            Assert.IsTrue(start && buffered && end && !processed);
        }

        /// <summary>
        /// Close should call OnLeave and OnClose for the active IMode
        /// </summary>
        [Test]
        public void Close_ShouldCallLeaveAndClose()
        {
            var normal = CreateAndAddNormalMode(MockBehavior.Loose);
            _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            normal.Setup(x => x.OnLeave()).Verifiable();
            normal.Setup(x => x.OnClose()).Verifiable();
            _vimBuffer.Close();
            normal.Verify();
        }

        /// <summary>
        /// Close should call IMode::Close for every IMode even the ones which
        /// are not active
        /// </summary>
        [Test]
        public void Close_CallCloseOnAll()
        {
            var insert = CreateAndAddInsertMode();
            insert.Setup(x => x.OnClose()).Verifiable();
            _vimBuffer.Close();
            insert.Verify();
        }

        /// <summary>
        /// Process should handle the return value correctly
        /// </summary>
        [Test]
        public void Process_HandleSwitchPreviousMode()
        {
            var normal = CreateAndAddNormalMode(MockBehavior.Loose);
            normal.Setup(x => x.Process(It.IsAny<KeyInput>())).Returns(ProcessResult.NewHandled(ModeSwitch.SwitchPreviousMode));
            _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            _vimBuffer.Process('l');
            Assert.AreEqual(ModeKind.Command, _vimBuffer.ModeKind);
        }

        /// <summary>
        /// The nop key shouldn't have any effects
        /// </summary>
        [Test]
        public void Process_Nop()
        {
            var old = _vimBuffer.TextSnapshot;
            foreach (var mode in _vimBuffer.AllModes)
            {
                _vimBuffer.SwitchMode(mode.ModeKind, ModeArgument.None);
                Assert.IsTrue(_vimBuffer.Process(VimKey.Nop));
                Assert.AreEqual(old, _vimBuffer.TextSnapshot);
            }
        }

        /// <summary>
        /// When we are InOneTimeCommand the HandledNeedMoreInput should not cause us to 
        /// do anything with respect to one time command
        /// </summary>
        [Test]
        public void Process_OneTimeCommand_NeedMoreInputDoesNothing()
        {
            var mode = CreateAndAddNormalMode(MockBehavior.Loose);
            mode.Setup(x => x.Process(It.IsAny<KeyInput>())).Returns(ProcessResult.HandledNeedMoreInput);
            _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            _vimBufferRaw.InOneTimeCommand = FSharpOption.Create(ModeKind.Replace);
            _vimBuffer.Process('c');
            Assert.IsTrue(_vimBufferRaw.InOneTimeCommand.IsSome(ModeKind.Replace));
        }

        /// <summary>
        /// Escape should go back to the original mode even if the current IMode doesn't
        /// support the escape key when we are in a one time command
        /// </summary>
        [Test]
        public void Process_OneTimeCommand_Escape()
        {
            var mode = CreateAndAddNormalMode(MockBehavior.Loose);
            mode.Setup(x => x.Process(It.IsAny<KeyInput>())).Returns(ProcessResult.Error);
            mode.Setup(x => x.CanProcess(It.IsAny<KeyInput>())).Returns(false);
            _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            _vimBufferRaw.InOneTimeCommand = FSharpOption.Create(ModeKind.Replace);
            _vimBuffer.Process(VimKey.Escape);
            Assert.AreEqual(ModeKind.Replace, _vimBuffer.ModeKind);
            Assert.IsTrue(_vimBufferRaw.InOneTimeCommand.IsNone());
        }

        /// <summary>
        /// When a command is completed in visual mode we shouldn't exit.  Else commands like
        /// 'l' would cause it to exit which is not the Vim behavior
        /// </summary>
        [Test]
        public void Process_OneTimeCommand_VisualMode_Handled()
        {
            var mode = CreateAndAddVisualLineMode(MockBehavior.Loose);
            mode.Setup(x => x.Process(It.IsAny<KeyInput>())).Returns(ProcessResult.NewHandled(ModeSwitch.NoSwitch));
            _vimBuffer.SwitchMode(ModeKind.VisualLine, ModeArgument.None);
            _vimBufferRaw.InOneTimeCommand = FSharpOption.Create(ModeKind.Replace);
            _vimBuffer.Process('l');
            Assert.AreEqual(ModeKind.VisualLine, _vimBuffer.ModeKind);
            Assert.IsTrue(_vimBufferRaw.InOneTimeCommand.Is(ModeKind.Replace));
        }

        /// <summary>
        /// Switch previous mode should still cause it to go back to the original though
        /// </summary>
        [Test]
        public void Process_OneTimeCommand_VisualMode_SwitchPreviousMode()
        {
            var mode = CreateAndAddVisualLineMode(MockBehavior.Loose);
            mode.Setup(x => x.Process(It.IsAny<KeyInput>())).Returns(ProcessResult.NewHandled(ModeSwitch.SwitchPreviousMode));
            _vimBuffer.SwitchMode(ModeKind.VisualLine, ModeArgument.None);
            _vimBufferRaw.InOneTimeCommand = FSharpOption.Create(ModeKind.Replace);
            _vimBuffer.Process('l');
            Assert.AreEqual(ModeKind.Replace, _vimBuffer.ModeKind);
            Assert.IsTrue(_vimBufferRaw.InOneTimeCommand.IsNone());
        }

        /// <summary>
        /// Ensure the mode sees the mapped KeyInput value
        /// </summary>
        [Test]
        public void Remap_OneToOne()
        {
            _keyMap.MapWithNoRemap("a", "l", KeyRemapMode.Normal);
            _textView.SetText("cat dog", 0);
            _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            _vimBuffer.Process('a');
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// When a single key is mapped to multiple both need to be passed onto the 
        /// IMode instance
        /// </summary>
        [Test]
        public void Remap_OneToMany()
        {
            _keyMap.MapWithNoRemap("a", "dw", KeyRemapMode.Normal);
            _textView.SetText("cat dog", 0);
            _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            _vimBuffer.Process('a');
            Assert.AreEqual("dog", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// Don't use mappings for the wrong IMode
        /// </summary>
        [Test]
        public void Remap_WrongMode()
        {
            _keyMap.MapWithNoRemap("l", "dw", KeyRemapMode.Insert);
            _textView.SetText("cat dog", 0);
            _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            _vimBuffer.Process('l');
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// When INormalMode is in OperatorPending we need to use operating pending 
        /// remapping
        /// </summary>
        [Test]
        public void Remap_OperatorPending()
        {
            _keyMap.MapWithNoRemap("z", "w", KeyRemapMode.OperatorPending);
            _textView.SetText("cat dog", 0);
            _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            _vimBuffer.Process("d");
            Assert.IsTrue(_vimBuffer.NormalMode.KeyRemapMode.IsOperatorPending);
            _vimBuffer.Process("z");
            Assert.AreEqual("dog", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// Recursive mappings should print out an error message when used
        /// </summary>
        [Test]
        public void Remap_Recursive()
        {
            _keyMap.MapWithRemap("a", "b", KeyRemapMode.Normal);
            _keyMap.MapWithRemap("b", "a", KeyRemapMode.Normal);
            var didRun = false;
            _vimBuffer.ErrorMessage +=
                (notUsed, msg) =>
                {
                    Assert.AreEqual(Resources.Vim_RecursiveMapping, msg);
                    didRun = true;
                };
            _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            _vimBuffer.Process('a');
            Assert.IsTrue(didRun);
        }

        /// <summary>
        /// When we buffer input and fail to find a mapping every KeyInput value
        /// should be passed to the IMode
        /// </summary>
        [Test]
        public void Remap_BufferedFailed()
        {
            _keyMap.MapWithNoRemap("do", "cat", KeyRemapMode.Normal);
            _textView.SetText("cat dog", 0);
            _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            _vimBuffer.Process("d");
            Assert.AreEqual('d', _vimBuffer.BufferedRemapKeyInputs.Head.Char);
            _vimBuffer.Process("w");
            Assert.AreEqual("dog", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// Make sure the KeyInput is passed down to the IMode
        /// </summary>
        [Test]
        public void CanProcess_Simple()
        {
            var keyInput = KeyInputUtil.CharToKeyInput('c');
            var normal = CreateAndAddNormalMode(MockBehavior.Loose);
            normal.Setup(x => x.CanProcess(keyInput)).Returns(true).Verifiable();
            _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            Assert.IsTrue(_vimBuffer.CanProcess(keyInput));
            normal.Verify();
        }

        /// <summary>
        /// Make sure the mapped KeyInput is passed down to the IMode
        /// </summary>
        [Test]
        public void CanProcess_Mapped()
        {
            _keyMap.MapWithRemap("a", "c", KeyRemapMode.Normal);
            var keyInput = KeyInputUtil.CharToKeyInput('c');
            var normal = CreateAndAddNormalMode(MockBehavior.Loose);
            normal.Setup(x => x.CanProcess(keyInput)).Returns(true).Verifiable();
            _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            Assert.IsTrue(_vimBuffer.CanProcess('a'));
            normal.Verify();
        }

        /// <summary>
        /// When there is buffered input due to a key mapping make sure that 
        /// we consider the final mapped input for processing and not the immediate
        /// KeyInput value
        /// </summary>
        [Test]
        public void CanProcess_BufferedInput()
        {
            _keyMap.MapWithRemap("la", "iexample", KeyRemapMode.Normal);
            _textView.SetText("dog cat");
            _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);

            // <F4> is not a valid command
            Assert.IsFalse(_vimBuffer.CanProcess(VimKey.F4));
            _vimBuffer.Process("l");
            Assert.IsFalse(_vimBuffer.BufferedRemapKeyInputs.IsEmpty);

            // Is is still not a valid command but when mapping is considered it will
            // expand to l<F4> and l is a valid command
            Assert.IsTrue(_vimBuffer.CanProcess(VimKey.F4));
        }

        /// <summary>
        /// The buffer can always process a nop key and should take no action when it's
        /// encountered
        /// </summary>
        [Test]
        public void CanProcess_Nop()
        {
            foreach (var mode in _vimBuffer.AllModes)
            {
                _vimBuffer.SwitchMode(mode.ModeKind, ModeArgument.None);
                Assert.IsTrue(_vimBuffer.CanProcess(VimKey.Nop));
            }
        }

        /// <summary>
        /// Make sure that commands like 'a' are still considered commands when the
        /// IVimBuffer is in normal mode
        /// </summary>
        [Test]
        public void CanProcessAsCommand_Command()
        {
            _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            Assert.IsTrue(_vimBuffer.CanProcessAsCommand('a'));
        }

        /// <summary>
        /// Make sure that commands like 'a' are not considered commands when in 
        /// insert mode
        /// </summary>
        [Test]
        public void CanProcessAsCommand_InsertMode()
        {
            _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            Assert.IsFalse(_vimBuffer.CanProcessAsCommand('a'));
        }

        /// <summary>
        /// Make sure that commands like 'a' are not considered commands when in 
        /// replace mode
        /// </summary>
        [Test]
        public void CanProcessAsCommand_ReplaceMode()
        {
            _vimBuffer.SwitchMode(ModeKind.Replace, ModeArgument.None);
            Assert.IsFalse(_vimBuffer.CanProcessAsCommand('a'));
        }

        /// <summary>
        /// The IVimBuffer should be removed from IVim on close
        /// </summary>
        [Test]
        public void Closed_BufferShouldBeRemoved()
        {
            var didSee = false;
            _vimBuffer.Closed += delegate { didSee = true; };
            _vimBuffer.Close();
            Assert.IsTrue(didSee);
        }

        /// <summary>
        /// Double close should throw
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void Closed_DoubleClose()
        {
            _vimBuffer.Close();
            _vimBuffer.Close();
        }

        /// <summary>
        /// Make sure the event is true while processing input
        /// </summary>
        [Test]
        public void IsProcessing_Basic()
        {
            var didRun = false;
            Assert.IsFalse(_vimBuffer.IsProcessingInput);
            _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            _textView.TextBuffer.Changed +=
                delegate
                {
                    Assert.IsTrue(_vimBuffer.IsProcessingInput);
                    didRun = true;
                };
            _vimBuffer.Process("h");   // Changing text will raise Changed
            Assert.IsFalse(_vimBuffer.IsProcessingInput);
            Assert.IsTrue(didRun);
        }

        /// <summary>
        /// Make sure the event properly resets while recursively processing 
        /// input
        /// </summary>
        [Test]
        public void IsProcessing_Recursive()
        {
            var didRun = false;
            var isFirst = true;
            Assert.IsFalse(_vimBuffer.IsProcessingInput);
            _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            _textView.TextBuffer.Changed +=
                delegate
                {
                    if (isFirst)
                    {
                        isFirst = false;
                        _vimBuffer.Process('o');
                    }
                    Assert.IsTrue(_vimBuffer.IsProcessingInput);
                    didRun = true;
                };
            _vimBuffer.Process("h");   // Changing text will raise Changed
            Assert.IsFalse(_vimBuffer.IsProcessingInput);
            Assert.IsTrue(didRun);
        }

        /// <summary>
        /// Ensure the key simulation raises the appropriate key APIs
        /// </summary>
        [Test]
        public void SimulateProcessed_RaiseEvent()
        {
            var ranStart = false;
            var ranProcessed = false;
            var ranEnd = false;
            _vimBuffer.KeyInputStart += delegate { ranStart = true; };
            _vimBuffer.KeyInputProcessed += delegate { ranProcessed = true; };
            _vimBuffer.KeyInputEnd += delegate { ranEnd = true; };
            _vimBuffer.SimulateProcessed(KeyInputUtil.CharToKeyInput('c'));
            Assert.IsTrue(ranStart);
            Assert.IsTrue(ranEnd);
            Assert.IsTrue(ranProcessed);
        }

        /// <summary>
        /// Ensure the SimulateProcessed API doesn't go through key remapping.  The caller
        /// who wants the simulated input is declaring the literal KeyInput was processed
        /// </summary>
        [Test]
        public void SimulateProcessed_DontMap()
        {
            _vimBuffer.Vim.KeyMap.MapWithNoRemap("a", "b", KeyRemapMode.Normal);
            _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            var ranProcessed = false;
            _vimBuffer.KeyInputProcessed +=
                (unused, tuple) =>
                {
                    Assert.AreEqual(KeyInputUtil.CharToKeyInput('a'), tuple.Item1);
                    ranProcessed = true;
                };
            _vimBuffer.SimulateProcessed(KeyInputUtil.CharToKeyInput('a'));
            Assert.IsTrue(ranProcessed);
        }

        /// <summary>
        /// When input is simulated it should clear any existing buffered KeyInput 
        /// values.  The caller who simulates the input is responsible for understanding
        /// and ignoring buffered input values
        /// </summary>
        [Test]
        public void SimulateProcessed_ClearBufferedInput()
        {
            _vimBuffer.Vim.KeyMap.MapWithNoRemap("jj", "b", KeyRemapMode.Normal);
            _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            _vimBuffer.Process('j');
            Assert.IsFalse(_vimBuffer.BufferedRemapKeyInputs.IsEmpty);
            _vimBuffer.SimulateProcessed(KeyInputUtil.CharToKeyInput('a'));
            Assert.IsTrue(_vimBuffer.BufferedRemapKeyInputs.IsEmpty);
        }
    }
}
