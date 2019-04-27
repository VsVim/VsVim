using Vim.EditorHost;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Vim.Extensions;
using Vim.Modes.Insert;
using Xunit;

namespace Vim.UnitTest
{
    public sealed class TextChangeTrackerTest : VimTestBase
    {
        private MockRepository _factory;
        private ITextBuffer _textBuffer;
        private ITextView _textView;
        private Mock<IVimTextBuffer> _vimTextBuffer;
        private Mock<ICommonOperations> _operations;
        private TextChangeTracker _trackerRaw;
        private ITextChangeTracker _tracker;
        private TextChange _lastChange;

        private void Create(params string[] lines)
        {
            _textView = CreateTextView(lines);
            _textBuffer = _textView.TextBuffer;
            _factory = new MockRepository(MockBehavior.Loose);
            _operations = _factory.Create<ICommonOperations>(MockBehavior.Strict);
            _operations.Setup(x => x.RecordLastChange(It.IsAny<SnapshotSpan>(), It.IsAny<SnapshotSpan>()));
            _operations.Setup(x => x.RecordLastYank(It.IsAny<SnapshotSpan>()));
            _vimTextBuffer = _factory.Create<IVimTextBuffer>(MockBehavior.Strict);
            _vimTextBuffer.SetupProperty(x => x.LastEditPoint);
            _vimTextBuffer.SetupProperty(x => x.LastChangeOrYankStart);
            _vimTextBuffer.SetupProperty(x => x.LastChangeOrYankEnd);
            _trackerRaw = new TextChangeTracker(_vimTextBuffer.Object, _textView, _operations.Object)
            {
                TrackCurrentChange = true
            };
            _tracker = _trackerRaw;
            _tracker.ChangeCompleted += (sender, args) => { _lastChange = args.TextChange; };
        }

        /// <summary>
        /// Make sure that no tracking occurs when we are disabled
        /// </summary>
        [WpfFact]
        public void DontTrackWhenDisabled()
        {
            Create("");
            _tracker.TrackCurrentChange = false;
            _textBuffer.Insert(0, "a");
            Assert.Null(_lastChange);
            Assert.True(_tracker.CurrentChange.IsNone());
        }

        /// <summary>
        /// Make sure we clear out the text when disabling.  Don't want a change to persist across
        /// several enabled sessions
        /// </summary>
        [WpfFact]
        public void DisableShouldClearCurrentChange()
        {
            Create("");
            _textBuffer.Insert(0, "a");
            _tracker.TrackCurrentChange = false;
            Assert.Null(_lastChange);
            Assert.True(_tracker.CurrentChange.IsNone());
        }

        [WpfFact]
        public void TypeForward1()
        {
            Create("the quick brown fox");
            _textBuffer.Insert(0, "a");
            Assert.Null(_lastChange);
            Assert.Equal(TextChange.NewInsert("a"), _tracker.CurrentChange.Value);
        }

        [WpfFact]
        public void TypeForward2()
        {
            Create("the quick brown fox");
            _textBuffer.Insert(0, "a");
            _textBuffer.Insert(1, "b");
            Assert.Null(_lastChange);
            Assert.Equal(TextChange.NewInsert("ab"), _tracker.CurrentChange.Value);
        }

        [WpfFact]
        public void TypeForward3()
        {
            Create("the quick brown fox");
            _textBuffer.Insert(0, "a");
            _textBuffer.Insert(1, "b");
            _textBuffer.Insert(2, "c");
            Assert.Null(_lastChange);
            Assert.Equal(TextChange.NewInsert("abc"), _tracker.CurrentChange.Value);
        }

        [WpfFact]
        public void TypeForward_AddMany1()
        {
            Create("the quick brown fox");
            _textBuffer.Insert(0, "a");
            _textBuffer.Insert(1, "bcd");
            Assert.Null(_lastChange);
            Assert.Equal(TextChange.NewInsert("abcd"), _tracker.CurrentChange.Value);
        }

        [WpfFact]
        public void TypeForward_AddMany2()
        {
            Create("the quick brown fox");
            _textBuffer.Insert(0, "ab");
            _textBuffer.Insert(2, "cde");
            Assert.Null(_lastChange);
            Assert.Equal(TextChange.NewInsert("abcde"), _tracker.CurrentChange.Value);
        }

        [WpfFact]
        public void Delete1()
        {
            Create("the quick brown fox");
            _textBuffer.Insert(0, "ab");
            _textBuffer.Delete(new Span(1, 1));
            Assert.Null(_lastChange);
            Assert.Equal(TextChange.NewInsert("a"), _tracker.CurrentChange.Value);
        }

        [WpfFact]
        public void Delete2()
        {
            Create("the quick brown fox");
            _textBuffer.Insert(0, "abc");
            _textBuffer.Delete(new Span(2, 1));
            _textBuffer.Delete(new Span(1, 1));
            Assert.Null(_lastChange);
            Assert.Equal(TextChange.NewInsert("a"), _tracker.CurrentChange.Value);
        }

