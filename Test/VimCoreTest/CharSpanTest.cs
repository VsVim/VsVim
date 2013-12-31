using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace Vim.UnitTest
{
    public abstract class CharSpanTest
    {
        public sealed class EqualityTest : CharSpanTest
        {
            private void Run(EqualityUnit<CharSpan> equalityUnit)
            {
                EqualityUtil.RunAll(equalityUnit);
            }

            [Fact]
            public void SameString()
            {
                var data = "big dog";
                Run(EqualityUnit
                    .Create(new CharSpan(data, CharComparer.Exact))
                    .WithEqualValues(new CharSpan(data, CharComparer.Exact))
                    .WithNotEqualValues(new CharSpan("other", CharComparer.Exact)));
            }

            [Fact]
            public void DifferentString()
            {
                Run(EqualityUnit
                    .Create(new CharSpan("big dog", CharComparer.Exact))
                    .WithEqualValues(new CharSpan("big dog", CharComparer.Exact))
                    .WithNotEqualValues(new CharSpan("other", CharComparer.Exact)));
            }

            [Fact]
            public void DifferentStringSameValue()
            {
                Run(EqualityUnit
                    .Create(new CharSpan("big dog", 0, 3, CharComparer.Exact))
                    .WithEqualValues(new CharSpan("a big dog", 2, 3, CharComparer.Exact))
                    .WithNotEqualValues(new CharSpan("other", CharComparer.Exact)));
            }

            [Fact]
            public void IgnoreCase()
            {
                Run(EqualityUnit
                    .Create(new CharSpan("big dog", 0, 3, CharComparer.IgnoreCase))
                    .WithEqualValues(new CharSpan("a BIG dog", 2, 3, CharComparer.IgnoreCase))
                    .WithNotEqualValues(new CharSpan("other", CharComparer.IgnoreCase)));
            }
        }
    }
}
