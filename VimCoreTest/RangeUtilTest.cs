using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Vim.Modes.Command;
using Vim;
using VimCoreTest.Utils;
using Microsoft.VisualStudio.Text;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Range = Vim.Modes.Command.Range;

namespace VimCoreTest
{
    [TestFixture]
    public class RangeUtilTest
    {
        private ITextBuffer _buffer;

        [SetUp]
        public void Init()
        {
            _buffer = null;
        }

        private void Create(params string[] lines)
        {
            _buffer = EditorUtil.CreateBuffer(lines);
        }

        private ParseRangeResult Parse(string input, IMarkMap map = null)
        {
            return CaptureComplete(new SnapshotPoint(_buffer.CurrentSnapshot, 0), input, map);
        }

        private void ParseLineRange(string input, int startLine, int endLine)
        {
            var ret = Parse(input);
            Assert.IsTrue(ret.IsSucceeded);
            Assert.AreEqual(0, ret.AsSucceeded().Item2.Count());
            var range = ret.AsSucceeded().Item1;
            if (range.IsSingleLine)
            {
                var r = range.AsSingleLine();
                Assert.AreEqual(startLine, r.Item.LineNumber);
                Assert.AreEqual(endLine, r.Item.LineNumber);
            }
            else if (range.IsRawSpan)
            {
                var span = range.AsRawSpan().item;
                Assert.AreEqual(startLine, span.Start.GetContainingLine().LineNumber);
                Assert.AreEqual(endLine, span.End.GetContainingLine().LineNumber);
            }
            else
            {
                Assert.IsTrue(range.IsLines);
                var lines = range.AsLines();
                Assert.AreEqual(startLine, lines.Item2);
                Assert.AreEqual(endLine, lines.Item3);
            }
        }

        private void ParseSingleLine(string input, int line)
        {
            var ret = Parse(input);
            Assert.IsTrue(ret.IsSucceeded);
            var range = ret.AsSucceeded().Item1;
            Assert.IsTrue(range.IsSingleLine);
            Assert.AreEqual(line, range.AsSingleLine().Item.LineNumber);
        }

        private ParseRangeResult CaptureComplete(SnapshotPoint point, string input, IMarkMap map = null)
        {
            map = map ?? new MarkMap();
            var list = input.Select(x => InputUtil.CharToKeyInput(x));
            return RangeUtil.ParseRange(point, map, ListModule.OfSeq(list));
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
            Create("foo","bar");
            var res = Parse("%");
            var tss = _buffer.CurrentSnapshot;
            Assert.IsTrue(res.IsSucceeded);
            Assert.AreEqual(new SnapshotSpan(tss, 0, tss.Length), RangeUtil.GetSnapshotSpan(res.AsSucceeded().Item1));
        }

        [Test]
        public void FullFile2()
        {
            Create("foo", "bar");
            var res = Parse("%bar");
            Assert.IsTrue(res.IsSucceeded);
            var range = res.AsSucceeded().Item1;
            Assert.AreEqual(new SnapshotSpan(_buffer.CurrentSnapshot, 0, _buffer.CurrentSnapshot.Length), RangeUtil.GetSnapshotSpan(range));
            Assert.IsTrue("bar".SequenceEqual(res.AsSucceeded().Item2.Select(x => x.Char)));
        }

        [Test]
        public void CurrentLine1()
        {
            Create("foo", "bar");
            var res = Parse(".");
            Assert.IsTrue(res.IsSucceeded);
            Assert.AreEqual(_buffer.CurrentSnapshot.GetLineFromLineNumber(0).ExtentIncludingLineBreak, RangeUtil.GetSnapshotSpan(res.AsSucceeded().Item1));
        }

        [Test]
        public void CurrentLine2()
        {
            Create("foo", "bar");
            var res = Parse(".,.");
            Assert.IsTrue(res.IsSucceeded);
            Assert.AreEqual(_buffer.CurrentSnapshot.GetLineFromLineNumber(0).ExtentIncludingLineBreak, RangeUtil.GetSnapshotSpan(res.AsSucceeded().Item1));
        }

        [Test]
        public void CurrentLine3()
        {
            Create("foo", "bar");
            var res = Parse(".foo");
            Assert.IsTrue(res.IsSucceeded);

            var range = res.AsSucceeded();
            Assert.AreEqual(_buffer.CurrentSnapshot.GetLineFromLineNumber(0).ExtentIncludingLineBreak, RangeUtil.GetSnapshotSpan(range.Item1));
            Assert.AreEqual('f', range.Item2.First().Char);
        }

        [Test]
        public void LineNumber1()
        {
            Create("a", "b", "c");
            var res = Parse("1,2");
            Assert.IsTrue(res.IsSucceeded);
               
            var span = new SnapshotSpan(
                _buffer.CurrentSnapshot.GetLineFromLineNumber(0).Start,
                _buffer.CurrentSnapshot.GetLineFromLineNumber(1).EndIncludingLineBreak);
            Assert.AreEqual(span, RangeUtil.GetSnapshotSpan(res.AsSucceeded().Item1));
        }

        [Test]
        public void ApplyCount1()
        {
            Create("foo","bar","baz","jaz");
            var first = Range.NewLines(_buffer.CurrentSnapshot, 0, 0);
            var second = RangeUtil.ApplyCount(first, 2);
            Assert.IsTrue(second.IsLines);
            var lines = second.AsLines();
            Assert.AreEqual(0, lines.Item2);
            Assert.AreEqual(1, lines.Item3);
        }

        [Test, Description("Count is bound to end of the file")]
        public void ApplyCount2()
        {
            Create("foo", "bar");
            var v1 = Range.NewLines(_buffer.CurrentSnapshot, 0, 0);
            var v2 = RangeUtil.ApplyCount(v1, 200);
            Assert.IsTrue(v2.IsLines);
            var lines = v2.AsLines();
            Assert.AreEqual(0, lines.Item2);
            Assert.AreEqual(_buffer.CurrentSnapshot.LineCount - 1, lines.Item3);
        }

