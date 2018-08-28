using System.Linq;
using Vim.EditorHost;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Xunit;

namespace Vim.UnitTest
{
    public abstract class VisualSelectionTest : VimTestBase
    {
        private ITextView _textView;
        private ITextBuffer _textBuffer;

        protected virtual void Create(params string[] lines)
        {
            _textView = CreateTextView(lines);
            _textBuffer = _textView.TextBuffer;
        }

        public abstract class BackAndForthTest : VisualSelectionTest
        {
            public sealed class BlockTest : BackAndForthTest
            {
                /// <summary>
                /// Make sure we get parity when going back and forth between block selection.  Only test the 
                /// top caret locations here as for a single line we will never give back a bottom one since
                /// it's just one line
                /// </summary>
                [WpfFact]
                public void SingleLine()
                {
                    Create("cats", "dogs", "fish");
                    var all = new[] { BlockCaretLocation.TopLeft, BlockCaretLocation.TopRight };
                    var blockSpanData = _textView.GetBlockSpan(1, 2, 0, 1);
                    foreach (var blockCaretLocation in all)
                    {
                        var visualSelection = VisualSelection.NewBlock(blockSpanData, blockCaretLocation);
                        visualSelection.SelectAndMoveCaret(_textView);
                        var currentVisualSelection = VisualSelection.CreateForSelection(_textView, VisualKind.Block, SelectionKind.Inclusive, tabStop: 4);
                        Assert.Equal(visualSelection, currentVisualSelection);
                    }
                }

                /// <summary>
                /// Make sure we get parity when going back and forth between block selection
                /// </summary>
                [WpfFact]
                public void MultiLine()
                {
                    Create("cats", "dogs", "fish");
                    var all = new[] { BlockCaretLocation.TopLeft, BlockCaretLocation.TopRight, BlockCaretLocation.BottomLeft, BlockCaretLocation.BottomRight };
                    var blockSpan = _textView.GetBlockSpan(1, 2, 0, 2);
                    foreach (var blockCaretLocation in all)
                    {
                        var visualSelection = VisualSelection.NewBlock(blockSpan, blockCaretLocation);
                        visualSelection.SelectAndMoveCaret(_textView);
                        var currentVisualSelection = VisualSelection.CreateForSelection(_textView, VisualKind.Block, SelectionKind.Inclusive, tabStop: 4);
                        Assert.Equal(visualSelection, currentVisualSelection);
                    }
                }
            }

            public sealed class CharacterTest : BackAndForthTest
            {
                /// <summary>
                /// Make sure we get parity when going back and forth between character selection
                /// </summary>
                [WpfFact]
                public void SingleLine()
                {
                    Create("cats", "dogs");
                    var characterSpan = new CharacterSpan(_textBuffer.GetSpan(1, 2));
                    var all = new[] { SearchPath.Forward, SearchPath.Backward };
                    foreach (var path in all)
                    {
                        var visualSelection = VisualSelection.NewCharacter(characterSpan, path);
                        visualSelection.SelectAndMoveCaret(_textView);
                        var currentVisualSelection = VisualSelection.CreateForSelection(_textView, VisualKind.Character, SelectionKind.Inclusive, tabStop: 4);
                        Assert.Equal(visualSelection, currentVisualSelection);
                    }
                }

                /// <summary>
                /// Make sure we get parity when going back and forth between character selection
                /// </summary>
                [WpfFact]
                public void MultiLine()
                {
                    Create("cats", "dogs", "fish");
                    var characterSpan = new CharacterSpan(_textView.GetLine(0).Start, 2, 4);
                    var all = new[] { SearchPath.Forward, SearchPath.Backward };
                    foreach (var path in all)
                    {
                        var visualSelection = VisualSelection.NewCharacter(characterSpan, path);
                        visualSelection.SelectAndMoveCaret(_textView);
                        var currentVisualSelection = VisualSelection.CreateForSelection(_textView, VisualKind.Character, SelectionKind.Inclusive, tabStop: 4);
                        Assert.Equal(visualSelection, currentVisualSelection);
                    }
                }
            }

