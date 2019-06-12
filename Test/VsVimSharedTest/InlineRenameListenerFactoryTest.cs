using System;
using Microsoft.VisualStudio.Shell;
using Moq;
using Vim.UnitTest;
using Xunit;
using Vim.VisualStudio.Implementation.InlineRename;

namespace Vim.VisualStudio.UnitTest
{
    public abstract class InlineRenameListenerFactoryTest : VimTestBase
    {
        private readonly MockRepository _factory;
        private readonly IVimBuffer _vimBuffer;
        private readonly InlineRenameListenerFactory _inlineRenameListenerFactory;

        protected InlineRenameListenerFactoryTest()
        {
            _factory = new MockRepository(MockBehavior.Strict);
            var vimApplicationSettings = _factory.Create<IVimApplicationSettings>();
            vimApplicationSettings.SetupGet(x => x.EnableExternalEditMonitoring).Returns(true);
            var serviceProvider = _factory.Create<SVsServiceProvider>();
            _inlineRenameListenerFactory = new InlineRenameListenerFactory(
                vimApplicationSettings.Object,
                serviceProvider.Object);
            _vimBuffer = CreateVimBufferWithContentType(VsVimConstants.CSharpContentType);
        }

        private IVimBuffer CreateVimBufferWithContentType(string contentTypeName)
        {
            var contentType = GetOrCreateContentType(contentTypeName, "code");
            var textView = CreateTextView(contentType);
            var vimBufferData = CreateVimBufferData(textView);
            var vimBuffer =  CreateVimBuffer(vimBufferData);
            _inlineRenameListenerFactory.OnVimBufferCreated(vimBuffer);
            return vimBuffer;
        }

        public sealed class RenameTest : InlineRenameListenerFactoryTest
        {
            private readonly Mock<IInlineRenameUtil> _renameUtil;

            public RenameTest()
            {
                _renameUtil = _factory.Create<IInlineRenameUtil>();
                _renameUtil.SetupGet(x => x.IsRenameActive).Returns(false);
                _inlineRenameListenerFactory.RenameUtil = _renameUtil.Object;
            }

            [WpfFact]
            public void RaiseNoRename()
            {
                _renameUtil.Raise(x => x.IsRenameActiveChanged += null, EventArgs.Empty);
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
            }

            [WpfFact]
            public void RaiseWithRename()
            {
                _renameUtil.SetupGet(x => x.IsRenameActive).Returns(true);
                _renameUtil.Raise(x => x.IsRenameActiveChanged += null, EventArgs.Empty);
                Assert.Equal(ModeKind.ExternalEdit, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// The inline rename controller shouldn't be tracknig non-inline
            /// rename buffers
            /// </summary>
            [WpfFact]
            public void RaiseWithRenameOnNonInlineRenameBuffer()
            {
                var textVimBuffer = CreateVimBufferWithContentType("text");
                _renameUtil.SetupGet(x => x.IsRenameActive).Returns(true);
                _renameUtil.Raise(x => x.IsRenameActiveChanged += null, EventArgs.Empty);
                Assert.Equal(ModeKind.ExternalEdit, _vimBuffer.ModeKind);
                Assert.Equal(ModeKind.Normal, textVimBuffer.ModeKind);
            }

            /// <summary>
            /// When we leave rename move all of the external edits back into
            /// the previous  mode
            /// </summary>
            [WpfFact]
            public void RenameLeft()
            {
                _renameUtil.SetupGet(x => x.IsRenameActive).Returns(true);
                _renameUtil.Raise(x => x.IsRenameActiveChanged += null, EventArgs.Empty);
                _renameUtil.SetupGet(x => x.IsRenameActive).Returns(false);
                _renameUtil.Raise(x => x.IsRenameActiveChanged += null, EventArgs.Empty);
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// When leaving rename mode don't change buffers that were already
            /// out of external edit mode
            /// </summary>
            [WpfFact]
            public void RenameLeftDontChangeNonExternalEdits()
            {
                _renameUtil.SetupGet(x => x.IsRenameActive).Returns(true);
                _renameUtil.Setup(x => x.Cancel());
                _renameUtil.Raise(x => x.IsRenameActiveChanged += null, EventArgs.Empty);
                _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                _renameUtil.SetupGet(x => x.IsRenameActive).Returns(false);
                _renameUtil.Raise(x => x.IsRenameActiveChanged += null, EventArgs.Empty);
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
            }
        }
    }
}
