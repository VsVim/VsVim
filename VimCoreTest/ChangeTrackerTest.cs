using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Vim;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Microsoft.VisualStudio.Text;
using VimCoreTest.Utils;

namespace VimCoreTest
{
    [TestFixture]
    public class ChangeTrackerTest
    {
        private ChangeTracker _trackerRaw;
        private IChangeTracker _tracker;
        private ITextBuffer _textBuffer;
        private Mock<ITextView> _textView;
        private MockVimBuffer _buffer;
        private Mock<INormalMode> _normalMode;

        private void CreateForText(params string[] lines)
        {
            _textBuffer = Utils.EditorUtil.CreateBuffer(lines);
            _textView = Utils.MockObjectFactory.CreateTextView(_textBuffer);
            _textView.SetupGet(x => x.HasAggregateFocus).Returns(true);
            _buffer = new MockVimBuffer();
            _buffer.TextViewImpl = _textView.Object;
            _buffer.TextBufferImpl = _textBuffer;
            _normalMode = new Mock<INormalMode>(MockBehavior.Loose);
            _buffer.NormalModeImpl = _normalMode.Object;
            _trackerRaw = new ChangeTracker();
            _tracker = _trackerRaw;
            _trackerRaw.OnVimBufferCreated(_buffer);
        }

        [Test]
        public void BufferChange1()
        {
            CreateForText("foo bar");
            _textBuffer.Insert(0, "f");
            Assert.IsTrue(_tracker.LastChange.HasValue());
            Assert.AreEqual("f", _tracker.LastChange.Value.AsTextChange().Item);
        }

        [Test]
        public void BufferChange2()
        {
            CreateForText("foo bar");
            _textBuffer.Insert(0, "f");
            _textBuffer.Insert(1, "o");
            Assert.IsTrue(_tracker.LastChange.HasValue());
            Assert.AreEqual("fo", _tracker.LastChange.Value.AsTextChange().Item);
        }

        [Test]
        public void BufferChange3()
        {
            CreateForText("foo bar");
            _textBuffer.Insert(0, "f");
            _textBuffer.Insert(1, "o");
            _textBuffer.Insert(2, "o");
            Assert.IsTrue(_tracker.LastChange.HasValue());
            Assert.AreEqual("foo", _tracker.LastChange.Value.AsTextChange().Item);
        }

        [Test, Description("As soon as the change is non-contiguous it should reset")]
        public void BufferChange4()
        {
            CreateForText("foo bar");
            _textBuffer.Insert(0, "f");
            _textBuffer.Insert(1, "o");
            _textBuffer.Insert(0, "b");
            Assert.IsTrue(_tracker.LastChange.HasValue());
            Assert.AreEqual("b", _tracker.LastChange.Value.AsTextChange().Item);
        }

        [Test, Description("As soon as the change is non-contiguous it should reset")]
        public void BufferChange5()
        {
            CreateForText("foo bar");
            _textBuffer.Insert(0, "f");
            _textBuffer.Insert(1, "o");
            _textBuffer.Insert(1, "b");
            Assert.IsTrue(_tracker.LastChange.HasValue());
            Assert.AreEqual("b", _tracker.LastChange.Value.AsTextChange().Item);
        }

        [Test]
        public void BufferChange6()
        {
            CreateForText("foo bar");
            _textBuffer.Insert(0, "f");
            _textBuffer.Insert(1, Environment.NewLine);
            Assert.IsTrue(_tracker.LastChange.HasValue());
            Assert.AreEqual("f" + Environment.NewLine, _tracker.LastChange.Value.AsTextChange().Item);
        }

        [Test, Description("More than 1 character is fine for a change")]
        public void BufferChange7()
        {
            CreateForText("foo bar");
            _textBuffer.Insert(0, "hey");
            Assert.IsTrue(_tracker.LastChange.HasValue());
            Assert.AreEqual("hey", _tracker.LastChange.Value.AsTextChange().Item);
        }

        [Test, Description("Change should only count if we have aggregate focus")]
        public void BufferChange8()
        {
            CreateForText("foo bar");
            _textView.SetupGet(x => x.HasAggregateFocus).Returns(false);
            _textBuffer.Insert(0, "hey");
            Assert.IsFalse(_tracker.LastChange.HasValue());
        }

        [Test, Description("Disconnected changes shouln't be remembered")]
        public void BufferChange9()
        {
            CreateForText("foo bar baz");
            using (var edit = _textBuffer.CreateEdit())
            {
                edit.Insert(0, "f");
                edit.Insert(7, "b");
                edit.Apply();
            }
            Assert.IsFalse(_tracker.LastChange.HasValue());
        }

        [Test, Description("Disconnected changes should clear the last change flag")]
        public void BufferChange10()
        {
            CreateForText("foo bar baz");
            _textBuffer.Insert(0, "f");
            using (var edit = _textBuffer.CreateEdit())
            {
                edit.Insert(1, "f");
                edit.Insert(7, "b");
                edit.Apply();
            }
            Assert.IsFalse(_tracker.LastChange.HasValue());
        }

        [Test, Description("Switching modes should break up the text change")]
        public void SwitchMode1()
        {
            CreateForText("foo");
            _textBuffer.Insert(0, "h");
            var mode = new Mock<IMode>();
            _buffer.RaiseSwitchedMode(mode.Object);
            _textBuffer.Insert(1, "e");
            Assert.AreEqual("e", _tracker.LastChange.Value.AsTextChange().Item);
        }

        [Test, Description("SwitchMode should not clear the LastChange field")]
        public void SwitchMode2()
        {
            CreateForText("foo");
            _textBuffer.Insert(0, "h");
            var mode = new Mock<IMode>();
            _buffer.RaiseSwitchedMode(mode.Object);
            Assert.AreEqual("h", _tracker.LastChange.Value.AsTextChange().Item);
        }


    }
}
