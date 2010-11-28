using System.Linq;
using Microsoft.VisualStudio.Text;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;

namespace VimCore.Test
{
    [TestFixture]
    public class FoldManagerTest
    {
        private FoldManager _manager;
        private ITextBuffer _textBuffer;

        public void SetUp(params string[] lines)
        {
            _textBuffer = EditorUtil.CreateBuffer(lines);
            _manager = new FoldManager(_textBuffer);
        }

        [TearDown]
        public void TearDown()
        {
            _textBuffer = null;
            _manager = null;
        }

        [Test]
        public void Folds1()
        {
            SetUp(string.Empty);
            Assert.AreEqual(0, _manager.Folds.Count());
        }

        [Test]
        public void Folds2()
        {
            SetUp("the quick brown", "fox jumped", " over the dog");
            var span = _textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak;
            _manager.CreateFold(span);
            Assert.AreEqual(span, _manager.Folds.Single());
        }

        [Test]
        [Description("Don't create a fold unless it's at least 2 lines")]
        public void CreateFold1()
        {
            SetUp("the quick brown", "fox jumped", " over the dog");
            var span = _textBuffer.GetSpan(0, 3);
            _manager.CreateFold(span);
            Assert.AreEqual(0, _manager.Folds.Count());
        }

        [Test]
        public void CreateFold2()
        {
            SetUp("the quick brown", "fox jumped", " over the dog");
            var span = _textBuffer.GetLineRange(0, 1).Extent;
            _manager.CreateFold(span);
            Assert.AreEqual(_textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak, _manager.Folds.Single());
        }

        [Test]
        public void CreateFold3()
        {
            SetUp("the quick brown", "fox jumped", " over the dog");
            var span1 = _textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak;
            _manager.CreateFold(span1);
            var span2 = _textBuffer.GetLineRange(0, 2).ExtentIncludingLineBreak;
            _manager.CreateFold(span2);
            Assert.IsTrue(_manager.Folds.Contains(span1));
            Assert.IsTrue(_manager.Folds.Contains(span2));
        }

        [Test]
        [Description("Should expand to the entire line span")]
        public void CreateFold4()
        {
            SetUp("the quick brown", "fox jumped", " over the dog");
            var span = new SnapshotSpan(_textBuffer.GetPoint(3), _textBuffer.GetLine(1).Start.Add(1));
            _manager.CreateFold(span);
            Assert.AreEqual(_textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak, _manager.Folds.Single());
        }

        [Test]
        public void DeleteFold1()
        {
            SetUp("the quick brown", "fox jumped", " over the dog");
            var span = _textBuffer.GetLineRange(0, 1).Extent;
            _manager.CreateFold(span);
            Assert.IsFalse(_manager.DeleteFold(_textBuffer.GetLine(2).Start));
        }

        [Test]
        public void DeleteFold2()
        {
            SetUp("the quick brown", "fox jumped", " over the dog");
            var span = _textBuffer.GetLineRange(0, 1).Extent;
            _manager.CreateFold(span);
            Assert.IsTrue(_manager.DeleteFold(_textBuffer.GetLine(0).Start));
            Assert.AreEqual(0, _manager.Folds.Count());
        }

        [Test]
        public void DeleteAllFolds1()
        {
            SetUp("the quick brown", "fox jumped", " over the dog");
            var span1 = _textBuffer.GetLineRange(0, 1).Extent;
            _manager.CreateFold(span1);
            var span2 = _textBuffer.GetLineRange(0, 2).Extent;
            _manager.CreateFold(span2);
            _manager.DeleteAllFolds();
            Assert.AreEqual(0, _manager.Folds.Count());
        }
    }
}