            public sealed class LineTest : BackAndForthTest
            {
                /// <summary>
                /// Make sure we get parity when going back and forth between line selection.  Don't check 
                /// forward / back on single line as they're the same
                /// </summary>
                [WpfFact]
                public void SingleLine()
                {
                    Create("cats", "dogs", "fish");
                    var lineRange = _textView.GetLineRange(0, 0);
                    var all = Enumerable.Range(0, 3);
                    foreach (var column in all)
                    {
                        var visualSelection = VisualSelection.NewLine(lineRange, SearchPath.Backward, column);
                        visualSelection.SelectAndMoveCaret(_textView);
                        var currentVisualSelection = VisualSelection.CreateForSelection(_textView, VisualKind.Line, SelectionKind.Inclusive, tabStop: 4);
                        Assert.Equal(visualSelection, currentVisualSelection);
                    }
                }

                /// <summary>
                /// Make sure we get parity when going back and forth between line selection
                /// </summary>
                [WpfFact]
                public void MultiLine()
                {
                    Create("cats", "dogs", "fish");
                    var lineRange = _textView.GetLineRange(0, 1);
                    var all = Enumerable.Range(0, 3);
                    var allDirections = new[] { SearchPath.Forward, SearchPath.Backward };
                    foreach (var path in allDirections)
                    {
                        foreach (var column in all)
                        {
                            var visualSelection = VisualSelection.NewLine(lineRange, path, column);
                            visualSelection.SelectAndMoveCaret(_textView);
                            var currentVisualSelection = VisualSelection.CreateForSelection(_textView, VisualKind.Line, SelectionKind.Inclusive, tabStop: 4);
                            Assert.Equal(visualSelection, currentVisualSelection);
                        }
                    }
                }
            }
        }

        public abstract class GetCaretPointTest : VisualSelectionTest
        {
            public sealed class CharaterTest : GetCaretPointTest
            {
                /// <summary>
                /// Get the appropriate Caret SnapshotPoint for a forward character span
                /// </summary>
                [WpfFact]
                public void Forward()
                {
                    Create("cats", "dogs");
                    var visualSelection = VisualSelection.NewCharacter(
                        new CharacterSpan(_textBuffer.GetSpan(0, 2)),
                        SearchPath.Forward);
                    Assert.Equal(_textBuffer.GetPoint(1), visualSelection.GetCaretPoint(SelectionKind.Inclusive));
                }

                /// <summary>
                /// Get the appropriate Caret SnapshotPoint for a backward character span
                /// </summary>
                [WpfFact]
                public void Backward()
                {
                    Create("cats", "dogs");
                    var visualSelection = VisualSelection.NewCharacter(
                        new CharacterSpan(_textBuffer.GetSpan(0, 2)),
                        SearchPath.Backward);
                    Assert.Equal(_textBuffer.GetPoint(0), visualSelection.GetCaretPoint(SelectionKind.Inclusive));
                }

                /// <summary>
                /// When the last line in a character selection is empty the caret -1 offset
                /// shouldn't be applied to the End location.  Else we'd end up in the line
                /// break instead of the start of the line
                /// </summary>
                [WpfFact]
                public void EmptyLastLine()
                {
                    Create("cat", "", "dog");
                    var visualSelection = VisualSelection.NewCharacter(
                        new CharacterSpan(_textBuffer.GetPoint(0), 2, 1),
                        SearchPath.Forward);
                    Assert.Equal(_textBuffer.GetLine(1).Start, visualSelection.GetCaretPoint(SelectionKind.Inclusive));
                }

                [WpfFact]
                public void InLineBreakForward()
                {
                    Create("cat", "dog");
                    var visualSelection = VisualSelection.NewCharacter(
                        new CharacterSpan(_textBuffer.GetPoint(0), _textBuffer.GetPoint(4)),
                        SearchPath.Forward);
                    Assert.Equal(4, visualSelection.GetCaretPoint(SelectionKind.Inclusive));
                }

                [WpfFact]
                public void InLineBreakForwardDeep()
                {
                    Create("cat", "dog");
                    var visualSelection = VisualSelection.NewCharacter(
                        new CharacterSpan(_textBuffer.GetPoint(0), _textBuffer.GetPoint(5)),
                        SearchPath.Forward);
                    Assert.Equal(4, visualSelection.GetCaretPoint(SelectionKind.Inclusive));
                }

