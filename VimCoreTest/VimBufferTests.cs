using System;
using System.Linq;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.UnitTest;
using Vim.UnitTest.Mock;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class VimBufferTests
    {
        private IWpfTextView _view;
        private MockRepository _factory;
        private Mock<IVim> _vim;
        private Mock<INormalMode> _normalMode;
        private Mock<IMode> _insertMode;
        private Mock<IMode> _disabledMode;
        private Mock<IJumpList> _jumpList;
        private Mock<IKeyMap> _keyMap;
        private Mock<IVimGlobalSettings> _globalSettings;
        private Mock<IVimLocalSettings> _settings;
        private Mock<IVimHost> _host;
        private Mock<IIncrementalSearch> _incrementalSearch;
        private Mock<ITextViewMotionUtil> _motionUtil;
        private VimBuffer _rawBuffer;
        private IVimBuffer _buffer;
        private MarkMap _markMap;

        [SetUp]
        public void Initialize()
        {
            _view = EditorUtil.CreateView("here we go");
            _markMap = new MarkMap(new TrackingLineColumnService());
            _factory = new MockRepository(MockBehavior.Strict);
            _globalSettings = MockObjectFactory.CreateGlobalSettings(factory:_factory);
            _settings = MockObjectFactory.CreateLocalSettings(_globalSettings.Object, factory:_factory);
            _keyMap = _factory.Create<IKeyMap>();
            _host = _factory.Create<IVimHost>(MockBehavior.Strict);
            _vim = MockObjectFactory.CreateVim(map: _markMap, settings: _globalSettings.Object, keyMap: _keyMap.Object, host: _host.Object, factory:_factory);
            _disabledMode = _factory.Create<IMode>(MockBehavior.Loose);
            _disabledMode.SetupGet(x => x.ModeKind).Returns(ModeKind.Disabled);
            _normalMode = _factory.Create<INormalMode>(MockBehavior.Loose);
            _normalMode.SetupGet(x => x.KeyRemapMode).Returns(KeyRemapMode.Normal);
            _normalMode.Setup(x => x.OnEnter(ModeArgument.None));
            _normalMode.SetupGet(x => x.ModeKind).Returns(ModeKind.Normal);
            _insertMode = _factory.Create<IMode>(MockBehavior.Loose);
            _insertMode.SetupGet(x => x.ModeKind).Returns(ModeKind.Insert);
            _jumpList = _factory.Create<IJumpList>(MockBehavior.Strict);
            _motionUtil = _factory.Create<ITextViewMotionUtil>(MockBehavior.Strict);
            _incrementalSearch = _factory.Create<IIncrementalSearch>();
            _rawBuffer = new VimBuffer(
                _vim.Object,
                _view,
                _jumpList.Object,
                _settings.Object,
                _incrementalSearch.Object,
                _motionUtil.Object);
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

        [Test, Description("SwitchPreviousMode should raise the SwitchedMode event")]
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
            var ki = KeyInputUtil.CharToKeyInput('f');
            _normalMode.Setup(x => x.Process(ki)).Returns(ProcessResult.Processed);
            var ran = false;
            _buffer.KeyInputProcessed += (s, i) => { ran = true; };
            _buffer.Process(ki);
            Assert.IsTrue(ran);
        }

        [Test]
        public void KeyInputStartAndEnd1()
        {
            DisableKeyRemap();
            var ki = KeyInputUtil.CharToKeyInput('c');
            _normalMode.Setup(x => x.Process(ki)).Returns(ProcessResult.Processed);
            var count = 1;
            _buffer.KeyInputStart += (_, args) =>
            {
                Assert.AreEqual(1, count++);
                Assert.AreSame(args, ki);
            };
            _buffer.KeyInputEnd += (_, args) =>
            {
                Assert.AreEqual(2, count++);
                Assert.AreSame(args, ki);
            };
            _buffer.Process(ki);
            Assert.AreEqual(3, count);
        }

        [Test]
        [Description("Should fire even if there is an exception")]
        public void KeyInputStartAndEnd2()
        {
            DisableKeyRemap();
            var ki = KeyInputUtil.CharToKeyInput('c');
            _normalMode.Setup(x => x.Process(ki)).Throws(new Exception());
            var count = 1;
            _buffer.KeyInputStart += (_, args) =>
            {
                Assert.AreEqual(1, count++);
                Assert.AreSame(args, ki);
            };
            _buffer.KeyInputEnd += (_, args) =>
            {
                Assert.AreEqual(2, count++);
                Assert.AreSame(args, ki);
            };
            var caught = false;
            try
            {
                _buffer.Process(ki);
            }
            catch
            {
                caught = true;
            }
            Assert.IsTrue(caught);
            Assert.AreEqual(3, count);
        }

        [Test]
        [Description("Should fire even if the KeyInput is buffered")]
        public void KeyInputStartAndEnd3()
        {
            var ki = KeyInputUtil.CharToKeyInput('f');
            _keyMap
                .Setup(x => x.GetKeyMapping(KeyInputSet.NewOneKeyInput(ki), It.IsAny<KeyRemapMode>()))
                .Returns(KeyMappingResult.MappingNeedsMoreInput);
            var count = 1;
            _buffer.KeyInputStart += (_, args) =>
            {
                Assert.AreEqual(1, count++);
                Assert.AreSame(args, ki);
            };
            _buffer.KeyInputEnd += (_, args) =>
            {
                Assert.AreEqual(2, count++);
                Assert.AreSame(args, ki);
            };
            _buffer.Process(ki);
            Assert.AreEqual(3, count);
        }

        [Test]
        public void KeyInputBuffered1()
        {
            DisableKeyRemap();
            var ki = KeyInputUtil.CharToKeyInput('f');
            _normalMode.Setup(x => x.Process(ki)).Returns(ProcessResult.Processed);
            var ran = false;
            _buffer.KeyInputBuffered += (s, i) => { ran = true; };
            _buffer.Process(ki);
            Assert.IsFalse(ran);
        }

        [Test]
        public void KeyInputBuffered2()
        {
            var ki = KeyInputUtil.CharToKeyInput('f');
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

        [Test, Description("Disable command should be preprocessed")]
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
            Assert.IsTrue(_buffer.Process('c'));
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
            Assert.IsTrue(_buffer.Process('c'));
            Assert.IsTrue(raised);
            _insertMode.Verify();
        }

        [Test]
        public void Remap1()
        {
            var newKi = KeyInputUtil.CharToKeyInput('c');
            _keyMap
                .Setup(x => x.GetKeyMapping(KeyInputSetUtil.OfChar('b'), KeyRemapMode.Normal))
                .Returns(KeyMappingResult.NewMapped(KeyInputSet.NewOneKeyInput(newKi)));
            _normalMode.Setup(x => x.Process(newKi)).Returns(ProcessResult._unique_Processed).Verifiable();
            Assert.IsTrue(_buffer.Process('b'));
            _keyMap.Verify();
            _normalMode.Verify();
        }

        [Test, Description("Multiple keys returned")]
        public void Remap2()
        {
            var list = new[] {
                KeyInputUtil.CharToKeyInput('c'),
                KeyInputUtil.CharToKeyInput('d') };
            _keyMap
                .Setup(x => x.GetKeyMapping(KeyInputSetUtil.OfChar('b'), KeyRemapMode.Normal))
                .Returns(KeyMappingResult.NewMapped(KeyInputSet.NewManyKeyInputs(list.ToFSharpList())));
            _normalMode.Setup(x => x.Process(list[0])).Returns(ProcessResult.Processed).Verifiable();
            _normalMode.Setup(x => x.Process(list[1])).Returns(ProcessResult.Processed).Verifiable();
            Assert.IsTrue(_buffer.Process('b'));
            _normalMode.Verify();
        }

        [Test, Description("Don't return a value for a different mode")]
        public void Remap3()
        {
            var list = new KeyInput[] {
                KeyInputUtil.CharToKeyInput('c'),
                KeyInputUtil.CharToKeyInput('d') };
            _keyMap
                .Setup(x => x.GetKeyMapping(KeyInputSetUtil.OfChar('b'), KeyRemapMode.Command))
                .Returns(KeyMappingResult.NewMapped(KeyInputSet.NewManyKeyInputs(list.ToFSharpList())));
            _keyMap
                .Setup(x => x.GetKeyMapping(KeyInputSetUtil.OfChar('b'), KeyRemapMode.Normal))
                .Returns(KeyMappingResult.NoMapping);
            _normalMode.Setup(x => x.Process(KeyInputUtil.CharToKeyInput('b'))).Returns(ProcessResult.Processed).Verifiable();
            Assert.IsTrue(_buffer.Process('b'));
            _normalMode.Verify();
        }

        [Test, Description("Don't send input down to normal mode if we're in operator pending")]
        public void Remap4()
        {
            var oldKi = KeyInputUtil.CharToKeyInput('b');
            var newKi = KeyInputUtil.CharToKeyInput('c');
            _keyMap
                .Setup(x => x.GetKeyMapping(KeyInputSet.NewOneKeyInput(oldKi), KeyRemapMode.Normal))
                .Returns(KeyMappingResult.NewMapped(KeyInputSet.NewOneKeyInput(newKi)));
            _keyMap
                .Setup(x => x.GetKeyMapping(KeyInputSet.NewOneKeyInput(oldKi), KeyRemapMode.OperatorPending))
                .Returns(KeyMappingResult.NewMapped(KeyInputSet.NewOneKeyInput(oldKi)));
            _normalMode.SetupGet(x => x.KeyRemapMode).Returns(KeyRemapMode.OperatorPending);
            _normalMode.Setup(x => x.Process(oldKi)).Returns(ProcessResult.Processed).Verifiable();
            Assert.IsTrue(_buffer.Process(oldKi));
            _normalMode.Verify();
        }

        [Test, Description("Don't send input down to normal mode if we're in waitforinput")]
        public void Remap5()
        {
            var oldKi = KeyInputUtil.CharToKeyInput('b');
            var newKi = KeyInputUtil.CharToKeyInput('c');
            _keyMap
                .Setup(x => x.GetKeyMapping(KeyInputSet.NewOneKeyInput(oldKi), KeyRemapMode.Language))
                .Returns(KeyMappingResult.NewMapped(KeyInputSet.NewOneKeyInput(newKi)));
            _normalMode.SetupGet(x => x.KeyRemapMode).Returns(KeyRemapMode.Language);
            _normalMode.Setup(x => x.Process(newKi)).Returns(ProcessResult.Processed).Verifiable();
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
            _buffer.Process('b');
            Assert.IsTrue(didRun);
        }

        [Test, Description("When more input is needed for a map don't pass it to the IMode")]
        public void Remap7()
        {
            _keyMap
                .Setup(x => x.GetKeyMapping(It.IsAny<KeyInputSet>(), KeyRemapMode.Normal))
                .Returns(KeyMappingResult.MappingNeedsMoreInput)
                .Verifiable();
            _buffer.Process('b');
            _keyMap.Verify();
        }

        [Test]
        public void Remap8()
        {
            _keyMap
                .Setup(x => x.GetKeyMapping(KeyInputSetUtil.OfChar('a'), KeyRemapMode.Normal))
                .Returns(KeyMappingResult.MappingNeedsMoreInput)
                .Verifiable();
            _buffer.Process('a');
            var toProcess = KeyInputUtil.CharToKeyInput('c');
            _keyMap
                .Setup(x => x.GetKeyMapping(It.IsAny<KeyInputSet>(), KeyRemapMode.Normal))
                .Returns(KeyMappingResult.NewMapped(KeyInputSet.NewOneKeyInput(toProcess)))
                .Verifiable();
            _normalMode.Setup(x => x.Process(toProcess)).Returns(ProcessResult.Processed).Verifiable();
            _buffer.Process('b');
            _keyMap.Verify();
            _normalMode.Verify();
        }

        [Test]
        public void BufferedRemapKeyInputs1()
        {
            _keyMap
                .Setup(x => x.GetKeyMapping(KeyInputSetUtil.OfChar('a'), KeyRemapMode.Normal))
                .Returns(KeyMappingResult.MappingNeedsMoreInput)
                .Verifiable();
            _buffer.Process('a');
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
                .Setup(x => x.GetKeyMapping(KeyInputSetUtil.OfChar('a'), KeyRemapMode.Normal))
                .Returns(KeyMappingResult.MappingNeedsMoreInput);
            _buffer.Process('a');
            _keyMap
                .Setup(x => x.GetKeyMapping(It.IsAny<KeyInputSet>(), KeyRemapMode.Normal))
                .Returns(KeyMappingResult.MappingNeedsMoreInput);
            _buffer.Process('b');
            var list = _buffer.BufferedRemapKeyInputs.ToList();
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual('a', list[0].Char);
            Assert.AreEqual('b', list[1].Char);
        }

        [Test]
        public void BufferedRemapKeyInputs4()
        {
            _keyMap
                .Setup(x => x.GetKeyMapping(KeyInputSetUtil.OfChar('a'), KeyRemapMode.Normal))
                .Returns(KeyMappingResult.MappingNeedsMoreInput);
            _buffer.Process('a');
            _keyMap
                .Setup(x => x.GetKeyMapping(It.IsAny<KeyInputSet>(), KeyRemapMode.Normal))
                .Returns(KeyMappingResult.NewMapped(KeyInputSet.NewOneKeyInput(KeyInputUtil.CharToKeyInput('b'))));
            _normalMode.Setup(x => x.Process(It.IsAny<KeyInput>())).Returns(ProcessResult.Processed);
            _buffer.Process('b');
            Assert.AreEqual(0, _buffer.BufferedRemapKeyInputs.Count());
        }

        [Test]
        public void CanProcess1()
        {
            var ki = KeyInputUtil.CharToKeyInput('c');
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
            var ki = KeyInputUtil.CharToKeyInput('c');
            var ki2 = KeyInputUtil.CharToKeyInput('d');
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
