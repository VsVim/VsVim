using Vim.Extensions;
using Xunit;

namespace Vim.UnitTest
{
    public abstract class MatchingTokenUtilTest
    {
        internal MatchingTokenUtil _matchingTokenUtil;

        protected MatchingTokenUtilTest()
        {
            _matchingTokenUtil = new MatchingTokenUtil();
        }

        public sealed class FindMatchingTokenKind : MatchingTokenUtilTest
        {
            private void AssertKind(MatchingTokenKind kind, string text, int column = 0)
            {
                var found = _matchingTokenUtil.FindMatchingTokenKind(text, column);
                Assert.True(found.IsSome());
                Assert.Equal(kind, found.Value);
            }

            private void AssertNone(string text, int column = 0)
            {
                var found = _matchingTokenUtil.FindMatchingTokenKind(text, column);
                Assert.False(found.IsSome());
            }

            [Fact]
            public void SimpleConditional()
            {
                AssertKind(MatchingTokenKind.Conditional, "#if");
                AssertKind(MatchingTokenKind.Conditional, "# if");
                AssertKind(MatchingTokenKind.Conditional, " #if");
            }

            [Fact]
            public void SimpleParens()
            {
                AssertKind(MatchingTokenKind.Parens, "()");
                AssertKind(MatchingTokenKind.Parens, "()", column: 1);
            }

            [Fact]
            public void SimpleBraces()
            {
                AssertKind(MatchingTokenKind.Braces, "{}");
                AssertKind(MatchingTokenKind.Braces, "{}", column: 1);
            }

            [Fact]
            public void SimpleBrackets()
            {
                AssertKind(MatchingTokenKind.Brackets, "[]");
                AssertKind(MatchingTokenKind.Brackets, "[]", column: 1);
            }

            [Fact]
            public void CloserWinsConditional()
            {
                AssertKind(MatchingTokenKind.Conditional, "#if (");
                AssertKind(MatchingTokenKind.Parens, "#if (", column: 4);
            }

            [Fact]
            public void CloserWinsParens()
            {
                AssertKind(MatchingTokenKind.Parens, "( [");
                AssertKind(MatchingTokenKind.Brackets, "( [", column: 1);
            }
        }

        public sealed class ParseConditionalTest : MatchingTokenUtilTest
        {
            private void AssertParsed(string text, int start, int length)
            {
                var result = _matchingTokenUtil.ParseConditional(text);
                Assert.True(result.IsSome());
                Assert.Equal(start, result.Value.Span.Start);
                Assert.Equal(length, result.Value.Span.Length);
            }

            [Fact]
            public void SimpleIf()
            {
                AssertParsed("#if", 0, 3);
            }

            [Fact]
            public void SpaceBeforePoundIf()
            {
                AssertParsed(" #if", 1, 3);
            }

            [Fact]
            public void SpaceAfterPoundIf()
            {
                AssertParsed("# if", 0, 4);
            }
        }
    }
}
