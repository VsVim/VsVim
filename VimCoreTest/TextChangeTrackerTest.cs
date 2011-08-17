using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.UnitTest;
using Vim.UnitTest.Mock;

namespace VimCore.UnitTest
{
    [TestFixture]
    public sealed class TextChangeTrackerTest
    {
        private MockRepository _factory;
        private ITextBuffer _textBuffer;
        private Mock<ITextView> _textView;
        private Mock<ITextCaret> _textCaret;
        private Mock<IVimLocalSettings> _localSettings;
        private Mock<ICommonOperations> _operations;
        private TextChangeTracker _trackerRaw;
        private ITextChangeTracker _tracker;
        private TextChange _lastChange;

        private void Create(params string[] lines)
        {
            _textBuffer = EditorUtil.CreateTextBuffer(lines);
            _factory = new MockRepository(MockBehavior.Loose);
            _textCaret = _factory.Create<ITextCaret>();
            _textView = MockObjectFactory.CreateTextView(
                textBuffer: _textBuffer,
                caret: _textCaret.Object,
                factory: _factory);
            _textView.SetupGet(x => x.HasAggregateFocus).Returns(true);
            _localSettings = _factory.Create<IVimLocalSettings>();
            _operations = _factory.Create<ICommonOperations>(MockBehavior.Strict);
            _trackerRaw = new TextChangeTracker(_textView.Object, _operations.Object);
            _trackerRaw.Enabled = true;
            _tracker = _trackerRaw;
            _tracker.ChangeCompleted += (sender, data) => { _lastChange = data; };
        }

        [TearDown]
        public void TearDown()
        {
            _tracker = null;
            _textBuffer = null;
            _lastChange = null;
        }

        /// <summary>
        /// Make sure that no tracking occurs when we are disabled
        /// </summary>
        [Test]
        public void DontTrackWhenDisabled()
        {
            Create("");
            _tracker.Enabled = false;
            _textBuffer.Insert(0, "a");
            Assert.IsNull(_lastChange);
            Assert.IsTrue(_tracker.CurrentChange.IsNone());
        }

        /// <summary>
        /// Make sure we clear out the text when disabling.  Don't want a change to persist across
        /// several enabled sessions
        /// </summary>
        [Test]
        public void DisableShouldClearCurrentChange()
        {
            Create("");
            _textBuffer.Insert(0, "a");
            _tracker.Enabled = false;
            Assert.IsNull(_lastChange);
            Assert.IsTrue(_tracker.CurrentChange.IsNone());
        }

        [Test]
        public void TypeForward1()
        {
            Create("the quick brown fox");
            _textBuffer.Insert(0, "a");
            Assert.IsNull(_lastChange);
            Assert.AreEqual(TextChange.NewInsert("a"), _tracker.CurrentChange.Value);
        }

        [Test]
        public void TypeForward2()
        {
            Create("the quick brown fox");
            _textBuffer.Insert(0, "a");
            _textBuffer.Insert(1, "b");
            Assert.IsNull(_lastChange);
            Assert.AreEqual(TextChange.NewInsert("ab"), _tracker.CurrentChange.Value);
        }

        [Test]
        public void TypeForward3()
        {
            Create("the quick brown fox");
            _textBuffer.Insert(0, "a");
            _textBuffer.Insert(1, "b");
            _textBuffer.Insert(2, "c");
            Assert.IsNull(_lastChange);
            Assert.AreEqual(TextChange.NewInsert("abc"), _tracker.CurrentChange.Value);
        }

        [Test]
        public void TypeForward_AddMany1()
        {
            Create("the quick brown fox");
            _textBuffer.Insert(0, "a");
            _textBuffer.Insert(1, "bcd");
            Assert.IsNull(_lastChange);
            Assert.AreEqual(TextChange.NewInsert("abcd"), _tracker.CurrentChange.Value);
        }

        [Test]
        public void TypeForward_AddMany2()
        {
            Create("the quick brown fox");
            _textBuffer.Insert(0, "ab");
            _textBuffer.Insert(2, "cde");
            Assert.IsNull(_lastChange);
            Assert.AreEqual(TextChange.NewInsert("abcde"), _tracker.CurrentChange.Value);
        }

        [Test]
        public void Delete1()
        {
            Create("the quick brown fox");
            _textBuffer.Insert(0, "ab");
            _textBuffer.Delete(new Span(1, 1));
            Assert.IsNull(_lastChange);
            Assert.AreEqual(TextChange.NewInsert("a"), _tracker.CurrentChange.Value);
        }

