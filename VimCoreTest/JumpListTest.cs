using System.Linq;
using Microsoft.VisualStudio.Text;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class JumpListTest
    {
        private ITextBuffer _textBuffer;
        private ITrackingLineColumnService _trackingLineColumnService;
        private JumpList _jumpListRaw;
        private IJumpList _jumpList;

        public void Create(params string[] lines)
        {
            _textBuffer = EditorUtil.CreateTextBuffer(lines);
            _trackingLineColumnService = new TrackingLineColumnService();
            _jumpListRaw = new JumpList(_trackingLineColumnService);
            _jumpList = _jumpListRaw;
        }

        [Test]
        public void Jumps_Empty()
        {
            Create("");
            Assert.AreEqual(0, _jumpList.Jumps.Count());
        }

        /// <summary>
        /// Move next should fail if there is nothing in the jump list
        /// </summary>
        [Test]
        public void MoveOlder_Empty()
        {
            Create("");
            Assert.IsFalse(_jumpList.MoveOlder(1));
        }

        /// <summary>
        /// Move next should fail if the count is too big and it should not change
        /// the position in the list
        /// </summary>
        [Test]
        public void MoveOlder_CountTooBig()
        {
            Create("cat", "dog");
            _jumpList.Add(_textBuffer.GetPoint(0));
            _jumpList.Add(_textBuffer.GetLine(1).Start);
            Assert.IsFalse(_jumpList.MoveOlder(10));
            Assert.IsTrue(_jumpList.CurrentIndex.IsSome(0));
        }

        /// <summary>
        /// Simple move next with a valid count
        /// </summary>
        [Test]
        public void MoveOlder_Valid()
        {
            Create("cat", "dog");
            _jumpList.Add(_textBuffer.GetPoint(0));
            _jumpList.Add(_textBuffer.GetLine(1).Start);
            Assert.IsTrue(_jumpList.MoveOlder(1));
            Assert.IsTrue(_jumpList.Current.IsSome(_textBuffer.GetLine(0).Start));
            Assert.IsTrue(_jumpList.CurrentIndex.IsSome(1));
        }

        /// <summary>
        /// Move previous on an empty list should fail
        /// </summary>
        [Test]
        public void MoveNewer_Empty()
        {
            Create("");
            Assert.IsFalse(_jumpList.MoveNewer(1));
        }

        /// <summary>
        /// Move previous when the count is too big should fail
        /// </summary>
        [Test]
        public void MoveNewer_CountTooBig()
        {
            Create("cat", "dog");
            _jumpList.Add(_textBuffer.GetLine(0).Start);
            _jumpList.Add(_textBuffer.GetLine(1).Start);
            _jumpList.MoveOlder(1);
            Assert.IsFalse(_jumpList.MoveNewer(2));
            Assert.IsTrue(_jumpList.CurrentIndex.IsSome(1));
        }

        /// <summary>
        /// Move previous when the count is too big should fail
        /// </summary>
        [Test]
        public void MoveNewer_CountValid()
        {
            Create("cat", "dog");
            _jumpList.Add(_textBuffer.GetLine(0).Start);
            _jumpList.Add(_textBuffer.GetLine(1).Start);
            _jumpList.MoveOlder(1);
            Assert.IsTrue(_jumpList.MoveNewer(1));
            Assert.IsTrue(_jumpList.CurrentIndex.IsSome(0));
        }

        /// <summary>
        /// First add should update the Current and CurrentIndex properties
        /// </summary>
        [Test]
        public void Add_First()
        {
            Create("");
            _jumpList.Add(_textBuffer.GetLine(0).Start);
            Assert.IsTrue(_jumpList.Current.IsSome(_textBuffer.GetPoint(0)));
            Assert.IsTrue(_jumpList.CurrentIndex.IsSome(0));
        }

        /// <summary>
        /// Add multiple should keep updating the Current and CurrentIndex properties
        /// </summary>
        [Test]
        public void Add_Several()
        {
            Create("a", "b", "c", "d", "e");
            for (var i = 0; i < 5; i++)
            {
                var point = _textBuffer.GetLine(i).Start;
                _jumpList.Add(point);
                Assert.IsTrue(_jumpList.Current.IsSome(point));
                Assert.IsTrue(_jumpList.CurrentIndex.IsSome(0));
            }

            Assert.AreEqual(5, _jumpList.Jumps.Count());
        }

        /// <summary>
        /// Adding in the same line shouldn't add a new entry.  It should just reorder
        /// the list
        /// </summary>
        [Test]
        public void Add_SameLine()
        {
            Create("a", "b", "c", "d", "e");
            _jumpList.Add(_textBuffer.GetLine(0).Start);
            _jumpList.Add(_textBuffer.GetLine(1).Start);
            _jumpList.Add(_textBuffer.GetLine(0).Start);
            Assert.IsTrue(_jumpList.Current.IsSome(_textBuffer.GetPoint(0)));
            Assert.AreEqual(2, _jumpList.Jumps.Count());
        }
    }
}
