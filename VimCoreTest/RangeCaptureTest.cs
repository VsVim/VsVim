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
    public class RangeCaptureTest
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

        private Range CaptureComplete(string input)
        {
            return CaptureComplete(new SnapshotPoint(_buffer.CurrentSnapshot, 0), input);
        }

        private Range CaptureComplete(SnapshotPoint point, string input)
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
            Assert.IsTrue(res.IsValidRange);
            Assert.AreEqual(new SnapshotSpan(tss, 0, tss.Length), res.AsValidRange().Item1);
        }

        [TestMethod]
        public void FullFile2()
        {
            Create("foo", "bar");
            var res = CaptureComplete("%bar");
            Assert.IsTrue(res.IsValidRange);
            var range = res.AsValidRange();
            Assert.AreEqual(new SnapshotSpan(_buffer.CurrentSnapshot, 0, _buffer.CurrentSnapshot.Length), range.Item1);
            Assert.IsTrue("bar".SequenceEqual(range.Item2.Select(x => x.Char)));
        }

        [TestMethod]
        public void CurrentLine1()
        {
            Create("foo", "bar");
            var res = CaptureComplete(".");
            Assert.IsTrue(res.IsValidRange);
            Assert.AreEqual(_buffer.CurrentSnapshot.GetLineFromLineNumber(0).ExtentIncludingLineBreak, res.AsValidRange().Item1);
        }

        [TestMethod]
        public void CurrentLine2()
        {
            Create("foo", "bar");
            var res = CaptureComplete(".,.");
            Assert.IsTrue(res.IsValidRange);
            Assert.AreEqual(_buffer.CurrentSnapshot.GetLineFromLineNumber(0).ExtentIncludingLineBreak, res.AsValidRange().Item1);
        }

        [TestMethod]
        public void CurrentLine3()
        {
            Create("foo", "bar");
            var res = CaptureComplete(".foo");
            Assert.IsTrue(res.IsValidRange);

            var range = res.AsValidRange();
            Assert.AreEqual(_buffer.CurrentSnapshot.GetLineFromLineNumber(0).ExtentIncludingLineBreak, range.Item1);
            Assert.AreEqual('f', range.Item2.First().Char);
        }

        [TestMethod]
        public void LineNumber1()
        {
            Create("a", "b", "c");
            var res = CaptureComplete("1,2");
            Assert.IsTrue(res.IsValidRange);
               
            var span = new SnapshotSpan(
                _buffer.CurrentSnapshot.GetLineFromLineNumber(0).Start,
                _buffer.CurrentSnapshot.GetLineFromLineNumber(1).EndIncludingLineBreak);
            Assert.AreEqual(span, res.AsValidRange().Item1);
        }
    }
}
