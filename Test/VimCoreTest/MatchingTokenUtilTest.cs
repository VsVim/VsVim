using System.Collections.Generic;
using EditorUtils;
using Microsoft.VisualStudio.Text;
using Vim.Extensions;
using Xunit;
using Moq;

namespace Vim.UnitTest
{
    public abstract class MatchingTokenUtilTest : VimTestBase
    {
        internal readonly MatchingTokenUtil _matchingTokenUtil;

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
            public void ConditionalSimple()
            {
                AssertKind(MatchingTokenKind.Directive, "#if");
                AssertKind(MatchingTokenKind.Directive, "# if");
                AssertKind(MatchingTokenKind.Directive, " #if");
            }

            /// <summary>
            /// The span of an #if is the entire line.  
            /// </summary>
            [Fact]
            public void ConditionalPastToken()
            {
                AssertKind(MatchingTokenKind.Directive, "#if rabbit dog", 10);
                AssertKind(MatchingTokenKind.Directive, "#if rabbit dog", 7);
            }

            [Fact]
            public void ParensSimple()
            {
                AssertKind(MatchingTokenKind.Parens, "()");
                AssertKind(MatchingTokenKind.Parens, "()", column: 1);
            }

            [Fact]
            public void BracesSimple()
            {
                AssertKind(MatchingTokenKind.Braces, "{}");
                AssertKind(MatchingTokenKind.Braces, "{}", column: 1);
            }

            [Fact]
            public void BracketsSimple()
            {
                AssertKind(MatchingTokenKind.Brackets, "[]");
                AssertKind(MatchingTokenKind.Brackets, "[]", column: 1);
            }

            [Fact]
            public void CloserWinsConditional()
            {
                AssertKind(MatchingTokenKind.Directive, "#if (");
                AssertKind(MatchingTokenKind.Parens, "#if (", column: 4);
            }

            [Fact]
            public void CloserWinsParens()
            {
                AssertKind(MatchingTokenKind.Parens, "( [");
                AssertKind(MatchingTokenKind.Brackets, "( [", column: 1);
            }

            [Fact]
            public void CloserParensPastIfConditional()
            {
                var lineText = "#if foo ()";
                AssertKind(MatchingTokenKind.Directive, lineText, column: 0);
                for (int i = 1; i < lineText.Length; i++)
                {
                    AssertKind(MatchingTokenKind.Parens, lineText, column: i);
                }
            }

