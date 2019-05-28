using System;
using Vim.EditorHost;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Vim.Extensions;
using Vim.Interpreter;
using Xunit;

namespace Vim.UnitTest
{
    public abstract class VimBufferTest : VimTestBase
    {
        private ITextView _textView;
        private VimBuffer _vimBufferRaw;
        private IVimBuffer _vimBuffer;
        private MockRepository _factory;
        private IKeyMap _keyMap;

        public VimBufferTest()
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
            mode.SetupGet(x => x.InCount).Returns(false);
            mode.Setup(x => x.OnLeave());
            mode.Setup(x => x.OnClose());
            _vimBufferRaw.RemoveMode(_vimBufferRaw.NormalMode);
            _vimBufferRaw.AddMode(mode.Object);
            return mode;
        }

        private Mock<IInsertMode> CreateAndAddInsertMode(MockBehavior behavior = MockBehavior.Strict)
        {
            var mode = _factory.Create<IInsertMode>(behavior);
            mode.SetupGet(x => x.ModeKind).Returns(ModeKind.Insert);
            mode.Setup(x => x.OnLeave());
            mode.Setup(x => x.OnClose());
            _vimBufferRaw.RemoveMode(_vimBuffer.InsertMode);
            _vimBufferRaw.AddMode(mode.Object);
            return mode;
        }

        private Mock<IVisualMode> CreateAndAddVisualLineMode(MockBehavior behavior = MockBehavior.Strict)
        {
            var mode = _factory.Create<IVisualMode>(behavior);
            mode.SetupGet(x => x.ModeKind).Returns(ModeKind.VisualLine);
            mode.SetupGet(x => x.KeyRemapMode).Returns(KeyRemapMode.Visual);
            mode.Setup(x => x.OnLeave());
            mode.Setup(x => x.OnClose());
            _vimBufferRaw.RemoveMode(_vimBuffer.VisualLineMode);
            _vimBufferRaw.AddMode(mode.Object);
            return mode;
        }

        public sealed class KeyInputTest : VimBufferTest
        {
            public KeyInputTest()
            {
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            }

            /// <summary>
            /// Make sure the processed event is raised during a key process
            /// </summary>
            [WpfFact]
            public void ProcessedFires()
            {
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                _textView.SetText("hello world");

                var ran = false;
                _vimBuffer.KeyInputProcessed += delegate { ran = true; };
                _vimBuffer.Process('l');
                Assert.True(ran);
            }

            /// <summary>
            /// Make sure the events are raised in order
            /// </summary>
            [WpfFact]
            public void EventOrderForNormal()
            {
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                _textView.SetText("hello world");

                var start = false;
                var processed = false;
                var end = false;
                _vimBuffer.KeyInputStart +=
                    (b, args) =>
                    {
                        Assert.Equal('l', args.KeyInput.Char);
                        Assert.False(processed || end);
                        start = true;
                    };
                _vimBuffer.KeyInputProcessed +=
                    (b, args) =>
                    {
                        var keyInput = args.KeyInput;
                        Assert.Equal('l', keyInput.Char);
                        Assert.True(start && !end);
                        processed = true;
                    };
                _vimBuffer.KeyInputEnd +=
                    (b, args) =>
                    {
                        Assert.Equal('l', args.KeyInput.Char);
                        Assert.True(start && processed);
                        end = true;
                    };
                _vimBuffer.Process('l');
                Assert.True(start && processed && end);
            }

