using System;
using System.Collections.Generic;
using Vim.EditorHost;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Xunit;
using Vim.Extensions;

namespace Vim.UnitTest
{
    public abstract class VisualSpanTest : VimTestBase
    {
        private IVimBuffer _vimBuffer;
        private ITextView _textView;
        private ITextBuffer _textBuffer;

        protected virtual void Create(params string[] lines)
        {
            _vimBuffer = CreateVimBuffer(lines);
            _textView = _vimBuffer.TextView;
            _textBuffer = _textView.TextBuffer;
        }

        public abstract class CreateForSelectionTest : VisualSpanTest
        {
            public sealed class CharacterTest : CreateForSelectionTest
            {
                [WpfFact]
                public void IncludeLineBreak()
                {
                    Create("cat", "dog");
                    _textView.Selection.Select(_textBuffer.GetPoint(0), _textBuffer.GetPoint(5));
                    TestableSynchronizationContext.RunAll();
                    var visualSpan = VisualSpan.CreateForSelection(_textView, VisualKind.Character, tabStop: 4);
                    var characterSpan = visualSpan.AsCharacter().Item;
                    Assert.True(characterSpan.IncludeLastLineLineBreak);
                    Assert.Equal(1, characterSpan.LineCount);
                }

                [WpfFact]
                public void EndsInEmptyLineCase()
                {
                    Create("cat", "", "dog");
                    _textView.Selection.Select(_textBuffer.GetPoint(0), _textBuffer.GetPoint(6));
                    TestableSynchronizationContext.RunAll();
                    Assert.Equal(1, _textView.Selection.StreamSelectionSpan.End.Position.GetContainingLine().LineNumber);
                    var visualSpan = VisualSpan.CreateForSelection(_textView, VisualKind.Character, tabStop: 4);
                    var characterSpan = visualSpan.AsCharacter().Item;
                    Assert.Equal(2, characterSpan.LineCount);
                    Assert.True(characterSpan.IncludeLastLineLineBreak);
                }

                /// <summary>
                /// An empty selection should produce an empty VisualSpan for character
                /// </summary>
                [WpfFact]
                public void Empty()
                {
                    Create("hello world");
                    var visualSpan = VisualSpan.CreateForSelection(_textView, VisualKind.Character, _vimBuffer.LocalSettings.TabStop);
                    Assert.Equal(0, visualSpan.EditSpan.OverarchingSpan.Length);
                }

                /// <summary>
                /// Creating a virtual span for a selection doesn't depend on the contents of the line
                /// </summary>
                /// <param name="line1"></param>
                [WpfTheory]
                [InlineData("")]
                [InlineData("a")]
                [InlineData("ab")]
                [InlineData("abc")]
                [InlineData("abcd")]
                [InlineData("abcde")]
                public void Virtual(string line1)
                {
                    Create(line1, "");
                    var point1 = _textBuffer.GetVirtualPointInLine(0, 0);
                    var point2 = _textBuffer.GetVirtualPointInLine(0, 4);
                    var span = new VirtualSnapshotSpan(point1, point2);
                    _textView.Selection.Select(point1, point2);
                    TestableSynchronizationContext.RunAll();
                    var visualSpan = VisualSpan.CreateForVirtualSelection(_textView, VisualKind.Character, tabStop: 4, useVirtualSpace: true);
                    Assert.Equal(point1, visualSpan.AsCharacter().Item.VirtualStart);
                    Assert.Equal(point2, visualSpan.AsCharacter().Item.VirtualEnd);
                    Assert.Equal(span.Length, visualSpan.AsCharacter().Item.VirtualLength);
                }
            }

            public sealed class LineTest : CreateForSelectionTest
            {
                /// <summary>
                /// An empty selection should still produce a complete line selection for line
                /// </summary>
                [WpfFact]
                public void Empty()
                {
                    Create("hello world");
                    var visualSpan = VisualSpan.CreateForSelection(_textView, VisualKind.Line, _vimBuffer.LocalSettings.TabStop);
                    Assert.Equal(_textBuffer.GetLineRange(0), visualSpan.AsLine().Item);
                }
            }

