using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Vim.UnitTest.Exports;
using Vim.UnitTest.Mock;
using Xunit;

namespace Vim.UnitTest
{
    /// <summary>
    /// Class for testing the full integration story of multiple carets
    /// </summary>
    public abstract class MultiSelectionIntegrationTest : VimTestBase
    {
        protected MockSelectionUtil _mockMultiSelectionUtil;
        protected IVimBuffer _vimBuffer;
        protected IWpfTextView _textView;
        protected ITextBuffer _textBuffer;
        protected IVimGlobalSettings _globalSettings;
        protected IVimLocalSettings _localSettings;
        protected IVimWindowSettings _windowSettings;
        protected ISelectionUtil _selectionUtil;
        protected TestableMouseDevice _testableMouseDevice;

        protected virtual void Create(params string[] lines)
        {
            _textView = CreateTextView(lines);
            _textBuffer = _textView.TextBuffer;
            _mockMultiSelectionUtil = new MockSelectionUtil(_textView, isMultiSelectionSupported: true);
            VimHost.TryCustomProcessFunc = (_, command) => _mockMultiSelectionUtil.TryCustomProcess(command);
            var vimBufferData = CreateVimBufferData(_textView, null, null, null, null, _mockMultiSelectionUtil);
            _vimBuffer = Vim.CreateVimBufferWithData(vimBufferData);
            _localSettings = _vimBuffer.LocalSettings;
            _globalSettings = _localSettings.GlobalSettings;
            _windowSettings = _vimBuffer.WindowSettings;
            _selectionUtil = _vimBuffer.VimBufferData.SelectionUtil;

            _testableMouseDevice = (TestableMouseDevice)MouseDevice;
            _testableMouseDevice.IsLeftButtonPressed = false;
            _testableMouseDevice.Point = null;
        }

        private void Create(bool isInclusive, params string[] lines)
        {
            Create(lines);
            _globalSettings.Selection = isInclusive ? "inclusive" : "exclusive";
        }

        public override void Dispose()
        {
            _testableMouseDevice.IsLeftButtonPressed = false;
            _testableMouseDevice.Point = null;
            base.Dispose();
        }

        private void ProcessNotation(string notation)
        {
            var keyInputSet = KeyNotationUtil.StringToKeyInputSet(notation);
            foreach (var keyInput in keyInputSet.KeyInputs)
            {
                _vimBuffer.Process(keyInput);
                DoEvents();
            }
        }

        private VirtualSnapshotPoint GetPoint(int lineNumber, int column)
        {
            return _textView.GetVirtualPointInLine(lineNumber, column);
        }

        private SelectedSpan[] SelectedSpans =>
            _selectionUtil.GetSelectedSpans().ToArray();

        private SnapshotPoint[] CaretPoints =>
            SelectedSpans.Select(x => x.CaretPoint.Position).ToArray();

        private VirtualSnapshotPoint[] CaretVirtualPoints =>
            SelectedSpans.Select(x => x.CaretPoint).ToArray();

        private void SetSelectedSpans(params SelectedSpan[] selectedSpans)
        {
            _selectionUtil.SetSelectedSpans(selectedSpans);
        }

        private void SetCaretPoints(params VirtualSnapshotPoint[] caretPoints)
        {
            SetSelectedSpans(caretPoints.Select(x => new SelectedSpan(x)).ToArray());
        }

        private void AssertCarets(params VirtualSnapshotPoint[] expectedCarets)
        {
            AssertSelections(expectedCarets.Select(x => new SelectedSpan(x)).ToArray());
        }

        private void AssertSelections(params SelectedSpan[] expectedSpans)
        {
            Assert.Equal(expectedSpans, SelectedSpans);
        }

        private void AssertSelectionsAdjustCaret(params SelectedSpan[] expectedSpans)
        {
            if (!_globalSettings.IsSelectionInclusive)
            {
                AssertSelections(expectedSpans);
                return;
            }
            var adjustedExpectedSpans =
                expectedSpans.Select(x => x.AdjustCaretForInclusive())
                .ToArray();
            Assert.Equal(adjustedExpectedSpans, SelectedSpans);
        }

        private void AssertSelectionsAdjustEnd(params SelectedSpan[] expectedSpans)
        {
            if (!_globalSettings.IsSelectionInclusive)
            {
                AssertSelections(expectedSpans);
                return;
            }
            var adjustedExpectedSpans =
                expectedSpans.Select(x => x.AdjustEndForInclusive())
                .ToArray();
            Assert.Equal(adjustedExpectedSpans, SelectedSpans);
        }

        private void AssertLines(params string[] lines)
        {
            Assert.Equal(lines, _textBuffer.GetLines().ToArray());
        }

