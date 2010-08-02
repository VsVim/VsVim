using System.Linq;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using VimCore.Test.Mock;
using VimCore.Test.Utils;

namespace VimCore.Test
{
    [TestFixture]
    public class VimBufferTests
    {
        IWpfTextView _view;
        Mock<IVim> _vim;
        Mock<INormalMode> _normalMode;
        Mock<IMode> _insertMode;
        Mock<IMode> _disabledMode;
        Mock<IJumpList> _jumpList;
        Mock<IKeyMap> _keyMap;
        Mock<IVimGlobalSettings> _globalSettings;
        Mock<IVimLocalSettings> _settings;
        Mock<IVimHost> _host;
        VimBuffer _rawBuffer;
        IVimBuffer _buffer;
        MarkMap _markMap;

        [SetUp]
        public void Initialize()
        {
            _view = EditorUtil.CreateView("here we go");
            _markMap = new MarkMap(new TrackingLineColumnService());
            _globalSettings = MockObjectFactory.CreateGlobalSettings();
            _settings = MockObjectFactory.CreateLocalSettings(_globalSettings.Object);
            _keyMap = new Mock<IKeyMap>(MockBehavior.Strict);
            _host = new Mock<IVimHost>(MockBehavior.Strict);
            _vim = MockObjectFactory.CreateVim(map:_markMap, settings:_globalSettings.Object, keyMap:_keyMap.Object, host:_host.Object);
            _disabledMode = new Mock<IMode>(MockBehavior.Loose);
            _disabledMode.SetupGet(x => x.ModeKind).Returns(ModeKind.Disabled);
            _normalMode = new Mock<INormalMode>(MockBehavior.Loose);
            _normalMode.Setup(x => x.OnEnter(ModeArgument.None));
            _normalMode.SetupGet(x => x.ModeKind).Returns(ModeKind.Normal);
            _normalMode.SetupGet(x => x.IsOperatorPending).Returns(false);
            _normalMode.SetupGet(x => x.IsWaitingForInput).Returns(false);
            _insertMode = new Mock<IMode>(MockBehavior.Loose);
            _insertMode.SetupGet(x => x.ModeKind).Returns(ModeKind.Insert);
            _jumpList = new Mock<IJumpList>(MockBehavior.Strict);
            _rawBuffer = new VimBuffer(
                _vim.Object,
                _view,
                _jumpList.Object,
                _settings.Object);
            _rawBuffer.AddMode(_normalMode.Object);
            _rawBuffer.AddMode(_insertMode.Object);
            _rawBuffer.AddMode(_disabledMode.Object);
            _rawBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            _buffer = _rawBuffer;
        }

        private void DisableKeyRemap()
        {
            _keyMap
                .Setup(x => x.GetKeyMapping(It.IsAny<KeyInputSet>(), It.IsAny<KeyRemapMode>()))
                .Returns(KeyMappingResult.NoMapping);
        }

        [Test]
        public void SwitchedMode1()
        {
            var ran = false;
            _normalMode.Setup(x => x.OnLeave());
            _buffer.SwitchedMode += (s, m) => { ran = true; };
            _buffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            Assert.IsTrue(ran);
        }

        [Test]
        public void SwitchPreviousMode1()
        {
            _normalMode.Setup(x => x.OnLeave()).Verifiable();
            _insertMode.Setup(x => x.OnEnter(ModeArgument.None)).Verifiable();
            _buffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
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
            _insertMode.Setup(x => x.OnEnter(ModeArgument.None)).Verifiable();
            _buffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            _insertMode.Setup(x => x.OnLeave()).Verifiable();

            var ran = false;
            _buffer.SwitchedMode += (s, m) => { ran = true; };
            _buffer.SwitchPreviousMode();
            Assert.IsTrue(ran);
        }

        [Test]
        public void KeyInputProcessed1()
        {
            DisableKeyRemap();
            var ki = InputUtil.CharToKeyInput('f');
            _normalMode.Setup(x => x.Process(ki)).Returns(ProcessResult.Processed);
            var ran = false;
            _buffer.KeyInputProcessed += (s, i) => { ran = true; };
            _buffer.Process(ki);
            Assert.IsTrue(ran);
        }

        [Test]
        public void KeyInputBuffered1()
        {
            DisableKeyRemap();
            var ki = InputUtil.CharToKeyInput('f');
            _normalMode.Setup(x => x.Process(ki)).Returns(ProcessResult.Processed);
            var ran = false;
            _buffer.KeyInputBuffered += (s, i) => { ran = true; };
            _buffer.Process(ki);
            Assert.IsFalse(ran);
        }

