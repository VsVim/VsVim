using System;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.UnitTest.Mock;

namespace VsVim.UnitTest
{
    [TestFixture]
    public class VsCommandTargetTest
    {
        private MockRepository _factory;
        private Mock<IVimBuffer> _buffer;
        private Mock<System.IServiceProvider> _serviceProvider;
        private Mock<IVsExtensibility> _vsExt;
        private Mock<IExternalEditorManager> _externalEditorManager;
        private Mock<IOleCommandTarget> _nextTarget;
        private VsCommandTarget _targetRaw;
        private IOleCommandTarget _target;

        [SetUp]
        public void SetUp()
        {
            _factory = new MockRepository(MockBehavior.Strict);
            _buffer = _factory.Create<IVimBuffer>(MockBehavior.Loose);

            // By default don't be an in automation function
            _vsExt = _factory.Create<IVsExtensibility>();
            _vsExt.Setup(x => x.IsInAutomationFunction()).Returns(0);

            // By default resharper isn't loaded
            _externalEditorManager = _factory.Create<IExternalEditorManager>();
            _externalEditorManager.SetupGet(x => x.IsResharperLoaded).Returns(false);

            _nextTarget = _factory.Create<IOleCommandTarget>(MockBehavior.Loose);

            _serviceProvider = MockObjectFactory.CreateServiceProvider(Tuple.Create(typeof(IVsExtensibility), (object)_vsExt.Object));

            var oldCommandFilter = _nextTarget.Object;
            var vsTextView = _factory.Create<IVsTextView>(MockBehavior.Loose);
            vsTextView.Setup(x => x.AddCommandFilter(It.IsAny<IOleCommandTarget>(), out oldCommandFilter)).Returns(0);
            var result = VsCommandTarget.Create(
                _buffer.Object,
                vsTextView.Object,
                _serviceProvider.Object,
                _externalEditorManager.Object);
            Assert.IsTrue(result.IsValue);
            _targetRaw = result.Value;
            _target = _targetRaw;
        }

        private static Tuple<Guid, uint, IntPtr> ToVsInforamtion(VimKey key)
        {
            switch (key)
            {
                case VimKey.Escape:
                    return Tuple.Create(VSConstants.VSStd2K, (uint)VSConstants.VSStd2KCmdID.CANCEL, IntPtr.Zero);
                case VimKey.Enter:
                    return Tuple.Create(VSConstants.VSStd2K, (uint)VSConstants.VSStd2KCmdID.RETURN, IntPtr.Zero);
                default:
                    Assert.Fail("Not a supported key");
                    return null;
            }
        }

        private void RunExec(VimKey key)
        {
            var data = ToVsInforamtion(key);
            var guid = data.Item1;
            _target.Exec(ref guid, data.Item2, 0, data.Item3, IntPtr.Zero);
        }

        private void RunQueryStatus(VimKey key)
        {
            var data = ToVsInforamtion(key);
            var guid = data.Item1;
            var cmds = new OLECMD[1];
            cmds[0] = new OLECMD { cmdID = data.Item2 };
            _target.QueryStatus(ref guid, 1, cmds, data.Item3);
        }

        private void AssertCannotConvert2K(VSConstants.VSStd2KCmdID id)
        {
            KeyInput ki;
            Assert.IsFalse(_targetRaw.TryConvert(VSConstants.VSStd2K, (uint)id, IntPtr.Zero, out ki));
        }

        private void AssertCanConvert2K(VSConstants.VSStd2KCmdID id, KeyInput expected)
        {
            KeyInput ki;
            Assert.IsTrue(_targetRaw.TryConvert(VSConstants.VSStd2K, (uint)id, IntPtr.Zero, out ki));
            Assert.AreEqual(expected, ki);
        }

        [Test]
        public void TryConvert1()
        {
            _buffer.Setup(x => x.CanProcess(It.IsAny<KeyInput>())).Returns(true);
            AssertCanConvert2K(VSConstants.VSStd2KCmdID.TAB, KeyInputUtil.VimKeyToKeyInput(VimKey.Tab));
        }

        [Test, Description("Don't convert keys when in automation")]
        public void TryConvert2()
        {
            _vsExt.Setup(x => x.IsInAutomationFunction()).Returns(1);
            _buffer.Setup(x => x.CanProcess(It.IsAny<KeyInput>())).Returns(true);
            AssertCannotConvert2K(VSConstants.VSStd2KCmdID.TAB);
        }

        [Test]
        public void QueryStatus_IgnoreEscapeIfCantProcess()
        {
            _buffer.Setup(x => x.CanProcess(KeyInputUtil.VimKeyToKeyInput(VimKey.Escape))).Returns(false);
            _nextTarget.SetupQueryStatus().Verifiable();
            RunQueryStatus(VimKey.Escape);
            _factory.Verify();
        }

