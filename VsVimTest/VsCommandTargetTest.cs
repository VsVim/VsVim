using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.UnitTest;
using Vim.UnitTest.Mock;

namespace VsVim.UnitTest
{
    [TestFixture]
    public class VsCommandTargetTest
    {
        private MockRepository _factory;
        private IVimBuffer _buffer;
        private Mock<IVsAdapter> _adapter;
        private Mock<IExternalEditorManager> _externalEditorManager;
        private Mock<IOleCommandTarget> _nextTarget;
        private Mock<IDisplayWindowBroker> _broker;
        private VsCommandTarget _targetRaw;
        private IOleCommandTarget _target;

        [SetUp]
        public void SetUp()
        {
            var textView = EditorUtil.CreateTextView("");
            _buffer = EditorUtil.FactoryService.Vim.CreateBuffer(textView);
            _factory = new MockRepository(MockBehavior.Strict);

            // By default resharper isn't loaded
            _externalEditorManager = _factory.Create<IExternalEditorManager>();
            _externalEditorManager.SetupGet(x => x.IsResharperInstalled).Returns(false);

            _nextTarget = _factory.Create<IOleCommandTarget>(MockBehavior.Loose);
            _adapter = _factory.Create<IVsAdapter>();
            _adapter.Setup(x => x.InAutomationFunction).Returns(false);
            _adapter.Setup(x => x.InDebugMode).Returns(false);
            _adapter.Setup(x => x.IsIncrementalSearchActive(It.IsAny<ITextView>())).Returns(false);

            _broker = _factory.Create<IDisplayWindowBroker>(MockBehavior.Loose);

            var oldCommandFilter = _nextTarget.Object;
            var vsTextView = _factory.Create<IVsTextView>(MockBehavior.Loose);
            vsTextView.Setup(x => x.AddCommandFilter(It.IsAny<IOleCommandTarget>(), out oldCommandFilter)).Returns(0);
            var result = VsCommandTarget.Create(
                _buffer,
                vsTextView.Object,
                _adapter.Object,
                _broker.Object,
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
            else if (keyInput.Key == VimKey.Back)
            {
                return Tuple.Create(VSConstants.VSStd2K, (uint)VSConstants.VSStd2KCmdID.BACKSPACE, IntPtr.Zero);
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

        private bool RunQueryStatus(KeyInput keyInput)
        {
            var data = ToVsInforamtion(keyInput);
            var guid = data.Item1;
            var cmds = new OLECMD[1];
            cmds[0] = new OLECMD { cmdID = data.Item2 };
            return
                ErrorHandler.Succeeded(_target.QueryStatus(ref guid, 1, cmds, data.Item3)) &&
                cmds[0].cmdf == (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
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
        public void TryConvert_Tab()
        {
            AssertCanConvert2K(VSConstants.VSStd2KCmdID.TAB, KeyInputUtil.TabKey);
        }

        [Test]
        public void TryConvert_InAutomationShouldFail()
        {
            _adapter.Setup(x => x.InAutomationFunction).Returns(true);
            AssertCannotConvert2K(VSConstants.VSStd2KCmdID.TAB);
        }

        [Test]
        public void TryConvert_InIncrementalSearchShouldFail()
        {
            _adapter.Setup(x => x.IsIncrementalSearchActive(It.IsAny<ITextView>())).Returns(true);
            AssertCannotConvert2K(VSConstants.VSStd2KCmdID.TAB);
        }

        [Test]
        public void QueryStatus_IgnoreEscapeIfCantProcess()
        {
            _buffer.SwitchMode(ModeKind.Disabled, ModeArgument.None);
            Assert.IsFalse(_buffer.CanProcess(KeyInputUtil.EscapeKey));
            _nextTarget.SetupQueryStatus().Verifiable();
            RunQueryStatus(KeyInputUtil.EscapeKey);
            _factory.Verify();
        }

        [Test]
        public void QueryStatus_EnableEscapeButDontHandleNormally()
        {
            _buffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            Assert.IsTrue(_buffer.CanProcess(VimKey.Escape));
            Assert.IsTrue(RunQueryStatus(KeyInputUtil.EscapeKey));
        }

        /// <summary>
        /// Don't actually run the Escape in the QueryStatus command if we're in visual mode
        /// </summary>
        [Test]
        public void QueryStatus_EnableEscapeAndDontHandleInResharperPlusVisualMode()
        {
            var count = 0;
            _buffer.KeyInputProcessed += delegate { count++; };
            _buffer.SwitchMode(ModeKind.VisualCharacter, ModeArgument.None);
            _externalEditorManager.SetupGet(x => x.IsResharperInstalled).Returns(true).Verifiable();
            RunQueryStatus(KeyInputUtil.EscapeKey);
            Assert.AreEqual(0, count);
            _factory.Verify();
        }

        /// <summary>
        /// Make sure we process Escape during QueryStatus if we're in insert mode.  R# will
        /// intercept escape and never give it to us and we'll think we're still in insert
        /// </summary>
        [Test]
        public void QueryStatus_EnableAndHandleEscapeInResharperPlusInsert()
        {
            var count = 0;
            _buffer.KeyInputProcessed += delegate { count++; };
            _buffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            _externalEditorManager.SetupGet(x => x.IsResharperInstalled).Returns(true).Verifiable();
            Assert.IsTrue(RunQueryStatus(KeyInputUtil.EscapeKey));
            Assert.IsTrue(_targetRaw.SwallowIfNextExecMatches.IsSome());
            Assert.AreEqual(KeyInputUtil.EscapeKey, _targetRaw.SwallowIfNextExecMatches.Value);
            Assert.AreEqual(1, count);
            _factory.Verify();
        }

        /// <summary>
        /// Make sure we process Escape during QueryStatus if we're in insert mode.  R# will
        /// intercept escape and never give it to us and we'll think we're still in insert
        /// </summary>
        [Test]
        public void QueryStatus_EnableAndHandleEscapeInResharperPlusExternalEdit()
        {
            var count = 0;
            _buffer.KeyInputProcessed += delegate { count++; };
            _buffer.SwitchMode(ModeKind.ExternalEdit, ModeArgument.None);
            _externalEditorManager.SetupGet(x => x.IsResharperInstalled).Returns(true).Verifiable();
            Assert.IsTrue(RunQueryStatus(KeyInputUtil.EscapeKey));
            Assert.IsTrue(_targetRaw.SwallowIfNextExecMatches.IsSome());
            Assert.AreEqual(KeyInputUtil.EscapeKey, _targetRaw.SwallowIfNextExecMatches.Value);
            Assert.AreEqual(1, count);
            _factory.Verify();
        }

        /// <summary>
        /// The Backspace key isn't special so don't special case it in R#
        /// </summary>
        [Test]
        public void QueryStatus_HandleBackspaceNormallyInResharperMode()
        {
            var count = 0;
            _buffer.KeyInputProcessed += delegate { count++; };
            _buffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            _externalEditorManager.SetupGet(x => x.IsResharperInstalled).Returns(true).Verifiable();
            Assert.IsTrue(RunQueryStatus(KeyInputUtil.VimKeyToKeyInput(VimKey.Back)));
            Assert.AreEqual(0, count);
            _factory.Verify();
        }

        [Test]
        public void Exec_PassOnIfCantHandle()
        {
            _buffer.SwitchMode(ModeKind.Disabled, ModeArgument.None);
            Assert.IsFalse(_buffer.CanProcess(VimKey.Enter));
            _nextTarget.SetupExec().Verifiable();
            RunExec(KeyInputUtil.EnterKey);
            _factory.Verify();
        }

        [Test]
        public void Exec_SwallowShouldNotPassOnTheCommandIfMatches()
        {
            _targetRaw.SwallowIfNextExecMatches = FSharpOption.Create(KeyInputUtil.EscapeKey);
            RunExec(KeyInputUtil.EscapeKey);
            Assert.IsTrue(_targetRaw.SwallowIfNextExecMatches.IsNone());
            _factory.Verify();
        }

        [Test]
        public void Exec_HandleEscapeNormally()
        {
            var count = 0;
            _buffer.KeyInputProcessed += delegate { count++; };
            _buffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            RunExec(KeyInputUtil.EscapeKey);
            Assert.AreEqual(1, count);
        }

        /// <summary>
        /// Make sure that KeyInput is simulated for any KeyInput which is intercepted
        /// </summary>
        [Test]
        public void Exec_SimulateInterceptedInput()
        {
            var count = 0;
            _buffer.KeyInputProcessed += delegate { count++; };
            _buffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            _nextTarget.SetupExec().Verifiable();
            RunExec(KeyInputUtil.EnterKey);
            Assert.AreEqual(1, count);
            _factory.Verify();
        }
    }
}

