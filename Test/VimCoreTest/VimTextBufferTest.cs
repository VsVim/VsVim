using EditorUtils;
using Microsoft.VisualStudio.Text;
using Xunit;
using Vim.Extensions;
using System.Linq;

namespace Vim.UnitTest
{
    public abstract class VimTextBufferTest : VimTestBase
    {
        protected IVimTextBuffer _vimTextBuffer;
        protected ITextBuffer _textBuffer;
        protected LocalMark _localMarkA = LocalMark.NewLetter(Letter.A);

        private void Create(params string[] lines)
        {
            _vimTextBuffer = CreateVimTextBuffer(lines);
            _textBuffer = _vimTextBuffer.TextBuffer;
        }

        public sealed class LastInsertExitPoint : VimTextBufferTest
        {
            [Fact]
            public void Simple()
            {
                Create("cat", "dog");
                var point = _textBuffer.GetPoint(1);
                _vimTextBuffer.LastInsertExitPoint = FSharpOption.Create(point);
                Assert.Equal(point, _vimTextBuffer.LastInsertExitPoint.Value);
            }

            /// <summary>
            /// The point should track edits
            /// </summary>
            [Fact]
            public void TracksEdits()
            {
                Create("cat", "dog");
                _vimTextBuffer.LastInsertExitPoint = FSharpOption.Create(_textBuffer.GetPoint(1));
                _textBuffer.Insert(0, "foo");
                Assert.Equal(1, _vimTextBuffer.LastInsertExitPoint.Value.Position);
            }

            /// <summary>
            /// A delete of the line that contains that LastInsertExitPoint should cause it to be 
            /// cleared
            /// </summary>
            [Fact]
            public void DeleteShouldClear()
            {
                Create("cat", "dog", "fish");
                _vimTextBuffer.LastInsertExitPoint = FSharpOption.Create(_textBuffer.GetPoint(1));
                _textBuffer.Delete(_textBuffer.GetLine(0).ExtentIncludingLineBreak.Span);
                Assert.True(_vimTextBuffer.LastInsertExitPoint.IsNone());
            }
        }

        public sealed class LocalMarkTest : VimTextBufferTest
        {
            /// <summary>
            /// Requesting a LocalMark which isn't set should produce an empty option
            /// </summary>
            [Fact]
            public void GetLocalMark_NotSet()
            {
                Create("");
                Assert.True(_vimTextBuffer.GetLocalMark(_localMarkA).IsNone());
            }

            /// <summary>
            /// Sanity check to ensure we can get and set a local mark 
            /// </summary>
            [Fact]
            public void SetLocalMark_FirstLine()
            {
                Create("hello world");
                Assert.True(_vimTextBuffer.SetLocalMark(_localMarkA, 0, 1));
                Assert.Equal(1, _vimTextBuffer.GetLocalMark(_localMarkA).Value.Position.Position);
            }

            /// <summary>
            /// Sanity check to ensure we can get and set a local mark 
            /// </summary>
            [Fact]
            public void SetLocalMark_SecondLine()
            {
                Create("hello", "world");
                Assert.True(_vimTextBuffer.SetLocalMark(_localMarkA, 1, 1));
                Assert.Equal(_textBuffer.GetLine(1).Start.Add(1).Position, _vimTextBuffer.GetLocalMark(_localMarkA).Value.Position.Position);
            }

            /// <summary>
            /// Attempting to set a read only mark should return false and not update the mark
            /// </summary>
            [Fact]
            public void SetLocalMark_ReadOnlyMark()
            {
                Create("hello", "world");
                var visualSpan = VisualSpan.NewCharacter(new CharacterSpan(_textBuffer.GetPoint(0), 1, 2));
                _vimTextBuffer.LastVisualSelection = FSharpOption.Create(VisualSelection.CreateForward(visualSpan));
                Assert.False(_vimTextBuffer.SetLocalMark(LocalMark.LastSelectionStart, 0, 4));
                Assert.Equal(0, _vimTextBuffer.GetLocalMark(LocalMark.LastSelectionStart).Value.Position.Position);
            }

            [Fact]
            public void RemoveLocalMark_NotFound()
            {
                Create("dog");
                Assert.False(_vimTextBuffer.RemoveLocalMark(LocalMark.NewLetter(Letter.A)));
            }

            [Fact]
            public void RemoveLocalMark_Found()
            {
                Create("dog");
                _vimTextBuffer.SetLocalMark(LocalMark.NewLetter(Letter.A), 0, 0);
                Assert.True(_vimTextBuffer.RemoveLocalMark(LocalMark.NewLetter(Letter.A)));
            }
        }

        public sealed class ClearTest : VimTextBufferTest
        {
            [Fact]
            public void ShouldRemoveLocalMarks()
            {
                Create("cat");
                var marks = Letter.All.Select(LocalMark.NewLetter);
                foreach (var mark in marks)
                {
                    _vimTextBuffer.SetLocalMark(mark, 0, 0);
                    Assert.True(_vimTextBuffer.GetLocalMark(mark).IsSome());
                }

                _vimTextBuffer.Clear();

                foreach (var mark in marks)
                {
                    Assert.False(_vimTextBuffer.GetLocalMark(mark).IsSome());
                }
            }

            [Fact]
            public void ShouldClearFields()
            {
                Create("cat");
                _vimTextBuffer.LastEditPoint = FSharpOption.Create(_textBuffer.GetPoint(0));
                _vimTextBuffer.LastInsertExitPoint = FSharpOption.Create(_textBuffer.GetPoint(0));
                _vimTextBuffer.Clear();
                Assert.True(_vimTextBuffer.LastEditPoint.IsNone());
                Assert.True(_vimTextBuffer.LastInsertExitPoint.IsNone());
            }
        }
    }
}