                [WpfFact]
                public void InLineBreakBackward()
                {
                    Create("cat", "dog");
                    var visualSelection = VisualSelection.NewCharacter(
                        new CharacterSpan(_textBuffer.GetPoint(0), 1, 4),
                        SearchPath.Backward);
                    Assert.Equal(0, visualSelection.GetCaretPoint(SelectionKind.Inclusive));
                }
            }

            public sealed class BlockTest : GetCaretPointTest
            {
                /// <summary>
                /// Get the appropriate Caret SnapshotPoint for a top right block selection
                /// </summary>
                [WpfFact]
                public void TopRight()
                {
                    Create("cats", "dogs", "fish");
                    var blockSpan = _textBuffer.GetBlockSpan(1, 2, 0, 2);
                    var visualSelection = VisualSelection.NewBlock(blockSpan, BlockCaretLocation.TopRight);
                    Assert.Equal(_textBuffer.GetPoint(2), visualSelection.GetCaretPoint(SelectionKind.Inclusive));
                }

                /// <summary>
                /// Ensure the caret point is appropriately on the bottom right for a block selection
                /// </summary>
                [WpfFact]
                public void BottomRight()
                {
                    Create("big dog", "big cat", "big tree", "big fish");
                    var blockSpan = _textBuffer.GetBlockSpan(1, 1, 0, 2);
                    var visualSelection = VisualSelection.NewBlock(blockSpan, BlockCaretLocation.BottomRight);
                    Assert.Equal(_textView.GetPointInLine(1, 1), visualSelection.GetCaretPoint(SelectionKind.Inclusive));
                }
            }
        }

        public sealed class MiscTest : VisualSelectionTest
        {
            /// <summary>
            /// Make sure we properly create from a forward selection
            /// </summary>
            [WpfFact]
            public void Create_Character()
            {
                Create("hello world");
                var span = new SnapshotSpan(_textView.GetLine(0).Start, 2);
                _textView.SelectAndMoveCaret(span);
                var visualSelection = VisualSelection.CreateForSelection(_textView, VisualKind.Character, SelectionKind.Inclusive, tabStop: 4);
                Assert.Equal(span, visualSelection.VisualSpan.EditSpan.OverarchingSpan);
                Assert.Equal(VisualKind.Character, visualSelection.VisualKind);
                Assert.True(visualSelection.IsCharacterForward);
            }

            /// <summary>
            /// Create from a Block VisualSpan and make sure we get the appropriate CaretPoint
            /// </summary>
            [WpfFact]
            public void CreateForVisualSpan_Block()
            {
                Create("cat", "dog");
                var visualSpan = VisualSpan.NewBlock(_textView.GetBlockSpan(0, 2, 0, 2));
                var visualSelection = VisualSelection.CreateForward(visualSpan);
                Assert.Equal(_textView.GetLine(1).Start.Add(1), visualSelection.GetCaretPoint(SelectionKind.Inclusive));
                Assert.Equal(visualSpan, visualSelection.VisualSpan);
            }
        }

        public abstract class CreateForPointsTest : VisualSelectionTest
        {
            public sealed class CharacterTest : CreateForPointsTest
            {
                /// <summary>
                /// Ensure that a backwards character span includes the caret point in the span
                /// </summary>
                [WpfFact]
                public void Backwards()
                {
                    Create("cats dogs");
                    var visualSelection = VisualSelection.CreateForPoints(VisualKind.Character, _textBuffer.GetPoint(3), _textBuffer.GetPoint(1), tabStop: 4);
                    Assert.Equal("ats", visualSelection.VisualSpan.EditSpan.OverarchingSpan.GetText());
                }

