using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Xunit;
using Vim.UnitTest.Mock;
using System;

namespace Vim.UnitTest
{
    public abstract class VimBufferFactoryIntegrationTest : VimTestBase
    {
        private readonly IVim _vim;
        private readonly IVimBufferFactory _vimBufferFactory;
        private readonly MockRepository _factory;

        protected VimBufferFactoryIntegrationTest()
        {
            _vimBufferFactory = VimBufferFactory;
            _vim = Vim;
            _factory = new MockRepository(MockBehavior.Strict);
        }

        public sealed class CreateVimBufferTest : VimBufferFactoryIntegrationTest
        {
            /// <summary>
            /// Ensure that CreateVimBuffer actually creates an IVimBuffer instance
            /// </summary>
            [Fact]
            public void Simple()
            {
                var textView = CreateTextView("");
                var vimTextBuffer = _vimBufferFactory.CreateVimTextBuffer(textView.TextBuffer, _vim);
                var buffer = _vimBufferFactory.CreateVimBuffer(textView, vimTextBuffer);
                Assert.NotNull(buffer);
                Assert.Equal(ModeKind.Normal, buffer.ModeKind);
                Assert.Same(vimTextBuffer, buffer.VimTextBuffer);
            }

            /// <summary>
            /// The IVimBufferFactory should be stateless and happily create multiple IVimBuffer instances for a 
            /// given ITextView (even though at an application level that will be illegal)
            /// </summary>
            [Fact]
            public void Stateless()
            {
                var textView = CreateTextView("");
                var vimTextBuffer = _vimBufferFactory.CreateVimTextBuffer(textView.TextBuffer, _vim);
                var buffer1 = _vimBufferFactory.CreateVimBuffer(textView, vimTextBuffer);
                var buffer2 = _vimBufferFactory.CreateVimBuffer(textView, vimTextBuffer);
                Assert.NotSame(buffer1, buffer2);
            }


            /// <summary>
            /// Create the IVimBuffer for an uninitialized ITextView instance.  This should create an 
            /// IVimBuffer in the uninitialized state 
            /// </summary>
            [Fact]
            public void UninitializedTextView()
            {
                var textBuffer = CreateTextBuffer("");
                var textView = MockObjectFactory.CreateTextView(textBuffer, factory: _factory);
                textView.SetupGet(x => x.TextViewLines).Returns((ITextViewLineCollection)null);
                textView.SetupGet(x => x.InLayout).Returns(false);
                textView.SetupGet(x => x.IsClosed).Returns(false);
                var vimTextBuffer = _vimBufferFactory.CreateVimTextBuffer(textBuffer, _vim);
                var vimBuffer = _vimBufferFactory.CreateVimBuffer(textView.Object, vimTextBuffer);
                Assert.Equal(ModeKind.Uninitialized, vimBuffer.ModeKind);
            }

            /// <summary>
            /// Once an ITextView state is initialized the IVimBuffer should move to the appropriate state
            /// </summary>
            [Fact]
            public void TextViewDelayInitialize()
            {
                var textBuffer = CreateTextBuffer("");
                var textView = MockObjectFactory.CreateTextView(textBuffer, factory: _factory);
                textView.SetupGet(x => x.TextViewLines).Returns((ITextViewLineCollection)null);
                textView.SetupGet(x => x.InLayout).Returns(false);
                textView.SetupGet(x => x.Caret.Position).Returns(new CaretPosition());
                textView.SetupGet(x => x.IsClosed).Returns(false);
                var vimTextBuffer = _vimBufferFactory.CreateVimTextBuffer(textBuffer, _vim);
                var vimBuffer = _vimBufferFactory.CreateVimBuffer(textView.Object, vimTextBuffer);
                Assert.Equal(ModeKind.Uninitialized, vimBuffer.ModeKind);

                textView.SetupGet(x => x.TextViewLines).Returns(new Mock<ITextViewLineCollection>().Object);
                textView.Raise(x => x.LayoutChanged += null, (TextViewLayoutChangedEventArgs)null);

                Assert.Equal(ModeKind.Normal, vimBuffer.ModeKind);
            }

            /// <summary>
            /// If another component forces a layout in the middle of a layout event we need to
            /// further delay the creation of the IVimBuffer until the next layout event.  
            /// 
            /// This is the scenario which produced the stack trace in issue 1376
            /// </summary>
            [Fact]
            public void TextViewInLayoutInsideLayoutEvent()
            {
                var textBuffer = CreateTextBuffer("");
                var vimTextBuffer = _vimBufferFactory.CreateVimTextBuffer(textBuffer, _vim);

                var textView = MockObjectFactory.CreateTextView(textBuffer, factory: _factory);
                textView.SetupGet(x => x.TextViewLines).Returns((ITextViewLineCollection)null);
                textView.SetupGet(x => x.InLayout).Returns(false);
                textView.SetupGet(x => x.IsClosed).Returns(false);

                var vimBuffer = _vimBufferFactory.CreateVimBuffer(textView.Object, vimTextBuffer);
                Assert.Equal(ModeKind.Uninitialized, vimBuffer.ModeKind);

                // Still can't initialize here because inside the event we are in another layout
                // which prevents a mode change
                textView.SetupGet(x => x.TextViewLines).Throws(new Exception());
                textView.SetupGet(x => x.InLayout).Returns(true);
                textView.Raise(x => x.LayoutChanged += null, (TextViewLayoutChangedEventArgs)null);
                Assert.Equal(ModeKind.Uninitialized, vimBuffer.ModeKind);

                textView.SetupGet(x => x.Caret.Position).Returns(new CaretPosition());
                textView.SetupGet(x => x.TextViewLines).Returns(_factory.Create<ITextViewLineCollection>().Object);
                textView.SetupGet(x => x.InLayout).Returns(false);
                textView.Raise(x => x.LayoutChanged += null, (TextViewLayoutChangedEventArgs)null);
                Assert.Equal(ModeKind.Normal, vimBuffer.ModeKind);
            }

            [Fact]
            public void TextViewClosedInsideLayoutEvent()
            {
                var textBuffer = CreateTextBuffer("");
                var vimTextBuffer = _vimBufferFactory.CreateVimTextBuffer(textBuffer, _vim);

                var textView = MockObjectFactory.CreateTextView(textBuffer, factory: _factory);
                textView.SetupGet(x => x.TextViewLines).Returns((ITextViewLineCollection)null);
                textView.SetupGet(x => x.InLayout).Returns(false);
                textView.SetupGet(x => x.IsClosed).Returns(false);

                var vimBuffer = _vimBufferFactory.CreateVimBuffer(textView.Object, vimTextBuffer);
                Assert.Equal(ModeKind.Uninitialized, vimBuffer.ModeKind);

                textView.SetupGet(x => x.TextViewLines).Throws(new Exception());
                textView.SetupGet(x => x.InLayout).Throws(new Exception());
                textView.SetupGet(x => x.IsClosed).Returns(true);
                textView.Raise(x => x.LayoutChanged += null, (TextViewLayoutChangedEventArgs)null);
                Assert.Equal(ModeKind.Uninitialized, vimBuffer.ModeKind);
            }
        }
    }
}
