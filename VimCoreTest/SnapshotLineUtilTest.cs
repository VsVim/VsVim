using System;
using System.Linq;
using EditorUtils;
using Microsoft.VisualStudio.Text;
using Xunit;

namespace Vim.UnitTest
{
    public class SnapshotLineUtilTest : VimTestBase
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
            _buffer = CreateTextBuffer(lines);
            _snapshot = _buffer.CurrentSnapshot;
        }

        [Fact]
        public void GetPoints1()
        {
            Create("foo");
            var points = SnapshotLineUtil.GetPoints(Path.Forward, _buffer.CurrentSnapshot.GetLineFromLineNumber(0));
            var text = points.Select(x => x.GetChar().ToString()).Aggregate((x, y) => x + y);
            Assert.Equal("foo",text);
        }

        [Fact]
        public void GetExtent1()
        {
            Create("foo");
            var span = SnapshotLineUtil.GetExtent(_buffer.GetLine(0));
            Assert.Equal("foo", span.GetText());
        }

        [Fact]
        public void GetExtentIncludingLineBreak1()
        {
            Create("foo", "baz");
            var span = SnapshotLineUtil.GetExtentIncludingLineBreak(_buffer.GetLine(0));
            Assert.Equal("foo" + Environment.NewLine, span.GetText());
        }


    }
}
