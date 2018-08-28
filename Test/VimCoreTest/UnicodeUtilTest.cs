using System;
using System.Linq;
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

        public sealed class MiscTests : UnicodeUtilTest
        {
            [Fact]
            public void Stats()
            {
                Assert.Equal(8, UnicodeUtil.WideBmpIntervalTree.Height);
                Assert.Equal(166, UnicodeUtil.WideBmpIntervalTree.Count);
                Assert.Equal(6, UnicodeUtil.WideAstralIntervalTree.Height);
                Assert.Equal(63, UnicodeUtil.WideAstralIntervalTree.Count);
            }

            [Fact]
            public void EnsureAllPresent()
            {
                foreach (var entry in UnicodeUtil.CreateUnicodeRangeEntries().Where(e => e.Width == EastAsianWidth.Wide))
                {
                    for (var codePoint = entry.Start; codePoint <= entry.Last; codePoint++)
                    {
                        Assert.True(UnicodeUtil.IsWide(codePoint));
                    }
                }
            }
        }
    }
}