        [Test]
        public void Delete2()
        {
            Create("the quick brown fox");
            _textBuffer.Insert(0, "abc");
            _textBuffer.Delete(new Span(2, 1));
            _textBuffer.Delete(new Span(1, 1));
            Assert.IsNull(_lastChange);
            Assert.AreEqual(TextChange.NewInsert("a"), _tracker.CurrentChange.Value);
        }

        [Test]
        public void Delete3()
        {
            Create("the quick brown fox");
            _textBuffer.Insert(0, "abc");
            _textBuffer.Delete(new Span(2, 1));
            Assert.IsNull(_lastChange);
            Assert.AreEqual(TextChange.NewInsert("ab"), _tracker.CurrentChange.Value);
        }

        [Test]
        public void Delete4()
        {
            Create("the quick brown fox");
            _textBuffer.Delete(new Span(2, 1));
            Assert.IsNull(_lastChange);
            Assert.AreEqual(TextChange.NewDelete(1), _tracker.CurrentChange.Value);
        }

        [Test]
        public void Delete5()
        {
            Create("the quick brown fox");
            _textBuffer.Delete(new Span(2, 2));
            Assert.IsNull(_lastChange);
            Assert.AreEqual(TextChange.NewDelete(2), _tracker.CurrentChange.Value);
        }

        [Test]
        [Description("Deleting backwards should join the deletes")]
        public void Delete6()
        {
            Create("the quick brown fox");
            _textBuffer.Delete(new Span(2, 1));
            _textBuffer.Delete(new Span(1, 1));
            Assert.IsNull(_lastChange);
            Assert.AreEqual(TextChange.NewDelete(2), _tracker.CurrentChange.Value);
        }

        /// <summary>
        /// When spaces are in the buffer and tabs are hit and used Visual Studio will often convert
        /// spaces to tabs.  Without interpreting the line it looks like X spaces are deleted and 2 
        /// tabs are inserted when really it's just a conversion and should show up as a single tab
        /// insert
        /// </summary>
        [Test]
        public void Special_SpaceToTab()
        {
            Create("    hello");
            _operations.Setup(x => x.NormalizeBlanks("    ")).Returns("\t");
            _operations.Setup(x => x.NormalizeBlanks("\t\t")).Returns("\t\t");
            _textBuffer.Replace(new Span(0, 4), "\t\t");
            Assert.AreEqual(TextChange.NewInsert("\t"), _tracker.CurrentChange.Value);
        }

        /// <summary>
        /// Make sure a straight forward replace is handled properly 
        /// </summary>
        [Test]
        public void Replace_Complete()
        {
            Create("cat");
            _textBuffer.Replace(new Span(0, 3), "dog");
            var change = _tracker.CurrentChange.Value;
            Assert.IsTrue(change.IsCombination);
            Assert.IsTrue(change.AsCombination().Item1.IsDelete(3));
            Assert.IsTrue(change.AsCombination().Item2.IsInsert("dog"));
        }

        /// <summary>
        /// Replace a set of text with a smaller set of text
        /// </summary>
        [Test]
        public void Replace_Small()
        {
            Create("house");
            _textBuffer.Replace(new Span(0, 5), "dog");
            var change = _tracker.CurrentChange.Value;
            Assert.IsTrue(change.IsCombination);
            Assert.IsTrue(change.AsCombination().Item1.IsDelete(5));
            Assert.IsTrue(change.AsCombination().Item2.IsInsert("dog"));
        }

        /// <summary>
        /// Replace a set of text with a bigger set of text
        /// </summary>
        [Test]
        public void Replace_Big()
        {
            Create("dog");
            _textBuffer.Replace(new Span(0, 3), "house");
            var change = _tracker.CurrentChange.Value;
            Assert.IsTrue(change.IsCombination);
            Assert.IsTrue(change.AsCombination().Item1.IsDelete(3));
            Assert.IsTrue(change.AsCombination().Item2.IsInsert("house"));
        }

        /// <summary>
        /// A replace which occurs after an insert should be merged
        /// </summary>
        [Test]
        public void Merge_ReplaceAfterInsert()
        {
            Create("dog");
            _textBuffer.Replace(new Span(0, 0), "i");
            Assert.IsTrue(_tracker.CurrentChange.Value.IsInsert);
            _textBuffer.Replace(new Span(1, 3), "cat");
            var change = _trackerRaw.CurrentChange.Value;
            Assert.IsTrue(change.IsCombination);
            Assert.IsTrue(change.AsCombination().Item1.IsInsert("i"));
            Assert.IsTrue(change.AsCombination().Item2.IsCombination);
        }
    }
}