            public sealed class BlockTest : CreateForSelectionTest
            {
                /// <summary>
                /// Visual Span respects vim semantics here and hence will change an empty selection
                /// into one with at least a single space 
                /// </summary>
                [WpfFact]
                public void Empty()
                {
                    Create("hello world");
                    var visualSpan = VisualSpan.CreateForSelection(_textView, VisualKind.Block, _vimBuffer.LocalSettings.TabStop);
                    var blockSpan = new BlockSpan(_textBuffer.GetPoint(0), tabStop: _vimBuffer.LocalSettings.TabStop, spaces: 1, height: 1);
                    Assert.Equal(blockSpan, visualSpan.AsBlock().Item);
                }
            }
        }

        public abstract class CreateForSelectionPointsTest : VisualSpanTest
        {
            public sealed class BlockTest : CreateForSelectionPointsTest
            {
                /// <summary>
                /// Ensure creating a VisualSpan for an empty points results in an a 1 space block 
                /// selection
                /// </summary>
                [WpfFact]
                public void Empty()
                {
                    Create("dog cat");
                    var point = _textBuffer.GetPoint(2);
                    var visualSpan = VisualSpan.CreateForSelectionPoints(VisualKind.Block, point, point, _vimBuffer.LocalSettings.TabStop);
                    var blockSpan = new BlockSpan(point, _vimBuffer.LocalSettings.TabStop, spaces: 1, height: 1);
                    Assert.Equal(blockSpan, visualSpan.AsBlock().Item);
                }

                [WpfFact]
                public void Backwards()
                {
                    Create("big cat", "big dog");
                    var visualSpan = VisualSpan.CreateForSelectionPoints(VisualKind.Block, _textBuffer.GetPoint(2), _textBuffer.GetPoint(0), _vimBuffer.LocalSettings.TabStop);
                    var blockSpan = new BlockSpan(_textBuffer.GetPoint(0), _vimBuffer.LocalSettings.TabStop, 2, 1);
                    Assert.Equal(blockSpan, visualSpan.AsBlock().Item);
                }

                /// <summary>
                /// Make sure that we properly handle the backward block selection which spans 
                /// multiple lines
                /// </summary>
                [WpfFact]
                public void BackwardsMultipleLines()
                {
                    Create("big cat", "big dog");
                    var visualSpan = VisualSpan.CreateForSelectionPoints(VisualKind.Block, _textBuffer.GetPoint(2), _textBuffer.GetPointInLine(1, 1), _vimBuffer.LocalSettings.TabStop);
                    var blockSpan = new BlockSpan(_textBuffer.GetPoint(1), _vimBuffer.LocalSettings.TabStop, 1, 2);
                    Assert.Equal(blockSpan, visualSpan.AsBlock().Item);
                }

                /// <summary>
                /// Make sure that we properly handle the forward block selection which spans 
                /// multiple lines
                /// </summary>
                [WpfFact]
                public void ForwardsMultipleLines()
                {
                    Create("big cat", "big dog");
                    var visualSpan = VisualSpan.CreateForSelectionPoints(VisualKind.Block, _textBuffer.GetPoint(1), _textBuffer.GetPointInLine(1, 3), _vimBuffer.LocalSettings.TabStop);
                    var blockSpan = new BlockSpan(_textBuffer.GetPoint(1), _vimBuffer.LocalSettings.TabStop, 2, 2);
                    Assert.Equal(blockSpan, visualSpan.AsBlock().Item);
                }
            }

            public sealed class CharacterTest : CreateForSelectionPointsTest
            {
                /// <summary>
                /// Ensure creating a VisualSpan for an empty points results in an empty selection
                /// </summary>
                [WpfFact]
                public void Empty()
                {
                    Create("dog cat");
                    var point = _textBuffer.GetPoint(2);
                    var visualSpan = VisualSpan.CreateForSelectionPoints(VisualKind.Character, point, point, _vimBuffer.LocalSettings.TabStop);
                    Assert.Equal(point, visualSpan.AsCharacter().Item.Start);
                    Assert.Equal(0, visualSpan.AsCharacter().Item.Length);
                }

