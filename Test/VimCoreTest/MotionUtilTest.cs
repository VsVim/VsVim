using System;
using System.Linq;
using Vim.EditorHost;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Vim.Extensions;
using Vim.UnitTest.Mock;
using Xunit;
using Xunit.Extensions;
using Xunit.Sdk;

namespace Vim.UnitTest
{
    public abstract class MotionUtilTest : VimTestBase
    {
        protected ITextBuffer _textBuffer;
        protected IWpfTextView _textView;
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

        protected virtual void Create(params string[] lines)
        {
            var textView = CreateTextView(lines);
            Create(textView);
        }

        protected void Create(int caretPosition, params string[] lines)
        {
            Create(lines);
            _textView.MoveCaretTo(caretPosition);
        }

        protected void Create(IWpfTextView textView)
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

        public void AssertData(MotionResult data, SnapshotSpan? span, MotionKind motionKind = null, bool? isForward = null, CaretColumn caretColumn = null)
        {
            if (span != null)
            {
                Assert.Equal(span.Value, data.Span);
            }
            if (isForward != null)
            {
                Assert.Equal(isForward.Value, data.IsForward);
            }
            if (motionKind != null)
            {
                Assert.Equal(motionKind, data.MotionKind);
            }
            if (caretColumn != null)
            {
                Assert.Equal(caretColumn, data.CaretColumn);
            }
        }

        public void AssertData(FSharpOption<MotionResult> data, SnapshotSpan? span = null, MotionKind motionKind = null, bool? isForward = null, CaretColumn caretColumn = null)
        {
            Assert.True(data.IsSome());
            AssertData(data.value, span, motionKind, isForward, caretColumn);
        }

        public sealed class AdjustMotionResult : MotionUtilTest
        {
            /// <summary>
            /// Inclusive motion values shouldn't be adjusted
            /// </summary>
            [WpfFact]
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
            [WpfFact]
            public void FullLine()
            {
                Create("  cat", "dog");
                var span = new SnapshotSpan(_textView.GetPoint(2), _textView.GetLine(1).Start);
                var result1 = VimUtil.CreateMotionResult(span, motionKind: MotionKind.CharacterWiseExclusive);
                var result2 = _motionUtil.AdjustMotionResult(Motion.CharLeft, result1);
                Assert.Equal(OperationKind.LineWise, result2.OperationKind);
                Assert.Equal(_textView.GetLine(0).ExtentIncludingLineBreak, result2.Span);
                Assert.Equal(span, result2.SpanBeforeExclusivePromotion.Value);
                Assert.True(result2.MotionKind.IsLineWise);
            }

            /// <summary>
            /// Don't make it full line if it doesn't start before the first real character
            /// </summary>
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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

        public sealed class ForcedCharacterWiseTest : MotionUtilTest
        {
            [WpfFact]
            public void LineDown()
            {
                Create("the", "dog");
                AssertData(
                    _motionUtil.ForceCharacterWise(Motion.LineDown, new MotionArgument(MotionContext.AfterOperator)),
                    span: _textBuffer.GetLine(0).ExtentIncludingLineBreak,
                    motionKind: MotionKind.CharacterWiseExclusive);
            }

            [WpfFact]
            public void FlipExclusiveToInclusive()
            {
                Create("dog");
                AssertData(
                    _motionUtil.ForceCharacterWise(Motion.CharRight, new MotionArgument(MotionContext.AfterOperator)),
                    span: _textBuffer.GetLineSpan(lineNumber: 0, length: 2),
                    motionKind: MotionKind.CharacterWiseInclusive);
            }

            [WpfFact]
            public void FlipInclusiveToExclusive()
            {
                Create("dog");
                AssertData(
                    _motionUtil.ForceCharacterWise(Motion.NewCharSearch(CharSearchKind.ToChar, SearchPath.Forward, 'o'), new MotionArgument(MotionContext.AfterOperator)),
                    span: _textBuffer.GetLineSpan(lineNumber: 0, length: 1),
                    motionKind: MotionKind.CharacterWiseExclusive);
            }
        }

        public sealed class GetBlockTest : MotionUtilTest
        {
            private SnapshotSpan GetBlockSpan(BlockKind blockKind, SnapshotPoint point)
            {
                var option = _motionUtil.GetBlock(blockKind, point);
                Assert.True(option.IsSome());
                var tuple = option.Value;
                return new SnapshotSpan(tuple.Item1, tuple.Item2.Add(1));
            }

            /// <summary>
            /// Simple matched bracket test
            /// </summary>
            [WpfFact]
            public void Simple()
            {
                Create("[cat] dog");
                var span = GetBlockSpan(BlockKind.Bracket, _textBuffer.GetPoint(0));
                Assert.Equal(_textBuffer.GetSpan(0, 5), span);
            }

            /// <summary>
            /// Simple matched bracket test from the middle
            /// </summary>
            [WpfFact]
            public void Simple_FromMiddle()
            {
                Create("[cat] dog");
                var span = GetBlockSpan(BlockKind.Bracket, _textBuffer.GetPoint(2));
                Assert.Equal(_textBuffer.GetSpan(0, 5), span);
            }

            /// <summary>
            /// Make sure that we can process the nested block when the caret is before it
            /// </summary>
            [WpfFact]
            public void Nested_Before()
            {
                Create("cat (fo(a)od) dog");
                var span = GetBlockSpan(BlockKind.Paren, _textBuffer.GetPoint(6));
                Assert.Equal(_textBuffer.GetSpan(4, 9), span);
            }

            /// <summary>
            /// Make sure that we can process the nested block when the caret is after it
            /// </summary>
            [WpfFact]
            public void Nested_After()
            {
                Create("cat (fo(a)od) dog");
                var span = GetBlockSpan(BlockKind.Paren, _textBuffer.GetPoint(10));
                Assert.Equal(_textBuffer.GetSpan(4, 9), span);
            }

            /// <summary>
            /// Make sure that we can process the nested block when the caret is at the end
            /// </summary>
            [WpfFact]
            public void Nested_FromLastChar()
            {
                Create("cat (fo(a)od) dog");
                var span = GetBlockSpan(BlockKind.Paren, _textBuffer.GetPoint(12));
                Assert.Equal(_textBuffer.GetSpan(4, 9), span);
            }

            /// <summary>
            /// Make sure that we can process the nested block when the caret is at the start
            /// </summary>
            [WpfFact]
            public void Nested_FromFirstChar()
            {
                Create("cat (fo(a)od) dog");
                var span = GetBlockSpan(BlockKind.Paren, _textBuffer.GetPoint(4));
                Assert.Equal(_textBuffer.GetSpan(4, 9), span);
            }
            /// <summary>
            /// Bad match because of no start char
            /// </summary>
            [WpfFact]
            public void Bad_NoStartChar()
            {
                Create("cat] dog");
                var span = _motionUtil.GetBlock(BlockKind.Bracket, _textBuffer.GetPoint(0));
                Assert.True(span.IsNone());
            }

            /// <summary>
            /// Bad match because of no end char
            /// </summary>
            [WpfFact]
            public void Bad_NoEndChar()
            {
                Create("[cat dog");
                var span = _motionUtil.GetBlock(BlockKind.Bracket, _textBuffer.GetPoint(0));
                Assert.True(span.IsNone());
            }

            [WpfFact]
            public void Bad_EscapedStartChar()
            {
                Create(@"\[cat] dog");
                var span = _motionUtil.GetBlock(BlockKind.Bracket, _textBuffer.GetPoint(1));
                Assert.True(span.IsNone());
            }

            [WpfFact]
            public void StringTrap_BeforeString()
            {
                Create("fun(a, \" (\", b) # bar");
                var span = GetBlockSpan(BlockKind.Paren, _textBuffer.GetPoint(3));
                Assert.Equal(_textBuffer.GetSpan(3, 12), span);
            }

            [WpfFact]
            public void StringTrap_AtStartOfString()
            {
                Create("fun(a, \" (\", b) # bar");
                var span = GetBlockSpan(BlockKind.Paren, _textBuffer.GetPoint(8));
                Assert.Equal(_textBuffer.GetSpan(3, 12), span);
            }

            [WpfFact]
            public void StringTrap_OnStartCharacter()
            {
                Create("fun(a, \" (\", b) # bar");
                var span = _motionUtil.GetBlock(BlockKind.Paren, _textBuffer.GetPoint(9));
                Assert.True(span.IsNone());
            }

            [WpfFact]
            public void StringTrap_AfterString()
            {
                Create("fun(a, \" (\", b) # bar");
                var span = GetBlockSpan(BlockKind.Paren, _textBuffer.GetPoint(14));
                Assert.Equal(_textBuffer.GetSpan(3, 12), span);
            }

            [WpfFact]
            public void BeforeBalancedString()
            {
                Create("fun(a, \"(foo)\", b) # bar");
                var span = GetBlockSpan(BlockKind.Paren, _textBuffer.GetPoint(8));
                Assert.Equal(_textBuffer.GetSpan(8, 5), span);
            }

            [WpfFact]
            public void InBalancedString()
            {
                Create("fun(a, \"(foo)\", b) # bar");
                var span = GetBlockSpan(BlockKind.Paren, _textBuffer.GetPoint(10));
                Assert.Equal(_textBuffer.GetSpan(8, 5), span);
            }

            [WpfFact]
            public void AfterBalancedString()
            {
                Create("fun(a, \"(foo)\", b) # bar");
                var span = GetBlockSpan(BlockKind.Paren, _textBuffer.GetPoint(13));
                Assert.Equal(_textBuffer.GetSpan(3, 15), span);
            }

            [WpfFact]
            public void InSplitString()
            {
                Create("fun(a, \" ( \", b, \" ) \", c) # bar");
                var span = GetBlockSpan(BlockKind.Paren, _textBuffer.GetPoint(10));
                Assert.Equal(_textBuffer.GetSpan(9, 11), span);
            }

            [WpfFact]
            public void StrayApostrophe()
            {
                // Reported in issue #2566.
                Create("if (done) { /* we're done */ Done(); }");
                var span = GetBlockSpan(BlockKind.CurlyBracket, _textBuffer.GetPoint(10));
                Assert.Equal(_textBuffer.GetSpan(10, 28), span);
            }
        }

        public sealed class AllBlockTest : MotionUtilTest
        {
            /// <summary>
            /// If there is not text after the { then that is simply excluded from the span.
            /// </summary>
            [WpfFact]
            public void SingleNoTextAfterOpenBrace()
            {
                Create("if (true)", "{", "  statement;", "}", "// after");

                var line = _textBuffer.GetLineFromLineNumber(2);
                var motionResult = _motionUtil.AllBlock(line.Start, BlockKind.CurlyBracket, count: 1).Value;
                var lineRange = _textBuffer.GetLineRange(startLine: 1, endLine: 3);
                Assert.Equal(lineRange.Extent, motionResult.Span);
                Assert.Equal(OperationKind.CharacterWise, motionResult.OperationKind);
            }

            [WpfFact]
            public void SingleTextBeforeOpenBrace()
            {
                // The key to this test is the text before the open brace
                Create("if (true)", "dog {", "  statement;", "}", "// after");

                var line = _textBuffer.GetLineFromLineNumber(2);
                var motionResult = _motionUtil.AllBlock(line.Start, BlockKind.CurlyBracket, count: 1).Value;
                var span = new SnapshotSpan(
                    _textBuffer.GetPointInLine(line: 1, column: 4),
                    _textBuffer.GetLine(3).End);
                Assert.Equal(span, motionResult.Span);
                Assert.Equal(OperationKind.CharacterWise, motionResult.OperationKind);
            }

            [WpfFact]
            public void SingleTextAfterCloseBrace()
            {
                // The key to this test is the text after the close brace
                Create("if (true)", "{", "  statement;", "} dog", "// after");

                var line = _textBuffer.GetLineFromLineNumber(2);
                var motionResult = _motionUtil.AllBlock(line.Start, BlockKind.CurlyBracket, count: 1).Value;
                var span = new SnapshotSpan(
                    _textBuffer.GetLine(1).Start,
                    _textBuffer.GetPointInLine(line: 3, column: 1));
                Assert.Equal(span, motionResult.Span);
                Assert.Equal(OperationKind.CharacterWise, motionResult.OperationKind);
            }

            [WpfFact]
            public void SingleLineWiseWithCount()
            {
                var text =
@"if (true)
{
  s1;
  if (false)
  {
    s2;
  }
}
more";
                Create(text.Split(new[] { Environment.NewLine }, StringSplitOptions.None));

                var line = _textBuffer.GetLineFromLineNumber(5);
                var motionResult = _motionUtil.AllBlock(line.Start, BlockKind.CurlyBracket, count: 2).Value;
                var lineRange = _textBuffer.GetLineRange(startLine: 1, endLine: 7);
                Assert.Equal(lineRange.Extent, motionResult.Span);
                Assert.Equal(OperationKind.CharacterWise, motionResult.OperationKind);
            }

            [WpfFact]
            public void ValidCount()
            {
                Create("a (cat (dog)) fish");

                var point = _textBuffer.GetPoint(8);
                Assert.Equal('d', point.GetChar());
                var motionResult = _motionUtil.AllBlock(point, BlockKind.Paren, count: 2).Value;

                Assert.Equal("(cat (dog))", motionResult.Span.GetText());
            }

