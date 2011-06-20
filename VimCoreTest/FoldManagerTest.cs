using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    /// <summary>
    /// Tests for the IFoldManager implementation.  The Vim behavior for folds, especial that of 
    /// nested folds, is largely undocumented and this class serves to test the implementation 
    /// we've inferred via behavior checks
    ///
    /// This test eschews any mocks because the behavior of both Vim and several editor types, 
    /// IOutuliningManager, in particular are not documented well.  Their behavior is the documentation
    /// and it's important that it's correct
    /// </summary>
    [TestFixture]
    public sealed class FoldManagerTest
    {
        private ITextView _textView;
        private IFoldData _foldData;
        private FoldManager _foldManagerRaw;
        private IFoldManager _foldManager;
        private Mock<IStatusUtil> _statusUtil;
        private ITextBuffer _visualBuffer;

        private void Create(params string[] lines)
        {
            _textView = EditorUtil.CreateTextView(lines);
            _visualBuffer = _textView.TextViewModel.VisualBuffer;
            _statusUtil = new Mock<IStatusUtil>(MockBehavior.Strict);
            _foldData = EditorUtil.FactoryService.FoldManagerFactory.GetFoldData(_textView.TextBuffer);
            _foldManagerRaw = new FoldManager(
                _textView,
                _foldData,
                _statusUtil.Object,
                FSharpOption.Create(EditorUtil.FactoryService.OutliningManagerService.GetOutliningManager(_textView)));
            _foldManager = _foldManagerRaw;
        }

        /// <summary>
        /// Creating a new fold in the ITextView should automatically close it
        /// </summary>
        [Test]
        public void CreateFold_ShouldClose()
        {
            Create("cat", "dog", "bear", "fish");
            _foldManager.CreateFold(_textView.GetLineRange(1, 2));
            Assert.AreEqual(3, _visualBuffer.CurrentSnapshot.LineCount);
            Assert.AreEqual("cat", _visualBuffer.GetLine(0).GetText());
            Assert.AreEqual("fish", _visualBuffer.GetLine(2).GetText());
        }

        /// <summary>
        /// Creating a fold with a range of 1 line should have no affect.  Vim doesn't supporting
        /// folding of 1 line as it makes no sense since it only supports line based folds
        /// </summary>
        [Test]
        public void CreateFold_OneLine()
        {
            Create("cat", "dog", "bear");
            _foldManager.CreateFold(_textView.GetLineRange(0, 0));
            Assert.AreEqual(3, _visualBuffer.CurrentSnapshot.LineCount);
        }
    }
}
