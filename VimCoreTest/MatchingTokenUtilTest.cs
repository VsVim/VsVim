using EditorUtils;
using Microsoft.VisualStudio.Text;
using Vim.Extensions;
using Xunit;

namespace Vim.UnitTest
{
    public abstract class MatchingTokenUtilTest : VimTestBase
    {
        internal MatchingTokenUtil _matchingTokenUtil;

        protected MatchingTokenUtilTest()
        {
            _matchingTokenUtil = new MatchingTokenUtil();
        }

        public sealed class FindMatchingTokenKindTest : MatchingTokenUtilTest
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

        public sealed class FindMatchingTokenTest : MatchingTokenUtilTest
        {
            private ITextBuffer _textBuffer;

            private void Create(params string[] lines)
            {
                _textBuffer = CreateTextBuffer(lines);
            }

            private void AssertMatch(int index, int matchIndex, int matchLength)
            {
                var point = new SnapshotPoint(_textBuffer.CurrentSnapshot, index);
                var result = _matchingTokenUtil.FindMatchingToken(point);
                Assert.True(result.IsSome());
                Assert.Equal(matchIndex, result.Value.Start);
                Assert.Equal(matchLength, result.Value.Length);
            }

            [Fact]
            public void ParenSimple()
            {
                Create("()");
                AssertMatch(0, 1, 1);
                AssertMatch(1, 0, 1);
            }

            [Fact]
            public void ParenMultiline()
            {
                Create("(", ")");
                AssertMatch(0, _textBuffer.GetPointInLine(1, 0).Position, 1);
                AssertMatch(_textBuffer.GetPointInLine(1, 0).Position, 0, 1);
            }

            [Fact]
            public void CommentSimple()
            {
                Create("/* */");
                AssertMatch(0, 3, 2);
                AssertMatch(3, 0, 2);
            }

            [Fact]
            public void CommentMultiline()
            {
                Create("/*", "*/");
                AssertMatch(0, _textBuffer.GetPointInLine(1, 0).Position, 2);
                AssertMatch(_textBuffer.GetPointInLine(1, 0).Position, 0, 2);
            }

            [Fact]
            public void ConditionalSimple()
            {
                Create("#if", "#endif");
                AssertMatch(0, _textBuffer.GetPointInLine(1, 0).Position, 6);
                AssertMatch(_textBuffer.GetPointInLine(1, 0).Position, 0, 3);
            }

            [Fact]
            public void ConditionalFromElse()
            {
                Create("#if", "#else", "#endif");
                AssertMatch(_textBuffer.GetPointInLine(1, 0).Position, _textBuffer.GetPointInLine(2, 0).Position, 6);
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

        public sealed class ParseConditionalBlocks : MatchingTokenUtilTest
        {
            private ITextBuffer _textBuffer;

            private void Create(params string[] lines)
            {
                _textBuffer = CreateTextBuffer(lines);
            }

            private void AssertBlock(ConditionalBlock block, params SnapshotSpan[] spans)
            {
                Assert.Equal(spans.Length, block.Conditionals.Count);
                for (int i = 0; i < spans.Length; i++)
                {
                    Assert.Equal(spans[i].Span, block.Conditionals[i].Span);
                }
            }

            private void AssertSingle(params SnapshotSpan[] spans)
            {
                var blocks = _matchingTokenUtil.ParseConditionalBlocks(_textBuffer.CurrentSnapshot);
                Assert.Equal(1, blocks.Count);
                AssertBlock(blocks[0], spans);
            }

            [Fact]
            public void Simple()
            {
                Create("#if", "#endif");
                AssertSingle(
                    _textBuffer.GetLineSpan(0, 3),
                    _textBuffer.GetLineSpan(1, 6));
            }

            [Fact]
            public void SimpleWithElse()
            {
                Create("#if", "#else", "#endif");
                AssertSingle(
                    _textBuffer.GetLineSpan(0, 3),
                    _textBuffer.GetLineSpan(1, 5),
                    _textBuffer.GetLineSpan(2, 6));
            }

            [Fact]
            public void SimpleWithElif()
            {
                Create("#if", "#elif", "#endif");
                AssertSingle(
                    _textBuffer.GetLineSpan(0, 3),
                    _textBuffer.GetLineSpan(1, 5),
                    _textBuffer.GetLineSpan(2, 6));
            }
        }
    }
}