            /// <summary>
            /// Start and End events should fire even if there is an exception thrown
            /// </summary>
            [WpfFact]
            public void ExceptionDuringProcessing()
            {
                var normal = CreateAndAddNormalMode(MockBehavior.Loose);
                normal.Setup(x => x.Process(It.IsAny<KeyInputData>())).Throws(new Exception());
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                _textView.SetText("hello world");

                var start = false;
                var end = false;
                _vimBuffer.KeyInputStart +=
                    (b, args) =>
                    {
                        Assert.Equal('l', args.KeyInput.Char);
                        Assert.True(!end);
                        start = true;
                    };
                _vimBuffer.KeyInputEnd +=
                    (b, args) =>
                    {
                        Assert.Equal('l', args.KeyInput.Char);
                        Assert.True(start);
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
                Assert.True(start && end && caught);
            }

            /// <summary>
            /// Start, Buffered and End should fire if the KeyInput is buffered due to 
            /// a mapping
            /// </summary>
            [WpfFact]
            public void BufferedInput()
            {
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                _vimBuffer.Vim.KeyMap.MapWithNoRemap("lad", "rad", KeyRemapMode.Normal);
                _textView.SetText("hello world");

                var start = false;
                var processed = false;
                var end = false;
                var buffered = false;
                _vimBuffer.KeyInputStart +=
                    (b, args) =>
                    {
                        Assert.Equal('l', args.KeyInput.Char);
                        Assert.True(!buffered && !end);
                        start = true;
                    };
                _vimBuffer.KeyInputEnd +=
                    (b, args) =>
                    {
                        Assert.Equal('l', args.KeyInput.Char);
                        Assert.True(start && buffered);
                        end = true;
                    };
                _vimBuffer.KeyInputBuffered +=
                    (b, args) =>
                    {
                        Assert.Equal('l', args.KeyInputSet.FirstKeyInput.Value.Char);
                        Assert.True(start && !end);
                        buffered = true;
                    };
                _vimBuffer.KeyInputProcessed += delegate { processed = true; };
                _vimBuffer.Process('l');
                Assert.True(start && buffered && end && !processed);
            }

            /// <summary>
            /// When KeyInputStart is handled we still need to fire the other 3 events (Processing, Processed and End) in the 
            /// proper order.  The naiive consumer should see this is a normal event sequence
            /// </summary>
            [WpfFact]
            public void KeyInputStartHandled()
            {
                var count = 0;
                _vimBuffer.KeyInputStart +=
                    (sender, e) =>
                    {
                        Assert.Equal('c', e.KeyInput.Char);
                        Assert.Equal(0, count);
                        count++;
                        e.Handled = true;
                    };
                _vimBuffer.KeyInputProcessing +=
                    (sender, e) =>
                    {
                        Assert.Equal('c', e.KeyInput.Char);
                        Assert.Equal(1, count);
                        Assert.True(e.Handled);
                        count++;
                    };
                _vimBuffer.KeyInputProcessed +=
                    (sender, e) =>
                    {
                        Assert.Equal('c', e.KeyInput.Char);
                        Assert.Equal(2, count);
                        count++;
                    };
                _vimBuffer.KeyInputEnd +=
                    (sender, e) =>
                    {
                        Assert.Equal('c', e.KeyInput.Char);
                        Assert.Equal(3, count);
                        count++;
                    };
                _vimBuffer.Process('c');
                Assert.Equal(4, count);
            }

            /// <summary>
            /// When KeyInputProcessing is handled we still need to fire the other 2 events (Processed and End) in the 
            /// proper order.  The naiive consumer should see this is a normal event sequence
            /// </summary>
            [WpfFact]
            public void KeyInputProcessingHandled()
            {
                var count = 0;
                _vimBuffer.KeyInputProcessing +=
                    (sender, e) =>
                    {
                        Assert.Equal('c', e.KeyInput.Char);
                        Assert.Equal(0, count);
                        e.Handled = true;
                        count++;
                    };
                _vimBuffer.KeyInputProcessed +=
                    (sender, e) =>
                    {
                        Assert.Equal('c', e.KeyInput.Char);
                        Assert.Equal(1, count);
                        count++;
                    };
                _vimBuffer.KeyInputEnd +=
                    (sender, e) =>
                    {
                        Assert.Equal('c', e.KeyInput.Char);
                        Assert.Equal(2, count);
                        count++;
                    };
                _vimBuffer.Process('c');
                Assert.Equal(3, count);
            }

            /// <summary>
            /// The start and end events shouldn't consider any mappings.  They should display the key
            /// which was actually pressed.  The Processing events though should consider the mappings
            /// </summary>
            [WpfFact]
            public void Mappings()
            {
                var seen = 0;
                _vimBuffer.ProcessNotation(":map a b", enter: true);
                _vimBuffer.KeyInputStart +=
                    (sender, e) =>
                    {
                        Assert.Equal('a', e.KeyInput.Char);
                        seen++;
                    };
                _vimBuffer.KeyInputEnd +=
                    (sender, e) =>
                    {
                        Assert.Equal('a', e.KeyInput.Char);
                        seen++;
                    };
                _vimBuffer.KeyInputProcessing +=
                    (sender, e) =>
                    {
                        Assert.Equal('b', e.KeyInput.Char);
                        seen++;
                    };
                _vimBuffer.KeyInputProcessed +=
                    (sender, e) =>
                    {
                        Assert.Equal('b', e.KeyInput.Char);
                        seen++;
                    };
                _vimBuffer.ProcessNotation("a");
                Assert.Equal(4, seen);
            }
        }

        public sealed class CloseTest : VimBufferTest
        {
            /// <summary>
            /// Close should call OnLeave and OnClose for the active IMode
            /// </summary>
            [WpfFact]
            public void ShouldCallLeaveAndClose()
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
            [WpfFact]
            public void CallCloseOnAll()
            {
                var insert = CreateAndAddInsertMode();
                insert.Setup(x => x.OnClose()).Verifiable();
                _vimBuffer.Close();
                insert.Verify();
            }

            /// <summary>
            /// The IVimBuffer should be removed from IVim on close
            /// </summary>
            [WpfFact]
            public void BufferShouldBeRemoved()
            {
                var didSee = false;
                _vimBuffer.Closed += delegate { didSee = true; };
                _vimBuffer.Close();
                Assert.True(didSee);
            }

            /// <summary>
            /// Closing the buffer while not processing input should raise PostClosed event.
            /// </summary>
            [WpfFact]
            public void ExternalCloseShouldRaisePostClosed()
            {
                var count = 0;
                _vimBuffer.PostClosed += delegate { count++; };
                _vimBuffer.Close();
                Assert.Equal(1, count);
            }
            
            /// <summary>
            /// Closing the buffer while processing input should also raise PostClosed event.
            /// </summary>
            [WpfFact]
            public void ProcessCloseCommandShouldRaisePostClosed()
            {
                var count = 0;
                _vimBuffer.PostClosed += delegate { count++; };

                var normal = CreateAndAddNormalMode(MockBehavior.Loose);

                int keyCount = 0;
                normal.Setup(x => x.Process(It.IsAny<KeyInputData>()))
                      .Callback(() =>
                                {
                                    if (keyCount == 0)
                                    {
                                        keyCount = 1;
                                        _vimBuffer.Process("Q");
                                        Assert.Equal(0, count);
                                    }
                                    else
                                        _vimBuffer.Close();
                                })
                      .Returns(ProcessResult.NewHandled(ModeSwitch.NoSwitch));
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                _vimBuffer.Process("Q");
                Assert.Equal(1, count);
            }
            
