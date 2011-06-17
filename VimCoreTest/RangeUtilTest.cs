using System;
using System.Linq;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.Modes.Command;
using Vim.UnitTest;
using Vim.UnitTest.Mock;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class RangeUtilTest
    {
        private ITextBuffer _textBuffer;

        [SetUp]
        public void Init()
        {
            _textBuffer = null;
        }

        private void Create(params string[] lines)
        {
            _textBuffer = EditorUtil.CreateTextBuffer(lines);
        }

        private ParseRangeResult Parse(string input, IMarkMap map = null, int contextLine = 0)
        {
            return CaptureComplete(_textBuffer.GetLine(contextLine), input, map);
        }

        private void ParseLineRange(string input, int startLine, int endLine, int contextLine = 0)
        {
            var ret = Parse(input, contextLine: contextLine);
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

        private ParseRangeResult CaptureComplete(ITextSnapshotLine line, string input, IMarkMap map = null)
        {
            map = map ?? new MarkMap(new TrackingLineColumnService());
            return RangeUtil.ParseRange(line, map, ListModule.OfSeq(input));
        }

        [Test]
        public void NoRange1()
        {
            Create(string.Empty);
            Action<string> del = input =>
                {
                    Assert.IsTrue(Parse(input).IsNoRange);
                };
            del(String.Empty);
            del("j");
            del("join");
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
            var second = RangeUtil.ApplyCount(2, first);
            Assert.AreEqual(_textBuffer.GetLineRange(0, 1), second);
        }

        [Test, Description("Count is bound to end of the file")]
        public void ApplyCount2()
        {
            Create("foo", "bar");
            var v1 = _textBuffer.GetLineRange(0);
            var v2 = RangeUtil.ApplyCount(200, v1);
            Assert.AreEqual(_textBuffer.GetLineRange(0, 1), v2);
        }

        [Test]
        public void ApplyCount3()
        {
            Create("foo", "bar", "baz");
            var v1 = _textBuffer.GetLineRange(0);
            var v2 = RangeUtil.ApplyCount(2, v1);
            Assert.AreEqual(_textBuffer.GetLineRange(0, 1), v2);
        }

        [Test]
        [Description("Count of 1 should go to the last line in the range")]
        public void Apply_CountOfOne()
        {
            Create("cat", "dog", "rabbit", "tree");
            var v1 = _textBuffer.GetLineRange(0, 1);
            var v2 = RangeUtil.ApplyCount(1, v1);
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
            var view = EditorUtil.CreateTextView("foo");
            var res = RangeUtil.RangeOrCurrentLine(view, FSharpOption<SnapshotLineRange>.None);
            Assert.AreEqual(view.GetLineRange(0), res);
        }

        [Test]
        public void RangeOrCurrentLine2()
        {
            Create("foo", "bar");
            var mock = new Moq.Mock<ITextView>(Moq.MockBehavior.Strict);
            var range = _textBuffer.GetLineRange(0);
            var res = RangeUtil.RangeOrCurrentLine(mock.Object, FSharpOption<SnapshotLineRange>.Some(range));
            Assert.AreEqual(range, res);
        }

        [Test]
        public void ParseMark1()
        {
            Create("foo", "bar");
            var point1 = new SnapshotPoint(_textBuffer.CurrentSnapshot, 0);
            var point2 = _textBuffer.CurrentSnapshot.GetLineFromLineNumber(1).EndIncludingLineBreak;
            var map = new MarkMap(new TrackingLineColumnService());
            map.SetLocalMark(point1, 'c');
            var range = Parse("'c,2", map);
            Assert.IsTrue(range.IsSucceeded);
            Assert.AreEqual(new SnapshotSpan(point1, point2), range.AsSucceeded().Item1.ExtentIncludingLineBreak);
        }

        [Test]
        public void ParseMark2()
        {
            Create("foo", "bar");
            var range = _textBuffer.GetLineRange(0, 1);
            var map = new MarkMap(new TrackingLineColumnService());
            map.SetLocalMark(range.Start, 'c');
            map.SetLocalMark(range.End, 'b');
            var parse = Parse("'c,'b", map);
            Assert.IsTrue(parse.IsSucceeded);
            Assert.AreEqual(range, parse.AsSucceeded().Item1);
        }

        [Test, Description("Marks are the same as line numbers")]
        public void ParseMark3()
        {
            Create("foo", "bar");
            var point1 = new SnapshotPoint(_textBuffer.CurrentSnapshot, 2);
            var point2 = _textBuffer.CurrentSnapshot.GetLineFromLineNumber(1).EndIncludingLineBreak;
            var map = new MarkMap(new TrackingLineColumnService());
            map.SetLocalMark(point1, 'c');
            var parse = Parse("'c,2", map);
            Assert.IsTrue(parse.IsSucceeded);
            Assert.AreEqual(_textBuffer.GetLineRange(0, 1), parse.AsSucceeded().Item1);
        }

        [Test, Description("Global mark")]
        public void ParseMark4()
        {
            Create("foo bar", "bar", "baz");
            var map = new Mock<IMarkMap>(MockBehavior.Strict);
            map
                .Setup(x => x.GetMark(_textBuffer, 'A'))
                .Returns(FSharpOption.Create(new VirtualSnapshotPoint(_textBuffer.CurrentSnapshot, 2)));
            var parse = Parse("'A,2", map.Object);
            Assert.IsTrue(parse.IsSucceeded);
            Assert.AreEqual(_textBuffer.GetLineRange(0, 1), parse.AsSucceeded().Item1);
        }

        [Test]
        public void ParseMark5()
        {
            Create("foo bar", "baz");
            var map = new Mock<IMarkMap>(MockBehavior.Strict);
            var range = Parse("'");
            Assert.IsTrue(range.IsFailed);
            Assert.AreEqual(Resources.Range_MarkMissingIdentifier, range.AsFailed().Item);
        }

        [Test]
        public void ParseMark6()
        {
            Create("foo bar", "baz");
            var map = new Mock<IMarkMap>(MockBehavior.Strict);
            map.Setup(x => x.GetMark(_textBuffer, 'c')).Returns(FSharpOption<VirtualSnapshotPoint>.None);
            var range = Parse("'c,2");
            Assert.IsTrue(range.IsFailed);
            Assert.AreEqual(Resources.Range_MarkNotValidInFile, range.AsFailed().Item);
        }

        [Test]
        public void ParseMark7()
        {
            Create("foo bar");
            var map = new Mock<IMarkMap>(MockBehavior.Strict);
            map.AddMark(_textBuffer, '<', _textBuffer.GetPoint(0));
            map.AddMark(_textBuffer, '>', _textBuffer.GetPoint(1));
            var range = Parse("'<,'>", map.Object);
            Assert.IsTrue(range.IsSucceeded);
            Assert.AreEqual(_textBuffer.GetLineRange(0), range.AsSucceeded().Item1);
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

        [Test, Description("Line number out of range should go to end of file")]
        public void Plus3()
        {
            Create("foo", "bar", "baz");
            ParseSingleLine("1+400", 2);
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
            ParseSingleLine("1-42", 0);
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
