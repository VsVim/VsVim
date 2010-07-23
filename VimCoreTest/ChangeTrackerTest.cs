using System;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using VimCore.Test.Mock;

namespace VimCore.Test
{
    [TestFixture]
    public class ChangeTrackerTest
    {
        private MockFactory _factory;
        private ChangeTracker _trackerRaw;
        private IChangeTracker _tracker;
        private ITextBuffer _textBuffer;
        private Mock<ITextView> _textView;
        private Mock<IMouseDevice> _mouseDevice;
        private MockVimBuffer _buffer;

        private void CreateForText(params string[] lines)
        {
            _textBuffer = Utils.EditorUtil.CreateBuffer(lines);
            _textView = Mock.MockObjectFactory.CreateTextView(_textBuffer);
            _textView.SetupGet(x => x.HasAggregateFocus).Returns(true);
            _mouseDevice = new Mock<IMouseDevice>(MockBehavior.Strict);
            _buffer = new MockVimBuffer();
            _buffer.TextViewImpl = _textView.Object;
            _buffer.TextBufferImpl = _textBuffer;

            _factory = new MockFactory(MockBehavior.Loose);
            _factory.DefaultValue = DefaultValue.Mock;
            _buffer.NormalModeImpl = _factory.Create<INormalMode>().Object;
            _buffer.VisualBlockModeImpl = _factory.Create<IVisualMode>().Object;
            _buffer.VisualCharacterModeImpl = _factory.Create<IVisualMode>().Object;
            _buffer.VisualLineModeImpl = _factory.Create<IVisualMode>().Object;
            _trackerRaw = new ChangeTracker(_mouseDevice.Object);
            _tracker = _trackerRaw;
            _trackerRaw.OnVimBufferCreated(_buffer);
        }

        [Test]
        public void BufferChange1()
        {
            CreateForText("foo bar");
            _textBuffer.Insert(0, "f");
            Assert.IsTrue(_tracker.LastChange.IsSome());
            Assert.AreEqual("f", _tracker.LastChange.Value.AsTextChange().Item);
        }

        [Test]
        public void BufferChange2()
        {
            CreateForText("foo bar");
            _textBuffer.Insert(0, "f");
            _textBuffer.Insert(1, "o");
            Assert.IsTrue(_tracker.LastChange.IsSome());
            Assert.AreEqual("fo", _tracker.LastChange.Value.AsTextChange().Item);
        }

        [Test]
        public void BufferChange3()
        {
            CreateForText("foo bar");
            _textBuffer.Insert(0, "f");
            _textBuffer.Insert(1, "o");
            _textBuffer.Insert(2, "o");
            Assert.IsTrue(_tracker.LastChange.IsSome());
            Assert.AreEqual("foo", _tracker.LastChange.Value.AsTextChange().Item);
        }

        [Test, Description("As soon as the change is non-contiguous it should reset")]
        public void BufferChange4()
        {
            CreateForText("foo bar");
            _textBuffer.Insert(0, "f");
            _textBuffer.Insert(1, "o");
            _textBuffer.Insert(0, "b");
            Assert.IsTrue(_tracker.LastChange.IsSome());
            Assert.AreEqual("b", _tracker.LastChange.Value.AsTextChange().Item);
        }

        [Test, Description("As soon as the change is non-contiguous it should reset")]
        public void BufferChange5()
        {
            CreateForText("foo bar");
            _textBuffer.Insert(0, "f");
            _textBuffer.Insert(1, "o");
            _textBuffer.Insert(1, "b");
            Assert.IsTrue(_tracker.LastChange.IsSome());
            Assert.AreEqual("b", _tracker.LastChange.Value.AsTextChange().Item);
        }

        [Test]
        public void BufferChange6()
        {
            CreateForText("foo bar");
            _textBuffer.Insert(0, "f");
            _textBuffer.Insert(1, Environment.NewLine);
            Assert.IsTrue(_tracker.LastChange.IsSome());
            Assert.AreEqual("f" + Environment.NewLine, _tracker.LastChange.Value.AsTextChange().Item);
        }

        [Test, Description("More than 1 character is fine for a change")]
        public void BufferChange7()
        {
            CreateForText("foo bar");
            _textBuffer.Insert(0, "hey");
            Assert.IsTrue(_tracker.LastChange.IsSome());
            Assert.AreEqual("hey", _tracker.LastChange.Value.AsTextChange().Item);
        }

        [Test, Description("Change should only count if we have aggregate focus")]
        public void BufferChange8()
        {
            CreateForText("foo bar");
            _textView.SetupGet(x => x.HasAggregateFocus).Returns(false);
            _textBuffer.Insert(0, "hey");
            Assert.IsFalse(_tracker.LastChange.IsSome());
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
            Assert.IsFalse(_tracker.LastChange.IsSome());
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
            Assert.IsFalse(_tracker.LastChange.IsSome());
        }

        [Test, Description("Don't process edits while we are processing KeyInput")]
        public void BufferChange11()
        {
            CreateForText("foo bar");
            _buffer.IsProcessingInputImpl = true;
            _textBuffer.Insert(0, "again");
            Assert.IsFalse(_tracker.LastChange.IsSome());
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
