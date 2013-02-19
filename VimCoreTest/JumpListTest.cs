using System.Linq;
using EditorUtils;
using Microsoft.VisualStudio.Text;
using Xunit;
using Vim.Extensions;

namespace Vim.UnitTest
{
    public sealed class JumpListTest : VimTestBase
    {
        private ITextBuffer _textBuffer;
        private IBufferTrackingService _bufferTrackingService;
        private JumpList _jumpListRaw;
        private IJumpList _jumpList;

        public void Create(params string[] lines)
        {
            var textView = CreateTextView(lines);
            _textBuffer = textView.TextBuffer;
            _bufferTrackingService = new BufferTrackingService();
            _jumpListRaw = new JumpList(textView, _bufferTrackingService);
            _jumpList = _jumpListRaw;
        }

        /// <summary>
        /// Sanity check the default state is an empty jump list
        /// </summary>
        [Fact]
        public void Jumps_Empty()
        {
            Create("");
            Assert.Equal(0, _jumpList.Jumps.Count());
        }

        /// <summary>
        /// Move older should fail if there is nothing in the jump list
        /// </summary>
        [Fact]
        public void MoveOlder_Empty()
        {
            Create("");
            _jumpList.StartTraversal();
            Assert.False(_jumpList.MoveOlder(1));
        }

        /// <summary>
        /// Move older should fail if the count is too big and it should not change
        /// the position in the list
        /// </summary>
        [Fact]
        public void MoveOlder_CountTooBig()
        {
            Create("cat", "dog");
            _jumpList.Add(_textBuffer.GetPoint(0));
            _jumpList.Add(_textBuffer.GetLine(1).Start);
            _jumpList.StartTraversal();
            Assert.False(_jumpList.MoveOlder(10));
            Assert.True(_jumpList.CurrentIndex.IsSome(0));
        }

        /// <summary>
        /// Simple move next with a valid count
        /// </summary>
        [Fact]
        public void MoveOlder_Valid()
        {
            Create("cat", "dog");
            _jumpList.Add(_textBuffer.GetPoint(0));
            _jumpList.Add(_textBuffer.GetLine(1).Start);
            _jumpList.StartTraversal();
            Assert.True(_jumpList.Current.IsSome(_textBuffer.GetLine(0).Start));
            Assert.True(_jumpList.CurrentIndex.IsSome(0));
            Assert.True(_jumpList.MoveOlder(1));
            Assert.True(_jumpList.CurrentIndex.IsSome(1));
        }

        /// <summary>
        /// Moving to an older position in the jump list shoudn't affect the last jump location
        /// </summary>
        [Fact]
        public void MoveOlder_DontChangeLastJumpLocation()
        {
            Create("dog", "cat", "fish", "bear");
            _jumpList.Add(_textBuffer.GetLine(1).Start);
            _jumpList.Add(_textBuffer.GetLine(2).Start);
            _jumpList.Add(_textBuffer.GetLine(3).Start);
            _jumpList.SetLastJumpLocation(1, 0);
            _jumpList.StartTraversal();
            Assert.True(_jumpList.MoveOlder(1));
            Assert.Equal(_textBuffer.GetLine(1).Start, _jumpList.LastJumpLocation.Value.Position.Position);
        }

        /// <summary>
        /// Move to a newer on an empty list should fail
        /// </summary>
        [Fact]
        public void MoveNewer_Empty()
        {
            Create("");
            _jumpList.StartTraversal();
            Assert.False(_jumpList.MoveNewer(1));
        }

        /// <summary>
        /// Move previous when the count is too big should fail
        /// </summary>
        [Fact]
        public void MoveNewer_CountTooBig()
        {
            Create("cat", "dog");
            _jumpList.Add(_textBuffer.GetLine(0).Start);
            _jumpList.Add(_textBuffer.GetLine(1).Start);
            _jumpList.StartTraversal();
            Assert.True(_jumpList.MoveOlder(1));
            Assert.False(_jumpList.MoveNewer(2));
            Assert.True(_jumpList.CurrentIndex.IsSome(1));
        }

        /// <summary>
        /// Move previous when the count is too big should fail
        /// </summary>
        [Fact]
        public void MoveNewer_CountValid()
        {
            Create("cat", "dog");
            _jumpList.Add(_textBuffer.GetLine(0).Start);
            _jumpList.Add(_textBuffer.GetLine(1).Start);
            _jumpList.StartTraversal();
            Assert.True(_jumpList.MoveOlder(1));
            Assert.True(_jumpList.MoveNewer(1));
            Assert.True(_jumpList.CurrentIndex.IsSome(0));
        }

        /// <summary>
        /// Moving to an newer position in the jump list shoudn't affect the last jump location
        /// </summary>
        [Fact]
        public void MoveNewer_DontChangeLastJumpLocation()
        {
            Create("dog", "cat", "fish", "bear");
            _jumpList.Add(_textBuffer.GetLine(1).Start);
            _jumpList.Add(_textBuffer.GetLine(2).Start);
            _jumpList.Add(_textBuffer.GetLine(3).Start);
            _jumpList.SetLastJumpLocation(1, 0);
            _jumpList.StartTraversal();
            _jumpList.MoveOlder(1);
            _jumpList.MoveNewer(1);
            Assert.Equal(_textBuffer.GetLine(1).Start, _jumpList.LastJumpLocation.Value.Position.Position);
        }

        /// <summary>
        /// First add shouldn't affect the Current or CurrentIndex properties
        /// </summary>
        [Fact]
        public void Add_First()
        {
            Create("");
            _jumpList.Add(_textBuffer.GetLine(0).Start);
            Assert.True(_jumpList.Current.IsNone());
            Assert.True(_jumpList.CurrentIndex.IsNone());
        }

        /// <summary>
        /// Add multiple should keep updating the Current and CurrentIndex properties
        /// </summary>
        [Fact]
        public void Add_Several()
        {
            Create("a", "b", "c", "d", "e");
            for (var i = 0; i < 5; i++)
            {
                var point = _textBuffer.GetLine(i).Start;
                _jumpList.Add(point);
                Assert.Equal(point, _jumpList.Jumps.First().Position);
            }

            Assert.Equal(5, _jumpList.Jumps.Count());
        }

        /// <summary>
        /// Adding in the same line shouldn't add a new entry.  It should just reorder
        /// the list
        /// </summary>
        [Fact]
        public void Add_SameLine()
        {
            Create("a", "b", "c", "d", "e");
            _jumpList.Add(_textBuffer.GetLine(0).Start);
            _jumpList.Add(_textBuffer.GetLine(1).Start);
            _jumpList.Add(_textBuffer.GetLine(0).Start);
            Assert.Equal(2, _jumpList.Jumps.Count());
            Assert.Equal(_textBuffer.GetLine(0).Start, _jumpList.Jumps.ElementAt(0).Position);
            Assert.Equal(_textBuffer.GetLine(1).Start, _jumpList.Jumps.ElementAt(1).Position);
        }

        /// <summary>
        /// Adding a value to the jump list should update the last jump location
        /// </summary>
        [Fact]
        public void Add_UpdateLastJumpLocation()
        {
            Create("cat", "dog", "fish");
            var point = _textBuffer.GetLine(1).Start.Add(2);
            _jumpList.Add(point);
            Assert.Equal(point, _jumpList.LastJumpLocation.Value.Position);
        }
    }
}
