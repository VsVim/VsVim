using System;
using Vim.EditorHost;
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
        protected TestableMouseDevice _testableMouseDevice;

        protected virtual void Create(params string[] lines)
        {
            _vimBuffer = CreateVimBuffer(lines);
            _textView = _vimBuffer.TextView;
            _textBuffer = _vimBuffer.TextBuffer;
            _globalSettings = _vimBuffer.GlobalSettings;
            _globalSettings.SelectModeOptions = SelectModeOptions.Mouse | SelectModeOptions.Keyboard;
            _textSelection = _textView.Selection;
            _testableMouseDevice = (TestableMouseDevice)MouseDevice;
            _testableMouseDevice.IsLeftButtonPressed = false;
            _testableMouseDevice.Point = null;
        }

        public override void Dispose()
        {
            _testableMouseDevice.IsLeftButtonPressed = false;
            _testableMouseDevice.Point = null;
            base.Dispose();
        }

        protected void EnterSelect(int start, int length)
        {
            var span = new SnapshotSpan(_textBuffer.CurrentSnapshot, start, length);
            _textView.SelectAndMoveCaret(span);
            DoEvents();
            Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
        }

        public sealed class Enter : SelectModeIntegrationTest
        {
            [WpfFact]
            public void SelectOfText()
            {
                Create("cat dog");
                _textSelection.Select(0, 3);
                DoEvents();
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// Extending the selection should just ask select mode to reset it's information
            /// </summary>
            [WpfFact]
            public void ExtendSelection()
            {
                Create("cat dog");
                _textSelection.Select(0, 3);
                DoEvents();
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                _textSelection.Select(0, 5);
                DoEvents();
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
            }

            [WpfFact]
            public void CommandToCharacter()
            {
                Create("cat");
                _vimBuffer.ProcessNotation("gh");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
            }

            [WpfFact]
            public void CommandToLine()
            {
                Create("cat");
                _vimBuffer.ProcessNotation("gH");
                Assert.Equal(ModeKind.SelectLine, _vimBuffer.ModeKind);
            }

            [WpfFact]
            public void CommandToBlock()
            {
                Create("cat");
                _vimBuffer.ProcessNotation("g<C-H>");
                Assert.Equal(ModeKind.SelectBlock, _vimBuffer.ModeKind);
            }

        }

        public sealed class LeftMouseTest : SelectModeIntegrationTest
        {
            [WpfFact]
            public void ExclusiveDrag()
            {
                Create("cat dog bear", "");
                _globalSettings.Selection = "exclusive";
                var startPoint = _textView.GetPointInLine(0, 4); // 'd' in 'dog'
                _testableMouseDevice.Point = startPoint;
                _vimBuffer.ProcessNotation("<LeftMouse>");
                Assert.Equal(startPoint.Position, _textView.GetCaretPoint().Position);
                _vimBuffer.ProcessNotation("<LeftDrag>");
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind); // still normal
                var midPoint = _textView.GetPointInLine(0, 5); // 'o' in 'dog'
                _testableMouseDevice.Point = midPoint;
                _vimBuffer.ProcessNotation("<LeftDrag>");
                Assert.Equal("d", _textView.GetSelectionSpan().GetText());
                Assert.Equal(midPoint.Position, _textView.GetCaretPoint().Position);
                var endPoint = _textView.GetPointInLine(0, 7); // ' ' after 'dog'
                _testableMouseDevice.Point = endPoint;
                _vimBuffer.ProcessNotation("<LeftRelease>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal("dog", _textView.GetSelectionSpan().GetText());
                Assert.Equal(endPoint.Position, _textView.GetCaretPoint().Position);
            }

            [WpfFact]
            public void InclusiveDrag()
            {
                Create("cat dog bear", "");
                _globalSettings.Selection = "inclusive";
                var startPoint = _textView.GetPointInLine(0, 4); // 'd' in 'dog'
                _testableMouseDevice.Point = startPoint;
                _vimBuffer.ProcessNotation("<LeftMouse>");
                Assert.Equal(startPoint.Position, _textView.GetCaretPoint().Position);
                _vimBuffer.ProcessNotation("<LeftDrag>");
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind); // still normal
                var midPoint = _textView.GetPointInLine(0, 5); // 'o' in 'dog'
                _testableMouseDevice.Point = midPoint;
                _vimBuffer.ProcessNotation("<LeftDrag>");
                Assert.Equal("do", _textView.GetSelectionSpan().GetText());
                Assert.Equal(midPoint.Position, _textView.GetCaretPoint().Position);
                var endPoint = _textView.GetPointInLine(0, 6); // 'g' in 'dog'
                _testableMouseDevice.Point = endPoint;
                _vimBuffer.ProcessNotation("<LeftRelease>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal("dog", _textView.GetSelectionSpan().GetText());
                Assert.Equal(endPoint.Position, _textView.GetCaretPoint().Position);
            }

            [WpfFact]
            public void InsertDrag()
            {
                Create("cat dog bear", "");
                _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                var startPoint = _textView.GetPointInLine(0, 4); // 'd' in 'dog'
                _testableMouseDevice.Point = startPoint;
                _vimBuffer.ProcessNotation("<LeftMouse>");
                Assert.Equal(startPoint.Position, _textView.GetCaretPoint().Position);
                _vimBuffer.ProcessNotation("<LeftDrag>");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind); // still insert
                var midPoint = _textView.GetPointInLine(0, 5); // 'o' in 'dog'
                _testableMouseDevice.Point = midPoint;
                _vimBuffer.ProcessNotation("<LeftDrag>");
                Assert.Equal("do", _textView.GetSelectionSpan().GetText());
                Assert.Equal(midPoint.Position, _textView.GetCaretPoint().Position);
                var endPoint = _textView.GetPointInLine(0, 6); // 'g' in 'dog'
                _testableMouseDevice.Point = endPoint;
                _vimBuffer.ProcessNotation("<LeftRelease>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal("dog", _textView.GetSelectionSpan().GetText());
                Assert.Equal(endPoint.Position, _textView.GetCaretPoint().Position);
                _testableMouseDevice.Point = startPoint;
                _vimBuffer.ProcessNotation("<LeftMouse><LeftRelease>");
                Assert.Equal(startPoint.Position, _textView.GetCaretPoint().Position);
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind); // back to insert
            }

            [WpfFact]
            public void ExclusiveShiftClick()
            {
                Create("cat dog bear", "");
                _globalSettings.Selection = "exclusive";
                var startPoint = _textView.GetPointInLine(0, 4); // 'd' in 'dog'
                _testableMouseDevice.Point = startPoint;
                _vimBuffer.ProcessNotation("<LeftMouse><LeftRelease>");
                Assert.Equal(startPoint.Position, _textView.GetCaretPoint().Position);
                var endPoint = _textView.GetPointInLine(0, 7); // ' ' after 'dog'
                _testableMouseDevice.Point = endPoint;
                _vimBuffer.ProcessNotation("<S-LeftMouse><S-LeftRelease>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal("dog", _textView.GetSelectionSpan().GetText());
                Assert.Equal(endPoint.Position, _textView.GetCaretPoint().Position);
            }

            [WpfFact]
            public void InclusiveShiftClick()
            {
                Create("cat dog bear", "");
                _globalSettings.Selection = "inclusive";
                var startPoint = _textView.GetPointInLine(0, 4); // 'd' in 'dog'
                _testableMouseDevice.Point = startPoint;
                _vimBuffer.ProcessNotation("<LeftMouse><LeftRelease>");
                Assert.Equal(startPoint.Position, _textView.GetCaretPoint().Position);
                var endPoint = _textView.GetPointInLine(0, 6); // 'g' in 'dog'
                _testableMouseDevice.Point = endPoint;
                _vimBuffer.ProcessNotation("<S-LeftMouse><S-LeftRelease>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal("dog", _textView.GetSelectionSpan().GetText());
                Assert.Equal(endPoint.Position, _textView.GetCaretPoint().Position);
            }

            [WpfFact]
            public void InsertShiftClick()
            {
                Create("cat dog bear", "");
                _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                var startPoint = _textView.GetPointInLine(0, 4); // 'd' in 'dog'
                _testableMouseDevice.Point = startPoint;
                _vimBuffer.ProcessNotation("<LeftMouse><LeftRelease>");
                Assert.Equal(startPoint.Position, _textView.GetCaretPoint().Position);
                var endPoint = _textView.GetPointInLine(0, 6); // 'g' in 'dog'
                _testableMouseDevice.Point = endPoint;
                _vimBuffer.ProcessNotation("<S-LeftMouse><S-LeftRelease>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal("dog", _textView.GetSelectionSpan().GetText());
                Assert.Equal(endPoint.Position, _textView.GetCaretPoint().Position);
                _testableMouseDevice.Point = startPoint;
                _vimBuffer.ProcessNotation("<LeftMouse><LeftRelease>");
                Assert.Equal(startPoint.Position, _textView.GetCaretPoint().Position);
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind); // back to insert
            }

            [WpfFact]
            public void LinewiseShiftClick()
            {
                Create("cat dog bear", "pig horse bat", "");
                _globalSettings.SelectMode = "cmd";
                _vimBuffer.ProcessNotation("V");
                Assert.Equal(ModeKind.SelectLine, _vimBuffer.ModeKind);
                Assert.Equal(_textBuffer.GetLineRange(0).ExtentIncludingLineBreak,
                    _textView.GetSelectionSpan());
                var point = _textView.GetPointInLine(1, 5); // 'o' in 'horse'
                _testableMouseDevice.Point = point;
                _vimBuffer.ProcessNotation("<S-LeftMouse><LeftRelease>");
                Assert.Equal(ModeKind.SelectLine, _vimBuffer.ModeKind);
                Assert.Equal(_textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak,
                    _textView.GetSelectionSpan());
                Assert.Equal(point.Position, _textView.GetCaretPoint().Position);
            }

            [WpfFact]
            public void ExclusiveDoubleClick()
            {
                Create("cat dog bear", "");
                _globalSettings.Selection = "exclusive";
                var point = _textView.GetPointInLine(0, 5); // 'o' in 'dog'
                _testableMouseDevice.Point = point;
                _vimBuffer.ProcessNotation("<LeftMouse><LeftRelease><2-LeftMouse><LeftRelease>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal("dog", _textView.GetSelectionSpan().GetText());
                Assert.Equal(7, _textView.GetCaretPoint().Position); // ' ' after 'dog'
            }

            [WpfFact]
            public void InclusiveDoubleClick()
            {
                Create("cat dog bear", "");
                _globalSettings.Selection = "inclusive";
                var point = _textView.GetPointInLine(0, 5); // 'o' in 'dog'
                _testableMouseDevice.Point = point;
                _vimBuffer.ProcessNotation("<LeftMouse><LeftRelease><2-LeftMouse><LeftRelease>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal("dog", _textView.GetSelectionSpan().GetText());
                Assert.Equal(6, _textView.GetCaretPoint().Position); // 'g' in 'dog'
            }

            [WpfFact]
            public void ExclusiveDoubleClickAndDrag()
            {
                Create("cat dog bear bat", "");
                _globalSettings.Selection = "exclusive";
                _testableMouseDevice.Point = _textView.GetPointInLine(0, 5); // 'o' in 'dog'
                _vimBuffer.ProcessNotation("<LeftMouse><LeftRelease><2-LeftMouse>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal("dog", _textView.GetSelectionSpan().GetText());
                Assert.Equal(7, _textView.GetCaretPoint().Position); // ' ' after 'dog'
                _testableMouseDevice.Point = _textView.GetPointInLine(0, 9); // 'e' in 'bear'
                _vimBuffer.ProcessNotation("<LeftDrag>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal("dog bear", _textView.GetSelectionSpan().GetText());
                Assert.Equal(12, _textView.GetCaretPoint().Position); // ' ' after 'bear'
                _testableMouseDevice.Point = _textView.GetPointInLine(0, 1); // 'a' in 'cat'
                _vimBuffer.ProcessNotation("<LeftDrag>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal("cat ", _textView.GetSelectionSpan().GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position); // 'c' in 'cat'
                _testableMouseDevice.Point = _textView.GetPointInLine(0, 9); // 'e' in 'bear'
                _vimBuffer.ProcessNotation("<LeftRelease>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal("dog bear", _textView.GetSelectionSpan().GetText());
                Assert.Equal(12, _textView.GetCaretPoint().Position); // ' ' after 'bear'
            }

            [WpfFact]
            public void InclusiveDoubleClickAndDrag()
            {
                Create("cat dog bear bat", "");
                _globalSettings.Selection = "inclusive";
                _testableMouseDevice.Point = _textView.GetPointInLine(0, 5); // 'o' in 'dog'
                _vimBuffer.ProcessNotation("<LeftMouse><LeftRelease><2-LeftMouse>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal("dog", _textView.GetSelectionSpan().GetText());
                Assert.Equal(6, _textView.GetCaretPoint().Position); // 'g' in 'dog'
                _testableMouseDevice.Point = _textView.GetPointInLine(0, 9); // 'e' in 'bear'
                _vimBuffer.ProcessNotation("<LeftDrag>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal("dog bear", _textView.GetSelectionSpan().GetText());
                Assert.Equal(11, _textView.GetCaretPoint().Position); // 'r' in 'bear'
                _testableMouseDevice.Point = _textView.GetPointInLine(0, 1); // 'a' in 'cat'
                _vimBuffer.ProcessNotation("<LeftDrag>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal("cat dog", _textView.GetSelectionSpan().GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position); // 'c' in 'cat'
                _testableMouseDevice.Point = _textView.GetPointInLine(0, 9); // 'e' in 'bear'
                _vimBuffer.ProcessNotation("<LeftRelease>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal("dog bear", _textView.GetSelectionSpan().GetText());
                Assert.Equal(11, _textView.GetCaretPoint().Position); // 'r' in 'bear'
            }

            [WpfFact]
            public void TripleClick()
            {
                Create("cat dog bear", "pig horse bat", "");
                var point = _textView.GetPointInLine(1, 5); // 'o' in 'horse'
                _testableMouseDevice.Point = point;
                _vimBuffer.ProcessNotation("<LeftMouse><LeftRelease><2-LeftMouse><LeftRelease><3-LeftMouse><LeftRelease>");
                Assert.Equal(ModeKind.SelectLine, _vimBuffer.ModeKind);
                Assert.Equal(_textBuffer.GetLineRange(1).ExtentIncludingLineBreak,
                    _textView.GetSelectionSpan());
                Assert.Equal(point.Position, _textView.GetCaretPoint().Position);
            }

            [WpfFact]
            public void QuadrupleClick()
            {
                Create("cat dog bear", "");
                var point = _textView.GetPointInLine(0, 5); // 'o' in 'dog'
                _testableMouseDevice.Point = point;
                _vimBuffer.ProcessNotation("<LeftMouse><LeftRelease><2-LeftMouse><LeftRelease><3-LeftMouse><LeftRelease><4-LeftMouse><LeftRelease>");
                Assert.Equal(ModeKind.SelectBlock, _vimBuffer.ModeKind);
                Assert.Equal("o", _textView.GetSelectionSpan().GetText());
                Assert.Equal(point.Position, _textView.GetCaretPoint().Position);
            }
        }

        public sealed class SpecialKeysFromNormal : SelectModeIntegrationTest
        {
            [WpfFact]
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

            [WpfFact]
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

            [WpfFact]
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

            [WpfFact]
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

            [WpfFact]
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

            [WpfFact]
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

            [WpfFact]
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

            [WpfFact]
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

            [WpfFact]
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

            [WpfFact]
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

            [WpfFact]
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

            [WpfFact]
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

            [WpfFact]
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

            [WpfFact]
            public void ShiftRightToNothing_Normal()
            {
                Create("cat dog");
                _vimBuffer.ProcessNotation("<S-Right>");
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                Assert.Equal(4, _textView.GetCaretPoint().Position);
            }

            [WpfFact]
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

            [WpfFact]
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

            [WpfFact]
            public void ShiftLeftToNothing_Normal()
            {
                Create("cat dog");
                _textView.MoveCaretTo(4);
                _vimBuffer.ProcessNotation("<S-Left>");
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            [WpfFact]
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

            [WpfFact]
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
            [WpfFact]
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

            [WpfFact]
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

            [WpfFact]
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

            [WpfFact]
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

            [WpfFact]
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

            [WpfFact]
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

            [WpfFact]
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

            [WpfFact]
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

            [WpfFact]
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

            [WpfFact]
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

            [WpfFact]
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

            [WpfFact]
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

            [WpfFact]
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

            [WpfFact]
            public void ShiftRightToNothing_Insert()
            {
                Create("cat dog");
                _vimBuffer.ProcessNotation("i");
                _vimBuffer.ProcessNotation("<S-Right>");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal(4, _textView.GetCaretPoint().Position);
            }

            [WpfFact]
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

            [WpfFact]
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

            [WpfFact]
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
            [WpfFact]
            public void SimpleAlpha()
            {
                Create("Test time");
                EnterSelect(1, 3);
                _vimBuffer.Process('o');
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal("To time", _textBuffer.GetLine(0).GetText());
                Assert.Equal(2, _textView.GetCaretPoint().Position);
            }

            [WpfFact]
            public void SimpleNumeric()
            {
                Create("Test time");
                EnterSelect(1, 3);
                _vimBuffer.Process('3');
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal("T3 time", _textBuffer.GetLine(0).GetText());
                Assert.Equal(2, _textView.GetCaretPoint().Position);
            }

            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
            public void Right()
            {
                Create("cat");
                _vimBuffer.ProcessNotation("gh<Right>");
                Assert.Equal("ca", _textView.GetSelectionSpan().GetText());
                Assert.Equal(1, _textView.GetCaretPoint().Position);
            }

            [WpfFact]
            public void Left()
            {
                Create("cat");
                _vimBuffer.ProcessNotation("lgh<Left>");
                Assert.Equal("ca", _textView.GetSelectionSpan().GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            [WpfFact]
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

            [WpfFact]
            public void Right()
            {
                Create("cat");
                _vimBuffer.ProcessNotation("gh<Right>");
                Assert.Equal(1, _textView.GetCaretPoint().Position);
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                Assert.True(_textView.Selection.IsEmpty);
            }

            [WpfFact]
            public void Left()
            {
                Create("cat");
                _vimBuffer.ProcessNotation("lgh<Left>");
                Assert.Equal(0, _textView.GetCaretPoint().Position);
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                Assert.True(_textView.Selection.IsEmpty);
            }

            [WpfFact]
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
            [WpfFact]
            public void ShiftRight()
            {
                Create("cat");
                _vimBuffer.ProcessNotation("gh<S-Right>");
                Assert.Equal("ca", _textView.GetSelectionSpan().GetText());
                Assert.Equal(1, _textView.GetCaretPoint().Position);
            }

            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
            public void SelectOneTimeCommand()
            {
                Create("cat dog eel");
                _globalSettings.Selection = "exclusive";
                _vimBuffer.ProcessNotation("wgh");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal("", _textView.GetSelectionSpan().GetText());
                _vimBuffer.ProcessNotation("<C-o>");
                Assert.Equal(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
                Assert.Equal("", _textView.GetSelectionSpan().GetText());
                _vimBuffer.ProcessNotation("w");
                Assert.Equal("dog ", _textView.GetSelectionSpan().GetText());
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                _vimBuffer.ProcessNotation("bear ");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal("cat bear eel", _textBuffer.GetLine(0).GetText());
            }    

            [WpfFact]
            public void SelectOneTimeCommand_Esc()
            {
                Create("cat dog eel");
                _vimBuffer.ProcessNotation("gh<C-o><Esc>");
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
            }   

            [WpfFact]
            public void SelectOneTimeCommand_ExCommand_Esc()
            {
                Create("cat dog eel");
                _vimBuffer.ProcessNotation("gh<C-o>:");
                Assert.Equal(ModeKind.Command, _vimBuffer.ModeKind);
                _vimBuffer.ProcessNotation("<Esc>");
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
            }   

            [WpfFact]
            public void SelectOneTimeCommand_ExCommand()
            {
                Create("cat dog eel");
                _vimBuffer.ProcessNotation("gh<C-o>:pwd<CR>");
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
            }   
            
            [WpfFact]
            public void Issue1317()
            {
                Create("hello world");
                _vimBuffer.ProcessNotation("vl");
                Assert.False(_vimBuffer.CanProcess(VimKey.RightMouse));
            }
        }
    }
}
