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
            public void ShiftRightToSelect_NormalInclusive()
            {
                Create("cat");
                _globalSettings.SelectModeOptions = SelectModeOptions.Keyboard;
                _globalSettings.KeyModelOptions = KeyModelOptions.StartSelection;
                _vimBuffer.ProcessNotation("<S-Right>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal("ca", _textView.GetSelectionSpan().GetText());
                Assert.Equal(1, _textView.GetCaretPoint().Position);
                _vimBuffer.ProcessNotation("<Esc>");
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                Assert.Equal(1, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ShiftRightToSelect_NormalExclusive()
            {
                Create("cat");
                _globalSettings.Selection = "exclusive";
                _globalSettings.SelectModeOptions = SelectModeOptions.Keyboard;
                _globalSettings.KeyModelOptions = KeyModelOptions.StartSelection;
                _vimBuffer.ProcessNotation("<S-Right>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal("c", _textView.GetSelectionSpan().GetText());
                Assert.Equal(1, _textView.GetCaretPoint().Position);
                _vimBuffer.ProcessNotation("<Esc>");
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                Assert.Equal(1, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ControlShiftRightToSelect_NormalInclusive()
            {
                Create("cat dog");
                _globalSettings.SelectModeOptions = SelectModeOptions.Keyboard;
                _globalSettings.KeyModelOptions = KeyModelOptions.StartSelection;
                _vimBuffer.ProcessNotation("<C-S-Right>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal("cat d", _textView.GetSelectionSpan().GetText());
                Assert.Equal(4, _textView.GetCaretPoint().Position);
                _vimBuffer.ProcessNotation("<Esc>");
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                Assert.Equal(4, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ControlShiftRightToSelect_NormalExclusive()
            {
                Create("cat dog");
                _globalSettings.Selection = "exclusive";
                _globalSettings.SelectModeOptions = SelectModeOptions.Keyboard;
                _globalSettings.KeyModelOptions = KeyModelOptions.StartSelection;
                _vimBuffer.ProcessNotation("<C-S-Right>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal("cat ", _textView.GetSelectionSpan().GetText());
                Assert.Equal(4, _textView.GetCaretPoint().Position);
                _vimBuffer.ProcessNotation("<Esc>");
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                Assert.Equal(4, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ShiftLeftToSelect_NormalInclusive()
            {
                Create("cat dog");
                _textView.MoveCaretTo(4);
                _globalSettings.SelectModeOptions = SelectModeOptions.Keyboard;
                _globalSettings.KeyModelOptions = KeyModelOptions.StartSelection;
                _vimBuffer.ProcessNotation("<S-Left>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal(" d", _textView.GetSelectionSpan().GetText());
                Assert.Equal(3, _textView.GetCaretPoint().Position);
                _vimBuffer.ProcessNotation("<Esc>");
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                Assert.Equal(3, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ShiftLeftToSelect_NormalExclusive()
            {
                Create("cat dog");
                _textView.MoveCaretTo(4);
                _globalSettings.Selection = "exclusive";
                _globalSettings.SelectModeOptions = SelectModeOptions.Keyboard;
                _globalSettings.KeyModelOptions = KeyModelOptions.StartSelection;
                _vimBuffer.ProcessNotation("<S-Left>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal(" ", _textView.GetSelectionSpan().GetText());
                Assert.Equal(3, _textView.GetCaretPoint().Position);
                _vimBuffer.ProcessNotation("<Esc>");
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                Assert.Equal(3, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ControlShiftLeftToSelect_NormalInclusive()
            {
                Create("cat dog fish");
                _textView.MoveCaretTo(8);
                _globalSettings.SelectModeOptions = SelectModeOptions.Keyboard;
                _globalSettings.KeyModelOptions = KeyModelOptions.StartSelection;
                _vimBuffer.ProcessNotation("<C-S-Left>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal("dog f", _textView.GetSelectionSpan().GetText());
                Assert.Equal(4, _textView.GetCaretPoint().Position);
                _vimBuffer.ProcessNotation("<Esc>");
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                Assert.Equal(4, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ControlShiftLeftToSelect_NormalExclusive()
            {
                Create("cat dog fish");
                _textView.MoveCaretTo(8);
                _globalSettings.Selection = "exclusive";
                _globalSettings.SelectModeOptions = SelectModeOptions.Keyboard;
                _globalSettings.KeyModelOptions = KeyModelOptions.StartSelection;
                _vimBuffer.ProcessNotation("<C-S-Left>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal("dog ", _textView.GetSelectionSpan().GetText());
                Assert.Equal(4, _textView.GetCaretPoint().Position);
                _vimBuffer.ProcessNotation("<Esc>");
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                Assert.Equal(4, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ControlShiftEndToSelect_NormalInclusiveFinalLineBreak()
            {
                Create("cat dog fish", "");
                _textView.MoveCaretTo(0);
                _globalSettings.SelectModeOptions = SelectModeOptions.Keyboard;
                _globalSettings.KeyModelOptions = KeyModelOptions.StartSelection;
                _vimBuffer.ProcessNotation("<C-S-End>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal("cat dog fish", _textView.GetSelectionSpan().GetText());
                Assert.Equal(12, _textView.GetCaretPoint().Position);
                _vimBuffer.ProcessNotation("<Esc>");
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                Assert.Equal(11, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ControlShiftEndToSelect_NormalExclusiveFinalLineBreak()
            {
                Create("cat dog fish", "");
                _textView.MoveCaretTo(0);
                _globalSettings.Selection = "exclusive";
                _globalSettings.SelectModeOptions = SelectModeOptions.Keyboard;
                _globalSettings.KeyModelOptions = KeyModelOptions.StartSelection;
                _vimBuffer.ProcessNotation("<C-S-End>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal("cat dog fish", _textView.GetSelectionSpan().GetText());
                Assert.Equal(14, _textView.GetCaretPoint().Position);
                _vimBuffer.ProcessNotation("<Esc>");
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                Assert.Equal(14, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ControlShiftEndToSelect_NormalInclusiveNoFinalLineBreak()
            {
                Create("cat dog fish");
                _textView.MoveCaretTo(0);
                _globalSettings.SelectModeOptions = SelectModeOptions.Keyboard;
                _globalSettings.KeyModelOptions = KeyModelOptions.StartSelection;
                _vimBuffer.ProcessNotation("<C-S-End>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal("cat dog fish", _textView.GetSelectionSpan().GetText());
                Assert.Equal(11, _textView.GetCaretPoint().Position);
                _vimBuffer.ProcessNotation("<Esc>");
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                Assert.Equal(11, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ControlShiftEndToSelect_NormalExclusiveNoFinalLineBreak()
            {
                Create("cat dog fish");
                _textView.MoveCaretTo(0);
                _globalSettings.Selection = "exclusive";
                _globalSettings.SelectModeOptions = SelectModeOptions.Keyboard;
                _globalSettings.KeyModelOptions = KeyModelOptions.StartSelection;
                _vimBuffer.ProcessNotation("<C-S-End>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal("cat dog fish", _textView.GetSelectionSpan().GetText());
                Assert.Equal(12, _textView.GetCaretPoint().Position);
                _vimBuffer.ProcessNotation("<Esc>");
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                Assert.Equal(11, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ShiftRightToVisual_Normal()
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
            public void ShiftRightToNothing_Normal()
            {
                Create("cat dog");
                _vimBuffer.ProcessNotation("<S-Right>");
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                Assert.Equal(4, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ShiftLeftToSelect_Normal()
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
            public void ShiftLeftToVisual_Normal()
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
            public void ShiftLeftToNothing_Normal()
            {
                Create("cat dog");
                _textView.MoveCaretTo(4);
                _vimBuffer.ProcessNotation("<S-Left>");
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ShiftLeftToFromLastCharacter_NormalInclusive()
            {
                Create("cat", "dog");
                _textView.MoveCaretTo(2);
                _globalSettings.SelectModeOptions = SelectModeOptions.Keyboard;
                _globalSettings.KeyModelOptions = KeyModelOptions.StartSelection;
                _vimBuffer.ProcessNotation("<S-Left>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal("at", _textView.GetSelectionSpan().GetText());
                Assert.Equal(1, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ShiftLeftToFromLastCharacter_NormalExclusive()
            {
                Create("cat", "dog");
                _textView.MoveCaretTo(2);
                _globalSettings.Selection = "exclusive";
                _globalSettings.SelectModeOptions = SelectModeOptions.Keyboard;
                _globalSettings.KeyModelOptions = KeyModelOptions.StartSelection;
                _vimBuffer.ProcessNotation("<S-Left>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal("a", _textView.GetSelectionSpan().GetText());
                Assert.Equal(1, _textView.GetCaretPoint().Position);
            }
        }

        public sealed class SpecialKeysFromInsert : SelectModeIntegrationTest
        {
            [Fact]
            public void ShiftRightToSelect_InsertInclusive()
            {
                Create("cat");
                _vimBuffer.ProcessNotation("i");
                _globalSettings.SelectModeOptions = SelectModeOptions.Keyboard;
                _globalSettings.KeyModelOptions = KeyModelOptions.StartSelection;
                _vimBuffer.ProcessNotation("<S-Right>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal("ca", _textView.GetSelectionSpan().GetText());
                Assert.Equal(1, _textView.GetCaretPoint().Position);
                _vimBuffer.ProcessNotation("<Esc>");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal(1, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ShiftRightToSelect_InsertExclusive()
            {
                Create("cat");
                _vimBuffer.ProcessNotation("i");
                _globalSettings.Selection = "exclusive";
                _globalSettings.SelectModeOptions = SelectModeOptions.Keyboard;
                _globalSettings.KeyModelOptions = KeyModelOptions.StartSelection;
                _vimBuffer.ProcessNotation("<S-Right>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal("c", _textView.GetSelectionSpan().GetText());
                Assert.Equal(1, _textView.GetCaretPoint().Position);
                _vimBuffer.ProcessNotation("<Esc>");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal(1, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ControlShiftRightToSelect_InsertInclusive()
            {
                Create("cat dog");
                _vimBuffer.ProcessNotation("i");
                _globalSettings.SelectModeOptions = SelectModeOptions.Keyboard;
                _globalSettings.KeyModelOptions = KeyModelOptions.StartSelection;
                _vimBuffer.ProcessNotation("<C-S-Right>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal("cat d", _textView.GetSelectionSpan().GetText());
                Assert.Equal(4, _textView.GetCaretPoint().Position);
                _vimBuffer.ProcessNotation("<Esc>");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal(4, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ControlShiftRightToSelect_InsertExclusive()
            {
                Create("cat dog");
                _vimBuffer.ProcessNotation("i");
                _globalSettings.Selection = "exclusive";
                _globalSettings.SelectModeOptions = SelectModeOptions.Keyboard;
                _globalSettings.KeyModelOptions = KeyModelOptions.StartSelection;
                _vimBuffer.ProcessNotation("<C-S-Right>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal("cat ", _textView.GetSelectionSpan().GetText());
                Assert.Equal(4, _textView.GetCaretPoint().Position);
                _vimBuffer.ProcessNotation("<Esc>");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal(4, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ShiftLeftToSelect_InsertInclusive()
            {
                Create("cat dog");
                _textView.MoveCaretTo(4);
                _vimBuffer.ProcessNotation("i");
                _globalSettings.SelectModeOptions = SelectModeOptions.Keyboard;
                _globalSettings.KeyModelOptions = KeyModelOptions.StartSelection;
                _vimBuffer.ProcessNotation("<S-Left>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal(" d", _textView.GetSelectionSpan().GetText());
                Assert.Equal(3, _textView.GetCaretPoint().Position);
                _vimBuffer.ProcessNotation("<Esc>");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal(3, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ShiftLeftToSelect_InsertExclusive()
            {
                Create("cat dog");
                _textView.MoveCaretTo(4);
                _vimBuffer.ProcessNotation("i");
                _globalSettings.Selection = "exclusive";
                _globalSettings.SelectModeOptions = SelectModeOptions.Keyboard;
                _globalSettings.KeyModelOptions = KeyModelOptions.StartSelection;
                _vimBuffer.ProcessNotation("<S-Left>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal(" ", _textView.GetSelectionSpan().GetText());
                Assert.Equal(3, _textView.GetCaretPoint().Position);
                _vimBuffer.ProcessNotation("<Esc>");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal(3, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ControlShiftLeftToSelect_InsertInclusive()
            {
                Create("cat dog fish");
                _textView.MoveCaretTo(8);
                _vimBuffer.ProcessNotation("i");
                _globalSettings.SelectModeOptions = SelectModeOptions.Keyboard;
                _globalSettings.KeyModelOptions = KeyModelOptions.StartSelection;
                _vimBuffer.ProcessNotation("<C-S-Left>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal("dog f", _textView.GetSelectionSpan().GetText());
                Assert.Equal(4, _textView.GetCaretPoint().Position);
                _vimBuffer.ProcessNotation("<Esc>");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal(4, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ControlShiftLeftToSelect_InsertExclusive()
            {
                Create("cat dog fish");
                _textView.MoveCaretTo(8);
                _vimBuffer.ProcessNotation("i");
                _globalSettings.Selection = "exclusive";
                _globalSettings.SelectModeOptions = SelectModeOptions.Keyboard;
                _globalSettings.KeyModelOptions = KeyModelOptions.StartSelection;
                _vimBuffer.ProcessNotation("<C-S-Left>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal("dog ", _textView.GetSelectionSpan().GetText());
                Assert.Equal(4, _textView.GetCaretPoint().Position);
                _vimBuffer.ProcessNotation("<Esc>");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal(4, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ControlShiftEndToSelect_InsertInclusiveFinalLineBreak()
            {
                Create("cat dog fish", "");
                _textView.MoveCaretTo(0);
                _vimBuffer.ProcessNotation("i");
                _globalSettings.SelectModeOptions = SelectModeOptions.Keyboard;
                _globalSettings.KeyModelOptions = KeyModelOptions.StartSelection;
                _vimBuffer.ProcessNotation("<C-S-End>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal("cat dog fish", _textView.GetSelectionSpan().GetText());
                Assert.Equal(12, _textView.GetCaretPoint().Position);
                _vimBuffer.ProcessNotation("<Esc>");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal(12, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ControlShiftEndToSelect_InsertExclusiveFinalLineBreak()
            {
                Create("cat dog fish", "");
                _textView.MoveCaretTo(0);
                _vimBuffer.ProcessNotation("i");
                _globalSettings.Selection = "exclusive";
                _globalSettings.SelectModeOptions = SelectModeOptions.Keyboard;
                _globalSettings.KeyModelOptions = KeyModelOptions.StartSelection;
                _vimBuffer.ProcessNotation("<C-S-End>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal("cat dog fish", _textView.GetSelectionSpan().GetText());
                Assert.Equal(14, _textView.GetCaretPoint().Position);
                _vimBuffer.ProcessNotation("<Esc>");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal(14, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ControlShiftEndToSelect_InsertInclusiveNoFinalLineBreak()
            {
                Create("cat dog fish");
                _textView.MoveCaretTo(0);
                _vimBuffer.ProcessNotation("i");
                _globalSettings.SelectModeOptions = SelectModeOptions.Keyboard;
                _globalSettings.KeyModelOptions = KeyModelOptions.StartSelection;
                _vimBuffer.ProcessNotation("<C-S-End>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal("cat dog fish", _textView.GetSelectionSpan().GetText());
                Assert.Equal(11, _textView.GetCaretPoint().Position);
                _vimBuffer.ProcessNotation("<Esc>");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal(11, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ControlShiftEndToSelect_InsertExclusiveNoFinalLineBreak()
            {
                Create("cat dog fish");
                _textView.MoveCaretTo(0);
                _vimBuffer.ProcessNotation("i");
                _globalSettings.Selection = "exclusive";
                _globalSettings.SelectModeOptions = SelectModeOptions.Keyboard;
                _globalSettings.KeyModelOptions = KeyModelOptions.StartSelection;
                _vimBuffer.ProcessNotation("<C-S-End>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal("cat dog fish", _textView.GetSelectionSpan().GetText());
                Assert.Equal(12, _textView.GetCaretPoint().Position);
                _vimBuffer.ProcessNotation("<Esc>");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal(12, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ShiftRightToVisual_Insert()
            {
                Create("cat");
                _vimBuffer.ProcessNotation("i");
                _globalSettings.SelectModeOptions = SelectModeOptions.None;
                _globalSettings.KeyModelOptions = KeyModelOptions.StartSelection;
                _vimBuffer.ProcessNotation("<S-Right>");
                Assert.Equal(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
                Assert.Equal("ca", _textView.GetSelectionSpan().GetText());
                Assert.Equal(1, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ShiftRightToNothing_Insert()
            {
                Create("cat dog");
                _vimBuffer.ProcessNotation("i");
                _vimBuffer.ProcessNotation("<S-Right>");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal(4, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ShiftLeftToSelect_Insert()
            {
                Create("cat");
                _textView.MoveCaretTo(1);
                _vimBuffer.ProcessNotation("i");
                _globalSettings.SelectModeOptions = SelectModeOptions.Keyboard;
                _globalSettings.KeyModelOptions = KeyModelOptions.StartSelection;
                _vimBuffer.ProcessNotation("<S-Left>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal("ca", _textView.GetSelectionSpan().GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ShiftLeftToVisual_Insert()
            {
                Create("cat");
                _textView.MoveCaretTo(1);
                _vimBuffer.ProcessNotation("i");
                _globalSettings.SelectModeOptions = SelectModeOptions.None;
                _globalSettings.KeyModelOptions = KeyModelOptions.StartSelection;
                _vimBuffer.ProcessNotation("<S-Left>");
                Assert.Equal(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
                Assert.Equal("ca", _textView.GetSelectionSpan().GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ShiftLeftToNothing_Insert()
            {
                Create("cat dog");
                _textView.MoveCaretTo(4);
                _vimBuffer.ProcessNotation("i");
                _vimBuffer.ProcessNotation("<S-Left>");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
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
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
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
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                Assert.Equal("cat bear", _textBuffer.GetLine(0).GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// The initial replacement and any subsequent insert mode input should be linked
            /// into a single undo
            /// </summary>
            [Fact]
            public void UndoAfterMultipleInputChars()
            {
                Create("dog cat bear");
                EnterSelect(4, 4);
                _vimBuffer.ProcessNotation("and <Esc>");
                Assert.Equal("dog and bear", _textBuffer.GetLine(0).GetText());
                _vimBuffer.ProcessNotation("u");
                Assert.Equal("dog cat bear", _textBuffer.GetLine(0).GetText());
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

            /// <summary>
            /// Make sure 'v' with 'selectmode=cmd' enters select mode
            /// </summary>
            [Fact]
            public void SelectCharacterModeWithSelectModeCommand()
            {
                Create("cat dog");
                _globalSettings.SelectMode = "cmd";
                _vimBuffer.ProcessNotation("v");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// Make sure 'V' with 'selectmode=cmd' enters select mode
            /// </summary>
            [Fact]
            public void SelectLineModeWithSelectModeCommand()
            {
                Create("cat dog");
                _globalSettings.SelectMode = "cmd";
                _vimBuffer.ProcessNotation("V");
                Assert.Equal(ModeKind.SelectLine, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// Make sure 'C-v' with 'selectmode=cmd' enters select mode
            /// </summary>
            [Fact]
            public void SelectBlockModeWithSelectModeCommand()
            {
                Create("cat dog");
                _globalSettings.SelectMode = "cmd";
                _vimBuffer.ProcessNotation("<C-v>");
                Assert.Equal(ModeKind.SelectBlock, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// Make sure 'C-q' with 'selectmode=cmd' enters select mode
            /// </summary>
            [Fact]
            public void SelectBlockModeAlternateWithSelectModeCommand()
            {
                Create("cat dog");
                _globalSettings.SelectMode = "cmd";
                _vimBuffer.ProcessNotation("<C-q>");
                Assert.Equal(ModeKind.SelectBlock, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// Make sure 'C-o' works from select mode
            /// </summary>
            [Fact]
            public void SelectOneTimeCommand()
            {
                Create("cat dog eel");
                _globalSettings.Selection = "exclusive";
                _vimBuffer.ProcessNotation("wgh");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal("d", _textView.GetSelectionSpan().GetText());
                _vimBuffer.ProcessNotation("<C-o>");
                Assert.Equal(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
                Assert.Equal("d", _textView.GetSelectionSpan().GetText());
                _vimBuffer.ProcessNotation("w");
                Assert.Equal("dog ", _textView.GetSelectionSpan().GetText());
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                _vimBuffer.ProcessNotation("bear ");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal("cat bear eel", _textBuffer.GetLine(0).GetText());
            }

            [Fact]
            public void Issue1317()
            {
                Create("hello world");
                _vimBuffer.ProcessNotation("vl");
                Assert.False(_vimBuffer.CanProcess(VimKey.LeftDrag));
            }
        }
    }
}
