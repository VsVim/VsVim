using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;
using Vim.UnitTest.Mock;

namespace VimCore.Test
{
    [TestFixture]
    public class TextChangeTrackerTest
    {
        private MockRepository _factory;
        private ITextBuffer _textBuffer;
        private Mock<ITextView> _textView;
        private Mock<ITextCaret> _textCaret;
        private Mock<IMouseDevice> _mouse;
        private Mock<IKeyboardDevice> _keyboard;
        private MockVimBuffer _vimBuffer;
        private TextChangeTracker _trackerRaw;
        private ITextChangeTracker _tracker;
        private string _lastChange;

        protected void Create(params string[] lines)
        {
            _textBuffer = EditorUtil.CreateBuffer(lines);
            _factory = new MockRepository(MockBehavior.Loose);
            _textCaret = _factory.Create<ITextCaret>();
            _textView = _factory.Create<ITextView>();
            _textView.SetupGet(x => x.Caret).Returns(_textCaret.Object);
            _textView.SetupGet(x => x.HasAggregateFocus).Returns(true);
            _mouse = _factory.Create<IMouseDevice>();
            _keyboard = _factory.Create<IKeyboardDevice>();
            _vimBuffer = new MockVimBuffer()
            {
                TextViewImpl = _textView.Object,
                TextBufferImpl = _textBuffer
            };
            _trackerRaw = new TextChangeTracker(_vimBuffer, _keyboard.Object, _mouse.Object);
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

        [Test]
        public void TypeForward1()
        {
            Create("the quick brown fox");
            _textBuffer.Insert(0, "a");
            Assert.IsNull(_lastChange);
            Assert.AreEqual("a", _tracker.CurrentChange);
        }

        [Test]
        public void TypeForward2()
        {
            Create("the quick brown fox");
            _textBuffer.Insert(0, "a");
            _textBuffer.Insert(1, "b");
            Assert.IsNull(_lastChange);
            Assert.AreEqual("ab", _tracker.CurrentChange);
        }

        [Test]
        public void TypeForward3()
        {
            Create("the quick brown fox");
            _textBuffer.Insert(0, "a");
            _textBuffer.Insert(1, "b");
            _textBuffer.Insert(2, "c");
            Assert.IsNull(_lastChange);
            Assert.AreEqual("abc", _tracker.CurrentChange);
        }

        [Test]
        public void TypeForward_AddMany1()
        {
            Create("the quick brown fox");
            _textBuffer.Insert(0, "a");
            _textBuffer.Insert(1, "bcd");
            Assert.IsNull(_lastChange);
            Assert.AreEqual("abcd", _tracker.CurrentChange);
        }

        [Test]
        public void TypeForward_AddMany2()
        {
            Create("the quick brown fox");
            _textBuffer.Insert(0, "ab");
            _textBuffer.Insert(2, "cde");
            Assert.IsNull(_lastChange);
            Assert.AreEqual("abcde", _tracker.CurrentChange);
        }

        [Test]
        public void Delete1()
        {
            Create("the quick brown fox");
            _textBuffer.Insert(0, "ab");
            _textBuffer.Delete(new Span(1, 1));
            Assert.IsNull(_lastChange);
            Assert.AreEqual("a", _tracker.CurrentChange);
        }

        [Test]
        public void Delete2()
        {
            Create("the quick brown fox");
            _textBuffer.Insert(0, "abc");
            _textBuffer.Delete(new Span(2, 1));
            _textBuffer.Delete(new Span(1, 1));
            Assert.IsNull(_lastChange);
            Assert.AreEqual("a", _tracker.CurrentChange);
        }

        [Test]
        public void Delete3()
        {
            Create("the quick brown fox");
            _textBuffer.Insert(0, "abc");
            _textBuffer.Delete(new Span(2, 1));
            Assert.IsNull(_lastChange);
            Assert.AreEqual("ab", _tracker.CurrentChange);
        }

        [Test]
        public void Replace1()
        {
            Create("the quick brown fox");
            _textBuffer.Insert(0, "a");
            _textBuffer.Replace(new Span(0, 1), "b");
            Assert.IsNull(_lastChange);
            Assert.AreEqual("b", _tracker.CurrentChange);
        }

        [Test]
        [Description("Mouse click should complete the change")]
        public void CaretMove1()
        {
            Create("the quick brown fox");
            _textBuffer.Insert(0, "a");
            _mouse.SetupGet(x => x.IsLeftButtonPressed).Returns(true).Verifiable();
            _textCaret.Raise(x => x.PositionChanged += null, (CaretPositionChangedEventArgs)null);
            Assert.AreEqual("a", _lastChange);
            Assert.AreEqual("", _tracker.CurrentChange);
            _factory.Verify();
        }

        [Test]
        [Description("Normal caret movement (as part of an edit) shouldn't complete changse")]
        public void CaretMove2()
        {
            Create("the quick brown fox");
            _mouse.SetupGet(x => x.IsLeftButtonPressed).Returns(false).Verifiable();
            _textBuffer.Insert(0, "a");
            _textCaret.Raise(x => x.PositionChanged += null, (CaretPositionChangedEventArgs)null);
            _textBuffer.Insert(1, "b");
            Assert.IsNull(_lastChange);
            Assert.AreEqual("ab", _tracker.CurrentChange);
            _factory.Verify();
        }

        [Test]
        [Description("Commit style events with no change shoudn't raise the ChangeCompleted event")]
        public void ChangedEvent1()
        {
            Create("the quick brown fox");
            var didRun = false;
            _tracker.ChangeCompleted += delegate { didRun = true; };
            _mouse.SetupGet(x => x.IsLeftButtonPressed).Returns(true).Verifiable();
            _textCaret.Raise(x => x.PositionChanged += null, (CaretPositionChangedEventArgs)null);
            Assert.IsFalse(didRun);
        }

        [Test]
        [Description("Don't double raise the event")]
        public void ChangedEvent2()
        {
            Create("the quick brown fox");
            _textBuffer.Insert(1, "b");
            _mouse.SetupGet(x => x.IsLeftButtonPressed).Returns(true).Verifiable();
            _textCaret.Raise(x => x.PositionChanged += null, (CaretPositionChangedEventArgs)null);
            var didRun = false;
            _tracker.ChangeCompleted += delegate { didRun = true; };
            _textCaret.Raise(x => x.PositionChanged += null, (CaretPositionChangedEventArgs)null);
            Assert.IsFalse(didRun);
        }

        [Test]
        public void SwitchMode1()
        {
            Create("the quick brown fox");
            _textBuffer.Insert(1, "b");
            _vimBuffer.RaiseSwitchedMode(null);
            Assert.AreEqual("b", _lastChange);
        }

        [Test]
        [Description("Don't run if there are no changes")]
        public void SwitchMode2()
        {
            Create("the quick brown fox");
            var didRun = false;
            _tracker.ChangeCompleted += delegate { didRun = true; };
            _vimBuffer.RaiseSwitchedMode(null);
            Assert.IsFalse(didRun);
        }
    }
}
