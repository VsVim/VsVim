using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Vim.Extensions;

namespace Vim.UnitTest
{
    public sealed class VimCharSetTest
    {
        [Theory]
        [InlineData("a-z", "abc", "!@#$")]
        [InlineData("z", "z", "abc123")]
        [InlineData("1-Z", "ABCZ", "abcz")]
        [InlineData("1-2", "", "12")]
        [InlineData("1-2,3-4", "", "1234")]
        [InlineData("1-2,a", "a", "b")]
        [InlineData("1-2,a,b", "ab", "c")]
        [InlineData("1-2,#", "#", "")]
        [InlineData("48-57,,,_", "123,_", "abc")]
        [InlineData("@-@", "@", "abc")]
        [InlineData("@-@,@", "@abcABC", ",-")]
        [InlineData("^@,c,a", "ca", "bdef")]
        [InlineData("@,^a-z", "ABC", "abc")]
        [InlineData(" -~,^,,9", " ~9", ",")]
        [InlineData("a,^", "a^", "b")]
        public void Parse(string text, string include, string exclude)
        {
            var option = VimCharSet.TryParse(text);
            Assert.True(option.IsSome());
            var vimCharSet = option.Value;
            Assert.Equal(text, vimCharSet.Text);

            foreach (var c in include)
            {
                Assert.True(vimCharSet.Contains(c));
            }

            foreach (var c in exclude)
            {
                Assert.False(vimCharSet.Contains(c));
            }
        }
    }
}