            [WpfFact]
            public void InvalidCount()
            {
                Create("a (cat (dog)) fish");

                var point = _textBuffer.GetPoint(8);
                Assert.Equal('d', point.GetChar());
                var motionResult = _motionUtil.AllBlock(point, BlockKind.Paren, count: 3);
                Assert.True(motionResult.IsNone());
            }
        }

        public sealed class InnerBlockTest : MotionUtilTest
        {
            /// <summary>
            /// If there is not text after the { then that is simply excluded from the span.
            /// </summary>
            [WpfFact]
            public void SingleNoTextAfterOpenBrace()
            {
                Create("if (true)", "{", "  statement;", "}", "// after");

                var line = _textBuffer.GetLineFromLineNumber(2);
                var motionResult = _motionUtil.InnerBlock(line.Start, BlockKind.CurlyBracket, count: 1).Value;
                Assert.Equal(line.ExtentIncludingLineBreak, motionResult.Span);

                // Definitely a linewise paste operation.  This can be verified by simply pasting the
                // result here. 
                Assert.Equal(OperationKind.LineWise, motionResult.OperationKind);
            }

            [WpfFact]
            public void SingleNoTextAfterOpenBraceSpaceBeforeClose()
            {
                // The key to this test is the space before the close brace. 
                Create("if (true)", "{", "  statement;", "  }", "// after");

                var line = _textBuffer.GetLineFromLineNumber(2);
                var motionResult = _motionUtil.InnerBlock(line.Start, BlockKind.CurlyBracket, count: 1).Value;
                Assert.Equal(line.ExtentIncludingLineBreak, motionResult.Span);
                Assert.Equal(OperationKind.LineWise, motionResult.OperationKind);
            }

            [WpfFact]
            public void SingleSpaceAfterOpenBrace()
            {
                // The key to this test is the space after the { 
                Create("if (true)", "{ ", "  statement;", "}", "// after");

                var line = _textBuffer.GetLineFromLineNumber(2);
                var motionResult = _motionUtil.InnerBlock(line.Start, BlockKind.CurlyBracket, count: 1).Value;
                var span = new SnapshotSpan(
                    _textBuffer.GetPointInLine(line: 1, column: 1),
                    _textBuffer.GetLine(2).End);
                Assert.Equal(span, motionResult.Span);
                Assert.Equal(OperationKind.CharacterWise, motionResult.OperationKind);
            }

            [WpfFact]
            public void SingleTextAfterOpenBrace()
            {
                Create("if (true)", "{ // test", "  statement;", "}", "// after");

                var line = _textBuffer.GetLineFromLineNumber(2);
                var motionResult = _motionUtil.InnerBlock(line.Start, BlockKind.CurlyBracket, count: 1).Value;
                var span = new SnapshotSpan(
                    _textBuffer.GetPointInLine(line: 1, column: 1),
                    _textBuffer.GetLine(2).End);
                Assert.Equal(span, motionResult.Span);
                Assert.Equal(OperationKind.CharacterWise, motionResult.OperationKind);
            }

            [WpfFact]
            public void SingleTextBeforeCloseBrace()
            {
                Create("if (true)", "{", "  statement;", "dog }", "// after");

                var line = _textBuffer.GetLineFromLineNumber(2);
                var motionResult = _motionUtil.InnerBlock(line.Start, BlockKind.CurlyBracket, count: 1).Value;

                var span = new SnapshotSpan(
                    _textBuffer.GetLine(2).Start,
                    _textBuffer.GetPointInLine(line: 3, column: 4));
                Assert.Equal(span, motionResult.Span);
                Assert.Equal(OperationKind.CharacterWise, motionResult.OperationKind);
            }

            [WpfFact]
            public void SingleLineWiseWithCount()
            {
                var text =
@"if (true)
{
  s1;
  if (false)
  {
    s2;
  }
}";
                Create(text.Split(new[] { Environment.NewLine }, StringSplitOptions.None));

                var line = _textBuffer.GetLineFromLineNumber(5);
                var motionResult = _motionUtil.InnerBlock(line.Start, BlockKind.CurlyBracket, count: 2).Value;
                var lineRange = SnapshotLineRangeUtil.CreateForLineAndCount(
                    _textBuffer.GetLine(2),
                    count: 5).Value;
                Assert.Equal(lineRange.ExtentIncludingLineBreak, motionResult.Span);

                // Definitely a linewise paste operation.  This can be verified by simply pasting the
                // result here. 
                Assert.Equal(OperationKind.LineWise, motionResult.OperationKind);
            }

            [WpfFact]
            public void ValidCount()
            {
                Create("a (cat (dog)) fish");

                var point = _textBuffer.GetPoint(8);
                Assert.Equal('d', point.GetChar());
                var motionResult = _motionUtil.InnerBlock(point, BlockKind.Paren, count: 2).Value;

                Assert.Equal("cat (dog)", motionResult.Span.GetText());
            }

            [WpfFact]
            public void InvalidCount()
            {
                Create("a (cat (dog)) fish");

                var point = _textBuffer.GetPoint(8);
                Assert.Equal('d', point.GetChar());
                var motionResult = _motionUtil.InnerBlock(point, BlockKind.Paren, count: 3);
                Assert.True(motionResult.IsNone());
            }

            [WpfFact]
            public void CountWithSideBySideBlocks()
            {
                Create("a (cat (dog)(blah)) fish");

                var point = _textBuffer.GetPoint(8);
                Assert.Equal('d', point.GetChar());
                var motionResult = _motionUtil.InnerBlock(point, BlockKind.Paren, count: 2).Value;

                Assert.Equal("cat (dog)(blah)", motionResult.Span.GetText());
            }

            [WpfFact]
            public void CountWithSideBySideBlocksAlt()
            {
                Create("a (cat (dog)(blah)) fish");

                var point = _textBuffer.GetPoint(13);
                Assert.Equal('b', point.GetChar());
                var motionResult = _motionUtil.InnerBlock(point, BlockKind.Paren, count: 2).Value;

                Assert.Equal("cat (dog)(blah)", motionResult.Span.GetText());
            }

            [WpfFact]
            public void CountWithSideBySideBlocksHarder()
            {
                Create("a (cat (dog)(blah)(again(deep))) fish");

                var point = _textBuffer.GetPoint(8);
                Assert.Equal('d', point.GetChar());
                var motionResult = _motionUtil.InnerBlock(point, BlockKind.Paren, count: 2).Value;

                Assert.Equal("cat (dog)(blah)(again(deep))", motionResult.Span.GetText());
            }

            /// <summary>
            /// Single line inner block test should use inner block behavior
            /// </summary>
            [WpfFact]
            public void Simple()
            {
                Create("[cat]");
                var lines = _motionUtil.InnerBlock(_textBuffer.GetPoint(2), BlockKind.Bracket, 1).Value.Span.GetText();
                Assert.Equal("cat", lines);
            }

            /// <summary>
            /// Multiline inner block test should use inner block behavior
            /// </summary>
            [WpfFact]
            public void Lines()
            {
                Create("[", "cat", "]");
                var lines = _motionUtil.InnerBlock(_textBuffer.GetPointInLine(1, 1), BlockKind.Bracket, 1).Value.Span.GetText();
                Assert.Equal(_textBuffer.GetLine(1).ExtentIncludingLineBreak.GetText(), lines);
            }

            /// <summary>
            /// Lines with whitespace inner block test
            /// </summary>
            [WpfFact]
            public void LinesAndWhitespace()
            {
                Create("", "    [", "      cat", "     ] ", "");
                var lines = _motionUtil.InnerBlock(_textBuffer.GetPointInLine(2, 1), BlockKind.Bracket, 1).Value.Span.GetText();
                Assert.Equal(_textBuffer.GetLine(2).ExtentIncludingLineBreak.GetText(), lines);
            }

            /// <summary>
            /// Inner block with content on line with start bracket
            /// </summary>
            [WpfFact]
            public void ContentOnLineWithOpeningBracket()
            {
                Create("[ dog", "  cat", "  ] ");
                var lines = _motionUtil.InnerBlock(_textBuffer.GetPointInLine(1, 1), BlockKind.Bracket, 1).Value.Span.GetText();
                Assert.Equal(" dog" + Environment.NewLine + "  cat", lines);
            }

            /// <summary>
            /// Inner block with content on line with start bracket
            /// </summary>
            [WpfFact]
            public void ContentOnLineWithClosingBracket()
            {
                Create("[ ", "  cat", "  dog ] ");
                var lines = _motionUtil.InnerBlock(_textBuffer.GetPointInLine(1, 1), BlockKind.Bracket, 1).Value.Span.GetText();
                Assert.Equal(" " + Environment.NewLine + "  cat" + Environment.NewLine + "  dog ", lines);
            }

            [WpfFact]
            public void DisregardDoubleCommentedMatchType()
            {
                Create(@"OutlineFileNameRegex(DuplicateBackslash(L""^OutlineFileName:(.*\\.*\\\\).* $""));");
                var motion = _motionUtil.InnerBlock(_textBuffer.GetPoint(22), BlockKind.Paren, 1);
                Assert.Equal(@"DuplicateBackslash(L""^OutlineFileName:(.*\\.*\\\\).* $"")", motion.Value.Span.GetText());
            }

            /// <summary>
            /// If the entire block is linewise empty, perform no motion
            /// </summary>
            [WpfFact]
            public void LinewiseEmpty()
            {
                // Reported in issue #1969.
                Create("    Main(", "    );", "");
                _textView.MoveCaretToLine(1);
                var caretPoint = _textView.Caret.Position.BufferPosition;
                var motion = _motionUtil.InnerBlock(caretPoint, BlockKind.Paren, 1);
                Assert.Equal(new SnapshotSpan(caretPoint, 0), motion.Value.Span);
            }
        }

        public sealed class QuotedStringTest : MotionUtilTest
        {
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
            public void ItFavorsTrailingWhitespaceOverLeading()
            {
                Create(@"  ""foo""  ");

                var data = _motionUtil.QuotedString('"');

                Assert.True(data.IsSome());
                AssertData(data.Value, new SnapshotSpan(_snapshot, 2, 7), MotionKind.CharacterWiseInclusive);
                Assert.Equal(@"""foo""  ", data.Value.Span.GetText());
            }

            [WpfFact]
            public void WhenFavoringTrailingSpace_ItActuallyLooksAtTheFirstCharAfterTheEndQuote()
            {
                Create(@"  ""foo""X ");
                var start = _snapshot.GetText().IndexOf('f');
                _textView.MoveCaretTo(start);

                var data = _motionUtil.QuotedString('"');

                Assert.Equal(@"  ""foo""", data.Value.Span.GetText());
            }

