using System;
using Microsoft.VisualStudio.Text;
using Vim.Extensions;
using Xunit;
using Vim.EditorHost;

namespace Vim.UnitTest
{
    public sealed class SnapshotLineRangeUtilTest : VimTestBase
    {
        private ITextBuffer _buffer;

        internal void Create(params string[] lines)
        {
            _buffer = CreateTextBuffer(lines);
        }

        [WpfFact]
        public void CreateForLine()
        {
            Create("hello", "world");
            var range = SnapshotLineRangeUtil.CreateForLine(_buffer.GetLine(0));
            Assert.Equal("hello", range.Extent.GetText());
            Assert.Equal(1, range.Count);
            Assert.Equal(0, range.StartLineNumber);
            Assert.Equal(0, range.LastLineNumber);
        }

        [WpfFact]
        public void CreateForLineNumberAndCount1()
        {
            Create("hello", "world");
            var range = SnapshotLineRangeUtil.CreateForLineNumberAndCount(_buffer.CurrentSnapshot, 0, 1).Value;
            Assert.Equal("hello", range.Extent.GetText());
            Assert.Equal(1, range.Count);
            Assert.Equal(0, range.StartLineNumber);
            Assert.Equal(0, range.LastLineNumber);
        }

        [WpfFact]
        public void CreateForLineNumberAndCount2()
        {
            Create("hello", "world");
            var range = SnapshotLineRangeUtil.CreateForLineNumberAndCount(_buffer.CurrentSnapshot, 0, 2).Value;
            Assert.Equal("hello" + Environment.NewLine + "world", range.Extent.GetText());
            Assert.Equal(2, range.Count);
            Assert.Equal(0, range.StartLineNumber);
            Assert.Equal(1, range.LastLineNumber);
        }

        /// <summary>
        /// Guard against a high count
        /// </summary>
        [WpfFact]
        public void CreateForLineNumberAndCount3()
        {
            Create("hello", "world");
            var opt = SnapshotLineRangeUtil.CreateForLineNumberAndCount(_buffer.CurrentSnapshot, 0, 300);
            Assert.True(opt.IsNone());
        }

        [WpfFact]
        public void CreateForLineNumberRange1()
        {
            Create("hello", "world");
            var range = SnapshotLineRangeUtil.CreateForLineNumberRange(_buffer.CurrentSnapshot, 0, 0);
            Assert.Equal("hello", range.Extent.GetText());
            Assert.Equal(1, range.Count);
            Assert.Equal(0, range.StartLineNumber);
            Assert.Equal(0, range.LastLineNumber);
        }

        [WpfFact]
        public void CreateForLineNumberRange2()
        {
            Create("hello", "world");
            var range = SnapshotLineRangeUtil.CreateForLineNumberRange(_buffer.CurrentSnapshot, 0, 1);
            Assert.Equal("hello" + Environment.NewLine + "world", range.Extent.GetText());
            Assert.Equal(2, range.Count);
            Assert.Equal(0, range.StartLineNumber);
            Assert.Equal(1, range.LastLineNumber);
        }

        /// <summary>
        /// Sanity check for valid count
        /// </summary>
        [WpfFact]
        public void CreateForLineAndMaxCount1()
        {
            Create("a", "b");
            var range = SnapshotLineRangeUtil.CreateForLineAndMaxCount(_buffer.GetLine(0), 1);
            Assert.Equal(1, range.Count);
            Assert.Equal(0, range.StartLineNumber);
            Assert.Equal(0, range.LastLineNumber);
        }

        /// <summary>
        /// Very high count
        /// </summary>
        [WpfFact]
        public void CreateForLineAndMaxCount2()
        {
            Create("a", "b");
            var range = SnapshotLineRangeUtil.CreateForLineAndMaxCount(_buffer.GetLine(0), 100);
            Assert.Equal(2, range.Count);
            Assert.Equal(0, range.StartLineNumber);
            Assert.Equal(1, range.LastLineNumber);
        }

        /// <summary>
        /// Count exactly at the max
        /// </summary>
        [WpfFact]
        public void CreateForLineAndMaxCount3()
        {
            Create("a", "b");
            var range = SnapshotLineRangeUtil.CreateForLineAndMaxCount(_buffer.GetLine(0), 2);
            Assert.Equal(2, range.Count);
            Assert.Equal(0, range.StartLineNumber);
            Assert.Equal(1, range.LastLineNumber);
        }
    }
}
