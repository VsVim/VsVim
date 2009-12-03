using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VimCore.Modes.Command;
using VimCore;
using VimCoreTest.Utils;
using Microsoft.VisualStudio.Text;

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

        private RangeResult CaptureComplete(string input)
        {
            return CaptureComplete(new SnapshotPoint(_buffer.CurrentSnapshot, 0), input);
        }

        private RangeResult CaptureComplete(SnapshotPoint point, string input)
        {
            Assert.IsFalse(String.IsNullOrEmpty(input));
            var output = RangeCapture.Capture(point, _map, InputUtil.CharToKeyInput(input[0]));
            int i = 1;
            while (i < input.Length)
            {
                Assert.IsTrue(output.IsNeedMore);
                var next = InputUtil.CharToKeyInput(input[i]);
                output = output.AsNeedMore().Item.Invoke(next);
            }
            Assert.IsTrue(output.IsNeedMore);
            output = output.AsNeedMore().item.Invoke(new KeyInput(' ', System.Windows.Input.Key.Space));
            Assert.IsFalse(output.IsNeedMore);
            return output;
        }

        [TestMethod]
        public void FullFile()
        {
            Create("foo","bar");
            var res = CaptureComplete("%");
            var tss = _buffer.CurrentSnapshot;
            Assert.IsTrue(res.IsRange);
            Assert.AreEqual(new SnapshotSpan(tss, 0, tss.Length), res.AsRange().Item1);
        }

        [TestMethod]
        public void CurrentLine1()
        {
            Create("foo", "bar");
            var res = CaptureComplete(".");
            Assert.IsTrue(res.IsRange);
            Assert.AreEqual(_buffer.CurrentSnapshot.GetLineFromLineNumber(0).ExtentIncludingLineBreak, res.AsRange().Item1);
        }

        [TestMethod]
        public void CurrentLine2()
        {
            Create("foo", "bar");
            var res = CaptureComplete(".,.");
            Assert.IsTrue(res.IsRange);
            Assert.AreEqual(_buffer.CurrentSnapshot.GetLineFromLineNumber(0).ExtentIncludingLineBreak, res.AsRange().Item1);
        }


    }
}