        [Test]
        public void KeyInputBuffered2()
        {
            var ki = InputUtil.CharToKeyInput('f');
            _keyMap
                .Setup(x => x.GetKeyMapping(KeyInputSet.NewOneKeyInput(ki), It.IsAny<KeyRemapMode>()))
                .Returns(KeyMappingResult.MappingNeedsMoreInput);
            var ran = false;
            _buffer.KeyInputBuffered += (s, i) => { ran = true; };
            _buffer.Process(ki);
            Assert.IsTrue(ran);
        }

        [Test, Description("Close should call OnLeave for the active mode")]
        public void Close1()
        {
            var ran = false;
            _vim.Setup(x => x.RemoveBuffer(_view)).Returns(true).Verifiable();
            _normalMode.Setup(x => x.OnLeave()).Callback(() => { ran = true; });
            _buffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            _buffer.Close();
            Assert.IsTrue(ran);
            _vim.Verify();
        }

        [Test, Description("Close should call OnClose for every IMode")]
        public void Close3()
        {
            _vim.Setup(x => x.RemoveBuffer(_view)).Returns(true).Verifiable();
            _buffer.Close();
            _normalMode.Verify(x => x.OnClose());
            _insertMode.Verify(x => x.OnClose());
            _disabledMode.Verify(x => x.OnClose());
        }

        [Test,Description("Disable command should be preprocessed")]
        public void Disable1()
        {
            DisableKeyRemap();
            _normalMode.Setup(x => x.OnLeave());
            _disabledMode.Setup(x => x.OnEnter(ModeArgument.None)).Verifiable();
            _buffer.Process(Vim.GlobalSettings.DisableCommand);
            _disabledMode.Verify();
        }

        [Test, Description("Handle Switch previous mode")]
        public void Process1()
        {
            DisableKeyRemap();
            var prev = _buffer.ModeKind;
            _normalMode.Setup(x => x.OnLeave());
            _insertMode.Setup(x => x.OnEnter(ModeArgument.None));
            _insertMode.Setup(x => x.OnLeave()).Verifiable();
            _buffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            _insertMode.Setup(x => x.Process(It.IsAny<KeyInput>())).Returns(ProcessResult.SwitchPreviousMode).Verifiable();
            Assert.IsTrue(_buffer.ProcessChar('c'));
            _insertMode.Verify();
            Assert.AreEqual(prev, _buffer.ModeKind);
        }

        [Test, Description("Switch previous mode should still fire the event")]
        public void Process2()
        {
            DisableKeyRemap();
            _normalMode.Setup(x => x.OnLeave());
            _insertMode.Setup(x => x.OnEnter(ModeArgument.None)).Verifiable();
            _insertMode.Setup(x => x.OnLeave()).Verifiable();
            _buffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            _insertMode.Setup(x => x.Process(It.IsAny<KeyInput>())).Returns(ProcessResult.SwitchPreviousMode).Verifiable();
            var raised = false;
            _buffer.SwitchedMode += (e, args) => { raised = true; };
            Assert.IsTrue(_buffer.ProcessChar('c'));
            Assert.IsTrue(raised);
            _insertMode.Verify();
        }

        [Test]
        public void Remap1()
        {
            var newKi = InputUtil.CharToKeyInput('c');
            _keyMap
                .Setup(x => x.GetKeyMapping(KeyInputSetUtil.ofChar('b'), KeyRemapMode.Normal))
                .Returns(KeyMappingResult.NewMapped(KeyInputSet.NewOneKeyInput(newKi)));
            _normalMode.Setup(x => x.Process(newKi)).Returns(ProcessResult._unique_Processed).Verifiable();
            Assert.IsTrue(_buffer.ProcessChar('b'));
            _keyMap.Verify();
            _normalMode.Verify();
        }

        [Test, Description("Multiple keys returned")]
        public void Remap2()
        {
            var list = new KeyInput[] {
                InputUtil.CharToKeyInput('c'),
                InputUtil.CharToKeyInput('d') };
            _keyMap
                .Setup(x => x.GetKeyMapping(KeyInputSetUtil.ofChar('b'), KeyRemapMode.Normal))
                .Returns(KeyMappingResult.NewMapped(KeyInputSet.NewManyKeyInputs(list.ToFSharpList())));
            _normalMode.Setup(x => x.Process(list[0])).Returns(ProcessResult.Processed).Verifiable();
            _normalMode.Setup(x => x.Process(list[1])).Returns(ProcessResult.Processed).Verifiable();
            Assert.IsTrue(_buffer.ProcessChar('b'));
            _normalMode.Verify();
        }

