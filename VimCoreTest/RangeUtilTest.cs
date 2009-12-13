using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VimCore.Modes.Command;
using VimCore;
using VimCoreTest.Utils;
using Microsoft.VisualStudio.Text;
using Microsoft.FSharp.Collections;

namespace VimCoreTest
{
    [TestClass]
    public class RangeUtilTest
    {
        private ITextBuffer _buffer;
        private MarkMap _map;

        [TestInitialize]
        public void Init()
        {
            _buffer = null;
            _map = new MarkMap();
        }

        private void Create(params string[] lines)
        {
            _buffer = EditorUtil.CreateBuffer(lines);
        }

        private ParseRangeResult CaptureComplete(string input)
        {
            return CaptureComplete(new SnapshotPoint(_buffer.CurrentSnapshot, 0), input);
        }

        private ParseRangeResult CaptureComplete(SnapshotPoint point, string input)
        {
            var list = input.Select(x => InputUtil.CharToKeyInput(x));
            return RangeUtil.ParseRange(point, _map, ListModule.OfSeq(list));
        }

        [TestMethod]
        public void NoRange1()
        {
            Create(string.Empty);
            Action<string> del = input =>
                {
                    Assert.IsTrue(CaptureComplete(input).IsNoRange);
                };
            del(String.Empty);
            del("j");
            del("join");
            del("1");   // A set of digits is not a range
            del("12");   // A set of digits is not a range
        }

        [TestMethod]
        public void FullFile()
        {
            Create("foo","bar");
            var res = CaptureComplete("%");
            var tss = _buffer.CurrentSnapshot;
            Assert.IsTrue(res.IsSucceeded);
            Assert.AreEqual(new SnapshotSpan(tss, 0, tss.Length), RangeUtil.GetSnapshotSpan(res.AsSucceeded().Item1));
        }

        [TestMethod]
        public void FullFile2()
        {
            Create("foo", "bar");
            var res = CaptureComplete("%bar");
            Assert.IsTrue(res.IsSucceeded);
            var range = res.AsSucceeded().Item1;
            Assert.AreEqual(new SnapshotSpan(_buffer.CurrentSnapshot, 0, _buffer.CurrentSnapshot.Length), RangeUtil.GetSnapshotSpan(range));
            Assert.IsTrue("bar".SequenceEqual(res.AsSucceeded().Item2.Select(x => x.Char)));
        }

        [TestMethod]
        public void CurrentLine1()
        {
            Create("foo", "bar");
            var res = CaptureComplete(".");
            Assert.IsTrue(res.IsSucceeded);
            Assert.AreEqual(_buffer.CurrentSnapshot.GetLineFromLineNumber(0).ExtentIncludingLineBreak, RangeUtil.GetSnapshotSpan(res.AsSucceeded().Item1));
        }

        [TestMethod]
        public void CurrentLine2()
        {
            Create("foo", "bar");
            var res = CaptureComplete(".,.");
            Assert.IsTrue(res.IsSucceeded);
            Assert.AreEqual(_buffer.CurrentSnapshot.GetLineFromLineNumber(0).ExtentIncludingLineBreak, RangeUtil.GetSnapshotSpan(res.AsSucceeded().Item1));
        }

        [TestMethod]
        public void CurrentLine3()
        {
            Create("foo", "bar");
            var res = CaptureComplete(".foo");
            Assert.IsTrue(res.IsSucceeded);

            var range = res.AsSucceeded();
            Assert.AreEqual(_buffer.CurrentSnapshot.GetLineFromLineNumber(0).ExtentIncludingLineBreak, RangeUtil.GetSnapshotSpan(range.Item1));
            Assert.AreEqual('f', range.Item2.First().Char);
        }

        [TestMethod]
        public void LineNumber1()
        {
            Create("a", "b", "c");
            var res = CaptureComplete("1,2");
            Assert.IsTrue(res.IsSucceeded);
               
            var span = new SnapshotSpan(
                _buffer.CurrentSnapshot.GetLineFromLineNumber(0).Start,
                _buffer.CurrentSnapshot.GetLineFromLineNumber(1).EndIncludingLineBreak);
            Assert.AreEqual(span, RangeUtil.GetSnapshotSpan(res.AsSucceeded().Item1));
        }

        [TestMethod]
        public void ApplyCount1()
        {
            Create("foo","bar","baz","jaz");
            var first = Range.NewLines(_buffer.CurrentSnapshot, 0, 0);
            var second = RangeUtil.ApplyCount(first, 2);
            Assert.IsTrue(second.IsLines);
            var lines = second.AsLines();
            Assert.AreEqual(0, lines.Item2);
            Assert.AreEqual(2, lines.Item3);
        }

        [TestMethod, Description("Count is bound to end of the file")]
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
        
    }
}