                /// <summary>
                /// The further point should be included in the selection even when it's the anchor point 
                /// </summary>
                [WpfFact]
                public void BackwardsInlcudeAnchor()
                {
                    Create("cats dogs");
                    var visualSelection = VisualSelection.CreateForPoints(VisualKind.Character, _textBuffer.GetPoint(3), _textBuffer.GetPoint(0), tabStop: 4);
                    var characterSpan = visualSelection.AsCharacter().CharacterSpan;
                    Assert.Equal(0, characterSpan.Start.Position);
                    Assert.Equal(3, characterSpan.Last.Value.Position);
                    Assert.Equal(4, characterSpan.End.Position);
                }

                [WpfFact]
                public void BackwardIntoLineBreak()
                {
                    Create("cat", "dog");
                    var visualSelection = VisualSelection.CreateForPoints(VisualKind.Character, _textBuffer.GetPoint(3), _textBuffer.GetPoint(0), tabStop: 4);
                    var character = visualSelection.AsCharacter();
                    Assert.True(character.CharacterSpan.IncludeLastLineLineBreak);
                    Assert.Equal(SearchPath.Backward, character.SearchPath);
                }

                [WpfFact]
                public void ForwardIntoLineBreak()
                {
                    Create("cat", "dog");
                    var visualSelection = VisualSelection.CreateForPoints(VisualKind.Character, _textBuffer.GetPoint(0), _textBuffer.GetPoint(3), tabStop: 4);
                    var character = visualSelection.AsCharacter();
                    Assert.True(character.CharacterSpan.IncludeLastLineLineBreak);
                    Assert.Equal(SearchPath.Forward, character.SearchPath);
                }

                /// <summary>
                /// Go into the second character of the line break here
                /// </summary>
                [WpfFact]
                public void ForwardIntoLineBreak2()
                {
                    Create("cat", "dog");
                    var visualSelection = VisualSelection.CreateForPoints(VisualKind.Character, _textBuffer.GetPoint(0), _textBuffer.GetPoint(4), tabStop: 4);
                    var character = visualSelection.AsCharacter();
                    Assert.True(character.CharacterSpan.IncludeLastLineLineBreak);
                    Assert.Equal(SearchPath.Forward, character.SearchPath);
                }
            }

            public sealed class LineTest : CreateForPointsTest
            {
                /// <summary>
                /// Ensure that a backwards line span includes the entire line
                /// </summary>
                [WpfFact]
                public void Backwards()
                {
                    Create("cats dogs");
                    var visualSelection = VisualSelection.CreateForPoints(VisualKind.Line, _textBuffer.GetPoint(3), _textBuffer.GetPoint(1), tabStop: 4);
                    Assert.Equal(_textBuffer.GetLineRange(0), visualSelection.AsLine().LineRange);
                }

                /// <summary>
                /// When the caret is at the start of a line make sure that we include that line in the 
                /// selection.  This is different than the activePoint which wouldn't cause the line to 
                /// be included because it's past the selection
                /// </summary>
                [WpfFact]
                public void CaretAtStart()
                {
                    Create("cat", "dog", "bear");
                    var visualSpan = VisualSelection.CreateForPoints(VisualKind.Line, _textBuffer.GetPoint(0), caretPoint: _textBuffer.GetPointInLine(1, 0), tabStop: 4);
                    Assert.Equal(_textBuffer.GetLineRange(0, 1), visualSpan.AsLine().LineRange);
                }

                /// <summary>
                /// The use of virtual space should not affect a linewise visual selection
                /// </summary>
                /// <param name="useVirtualSpace"></param>
                [WpfTheory]
                [InlineData(false)]
                [InlineData(true)]
                public void CaretOnBlankLine(bool useVirtualSpace)
                {
                    Create("cats", "", "dogs", "");
                    var visualSelection = VisualSelection.CreateForVirtualPoints(VisualKind.Line, _textBuffer.GetVirtualPoint(0), _textBuffer.GetVirtualPointInLine(1, 0), tabStop: 4, useVirtualSpace);
                    Assert.Equal(_textBuffer.GetLineRange(0, 1), visualSelection.AsLine().LineRange);
                }
            }

            public sealed class BlockTest : CreateForPointsTest
            {
                protected override void Create(params string[] lines)
                {
                    Create(2, lines);
                }

                private void Create(int tabStop, params string[] lines)
                {
                    base.Create();
                    UpdateLayout(_textView, tabStop: tabStop);
                    _textView.SetText(lines);
                }

