using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Vim;
using Microsoft.VisualStudio.Text;
using Microsoft.FSharp.Core;

namespace VimCoreTest
{
    /// <summary>
    /// Summary description for TssUtilTest
    /// </summary>
    [TestFixture]
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

        [Test]
        public void CharLeft1()
        {
            Initialize("foo bar");
            var res = MotionUtil.CharLeft(new SnapshotPoint(_snapshot, 1), 1);
            Assert.AreEqual(1, res.Length);
            Assert.AreEqual(0, res.Start.Position);
            Assert.AreEqual(1, res.End.Position);
        }

        [Test, Description("Don't go off the end of the buffer")]
        public void CharLeft2()
        {
            Initialize("foo bar");
            var res = MotionUtil.CharLeft(new SnapshotPoint(_snapshot, 1), 100);
            Assert.AreEqual(1, res.Length);
            Assert.AreEqual(0, res.Start.Position);
            Assert.AreEqual(1, res.End.Position);
        }

        [Test]
        public void CharRight1()
        {
            Initialize("foo bar");
            var res = MotionUtil.CharRight(new SnapshotPoint(_snapshot, 0), 1);
            Assert.AreEqual(1, res.Length);
            Assert.AreEqual(0, res.Start.Position);
            Assert.AreEqual(1, res.End.Position);
        }

        [Test]
        public void CharRight2()
        {
            Initialize("foo bar");
            var res = MotionUtil.CharRight(new SnapshotPoint(_snapshot, 0), 2);
            Assert.AreEqual(2, res.Length);
            Assert.AreEqual(0, res.Start.Position);
            Assert.AreEqual(2, res.End.Position);
        }

        [Test, Description("Don't go off the end of the buffer")]
        public void CharRight3()
        {
            Initialize("foo bar");
            var res = MotionUtil.CharRight(new SnapshotPoint(_snapshot, 0), 200);
            var line = _snapshot.GetLineFromLineNumber(0);
            Assert.AreEqual(line.Extent.Length, res.Length);
            Assert.AreEqual(line.Start, res.Start);
            Assert.AreEqual(line.End, res.End);
        }

        [Test, Description("End of the line should get the last character")]
        public void CharRight4()
        {
            Initialize("foo", "bar");
            var start = _buffer.GetLine(0).Start.Add(2);
            var res = MotionUtil.CharRight(start, 1);
            Assert.AreEqual(1, res.Length);
            Assert.AreEqual(start, res.Start);
        }

        [Test]
        public void CharUp1()
        {
            Initialize("foo", "bar");
            var res = MotionUtil.CharUp(_snapshot.GetLineFromLineNumber(1).Start, 1);
            var line = _snapshot.GetLineFromLineNumber(0);
            Assert.AreEqual(line.Start, res.Start);
            Assert.AreEqual(_snapshot.GetLineFromLineNumber(1).Start, res.End);
        }

        [Test]
        public void CharUp2()
        {
            Initialize("foo", "bar");
            var res = MotionUtil.CharUp(_snapshot.GetLineFromLineNumber(1).Start.Add(1), 1);
            var line = _snapshot.GetLineFromLineNumber(0);
            Assert.AreEqual(line.Start.Add(1), res.Start);
            Assert.AreEqual(_snapshot.GetLineFromLineNumber(1).Start.Add(1), res.End);
        }

        [Test]
        public void CharDown1()
        {
            Initialize("foo", "bar");
            var res = MotionUtil.CharDown(new SnapshotPoint(_snapshot, 1), 1);
            var line = _snapshot.GetLineFromLineNumber(1);
            Assert.AreEqual(line.Start.Add(1), res.End);
            Assert.AreEqual(1, res.Start.Position);
        }


        [Test, Description("Don't go past the end of the buffer")]
        public void CharDown2()
        {
            Initialize("foo", "bar");
            var res = MotionUtil.CharDown(new SnapshotPoint(_snapshot, 1), 100);
            var line = _snapshot.GetLineFromLineNumber(1);
            Assert.AreEqual(line.Start.Add(1), res.End);
            Assert.AreEqual(1, res.Start.Position);
        }

        [Test]
        public void LineUp1()
        {
            Initialize("foo", "bar");
            var res = MotionUtil.LineUp(_snapshot.GetLineFromLineNumber(1).Start, 1);
            Assert.AreEqual("foo" + Environment.NewLine + "bar", res.GetText());
        }

        [Test]
        public void LineDown1()
        {
            Initialize("foo", "bar");
            var res = MotionUtil.LineDown(_snapshot.GetLineFromLineNumber(0).Start, 1);
            Assert.AreEqual("foo" + Environment.NewLine + "bar", res.GetText());
        }



    }
}