        [Test, Description("Don't return a value for a different mode")]
        public void Remap3()
        {
            var list = new KeyInput[] {
                InputUtil.CharToKeyInput('c'),
                InputUtil.CharToKeyInput('d') };
            _keyMap
                .Setup(x => x.GetKeyMapping(KeyInputSetUtil.ofChar('b'), KeyRemapMode.Command))
                .Returns(KeyMappingResult.NewMapped(KeyInputSet.NewManyKeyInputs(list.ToFSharpList())));
            _keyMap
                .Setup(x => x.GetKeyMapping(KeyInputSetUtil.ofChar('b'), KeyRemapMode.Normal))
                .Returns(KeyMappingResult.NoMapping);
            _normalMode.Setup(x => x.Process(InputUtil.CharToKeyInput('b'))).Returns(ProcessResult.Processed).Verifiable();
            Assert.IsTrue(_buffer.ProcessChar('b'));
            _normalMode.Verify();
        }

        [Test, Description("Don't send input down to normal mode if we're in operator pending")]
        public void Remap4()
        {
            var oldKi = InputUtil.CharToKeyInput('b');
            var newKi = InputUtil.CharToKeyInput('c');
            _keyMap
                .Setup(x => x.GetKeyMapping(KeyInputSet.NewOneKeyInput(oldKi), KeyRemapMode.Normal))
                .Returns(KeyMappingResult.NewMapped(KeyInputSet.NewOneKeyInput(newKi)));
            _keyMap
                .Setup(x => x.GetKeyMapping(KeyInputSet.NewOneKeyInput(oldKi), KeyRemapMode.OperatorPending))
                .Returns(KeyMappingResult.NewMapped(KeyInputSet.NewOneKeyInput(oldKi)));
            _normalMode.SetupGet(x => x.IsOperatorPending).Returns(true);
            _normalMode.Setup(x => x.Process(oldKi)).Returns(ProcessResult.Processed).Verifiable();
            Assert.IsTrue(_buffer.Process(oldKi));
            _normalMode.Verify();
        }

        [Test, Description("Don't send input down to normal mode if we're in waitforinput")]
        public void Remap5()
        {
            var oldKi = InputUtil.CharToKeyInput('b');
            _normalMode.SetupGet(x => x.IsOperatorPending).Returns(false);
            _normalMode.SetupGet(x => x.IsWaitingForInput).Returns(true);
            _normalMode.Setup(x => x.Process(oldKi)).Returns(ProcessResult.Processed).Verifiable();
            Assert.IsTrue(_buffer.Process(oldKi));
            _normalMode.Verify();
        }

        [Test, Description("Recursive mapping should print out an error message")]
        public void Remap6()
        {
            var didRun = false;
            _buffer.ErrorMessage += (notUsed, msg) =>
                {
                    Assert.AreEqual(Resources.Vim_RecursiveMapping, msg);
                    didRun = true;
                };
            _keyMap
                .Setup(x => x.GetKeyMapping(It.IsAny<KeyInputSet>(), KeyRemapMode.Normal))
                .Returns(KeyMappingResult.NewRecursiveMapping(KeyInputSet.Empty));
            _buffer.ProcessChar('b');
            Assert.IsTrue(didRun);
        }

        [Test, Description("When more input is needed for a map don't pass it to the IMode")]
        public void Remap7()
        {
            _keyMap
                .Setup(x => x.GetKeyMapping(It.IsAny<KeyInputSet>(), KeyRemapMode.Normal))
                .Returns(KeyMappingResult.MappingNeedsMoreInput)
                .Verifiable();
            _buffer.ProcessChar('b');
            _keyMap.Verify();
        }

        [Test]
        public void Remap8()
        {
            _keyMap
                .Setup(x => x.GetKeyMapping(KeyInputSetUtil.ofChar('a'), KeyRemapMode.Normal))
                .Returns(KeyMappingResult.MappingNeedsMoreInput)
                .Verifiable();
            _buffer.ProcessChar('a');
            var toProcess = InputUtil.CharToKeyInput('c');
            _keyMap
                .Setup(x => x.GetKeyMapping(It.IsAny<KeyInputSet>(), KeyRemapMode.Normal))
                .Returns(KeyMappingResult.NewMapped(KeyInputSet.NewOneKeyInput(toProcess)))
                .Verifiable();
            _normalMode.Setup(x => x.Process(toProcess)).Returns(ProcessResult.Processed).Verifiable();
            _buffer.ProcessChar('b');
            _keyMap.Verify();
            _normalMode.Verify();
        }

