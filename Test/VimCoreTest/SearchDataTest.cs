﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace Vim.UnitTest
{
    public abstract class SearchDataTest
    {
        public sealed class EqualityTest : SearchDataTest
        {
            private void Run(EqualityUnit<SearchData> equalityUnit)
            {
                EqualityUtil.RunAll(
                    (x, y) => x == y,
                    (x, y) => x != y,
                    equalityUnit);
            }

            [Fact]
            public void Pattern()
            {
                Run(EqualityUnit
                    .Create(new SearchData("world", SearchPath.Forward, true))
                    .WithEqualValues(new SearchData("world", SearchPath.Forward, true))
                    .WithNotEqualValues(new SearchData("hello", SearchPath.Forward, true)));
            }

            [Fact]
            public void Paths()
            {
                Run(EqualityUnit
                    .Create(new SearchData("world", SearchPath.Forward, true))
                    .WithEqualValues(new SearchData("world", SearchPath.Forward, true))
                    .WithNotEqualValues(new SearchData("world", SearchPath.Backward, true)));
            }

            [Fact]
            public void Wrap()
            {
                Run(EqualityUnit
                    .Create(new SearchData("world", SearchPath.Forward, true))
                    .WithEqualValues(new SearchData("world", SearchPath.Forward, true))
                    .WithNotEqualValues(new SearchData("world", SearchPath.Forward, false)));
            }
        }

        public sealed class MiscTest : SearchDataTest
        {
            [Fact]
            public void Options()
            {
                Assert.Equal(SearchOptions.Default, SearchOptions.ConsiderIgnoreCase | SearchOptions.ConsiderSmartCase);
            }
        }
    }
}
