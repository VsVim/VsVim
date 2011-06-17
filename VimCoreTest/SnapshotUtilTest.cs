using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Microsoft.VisualStudio.Text;
using Vim;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class SnapshotUtilTest
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
            _buffer = EditorUtil.CreateTextBuffer(lines);
            _snapshot = _buffer.CurrentSnapshot;
        }

        [TearDown]
        public void TearDown()
        {
            _buffer = null;
            _snapshot = null;
        }

        [Test]
        public void GetStartPoint()
        {
            Create("foo bar");
            var start = SnapshotUtil.GetStartPoint(_buffer.CurrentSnapshot);
            var line = _buffer.CurrentSnapshot.GetLineFromLineNumber(0);
            Assert.AreEqual(line.Start, start);
        }

        [Test]
        public void GetEndPoint()
        {
            Create("foo bar");
            var end = SnapshotUtil.GetEndPoint(_buffer.CurrentSnapshot);
            var line = _buffer.CurrentSnapshot.GetLineFromLineNumber(0);
            Assert.AreEqual(line.End, end);
        }
    }
}