                /// <summary>
                /// A virtual span doesn't depend on the contents of the line
                /// </summary>
                /// <param name="line1"></param>
                [WpfTheory]
                [InlineData("")]
                [InlineData("a")]
                [InlineData("ab")]
                [InlineData("abc")]
                [InlineData("abcd")]
                [InlineData("abcde")]
                public void Virtual(string line1)
                {
                    Create(line1, "");
                    var point1 = _textBuffer.GetVirtualPointInLine(0, 0);
                    var point2 = _textBuffer.GetVirtualPointInLine(0, 4);
                    var span = new VirtualSnapshotSpan(point1, point2);
                    var visualSpan = VisualSpan.CreateForVirtualSelectionPoints(VisualKind.Character, point1, point2, _vimBuffer.LocalSettings.TabStop, true);
                    Assert.Equal(point1, visualSpan.AsCharacter().Item.VirtualStart);
                    Assert.Equal(point2, visualSpan.AsCharacter().Item.VirtualEnd);
                    Assert.Equal(span.Length, visualSpan.AsCharacter().Item.VirtualLength);
                }
            }

            public sealed class LineTest : CreateForSelectionPointsTest
            {
                /// <summary>
                /// Ensure we handle the case where the start and end point are the same point at the 
                /// start of the line.  The code should return the single line range for the line 
                /// containing the points
                /// </summary>
                [WpfFact]
                public void SamePoint()
                {
                    Create("cat", "dog", "tree");
                    var point = _textBuffer.GetLine(1).Start;
                    var visualSpan = VisualSpan.CreateForSelectionPoints(VisualKind.Line, point, point, _vimBuffer.LocalSettings.TabStop);
                    Assert.Equal(_textBuffer.GetLineRange(1), visualSpan.AsLine().LineRange);
                }

                /// <summary>
                /// Make sure the code handles the case where the caret is positioned at the end of the
                /// ITextSnapshot.  Should return the last line
                /// </summary>
                [WpfFact]
                public void EndOfSnapshot()
                {
                    Create("cat", "dog");
                    var point = new SnapshotPoint(_textBuffer.CurrentSnapshot, _textBuffer.CurrentSnapshot.Length);
                    var visualSpan = VisualSpan.CreateForSelectionPoints(VisualKind.Line, point, point, _vimBuffer.LocalSettings.TabStop);
                    Assert.Equal(1, visualSpan.AsLine().LineRange.LastLineNumber);
                }
            }
        }

        public abstract class SelectTest : VisualSpanTest
        {
            /// <summary>
            /// When there are unexpanded tabs in the ITextView the block selection needs to treat
            /// the tabs as the equivalent number of spaces.  
            /// </summary>
            public sealed class BlockWithTabTest : SelectTest
            {
                protected override void Create(params string[] lines)
                {
                    Create(2, lines);
                }

                private void Create(int tabStop, params string[] lines)
                {
                    base.Create();
                    UpdateTabStop(_vimBuffer, tabStop);
                    _vimBuffer.TextView.SetText(lines);
                }

                /// <summary>
                /// In this scenario the caret has moved past the tab and onto the start of the next 
                /// letter. The selection will encompass the tab and the letter 
                ///     trucker
                ///       cat
                /// </summary>
                [WpfFact]
                public void SimpleCaretPastTab()
                {
                    Create("trucker", "\tcat");
                    var blockSpan = new BlockSpan(_textBuffer.GetPoint(1), tabStop: 2, spaces: 2, height: 2);
                    var visualSpan = VisualSpan.NewBlock(blockSpan);
                    visualSpan.Select(_textView, SearchPath.Forward);
                    TestableSynchronizationContext.RunAll();

                    // It may seem odd for the second span to start on column 1 since the tab is partially
                    // included in the line.  However Visual Studio has this behavior.  It won't select a 
                    // character at the start / end of a selection unless it's completely included 
                    Assert.Equal(
                        new[]
                        {
                            _textBuffer.GetLineSpan(0, 1, 2),
                            _textBuffer.GetLineSpan(1, 1, 1)
                        },
                        _textView.Selection.SelectedSpans);
                }

