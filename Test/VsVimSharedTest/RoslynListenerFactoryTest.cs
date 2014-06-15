using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Shell;
using Moq;
using Vim;
using Vim.UnitTest;
using VsVim.Implementation.Roslyn;
using Xunit;

namespace VsVim.Shared.UnitTest
{
    public abstract class RoslynListenerFactoryTest : VimTestBase
    {
        private readonly MockRepository _factory;
        private readonly IVimBuffer _vimBuffer;
        private readonly RoslynListenerFactory _roslynListenerFactory;

        protected RoslynListenerFactoryTest()
        {
            _factory = new MockRepository(MockBehavior.Strict);
            _roslynListenerFactory = new RoslynListenerFactory(_factory.Create<SVsServiceProvider>().Object);
            _vimBuffer = CreateVimBufferWithContentType(Constants.CSharpContentType);
        }

        private IVimBuffer CreateVimBufferWithContentType(string contentTypeName)
        {
            var contentType = GetOrCreateContentType(contentTypeName, "code");
            var textView = CreateTextView(contentType);
            var vimBufferData = CreateVimBufferData(textView);
            var vimBuffer =  CreateVimBuffer(vimBufferData);
            _roslynListenerFactory.OnVimBufferCreated(vimBuffer);
            return vimBuffer;
        }

        public sealed class RenameTest : RoslynListenerFactoryTest
        {
            private readonly Mock<IRoslynRenameUtil> _renameUtil;

            public RenameTest()
            {
                _renameUtil = _factory.Create<IRoslynRenameUtil>();
                _renameUtil.SetupGet(x => x.IsRenameActive).Returns(false);
                _roslynListenerFactory.RenameUtil = _renameUtil.Object;
            }

            [Fact]
            public void RaiseNoRename()
            {
                _renameUtil.Raise(x => x.IsRenameActiveChanged += null, EventArgs.Empty);
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
            }

            [Fact]
            public void RaiseWithRename()
            {
                _renameUtil.SetupGet(x => x.IsRenameActive).Returns(true);
                _renameUtil.Raise(x => x.IsRenameActiveChanged += null, EventArgs.Empty);
                Assert.Equal(ModeKind.ExternalEdit, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// The roslyn controller shouldn't be tracknig non-roslyn buffers
            /// </summary>
            [Fact]
            public void RaiseWithRenameOnNonRoslynBuffer()
            {
                var textVimBuffer = CreateVimBufferWithContentType("text");
                _renameUtil.SetupGet(x => x.IsRenameActive).Returns(true);
                _renameUtil.Raise(x => x.IsRenameActiveChanged += null, EventArgs.Empty);
                Assert.Equal(ModeKind.ExternalEdit, _vimBuffer.ModeKind);
                Assert.Equal(ModeKind.Normal, textVimBuffer.ModeKind);
            }

            /// <summary>
            /// When we leave rename move all of the external edits back into the previous 
            /// mode 
            /// </summary>
            [Fact]
            public void RenameLeft()
            {
                _renameUtil.SetupGet(x => x.IsRenameActive).Returns(true);
                _renameUtil.Raise(x => x.IsRenameActiveChanged += null, EventArgs.Empty);
                _renameUtil.SetupGet(x => x.IsRenameActive).Returns(false);
                _renameUtil.Raise(x => x.IsRenameActiveChanged += null, EventArgs.Empty);
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// When leaving rename mode don't change buffers that were already out of external
            /// edit mode 
            /// </summary>
            [Fact]
            public void RenameLeftDontChangeNonExternalEdits()
            {
                _renameUtil.SetupGet(x => x.IsRenameActive).Returns(true);
                _renameUtil.Raise(x => x.IsRenameActiveChanged += null, EventArgs.Empty);
                _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                _renameUtil.SetupGet(x => x.IsRenameActive).Returns(false);
                _renameUtil.Raise(x => x.IsRenameActiveChanged += null, EventArgs.Empty);
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
            }
        }
    }
}