            /// <summary>
            /// Closing the buffer while processing buffered key input should raise PostClosed event.
            /// </summary>
            [WpfFact]
            public void ProcessBufferedCloseCommandShouldRaisePostClosed()
            {
                var count = 0;
                _vimBuffer.PostClosed += delegate { count++; };

                var normal = CreateAndAddNormalMode(MockBehavior.Loose);
                normal.Setup(x => x.Process(It.Is<KeyInputData>(k => k.KeyInput.Equals(KeyInputUtil.CharToKeyInput('A')))))
                      .Callback(() => { _vimBuffer.Close(); })
                      .Returns(ProcessResult.NewHandled(ModeSwitch.NoSwitch));
                
                _vimBuffer.Vim.KeyMap.MapWithNoRemap("Q", "A", KeyRemapMode.Normal);
                _vimBuffer.Vim.KeyMap.MapWithNoRemap("QQ", "B", KeyRemapMode.Normal);
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                
                _vimBuffer.Process("Q");
                Assert.Equal(0, count);
                _vimBuffer.ProcessBufferedKeyInputs();
                Assert.Equal(1, count);
            }
            
            /// <summary>
            /// Double close should throw
            /// </summary>
            [WpfFact]
            public void DoubleClose()
            {
                _vimBuffer.Close();
                Assert.Throws<InvalidOperationException>(() => _vimBuffer.Close());
            }

            [WpfFact]
            public void CloseEventOrder()
            {
                var count = 0;
                _vimBuffer.Closing +=
                    delegate
                    {
                        Assert.False(_vimBuffer.IsClosed);
                        Assert.Equal(0, count);
                        count++;
                    };
                _vimBuffer.Closed +=
                    delegate
                    {
                        Assert.Equal(1, count);
                        count++;
                    };
                _vimBuffer.Close();
                Assert.Equal(2, count);
            }

            /// <summary>
            /// An exception in closing should be ignored.  
            /// </summary>
            [WpfFact]
            public void ExceptionInClosing()
            {
                var count = 0;
                _vimBuffer.Closing +=
                    delegate
                    {
                        Assert.False(_vimBuffer.IsClosed);
                        Assert.Equal(0, count);
                        count++;
                        throw new Exception();
                    };
                _vimBuffer.Closed +=
                    delegate
                    {
                        Assert.Equal(1, count);
                        count++;
                    };
                _vimBuffer.Close();
                Assert.Equal(2, count);
            }
        }
        
        public class SpecialMarks : ClosingSetsLastEditedPositionMark
        {
            [WpfFact]
            public void SpecialMarksAreSet()
            {
                var s_emptyList = FSharpList<Mark>.Empty;
                OpenFakeVimBufferTestWindow("");
                var interpreter = new VimInterpreter(
                    _vimBuffer,
                    CommonOperationsFactory.GetCommonOperations(_vimBufferData),
                    FoldManagerFactory.GetFoldManager(_vimBufferData.TextView),
                    new FileSystem(),
                    BufferTrackingService);
                _vimBuffer.ProcessNotation("<ESC>i1<CR>2<CR>3<CR>4<CR>5<CR>6<CR>7<CR>8<CR>9<CR>0<ESC>");
                interpreter.RunDisplayMarks(s_emptyList);
                var expectedMarks = new[] {
                    @"mark line  col file/text",
                    @" '      1    0 VimBufferTest.cs",
                   @" ""      1    0 VimBufferTest.cs",
                    @" [      1    0 VimBufferTest.cs",
                    @" ]     10    1 VimBufferTest.cs",
                    @" ^     10    1 VimBufferTest.cs",
                    @" .     10    0 VimBufferTest.cs",
                };
                Assert.Equal(string.Join(Environment.NewLine, expectedMarks), _statusUtil.LastStatus);

                // set an upper and lower mark
                _vimBuffer.ProcessNotation("kmzkmZ");

                _vimBuffer.ProcessNotation("1G");
                interpreter.RunDisplayMarks(s_emptyList);
                expectedMarks = new[] {
                    @"mark line  col file/text",
                    @" '      8    0 VimBufferTest.cs",
                    @" z      9    0 VimBufferTest.cs",
                    @" Z      8    0 VimBufferTest.cs",
                   @" ""      1    0 VimBufferTest.cs",
                    @" [      1    0 VimBufferTest.cs",
                    @" ]     10    1 VimBufferTest.cs",
                    @" ^     10    1 VimBufferTest.cs",
                    @" .     10    0 VimBufferTest.cs",
                };
                Assert.Equal(string.Join(Environment.NewLine, expectedMarks), _statusUtil.LastStatus);

                _vimBuffer.ProcessNotation("yy");
                interpreter.RunDisplayMarks(s_emptyList);
                expectedMarks = new[] {
                    @"mark line  col file/text",
                    @" '      8    0 VimBufferTest.cs",
                    @" z      9    0 VimBufferTest.cs",
                    @" Z      8    0 VimBufferTest.cs",
                   @" ""      1    0 VimBufferTest.cs",
                    @" [      1    0 VimBufferTest.cs",
                    @" ]      1    1 VimBufferTest.cs",
                    @" ^     10    1 VimBufferTest.cs",
                    @" .     10    0 VimBufferTest.cs",
                };
                Assert.Equal(string.Join(Environment.NewLine, expectedMarks), _statusUtil.LastStatus);

                // set an upper and lower mark
                _vimBuffer.ProcessNotation("jmajmA2k");

                _vimBuffer.ProcessNotation("5jp");
                interpreter.RunDisplayMarks(s_emptyList);
                expectedMarks = new[] {
                    @"mark line  col file/text",
                    @" '      9    0 VimBufferTest.cs",
                    @" a      2    0 VimBufferTest.cs",
                    @" z     10    0 VimBufferTest.cs",
                    @" A      3    0 VimBufferTest.cs",
                    @" Z      9    0 VimBufferTest.cs",
                   @" ""      1    0 VimBufferTest.cs",
                    @" [      7    0 VimBufferTest.cs",
                    @" ]      7    1 VimBufferTest.cs",
                    @" ^     11    1 VimBufferTest.cs",
                    @" .      7    1 VimBufferTest.cs",
                };
                Assert.Equal(string.Join(Environment.NewLine, expectedMarks), _statusUtil.LastStatus);

                _vimBuffer.ProcessNotation("kV<ESC>");
                interpreter.RunDisplayMarks(s_emptyList);

                expectedMarks = new[] {
                    @"mark line  col file/text",
                    @" '      9    0 VimBufferTest.cs",
                    @" a      2    0 VimBufferTest.cs",
                    @" z     10    0 VimBufferTest.cs",
                    @" A      3    0 VimBufferTest.cs",
                    @" Z      9    0 VimBufferTest.cs",
                   @" ""      1    0 VimBufferTest.cs",
                    @" [      7    0 VimBufferTest.cs",
                    @" ]      7    1 VimBufferTest.cs",
                    @" ^     11    1 VimBufferTest.cs",
                    @" .      7    1 VimBufferTest.cs",
                    @" <      6    0 VimBufferTest.cs",
                    @" >      6    1 VimBufferTest.cs",
                };
                Assert.Equal(string.Join(Environment.NewLine, expectedMarks), _statusUtil.LastStatus);
            }
        }