        [Test]
        public void BufferedRemapKeyInputs1()
        {
            _keyMap
                .Setup(x => x.GetKeyMapping(KeyInputSetUtil.ofChar('a'), KeyRemapMode.Normal))
                .Returns(KeyMappingResult.MappingNeedsMoreInput)
                .Verifiable();
            _buffer.ProcessChar('a');
            var list = _buffer.BufferedRemapKeyInputs.ToList();
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual('a', list[0].Char);
        }

        [Test]
        public void BufferedRemapKeyInputs2()
        {
            Assert.AreEqual(0, _buffer.BufferedRemapKeyInputs.Count());
        }

        [Test]
        public void BufferedRemapKeyInputs3()
        {
            _keyMap
                .Setup(x => x.GetKeyMapping(KeyInputSetUtil.ofChar('a'), KeyRemapMode.Normal))
                .Returns(KeyMappingResult.MappingNeedsMoreInput);
            _buffer.ProcessChar('a');
            _keyMap
                .Setup(x => x.GetKeyMapping(It.IsAny<KeyInputSet>(), KeyRemapMode.Normal))
                .Returns(KeyMappingResult.MappingNeedsMoreInput);
            _buffer.ProcessChar('b');
            var list = _buffer.BufferedRemapKeyInputs.ToList();
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual('a', list[0].Char);
            Assert.AreEqual('b', list[1].Char);
        }

        [Test]
        public void BufferedRemapKeyInputs4()
        {
            _keyMap
                .Setup(x => x.GetKeyMapping(KeyInputSetUtil.ofChar('a'), KeyRemapMode.Normal))
                .Returns(KeyMappingResult.MappingNeedsMoreInput);
            _buffer.ProcessChar('a');
            _keyMap
                .Setup(x => x.GetKeyMapping(It.IsAny<KeyInputSet>(), KeyRemapMode.Normal))
                .Returns(KeyMappingResult.NewMapped(KeyInputSet.NewOneKeyInput(InputUtil.CharToKeyInput('b'))));
            _normalMode.Setup(x => x.Process(It.IsAny<KeyInput>())).Returns(ProcessResult.Processed);
            _buffer.ProcessChar('b');
            Assert.AreEqual(0, _buffer.BufferedRemapKeyInputs.Count());
        }

        [Test]
        public void CanProcess1()
        {
            var ki = InputUtil.CharToKeyInput('c');
            _keyMap
                .Setup(x => x.GetKeyMapping(KeyInputSet.NewOneKeyInput(ki), KeyRemapMode.Normal))
                .Returns(KeyMappingResult.NoMapping)
                .Verifiable();
            _normalMode
                .Setup(x => x.CanProcess(ki))
                .Returns(true)
                .Verifiable();
            Assert.IsTrue(_buffer.CanProcess(ki));
            _normalMode.Verify();
            _keyMap.Verify();
        }

        [Test]
        public void CanProcess2()
        {
            var ki = InputUtil.CharToKeyInput('c');
            var ki2 = InputUtil.CharToKeyInput('d');
            _keyMap
                .Setup(x => x.GetKeyMapping(KeyInputSet.NewOneKeyInput(ki), KeyRemapMode.Normal))
                .Returns(KeyMappingResult.NewMapped(KeyInputSet.NewOneKeyInput(ki2)))
                .Verifiable();
            _normalMode
                .Setup(x => x.CanProcess(ki2))
                .Returns(true)
                .Verifiable();
            Assert.IsTrue(_buffer.CanProcess(ki));
            _normalMode.Verify();
            _keyMap.Verify();
        }

        [Test]
        public void Closed1()
        {
            _vim.Setup(x => x.RemoveBuffer(_view)).Returns(true).Verifiable();
            var didSee = false;
            _buffer.Closed += delegate { didSee = true; };
            _buffer.Close();
            Assert.IsTrue(didSee);
        }

        [Test]
        [ExpectedException(typeof(System.InvalidOperationException))]
        public void Closed2()
        {
            _vim.Setup(x => x.RemoveBuffer(_view)).Returns(true).Verifiable();
            _buffer.Close();
            _buffer.Close();
        }

    }
}