            [Fact]
            public void CommentEnd()
            {
                AssertKind(MatchingTokenKind.Comment, "*/", 0);
                AssertKind(MatchingTokenKind.Comment, "*/", 1);
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
            public void ParenNested()
            {
                Create("(((a)))");
                AssertMatch(0, 6, 1);
                AssertMatch(6, 0, 1);
                AssertMatch(1, 5, 1);
                AssertMatch(5, 1, 1);
                AssertMatch(2, 4, 1);
                AssertMatch(4, 2, 1);
            }

            [Fact]
            public void ParenConsequtive()
            {
                Create("()()");
                AssertMatch(0, 1, 1);
                AssertMatch(1, 0, 1);
                AssertMatch(2, 3, 1);
                AssertMatch(3, 2, 1);
            }

            [Fact]
            public void CommentSimple()
            {
                Create("/* */");
                AssertMatch(0, 4, 1);
                AssertMatch(3, 0, 1);
                AssertMatch(4, 0, 1);
            }

            [Fact]
            public void CommentMultiline()
            {
                Create("/*", "*/");
                AssertMatch(0, _textBuffer.GetPointInLine(1, 1).Position, 1);
                AssertMatch(_textBuffer.GetPointInLine(1, 0).Position, 0, 1);
            }

            [Fact]
            public void CommentWrongNesting()
            {
                Create("/* /* */");
                AssertMatch(0, 7, 1);
                AssertMatch(6, 0, 1);
                AssertMatch(7, 0, 1);
                AssertMatch(3, 7, 1);
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

            [Fact]
            public void ConditionalCaretInMiddleOfToken()
            {
                Create("#if", "#endif");
                AssertMatch(1, _textBuffer.GetPointInLine(1, 0).Position, 6);
                AssertMatch(_textBuffer.GetPointInLine(1, 3).Position, 0, 3);
            }

            [Fact]
            public void Mixed()
            {
                Create("{ { (( } /* a /*) b */ })");
                AssertMatch(0, 23, 1);
                AssertMatch(2, 7, 1);
                AssertMatch(4, 24, 1);
                AssertMatch(5, 16, 1);
                AssertMatch(9, 21, 1);
            }
        }

        public sealed class ParseDirectiveTest : MatchingTokenUtilTest
        {
            private void AssertParsed(string text, int start, int length)
            {
                var result = _matchingTokenUtil.ParseDirective(text);
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

        public sealed class ParseDirectiveBlockTest : MatchingTokenUtilTest
        {
            private ITextBuffer _textBuffer;

            private void Create(params string[] lines)
            {
                _textBuffer = CreateTextBuffer(lines);
            }

            private List<DirectiveBlock> ParseSorted()
            {
                var blocks = _matchingTokenUtil.ParseDirectiveBlocks(_textBuffer.CurrentSnapshot);
                blocks.Sort((left, right) => left.Directives[0].Span.Start.CompareTo(right.Directives[0].Span.Start));
                return blocks;
            }

            private void AssertBlock(DirectiveBlock block, params SnapshotSpan[] spans)
            {
                Assert.Equal(spans.Length, block.Directives.Count);
                for (int i = 0; i < spans.Length; i++)
                {
                    Assert.Equal(spans[i].Span, block.Directives[i].Span);
                }
            }

            private void AssertSingle(params SnapshotSpan[] spans)
            {
                var blocks = _matchingTokenUtil.ParseDirectiveBlocks(_textBuffer.CurrentSnapshot);
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

            [Fact]
            public void NestedSingleInIf()
            {
                Create("#if", "#if", "#endif", "#endif");
                var blocks = ParseSorted();
                AssertBlock(
                    blocks[0],
                    _textBuffer.GetLineSpan(0, 3),
                    _textBuffer.GetLineSpan(3, 6));
                AssertBlock(
                    blocks[1],
                    _textBuffer.GetLineSpan(1, 3),
                    _textBuffer.GetLineSpan(2, 6));
            }

            [Fact]
            public void NestedSingleInIfWithElse()
            {
                Create("#if", "#if", "#endif", "#else", "#endif");
                var blocks = ParseSorted();
                AssertBlock(
                    blocks[0],
                    _textBuffer.GetLineSpan(0, 3),
                    _textBuffer.GetLineSpan(3, 5),
                    _textBuffer.GetLineSpan(4, 6));
                AssertBlock(
                    blocks[1],
                    _textBuffer.GetLineSpan(1, 3),
                    _textBuffer.GetLineSpan(2, 6));
            }

            [Fact]
            public void NestedSingleInElse()
            {
                Create("#if", "#else", "#if", "#endif", "#endif");
                var blocks = ParseSorted();
                AssertBlock(
                    blocks[0],
                    _textBuffer.GetLineSpan(0, 3),
                    _textBuffer.GetLineSpan(1, 5),
                    _textBuffer.GetLineSpan(4, 6));
                AssertBlock(
                    blocks[1],
                    _textBuffer.GetLineSpan(2, 3),
                    _textBuffer.GetLineSpan(3, 6));
            }

            [Fact]
            public void InSequence()
            {
                Create("#if", "#endif", "#if", "#endif");
                var blocks = ParseSorted();
                AssertBlock(
                    blocks[0],
                    _textBuffer.GetLineSpan(0, 3),
                    _textBuffer.GetLineSpan(1, 6));
                AssertBlock(
                    blocks[1],
                    _textBuffer.GetLineSpan(2, 3),
                    _textBuffer.GetLineSpan(3, 6));
            }
        }

        public sealed class FindUnmatchedTokenTest : MatchingTokenUtilTest
        {
            private ITextBuffer _textBuffer;

            private void Create(params string[] lines)
            {
                _textBuffer = CreateTextBuffer(lines);
            }

            private SnapshotPoint Find(Path path, UnmatchedTokenKind kind, int position = 0, int count = 1)
            {
                var point = _textBuffer.GetPoint(position);
                var found = _matchingTokenUtil.FindUnmatchedToken(path, kind, point, count);
                Assert.True(found.IsSome());
                return found.Value;
            }

            [Fact]
            public void BraceForward()
            {
                Create(" {}}");
                var point = Find(Path.Forward, UnmatchedTokenKind.CurlyBracket);
                Assert.Equal(3, point.Position);
            }

            [Fact]
            public void BraceBackward()
            {
                Create("{{}  ");
                var point = Find(Path.Backward, UnmatchedTokenKind.CurlyBracket, 4);
                Assert.Equal(0, point.Position);
            }
        }

        public sealed class MiscTest : MatchingTokenUtilTest
        {
            private ITextBuffer _textBuffer;

            private void Create(params string[] lines)
            {
                _textBuffer = CreateTextBuffer(lines);
            }

            /// <summary>
            /// Make sure that the caching logic will recognize the buffer change and actually
            /// parse the new snapshot
            /// </summary>
            [Fact]
            public void Caching()
            {
                Create("#if", "#endif", "#if", "#endif");
                var blocks = _matchingTokenUtil.GetDirectiveBlocks(_textBuffer.CurrentSnapshot);
                Assert.Equal(2, blocks.Count);
                _textBuffer.Delete(_textBuffer.GetLineRange(2, 3).ExtentIncludingLineBreak);
                blocks = _matchingTokenUtil.GetDirectiveBlocks(_textBuffer.CurrentSnapshot);
                Assert.Equal(1, blocks.Count);
            }

            /// <summary>
            /// This addresses issue 1366.  Essentially ensure absolutely that the cache is being
            /// used 
            /// </summary>
            [Fact]
            public void EnsureCacheUsed()
            {
                Create("#if", "#endif", "#if", "#endif");
                var snapshot = _textBuffer.CurrentSnapshot;
                var blocks = _matchingTokenUtil.GetDirectiveBlocks(snapshot);
                Assert.Equal(2, blocks.Count);

                var factory = new MockRepository(MockBehavior.Strict);
                var mockBuffer = factory.Create<ITextBuffer>();
                mockBuffer.SetupGet(x => x.Properties).Returns(snapshot.TextBuffer.Properties);
                var mockSnapshot = factory.Create<ITextSnapshot>();
                mockSnapshot.SetupGet(x => x.Version).Returns(snapshot.Version);
                mockSnapshot.SetupGet(x => x.TextBuffer).Returns(mockBuffer.Object);

                blocks = _matchingTokenUtil.GetDirectiveBlocks(mockSnapshot.Object);
                Assert.Equal(2, blocks.Count);
            }
        }
    }
}
