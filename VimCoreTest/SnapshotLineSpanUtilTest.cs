using System;
using Microsoft.VisualStudio.Text;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;

namespace VimCore.Test
{
    [TestFixture]
    public class SnapshotLineSpanUtilTest
    {
        private ITextBuffer _buffer;

        public void Create(params string[] lines)
        {
            _buffer = EditorUtil.CreateBuffer(lines);
        }

        [Test]
        public void CreateForSingleLine1()
        {
            Create("hello", "world");
            var range = SnapshotLineSpanUtil.CreateForSingleLine(_buffer.CurrentSnapshot, 0);
            Assert.AreEqual("hello", range.Extent.GetText());
            Assert.AreEqual(1, range.Count);
            Assert.AreEqual(0, range.StartLineNumber);
            Assert.AreEqual(0, range.EndLineNumber);
        }

        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void CreateForSingleLine2()
        {
            Create("hello", "world");
            var range = SnapshotLineSpanUtil.CreateForSingleLine(_buffer.CurrentSnapshot, 5);
        }

        [Test]
        public void CreateForStartAndCount1()
        {
            Create("hello", "world");
            var range = SnapshotLineSpanUtil.CreateForStartAndCount(_buffer.CurrentSnapshot, 0, 1);
            Assert.AreEqual("hello", range.Extent.GetText());
            Assert.AreEqual(1, range.Count);
            Assert.AreEqual(0, range.StartLineNumber);
            Assert.AreEqual(0, range.EndLineNumber);
        }

        [Test]
        public void CreateForStartAndCount2()
        {
            Create("hello", "world");
            var range = SnapshotLineSpanUtil.CreateForStartAndCount(_buffer.CurrentSnapshot, 0, 2);
            Assert.AreEqual("hello" + Environment.NewLine + "world", range.Extent.GetText());
            Assert.AreEqual(2, range.Count);
            Assert.AreEqual(0, range.StartLineNumber);
            Assert.AreEqual(1, range.EndLineNumber);
        }

        [Test]
        public void CreateForStartAndEndLine1()
        {
            Create("hello", "world");
            var range = SnapshotLineSpanUtil.CreateForStartAndEndLine(_buffer.CurrentSnapshot, 0, 0);
            Assert.AreEqual("hello", range.Extent.GetText());
            Assert.AreEqual(1, range.Count);
            Assert.AreEqual(0, range.StartLineNumber);
            Assert.AreEqual(0, range.EndLineNumber);
        }

        [Test]
        public void CreateForStartAndEndLine2()
        {
            Create("hello", "world");
            var range = SnapshotLineSpanUtil.CreateForStartAndEndLine(_buffer.CurrentSnapshot, 0, 1);
            Assert.AreEqual("hello" + Environment.NewLine + "world", range.Extent.GetText());
            Assert.AreEqual(2, range.Count);
            Assert.AreEqual(0, range.StartLineNumber);
            Assert.AreEqual(1, range.EndLineNumber);
        }
    }
}
