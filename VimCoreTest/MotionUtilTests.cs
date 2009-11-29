using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VimCore;
using Microsoft.VisualStudio.Text;
using Microsoft.FSharp.Core;

namespace VimCoreTest
{
    /// <summary>
    /// Summary description for TssUtilTest
    /// </summary>
    [TestClass]
    public class MotionUtilTests
    {
        ITextBuffer _buffer = null;
        ITextSnapshot _snapshot = null;

        public void Initialize(params string[] lines)
        {
            _buffer = Utils.EditorUtil.CreateBuffer(lines);
            _snapshot = _buffer.CurrentSnapshot;
            _buffer.Changed += (s, e) => _snapshot = _buffer.CurrentSnapshot;
        }

        [TestMethod]
        public void CharLeft1()
        {
            Initialize("foo bar");
            var res = MotionUtil.CharLeft(new SnapshotPoint(_snapshot, 1), 1);
            Assert.AreEqual(1, res.Length);
            Assert.AreEqual(0, res.Start.Position);
            Assert.AreEqual(1, res.End.Position);
        }

        [TestMethod, Description("Don't go off the end of the buffer")]
        public void CharLeft2()
        {
            Initialize("foo bar");
            var res = MotionUtil.CharLeft(new SnapshotPoint(_snapshot, 1), 100);
            Assert.AreEqual(1, res.Length);
            Assert.AreEqual(0, res.Start.Position);
            Assert.AreEqual(1, res.End.Position);
        }

        [TestMethod]
        public void CharRight1()
        {
            Initialize("foo bar");
            var res = MotionUtil.CharRight(new SnapshotPoint(_snapshot, 0), 1);
            Assert.AreEqual(1, res.Length);
            Assert.AreEqual(0, res.Start.Position);
            Assert.AreEqual(1, res.End.Position);
        }

        [TestMethod]
        public void CharRight2()
        {
            Initialize("foo bar");
            var res = MotionUtil.CharRight(new SnapshotPoint(_snapshot, 0), 2);
            Assert.AreEqual(2, res.Length);
            Assert.AreEqual(0, res.Start.Position);
            Assert.AreEqual(2, res.End.Position);
        }

        [TestMethod, Description("Don't go off the end of the buffer")]
        public void CharRight3()
        {
            Initialize("foo bar");
            var res = MotionUtil.CharRight(new SnapshotPoint(_snapshot, 0), 200);
            var line = _snapshot.GetLineFromLineNumber(0);
            Assert.AreEqual(line.Extent.Length, res.Length);
            Assert.AreEqual(line.Start, res.Start);
            Assert.AreEqual(line.End, res.End);
        }

        [TestMethod]
        public void CharUp1()
        {
            Initialize("foo", "bar");
            var res = MotionUtil.CharUp(_snapshot.GetLineFromLineNumber(1).Start, 1);
            var line = _snapshot.GetLineFromLineNumber(0);
            Assert.AreEqual(line.Start, res.Start);
            Assert.AreEqual(_snapshot.GetLineFromLineNumber(1).Start, res.End);
        }

        [TestMethod]
        public void CharUp2()
        {
            Initialize("foo", "bar");
            var res = MotionUtil.CharUp(_snapshot.GetLineFromLineNumber(1).Start.Add(1), 1);
            var line = _snapshot.GetLineFromLineNumber(0);
            Assert.AreEqual(line.Start.Add(1), res.Start);
            Assert.AreEqual(_snapshot.GetLineFromLineNumber(1).Start.Add(1), res.End);
        }

        [TestMethod]
        public void CharDown1()
        {
            Initialize("foo", "bar");
            var res = MotionUtil.CharDown(new SnapshotPoint(_snapshot, 1), 1);
            var line = _snapshot.GetLineFromLineNumber(1);
            Assert.AreEqual(line.Start.Add(1), res.End);
            Assert.AreEqual(1, res.Start.Position);
        }


        [TestMethod, Description("Don't go past the end of the buffer")]
        public void CharDown2()
        {
            Initialize("foo", "bar");
            var res = MotionUtil.CharDown(new SnapshotPoint(_snapshot, 1), 100);
            var line = _snapshot.GetLineFromLineNumber(1);
            Assert.AreEqual(line.Start.Add(1), res.End);
            Assert.AreEqual(1, res.Start.Position);
        }

        [TestMethod]
        public void LineUp1()
        {
            Initialize("foo", "bar");
            var res = MotionUtil.LineUp(_snapshot.GetLineFromLineNumber(1).Start, 1);
            Assert.AreEqual("foo" + Environment.NewLine + "bar", res.GetText());
        }

        [TestMethod]
        public void LineDown1()
        {
            Initialize("foo", "bar");
            var res = MotionUtil.LineDown(_snapshot.GetLineFromLineNumber(0).Start, 1);
            Assert.AreEqual("foo" + Environment.NewLine + "bar", res.GetText());
        }

    }
}
