using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Xunit;
using Vim.UnitTest.Mock;
using Vim.EditorHost;
using System;
using System.Windows.Threading;

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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
        }
    }
}
