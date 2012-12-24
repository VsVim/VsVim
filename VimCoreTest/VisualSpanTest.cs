﻿using System;
using System.Collections.Generic;
using EditorUtils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Xunit;

namespace Vim.UnitTest
{
    public sealed class VisualSpanTest : VimTestBase
    {
        private ITextView _textView;
        private ITextBuffer _textBuffer;

        private void Create(params string[] lines)
        {
            _textView = CreateTextView(lines);
            _textBuffer = _textView.TextBuffer;
        }

        /// <summary>
        /// The selection of a reverse character span should cause a reversed selection
        /// </summary>
        [Fact]
        public void Select_Character_Backwards()
        {
            Create("big dog", "big cat", "big tree", "big fish");
            var characterSpan = CharacterSpan.CreateForSpan(_textBuffer.GetSpan(1, 3));
            var visualSpan = VisualSpan.NewCharacter(characterSpan);
            visualSpan.Select(_textView, Path.Backward);
            Assert.True(_textView.Selection.IsReversed);
            Assert.Equal(characterSpan.Span, _textView.GetSelectionSpan());
        }

        /// <summary>
        /// The selection of a forward character span should cause a forward selection
        /// </summary>
        [Fact]
        public void Select_Character_Forwards()
        {
            Create("big dog", "big cat", "big tree", "big fish");
            var characterSpan = CharacterSpan.CreateForSpan(_textBuffer.GetSpan(1, 3));
            var visualSpan = VisualSpan.NewCharacter(characterSpan);
            visualSpan.Select(_textView, Path.Forward);
            Assert.False(_textView.Selection.IsReversed);
            Assert.Equal(characterSpan.Span, _textView.GetSelectionSpan());
        }

        /// <summary>
        /// The selection of a reverse line span should cause a reversed selection
        /// </summary>
        [Fact]
        public void Select_Line_Backwards()
        {
            Create("big dog", "big cat", "big tree", "big fish");
            var lineRange = _textBuffer.GetLineRange(1);
            var visualSpan = VisualSpan.NewLine(lineRange);
            visualSpan.Select(_textView, Path.Backward);
            Assert.True(_textView.Selection.IsReversed);
            Assert.Equal(lineRange.ExtentIncludingLineBreak, _textView.GetSelectionSpan());
        }

        /// <summary>
        /// The selection of a forward line span should cause a forward selection
        /// </summary>
        [Fact]
        public void Select_Line_Forward()
        {
            Create("big dog", "big cat", "big tree", "big fish");
            var lineRange = _textBuffer.GetLineRange(1);
            var visualSpan = VisualSpan.NewLine(lineRange);
            visualSpan.Select(_textView, Path.Forward);
            Assert.False(_textView.Selection.IsReversed);
            Assert.Equal(lineRange.ExtentIncludingLineBreak, _textView.GetSelectionSpan());
        }

        /// <summary>
        /// Simple selection of a block 
        /// </summary>
        [Fact]
        public void Select_Block_Simple()
        {
            Create("big dog", "big cat", "big tree", "big fish");
            var blockSpan = _textBuffer.GetBlockSpan(1, 2, 0, 2);
            var visualSpan = VisualSpan.NewBlock(blockSpan);
            visualSpan.Select(_textView, Path.Forward);
            Assert.Equal(blockSpan, _textView.GetSelectionBlockSpan());
            Assert.Equal(TextSelectionMode.Box, _textView.Selection.Mode);
        }

        /// <summary>
        /// Overlap of simple selection of a block with plain (non wide) characters should be 0
        /// </summary>
        [Fact]
        public void Select_BlockOverlap_Simple()
        {
            Create("big dog", "big cat", "big tree", "big fish");
            var blockSpan = _textBuffer.GetBlockSpan(1, 2, 0, 2);

            foreach (var spanWithOverlap in blockSpan.BlockSpansWithOverlap(Vim.VimRcLocalSettings))
            {
                Assert.Equal(0, spanWithOverlap.Item1);
                Assert.Equal(0, spanWithOverlap.Item3);
            }
        }