        public sealed class MockTest : MultiSelectionIntegrationTest
        {
            /// <summary>
            /// Mock inftrastructure should use the real text view for the
            /// primary selection and the internal data structure for the
            /// secondary selection
            /// </summary>
            [WpfFact]
            public void Basic()
            {
                Create("cat", "bat", "");
                SetCaretPoints(
                    _textView.GetVirtualPointInLine(0, 1),
                    _textView.GetVirtualPointInLine(1, 1));

                // Verify real caret and real selection.
                Assert.Equal(
                    _textView.GetVirtualPointInLine(0, 1),
                    _textView.GetCaretVirtualPoint());
                Assert.Equal(
                    new VirtualSnapshotSpan(new SnapshotSpan(_textView.GetPointInLine(0, 1), 0)),
                    _textView.GetVirtualSelectionSpan());

                // Verify secondary selection agrees with mock vim host.
                Assert.Single(_mockMultiSelectionUtil.SecondarySelectedSpans);
                Assert.Equal(
                    new SelectedSpan(_textView.GetVirtualPointInLine(1, 1)),
                    _mockMultiSelectionUtil.SecondarySelectedSpans[0]);
            }
        }

        public sealed class MultiSelectionTrackerTest : MultiSelectionIntegrationTest
        {
            [WpfFact]
            public void RestoreCarets()
            {
                Create("abc def ghi", "jkl mno pqr", "");
                _globalSettings.StartOfLine = false;
                SetCaretPoints(GetPoint(0, 4), GetPoint(1, 4));
                ProcessNotation(":1<CR>");
                AssertCarets(GetPoint(0, 4), GetPoint(1, 4));
            }

            [WpfFact]
            public void MoveCarets()
            {
                Create("abc def ghi", "jkl mno pqr", "stu vwx yz.", "");
                _globalSettings.StartOfLine = false;
                SetCaretPoints(GetPoint(0, 4), GetPoint(1, 4));
                ProcessNotation(":2<CR>");
                AssertCarets(GetPoint(1, 4), GetPoint(2, 4));
            }

