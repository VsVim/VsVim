using Xunit;

namespace Vim.UnitTest
{
    public abstract class EditUtilTest
    {
        public sealed class NormalizeNewLinesTest : EditUtilTest
        {
            [Fact]
            public void UnixToDos()
            {
                var text = EditUtil.NormalizeNewLines("dog\ncat", "\r\n");
                Assert.Equal("dog\r\ncat", text);
            }

            [Fact]
            public void UnixToDosAtEnd()
            {
                var text = EditUtil.NormalizeNewLines("dog\ncat\n", "\r\n");
                Assert.Equal("dog\r\ncat\r\n", text);
            }

            [Fact]
            public void DosToUnix()
            {
                var text = EditUtil.NormalizeNewLines("dog\r\ncat", "\n");
                Assert.Equal("dog\ncat", text);
            }

            [Fact]
            public void DosToUnixAtEnd()
            {
                var text = EditUtil.NormalizeNewLines("dog\r\ncat\r\n", "\n");
                Assert.Equal("dog\ncat\n", text);
            }
        }
    }
}
