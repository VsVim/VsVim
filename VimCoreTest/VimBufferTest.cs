using System;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    [TestFixture]
    public sealed class VimBufferTest : VimTestBase
    {
        private ITextView _textView;
        private VimBuffer _bufferRaw;
        private IVimBuffer _buffer;
        private MockRepository _factory;
        private IKeyMap _keyMap;

        [SetUp]
        public void Setup()
        {
            _textView = EditorUtil.CreateTextView("here we go");
            _textView.MoveCaretTo(0);
            _buffer = EditorUtil.FactoryService.Vim.CreateBuffer(_textView);
            _buffer.SwitchMode(ModeKind.Command, ModeArgument.None);
            _keyMap = _buffer.Vim.KeyMap;
            _bufferRaw = (VimBuffer)_buffer;
            _factory = new MockRepository(MockBehavior.Strict);
        }

        private Mock<INormalMode> CreateAndAddNormalMode(MockBehavior behavior = MockBehavior.Strict)
        {
            var mode = _factory.Create<INormalMode>(behavior);
            mode.SetupGet(x => x.ModeKind).Returns(ModeKind.Normal);
            mode.SetupGet(x => x.KeyRemapMode).Returns(KeyRemapMode.Normal);
            _bufferRaw.RemoveMode(_bufferRaw.NormalMode);
            _bufferRaw.AddMode(mode.Object);
            return mode;
        }

        private Mock<IInsertMode> CreateAndAddInsertMode(MockBehavior behavior = MockBehavior.Strict)
        {
            var mode = _factory.Create<IInsertMode>(behavior);
            mode.SetupGet(x => x.ModeKind).Returns(ModeKind.Insert);
            _bufferRaw.RemoveMode(_buffer.InsertMode);
            _bufferRaw.AddMode(mode.Object);
            return mode;
        }

        /// <summary>
        /// Make sure the SwitchdMode event fires when switching modes.
        /// </summary>
        [Test]
        public void SwitchedMode_Event()
        {
            var ran = false;
            _buffer.SwitchedMode += (s, m) => { ran = true; };
            _buffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
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
            _buffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            _buffer.SwitchedMode += (s, m) => { ran = true; };
            _buffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
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
            _buffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            normal.Verify();
            normal.Setup(x => x.OnLeave());
            insert.Setup(x => x.OnEnter(ModeArgument.None));
            _buffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            normal.Verify();
            insert.Verify();

            insert.Setup(x => x.OnLeave()).Verifiable();
            var prev = _buffer.SwitchPreviousMode();
            Assert.AreSame(normal.Object, prev);
            insert.Verify();
        }

        /// <summary>
        /// SwitchPreviousMode should raise the SwitchedMode event
        /// </summary>
        [Test, Description("SwitchPreviousMode should raise the SwitchedMode event")]
        public void SwitchPreviousMode2()
        {
            _buffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            var ran = false;
            _buffer.SwitchedMode += (s, m) => { ran = true; };
            _buffer.SwitchPreviousMode();
            Assert.IsTrue(ran);
        }

        /// <summary>
        /// Make sure the processed event is raised during a key process
        /// </summary>
        [Test]
        public void KeyInputEvents_Processed()
        {
            _buffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            _textView.SetText("hello world");

            var ran = false;
            _buffer.KeyInputProcessed += delegate { ran = true; };
            _buffer.Process('l');
            Assert.IsTrue(ran);
        }

        /// <summary>
        /// Make sure the events are raised in order
        /// </summary>
        [Test]
        public void KeyInputEvents_InOrder()
        {
            _buffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            _textView.SetText("hello world");

            var start = false;
            var processed = false;
            var end = false;
            _buffer.KeyInputStart +=
                (b, keyInput) =>
                {
                    Assert.AreEqual('l', keyInput.Char);
                    Assert.IsFalse(processed || end);
                    start = true;
                };
            _buffer.KeyInputProcessed +=
                (b, tuple) =>
                {
                    var keyInput = tuple.Item1;
                    Assert.AreEqual('l', keyInput.Char);
                    Assert.IsTrue(start && !end);
                    processed = true;
                };
            _buffer.KeyInputEnd +=
                (b, keyInput) =>
                {
                    Assert.AreEqual('l', keyInput.Char);
                    Assert.IsTrue(start && processed);
                    end = true;
                };
            _buffer.Process('l');
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
            _buffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            _textView.SetText("hello world");

            var start = false;
            var end = false;
            _buffer.KeyInputStart +=
                (b, keyInput) =>
                {
                    Assert.AreEqual('l', keyInput.Char);
                    Assert.IsTrue(!end);
                    start = true;
                };
            _buffer.KeyInputEnd +=
                (b, keyInput) =>
                {
                    Assert.AreEqual('l', keyInput.Char);
                    Assert.IsTrue(start);
                    end = true;
                };
            var caught = false;
            try
            {
                _buffer.Process('l');
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
            _buffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            _buffer.Vim.KeyMap.MapWithNoRemap("lad", "rad", KeyRemapMode.Normal);
            _textView.SetText("hello world");

            var start = false;
            var processed = false;
            var end = false;
            var buffered = false;
            _buffer.KeyInputStart +=
                (b, keyInput) =>
                {
                    Assert.AreEqual('l', keyInput.Char);
                    Assert.IsTrue(!buffered && !end);
                    start = true;
                };
            _buffer.KeyInputEnd +=
                (b, keyInput) =>
                {
                    Assert.AreEqual('l', keyInput.Char);
                    Assert.IsTrue(start && buffered);
                    end = true;
                };
            _buffer.KeyInputBuffered +=
                (b, keyInput) =>
                {
                    Assert.AreEqual('l', keyInput.Char);
                    Assert.IsTrue(start && !end);
                    buffered = true;
                };
            _buffer.KeyInputProcessed += delegate { processed = true; };
            _buffer.Process('l');
            Assert.IsTrue(start && buffered && end && !processed);
        }

        /// <summary>
        /// Close should call OnLeave and OnClose for the active IMode
        /// </summary>
        [Test]
        public void Close_ShouldCallLeaveAndClose()
        {
            var normal = CreateAndAddNormalMode(MockBehavior.Loose);
            _buffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            normal.Setup(x => x.OnLeave()).Verifiable();
            normal.Setup(x => x.OnClose()).Verifiable();
            _buffer.Close();
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
            _buffer.Close();
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
            _buffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            _buffer.Process('l');
            Assert.AreEqual(ModeKind.Command, _buffer.ModeKind);
        }

        /// <summary>
        /// Ensure the mode sees the mapped KeyInput value
        /// </summary>
        [Test]
        public void Remap_OneToOne()
        {
            _keyMap.MapWithNoRemap("a", "l", KeyRemapMode.Normal);
            _textView.SetText("cat dog", 0);
            _buffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            _buffer.Process('a');
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
            _buffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            _buffer.Process('a');
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
            _buffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            _buffer.Process('l');
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
            _buffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            _buffer.Process("d");
            Assert.IsTrue(_buffer.NormalMode.KeyRemapMode.IsOperatorPending);
            _buffer.Process("z");
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
            _buffer.ErrorMessage +=
                (notUsed, msg) =>
                {
                    Assert.AreEqual(Resources.Vim_RecursiveMapping, msg);
                    didRun = true;
                };
            _buffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            _buffer.Process('a');
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
            _buffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            _buffer.Process("d");
            Assert.AreEqual('d', _buffer.BufferedRemapKeyInputs.Head.Char);
            _buffer.Process("w");
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
            _buffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            Assert.IsTrue(_buffer.CanProcess(keyInput));
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
            _buffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            Assert.IsTrue(_buffer.CanProcess('a'));
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
            _buffer.SwitchMode(ModeKind.Normal, ModeArgument.None);

            // <F4> is not a valid command
            Assert.IsFalse(_buffer.CanProcess(VimKey.F4));
            _buffer.Process("l");
            Assert.IsFalse(_buffer.BufferedRemapKeyInputs.IsEmpty);

            // Is is still not a valid command but when mapping is considered it will
            // expand to l<F4> and l is a valid command
            Assert.IsTrue(_buffer.CanProcess(VimKey.F4));
        }

        /// <summary>
        /// Make sure that commands like 'a' are still considered commands when the
        /// IVimBuffer is in normal mode
        /// </summary>
        [Test]
        public void CanProcessAsCommand_Command()
        {
            _buffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            Assert.IsTrue(_buffer.CanProcessAsCommand('a'));
        }

        /// <summary>
        /// Make sure that commands like 'a' are not considered commands when in 
        /// insert mode
        /// </summary>
        [Test]
        public void CanProcessAsCommand_InsertMode()
        {
            _buffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            Assert.IsFalse(_buffer.CanProcessAsCommand('a'));
        }

        /// <summary>
        /// Make sure that commands like 'a' are not considered commands when in 
        /// replace mode
        /// </summary>
        [Test]
        public void CanProcessAsCommand_ReplaceMode()
        {
            _buffer.SwitchMode(ModeKind.Replace, ModeArgument.None);
            Assert.IsFalse(_buffer.CanProcessAsCommand('a'));
        }

        /// <summary>
        /// The IVimBuffer should be removed from IVim on close
        /// </summary>
        [Test]
        public void Closed_BufferShouldBeRemoved()
        {
            var didSee = false;
            _buffer.Closed += delegate { didSee = true; };
            _buffer.Close();
            Assert.IsTrue(didSee);
        }

        /// <summary>
        /// Double close should throw
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void Closed_DoubleClose()
        {
            _buffer.Close();
            _buffer.Close();
        }

        /// <summary>
        /// Make sure the event is true while processing input
        /// </summary>
        [Test]
        public void IsProcessing_Basic()
        {
            var didRun = false;
            Assert.IsFalse(_buffer.IsProcessingInput);
            _buffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            _textView.TextBuffer.Changed +=
                delegate
                {
                    Assert.IsTrue(_buffer.IsProcessingInput);
                    didRun = true;
                };
            _buffer.Process("h");   // Changing text will raise Changed
            Assert.IsFalse(_buffer.IsProcessingInput);
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
            Assert.IsFalse(_buffer.IsProcessingInput);
            _buffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            _textView.TextBuffer.Changed +=
                delegate
                {
                    if (isFirst)
                    {
                        isFirst = false;
                        _buffer.Process('o');
                    }
                    Assert.IsTrue(_buffer.IsProcessingInput);
                    didRun = true;
                };
            _buffer.Process("h");   // Changing text will raise Changed
            Assert.IsFalse(_buffer.IsProcessingInput);
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
            _buffer.KeyInputStart += delegate { ranStart = true; };
            _buffer.KeyInputProcessed += delegate { ranProcessed = true; };
            _buffer.KeyInputEnd += delegate { ranEnd = true; };
            _buffer.SimulateProcessed(KeyInputUtil.CharToKeyInput('c'));
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
            _buffer.Vim.KeyMap.MapWithNoRemap("a", "b", KeyRemapMode.Normal);
            _buffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            var ranProcessed = false;
            _buffer.KeyInputProcessed +=
                (unused, tuple) =>
                {
                    Assert.AreEqual(KeyInputUtil.CharToKeyInput('a'), tuple.Item1);
                    ranProcessed = true;
                };
            _buffer.SimulateProcessed(KeyInputUtil.CharToKeyInput('a'));
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
            _buffer.Vim.KeyMap.MapWithNoRemap("jj", "b", KeyRemapMode.Normal);
            _buffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            _buffer.Process('j');
            Assert.IsFalse(_buffer.BufferedRemapKeyInputs.IsEmpty);
            _buffer.SimulateProcessed(KeyInputUtil.CharToKeyInput('a'));
            Assert.IsTrue(_buffer.BufferedRemapKeyInputs.IsEmpty);
        }
    }
}
