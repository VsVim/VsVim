using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Microsoft.VisualStudio.Text;
using Vim;

namespace VimCore.Test
{
    [TestFixture]
    public class SnapshotSpanUtilTest
    {
        static string[] s_lines = new string[]
            {
                "summary description for this line",
                "some other line",
                "running out of things to make up"
            };

        ITextBuffer _buffer = null;
        ITextSnapshot _snapshot = null;

        public void Create(params string[] lines)
        {
            _buffer = Utils.EditorUtil.CreateBuffer(lines);
            _snapshot = _buffer.CurrentSnapshot;
        }

        [TearDown]
        public void TearDown()
        {
            _buffer = null;
            _snapshot = null;
        }

        [Test, Description("Make sure all points valid")]
        public void GetPoints1()
        {
            Create(s_lines);
            var points =
                _buffer.CurrentSnapshot.Lines
                .Select(x => x.Extent)
                .SelectMany(SnapshotSpanUtil.GetPoints);
            foreach (var point in points )
            {
                var notUsed = point.GetChar();
            }
        }

        [Test, Description("Make sure all points valid")]
        public void GetPoints2()
        {
            Create(s_lines);
            var points =
                _buffer.CurrentSnapshot.Lines
                .Select(x => x.ExtentIncludingLineBreak)
                .SelectMany(SnapshotSpanUtil.GetPoints);
            foreach (var point in points )
            {
                var notUsed = point.GetChar();
            }
        }

        [Test]
        public void GetPoints3()
        {
            Create("foo");
            var points =
                _buffer.CurrentSnapshot.Lines
                .Select(x => x.Extent)
                .SelectMany(SnapshotSpanUtil.GetPoints)
                .ToList();
            Assert.AreEqual(3, points.Count);
            Assert.AreEqual('f', points[0].GetChar());
            Assert.AreEqual('o', points[1].GetChar());
            Assert.AreEqual('o', points[2].GetChar());
        }

        [Test]
        public void GetPoints4()
        {
            Create("foo", "bar");
            var points = SnapshotSpanUtil.GetPoints(_buffer.GetLine(0).Extent);
            var chars = points.Select(x => x.GetChar()).ToList();
            CollectionAssert.AreEqual(new char[] {'f', 'o', 'o'}, chars);
        }

        [Test]
        public void GetPoints5()
        {
            Create("foo bar");
            var points = SnapshotSpanUtil.GetPoints(new SnapshotSpan(_buffer.CurrentSnapshot, 0, 0));
            Assert.AreEqual(0, points.Count());
        }

        [Test]
        public void GetPointsBackward1()
        {
            Create("foo");
            var span = _buffer.GetLine(0).Extent;
            var points = SnapshotSpanUtil.GetPointsBackward(span).ToList();
            Assert.AreEqual(3, points.Count);
            Assert.AreEqual('o', points[0].GetChar());
            Assert.AreEqual('o', points[1].GetChar());
            Assert.AreEqual('f', points[2].GetChar());
        }

        [Test]
        public void GetPointsBackward2()
        {
            Create("foo", "bar");
            var span = _buffer.GetLine(1).Extent;
            var points = SnapshotSpanUtil.GetPointsBackward(span).ToList();
            Assert.AreEqual(3, points.Count);
            Assert.AreEqual('r', points[0].GetChar());
            Assert.AreEqual('a', points[1].GetChar());
            Assert.AreEqual('b', points[2].GetChar());
        }

        [Test]
        public void GetPointsBackward3()
        {
            Create("foo", "bar");
            var span = new SnapshotSpan(_buffer.CurrentSnapshot, 0, 1);
            var point = SnapshotSpanUtil.GetPointsBackward(span).Single();
            Assert.AreEqual('f', point.GetChar());
        }

        [Test]
        public void GetPointsBackward4()
        {
            Create("foo", "bar");
            var span = new SnapshotSpan(_buffer.CurrentSnapshot, 0, 2);
            var points = SnapshotSpanUtil.GetPointsBackward(span).Select(x => x.GetChar()).ToList();
            CollectionAssert.AreEqual(new char[] { 'o', 'f' }, points);
        }

        [Test]
        public void GetPointsBackward5()
        {
            Create("foo", "bar");
            var span = _buffer.GetLine(0).Extent;
            var points = SnapshotSpanUtil.GetPointsBackward(span).ToList();
            Assert.AreEqual(3, points.Count);
            Assert.AreEqual('o', points[0].GetChar());
            Assert.AreEqual('o', points[1].GetChar());
            Assert.AreEqual('f', points[2].GetChar());
        }

        [Test]
        public void GetPointsBackward6()
        {
            Create("foo bar");
            var points = SnapshotSpanUtil.GetPointsBackward(new SnapshotSpan(_buffer.CurrentSnapshot, 0, 0));
            Assert.AreEqual(0, points.Count());
        }

    }
}