            [WpfFact]
            public void ItFavorsTrailingWhitespaceOverLeading_WithOnlyOneTrailingSpace()
            {
                Create(@"  ""foo"" ");
                var start = _snapshot.GetText().IndexOf('f');
                _textView.MoveCaretTo(start);

                var data = _motionUtil.QuotedString('"');

                Assert.Equal(@"""foo"" ", data.Value.Span.GetText());
            }

            /// <summary>
            /// Ignore the escaped quotes
            /// </summary>
            [WpfFact]
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
            [WpfFact]
            public void ItIgnoresEscapedQuotes_AlternateEscape()
            {
                Create(@"""foo(""""");
                _localSettings.QuoteEscape = @"(";
                var data = _motionUtil.QuotedString('"');
                Assert.True(data.IsSome());
                AssertData(data.Value, new SnapshotSpan(_snapshot, 0, 7), MotionKind.CharacterWiseInclusive);
            }

            [WpfFact]
            public void NothingIsSelectedWhenThereAreNoQuotes()
            {
                Create(@"foo");
                var data = _motionUtil.QuotedString('"');
                Assert.True(data.IsNone());
            }

            [WpfFact]
            public void ItSelectsTheInsideOfTheSecondQuotedWord()
            {
                Create(@"""foo"" ""bar""");
                var start = _snapshot.GetText().IndexOf('b');
                _textView.MoveCaretTo(start);
                var data = _motionUtil.QuotedString('"');
                Assert.True(data.IsSome());
                AssertData(data.Value, new SnapshotSpan(_snapshot, start - 2, 6), MotionKind.CharacterWiseInclusive);
            }

            [WpfFact]
            public void SingleQuotesWork()
            {
                Create(@"""foo"" 'bar'");
                var start = _snapshot.GetText().IndexOf('b');
                _textView.MoveCaretTo(start);
                var data = _motionUtil.QuotedString('\'');
                Assert.True(data.IsSome());
                AssertData(data.Value, new SnapshotSpan(_snapshot, start - 2, 6), MotionKind.CharacterWiseInclusive);
            }

            [WpfFact]
            public void BackquotesWork()
            {
                Create(@"""foo"" `bar`");
                var start = _snapshot.GetText().IndexOf('b');
                _textView.MoveCaretTo(start);
                var data = _motionUtil.QuotedString('`');
                Assert.True(data.IsSome());
                AssertData(data.Value, new SnapshotSpan(_snapshot, start - 2, 6), MotionKind.CharacterWiseInclusive);
            }

            [WpfFact]
            public void UnmatchedQuotesFirst()
            {
                Create(@"x 'cat'dog'");
                _textView.MoveCaretTo(3);
                var data = _motionUtil.QuotedStringContentsWithCount('\'', 1);
                Assert.True(data.IsSome());
                Assert.Equal("cat", data.Value.Span.GetText());
            }

            [WpfFact]
            public void UnmatchedQuotesSecond()
            {
                Create(@"x 'cat'dog'");
                _textView.MoveCaretTo(8);
                var data = _motionUtil.QuotedStringContentsWithCount('\'', 1);
                Assert.True(data.IsSome());
                Assert.Equal("dog", data.Value.Span.GetText());
            }

            /// <summary>
            /// When landing directly on a quote that has a preceding quote it is always considered the 
            /// second quote in a string
            /// </summary>
            [WpfFact]
            public void UnmatchedQuotesMiddleQuote()
            {
                Create(@"x 'cat'dog'");
                _textView.MoveCaretTo(6);
                Assert.Equal('\'', _textView.GetCaretPoint().GetChar());
                var data = _motionUtil.QuotedStringContentsWithCount('\'', 1);
                Assert.True(data.IsSome());
                Assert.Equal("cat", data.Value.Span.GetText());
            }

            /// <summary>
            /// Border between valid string pairs
            /// </summary>
            [WpfFact]
            public void Border()
            {
                Create(@"x 'cat'dog'fish'");
                _textView.MoveCaretTo(10);
                Assert.Equal('\'', _textView.GetCaretPoint().GetChar());
                var data = _motionUtil.QuotedStringContentsWithCount('\'', 1);
                Assert.True(data.IsSome());
                Assert.Equal("fish", data.Value.Span.GetText());
            }

            [WpfFact]
            public void Issue1454()
            {
                Create(@"let x = '\\'");
                _textView.MoveCaretTo(9);
                var data = _motionUtil.QuotedStringContentsWithCount('\'', 1);
                Assert.True(data.IsSome());
                Assert.Equal(@"\\", data.Value.Span.GetText());
            }
        }

        public sealed class Word : MotionUtilTest
        {
            /// <summary>
            /// If the word motion crosses a new line and it's a moveement then we keep it. The 
            /// caret should move to the next line
            /// </summary>
            [WpfFact]
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
            [WpfFact]
            public void AcrossLineBreakOperator()
            {
                Create("cat  ", " dog");
                var motionResult = _motionUtil.WordForward(WordKind.NormalWord, 1, MotionContext.AfterOperator);
                Assert.Equal("cat  ", motionResult.Span.GetText());
            }

            /// <summary>
            /// Blank lines don't factor into an operator.  Make sure we back up over it completel
            /// </summary>
            [WpfFact]
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
            [WpfFact]
            public void AcrossEmptyLineOperator()
            {
                Create("dog", "cat", "", "  fish");
                _textView.MoveCaretToLine(1);
                var motionResult = _motionUtil.WordForward(WordKind.NormalWord, 2, MotionContext.AfterOperator);
                Assert.Equal("cat" + Environment.NewLine + Environment.NewLine, motionResult.Span.GetText());
            }

            [WpfFact]
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

            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
            public void Forward4()
            {
                Create("foo bar", "baz jaz");
                var res = _motionUtil.WordForward(WordKind.NormalWord, 3, MotionContext.Movement);
                Assert.Equal("foo bar" + Environment.NewLine + "baz ", res.Span.GetText());
            }

            /// <summary>
            /// Count off the end of the buffer
            /// </summary>
            [WpfFact]
            public void Forward5()
            {
                Create("foo bar");
                var res = _motionUtil.WordForward(WordKind.NormalWord, 10, MotionContext.Movement);
                Assert.Equal("foo bar", res.Span.GetText());
            }

            [WpfFact]
            public void ForwardBigWordIsAnyWord()
            {
                Create("foo bar");
                var res = _motionUtil.WordForward(WordKind.BigWord, 1, MotionContext.Movement);
                Assert.True(res.IsAnyWordMotion);
            }

            [WpfFact]
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
            [WpfFact]
            public void ForwardFromBlankLineEnd()
            {
                Create("cat", "   ", "dog");
                _textView.MoveCaretToLine(1, 2);
                var result = _motionUtil.WordForward(WordKind.NormalWord, 1, MotionContext.AfterOperator);
                Assert.Equal(" ", result.Span.GetText());
            }

            [WpfFact]
            public void ForwardFromBlankLineMiddle()
            {
                Create("cat", "   ", "dog");
                _textView.MoveCaretToLine(1, 1);
                var result = _motionUtil.WordForward(WordKind.NormalWord, 1, MotionContext.AfterOperator);
                Assert.Equal("  ", result.Span.GetText());
            }

            [WpfFact]
            public void ForwardFromDoubleBlankLineEnd()
            {
                Create("cat", "   ", "   ", "dog");
                _textView.MoveCaretToLine(1, 2);
                var result = _motionUtil.WordForward(WordKind.NormalWord, 1, MotionContext.AfterOperator);
                Assert.Equal(" ", result.Span.GetText());
            }
        }

        public sealed class VisibleWindow : MotionUtilTest
        {
            [WpfFact]
            public void LineFromTopOfVisibleWindow1()
            {
                Create("foo", "bar", "baz");
                _textView.SetVisibleLineRange(start: 0, length: 1);
                var data = _motionUtil.LineFromTopOfVisibleWindow(FSharpOption<int>.None).Value;
                Assert.Equal(_textBuffer.GetLineRange(0).ExtentIncludingLineBreak, data.Span);
                Assert.True(data.MotionKind.IsLineWise);
                Assert.True(data.IsForward);
            }

            [WpfFact]
            public void LineFromTopOfVisibleWindow2()
            {
                Create("foo", "bar", "baz", "jazz");
                _textView.SetVisibleLineRange(start: 0, length: 2);
                var data = _motionUtil.LineFromTopOfVisibleWindow(FSharpOption.Create(2)).Value;
                Assert.Equal(_textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
                Assert.True(data.MotionKind.IsLineWise);
                Assert.True(data.IsForward);
            }

            /// <summary>
            /// From visible line not caret point
            /// </summary>
            [WpfFact]
            public void LineFromTopOfVisibleWindow3()
            {
                Create("foo", "bar", "baz", "jazz");
                _textView.SetVisibleLineRange(start: 0, length: 2);
                _textView.MoveCaretTo(_textBuffer.GetLine(1).Start.Position);
                var data = _motionUtil.LineFromTopOfVisibleWindow(FSharpOption.Create(2)).Value;
                Assert.Equal(_textBuffer.GetLineRange(1, 1).ExtentIncludingLineBreak, data.Span);
                Assert.True(data.MotionKind.IsLineWise);
                Assert.True(data.IsForward);
            }

            [WpfFact]
            public void LineFromTopOfVisibleWindow4()
            {
                Create("  foo", "bar");
                _textView.SetVisibleLineRange(start: 0, length: 1);
                _textView.MoveCaretTo(_textBuffer.GetLine(1).End);
                var data = _motionUtil.LineFromTopOfVisibleWindow(FSharpOption<int>.None).Value;
                Assert.Equal(2, data.CaretColumn.AsInLastLine().ColumnNumber);
            }

            [WpfFact]
            public void LineFromTopOfVisibleWindow5()
            {
                Create("  foo", "bar");
                _textView.SetVisibleLineRange(start: 0, length: 1);
                _textView.MoveCaretTo(_textBuffer.GetLine(1).End);
                _globalSettings.StartOfLine = false;
                var data = _motionUtil.LineFromTopOfVisibleWindow(FSharpOption<int>.None).Value;
                Assert.Equal(3, data.CaretColumn.AsInLastLine().ColumnNumber);
            }

            [WpfFact]
            public void LineFromBottomOfVisibleWindow1()
            {
                Create("a", "b", "c", "d");
                _textView.SetVisibleLineRange(start: 0, length: 3);
                var data = _motionUtil.LineFromBottomOfVisibleWindow(FSharpOption<int>.None).Value;
                Assert.Equal(_textBuffer.GetLineRange(0, 2).ExtentIncludingLineBreak, data.Span);
                Assert.True(data.IsForward);
                Assert.Equal(OperationKind.LineWise, data.OperationKind);
            }

            [WpfFact]
            public void LineFromBottomOfVisibleWindow2()
            {
                Create("a", "b", "c", "d");
                _textView.SetVisibleLineRange(start: 0, length: 3);
                var data = _motionUtil.LineFromBottomOfVisibleWindow(FSharpOption.Create(2)).Value;
                Assert.Equal(_textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
                Assert.True(data.IsForward);
                Assert.Equal(OperationKind.LineWise, data.OperationKind);
            }

            [WpfFact]
            public void LineFromBottomOfVisibleWindow3()
            {
                Create("a", "b", "c", "d");
                _textView.SetVisibleLineRange(start: 0, length: 3);
                _textView.MoveCaretTo(_textBuffer.GetLine(2).End);
                var data = _motionUtil.LineFromBottomOfVisibleWindow(FSharpOption.Create(2)).Value;
                Assert.Equal(_textBuffer.GetLineRange(1, 2).ExtentIncludingLineBreak, data.Span);
                Assert.False(data.IsForward);
                Assert.Equal(OperationKind.LineWise, data.OperationKind);
            }

            [WpfFact]
            public void LineFromBottomOfVisibleWindow4()
            {
                Create("a", "b", "  c", "d");
                _textView.SetVisibleLineRange(start: 0, length: 3);
                var data = _motionUtil.LineFromBottomOfVisibleWindow(FSharpOption<int>.None).Value;
                Assert.Equal(2, data.CaretColumn.AsInLastLine().ColumnNumber);
            }

            [WpfFact]
            public void LineFromBottomOfVisibleWindow5()
            {
                Create("a", "b", "  c", "d");
                _textView.SetVisibleLineRange(start: 0, length: 2);
                _globalSettings.StartOfLine = false;
                var data = _motionUtil.LineFromBottomOfVisibleWindow(FSharpOption<int>.None).Value;
                Assert.Equal(0, data.CaretColumn.AsInLastLine().ColumnNumber);
            }

            [WpfFact]
            public void LineFromMiddleOfWindow1()
            {
                Create("a", "b", "c", "d");
                _textView.SetVisibleLineRange(start: 0, length: 2);
                var data = _motionUtil.LineInMiddleOfVisibleWindow();
                Assert.Equal(new SnapshotSpan(_textBuffer.GetPoint(0), _textBuffer.GetLine(1).EndIncludingLineBreak), data.Value.Span);
                Assert.Equal(OperationKind.LineWise, data.Value.OperationKind);
            }
        }

        public sealed class GoToLine : MotionUtilTest
        {
            [WpfFact]
            public void WithStartOfLine()
            {
                // Reported in issue #2224.
                Create("aaa xxx", "bbb yyy", "ccc zzz", "");
                _globalSettings.StartOfLine = true;
                _textView.MoveCaretToLine(2, 4);
                _motionUtil._commonOperations.MaintainCaretColumn = MaintainCaretColumn.NewSpaces(4);
                var data = _motionUtil.LineOrLastToFirstNonBlank(FSharpOption.Create(1));
                Assert.Equal(_textBuffer.GetLineRange(0, 2).ExtentIncludingLineBreak, data.Span);
                Assert.True(data.MotionKind.IsLineWise);
                Assert.True(!data.IsForward);
                Assert.Equal(0, data.CaretColumn.AsInLastLine().ColumnNumber);
                Assert.True(!data.MotionResultFlags.HasFlag(MotionResultFlags.MaintainCaretColumn));
            }

            [WpfFact]
            public void WithouthStartOfLine()
            {
                Create("aaa xxx", "bbb yyy", "ccc zzz", "");
                _globalSettings.StartOfLine = false;
                _textView.MoveCaretToLine(2, 4);
                _motionUtil._commonOperations.MaintainCaretColumn = MaintainCaretColumn.NewSpaces(4);
                var data = _motionUtil.LineOrLastToFirstNonBlank(FSharpOption.Create(1));
                Assert.Equal(_textBuffer.GetLineRange(0, 2).ExtentIncludingLineBreak, data.Span);
                Assert.True(data.MotionKind.IsLineWise);
                Assert.True(!data.IsForward);
                Assert.Equal(4, data.CaretColumn.AsInLastLine().ColumnNumber);
                Assert.True(data.MotionResultFlags.HasFlag(MotionResultFlags.MaintainCaretColumn));
            }
        }

        public sealed class MatchingTokenTest : MotionUtilTest
        {
            [WpfFact]
            public void SimpleParens()
            {
                Create("( )");
                var data = _motionUtil.MatchingToken().Value;
                Assert.Equal("( )", data.Span.GetText());
                Assert.True(data.IsForward);
                Assert.Equal(MotionKind.CharacterWiseInclusive, data.MotionKind);
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
            }

            [WpfFact]
            public void SimpleParensWithPrefix()
            {
                Create("cat( )");
                var data = _motionUtil.MatchingToken().Value;
                Assert.Equal("cat( )", data.Span.GetText());
                Assert.True(data.IsForward);
            }

            [WpfFact]
            public void TooManyOpenOnSameLine()
            {
                Create("cat(( )");
                Assert.True(_motionUtil.MatchingToken().IsNone());
            }

            [WpfFact]
            public void AcrossLines()
            {
                Create("cat(", ")");
                var span = new SnapshotSpan(
                    _textView.GetLine(0).Start,
                    _textView.GetLine(1).Start.Add(1));
                var data = _motionUtil.MatchingToken().Value;
                Assert.Equal(span, data.Span);
                Assert.True(data.IsForward);
            }

            [WpfFact]
            public void ParensFromEnd()
            {
                Create("cat( )");
                _textView.MoveCaretTo(5);
                var data = _motionUtil.MatchingToken().Value;
                Assert.Equal("( )", data.Span.GetText());
                Assert.False(data.IsForward);
            }

            [WpfFact]
            public void ParensFromMiddle()
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
            [WpfFact]
            public void ParensNestedFromEnd()
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
            [WpfFact]
            public void ParensConsecutiveSetsFromEnd()
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
            [WpfFact]
            public void ParensConsecutiveSetsFromEnd2()
            {
                Create("((a)) /* ((b))");
                _textView.MoveCaretTo(13);
                var data = _motionUtil.MatchingToken().Value;
                Assert.Equal("((b))", data.Span.GetText());
                Assert.False(data.IsForward);
            }

            [WpfFact]
            public void CommentStartDoesNotNest()
            {
                Create("/* /* */");
                var data = _motionUtil.MatchingToken().Value;
                Assert.Equal("/* /* */", data.Span.GetText());
                Assert.True(data.IsForward);
            }

            [WpfFact]
            public void IfElsePreProc()
            {
                Create("#if foo #endif", "again", "#endif");
                var data = _motionUtil.MatchingToken().Value;
                var span = new SnapshotSpan(_textView.GetPoint(0), _textView.GetLine(2).Start.Add(1));
                Assert.Equal(span, data.Span);
                Assert.Equal(MotionKind.CharacterWiseInclusive, data.MotionKind);
            }

            /// <summary>
            /// In the case the caret is on the end position of a line the search should actually start
            /// on the last valid column. Yet the returned span should not include the start token.
            /// </summary>
            [WpfFact]
            public void EndOfLine()
            {
                Create("{", "}  ");
                _textView.MoveCaretTo(_textBuffer.GetLine(0).End);
                var data = _motionUtil.MatchingToken().Value;
                var span = new SnapshotSpan(
                    _textBuffer.GetPointInLine(line: 0, column: 1),
                    _textBuffer.GetPointInLine(line: 1, column: 1));
                Assert.Equal(span, data.Span);
                Assert.Equal(MotionKind.CharacterWiseInclusive, data.MotionKind);
            }
        }

        public sealed class InnerParagraph : MotionUtilTest
        {
            [WpfFact]
            public void Empty()
            {
                Create("");
                var span = _motionUtil.InnerParagraph(1).Value.Span;
                Assert.Equal(_snapshot.GetLineRange(0).ExtentIncludingLineBreak, span);
            }

            [WpfFact]
            public void OneLiner()
            {
                Create("a");
                var span = _motionUtil.InnerParagraph(1).Value.Span;
                Assert.Equal(_snapshot.GetLineRange(0).ExtentIncludingLineBreak, span);
            }

            [WpfFact]
            public void SelectConsecutiveFilledLinesUntilEnd()
            {
                Create("a", "b", "c");
                var span = _motionUtil.InnerParagraph(1).Value.Span;
                Assert.Equal(_snapshot.GetLineRange(0, 2).ExtentIncludingLineBreak, span);
            }

            [WpfFact]
            public void SelectConsecutiveCount2FilledLinesUntilEndIsInvalid()
            {
                Create("a", "b", "c");
                Assert.True(_motionUtil.InnerParagraph(2).IsNone());
            }

            [WpfFact]
            public void SelectConsecutiveFilledLinesUntilBlank()
            {
                Create("a", "b", "");
                var span = _motionUtil.InnerParagraph(1).Value.Span;
                Assert.Equal(_snapshot.GetLineRange(0, 1).ExtentIncludingLineBreak, span);
            }

            [WpfFact]
            public void SelectConsecutiveFilledLinesUntilBlankFromMiddle()
            {
                Create("a", "b", "");
                _textView.MoveCaretToLine(1);
                var span = _motionUtil.InnerParagraph(1).Value.Span;
                Assert.Equal(_snapshot.GetLineRange(0, 1).ExtentIncludingLineBreak, span);
            }

            [WpfFact]
            public void StartingAfterFirstBlockSelectFilledLinesUntilEnd()
            {
                Create("a", "b", "", "c", "d", "e");
                _textView.MoveCaretToLine(4);
                var span = _motionUtil.InnerParagraph(1).Value.Span;
                Assert.Equal(_snapshot.GetLineRange(3, 5).ExtentIncludingLineBreak, span);
            }

            [WpfFact]
            public void StartingAfterFirstBlockSelectFilledLinesUntilBlankFromMiddle()
            {
                Create("a", "b", "", "c", "d", "e", " ");
                _textView.MoveCaretToLine(4);
                var span = _motionUtil.InnerParagraph(1).Value.Span;
                Assert.Equal(_snapshot.GetLineRange(3, 5).ExtentIncludingLineBreak, span);
            }

            [WpfFact]
            public void SelectConsecutiveFilledLinesUntilBlankOrWhitespace()
            {
                Create("a", " ", "");
                var span = _motionUtil.InnerParagraph(1).Value.Span;
                Assert.Equal(_snapshot.GetLineRange(0).ExtentIncludingLineBreak, span);
            }

            [WpfFact]
            public void SelectConsecutiveBlankLinesUntilFilled()
            {
                Create("", "", "a");
                var span = _motionUtil.InnerParagraph(1).Value.Span;
                Assert.Equal(_snapshot.GetLineRange(0, 1).ExtentIncludingLineBreak, span);
            }

            [WpfFact]
            public void SelectConsecutiveBlankLinesUntilFilledFromMiddle()
            {
                Create("", "", "a");
                _textView.MoveCaretToLine(1);
                var span = _motionUtil.InnerParagraph(1).Value.Span;
                Assert.Equal(_snapshot.GetLineRange(0, 1).ExtentIncludingLineBreak, span);
            }

            [WpfFact]
            public void SelectConsecutiveBlankLinesOrWhitespaceUntilFilled()
            {
                Create("", " ", "a");
                var span = _motionUtil.InnerParagraph(1).Value.Span;
                Assert.Equal(_snapshot.GetLineRange(0, 1).ExtentIncludingLineBreak, span);
            }

            [WpfFact]
            public void SelectConsecutiveBlankLinesWithWhitespaceOrTab()
            {
                Create("", "\t", " ");
                var span = _motionUtil.InnerParagraph(1).Value.Span;
                Assert.Equal(_snapshot.GetLineRange(0, 2).ExtentIncludingLineBreak, span);
            }

            [WpfFact]
            public void SelectMultiple()
            {
                Create("a", "b", "", "c");
                var span = _motionUtil.InnerParagraph(2).Value.Span;
                Assert.Equal(_snapshot.GetLineRange(0, 2).ExtentIncludingLineBreak, span);
            }

            [WpfFact]
            public void SelectMultipleStartingWithBlanks()
            {
                Create("", "", "a", "");
                var span = _motionUtil.InnerParagraph(2).Value.Span;
                Assert.Equal(_snapshot.GetLineRange(0, 2).ExtentIncludingLineBreak, span);
            }

            [WpfFact]
            public void CountTooHigh()
            {
                Create("a", "b", "", "c");
                Assert.True(_motionUtil.InnerParagraph(5).IsNone());
            }
        }

        /// <summary>
        /// Tests for the 'G' motion
        /// </summary>
        public sealed class LineOrLast : MotionUtilTest
        {
            [WpfFact]
            public void ToFirstNonBlank1()
            {
                Create("foo", "bar", "baz");
                var data = _motionUtil.LineOrLastToFirstNonBlank(FSharpOption.Create(2));
                Assert.Equal(_textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
                Assert.True(data.IsForward);
                Assert.True(data.MotionKind.IsLineWise);
                Assert.Equal(0, data.CaretColumn.AsInLastLine().ColumnNumber);
            }

            [WpfFact]
            public void ToFirstNonBlank2()
            {
                Create("foo", "bar", "baz");
                _textView.MoveCaretTo(_textBuffer.GetLine(1).Start);
                var data = _motionUtil.LineOrLastToFirstNonBlank(FSharpOption.Create(0));
                Assert.Equal(_textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
                Assert.False(data.IsForward);
                Assert.True(data.MotionKind.IsLineWise);
                Assert.Equal(0, data.CaretColumn.AsInLastLine().ColumnNumber);
            }

            [WpfFact]
            public void ToFirstNonBlank3()
            {
                Create("foo", "bar", "baz");
                _textView.MoveCaretTo(_textBuffer.GetLine(1).Start);
                var data = _motionUtil.LineOrLastToFirstNonBlank(FSharpOption.Create(500));
                Assert.Equal(_textBuffer.GetLineRange(1, 2).ExtentIncludingLineBreak, data.Span);
                Assert.True(data.IsForward);
                Assert.True(data.MotionKind.IsLineWise);
                Assert.Equal(0, data.CaretColumn.AsInLastLine().ColumnNumber);
            }

            [WpfFact]
            public void ToFirstNonBlank4()
            {
                Create("foo", "bar", "baz");
                var data = _motionUtil.LineOrLastToFirstNonBlank(FSharpOption<int>.None);
                var span = new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, _textBuffer.CurrentSnapshot.Length);
                Assert.Equal(span, data.Span);
                Assert.True(data.IsForward);
                Assert.True(data.MotionKind.IsLineWise);
                Assert.Equal(0, data.CaretColumn.AsInLastLine().ColumnNumber);
            }
        }

        public sealed class Misc : MotionUtilTest
        {
            [WpfFact]
            public void EndOfLine1()
            {
                Create("foo bar", "baz");
                var res = _motionUtil.EndOfLine(1);
                var span = res.Span;
                Assert.Equal("foo bar", span.GetText());
                Assert.Equal(MotionKind.CharacterWiseInclusive, res.MotionKind);
                Assert.Equal(OperationKind.CharacterWise, res.OperationKind);
            }

            [WpfFact]
            public void EndOfLine2()
            {
                Create("foo bar", "baz");
                _textView.MoveCaretTo(1);
                var res = _motionUtil.EndOfLine(1);
                Assert.Equal("oo bar", res.Span.GetText());
            }

            [WpfFact]
            public void EndOfLine3()
            {
                Create("foo", "bar", "baz");
                var res = _motionUtil.EndOfLine(2);
                Assert.Equal("foo" + Environment.NewLine + "bar", res.Span.GetText());
                Assert.Equal(MotionKind.CharacterWiseInclusive, res.MotionKind);
                Assert.Equal(OperationKind.CharacterWise, res.OperationKind);
            }

            [WpfFact]
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
            [WpfFact]
            public void EndOfLine5()
            {
                Create("foo");
                var res = _motionUtil.EndOfLine(300);
                Assert.Equal("foo", res.Span.GetText());
            }

            [WpfFact]
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

            [WpfFact]
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
            [WpfFact]
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

            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
            public void FirstNonBlankOnCurrentLine4()
            {
                Create(0, "   bar");
                var data = _motionUtil.FirstNonBlankOnCurrentLine();
                Assert.Equal(_textBuffer.GetSpan(0, 3), data.Span);
            }

            /// <summary>
            /// Empty line case
            /// </summary>
            [WpfFact]
            public void FirstNonBlankOnCurrentLine5()
            {
                Create(0, "");
                var data = _motionUtil.FirstNonBlankOnCurrentLine();
                Assert.Equal(_textBuffer.GetSpan(0, 0), data.Span);
            }

            /// <summary>
            /// Backwards case
            /// </summary>
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
            public void FirstNonBlankOnLine_Double()
            {
                Create(0, "cat", " dog");
                var data = _motionUtil.FirstNonBlankOnLine(2);
                Assert.Equal(_textView.GetLineRange(0, 1), data.LineRange);
                Assert.Equal(1, data.CaretColumn.AsInLastLine().ColumnNumber);
            }

            /// <summary>
            /// Make sure to include the trailing white space
            /// </summary>
            [WpfFact]
            public void AllWord_Simple()
            {
                Create("foo bar");
                var data = _motionUtil.AllWord(WordKind.NormalWord, 1, _textView.GetCaretPoint()).Value;
                Assert.Equal("foo ", data.Span.GetText());
            }

            /// <summary>
            /// Grab the entire word even if starting in the middle
            /// </summary>
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
            public void AllWord_DontGoIntoPreviousLineBreak()
            {
                Create("dog", "cat", "fish");
                _textView.MoveCaretToLine(1);
                var data = _motionUtil.GetMotion(Motion.NewAllWord(WordKind.NormalWord)).Value;
                Assert.Equal("cat", data.Span.GetText());
            }

            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
            public void CharLeft_CountTooHigh()
            {
                Create("dog", "cat");
                _textView.MoveCaretToLine(1, 1);
                var data = _motionUtil.CharLeft(300);
                Assert.Equal("c", data.Span.GetText());
            }

            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
            public void SpaceLeft_FromBlankLine()
            {
                Create("cat", "", "dog");
                _textView.MoveCaretTo(_textView.GetLine(1).Start);
                var data = _motionUtil.SpaceLeft(1);
                Assert.Equal("t\r\n", data.Span.GetText());
            }

            [WpfFact]
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
            [WpfFact]
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

            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
            public void EndOfWord6()
            {
                Create("foo bar baz");
                _textView.MoveCaretTo(2);
                var res = _motionUtil.EndOfWord(WordKind.NormalWord, 1);
                Assert.Equal("o bar", res.Span.GetText());
            }

            [WpfFact]
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
            [WpfFact]
            public void EndOfWord8()
            {
                Create("the dog goes around the house");
                _textView.MoveCaretTo(1);
                Assert.Equal('h', _textView.GetCaretPoint().GetChar());
                var res = _motionUtil.EndOfWord(WordKind.NormalWord, 1);
                Assert.Equal("he", res.Span.GetText());
            }

            [WpfFact]
            public void EndOfWord_DontStopOnPunctuation()
            {
                Create("A. the ball");
                _textView.MoveCaretTo(1);
                var data = _motionUtil.EndOfWord(WordKind.NormalWord, 1);
                Assert.Equal(". the", data.Span.GetText());
            }

            [WpfFact]
            public void EndOfWord_DoublePunctuation()
            {
                Create("A.. the ball");
                _textView.MoveCaretTo(1);
                var data = _motionUtil.EndOfWord(WordKind.NormalWord, 1);
                Assert.Equal("..", data.Span.GetText());
            }

            [WpfFact]
            public void EndOfWord_DoublePunctuationWithCount()
            {
                Create("A.. the ball");
                _textView.MoveCaretTo(1);
                var data = _motionUtil.EndOfWord(WordKind.NormalWord, 2);
                Assert.Equal(".. the", data.Span.GetText());
            }

            [WpfFact]
            public void EndOfWord_DoublePunctuationIsAWord()
            {
                Create("A.. the ball");
                _textView.MoveCaretTo(0);
                var data = _motionUtil.EndOfWord(WordKind.NormalWord, 1);
                Assert.Equal("A..", data.Span.GetText());
            }

            [WpfFact]
            public void EndOfWord_DontStopOnEndOfLine()
            {
                Create("A. ", "the ball");
                _textView.MoveCaretTo(1);
                var data = _motionUtil.EndOfWord(WordKind.NormalWord, 1);
                Assert.Equal(". " + Environment.NewLine + "the", data.Span.GetText());
            }

            [WpfFact]
            public void ForwardChar1()
            {
                Create("foo bar baz");
                Assert.Equal("fo", _motionUtil.CharSearch('o', 1, CharSearchKind.ToChar, SearchPath.Forward).Value.Span.GetText());
                _textView.MoveCaretTo(1);
                Assert.Equal("oo", _motionUtil.CharSearch('o', 1, CharSearchKind.ToChar, SearchPath.Forward).Value.Span.GetText());
                _textView.MoveCaretTo(1);
                Assert.Equal("oo b", _motionUtil.CharSearch('b', 1, CharSearchKind.ToChar, SearchPath.Forward).Value.Span.GetText());
            }

            [WpfFact]
            public void ForwardChar2()
            {
                Create("foo bar baz");
                var data = _motionUtil.CharSearch('q', 1, CharSearchKind.ToChar, SearchPath.Forward);
                Assert.True(data.IsNone());
            }

            [WpfFact]
            public void ForwardChar3()
            {
                Create("foo bar baz");
                var data = _motionUtil.CharSearch('o', 1, CharSearchKind.ToChar, SearchPath.Forward).Value;
                Assert.Equal(MotionKind.CharacterWiseInclusive, data.MotionKind);
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
            }

            /// <summary>
            /// Bad count gets nothing in gVim"
            /// </summary>
            [WpfFact]
            public void ForwardChar4()
            {
                Create("foo bar baz");
                var data = _motionUtil.CharSearch('o', 300, CharSearchKind.ToChar, SearchPath.Forward);
                Assert.True(data.IsNone());
            }

            /// <summary>
            /// A forward char search on an empty line shouldn't produce a result.  It's also a corner
            /// case prone to produce exceptions
            /// </summary>
            [WpfFact]
            public void ForwardChar_EmptyLine()
            {
                Create("cat", "", "dog");
                _textView.MoveCaretToLine(1);
                var data = _motionUtil.CharSearch('o', 1, CharSearchKind.ToChar, SearchPath.Forward);
                Assert.True(data.IsNone());
            }

            [WpfFact]
            public void ForwardTillChar1()
            {
                Create("foo bar baz");
                Assert.Equal("f", _motionUtil.CharSearch('o', 1, CharSearchKind.TillChar, SearchPath.Forward).Value.Span.GetText());
                Assert.Equal("foo ", _motionUtil.CharSearch('b', 1, CharSearchKind.TillChar, SearchPath.Forward).Value.Span.GetText());
            }

            [WpfFact]
            public void ForwardTillChar2()
            {
                Create("foo bar baz");
                Assert.True(_motionUtil.CharSearch('q', 1, CharSearchKind.TillChar, SearchPath.Forward).IsNone());
            }

            [WpfFact]
            public void ForwardTillChar3()
            {
                Create("foo bar baz");
                Assert.Equal("fo", _motionUtil.CharSearch('o', 2, CharSearchKind.TillChar, SearchPath.Forward).Value.Span.GetText());
            }

            /// <summary>
            /// Bad count gets nothing in gVim
            /// </summary>
            [WpfFact]
            public void ForwardTillChar4()
            {
                Create("foo bar baz");
                Assert.True(_motionUtil.CharSearch('o', 300, CharSearchKind.TillChar, SearchPath.Forward).IsNone());
            }

            [WpfFact]
            public void BackwardCharMotion1()
            {
                Create("the boy kicked the ball");
                _textView.MoveCaretTo(_textBuffer.GetLine(0).End);
                var data = _motionUtil.CharSearch('b', 1, CharSearchKind.ToChar, SearchPath.Backward).Value;
                Assert.Equal("ball", data.Span.GetText());
                Assert.Equal(MotionKind.CharacterWiseExclusive, data.MotionKind);
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
            }

            [WpfFact]
            public void BackwardCharMotion2()
            {
                Create("the boy kicked the ball");
                _textView.MoveCaretTo(_textBuffer.GetLine(0).End);
                var data = _motionUtil.CharSearch('b', 2, CharSearchKind.ToChar, SearchPath.Backward).Value;
                Assert.Equal("boy kicked the ball", data.Span.GetText());
                Assert.Equal(MotionKind.CharacterWiseExclusive, data.MotionKind);
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
            }

            /// <summary>
            /// Doing a backward char search on an empty line should produce no data
            /// </summary>
            [WpfFact]
            public void BackwardChar_OnEmptyLine()
            {
                Create("cat", "", "dog");
                _textView.MoveCaretToLine(1);
                var data = _motionUtil.CharSearch('b', 1, CharSearchKind.ToChar, SearchPath.Backward);
                Assert.True(data.IsNone());
            }

            [WpfFact]
            public void BackwardTillCharMotion1()
            {
                Create("the boy kicked the ball");
                _textView.MoveCaretTo(_textBuffer.GetLine(0).End);
                var data = _motionUtil.CharSearch('b', 1, CharSearchKind.TillChar, SearchPath.Backward).Value;
                Assert.Equal("all", data.Span.GetText());
                Assert.Equal(MotionKind.CharacterWiseExclusive, data.MotionKind);
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
            }

            [WpfFact]
            public void BackwardTillCharMotion2()
            {
                Create("the boy kicked the ball");
                _textView.MoveCaretTo(_textBuffer.GetLine(0).End);
                var data = _motionUtil.CharSearch('b', 2, CharSearchKind.TillChar, SearchPath.Backward).Value;
                Assert.Equal("oy kicked the ball", data.Span.GetText());
                Assert.Equal(MotionKind.CharacterWiseExclusive, data.MotionKind);
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
            }

            /// <summary>
            /// Inner word from the middle of a word
            /// </summary>
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
            public void InnerWord_FromLineBreakWthCount()
            {
                Create("cat", "fish dog");
                _textView.MoveCaretTo(_textView.GetLine(0).End);
                var data = _motionUtil.InnerWord(WordKind.NormalWord, 2, _textView.GetCaretPoint()).Value;
                Assert.Equal(Environment.NewLine + "fish", data.Span.GetText());
                Assert.True(data.IsForward);
            }

            [WpfFact]
            public void LineOrFirstToFirstNonBlank1()
            {
                Create("foo", "bar", "baz");
                _textView.MoveCaretTo(_textBuffer.GetLine(1).Start);
                var data = _motionUtil.LineOrFirstToFirstNonBlank(FSharpOption.Create(0));
                Assert.Equal(_textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
                Assert.False(data.IsForward);
                Assert.True(data.MotionKind.IsLineWise);
                Assert.Equal(0, data.CaretColumn.AsInLastLine().ColumnNumber);
            }

            [WpfFact]
            public void LineOrFirstToFirstNonBlank2()
            {
                Create("foo", "bar", "baz");
                var data = _motionUtil.LineOrFirstToFirstNonBlank(FSharpOption.Create(2));
                Assert.Equal(_textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
                Assert.True(data.IsForward);
                Assert.True(data.MotionKind.IsLineWise);
                Assert.Equal(0, data.CaretColumn.AsInLastLine().ColumnNumber);
            }

            [WpfFact]
            public void LineOrFirstToFirstNonBlank3()
            {
                Create("foo", "  bar", "baz");
                var data = _motionUtil.LineOrFirstToFirstNonBlank(FSharpOption.Create(2));
                Assert.Equal(_textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
                Assert.True(data.IsForward);
                Assert.True(data.MotionKind.IsLineWise);
                Assert.Equal(2, data.CaretColumn.AsInLastLine().ColumnNumber);
            }

            [WpfFact]
            public void LineOrFirstToFirstNonBlank4()
            {
                Create("foo", "  bar", "baz");
                _textView.MoveCaretTo(_textBuffer.GetLine(1).Start);
                var data = _motionUtil.LineOrFirstToFirstNonBlank(FSharpOption.Create(500));
                Assert.Equal(_textBuffer.GetLineRange(1, 2).ExtentIncludingLineBreak, data.Span);
                Assert.True(data.IsForward);
                Assert.True(data.MotionKind.IsLineWise);
                Assert.Equal(0, data.CaretColumn.AsInLastLine().ColumnNumber);
            }

            [WpfFact]
            public void LineOrFirstToFirstNonBlank5()
            {
                Create("  the", "dog", "jumped");
                _textView.MoveCaretTo(_textView.GetLine(1).Start);
                var data = _motionUtil.LineOrFirstToFirstNonBlank(FSharpOption<int>.None);
                Assert.Equal(0, data.Span.Start.Position);
                Assert.Equal(2, data.CaretColumn.AsInLastLine().ColumnNumber);
                Assert.False(data.IsForward);
            }

            [WpfFact]
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

            [WpfFact]
            public void LastNonBlankOnLine2()
            {
                Create("foo", "bar ", "jaz");
                var data = _motionUtil.LastNonBlankOnLine(2);
                Assert.Equal(new SnapshotSpan(_textBuffer.GetPoint(0), _textBuffer.GetLine(1).Start.Add(3)), data.Span);
                Assert.True(data.IsForward);
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
                Assert.Equal(MotionKind.CharacterWiseInclusive, data.MotionKind);
            }

            [WpfFact]
            public void LastNonBlankOnLine3()
            {
                Create("foo", "bar ", "jaz");
                var data = _motionUtil.LastNonBlankOnLine(300);
                Assert.Equal(new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, _textBuffer.CurrentSnapshot.Length), data.Span);
                Assert.True(data.IsForward);
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
                Assert.Equal(MotionKind.CharacterWiseInclusive, data.MotionKind);
            }

            [WpfFact]
            public void LastNonBlankOnLine3_WithFinalNewLine()
            {
                Create("foo", "bar ", "jaz", "");
                var data = _motionUtil.LastNonBlankOnLine(300);
                Assert.Equal(new SnapshotSpan(_textBuffer.GetPoint(0), _textBuffer.GetLine(2).Start.Add(3)), data.Span);
                Assert.True(data.IsForward);
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
                Assert.Equal(MotionKind.CharacterWiseInclusive, data.MotionKind);
            }

            [WpfFact]
            public void LineDownToFirstNonBlank1()
            {
                Create("a", "b", "c", "d");
                var data = _motionUtil.LineDownToFirstNonBlank(1);
                Assert.True(data.MotionKind.IsLineWise);
                Assert.Equal(_textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
                Assert.True(data.IsForward);
            }

            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
            public void LineDownToFirstNonBlank4()
            {
                Create("cat", "dog", "bird");
                _textView.MoveCaretTo(1);
                var data = _motionUtil.LineDownToFirstNonBlank(1);
                var span = _textView.GetLineRange(0, 1).ExtentIncludingLineBreak;
                Assert.Equal(span, data.Span);
            }

            [WpfFact]
            public void LineDownToFirstNonBlank5()
            {
                Create("cat", "  dog", "bird");
                _textView.MoveCaretTo(1);
                var data = _motionUtil.LineDownToFirstNonBlank(1);
                Assert.True(data.CaretColumn.IsInLastLine);
                Assert.Equal(2, data.CaretColumn.AsInLastLine().ColumnNumber);
            }

            [WpfFact]
            public void LineDownToFirstNonBlank6()
            {
                Create("cat", "  dog and again", "bird");
                _textView.MoveCaretTo(1);
                var data = _motionUtil.LineDownToFirstNonBlank(1);
                Assert.True(data.CaretColumn.IsInLastLine);
                Assert.Equal(2, data.CaretColumn.AsInLastLine().ColumnNumber);
            }

            [WpfFact]
            public void LineDownToFirstNonBlankg()
            {
                Create("cat", "  dog and again", " here bird again");
                _textView.MoveCaretTo(1);
                var data = _motionUtil.LineDownToFirstNonBlank(2);
                Assert.True(data.CaretColumn.IsInLastLine);
                Assert.Equal(1, data.CaretColumn.AsInLastLine().ColumnNumber);
            }

            /// <summary>
            /// Line down to a completely blank line should go to the end of
            /// the line
            /// </summary>
            [WpfFact]
            public void LineDownToCompletelyBlankLine()
            {
                Create("cat", "        ", "bird");
                var data = _motionUtil.LineDownToFirstNonBlank(1);
                Assert.True(data.CaretColumn.IsInLastLine);
                Assert.Equal(8, data.CaretColumn.AsInLastLine().ColumnNumber);
            }

            [WpfFact]
            public void LineDown1()
            {
                Create("dog", "cat", "bird");
                var data = _motionUtil.LineDown(1).Value;
                AssertData(
                    data,
                    _textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak,
                    motionKind: MotionKind.LineWise,
                    caretColumn: CaretColumn.NewInLastLine(0));
            }

            [WpfFact]
            public void LineDown2()
            {
                Create("dog", "cat", "bird");
                var data = _motionUtil.LineDown(2).Value;
                AssertData(
                    data,
                    _textBuffer.GetLineRange(0, 2).ExtentIncludingLineBreak,
                    motionKind: MotionKind.LineWise,
                    caretColumn: CaretColumn.NewInLastLine(0));
            }

            [WpfFact]
            public void LineUp1()
            {
                Create("dog", "cat", "bird", "horse");
                _textView.MoveCaretTo(_textView.GetLine(2).Start);
                var data = _motionUtil.LineUp(1).Value;
                AssertData(
                    data,
                    _textBuffer.GetLineRange(1, 2).ExtentIncludingLineBreak,
                    motionKind: MotionKind.LineWise,
                    caretColumn: CaretColumn.NewInLastLine(0));
            }

            [WpfFact]
            public void LineUp2()
            {
                Create("dog", "cat", "bird", "horse");
                _textView.MoveCaretTo(_textView.GetLine(2).Start);
                var data = _motionUtil.LineUp(2).Value;
                AssertData(
                    data,
                    _textBuffer.GetLineRange(0, 2).ExtentIncludingLineBreak,
                    motionKind: MotionKind.LineWise,
                    caretColumn: CaretColumn.NewInLastLine(0));
            }

            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
            public void SectionForward_BackwardsOnLastLine()
            {
                Create(0, "dog", "  cat");
                _textView.MoveCaretToLine(1, 4);
                var data = _motionUtil.SectionForward(MotionContext.Movement, 1);
                Assert.Equal("ca", data.Span.GetText());
                Assert.False(data.IsForward);
            }


            [WpfFact]
            public void SectionForward2()
            {
                Create(0, "dog", "\fpig", "fox");
                var data = _motionUtil.SectionForward(MotionContext.AfterOperator, 2);
                Assert.Equal(new SnapshotSpan(_snapshot, 0, _snapshot.Length), data.Span);
            }

            [WpfFact]
            public void SectionForward3()
            {
                Create(0, "dog", "{pig", "fox");
                var data = _motionUtil.SectionForward(MotionContext.AfterOperator, 2);
                Assert.Equal(new SnapshotSpan(_snapshot, 0, _snapshot.Length), data.Span);
            }

            [WpfFact]
            public void SectionForward4()
            {
                Create(0, "dog", "{pig", "{fox");
                var data = _motionUtil.SectionForward(MotionContext.Movement, 1);
                Assert.Equal(_textView.GetLineRange(0).ExtentIncludingLineBreak, data.Span);
            }

            [WpfFact]
            public void SectionForward5()
            {
                Create(0, "dog", "}pig", "fox");
                var data = _motionUtil.SectionForward(MotionContext.AfterOperator, 1);
                Assert.Equal(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
            }

            /// <summary>
            /// Only look for } after an operator
            /// </summary>
            [WpfFact]
            public void SectionForward6()
            {
                Create(0, "dog", "}pig", "fox");
                var data = _motionUtil.SectionForward(MotionContext.Movement, 1);
                Assert.Equal(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
            }

            [WpfFact]
            public void SectionBackwardOrOpenBrace1()
            {
                Create(0, "dog", "{brace", "pig", "}fox");
                var data = _motionUtil.SectionBackwardOrOpenBrace(1);
                Assert.True(data.Span.IsEmpty);
            }

            [WpfFact]
            public void SectionBackwardOrOpenBrace2()
            {
                Create("dog", "{brace", "pig", "}fox");
                _textView.MoveCaretTo(_textView.GetLine(2).Start.Position);
                var data = _motionUtil.SectionBackwardOrOpenBrace(1);
                Assert.Equal(_textView.GetLineRange(1).ExtentIncludingLineBreak, data.Span);
            }

            [WpfFact]
            public void SectionBackwardOrOpenBrace3()
            {
                Create("dog", "{brace", "pig", "}fox");
                _textView.MoveCaretTo(_textView.GetLine(2).Start.Position);
                var data = _motionUtil.SectionBackwardOrOpenBrace(2);
                Assert.Equal(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
            }

            [WpfFact]
            public void SectionBackwardOrOpenBrace4()
            {
                Create(0, "dog", "\fbrace", "pig", "}fox");
                var data = _motionUtil.SectionBackwardOrOpenBrace(1);
                Assert.True(data.Span.IsEmpty);
            }

            [WpfFact]
            public void SectionBackwardOrOpenBrace5()
            {
                Create("dog", "\fbrace", "pig", "}fox");
                _textView.MoveCaretTo(_textView.GetLine(2).Start.Position);
                var data = _motionUtil.SectionBackwardOrOpenBrace(1);
                Assert.Equal(_textView.GetLineRange(1).ExtentIncludingLineBreak, data.Span);
            }

            [WpfFact]
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
            [WpfFact]
            public void SectionBackwardOrOpenBrace7()
            {
                Create("dog", "\f{brace", "pig", "}fox");
                _textView.MoveCaretTo(_textView.GetLine(2).Start.Position);
                var data = _motionUtil.SectionBackwardOrOpenBrace(2);
                Assert.Equal(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
            }

            [WpfFact]
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

            [WpfFact]
            public void SectionBackwardOrCloseBrace1()
            {
                Create(0, "dog", "}brace", "pig", "}fox");
                var data = _motionUtil.SectionBackwardOrCloseBrace(1);
                Assert.True(data.Span.IsEmpty);
            }

            [WpfFact]
            public void SectionBackwardOrCloseBrace2()
            {
                Create("dog", "}brace", "pig", "}fox");
                _textView.MoveCaretTo(_textView.GetLine(2).Start.Position);
                var data = _motionUtil.SectionBackwardOrCloseBrace(1);
                Assert.Equal(_textView.GetLineRange(1).ExtentIncludingLineBreak, data.Span);
            }

            [WpfFact]
            public void SectionBackwardOrCloseBrace3()
            {
                Create("dog", "}brace", "pig", "}fox");
                _textView.MoveCaretTo(_textView.GetLine(2).Start.Position);
                var data = _motionUtil.SectionBackwardOrCloseBrace(2);
                Assert.Equal(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
            }

            [WpfFact]
            public void SectionBackwardOrCloseBrace4()
            {
                Create(0, "dog", "\fbrace", "pig", "}fox");
                var data = _motionUtil.SectionBackwardOrCloseBrace(1);
                Assert.True(data.Span.IsEmpty);
            }

            [WpfFact]
            public void SectionBackwardOrCloseBrace5()
            {
                Create("dog", "\fbrace", "pig", "}fox");
                _textView.MoveCaretTo(_textView.GetLine(2).Start.Position);
                var data = _motionUtil.SectionBackwardOrCloseBrace(1);
                Assert.Equal(_textView.GetLineRange(1).ExtentIncludingLineBreak, data.Span);
            }

            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
            public void ParagraphForward_LastChar()
            {
                Create("dog", "pig", "cat");
                _textView.MoveCaretTo(_textView.TextSnapshot.GetEndPoint().Subtract(1));
                var data = _motionUtil.ParagraphForward(1);
                Assert.Equal("t", data.Span.GetText());
            }

            [WpfFact]
            public void QuotedStringContents1()
            {
                Create(@"""foo""");
                var data = _motionUtil.QuotedStringContentsWithCount('"', 1);
                Assert.True(data.IsSome());
                AssertData(data.Value, new SnapshotSpan(_snapshot, 1, 3), MotionKind.CharacterWiseInclusive);
            }

            [WpfFact]
            public void QuotedStringContents2()
            {
                Create(@" ""bar""");
                var data = _motionUtil.QuotedStringContentsWithCount('"', 1);
                Assert.True(data.IsSome());
                AssertData(data.Value, new SnapshotSpan(_snapshot, 2, 3), MotionKind.CharacterWiseInclusive);
            }

            [WpfFact]
            public void QuotedStringContents3()
            {
                Create(@"""foo"" ""bar""");
                var start = _snapshot.GetText().IndexOf('b');
                _textView.MoveCaretTo(start);
                var data = _motionUtil.QuotedStringContentsWithCount('"', 1);
                Assert.True(data.IsSome());
                AssertData(data.Value, new SnapshotSpan(_snapshot, start, 3), MotionKind.CharacterWiseInclusive);
            }

            /// <summary>
            /// Ensure that the space after the sentence is included
            /// </summary>
            [WpfFact]
            public void SentencesForward_SpaceAfter()
            {
                Create("a! b");
                var data = _motionUtil.SentenceForward(1);
                AssertData(data, new SnapshotSpan(_snapshot, 0, 3));
            }

            /// <summary>
            /// At the end of the ITextBuffer there isn't a next sentence
            /// </summary>
            [WpfFact]
            public void SentencesForward_EndOfBuffer()
            {
                Create("a! b");
                _textView.MoveCaretTo(_snapshot.Length);
                var data = _motionUtil.SentenceForward(1);
                AssertData(data, new SnapshotSpan(_snapshot, _snapshot.Length, 0));
            }

            [WpfFact]
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
            [WpfFact]
            public void Mark_DoesNotExist()
            {
                Create("the dog chased the ball");
                Assert.True(_motionUtil.Mark(_localMarkA).IsNone());
            }

            /// <summary>
            /// Ensure that a backwards mark produces a backwards span
            /// </summary>
            [WpfFact]
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

            [WpfFact]
            public void MarkLine_DoesNotExist()
            {
                Create("the dog chased the ball");
                Assert.True(_motionUtil.MarkLine(_localMarkA).IsNone());
            }

            [WpfFact]
            public void MarkLine_Forward()
            {
                Create("cat", "dog", "pig", "tree");
                _vimTextBuffer.SetLocalMark(_localMarkA, 1, 1);
                var data = _motionUtil.MarkLine(_localMarkA).Value;
                Assert.Equal(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
                Assert.True(data.IsForward);
                Assert.True(data.MotionKind.IsLineWise);
            }

            [WpfFact]
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

            [WpfFact]
            public void LineUpToFirstNonBlank_UseColumnNotPosition()
            {
                Create("the", "  dog", "cat");
                _textView.MoveCaretToLine(2);
                var data = _motionUtil.LineUpToFirstNonBlank(1);
                Assert.Equal(2, data.CaretColumn.AsInLastLine().ColumnNumber);
                Assert.False(data.IsForward);
                Assert.Equal(_textView.GetLineRange(1, 2).ExtentIncludingLineBreak, data.Span);
            }

            /// <summary>
            /// Make sure find the full paragraph from a point in the middle.
            /// </summary>
            [WpfFact]
            public void AllParagraph_FromMiddle()
            {
                Create("a", "b", "", "c");
                _textView.MoveCaretToLine(1);
                var span = _motionUtil.AllParagraph(1).Value.Span;
                Assert.Equal(_snapshot.GetLineRange(0, 1).ExtentIncludingLineBreak, span);
            }

            /// <summary>
            /// Get a paragraph motion from the start of the ITextBuffer
            /// </summary>
            [WpfFact]
            public void AllParagraph_FromStart()
            {
                Create("a", "b", "", "c");
                var span = _motionUtil.AllParagraph(1).Value.Span;
                Assert.Equal(_snapshot.GetLineRange(0, 1).ExtentIncludingLineBreak, span);
            }

            /// <summary>
            /// A full paragraph should not include the preceding blanks when starting on
            /// an actual portion of the paragraph
            /// </summary>
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
            public void NextWord_NoWordUnderCaret()
            {
                Create("  ", "foo bar baz");
                _vimData.LastSearchData = VimUtil.CreateSearchData("cat");
                _statusUtil.Setup(x => x.OnError(Resources.NormalMode_NoWordUnderCursor)).Verifiable();
                _motionUtil.NextWord(SearchPath.Forward, 1);
                _statusUtil.Verify();
                Assert.Equal("cat", _vimData.LastSearchData.Pattern);
            }

            /// <summary>
            /// Simple match should update LastSearchData and return the appropriate span
            /// </summary>
            [WpfFact]
            public void NextWord_Simple()
            {
                Create("hello world", "hello");
                var result = _motionUtil.NextWord(SearchPath.Forward, 1).Value;
                Assert.Equal(_textView.GetLine(0).ExtentIncludingLineBreak, result.Span);
                Assert.Equal(@"\<hello\>", _vimData.LastSearchData.Pattern);
            }

            /// <summary>
            /// If the caret starts on a blank move to the first non-blank to find the 
            /// word.  This is true if we are searching forward or backward.  The original
            /// point though should be included in the search
            /// </summary>
            [WpfFact]
            public void NextWord_BackwardGoPastBlanks()
            {
                Create("dog   cat", "cat");
                _statusUtil.Setup(x => x.OnWarning(Resources.Common_SearchBackwardWrapped)).Verifiable();
                _textView.MoveCaretTo(4);
                var result = _motionUtil.NextWord(SearchPath.Backward, 1).Value;
                Assert.Equal("  cat" + Environment.NewLine, result.Span.GetText());
                _statusUtil.Verify();
            }

            /// <summary>
            /// Make sure that searching backward from the middle of a word starts at the 
            /// beginning of the word
            /// </summary>
            [WpfFact]
            public void NextWord_BackwardFromMiddleOfWord()
            {
                Create("cat cat cat");
                _textView.MoveCaretTo(5);
                var result = _motionUtil.NextWord(SearchPath.Backward, 1).Value;
                Assert.Equal("cat c", result.Span.GetText());
            }

            /// <summary>
            /// A non-word shouldn't require whole word
            /// </summary>
            [WpfFact]
            public void NextWord_Nonword()
            {
                Create("{", "dog", "{", "cat");
                var result = _motionUtil.NextWord(SearchPath.Forward, 1).Value;
                Assert.Equal(_textView.GetLine(2).Start, result.Span.End);
            }

            /// <summary>
            /// Make sure we pass the LastSearch value to the method and move the caret
            /// for the provided SearchResult
            /// </summary>
            [WpfFact]
            public void LastSearch_UsePattern()
            {
                Create("foo bar", "foo");
                var data = new SearchData("foo", SearchPath.Forward);
                _vimData.LastSearchData = data;
                var result = _motionUtil.LastSearch(false, 1).Value;
                Assert.Equal("foo bar" + Environment.NewLine, result.Span.GetText());
            }

            /// <summary>
            /// Make sure that this doesn't update the LastSearh field.  Only way to check this is 
            /// when we reverse the polarity of the search
            /// </summary>
            [WpfFact]
            public void LastSearch_DontUpdateLastSearch()
            {
                Create("dog cat", "dog", "dog");
                var data = new SearchData("dog", SearchPath.Forward);
                _vimData.LastSearchData = data;
                _statusUtil.Setup(x => x.OnWarning(Resources.Common_SearchBackwardWrapped)).Verifiable();
                _motionUtil.LastSearch(true, 1);
                Assert.Equal(data, _vimData.LastSearchData);
                _statusUtil.Verify();
            }

            /// <summary>
            /// Break on section macros
            /// </summary>
            [WpfFact]
            public void GetSections_WithMacroAndCloseSplit()
            {
                Create("dog.", ".HH", "cat.");
                _globalSettings.Sections = "HH";
                var ret = _motionUtil.GetSections(SectionKind.OnCloseBrace, SearchPath.Forward, _textBuffer.GetPoint(0));
                Assert.Equal(
                    new[] { "dog." + Environment.NewLine, ".HH" + Environment.NewLine + "cat." },
                    ret.Select(x => x.GetText()).ToList());
            }

            /// <summary>
            /// Break on section macros
            /// </summary>
            [WpfFact]
            public void GetSections_WithMacroBackwardAndCloseSplit()
            {
                Create("dog.", ".HH", "cat.");
                _globalSettings.Sections = "HH";
                var ret = _motionUtil.GetSections(SectionKind.OnCloseBrace, SearchPath.Backward, _textBuffer.GetEndPoint());
                Assert.Equal(
                    new[] { ".HH" + Environment.NewLine + "cat.", "dog." + Environment.NewLine },
                    ret.Select(x => x.GetText()).ToList());
            }

            /// <summary>
            /// Going forward we should include the brace line
            /// </summary>
            [WpfFact]
            public void GetSectionsWithSplit_FromBraceLine()
            {
                Create("dog.", "}", "cat");
                var ret = _motionUtil.GetSections(SectionKind.OnCloseBrace, SearchPath.Forward, _textBuffer.GetLine(1).Start);
                Assert.Equal(
                    new[] { "}" + Environment.NewLine + "cat" },
                    ret.Select(x => x.GetText()).ToList());

                ret = _motionUtil.GetSections(SectionKind.OnCloseBrace, SearchPath.Forward, _textBuffer.GetPoint(0));
                Assert.Equal(
                    new[] { "dog." + Environment.NewLine, "}" + Environment.NewLine + "cat" },
                    ret.Select(x => x.GetText()).ToList());
            }

            /// <summary>
            /// Going backward we should not include the brace line
            /// </summary>
            [WpfFact]
            public void GetSectionsWithSplit_FromBraceLineBackward()
            {
                Create("dog.", "}", "cat");
                var ret = _motionUtil.GetSections(SectionKind.OnCloseBrace, SearchPath.Backward, _textBuffer.GetLine(1).Start);
                Assert.Equal(
                    new[] { "dog." + Environment.NewLine },
                    ret.Select(x => x.GetText()).ToList());

                ret = _motionUtil.GetSections(SectionKind.OnCloseBrace, SearchPath.Backward, _textBuffer.GetEndPoint());
                Assert.Equal(
                    new[] { "}" + Environment.NewLine + "cat", "dog." + Environment.NewLine },
                    ret.Select(x => x.GetText()).ToList());
            }

            [WpfFact]
            public void GetSentences1()
            {
                Create("a. b.");
                var ret = _motionUtil.GetSentences(SentenceKind.Default, SearchPath.Forward, _snapshot.GetColumnFromPosition(0));
                Assert.Equal(
                    new[] { "a.", "b." },
                    ret.Select(x => x.GetText()).ToList());
            }

            [WpfFact]
            public void GetSentences2()
            {
                Create("a! b.");
                var ret = _motionUtil.GetSentences(SentenceKind.Default, SearchPath.Forward, _snapshot.GetColumnFromPosition(0));
                Assert.Equal(
                    new[] { "a!", "b." },
                    ret.Select(x => x.GetText()).ToList());
            }

            [WpfFact]
            public void GetSentences3()
            {
                Create("a? b.");
                var ret = _motionUtil.GetSentences(SentenceKind.Default, SearchPath.Forward, _snapshot.GetColumnFromPosition(0));
                Assert.Equal(
                    new[] { "a?", "b." },
                    ret.Select(x => x.GetText()).ToList());
            }

            [WpfFact]
            public void GetSentences4()
            {
                Create("a? b.");
                var ret = _motionUtil.GetSentences(SentenceKind.Default, SearchPath.Forward, _snapshot.GetEndColumn());
                Assert.Equal(
                    new string[] { },
                    ret.Select(x => x.GetText()).ToList());
            }

            /// <summary>
            /// Make sure the return doesn't include an empty span for the end point
            /// </summary>
            [WpfFact]
            public void GetSentences_BackwardFromEndOfBuffer()
            {
                Create("a? b.");
                var ret = _motionUtil.GetSentences(SentenceKind.Default, SearchPath.Backward, _snapshot.GetEndColumn());
                Assert.Equal(
                    new[] { "b.", "a?" },
                    ret.Select(x => x.GetText()).ToList());
            }

            /// <summary>
            /// Sentences are an exclusive motion and hence backward from a single whitespace 
            /// to a sentence boundary should not include the whitespace
            /// </summary>
            [WpfFact]
            public void GetSentences_BackwardFromSingleWhitespace()
            {
                Create("a? b.");
                var ret = _motionUtil.GetSentences(SentenceKind.Default, SearchPath.Backward, _snapshot.GetColumnFromPosition(2));
                Assert.Equal(
                    new[] { "a?" },
                    ret.Select(x => x.GetText()).ToList());
            }

            /// <summary>
            /// Make sure we include many legal trailing characters
            /// </summary>
            [WpfFact]
            public void GetSentences_ManyTrailingChars()
            {
                Create("a?)]' b.");
                var ret = _motionUtil.GetSentences(SentenceKind.Default, SearchPath.Forward, _snapshot.GetColumnFromPosition(0));
                Assert.Equal(
                    new[] { "a?)]'", "b." },
                    ret.Select(x => x.GetText()).ToList());
            }

            /// <summary>
            /// The character should go on the previous sentence
            /// </summary>
            [WpfFact]
            public void GetSentences_BackwardWithCharBetween()
            {
                Create("a?) b.");
                var ret = _motionUtil.GetSentences(SentenceKind.Default, SearchPath.Backward, _snapshot.GetEndColumn());
                Assert.Equal(
                    new[] { "b.", "a?)" },
                    ret.Select(x => x.GetText()).ToList());
            }

            /// <summary>
            /// Not a sentence unless the end character is followed by a space / tab / newline
            /// </summary>
            [WpfFact]
            public void GetSentences_NeedSpaceAfterEndCharacter()
            {
                Create("a!b. c");
                var ret = _motionUtil.GetSentences(SentenceKind.Default, SearchPath.Forward, _snapshot.GetColumnFromPosition(0));
                Assert.Equal(
                    new[] { "a!b.", "c" },
                    ret.Select(x => x.GetText()).ToList());
            }

            /// <summary>
            /// Only a valid boundary if the end character is followed by one of the 
            /// legal follow up characters (spaces, tabs, end of line after trailing chars)
            /// </summary>
            [WpfFact]
            public void GetSentences_IncompleteBoundary()
            {
                Create("a!b. c");
                var ret = _motionUtil.GetSentences(SentenceKind.Default, SearchPath.Backward, _snapshot.GetEndColumn());
                Assert.Equal(
                    new[] { "c", "a!b." },
                    ret.Select(x => x.GetText()).ToList());
            }

            /// <summary>
            /// Make sure blank lines are included as sentence boundaries.  Note: Only the first blank line in a series
            /// of lines is actually a sentence.  Every following blank is just white space in between the blank line
            /// sentence and the start of the next sentence
            /// </summary>
            [WpfFact]
            public void GetSentences_ForwardBlankLinesAreBoundaries()
            {
                Create("a", "", "", "b");
                var ret = _motionUtil.GetSentences(SentenceKind.Default, SearchPath.Forward, _snapshot.GetColumnFromPosition(0));
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
            [WpfFact]
            public void GetSentences_FromMiddleOfWord()
            {
                Create("dog", "cat", "bear");
                var ret = _motionUtil.GetSentences(SentenceKind.Default, SearchPath.Forward, _snapshot.GetEndColumn().Subtract(1));
                Assert.Equal(
                    new[] { "dog" + Environment.NewLine + "cat" + Environment.NewLine + "bear" },
                    ret.Select(x => x.GetText()).ToList());
            }

            /// <summary>
            /// Don't include a sentence if we go backward from the first character of
            /// the sentence
            /// </summary>
            [WpfFact]
            public void GetSentences_BackwardFromStartOfSentence()
            {
                Create("dog. cat");
                var ret = _motionUtil.GetSentences(SentenceKind.Default, SearchPath.Backward, _textBuffer.GetColumnFromPosition(4));
                Assert.Equal(
                    new[] { "dog." },
                    ret.Select(x => x.GetText()).ToList());
            }

            /// <summary>
            /// A blank line is a sentence
            /// </summary>
            [WpfFact]
            public void GetSentences_BlankLinesAreSentences()
            {
                Create("dog.  ", "", "cat.");
                var ret = _motionUtil.GetSentences(SentenceKind.Default, SearchPath.Forward, _textBuffer.GetColumnFromPosition(0));
                Assert.Equal(
                    new[] { "dog.", Environment.NewLine, "cat." },
                    ret.Select(x => x.GetText()).ToList());
            }

            [WpfFact]
            public void GetParagraphs_SingleBreak()
            {
                Create("a", "b", "", "c");
                var ret = _motionUtil.GetParagraphs(SearchPath.Forward, _snapshot.GetColumnFromPosition(0));
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
            [WpfFact]
            public void GetParagraphs_ConsequtiveBreaks()
            {
                Create("a", "b", "", "", "c");
                var ret = _motionUtil.GetParagraphs(SearchPath.Forward, _snapshot.GetColumnFromPosition(0));
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
            [WpfFact]
            public void GetParagraphs_FormFeedShouldBeBoundary()
            {
                Create("a", "b", "\f", "", "c");
                var ret = _motionUtil.GetParagraphs(SearchPath.Forward, _snapshot.GetColumnFromPosition(0));
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
            [WpfFact]
            public void GetParagraphs_FormFeedIsNotConsequtive()
            {
                Create("a", "b", "\f", "", "c");
                var ret = _motionUtil.GetParagraphs(SearchPath.Forward, _snapshot.GetColumnFromPosition(0));
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
            [WpfFact]
            public void GetParagraphs_MacroBreak()
            {
                Create("a", ".hh", "bear");
                _globalSettings.Paragraphs = "hh";
                var ret = _motionUtil.GetParagraphs(SearchPath.Forward, _snapshot.GetColumnFromPosition(0));
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
            [WpfFact]
            public void GetParagraphs_MacroBreakLengthOne()
            {
                Create("a", ".j", "bear");
                _globalSettings.Paragraphs = "hhj ";
                var ret = _motionUtil.GetParagraphs(SearchPath.Forward, _snapshot.GetColumnFromPosition(0));
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
            [WpfFact]
            public void Bar_Simple()
            {
                Create("The quick brown fox jumps over the lazy dog.");
                _textView.MoveCaretTo(0);
                var data = _motionUtil.LineToColumn(4);
                Assert.Equal("The", data.Span.GetText());
                Assert.True(data.IsForward);
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
                Assert.Equal(MotionKind.CharacterWiseExclusive, data.MotionKind);
                Assert.Equal(CaretColumn.NewScreenColumn(3), data.CaretColumn);
            }

            /// <summary>
            /// Make sure that the bar motion handles backward moves properly
            /// </summary>
            [WpfFact]
            public void Bar_Backward()
            {
                Create("The quick brown fox jumps over the lazy dog.");
                _textView.MoveCaretTo(3);
                var data = _motionUtil.LineToColumn(1);
                Assert.Equal("The", data.Span.GetText());
                Assert.False(data.IsForward);
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
                Assert.Equal(MotionKind.CharacterWiseExclusive, data.MotionKind);
                Assert.Equal(CaretColumn.NewScreenColumn(0), data.CaretColumn);
            }

            /// <summary>
            /// Make sure that the bar motion can handle ending up in the same column
            /// </summary>
            [WpfFact]
            public void Bar_NoMove()
            {
                Create("The quick brown fox jumps over the lazy dog.");
                _textView.MoveCaretTo(2);
                var data = _motionUtil.LineToColumn(3);
                Assert.Equal(0, data.Span.Length);
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
                Assert.Equal(MotionKind.CharacterWiseExclusive, data.MotionKind);
                Assert.Equal(CaretColumn.NewScreenColumn(2), data.CaretColumn);
            }

            /// <summary>
            /// Make sure that the bar motion goes to the tab that spans over the given column
            /// </summary>
            [WpfFact]
            public void Bar_OverTabs()
            {
                Create("\t\t\tThe quick brown fox jumps over the lazy dog.");
                _textView.MoveCaretTo(3);
                _localSettings.TabStop = 4;

                var data = _motionUtil.LineToColumn(2);

                // Tabs are 4 spaces long; we should end up in the first tab
                Assert.Equal("\t\t\t", data.Span.GetText());
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
                Assert.Equal(MotionKind.CharacterWiseExclusive, data.MotionKind);
                Assert.Equal(CaretColumn.NewScreenColumn(1), data.CaretColumn);
            }

            /// <summary>
            /// Make sure that the bar motion knows where it wanted to end up, even past the end of line.
            /// </summary>
            [WpfFact]
            public void Bar_PastEnd()
            {
                Create("Teh");
                _textView.MoveCaretTo(1);
                var data = _motionUtil.LineToColumn(100);

                Assert.Equal(data.Span.End, _textView.GetLine(0).End);
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
                Assert.Equal(MotionKind.CharacterWiseExclusive, data.MotionKind);
                Assert.Equal(CaretColumn.NewScreenColumn(99), data.CaretColumn);
            }
        }

        public sealed class VirtualEdit : MotionUtilTest
        {
            protected override void Create(params string[] lines)
            {
                base.Create(lines);
                _globalSettings.VirtualEdit = "all";
            }

            [WpfFact]
            public void CharRightAtStartOfLine()
            {
                Create("foo", "");
                var data = _motionUtil.CharRight(1);
                Assert.Equal("f", data.Span.GetText());
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
                Assert.Equal(MotionKind.CharacterWiseExclusive, data.MotionKind);
                Assert.Equal(CaretColumn.None, data.CaretColumn);
            }

            [WpfFact]
            public void CharRightAtDollar()
            {
                Create("foo", "");
                _textView.MoveCaretTo(2);
                var data = _motionUtil.CharRight(1);
                Assert.Equal("o", data.Span.GetText());
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
                Assert.Equal(MotionKind.CharacterWiseExclusive, data.MotionKind);
                Assert.Equal(CaretColumn.None, data.CaretColumn);
            }

            [WpfFact]
            public void CharRightAtEndOfLine()
            {
                Create("foo", "");
                _textView.MoveCaretTo(3);
                var data = _motionUtil.CharRight(1);
                Assert.Equal("", data.Span.GetText());
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
                Assert.Equal(MotionKind.CharacterWiseExclusive, data.MotionKind);
                Assert.Equal(CaretColumn.NewInLastLine(4), data.CaretColumn);
            }

            [WpfFact]
            public void CharRightPastEndOfLine()
            {
                Create("foo", "");
                _textView.MoveCaretTo(3, virtualSpaces: 1);
                var data = _motionUtil.CharRight(1);
                Assert.Equal("", data.Span.GetText());
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
                Assert.Equal(MotionKind.CharacterWiseExclusive, data.MotionKind);
                Assert.Equal(CaretColumn.NewInLastLine(5), data.CaretColumn);
            }

            [WpfFact]
            public void CharLeftPastEndOfLine()
            {
                Create("foo", "");
                _textView.MoveCaretTo(3, virtualSpaces: 2);
                var data = _motionUtil.CharLeft(1);
                Assert.Equal("", data.Span.GetText());
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
                Assert.Equal(MotionKind.CharacterWiseExclusive, data.MotionKind);
                Assert.Equal(CaretColumn.NewInLastLine(4), data.CaretColumn);
            }

            [WpfFact]
            public void CharLeftToEndOfLine()
            {
                Create("foo", "");
                _textView.MoveCaretTo(3, virtualSpaces: 1);
                var data = _motionUtil.CharLeft(1);
                Assert.Equal("", data.Span.GetText());
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
                Assert.Equal(MotionKind.CharacterWiseExclusive, data.MotionKind);
                Assert.Equal(CaretColumn.None, data.CaretColumn);
            }

            [WpfFact]
            public void CharLeftToDollar()
            {
                Create("foo", "");
                _textView.MoveCaretTo(3);
                var data = _motionUtil.CharLeft(1);
                Assert.Equal("o", data.Span.GetText());
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
                Assert.Equal(MotionKind.CharacterWiseExclusive, data.MotionKind);
                Assert.Equal(CaretColumn.None, data.CaretColumn);
            }

            [WpfFact]
            public void CharLeftToStartOfLine()
            {
                Create("foo", "");
                _textView.MoveCaretTo(1);
                var data = _motionUtil.CharLeft(1);
                Assert.Equal("f", data.Span.GetText());
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
                Assert.Equal(MotionKind.CharacterWiseExclusive, data.MotionKind);
                Assert.Equal(CaretColumn.None, data.CaretColumn);
            }

            [WpfFact]
            public void WrapCharRightAtStartOfLine()
            {
                Create("foo", "");
                _globalSettings.WhichWrap = "h,l";
                var data = _motionUtil.CharRight(1);
                Assert.Equal("f", data.Span.GetText());
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
                Assert.Equal(MotionKind.CharacterWiseExclusive, data.MotionKind);
                Assert.Equal(CaretColumn.None, data.CaretColumn);
            }

            [WpfFact]
            public void WrapCharRightAtDollar()
            {
                Create("foo", "");
                _globalSettings.WhichWrap = "h,l";
                _textView.MoveCaretTo(2);
                var data = _motionUtil.CharRight(1);
                Assert.Equal("o", data.Span.GetText());
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
                Assert.Equal(MotionKind.CharacterWiseExclusive, data.MotionKind);
                Assert.Equal(CaretColumn.None, data.CaretColumn);
            }

            [WpfFact]
            public void WrapCharRightAtEndOfLine()
            {
                Create("foo", "");
                _globalSettings.WhichWrap = "h,l";
                _textView.MoveCaretTo(3);
                var data = _motionUtil.CharRight(1);
                Assert.Equal("", data.Span.GetText());
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
                Assert.Equal(MotionKind.CharacterWiseExclusive, data.MotionKind);
                Assert.Equal(CaretColumn.NewInLastLine(4), data.CaretColumn);
            }

            [WpfFact]
            public void WrapCharRightPastEndOfLine()
            {
                Create("foo", "");
                _globalSettings.WhichWrap = "h,l";
                _textView.MoveCaretTo(3, virtualSpaces: 1);
                var data = _motionUtil.CharRight(1);
                Assert.Equal("", data.Span.GetText());
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
                Assert.Equal(MotionKind.CharacterWiseExclusive, data.MotionKind);
                Assert.Equal(CaretColumn.NewInLastLine(5), data.CaretColumn);
            }

            [WpfFact]
            public void WrapCharLeftPastEndOfLine()
            {
                Create("foo", "");
                _globalSettings.WhichWrap = "h,l";
                _textView.MoveCaretTo(3, virtualSpaces: 2);
                var data = _motionUtil.CharLeft(1);
                Assert.Equal("", data.Span.GetText());
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
                Assert.Equal(MotionKind.CharacterWiseExclusive, data.MotionKind);
                Assert.Equal(CaretColumn.NewInLastLine(4), data.CaretColumn);
            }

            [WpfFact]
            public void WrapCharLeftToEndOfLine()
            {
                Create("foo", "");
                _globalSettings.WhichWrap = "h,l";
                _textView.MoveCaretTo(3, virtualSpaces: 1);
                var data = _motionUtil.CharLeft(1);
                Assert.Equal("", data.Span.GetText());
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
                Assert.Equal(MotionKind.CharacterWiseExclusive, data.MotionKind);
                Assert.Equal(CaretColumn.None, data.CaretColumn);
            }

            [WpfFact]
            public void WrapCharLeftToDollar()
            {
                Create("foo", "");
                _globalSettings.WhichWrap = "h,l";
                _textView.MoveCaretTo(3);
                var data = _motionUtil.CharLeft(1);
                Assert.Equal("o", data.Span.GetText());
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
                Assert.Equal(MotionKind.CharacterWiseExclusive, data.MotionKind);
                Assert.Equal(CaretColumn.None, data.CaretColumn);
            }

            [WpfFact]
            public void WrapCharLeftToStartOfLine()
            {
                Create("foo", "");
                _globalSettings.WhichWrap = "h,l";
                _textView.MoveCaretTo(1);
                var data = _motionUtil.CharLeft(1);
                Assert.Equal("f", data.Span.GetText());
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
                Assert.Equal(MotionKind.CharacterWiseExclusive, data.MotionKind);
                Assert.Equal(CaretColumn.None, data.CaretColumn);
            }

            [WpfFact]
            public void LineDownRealToReal()
            {
                Create("foo bar", "baz qux", "");
                _textView.MoveCaretTo(4);
                var data = _motionUtil.LineDown(1);
                AssertData(
                    data,
                    _textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak,
                    motionKind: MotionKind.LineWise,
                    caretColumn: CaretColumn.NewInLastLine(4));
            }

            [WpfFact]
            public void LineDownRealToVirtual()
            {
                Create("foo bar", "baz", "");
                _textView.MoveCaretTo(4);
                var data = _motionUtil.LineDown(1);
                AssertData(
                    data,
                    _textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak,
                    motionKind: MotionKind.LineWise,
                    caretColumn: CaretColumn.NewInLastLine(4));
            }

            [WpfFact]
            public void LineDownVirtualToReal()
            {
                Create("foo", "bar baz", "");
                _textView.MoveCaretTo(3, virtualSpaces: 1);
                var data = _motionUtil.LineDown(1);
                AssertData(
                    data,
                    _textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak,
                    motionKind: MotionKind.LineWise,
                    caretColumn: CaretColumn.NewInLastLine(4));
            }

            [WpfFact]
            public void LineDownVirtualToVirtual()
            {
                Create("foo", "bar", "");
                _textView.MoveCaretTo(3, virtualSpaces: 1);
                var data = _motionUtil.LineDown(1);
                AssertData(
                    data,
                    _textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak,
                    motionKind: MotionKind.LineWise,
                    caretColumn: CaretColumn.NewInLastLine(4));
            }

            [WpfFact]
            public void LineDownVirtualToVirtualWithTab()
            {
                Create("\tx", "bar", "");
                _localSettings.TabStop = 4;
                _textView.MoveCaretTo(2, virtualSpaces: 1);
                var data = _motionUtil.LineDown(1);
                AssertData(
                    data,
                    _textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak,
                    motionKind: MotionKind.LineWise,
                    caretColumn: CaretColumn.NewInLastLine(3));
            }

            [WpfFact]
            public void LineUpRealToReal()
            {
                Create("foo bar", "baz qux", "");
                _textView.MoveCaretToLine(1, 4);
                var data = _motionUtil.LineUp(1);
                AssertData(
                    data,
                    _textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak,
                    motionKind: MotionKind.LineWise,
                    caretColumn: CaretColumn.NewInLastLine(4));
            }

            [WpfFact]
            public void LineUpRealToVirtual()
            {
                Create("foo", "bar baz", "");
                _textView.MoveCaretToLine(1, 4);
                var data = _motionUtil.LineUp(1);
                AssertData(
                    data,
                    _textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak,
                    motionKind: MotionKind.LineWise,
                    caretColumn: CaretColumn.NewInLastLine(4));
            }

            [WpfFact]
            public void LineUpVirtualToReal()
            {
                Create("foo bar", "baz", "");
                _textView.MoveCaretToLine(1, 3, virtualSpaces: 1);
                var data = _motionUtil.LineUp(1);
                AssertData(
                    data,
                    _textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak,
                    motionKind: MotionKind.LineWise,
                    caretColumn: CaretColumn.NewInLastLine(4));
            }

            [WpfFact]
            public void LineUpVirtualToRealWithTab()
            {
                Create("foo", "\tx", "");
                _textView.MoveCaretToLine(1, 2, virtualSpaces: 1);
                var data = _motionUtil.LineUp(1);
                AssertData(
                    data,
                    _textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak,
                    motionKind: MotionKind.LineWise,
                    caretColumn: CaretColumn.NewInLastLine(3));
            }

            [WpfFact]
            public void LineUpVirtualToVirtual()
            {
                Create("foo", "bar", "");
                _textView.MoveCaretToLine(1, 3, virtualSpaces: 1);
                var data = _motionUtil.LineUp(1);
                AssertData(
                    data,
                    _textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak,
                    motionKind: MotionKind.LineWise,
                    caretColumn: CaretColumn.NewInLastLine(4));
            }
        }
    }
}
