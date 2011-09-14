using System.Linq;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using NUnit.Framework;
using Vim;
using Vim.Modes.Command;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    [TestFixture]
    public sealed class RangeUtilTest : VimTestBase
    {
        private ITextView _textView;
        private ITextBuffer _textBuffer;
        private IVimTextBuffer _vimTextBuffer;
        private RangeUtil _rangeUtil;

        private void Create(params string[] lines)
        {
            _textView = CreateTextView(lines);
            _textBuffer = _textView.TextBuffer;
            var vimBufferData = CreateVimBufferData(_textView);
            _vimTextBuffer = vimBufferData.VimTextBuffer;
            _rangeUtil = new RangeUtil(vimBufferData, CommonOperationsFactory.GetCommonOperations(vimBufferData));
        }

        private ParseRangeResult Parse(string input)
        {
            return CaptureComplete(input);
        }

        private void ParseLineRange(string input, int startLine, int endLine, int contextLine = 0)
        {
            _textView.MoveCaretToLine(contextLine);
            var ret = Parse(input);
            Assert.IsTrue(ret.IsSucceeded);
            Assert.AreEqual(0, ret.AsSucceeded().Item2.Count());
            var range = ret.AsSucceeded().Item1;
            Assert.AreEqual(startLine, range.StartLineNumber);
            Assert.AreEqual(endLine, range.EndLineNumber);
        }

        private void ParseSingleLine(string input, int line)
        {
            var ret = Parse(input);
            Assert.IsTrue(ret.IsSucceeded);
            var range = ret.AsSucceeded().Item1;
            Assert.AreEqual(1, range.Count);
            Assert.AreEqual(line, range.StartLineNumber);
        }

        private ParseRangeResult CaptureComplete(string input)
        {
            return _rangeUtil.ParseRange(ListModule.OfSeq(input));
        }

        [Test]
        public void FullFile()
        {
            Create("foo", "bar");
            var res = Parse("%");
            var tss = _textBuffer.CurrentSnapshot;
            Assert.IsTrue(res.IsSucceeded);
            Assert.AreEqual(new SnapshotSpan(tss, 0, tss.Length), res.AsSucceeded().Item1.ExtentIncludingLineBreak);
        }

        [Test]
        public void FullFile2()
        {
            Create("foo", "bar");
            var res = Parse("%bar");
            Assert.IsTrue(res.IsSucceeded);
            var range = res.AsSucceeded().Item1;
            Assert.AreEqual(new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, _textBuffer.CurrentSnapshot.Length), range.ExtentIncludingLineBreak);
            Assert.IsTrue("bar".SequenceEqual(res.AsSucceeded().Item2));
        }

        [Test]
        public void CurrentLine1()
        {
            Create("foo", "bar");
            var res = Parse(".");
            Assert.IsTrue(res.IsSucceeded);
            Assert.AreEqual(_textBuffer.GetLineRange(0), res.AsSucceeded().Item1);
        }

        [Test]
        public void CurrentLine2()
        {
            Create("foo", "bar");
            var res = Parse(".,.");
            Assert.IsTrue(res.IsSucceeded);
            Assert.AreEqual(_textBuffer.GetLineRange(0), res.AsSucceeded().Item1);
        }

        [Test]
        public void CurrentLine3()
        {
            Create("foo", "bar");
            var res = Parse(".foo");
            Assert.IsTrue(res.IsSucceeded);

            var range = res.AsSucceeded();
            Assert.AreEqual(_textBuffer.GetLineRange(0), res.AsSucceeded().Item1);
            Assert.AreEqual('f', range.Item2.First());
        }

        [Test]
        public void LineNumber1()
        {
            Create("a", "b", "c");
            var res = Parse("1,2");
            Assert.IsTrue(res.IsSucceeded);
            Assert.AreEqual(_textBuffer.GetLineRange(0, 1), res.AsSucceeded().Item1);
        }

        [Test]
        public void ApplyCount1()
        {
            Create("foo", "bar", "baz", "jaz");
            var first = _textBuffer.GetLineRange(0);
            var second = _rangeUtil.ApplyCount(2, first);
            Assert.AreEqual(_textBuffer.GetLineRange(0, 1), second);
        }

        [Test, Description("Count is bound to end of the file")]
        public void ApplyCount2()
        {
            Create("foo", "bar");
            var v1 = _textBuffer.GetLineRange(0);
            var v2 = _rangeUtil.ApplyCount(200, v1);
            Assert.AreEqual(_textBuffer.GetLineRange(0, 1), v2);
        }

        [Test]
        public void ApplyCount3()
        {
            Create("foo", "bar", "baz");
            var v1 = _textBuffer.GetLineRange(0);
            var v2 = _rangeUtil.ApplyCount(2, v1);
            Assert.AreEqual(_textBuffer.GetLineRange(0, 1), v2);
        }

        [Test]
        [Description("Count of 1 should go to the last line in the range")]
        public void Apply_CountOfOne()
        {
            Create("cat", "dog", "rabbit", "tree");
            var v1 = _textBuffer.GetLineRange(0, 1);
            var v2 = _rangeUtil.ApplyCount(1, v1);
            Assert.AreEqual(SnapshotLineRangeUtil.CreateForLine(_textBuffer.GetLine(1)), v2);
        }

        [Test]
        public void SingleLine1()
        {
            Create("foo", "bar");
            var res = Parse("1");
            Assert.IsTrue(res.IsSucceeded);
            Assert.AreEqual(0, res.AsSucceeded().Item1.StartLineNumber);
        }

        [Test]
        public void RangeOrCurrentLine1()
        {
            Create("foo");
            var res = _rangeUtil.RangeOrCurrentLine(FSharpOption<SnapshotLineRange>.None);
            Assert.AreEqual(_textView.GetLineRange(0), res);
        }

        [Test]
        public void RangeOrCurrentLine2()
        {
            Create("foo", "bar");
            var mock = new Moq.Mock<ITextView>(Moq.MockBehavior.Strict);
            var range = _textBuffer.GetLineRange(0);
            var res = _rangeUtil.RangeOrCurrentLine(FSharpOption<SnapshotLineRange>.Some(range));
            Assert.AreEqual(range, res);
        }

        [Test]
        public void ParseMark1()
        {
            Create("foo", "bar");
            var point1 = new SnapshotPoint(_textBuffer.CurrentSnapshot, 0);
            var point2 = _textBuffer.CurrentSnapshot.GetLineFromLineNumber(1).EndIncludingLineBreak;
            _vimTextBuffer.SetLocalMark(LocalMark.OfChar('c').Value, 0, 0);
            var range = Parse("'c,2");
            Assert.IsTrue(range.IsSucceeded);
            Assert.AreEqual(new SnapshotSpan(point1, point2), range.AsSucceeded().Item1.ExtentIncludingLineBreak);
        }

        [Test]
        [Ignore("just need to fix up")]
        public void ParseMark2()
        {
            Create("foo", "bar");
            var range = _textBuffer.GetLineRange(0, 1);

            /*
            map.SetLocalMark(range.Start, 'c');
            map.SetLocalMark(range.End, 'b');

            var parse = Parse("'c,'b", map);
            Assert.IsTrue(parse.IsSucceeded);
            Assert.AreEqual(range, parse.AsSucceeded().Item1);
            */
        }

        [Test, Description("Marks are the same as line numbers")]
        public void ParseMark3()
        {
            Create("foo", "bar");
            _vimTextBuffer.SetLocalMark(LocalMark.OfChar('c').Value, 0, 2);
            var parse = Parse("'c,2");
            Assert.IsTrue(parse.IsSucceeded);
            Assert.AreEqual(_textBuffer.GetLineRange(0, 1), parse.AsSucceeded().Item1);
        }

        [Test, Description("Global mark")]
        public void ParseMark4()
        {
            Create("foo bar", "bar", "baz");
            Vim.MarkMap.SetGlobalMark(Letter.A, _vimTextBuffer, 0, 2);
            var parse = Parse("'A,2");
            Assert.IsTrue(parse.IsSucceeded);
            Assert.AreEqual(_textBuffer.GetLineRange(0, 1), parse.AsSucceeded().Item1);
        }

        [Test]
        public void ParseMark6()
        {
            Create("foo bar", "baz");
            var range = Parse("'c,2");
            Assert.IsTrue(range.IsFailed);
        }

        [Test]
        public void Plus1()
        {
            Create("foo", "bar", "baz", "jaz");
            ParseSingleLine("1+2", 2);
        }

        [Test]
        public void Plus2()
        {
            Create("foo", "bar", "baz");
            ParseSingleLine("1+", 1);
        }

        [Test]
        public void Plus4()
        {
            Create("foo", "bar", "baz", "jaz", "aoeu", "za,.p");
            ParseLineRange("1+1,3", 1, 2);
        }

        [Test]
        public void Minus1()
        {
            Create("foo", "bar", "baz", "jaz", "aoeu", "za,.p");
            ParseSingleLine("1-1", 0);
        }

        [Test]
        public void Minus2()
        {
            Create("foo", "bar", "baz", "jaz", "aoeu", "za,.p");
            ParseSingleLine("2-", 0);
        }

        [Test]
        public void Minus3()
        {
            Create("foo", "bar", "baz", "jaz", "aoeu", "za,.p");
            ParseSingleLine("5-3", 1);
        }

        [Test]
        public void Minus4()
        {
            Create("foo", "bar", "baz", "jaz", "aoeu", "za,.p");
            ParseLineRange("1,5-2", 0, 2);
        }

        [Test]
        public void ParseDollar_MultiLineBuffer()
        {
            Create("cat", "tree", "dog");
            ParseSingleLine("$", 2);
        }

        [Test]
        public void ParseDollar_OneLineBuffer()
        {
            Create("cat");
            ParseSingleLine("$", 0);
        }

        [Test]
        public void ParseDollar_CurrentToEnd()
        {
            Create("cat", "tree", "dog");
            ParseLineRange(".,$", 0, 2);
        }

        [Test]
        public void Parse_RightSideIncrementsLeft()
        {
            Create("cat", "dog", "bear", "frog", "tree");
            ParseLineRange(".,+2", 0, 2);
        }

        [Test]
        public void Parse_LeftSideIncrementsCurrent()
        {
            Create("cat", "dog", "bear", "frog", "tree");
            ParseLineRange(".,+2", 0, 2);
        }

        [Test]
        public void Parse_LeftSideIncrementsCurrentFromSecondLine()
        {
            Create("cat", "dog", "bear", "frog", "tree");
            ParseLineRange(".,+2", 1, 3, contextLine: 1);
        }
    }
}
