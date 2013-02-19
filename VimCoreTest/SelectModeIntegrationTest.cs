using System;
using EditorUtils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Xunit;
using Vim.UnitTest.Exports;

namespace Vim.UnitTest
{
    public abstract class SelectModeIntegrationTest : VimTestBase
    {
        protected IVimBuffer _vimBuffer;
        protected ITextView _textView;
        protected ITextBuffer _textBuffer;
        protected ITextSelection _textSelection;
        protected IVimGlobalSettings _globalSettings;
        protected TestableSynchronizationContext _context;
        protected TestableMouseDevice _testableMouseDevice;

        protected virtual void Create(params string[] lines)
        {
            _vimBuffer = CreateVimBuffer(lines);
            _textView = _vimBuffer.TextView;
            _textBuffer = _vimBuffer.TextBuffer;
            _globalSettings = _vimBuffer.GlobalSettings;
            _globalSettings.SelectModeOptions = SelectModeOptions.Mouse | SelectModeOptions.Keyboard;
            _textSelection = _textView.Selection;
            _context = new TestableSynchronizationContext();
            _context.Install();
            _testableMouseDevice = (TestableMouseDevice)MouseDevice;
            _testableMouseDevice.IsLeftButtonPressed = true;
        }

        public override void Dispose()
        {
            base.Dispose();
            _testableMouseDevice.IsLeftButtonPressed = false;
        }

        protected void EnterSelect(int start, int length)
        {
            var span = new SnapshotSpan(_textBuffer.CurrentSnapshot, start, length);
            _textView.SelectAndMoveCaret(span);
            _context.RunAll();
            Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
        }

        public sealed class Enter : SelectModeIntegrationTest
        {
            [Fact]
            public void SelectOfText()
            {
                Create("cat dog");
                _textSelection.Select(0, 3);
                _context.RunAll();
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// Extending the selection should just ask select mode to reset it's information
            /// </summary>
            [Fact]
            public void ExtendSelection()
            {
                Create("cat dog");
                _textSelection.Select(0, 3);
                _context.RunAll();
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                _textSelection.Select(0, 5);
                Assert.False(_context.IsEmpty);
                _context.RunAll();
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
            }

            [Fact]
            public void CommandToCharacter()
            {
                Create("cat");
                _vimBuffer.ProcessNotation("gh");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
            }

            [Fact]
            public void CommandToLine()
            {
                Create("cat");
                _vimBuffer.ProcessNotation("gH");
                Assert.Equal(ModeKind.SelectLine, _vimBuffer.ModeKind);
            }

            [Fact]
            public void CommandToBlock()
            {
                Create("cat");
                _vimBuffer.ProcessNotation("g<C-H>");
                Assert.Equal(ModeKind.SelectBlock, _vimBuffer.ModeKind);
            }
        }