            [WpfTheory, InlineData(false), InlineData(true)]
            public void ExternalSelection(bool isInclusive)
            {
                Create(isInclusive, "abc def ghi", "jkl mno pqr", "");

                // Primary selection.
                _textView.Caret.MoveTo(GetPoint(0, 7));
                _textView.Selection.Select(GetPoint(0, 4), GetPoint(0, 7));
                DoEvents();
                Assert.Equal(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
                AssertSelectionsAdjustCaret(
                    GetPoint(0, 7).GetSelectedSpan(-3, 0, false)); // 'def|*' or 'de|f*'

                // Secondary selection.
                SetSelectedSpans(
                    SelectedSpans[0],
                    GetPoint(1, 7).GetSelectedSpan(-3, 0, false)); // 'mno|*'
                DoEvents();
                Assert.Equal(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
                AssertSelectionsAdjustCaret(
                    GetPoint(0, 7).GetSelectedSpan(-3, 0, false), // 'def|*' or 'de|f*'
                    GetPoint(1, 7).GetSelectedSpan(-3, 0, false)); // 'mno|*' or 'mn|o*'
            }
        }

        public sealed class AddCaretTest : MultiSelectionIntegrationTest
        {
            /// <summary>
            /// Using alt-click adds a new caret
            /// </summary>
            [WpfFact]
            public void AddCaret()
            {
                Create("abc def ghi", "jkl mno pqr", "");
                _textView.SetVisibleLineCount(2);
                SetCaretPoints(GetPoint(0, 4));
                _testableMouseDevice.Point = GetPoint(1, 8).Position; // 'g' in 'ghi'
                ProcessNotation("<C-A-LeftMouse><C-A-LeftRelease>");
                AssertCarets(GetPoint(0, 4), GetPoint(1, 8));
            }

            [WpfFact]
            public void RemovePrimaryCaret()
            {
                Create("abc def ghi", "jkl mno pqr", "");
                _textView.SetVisibleLineCount(2);
                SetCaretPoints(GetPoint(0, 4));
                _testableMouseDevice.Point = GetPoint(1, 8).Position; // 'g' in 'ghi'
                ProcessNotation("<C-A-LeftMouse><C-A-LeftRelease>");
                AssertCarets(GetPoint(0, 4), GetPoint(1, 8));
                _testableMouseDevice.Point = GetPoint(0, 4).Position; // 'd' in 'def'
                ProcessNotation("<C-A-LeftMouse><C-A-LeftRelease>");
                AssertCarets(GetPoint(1, 8));
            }

            [WpfFact]
            public void RemoveSecondaryCaret()
            {
                Create("abc def ghi", "jkl mno pqr", "");
                _textView.SetVisibleLineCount(2);
                SetCaretPoints(GetPoint(0, 4));
                _testableMouseDevice.Point = GetPoint(1, 8).Position; // 'g' in 'ghi'
                ProcessNotation("<C-A-LeftMouse><C-A-LeftRelease>");
                AssertCarets(GetPoint(0, 4), GetPoint(1, 8));
                ProcessNotation("<C-A-LeftMouse><C-A-LeftRelease>"); // 'g' in 'ghi'
                AssertCarets(GetPoint(0, 4));
            }

            [WpfFact]
            public void RemoveOnlyCaret()
            {
                Create("abc def ghi", "jkl mno pqr", "");
                _textView.SetVisibleLineCount(2);
                SetCaretPoints(GetPoint(0, 4));
                _testableMouseDevice.Point = GetPoint(0, 4).Position; // 'd' in 'def'
                ProcessNotation("<C-A-LeftMouse><C-A-LeftRelease>");
                AssertCarets(GetPoint(0, 4));
            }

            /// <summary>
            /// Using ctrl-alt-arrow adds a new caret
            /// </summary>
            [WpfFact]
            public void AddOnLineAbove()
            {
                Create("abc def ghi", "jkl mno pqr", "");
                _textView.Caret.MoveTo(GetPoint(1, 4));
                ProcessNotation("<C-A-Up>");
                AssertCarets(GetPoint(1, 4), GetPoint(0, 4));
            }

            /// <summary>
            /// Using ctrl-alt-arrow adds a new caret
            /// </summary>
            [WpfFact]
            public void AddOnLineBelow()
            {
                Create("abc def ghi", "jkl mno pqr", "");
                _textView.Caret.MoveTo(GetPoint(0, 4));
                ProcessNotation("<C-A-Down>");
                AssertCarets(GetPoint(0, 4), GetPoint(1, 4));
            }

            /// <summary>
            /// Using ctrl-alt-arrow adds a new caret
            /// </summary>
            [WpfFact]
            public void AddTwoAboveAndBelow()
            {
                Create(
                    "abc def ghi",
                    "jkl mno pqr",
                    "abc def ghi",
                    "jkl mno pqr",
                    "abc def ghi",
                    "jkl mno pqr",
                    "");
                _textView.Caret.MoveTo(GetPoint(2, 4));
                ProcessNotation("<C-A-Up><C-A-Up><C-A-Down><C-A-Down>");
                AssertCarets(
                    GetPoint(2, 4),
                    GetPoint(0, 4),
                    GetPoint(1, 4),
                    GetPoint(3, 4),
                    GetPoint(4, 4));
            }
        }

        public sealed class NormalModeTest : MultiSelectionIntegrationTest
        {
            /// <summary>
            /// Test clearing carets with escape
            /// </summary>
            [WpfFact]
            public void ClearCarets()
            {
                Create("abc def ghi", "jkl mno pqr", "");
                SetCaretPoints(GetPoint(0, 4), GetPoint(1, 4));
                ProcessNotation("<Esc>");
                AssertCarets(GetPoint(0, 4));
            }

            /// <summary>
            /// Test cancelling with control-C
            /// </summary>
            [WpfFact]
            public void CancelOperation()
            {
                Create("abc def ghi", "jkl mno pqr", "");
                SetCaretPoints(GetPoint(0, 4), GetPoint(1, 4));
                ProcessNotation("<C-C>");
                AssertCarets(GetPoint(0, 4));
            }

            /// <summary>
            /// Test restoring carets
            /// </summary>
            [WpfFact]
            public void RestoreCarets()
            {
                Create("abc def ghi", "jkl mno pqr", "");
                SetCaretPoints(GetPoint(0, 4), GetPoint(1, 4));
                ProcessNotation("<C-C>");
                AssertCarets(GetPoint(0, 4));
                ProcessNotation("<C-A-p>");
                SetCaretPoints(GetPoint(0, 4), GetPoint(1, 4));
            }

            /// <summary>
            /// Test moving the caret
            /// </summary>
            [WpfFact]
            public void Motion()
            {
                Create("abc def ghi", "jkl mno pqr", "");
                SetCaretPoints(GetPoint(0, 4), GetPoint(1, 4));
                ProcessNotation("w");
                AssertCarets(GetPoint(0, 8), GetPoint(1, 8));
            }

            /// <summary>
            /// Test inserting text
            /// </summary>
            [WpfFact]
            public void Insert()
            {
                Create("abc def ghi", "jkl mno pqr", "");
                SetCaretPoints(GetPoint(0, 4), GetPoint(1, 4));
                ProcessNotation("ixxx <Esc>");
                AssertLines("abc xxx def ghi", "jkl xxx mno pqr", "");
                AssertCarets(GetPoint(0, 7), GetPoint(1, 7));
            }

            /// <summary>
            /// Test undoing inserted text
            /// </summary>
            [WpfFact]
            public void UndoInsert()
            {
                Create("abc def ghi", "jkl mno pqr", "");
                SetCaretPoints(GetPoint(0, 4), GetPoint(1, 4));
                ProcessNotation("ixxx <Esc>");
                AssertLines("abc xxx def ghi", "jkl xxx mno pqr", "");
                AssertCarets(GetPoint(0, 7), GetPoint(1, 7));
                ProcessNotation("u");
                AssertLines("abc def ghi", "jkl mno pqr", "");
                AssertCarets(GetPoint(0, 4), GetPoint(1, 4));
            }

            /// <summary>
            /// Test repeating inserted text
            /// </summary>
            [WpfFact]
            public void RepeatInsert()
            {
                Create("abc def ghi", "jkl mno pqr", "");
                SetCaretPoints(GetPoint(0, 4), GetPoint(1, 4));
                ProcessNotation("ixxx <Esc>ww.");
                AssertLines("abc xxx def xxx ghi", "jkl xxx mno xxx pqr", "");
                AssertCarets(GetPoint(0, 15), GetPoint(1, 15));
            }

            /// <summary>
            /// Test deleting the word at the caret
            /// </summary>
            [WpfFact]
            public void Delete()
            {
                Create("abc def ghi", "jkl mno pqr", "");
                SetCaretPoints(GetPoint(0, 4), GetPoint(1, 4));
                ProcessNotation("dw");
                AssertLines("abc ghi", "jkl pqr", "");
                AssertCarets(GetPoint(0, 4), GetPoint(1, 4));
            }

            /// <summary>
            /// Test changing the word at the caret
            /// </summary>
            [WpfFact]
            public void Change()
            {
                Create("abc def ghi", "jkl mno pqr", "");
                SetCaretPoints(GetPoint(0, 4), GetPoint(1, 4));
                ProcessNotation("cwxxx<Esc>");
                AssertLines("abc xxx ghi", "jkl xxx pqr", "");
                AssertCarets(GetPoint(0, 6), GetPoint(1, 6));
            }

            /// <summary>
            /// Test putting before word at the caret
            /// </summary>
            [WpfFact]
            public void Put()
            {
                Create("abc def ghi", "jkl mno pqr", "");
                ProcessNotation("yw");
                SetCaretPoints(GetPoint(0, 4), GetPoint(1, 4));
                ProcessNotation("wP");
                AssertLines("abc def abc ghi", "jkl mno abc pqr", "");
                AssertCarets(GetPoint(0, 11), GetPoint(1, 11));
            }

            /// <summary>
            /// Test deleting and putting the word at the caret
            /// </summary>
            [WpfTheory, InlineData(""), InlineData("unnamed")]
            public void DeleteAndPut(string clipboardSetting)
            {
                Create("abc def ghi jkl", "mno pqr stu vwx", "");
                _globalSettings.Clipboard = clipboardSetting;
                SetCaretPoints(GetPoint(0, 4), GetPoint(1, 4));
                ProcessNotation("dwwP");
                AssertLines("abc ghi def jkl", "mno stu pqr vwx", "");
                AssertCarets(GetPoint(0, 11), GetPoint(1, 11));
            }
        }

        public sealed class VisualCharacterModeTest : MultiSelectionIntegrationTest
        {
            /// <summary>
            /// Test entering visual character mode
            /// </summary>
            [WpfTheory, InlineData(false), InlineData(true)]
            public void Enter(bool isInclusive)
            {
                Create(isInclusive, "abc def ghi", "jkl mno pqr", "");
                SetCaretPoints(GetPoint(0, 4), GetPoint(1, 4));
                ProcessNotation("v");
                Assert.Equal(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
                AssertSelectionsAdjustEnd(
                    GetPoint(0, 4).GetSelectedSpan(0, 0, false), // '|*'def or '|d*'ef
                    GetPoint(1, 4).GetSelectedSpan(0, 0, false)); // '|*'mno or 'm*'no
            }

            /// <summary>
            /// Test moving the caret forward
            /// </summary>
            [WpfTheory, InlineData(false), InlineData(true)]
            public void MotionForward(bool isInclusive)
            {
                Create(isInclusive, "abc def ghi", "jkl mno pqr", "");
                SetCaretPoints(GetPoint(0, 4), GetPoint(1, 4));
                ProcessNotation("vw");
                AssertSelectionsAdjustEnd(
                    GetPoint(0, 8).GetSelectedSpan(-4, 0, false), // 'def |' or 'def |g*'
                    GetPoint(1, 8).GetSelectedSpan(-4, 0, false)); // 'mno |' or 'mno |p*'
            }

            /// <summary>
            /// Test adding a selection on an adjacent line
            /// </summary>
            [WpfTheory, InlineData(false), InlineData(true)]
            public void AddSelection(bool isInclusive)
            {
                Create(isInclusive, "abc def ghi", "jkl mno pqr", "");
                SetCaretPoints(GetPoint(0, 4));
                ProcessNotation("vw<C-A-Down>");
                AssertSelectionsAdjustEnd(
                    GetPoint(0, 8).GetSelectedSpan(-4, 0, false), // 'def |' or 'def |g*'
                    GetPoint(1, 8).GetSelectedSpan(-4, 0, false)); // 'mno |' or 'mno |p*'
            }

            /// <summary>
            /// Test moving the caret backward
            /// </summary>
            [WpfTheory, InlineData(false), InlineData(true)]
            public void MotionBackward(bool isInclusive)
            {
                Create(isInclusive, "abc def ghi", "jkl mno pqr", "");
                SetCaretPoints(GetPoint(0, 8), GetPoint(1, 8));
                ProcessNotation("vb");
                AssertSelectionsAdjustEnd(
                    GetPoint(0, 4).GetSelectedSpan(0, 4, true), // '|def '
                    GetPoint(1, 4).GetSelectedSpan(0, 4, true)); // '|mno '
            }

            /// <summary>
            /// Motion through zero width
            /// </summary>
            [WpfTheory, InlineData(false), InlineData(true)]
            public void MotionThroughZeroWidth(bool isInclusive)
            {
                Create(isInclusive, "abc def ghi", "jkl mno pqr", "");
                SetCaretPoints(GetPoint(0, 4), GetPoint(1, 4));
                ProcessNotation("vwbb");
                AssertSelectionsAdjustEnd(
                    GetPoint(0, 0).GetSelectedSpan(0, 4, true), // '|abc '
                    GetPoint(1, 0).GetSelectedSpan(0, 4, true)); // '|jkl '
            }

            /// <summary>
            /// Test deleting text
            /// </summary>
            [WpfTheory, InlineData(false), InlineData(true)]
            public void Delete(bool isInclusive)
            {
                Create(isInclusive, "abc def ghi", "jkl mno pqr", "");
                SetCaretPoints(GetPoint(0, 4), GetPoint(1, 4));
                ProcessNotation("veld");
                AssertLines("abc ghi", "jkl pqr", "");
                AssertCarets(GetPoint(0, 4), GetPoint(1, 4));
            }

            /// <summary>
            /// Test changing text
            /// </summary>
            [WpfTheory, InlineData(false), InlineData(true)]
            public void Change(bool isInclusive)
            {
                Create(isInclusive, "abc def ghi", "jkl mno pqr", "");
                SetCaretPoints(GetPoint(0, 4), GetPoint(1, 4));
                ProcessNotation("vec");
                AssertLines("abc  ghi", "jkl  pqr", "");
                AssertCarets(GetPoint(0, 4), GetPoint(1, 4));
                ProcessNotation("xxx<Esc>");
                AssertLines("abc xxx ghi", "jkl xxx pqr", "");
                AssertCarets(GetPoint(0, 6), GetPoint(1, 6));
            }

            /// <summary>
            /// Test changing four lines of text
            /// </summary>
            [WpfTheory, InlineData(false), InlineData(true)]
            public void ChangeFourLines(bool isInclusive)
            {
                Create(isInclusive, "abc def ghi", "jkl mno pqr", "abc def ghi", "jkl mno pqr", "");
                SetCaretPoints(GetPoint(0, 4), GetPoint(1, 4), GetPoint(2, 4), GetPoint(3, 4));
                ProcessNotation("vec");
                AssertLines("abc  ghi", "jkl  pqr", "abc  ghi", "jkl  pqr", "");
                AssertCarets(GetPoint(0, 4), GetPoint(1, 4), GetPoint(2, 4), GetPoint(3, 4));
                ProcessNotation("xxx<Esc>");
                AssertLines("abc xxx ghi", "jkl xxx pqr", "abc xxx ghi", "jkl xxx pqr", "");
                AssertCarets(GetPoint(0, 6), GetPoint(1, 6), GetPoint(2, 6), GetPoint(3, 6));
            }

            /// <summary>
            /// Spacing back and forth through the centerline
            /// </summary>
            [WpfTheory, InlineData(false), InlineData(true)]
            public void SpacingBackAndForth(bool isInclusive)
            {
                Create(isInclusive, "abc def ghi", "jkl mno pqr", "");
                SetCaretPoints(GetPoint(0, 4), GetPoint(1, 4));
                ProcessNotation("vhh");
                AssertSelectionsAdjustEnd(
                    GetPoint(0, 2).GetSelectedSpan(0, 2, true), // '*|c '
                    GetPoint(1, 2).GetSelectedSpan(0, 2, true)); // '*|l '
                ProcessNotation("llll");
                AssertSelectionsAdjustEnd(
                    GetPoint(0, 6).GetSelectedSpan(-2, 0, false), // 'de|*'
                    GetPoint(1, 6).GetSelectedSpan(-2, 0, false)); // 'jk|*'
                ProcessNotation("hhhh");
                AssertSelectionsAdjustEnd(
                    GetPoint(0, 2).GetSelectedSpan(0, 2, true), // '*|c '
                    GetPoint(1, 2).GetSelectedSpan(0, 2, true)); // '*|l '
            }

            /// <summary>
            /// Split selection into carets
            /// </summary>
            [WpfFact]
            public void SplitSelection()
            {
                Create("    abc def ghi", "    jkl mno pqr", "    stu vwx yz.", "");
                SetCaretPoints(GetPoint(0, 4));
                ProcessNotation("vjj<C-A-i>");
                AssertCarets(GetPoint(0, 4), GetPoint(1, 0), GetPoint(2, 0));
            }

            /// <summary>
            /// Invert anchor and active point in all selections
            /// </summary>
            /// <param name="isInclusive"></param>
            [WpfTheory, InlineData(false), InlineData(true)]
            public void InvertSelection(bool isInclusive)
            {
                Create(isInclusive, "abc def ghi", "jkl mno pqr", "stu vwx yz.", "");
                SetCaretPoints(GetPoint(0, 4), GetPoint(1, 4), GetPoint(2, 4));
                ProcessNotation("ve");
                AssertSelectionsAdjustCaret(
                    GetPoint(0, 7).GetSelectedSpan(-3, 0, false), // 'def|*' or 'de|f*'
                    GetPoint(1, 7).GetSelectedSpan(-3, 0, false), // 'mno|*' or 'mn|o*'
                    GetPoint(2, 7).GetSelectedSpan(-3, 0, false)); // 'vwx|*' or 'vw|x*'
                ProcessNotation("o");
                AssertSelectionsAdjustCaret(
                    GetPoint(0, 4).GetSelectedSpan(0, 3, true), // '*|def'
                    GetPoint(1, 4).GetSelectedSpan(0, 3, true), // '*|mno'
                    GetPoint(2, 4).GetSelectedSpan(0, 3, true)); // '*|vwx'
                ProcessNotation("o");
                AssertSelectionsAdjustCaret(
                    GetPoint(0, 7).GetSelectedSpan(-3, 0, false), // 'def|*' or 'de|f*'
                    GetPoint(1, 7).GetSelectedSpan(-3, 0, false), // 'mno|*' or 'mn|o*'
                    GetPoint(2, 7).GetSelectedSpan(-3, 0, false)); // 'vwx|*' or 'vw|x*'
            }

            /// <summary>
            /// Using double-click should revert to a single selection
            /// </summary>
            [WpfTheory, InlineData(false), InlineData(true)]
            public void SelectWord(bool isInclusive)
            {
                Create(isInclusive, "abc def ghi jkl", "mno pqr stu vwx", "");
                _textView.SetVisibleLineCount(2);
                SetCaretPoints(GetPoint(0, 0), GetPoint(1, 0));
                _testableMouseDevice.Point = GetPoint(0, 5).Position; // 'e' in 'def'
                ProcessNotation("<LeftMouse><LeftRelease><2-LeftMouse><LeftRelease>");
                Assert.Equal(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
                AssertSelectionsAdjustCaret(
                    GetPoint(0, 7).GetSelectedSpan(-3, 0, false)); // 'def|*' or 'de|f*'
            }

            /// <summary>
            /// Using ctrl-alt-double-click should add a word to the selection
            /// </summary>
            [WpfTheory, InlineData(false), InlineData(true)]
            public void AddWordToSelection(bool isInclusive)
            {
                Create(isInclusive, "abc def ghi jkl", "mno pqr stu vwx", "");
                _textView.SetVisibleLineCount(2);

                // First double-click.
                _testableMouseDevice.Point = GetPoint(0, 5).Position; // 'e' in 'def'
                ProcessNotation("<C-A-LeftMouse><C-A-LeftRelease><C-A-2-LeftMouse><C-A-LeftRelease>");
                Assert.Equal(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
                AssertSelectionsAdjustCaret(
                    GetPoint(0, 7).GetSelectedSpan(-3, 0, false)); // 'def|*' or 'de|f*'

                // Second double-click.
                _testableMouseDevice.Point = GetPoint(1, 9).Position; // 't' in 'stu'
                ProcessNotation("<C-A-LeftMouse><C-A-LeftRelease><C-A-2-LeftMouse><C-A-LeftRelease>");
                Assert.Equal(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
                AssertSelectionsAdjustCaret(
                    GetPoint(0, 7).GetSelectedSpan(-3, 0, false), // 'def|*' or 'de|f*'
                    GetPoint(1, 11).GetSelectedSpan(-3, 0, false)); // 'stu|*' or 'st|u*'
            }

            /// <summary>
            /// Adding the next occurrence of the primary selection should wrap
            /// </summary>
            [WpfTheory, InlineData(false), InlineData(true)]
            public void AddNextOccurrence(bool isInclusive)
            {
                Create(isInclusive, "abc def ghi", "abc def ghi", "abc def ghi", "");
                SetCaretPoints(GetPoint(1, 5));

                // Select word.
                ProcessNotation("<C-A-N>");
                Assert.Equal(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
                AssertSelectionsAdjustCaret(
                    GetPoint(1, 7).GetSelectedSpan(-3, 0, false)); // 'def|*' or 'de|f*'

                // Select next occurrence below.
                ProcessNotation("<C-A-N>");
                AssertSelectionsAdjustCaret(
                    GetPoint(1, 7).GetSelectedSpan(-3, 0, false), // 'def|*' or 'de|f*'
                    GetPoint(2, 7).GetSelectedSpan(-3, 0, false)); // 'def|*' or 'de|f*'

                // Select next occurrence above.
                ProcessNotation("<C-A-N>");
                AssertSelectionsAdjustCaret(
                    GetPoint(1, 7).GetSelectedSpan(-3, 0, false), // 'def|*' or 'de|f*'
                    GetPoint(0, 7).GetSelectedSpan(-3, 0, false), // 'def|*' or 'de|f*'
                    GetPoint(2, 7).GetSelectedSpan(-3, 0, false)); // 'def|*' or 'de|f*'

                // No more matches.
                var didHit = false;
                _vimBuffer.ErrorMessage +=
                    (_, args) =>
                    {
                        Assert.Equal(Resources.VisualMode_NoMoreMatches, args.Message);
                        didHit = true;
                    };
                ProcessNotation("<C-A-N>");
                Assert.True(didHit);
            }
        }

        public sealed class VisualLineModeTest : MultiSelectionIntegrationTest
        {
            /// <summary>
            /// Test entering visual line mode
            /// </summary>
            [WpfFact]
            public void Enter()
            {
                Create("abc def", "ghi jkl", "mno pqr", "stu vwx", "");
                SetCaretPoints(GetPoint(0, 4), GetPoint(2, 4));
                ProcessNotation("V");
                Assert.Equal(ModeKind.VisualLine, _vimBuffer.ModeKind);
                AssertSelections(
                    GetPoint(0, 4).Position.GetSelectedSpan(-4, 5, false), // 'abc |def^M^J*'
                    GetPoint(2, 4).Position.GetSelectedSpan(-4, 5, false)); // 'mno |pqr^M^J*'
            }

            /// <summary>
            /// Split selection into carets
            /// </summary>
            [WpfFact]
            public void SplitSelection()
            {
                Create("    abc def ghi", "    jkl mno pqr", "    stu vwx yz.", "");
                SetCaretPoints(GetPoint(0, 4));
                ProcessNotation("Vjj<C-A-i>");
                AssertCarets(GetPoint(0, 4), GetPoint(1, 4), GetPoint(2, 4));
            }

            /// <summary>
            /// Test deleting lines
            /// </summary>
            [WpfFact]
            public void Delete()
            {
                Create("abc def", "ghi jkl", "mno pqr", "stu vwx", "");
                SetCaretPoints(GetPoint(0, 4), GetPoint(2, 4));
                ProcessNotation("Vx");
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                AssertLines("ghi jkl", "stu vwx", "");
                AssertCarets(GetPoint(0, 0), GetPoint(1, 0));
            }
        }

        public sealed class VisualBlockModeTest : MultiSelectionIntegrationTest
        {
            [WpfFact]
            public void SplitSelection()
            {
                Create("    abc def ghi", "    jkl mno pqr", "    stu vwx yz.", "");
                SetCaretPoints(GetPoint(0, 4));
                ProcessNotation("<C-V>jj<C-A-i>");
                AssertCarets(GetPoint(0, 4), GetPoint(1, 4), GetPoint(2, 4));
            }
        }

        public sealed class SelectModeTest : MultiSelectionIntegrationTest
        {
            protected override void Create(params string[] lines)
            {
                base.Create(lines);
                _globalSettings.SelectModeOptions =
                    SelectModeOptions.Mouse
                    | SelectModeOptions.Keyboard
                    | SelectModeOptions.Command;
                _globalSettings.KeyModelOptions = KeyModelOptions.StartSelection;
                _globalSettings.Selection = "exclusive";
            }

            /// <summary>
            /// Test entering select mode
            /// </summary>
            [WpfFact]
            public void Enter()
            {
                Create("abc def ghi", "jkl mno pqr", "");
                SetCaretPoints(GetPoint(0, 0), GetPoint(1, 0));
                ProcessNotation("<S-Right>");
                AssertSelections(
                    GetPoint(0, 1).GetSelectedSpan(-1, 0, false), // 'a|'
                    GetPoint(1, 1).GetSelectedSpan(-1, 0, false)); // 'j|'
            }

            /// <summary>
            /// Test replacing the selection
            /// </summary>
            [WpfFact]
            public void ReplaceSelection()
            {
                Create("abc def ghi", "jkl mno pqr", "");
                SetCaretPoints(GetPoint(0, 4), GetPoint(1, 4));
                ProcessNotation("gh<C-S-Right>xxx ");
                AssertLines("abc xxx ghi", "jkl xxx pqr", "");
                AssertCarets(GetPoint(0, 8), GetPoint(1, 8));
            }

            /// <summary>
            /// Test extending the selection forward
            /// </summary>
            [WpfFact]
            public void ExtendForward()
            {
                Create("abc def ghi", "jkl mno pqr", "");
                SetCaretPoints(GetPoint(0, 4), GetPoint(1, 4));
                ProcessNotation("gh<C-S-Right>");
                AssertSelections(
                    GetPoint(0, 8).GetSelectedSpan(-4, 0, false), // 'def |'
                    GetPoint(1, 8).GetSelectedSpan(-4, 0, false)); // 'mno |'
            }

            /// <summary>
            /// Test add a selection on an adjacent line
            /// </summary>
            [WpfFact]
            public void AddSelection()
            {
                Create("abc def ghi", "jkl mno pqr", "");
                SetCaretPoints(GetPoint(0, 4));
                ProcessNotation("gh<C-S-Right><C-A-Down>");
                AssertSelections(
                    GetPoint(0, 8).GetSelectedSpan(-4, 0, false), // 'def |'
                    GetPoint(1, 8).GetSelectedSpan(-4, 0, false)); // 'mno |'
            }

            /// <summary>
            /// Test extending the selection backward
            /// </summary>
            [WpfFact]
            public void ExtendBackward()
            {
                Create("abc def ghi", "jkl mno pqr", "");
                SetCaretPoints(GetPoint(0, 8), GetPoint(1, 8));
                ProcessNotation("gh<C-S-Left>");
                AssertSelections(
                    GetPoint(0, 4).GetSelectedSpan(0, 4, true), // '|def '
                    GetPoint(1, 4).GetSelectedSpan(0, 4, true)); // '|mno '
            }

            /// <summary>
            /// Test extending the selection through zero width
            /// </summary>
            [WpfFact]
            public void ExtendThroughZeroWidth()
            {
                Create("abc def ghi", "jkl mno pqr", "");
                SetCaretPoints(GetPoint(0, 4), GetPoint(1, 4));
                ProcessNotation("gh<C-S-Right><C-S-Left><C-S-Left>");
                AssertSelections(
                    GetPoint(0, 0).GetSelectedSpan(0, 4, true), // 'abc |'
                    GetPoint(1, 0).GetSelectedSpan(0, 4, true)); // 'jkl |'
            }

            /// <summary>
            /// Using single-click should revert to a single caret
            /// </summary>
            [WpfFact]
            public void Click()
            {
                Create("abc def ghi jkl", "mno pqr stu vwx", "");
                _textView.SetVisibleLineCount(2);
                SetCaretPoints(GetPoint(0, 0), GetPoint(1, 0));
                _testableMouseDevice.Point = GetPoint(0, 5).Position; // 'e' in 'def'
                ProcessNotation("<LeftMouse><LeftRelease>");
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                AssertCarets(GetPoint(0, 5)); // d'|*'ef
            }

            /// <summary>
            /// Using double-click should revert to a single selection
            /// </summary>
            [WpfTheory, InlineData(false), InlineData(true)]
            public void SelectWord(bool isInclusive)
            {
                Create(isInclusive, "abc def ghi jkl", "mno pqr stu vwx", "");
                _textView.SetVisibleLineCount(2);
                SetCaretPoints(GetPoint(0, 0), GetPoint(1, 0));
                _testableMouseDevice.Point = GetPoint(0, 5).Position; // 'e' in 'def'
                ProcessNotation("<LeftMouse><LeftRelease><2-LeftMouse><LeftRelease>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                AssertSelectionsAdjustCaret(
                    GetPoint(0, 7).GetSelectedSpan(-3, 0, false)); // 'def|*' or 'def|*'
            }

            /// <summary>
            /// Using ctrl-alt-double-click should add a word to the selection
            /// </summary>
            [WpfTheory, InlineData(false), InlineData(true)]
            public void AddWordToSelection(bool isInclusive)
            {
                Create(isInclusive, "abc def ghi jkl", "mno pqr stu vwx", "");
                _textView.SetVisibleLineCount(2);

                // First double-click.
                _testableMouseDevice.Point = GetPoint(0, 5).Position; // 'e' in 'def'
                ProcessNotation("<C-A-LeftMouse><C-A-LeftRelease><C-A-2-LeftMouse><C-A-LeftRelease>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                AssertSelectionsAdjustCaret(
                    GetPoint(0, 7).GetSelectedSpan(-3, 0, false)); // 'def|*' or 'de|f*'

                // Second double-click.
                _testableMouseDevice.Point = GetPoint(1, 9).Position; // 't' in 'stu'
                ProcessNotation("<C-A-LeftMouse><C-A-LeftRelease><C-A-2-LeftMouse><C-A-LeftRelease>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                AssertSelectionsAdjustCaret(
                    GetPoint(0, 7).GetSelectedSpan(-3, 0, false), // 'def|*' or 'de|f*'
                    GetPoint(1, 11).GetSelectedSpan(-3, 0, false)); // 'stu|*' or 'st|u*'
            }

            /// <summary>
            /// Adding the next occurrence of the primary selection should wrap
            /// </summary>
            [WpfTheory, InlineData(false), InlineData(true)]
            public void AddNextOccurrence(bool isInclusive)
            {
                Create(isInclusive, "abc def ghi", "abc def ghi", "abc def ghi", "");
                SetCaretPoints(GetPoint(1, 5));

                // Select word.
                ProcessNotation("<C-A-N>");
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                AssertSelectionsAdjustCaret(
                    GetPoint(1, 7).GetSelectedSpan(-3, 0, false)); // 'def|*' or 'de|f*'

                // Select next occurrence below.
                ProcessNotation("<C-A-N>");
                AssertSelectionsAdjustCaret(
                    GetPoint(1, 7).GetSelectedSpan(-3, 0, false), // 'def|*' or 'de|f*'
                    GetPoint(2, 7).GetSelectedSpan(-3, 0, false)); // 'def|*' or 'de|f*'

                // Select next occurrence above.
                ProcessNotation("<C-A-N>");
                AssertSelectionsAdjustCaret(
                    GetPoint(1, 7).GetSelectedSpan(-3, 0, false), // 'def|*' or 'de|f*'
                    GetPoint(0, 7).GetSelectedSpan(-3, 0, false), // 'def|*' or 'de|f*'
                    GetPoint(2, 7).GetSelectedSpan(-3, 0, false)); // 'def|*' or 'de|f*'

                // No more matches.
                var didHit = false;
                _vimBuffer.ErrorMessage +=
                    (_, args) =>
                    {
                        Assert.Equal(Resources.VisualMode_NoMoreMatches, args.Message);
                        didHit = true;
                    };
                ProcessNotation("<C-A-N>");
                Assert.True(didHit);
            }
        }
    }
}
