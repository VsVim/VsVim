using System.Linq;
using Microsoft.VisualStudio.Text;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    /// <summary>
    /// Tests for the IFoldData implementation.  This class is responsible for simply holding onto the
    /// folded regions for a given ITextBuffer
    /// </summary>
    [TestFixture]
    public sealed class FoldDataTest
    {
        private ITextBuffer _textBuffer;
        private IFoldData _foldData;
        private FoldData _foldDataRaw;

        private void Create(params string[] lines)
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

        /// <summary>
        /// The default behavior of the Folds property is to have no folds
        /// </summary>
        [Test]
        public void Folds_DefaultIsEmpty()
        {
            Create(string.Empty);
            Assert.AreEqual(0, _foldData.Folds.Count());
        }

        /// <summary>
        /// Verify the expected behavior that creating a fold will result in the value
        /// being available in the collection
        /// </summary>
        [Test]
        public void Folds_Simple()
        {
            Create("the quick brown", "fox jumped", " over the dog");
            var range = _textBuffer.GetLineRange(0, 1);
            _foldData.CreateFold(range);
            Assert.AreEqual(range.Extent, _foldData.Folds.Single());
        }

        /// <summary>
        /// Don't create a fold unless it's at least 2 lines.  Vim doesn't suppor the notion
        /// of single line folds
        /// </summary>
        [Test]
        public void CreateFold_OneLine()
        {
            Create("the quick brown", "fox jumped", " over the dog");
            var range = _textBuffer.GetLineRange(0);
            _foldData.CreateFold(range);
            Assert.AreEqual(0, _foldData.Folds.Count());
        }

        /// <summary>
        /// Standard fold creation case
        /// </summary>
        [Test]
        public void CreateFold_Standard()
        {
            Create("the quick brown", "fox jumped", " over the dog");
            var range = _textBuffer.GetLineRange(0, 1);
            _foldData.CreateFold(range);
            Assert.AreEqual(range.Extent, _foldData.Folds.Single());
        }

        /// <summary>
        /// Sanity check that the API supports multiple folds
        /// </summary>
        [Test]
        public void CreateFold_MultipleFolds()
        {
            Create("the quick brown", "fox jumped", " over the dog");
            var range1 = _textBuffer.GetLineRange(0, 1);
            _foldData.CreateFold(range1);
            var range2 = _textBuffer.GetLineRange(0, 2);
            _foldData.CreateFold(range2);
            Assert.IsTrue(_foldData.Folds.Contains(range1.Extent));
            Assert.IsTrue(_foldData.Folds.Contains(range2.Extent));
        }

        [Test]
        public void DeleteFold1()
        {
            Create("the quick brown", "fox jumped", " over the dog");
            var range = _textBuffer.GetLineRange(0, 1);
            _foldData.CreateFold(range);
            Assert.IsFalse(_foldData.DeleteFold(_textBuffer.GetLine(2).Start));
        }

        [Test]
        public void DeleteFold2()
        {
            Create("the quick brown", "fox jumped", " over the dog");
            var range = _textBuffer.GetLineRange(0, 1);
            _foldData.CreateFold(range);
            Assert.IsTrue(_foldData.DeleteFold(_textBuffer.GetLine(0).Start));
            Assert.AreEqual(0, _foldData.Folds.Count());
        }

        [Test]
        public void DeleteAllFolds1()
        {
            Create("the quick brown", "fox jumped", " over the dog");
            _foldData.CreateFold(_textBuffer.GetLineRange(0, 1));
            _foldData.CreateFold(_textBuffer.GetLineRange(0, 2));
            _foldData.DeleteAllFolds(_textBuffer.GetExtent());
            Assert.AreEqual(0, _foldData.Folds.Count());
        }
    }
}