                /// <summary>
                /// Ensure that a backwards block span includes the entire line
                /// </summary>
                [WpfFact]
                public void Backwards()
                {
                    Create("cats dogs");
                    var visualSelection = VisualSelection.CreateForPoints(
                        VisualKind.Block,
                        _textBuffer.GetPoint(3),
                        _textBuffer.GetPoint(1),
                        tabStop: 4);
                    Assert.Equal(_textBuffer.GetSpan(1, 3), visualSelection.AsBlock().BlockSpan.BlockSpans.Head);
                }

                /// <summary>
                /// Get the selection for a backwards block that spans multiple lines.  In this case the 
                /// point is actually ahead of the anchor point in the position sense but the selection is 
                /// still backwards
                /// </summary>
                [WpfFact]
                public void BackwardSeveralLines()
                {
                    Create("big cat", "big dog");
                    var visualSelection = VisualSelection.CreateForPoints(
                        VisualKind.Block,
                        _textBuffer.GetPoint(2),
                        _textBuffer.GetPointInLine(1, 1),
                        tabStop: 4);
                    Assert.Equal(_textBuffer.GetBlockSpan(1, 2, 0, 2), visualSelection.AsBlock().BlockSpan);
                }

                /// <summary>
                /// Having the caret on column zero presents a few problems.  It can very easily be treated as 
                /// 'end' and hence not included causing the line count to be off.  Make sure that it is 
                /// included 
                /// </summary>
                [WpfFact]
                public void BackwardsCaretOnColumnZero()
                {
                    Create("cat", "dog");
                    var visualSelection = VisualSelection.CreateForPoints(
                        VisualKind.Block,
                        _textBuffer.GetPointInLine(0, 1),
                        _textBuffer.GetPointInLine(1, 0),
                        tabStop: 4);
                    var blockSpan = new BlockSpan(_textBuffer.GetPoint(0), tabStop: 4, spaces: 2, height: 2);
                    Assert.Equal(blockSpan, visualSelection.AsBlock().BlockSpan);
                }

                /// <summary>
                /// Make sure the API is correctly using spaces and not columns.  Otherwise selections like the 
                /// following look backwards.  Column wise it is backwards but spaces wise it is forwards
                /// </summary>
                [WpfFact]
                public void EnsureUsingSpaces()
                {
                    Create(4, "cat", "\tdog");
                    var visualSelection = VisualSelection.CreateForPoints(
                        VisualKind.Block,
                        _textBuffer.GetPointInLine(0, 2),
                        _textBuffer.GetPointInLine(1, 1),
                        tabStop: 4);
                    var blockSpan = new BlockSpan(_textBuffer.GetPointInLine(0, 2), tabStop: 4, spaces: 3, height: 2);
                    Assert.Equal(blockSpan, visualSelection.AsBlock().BlockSpan);
                }

                [WpfFact]
                public void ForwardSimple()
                {
                    Create("cats", "dogs");
                    var visualSelection = VisualSelection.CreateForPoints(
                        VisualKind.Block,
                        _textBuffer.GetPointInLine(0, 1),
                        _textBuffer.GetPointInLine(1, 3),
                        tabStop: 4);
                    var blockSpan = new BlockSpan(_textBuffer.GetPointInLine(0, 1), tabStop: 4, spaces: 3, height: 2);
                    Assert.Equal(blockSpan, visualSelection.AsBlock().BlockSpan);
                }

                [WpfFact]
                public void AfterTab()
                {
                    Create("cat", "d\tog");
                    var visualSelection = VisualSelection.CreateForPoints(
                        VisualKind.Block,
                        _textBuffer.GetPointInLine(0, 2),
                        _textBuffer.GetPointInLine(1, 2),
                        tabStop: 4);
                    var blockSpan = visualSelection.AsBlock().BlockSpan;

                    var endPoint = _textBuffer.GetPointInLine(1, 3);
                    Assert.Equal('g', endPoint.GetChar());
                    Assert.Equal(endPoint, blockSpan.End.StartPoint);
                }
            }
        }

