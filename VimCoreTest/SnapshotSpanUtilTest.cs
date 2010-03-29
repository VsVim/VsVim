using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Microsoft.VisualStudio.Text;
using Vim;

namespace VimCoreTest
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

    }
}
