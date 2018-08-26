using System;
using Xunit;

namespace Vim.UnitTest
{
    public abstract class UnicodeUtilTest
    {
        public sealed class IsWideTest : UnicodeUtilTest
        {
            [Fact]
            public void Simple()
            {
                Assert.False(UnicodeUtil.IsWideBmp('a'));
            }

            [Fact]
            public void Alien()
            {
                const string alien = "\U0001F47D"; // 👽
                var codePoint = Char.ConvertToUtf32(alien, 0);
                Assert.True(UnicodeUtil.IsWideAstral(codePoint));
            }
        }
    }
}