        [Test]
        public void QueryStatus_EnableEscapeButDontHandleNormally()
        {
            _buffer.Setup(x => x.CanProcess(KeyInputUtil.VimKeyToKeyInput(VimKey.Escape))).Returns(true);
            RunQueryStatus(VimKey.Escape);
            _factory.Verify();
        }

        [Test]
        public void QueryStatus_EnableEscapeAndDontHandleNormally()
        {
            _buffer.Setup(x => x.CanProcess(KeyInputUtil.VimKeyToKeyInput(VimKey.Escape))).Returns(true);
            RunQueryStatus(VimKey.Escape);
            _factory.Verify();
        }

        [Test]
        public void QueryStatus_EnableEscapeAndDontHandleInResharperPlusNormalMode()
        {
            _buffer.Setup(x => x.CanProcess(KeyInputUtil.VimKeyToKeyInput(VimKey.Escape))).Returns(true);
            _buffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Normal).Verifiable();
            _externalEditorManager.SetupGet(x => x.IsResharperLoaded).Returns(true).Verifiable();
            RunQueryStatus(VimKey.Escape);
            _factory.Verify();
        }

        [Test]
        public void QueryStatus_EnableAndHandleEscapeInResharperPlusInsert()
        {
            var ki = KeyInputUtil.VimKeyToKeyInput(VimKey.Escape);
            _buffer.Setup(x => x.CanProcess(ki)).Returns(true).Verifiable();
            _buffer.Setup(x => x.Process(ki)).Returns(true).Verifiable();
            _buffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Insert).Verifiable();
            _externalEditorManager.SetupGet(x => x.IsResharperLoaded).Returns(true).Verifiable();
            RunQueryStatus(VimKey.Escape);
            Assert.IsTrue(_targetRaw.IgnoreNextExecIfEscape);
            _factory.Verify();
        }

        [Test]
        public void QueryStatus_EnableAndHandleEscapeInResharperPlusExternalEdit()
        {
            var ki = KeyInputUtil.VimKeyToKeyInput(VimKey.Escape);
            _buffer.Setup(x => x.CanProcess(ki)).Returns(true).Verifiable();
            _buffer.Setup(x => x.Process(ki)).Returns(true).Verifiable();
            _buffer.SetupGet(x => x.ModeKind).Returns(ModeKind.ExternalEdit).Verifiable();
            _externalEditorManager.SetupGet(x => x.IsResharperLoaded).Returns(true).Verifiable();
            RunQueryStatus(VimKey.Escape);
            Assert.IsTrue(_targetRaw.IgnoreNextExecIfEscape);
            _factory.Verify();
        }

        [Test]
        public void QueryStatus_HandleEnterNormallyInResharperMode()
        {
            var ki = KeyInputUtil.VimKeyToKeyInput(VimKey.Enter);
            _buffer.Setup(x => x.CanProcess(ki)).Returns(true).Verifiable();
            _externalEditorManager.SetupGet(x => x.IsResharperLoaded).Returns(true).Verifiable();
            RunQueryStatus(VimKey.Enter);
            Assert.IsFalse(_targetRaw.IgnoreNextExecIfEscape);
            _factory.Verify();
        }

        [Test]
        public void Exec_PassOnIfCantHandle()
        {
            var ki = KeyInputUtil.VimKeyToKeyInput(VimKey.Escape);
            _buffer.Setup(x => x.CanProcess(ki)).Returns(false).Verifiable();
            _nextTarget.SetupExec().Verifiable();
            RunExec(VimKey.Escape);
            _factory.Verify();
        }

        [Test]
        public void Exec_IgnoreIfEscapeAndSetToIgnore()
        {
            var ki = KeyInputUtil.VimKeyToKeyInput(VimKey.Escape);
            _buffer.Setup(x => x.CanProcess(ki)).Returns(true).Verifiable();
            _nextTarget.SetupExec().Verifiable();
            _targetRaw.IgnoreNextExecIfEscape = true;
            RunExec(VimKey.Escape);
            Assert.IsFalse(_targetRaw.IgnoreNextExecIfEscape);
            _factory.Verify();
        }

        [Test]
        public void Exec_HandleEscapeNormally()
        {
            var ki = KeyInputUtil.VimKeyToKeyInput(VimKey.Escape);
            _buffer.Setup(x => x.CanProcess(ki)).Returns(true).Verifiable();
            _buffer.Setup(x => x.Process(ki)).Returns(true).Verifiable();
            RunExec(VimKey.Escape);
            _factory.Verify();
        }
    }
}
