using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace Vim.UnitTest
{
    public abstract class StringUtilTest
    {
        public sealed class ReplaceNoCase : StringUtilTest
        {
            private void Expect(string source, string toFind, string toReplace, string expected)
            {
                for (int i = 0; i < toFind.Length; i++)
                {
                    var original = toFind[i];
                    var changed = Char.IsUpper(original) ? Char.ToLower(original) : Char.ToUpper(original);
                    var toFindChanged = toFind.Replace(original, changed);
                    var replace = StringUtil.replaceNoCase(source, toFindChanged, toReplace);
                    Assert.Equal(expected, replace);
                }
            }

            [Fact]
            public void Front()
            {
                Expect("cat", "ca", "bel", "belt");
            }

            [Fact]
            public void Back()
            {
                Expect("cat", "at", "ode", "code");
            }

            [Fact]
            public void Middle()
            {
                Expect("cat", "a", "o", "cot");
            }

            [Fact]
            public void Many()
            {
                Expect("ceedeed", "ee", "o", "codod");
            }
        }
    }
}
