using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace Vim.UnitTest
{
    public abstract class SentenceKeyTest
    {
        public sealed class EqualityTest : SentenceKeyTest
        {
            [Fact]
            public void Simple()
            {
                var unit = EqualityUnit
                    .Create(new SentenceKey(SentenceKind.Default, 1))
                    .WithEqualValues(new SentenceKey(SentenceKind.Default, 1))
                    .WithNotEqualValues(new SentenceKey(SentenceKind.Default, 2));
                EqualityUtil.RunAll(unit);
            }

            [Fact]
            public void Simple2()
            {
                var unit = EqualityUnit
                    .Create(new SentenceKey(SentenceKind.Default, 1))
                    .WithEqualValues(new SentenceKey(SentenceKind.Default, 1))
                    .WithNotEqualValues(new SentenceKey(SentenceKind.NoTrailingCharacters, 2));
                EqualityUtil.RunAll(unit);
            }
        }
    }
}
