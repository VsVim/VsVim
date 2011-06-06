using System;
using System.Linq;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.UnitTest;
using Vim.UnitTest.Mock;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class MotionUtilTest
    {
        private ITextBuffer _buffer;
        private ITextBuffer _textBuffer;
        private ITextView _textView;
        private ITextSnapshot _snapshot;
        private IVimLocalSettings _localSettings;
        private IVimGlobalSettings _globalSettings;
        private MotionUtil _motionUtil;
        private ISearchService _search;
        private ITextStructureNavigator _navigator;
        private IVimData _vimData;
        private IMarkMap _markMap;
        private IJumpList _jumpList;
        private Mock<IStatusUtil> _statusUtil;

        [TearDown]
        public void TearDown()
        {
            _buffer = null;
        }

        private void Create(params string[] lines)
        {
            var textView = EditorUtil.CreateView(lines);
            Create(textView, EditorUtil.GetOptions(textView));
        }

        private void Create(int caretPosition, params string[] lines)
        {
            Create(lines);
            _textView.MoveCaretTo(caretPosition);
        }

        private void Create(ITextView textView, IEditorOptions editorOptions = null)
        {
            _textView = textView;
            _textBuffer = textView.TextBuffer;
            _buffer = _textView.TextBuffer;
            _snapshot = _buffer.CurrentSnapshot;
            _buffer.Changed += delegate { _snapshot = _buffer.CurrentSnapshot; };
            _globalSettings = new Vim.GlobalSettings();
            _localSettings = new LocalSettings(_globalSettings, FSharpOption.CreateForReference(editorOptions), FSharpOption.CreateForReference(textView));
            _markMap = new MarkMap(new TrackingLineColumnService());
            _vimData = new VimData();
            _search = VimUtil.CreateSearchService(_globalSettings);
            _jumpList = VimUtil.CreateJumpList();
            _statusUtil = new Mock<IStatusUtil>(MockBehavior.Strict);
            _navigator = VimUtil.CreateTextStructureNavigator(_textView.TextBuffer);
            _motionUtil = new MotionUtil(
                _textView,
                _markMap,
                _localSettings,
                _search,
                _navigator,
                _jumpList,
                _statusUtil.Object,
                _vimData);
        }

        public void AssertData(
            MotionResult data,
            SnapshotSpan? span,
            MotionKind motionKind = null)
        {
            if (span.HasValue)
            {
                Assert.AreEqual(span.Value, data.Span);
            }
            if (motionKind != null)
            {
                Assert.AreEqual(motionKind, data.MotionKind);
            }
        }

        [Test]
        public void WordForward1()
        {
            Create("foo bar");
            _textView.MoveCaretTo(0);
            var res = _motionUtil.WordForward(WordKind.NormalWord, 1);
            var span = res.Span;
            Assert.AreEqual(4, span.Length);
            Assert.AreEqual("foo ", span.GetText());
            Assert.IsTrue(res.IsAnyWordMotion);
            Assert.AreEqual(OperationKind.CharacterWise, res.OperationKind);
            Assert.IsTrue(res.IsAnyWordMotion);
        }

        [Test]
        public void WordForward2()
        {
            Create("foo bar");
            _textView.MoveCaretTo(1);
            var res = _motionUtil.WordForward(WordKind.NormalWord, 1);
            var span = res.Span;
            Assert.AreEqual(3, span.Length);
            Assert.AreEqual("oo ", span.GetText());
        }

        [Test, Description("Word motion with a count")]
        public void WordForward3()
        {
            Create("foo bar baz");
            _textView.MoveCaretTo(0);
            var res = _motionUtil.WordForward(WordKind.NormalWord, 2);
            Assert.AreEqual("foo bar ", res.Span.GetText());
        }

        [Test, Description("Count across lines")]
        public void WordForward4()
        {
            Create("foo bar", "baz jaz");
            var res = _motionUtil.WordForward(WordKind.NormalWord, 3);
            Assert.AreEqual("foo bar" + Environment.NewLine + "baz ", res.Span.GetText());
        }

        [Test, Description("Count off the end of the buffer")]
        public void WordForward5()
        {
            Create("foo bar");
            var res = _motionUtil.WordForward(WordKind.NormalWord, 10);
            Assert.AreEqual("foo bar", res.Span.GetText());
        }

        [Test]
        public void WordForward_BigWordIsAnyWord()
        {
            Create("foo bar");
            var res = _motionUtil.WordForward(WordKind.BigWord, 1);
            Assert.IsTrue(res.IsAnyWordMotion);
        }

        [Test]
        public void WordBackward_BothAreAnyWord()
        {
            Create("foo bar");
            Assert.IsTrue(_motionUtil.WordBackward(WordKind.NormalWord, 1).IsAnyWordMotion);
            Assert.IsTrue(_motionUtil.WordBackward(WordKind.BigWord, 1).IsAnyWordMotion);
        }

        [Test]
        public void EndOfLine1()
        {
            Create("foo bar", "baz");
            var res = _motionUtil.EndOfLine(1);
            var span = res.Span;
            Assert.AreEqual("foo bar", span.GetText());
            Assert.AreEqual(MotionKind.CharacterWiseInclusive, res.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, res.OperationKind);
        }

        [Test]
        public void EndOfLine2()
        {
            Create("foo bar", "baz");
            _textView.MoveCaretTo(1);
            var res = _motionUtil.EndOfLine(1);
            Assert.AreEqual("oo bar", res.Span.GetText());
        }

        [Test]
        public void EndOfLine3()
        {
            Create("foo", "bar", "baz");
            var res = _motionUtil.EndOfLine(2);
            Assert.AreEqual("foo" + Environment.NewLine + "bar", res.Span.GetText());
            Assert.AreEqual(MotionKind.CharacterWiseInclusive, res.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, res.OperationKind);
        }

        [Test]
        public void EndOfLine4()
        {
            Create("foo", "bar", "baz", "jar");
            var res = _motionUtil.EndOfLine(3);
            var tuple = res;
            Assert.AreEqual("foo" + Environment.NewLine + "bar" + Environment.NewLine + "baz", tuple.Span.GetText());
            Assert.AreEqual(MotionKind.CharacterWiseInclusive, tuple.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, tuple.OperationKind);
        }

        [Test, Description("Make sure counts past the end of the buffer don't crash")]
        public void EndOfLine5()
        {
            Create("foo");
            var res = _motionUtil.EndOfLine(300);
            Assert.AreEqual("foo", res.Span.GetText());
        }

        [Test]
        public void BeginingOfLine1()
        {
            Create("foo");
            _textView.MoveCaretTo(1);
            var data = _motionUtil.BeginingOfLine();
            Assert.AreEqual(new SnapshotSpan(_buffer.CurrentSnapshot, 0, 1), data.Span);
            Assert.AreEqual(MotionKind.CharacterWiseExclusive, data.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, data.OperationKind);
            Assert.IsFalse(data.IsForward);
        }

        [Test]
        public void BeginingOfLine2()
        {
            Create("foo");
            _textView.MoveCaretTo(2);
            var data = _motionUtil.BeginingOfLine();
            Assert.AreEqual(new SnapshotSpan(_buffer.CurrentSnapshot, 0, 2), data.Span);
            Assert.AreEqual(MotionKind.CharacterWiseExclusive, data.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, data.OperationKind);
            Assert.IsFalse(data.IsForward);
        }

        [Test]
        [Description("Go to begining even if there is whitespace")]
        public void BeginingOfLine3()
        {
            Create("  foo");
            _textView.MoveCaretTo(4);
            var data = _motionUtil.BeginingOfLine();
            Assert.AreEqual(new SnapshotSpan(_buffer.CurrentSnapshot, 0, 4), data.Span);
            Assert.AreEqual(MotionKind.CharacterWiseExclusive, data.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, data.OperationKind);
            Assert.IsFalse(data.IsForward);
        }

        [Test]
        public void FirstNonWhiteSpaceOnCurrentLine1()
        {
            Create("foo");
            _textView.MoveCaretTo(_buffer.GetLineFromLineNumber(0).End);
            var tuple = _motionUtil.FirstNonWhiteSpaceOnCurrentLine();
            Assert.AreEqual("foo", tuple.Span.GetText());
            Assert.AreEqual(MotionKind.CharacterWiseExclusive, tuple.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, tuple.OperationKind);
        }

        [Test, Description("Make sure it goes to the first non-whitespace character")]
        public void FirstNonWhiteSpaceOnCurrentLine2()
        {
            Create("  foo");
            _textView.MoveCaretTo(_buffer.GetLineFromLineNumber(0).End);
            var tuple = _motionUtil.FirstNonWhiteSpaceOnCurrentLine();
            Assert.AreEqual("foo", tuple.Span.GetText());
            Assert.AreEqual(MotionKind.CharacterWiseExclusive, tuple.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, tuple.OperationKind);
        }

        [Test, Description("Make sure to ignore tabs")]
        public void FirstNonWhiteSpaceOnCurrentLine3()
        {
            var text = "\tfoo";
            Create(text);
            _textView.MoveCaretTo(_buffer.GetLineFromLineNumber(0).End);
            var tuple = _motionUtil.FirstNonWhiteSpaceOnCurrentLine();
            Assert.AreEqual(text.IndexOf('f'), tuple.Span.Start);
            Assert.IsFalse(tuple.IsForward);
        }

        [Test]
        [Description("Make sure to move forward to the first non-whitespace")]
        public void FirstNonWhiteSpaceOnCurrentLine4()
        {
            Create(0, "   bar");
            var data = _motionUtil.FirstNonWhiteSpaceOnCurrentLine();
            Assert.AreEqual(_buffer.GetSpan(0, 3), data.Span);
        }

        [Test]
        [Description("Empty line case")]
        public void FirstNonWhiteSpaceOnCurrentLine5()
        {
            Create(0, "");
            var data = _motionUtil.FirstNonWhiteSpaceOnCurrentLine();
            Assert.AreEqual(_buffer.GetSpan(0, 0), data.Span);
        }

        [Test]
        [Description("Backwards case")]
        public void FirstNonWhiteSpaceOnCurrentLine6()
        {
            Create(3, "bar");
            var data = _motionUtil.FirstNonWhiteSpaceOnCurrentLine();
            Assert.AreEqual(_buffer.GetSpan(0, 3), data.Span);
            Assert.IsFalse(data.IsForward);
        }

        /// <summary>
        /// A count of 1 should return a single line 
        /// </summary>
        [Test]
        public void FirstNonWhiteSpaceOnLine_Single()
        {
            Create(0, "cat", "dog");
            var data = _motionUtil.FirstNonWhiteSpaceOnLine(1);
            Assert.AreEqual("cat", data.LineRange.Extent.GetText());
            Assert.AreEqual(OperationKind.LineWise, data.OperationKind);
            Assert.IsTrue(data.IsForward);
            Assert.IsTrue(data.CaretColumn.IsInLastLine);
        }

        /// <summary>
        /// A count of 2 should return 2 lines and the column should be on the last
        /// line
        /// </summary>
        [Test]
        public void FirstNonWhiteSpaceOnLine_Double()
        {
            Create(0, "cat", " dog");
            var data = _motionUtil.FirstNonWhiteSpaceOnLine(2);
            Assert.AreEqual(_textView.GetLineRange(0, 1), data.LineRange);
            Assert.AreEqual(1, data.CaretColumn.AsInLastLine().Item);
        }

        /// <summary>
        /// Should take the trailing white space
        /// </summary>
        [Test]
        public void AllSentence_Simple()
        {
            Create("dog. cat. bear.");
            var data = _motionUtil.AllSentence(1);
            Assert.AreEqual("dog. ", data.Span.GetText());
        }

        /// <summary>
        /// Take the leading white space when there is a preceding sentence and no trailing 
        /// white space
        /// </summary>
        [Test]
        public void AllSentence_NoTrailingWhiteSpace()
        {
            Create("dog. cat.");
            _textView.MoveCaretTo(5);
            var data = _motionUtil.AllSentence(1);
            Assert.AreEqual(" cat.", data.Span.GetText());
        }

        /// <summary>
        /// When starting in the white space include it in the motion instead of the trailing
        /// white space
        /// </summary>
        [Test]
        public void AllSentence_FromWhiteSpace()
        {
            Create("dog. cat. bear.");
            _textView.MoveCaretTo(4);
            var data = _motionUtil.AllSentence(1);
            Assert.AreEqual(" cat.", data.Span.GetText());
        }

        /// <summary>
        /// When the trailing white space goes across new lines then we should still be including
        /// that 
        /// </summary>
        [Test]
        public void AllSentence_WhiteSpaceAcrossNewLine()
        {
            Create("dog.  ", "  cat");
            var data = _motionUtil.AllSentence(1);
            Assert.AreEqual("dog.  " + Environment.NewLine + "  ", data.Span.GetText());
        }

        /// <summary>
        /// This is intended to make sure that 'as' goes through the standard exclusive adjustment
        /// operation.  Even though it technically extends into the next line ':help exclusive-linewise'
        /// dictates it should be changed into a line wise motion
        /// </summary>
        [Test]
        public void AllSentence_OneSentencePerLine()
        {
            Create("dog.", "cat.");
            var data = _motionUtil.GetMotion(Motion.AllSentence).Value;
            Assert.AreEqual(OperationKind.LineWise, data.OperationKind);
            Assert.AreEqual("dog." + Environment.NewLine, data.Span.GetText());
        }

        /// <summary>
        /// Blank lines are sentences so don't include them as white space.  The gap between the '.'
        /// and the blank line is white space though and should be included in the motion
        /// </summary>
        [Test]
        public void AllSentence_DontJumpBlankLinesAsWhiteSpace()
        {
            Create("dog.", "", "cat.");
            var data = _motionUtil.GetMotion(Motion.AllSentence).Value;
            Assert.AreEqual(OperationKind.LineWise, data.OperationKind);
            Assert.AreEqual("dog." + Environment.NewLine, data.Span.GetText());
        }

        /// <summary>
        /// Blank lines are sentences so treat them as such.  Note: A blank line includes the entire blank
        /// line.  But when operating on a blank line it often appears that it doesn't due to the 
        /// rules around motion adjustments spelled out in ':help exclusive'.  Namely if an exclusive motion
        /// ends in column 0 then it gets moved back to the end of the previous line and becomes inclusive
        /// </summary>
        [Test]
        public void AllSentence_BlankLinesAreSentences()
        {
            Create("dog.  ", "", "cat.");
            _textView.MoveCaretToLine(1);
            var data = _motionUtil.AllSentence(1);
            Assert.AreEqual("  " + Environment.NewLine + Environment.NewLine, data.Span.GetText());

            // Make sure it's adjusted properly for the exclusive exception
            data = _motionUtil.GetMotion(Motion.AllSentence).Value;
            Assert.AreEqual("  " + Environment.NewLine, data.Span.GetText());
        }

        /// <summary>
        /// Make sure to include the trailing white space
        /// </summary>
        [Test]
        public void AllWord_Simple()
        {
            Create("foo bar");
            var data = _motionUtil.AllWord(WordKind.NormalWord, 1).Value;
            Assert.AreEqual("foo ", data.Span.GetText());
        }

        /// <summary>
        /// Grab the entire word even if starting in the middle
        /// </summary>
        [Test]
        public void AllWord_FromMiddle()
        {
            Create("foo bar");
            _textView.MoveCaretTo(1);
            var data = _motionUtil.AllWord(WordKind.NormalWord, 1).Value;
            Assert.AreEqual("foo ", data.Span.GetText());
        }

        /// <summary>
        /// All word with a count motion
        /// </summary>
        [Test]
        public void AllWord_WithCount()
        {
            Create("foo bar baz");
            _textView.MoveCaretTo(1);
            var data = _motionUtil.AllWord(WordKind.NormalWord, 2).Value;
            Assert.AreEqual("foo bar ", data.Span.GetText());
        }

        /// <summary>
        /// When starting in white space the space before the word should be included instead
        /// of the white space after it
        /// </summary>
        [Test]
        public void AllWord_StartInWhiteSpace()
        {
            Create("dog cat tree");
            _textView.MoveCaretTo(3);
            var data = _motionUtil.AllWord(WordKind.NormalWord, 1).Value;
            Assert.AreEqual(" cat", data.Span.GetText());
        }

        /// <summary>
        /// When there is no trailing white space and a preceding word then the preceding white
        /// space should be included
        /// </summary>
        [Test]
        public void AllWord_NoTrailingWhiteSpace()
        {
            Create("dog cat");
            _textView.MoveCaretTo(5);
            var data = _motionUtil.AllWord(WordKind.NormalWord, 1).Value;
            Assert.AreEqual(" cat", data.Span.GetText());
        }

        /// <summary>
        /// If there is no trailing white space nor is their a preceding word on the same line
        /// then it shouldn't include the preceding white space
        /// </summary>
        [Test]
        public void AllWord_NoTrailingWhiteSpaceOrPrecedingWordOnSameLine()
        {
            Create("dog", "  cat");
            _textView.MoveCaretTo(_textView.GetLine(1).Start.Add(2));
            var data = _motionUtil.AllWord(WordKind.NormalWord, 1).Value;
            Assert.AreEqual("cat", data.Span.GetText());
        }

        /// <summary>
        /// If there is no trailing white space nor is their a preceding word on the same line
        /// but it is the start of the buffer then do include the white space
        /// </summary>
        [Test]
        public void AllWord_NoTrailingWhiteSpaceOrPrecedingWordAtStartOfBuffer()
        {
            Create("  cat");
            _textView.MoveCaretTo(3);
            var data = _motionUtil.AllWord(WordKind.NormalWord, 1).Value;
            Assert.AreEqual("cat", data.Span.GetText());
        }

        /// <summary>
        /// Make sure we include the full preceding white space if the motion starts in any 
        /// part of it
        /// </summary>
        [Test]
        public void AllWord_FromMiddleOfPrecedingWhiteSpace()
        {
            Create("cat   dog");
            _textView.MoveCaretTo(4);
            var data = _motionUtil.AllWord(WordKind.NormalWord, 1).Value;
            Assert.AreEqual("   dog", data.Span.GetText());
        }

        /// <summary>
        /// On a one word line don't go into the previous line break looking for preceding
        /// white space
        /// </summary>
        [Test]
        public void AllWord_DontGoIntoPreviousLineBreak()
        {
            Create("dog", "cat", "fish");
            _textView.MoveCaretToLine(1);
            var data = _motionUtil.GetMotion(Motion.NewAllWord(WordKind.NormalWord)).Value;
            Assert.AreEqual("cat", data.Span.GetText());
        }

        [Test]
        public void CharLeft_Simple()
        {
            Create("foo bar");
            _textView.MoveCaretTo(2);
            var data = _motionUtil.CharLeft(2);
            Assert.AreEqual("fo", data.Span.GetText());
        }

        /// <summary>
        /// The char left operation should produce an empty span if it's at the start 
        /// of a line 
        /// </summary>
        [Test]
        public void CharLeft_FailAtStartOfLine()
        {
            Create("dog", "cat");
            _textView.MoveCaretToLine(1);
            var data = _motionUtil.CharLeft(1);
            Assert.AreEqual(0, data.Span.Length);
        }

        /// <summary>
        /// When the count is to high but the caret is not at the start then the 
        /// caret should just move to the start of the line
        /// </summary>
        [Test]
        public void CharLeft_CountTooHigh()
        {
            Create("dog", "cat");
            _textView.MoveCaretToLine(1, 1);
            var data = _motionUtil.CharLeft(300);
            Assert.AreEqual("c", data.Span.GetText());
        }

        [Test]
        public void CharRight_Simple()
        {
            Create("foo");
            var data = _motionUtil.CharRight(1);
            Assert.AreEqual("f", data.Span.GetText());
            Assert.AreEqual(OperationKind.CharacterWise, data.OperationKind);
            Assert.AreEqual(MotionKind.CharacterWiseExclusive, data.MotionKind);
        }

        /// <summary>
        /// The char right motion actually needs to succeed at the last point of the 
        /// line.  It often appears to not succeed because many users have
        /// 'virtualedit=' (at least not 'onemore').  So a 'l' at the end of the 
        /// line fails to move the caret which gives the appearance of failure.  In
        /// fact it succeeded but the caret move is not legal
        /// </summary>
        [Test]
        public void CharRight_LastPointOnLine()
        {
            Create("cat", "dog", "tree");
            _textView.MoveCaretToLine(1, 2);
            var data = _motionUtil.CharRight(1);
            Assert.AreEqual("g", data.Span.GetText());
        }

        /// <summary>
        /// The char right should produce an empty span at the end of the line
        /// </summary>
        [Test]
        public void CharRight_EndOfLine()
        {
            Create("cat", "dog");
            _textView.MoveCaretTo(3);
            var data = _motionUtil.CharRight(1);
            Assert.AreEqual("", data.Span.GetText());
        }

        [Test]
        public void EndOfWord1()
        {
            Create("foo bar");
            var res = _motionUtil.EndOfWord(WordKind.NormalWord, 1);
            Assert.AreEqual(MotionKind.CharacterWiseInclusive, res.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, res.OperationKind);
            Assert.AreEqual(new SnapshotSpan(_buffer.CurrentSnapshot, 0, 3), res.Span);
        }

        [Test, Description("Needs to cross the end of the line")]
        public void EndOfWord2()
        {
            Create("foo   ", "bar");
            _textView.MoveCaretTo(4);
            var res = _motionUtil.EndOfWord(WordKind.NormalWord, 1);
            var span = new SnapshotSpan(
                _buffer.GetPoint(4),
                _buffer.GetLineFromLineNumber(1).Start.Add(3));
            Assert.AreEqual(span, res.Span);
            Assert.AreEqual(MotionKind.CharacterWiseInclusive, res.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, res.OperationKind);
        }

        [Test]
        public void EndOfWord3()
        {
            Create("foo bar baz jaz");
            var res = _motionUtil.EndOfWord(WordKind.NormalWord, 2);
            var span = new SnapshotSpan(_buffer.CurrentSnapshot, 0, 7);
            Assert.AreEqual(span, res.Span);
            Assert.AreEqual(MotionKind.CharacterWiseInclusive, res.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, res.OperationKind);
        }

        [Test, Description("Work across blank lines")]
        public void EndOfWord4()
        {
            Create("foo   ", "", "bar");
            _textView.MoveCaretTo(4);
            var res = _motionUtil.EndOfWord(WordKind.NormalWord, 1);
            var span = new SnapshotSpan(
                _buffer.GetPoint(4),
                _buffer.GetLineFromLineNumber(2).Start.Add(3));
            Assert.AreEqual(span, res.Span);
            Assert.AreEqual(MotionKind.CharacterWiseInclusive, res.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, res.OperationKind);
        }

        [Test, Description("Go off the end of the buffer")]
        public void EndOfWord5()
        {
            Create("foo   ", "", "bar");
            _textView.MoveCaretTo(4);
            var res = _motionUtil.EndOfWord(WordKind.NormalWord, 400);
            var span = new SnapshotSpan(
                _textView.TextSnapshot,
                Span.FromBounds(4, _textView.TextSnapshot.Length));
            Assert.AreEqual(span, res.Span);
        }

        [Test]
        [Description("On the last char of a word motion should proceed forward")]
        public void EndOfWord6()
        {
            Create("foo bar baz");
            _textView.MoveCaretTo(2);
            var res = _motionUtil.EndOfWord(WordKind.NormalWord, 1);
            Assert.AreEqual("o bar", res.Span.GetText());
        }

        [Test]
        public void EndOfWord7()
        {
            Create("foo", "bar");
            _textView.MoveCaretTo(2);
            var res = _motionUtil.EndOfWord(WordKind.NormalWord, 1);
            Assert.AreEqual("o" + Environment.NewLine + "bar", res.Span.GetText());
        }

        [Test]
        [Description("Second to last character")]
        public void EndOfWord8()
        {
            Create("the dog goes around the house");
            _textView.MoveCaretTo(1);
            Assert.AreEqual('h', _textView.GetCaretPoint().GetChar());
            var res = _motionUtil.EndOfWord(WordKind.NormalWord, 1);
            Assert.AreEqual("he", res.Span.GetText());
        }

        [Test]
        public void EndOfWord_DontStopOnPunctuation()
        {
            Create("A. the ball");
            _textView.MoveCaretTo(1);
            var data = _motionUtil.EndOfWord(WordKind.NormalWord, 1);
            Assert.AreEqual(". the", data.Span.GetText());
        }

        [Test]
        public void EndOfWord_DoublePunctuation()
        {
            Create("A.. the ball");
            _textView.MoveCaretTo(1);
            var data = _motionUtil.EndOfWord(WordKind.NormalWord, 1);
            Assert.AreEqual("..", data.Span.GetText());
        }

        [Test]
        public void EndOfWord_DoublePunctuationWithCount()
        {
            Create("A.. the ball");
            _textView.MoveCaretTo(1);
            var data = _motionUtil.EndOfWord(WordKind.NormalWord, 2);
            Assert.AreEqual(".. the", data.Span.GetText());
        }

        [Test]
        public void EndOfWord_DoublePunctuationIsAWord()
        {
            Create("A.. the ball");
            _textView.MoveCaretTo(0);
            var data = _motionUtil.EndOfWord(WordKind.NormalWord, 1);
            Assert.AreEqual("A..", data.Span.GetText());
        }

        [Test]
        public void EndOfWord_DontStopOnEndOfLine()
        {
            Create("A. ", "the ball");
            _textView.MoveCaretTo(1);
            var data = _motionUtil.EndOfWord(WordKind.NormalWord, 1);
            Assert.AreEqual(". " + Environment.NewLine + "the", data.Span.GetText());
        }

        [Test]
        public void ForwardChar1()
        {
            Create("foo bar baz");
            Assert.AreEqual("fo", _motionUtil.CharSearch('o', 1, CharSearchKind.ToChar, Path.Forward).Value.Span.GetText());
            _textView.MoveCaretTo(1);
            Assert.AreEqual("oo", _motionUtil.CharSearch('o', 1, CharSearchKind.ToChar, Path.Forward).Value.Span.GetText());
            _textView.MoveCaretTo(1);
            Assert.AreEqual("oo b", _motionUtil.CharSearch('b', 1, CharSearchKind.ToChar, Path.Forward).Value.Span.GetText());
        }

        [Test]
        public void ForwardChar2()
        {
            Create("foo bar baz");
            var data = _motionUtil.CharSearch('q', 1, CharSearchKind.ToChar, Path.Forward);
            Assert.IsTrue(data.IsNone());
        }

        [Test]
        public void ForwardChar3()
        {
            Create("foo bar baz");
            var data = _motionUtil.CharSearch('o', 1, CharSearchKind.ToChar, Path.Forward).Value;
            Assert.AreEqual(MotionKind.CharacterWiseInclusive, data.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, data.OperationKind);
        }

        [Test, Description("Bad count gets nothing in gVim")]
        public void ForwardChar4()
        {
            Create("foo bar baz");
            var data = _motionUtil.CharSearch('o', 300, CharSearchKind.ToChar, Path.Forward);
            Assert.IsTrue(data.IsNone());
        }

        [Test]
        public void ForwardTillChar1()
        {
            Create("foo bar baz");
            Assert.AreEqual("f", _motionUtil.CharSearch('o', 1, CharSearchKind.TillChar, Path.Forward).Value.Span.GetText());
            Assert.AreEqual("foo ", _motionUtil.CharSearch('b', 1, CharSearchKind.TillChar, Path.Forward).Value.Span.GetText());
        }

        [Test]
        public void ForwardTillChar2()
        {
            Create("foo bar baz");
            Assert.IsTrue(_motionUtil.CharSearch('q', 1, CharSearchKind.TillChar, Path.Forward).IsNone());
        }

        [Test]
        public void ForwardTillChar3()
        {
            Create("foo bar baz");
            Assert.AreEqual("fo", _motionUtil.CharSearch('o', 2, CharSearchKind.TillChar, Path.Forward).Value.Span.GetText());
        }

        [Test, Description("Bad count gets nothing in gVim")]
        public void ForwardTillChar4()
        {
            Create("foo bar baz");
            Assert.IsTrue(_motionUtil.CharSearch('o', 300, CharSearchKind.TillChar, Path.Forward).IsNone());
        }

        [Test]
        public void BackwardCharMotion1()
        {
            Create("the boy kicked the ball");
            _textView.MoveCaretTo(_buffer.GetLine(0).End);
            var data = _motionUtil.CharSearch('b', 1, CharSearchKind.ToChar, Path.Backward).Value;
            Assert.AreEqual("ball", data.Span.GetText());
            Assert.AreEqual(MotionKind.CharacterWiseExclusive, data.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, data.OperationKind);
        }

        [Test]
        public void BackwardCharMotion2()
        {
            Create("the boy kicked the ball");
            _textView.MoveCaretTo(_buffer.GetLine(0).End);
            var data = _motionUtil.CharSearch('b', 2, CharSearchKind.ToChar, Path.Backward).Value;
            Assert.AreEqual("boy kicked the ball", data.Span.GetText());
            Assert.AreEqual(MotionKind.CharacterWiseExclusive, data.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, data.OperationKind);
        }

        [Test]
        public void BackwardTillCharMotion1()
        {
            Create("the boy kicked the ball");
            _textView.MoveCaretTo(_buffer.GetLine(0).End);
            var data = _motionUtil.CharSearch('b', 1, CharSearchKind.TillChar, Path.Backward).Value;
            Assert.AreEqual("all", data.Span.GetText());
            Assert.AreEqual(MotionKind.CharacterWiseExclusive, data.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, data.OperationKind);
        }

        [Test]
        public void BackwardTillCharMotion2()
        {
            Create("the boy kicked the ball");
            _textView.MoveCaretTo(_buffer.GetLine(0).End);
            var data = _motionUtil.CharSearch('b', 2, CharSearchKind.TillChar, Path.Backward).Value;
            Assert.AreEqual("oy kicked the ball", data.Span.GetText());
            Assert.AreEqual(MotionKind.CharacterWiseExclusive, data.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, data.OperationKind);
        }

        /// <summary>
        /// Inner word from the middle of a word
        /// </summary>
        [Test]
        public void InnerWord_Simple()
        {
            Create("the dog");
            _textView.MoveCaretTo(1);
            var data = _motionUtil.InnerWord(WordKind.NormalWord, 1).Value;
            Assert.AreEqual("the", data.Span.GetText());
            Assert.IsTrue(data.IsInclusive);
            Assert.IsTrue(data.IsForward);
        }

        /// <summary>
        /// An inner word motion which begins in space should include the full space span
        /// in the return
        /// </summary>
        [Test]
        public void InnerWord_FromSpace()
        {
            Create("   the dog");
            _textView.MoveCaretTo(1);
            var data = _motionUtil.InnerWord(WordKind.NormalWord, 1).Value;
            Assert.AreEqual("   ", data.Span.GetText());
        }


        /// <summary>
        /// The count should apply equally to white space and the following words
        /// </summary>
        [Test]
        public void InnerWord_FromSpaceWithCount()
        {
            Create("   the dog");
            _textView.MoveCaretTo(1);
            var data = _motionUtil.InnerWord(WordKind.NormalWord, 2).Value;
            Assert.AreEqual("   the", data.Span.GetText());
        }

        /// <summary>
        /// Including a case where the count gives us white space on both ends of the 
        /// returned span
        /// </summary>
        [Test]
        public void InnerWord_FromSpaceWithOddCount()
        {
            Create("   the dog");
            _textView.MoveCaretTo(1);
            var data = _motionUtil.InnerWord(WordKind.NormalWord, 3).Value;
            Assert.AreEqual("   the ", data.Span.GetText());
        }

        /// <summary>
        /// When the caret is in the line break and there is a word at the end of the 
        /// line and there is a count of 1 then we just grab the last character of the
        /// previous word
        /// </summary>
        [Test]
        public void InnerWord_FromLineBreakWthPrecedingWord()
        {
            Create("cat", "dog");
            _textView.MoveCaretTo(_textView.GetLine(0).End);
            var data = _motionUtil.InnerWord(WordKind.NormalWord, 1).Value;
            Assert.AreEqual("t", data.Span.GetText());
            Assert.IsTrue(data.IsForward);
        }

        /// <summary>
        /// When the caret is in the line break and there is a space at the end of the 
        /// line and there is a count of 1 then we just grab the entire preceding space
        /// </summary>
        [Test]
        public void InnerWord_FromLineBreakWthPrecedingSpace()
        {
            Create("cat  ", "dog");
            _textView.MoveCaretTo(_textView.GetLine(0).End);
            var data = _motionUtil.InnerWord(WordKind.NormalWord, 1).Value;
            Assert.AreEqual("  ", data.Span.GetText());
            Assert.IsTrue(data.IsForward);
        }

        /// <summary>
        /// When in the line break and given a count the line break counts as our first
        /// and other wise proceed as a normal inner word motion
        /// </summary>
        [Test]
        public void InnerWord_FromLineBreakWthCount()
        {
            Create("cat", "fish dog");
            _textView.MoveCaretTo(_textView.GetLine(0).End);
            var data = _motionUtil.InnerWord(WordKind.NormalWord, 2).Value;
            Assert.AreEqual(Environment.NewLine + "fish", data.Span.GetText());
            Assert.IsTrue(data.IsForward);
        }

        /// <summary>
        /// A single space after a '.' should make the '.' the sentence end
        /// </summary>
        [Test]
        public void IsSentenceEnd_SingleSpace()
        {
            Create("a!b. c");
            Assert.IsTrue(_motionUtil.IsSentenceEnd(SentenceKind.Default, _textBuffer.GetPoint(4)));
        }

        /// <summary>
        /// The last portion of many trailing characters is the end of a sentence
        /// </summary>
        [Test]
        public void IsSentenceEnd_ManyTrailingCharacters()
        {
            Create("a?)]' b.");
            Assert.IsTrue(_motionUtil.IsSentenceEnd(SentenceKind.Default, _textBuffer.GetPoint(5)));
        }

        /// <summary>
        /// Don't report the start of a buffer as being the end of a sentence
        /// </summary>
        [Test]
        public void IsSentenceEnd_StartOfBuffer()
        {
            Create("dog. cat");
            Assert.IsFalse(_motionUtil.IsSentenceEnd(SentenceKind.Default, _textBuffer.GetPoint(0)));
        }

        /// <summary>
        /// A blank line is a complete sentence so the EndLine value is the end of the sentence
        /// </summary>
        [Test]
        public void IsSentenceEnd_BlankLine()
        {
            Create("dog", "", "bear");
            Assert.IsTrue(_motionUtil.IsSentenceEnd(SentenceKind.Default, _textView.GetLine(1).Start));
        }

        [Test]
        public void IsSentenceEnd_Thorough()
        {
            Create("dog", "cat", "bear");
            for (var i = 1; i < _textBuffer.CurrentSnapshot.Length; i++)
            {
                var point = _textBuffer.GetPoint(i);
                var test = _motionUtil.IsSentenceEnd(SentenceKind.Default, point);
                Assert.IsFalse(test);
            }
        }

        [Test]
        public void IsSentenceStartOnly_AfterTrailingChars()
        {
            Create("a?)]' b.");
            Assert.IsTrue(_motionUtil.IsSentenceStartOnly(SentenceKind.Default, _textBuffer.GetPoint(6)));
        }

        /// <summary>
        /// Make sure we don't report the second char as the start due to a math error
        /// </summary>
        [Test]
        public void IsSentenceStartOnly_SecondChar()
        {
            Create("dog. cat");
            Assert.IsTrue(_motionUtil.IsSentenceStartOnly(SentenceKind.Default, _textBuffer.GetPoint(0)));
            Assert.IsFalse(_motionUtil.IsSentenceStartOnly(SentenceKind.Default, _textBuffer.GetPoint(1)));
        }

        /// <summary>
        /// A blank line is a sentence start
        /// </summary>
        [Test]
        public void IsSentenceStart_BlankLine()
        {
            Create("dog.  ", "", "");
            Assert.IsTrue(_motionUtil.IsSentenceStart(SentenceKind.Default, _textBuffer.GetLine(1).Start));
        }

        [Test]
        public void LineOrFirstToFirstNonWhiteSpace1()
        {
            Create("foo", "bar", "baz");
            _textView.MoveCaretTo(_buffer.GetLine(1).Start);
            var data = _motionUtil.LineOrFirstToFirstNonWhiteSpace(FSharpOption.Create(0));
            Assert.AreEqual(_buffer.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
            Assert.IsFalse(data.IsForward);
            Assert.IsTrue(data.MotionKind.IsLineWise);
            Assert.AreEqual(0, data.CaretColumn.AsInLastLine().Item);
        }

        [Test]
        public void LineOrFirstToFirstNonWhiteSpace2()
        {
            Create("foo", "bar", "baz");
            var data = _motionUtil.LineOrFirstToFirstNonWhiteSpace(FSharpOption.Create(2));
            Assert.AreEqual(_buffer.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
            Assert.IsTrue(data.IsForward);
            Assert.IsTrue(data.MotionKind.IsLineWise);
            Assert.AreEqual(0, data.CaretColumn.AsInLastLine().Item);
        }

        [Test]
        public void LineOrFirstToFirstNonWhiteSpace3()
        {
            Create("foo", "  bar", "baz");
            var data = _motionUtil.LineOrFirstToFirstNonWhiteSpace(FSharpOption.Create(2));
            Assert.AreEqual(_buffer.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
            Assert.IsTrue(data.IsForward);
            Assert.IsTrue(data.MotionKind.IsLineWise);
            Assert.AreEqual(2, data.CaretColumn.AsInLastLine().Item);
        }

        [Test]
        public void LineOrFirstToFirstNonWhiteSpace4()
        {
            Create("foo", "  bar", "baz");
            var data = _motionUtil.LineOrFirstToFirstNonWhiteSpace(FSharpOption.Create(500));
            Assert.AreEqual(_buffer.GetLineRange(0, 0).ExtentIncludingLineBreak, data.Span);
            Assert.IsTrue(data.IsForward);
            Assert.IsTrue(data.MotionKind.IsLineWise);
            Assert.AreEqual(0, data.CaretColumn.AsInLastLine().Item);
        }

        [Test]
        public void LineOrFirstToFirstNonWhiteSpace5()
        {
            Create("  the", "dog", "jumped");
            _textView.MoveCaretTo(_textView.GetLine(1).Start);
            var data = _motionUtil.LineOrFirstToFirstNonWhiteSpace(FSharpOption<int>.None);
            Assert.AreEqual(0, data.Span.Start.Position);
            Assert.AreEqual(2, data.CaretColumn.AsInLastLine().Item);
            Assert.IsFalse(data.IsForward);
        }

        [Test]
        public void LineOrLastToFirstNonWhiteSpace1()
        {
            Create("foo", "bar", "baz");
            var data = _motionUtil.LineOrLastToFirstNonWhiteSpace(FSharpOption.Create(2));
            Assert.AreEqual(_buffer.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
            Assert.IsTrue(data.IsForward);
            Assert.IsTrue(data.MotionKind.IsLineWise);
            Assert.AreEqual(0, data.CaretColumn.AsInLastLine().Item);
        }

        [Test]
        public void LineOrLastToFirstNonWhiteSpace2()
        {
            Create("foo", "bar", "baz");
            _textView.MoveCaretTo(_buffer.GetLine(1).Start);
            var data = _motionUtil.LineOrLastToFirstNonWhiteSpace(FSharpOption.Create(0));
            Assert.AreEqual(_buffer.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
            Assert.IsFalse(data.IsForward);
            Assert.IsTrue(data.MotionKind.IsLineWise);
            Assert.AreEqual(0, data.CaretColumn.AsInLastLine().Item);
        }

        [Test]
        public void LineOrLastToFirstNonWhiteSpace3()
        {
            Create("foo", "bar", "baz");
            _textView.MoveCaretTo(_buffer.GetLine(1).Start);
            var data = _motionUtil.LineOrLastToFirstNonWhiteSpace(FSharpOption.Create(500));
            Assert.AreEqual(_buffer.GetLineRange(1, 2).ExtentIncludingLineBreak, data.Span);
            Assert.IsTrue(data.IsForward);
            Assert.IsTrue(data.MotionKind.IsLineWise);
            Assert.AreEqual(0, data.CaretColumn.AsInLastLine().Item);
        }

        [Test]
        public void LineOrLastToFirstNonWhiteSpace4()
        {
            Create("foo", "bar", "baz");
            var data = _motionUtil.LineOrLastToFirstNonWhiteSpace(FSharpOption<int>.None);
            var span = new SnapshotSpan(_buffer.CurrentSnapshot, 0, _buffer.CurrentSnapshot.Length);
            Assert.AreEqual(span, data.Span);
            Assert.IsTrue(data.IsForward);
            Assert.IsTrue(data.MotionKind.IsLineWise);
            Assert.AreEqual(0, data.CaretColumn.AsInLastLine().Item);
        }

        [Test]
        public void LastNonWhiteSpaceOnLine1()
        {
            Create("foo", "bar ");
            var data = _motionUtil.LastNonWhiteSpaceOnLine(1);
            Assert.AreEqual(_buffer.GetLineRange(0).Extent, data.Span);
            Assert.IsTrue(data.IsForward);
            Assert.IsTrue(data.MotionKind.IsCharacterWiseInclusive);
            Assert.AreEqual(OperationKind.CharacterWise, data.OperationKind);
            Assert.AreEqual(MotionKind.CharacterWiseInclusive, data.MotionKind);
        }

        [Test]
        public void LastNonWhiteSpaceOnLine2()
        {
            Create("foo", "bar ", "jaz");
            var data = _motionUtil.LastNonWhiteSpaceOnLine(2);
            Assert.AreEqual(new SnapshotSpan(_buffer.GetPoint(0), _buffer.GetLine(1).Start.Add(3)), data.Span);
            Assert.IsTrue(data.IsForward);
            Assert.AreEqual(OperationKind.CharacterWise, data.OperationKind);
            Assert.AreEqual(MotionKind.CharacterWiseInclusive, data.MotionKind);
        }

        [Test]
        public void LastNonWhiteSpaceOnLine3()
        {
            Create("foo", "bar ", "jaz", "");
            var data = _motionUtil.LastNonWhiteSpaceOnLine(300);
            Assert.AreEqual(new SnapshotSpan(_buffer.CurrentSnapshot, 0, _buffer.CurrentSnapshot.Length), data.Span);
            Assert.IsTrue(data.IsForward);
            Assert.AreEqual(OperationKind.CharacterWise, data.OperationKind);
            Assert.AreEqual(MotionKind.CharacterWiseInclusive, data.MotionKind);
        }

        [Test]
        public void LineFromTopOfVisibleWindow1()
        {
            var buffer = EditorUtil.CreateBuffer("foo", "bar", "baz");
            var tuple = MockObjectFactory.CreateTextViewWithVisibleLines(buffer, 0, 1);
            Create(tuple.Item1.Object);
            var data = _motionUtil.LineFromTopOfVisibleWindow(FSharpOption<int>.None);
            Assert.AreEqual(buffer.GetLineRange(0).Extent, data.Span);
            Assert.IsTrue(data.MotionKind.IsLineWise);
            Assert.IsTrue(data.IsForward);
        }

        [Test]
        public void LineFromTopOfVisibleWindow2()
        {
            var buffer = EditorUtil.CreateBuffer("foo", "bar", "baz", "jazz");
            var tuple = MockObjectFactory.CreateTextViewWithVisibleLines(buffer, 0, 2);
            Create(tuple.Item1.Object);
            var data = _motionUtil.LineFromTopOfVisibleWindow(FSharpOption.Create(2));
            Assert.AreEqual(buffer.GetLineRange(0, 1).Extent, data.Span);
            Assert.IsTrue(data.MotionKind.IsLineWise);
            Assert.IsTrue(data.IsForward);
        }

        [Test]
        [Description("From visible line not caret point")]
        public void LineFromTopOfVisibleWindow3()
        {
            var buffer = EditorUtil.CreateBuffer("foo", "bar", "baz", "jazz");
            var tuple = MockObjectFactory.CreateTextViewWithVisibleLines(buffer, 0, 2, caretPosition: buffer.GetLine(2).Start.Position);
            Create(tuple.Item1.Object);
            var data = _motionUtil.LineFromTopOfVisibleWindow(FSharpOption.Create(2));
            Assert.AreEqual(buffer.GetLineRange(0, 1).Extent, data.Span);
            Assert.IsTrue(data.MotionKind.IsLineWise);
            Assert.IsFalse(data.IsForward);
        }

        [Test]
        public void LineFromTopOfVisibleWindow4()
        {
            var buffer = EditorUtil.CreateBuffer("  foo", "bar");
            var tuple = MockObjectFactory.CreateTextViewWithVisibleLines(buffer, 0, 1, caretPosition: buffer.GetLine(1).End);
            Create(tuple.Item1.Object);
            var data = _motionUtil.LineFromTopOfVisibleWindow(FSharpOption<int>.None);
            Assert.AreEqual(2, data.CaretColumn.AsInLastLine().Item);
        }

        [Test]
        public void LineFromTopOfVisibleWindow5()
        {
            var buffer = EditorUtil.CreateBuffer("  foo", "bar");
            var tuple = MockObjectFactory.CreateTextViewWithVisibleLines(buffer, 0, 1, caretPosition: buffer.GetLine(1).End);
            Create(tuple.Item1.Object);
            _globalSettings.StartOfLine = false;
            var data = _motionUtil.LineFromTopOfVisibleWindow(FSharpOption<int>.None);
            Assert.IsTrue(data.CaretColumn.IsNone);
        }

        [Test]
        public void LineFromBottomOfVisibleWindow1()
        {
            var buffer = EditorUtil.CreateBuffer("a", "b", "c", "d");
            var tuple = MockObjectFactory.CreateTextViewWithVisibleLines(buffer, 0, 2);
            Create(tuple.Item1.Object);
            var data = _motionUtil.LineFromBottomOfVisibleWindow(FSharpOption<int>.None);
            Assert.AreEqual(new SnapshotSpan(_buffer.GetPoint(0), _buffer.GetLine(2).End), data.Span);
            Assert.IsTrue(data.IsForward);
            Assert.AreEqual(OperationKind.LineWise, data.OperationKind);
        }

        [Test]
        public void LineFromBottomOfVisibleWindow2()
        {
            var buffer = EditorUtil.CreateBuffer("a", "b", "c", "d");
            var tuple = MockObjectFactory.CreateTextViewWithVisibleLines(buffer, 0, 2);
            Create(tuple.Item1.Object);
            var data = _motionUtil.LineFromBottomOfVisibleWindow(FSharpOption.Create(2));
            Assert.AreEqual(new SnapshotSpan(_buffer.GetPoint(0), _buffer.GetLine(1).End), data.Span);
            Assert.IsTrue(data.IsForward);
            Assert.AreEqual(OperationKind.LineWise, data.OperationKind);
        }

        [Test]
        public void LineFromBottomOfVisibleWindow3()
        {
            var buffer = EditorUtil.CreateBuffer("a", "b", "c", "d");
            var tuple = MockObjectFactory.CreateTextViewWithVisibleLines(buffer, 0, 2, caretPosition: buffer.GetLine(2).End);
            Create(tuple.Item1.Object);
            var data = _motionUtil.LineFromBottomOfVisibleWindow(FSharpOption.Create(2));
            Assert.AreEqual(new SnapshotSpan(_buffer.GetLine(1).Start, _buffer.GetLine(2).End), data.Span);
            Assert.IsFalse(data.IsForward);
            Assert.AreEqual(OperationKind.LineWise, data.OperationKind);
        }

        [Test]
        public void LineFromBottomOfVisibleWindow4()
        {
            var buffer = EditorUtil.CreateBuffer("a", "b", "  c", "d");
            var tuple = MockObjectFactory.CreateTextViewWithVisibleLines(buffer, 0, 2);
            Create(tuple.Item1.Object);
            var data = _motionUtil.LineFromBottomOfVisibleWindow(FSharpOption<int>.None);
            Assert.AreEqual(2, data.CaretColumn.AsInLastLine().Item);
        }

        [Test]
        public void LineFromBottomOfVisibleWindow5()
        {
            var buffer = EditorUtil.CreateBuffer("a", "b", "  c", "d");
            var tuple = MockObjectFactory.CreateTextViewWithVisibleLines(buffer, 0, 2);
            Create(tuple.Item1.Object);
            _globalSettings.StartOfLine = false;
            var data = _motionUtil.LineFromBottomOfVisibleWindow(FSharpOption<int>.None);
            Assert.IsTrue(data.CaretColumn.IsNone);
        }

        [Test]
        public void LineFromMiddleOfWindow1()
        {
            var buffer = EditorUtil.CreateBuffer("a", "b", "c", "d");
            var tuple = MockObjectFactory.CreateTextViewWithVisibleLines(buffer, 0, 2);
            Create(tuple.Item1.Object);
            var data = _motionUtil.LineInMiddleOfVisibleWindow();
            Assert.AreEqual(new SnapshotSpan(_buffer.GetPoint(0), _buffer.GetLine(1).End), data.Span);
            Assert.AreEqual(OperationKind.LineWise, data.OperationKind);
        }

        [Test]
        public void LineDownToFirstNonWhiteSpace1()
        {
            Create("a", "b", "c", "d");
            var data = _motionUtil.LineDownToFirstNonWhiteSpace(1);
            Assert.IsTrue(data.MotionKind.IsLineWise);
            Assert.AreEqual(_buffer.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
            Assert.IsTrue(data.IsForward);
        }

        [Test]
        public void LineDownToFirstNonWhiteSpace2()
        {
            Create("a", "b", "c", "d");
            var data = _motionUtil.LineDownToFirstNonWhiteSpace(2);
            Assert.IsTrue(data.MotionKind.IsLineWise);
            Assert.AreEqual(_buffer.GetLineRange(0, 2).ExtentIncludingLineBreak, data.Span);
            Assert.IsTrue(data.IsForward);
        }

        [Test]
        [Description("Count of 0 is valid for this motion")]
        public void LineDownToFirstNonWhiteSpace3()
        {
            Create("a", "b", "c", "d");
            var data = _motionUtil.LineDownToFirstNonWhiteSpace(0);
            Assert.IsTrue(data.MotionKind.IsLineWise);
            Assert.AreEqual(_buffer.GetLineRange(0).ExtentIncludingLineBreak, data.Span);
            Assert.IsTrue(data.IsForward);
        }

        [Test]
        [Description("This is a linewise motion and should return line spans")]
        public void LineDownToFirstNonWhiteSpace4()
        {
            Create("cat", "dog", "bird");
            _textView.MoveCaretTo(1);
            var data = _motionUtil.LineDownToFirstNonWhiteSpace(1);
            var span = _textView.GetLineRange(0, 1).ExtentIncludingLineBreak;
            Assert.AreEqual(span, data.Span);
        }

        [Test]
        public void LineDownToFirstNonWhiteSpace5()
        {
            Create("cat", "  dog", "bird");
            _textView.MoveCaretTo(1);
            var data = _motionUtil.LineDownToFirstNonWhiteSpace(1);
            Assert.IsTrue(data.CaretColumn.IsInLastLine);
            Assert.AreEqual(2, data.CaretColumn.AsInLastLine().Item);
        }

        [Test]
        public void LineDownToFirstNonWhiteSpace6()
        {
            Create("cat", "  dog and again", "bird");
            _textView.MoveCaretTo(1);
            var data = _motionUtil.LineDownToFirstNonWhiteSpace(1);
            Assert.IsTrue(data.CaretColumn.IsInLastLine);
            Assert.AreEqual(2, data.CaretColumn.AsInLastLine().Item);
        }

        [Test]
        public void LineDownToFirstNonWhiteSpaceg()
        {
            Create("cat", "  dog and again", " here bird again");
            _textView.MoveCaretTo(1);
            var data = _motionUtil.LineDownToFirstNonWhiteSpace(2);
            Assert.IsTrue(data.CaretColumn.IsInLastLine);
            Assert.AreEqual(1, data.CaretColumn.AsInLastLine().Item);
        }

        [Test]
        public void LineDown1()
        {
            Create("dog", "cat", "bird");
            var data = _motionUtil.LineDown(1).Value;
            AssertData(
                data,
                _buffer.GetLineRange(0, 1).ExtentIncludingLineBreak,
                MotionKind.NewLineWise(CaretColumn.NewInLastLine(0)));
        }

        [Test]
        public void LineDown2()
        {
            Create("dog", "cat", "bird");
            var data = _motionUtil.LineDown(2).Value;
            AssertData(
                data,
                _buffer.GetLineRange(0, 2).ExtentIncludingLineBreak,
                MotionKind.NewLineWise(CaretColumn.NewInLastLine(0)));
        }

        [Test]
        public void LineUp1()
        {
            Create("dog", "cat", "bird", "horse");
            _textView.MoveCaretTo(_textView.GetLine(2).Start);
            var data = _motionUtil.LineUp(1).Value;
            AssertData(
                data,
                _buffer.GetLineRange(1, 2).ExtentIncludingLineBreak,
                MotionKind.NewLineWise(CaretColumn.NewInLastLine(0)));
        }

        [Test]
        public void LineUp2()
        {
            Create("dog", "cat", "bird", "horse");
            _textView.MoveCaretTo(_textView.GetLine(2).Start);
            var data = _motionUtil.LineUp(2).Value;
            AssertData(
                data,
                _buffer.GetLineRange(0, 2).ExtentIncludingLineBreak,
                MotionKind.NewLineWise(CaretColumn.NewInLastLine(0)));
        }

        [Test]
        public void LineUp3()
        {
            Create("foo", "bar");
            _textView.MoveCaretTo(_buffer.GetLineFromLineNumber(1).Start);
            var data = _motionUtil.LineUp(1).Value;
            Assert.AreEqual(OperationKind.LineWise, data.OperationKind);
            Assert.AreEqual("foo" + Environment.NewLine + "bar", data.Span.GetText());
        }

        [Test]
        public void SectionForward1()
        {
            Create(0, "dog", "\fpig", "{fox");
            var data = _motionUtil.SectionForward(MotionContext.Movement, 1);
            Assert.AreEqual(_textView.GetLineRange(0).ExtentIncludingLineBreak, data.Span);
        }

        [Test]
        public void SectionForward2()
        {
            Create(0, "dog", "\fpig", "fox");
            var data = _motionUtil.SectionForward(MotionContext.Movement, 2);
            Assert.AreEqual(new SnapshotSpan(_snapshot, 0, _snapshot.Length), data.Span);
        }

        [Test]
        public void SectionForward3()
        {
            Create(0, "dog", "{pig", "fox");
            var data = _motionUtil.SectionForward(MotionContext.Movement, 2);
            Assert.AreEqual(new SnapshotSpan(_snapshot, 0, _snapshot.Length), data.Span);
        }

        [Test]
        public void SectionForward4()
        {
            Create(0, "dog", "{pig", "{fox");
            var data = _motionUtil.SectionForward(MotionContext.Movement, 1);
            Assert.AreEqual(_textView.GetLineRange(0).ExtentIncludingLineBreak, data.Span);
        }

        [Test]
        public void SectionForward5()
        {
            Create(0, "dog", "}pig", "fox");
            var data = _motionUtil.SectionForward(MotionContext.AfterOperator, 1);
            Assert.AreEqual(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
        }

        [Test]
        [Description("Only look for } after an operator")]
        public void SectionForward6()
        {
            Create(0, "dog", "}pig", "fox");
            var data = _motionUtil.SectionForward(MotionContext.Movement, 1);
            Assert.AreEqual(new SnapshotSpan(_snapshot, 0, _snapshot.Length), data.Span);
        }

        [Test]
        public void SectionBackwardOrOpenBrace1()
        {
            Create(0, "dog", "{brace", "pig", "}fox");
            var data = _motionUtil.SectionBackwardOrOpenBrace(1);
            Assert.IsTrue(data.Span.IsEmpty);
        }

        [Test]
        public void SectionBackwardOrOpenBrace2()
        {
            Create("dog", "{brace", "pig", "}fox");
            _textView.MoveCaretTo(_textView.GetLine(2).Start.Position);
            var data = _motionUtil.SectionBackwardOrOpenBrace(1);
            Assert.AreEqual(_textView.GetLineRange(1).ExtentIncludingLineBreak, data.Span);
        }

        [Test]
        public void SectionBackwardOrOpenBrace3()
        {
            Create("dog", "{brace", "pig", "}fox");
            _textView.MoveCaretTo(_textView.GetLine(2).Start.Position);
            var data = _motionUtil.SectionBackwardOrOpenBrace(2);
            Assert.AreEqual(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
        }

        [Test]
        public void SectionBackwardOrOpenBrace4()
        {
            Create(0, "dog", "\fbrace", "pig", "}fox");
            var data = _motionUtil.SectionBackwardOrOpenBrace(1);
            Assert.IsTrue(data.Span.IsEmpty);
        }

        [Test]
        public void SectionBackwardOrOpenBrace5()
        {
            Create("dog", "\fbrace", "pig", "}fox");
            _textView.MoveCaretTo(_textView.GetLine(2).Start.Position);
            var data = _motionUtil.SectionBackwardOrOpenBrace(1);
            Assert.AreEqual(_textView.GetLineRange(1).ExtentIncludingLineBreak, data.Span);
        }

        [Test]
        public void SectionBackwardOrOpenBrace6()
        {
            Create("dog", "\fbrace", "pig", "}fox");
            _textView.MoveCaretTo(_textView.GetLine(2).Start.Position);
            var data = _motionUtil.SectionBackwardOrOpenBrace(2);
            Assert.AreEqual(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
        }

        [Test]
        [Description("Ignore the brace not on first column")]
        public void SectionBackwardOrOpenBrace7()
        {
            Create("dog", "\f{brace", "pig", "}fox");
            _textView.MoveCaretTo(_textView.GetLine(2).Start.Position);
            var data = _motionUtil.SectionBackwardOrOpenBrace(2);
            Assert.AreEqual(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
        }

        [Test]
        public void SectionBackwardOrOpenBrace8()
        {
            Create("dog", "{{foo", "{bar", "hello");
            _textView.MoveCaretTo(_textView.GetLine(2).End);
            var data = _motionUtil.SectionBackwardOrOpenBrace(2);
            Assert.AreEqual(
                new SnapshotSpan(
                    _buffer.GetLine(0).Start,
                    _buffer.GetLine(2).End),
                data.Span);
        }

        [Test]
        public void SectionBackwardOrCloseBrace1()
        {
            Create(0, "dog", "}brace", "pig", "}fox");
            var data = _motionUtil.SectionBackwardOrCloseBrace(1);
            Assert.IsTrue(data.Span.IsEmpty);
        }

        [Test]
        public void SectionBackwardOrCloseBrace2()
        {
            Create("dog", "}brace", "pig", "}fox");
            _textView.MoveCaretTo(_textView.GetLine(2).Start.Position);
            var data = _motionUtil.SectionBackwardOrCloseBrace(1);
            Assert.AreEqual(_textView.GetLineRange(1).ExtentIncludingLineBreak, data.Span);
        }

        [Test]
        public void SectionBackwardOrCloseBrace3()
        {
            Create("dog", "}brace", "pig", "}fox");
            _textView.MoveCaretTo(_textView.GetLine(2).Start.Position);
            var data = _motionUtil.SectionBackwardOrCloseBrace(2);
            Assert.AreEqual(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
        }

        [Test]
        public void SectionBackwardOrCloseBrace4()
        {
            Create(0, "dog", "\fbrace", "pig", "}fox");
            var data = _motionUtil.SectionBackwardOrCloseBrace(1);
            Assert.IsTrue(data.Span.IsEmpty);
        }

        [Test]
        public void SectionBackwardOrCloseBrace5()
        {
            Create("dog", "\fbrace", "pig", "}fox");
            _textView.MoveCaretTo(_textView.GetLine(2).Start.Position);
            var data = _motionUtil.SectionBackwardOrCloseBrace(1);
            Assert.AreEqual(_textView.GetLineRange(1).ExtentIncludingLineBreak, data.Span);
        }

        [Test]
        public void SectionBackwardOrCloseBrace6()
        {
            Create("dog", "\fbrace", "pig", "}fox");
            _textView.MoveCaretTo(_textView.GetLine(2).Start.Position);
            var data = _motionUtil.SectionBackwardOrCloseBrace(2);
            Assert.AreEqual(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
        }

        [Test]
        [Description("Ignore the brace not on first column")]
        public void SectionBackwardOrCloseBrace7()
        {
            Create("dog", "\f}brace", "pig", "}fox");
            _textView.MoveCaretTo(_textView.GetLine(2).Start.Position);
            var data = _motionUtil.SectionBackwardOrCloseBrace(2);
            Assert.AreEqual(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
        }

        /// <summary>
        /// At the end of the ITextBuffer the span should return an empty span.  This can be 
        /// repro'd be setting ve=onemore and trying a 'y(' operation past the last character
        /// in the buffer
        /// </summary>
        [Test]
        public void ParagraphForward_EndPoint()
        {
            Create("dog", "pig", "cat");
            _textView.MoveCaretTo(_textView.TextSnapshot.GetEndPoint());
            var data = _motionUtil.ParagraphForward(1);
            Assert.AreEqual("", data.Span.GetText());
        }

        /// <summary>
        /// A forward paragraph from the last character should return the char
        /// </summary>
        [Test]
        public void ParagraphForward_LastChar()
        {
            Create("dog", "pig", "cat");
            _textView.MoveCaretTo(_textView.TextSnapshot.GetEndPoint().Subtract(1));
            var data = _motionUtil.ParagraphForward(1);
            Assert.AreEqual("t", data.Span.GetText());
        }

        [Test]
        public void QuotedString1()
        {
            Create(@"""foo""");
            var data = _motionUtil.QuotedString();
            Assert.IsTrue(data.IsSome());
            AssertData(data.Value, new SnapshotSpan(_snapshot, 0, 5), MotionKind.CharacterWiseInclusive);
        }

        [Test]
        [Description("Include the leading whitespace")]
        public void QuotedString2()
        {
            Create(@"  ""foo""");
            var data = _motionUtil.QuotedString();
            Assert.IsTrue(data.IsSome());
            AssertData(data.Value, new SnapshotSpan(_snapshot, 0, 7), MotionKind.CharacterWiseInclusive);
        }

        [Test]
        [Description("Include the trailing whitespace")]
        public void QuotedString3()
        {
            Create(@"""foo""  ");
            var data = _motionUtil.QuotedString();
            Assert.IsTrue(data.IsSome());
            AssertData(data.Value, new SnapshotSpan(_snapshot, 0, 7), MotionKind.CharacterWiseInclusive);
        }

        [Test]
        [Description("Favor the trailing whitespace over leading")]
        public void QuotedString4()
        {
            Create(@"  ""foo""  ");
            var data = _motionUtil.QuotedString();
            Assert.IsTrue(data.IsSome());
            AssertData(data.Value, new SnapshotSpan(_snapshot, 2, 7), MotionKind.CharacterWiseInclusive);
        }

        [Test]
        [Description("Ignore the escaped quotes")]
        public void QuotedString5()
        {
            Create(@"""foo\""""");
            var data = _motionUtil.QuotedString();
            Assert.IsTrue(data.IsSome());
            AssertData(data.Value, new SnapshotSpan(_snapshot, 0, 7), MotionKind.CharacterWiseInclusive);
        }

        [Test]
        [Description("Ignore the escaped quotes")]
        public void QuotedString6()
        {
            Create(@"""foo(""""");
            _localSettings.QuoteEscape = @"(";
            var data = _motionUtil.QuotedString();
            Assert.IsTrue(data.IsSome());
            AssertData(data.Value, new SnapshotSpan(_snapshot, 0, 7), MotionKind.CharacterWiseInclusive);
        }

        [Test]
        public void QuotedString7()
        {
            Create(@"foo");
            var data = _motionUtil.QuotedString();
            Assert.IsTrue(data.IsNone());
        }

        [Test]
        public void QuotedString8()
        {
            Create(@"""foo"" ""bar""");
            var start = _snapshot.GetText().IndexOf('b');
            _textView.MoveCaretTo(start);
            var data = _motionUtil.QuotedString();
            Assert.IsTrue(data.IsSome());
            AssertData(data.Value, new SnapshotSpan(_snapshot, start - 2, 6), MotionKind.CharacterWiseInclusive);
        }

        [Test]
        public void QuotedStringContents1()
        {
            Create(@"""foo""");
            var data = _motionUtil.QuotedStringContents();
            Assert.IsTrue(data.IsSome());
            AssertData(data.Value, new SnapshotSpan(_snapshot, 1, 3), MotionKind.CharacterWiseInclusive);
        }

        [Test]
        public void QuotedStringContents2()
        {
            Create(@" ""bar""");
            var data = _motionUtil.QuotedStringContents();
            Assert.IsTrue(data.IsSome());
            AssertData(data.Value, new SnapshotSpan(_snapshot, 2, 3), MotionKind.CharacterWiseInclusive);
        }

        [Test]
        public void QuotedStringContents3()
        {
            Create(@"""foo"" ""bar""");
            var start = _snapshot.GetText().IndexOf('b');
            _textView.MoveCaretTo(start);
            var data = _motionUtil.QuotedStringContents();
            Assert.IsTrue(data.IsSome());
            AssertData(data.Value, new SnapshotSpan(_snapshot, start, 3), MotionKind.CharacterWiseInclusive);
        }

        /// <summary>
        /// Ensure that the space after the sentence is included
        /// </summary>
        [Test]
        public void SentencesForward_SpaceAfter()
        {
            Create("a! b");
            var data = _motionUtil.SentenceForward(1);
            AssertData(data, new SnapshotSpan(_snapshot, 0, 3));
        }

        /// <summary>
        /// At the end of the ITextBuffer there isn't a next sentence
        /// </summary>
        [Test]
        public void SentencesForward_EndOfBuffer()
        {
            Create("a! b");
            _textView.MoveCaretTo(_snapshot.Length);
            var data = _motionUtil.SentenceForward(1);
            AssertData(data, new SnapshotSpan(_snapshot, _snapshot.Length, 0));
        }

        [Test]
        public void Mark_Forward()
        {
            Create("the dog chased the ball");
            _markMap.SetMark(_textView.GetPoint(3), 'a');
            var data = _motionUtil.Mark('a').Value;
            Assert.AreEqual("the", data.Span.GetText());
            Assert.AreEqual(OperationKind.CharacterWise, data.OperationKind);
            Assert.AreEqual(MotionKind.CharacterWiseExclusive, data.MotionKind);
            Assert.IsTrue(data.IsForward);
        }

        [Test]
        public void Mark_DoesNotExist()
        {
            Create("the dog chased the ball");
            Assert.IsTrue(_motionUtil.Mark('a').IsNone());
        }

        [Test]
        public void Mark_Backward()
        {
            Create("the dog chased the ball");
            _textView.MoveCaretTo(3);
            _markMap.SetMark(_textView.GetPoint(0), 'a');
            var data = _motionUtil.Mark('a').Value;
            Assert.AreEqual("the", data.Span.GetText());
            Assert.AreEqual(OperationKind.CharacterWise, data.OperationKind);
            Assert.AreEqual(MotionKind.CharacterWiseExclusive, data.MotionKind);
            Assert.IsFalse(data.IsForward);
        }

        [Test]
        public void MarkLine_DoesNotExist()
        {
            Create("the dog chased the ball");
            Assert.IsTrue(_motionUtil.MarkLine('a').IsNone());
        }

        [Test]
        public void MarkLine_Forward()
        {
            Create("cat", "dog", "pig", "tree");
            _markMap.SetMark(_textView.GetLine(1).Start.Add(1), 'a');
            var data = _motionUtil.MarkLine('a').Value;
            Assert.AreEqual(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
            Assert.IsTrue(data.IsForward);
            Assert.IsTrue(data.MotionKind.IsLineWise);
        }

        [Test]
        public void MarkLine_Backward()
        {
            Create("cat", "dog", "pig", "tree");
            _textView.MoveCaretTo(_textView.GetLine(1).Start.Add(1));
            _markMap.SetMark(_textView.GetPoint(0), 'a');
            var data = _motionUtil.MarkLine('a').Value;
            Assert.AreEqual(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
            Assert.IsFalse(data.IsForward);
            Assert.IsTrue(data.MotionKind.IsLineWise);
        }

        [Test]
        public void LineUpToFirstNonWhiteSpace_UseColumnNotPosition()
        {
            Create("the", "  dog", "cat");
            _textView.MoveCaretToLine(2);
            var data = _motionUtil.LineUpToFirstNonWhiteSpace(1);
            Assert.AreEqual(2, data.CaretColumn.AsInLastLine().Item);
            Assert.IsFalse(data.IsForward);
            Assert.AreEqual(_textView.GetLineRange(1, 2).ExtentIncludingLineBreak, data.Span);
        }

        [Test]
        public void MatchingToken_SimpleParens()
        {
            Create("( )");
            var data = _motionUtil.MatchingToken().Value;
            Assert.AreEqual("( )", data.Span.GetText());
            Assert.IsTrue(data.IsForward);
            Assert.AreEqual(MotionKind.CharacterWiseInclusive, data.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, data.OperationKind);
        }

        [Test]
        public void MatchingToken_SimpleParensWithPrefix()
        {
            Create("cat( )");
            var data = _motionUtil.MatchingToken().Value;
            Assert.AreEqual("cat( )", data.Span.GetText());
            Assert.IsTrue(data.IsForward);
        }

        [Test]
        public void MatchingToken_TooManyOpenOnSameLine()
        {
            Create("cat(( )");
            Assert.IsTrue(_motionUtil.MatchingToken().IsNone());
        }

        [Test]
        public void MatchingToken_AcrossLines()
        {
            Create("cat(", ")");
            var span = new SnapshotSpan(
                _textView.GetLine(0).Start,
                _textView.GetLine(1).Start.Add(1));
            var data = _motionUtil.MatchingToken().Value;
            Assert.AreEqual(span, data.Span);
            Assert.IsTrue(data.IsForward);
        }

        [Test]
        public void MatchingToken_ParensFromEnd()
        {
            Create("cat( )");
            _textView.MoveCaretTo(5);
            var data = _motionUtil.MatchingToken().Value;
            Assert.AreEqual("( )", data.Span.GetText());
            Assert.IsFalse(data.IsForward);
        }

        [Test]
        public void MatchingToken_ParensFromMiddle()
        {
            Create("cat( )");
            _textView.MoveCaretTo(4);
            var data = _motionUtil.MatchingToken().Value;
            Assert.AreEqual("( ", data.Span.GetText());
            Assert.IsFalse(data.IsForward);
        }

        /// <summary>
        /// Make sure we function properly with nested parens.
        /// </summary>
        [Test]
        public void MatchingToken_ParensNestedFromEnd()
        {
            Create("(((a)))");
            _textView.MoveCaretTo(5);
            var data = _motionUtil.MatchingToken().Value;
            Assert.AreEqual("((a))", data.Span.GetText());
            Assert.IsFalse(data.IsForward);
        }

        /// <summary>
        /// Make sure we function properly with consequitive sets of parens
        /// </summary>
        [Test]
        public void MatchingToken_ParensConsecutiveSetsFromEnd()
        {
            Create("((a)) /* ((b))");
            _textView.MoveCaretTo(12);
            var data = _motionUtil.MatchingToken().Value;
            Assert.AreEqual("(b)", data.Span.GetText());
            Assert.IsFalse(data.IsForward);
        }

        /// <summary>
        /// Make sure we function properly with consequitive sets of parens
        /// </summary>
        [Test]
        public void MatchingToken_ParensConsecutiveSetsFromEnd2()
        {
            Create("((a)) /* ((b))");
            _textView.MoveCaretTo(13);
            var data = _motionUtil.MatchingToken().Value;
            Assert.AreEqual("((b))", data.Span.GetText());
            Assert.IsFalse(data.IsForward);
        }

        [Test]
        public void MatchingToken_CommentStartDoesNotNest()
        {
            Create("/* /* */");
            var data = _motionUtil.MatchingToken().Value;
            Assert.AreEqual("/* /* */", data.Span.GetText());
            Assert.IsTrue(data.IsForward);
        }

        [Test]
        public void MatchingToken_IfElsePreProc()
        {
            Create("#if foo #endif", "again", "#endif");
            var data = _motionUtil.MatchingToken().Value;
            var span = new SnapshotSpan(_textView.GetPoint(0), _textView.GetLine(2).Start.Add(6));
            Assert.AreEqual(span, data.Span);
            Assert.AreEqual(MotionKind.CharacterWiseInclusive, data.MotionKind);
        }

        /// <summary>
        /// Make sure find the full paragraph from a point in the middle.
        /// </summary>
        [Test]
        public void AllParagraph_FromMiddle()
        {
            Create("a", "b", "", "c");
            _textView.MoveCaretToLine(1);
            var span = _motionUtil.AllParagraph(1).Value.Span;
            Assert.AreEqual(_snapshot.GetLineRange(0, 2).ExtentIncludingLineBreak, span);
        }

        /// <summary>
        /// Get a paragraph motion from the start of the ITextBuffer
        /// </summary>
        [Test]
        public void AllParagraph_FromStart()
        {
            Create("a", "b", "", "c");
            var span = _motionUtil.AllParagraph(1).Value.Span;
            Assert.AreEqual(_snapshot.GetLineRange(0, 2).ExtentIncludingLineBreak, span);
        }

        /// <summary>
        /// A full paragraph should not include the preceding blanks when starting on
        /// an actual portion of the paragraph
        /// </summary>
        [Test]
        public void AllParagraph_FromStartWithPreceedingBlank()
        {
            Create("a", "b", "", "c");
            _textView.MoveCaretToLine(2);
            var span = _motionUtil.AllParagraph(1).Value.Span;
            Assert.AreEqual(_snapshot.GetLineRange(2, 3).ExtentIncludingLineBreak, span);
        }

        /// <summary>
        /// Make sure the preceding blanks are included when starting on a blank
        /// line but not the trailing ones
        /// </summary>
        [Test]
        public void AllParagraph_FromBlankLine()
        {
            Create("", "dog", "cat", "", "pig", "");
            _textView.MoveCaretToLine(3);
            var span = _motionUtil.AllParagraph(1).Value.Span;
            Assert.AreEqual(_snapshot.GetLineRange(3, 4).ExtentIncludingLineBreak, span);
        }

        /// <summary>
        /// If the span consists of only blank lines then it results in a failed motion.
        /// </summary>
        [Test]
        public void AllParagraph_InBlankLinesAtEnd()
        {
            Create("", "dog", "", "");
            _textView.MoveCaretToLine(2);
            Assert.IsTrue(_motionUtil.AllParagraph(1).IsNone());
        }

        /// <summary>
        /// Make sure we raise an error if there is no word under the caret and that it 
        /// doesn't update LastSearchText
        /// </summary>
        [Test]
        public void NextWord_NoWordUnderCaret()
        {
            Create("  ", "foo bar baz");
            _vimData.LastPatternData = VimUtil.CreatePatternData("cat");
            _statusUtil.Setup(x => x.OnError(Resources.NormalMode_NoWordUnderCursor)).Verifiable();
            _motionUtil.NextWord(Path.Forward, 1);
            _statusUtil.Verify();
            Assert.AreEqual("cat", _vimData.LastPatternData.Pattern);
        }

        /// <summary>
        /// Simple match should update LastSearchData and return the appropriate span
        /// </summary>
        [Test]
        public void NextWord_Simple()
        {
            Create("hello world", "hello");
            var result = _motionUtil.NextWord(Path.Forward, 1).Value;
            Assert.AreEqual(_textView.GetLine(0).ExtentIncludingLineBreak, result.Span);
            Assert.AreEqual(@"\<hello\>", _vimData.LastPatternData.Pattern);
        }

        /// <summary>
        /// If the caret starts on a blank move to the first non-blank to find the 
        /// word.  This is true if we are searching forward or backward.  The original
        /// point though should be included in the search
        /// </summary>
        [Test]
        public void NextWord_BackwardGoPastBlanks()
        {
            Create("dog   cat", "cat");
            _statusUtil.Setup(x => x.OnWarning(Resources.Common_SearchBackwardWrapped)).Verifiable();
            _textView.MoveCaretTo(4);
            var result = _motionUtil.NextWord(Path.Backward, 1).Value;
            Assert.AreEqual("  cat" + Environment.NewLine, result.Span.GetText());
            _statusUtil.Verify();
        }

        /// <summary>
        /// Make sure that searching backward from the middle of a word starts at the 
        /// beginning of the word
        /// </summary>
        [Test]
        public void NextWord_BackwardFromMiddleOfWord()
        {
            Create("cat cat cat");
            _textView.MoveCaretTo(5);
            var result = _motionUtil.NextWord(Path.Backward, 1).Value;
            Assert.AreEqual("cat c", result.Span.GetText());
        }

        /// <summary>
        /// Make sure we pass the LastSearch value to the method and move the caret
        /// for the provided SearchResult
        /// </summary>
        [Test]
        public void LastSearch_UsePattern()
        {
            Create("foo bar", "foo");
            var data = VimUtil.CreatePatternData("foo", Path.Forward);
            _vimData.LastPatternData = data;
            var result = _motionUtil.LastSearch(false, 1).Value;
            Assert.AreEqual("foo bar" + Environment.NewLine, result.Span.GetText());
        }

        /// <summary>
        /// Make sure that this doesn't update the LastSearh field.  Only way to check this is 
        /// when we reverse the polarity of the search
        /// </summary>
        [Test]
        public void LastSearch_DontUpdateLastSearch()
        {
            Create("dog cat", "dog", "dog");
            var data = VimUtil.CreatePatternData("dog", Path.Forward);
            _vimData.LastPatternData = data;
            _statusUtil.Setup(x => x.OnWarning(Resources.Common_SearchBackwardWrapped)).Verifiable();
            _motionUtil.LastSearch(true, 1);
            Assert.AreEqual(data, _vimData.LastPatternData);
            _statusUtil.Verify();
        }

        /// <summary>
        /// Inclusive motion values shouldn't be adjusted
        /// </summary>
        [Test]
        public void AdjustMotionResult_Inclusive()
        {
            Create("cat", "dog");
            var result1 = VimUtil.CreateMotionResult(_textView.GetLine(0).ExtentIncludingLineBreak, motionKind: MotionKind.CharacterWiseInclusive);
            var result2 = _motionUtil.AdjustMotionResult(Motion.CharLeft, result1);
            Assert.AreEqual(result1, result2);
        }

        /// <summary>
        /// Make sure adjusted ones become line wise if it meets the criteria
        /// </summary>
        [Test]
        public void AdjustMotionResult_FullLine()
        {
            Create("  cat", "dog");
            var span = new SnapshotSpan(_textView.GetPoint(2), _textView.GetLine(1).Start);
            var result1 = VimUtil.CreateMotionResult(span, motionKind: MotionKind.CharacterWiseExclusive);
            var result2 = _motionUtil.AdjustMotionResult(Motion.CharLeft, result1);
            Assert.AreEqual(OperationKind.LineWise, result2.OperationKind);
            Assert.AreEqual(_textView.GetLine(0).ExtentIncludingLineBreak, result2.Span);
            Assert.IsTrue(result2.MotionKind.IsLineWise);
        }

        /// <summary>
        /// Don't make it full line if it doesn't start before the first real character
        /// </summary>
        [Test]
        public void AdjustMotionResult_NotFullLine()
        {
            Create("  cat", "dog");
            var span = new SnapshotSpan(_textView.GetPoint(3), _textView.GetLine(1).Start);
            var result1 = VimUtil.CreateMotionResult(span, motionKind: MotionKind.CharacterWiseExclusive);
            var result2 = _motionUtil.AdjustMotionResult(Motion.CharLeft, result1);
            Assert.AreEqual(OperationKind.CharacterWise, result2.OperationKind);
            Assert.AreEqual("at", result2.Span.GetText());
            Assert.IsTrue(result2.MotionKind.IsCharacterWiseInclusive);
            Assert.IsTrue(MotionResultFlags.ExclusivePromotion == (result2.MotionResultFlags & MotionResultFlags.ExclusivePromotion));
        }
        /// <summary>
        /// Make sure special motions don't get adjusted to 'exclusive-linewise'.  These are
        /// not documented anywhere but the exception appears to be word motions
        /// </summary>
        [Test]
        public void AdjustMotionResult_FullLineExceptions()
        {
            Create("  cat", "dog");
            var span = new SnapshotSpan(_textView.GetPoint(2), _textView.GetLine(1).Start);
            var result1 = VimUtil.CreateMotionResult(span, motionKind: MotionKind.CharacterWiseExclusive);
            var result2 = _motionUtil.AdjustMotionResult(Motion.NewAllWord(WordKind.NormalWord), result1);
            Assert.AreEqual(OperationKind.CharacterWise, result2.OperationKind);
            Assert.AreEqual("cat", result2.Span.GetText());
            Assert.IsTrue(result2.MotionKind.IsCharacterWiseInclusive);
        }

        /// <summary>
        /// Break on section macros
        /// </summary>
        [Test]
        public void GetSections_WithMacroAndCloseSplit()
        {
            Create("dog.", ".HH", "cat.");
            _globalSettings.Sections = "HH";
            var ret = _motionUtil.GetSections(SectionKind.OnCloseBrace, Path.Forward, _textBuffer.GetPoint(0));
            CollectionAssert.AreEqual(
                new[] { "dog." + Environment.NewLine, ".HH" + Environment.NewLine + "cat." },
                ret.Select(x => x.GetText()).ToList());
        }

        /// <summary>
        /// Break on section macros
        /// </summary>
        [Test]
        public void GetSections_WithMacroBackwardAndCloseSplit()
        {
            Create("dog.", ".HH", "cat.");
            _globalSettings.Sections = "HH";
            var ret = _motionUtil.GetSections(SectionKind.OnCloseBrace, Path.Backward, _textBuffer.GetEndPoint());
            CollectionAssert.AreEqual(
                new[] { ".HH" + Environment.NewLine + "cat.", "dog." + Environment.NewLine },
                ret.Select(x => x.GetText()).ToList());
        }

        /// <summary>
        /// Going forward we should include the brace line
        /// </summary>
        [Test]
        public void GetSectionsWithSplit_FromBraceLine()
        {
            Create("dog.", "}", "cat");
            var ret = _motionUtil.GetSections(SectionKind.OnCloseBrace, Path.Forward, _textBuffer.GetLine(1).Start);
            CollectionAssert.AreEqual(
                new[] { "}" + Environment.NewLine + "cat" },
                ret.Select(x => x.GetText()).ToList());

            ret = _motionUtil.GetSections(SectionKind.OnCloseBrace, Path.Forward, _textBuffer.GetPoint(0));
            CollectionAssert.AreEqual(
                new[] { "dog." + Environment.NewLine, "}" + Environment.NewLine + "cat" },
                ret.Select(x => x.GetText()).ToList());
        }

        /// <summary>
        /// Going backward we should not include the brace line
        /// </summary>
        [Test]
        public void GetSectionsWithSplit_FromBraceLineBackward()
        {
            Create("dog.", "}", "cat");
            var ret = _motionUtil.GetSections(SectionKind.OnCloseBrace, Path.Backward, _textBuffer.GetLine(1).Start);
            CollectionAssert.AreEqual(
                new[] { "dog." + Environment.NewLine },
                ret.Select(x => x.GetText()).ToList());

            ret = _motionUtil.GetSections(SectionKind.OnCloseBrace, Path.Backward, _textBuffer.GetEndPoint());
            CollectionAssert.AreEqual(
                new[] { "}" + Environment.NewLine + "cat", "dog." + Environment.NewLine },
                ret.Select(x => x.GetText()).ToList());
        }

        [Test]
        public void GetSentences1()
        {
            Create("a. b.");
            var ret = _motionUtil.GetSentences(SentenceKind.Default, Path.Forward, _snapshot.GetPoint(0));
            CollectionAssert.AreEquivalent(
                new[] { "a.", "b." },
                ret.Select(x => x.GetText()).ToList());
        }

        [Test]
        public void GetSentences2()
        {
            Create("a! b.");
            var ret = _motionUtil.GetSentences(SentenceKind.Default, Path.Forward, _snapshot.GetPoint(0));
            CollectionAssert.AreEquivalent(
                new[] { "a!", "b." },
                ret.Select(x => x.GetText()).ToList());
        }

        [Test]
        public void GetSentences3()
        {
            Create("a? b.");
            var ret = _motionUtil.GetSentences(SentenceKind.Default, Path.Forward, _snapshot.GetPoint(0));
            CollectionAssert.AreEquivalent(
                new[] { "a?", "b." },
                ret.Select(x => x.GetText()).ToList());
        }

        [Test]
        public void GetSentences4()
        {
            Create("a? b.");
            var ret = _motionUtil.GetSentences(SentenceKind.Default, Path.Forward, _snapshot.GetEndPoint());
            CollectionAssert.AreEquivalent(
                new string[] { },
                ret.Select(x => x.GetText()).ToList());
        }

        /// <summary>
        /// Make sure the return doesn't include an empty span for the end point
        /// </summary>
        [Test]
        public void GetSentences_BackwardFromEndOfBuffer()
        {
            Create("a? b.");
            var ret = _motionUtil.GetSentences(SentenceKind.Default, Path.Backward, _snapshot.GetEndPoint());
            CollectionAssert.AreEquivalent(
                new[] { "b.", "a?" },
                ret.Select(x => x.GetText()).ToList());
        }

        /// <summary>
        /// Sentences are an exclusive motion and hence backward from a single whitespace 
        /// to a sentence boundary should not include the whitespace
        /// </summary>
        [Test]
        public void GetSentences_BackwardFromSingleWhitespace()
        {
            Create("a? b.");
            var ret = _motionUtil.GetSentences(SentenceKind.Default, Path.Backward, _snapshot.GetPoint(2));
            CollectionAssert.AreEquivalent(
                new[] { "a?" },
                ret.Select(x => x.GetText()).ToList());
        }

        /// <summary>
        /// Make sure we include many legal trailing characters
        /// </summary>
        [Test]
        public void GetSentences_ManyTrailingChars()
        {
            Create("a?)]' b.");
            var ret = _motionUtil.GetSentences(SentenceKind.Default, Path.Forward, _snapshot.GetPoint(0));
            CollectionAssert.AreEquivalent(
                new[] { "a?)]'", "b." },
                ret.Select(x => x.GetText()).ToList());
        }

        /// <summary>
        /// The character should go on the previous sentence
        /// </summary>
        [Test]
        public void GetSentences_BackwardWithCharBetween()
        {
            Create("a?) b.");
            var ret = _motionUtil.GetSentences(SentenceKind.Default, Path.Backward, _snapshot.GetEndPoint());
            CollectionAssert.AreEquivalent(
                new[] { "b.", "a?)" },
                ret.Select(x => x.GetText()).ToList());
        }

        /// <summary>
        /// Not a sentence unless the end character is followed by a space / tab / newline
        /// </summary>
        [Test]
        public void GetSentences_NeedSpaceAfterEndCharacter()
        {
            Create("a!b. c");
            var ret = _motionUtil.GetSentences(SentenceKind.Default, Path.Forward, _snapshot.GetPoint(0));
            CollectionAssert.AreEquivalent(
                new[] { "a!b.", "c" },
                ret.Select(x => x.GetText()).ToList());
        }

        /// <summary>
        /// Only a valid boundary if the end character is followed by one of the 
        /// legal follow up characters (spaces, tabs, end of line after trailing chars)
        /// </summary>
        [Test]
        public void GetSentences_IncompleteBoundary()
        {
            Create("a!b. c");
            var ret = _motionUtil.GetSentences(SentenceKind.Default, Path.Backward, _snapshot.GetEndPoint());
            CollectionAssert.AreEquivalent(
                new[] { "c", "a!b." },
                ret.Select(x => x.GetText()).ToList());
        }

        /// <summary>
        /// Make sure blank lines are included as sentence boundaries.  Note: Only the first blank line in a series
        /// of lines is actually a sentence.  Every following blank is just white space in between the blank line
        /// sentence and the start of the next sentence
        /// </summary>
        [Test]
        public void GetSentences_ForwardBlankLinesAreBoundaries()
        {
            Create("a", "", "", "b");
            var ret = _motionUtil.GetSentences(SentenceKind.Default, Path.Forward, _snapshot.GetPoint(0));
            CollectionAssert.AreEquivalent(
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
        [Test]
        public void GetSentences_FromMiddleOfWord()
        {
            Create("dog", "cat", "bear");
            var ret = _motionUtil.GetSentences(SentenceKind.Default, Path.Forward, _snapshot.GetEndPoint().Subtract(1));
            CollectionAssert.AreEquivalent(
                new[] { "dog" + Environment.NewLine + "cat" + Environment.NewLine + "bear" },
                ret.Select(x => x.GetText()).ToList());
        }

        /// <summary>
        /// Don't include a sentence if we go backward from the first character of
        /// the sentence
        /// </summary>
        [Test]
        public void GetSentences_BackwardFromStartOfSentence()
        {
            Create("dog. cat");
            var ret = _motionUtil.GetSentences(SentenceKind.Default, Path.Backward, _textBuffer.GetPoint(4));
            CollectionAssert.AreEquivalent(
                new[] { "dog." },
                ret.Select(x => x.GetText()).ToList());
        }

        /// <summary>
        /// A blank line is a sentence
        /// </summary>
        [Test]
        public void GetSentences_BlankLinesAreSentences()
        {
            Create("dog.  ", "", "cat.");
            var ret = _motionUtil.GetSentences(SentenceKind.Default, Path.Forward, _textBuffer.GetPoint(0));
            CollectionAssert.AreEquivalent(
                new[] { "dog.", Environment.NewLine, "cat." },
                ret.Select(x => x.GetText()).ToList());
        }

        /// <summary>
        /// Break up the buffer into simple words
        /// </summary>
        [Test]
        public void GetWords_Normal()
        {
            Create("dog ca$$t $b");
            var ret = _motionUtil.GetWords(WordKind.NormalWord, Path.Forward, _textBuffer.GetPoint(0));
            CollectionAssert.AreEquivalent(
                new[] { "dog", "ca", "$$", "t", "$", "b" },
                ret.Select(x => x.GetText()).ToList());
        }

        /// <summary>
        /// A blank line should be a word 
        /// </summary>
        [Test]
        public void GetWords_BlankLine()
        {
            Create("dog cat", "", "bear");
            var ret = _motionUtil.GetWords(WordKind.NormalWord, Path.Forward, _textBuffer.GetPoint(0));
            CollectionAssert.AreEquivalent(
                new[] { "dog", "cat", Environment.NewLine, "bear" },
                ret.Select(x => x.GetText()).ToList());
        }

        /// <summary>
        /// From the middle of a word should return the span of the entire word
        /// </summary>
        [Test]
        public void GetWords_FromMiddleOfWord()
        {
            Create("dog cat");
            var ret = _motionUtil.GetWords(WordKind.NormalWord, Path.Forward, _textBuffer.GetPoint(1));
            CollectionAssert.AreEquivalent(
                new[] { "dog", "cat" },
                ret.Select(x => x.GetText()).ToList());
        }

        /// <summary>
        /// From the end of a word should return the span of the entire word
        /// </summary>
        [Test]
        public void GetWords_FromEndOfWord()
        {
            Create("dog cat");
            var ret = _motionUtil.GetWords(WordKind.NormalWord, Path.Forward, _textBuffer.GetPoint(2));
            CollectionAssert.AreEquivalent(
                new[] { "dog", "cat" },
                ret.Select(x => x.GetText()).ToList());
        }

        /// <summary>
        /// From the middle of a word backward
        /// </summary>
        [Test]
        public void GetWords_BackwardFromMiddle()
        {
            Create("dog cat");
            var ret = _motionUtil.GetWords(WordKind.NormalWord, Path.Backward, _textBuffer.GetPoint(5));
            CollectionAssert.AreEquivalent(
                new[] { "cat", "dog" },
                ret.Select(x => x.GetText()).ToList());
        }

        /// <summary>
        /// From the start of a word backward should not include that particular word 
        /// </summary>
        [Test]
        public void GetWords_BackwardFromStart()
        {
            Create("dog cat");
            var ret = _motionUtil.GetWords(WordKind.NormalWord, Path.Backward, _textBuffer.GetPoint(4));
            CollectionAssert.AreEquivalent(
                new[] { "dog" },
                ret.Select(x => x.GetText()).ToList());
        }

        /// <summary>
        /// Make sure that blank lines are counted as words when
        /// </summary>
        [Test]
        public void GetWords_BackwardBlankLine()
        {
            Create("dog", "", "cat");
            var ret = _motionUtil.GetWords(WordKind.NormalWord, Path.Backward, _textBuffer.GetLine(2).Start.Add(1));
            CollectionAssert.AreEquivalent(
                new[] { "cat", Environment.NewLine, "dog" },
                ret.Select(x => x.GetText()).ToList());
        }

        [Test]
        public void GetParagraphs_SingleBreak()
        {
            Create("a", "b", "", "c");
            var ret = _motionUtil.GetParagraphs(Path.Forward, _snapshot.GetPoint(0));
            CollectionAssert.AreEquivalent(
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
        [Test]
        public void GetParagraphs_ConsequtiveBreaks()
        {
            Create("a", "b", "", "", "c");
            var ret = _motionUtil.GetParagraphs(Path.Forward, _snapshot.GetPoint(0));
            CollectionAssert.AreEquivalent(
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
        [Test]
        public void GetParagraphs_FormFeedShouldBeBoundary()
        {
            Create("a", "b", "\f", "", "c");
            var ret = _motionUtil.GetParagraphs(Path.Forward, _snapshot.GetPoint(0));
            CollectionAssert.AreEquivalent(
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
        [Test]
        public void GetParagraphs_FormFeedIsNotConsequtive()
        {
            Create("a", "b", "\f", "", "c");
            var ret = _motionUtil.GetParagraphs(Path.Forward, _snapshot.GetPoint(0));
            CollectionAssert.AreEquivalent(
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
        [Test]
        public void GetParagraphs_MacroBreak()
        {
            Create("a", ".hh", "bear");
            _globalSettings.Paragraphs = "hh";
            var ret = _motionUtil.GetParagraphs(Path.Forward, _snapshot.GetPoint(0));
            CollectionAssert.AreEquivalent(
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
        [Test]
        public void GetParagraphs_MacroBreakLengthOne()
        {
            Create("a", ".j", "bear");
            _globalSettings.Paragraphs = "hhj ";
            var ret = _motionUtil.GetParagraphs(Path.Forward, _snapshot.GetPoint(0));
            CollectionAssert.AreEquivalent(
                new[]
                {
                    _textBuffer.GetLineRange(0, 0).ExtentIncludingLineBreak,
                    _textBuffer.GetLineRange(1,2).ExtentIncludingLineBreak
                },
                ret.ToList());
        }
    }

}