        public sealed class SpecialKeysFromNormal : SelectModeIntegrationTest
        {
            [Fact]
            public void ShiftRightToSelect()
            {
                Create("cat");
                _globalSettings.SelectModeOptions = SelectModeOptions.Keyboard;
                _globalSettings.KeyModelOptions = KeyModelOptions.StartSelection;
                _vimBuffer.ProcessNotation("<S-Right>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal("ca", _textView.GetSelectionSpan().GetText());
                Assert.Equal(1, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ShiftRightToVisual()
            {
                Create("cat");
                _globalSettings.SelectModeOptions = SelectModeOptions.None;
                _globalSettings.KeyModelOptions = KeyModelOptions.StartSelection;
                _vimBuffer.ProcessNotation("<S-Right>");
                Assert.Equal(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
                Assert.Equal("ca", _textView.GetSelectionSpan().GetText());
                Assert.Equal(1, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ShiftRightToNothing()
            {
                Create("cat dog");
                _vimBuffer.ProcessNotation("<S-Right>");
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                Assert.Equal(4, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ShiftLeftToSelect()
            {
                Create("cat");
                _textView.MoveCaretTo(1);
                _globalSettings.SelectModeOptions = SelectModeOptions.Keyboard;
                _globalSettings.KeyModelOptions = KeyModelOptions.StartSelection;
                _vimBuffer.ProcessNotation("<S-Left>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal("ca", _textView.GetSelectionSpan().GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ShiftLeftToVisual()
            {
                Create("cat");
                _textView.MoveCaretTo(1);
                _globalSettings.SelectModeOptions = SelectModeOptions.None;
                _globalSettings.KeyModelOptions = KeyModelOptions.StartSelection;
                _vimBuffer.ProcessNotation("<S-Left>");
                Assert.Equal(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
                Assert.Equal("ca", _textView.GetSelectionSpan().GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ShiftLeftToNothing()
            {
                Create("cat dog");
                _textView.MoveCaretTo(4);
                _vimBuffer.ProcessNotation("<S-Left>");
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }
        }

        public sealed class Edit : SelectModeIntegrationTest
        {
            [Fact]
            public void SimpleAlpha()
            {
                Create("Test time");
                EnterSelect(1, 3);
                _vimBuffer.Process('o');
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal("To time", _textBuffer.GetLine(0).GetText());
                Assert.Equal(2, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void SimpleNumeric()
            {
                Create("Test time");
                EnterSelect(1, 3);
                _vimBuffer.Process('3');
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal("T3 time", _textBuffer.GetLine(0).GetText());
                Assert.Equal(2, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void SimpleEnter()
            {
                Create("dog go time");
                EnterSelect(4, 3);
                _vimBuffer.Process(VimKey.Enter);
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal(new[] { "dog ", "time" }, _textBuffer.GetLines());
                Assert.Equal(_textBuffer.GetLine(1).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// The delete key should delete the selection and not insert any text into the 
            /// line
            ///
            /// issue #911
            /// </summary>
            [Fact]
            public void DeleteKey()
            {
                Create("dog cat bear");
                EnterSelect(0, 4);
                _vimBuffer.Process(VimKey.Delete);
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal("cat bear", _textBuffer.GetLine(0).GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// The backspace key should delete the selection and not insert any text into the 
            /// line
            /// </summary>
            [Fact]
            public void BackspaceKey()
            {
                Create("dog cat bear");
                EnterSelect(0, 4);
                _vimBuffer.Process(VimKey.Back);
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal("cat bear", _textBuffer.GetLine(0).GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }
        }

        public sealed class KeyMovementCharacter : SelectModeIntegrationTest
        {
            [Fact]
            public void Right()
            {
                Create("cat");
                _vimBuffer.ProcessNotation("gh<Right>");
                Assert.Equal("ca", _textView.GetSelectionSpan().GetText());
                Assert.Equal(1, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void Left()
            {
                Create("cat");
                _vimBuffer.ProcessNotation("lgh<Left>");
                Assert.Equal("ca", _textView.GetSelectionSpan().GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void Down()
            {
                Create("cat", "dog");
                _vimBuffer.ProcessNotation("gh<Down>");
                Assert.Equal(_textBuffer.GetLine(1).Start, _textView.GetCaretPoint());
                Assert.Equal("cat" + Environment.NewLine + "d", _textView.GetSelectionSpan().GetText());
            }
        }

        public sealed class KeyMovementWithStopSelection : SelectModeIntegrationTest
        {
            protected override void Create(params string[] lines)
            {
                base.Create(lines);
                _globalSettings.KeyModelOptions = KeyModelOptions.StopSelection;
            }

            [Fact]
            public void Right()
            {
                Create("cat");
                _vimBuffer.ProcessNotation("gh<Right>");
                Assert.Equal(1, _textView.GetCaretPoint().Position);
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                Assert.True(_textView.Selection.IsEmpty);
            }

            [Fact]
            public void Left()
            {
                Create("cat");
                _vimBuffer.ProcessNotation("lgh<Left>");
                Assert.Equal(0, _textView.GetCaretPoint().Position);
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                Assert.True(_textView.Selection.IsEmpty);
            }

            [Fact]
            public void Down()
            {
                Create("cat", "dog");
                _vimBuffer.ProcessNotation("gh<Down>");
                Assert.Equal(_textBuffer.GetLine(1).Start, _textView.GetCaretPoint());
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                Assert.True(_textView.Selection.IsEmpty);
            }

            /// <summary>
            /// Even when we have stopsel set the shifted keys should be extending the selection
            /// </summary>
            [Fact]
            public void ShiftRight()
            {
                Create("cat");
                _vimBuffer.ProcessNotation("gh<S-Right>");
                Assert.Equal("ca", _textView.GetSelectionSpan().GetText());
                Assert.Equal(1, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ShiftLeft()
            {
                Create("cat");
                _vimBuffer.ProcessNotation("lgh<S-Left>");
                Assert.Equal("ca", _textView.GetSelectionSpan().GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }
        }

        public sealed class Misc : SelectModeIntegrationTest
        {
            /// <summary>
            /// Make sure that escape will leave select mode
            /// </summary>
            [Fact]
            public void Escape()
            {
                Create("cat dog");
                EnterSelect(0, 3);
                _vimBuffer.Process(VimKey.Escape);
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                Assert.Equal(2, _textView.GetCaretPoint().Position);
            }
        }
    }
}