        public abstract class CreateInitialTest : VisualSelectionTest
        {
            public sealed class CharacterTest : CreateInitialTest
            {
                [WpfFact]
                public void SelectionExclusive()
                {
                    Create("hello world");
                    var visualSelection = VisualSelection.CreateInitial(VisualKind.Character, _textBuffer.GetVirtualPoint(0), 4, SelectionKind.Exclusive, false);
                    Assert.Equal(0, visualSelection.AsCharacter().CharacterSpan.Length);
                }

                [WpfFact]
                public void SelectionInclusive()
                {
                    Create("hello world");
                    var visualSelection = VisualSelection.CreateInitial(VisualKind.Character, _textBuffer.GetVirtualPoint(0), 4, SelectionKind.Inclusive, false);
                    Assert.Equal(1, visualSelection.AsCharacter().CharacterSpan.Length);
                }

                [WpfFact]
                public void VirtualSelectionExclusive()
                {
                    Create("hello world");
                    var virtualPoint = _textBuffer.GetVirtualPointInLine(0, 20);
                    var visualSelection = VisualSelection.CreateInitial(VisualKind.Character,
                        virtualPoint, 4, SelectionKind.Exclusive, true);
                    Assert.Equal(virtualPoint, visualSelection.AsCharacter().CharacterSpan.VirtualStart);
                    Assert.Equal(0, visualSelection.AsCharacter().CharacterSpan.VirtualLength);
                }

                [WpfFact]
                public void VirtualSelectionInclusive()
                {
                    Create("hello world");
                    var virtualPoint = _textBuffer.GetVirtualPointInLine(0, 20);
                    var visualSelection = VisualSelection.CreateInitial(VisualKind.Character,
                        virtualPoint, 4, SelectionKind.Inclusive, true);
                    Assert.Equal(virtualPoint, visualSelection.AsCharacter().CharacterSpan.VirtualStart);
                    Assert.Equal(1, visualSelection.AsCharacter().CharacterSpan.VirtualLength);
                }
            }
        }

        public sealed class AdjustForSelectionKindTest : VisualSelectionTest
        {
            /// <summary>
            /// Block selection of width 1 with exclusive selection should still have a single
            /// column.  Even though normal block selections are shrunk by a column
            /// </summary>
            [WpfFact]
            public void Block_SingleColumnExclusive()
            {
                Create("cats", "dogs", "trees");
                var blockSpan = _textBuffer.GetBlockSpan(1, 1, 0, 2);
                var visualSpan = VisualSpan.NewBlock(blockSpan);
                var visualSelection = VisualSelection.CreateForward(visualSpan);
                Assert.Equal(visualSelection, visualSelection.AdjustForSelectionKind(SelectionKind.Exclusive));
            }

            /// <summary>
            /// Block selection should lose a column in Exclusive
            /// </summary>
            [WpfFact]
            public void Block_MultiColumnExclusive()
            {
                Create("cats", "dogs", "trees");
                var blockSpan = _textBuffer.GetBlockSpan(1, 2, 0, 2);
                var visualSpan = VisualSpan.NewBlock(blockSpan);
                var visualSelection = VisualSelection.CreateForward(visualSpan);
                var otherVisualSelection = visualSelection.AdjustForSelectionKind(SelectionKind.Exclusive);
                var otherBlockSpan = otherVisualSelection.AsBlock().BlockSpan;
                Assert.Equal(blockSpan.Start, otherBlockSpan.Start);
                Assert.Equal(1, otherBlockSpan.SpacesLength);
                Assert.Equal(2, otherBlockSpan.Height);
            }

            /// <summary>
            /// A character span should lose one if we adjust it for exclusive 
            /// </summary>
            [WpfFact]
            public void Character_Forward()
            {
                Create("cat dog bear");
                var characterSpan = new CharacterSpan(_textBuffer.GetSpan(0, 4));
                var visualSpan = VisualSpan.NewCharacter(characterSpan);
                var visualSelection = VisualSelection.CreateForward(visualSpan);
                Assert.Equal("cat", visualSelection.AdjustForSelectionKind(SelectionKind.Exclusive).VisualSpan.EditSpan.OverarchingSpan.GetText());
            }
        }
    }
}