        public class ClosingSetsLastEditedPositionMark : VimBufferTest
        {
            protected TestableStatusUtil _statusUtil = new TestableStatusUtil();
            protected IVimBufferData _vimBufferData;

            public ClosingSetsLastEditedPositionMark()
            {
                OpenFakeVimBufferTestWindow();
                _vimBuffer.MarkMap.UnloadBuffer(_vimBufferData, "VimBufferTest.cs", 0, 0);
            }

            protected void OpenFakeVimBufferTestWindow()
            {
                OpenFakeVimBufferTestWindow("Hello", "World!");
            }

            protected void OpenFakeVimBufferTestWindow(params string[] lines)
            {
                _textView = CreateTextView(lines);
                _textView.MoveCaretTo(0);
                _textView.TextBuffer.Properties.AddProperty(Mock.MockVimHost.FileNameKey, "VimBufferTest.cs");
                _vimBufferData = CreateVimBufferData(_textView, statusUtil: _statusUtil);
                _vimBuffer = CreateVimBuffer(_vimBufferData);
                _vimBuffer.SwitchMode(ModeKind.Command, ModeArgument.None);
            }

            protected void AssertPosition(int lineNumber, int column, FSharpOption<VirtualSnapshotPoint> option)
            {
                Assert.True(option.IsSome());
                var line = VirtualSnapshotPointUtil.GetPoint(option.Value).GetColumn();
                Assert.Equal(lineNumber, line.LineNumber);
                Assert.Equal(column, line.ColumnNumber);
            }

            [WpfFact]
            public void FirstTimeBufferIsZeroZero()
            {
                var option = Vim.MarkMap.GetMark(Mark.LastExitedPosition, _vimBuffer.VimBufferData);
                AssertPosition(0, 0, option);
            }

            [WpfFact]
            public void ReopeningTheWindow()
            {
                _textView.MoveCaretToLine(1, 2);
                _vimBuffer.Close();

                // reopen the file
                OpenFakeVimBufferTestWindow();

                var option = Vim.MarkMap.GetMark(Mark.LastExitedPosition, _vimBuffer.VimBufferData);
                AssertPosition(1, 2, option);
            }

            [WpfFact]
            public void ReopeningTheWindowLastColumn()
            {
                _vimBuffer.MarkMap.UnloadBuffer(_vimBufferData, "VimBufferTest.cs", 0, 5);
                OpenFakeVimBufferTestWindow();

                var option = Vim.MarkMap.GetMark(Mark.LastExitedPosition, _vimBuffer.VimBufferData);
                AssertPosition(0, 5, option);
            }

            [WpfFact]
            public void ReopeningTheWindowLastColumnAfterFirstLine()
            {
                _vimBuffer.MarkMap.UnloadBuffer(_vimBufferData, "VimBufferTest.cs", 1, 6);
                OpenFakeVimBufferTestWindow();

                var option = Vim.MarkMap.GetMark(Mark.LastExitedPosition, _vimBuffer.VimBufferData);
                AssertPosition(1, 6, option);
            }

            [WpfFact]
            public void ReopeningTheWindowLastPositionAtColumnZeroWithLenZeroIsOk()
            {
                _textView.SetText("Hello", "", "World!");
                _textView.MoveCaretToLine(1, 0);
                _vimBuffer.Close();

                // reopen the file
                OpenFakeVimBufferTestWindow();

                var option = Vim.MarkMap.GetMark(Mark.LastExitedPosition, _vimBuffer.VimBufferData);
                AssertPosition(1, 0, option);
            }

            [WpfFact]
            public void ReopeningTheWindowWithInvalidColumnLastPositionGoesToZeroZero()
            {
                _textView.MoveCaretToLine(1, 2);
                _vimBuffer.Close();

                // reopen the file to invalid column position
                OpenFakeVimBufferTestWindow("Hello", "");

                var option = Vim.MarkMap.GetMark(Mark.LastExitedPosition, _vimBuffer.VimBufferData);
                AssertPosition(0, 0, option);
            }