                [WpfFact]
                public void SimpleCaretPastTab2()
                {
                    Create("trucker", "\tcat");
                    var blockSpan = new BlockSpan(_textBuffer.GetPoint(1), tabStop: 2, spaces: 3, height: 2);
                    var visualSpan = VisualSpan.NewBlock(blockSpan);
                    visualSpan.Select(_textView, SearchPath.Forward);
                    TestableSynchronizationContext.RunAll();
                    Assert.Equal(
                        new[]
                        {
                            _textBuffer.GetLineSpan(0, 1, 3),
                            _textBuffer.GetLineSpan(1, 1, 2)
                        },
                        _textView.Selection.SelectedSpans);
                }

                /// <summary>
                /// When the caret is in in the tab and the anchor is visually above the tab.  Then the 
                /// anchor is visually moved to encompass the entire tab
                /// </summary>
                [WpfFact]
                public void CaretInTabAnchorAboveTab()
                {
                    Create(4, "trucker", "\tcat");
                    var blockSpan = new BlockSpan(_textBuffer.GetPoint(1), tabStop: 4, spaces: 1, height: 2);
                    var visualSpan = VisualSpan.NewBlock(blockSpan);
                    visualSpan.Select(_textView, SearchPath.Forward);
                    TestableSynchronizationContext.RunAll();
                    Assert.Equal(
                        new[]
                        {
                            _textBuffer.GetLineSpan(0, 0, 4),
                            _textBuffer.GetLineSpan(1, 0, 1)
                        },
                        _textView.Selection.SelectedSpans);
                }
            }

            public sealed class CharacterTest : SelectTest
            {
                /// <summary>
                /// The selection of a reverse character span should cause a reversed selection
                /// </summary>
                [WpfFact]
                public void Backward()
                {
                    Create("big dog", "big cat", "big tree", "big fish");
                    var characterSpan = new CharacterSpan(_textBuffer.GetSpan(1, 3));
                    var visualSpan = VisualSpan.NewCharacter(characterSpan);
                    visualSpan.Select(_textView, SearchPath.Backward);
                    TestableSynchronizationContext.RunAll();
                    Assert.True(_textView.Selection.IsReversed);
                    Assert.Equal(characterSpan.Span, _textView.GetSelectionSpan());
                }

                [WpfFact]
                public void BackwardIntoLineBreak()
                {
                    Create("cat", "dog");
                    var characterSpan = new CharacterSpan(_textBuffer.GetSpan(0, 4));
                    var visualSpan = VisualSpan.NewCharacter(characterSpan);
                    visualSpan.Select(_textView, SearchPath.Backward);
                    TestableSynchronizationContext.RunAll();
                    Assert.Equal(4, _textView.Selection.StreamSelectionSpan.Length);
                    Assert.True(_textView.Selection.IsReversed);
                }

                /// <summary>
                /// The selection of a forward character span should cause a forward selection
                /// </summary>
                [WpfFact]
                public void Forward()
                {
                    Create("big dog", "big cat", "big tree", "big fish");
                    var characterSpan = new CharacterSpan(_textBuffer.GetSpan(1, 3));
                    var visualSpan = VisualSpan.NewCharacter(characterSpan);
                    visualSpan.Select(_textView, SearchPath.Forward);
                    TestableSynchronizationContext.RunAll();
                    Assert.False(_textView.Selection.IsReversed);
                    Assert.Equal(characterSpan.Span, _textView.GetSelectionSpan());
                }

