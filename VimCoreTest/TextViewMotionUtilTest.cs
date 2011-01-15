using System;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.UnitTest;
using Vim.UnitTest.Mock;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class TextViewMotionUtilTest
    {
        static string[] s_lines = new string[]
            {
                "summary description for this line",
                "some other line",
                "running out of things to make up"
            };

        private ITextBuffer _buffer;
        private ITextView _textView;
        private ITextSnapshot _snapshot;
        private IVimLocalSettings _localSettings;
        private IVimGlobalSettings _settings;
        private TextViewMotionUtil _utilRaw;
        private ITextViewMotionUtil _util;

        [TearDown]
        public void TearDown()
        {
            _buffer = null;
        }

        public void Create(params string[] lines)
        {
            Create(EditorUtil.CreateView(lines));
        }

        public void Create(int caretPosition, params string[] lines)
        {
            Create(lines);
            _textView.MoveCaretTo(caretPosition);
        }

        public void Create(ITextView textView)
        {
            _textView = textView;
            _buffer = _textView.TextBuffer;
            _snapshot = _buffer.CurrentSnapshot;
            _buffer.Changed += delegate { _snapshot = _buffer.CurrentSnapshot; };
            _settings = new Vim.GlobalSettings();
            _localSettings = new LocalSettings(_settings, _textView);
            _utilRaw = new TextViewMotionUtil(_textView, _localSettings);
            _util = _utilRaw;
        }

        public void AssertData(
            MotionData data,
            SnapshotSpan? span,
            MotionKind motionKind = null,
            OperationKind operationKind = null)
        {
            if (span.HasValue)
            {
                Assert.AreEqual(span.Value, data.Span);
            }
            if (motionKind != null)
            {
                Assert.AreEqual(motionKind, data.MotionKind);
            }
            if (operationKind != null)
            {
                Assert.AreEqual(operationKind, data.OperationKind);
            }
        }

        [Test]
        public void WordForward1()
        {
            Create("foo bar");
            _textView.MoveCaretTo(0);
            var res = _util.WordForward(WordKind.NormalWord, 1);
            var span = res.Span;
            Assert.AreEqual(4, span.Length);
            Assert.AreEqual("foo ", span.GetText());
            Assert.AreEqual(MotionKind.Exclusive, res.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, res.OperationKind);
        }

        [Test]
        public void WordForward2()
        {
            Create("foo bar");
            _textView.MoveCaretTo(1);
            var res = _util.WordForward(WordKind.NormalWord, 1);
            var span = res.Span;
            Assert.AreEqual(3, span.Length);
            Assert.AreEqual("oo ", span.GetText());
        }

        [Test, Description("Word motion with a count")]
        public void WordForward3()
        {
            Create("foo bar baz");
            _textView.MoveCaretTo(0);
            var res = _util.WordForward(WordKind.NormalWord, 2);
            Assert.AreEqual("foo bar ", res.Span.GetText());
        }

        [Test, Description("Count across lines")]
        public void WordForward4()
        {
            Create("foo bar", "baz jaz");
            var res = _util.WordForward(WordKind.NormalWord, 3);
            Assert.AreEqual("foo bar" + Environment.NewLine + "baz ", res.Span.GetText());
        }

        [Test, Description("Count off the end of the buffer")]
        public void WordForward5()
        {
            Create("foo bar");
            var res = _util.WordForward(WordKind.NormalWord, 10);
            Assert.AreEqual("foo bar", res.Span.GetText());
        }

        [Test]
        public void EndOfLine1()
        {
            Create("foo bar", "baz");
            var res = _util.EndOfLine(1);
            var span = res.Span;
            Assert.AreEqual("foo bar", span.GetText());
            Assert.AreEqual(MotionKind.Inclusive, res.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, res.OperationKind);
        }

        [Test]
        public void EndOfLine2()
        {
            Create("foo bar", "baz");
            _textView.MoveCaretTo(1);
            var res = _util.EndOfLine(1);
            Assert.AreEqual("oo bar", res.Span.GetText());
        }

        [Test]
        public void EndOfLine3()
        {
            Create("foo", "bar", "baz");
            var res = _util.EndOfLine(2);
            Assert.AreEqual("foo" + Environment.NewLine + "bar", res.Span.GetText());
            Assert.AreEqual(MotionKind.Inclusive, res.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, res.OperationKind);
        }

        [Test]
        public void EndOfLine4()
        {
            Create("foo", "bar", "baz", "jar");
            var res = _util.EndOfLine(3);
            var tuple = res;
            Assert.AreEqual("foo" + Environment.NewLine + "bar" + Environment.NewLine + "baz", tuple.Span.GetText());
            Assert.AreEqual(MotionKind.Inclusive, tuple.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, tuple.OperationKind);
        }

        [Test, Description("Make sure counts past the end of the buffer don't crash")]
        public void EndOfLine5()
        {
            Create("foo");
            var res = _util.EndOfLine(300);
            Assert.AreEqual("foo", res.Span.GetText());
        }

        [Test]
        public void BeginingOfLine1()
        {
            Create("foo");
            _textView.MoveCaretTo(1);
            var data = _util.BeginingOfLine();
            Assert.AreEqual(new SnapshotSpan(_buffer.CurrentSnapshot, 0, 1), data.OperationSpan);
            Assert.AreEqual(MotionKind.Exclusive, data.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, data.OperationKind);
            Assert.IsFalse(data.IsForward);
        }

        [Test]
        public void BeginingOfLine2()
        {
            Create("foo");
            _textView.MoveCaretTo(2);
            var data = _util.BeginingOfLine();
            Assert.AreEqual(new SnapshotSpan(_buffer.CurrentSnapshot, 0, 2), data.OperationSpan);
            Assert.AreEqual(MotionKind.Exclusive, data.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, data.OperationKind);
            Assert.IsFalse(data.IsForward);
        }

        [Test]
        [Description("Go to begining even if there is whitespace")]
        public void BeginingOfLine3()
        {
            Create("  foo");
            _textView.MoveCaretTo(4);
            var data = _util.BeginingOfLine();
            Assert.AreEqual(new SnapshotSpan(_buffer.CurrentSnapshot, 0, 4), data.OperationSpan);
            Assert.AreEqual(MotionKind.Exclusive, data.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, data.OperationKind);
            Assert.IsFalse(data.IsForward);
        }

        [Test]
        public void FirstNonWhitespaceOnLine1()
        {
            Create("foo");
            _textView.MoveCaretTo(_buffer.GetLineFromLineNumber(0).End);
            var tuple = _util.FirstNonWhitespaceOnLine();
            Assert.AreEqual("foo", tuple.Span.GetText());
            Assert.AreEqual(MotionKind.Exclusive, tuple.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, tuple.OperationKind);
        }

        [Test, Description("Make sure it goes to the first non-whitespace character")]
        public void FirstNonWhitespaceOnLine2()
        {
            Create("  foo");
            _textView.MoveCaretTo(_buffer.GetLineFromLineNumber(0).End);
            var tuple = _util.FirstNonWhitespaceOnLine();
            Assert.AreEqual("foo", tuple.Span.GetText());
            Assert.AreEqual(MotionKind.Exclusive, tuple.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, tuple.OperationKind);
        }

        [Test, Description("Make sure to ignore tabs")]
        public void FirstNonWhitespaceOnLine3()
        {
            var text = "\tfoo";
            Create(text);
            _textView.MoveCaretTo(_buffer.GetLineFromLineNumber(0).End);
            var tuple = _util.FirstNonWhitespaceOnLine();
            Assert.AreEqual(text.IndexOf('f'), tuple.Span.Start);
            Assert.IsFalse(tuple.IsForward);
        }

        [Test]
        [Description("Make sure to move forward to the first non-whitespace")]
        public void FirstNonWhitespaceOnLine4()
        {
            Create(0, "   bar");
            var data = _util.FirstNonWhitespaceOnLine();
            Assert.AreEqual(_buffer.GetSpan(0, 3), data.Span);
        }

        [Test]
        [Description("Empty line case")]
        public void FirstNonWhitespaceOnLine5()
        {
            Create(0, "");
            var data = _util.FirstNonWhitespaceOnLine();
            Assert.AreEqual(_buffer.GetSpan(0, 0), data.Span);
        }

        [Test]
        [Description("Backwards case")]
        public void FirstNonWhitespaceOnLine6()
        {
            Create(3, "bar");
            var data = _util.FirstNonWhitespaceOnLine();
            Assert.AreEqual(_buffer.GetSpan(0, 3), data.Span);
            Assert.IsFalse(data.IsForward);
        }

        [Test]
        public void AllWord1()
        {
            Create("foo bar");
            var data = _util.AllWord(WordKind.NormalWord, 1);
            Assert.AreEqual("foo ", data.Span.GetText());
        }

        [Test]
        public void AllWord2()
        {
            Create("foo bar");
            _textView.MoveCaretTo(1);
            var data = _util.AllWord(WordKind.NormalWord, 1);
            Assert.AreEqual("foo ", data.Span.GetText());
        }

        [Test]
        public void AllWord3()
        {
            Create("foo bar baz");
            _textView.MoveCaretTo(1);
            var data = _util.AllWord(WordKind.NormalWord, 2);
            Assert.AreEqual("foo bar ", data.Span.GetText());
        }

        [Test]
        public void CharLeft1()
        {
            Create("foo bar");
            _textView.MoveCaretTo(2);
            var data = _util.CharLeft(2);
            Assert.IsTrue(data.IsSome());
            Assert.AreEqual("fo", data.Value.Span.GetText());
        }

        [Test]
        public void CharRight1()
        {
            Create("foo");
            var data = _util.CharRight(1);
            Assert.AreEqual("f", data.Value.Span.GetText());
            Assert.AreEqual(OperationKind.CharacterWise, data.Value.OperationKind);
            Assert.AreEqual(MotionKind.Exclusive, data.Value.MotionKind);
        }

        [Test]
        public void EndOfWord1()
        {
            Create("foo bar");
            var res = _util.EndOfWord(WordKind.NormalWord, 1);
            Assert.AreEqual(MotionKind.Inclusive, res.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, res.OperationKind);
            Assert.AreEqual(new SnapshotSpan(_buffer.CurrentSnapshot, 0, 3), res.Span);
        }

        [Test, Description("Needs to cross the end of the line")]
        public void EndOfWord2()
        {
            Create("foo   ", "bar");
            _textView.MoveCaretTo(4);
            var res = _util.EndOfWord(WordKind.NormalWord, 1);
            var span = new SnapshotSpan(
                _buffer.GetPoint(4),
                _buffer.GetLineFromLineNumber(1).Start.Add(3));
            Assert.AreEqual(span, res.Span);
            Assert.AreEqual(MotionKind.Inclusive, res.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, res.OperationKind);
        }

        [Test]
        public void EndOfWord3()
        {
            Create("foo bar baz jaz");
            var res = _util.EndOfWord(WordKind.NormalWord, 2);
            var span = new SnapshotSpan(_buffer.CurrentSnapshot, 0, 7);
            Assert.AreEqual(span, res.Span);
            Assert.AreEqual(MotionKind.Inclusive, res.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, res.OperationKind);
        }

        [Test, Description("Work across blank lines")]
        public void EndOfWord4()
        {
            Create("foo   ", "", "bar");
            _textView.MoveCaretTo(4);
            var res = _util.EndOfWord(WordKind.NormalWord, 1);
            var span = new SnapshotSpan(
                _buffer.GetPoint(4),
                _buffer.GetLineFromLineNumber(2).Start.Add(3));
            Assert.AreEqual(span, res.Span);
            Assert.AreEqual(MotionKind.Inclusive, res.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, res.OperationKind);
        }

        [Test, Description("Go off the end of the buffer")]
        public void EndOfWord5()
        {
            Create("foo   ", "", "bar");
            _textView.MoveCaretTo(4);
            var res = _util.EndOfWord(WordKind.NormalWord, 400);
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
            var res = _util.EndOfWord(WordKind.NormalWord, 1);
            Assert.AreEqual("o bar", res.Span.GetText());
        }

        [Test]
        public void EndOfWord7()
        {
            Create("foo", "bar");
            _textView.MoveCaretTo(2);
            var res = _util.EndOfWord(WordKind.NormalWord, 1);
            Assert.AreEqual("o" + Environment.NewLine + "bar", res.Span.GetText());
        }

        [Test]
        [Description("Second to last character")]
        public void EndOfWord8()
        {
            Create("the dog goes around the house");
            _textView.MoveCaretTo(1);
            Assert.AreEqual('h', _textView.GetCaretPoint().GetChar());
            var res = _util.EndOfWord(WordKind.NormalWord, 1);
            Assert.AreEqual("he", res.Span.GetText());
        }

        [Test]
        public void EndOfWord_DontStopOnPunctuation()
        {
            Create("A. the ball");
            _textView.MoveCaretTo(1);
            var data = _util.EndOfWord(WordKind.NormalWord, 1);
            Assert.AreEqual(". the", data.OperationSpan.GetText());
        }

        [Test]
        public void EndOfWord_DoublePunctuation()
        {
            Create("A.. the ball");
            _textView.MoveCaretTo(1);
            var data = _util.EndOfWord(WordKind.NormalWord, 1);
            Assert.AreEqual("..", data.OperationSpan.GetText());
        }

        [Test]
        public void EndOfWord_DoublePunctuationWithCount()
        {
            Create("A.. the ball");
            _textView.MoveCaretTo(1);
            var data = _util.EndOfWord(WordKind.NormalWord, 2);
            Assert.AreEqual(".. the", data.OperationSpan.GetText());
        }

        [Test]
        public void EndOfWord_DoublePunctuationIsAWord()
        {
            Create("A.. the ball");
            _textView.MoveCaretTo(0);
            var data = _util.EndOfWord(WordKind.NormalWord, 1);
            Assert.AreEqual("A..", data.OperationSpan.GetText());
        }

        [Test]
        public void EndOfWord_DontStopOnEndOfLine()
        {
            Create("A. ", "the ball");
            _textView.MoveCaretTo(1);
            var data = _util.EndOfWord(WordKind.NormalWord, 1);
            Assert.AreEqual(". " + Environment.NewLine + "the", data.OperationSpan.GetText());
        }

        [Test]
        public void ForwardChar1()
        {
            Create("foo bar baz");
            Assert.AreEqual("fo", _util.CharSearch('o', 1, CharSearch.ToChar, Direction.Forward).Value.Span.GetText());
            _textView.MoveCaretTo(1);
            Assert.AreEqual("oo", _util.CharSearch('o', 1, CharSearch.ToChar, Direction.Forward).Value.Span.GetText());
            _textView.MoveCaretTo(1);
            Assert.AreEqual("oo b", _util.CharSearch('b', 1, CharSearch.ToChar, Direction.Forward).Value.Span.GetText());
        }

        [Test]
        public void ForwardChar2()
        {
            Create("foo bar baz");
            var data = _util.CharSearch('q', 1, CharSearch.ToChar, Direction.Forward);
            Assert.IsTrue(data.IsNone());
        }

        [Test]
        public void ForwardChar3()
        {
            Create("foo bar baz");
            var data = _util.CharSearch('o', 1, CharSearch.ToChar, Direction.Forward).Value;
            Assert.AreEqual(MotionKind.Inclusive, data.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, data.OperationKind);
        }

        [Test, Description("Bad count gets nothing in gVim")]
        public void ForwardChar4()
        {
            Create("foo bar baz");
            var data = _util.CharSearch('o', 300, CharSearch.ToChar, Direction.Forward);
            Assert.IsTrue(data.IsNone());
        }

        [Test]
        public void ForwardTillChar1()
        {
            Create("foo bar baz");
            Assert.AreEqual("f", _util.CharSearch('o', 1, CharSearch.TillChar, Direction.Forward).Value.Span.GetText());
            Assert.AreEqual("foo ", _util.CharSearch('b', 1, CharSearch.TillChar, Direction.Forward).Value.Span.GetText());
        }

        [Test]
        public void ForwardTillChar2()
        {
            Create("foo bar baz");
            Assert.IsTrue(_util.CharSearch('q', 1, CharSearch.TillChar, Direction.Forward).IsNone());
        }

        [Test]
        public void ForwardTillChar3()
        {
            Create("foo bar baz");
            Assert.AreEqual("fo", _util.CharSearch('o', 2, CharSearch.TillChar, Direction.Forward).Value.Span.GetText());
        }

        [Test, Description("Bad count gets nothing in gVim")]
        public void ForwardTillChar4()
        {
            Create("foo bar baz");
            Assert.IsTrue(_util.CharSearch('o', 300, CharSearch.TillChar, Direction.Forward).IsNone());
        }

        [Test]
        public void BackwardCharMotion1()
        {
            Create("the boy kicked the ball");
            _textView.MoveCaretTo(_buffer.GetLine(0).End);
            var data = _util.CharSearch('b', 1, CharSearch.ToChar, Direction.Backward).Value;
            Assert.AreEqual("ball", data.Span.GetText());
            Assert.AreEqual(MotionKind.Exclusive, data.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, data.OperationKind);
        }

        [Test]
        public void BackwardCharMotion2()
        {
            Create("the boy kicked the ball");
            _textView.MoveCaretTo(_buffer.GetLine(0).End);
            var data = _util.CharSearch('b', 2, CharSearch.ToChar, Direction.Backward).Value;
            Assert.AreEqual("boy kicked the ball", data.Span.GetText());
            Assert.AreEqual(MotionKind.Exclusive, data.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, data.OperationKind);
        }

        [Test]
        public void BackwardTillCharMotion1()
        {
            Create("the boy kicked the ball");
            _textView.MoveCaretTo(_buffer.GetLine(0).End);
            var data = _util.CharSearch('b', 1, CharSearch.TillChar, Direction.Backward).Value;
            Assert.AreEqual("all", data.Span.GetText());
            Assert.AreEqual(MotionKind.Exclusive, data.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, data.OperationKind);
        }

        [Test]
        public void BackwardTillCharMotion2()
        {
            Create("the boy kicked the ball");
            _textView.MoveCaretTo(_buffer.GetLine(0).End);
            var data = _util.CharSearch('b', 2, CharSearch.TillChar, Direction.Backward).Value;
            Assert.AreEqual("oy kicked the ball", data.Span.GetText());
            Assert.AreEqual(MotionKind.Exclusive, data.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, data.OperationKind);
        }

        [Test]
        public void LineOrFirstToFirstNonWhitespace1()
        {
            Create("foo", "bar", "baz");
            _textView.MoveCaretTo(_buffer.GetLine(1).Start);
            var data = _util.LineOrFirstToFirstNonWhitespace(FSharpOption.Create(0));
            Assert.AreEqual(_buffer.GetLineRange(0, 1).Extent, data.Span);
            Assert.IsFalse(data.IsForward);
            Assert.AreEqual(OperationKind.LineWise, data.OperationKind);
            Assert.AreEqual(MotionKind.Inclusive, data.MotionKind);
            Assert.AreEqual(0, data.Column.Value);
        }

        [Test]
        public void LineOrFirstToFirstNonWhitespace2()
        {
            Create("foo", "bar", "baz");
            var data = _util.LineOrFirstToFirstNonWhitespace(FSharpOption.Create(2));
            Assert.AreEqual(_buffer.GetLineRange(0, 1).Extent, data.Span);
            Assert.IsTrue(data.IsForward);
            Assert.AreEqual(OperationKind.LineWise, data.OperationKind);
            Assert.AreEqual(MotionKind.Inclusive, data.MotionKind);
            Assert.AreEqual(0, data.Column.Value);
        }

        [Test]
        public void LineOrFirstToFirstNonWhitespace3()
        {
            Create("foo", "  bar", "baz");
            var data = _util.LineOrFirstToFirstNonWhitespace(FSharpOption.Create(2));
            Assert.AreEqual(_buffer.GetLineRange(0, 1).Extent, data.Span);
            Assert.IsTrue(data.IsForward);
            Assert.AreEqual(OperationKind.LineWise, data.OperationKind);
            Assert.AreEqual(MotionKind.Inclusive, data.MotionKind);
            Assert.AreEqual(2, data.Column.Value);
        }

        [Test]
        public void LineOrFirstToFirstNonWhitespace4()
        {
            Create("foo", "  bar", "baz");
            var data = _util.LineOrFirstToFirstNonWhitespace(FSharpOption.Create(500));
            Assert.AreEqual(_buffer.GetLineRange(0, 0).Extent, data.Span);
            Assert.IsTrue(data.IsForward);
            Assert.AreEqual(OperationKind.LineWise, data.OperationKind);
            Assert.AreEqual(MotionKind.Inclusive, data.MotionKind);
            Assert.AreEqual(0, data.Column.Value);
        }

        [Test]
        public void LineOrFirstToFirstNonWhitespace5()
        {
            Create("  the", "dog", "jumped");
            _textView.MoveCaretTo(_textView.GetLine(1).Start);
            var data = _util.LineOrFirstToFirstNonWhitespace(FSharpOption<int>.None);
            Assert.AreEqual(0, data.Span.Start.Position);
            Assert.AreEqual(2, data.Column.Value);
            Assert.IsFalse(data.IsForward);
        }

        [Test]
        public void LineOrLastToFirstNonWhitespace1()
        {
            Create("foo", "bar", "baz");
            var data = _util.LineOrLastToFirstNonWhitespace(FSharpOption.Create(2));
            Assert.AreEqual(_buffer.GetLineRange(0, 1).Extent, data.Span);
            Assert.IsTrue(data.IsForward);
            Assert.AreEqual(OperationKind.LineWise, data.OperationKind);
            Assert.AreEqual(MotionKind.Inclusive, data.MotionKind);
            Assert.AreEqual(0, data.Column.Value);
        }

        [Test]
        public void LineOrLastToFirstNonWhitespace2()
        {
            Create("foo", "bar", "baz");
            _textView.MoveCaretTo(_buffer.GetLine(1).Start);
            var data = _util.LineOrLastToFirstNonWhitespace(FSharpOption.Create(0));
            Assert.AreEqual(_buffer.GetLineRange(0, 1).Extent, data.Span);
            Assert.IsFalse(data.IsForward);
            Assert.AreEqual(OperationKind.LineWise, data.OperationKind);
            Assert.AreEqual(MotionKind.Inclusive, data.MotionKind);
            Assert.AreEqual(0, data.Column.Value);
        }

        [Test]
        public void LineOrLastToFirstNonWhitespace3()
        {
            Create("foo", "bar", "baz");
            _textView.MoveCaretTo(_buffer.GetLine(1).Start);
            var data = _util.LineOrLastToFirstNonWhitespace(FSharpOption.Create(500));
            Assert.AreEqual(_buffer.GetLineRange(1, 2).Extent, data.Span);
            Assert.IsTrue(data.IsForward);
            Assert.AreEqual(OperationKind.LineWise, data.OperationKind);
            Assert.AreEqual(MotionKind.Inclusive, data.MotionKind);
            Assert.AreEqual(0, data.Column.Value);
        }

        [Test]
        public void LineOrLastToFirstNonWhitespace4()
        {
            Create("foo", "bar", "baz");
            var data = _util.LineOrLastToFirstNonWhitespace(FSharpOption<int>.None);
            var span = new SnapshotSpan(_buffer.CurrentSnapshot, 0, _buffer.CurrentSnapshot.Length);
            Assert.AreEqual(span, data.Span);
            Assert.IsTrue(data.IsForward);
            Assert.AreEqual(OperationKind.LineWise, data.OperationKind);
            Assert.AreEqual(MotionKind.Inclusive, data.MotionKind);
            Assert.AreEqual(0, data.Column.Value);
        }

        [Test]
        public void LastNonWhitespaceOnLine1()
        {
            Create("foo", "bar ");
            var data = _util.LastNonWhitespaceOnLine(1);
            Assert.AreEqual(_buffer.GetLineRange(0).Extent, data.Span);
            Assert.IsTrue(data.IsForward);
            Assert.AreEqual(OperationKind.CharacterWise, data.OperationKind);
            Assert.AreEqual(MotionKind.Inclusive, data.MotionKind);
        }

        [Test]
        public void LastNonWhitespaceOnLine2()
        {
            Create("foo", "bar ", "jaz");
            var data = _util.LastNonWhitespaceOnLine(2);
            Assert.AreEqual(new SnapshotSpan(_buffer.GetPoint(0), _buffer.GetLine(1).Start.Add(3)), data.Span);
            Assert.IsTrue(data.IsForward);
            Assert.AreEqual(OperationKind.CharacterWise, data.OperationKind);
            Assert.AreEqual(MotionKind.Inclusive, data.MotionKind);
        }

        [Test]
        public void LastNonWhitespaceOnLine3()
        {
            Create("foo", "bar ", "jaz", "");
            var data = _util.LastNonWhitespaceOnLine(300);
            Assert.AreEqual(new SnapshotSpan(_buffer.CurrentSnapshot, 0, _buffer.CurrentSnapshot.Length), data.Span);
            Assert.IsTrue(data.IsForward);
            Assert.AreEqual(OperationKind.CharacterWise, data.OperationKind);
            Assert.AreEqual(MotionKind.Inclusive, data.MotionKind);
        }

        [Test]
        public void LineFromTopOfVisibleWindow1()
        {
            var buffer = EditorUtil.CreateBuffer("foo", "bar", "baz");
            var tuple = MockObjectFactory.CreateTextViewWithVisibleLines(buffer, 0, 1);
            Create(tuple.Item1.Object);
            var data = _util.LineFromTopOfVisibleWindow(FSharpOption<int>.None);
            Assert.AreEqual(buffer.GetLineRange(0).Extent, data.Span);
            Assert.AreEqual(MotionKind.Inclusive, data.MotionKind);
            Assert.AreEqual(OperationKind.LineWise, data.OperationKind);
            Assert.IsTrue(data.IsForward);
        }

        [Test]
        public void LineFromTopOfVisibleWindow2()
        {
            var buffer = EditorUtil.CreateBuffer("foo", "bar", "baz", "jazz");
            var tuple = MockObjectFactory.CreateTextViewWithVisibleLines(buffer, 0, 2);
            Create(tuple.Item1.Object);
            var data = _util.LineFromTopOfVisibleWindow(FSharpOption.Create(2));
            Assert.AreEqual(buffer.GetLineRange(0, 1).Extent, data.Span);
            Assert.AreEqual(MotionKind.Inclusive, data.MotionKind);
            Assert.AreEqual(OperationKind.LineWise, data.OperationKind);
            Assert.IsTrue(data.IsForward);
        }

        [Test]
        [Description("From visible line not caret point")]
        public void LineFromTopOfVisibleWindow3()
        {
            var buffer = EditorUtil.CreateBuffer("foo", "bar", "baz", "jazz");
            var tuple = MockObjectFactory.CreateTextViewWithVisibleLines(buffer, 0, 2, caretPosition: buffer.GetLine(2).Start.Position);
            Create(tuple.Item1.Object);
            var data = _util.LineFromTopOfVisibleWindow(FSharpOption.Create(2));
            Assert.AreEqual(buffer.GetLineRange(0, 1).Extent, data.Span);
            Assert.AreEqual(MotionKind.Inclusive, data.MotionKind);
            Assert.AreEqual(OperationKind.LineWise, data.OperationKind);
            Assert.IsFalse(data.IsForward);
        }

        [Test]
        public void LineFromTopOfVisibleWindow4()
        {
            var buffer = EditorUtil.CreateBuffer("  foo", "bar");
            var tuple = MockObjectFactory.CreateTextViewWithVisibleLines(buffer, 0, 1, caretPosition: buffer.GetLine(1).End);
            Create(tuple.Item1.Object);
            var data = _util.LineFromTopOfVisibleWindow(FSharpOption<int>.None);
            Assert.AreEqual(2, data.Column.Value);
        }

        [Test]
        public void LineFromTopOfVisibleWindow5()
        {
            var buffer = EditorUtil.CreateBuffer("  foo", "bar");
            var tuple = MockObjectFactory.CreateTextViewWithVisibleLines(buffer, 0, 1, caretPosition: buffer.GetLine(1).End);
            Create(tuple.Item1.Object);
            _settings.StartOfLine = false;
            var data = _util.LineFromTopOfVisibleWindow(FSharpOption<int>.None);
            Assert.IsTrue(data.Column.IsNone());
        }

        [Test]
        public void LineFromBottomOfVisibleWindow1()
        {
            var buffer = EditorUtil.CreateBuffer("a", "b", "c", "d");
            var tuple = MockObjectFactory.CreateTextViewWithVisibleLines(buffer, 0, 2);
            Create(tuple.Item1.Object);
            var data = _util.LineFromBottomOfVisibleWindow(FSharpOption<int>.None);
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
            var data = _util.LineFromBottomOfVisibleWindow(FSharpOption.Create(2));
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
            var data = _util.LineFromBottomOfVisibleWindow(FSharpOption.Create(2));
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
            var data = _util.LineFromBottomOfVisibleWindow(FSharpOption<int>.None);
            Assert.AreEqual(2, data.Column.Value);
        }

        [Test]
        public void LineFromBottomOfVisibleWindow5()
        {
            var buffer = EditorUtil.CreateBuffer("a", "b", "  c", "d");
            var tuple = MockObjectFactory.CreateTextViewWithVisibleLines(buffer, 0, 2);
            Create(tuple.Item1.Object);
            _settings.StartOfLine = false;
            var data = _util.LineFromBottomOfVisibleWindow(FSharpOption<int>.None);
            Assert.IsTrue(data.Column.IsNone());
        }

        [Test]
        public void LineFromMiddleOfWindow1()
        {
            var buffer = EditorUtil.CreateBuffer("a", "b", "c", "d");
            var tuple = MockObjectFactory.CreateTextViewWithVisibleLines(buffer, 0, 2);
            Create(tuple.Item1.Object);
            var data = _util.LineInMiddleOfVisibleWindow();
            Assert.AreEqual(new SnapshotSpan(_buffer.GetPoint(0), _buffer.GetLine(1).End), data.Span);
            Assert.AreEqual(OperationKind.LineWise, data.OperationKind);
        }

        [Test]
        public void LineDownToFirstNonWhitespace1()
        {
            Create("a", "b", "c", "d");
            var data = _util.LineDownToFirstNonWhitespace(1);
            Assert.AreEqual(MotionKind.Inclusive, data.MotionKind);
            Assert.AreEqual(OperationKind.LineWise, data.OperationKind);
            Assert.AreEqual(_buffer.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
            Assert.IsTrue(data.IsForward);
        }

        [Test]
        public void LineDownToFirstNonWhitespace2()
        {
            Create("a", "b", "c", "d");
            var data = _util.LineDownToFirstNonWhitespace(2);
            Assert.AreEqual(MotionKind.Inclusive, data.MotionKind);
            Assert.AreEqual(OperationKind.LineWise, data.OperationKind);
            Assert.AreEqual(_buffer.GetLineRange(0, 2).ExtentIncludingLineBreak, data.Span);
            Assert.IsTrue(data.IsForward);
        }

        [Test]
        [Description("Count of 0 is valid for this motion")]
        public void LineDownToFirstNonWhitespace3()
        {
            Create("a", "b", "c", "d");
            var data = _util.LineDownToFirstNonWhitespace(0);
            Assert.AreEqual(MotionKind.Inclusive, data.MotionKind);
            Assert.AreEqual(OperationKind.LineWise, data.OperationKind);
            Assert.AreEqual(_buffer.GetLineRange(0).ExtentIncludingLineBreak, data.Span);
            Assert.IsTrue(data.IsForward);
        }

        [Test]
        [Description("This is a linewise motion and should return line spans")]
        public void LineDownToFirstNonWhitespace4()
        {
            Create("cat", "dog", "bird");
            _textView.MoveCaretTo(1);
            var data = _util.LineDownToFirstNonWhitespace(1);
            var span = _textView.GetLineRange(0, 1).ExtentIncludingLineBreak;
            Assert.AreEqual(span, data.Span);
        }

        [Test]
        public void LineDownToFirstNonWhitespace5()
        {
            Create("cat", "  dog", "bird");
            _textView.MoveCaretTo(1);
            var data = _util.LineDownToFirstNonWhitespace(1);
            Assert.IsTrue(data.Column.IsSome());
            Assert.AreEqual(2, data.Column.Value);
        }

        [Test]
        public void LineDownToFirstNonWhitespace6()
        {
            Create("cat", "  dog and again", "bird");
            _textView.MoveCaretTo(1);
            var data = _util.LineDownToFirstNonWhitespace(1);
            Assert.IsTrue(data.Column.IsSome());
            Assert.AreEqual(2, data.Column.Value);
        }

        [Test]
        public void LineDownToFirstNonWhitespaceg()
        {
            Create("cat", "  dog and again", " here bird again");
            _textView.MoveCaretTo(1);
            var data = _util.LineDownToFirstNonWhitespace(2);
            Assert.IsTrue(data.Column.IsSome());
            Assert.AreEqual(1, data.Column.Value);
        }

        [Test]
        public void LineDown1()
        {
            Create("dog", "cat", "bird");
            var data = _util.LineDown(1);
            AssertData(
                data,
                _buffer.GetLineRange(0, 1).ExtentIncludingLineBreak,
                MotionKind.Inclusive,
                OperationKind.LineWise);
        }

        [Test]
        public void LineDown2()
        {
            Create("dog", "cat", "bird");
            var data = _util.LineDown(2);
            AssertData(
                data,
                _buffer.GetLineRange(0, 2).ExtentIncludingLineBreak,
                MotionKind.Inclusive,
                OperationKind.LineWise);
        }

        [Test]
        public void LineUp1()
        {
            Create("dog", "cat", "bird", "horse");
            _textView.MoveCaretTo(_textView.GetLine(2).Start);
            var data = _util.LineUp(1);
            AssertData(
                data,
                _buffer.GetLineRange(1, 2).ExtentIncludingLineBreak,
                MotionKind.Inclusive,
                OperationKind.LineWise);
        }

        [Test]
        public void LineUp2()
        {
            Create("dog", "cat", "bird", "horse");
            _textView.MoveCaretTo(_textView.GetLine(2).Start);
            var data = _util.LineUp(2);
            AssertData(
                data,
                _buffer.GetLineRange(0, 2).ExtentIncludingLineBreak,
                MotionKind.Inclusive,
                OperationKind.LineWise);
        }

        [Test]
        public void LineUp3()
        {
            Create("foo", "bar");
            _textView.MoveCaretTo(_buffer.GetLineFromLineNumber(1).Start);
            var data = _util.LineUp(1);
            Assert.AreEqual(OperationKind.LineWise, data.OperationKind);
            Assert.AreEqual("foo" + Environment.NewLine + "bar", data.Span.GetText());
        }

        [Test]
        public void SectionForward1()
        {
            Create(0, "dog", "\fpig", "{fox");
            var data = _util.SectionForward(MotionContext.Movement, 1);
            Assert.AreEqual(_textView.GetLineRange(0).ExtentIncludingLineBreak, data.Span);
            Assert.AreEqual(0, data.Column.Value);
        }

        [Test]
        public void SectionForward2()
        {
            Create(0, "dog", "\fpig", "fox");
            var data = _util.SectionForward(MotionContext.Movement, 2);
            Assert.AreEqual(new SnapshotSpan(_snapshot, 0, _snapshot.Length), data.Span);
            Assert.AreEqual(0, data.Column.Value);
        }

        [Test]
        public void SectionForward3()
        {
            Create(0, "dog", "{pig", "fox");
            var data = _util.SectionForward(MotionContext.Movement, 2);
            Assert.AreEqual(new SnapshotSpan(_snapshot, 0, _snapshot.Length), data.Span);
            Assert.AreEqual(0, data.Column.Value);
        }

        [Test]
        public void SectionForward4()
        {
            Create(0, "dog", "{pig", "{fox");
            var data = _util.SectionForward(MotionContext.Movement, 1);
            Assert.AreEqual(_textView.GetLineRange(0).ExtentIncludingLineBreak, data.Span);
            Assert.AreEqual(0, data.Column.Value);
        }

        [Test]
        public void SectionForward5()
        {
            Create(0, "dog", "}pig", "fox");
            var data = _util.SectionForward(MotionContext.AfterOperator, 1);
            Assert.AreEqual(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
            Assert.AreEqual(0, data.Column.Value);
        }

        [Test]
        [Description("Only look for } after an operator")]
        public void SectionForward6()
        {
            Create(0, "dog", "}pig", "fox");
            var data = _util.SectionForward(MotionContext.Movement, 1);
            Assert.AreEqual(new SnapshotSpan(_snapshot, 0, _snapshot.Length), data.Span);
            Assert.AreEqual(0, data.Column.Value);
        }

        [Test]
        public void SectionBackwardOrOpenBrace1()
        {
            Create(0, "dog", "{brace", "pig", "}fox");
            var data = _util.SectionBackwardOrOpenBrace(1);
            Assert.IsTrue(data.Span.IsEmpty);
        }

        [Test]
        public void SectionBackwardOrOpenBrace2()
        {
            Create("dog", "{brace", "pig", "}fox");
            _textView.MoveCaretTo(_textView.GetLine(2).Start.Position);
            var data = _util.SectionBackwardOrOpenBrace(1);
            Assert.AreEqual(_textView.GetLineRange(1).ExtentIncludingLineBreak, data.Span);
        }

        [Test]
        public void SectionBackwardOrOpenBrace3()
        {
            Create("dog", "{brace", "pig", "}fox");
            _textView.MoveCaretTo(_textView.GetLine(2).Start.Position);
            var data = _util.SectionBackwardOrOpenBrace(2);
            Assert.AreEqual(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
        }

        [Test]
        public void SectionBackwardOrOpenBrace4()
        {
            Create(0, "dog", "\fbrace", "pig", "}fox");
            var data = _util.SectionBackwardOrOpenBrace(1);
            Assert.IsTrue(data.Span.IsEmpty);
        }

        [Test]
        public void SectionBackwardOrOpenBrace5()
        {
            Create("dog", "\fbrace", "pig", "}fox");
            _textView.MoveCaretTo(_textView.GetLine(2).Start.Position);
            var data = _util.SectionBackwardOrOpenBrace(1);
            Assert.AreEqual(_textView.GetLineRange(1).ExtentIncludingLineBreak, data.Span);
        }

        [Test]
        public void SectionBackwardOrOpenBrace6()
        {
            Create("dog", "\fbrace", "pig", "}fox");
            _textView.MoveCaretTo(_textView.GetLine(2).Start.Position);
            var data = _util.SectionBackwardOrOpenBrace(2);
            Assert.AreEqual(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
        }

        [Test]
        [Description("Ignore the brace not on first column")]
        public void SectionBackwardOrOpenBrace7()
        {
            Create("dog", "\f{brace", "pig", "}fox");
            _textView.MoveCaretTo(_textView.GetLine(2).Start.Position);
            var data = _util.SectionBackwardOrOpenBrace(2);
            Assert.AreEqual(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
        }

        [Test]
        public void SectionBackwardOrOpenBrace8()
        {
            Create("dog", "{{foo", "{bar", "hello");
            _textView.MoveCaretTo(_textView.GetLine(2).End);
            var data = _util.SectionBackwardOrOpenBrace(2);
            Assert.AreEqual(
                new SnapshotSpan(
                    _buffer.GetLine(1).Start,
                    _buffer.GetLine(2).End),
                data.Span);
        }

        [Test]
        public void SectionBackwardOrCloseBrace1()
        {
            Create(0, "dog", "}brace", "pig", "}fox");
            var data = _util.SectionBackwardOrCloseBrace(1);
            Assert.IsTrue(data.Span.IsEmpty);
        }

        [Test]
        public void SectionBackwardOrCloseBrace2()
        {
            Create("dog", "}brace", "pig", "}fox");
            _textView.MoveCaretTo(_textView.GetLine(2).Start.Position);
            var data = _util.SectionBackwardOrCloseBrace(1);
            Assert.AreEqual(_textView.GetLineRange(1).ExtentIncludingLineBreak, data.Span);
        }

        [Test]
        public void SectionBackwardOrCloseBrace3()
        {
            Create("dog", "}brace", "pig", "}fox");
            _textView.MoveCaretTo(_textView.GetLine(2).Start.Position);
            var data = _util.SectionBackwardOrCloseBrace(2);
            Assert.AreEqual(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
        }

        [Test]
        public void SectionBackwardOrCloseBrace4()
        {
            Create(0, "dog", "\fbrace", "pig", "}fox");
            var data = _util.SectionBackwardOrCloseBrace(1);
            Assert.IsTrue(data.Span.IsEmpty);
        }

        [Test]
        public void SectionBackwardOrCloseBrace5()
        {
            Create("dog", "\fbrace", "pig", "}fox");
            _textView.MoveCaretTo(_textView.GetLine(2).Start.Position);
            var data = _util.SectionBackwardOrCloseBrace(1);
            Assert.AreEqual(_textView.GetLineRange(1).ExtentIncludingLineBreak, data.Span);
        }

        [Test]
        public void SectionBackwardOrCloseBrace6()
        {
            Create("dog", "\fbrace", "pig", "}fox");
            _textView.MoveCaretTo(_textView.GetLine(2).Start.Position);
            var data = _util.SectionBackwardOrCloseBrace(2);
            Assert.AreEqual(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
        }

        [Test]
        [Description("Ignore the brace not on first column")]
        public void SectionBackwardOrCloseBrace7()
        {
            Create("dog", "\f}brace", "pig", "}fox");
            _textView.MoveCaretTo(_textView.GetLine(2).Start.Position);
            var data = _util.SectionBackwardOrCloseBrace(2);
            Assert.AreEqual(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
        }

        [Test]
        [Description("End of buffer should grab the last character")]
        public void ParagraphForward1()
        {
            Create("dog", "pig", "cat");
            _textView.MoveCaretTo(_textView.TextSnapshot.GetEndPoint());
            var data = _util.ParagraphForward(1);
            Assert.AreEqual("t", data.Span.GetText());
        }

        [Test]
        [Description("End of buffer should grab the last character even with VirtualEdit=onemore")]
        public void ParagraphForward2()
        {
            Create("dog", "pig", "cat");
            _settings.VirtualEdit = "onemore";
            _textView.MoveCaretTo(_textView.TextSnapshot.GetEndPoint());
            var data = _util.ParagraphForward(1);
            Assert.AreEqual("t", data.Span.GetText());
        }

        [Test]
        public void ParagraphBackward1()
        {
            Create(0, "dog", "pig", "cat");
            var data = _util.ParagraphBackward(1);
            Assert.IsTrue(data.Span.IsEmpty);
        }

        [Test]
        public void QuotedString1()
        {
            Create(@"""foo""");
            var data = _util.QuotedString();
            Assert.IsTrue(data.IsSome());
            AssertData(data.Value, new SnapshotSpan(_snapshot, 0, 5), MotionKind.Inclusive, OperationKind.CharacterWise);
        }

        [Test]
        [Description("Include the leading whitespace")]
        public void QuotedString2()
        {
            Create(@"  ""foo""");
            var data = _util.QuotedString();
            Assert.IsTrue(data.IsSome());
            AssertData(data.Value, new SnapshotSpan(_snapshot, 0, 7), MotionKind.Inclusive, OperationKind.CharacterWise);
        }

        [Test]
        [Description("Include the trailing whitespace")]
        public void QuotedString3()
        {
            Create(@"""foo""  ");
            var data = _util.QuotedString();
            Assert.IsTrue(data.IsSome());
            AssertData(data.Value, new SnapshotSpan(_snapshot, 0, 7), MotionKind.Inclusive, OperationKind.CharacterWise);
        }

        [Test]
        [Description("Favor the trailing whitespace over leading")]
        public void QuotedString4()
        {
            Create(@"  ""foo""  ");
            var data = _util.QuotedString();
            Assert.IsTrue(data.IsSome());
            AssertData(data.Value, new SnapshotSpan(_snapshot, 2, 7), MotionKind.Inclusive, OperationKind.CharacterWise);
        }

        [Test]
        [Description("Ignore the escaped quotes")]
        public void QuotedString5()
        {
            Create(@"""foo\""""");
            var data = _util.QuotedString();
            Assert.IsTrue(data.IsSome());
            AssertData(data.Value, new SnapshotSpan(_snapshot, 0, 7), MotionKind.Inclusive, OperationKind.CharacterWise);
        }

        [Test]
        [Description("Ignore the escaped quotes")]
        public void QuotedString6()
        {
            Create(@"""foo(""""");
            _localSettings.QuoteEscape = @"(";
            var data = _util.QuotedString();
            Assert.IsTrue(data.IsSome());
            AssertData(data.Value, new SnapshotSpan(_snapshot, 0, 7), MotionKind.Inclusive, OperationKind.CharacterWise);
        }

        [Test]
        public void QuotedString7()
        {
            Create(@"foo");
            var data = _util.QuotedString();
            Assert.IsTrue(data.IsNone());
        }

        [Test]
        public void QuotedString8()
        {
            Create(@"""foo"" ""bar""");
            var start = _snapshot.GetText().IndexOf('b');
            _textView.MoveCaretTo(start);
            var data = _util.QuotedString();
            Assert.IsTrue(data.IsSome());
            AssertData(data.Value, new SnapshotSpan(_snapshot, start - 2, 6), MotionKind.Inclusive, OperationKind.CharacterWise);
        }

        [Test]
        public void QuotedStringContents1()
        {
            Create(@"""foo""");
            var data = _util.QuotedStringContents();
            Assert.IsTrue(data.IsSome());
            AssertData(data.Value, new SnapshotSpan(_snapshot, 1, 3), MotionKind.Inclusive, OperationKind.CharacterWise);
        }

        [Test]
        public void QuotedStringContents2()
        {
            Create(@" ""bar""");
            var data = _util.QuotedStringContents();
            Assert.IsTrue(data.IsSome());
            AssertData(data.Value, new SnapshotSpan(_snapshot, 2, 3), MotionKind.Inclusive, OperationKind.CharacterWise);
        }

        [Test]
        public void QuotedStringContents3()
        {
            Create(@"""foo"" ""bar""");
            var start = _snapshot.GetText().IndexOf('b');
            _textView.MoveCaretTo(start);
            var data = _util.QuotedStringContents();
            Assert.IsTrue(data.IsSome());
            AssertData(data.Value, new SnapshotSpan(_snapshot, start, 3), MotionKind.Inclusive, OperationKind.CharacterWise);
        }

        [Test]
        public void SentencesForward1()
        {
            Create("a! b");
            var data = _util.SentenceForward(1);
            AssertData(data, new SnapshotSpan(_snapshot, 0, 2));
        }

        [Test]
        [Description("Don't return anything when at the end of the buffer")]
        public void SentencesForward2()
        {
            Create("a! b");
            _textView.MoveCaretTo(_snapshot.Length);
            var data = _util.SentenceForward(1);
            AssertData(data, new SnapshotSpan(_snapshot, _snapshot.Length, 0));
        }

    }

}
