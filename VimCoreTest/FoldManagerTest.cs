using System.Linq;
using Microsoft.VisualStudio.Text;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class FoldManagerTest
    {
        private FoldManager _manager;
        private ITextBuffer _textBuffer;

        public void SetUp(params string[] lines)
        {
            _textBuffer = EditorUtil.CreateTextBuffer(lines);
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
            var range = _textBuffer.GetLineRange(0, 1);
            _manager.CreateFold(range);
            Assert.AreEqual(range.ExtentIncludingLineBreak, _manager.Folds.Single());
        }

        [Test]
        [Description("Don't create a fold unless it's at least 2 lines")]
        public void CreateFold1()
        {
            SetUp("the quick brown", "fox jumped", " over the dog");
            var range = _textBuffer.GetLineRange(0);
            _manager.CreateFold(range);
            Assert.AreEqual(0, _manager.Folds.Count());
        }

        [Test]
        public void CreateFold2()
        {
            SetUp("the quick brown", "fox jumped", " over the dog");
            var range = _textBuffer.GetLineRange(0, 1);
            _manager.CreateFold(range);
            Assert.AreEqual(range.ExtentIncludingLineBreak, _manager.Folds.Single());
        }

        [Test]
        public void CreateFold3()
        {
            SetUp("the quick brown", "fox jumped", " over the dog");
            var range1 = _textBuffer.GetLineRange(0, 1);
            _manager.CreateFold(range1);
            var range2 = _textBuffer.GetLineRange(0, 2);
            _manager.CreateFold(range2);
            Assert.IsTrue(_manager.Folds.Contains(range1.ExtentIncludingLineBreak));
            Assert.IsTrue(_manager.Folds.Contains(range2.ExtentIncludingLineBreak));
        }

        [Test]
        public void DeleteFold1()
        {
            SetUp("the quick brown", "fox jumped", " over the dog");
            var range = _textBuffer.GetLineRange(0, 1);
            _manager.CreateFold(range);
            Assert.IsFalse(_manager.DeleteFold(_textBuffer.GetLine(2).Start));
        }

        [Test]
        public void DeleteFold2()
        {
            SetUp("the quick brown", "fox jumped", " over the dog");
            var range = _textBuffer.GetLineRange(0, 1);
            _manager.CreateFold(range);
            Assert.IsTrue(_manager.DeleteFold(_textBuffer.GetLine(0).Start));
            Assert.AreEqual(0, _manager.Folds.Count());
        }

        [Test]
        public void DeleteAllFolds1()
        {
            SetUp("the quick brown", "fox jumped", " over the dog");
            _manager.CreateFold(_textBuffer.GetLineRange(0, 1));
            _manager.CreateFold(_textBuffer.GetLineRange(0, 2));
            _manager.DeleteAllFolds();
            Assert.AreEqual(0, _manager.Folds.Count());
        }
    }
}