                [WpfFact]
                public void ForwardIntoLineBreak()
                {
                    Create("cat", "dog");
                    var characterSpan = new CharacterSpan(_textBuffer.GetSpan(0, 4));
                    var visualSpan = VisualSpan.NewCharacter(characterSpan);
                    visualSpan.Select(_textView, SearchPath.Forward);
                    TestableSynchronizationContext.RunAll();
                    Assert.Equal(4, _textView.Selection.StreamSelectionSpan.Length);
                    Assert.False(_textView.Selection.IsReversed);
                }

                /// <summary>
                /// Selection of a virtual span doesn't depend on the contents of the line
                /// </summary>
                /// <param name="line1"></param>
                [WpfTheory]
                [InlineData("")]
                [InlineData("a")]
                [InlineData("ab")]
                [InlineData("abc")]
                [InlineData("abcd")]
                [InlineData("abcde")]
                public void Virtual(string line1)
                {
                    Create(line1, "");
                    var point1 = _textBuffer.GetVirtualPointInLine(0, 0);
                    var point2 = _textBuffer.GetVirtualPointInLine(0, 4);
                    var span = new VirtualSnapshotSpan(point1, point2);
                    var characterSpan = new CharacterSpan(span, true);
                    var visualSpan = VisualSpan.NewCharacter(characterSpan);
                    visualSpan.Select(_textView, SearchPath.Forward);
                    TestableSynchronizationContext.RunAll();
                    Assert.Equal(point1, _textView.Selection.Start);
                    Assert.Equal(point2, _textView.Selection.End);
                }
            }

            public sealed class LineTest : SelectTest
            {
                /// <summary>
                /// The selection of a reverse line span should cause a reversed selection
                /// </summary>
                [WpfFact]
                public void Backwards()
                {
                    Create("big dog", "big cat", "big tree", "big fish");
                    var lineRange = _textBuffer.GetLineRange(1);
                    var visualSpan = VisualSpan.NewLine(lineRange);
                    visualSpan.Select(_textView, SearchPath.Backward);
                    TestableSynchronizationContext.RunAll();
                    Assert.True(_textView.Selection.IsReversed);
                    Assert.Equal(lineRange.ExtentIncludingLineBreak, _textView.GetSelectionSpan());
                }

                /// <summary>
                /// The selection of a forward line span should cause a forward selection
                /// </summary>
                [WpfFact]
                public void Forward()
                {
                    Create("big dog", "big cat", "big tree", "big fish");
                    var lineRange = _textBuffer.GetLineRange(1);
                    var visualSpan = VisualSpan.NewLine(lineRange);
                    visualSpan.Select(_textView, SearchPath.Forward);
                    TestableSynchronizationContext.RunAll();
                    Assert.False(_textView.Selection.IsReversed);
                    Assert.Equal(lineRange.ExtentIncludingLineBreak, _textView.GetSelectionSpan());
                }
            }

            public sealed class BlockTest : SelectTest
            {
                /// <summary>
                /// Simple selection of a block 
                /// </summary>
                [WpfFact]
                public void Simple()
                {
                    Create("big dog", "big cat", "big tree", "big fish");
                    var blockSpan = _vimBuffer.GetBlockSpan(1, 2, 0, 2);
                    var visualSpan = VisualSpan.NewBlock(blockSpan);
                    visualSpan.Select(_textView, SearchPath.Forward);
                    TestableSynchronizationContext.RunAll();
                    Assert.Equal(blockSpan, _vimBuffer.GetSelectionBlockSpan());
                    Assert.Equal(TextSelectionMode.Box, _textView.Selection.Mode);
                }
            }

