using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Moq;
using Xunit;
using Vim;
using Vim.UnitTest;
using Vim.VisualStudio.Implementation.Misc;
using EditorUtils;

namespace Vim.VisualStudio.UnitTest
{
    public sealed class MindScapeTest : VimTestBase
    {
        private MindScape _mindScape;
        private IVimBufferCreationListener _creationListener;
        private MockRepository _factory;
        private Mock<SVsServiceProvider> _serviceProvider;
        private Mock<IVsShell> _vsShell;
        private Mock<ICompletionBroker> _completionBroker;

        private void Create(bool isInstalled = true)
        {
            _factory = new MockRepository(MockBehavior.Strict);

            _vsShell = _factory.Create<IVsShell>();

            var guid = MindScape.MindScapePackageGuid;
            var installed = isInstalled ? 1 : 0;
            _vsShell.Setup(x => x.IsPackageInstalled(ref guid, out installed)).Returns(0);
            _completionBroker = _factory.Create<ICompletionBroker>();
            _serviceProvider = _factory.Create<SVsServiceProvider>();
            _serviceProvider.Setup(x => x.GetService(typeof(SVsShell))).Returns(_vsShell.Object);

            _mindScape = new MindScape(_serviceProvider.Object, _completionBroker.Object);
            _creationListener = _mindScape;
        }

        private IVimBuffer CreateVimBufferForMindScape(params string[] lines)
        {
            var contentType = ContentTypeRegistryService.GetContentType("scss");
            if (contentType == null)
            {
                contentType = ContentTypeRegistryService.AddContentType("scss", new[] { "text " });
            }

            var textBuffer = CreateTextBuffer(contentType, lines);
            var textView = TextEditorFactoryService.CreateTextView(textBuffer);
            return Vim.CreateVimBuffer(textView);
        }

        /// <summary>
        /// If it's not installed then don't listen to any key strokes
        /// </summary>
        [Fact]
        public void NotInstalled()
        {
            Create(isInstalled: false);
            var vimBuffer = CreateVimBufferForMindScape("");
            _creationListener.VimBufferCreated(vimBuffer);
            vimBuffer.Process('c');
            _completionBroker.Verify();
        }

        /// <summary>
        /// Do nothing if there is no completion active
        /// </summary>
        [Fact]
        public void IgnoreWhenNoCompletionActive()
        {
            Create(isInstalled: true);
            var vimBuffer = CreateVimBufferForMindScape("hello world");
            _creationListener.VimBufferCreated(vimBuffer);
            _completionBroker.Setup(x => x.IsCompletionActive(vimBuffer.TextView)).Returns(false).Verifiable();
            vimBuffer.Process('l');
            _completionBroker.Verify();
            Assert.Equal(1, vimBuffer.TextView.GetCaretPoint());
        }

        /// <summary>
        /// Dismiss intellisense if it shows up during normal mode
        /// </summary>
        [Fact]
        public void DismissCompletionInNormalMode()
        {
            Create(isInstalled: true);
            var vimBuffer = CreateVimBufferForMindScape("hello world");
            _creationListener.VimBufferCreated(vimBuffer);
            _completionBroker.Setup(x => x.IsCompletionActive(vimBuffer.TextView)).Returns(true).Verifiable();
            _completionBroker.Setup(x => x.DismissAllSessions(vimBuffer.TextView)).Verifiable();
            vimBuffer.Process('l');
            _completionBroker.Verify();
            Assert.Equal(1, vimBuffer.TextView.GetCaretPoint());
        }

        /// <summary>
        /// Dismiss intellisense if it pops up in the transition to insert mode
        /// </summary>
        [Fact]
        public void DismissCompletionInTransitionToInsert()
        {
            Create(isInstalled: true);
            var vimBuffer = CreateVimBufferForMindScape("hello world");
            _creationListener.VimBufferCreated(vimBuffer);
            _completionBroker.Setup(x => x.IsCompletionActive(vimBuffer.TextView)).Returns(true).Verifiable();
            _completionBroker.Setup(x => x.DismissAllSessions(vimBuffer.TextView)).Verifiable();
            vimBuffer.Process('i');
            _completionBroker.Verify();
        }

        /// <summary>
        /// Don't dismiss during insert mode
        /// </summary>
        [Fact]
        public void DontDismissInInsert()
        {
            Create(isInstalled: true);
            var vimBuffer = CreateVimBufferForMindScape("hello world");
            vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            _creationListener.VimBufferCreated(vimBuffer);
            _completionBroker.Setup(x => x.IsCompletionActive(vimBuffer.TextView)).Returns(true).Verifiable();
            vimBuffer.Process('x');
            _completionBroker.Verify();
        }
    }
}
