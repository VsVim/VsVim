using System;
using System.Linq;
using EditorUtils;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Vim.Extensions;
using Vim.UnitTest.Mock;
using Xunit;

namespace Vim.UnitTest
{
    public abstract class MotionUtilTest : VimTestBase
    {
        protected ITextBuffer _textBuffer;
        protected ITextView _textView;
        protected ITextSnapshot _snapshot;
        protected IVimTextBuffer _vimTextBuffer;
        protected IVimLocalSettings _localSettings;
        protected IVimGlobalSettings _globalSettings;
        internal MotionUtil _motionUtil;
        protected ISearchService _search;
        protected IVimData _vimData;
        protected IMarkMap _markMap;
        protected Mock<IStatusUtil> _statusUtil;
        protected LocalMark _localMarkA = LocalMark.NewLetter(Letter.A);
        protected Mark _markLocalA = Mark.NewLocalMark(LocalMark.NewLetter(Letter.A));

        protected void Create(params string[] lines)
        {
            var textView = CreateTextView(lines);
            Create(textView);
        }

        protected void Create(int caretPosition, params string[] lines)
        {
            Create(lines);
            _textView.MoveCaretTo(caretPosition);
        }

        protected void Create(ITextView textView)
        {
            _textView = textView;
            _textBuffer = textView.TextBuffer;
            _snapshot = _textBuffer.CurrentSnapshot;
            _textBuffer.Changed += delegate { _snapshot = _textBuffer.CurrentSnapshot; };

            _vimTextBuffer = Vim.CreateVimTextBuffer(_textBuffer);
            _statusUtil = new Mock<IStatusUtil>(MockBehavior.Strict);
            var vimBufferData = CreateVimBufferData(_vimTextBuffer, _textView, statusUtil: _statusUtil.Object);
            _globalSettings = vimBufferData.LocalSettings.GlobalSettings;
            _localSettings = vimBufferData.LocalSettings;
            _markMap = vimBufferData.Vim.MarkMap;
            _vimData = vimBufferData.Vim.VimData;
            _search = vimBufferData.Vim.SearchService;
            var wordNavigator = CreateTextStructureNavigator(_textView.TextBuffer, WordKind.NormalWord);
            var operations = CommonOperationsFactory.GetCommonOperations(vimBufferData);
            _motionUtil = new MotionUtil(vimBufferData, operations);
        }

        public void AssertData(MotionResult data, SnapshotSpan? span, MotionKind motionKind = null, CaretColumn desiredColumn = null)
        {
            if (span.HasValue)
            {
                Assert.Equal(span.Value, data.Span);
            }
            if (motionKind != null)
            {
                Assert.Equal(motionKind, data.MotionKind);
            }
            if (desiredColumn != null)
            {
                Assert.Equal(desiredColumn, data.DesiredColumn);
            }
        }

        public sealed class AdjustMotionResult : MotionUtilTest
        {
            /// <summary>
            /// Inclusive motion values shouldn't be adjusted
            /// </summary>
            [Fact]
            public void Inclusive()
            {
                Create("cat", "dog");
                var result1 = VimUtil.CreateMotionResult(_textView.GetLine(0).ExtentIncludingLineBreak, motionKind: MotionKind.CharacterWiseInclusive);
                var result2 = _motionUtil.AdjustMotionResult(Motion.CharLeft, result1);
                Assert.Equal(result1, result2);
            }

            /// <summary>
            /// Make sure adjusted ones become line wise if it meets the criteria
            /// </summary>
            [Fact]
            public void FullLine()
            {
                Create("  cat", "dog");
                var span = new SnapshotSpan(_textView.GetPoint(2), _textView.GetLine(1).Start);
                var result1 = VimUtil.CreateMotionResult(span, motionKind: MotionKind.CharacterWiseExclusive);
                var result2 = _motionUtil.AdjustMotionResult(Motion.CharLeft, result1);
                Assert.Equal(OperationKind.LineWise, result2.OperationKind);
                Assert.Equal(_textView.GetLine(0).ExtentIncludingLineBreak, result2.Span);
                Assert.Equal(span, result2.OriginalSpan);
                Assert.True(result2.MotionKind.IsLineWise);
            }

            /// <summary>
            /// Don't make it full line if it doesn't start before the first real character
            /// </summary>
            [Fact]
            public void NotFullLine()
            {
                Create("  cat", "dog");
                var span = new SnapshotSpan(_textView.GetPoint(3), _textView.GetLine(1).Start);
                var result1 = VimUtil.CreateMotionResult(span, motionKind: MotionKind.CharacterWiseExclusive);
                var result2 = _motionUtil.AdjustMotionResult(Motion.CharLeft, result1);
                Assert.Equal(OperationKind.CharacterWise, result2.OperationKind);
                Assert.Equal("at", result2.Span.GetText());
                Assert.True(result2.MotionKind.IsCharacterWiseInclusive);
            }

            /// <summary>
            /// If the last word on the line is yanked and there is a space after it then the
            /// space should be included (basically the exclusive adjustment shouldn't leave
            /// it in)
            /// </summary>
            [Fact]
            public void WordWithSpaceBeforeEndOfLine()
            {
                Create("cat ", " dog");
                var motionResult = _motionUtil.GetMotion(Motion.NewWordForward(WordKind.NormalWord)).Value;
                Assert.Equal("cat ", motionResult.Span.GetText());
            }
        }

        public sealed class AllSentenceTest : MotionUtilTest
        {
            /// <summary>
            /// Should take the trailing white space
            /// </summary>
            [Fact]
            public void Simple()
            {
                Create("dog. cat. bear.");
                var data = _motionUtil.AllSentence(1);
                Assert.Equal("dog. ", data.Span.GetText());
            }

            /// <summary>
            /// Take the leading white space when there is a preceding sentence and no trailing 
            /// white space
            /// </summary>
            [Fact]
            public void NoTrailingWhiteSpace()
            {
                Create("dog. cat.");
                _textView.MoveCaretTo(5);
                var data = _motionUtil.AllSentence(1);
                Assert.Equal(" cat.", data.Span.GetText());
            }

            /// <summary>
            /// When starting in the white space include it in the motion instead of the trailing
            /// white space
            /// </summary>
            [Fact]
            public void FromWhiteSpace()
            {
                Create("dog. cat. bear.");
                _textView.MoveCaretTo(4);
                var data = _motionUtil.AllSentence(1);
                Assert.Equal(" cat.", data.Span.GetText());
            }

            /// <summary>
            /// When the trailing white space goes across new lines then we should still be including
            /// that 
            /// </summary>
            [Fact]
            public void WhiteSpaceAcrossNewLine()
            {
                Create("dog.  ", "  cat");
                var data = _motionUtil.AllSentence(1);
                Assert.Equal("dog.  " + Environment.NewLine + "  ", data.Span.GetText());
            }

            /// <summary>
            /// This is intended to make sure that 'as' goes through the standard exclusive adjustment
            /// operation.  Even though it technically extends into the next line ':help exclusive-linewise'
            /// dictates it should be changed into a line wise motion
            /// </summary>
            [Fact]
            public void OneSentencePerLine()
            {
                Create("dog.", "cat.");
                var data = _motionUtil.GetMotion(Motion.AllSentence).Value;
                Assert.Equal(OperationKind.LineWise, data.OperationKind);
                Assert.Equal("dog." + Environment.NewLine, data.Span.GetText());
            }

            /// <summary>
            /// Blank lines are sentences so don't include them as white space.  The gap between the '.'
            /// and the blank line is white space though and should be included in the motion
            /// </summary>
            [Fact]
            public void DontJumpBlankLinesAsWhiteSpace()
            {
                Create("dog.", "", "cat.");
                var data = _motionUtil.GetMotion(Motion.AllSentence).Value;
                Assert.Equal(OperationKind.LineWise, data.OperationKind);
                Assert.Equal("dog." + Environment.NewLine, data.Span.GetText());
            }

            /// <summary>
            /// Blank lines are sentences so treat them as such.  Note: A blank line includes the entire blank
            /// line.  But when operating on a blank line it often appears that it doesn't due to the 
            /// rules around motion adjustments spelled out in ':help exclusive'.  Namely if an exclusive motion
            /// ends in column 0 then it gets moved back to the end of the previous line and becomes inclusive
            /// </summary>
            [Fact]
            public void BlankLinesAreSentences()
            {
                Create("dog.  ", "", "cat.");
                _textView.MoveCaretToLine(1);
                var data = _motionUtil.AllSentence(1);
                Assert.Equal("  " + Environment.NewLine + Environment.NewLine, data.Span.GetText());

                // Make sure it's adjusted properly for the exclusive exception
                data = _motionUtil.GetMotion(Motion.AllSentence).Value;
                Assert.Equal("  " + Environment.NewLine, data.Span.GetText());
            }
        }

        public sealed class QuotedStringTest : MotionUtilTest
        {
            [Fact]
            public void ItSelectsQuotesAlongWithInnerText()
            {
                Create(@"""foo""");
                var data = _motionUtil.QuotedString('"');
                Assert.True(data.IsSome());
                AssertData(data.Value, new SnapshotSpan(_snapshot, 0, 5), MotionKind.CharacterWiseInclusive);
            }

            /// <summary>
            /// Include the leading whitespace
            /// </summary>
            [Fact]
            public void ItIncludesTheLeadingWhitespace()
            {
                Create(@"  ""foo""");
                var data = _motionUtil.QuotedString('"');
                Assert.True(data.IsSome());
                AssertData(data.Value, new SnapshotSpan(_snapshot, 0, 7), MotionKind.CharacterWiseInclusive);
            }