            [WpfFact]
            public void ReopeningTheWindowWithInvalidLineLastPositionGoesToZeroZero()
            {
                _textView.MoveCaretToLine(1, 2);
                _vimBuffer.Close();

                // reopen the file to invalid line position
                OpenFakeVimBufferTestWindow("Hello");

                var option = Vim.MarkMap.GetMark(Mark.LastExitedPosition, _vimBuffer.VimBufferData);
                AssertPosition(0, 0, option);
            }
        }

        public class UnloadedMarksTest : ClosingSetsLastEditedPositionMark
        {
            [WpfFact]
            public void ReloadUnloadedMark()
            {
                Vim.MarkMap.SetGlobalMark(Letter.A, _vimBufferData.VimTextBuffer, 1, 2);
                AssertPosition(1, 2, Vim.MarkMap.GetGlobalMark(Letter.A));

                _vimBuffer.Close();
                Assert.True(Vim.MarkMap.GetGlobalMark(Letter.A).IsNone());

                // reopen the file
                OpenFakeVimBufferTestWindow();
                AssertPosition(1, 2, Vim.MarkMap.GetGlobalMark(Letter.A));
            }
        }

        public sealed class MiscTest : VimBufferTest
        {
            /// <summary>
            /// Make sure the SwitchdMode event fires when switching modes.
            /// </summary>
            [WpfFact]
            public void SwitchedMode_Event()
            {
                var ran = false;
                _vimBuffer.SwitchedMode += (s, m) => { ran = true; };
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                Assert.True(ran);
            }

            /// <summary>
            /// Make sure the SwitchdMode event fires even when switching to the
            /// same mode
            /// </summary>
            [WpfFact]
            public void SwitchedMode_SameModeEvent()
            {
                var ran = false;
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                _vimBuffer.SwitchedMode += (s, m) => { ran = true; };
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                Assert.True(ran);
            }

            /// <summary>
            /// Ensure switching to the previous mode operates correctly
            /// </summary>
            [WpfFact]
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
                Assert.Same(normal.Object, prev);
                insert.Verify();

                // On tear down all IVimBuffer instances are closed as well as their active
                // IMode.  Need a setup here
                insert.Setup(x => x.OnClose());
                normal.Setup(x => x.OnClose());
            }

            /// <summary>
            /// SwitchPreviousMode should raise the SwitchedMode event
            /// </summary>
            [WpfFact]
            public void SwitchPreviousMode_RaiseSwitchedMode()
            {
                _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                var ran = false;
                _vimBuffer.SwitchedMode += (s, m) => { ran = true; };
                _vimBuffer.SwitchPreviousMode();
                Assert.True(ran);
            }

            /// <summary>
            /// When a mode returns the SwitchModeOneTimeCommand value it should cause the 
            /// InOneTimeCommand value to be set
            /// </summary>
            [WpfFact]
            public void SwitchModeOneTimeCommand_SetProperty()
            {
                var mode = CreateAndAddInsertMode(MockBehavior.Loose);
                mode.Setup(x => x.Process(It.IsAny<KeyInputData>())).Returns(ProcessResult.NewHandled(ModeSwitch.NewSwitchModeOneTimeCommand(ModeKind.Normal)));
                _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                _vimBuffer.Process('c');
                Assert.True(_vimBuffer.InOneTimeCommand.IsSome(ModeKind.Insert));
            }

