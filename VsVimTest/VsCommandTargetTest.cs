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
        private Mock<IVsAdapter> _adapter;
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
            _adapter = _factory.Create<IVsAdapter>();

            var oldCommandFilter = _nextTarget.Object;
            var vsTextView = _factory.Create<IVsTextView>(MockBehavior.Loose);
            vsTextView.Setup(x => x.AddCommandFilter(It.IsAny<IOleCommandTarget>(), out oldCommandFilter)).Returns(0);
            var result = VsCommandTarget.Create(
                _buffer.Object,
                vsTextView.Object,
                _adapter.Object,
                _externalEditorManager.Object);
            Assert.IsTrue(result.IsSuccess);
            _targetRaw = result.Value;
            _target = _targetRaw;
        }

        private static Tuple<Guid, uint, IntPtr> ToVsInforamtion(KeyInput keyInput)
        {
            if (keyInput == KeyInputUtil.EscapeKey)
            {
                return Tuple.Create(VSConstants.VSStd2K, (uint)VSConstants.VSStd2KCmdID.CANCEL, IntPtr.Zero);
            }
            else if (keyInput == KeyInputUtil.EnterKey)
            {
                return Tuple.Create(VSConstants.VSStd2K, (uint)VSConstants.VSStd2KCmdID.RETURN, IntPtr.Zero);
            }
            else
            {
                Assert.Fail("Not a supported key");
                return null;
            }
        }

        private void RunExec(KeyInput keyInput)
        {
            var data = ToVsInforamtion(keyInput);
            var guid = data.Item1;
            _target.Exec(ref guid, data.Item2, 0, data.Item3, IntPtr.Zero);
        }

        private void RunQueryStatus(KeyInput keyInput)
        {
            var data = ToVsInforamtion(keyInput);
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
            AssertCanConvert2K(VSConstants.VSStd2KCmdID.TAB, KeyInputUtil.TabKey);
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
            _buffer.Setup(x => x.CanProcess(KeyInputUtil.EscapeKey)).Returns(false);
            _nextTarget.SetupQueryStatus().Verifiable();
            RunQueryStatus(KeyInputUtil.EscapeKey);
            _factory.Verify();
        }

        [Test]
        public void QueryStatus_EnableEscapeButDontHandleNormally()
        {
            _buffer.Setup(x => x.CanProcess(KeyInputUtil.EscapeKey)).Returns(true);
            RunQueryStatus(KeyInputUtil.EscapeKey);
            _factory.Verify();
        }

        [Test]
        public void QueryStatus_EnableEscapeAndDontHandleNormally()
        {
            _buffer.Setup(x => x.CanProcess(KeyInputUtil.EscapeKey)).Returns(true);
            RunQueryStatus(KeyInputUtil.EscapeKey);
            _factory.Verify();
        }

        [Test]
        public void QueryStatus_EnableEscapeAndDontHandleInResharperPlusNormalMode()
        {
            _buffer.Setup(x => x.CanProcess(KeyInputUtil.EscapeKey)).Returns(true);
            _buffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Normal).Verifiable();
            _externalEditorManager.SetupGet(x => x.IsResharperLoaded).Returns(true).Verifiable();
            RunQueryStatus(KeyInputUtil.EscapeKey);
            _factory.Verify();
        }

        [Test]
        public void QueryStatus_EnableAndHandleEscapeInResharperPlusInsert()
        {
            var ki = KeyInputUtil.EscapeKey;
            _buffer.Setup(x => x.CanProcess(ki)).Returns(true).Verifiable();
            _buffer.Setup(x => x.Process(ki)).Returns(true).Verifiable();
            _buffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Insert).Verifiable();
            _externalEditorManager.SetupGet(x => x.IsResharperLoaded).Returns(true).Verifiable();
            RunQueryStatus(KeyInputUtil.EscapeKey);
            Assert.IsTrue(_targetRaw.IgnoreIfNextExecMatches.IsSome);
            Assert.AreEqual(KeyInputUtil.EscapeKey, _targetRaw.IgnoreIfNextExecMatches.Value);
            _factory.Verify();
        }

        [Test]
        public void QueryStatus_EnableAndHandleEscapeInResharperPlusExternalEdit()
        {
            var ki = KeyInputUtil.EscapeKey;
            _buffer.Setup(x => x.CanProcess(ki)).Returns(true).Verifiable();
            _buffer.Setup(x => x.Process(ki)).Returns(true).Verifiable();
            _buffer.SetupGet(x => x.ModeKind).Returns(ModeKind.ExternalEdit).Verifiable();
            _externalEditorManager.SetupGet(x => x.IsResharperLoaded).Returns(true).Verifiable();
            RunQueryStatus(KeyInputUtil.EscapeKey);
            Assert.IsTrue(_targetRaw.IgnoreIfNextExecMatches.IsSome);
            Assert.AreEqual(KeyInputUtil.EscapeKey, _targetRaw.IgnoreIfNextExecMatches.Value);
            _factory.Verify();
        }

        [Test]
        public void QueryStatus_HandleEnterNormallyInResharperMode()
        {
            var ki = KeyInputUtil.EnterKey;
            _buffer.Setup(x => x.CanProcess(ki)).Returns(true).Verifiable();
            _externalEditorManager.SetupGet(x => x.IsResharperLoaded).Returns(true).Verifiable();
            RunQueryStatus(ki);
            Assert.IsTrue(_targetRaw.IgnoreIfNextExecMatches.IsNone);
            _factory.Verify();
        }

        [Test]
        public void Exec_PassOnIfCantHandle()
        {
            var ki = KeyInputUtil.EscapeKey;
            _buffer.Setup(x => x.CanProcess(ki)).Returns(false).Verifiable();
            _nextTarget.SetupExec().Verifiable();
            RunExec(KeyInputUtil.EscapeKey);
            _factory.Verify();
        }

        [Test]
        public void Exec_IgnoreIfEscapeAndSetToIgnore()
        {
            var ki = KeyInputUtil.EscapeKey;
            _buffer.Setup(x => x.CanProcess(ki)).Returns(true).Verifiable();
            _nextTarget.SetupExec().Verifiable();
            _targetRaw.IgnoreIfNextExecMatches = Option.CreateValue(KeyInputUtil.EscapeKey);
            RunExec(KeyInputUtil.EscapeKey);
            Assert.IsTrue(_targetRaw.IgnoreIfNextExecMatches.IsNone);
            _factory.Verify();
        }

        [Test]
        public void Exec_HandleEscapeNormally()
        {
            var ki = KeyInputUtil.EscapeKey;
            _buffer.Setup(x => x.CanProcess(ki)).Returns(true).Verifiable();
            _buffer.Setup(x => x.Process(ki)).Returns(true).Verifiable();
            RunExec(KeyInputUtil.EscapeKey);
            _factory.Verify();
        }
    }
}
