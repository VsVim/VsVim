using System.Linq;
using Vim.EditorHost;
using Microsoft.VisualStudio.Text;
using Xunit;

namespace Vim.UnitTest
{
    /// <summary>
    /// Tests for the IFoldData implementation.  This class is responsible for simply holding onto the
    /// folded regions for a given ITextBuffer
    /// </summary>
    public sealed class FoldDataTest : VimTestBase
    {
        private ITextBuffer _textBuffer;
        private IFoldData _foldData;
        private FoldData _foldDataRaw;

        private void Create(params string[] lines)
        {
            _textBuffer = CreateTextBuffer(lines);
            _foldDataRaw = new FoldData(_textBuffer);
            _foldData = _foldDataRaw;
        }

        /// <summary>
        /// The default behavior of the Folds property is to have no folds
        /// </summary>
        [WpfFact]
        public void Folds_DefaultIsEmpty()
        {
            Create(string.Empty);
            Assert.Empty(_foldData.Folds);
        }

        /// <summary>
        /// Verify the expected behavior that creating a fold will result in the value
        /// being available in the collection
        /// </summary>
        [WpfFact]
        public void Folds_Simple()
        {
            Create("the quick brown", "fox jumped", " over the dog");
            var range = _textBuffer.GetLineRange(0, 1);
            _foldData.CreateFold(range);
            Assert.Equal(range.Extent, _foldData.Folds.Single());
        }

        /// <summary>
        /// Don't create a fold unless it's at least 2 lines.  Vim doesn't suppor the notion
        /// of single line folds
        /// </summary>
        [WpfFact]
        public void CreateFold_OneLine()
        {
            Create("the quick brown", "fox jumped", " over the dog");
            var range = _textBuffer.GetLineRange(0);
            _foldData.CreateFold(range);
            Assert.Empty(_foldData.Folds);
        }

        /// <summary>
        /// Standard fold creation case
        /// </summary>
        [WpfFact]
        public void CreateFold_Standard()
        {
            Create("the quick brown", "fox jumped", " over the dog");
            var range = _textBuffer.GetLineRange(0, 1);
            _foldData.CreateFold(range);
            Assert.Equal(range.Extent, _foldData.Folds.Single());
        }

        /// <summary>
        /// Sanity check that the API supports multiple folds
        /// </summary>
        [WpfFact]
        public void CreateFold_MultipleFolds()
        {
            Create("the quick brown", "fox jumped", " over the dog");
            var range1 = _textBuffer.GetLineRange(0, 1);
            _foldData.CreateFold(range1);
            var range2 = _textBuffer.GetLineRange(0, 2);
            _foldData.CreateFold(range2);
            Assert.Contains(range1.Extent, _foldData.Folds);
            Assert.Contains(range2.Extent, _foldData.Folds);
        }

        [WpfFact]
        public void DeleteFold1()
        {
            Create("the quick brown", "fox jumped", " over the dog");
            var range = _textBuffer.GetLineRange(0, 1);
            _foldData.CreateFold(range);
            Assert.False(_foldData.DeleteFold(_textBuffer.GetLine(2).Start));
        }

        [WpfFact]
        public void DeleteFold2()
        {
            Create("the quick brown", "fox jumped", " over the dog");
            var range = _textBuffer.GetLineRange(0, 1);
            _foldData.CreateFold(range);
            Assert.True(_foldData.DeleteFold(_textBuffer.GetLine(0).Start));
            Assert.Empty(_foldData.Folds);
        }

        [WpfFact]
        public void DeleteAllFolds1()
        {
            Create("the quick brown", "fox jumped", " over the dog");
            _foldData.CreateFold(_textBuffer.GetLineRange(0, 1));
            _foldData.CreateFold(_textBuffer.GetLineRange(0, 2));
            _foldData.DeleteAllFolds(_textBuffer.GetExtent());
            Assert.Empty(_foldData.Folds);
        }
    }
}
