using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Vim;
using Microsoft.VisualStudio.Text;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Editor;
using VimCoreTest.Utils;
using Microsoft.FSharp.Collections;
using Moq;
using Microsoft.VisualStudio.Text.Operations;

namespace VimCoreTest
{
    [TestFixture]
    public class VimBufferTests
    {
        IWpfTextView _view;
        IEditorOperations _editorOperations;
        Mock<IVim> _vim;
        Mock<IMode> _normalMode;
        Mock<IMode> _insertMode;
        Mock<IMode> _disabledMode;
        Mock<IJumpList> _jumpList;
        MockBlockCaret _blockCaret;
        VimBuffer _rawBuffer;
        IVimBuffer _buffer;
        MarkMap _markMap;

        [SetUp]
        public void Initialize()
        {
            var tuple = EditorUtil.CreateViewAndOperations("here we go");
            _view = tuple.Item1;
            _editorOperations = tuple.Item2;
            _markMap = new MarkMap();
            _vim = MockObjectFactory.CreateVim(map:_markMap);
            _blockCaret = new MockBlockCaret();
            _disabledMode = new Mock<IMode>(MockBehavior.Strict);
            _disabledMode.SetupGet(x => x.ModeKind).Returns(ModeKind.Disabled);
            _normalMode = new Mock<IMode>(MockBehavior.Strict);
            _normalMode.Setup(x => x.OnEnter());
            _normalMode.SetupGet(x => x.ModeKind).Returns(ModeKind.Normal);
            _insertMode = new Mock<IMode>(MockBehavior.Strict);
            _insertMode.SetupGet(x => x.ModeKind).Returns(ModeKind.Insert);
            _jumpList = new Mock<IJumpList>(MockBehavior.Strict);
            _rawBuffer = new VimBuffer(
                _vim.Object,
                _view,
                _editorOperations,
                _blockCaret,
                _jumpList.Object);
            _rawBuffer.AddMode(_normalMode.Object);
            _rawBuffer.AddMode(_insertMode.Object);
            _rawBuffer.AddMode(_disabledMode.Object);
            _rawBuffer.SwitchMode(ModeKind.Normal);
            _buffer = _rawBuffer;
        }

        [Test]
        public void SwitchedMode1()
        {
            var ran = false;
            _normalMode.Setup(x => x.OnLeave());
            _buffer.SwitchedMode += (s, m) => { ran = true; };
            _buffer.SwitchMode(ModeKind.Normal);
            Assert.IsTrue(ran);
        }

        [Test]
        public void SwitchPreviousMode1()
        {
            _normalMode.Setup(x => x.OnLeave()).Verifiable();
            _insertMode.Setup(x => x.OnEnter()).Verifiable();
            _buffer.SwitchMode(ModeKind.Insert);
            _normalMode.Verify();
            _insertMode.Verify();

            _insertMode.Setup(x => x.OnLeave()).Verifiable();
            var prev = _buffer.SwitchPreviousMode();
            Assert.AreSame(_normalMode.Object, prev);
            _insertMode.Verify();
        }

        [Test,Description("SwitchPreviousMode should raise the SwitchedMode event")]
        public void SwitchPreviousMode2()
        {
            _normalMode.Setup(x => x.OnLeave()).Verifiable();
            _insertMode.Setup(x => x.OnEnter()).Verifiable();
            _buffer.SwitchMode(ModeKind.Insert);
            _insertMode.Setup(x => x.OnLeave()).Verifiable();

            var ran = false;
            _buffer.SwitchedMode += (s, m) => { ran = true; };
            _buffer.SwitchPreviousMode();
            Assert.IsTrue(ran);
        }

        [Test]
        public void KeyInputProcessed1()
        {
            var ki = new KeyInput('f', Key.F);
            _normalMode.Setup(x => x.Process(ki)).Returns(ProcessResult.Processed);
            var ran = false;
            _buffer.KeyInputProcessed += (s, i) => { ran = true; };
            _buffer.ProcessInput(ki);
            Assert.IsTrue(ran);
        }

        [Test, Description("Close should call OnLeave for the active mode")]
        public void Close1()
        {
            var ran = false;
            _vim.Setup(x => x.RemoveBuffer(_view)).Returns(true).Verifiable();
            _normalMode.Setup(x => x.OnLeave()).Callback(() => { ran = true; });
            _buffer.SwitchMode(ModeKind.Normal);
            _buffer.Close();
            Assert.IsTrue(ran);
            _vim.Verify();
        }

        [Test, Description("Close should destroy the block caret")]
        public void Close2()
        {
            _normalMode.Setup(x => x.OnLeave());
            _vim.Setup(x => x.RemoveBuffer(_view)).Returns(true).Verifiable();
            _buffer.Close();
            Assert.AreEqual(1, _blockCaret.DestroyCount);
            _vim.Verify();
        }

        [Test, Description("Close should clear out the mark map")]
        public void Close3()
        {
            _markMap.SetLocalMark(new SnapshotPoint(_view.TextSnapshot, 0), 'c');
            _vim.Setup(x => x.RemoveBuffer(_view)).Returns(true).Verifiable();
            _normalMode.Setup(x => x.OnLeave());
            _buffer.SwitchMode(ModeKind.Normal);
            _buffer.Close();
            Assert.IsTrue(_markMap.GetLocalMark(_view.TextBuffer, 'c').IsNone());
            _vim.Verify();
        }

        [Test,Description("Disable command should be preprocessed")]
        public void Disable1()
        {
            var settings = VimSettingsUtil.CreateDefault;
            _normalMode.Setup(x => x.OnLeave());
            _disabledMode.Setup(x => x.OnEnter()).Verifiable();
            _buffer.ProcessInput(settings.DisableCommand);
            _disabledMode.Verify();
        }

        [Test, Description("Handle Switch previous mode")]
        public void Process1()
        {
            var prev = _buffer.ModeKind;
            _normalMode.Setup(x => x.OnLeave());
            _insertMode.Setup(x => x.OnEnter());
            _insertMode.Setup(x => x.OnLeave()).Verifiable();
            _buffer.SwitchMode(ModeKind.Insert);
            _insertMode.Setup(x => x.Process(It.IsAny<KeyInput>())).Returns(ProcessResult.SwitchPreviousMode).Verifiable();
            Assert.IsTrue(_buffer.ProcessChar('c'));
            _insertMode.Verify();
            Assert.AreEqual(prev, _buffer.ModeKind);
        }

        [Test, Description("Switch previous mode should still fire the event")]
        public void Process2()
        {
            _normalMode.Setup(x => x.OnLeave());
            _insertMode.Setup(x => x.OnEnter()).Verifiable();
            _insertMode.Setup(x => x.OnLeave()).Verifiable();
            _buffer.SwitchMode(ModeKind.Insert);
            _insertMode.Setup(x => x.Process(It.IsAny<KeyInput>())).Returns(ProcessResult.SwitchPreviousMode).Verifiable();
            var raised = false;
            _buffer.SwitchedMode += (e, args) => { raised = true; };
            Assert.IsTrue(_buffer.ProcessChar('c'));
            Assert.IsTrue(raised);
            _insertMode.Verify();
        }
    }
}
