using System;
using Microsoft.VisualStudio.Text;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class SnapshotLineRangeUtilTest
    {
        private ITextBuffer _buffer;

        public void Create(params string[] lines)
        {
            _buffer = EditorUtil.CreateTextBuffer(lines);
        }

        [Test]
        public void CreateForLine()
        {
            Create("hello", "world");
            var range = SnapshotLineRangeUtil.CreateForLine(_buffer.GetLine(0));
            Assert.AreEqual("hello", range.Extent.GetText());
            Assert.AreEqual(1, range.Count);
            Assert.AreEqual(0, range.StartLineNumber);
            Assert.AreEqual(0, range.EndLineNumber);
        }

        [Test]
        public void CreateForLineNumberAndCount1()
        {
            Create("hello", "world");
            var range = SnapshotLineRangeUtil.CreateForLineNumberAndCount(_buffer.CurrentSnapshot, 0, 1).Value;
            Assert.AreEqual("hello", range.Extent.GetText());
            Assert.AreEqual(1, range.Count);
            Assert.AreEqual(0, range.StartLineNumber);
            Assert.AreEqual(0, range.EndLineNumber);
        }

        [Test]
        public void CreateForLineNumberAndCount2()
        {
            Create("hello", "world");
            var range = SnapshotLineRangeUtil.CreateForLineNumberAndCount(_buffer.CurrentSnapshot, 0, 2).Value;
            Assert.AreEqual("hello" + Environment.NewLine + "world", range.Extent.GetText());
            Assert.AreEqual(2, range.Count);
            Assert.AreEqual(0, range.StartLineNumber);
            Assert.AreEqual(1, range.EndLineNumber);
        }

        [Test]
        [Description("Guard against a high count")]
        public void CreateForLineNumberAndCount3()
        {
            Create("hello", "world");
            var opt = SnapshotLineRangeUtil.CreateForLineNumberAndCount(_buffer.CurrentSnapshot, 0, 300);
            Assert.IsTrue(opt.IsNone());
        }

        [Test]
        public void CreateForLineNumberRange1()
        {
            Create("hello", "world");
            var range = SnapshotLineRangeUtil.CreateForLineNumberRange(_buffer.CurrentSnapshot, 0, 0);
            Assert.AreEqual("hello", range.Extent.GetText());
            Assert.AreEqual(1, range.Count);
            Assert.AreEqual(0, range.StartLineNumber);
            Assert.AreEqual(0, range.EndLineNumber);
        }

        [Test]
        public void CreateForLineNumberRange2()
        {
            Create("hello", "world");
            var range = SnapshotLineRangeUtil.CreateForLineNumberRange(_buffer.CurrentSnapshot, 0, 1);
            Assert.AreEqual("hello" + Environment.NewLine + "world", range.Extent.GetText());
            Assert.AreEqual(2, range.Count);
            Assert.AreEqual(0, range.StartLineNumber);
            Assert.AreEqual(1, range.EndLineNumber);
        }

        [Test]
        [Description("Sanity check for valid count")]
        public void CreateForLineAndMaxCount1()
        {
            Create("a", "b");
            var range = SnapshotLineRangeUtil.CreateForLineAndMaxCount(_buffer.GetLine(0), 1);
            Assert.AreEqual(1, range.Count);
            Assert.AreEqual(0, range.StartLineNumber);
            Assert.AreEqual(0, range.EndLineNumber);
        }

        [Test]
        [Description("Very high count")]
        public void CreateForLineAndMaxCount2()
        {
            Create("a", "b");
            var range = SnapshotLineRangeUtil.CreateForLineAndMaxCount(_buffer.GetLine(0), 100);
            Assert.AreEqual(2, range.Count);
            Assert.AreEqual(0, range.StartLineNumber);
            Assert.AreEqual(1, range.EndLineNumber);
        }

        [Test]
        [Description("Count exactly at the max")]
        public void CreateForLineAndMaxCount3()
        {
            Create("a", "b");
            var range = SnapshotLineRangeUtil.CreateForLineAndMaxCount(_buffer.GetLine(0), 2);
            Assert.AreEqual(2, range.Count);
            Assert.AreEqual(0, range.StartLineNumber);
            Assert.AreEqual(1, range.EndLineNumber);
        }
    }
}
