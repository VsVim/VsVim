using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Outlining;
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
        private IAdhocOutliner _adhocOutliner;
        private IOutliningManager _outliningeManager;

        private void Create(params string[] lines)
        {
            _textView = EditorUtil.CreateTextView(lines);
            _visualBuffer = _textView.TextViewModel.VisualBuffer;
            _adhocOutliner = EditorUtil.FactoryService.AdhocOutlinerFactory.GetAdhocOutliner(_textView.TextBuffer);
            _outliningeManager = EditorUtil.FactoryService.OutliningManagerService.GetOutliningManager(_textView);
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

        /// <summary>
        /// When dealing with a non-vim outline which does not stretch the span of the line, we should 
        /// still be able to open it from any point on the line
        /// </summary>
        [Test]
        public void OpenFold_AdhocPartialLine()
        {
            Create("cat dog", "fish tree");
            _adhocOutliner.CreateOutliningRegion(_textView.GetLineSpan(0, 3, 4), "", "");
            _outliningeManager.CollapseAll(_textView.GetLine(0).ExtentIncludingLineBreak, _ => true);
            Assert.AreEqual("cat", _visualBuffer.GetLine(0).GetText());
            _foldManager.OpenFold(_textView.GetPoint(0), 1);
            Assert.AreEqual("cat dog", _visualBuffer.GetLine(0).GetText());
        }

        /// <summary>
        /// When there are multiple collapsed folds the first fold should be preferred when opening
        /// </summary>
        [Test]
        public void OpenFold_PreferFirstFold()
        {
            Create("dog", "cat", "fish", "bear", "tree", "pig");
            _foldManager.CreateFold(_textView.GetLineRange(2, 3));
            _foldManager.CreateFold(_textView.GetLineRange(1, 4));
            Assert.AreEqual(3, _visualBuffer.CurrentSnapshot.LineCount);
            _foldManager.OpenFold(_textView.GetLine(2).Start, 1);
            Assert.AreEqual("cat", _visualBuffer.GetLine(1).GetText());
            _foldManager.OpenFold(_textView.GetLine(2).Start, 1);
            Assert.AreEqual("fish", _visualBuffer.GetLine(2).GetText());
        }

        /// <summary>
        /// When dealing with a non-vim outline which does not stretch the span of the line, we should 
        /// still be able to close it from any point on the line
        /// </summary>
        [Test]
        public void CloseFold_AdhocPartialLine()
        {
            Create("cat dog", "fish tree");
            _adhocOutliner.CreateOutliningRegion(_textView.GetLineSpan(0, 3, 4), "", "");
            _foldManager.CloseFold(_textView.GetPoint(0), 1);
            Assert.AreEqual("cat", _visualBuffer.GetLine(0).GetText());
        }
    }
}