        /// <summary>
        /// Block selection can completely overlaps wide characters
        /// </summary>
        [Fact]
        public void Select_BlockOverlap_Full()
        {
            Create("big dog", "bあ cat", "bい tree", "bう fish");
            var blockSpan = _textBuffer.GetBlockSpan(1, 2, 0, 2);
            var spans = blockSpan.BlockSpansWithOverlap(Vim.VimRcLocalSettings);

            foreach (var spanWithOverlap in spans)
            {
                Assert.Equal(0, spanWithOverlap.Item1);
                Assert.Equal(0, spanWithOverlap.Item3);
            }

            Assert.Equal("ig", spans.Head.Item2.GetText());
            Assert.Equal("あ", spans.Rest.Head.Item2.GetText());
        }

        /// <summary>
        /// Block selection can partly overlaps wide characters
        /// </summary>
        [Fact]
        public void Select_BlockOverlap_Partial()
        {
            Create("aiueo", "あいうえお");
            var blockSpan = _textBuffer.GetBlockSpan(1, 2, 0, 2);
            var expected = new List<Tuple<int, int>> {
                Tuple.Create(0, 0),
                Tuple.Create(1, 1),
                Tuple.Create(0, 0) };
            var actual = blockSpan.BlockSpansWithOverlap(Vim.VimRcLocalSettings);

            Assert.Equal(expected[0].Item1, actual.Head.Item1);
            Assert.Equal("iu", actual.Head.Item2.GetText());
            Assert.Equal(expected[0].Item2, actual.Head.Item3);

            Assert.Equal(expected[1].Item1, actual.Rest.Head.Item1);
            Assert.Equal("あい", actual.Rest.Head.Item2.GetText());
            Assert.Equal(expected[1].Item2, actual.Rest.Head.Item3);
        }

        /// <summary>
        /// Block selection should include all non spacing characters
        /// </summary>
        [Fact]
        public void Select_BlockOverlap_NonSpacing()
        {
            string[] lines = new string[] {"hello", "h\u0327e\u0301\u200bllo\u030a\u0305"};
            Create(lines);
            var blockSpan = _textBuffer.GetBlockSpan(0, 5, 0, 2);
            var expected = new List<Tuple<int, int>> {
                Tuple.Create(0, 0),
                Tuple.Create(0, Vim.VimRcLocalSettings.TabStop - 1) };
            var actual = blockSpan.BlockSpansWithOverlap(Vim.VimRcLocalSettings);

            Assert.Equal(lines[1], actual.Rest.Head.Item2.GetText());
        }

        /// <summary>
        /// Overlap of simple selection of a block that partly overlaps a tab character
        /// </summary>
        [Fact]
        public void Select_BlockOverlap_VeryWideCharacter()
        {
            Create("aiueo", "\t");
            var blockSpan = _textBuffer.GetBlockSpan(0, 1, 0, 2);
            var expected = new List<Tuple<int, int>> {
                Tuple.Create(0, 0),
                Tuple.Create(0, Vim.VimRcLocalSettings.TabStop - 1) };
            var actual = blockSpan.BlockSpansWithOverlap(Vim.VimRcLocalSettings);

            Assert.Equal(expected[0].Item1, actual.Head.Item1);
            Assert.Equal(expected[0].Item2, actual.Head.Item3);

            Assert.Equal(expected[1].Item1, actual.Rest.Head.Item1);
            Assert.Equal(expected[1].Item2, actual.Rest.Head.Item3);
        }

        /// <summary>
        /// An empty selection should produce an empty VisualSpan for character
        /// </summary>
        [Fact]
        public void CreateForSelection_Character_Empty()
        {
            Create("hello world");
            var visualSpan = VisualSpan.CreateForSelection(_textView, VisualKind.Character);
            Assert.Equal(0, visualSpan.EditSpan.OverarchingSpan.Length);
        }

        /// <summary>
        /// VisualSpan doesn't understand weird Vim semantics.  An empty selection is an 
        /// empty selection even if it's block
        /// </summary>
        [Fact]
        public void CreateForSelection_Block_Empty()
        {
            Create("hello world");
            var visualSpan = VisualSpan.CreateForSelection(_textView, VisualKind.Block);
            var blockSpan = new BlockSpan(_textBuffer.GetPoint(0), 0, 1);
            Assert.Equal(blockSpan, visualSpan.AsBlock().Item);
        }