        [Test]
        public void ApplyCount3()
        {
            Create("foo","bar","baz");
            var v1 = Range.NewSingleLine(_buffer.CurrentSnapshot.GetLineFromLineNumber(0));
            var v2 = RangeUtil.ApplyCount(v1, 2);
            Assert.IsTrue(v2.IsLines);
            var lines = v2.AsLines();
            Assert.AreEqual(0, lines.Item2);
            Assert.AreEqual(1, lines.Item3);
        }

        [Test]
        public void SingleLine1()
        {
            Create("foo", "bar");
            var res = Parse("1");
            Assert.IsTrue(res.IsSucceeded);
            Assert.AreEqual(0, res.AsSucceeded().Item1.AsSingleLine().Item.LineNumber);
        }

        [Test]
        public void RangeOrCurrentLine1()
        {
            var view = EditorUtil.CreateView("foo");
            var res = RangeUtil.RangeOrCurrentLine(view, FSharpOption<Range>.None);
            Assert.AreEqual(view.TextSnapshot.GetLineFromLineNumber(0).Extent, RangeUtil.GetSnapshotSpan(res));
            Assert.IsTrue(res.IsSingleLine);
        }

        [Test]
        public void RangeOrCurrentLine2()
        {
            Create("foo","bar");
            var mock = new Moq.Mock<ITextView>(Moq.MockBehavior.Strict);
            var range = Vim.Modes.Command.Range.NewLines(_buffer.CurrentSnapshot, 0, 0);
            var res = RangeUtil.RangeOrCurrentLine(mock.Object, FSharpOption<Vim.Modes.Command.Range>.Some(range));
            Assert.IsTrue(res.IsLines);
        }

        [Test]
        public void ParseMark1()
        {
            Create("foo", "bar");
            var point1 = new SnapshotPoint(_buffer.CurrentSnapshot, 0);
            var point2 = _buffer.CurrentSnapshot.GetLineFromLineNumber(1).EndIncludingLineBreak;
            var map = new MarkMap();
            map.SetLocalMark(point1, 'c');
            var range = Parse("'c,2",map);
            Assert.IsTrue(range.IsSucceeded);
            Assert.AreEqual(new SnapshotSpan(point1,point2), RangeUtil.GetSnapshotSpan(range.AsSucceeded().Item1));
        }

        [Test]
        public void ParseMark2()
        {
            Create("foo", "bar");
            var point1 = new SnapshotPoint(_buffer.CurrentSnapshot, 0);
            var point2 = _buffer.CurrentSnapshot.GetLineFromLineNumber(1).EndIncludingLineBreak;
            var map = new MarkMap();
            map.SetLocalMark(point1, 'c');
            map.SetLocalMark(point2, 'b');
            var range = Parse("'c,'b", map);
            Assert.IsTrue(range.IsSucceeded);
            Assert.AreEqual(new SnapshotSpan(point1, point2), RangeUtil.GetSnapshotSpan(range.AsSucceeded().Item1));
        }

        [Test,Description("Marks are the same as line numbers")]
        public void ParseMark3()
        {
            Create("foo", "bar");
            var point1 = new SnapshotPoint(_buffer.CurrentSnapshot, 2);
            var point2 = _buffer.CurrentSnapshot.GetLineFromLineNumber(1).EndIncludingLineBreak;
            var map = new MarkMap();
            map.SetLocalMark(point1, 'c');
            var range = Parse("'c,2", map);
            Assert.IsTrue(range.IsSucceeded);
            Assert.AreEqual(new SnapshotSpan(point1.GetContainingLine().Start, point2), RangeUtil.GetSnapshotSpan(range.AsSucceeded().Item1));
        }

        [Test,Description("Global mark")]
        public void ParseMark4()
        {
            Create("foo bar","bar","baz");
            var map = new Mock<IMarkMap>(MockBehavior.Strict);
            map
                .Setup(x => x.GetMark(_buffer, 'A'))
                .Returns(FSharpOption.Create(new VirtualSnapshotPoint(_buffer.CurrentSnapshot, 2)));
            var range = Parse("'A,2",map.Object);
            Assert.IsTrue(range.IsSucceeded);
            var span = RangeUtil.GetSnapshotSpan(range.AsSucceeded().Item1);
            Assert.AreEqual(_buffer.CurrentSnapshot.GetLineSpan(0,1), span);
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
            map.Setup(x => x.GetMark(_buffer, 'c')).Returns(FSharpOption<VirtualSnapshotPoint>.None);
            var range = Parse("'c,2");
            Assert.IsTrue(range.IsFailed);
            Assert.AreEqual(Resources.Range_MarkNotValidInFile, range.AsFailed().Item);
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
            Create("foo", "bar", "baz","jaz","aoeu","za,.p");
            ParseLineRange("1+1,3", 1, 2);
        }

        [Test]
        public void Minus1()
        {
            Create("foo", "bar", "baz","jaz","aoeu","za,.p");
            ParseSingleLine("1-42", 0);
        }

        [Test]
        public void Minus2()
        {
            Create("foo", "bar", "baz","jaz","aoeu","za,.p");
            ParseSingleLine("2-", 0);
        }

        [Test]
        public void Minus3()
        {
            Create("foo", "bar", "baz","jaz","aoeu","za,.p");
            ParseSingleLine("5-3", 1);
        }

        [Test]
        public void Minus4()
        {
            Create("foo", "bar", "baz","jaz","aoeu","za,.p");
            ParseLineRange("1,5-2", 0, 2);
        }

    }
}
