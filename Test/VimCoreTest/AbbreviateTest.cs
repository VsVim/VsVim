using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Vim.Extensions;

namespace Vim.UnitTest
{
    public abstract class AbbreviateTest : VimTestBase
    {
        public sealed class AbbreviateKindTest : AbbreviateTest
        {
            [WpfTheory]
            [InlineData("foo")]
            [InlineData("g3")]
            public void FullId(string item)
            {
                var kind = AbbreviateKind.OfString(item);
                Assert.True(kind.IsSome(x => x.IsFullId));
            }

            [WpfTheory]
            [InlineData("#i")]
            [InlineData("..f")]
            [InlineData("$/7")]
            public void EndId(string item)
            {
                var kind = AbbreviateKind.OfString(item);
                Assert.True(kind.IsSome(x => x.IsEndId));
            }

            [WpfTheory]
            [InlineData("def#")]
            [InlineData("4/7$")]
            public void NonId(string item)
            {
                var kind = AbbreviateKind.OfString(item);
                Assert.True(kind.IsSome(x => x.IsNonId));
            }

            [WpfTheory]
            [InlineData("a.b")]
            [InlineData("#def")]
            [InlineData("a b")]
            [InlineData("_$r")]
            public void Invalid(string item)
            {
                var kind = AbbreviateKind.OfString(item);
                Assert.True(kind.IsNone());
            }
        }
    }
}
