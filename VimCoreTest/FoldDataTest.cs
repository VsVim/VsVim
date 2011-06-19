using System.Linq;
using Microsoft.VisualStudio.Text;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    /// <summary>
    /// Tests for the IFoldData implementation.
    /// </summary>
    [TestFixture]
    public sealed class FoldDataTest
    {
        private ITextBuffer _textBuffer;
        private IFoldData _foldData;
        private FoldData _foldDataRaw;

        public void SetUp(params string[] lines)
        {
            _textBuffer = EditorUtil.CreateTextBuffer(lines);
            _foldDataRaw = new FoldData(_textBuffer);
            _foldData = _foldDataRaw;
        }

        [TearDown]
        public void TearDown()
        {
            _textBuffer = null;
            _foldData = null;
            _foldDataRaw = null;
        }

        [Test]
        public void Folds1()
        {
            SetUp(string.Empty);
            Assert.AreEqual(0, _foldData.Folds.Count());
        }

        [Test]
        public void Folds2()
        {
            SetUp("the quick brown", "fox jumped", " over the dog");
            var range = _textBuffer.GetLineRange(0, 1);
            _foldData.CreateFold(range);
            Assert.AreEqual(range.ExtentIncludingLineBreak, _foldData.Folds.Single());
        }

        [Test]
        [Description("Don't create a fold unless it's at least 2 lines")]
        public void CreateFold1()
        {
            SetUp("the quick brown", "fox jumped", " over the dog");
            var range = _textBuffer.GetLineRange(0);
            _foldData.CreateFold(range);
            Assert.AreEqual(0, _foldData.Folds.Count());
        }

        [Test]
        public void CreateFold2()
        {
            SetUp("the quick brown", "fox jumped", " over the dog");
            var range = _textBuffer.GetLineRange(0, 1);
            _foldData.CreateFold(range);
            Assert.AreEqual(range.ExtentIncludingLineBreak, _foldData.Folds.Single());
        }

        [Test]
        public void CreateFold3()
        {
            SetUp("the quick brown", "fox jumped", " over the dog");
            var range1 = _textBuffer.GetLineRange(0, 1);
            _foldData.CreateFold(range1);
            var range2 = _textBuffer.GetLineRange(0, 2);
            _foldData.CreateFold(range2);
            Assert.IsTrue(_foldData.Folds.Contains(range1.ExtentIncludingLineBreak));
            Assert.IsTrue(_foldData.Folds.Contains(range2.ExtentIncludingLineBreak));
        }

        [Test]
        public void DeleteFold1()
        {
            SetUp("the quick brown", "fox jumped", " over the dog");
            var range = _textBuffer.GetLineRange(0, 1);
            _foldData.CreateFold(range);
            Assert.IsFalse(_foldData.DeleteFold(_textBuffer.GetLine(2).Start));
        }

        [Test]
        public void DeleteFold2()
        {
            SetUp("the quick brown", "fox jumped", " over the dog");
            var range = _textBuffer.GetLineRange(0, 1);
            _foldData.CreateFold(range);
            Assert.IsTrue(_foldData.DeleteFold(_textBuffer.GetLine(0).Start));
            Assert.AreEqual(0, _foldData.Folds.Count());
        }

        [Test]
        public void DeleteAllFolds1()
        {
            SetUp("the quick brown", "fox jumped", " over the dog");
            _foldData.CreateFold(_textBuffer.GetLineRange(0, 1));
            _foldData.CreateFold(_textBuffer.GetLineRange(0, 2));
            _foldData.DeleteAllFolds();
            Assert.AreEqual(0, _foldData.Folds.Count());
        }
    }
}