        [WpfFact]
        public void Delete3()
        {
            Create("the quick brown fox");
            _textBuffer.Insert(0, "abc");
            _textBuffer.Delete(new Span(2, 1));
            Assert.Null(_lastChange);
            Assert.Equal(TextChange.NewInsert("ab"), _tracker.CurrentChange.Value);
        }

        [WpfFact]
        public void Delete4()
        {
            Create("the quick brown fox");
            _textBuffer.Delete(new Span(2, 1));
            Assert.Null(_lastChange);
            Assert.Equal(TextChange.NewDeleteLeft(1), _tracker.CurrentChange.Value);
        }

        [WpfFact]
        public void Delete5()
        {
            Create("the quick brown fox");
            _textBuffer.Delete(new Span(2, 2));
            Assert.Null(_lastChange);
            Assert.Equal(TextChange.NewDeleteLeft(2), _tracker.CurrentChange.Value);
        }

        /// <summary>
        /// Deleting backwards should join the deletes
        /// </summary>
        [WpfFact]
        public void Delete6()
        {
            Create("the quick brown fox");
            _textBuffer.Delete(new Span(2, 1));
            _textBuffer.Delete(new Span(1, 1));
            Assert.Null(_lastChange);
            Assert.Equal(TextChange.NewDeleteLeft(2), _tracker.CurrentChange.Value);
        }

        /// <summary>
        /// Make sure that it can detect a delete right vs. a delete left
        /// </summary>
        [WpfFact]
        public void DeleteRight_Simple()
        {
            Create("cat dog");
            _textBuffer.Delete(new Span(0, 3));
            Assert.True(_tracker.CurrentChange.Value.IsDeleteRight(3));
        }

        /// <summary>
        /// Don't treat a caret to the right of the start as a delete right
        /// </summary>
        [WpfFact]
        public void DeleteRight_FromMiddle()
        {
            Create("cat dog");
            _textView.MoveCaretTo(1);
            _textBuffer.Delete(new Span(0, 3));
            Assert.True(_tracker.CurrentChange.Value.IsDeleteLeft(3));
        }

        /// <summary>
        /// When spaces are in the buffer and tabs are hit and used Visual Studio will often convert
        /// spaces to tabs.  Without interpreting the line it looks like X spaces are deleted and 2 
        /// tabs are inserted when really it's just a conversion and should show up as a single tab
        /// insert
        /// </summary>
        [WpfFact]
        public void Special_SpaceToTab()
        {
            Create("    hello");
            _operations.Setup(x => x.GetSpacesToPoint(It.IsAny<SnapshotPoint>())).Returns(0);
            _operations.Setup(x => x.NormalizeBlanks("    ", 0)).Returns("\t");
            _operations.Setup(x => x.NormalizeBlanks("\t\t", 0)).Returns("\t\t");
            _textBuffer.Replace(new Span(0, 4), "\t\t");
            Assert.Equal(TextChange.NewInsert("\t"), _tracker.CurrentChange.Value);
        }

        /// <summary>
        /// Make sure a straight forward replace is handled properly 
        /// </summary>
        [WpfFact]
        public void Replace_Complete()
        {
            Create("cat");
            _textView.MoveCaretTo(1);
            _textBuffer.Replace(new Span(0, 3), "dog");
            var change = _tracker.CurrentChange.Value;
            Assert.True(change.IsCombination);
            Assert.True(change.AsCombination().Left.IsDeleteLeft(3));
            Assert.True(change.AsCombination().Right.IsInsert("dog"));
        }

        /// <summary>
        /// Replace a set of text with a smaller set of text
        /// </summary>
        [WpfFact]
        public void Replace_Small()
        {
            Create("house");
            _textView.MoveCaretTo(1);
            _textBuffer.Replace(new Span(0, 5), "dog");
            var change = _tracker.CurrentChange.Value;
            Assert.True(change.IsCombination);
            Assert.True(change.AsCombination().Left.IsDeleteLeft(5));
            Assert.True(change.AsCombination().Right.IsInsert("dog"));
        }

        /// <summary>
        /// Replace a set of text with a bigger set of text
        /// </summary>
        [WpfFact]
        public void Replace_Big()
        {
            Create("dog");
            _textView.MoveCaretTo(1);
            _textBuffer.Replace(new Span(0, 3), "house");
            var change = _tracker.CurrentChange.Value;
            Assert.True(change.IsCombination);
            Assert.True(change.AsCombination().Left.IsDeleteLeft(3));
            Assert.True(change.AsCombination().Right.IsInsert("house"));
        }

        /// <summary>
        /// A replace which occurs after an insert should be merged
        /// </summary>
        [WpfFact]
        public void Merge_ReplaceAfterInsert()
        {
            Create("dog");
            _textBuffer.Replace(new Span(0, 0), "i");
            Assert.True(_tracker.CurrentChange.Value.IsInsert);
            _textBuffer.Replace(new Span(1, 3), "cat");
            var change = _trackerRaw.CurrentChange.Value;
            Assert.True(change.IsCombination);
            Assert.True(change.AsCombination().Left.IsInsert("i"));
            Assert.True(change.AsCombination().Right.IsCombination);
        }
    }
}