        /// <summary>
        /// An empty selection should still produce a complete line selection for line
        /// </summary>
        [Fact]
        public void CreateForSelection_Line_Empty()
        {
            Create("hello world");
            var visualSpan = VisualSpan.CreateForSelection(_textView, VisualKind.Line);
            Assert.Equal(_textBuffer.GetLineRange(0), visualSpan.AsLine().Item);
        }

        /// <summary>
        /// Ensure creating a VisualSpan for an empty points results in an empty selection
        /// </summary>
        [Fact]
        public void CreateForSelectionPoints_Block_Empty()
        {
            Create("dog cat");
            var point = _textBuffer.GetPoint(2);
            var visualSpan = VisualSpan.CreateForSelectionPoints(VisualKind.Block, point, point);
            var blockSpan = new BlockSpan(point, 0, 1);
            Assert.Equal(blockSpan, visualSpan.AsBlock().Item);
        }

        [Fact]
        public void CreateForSelectionPoints_Block_Backwards()
        {
            Create("big cat", "big dog");
            var visualSpan = VisualSpan.CreateForSelectionPoints(VisualKind.Block, _textBuffer.GetPoint(2), _textBuffer.GetPoint(0));
            var blockSpan = new BlockSpan(_textBuffer.GetPoint(0), 2, 1);
            Assert.Equal(blockSpan, visualSpan.AsBlock().Item);
        }

        /// <summary>
        /// Make sure that we properly handle the backward block selection which spans 
        /// multiple lines
        /// </summary>
        [Fact]
        public void CreateForSelectionPoints_Block_BackwardsMultipleLines()
        {
            Create("big cat", "big dog");
            var visualSpan = VisualSpan.CreateForSelectionPoints(VisualKind.Block, _textBuffer.GetPoint(2), _textBuffer.GetPointInLine(1, 1));
            var blockSpan = new BlockSpan(_textBuffer.GetPoint(1), 1, 2);
            Assert.Equal(blockSpan, visualSpan.AsBlock().Item);
        }

        /// <summary>
        /// Make sure that we properly handle the forward block selection which spans 
        /// multiple lines
        /// </summary>
        [Fact]
        public void CreateForSelectionPoints_Block_ForwardsMultipleLines()
        {
            Create("big cat", "big dog");
            var visualSpan = VisualSpan.CreateForSelectionPoints(VisualKind.Block, _textBuffer.GetPoint(1), _textBuffer.GetPointInLine(1, 3));
            var blockSpan = new BlockSpan(_textBuffer.GetPoint(1), 2, 2);
            Assert.Equal(blockSpan, visualSpan.AsBlock().Item);
        }

        /// <summary>
        /// Ensure creating a VisualSpan for an empty points results in an empty selection
        /// </summary>
        [Fact]
        public void CreateForSelectionPoints_Character_Empty()
        {
            Create("dog cat");
            var point = _textBuffer.GetPoint(2);
            var visualSpan = VisualSpan.CreateForSelectionPoints(VisualKind.Character, point, point);
            Assert.Equal(point, visualSpan.AsCharacter().Item.Start);
            Assert.Equal(0, visualSpan.AsCharacter().Item.Length);
        }

        /// <summary>
        /// Ensure we handle the case where the start and end point are the same point at the 
        /// start of the line.  The code should return the single line range for the line 
        /// containing the points
        /// </summary>
        [Fact]
        public void CreateForSelectionPoints_Line_SamePoint()
        {
            Create("cat", "dog", "tree");
            var point = _textBuffer.GetLine(1).Start;
            var visualSpan = VisualSpan.CreateForSelectionPoints(VisualKind.Line, point, point);
            Assert.Equal(_textBuffer.GetLineRange(1), visualSpan.AsLine().LineRange);
        }

        /// <summary>
        /// Make sure the code handles the case where the caret is positioned at the end of the
        /// ITextSnapshot.  Should return the last line
        /// </summary>
        [Fact]
        public void CreateForSelectionPoints_Line_EndOfSnapshot()
        {
            Create("cat", "dog");
            var point = new SnapshotPoint(_textBuffer.CurrentSnapshot, _textBuffer.CurrentSnapshot.Length);
            var visualSpan = VisualSpan.CreateForSelectionPoints(VisualKind.Line, point, point);
            Assert.Equal(1, visualSpan.AsLine().LineRange.LastLineNumber);
        }
    }
}
