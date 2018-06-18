using System;
using System.Linq;
using Vim.EditorHost;
using Microsoft.VisualStudio.Text;
using Xunit;

namespace Vim.UnitTest
{
    public class SnapshotLineUtilTest : VimTestBase
    {
        private static readonly string[] s_lines = new string[]
            {
                "summary description for this line",
                "some other line",
                "running out of things to make up"
            };

        private ITextBuffer _buffer = null;
        private ITextSnapshot _snapshot = null;

        private void Create(params string[] lines)
        {
            _buffer = CreateTextBuffer(lines);
            _snapshot = _buffer.CurrentSnapshot;
        }

        [WpfFact]
        public void GetPoints1()
        {
            Create("foo");
            var points = SnapshotLineUtil.GetPoints(SearchPath.Forward, _buffer.CurrentSnapshot.GetLineFromLineNumber(0));
            var text = points.Select(x => x.GetChar().ToString()).Aggregate((x, y) => x + y);
            Assert.Equal("foo", text);
        }

        [WpfFact]
        public void GetExtent1()
        {
            Create("foo");
            var span = SnapshotLineUtil.GetExtent(_buffer.GetLine(0));
            Assert.Equal("foo", span.GetText());
        }

        [WpfFact]
        public void GetExtentIncludingLineBreak1()
        {
            Create("foo", "baz");
            var span = SnapshotLineUtil.GetExtentIncludingLineBreak(_buffer.GetLine(0));
            Assert.Equal("foo" + Environment.NewLine, span.GetText());
        }
    }
}