            /// <summary>
            /// Include the trailing whitespace
            /// </summary>
            [Fact]
            public void ItIncludesTheTrailingWhitespace()
            {
                Create(@"""foo""  ");
                var data = _motionUtil.QuotedString('"');
                Assert.True(data.IsSome());
                AssertData(data.Value, new SnapshotSpan(_snapshot, 0, 7), MotionKind.CharacterWiseInclusive);
            }

            /// <summary>
            /// Favor the trailing whitespace over leading
            /// </summary>
            [Fact]
            public void ItFavorsTrailingWhitespaceOverLeading()
            {
                Create(@"  ""foo""  ");

                var data = _motionUtil.QuotedString('"');

                Assert.True(data.IsSome());
                AssertData(data.Value, new SnapshotSpan(_snapshot, 2, 7), MotionKind.CharacterWiseInclusive);
                Assert.Equal(data.Value.Span.GetText(), @"""foo""  ");
            }

            [Fact]
            public void WhenFavoringTrailingSpace_ItActuallyLooksAtTheFirstCharAfterTheEndQuote()
            {
                Create(@"  ""foo""X ");
                var start = _snapshot.GetText().IndexOf('f');
                _textView.MoveCaretTo(start);

                var data = _motionUtil.QuotedString('"');

                Assert.Equal(data.Value.Span.GetText(), @"  ""foo""");
            }

            [Fact]
            public void ItFavorsTrailingWhitespaceOverLeading_WithOnlyOneTrailingSpace()
            {
                Create(@"  ""foo"" ");
                var start = _snapshot.GetText().IndexOf('f');
                _textView.MoveCaretTo(start);

                var data = _motionUtil.QuotedString('"');

                Assert.Equal(data.Value.Span.GetText(), @"""foo"" ");
            }

            /// <summary>
            /// Ignore the escaped quotes
            /// </summary>
            [Fact]
            public void ItIgnoresEscapedQuotes()
            {
                Create(@"""foo\""""");

                var data = _motionUtil.QuotedString('"');

                Assert.True(data.IsSome());
                AssertData(data.Value, new SnapshotSpan(_snapshot, 0, 7), MotionKind.CharacterWiseInclusive);
            }

            /// <summary>
            /// Ignore the escaped quotes
            /// </summary>
            [Fact]
            public void ItIgnoresEscapedQuotes_AlternateEscape()
            {
                Create(@"""foo(""""");
                _localSettings.QuoteEscape = @"(";
                var data = _motionUtil.QuotedString('"');
                Assert.True(data.IsSome());
                AssertData(data.Value, new SnapshotSpan(_snapshot, 0, 7), MotionKind.CharacterWiseInclusive);
            }

            [Fact]
            public void NothingIsSelectedWhenThereAreNoQuotes()
            {
                Create(@"foo");
                var data = _motionUtil.QuotedString('"');
                Assert.True(data.IsNone());
            }

            [Fact]
            public void ItSelectsTheInsideOfTheSecondQuotedWord()
            {
                Create(@"""foo"" ""bar""");
                var start = _snapshot.GetText().IndexOf('b');
                _textView.MoveCaretTo(start);
                var data = _motionUtil.QuotedString('"');
                Assert.True(data.IsSome());
                AssertData(data.Value, new SnapshotSpan(_snapshot, start - 2, 6), MotionKind.CharacterWiseInclusive);
            }

            [Fact]
            public void SingleQuotesWork()
            {
                Create(@"""foo"" 'bar'");
                var start = _snapshot.GetText().IndexOf('b');
                _textView.MoveCaretTo(start);
                var data = _motionUtil.QuotedString('\'');
                Assert.True(data.IsSome());
                AssertData(data.Value, new SnapshotSpan(_snapshot, start - 2, 6), MotionKind.CharacterWiseInclusive);
            }

            [Fact]
            public void BackquotesWork()
            {
                Create(@"""foo"" `bar`");
                var start = _snapshot.GetText().IndexOf('b');
                _textView.MoveCaretTo(start);
                var data = _motionUtil.QuotedString('`');
                Assert.True(data.IsSome());
                AssertData(data.Value, new SnapshotSpan(_snapshot, start - 2, 6), MotionKind.CharacterWiseInclusive);
            }
        }

        public sealed class Word : MotionUtilTest
        {
            /// <summary>
            /// If the word motion crosses a new line and it's a moveement then we keep it. The 
            /// caret should move to the next line
            /// </summary>
            [Fact]
            public void AcrossLineBreakMovement()
            {
                Create("cat", " dog");
                var motionResult = _motionUtil.WordForward(WordKind.NormalWord, 1, MotionContext.Movement);
                Assert.Equal(_textBuffer.GetLine(1).Start.Add(1), motionResult.Span.End);
            }

            /// <summary>
            /// If the word motion crosses a line rbeak and it's an operator then we back it up 
            /// because we don't want the new line in the operator
            /// </summary>
            [Fact]
            public void AcrossLineBreakOperator()
            {
                Create("cat  ", " dog");
                var motionResult = _motionUtil.WordForward(WordKind.NormalWord, 1, MotionContext.AfterOperator);
                Assert.Equal("cat  ", motionResult.Span.GetText());
            }

            /// <summary>
            /// Blank lines don't factor into an operator.  Make sure we back up over it completel
            /// </summary>
            [Fact]
            public void AcrossBlankLineOperator()
            {
                Create("dog", "cat", " ", " ", "  fish");
                _textView.MoveCaretToLine(1);
                var motionResult = _motionUtil.WordForward(WordKind.NormalWord, 1, MotionContext.AfterOperator);
                Assert.Equal("cat", motionResult.Span.GetText());
            }

            /// <summary>
            /// Empty lines are different because they are actual words so we don't back over them
            /// </summary>
            [Fact]
            public void AcrossEmptyLineOperator()
            {
                Create("dog", "cat", "", "  fish");
                _textView.MoveCaretToLine(1);
                var motionResult = _motionUtil.WordForward(WordKind.NormalWord, 2, MotionContext.AfterOperator);
                Assert.Equal("cat" + Environment.NewLine + Environment.NewLine, motionResult.Span.GetText());
            }

            [Fact]
            public void Forward1()
            {
                Create("foo bar");
                _textView.MoveCaretTo(0);
                var res = _motionUtil.WordForward(WordKind.NormalWord, 1, MotionContext.Movement);
                var span = res.Span;
                Assert.Equal(4, span.Length);
                Assert.Equal("foo ", span.GetText());
                Assert.True(res.IsAnyWordMotion);
                Assert.Equal(OperationKind.CharacterWise, res.OperationKind);
                Assert.True(res.IsAnyWordMotion);
            }

            [Fact]
            public void Forward2()
            {
                Create("foo bar");
                _textView.MoveCaretTo(1);
                var res = _motionUtil.WordForward(WordKind.NormalWord, 1, MotionContext.Movement);
                var span = res.Span;
                Assert.Equal(3, span.Length);
                Assert.Equal("oo ", span.GetText());
            }

            /// <summary>
            /// Word motion with a count
            /// </summary>
            [Fact]
            public void Forward3()
            {
                Create("foo bar baz");
                _textView.MoveCaretTo(0);
                var res = _motionUtil.WordForward(WordKind.NormalWord, 2, MotionContext.Movement);
                Assert.Equal("foo bar ", res.Span.GetText());
            }

            /// <summary>
            /// Count across lines
            /// </summary>
            [Fact]
            public void Forward4()
            {
                Create("foo bar", "baz jaz");
                var res = _motionUtil.WordForward(WordKind.NormalWord, 3, MotionContext.Movement);
                Assert.Equal("foo bar" + Environment.NewLine + "baz ", res.Span.GetText());
            }

            /// <summary>
            /// Count off the end of the buffer
            /// </summary>
            [Fact]
            public void Forward5()
            {
                Create("foo bar");
                var res = _motionUtil.WordForward(WordKind.NormalWord, 10, MotionContext.Movement);
                Assert.Equal("foo bar", res.Span.GetText());
            }

            [Fact]
            public void ForwardBigWordIsAnyWord()
            {
                Create("foo bar");
                var res = _motionUtil.WordForward(WordKind.BigWord, 1, MotionContext.Movement);
                Assert.True(res.IsAnyWordMotion);
            }

            [Fact]
            public void BackwardBothAreAnyWord()
            {
                Create("foo bar");
                Assert.True(_motionUtil.WordBackward(WordKind.NormalWord, 1).IsAnyWordMotion);
                Assert.True(_motionUtil.WordBackward(WordKind.BigWord, 1).IsAnyWordMotion);
            }

            /// <summary>
            /// Make sure we handle the case where the motion ends on the start of the next line
            /// but also begins in a blank line
            /// </summary>
            [Fact]
            public void ForwardFromBlankLineEnd()
            {
                Create("cat", "   ", "dog");
                _textView.MoveCaretToLine(1, 2);
                var result = _motionUtil.WordForward(WordKind.NormalWord, 1, MotionContext.AfterOperator);
                Assert.Equal(" ", result.Span.GetText());
            }

            [Fact]
            public void ForwardFromBlankLineMiddle()
            {
                Create("cat", "   ", "dog");
                _textView.MoveCaretToLine(1, 1);
                var result = _motionUtil.WordForward(WordKind.NormalWord, 1, MotionContext.AfterOperator);
                Assert.Equal("  ", result.Span.GetText());
            }

            [Fact]
            public void ForwardFromDoubleBlankLineEnd()
            {
                Create("cat", "   ", "   ", "dog");
                _textView.MoveCaretToLine(1, 2);
                var result = _motionUtil.WordForward(WordKind.NormalWord, 1, MotionContext.AfterOperator);
                Assert.Equal(" ", result.Span.GetText());
            }
        }

        public sealed class Misc : MotionUtilTest
        {
            [Fact]
            public void EndOfLine1()
            {
                Create("foo bar", "baz");
                var res = _motionUtil.EndOfLine(1);
                var span = res.Span;
                Assert.Equal("foo bar", span.GetText());
                Assert.Equal(MotionKind.CharacterWiseInclusive, res.MotionKind);
                Assert.Equal(OperationKind.CharacterWise, res.OperationKind);
            }

            [Fact]
            public void EndOfLine2()
            {
                Create("foo bar", "baz");
                _textView.MoveCaretTo(1);
                var res = _motionUtil.EndOfLine(1);
                Assert.Equal("oo bar", res.Span.GetText());
            }

            [Fact]
            public void EndOfLine3()
            {
                Create("foo", "bar", "baz");
                var res = _motionUtil.EndOfLine(2);
                Assert.Equal("foo" + Environment.NewLine + "bar", res.Span.GetText());
                Assert.Equal(MotionKind.CharacterWiseInclusive, res.MotionKind);
                Assert.Equal(OperationKind.CharacterWise, res.OperationKind);
            }

            [Fact]
            public void EndOfLine4()
            {
                Create("foo", "bar", "baz", "jar");
                var res = _motionUtil.EndOfLine(3);
                var tuple = res;
                Assert.Equal("foo" + Environment.NewLine + "bar" + Environment.NewLine + "baz", tuple.Span.GetText());
                Assert.Equal(MotionKind.CharacterWiseInclusive, tuple.MotionKind);
                Assert.Equal(OperationKind.CharacterWise, tuple.OperationKind);
            }

            /// <summary>
            /// Make sure counts past the end of the buffer don't crash
            /// </summary>
            [Fact]
            public void EndOfLine5()
            {
                Create("foo");
                var res = _motionUtil.EndOfLine(300);
                Assert.Equal("foo", res.Span.GetText());
            }

            [Fact]
            public void BeginingOfLine1()
            {
                Create("foo");
                _textView.MoveCaretTo(1);
                var data = _motionUtil.BeginingOfLine();
                Assert.Equal(new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, 1), data.Span);
                Assert.Equal(MotionKind.CharacterWiseExclusive, data.MotionKind);
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
                Assert.False(data.IsForward);
            }

            [Fact]
            public void BeginingOfLine2()
            {
                Create("foo");
                _textView.MoveCaretTo(2);
                var data = _motionUtil.BeginingOfLine();
                Assert.Equal(new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, 2), data.Span);
                Assert.Equal(MotionKind.CharacterWiseExclusive, data.MotionKind);
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
                Assert.False(data.IsForward);
            }

            /// <summary>
            /// Go to begining even if there is whitespace
            /// </summary>
            [Fact]
            public void BeginingOfLine3()
            {
                Create("  foo");
                _textView.MoveCaretTo(4);
                var data = _motionUtil.BeginingOfLine();
                Assert.Equal(new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, 4), data.Span);
                Assert.Equal(MotionKind.CharacterWiseExclusive, data.MotionKind);
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
                Assert.False(data.IsForward);
            }

            [Fact]
            public void FirstNonBlankOnCurrentLine1()
            {
                Create("foo");
                _textView.MoveCaretTo(_textBuffer.GetLineFromLineNumber(0).End);
                var tuple = _motionUtil.FirstNonBlankOnCurrentLine();
                Assert.Equal("foo", tuple.Span.GetText());
                Assert.Equal(MotionKind.CharacterWiseExclusive, tuple.MotionKind);
                Assert.Equal(OperationKind.CharacterWise, tuple.OperationKind);
            }

            /// <summary>
            /// Make sure it goes to the first non-whitespace character
            /// </summary>
            [Fact]
            public void FirstNonBlankOnCurrentLine2()
            {
                Create("  foo");
                _textView.MoveCaretTo(_textBuffer.GetLineFromLineNumber(0).End);
                var tuple = _motionUtil.FirstNonBlankOnCurrentLine();
                Assert.Equal("foo", tuple.Span.GetText());
                Assert.Equal(MotionKind.CharacterWiseExclusive, tuple.MotionKind);
                Assert.Equal(OperationKind.CharacterWise, tuple.OperationKind);
            }

            /// <summary>
            /// Make sure to ignore tabs
            /// </summary>
            [Fact]
            public void FirstNonBlankOnCurrentLine3()
            {
                var text = "\tfoo";
                Create(text);
                _textView.MoveCaretTo(_textBuffer.GetLineFromLineNumber(0).End);
                var tuple = _motionUtil.FirstNonBlankOnCurrentLine();
                Assert.Equal(text.IndexOf('f'), tuple.Span.Start);
                Assert.False(tuple.IsForward);
            }

            /// <summary>
            /// Make sure to move forward to the first non-whitespace
            /// </summary>
            [Fact]
            public void FirstNonBlankOnCurrentLine4()
            {
                Create(0, "   bar");
                var data = _motionUtil.FirstNonBlankOnCurrentLine();
                Assert.Equal(_textBuffer.GetSpan(0, 3), data.Span);
            }

            /// <summary>
            /// Empty line case
            /// </summary>
            [Fact]
            public void FirstNonBlankOnCurrentLine5()
            {
                Create(0, "");
                var data = _motionUtil.FirstNonBlankOnCurrentLine();
                Assert.Equal(_textBuffer.GetSpan(0, 0), data.Span);
            }

            /// <summary>
            /// Backwards case
            /// </summary>
            [Fact]
            public void FirstNonBlankOnCurrentLine6()
            {
                Create(3, "bar");
                var data = _motionUtil.FirstNonBlankOnCurrentLine();
                Assert.Equal(_textBuffer.GetSpan(0, 3), data.Span);
                Assert.False(data.IsForward);
            }

            /// <summary>
            /// A count of 1 should return a single line 
            /// </summary>
            [Fact]
            public void FirstNonBlankOnLine_Single()
            {
                Create(0, "cat", "dog");
                var data = _motionUtil.FirstNonBlankOnLine(1);
                Assert.Equal("cat", data.LineRange.Extent.GetText());
                Assert.Equal(OperationKind.LineWise, data.OperationKind);
                Assert.True(data.IsForward);
                Assert.True(data.CaretColumn.IsInLastLine);
            }

            /// <summary>
            /// A count of 2 should return 2 lines and the column should be on the last
            /// line
            /// </summary>
            [Fact]
            public void FirstNonBlankOnLine_Double()
            {
                Create(0, "cat", " dog");
                var data = _motionUtil.FirstNonBlankOnLine(2);
                Assert.Equal(_textView.GetLineRange(0, 1), data.LineRange);
                Assert.Equal(1, data.CaretColumn.AsInLastLine().Item);
            }

            /// <summary>
            /// Make sure to include the trailing white space
            /// </summary>
            [Fact]
            public void AllWord_Simple()
            {
                Create("foo bar");
                var data = _motionUtil.AllWord(WordKind.NormalWord, 1, _textView.GetCaretPoint()).Value;
                Assert.Equal("foo ", data.Span.GetText());
            }

            /// <summary>
            /// Grab the entire word even if starting in the middle
            /// </summary>
            [Fact]
            public void AllWord_FromMiddle()
            {
                Create("foo bar");
                _textView.MoveCaretTo(1);
                var data = _motionUtil.AllWord(WordKind.NormalWord, 1, _textView.GetCaretPoint()).Value;
                Assert.Equal("foo ", data.Span.GetText());
            }

            /// <summary>
            /// All word with a count motion
            /// </summary>
            [Fact]
            public void AllWord_WithCount()
            {
                Create("foo bar baz");
                _textView.MoveCaretTo(1);
                var data = _motionUtil.AllWord(WordKind.NormalWord, 2, _textView.GetCaretPoint()).Value;
                Assert.Equal("foo bar ", data.Span.GetText());
            }

            /// <summary>
            /// When starting in white space the space before the word should be included instead
            /// of the white space after it
            /// </summary>
            [Fact]
            public void AllWord_StartInWhiteSpace()
            {
                Create("dog cat tree");
                _textView.MoveCaretTo(3);
                var data = _motionUtil.AllWord(WordKind.NormalWord, 1, _textView.GetCaretPoint()).Value;
                Assert.Equal(" cat", data.Span.GetText());
            }

            /// <summary>
            /// When there is no trailing white space and a preceding word then the preceding white
            /// space should be included
            /// </summary>
            [Fact]
            public void AllWord_NoTrailingWhiteSpace()
            {
                Create("dog cat");
                _textView.MoveCaretTo(5);
                var data = _motionUtil.AllWord(WordKind.NormalWord, 1, _textView.GetCaretPoint()).Value;
                Assert.Equal(" cat", data.Span.GetText());
            }

            /// <summary>
            /// If there is no trailing white space nor is their a preceding word on the same line
            /// then it shouldn't include the preceding white space
            /// </summary>
            [Fact]
            public void AllWord_NoTrailingWhiteSpaceOrPrecedingWordOnSameLine()
            {
                Create("dog", "  cat");
                _textView.MoveCaretTo(_textView.GetLine(1).Start.Add(2));
                var data = _motionUtil.AllWord(WordKind.NormalWord, 1, _textView.GetCaretPoint()).Value;
                Assert.Equal("cat", data.Span.GetText());
            }

            /// <summary>
            /// If there is no trailing white space nor is their a preceding word on the same line
            /// but it is the start of the buffer then do include the white space
            /// </summary>
            [Fact]
            public void AllWord_NoTrailingWhiteSpaceOrPrecedingWordAtStartOfBuffer()
            {
                Create("  cat");
                _textView.MoveCaretTo(3);
                var data = _motionUtil.AllWord(WordKind.NormalWord, 1, _textView.GetCaretPoint()).Value;
                Assert.Equal("cat", data.Span.GetText());
            }

            /// <summary>
            /// Make sure we include the full preceding white space if the motion starts in any 
            /// part of it
            /// </summary>
            [Fact]
            public void AllWord_FromMiddleOfPrecedingWhiteSpace()
            {
                Create("cat   dog");
                _textView.MoveCaretTo(4);
                var data = _motionUtil.AllWord(WordKind.NormalWord, 1, _textView.GetCaretPoint()).Value;
                Assert.Equal("   dog", data.Span.GetText());
            }

            /// <summary>
            /// On a one word line don't go into the previous line break looking for preceding
            /// white space
            /// </summary>
            [Fact]
            public void AllWord_DontGoIntoPreviousLineBreak()
            {
                Create("dog", "cat", "fish");
                _textView.MoveCaretToLine(1);
                var data = _motionUtil.GetMotion(Motion.NewAllWord(WordKind.NormalWord)).Value;
                Assert.Equal("cat", data.Span.GetText());
            }

            [Fact]
            public void CharLeft_Simple()
            {
                Create("foo bar");
                _textView.MoveCaretTo(2);
                var data = _motionUtil.CharLeft(2);
                Assert.Equal("fo", data.Span.GetText());
            }

            /// <summary>
            /// The char left operation should produce an empty span if it's at the start 
            /// of a line 
            /// </summary>
            [Fact]
            public void CharLeft_FailAtStartOfLine()
            {
                Create("dog", "cat");
                _textView.MoveCaretToLine(1);
                var data = _motionUtil.CharLeft(1);
                Assert.Equal(0, data.Span.Length);
            }

            /// <summary>
            /// When the count is to high but the caret is not at the start then the 
            /// caret should just move to the start of the line
            /// </summary>
            [Fact]
            public void CharLeft_CountTooHigh()
            {
                Create("dog", "cat");
                _textView.MoveCaretToLine(1, 1);
                var data = _motionUtil.CharLeft(300);
                Assert.Equal("c", data.Span.GetText());
            }

            [Fact]
            public void CharRight_Simple()
            {
                Create("foo");
                var data = _motionUtil.CharRight(1);
                Assert.Equal("f", data.Span.GetText());
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
                Assert.Equal(MotionKind.CharacterWiseExclusive, data.MotionKind);
            }

            /// <summary>
            /// The char right motion actually needs to succeed at the last point of the 
            /// line.  It often appears to not succeed because many users have
            /// 'virtualedit=' (at least not 'onemore').  So a 'l' at the end of the 
            /// line fails to move the caret which gives the appearance of failure.  In
            /// fact it succeeded but the caret move is not legal
            /// </summary>
            [Fact]
            public void CharRight_LastPointOnLine()
            {
                Create("cat", "dog", "tree");
                _textView.MoveCaretToLine(1, 2);
                var data = _motionUtil.CharRight(1);
                Assert.Equal("g", data.Span.GetText());
            }

            /// <summary>
            /// The char right should produce an empty span at the end of the line
            /// </summary>
            [Fact]
            public void CharRight_EndOfLine()
            {
                Create("cat", "dog");
                _textView.MoveCaretTo(3);
                var data = _motionUtil.CharRight(1);
                Assert.Equal("", data.Span.GetText());
            }

            /// <summary>
            /// Space right on the last character of the line should produce a
            /// span to the beginning of the next line
            /// </summary>
            [Fact]
            public void SpaceRight_LastCharacter()
            {
                Create("cat", "dog");
                _textView.MoveCaretTo(2);
                var data = _motionUtil.SpaceRight(1);
                Assert.Equal("t\r\n", data.Span.GetText());
            }

            /// <summary>
            /// Space right after the last character of the line should produce a
            /// span containing just the line break
            /// </summary>
            [Fact]
            public void SpaceRight_EndOfLine()
            {
                Create("cat", "dog");
                _textView.MoveCaretTo(3);
                var data = _motionUtil.SpaceRight(1);
                Assert.Equal("\r\n", data.Span.GetText());
            }

            /// <summary>
            /// Space right on the last character of the line with
            /// 'virtualedit=onemore' should produce a span containing
            /// just that character
            /// </summary>
            [Fact]
            public void SpaceRight_LastCharacterVirtualEdit()
            {
                Create("cat", "dog");
                _globalSettings.VirtualEdit = "onemore";
                _textView.MoveCaretTo(2);
                var data = _motionUtil.SpaceRight(1);
                Assert.Equal("t", data.Span.GetText());
            }

            /// <summary>
            /// Space right after the last character of the line with
            /// 'virtualedit=onemore' should produce a span containing
            /// just the line break
            /// </summary>
            [Fact]
            public void SpaceRight_EndOfLineVirtualEdit()
            {
                Create("cat", "dog");
                _globalSettings.VirtualEdit = "onemore";
                _textView.MoveCaretTo(3);
                var data = _motionUtil.SpaceRight(1);
                Assert.Equal("\r\n", data.Span.GetText());
            }

            /// <summary>
            /// Space right before a blank line should produce a span to the
            /// beginning of the next line
            /// </summary>
            [Fact]
            public void SpaceRight_ToBlankLine()
            {
                Create("cat", "", "dog");
                _textView.MoveCaretTo(2);
                var data = _motionUtil.SpaceRight(1);
                Assert.Equal("t\r\n", data.Span.GetText());
            }

            /// <summary>
            /// Space right on a blank line should produce a span to the
            /// beginning of the next line
            /// </summary>
            [Fact]
            public void SpaceRight_FromBlankLine()
            {
                Create("cat", "", "dog");
                _textView.MoveCaretTo(_textView.GetLine(1).Start);
                var data = _motionUtil.SpaceRight(1);
                Assert.Equal("\r\n", data.Span.GetText());
            }

            /// <summary>
            /// Space left on the first character of the line should produce a
            /// span to the last character of the previous line
            /// </summary>
            [Fact]
            public void SpaceLeft_FirstCharacter()
            {
                Create("cat", "dog");
                _textView.MoveCaretTo(_textView.GetLine(1).Start);
                var data = _motionUtil.SpaceLeft(1);
                Assert.Equal("t\r\n", data.Span.GetText());
            }

            /// <summary>
            /// Space left after the last character of the line should produce a
            /// span containing just the last character of the line
            /// </summary>
            [Fact]
            public void SpaceLeft_EndOfLine()
            {
                Create("cat", "dog");
                _textView.MoveCaretTo(3);
                var data = _motionUtil.SpaceLeft(1);
                Assert.Equal("t", data.Span.GetText());
            }

            /// <summary>
            /// Space left on the first character of the line with
            /// 'virtualedit=onemore' should produce a
            /// span to the end of the previous line
            /// </summary>
            [Fact]
            public void SpaceLeft_FirstCharacterVirtualEdit()
            {
                Create("cat", "dog");
                _globalSettings.VirtualEdit = "onemore";
                _textView.MoveCaretTo(_textView.GetLine(1).Start);
                var data = _motionUtil.SpaceLeft(1);
                Assert.Equal("\r\n", data.Span.GetText());
            }

            /// <summary>
            /// Space left after the last character of the line with
            /// 'virtualedit=onemore' should produce a span containing
            /// just the last character of the line
            /// </summary>
            [Fact]
            public void SpaceLeft_EndOfLineVirtualEdit()
            {
                Create("cat", "dog");
                _globalSettings.VirtualEdit = "onemore";
                _textView.MoveCaretTo(3);
                var data = _motionUtil.SpaceLeft(1);
                Assert.Equal("t", data.Span.GetText());
            }

            /// <summary>
            /// Space left before a blank line should produce a span to the
            /// end of the previous line
            /// </summary>
            [Fact]
            public void SpaceLeft_ToBlankLine()
            {
                Create("cat", "", "dog");
                _textView.MoveCaretTo(_textView.GetLine(2).Start);
                var data = _motionUtil.SpaceLeft(1);
                Assert.Equal("\r\n", data.Span.GetText());
            }

            /// <summary>
            /// Space left on a blank line should produce a span to the
            /// last character of the previous line
            /// </summary>
            [Fact]
            public void SpaceLeft_FromBlankLine()
            {
                Create("cat", "", "dog");
                _textView.MoveCaretTo(_textView.GetLine(1).Start);
                var data = _motionUtil.SpaceLeft(1);
                Assert.Equal("t\r\n", data.Span.GetText());
            }

            [Fact]
            public void EndOfWord1()
            {
                Create("foo bar");
                var res = _motionUtil.EndOfWord(WordKind.NormalWord, 1);
                Assert.Equal(MotionKind.CharacterWiseInclusive, res.MotionKind);
                Assert.Equal(OperationKind.CharacterWise, res.OperationKind);
                Assert.Equal(new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, 3), res.Span);
            }

            /// <summary>
            /// Needs to cross the end of the line
            /// </summary>
            [Fact]
            public void EndOfWord2()
            {
                Create("foo   ", "bar");
                _textView.MoveCaretTo(4);
                var res = _motionUtil.EndOfWord(WordKind.NormalWord, 1);
                var span = new SnapshotSpan(
                    _textBuffer.GetPoint(4),
                    _textBuffer.GetLineFromLineNumber(1).Start.Add(3));
                Assert.Equal(span, res.Span);
                Assert.Equal(MotionKind.CharacterWiseInclusive, res.MotionKind);
                Assert.Equal(OperationKind.CharacterWise, res.OperationKind);
            }

            [Fact]
            public void EndOfWord3()
            {
                Create("foo bar baz jaz");
                var res = _motionUtil.EndOfWord(WordKind.NormalWord, 2);
                var span = new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, 7);
                Assert.Equal(span, res.Span);
                Assert.Equal(MotionKind.CharacterWiseInclusive, res.MotionKind);
                Assert.Equal(OperationKind.CharacterWise, res.OperationKind);
            }

            /// <summary>
            /// Work across blank lines
            /// </summary>
            [Fact]
            public void EndOfWord4()
            {
                Create("foo   ", "", "bar");
                _textView.MoveCaretTo(4);
                var res = _motionUtil.EndOfWord(WordKind.NormalWord, 1);
                var span = new SnapshotSpan(
                    _textBuffer.GetPoint(4),
                    _textBuffer.GetLineFromLineNumber(2).Start.Add(3));
                Assert.Equal(span, res.Span);
                Assert.Equal(MotionKind.CharacterWiseInclusive, res.MotionKind);
                Assert.Equal(OperationKind.CharacterWise, res.OperationKind);
            }

            /// <summary>
            /// Go off the end of the buffer
            /// </summary>
            [Fact]
            public void EndOfWord5()
            {
                Create("foo   ", "", "bar");
                _textView.MoveCaretTo(4);
                var res = _motionUtil.EndOfWord(WordKind.NormalWord, 400);
                var span = new SnapshotSpan(
                    _textView.TextSnapshot,
                    Span.FromBounds(4, _textView.TextSnapshot.Length));
                Assert.Equal(span, res.Span);
            }

            /// <summary>
            /// On the last char of a word motion should proceed forward
            /// </summary>
            [Fact]
            public void EndOfWord6()
            {
                Create("foo bar baz");
                _textView.MoveCaretTo(2);
                var res = _motionUtil.EndOfWord(WordKind.NormalWord, 1);
                Assert.Equal("o bar", res.Span.GetText());
            }

            [Fact]
            public void EndOfWord7()
            {
                Create("foo", "bar");
                _textView.MoveCaretTo(2);
                var res = _motionUtil.EndOfWord(WordKind.NormalWord, 1);
                Assert.Equal("o" + Environment.NewLine + "bar", res.Span.GetText());
            }

            /// <summary>
            /// Second to last character
            /// </summary>
            [Fact]
            public void EndOfWord8()
            {
                Create("the dog goes around the house");
                _textView.MoveCaretTo(1);
                Assert.Equal('h', _textView.GetCaretPoint().GetChar());
                var res = _motionUtil.EndOfWord(WordKind.NormalWord, 1);
                Assert.Equal("he", res.Span.GetText());
            }

            [Fact]
            public void EndOfWord_DontStopOnPunctuation()
            {
                Create("A. the ball");
                _textView.MoveCaretTo(1);
                var data = _motionUtil.EndOfWord(WordKind.NormalWord, 1);
                Assert.Equal(". the", data.Span.GetText());
            }

            [Fact]
            public void EndOfWord_DoublePunctuation()
            {
                Create("A.. the ball");
                _textView.MoveCaretTo(1);
                var data = _motionUtil.EndOfWord(WordKind.NormalWord, 1);
                Assert.Equal("..", data.Span.GetText());
            }

            [Fact]
            public void EndOfWord_DoublePunctuationWithCount()
            {
                Create("A.. the ball");
                _textView.MoveCaretTo(1);
                var data = _motionUtil.EndOfWord(WordKind.NormalWord, 2);
                Assert.Equal(".. the", data.Span.GetText());
            }

            [Fact]
            public void EndOfWord_DoublePunctuationIsAWord()
            {
                Create("A.. the ball");
                _textView.MoveCaretTo(0);
                var data = _motionUtil.EndOfWord(WordKind.NormalWord, 1);
                Assert.Equal("A..", data.Span.GetText());
            }

            [Fact]
            public void EndOfWord_DontStopOnEndOfLine()
            {
                Create("A. ", "the ball");
                _textView.MoveCaretTo(1);
                var data = _motionUtil.EndOfWord(WordKind.NormalWord, 1);
                Assert.Equal(". " + Environment.NewLine + "the", data.Span.GetText());
            }

            [Fact]
            public void ForwardChar1()
            {
                Create("foo bar baz");
                Assert.Equal("fo", _motionUtil.CharSearch('o', 1, CharSearchKind.ToChar, Path.Forward).Value.Span.GetText());
                _textView.MoveCaretTo(1);
                Assert.Equal("oo", _motionUtil.CharSearch('o', 1, CharSearchKind.ToChar, Path.Forward).Value.Span.GetText());
                _textView.MoveCaretTo(1);
                Assert.Equal("oo b", _motionUtil.CharSearch('b', 1, CharSearchKind.ToChar, Path.Forward).Value.Span.GetText());
            }

            [Fact]
            public void ForwardChar2()
            {
                Create("foo bar baz");
                var data = _motionUtil.CharSearch('q', 1, CharSearchKind.ToChar, Path.Forward);
                Assert.True(data.IsNone());
            }

            [Fact]
            public void ForwardChar3()
            {
                Create("foo bar baz");
                var data = _motionUtil.CharSearch('o', 1, CharSearchKind.ToChar, Path.Forward).Value;
                Assert.Equal(MotionKind.CharacterWiseInclusive, data.MotionKind);
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
            }

            /// <summary>
            /// Bad count gets nothing in gVim"
            /// </summary>
            [Fact]
            public void ForwardChar4()
            {
                Create("foo bar baz");
                var data = _motionUtil.CharSearch('o', 300, CharSearchKind.ToChar, Path.Forward);
                Assert.True(data.IsNone());
            }

            /// <summary>
            /// A forward char search on an empty line shouldn't produce a result.  It's also a corner
            /// case prone to produce exceptions
            /// </summary>
            [Fact]
            public void ForwardChar_EmptyLine()
            {
                Create("cat", "", "dog");
                _textView.MoveCaretToLine(1);
                var data = _motionUtil.CharSearch('o', 1, CharSearchKind.ToChar, Path.Forward);
                Assert.True(data.IsNone());
            }

            [Fact]
            public void ForwardTillChar1()
            {
                Create("foo bar baz");
                Assert.Equal("f", _motionUtil.CharSearch('o', 1, CharSearchKind.TillChar, Path.Forward).Value.Span.GetText());
                Assert.Equal("foo ", _motionUtil.CharSearch('b', 1, CharSearchKind.TillChar, Path.Forward).Value.Span.GetText());
            }

            [Fact]
            public void ForwardTillChar2()
            {
                Create("foo bar baz");
                Assert.True(_motionUtil.CharSearch('q', 1, CharSearchKind.TillChar, Path.Forward).IsNone());
            }

            [Fact]
            public void ForwardTillChar3()
            {
                Create("foo bar baz");
                Assert.Equal("fo", _motionUtil.CharSearch('o', 2, CharSearchKind.TillChar, Path.Forward).Value.Span.GetText());
            }

            /// <summary>
            /// Bad count gets nothing in gVim
            /// </summary>
            [Fact]
            public void ForwardTillChar4()
            {
                Create("foo bar baz");
                Assert.True(_motionUtil.CharSearch('o', 300, CharSearchKind.TillChar, Path.Forward).IsNone());
            }

            [Fact]
            public void BackwardCharMotion1()
            {
                Create("the boy kicked the ball");
                _textView.MoveCaretTo(_textBuffer.GetLine(0).End);
                var data = _motionUtil.CharSearch('b', 1, CharSearchKind.ToChar, Path.Backward).Value;
                Assert.Equal("ball", data.Span.GetText());
                Assert.Equal(MotionKind.CharacterWiseExclusive, data.MotionKind);
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
            }

            [Fact]
            public void BackwardCharMotion2()
            {
                Create("the boy kicked the ball");
                _textView.MoveCaretTo(_textBuffer.GetLine(0).End);
                var data = _motionUtil.CharSearch('b', 2, CharSearchKind.ToChar, Path.Backward).Value;
                Assert.Equal("boy kicked the ball", data.Span.GetText());
                Assert.Equal(MotionKind.CharacterWiseExclusive, data.MotionKind);
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
            }

            /// <summary>
            /// Doing a backward char search on an empty line should produce no data
            /// </summary>
            [Fact]
            public void BackwardChar_OnEmptyLine()
            {
                Create("cat", "", "dog");
                _textView.MoveCaretToLine(1);
                var data = _motionUtil.CharSearch('b', 1, CharSearchKind.ToChar, Path.Backward);
                Assert.True(data.IsNone());
            }

            [Fact]
            public void BackwardTillCharMotion1()
            {
                Create("the boy kicked the ball");
                _textView.MoveCaretTo(_textBuffer.GetLine(0).End);
                var data = _motionUtil.CharSearch('b', 1, CharSearchKind.TillChar, Path.Backward).Value;
                Assert.Equal("all", data.Span.GetText());
                Assert.Equal(MotionKind.CharacterWiseExclusive, data.MotionKind);
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
            }

            [Fact]
            public void BackwardTillCharMotion2()
            {
                Create("the boy kicked the ball");
                _textView.MoveCaretTo(_textBuffer.GetLine(0).End);
                var data = _motionUtil.CharSearch('b', 2, CharSearchKind.TillChar, Path.Backward).Value;
                Assert.Equal("oy kicked the ball", data.Span.GetText());
                Assert.Equal(MotionKind.CharacterWiseExclusive, data.MotionKind);
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
            }

            /// <summary>
            /// Inner word from the middle of a word
            /// </summary>
            [Fact]
            public void InnerWord_Simple()
            {
                Create("the dog");
                _textView.MoveCaretTo(1);
                var data = _motionUtil.InnerWord(WordKind.NormalWord, 1, _textView.GetCaretPoint()).Value;
                Assert.Equal("the", data.Span.GetText());
                Assert.True(data.IsInclusive);
                Assert.True(data.IsForward);
            }

            /// <summary>
            /// An inner word motion which begins in space should include the full space span
            /// in the return
            /// </summary>
            [Fact]
            public void InnerWord_FromSpace()
            {
                Create("   the dog");
                _textView.MoveCaretTo(1);
                var data = _motionUtil.InnerWord(WordKind.NormalWord, 1, _textView.GetCaretPoint()).Value;
                Assert.Equal("   ", data.Span.GetText());
            }

            /// <summary>
            /// The count should apply equally to white space and the following words
            /// </summary>
            [Fact]
            public void InnerWord_FromSpaceWithCount()
            {
                Create("   the dog");
                _textView.MoveCaretTo(1);
                var data = _motionUtil.InnerWord(WordKind.NormalWord, 2, _textView.GetCaretPoint()).Value;
                Assert.Equal("   the", data.Span.GetText());
            }

            /// <summary>
            /// Including a case where the count gives us white space on both ends of the 
            /// returned span
            /// </summary>
            [Fact]
            public void InnerWord_FromSpaceWithOddCount()
            {
                Create("   the dog");
                _textView.MoveCaretTo(1);
                var data = _motionUtil.InnerWord(WordKind.NormalWord, 3, _textView.GetCaretPoint()).Value;
                Assert.Equal("   the ", data.Span.GetText());
            }

            /// <summary>
            /// When the caret is in the line break and there is a word at the end of the 
            /// line and there is a count of 1 then we just grab the last character of the
            /// previous word
            /// </summary>
            [Fact]
            public void InnerWord_FromLineBreakWthPrecedingWord()
            {
                Create("cat", "dog");
                _textView.MoveCaretTo(_textView.GetLine(0).End);
                var data = _motionUtil.InnerWord(WordKind.NormalWord, 1, _textView.GetCaretPoint()).Value;
                Assert.Equal("t", data.Span.GetText());
                Assert.True(data.IsForward);
            }

            /// <summary>
            /// When the caret is in the line break and there is a space at the end of the 
            /// line and there is a count of 1 then we just grab the entire preceding space
            /// </summary>
            [Fact]
            public void InnerWord_FromLineBreakWthPrecedingSpace()
            {
                Create("cat  ", "dog");
                _textView.MoveCaretTo(_textView.GetLine(0).End);
                var data = _motionUtil.InnerWord(WordKind.NormalWord, 1, _textView.GetCaretPoint()).Value;
                Assert.Equal("  ", data.Span.GetText());
                Assert.True(data.IsForward);
            }

            /// <summary>
            /// When in the line break and given a count the line break counts as our first
            /// and other wise proceed as a normal inner word motion
            /// </summary>
            [Fact]
            public void InnerWord_FromLineBreakWthCount()
            {
                Create("cat", "fish dog");
                _textView.MoveCaretTo(_textView.GetLine(0).End);
                var data = _motionUtil.InnerWord(WordKind.NormalWord, 2, _textView.GetCaretPoint()).Value;
                Assert.Equal(Environment.NewLine + "fish", data.Span.GetText());
                Assert.True(data.IsForward);
            }

            [Fact]
            public void LineOrFirstToFirstNonBlank1()
            {
                Create("foo", "bar", "baz");
                _textView.MoveCaretTo(_textBuffer.GetLine(1).Start);
                var data = _motionUtil.LineOrFirstToFirstNonBlank(FSharpOption.Create(0));
                Assert.Equal(_textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
                Assert.False(data.IsForward);
                Assert.True(data.MotionKind.IsLineWise);
                Assert.Equal(0, data.CaretColumn.AsInLastLine().Item);
            }

            [Fact]
            public void LineOrFirstToFirstNonBlank2()
            {
                Create("foo", "bar", "baz");
                var data = _motionUtil.LineOrFirstToFirstNonBlank(FSharpOption.Create(2));
                Assert.Equal(_textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
                Assert.True(data.IsForward);
                Assert.True(data.MotionKind.IsLineWise);
                Assert.Equal(0, data.CaretColumn.AsInLastLine().Item);
            }

            [Fact]
            public void LineOrFirstToFirstNonBlank3()
            {
                Create("foo", "  bar", "baz");
                var data = _motionUtil.LineOrFirstToFirstNonBlank(FSharpOption.Create(2));
                Assert.Equal(_textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
                Assert.True(data.IsForward);
                Assert.True(data.MotionKind.IsLineWise);
                Assert.Equal(2, data.CaretColumn.AsInLastLine().Item);
            }

            [Fact]
            public void LineOrFirstToFirstNonBlank4()
            {
                Create("foo", "  bar", "baz");
                var data = _motionUtil.LineOrFirstToFirstNonBlank(FSharpOption.Create(500));
                Assert.Equal(_textBuffer.GetLineRange(0, 0).ExtentIncludingLineBreak, data.Span);
                Assert.True(data.IsForward);
                Assert.True(data.MotionKind.IsLineWise);
                Assert.Equal(0, data.CaretColumn.AsInLastLine().Item);
            }

            [Fact]
            public void LineOrFirstToFirstNonBlank5()
            {
                Create("  the", "dog", "jumped");
                _textView.MoveCaretTo(_textView.GetLine(1).Start);
                var data = _motionUtil.LineOrFirstToFirstNonBlank(FSharpOption<int>.None);
                Assert.Equal(0, data.Span.Start.Position);
                Assert.Equal(2, data.CaretColumn.AsInLastLine().Item);
                Assert.False(data.IsForward);
            }

            [Fact]
            public void LineOrLastToFirstNonBlank1()
            {
                Create("foo", "bar", "baz");
                var data = _motionUtil.LineOrLastToFirstNonBlank(FSharpOption.Create(2));
                Assert.Equal(_textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
                Assert.True(data.IsForward);
                Assert.True(data.MotionKind.IsLineWise);
                Assert.Equal(0, data.CaretColumn.AsInLastLine().Item);
            }

            [Fact]
            public void LineOrLastToFirstNonBlank2()
            {
                Create("foo", "bar", "baz");
                _textView.MoveCaretTo(_textBuffer.GetLine(1).Start);
                var data = _motionUtil.LineOrLastToFirstNonBlank(FSharpOption.Create(0));
                Assert.Equal(_textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
                Assert.False(data.IsForward);
                Assert.True(data.MotionKind.IsLineWise);
                Assert.Equal(0, data.CaretColumn.AsInLastLine().Item);
            }

            [Fact]
            public void LineOrLastToFirstNonBlank3()
            {
                Create("foo", "bar", "baz");
                _textView.MoveCaretTo(_textBuffer.GetLine(1).Start);
                var data = _motionUtil.LineOrLastToFirstNonBlank(FSharpOption.Create(500));
                Assert.Equal(_textBuffer.GetLineRange(1, 2).ExtentIncludingLineBreak, data.Span);
                Assert.True(data.IsForward);
                Assert.True(data.MotionKind.IsLineWise);
                Assert.Equal(0, data.CaretColumn.AsInLastLine().Item);
            }

            [Fact]
            public void LineOrLastToFirstNonBlank4()
            {
                Create("foo", "bar", "baz");
                var data = _motionUtil.LineOrLastToFirstNonBlank(FSharpOption<int>.None);
                var span = new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, _textBuffer.CurrentSnapshot.Length);
                Assert.Equal(span, data.Span);
                Assert.True(data.IsForward);
                Assert.True(data.MotionKind.IsLineWise);
                Assert.Equal(0, data.CaretColumn.AsInLastLine().Item);
            }

            [Fact]
            public void LastNonBlankOnLine1()
            {
                Create("foo", "bar ");
                var data = _motionUtil.LastNonBlankOnLine(1);
                Assert.Equal(_textBuffer.GetLineRange(0).Extent, data.Span);
                Assert.True(data.IsForward);
                Assert.True(data.MotionKind.IsCharacterWiseInclusive);
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
                Assert.Equal(MotionKind.CharacterWiseInclusive, data.MotionKind);
            }

            [Fact]
            public void LastNonBlankOnLine2()
            {
                Create("foo", "bar ", "jaz");
                var data = _motionUtil.LastNonBlankOnLine(2);
                Assert.Equal(new SnapshotSpan(_textBuffer.GetPoint(0), _textBuffer.GetLine(1).Start.Add(3)), data.Span);
                Assert.True(data.IsForward);
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
                Assert.Equal(MotionKind.CharacterWiseInclusive, data.MotionKind);
            }

            [Fact]
            public void LastNonBlankOnLine3()
            {
                Create("foo", "bar ", "jaz", "");
                var data = _motionUtil.LastNonBlankOnLine(300);
                Assert.Equal(new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, _textBuffer.CurrentSnapshot.Length), data.Span);
                Assert.True(data.IsForward);
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
                Assert.Equal(MotionKind.CharacterWiseInclusive, data.MotionKind);
            }

            [Fact]
            public void LineFromTopOfVisibleWindow1()
            {
                var buffer = CreateTextBuffer("foo", "bar", "baz");
                var tuple = MockObjectFactory.CreateTextViewWithVisibleLines(buffer, 0, 1);
                Create(tuple.Item1.Object);
                var data = _motionUtil.LineFromTopOfVisibleWindow(FSharpOption<int>.None);
                Assert.Equal(buffer.GetLineRange(0).Extent, data.Span);
                Assert.True(data.MotionKind.IsLineWise);
                Assert.True(data.IsForward);
            }

            [Fact]
            public void LineFromTopOfVisibleWindow2()
            {
                var buffer = CreateTextBuffer("foo", "bar", "baz", "jazz");
                var tuple = MockObjectFactory.CreateTextViewWithVisibleLines(buffer, 0, 2);
                Create(tuple.Item1.Object);
                var data = _motionUtil.LineFromTopOfVisibleWindow(FSharpOption.Create(2));
                Assert.Equal(buffer.GetLineRange(0, 1).Extent, data.Span);
                Assert.True(data.MotionKind.IsLineWise);
                Assert.True(data.IsForward);
            }

            /// <summary>
            /// From visible line not caret point
            /// </summary>
            [Fact]
            public void LineFromTopOfVisibleWindow3()
            {
                var buffer = CreateTextBuffer("foo", "bar", "baz", "jazz");
                var tuple = MockObjectFactory.CreateTextViewWithVisibleLines(buffer, 0, 2, caretPosition: buffer.GetLine(2).Start.Position);
                Create(tuple.Item1.Object);
                var data = _motionUtil.LineFromTopOfVisibleWindow(FSharpOption.Create(2));
                Assert.Equal(buffer.GetLineRange(0, 1).Extent, data.Span);
                Assert.True(data.MotionKind.IsLineWise);
                Assert.False(data.IsForward);
            }

            [Fact]
            public void LineFromTopOfVisibleWindow4()
            {
                var buffer = CreateTextBuffer("  foo", "bar");
                var tuple = MockObjectFactory.CreateTextViewWithVisibleLines(buffer, 0, 1, caretPosition: buffer.GetLine(1).End);
                Create(tuple.Item1.Object);
                var data = _motionUtil.LineFromTopOfVisibleWindow(FSharpOption<int>.None);
                Assert.Equal(2, data.CaretColumn.AsInLastLine().Item);
            }

            [Fact]
            public void LineFromTopOfVisibleWindow5()
            {
                var buffer = CreateTextBuffer("  foo", "bar");
                var tuple = MockObjectFactory.CreateTextViewWithVisibleLines(buffer, 0, 1, caretPosition: buffer.GetLine(1).End);
                Create(tuple.Item1.Object);
                _globalSettings.StartOfLine = false;
                var data = _motionUtil.LineFromTopOfVisibleWindow(FSharpOption<int>.None);
                Assert.True(data.CaretColumn.IsNone);
            }

            [Fact]
            public void LineFromBottomOfVisibleWindow1()
            {
                var buffer = CreateTextBuffer("a", "b", "c", "d");
                var tuple = MockObjectFactory.CreateTextViewWithVisibleLines(buffer, 0, 2);
                Create(tuple.Item1.Object);
                var data = _motionUtil.LineFromBottomOfVisibleWindow(FSharpOption<int>.None);
                Assert.Equal(new SnapshotSpan(_textBuffer.GetPoint(0), _textBuffer.GetLine(2).End), data.Span);
                Assert.True(data.IsForward);
                Assert.Equal(OperationKind.LineWise, data.OperationKind);
            }

            [Fact]
            public void LineFromBottomOfVisibleWindow2()
            {
                var buffer = CreateTextBuffer("a", "b", "c", "d");
                var tuple = MockObjectFactory.CreateTextViewWithVisibleLines(buffer, 0, 2);
                Create(tuple.Item1.Object);
                var data = _motionUtil.LineFromBottomOfVisibleWindow(FSharpOption.Create(2));
                Assert.Equal(new SnapshotSpan(_textBuffer.GetPoint(0), _textBuffer.GetLine(1).End), data.Span);
                Assert.True(data.IsForward);
                Assert.Equal(OperationKind.LineWise, data.OperationKind);
            }

            [Fact]
            public void LineFromBottomOfVisibleWindow3()
            {
                var buffer = CreateTextBuffer("a", "b", "c", "d");
                var tuple = MockObjectFactory.CreateTextViewWithVisibleLines(buffer, 0, 2, caretPosition: buffer.GetLine(2).End);
                Create(tuple.Item1.Object);
                var data = _motionUtil.LineFromBottomOfVisibleWindow(FSharpOption.Create(2));
                Assert.Equal(new SnapshotSpan(_textBuffer.GetLine(1).Start, _textBuffer.GetLine(2).End), data.Span);
                Assert.False(data.IsForward);
                Assert.Equal(OperationKind.LineWise, data.OperationKind);
            }

            [Fact]
            public void LineFromBottomOfVisibleWindow4()
            {
                var buffer = CreateTextBuffer("a", "b", "  c", "d");
                var tuple = MockObjectFactory.CreateTextViewWithVisibleLines(buffer, 0, 2);
                Create(tuple.Item1.Object);
                var data = _motionUtil.LineFromBottomOfVisibleWindow(FSharpOption<int>.None);
                Assert.Equal(2, data.CaretColumn.AsInLastLine().Item);
            }

            [Fact]
            public void LineFromBottomOfVisibleWindow5()
            {
                var buffer = CreateTextBuffer("a", "b", "  c", "d");
                var tuple = MockObjectFactory.CreateTextViewWithVisibleLines(buffer, 0, 2);
                Create(tuple.Item1.Object);
                _globalSettings.StartOfLine = false;
                var data = _motionUtil.LineFromBottomOfVisibleWindow(FSharpOption<int>.None);
                Assert.True(data.CaretColumn.IsNone);
            }

            [Fact]
            public void LineFromMiddleOfWindow1()
            {
                var buffer = CreateTextBuffer("a", "b", "c", "d");
                var tuple = MockObjectFactory.CreateTextViewWithVisibleLines(buffer, 0, 2);
                Create(tuple.Item1.Object);
                var data = _motionUtil.LineInMiddleOfVisibleWindow();
                Assert.Equal(new SnapshotSpan(_textBuffer.GetPoint(0), _textBuffer.GetLine(1).End), data.Span);
                Assert.Equal(OperationKind.LineWise, data.OperationKind);
            }

            [Fact]
            public void LineDownToFirstNonBlank1()
            {
                Create("a", "b", "c", "d");
                var data = _motionUtil.LineDownToFirstNonBlank(1);
                Assert.True(data.MotionKind.IsLineWise);
                Assert.Equal(_textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
                Assert.True(data.IsForward);
            }

            [Fact]
            public void LineDownToFirstNonBlank2()
            {
                Create("a", "b", "c", "d");
                var data = _motionUtil.LineDownToFirstNonBlank(2);
                Assert.True(data.MotionKind.IsLineWise);
                Assert.Equal(_textBuffer.GetLineRange(0, 2).ExtentIncludingLineBreak, data.Span);
                Assert.True(data.IsForward);
            }

            /// <summary>
            /// Count of 0 is valid for this motion
            /// </summary>
            [Fact]
            public void LineDownToFirstNonBlank3()
            {
                Create("a", "b", "c", "d");
                var data = _motionUtil.LineDownToFirstNonBlank(0);
                Assert.True(data.MotionKind.IsLineWise);
                Assert.Equal(_textBuffer.GetLineRange(0).ExtentIncludingLineBreak, data.Span);
                Assert.True(data.IsForward);
            }

            /// <summary>
            /// This is a linewise motion and should return line spans
            /// </summary>
            [Fact]
            public void LineDownToFirstNonBlank4()
            {
                Create("cat", "dog", "bird");
                _textView.MoveCaretTo(1);
                var data = _motionUtil.LineDownToFirstNonBlank(1);
                var span = _textView.GetLineRange(0, 1).ExtentIncludingLineBreak;
                Assert.Equal(span, data.Span);
            }

            [Fact]
            public void LineDownToFirstNonBlank5()
            {
                Create("cat", "  dog", "bird");
                _textView.MoveCaretTo(1);
                var data = _motionUtil.LineDownToFirstNonBlank(1);
                Assert.True(data.CaretColumn.IsInLastLine);
                Assert.Equal(2, data.CaretColumn.AsInLastLine().Item);
            }

            [Fact]
            public void LineDownToFirstNonBlank6()
            {
                Create("cat", "  dog and again", "bird");
                _textView.MoveCaretTo(1);
                var data = _motionUtil.LineDownToFirstNonBlank(1);
                Assert.True(data.CaretColumn.IsInLastLine);
                Assert.Equal(2, data.CaretColumn.AsInLastLine().Item);
            }

            [Fact]
            public void LineDownToFirstNonBlankg()
            {
                Create("cat", "  dog and again", " here bird again");
                _textView.MoveCaretTo(1);
                var data = _motionUtil.LineDownToFirstNonBlank(2);
                Assert.True(data.CaretColumn.IsInLastLine);
                Assert.Equal(1, data.CaretColumn.AsInLastLine().Item);
            }

            [Fact]
            public void LineDown1()
            {
                Create("dog", "cat", "bird");
                var data = _motionUtil.LineDown(1).Value;
                AssertData(
                    data,
                    _textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak,
                    MotionKind.LineWise,
                    CaretColumn.NewInLastLine(0));
            }

            [Fact]
            public void LineDown2()
            {
                Create("dog", "cat", "bird");
                var data = _motionUtil.LineDown(2).Value;
                AssertData(
                    data,
                    _textBuffer.GetLineRange(0, 2).ExtentIncludingLineBreak,
                    MotionKind.LineWise,
                    CaretColumn.NewInLastLine(0));
            }

            [Fact]
            public void LineUp1()
            {
                Create("dog", "cat", "bird", "horse");
                _textView.MoveCaretTo(_textView.GetLine(2).Start);
                var data = _motionUtil.LineUp(1).Value;
                AssertData(
                    data,
                    _textBuffer.GetLineRange(1, 2).ExtentIncludingLineBreak,
                    MotionKind.LineWise,
                    CaretColumn.NewInLastLine(0));
            }

            [Fact]
            public void LineUp2()
            {
                Create("dog", "cat", "bird", "horse");
                _textView.MoveCaretTo(_textView.GetLine(2).Start);
                var data = _motionUtil.LineUp(2).Value;
                AssertData(
                    data,
                    _textBuffer.GetLineRange(0, 2).ExtentIncludingLineBreak,
                    MotionKind.LineWise,
                    CaretColumn.NewInLastLine(0));
            }

            [Fact]
            public void LineUp3()
            {
                Create("foo", "bar");
                _textView.MoveCaretTo(_textBuffer.GetLineFromLineNumber(1).Start);
                var data = _motionUtil.LineUp(1).Value;
                Assert.Equal(OperationKind.LineWise, data.OperationKind);
                Assert.Equal("foo" + Environment.NewLine + "bar", data.Span.GetText());
            }

            /// <summary>
            /// Make sure that section forward stops on a formfeed and doesn't 
            /// include it in the SnapshotSpan
            /// </summary>
            [Fact]
            public void SectionForward_FormFeedInFirstColumn()
            {
                Create(0, "dog", "\fpig", "{fox");
                var data = _motionUtil.SectionForward(MotionContext.Movement, 1);
                Assert.Equal(_textView.GetLineRange(0).ExtentIncludingLineBreak, data.Span);
            }

            /// <summary>
            /// Make sure that when the formfeed is the first character on the last line 
            /// that we don't count it as a blank when doing a last line adjustment for
            /// a movement
            /// </summary>
            [Fact]
            public void SectionForward_FormFeedOnLastLine()
            {
                Create(0, "dog", "cat", "\bbear");
                var data = _motionUtil.SectionForward(MotionContext.Movement, 1);
                Assert.Equal(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
                Assert.True(data.IsForward);
            }

            /// <summary>
            /// Doing a movement on the last line the movement should be backwards when the
            /// caret is positioned after the first non-blank on the line
            /// </summary>
            [Fact]
            public void SectionForward_BackwardsOnLastLine()
            {
                Create(0, "dog", "  cat");
                _textView.MoveCaretToLine(1, 4);
                var data = _motionUtil.SectionForward(MotionContext.Movement, 1);
                Assert.Equal("ca", data.Span.GetText());
                Assert.False(data.IsForward);
            }


            [Fact]
            public void SectionForward2()
            {
                Create(0, "dog", "\fpig", "fox");
                var data = _motionUtil.SectionForward(MotionContext.AfterOperator, 2);
                Assert.Equal(new SnapshotSpan(_snapshot, 0, _snapshot.Length), data.Span);
            }

            [Fact]
            public void SectionForward3()
            {
                Create(0, "dog", "{pig", "fox");
                var data = _motionUtil.SectionForward(MotionContext.AfterOperator, 2);
                Assert.Equal(new SnapshotSpan(_snapshot, 0, _snapshot.Length), data.Span);
            }

            [Fact]
            public void SectionForward4()
            {
                Create(0, "dog", "{pig", "{fox");
                var data = _motionUtil.SectionForward(MotionContext.Movement, 1);
                Assert.Equal(_textView.GetLineRange(0).ExtentIncludingLineBreak, data.Span);
            }

            [Fact]
            public void SectionForward5()
            {
                Create(0, "dog", "}pig", "fox");
                var data = _motionUtil.SectionForward(MotionContext.AfterOperator, 1);
                Assert.Equal(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
            }

            /// <summary>
            /// Only look for } after an operator
            /// </summary>
            [Fact]
            public void SectionForward6()
            {
                Create(0, "dog", "}pig", "fox");
                var data = _motionUtil.SectionForward(MotionContext.Movement, 1);
                Assert.Equal(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
            }

            [Fact]
            public void SectionBackwardOrOpenBrace1()
            {
                Create(0, "dog", "{brace", "pig", "}fox");
                var data = _motionUtil.SectionBackwardOrOpenBrace(1);
                Assert.True(data.Span.IsEmpty);
            }

            [Fact]
            public void SectionBackwardOrOpenBrace2()
            {
                Create("dog", "{brace", "pig", "}fox");
                _textView.MoveCaretTo(_textView.GetLine(2).Start.Position);
                var data = _motionUtil.SectionBackwardOrOpenBrace(1);
                Assert.Equal(_textView.GetLineRange(1).ExtentIncludingLineBreak, data.Span);
            }

            [Fact]
            public void SectionBackwardOrOpenBrace3()
            {
                Create("dog", "{brace", "pig", "}fox");
                _textView.MoveCaretTo(_textView.GetLine(2).Start.Position);
                var data = _motionUtil.SectionBackwardOrOpenBrace(2);
                Assert.Equal(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
            }

            [Fact]
            public void SectionBackwardOrOpenBrace4()
            {
                Create(0, "dog", "\fbrace", "pig", "}fox");
                var data = _motionUtil.SectionBackwardOrOpenBrace(1);
                Assert.True(data.Span.IsEmpty);
            }

            [Fact]
            public void SectionBackwardOrOpenBrace5()
            {
                Create("dog", "\fbrace", "pig", "}fox");
                _textView.MoveCaretTo(_textView.GetLine(2).Start.Position);
                var data = _motionUtil.SectionBackwardOrOpenBrace(1);
                Assert.Equal(_textView.GetLineRange(1).ExtentIncludingLineBreak, data.Span);
            }

            [Fact]
            public void SectionBackwardOrOpenBrace6()
            {
                Create("dog", "\fbrace", "pig", "}fox");
                _textView.MoveCaretTo(_textView.GetLine(2).Start.Position);
                var data = _motionUtil.SectionBackwardOrOpenBrace(2);
                Assert.Equal(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
            }

            /// <summary>
            /// Ignore the brace not on first column
            /// </summary>
            [Fact]
            public void SectionBackwardOrOpenBrace7()
            {
                Create("dog", "\f{brace", "pig", "}fox");
                _textView.MoveCaretTo(_textView.GetLine(2).Start.Position);
                var data = _motionUtil.SectionBackwardOrOpenBrace(2);
                Assert.Equal(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
            }

            [Fact]
            public void SectionBackwardOrOpenBrace8()
            {
                Create("dog", "{{foo", "{bar", "hello");
                _textView.MoveCaretTo(_textView.GetLine(2).End);
                var data = _motionUtil.SectionBackwardOrOpenBrace(2);
                Assert.Equal(
                    new SnapshotSpan(
                        _textBuffer.GetLine(0).Start,
                        _textBuffer.GetLine(2).End),
                    data.Span);
            }

            [Fact]
            public void SectionBackwardOrCloseBrace1()
            {
                Create(0, "dog", "}brace", "pig", "}fox");
                var data = _motionUtil.SectionBackwardOrCloseBrace(1);
                Assert.True(data.Span.IsEmpty);
            }

            [Fact]
            public void SectionBackwardOrCloseBrace2()
            {
                Create("dog", "}brace", "pig", "}fox");
                _textView.MoveCaretTo(_textView.GetLine(2).Start.Position);
                var data = _motionUtil.SectionBackwardOrCloseBrace(1);
                Assert.Equal(_textView.GetLineRange(1).ExtentIncludingLineBreak, data.Span);
            }

            [Fact]
            public void SectionBackwardOrCloseBrace3()
            {
                Create("dog", "}brace", "pig", "}fox");
                _textView.MoveCaretTo(_textView.GetLine(2).Start.Position);
                var data = _motionUtil.SectionBackwardOrCloseBrace(2);
                Assert.Equal(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
            }

            [Fact]
            public void SectionBackwardOrCloseBrace4()
            {
                Create(0, "dog", "\fbrace", "pig", "}fox");
                var data = _motionUtil.SectionBackwardOrCloseBrace(1);
                Assert.True(data.Span.IsEmpty);
            }

            [Fact]
            public void SectionBackwardOrCloseBrace5()
            {
                Create("dog", "\fbrace", "pig", "}fox");
                _textView.MoveCaretTo(_textView.GetLine(2).Start.Position);
                var data = _motionUtil.SectionBackwardOrCloseBrace(1);
                Assert.Equal(_textView.GetLineRange(1).ExtentIncludingLineBreak, data.Span);
            }

            [Fact]
            public void SectionBackwardOrCloseBrace6()
            {
                Create("dog", "\fbrace", "pig", "}fox");
                _textView.MoveCaretTo(_textView.GetLine(2).Start.Position);
                var data = _motionUtil.SectionBackwardOrCloseBrace(2);
                Assert.Equal(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
            }

            /// <summary>
            /// Ignore the brace not on first column
            /// </summary>
            [Fact]
            public void SectionBackwardOrCloseBrace7()
            {
                Create("dog", "\f}brace", "pig", "}fox");
                _textView.MoveCaretTo(_textView.GetLine(2).Start.Position);
                var data = _motionUtil.SectionBackwardOrCloseBrace(2);
                Assert.Equal(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
            }

            /// <summary>
            /// At the end of the ITextBuffer the span should return an empty span.  This can be 
            /// repro'd be setting ve=onemore and trying a 'y(' operation past the last character
            /// in the buffer
            /// </summary>
            [Fact]
            public void ParagraphForward_EndPoint()
            {
                Create("dog", "pig", "cat");
                _textView.MoveCaretTo(_textView.TextSnapshot.GetEndPoint());
                var data = _motionUtil.ParagraphForward(1);
                Assert.Equal("", data.Span.GetText());
            }

            /// <summary>
            /// A forward paragraph from the last character should return the char
            /// </summary>
            [Fact]
            public void ParagraphForward_LastChar()
            {
                Create("dog", "pig", "cat");
                _textView.MoveCaretTo(_textView.TextSnapshot.GetEndPoint().Subtract(1));
                var data = _motionUtil.ParagraphForward(1);
                Assert.Equal("t", data.Span.GetText());
            }

            [Fact]
            public void QuotedStringContents1()
            {
                Create(@"""foo""");
                var data = _motionUtil.QuotedStringContents('"');
                Assert.True(data.IsSome());
                AssertData(data.Value, new SnapshotSpan(_snapshot, 1, 3), MotionKind.CharacterWiseInclusive);
            }

            [Fact]
            public void QuotedStringContents2()
            {
                Create(@" ""bar""");
                var data = _motionUtil.QuotedStringContents('"');
                Assert.True(data.IsSome());
                AssertData(data.Value, new SnapshotSpan(_snapshot, 2, 3), MotionKind.CharacterWiseInclusive);
            }

            [Fact]
            public void QuotedStringContents3()
            {
                Create(@"""foo"" ""bar""");
                var start = _snapshot.GetText().IndexOf('b');
                _textView.MoveCaretTo(start);
                var data = _motionUtil.QuotedStringContents('"');
                Assert.True(data.IsSome());
                AssertData(data.Value, new SnapshotSpan(_snapshot, start, 3), MotionKind.CharacterWiseInclusive);
            }

            /// <summary>
            /// Ensure that the space after the sentence is included
            /// </summary>
            [Fact]
            public void SentencesForward_SpaceAfter()
            {
                Create("a! b");
                var data = _motionUtil.SentenceForward(1);
                AssertData(data, new SnapshotSpan(_snapshot, 0, 3));
            }

            /// <summary>
            /// At the end of the ITextBuffer there isn't a next sentence
            /// </summary>
            [Fact]
            public void SentencesForward_EndOfBuffer()
            {
                Create("a! b");
                _textView.MoveCaretTo(_snapshot.Length);
                var data = _motionUtil.SentenceForward(1);
                AssertData(data, new SnapshotSpan(_snapshot, _snapshot.Length, 0));
            }

            [Fact]
            public void Mark_Forward()
            {
                Create("the dog chased the ball");
                _vimTextBuffer.SetLocalMark(_localMarkA, 0, 3);
                var data = _motionUtil.Mark(_localMarkA).Value;
                Assert.Equal("the", data.Span.GetText());
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
                Assert.Equal(MotionKind.CharacterWiseExclusive, data.MotionKind);
                Assert.True(data.IsForward);
            }

            /// <summary>
            /// If a Mark is not set then the Mark motion should fail
            /// </summary>
            [Fact]
            public void Mark_DoesNotExist()
            {
                Create("the dog chased the ball");
                Assert.True(_motionUtil.Mark(_localMarkA).IsNone());
            }

            /// <summary>
            /// Ensure that a backwards mark produces a backwards span
            /// </summary>
            [Fact]
            public void Mark_Backward()
            {
                Create("the dog chased the ball");
                _textView.MoveCaretTo(3);
                _vimTextBuffer.SetLocalMark(_localMarkA, 0, 0);
                var data = _motionUtil.Mark(_localMarkA).Value;
                Assert.Equal("the", data.Span.GetText());
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
                Assert.Equal(MotionKind.CharacterWiseExclusive, data.MotionKind);
                Assert.False(data.IsForward);
            }

            [Fact]
            public void MarkLine_DoesNotExist()
            {
                Create("the dog chased the ball");
                Assert.True(_motionUtil.MarkLine(_localMarkA).IsNone());
            }

            [Fact]
            public void MarkLine_Forward()
            {
                Create("cat", "dog", "pig", "tree");
                _vimTextBuffer.SetLocalMark(_localMarkA, 1, 1);
                var data = _motionUtil.MarkLine(_localMarkA).Value;
                Assert.Equal(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
                Assert.True(data.IsForward);
                Assert.True(data.MotionKind.IsLineWise);
            }

            [Fact]
            public void MarkLine_Backward()
            {
                Create("cat", "dog", "pig", "tree");
                _textView.MoveCaretTo(_textView.GetLine(1).Start.Add(1));
                _vimTextBuffer.SetLocalMark(_localMarkA, 0, 0);
                var data = _motionUtil.MarkLine(_localMarkA).Value;
                Assert.Equal(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
                Assert.False(data.IsForward);
                Assert.True(data.MotionKind.IsLineWise);
            }

            [Fact]
            public void LineUpToFirstNonBlank_UseColumnNotPosition()
            {
                Create("the", "  dog", "cat");
                _textView.MoveCaretToLine(2);
                var data = _motionUtil.LineUpToFirstNonBlank(1);
                Assert.Equal(2, data.CaretColumn.AsInLastLine().Item);
                Assert.False(data.IsForward);
                Assert.Equal(_textView.GetLineRange(1, 2).ExtentIncludingLineBreak, data.Span);
            }

            [Fact]
            public void MatchingToken_SimpleParens()
            {
                Create("( )");
                var data = _motionUtil.MatchingToken().Value;
                Assert.Equal("( )", data.Span.GetText());
                Assert.True(data.IsForward);
                Assert.Equal(MotionKind.CharacterWiseInclusive, data.MotionKind);
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
            }

            [Fact]
            public void MatchingToken_SimpleParensWithPrefix()
            {
                Create("cat( )");
                var data = _motionUtil.MatchingToken().Value;
                Assert.Equal("cat( )", data.Span.GetText());
                Assert.True(data.IsForward);
            }

            [Fact]
            public void MatchingToken_TooManyOpenOnSameLine()
            {
                Create("cat(( )");
                Assert.True(_motionUtil.MatchingToken().IsNone());
            }

            [Fact]
            public void MatchingToken_AcrossLines()
            {
                Create("cat(", ")");
                var span = new SnapshotSpan(
                    _textView.GetLine(0).Start,
                    _textView.GetLine(1).Start.Add(1));
                var data = _motionUtil.MatchingToken().Value;
                Assert.Equal(span, data.Span);
                Assert.True(data.IsForward);
            }

            [Fact]
            public void MatchingToken_ParensFromEnd()
            {
                Create("cat( )");
                _textView.MoveCaretTo(5);
                var data = _motionUtil.MatchingToken().Value;
                Assert.Equal("( )", data.Span.GetText());
                Assert.False(data.IsForward);
            }

            [Fact]
            public void MatchingToken_ParensFromMiddle()
            {
                Create("cat( )");
                _textView.MoveCaretTo(4);
                var data = _motionUtil.MatchingToken().Value;
                Assert.Equal("( ", data.Span.GetText());
                Assert.False(data.IsForward);
            }

            /// <summary>
            /// Make sure we function properly with nested parens.
            /// </summary>
            [Fact]
            public void MatchingToken_ParensNestedFromEnd()
            {
                Create("(((a)))");
                _textView.MoveCaretTo(5);
                var data = _motionUtil.MatchingToken().Value;
                Assert.Equal("((a))", data.Span.GetText());
                Assert.False(data.IsForward);
            }

            /// <summary>
            /// Make sure we function properly with consecutive sets of parens
            /// </summary>
            [Fact]
            public void MatchingToken_ParensConsecutiveSetsFromEnd()
            {
                Create("((a)) /* ((b))");
                _textView.MoveCaretTo(12);
                var data = _motionUtil.MatchingToken().Value;
                Assert.Equal("(b)", data.Span.GetText());
                Assert.False(data.IsForward);
            }

            /// <summary>
            /// Make sure we function properly with consecutive sets of parens
            /// </summary>
            [Fact]
            public void MatchingToken_ParensConsecutiveSetsFromEnd2()
            {
                Create("((a)) /* ((b))");
                _textView.MoveCaretTo(13);
                var data = _motionUtil.MatchingToken().Value;
                Assert.Equal("((b))", data.Span.GetText());
                Assert.False(data.IsForward);
            }

            [Fact]
            public void MatchingToken_CommentStartDoesNotNest()
            {
                Create("/* /* */");
                var data = _motionUtil.MatchingToken().Value;
                Assert.Equal("/* /* */", data.Span.GetText());
                Assert.True(data.IsForward);
            }

            [Fact]
            public void MatchingToken_IfElsePreProc()
            {
                Create("#if foo #endif", "again", "#endif");
                var data = _motionUtil.MatchingToken().Value;
                var span = new SnapshotSpan(_textView.GetPoint(0), _textView.GetLine(2).Start.Add(1));
                Assert.Equal(span, data.Span);
                Assert.Equal(MotionKind.CharacterWiseInclusive, data.MotionKind);
            }

            /// <summary>
            /// Make sure find the full paragraph from a point in the middle.
            /// </summary>
            [Fact]
            public void AllParagraph_FromMiddle()
            {
                Create("a", "b", "", "c");
                _textView.MoveCaretToLine(1);
                var span = _motionUtil.AllParagraph(1).Value.Span;
                Assert.Equal(_snapshot.GetLineRange(0, 2).ExtentIncludingLineBreak, span);
            }

            /// <summary>
            /// Get a paragraph motion from the start of the ITextBuffer
            /// </summary>
            [Fact]
            public void AllParagraph_FromStart()
            {
                Create("a", "b", "", "c");
                var span = _motionUtil.AllParagraph(1).Value.Span;
                Assert.Equal(_snapshot.GetLineRange(0, 2).ExtentIncludingLineBreak, span);
            }

            /// <summary>
            /// A full paragraph should not include the preceding blanks when starting on
            /// an actual portion of the paragraph
            /// </summary>
            [Fact]
            public void AllParagraph_FromStartWithPreceedingBlank()
            {
                Create("a", "b", "", "c");
                _textView.MoveCaretToLine(2);
                var span = _motionUtil.AllParagraph(1).Value.Span;
                Assert.Equal(_snapshot.GetLineRange(2, 3).ExtentIncludingLineBreak, span);
            }

            /// <summary>
            /// Make sure the preceding blanks are included when starting on a blank
            /// line but not the trailing ones
            /// </summary>
            [Fact]
            public void AllParagraph_FromBlankLine()
            {
                Create("", "dog", "cat", "", "pig", "");
                _textView.MoveCaretToLine(3);
                var span = _motionUtil.AllParagraph(1).Value.Span;
                Assert.Equal(_snapshot.GetLineRange(3, 4).ExtentIncludingLineBreak, span);
            }

            /// <summary>
            /// If the span consists of only blank lines then it results in a failed motion.
            /// </summary>
            [Fact]
            public void AllParagraph_InBlankLinesAtEnd()
            {
                Create("", "dog", "", "");
                _textView.MoveCaretToLine(2);
                Assert.True(_motionUtil.AllParagraph(1).IsNone());
            }

            /// <summary>
            /// Make sure we raise an error if there is no word under the caret and that it 
            /// doesn't update LastSearchText
            /// </summary>
            [Fact]
            public void NextWord_NoWordUnderCaret()
            {
                Create("  ", "foo bar baz");
                _vimData.LastSearchData = VimUtil.CreateSearchData("cat");
                _statusUtil.Setup(x => x.OnError(Resources.NormalMode_NoWordUnderCursor)).Verifiable();
                _motionUtil.NextWord(Path.Forward, 1);
                _statusUtil.Verify();
                Assert.Equal("cat", _vimData.LastSearchData.Pattern);
            }

            /// <summary>
            /// Simple match should update LastSearchData and return the appropriate span
            /// </summary>
            [Fact]
            public void NextWord_Simple()
            {
                Create("hello world", "hello");
                var result = _motionUtil.NextWord(Path.Forward, 1).Value;
                Assert.Equal(_textView.GetLine(0).ExtentIncludingLineBreak, result.Span);
                Assert.Equal(@"\<hello\>", _vimData.LastSearchData.Pattern);
            }

            /// <summary>
            /// If the caret starts on a blank move to the first non-blank to find the 
            /// word.  This is true if we are searching forward or backward.  The original
            /// point though should be included in the search
            /// </summary>
            [Fact]
            public void NextWord_BackwardGoPastBlanks()
            {
                Create("dog   cat", "cat");
                _statusUtil.Setup(x => x.OnWarning(Resources.Common_SearchBackwardWrapped)).Verifiable();
                _textView.MoveCaretTo(4);
                var result = _motionUtil.NextWord(Path.Backward, 1).Value;
                Assert.Equal("  cat" + Environment.NewLine, result.Span.GetText());
                _statusUtil.Verify();
            }

            /// <summary>
            /// Make sure that searching backward from the middle of a word starts at the 
            /// beginning of the word
            /// </summary>
            [Fact]
            public void NextWord_BackwardFromMiddleOfWord()
            {
                Create("cat cat cat");
                _textView.MoveCaretTo(5);
                var result = _motionUtil.NextWord(Path.Backward, 1).Value;
                Assert.Equal("cat c", result.Span.GetText());
            }

            /// <summary>
            /// A non-word shouldn't require whole word
            /// </summary>
            [Fact]
            public void NextWord_Nonword()
            {
                Create("{", "dog", "{", "cat");
                var result = _motionUtil.NextWord(Path.Forward, 1).Value;
                Assert.Equal(_textView.GetLine(2).Start, result.Span.End);
            }

            /// <summary>
            /// Make sure we pass the LastSearch value to the method and move the caret
            /// for the provided SearchResult
            /// </summary>
            [Fact]
            public void LastSearch_UsePattern()
            {
                Create("foo bar", "foo");
                var data = new SearchData("foo", Path.Forward);
                _vimData.LastSearchData = data;
                var result = _motionUtil.LastSearch(false, 1).Value;
                Assert.Equal("foo bar" + Environment.NewLine, result.Span.GetText());
            }

            /// <summary>
            /// Make sure that this doesn't update the LastSearh field.  Only way to check this is 
            /// when we reverse the polarity of the search
            /// </summary>
            [Fact]
            public void LastSearch_DontUpdateLastSearch()
            {
                Create("dog cat", "dog", "dog");
                var data = new SearchData("dog", Path.Forward);
                _vimData.LastSearchData = data;
                _statusUtil.Setup(x => x.OnWarning(Resources.Common_SearchBackwardWrapped)).Verifiable();
                _motionUtil.LastSearch(true, 1);
                Assert.Equal(data, _vimData.LastSearchData);
                _statusUtil.Verify();
            }

            /// <summary>
            /// Simple matched bracket test
            /// </summary>
            [Fact]
            public void GetBlock_Simple()
            {
                Create("[cat] dog");
                var span = _motionUtil.GetBlock(BlockKind.Bracket, _textBuffer.GetPoint(0)).Value;
                Assert.Equal(_textBuffer.GetSpan(0, 5), span);
            }

            /// <summary>
            /// Simple matched bracket test from the middle
            /// </summary>
            [Fact]
            public void GetBlock_Simple_FromMiddle()
            {
                Create("[cat] dog");
                var span = _motionUtil.GetBlock(BlockKind.Bracket, _textBuffer.GetPoint(2)).Value;
                Assert.Equal(_textBuffer.GetSpan(0, 5), span);
            }

            /// <summary>
            /// Make sure that we can process the nested block when the caret is before it
            /// </summary>
            [Fact]
            public void GetBlock_Nested_Before()
            {
                Create("cat (fo(a)od) dog");
                var span = _motionUtil.GetBlock(BlockKind.Paren, _textBuffer.GetPoint(6)).Value;
                Assert.Equal(_textBuffer.GetSpan(4, 9), span);
            }

            /// <summary>
            /// Make sure that we can process the nested block when the caret is after it
            /// </summary>
            [Fact]
            public void GetBlock_Nested_After()
            {
                Create("cat (fo(a)od) dog");
                var span = _motionUtil.GetBlock(BlockKind.Paren, _textBuffer.GetPoint(10)).Value;
                Assert.Equal(_textBuffer.GetSpan(4, 9), span);
            }

            /// <summary>
            /// Bad match because of no start char
            /// </summary>
            [Fact]
            public void GetBlock_Bad_NoStartChar()
            {
                Create("cat] dog");
                var span = _motionUtil.GetBlock(BlockKind.Bracket, _textBuffer.GetPoint(0));
                Assert.True(span.IsNone());
            }

            /// <summary>
            /// Bad match because of no end char
            /// </summary>
            [Fact]
            public void GetBlock_Bad_NoEndChar()
            {
                Create("[cat dog");
                var span = _motionUtil.GetBlock(BlockKind.Bracket, _textBuffer.GetPoint(0));
                Assert.True(span.IsNone());
            }

            [Fact]
            public void GetBlock_Bad_EscapedStartChar()
            {
                Create(@"\[cat] dog");
                var span = _motionUtil.GetBlock(BlockKind.Bracket, _textBuffer.GetPoint(1));
                Assert.True(span.IsNone());
            }

            /// <summary>
            /// Break on section macros
            /// </summary>
            [Fact]
            public void GetSections_WithMacroAndCloseSplit()
            {
                Create("dog.", ".HH", "cat.");
                _globalSettings.Sections = "HH";
                var ret = _motionUtil.GetSections(SectionKind.OnCloseBrace, Path.Forward, _textBuffer.GetPoint(0));
                Assert.Equal(
                    new[] { "dog." + Environment.NewLine, ".HH" + Environment.NewLine + "cat." },
                    ret.Select(x => x.GetText()).ToList());
            }

            /// <summary>
            /// Break on section macros
            /// </summary>
            [Fact]
            public void GetSections_WithMacroBackwardAndCloseSplit()
            {
                Create("dog.", ".HH", "cat.");
                _globalSettings.Sections = "HH";
                var ret = _motionUtil.GetSections(SectionKind.OnCloseBrace, Path.Backward, _textBuffer.GetEndPoint());
                Assert.Equal(
                    new[] { ".HH" + Environment.NewLine + "cat.", "dog." + Environment.NewLine },
                    ret.Select(x => x.GetText()).ToList());
            }

            /// <summary>
            /// Going forward we should include the brace line
            /// </summary>
            [Fact]
            public void GetSectionsWithSplit_FromBraceLine()
            {
                Create("dog.", "}", "cat");
                var ret = _motionUtil.GetSections(SectionKind.OnCloseBrace, Path.Forward, _textBuffer.GetLine(1).Start);
                Assert.Equal(
                    new[] { "}" + Environment.NewLine + "cat" },
                    ret.Select(x => x.GetText()).ToList());

                ret = _motionUtil.GetSections(SectionKind.OnCloseBrace, Path.Forward, _textBuffer.GetPoint(0));
                Assert.Equal(
                    new[] { "dog." + Environment.NewLine, "}" + Environment.NewLine + "cat" },
                    ret.Select(x => x.GetText()).ToList());
            }

            /// <summary>
            /// Going backward we should not include the brace line
            /// </summary>
            [Fact]
            public void GetSectionsWithSplit_FromBraceLineBackward()
            {
                Create("dog.", "}", "cat");
                var ret = _motionUtil.GetSections(SectionKind.OnCloseBrace, Path.Backward, _textBuffer.GetLine(1).Start);
                Assert.Equal(
                    new[] { "dog." + Environment.NewLine },
                    ret.Select(x => x.GetText()).ToList());

                ret = _motionUtil.GetSections(SectionKind.OnCloseBrace, Path.Backward, _textBuffer.GetEndPoint());
                Assert.Equal(
                    new[] { "}" + Environment.NewLine + "cat", "dog." + Environment.NewLine },
                    ret.Select(x => x.GetText()).ToList());
            }

            [Fact]
            public void GetSentences1()
            {
                Create("a. b.");
                var ret = _motionUtil.GetSentences(SentenceKind.Default, Path.Forward, _snapshot.GetPoint(0));
                Assert.Equal(
                    new[] { "a.", "b." },
                    ret.Select(x => x.GetText()).ToList());
            }

            [Fact]
            public void GetSentences2()
            {
                Create("a! b.");
                var ret = _motionUtil.GetSentences(SentenceKind.Default, Path.Forward, _snapshot.GetPoint(0));
                Assert.Equal(
                    new[] { "a!", "b." },
                    ret.Select(x => x.GetText()).ToList());
            }

            [Fact]
            public void GetSentences3()
            {
                Create("a? b.");
                var ret = _motionUtil.GetSentences(SentenceKind.Default, Path.Forward, _snapshot.GetPoint(0));
                Assert.Equal(
                    new[] { "a?", "b." },
                    ret.Select(x => x.GetText()).ToList());
            }

            [Fact]
            public void GetSentences4()
            {
                Create("a? b.");
                var ret = _motionUtil.GetSentences(SentenceKind.Default, Path.Forward, _snapshot.GetEndPoint());
                Assert.Equal(
                    new string[] { },
                    ret.Select(x => x.GetText()).ToList());
            }

            /// <summary>
            /// Make sure the return doesn't include an empty span for the end point
            /// </summary>
            [Fact]
            public void GetSentences_BackwardFromEndOfBuffer()
            {
                Create("a? b.");
                var ret = _motionUtil.GetSentences(SentenceKind.Default, Path.Backward, _snapshot.GetEndPoint());
                Assert.Equal(
                    new[] { "b.", "a?" },
                    ret.Select(x => x.GetText()).ToList());
            }

            /// <summary>
            /// Sentences are an exclusive motion and hence backward from a single whitespace 
            /// to a sentence boundary should not include the whitespace
            /// </summary>
            [Fact]
            public void GetSentences_BackwardFromSingleWhitespace()
            {
                Create("a? b.");
                var ret = _motionUtil.GetSentences(SentenceKind.Default, Path.Backward, _snapshot.GetPoint(2));
                Assert.Equal(
                    new[] { "a?" },
                    ret.Select(x => x.GetText()).ToList());
            }

            /// <summary>
            /// Make sure we include many legal trailing characters
            /// </summary>
            [Fact]
            public void GetSentences_ManyTrailingChars()
            {
                Create("a?)]' b.");
                var ret = _motionUtil.GetSentences(SentenceKind.Default, Path.Forward, _snapshot.GetPoint(0));
                Assert.Equal(
                    new[] { "a?)]'", "b." },
                    ret.Select(x => x.GetText()).ToList());
            }

            /// <summary>
            /// The character should go on the previous sentence
            /// </summary>
            [Fact]
            public void GetSentences_BackwardWithCharBetween()
            {
                Create("a?) b.");
                var ret = _motionUtil.GetSentences(SentenceKind.Default, Path.Backward, _snapshot.GetEndPoint());
                Assert.Equal(
                    new[] { "b.", "a?)" },
                    ret.Select(x => x.GetText()).ToList());
            }

            /// <summary>
            /// Not a sentence unless the end character is followed by a space / tab / newline
            /// </summary>
            [Fact]
            public void GetSentences_NeedSpaceAfterEndCharacter()
            {
                Create("a!b. c");
                var ret = _motionUtil.GetSentences(SentenceKind.Default, Path.Forward, _snapshot.GetPoint(0));
                Assert.Equal(
                    new[] { "a!b.", "c" },
                    ret.Select(x => x.GetText()).ToList());
            }

            /// <summary>
            /// Only a valid boundary if the end character is followed by one of the 
            /// legal follow up characters (spaces, tabs, end of line after trailing chars)
            /// </summary>
            [Fact]
            public void GetSentences_IncompleteBoundary()
            {
                Create("a!b. c");
                var ret = _motionUtil.GetSentences(SentenceKind.Default, Path.Backward, _snapshot.GetEndPoint());
                Assert.Equal(
                    new[] { "c", "a!b." },
                    ret.Select(x => x.GetText()).ToList());
            }

            /// <summary>
            /// Make sure blank lines are included as sentence boundaries.  Note: Only the first blank line in a series
            /// of lines is actually a sentence.  Every following blank is just white space in between the blank line
            /// sentence and the start of the next sentence
            /// </summary>
            [Fact]
            public void GetSentences_ForwardBlankLinesAreBoundaries()
            {
                Create("a", "", "", "b");
                var ret = _motionUtil.GetSentences(SentenceKind.Default, Path.Forward, _snapshot.GetPoint(0));
                Assert.Equal(
                    new[]
                {
                    _textBuffer.GetLineRange(0, 0).ExtentIncludingLineBreak,
                    _textBuffer.GetLineRange(1, 1).ExtentIncludingLineBreak,
                    _textBuffer.GetLineRange(3, 3).ExtentIncludingLineBreak
                },
                    ret.ToList());
            }

            /// <summary>
            /// Get a sentence from the middle of the word
            /// </summary>
            [Fact]
            public void GetSentences_FromMiddleOfWord()
            {
                Create("dog", "cat", "bear");
                var ret = _motionUtil.GetSentences(SentenceKind.Default, Path.Forward, _snapshot.GetEndPoint().Subtract(1));
                Assert.Equal(
                    new[] { "dog" + Environment.NewLine + "cat" + Environment.NewLine + "bear" },
                    ret.Select(x => x.GetText()).ToList());
            }

            /// <summary>
            /// Don't include a sentence if we go backward from the first character of
            /// the sentence
            /// </summary>
            [Fact]
            public void GetSentences_BackwardFromStartOfSentence()
            {
                Create("dog. cat");
                var ret = _motionUtil.GetSentences(SentenceKind.Default, Path.Backward, _textBuffer.GetPoint(4));
                Assert.Equal(
                    new[] { "dog." },
                    ret.Select(x => x.GetText()).ToList());
            }

            /// <summary>
            /// A blank line is a sentence
            /// </summary>
            [Fact]
            public void GetSentences_BlankLinesAreSentences()
            {
                Create("dog.  ", "", "cat.");
                var ret = _motionUtil.GetSentences(SentenceKind.Default, Path.Forward, _textBuffer.GetPoint(0));
                Assert.Equal(
                    new[] { "dog.", Environment.NewLine, "cat." },
                    ret.Select(x => x.GetText()).ToList());
            }

            [Fact]
            public void GetParagraphs_SingleBreak()
            {
                Create("a", "b", "", "c");
                var ret = _motionUtil.GetParagraphs(Path.Forward, _snapshot.GetPoint(0));
                Assert.Equal(
                    new[]
                {
                    _textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak,
                    _textBuffer.GetLineRange(2, 3).ExtentIncludingLineBreak
                },
                    ret.ToList());
            }

            /// <summary>
            /// Consecutive breaks should not produce separate paragraphs.  They are treated as 
            /// part of the same paragraph
            /// </summary>
            [Fact]
            public void GetParagraphs_ConsequtiveBreaks()
            {
                Create("a", "b", "", "", "c");
                var ret = _motionUtil.GetParagraphs(Path.Forward, _snapshot.GetPoint(0));
                Assert.Equal(
                    new[]
                {
                    _textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak,
                    _textBuffer.GetLineRange(2, 4).ExtentIncludingLineBreak
                },
                    ret.ToList());
            }

            /// <summary>
            /// Form feed is a section and hence a paragraph boundary
            /// </summary>
            [Fact]
            public void GetParagraphs_FormFeedShouldBeBoundary()
            {
                Create("a", "b", "\f", "", "c");
                var ret = _motionUtil.GetParagraphs(Path.Forward, _snapshot.GetPoint(0));
                Assert.Equal(
                    new[]
                {
                    _textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak,
                    _textBuffer.GetLineRange(2, 2).ExtentIncludingLineBreak,
                    _textBuffer.GetLineRange(3, 4).ExtentIncludingLineBreak
                },
                    ret.ToList());
            }

            /// <summary>
            /// A form feed is a section boundary and should not count as a consecutive paragraph
            /// boundary
            /// </summary>
            [Fact]
            public void GetParagraphs_FormFeedIsNotConsequtive()
            {
                Create("a", "b", "\f", "", "c");
                var ret = _motionUtil.GetParagraphs(Path.Forward, _snapshot.GetPoint(0));
                Assert.Equal(
                    new[]
                {
                    _textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak,
                    _textBuffer.GetLineRange(2, 2).ExtentIncludingLineBreak,
                    _textBuffer.GetLineRange(3, 4).ExtentIncludingLineBreak
                },
                    ret.ToList());
            }

            /// <summary>
            /// Make sure we respect macro breaks
            /// </summary>
            [Fact]
            public void GetParagraphs_MacroBreak()
            {
                Create("a", ".hh", "bear");
                _globalSettings.Paragraphs = "hh";
                var ret = _motionUtil.GetParagraphs(Path.Forward, _snapshot.GetPoint(0));
                Assert.Equal(
                    new[]
                {
                    _textBuffer.GetLineRange(0, 0).ExtentIncludingLineBreak,
                    _textBuffer.GetLineRange(1,2).ExtentIncludingLineBreak
                },
                    ret.ToList());
            }

            /// <summary>
            /// Make sure we respect macro breaks of length 1
            /// </summary>
            [Fact]
            public void GetParagraphs_MacroBreakLengthOne()
            {
                Create("a", ".j", "bear");
                _globalSettings.Paragraphs = "hhj ";
                var ret = _motionUtil.GetParagraphs(Path.Forward, _snapshot.GetPoint(0));
                Assert.Equal(
                    new[]
                {
                    _textBuffer.GetLineRange(0, 0).ExtentIncludingLineBreak,
                    _textBuffer.GetLineRange(1,2).ExtentIncludingLineBreak
                },
                    ret.ToList());
            }

            /// <summary>
            /// Make sure that the bar motion goes to the specified column
            /// </summary>
            [Fact]
            public void Bar_Simple()
            {
                Create("The quick brown fox jumps over the lazy dog.");
                _textView.MoveCaretTo(0);
                var data = _motionUtil.LineToColumn(4);
                Assert.Equal("The", data.Span.GetText());
                Assert.Equal(true, data.IsForward);
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
                Assert.Equal(MotionKind.CharacterWiseExclusive, data.MotionKind);
                Assert.Equal(CaretColumn.NewScreenColumn(3), data.DesiredColumn);
            }

            /// <summary>
            /// Make sure that the bar motion handles backward moves properly
            /// </summary>
            [Fact]
            public void Bar_Backward()
            {
                Create("The quick brown fox jumps over the lazy dog.");
                _textView.MoveCaretTo(3);
                var data = _motionUtil.LineToColumn(1);
                Assert.Equal("The", data.Span.GetText());
                Assert.Equal(false, data.IsForward);
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
                Assert.Equal(MotionKind.CharacterWiseExclusive, data.MotionKind);
                Assert.Equal(CaretColumn.NewScreenColumn(0), data.DesiredColumn);
            }

            /// <summary>
            /// Make sure that the bar motion can handle ending up in the same column
            /// </summary>
            [Fact]
            public void Bar_NoMove()
            {
                Create("The quick brown fox jumps over the lazy dog.");
                _textView.MoveCaretTo(2);
                var data = _motionUtil.LineToColumn(3);
                Assert.Equal(0, data.Span.Length);
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
                Assert.Equal(MotionKind.CharacterWiseExclusive, data.MotionKind);
                Assert.Equal(CaretColumn.NewScreenColumn(2), data.DesiredColumn);
            }

            /// <summary>
            /// Make sure that the bar motion goes to the tab that spans over the given column
            /// </summary>
            [Fact]
            public void Bar_OverTabs()
            {
                Create("\t\t\tThe quick brown fox jumps over the lazy dog.");
                _textView.MoveCaretTo(3);
                _localSettings.TabStop = 4;

                var data = _motionUtil.LineToColumn(2);

                // Tabs are 4 spaces long; we should end up in the first tab
                Assert.Equal(data.Span.GetText(), "\t\t\t");
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
                Assert.Equal(MotionKind.CharacterWiseExclusive, data.MotionKind);
                Assert.Equal(CaretColumn.NewScreenColumn(1), data.DesiredColumn);
            }

            /// <summary>
            /// Make sure that the bar motion knows where it wanted to end up, even past the end of line.
            /// </summary>
            [Fact]
            public void Bar_PastEnd()
            {
                Create("Teh");
                _textView.MoveCaretTo(1);
                var data = _motionUtil.LineToColumn(100);

                Assert.Equal(data.Span.End, _textView.GetLine(0).End);
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
                Assert.Equal(MotionKind.CharacterWiseExclusive, data.MotionKind);
                Assert.Equal(CaretColumn.NewScreenColumn(99), data.DesiredColumn);
            }
        }
    }
}