            /// <summary>
            /// Process should handle the return value correctly
            /// </summary>
            [WpfFact]
            public void Process_HandleSwitchPreviousMode()
            {
                var normal = CreateAndAddNormalMode(MockBehavior.Loose);
                normal.Setup(x => x.Process(It.IsAny<KeyInputData>())).Returns(ProcessResult.NewHandled(ModeSwitch.SwitchPreviousMode));
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                _vimBuffer.Process('l');
                Assert.Equal(ModeKind.Command, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// The nop key shouldn't have any effects
            /// </summary>
            [WpfFact]
            public void Process_Nop()
            {
                var old = _vimBuffer.TextSnapshot;
                foreach (var mode in _vimBuffer.AllModes)
                {
                    _vimBuffer.SwitchMode(mode.ModeKind, ModeArgument.None);
                    Assert.True(_vimBuffer.Process(VimKey.Nop));
                    Assert.Equal(old, _vimBuffer.TextSnapshot);
                }
            }

            /// <summary>
            /// When we are InOneTimeCommand the HandledNeedMoreInput should not cause us to 
            /// do anything with respect to one time command
            /// </summary>
            [WpfFact]
            public void Process_OneTimeCommand_NeedMoreInputDoesNothing()
            {
                var mode = CreateAndAddNormalMode(MockBehavior.Loose);
                mode.Setup(x => x.Process(It.IsAny<KeyInputData>())).Returns(ProcessResult.HandledNeedMoreInput);
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                _vimBufferRaw.InOneTimeCommand = FSharpOption.Create(ModeKind.Replace);
                _vimBuffer.Process('c');
                Assert.True(_vimBufferRaw.InOneTimeCommand.IsSome(ModeKind.Replace));
            }

            /// <summary>
            /// Escape should go back to the original mode even if the current IMode doesn't
            /// support the escape key when we are in a one time command
            /// </summary>
            [WpfFact]
            public void Process_OneTimeCommand_Escape()
            {
                var mode = CreateAndAddNormalMode(MockBehavior.Loose);
                mode.Setup(x => x.Process(It.IsAny<KeyInputData>())).Returns(ProcessResult.Error);
                mode.Setup(x => x.CanProcess(It.IsAny<KeyInput>())).Returns(false);
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                _vimBufferRaw.InOneTimeCommand = FSharpOption.Create(ModeKind.Replace);
                _vimBuffer.Process(VimKey.Escape);
                Assert.Equal(ModeKind.Replace, _vimBuffer.ModeKind);
                Assert.True(_vimBufferRaw.InOneTimeCommand.IsNone());
            }

            /// <summary>
            /// When a command is completed in visual mode we shouldn't exit.  Else commands like
            /// 'l' would cause it to exit which is not the Vim behavior
            /// </summary>
            [WpfFact]
            public void Process_OneTimeCommand_VisualMode_Handled()
            {
                var mode = CreateAndAddVisualLineMode(MockBehavior.Loose);
                mode.Setup(x => x.Process(It.IsAny<KeyInputData>())).Returns(ProcessResult.NewHandled(ModeSwitch.NoSwitch));
                _vimBuffer.SwitchMode(ModeKind.VisualLine, ModeArgument.None);
                _vimBufferRaw.InOneTimeCommand = FSharpOption.Create(ModeKind.Replace);
                _vimBuffer.Process('l');
                Assert.Equal(ModeKind.VisualLine, _vimBuffer.ModeKind);
                Assert.True(_vimBufferRaw.InOneTimeCommand.Is(ModeKind.Replace));
            }

            /// <summary>
            /// Switch previous mode should still cause it to go back to the original though
            /// </summary>
            [WpfFact]
            public void Process_OneTimeCommand_VisualMode_SwitchPreviousMode()
            {
                var mode = CreateAndAddVisualLineMode(MockBehavior.Loose);
                mode.Setup(x => x.Process(It.IsAny<KeyInputData>())).Returns(ProcessResult.NewHandled(ModeSwitch.SwitchPreviousMode));
                _vimBuffer.SwitchMode(ModeKind.VisualLine, ModeArgument.None);
                _vimBufferRaw.InOneTimeCommand = FSharpOption.Create(ModeKind.Replace);
                _vimBuffer.Process('l');
                Assert.Equal(ModeKind.Replace, _vimBuffer.ModeKind);
                Assert.True(_vimBufferRaw.InOneTimeCommand.IsNone());
            }

            /// <summary>
            /// Processing the buffered key inputs when there are none should have no effect
            /// </summary>
            [WpfFact]
            public void ProcessBufferedKeyInputs_Nothing()
            {
                var runCount = 0;
                _vimBuffer.KeyInputProcessed += delegate { runCount++; };
                _vimBuffer.ProcessBufferedKeyInputs();
                Assert.Equal(0, runCount);
            }

            /// <summary>
            /// Processing the buffered key inputs should raise the processed event 
            /// </summary>
            [WpfFact]
            public void ProcessBufferedKeyInputs_RaiseProcessed()
            {
                var runCount = 0;
                _textView.SetText("");
                _vimBuffer.Vim.KeyMap.MapWithNoRemap("cat", "chase the cat", KeyRemapMode.Insert);
                _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                _vimBuffer.KeyInputProcessed += delegate { runCount++; };
                _vimBuffer.Process("ca");
                Assert.Equal(0, runCount);
                _vimBuffer.ProcessBufferedKeyInputs();
                Assert.Equal(2, runCount);
                Assert.Equal("ca", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// Ensure the mode sees the mapped KeyInput value
            /// </summary>
            [WpfFact]
            public void Remap_OneToOne()
            {
                _keyMap.MapWithNoRemap("a", "l", KeyRemapMode.Normal);
                _textView.SetText("cat dog", 0);
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                _vimBuffer.Process('a');
                Assert.Equal(1, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// When a single key is mapped to multiple both need to be passed onto the 
            /// IMode instance
            /// </summary>
            [WpfFact]
            public void Remap_OneToMany()
            {
                _keyMap.MapWithNoRemap("a", "dw", KeyRemapMode.Normal);
                _textView.SetText("cat dog", 0);
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                _vimBuffer.Process('a');
                Assert.Equal("dog", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// Don't use mappings for the wrong IMode
            /// </summary>
            [WpfFact]
            public void Remap_WrongMode()
            {
                _keyMap.MapWithNoRemap("l", "dw", KeyRemapMode.Insert);
                _textView.SetText("cat dog", 0);
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                _vimBuffer.Process('l');
                Assert.Equal(1, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// When INormalMode is in OperatorPending we need to use operating pending 
            /// remapping
            /// </summary>
            [WpfFact]
            public void Remap_OperatorPending()
            {
                _keyMap.MapWithNoRemap("z", "w", KeyRemapMode.OperatorPending);
                _textView.SetText("cat dog", 0);
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                _vimBuffer.Process("d");
                Assert.Equal(_vimBuffer.NormalMode.KeyRemapMode, KeyRemapMode.OperatorPending);
                _vimBuffer.Process("z");
                Assert.Equal("dog", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// Recursive mappings should print out an error message when used
            /// </summary>
            [WpfFact]
            public void Remap_Recursive()
            {
                _keyMap.MapWithRemap("a", "b", KeyRemapMode.Normal);
                _keyMap.MapWithRemap("b", "a", KeyRemapMode.Normal);
                var didRun = false;
                _vimBuffer.ErrorMessage +=
                    (notUsed, args) =>
                    {
                        Assert.Equal(Resources.Vim_RecursiveMapping, args.Message);
                        didRun = true;
                    };
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                _vimBuffer.Process('a');
                Assert.True(didRun);
            }

            /// <summary>
            /// When we buffer input and fail to find a mapping every KeyInput value
            /// should be passed to the IMode
            /// </summary>
            [WpfFact]
            public void Remap_BufferedFailed()
            {
                _keyMap.MapWithNoRemap("do", "cat", KeyRemapMode.Normal);
                _textView.SetText("cat dog", 0);
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                _vimBuffer.Process("d");
                Assert.Equal('d', _vimBuffer.BufferedKeyInputs.Head.Char);
                _vimBuffer.Process("w");
                Assert.Equal("dog", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// Make sure the KeyInput is passed down to the IMode
            /// </summary>
            [WpfFact]
            public void CanProcess_Simple()
            {
                var keyInput = KeyInputUtil.CharToKeyInput('c');
                var normal = CreateAndAddNormalMode(MockBehavior.Loose);
                normal.Setup(x => x.CanProcess(keyInput)).Returns(true).Verifiable();
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                Assert.True(_vimBuffer.CanProcess(keyInput));
                normal.Verify();
            }

            /// <summary>
            /// Make sure the mapped KeyInput is passed down to the IMode
            /// </summary>
            [WpfFact]
            public void CanProcess_Mapped()
            {
                _keyMap.MapWithRemap("a", "c", KeyRemapMode.Normal);
                var keyInput = KeyInputUtil.CharToKeyInput('c');
                var normal = CreateAndAddNormalMode(MockBehavior.Loose);
                normal.Setup(x => x.CanProcess(keyInput)).Returns(true).Verifiable();
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                Assert.True(_vimBuffer.CanProcess('a'));
                normal.Verify();
            }

            /// <summary>
            /// When there is buffered input due to a key mapping make sure that 
            /// we consider the final mapped input for processing and not the immediate
            /// KeyInput value
            /// </summary>
            [WpfFact]
            public void CanProcess_BufferedInput()
            {
                _keyMap.MapWithRemap("la", "iexample", KeyRemapMode.Normal);
                _textView.SetText("dog cat");
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);

                // <F4> is not a valid command
                var keyInput = KeyInputUtil.CharToKeyInput('¤');
                Assert.False(_vimBuffer.CanProcess(keyInput));
                _vimBuffer.Process("l");
                Assert.False(_vimBuffer.BufferedKeyInputs.IsEmpty);

                // Is is still not a valid command but when mapping is considered it will
                // expand to l<F4> and l is a valid command
                Assert.True(_vimBuffer.CanProcess(keyInput));
            }

            /// <summary>
            /// The buffer can always process a nop key and should take no action when it's
            /// encountered
            /// </summary>
            [WpfFact]
            public void CanProcess_Nop()
            {
                foreach (var mode in _vimBuffer.AllModes)
                {
                    _vimBuffer.SwitchMode(mode.ModeKind, ModeArgument.None);
                    Assert.True(_vimBuffer.CanProcess(VimKey.Nop));
                }
            }

            /// <summary>
            /// Make sure that we can handle keypad divide in normal mode as it's simply 
            /// processed as a divide 
            /// </summary>
            [WpfFact]
            public void CanProcess_KeypadDivide()
            {
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                Assert.True(_vimBuffer.CanProcess(VimKey.KeypadDivide));
            }

            /// <summary>
            /// Make sure that the underlying mode doesn't see Keypad divide but instead sees 
            /// divide as this is how Vim handles keys post mapping
            /// </summary>
            [WpfFact]
            public void CanProcess_KeypadDivideAsForwardSlash()
            {
                var normalMode = CreateAndAddNormalMode();
                normalMode.Setup(x => x.OnEnter(ModeArgument.None)).Verifiable();
                normalMode.Setup(x => x.CanProcess(KeyInputUtil.CharToKeyInput('/'))).Returns(true);
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                Assert.True(_vimBuffer.CanProcess(VimKey.KeypadDivide));
            }

            /// <summary>
            /// Make sure that we can handle keypad divide in normal mode as it's simply 
            /// processed as a divide 
            /// </summary>
            [WpfFact]
            public void CanProcessAsCommand_KeypadDivide()
            {
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                Assert.True(_vimBuffer.CanProcessAsCommand(VimKey.KeypadDivide));
            }

            /// <summary>
            /// Make sure that the underlying mode doesn't see Keypad divide but instead sees 
            /// divide as this is how Vim handles keys post mapping
            /// </summary>
            [WpfFact]
            public void CanProcessAsCommand_KeypadDivideAsForwardSlash()
            {
                var normalMode = CreateAndAddNormalMode();
                normalMode.Setup(x => x.OnEnter(ModeArgument.None)).Verifiable();
                normalMode.Setup(x => x.CanProcess(KeyInputUtil.CharToKeyInput('/'))).Returns(true);
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                Assert.True(_vimBuffer.CanProcess(VimKey.KeypadDivide));
            }

            /// <summary>
            /// Make sure that commands like 'a' are still considered commands when the
            /// IVimBuffer is in normal mode
            /// </summary>
            [WpfFact]
            public void CanProcessAsCommand_Command()
            {
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                Assert.True(_vimBuffer.CanProcessAsCommand('a'));
            }

            /// <summary>
            /// Make sure that commands like 'a' are not considered commands when in 
            /// insert mode
            /// </summary>
            [WpfFact]
            public void CanProcessAsCommand_InsertMode()
            {
                _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                Assert.False(_vimBuffer.CanProcessAsCommand('a'));
            }

            /// <summary>
            /// Make sure that commands like 'a' are not considered commands when in 
            /// replace mode
            /// </summary>
            [WpfFact]
            public void CanProcessAsCommand_ReplaceMode()
            {
                _vimBuffer.SwitchMode(ModeKind.Replace, ModeArgument.None);
                Assert.False(_vimBuffer.CanProcessAsCommand('a'));
            }

            /// <summary>
            /// Make sure the event is true while processing input
            /// </summary>
            [WpfFact]
            public void IsProcessing_Basic()
            {
                var didRun = false;
                Assert.False(_vimBuffer.IsProcessingInput);
                _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                _textView.TextBuffer.Changed +=
                    delegate
                    {
                        Assert.True(_vimBuffer.IsProcessingInput);
                        didRun = true;
                    };
                _vimBuffer.Process("h");   // Changing text will raise Changed
                Assert.False(_vimBuffer.IsProcessingInput);
                Assert.True(didRun);
            }

            /// <summary>
            /// Make sure the event properly resets while recursively processing 
            /// input
            /// </summary>
            [WpfFact]
            public void IsProcessing_Recursive()
            {
                var didRun = false;
                var isFirst = true;
                Assert.False(_vimBuffer.IsProcessingInput);
                _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                _textView.TextBuffer.Changed +=
                    delegate
                    {
                        if (isFirst)
                        {
                            isFirst = false;
                            _vimBuffer.Process('o');
                        }
                        Assert.True(_vimBuffer.IsProcessingInput);
                        didRun = true;
                    };
                _vimBuffer.Process("h");   // Changing text will raise Changed
                Assert.False(_vimBuffer.IsProcessingInput);
                Assert.True(didRun);
            }

            /// <summary>
            /// Ensure the key simulation raises the appropriate key APIs
            /// </summary>
            [WpfFact]
            public void SimulateProcessed_RaiseEvent()
            {
                var ranStart = false;
                var ranProcessed = false;
                var ranEnd = false;
                _vimBuffer.KeyInputStart += delegate { ranStart = true; };
                _vimBuffer.KeyInputProcessed += delegate { ranProcessed = true; };
                _vimBuffer.KeyInputEnd += delegate { ranEnd = true; };
                _vimBuffer.SimulateProcessed(KeyInputUtil.CharToKeyInput('c'));
                Assert.True(ranStart);
                Assert.True(ranEnd);
                Assert.True(ranProcessed);
            }

            /// <summary>
            /// Ensure the SimulateProcessed API doesn't go through key remapping.  The caller
            /// who wants the simulated input is declaring the literal KeyInput was processed
            /// </summary>
            [WpfFact]
            public void SimulateProcessed_DontMap()
            {
                _vimBuffer.Vim.KeyMap.MapWithNoRemap("a", "b", KeyRemapMode.Normal);
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                var ranProcessed = false;
                _vimBuffer.KeyInputProcessed +=
                    (unused, args) =>
                    {
                        Assert.Equal(KeyInputUtil.CharToKeyInput('a'), args.KeyInput);
                        ranProcessed = true;
                    };
                _vimBuffer.SimulateProcessed(KeyInputUtil.CharToKeyInput('a'));
                Assert.True(ranProcessed);
            }

            /// <summary>
            /// When input is simulated it should clear any existing buffered KeyInput 
            /// values.  The caller who simulates the input is responsible for understanding
            /// and ignoring buffered input values
            /// </summary>
            [WpfFact]
            public void SimulateProcessed_ClearBufferedInput()
            {
                _vimBuffer.Vim.KeyMap.MapWithNoRemap("jj", "b", KeyRemapMode.Normal);
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                _vimBuffer.Process('j');
                Assert.False(_vimBuffer.BufferedKeyInputs.IsEmpty);
                _vimBuffer.SimulateProcessed(KeyInputUtil.CharToKeyInput('a'));
                Assert.True(_vimBuffer.BufferedKeyInputs.IsEmpty);
            }

            /// <summary>
            /// When creating an <see cref="IVimBuffer"/> over an existing <see cref="IVimTextBuffer"/> instance,
            /// the initial mode of the newly created instance will be that of the existing one.  Hence it's possible
            /// to transition directly from <see cref="ModeKind.Uninitialized"/> to say <see cref="ModeKind.VisualCharacter" />. 
            /// Need to ensure that is handled correctly.
            /// </summary>
            [WpfFact]
            public void InitialModeIsVisual()
            {
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                _vimBuffer.Process("v");
                Assert.Equal(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
                var altTextView = VimEditorHost.TextEditorFactoryService.CreateTextView(_vimBuffer.TextBuffer);
                var altVimBuffer = VimEditorHost.VimBufferFactory.CreateVimBuffer(altTextView, _vimBuffer.VimTextBuffer);
                try
                {
                    for (var i = 0; i < 5; i++)
                    {
                        altVimBuffer.Process(VimKey.Escape);
                        Assert.Equal(ModeKind.Normal, altVimBuffer.ModeKind);
                        altVimBuffer.Process("v");
                        Assert.Equal(ModeKind.VisualCharacter, altVimBuffer.ModeKind);
                    }
                }
                finally
                {
                    altTextView.Close();
                    altVimBuffer.Close();
                }
            }

            /// <summary>
            /// Make sure that we properly transition to normal mode when leaving visual mode 
            /// </summary>
            [WpfFact]
            public void Issue1170()
            {
                _vimBuffer.ProcessNotation(@"i<Esc>v<Esc>");
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
            }

            [WpfFact]
            public void Issue1955()
            {
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                _vimBuffer.Process("v");
                var altTextView = VimEditorHost.TextEditorFactoryService.CreateTextView(_vimBuffer.TextBuffer);
                var altVimBuffer = VimEditorHost.VimBufferFactory.CreateVimBuffer(altTextView, _vimBuffer.VimTextBuffer);
                try
                {
                    altVimBuffer.SwitchMode(ModeKind.Command, ModeArgument.None);
                    altVimBuffer.SwitchMode(ModeKind.VisualCharacter, ModeArgument.None);
                    Assert.Equal(ModeKind.Command, ((VimBuffer)altVimBuffer).ModeMap.PreviousMode.Value.ModeKind);
                }
                finally
                {
                    altTextView.Close();
                    altVimBuffer.Close();
                }
            }
        }
    }
}