            public sealed class BlockOverlapTest : SelectTest
            {
                /// <summary>
                /// Overlap of simple selection of a block with plain (non wide) characters should be 0
                /// </summary>
                [WpfFact]
                public void Simple()
                {
                    Create("big dog", "big cat", "big tree", "big fish");
                    var blockSpan = _textBuffer.GetBlockSpan(1, _vimBuffer.LocalSettings.TabStop, 0, 2);

                    foreach (var spanWithOverlap in blockSpan.BlockOverlapColumnSpans)
                    {
                        Assert.Equal(0, spanWithOverlap.Start.SpacesAfter);
                        Assert.Equal(0, spanWithOverlap.End.SpacesBefore);
                    }
                }

                /// <summary>
                /// Block selection can completely overlaps wide characters
                /// </summary>
                [WpfFact]
                public void Full()
                {
                    Create("big dog", "b\u3042 cat", "b\u3044 tree", "b\u3046 fish");
                    var blockSpan = _textBuffer.GetBlockSpan(column: 1, length: 2, startLine: 0, lineCount: 2, tabStop: _vimBuffer.LocalSettings.TabStop);
                    var spans = blockSpan.BlockOverlapColumnSpans;

                    foreach (var spanWithOverlap in spans)
                    {
                        Assert.Equal(0, spanWithOverlap.Start.SpacesBefore);
                        Assert.Equal(0, spanWithOverlap.End.SpacesAfter);
                    }

                    var col = spans.ToReadOnlyCollection();
                    Assert.Equal("ig", col[0].GetText());
                    Assert.Equal("\u3042", col[1].GetText());
                }

                /// <summary>
                /// Block selection can partly overlaps wide characters
                /// </summary>
                [WpfFact]
                public void Partial()
                {
                    Create("aiueo", "\u3042\u3044\u3046\u3048\u304A");
                    var blockSpan = _textBuffer.GetBlockSpan(column: 1, length: 2, startLine: 0, lineCount: 2, tabStop: _vimBuffer.LocalSettings.TabStop);
                    var col = blockSpan.BlockOverlapColumnSpans.ToReadOnlyCollection();

                    Assert.False(col[0].HasOverlap);
                    Assert.Equal("iu", col[0].InnerSpan.GetText());

                    Assert.True(col[1].HasOverlap);
                    Assert.Equal(1, col[1].Start.SpacesBefore);
                    Assert.Equal(0, col[1].End.SpacesAfter);
                    Assert.Equal(1, col[1].End.SpacesBefore);
                    Assert.Equal("\u3042\u3044", col[1].OverarchingSpan.GetText());
                }

                /// <summary>
                /// Block selection should include all non spacing characters
                /// </summary>
                [WpfFact]
                public void NonSpacing()
                {
                    string[] lines = new string[] { "hello", "h\u0327e\u0301\u200bllo\u030a\u0305" };
                    Create(lines);
                    var blockSpan = _textBuffer.GetBlockSpan(0, length: 6, startLine: 0, lineCount: 2, tabStop: _vimBuffer.LocalSettings.TabStop);
                    var expected = new List<Tuple<int, int>> {
                        Tuple.Create(0, 0),
                        Tuple.Create(0, _vimBuffer.LocalSettings.TabStop - 1) };
                    var actual = blockSpan.BlockOverlapColumnSpans;

                    Assert.Equal(lines[1], actual.Rest.Head.InnerSpan.GetText());
                }

                /// <summary>
                /// Overlap of simple selection of a block that partly overlaps a tab character
                /// </summary>
                [WpfFact]
                public void VeryWideCharacter()
                {
                    Create("aiueo", "\t");
                    var blockSpan = _textBuffer.GetBlockSpan(0, 1, 0, 2, tabStop: _vimBuffer.LocalSettings.TabStop);
                    var expected = new List<Tuple<int, int>> {
                        Tuple.Create(0, 0),
                        Tuple.Create(0, _vimBuffer.LocalSettings.TabStop - 1) };
                    var col = blockSpan.BlockOverlapColumnSpans.ToReadOnlyCollection();

                    Assert.False(col[0].HasOverlap);

                    Assert.Equal(0, col[1].Start.SpacesBefore);
                    Assert.Equal(1, col[1].End.SpacesBefore);
                }
            }
        }
    }
}
