using EnvDTE;
using Microsoft.VisualStudio.Shell.Interop;
using Moq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows.Input;
using Vim;
using Vim.UI.Wpf;
using Vim.UnitTest;
using Vim.UnitTest.Mock;
using Vim.VisualStudio.Implementation.Misc;
using Xunit;

namespace Vim.VisualStudio.UnitTest
{
    public abstract class FallbackKeyProcessorTest : VimTestBase
    {
        private FallbackKeyProcessor _keyProcessor;
        private Mock<IVsShell> _vsShell;
        private Mock<_DTE> _dte;
        private Mock<Commands> _commands;
        private Mock<IVimApplicationSettings> _vimApplicationSettings;
        private List<CommandKeyBinding> _removedBindingList;
        private MockKeyboardDevice _keyboardDevice;
        private IVimBuffer _vimBuffer;

        protected FallbackKeyProcessorTest(bool useVimBuffer = false)
        {
            _keyboardDevice = new MockKeyboardDevice();
            _commands = new Mock<Commands>(MockBehavior.Strict);
            _dte = new Mock<_DTE>(MockBehavior.Loose);
            _dte.SetupGet(x => x.Commands).Returns(_commands.Object);
            _vsShell = new Mock<IVsShell>(MockBehavior.Loose);
            _removedBindingList = new List<CommandKeyBinding>();
            _vimApplicationSettings = new Mock<IVimApplicationSettings>(MockBehavior.Loose);
            _vimApplicationSettings
                .SetupGet(x => x.RemovedBindings)
                .Returns(() => _removedBindingList.AsReadOnly());

            var textView = CreateTextView("");
            _vimBuffer = useVimBuffer
                ? Vim.GetOrCreateVimBuffer(textView)
                : null;
            _keyProcessor = new FallbackKeyProcessor(
                _vsShell.Object,
                _dte.Object,
                CompositionContainer.GetExportedValue<IKeyUtil>(),
                _vimApplicationSettings.Object,
                textView,
                _vimBuffer,
                new ScopeData());
        }

        private CommandId AddRemovedBinding(string keyStroke, string name = "comment")
        {
            var keyBinding = KeyBinding.Parse(keyStroke);
            var commandId = new CommandId(Guid.NewGuid(), 0);
            _removedBindingList.Add(new CommandKeyBinding(commandId, name, keyBinding));
            _vimApplicationSettings
                .Raise(x => x.SettingsChanged += null, new ApplicationSettingsEventArgs());
            return commandId;
        }

        private void ExpectRaise(CommandId commandId)
        {
            object p1 = null;
            object p2 = null;
            _commands
                .Setup(x => x.Raise(commandId.Group.ToString(), (int)commandId.Id, ref p1, ref p2))
                .Verifiable();
        }

        private void ExpectNotRaise(CommandId commandId, Action isCalled)
        {
            object p1 = null;
            object p2 = null;
            _commands
                .Setup(x => x.Raise(commandId.Group.ToString(), (int)commandId.Id, ref p1, ref p2))
                .Callback(isCalled);
        }

        public sealed class VimBufferTest : FallbackKeyProcessorTest
        {
            public VimBufferTest() : base(useVimBuffer: true)
            {
            }

            [Fact]
            public void SimpleInDisabled()
            {
                var commandId = AddRemovedBinding("Global::ctrl+k");
                _vimBuffer.SwitchMode(ModeKind.Disabled, ModeArgument.None);
                ExpectRaise(commandId);
                _keyProcessor.KeyDown(_keyboardDevice.CreateKeyEventArgs(Key.K, ModifierKeys.Control));
                _commands.Verify();
            }

            [Fact]
            public void SimpleInNormal()
            {
                var commandId = AddRemovedBinding("Global::ctrl+k");
                _vimBuffer.SwitchMode(ModeKind.Disabled, ModeArgument.None);
                _keyProcessor.KeyDown(_keyboardDevice.CreateKeyEventArgs(Key.C, ModifierKeys.Control));
                _commands.Verify();
            }
        }

        public sealed class VimBufferDisabledTest : FallbackKeyProcessorTest
        {
            public VimBufferDisabledTest() : base(useVimBuffer: true)
            {
            }

            [Fact]
            public void CapitalC()
            {
                var commandId = AddRemovedBinding("Text Editor::Ctrl+Shift+Alt+C", "Edit.CopyParameterTip");
                _vimBuffer.SwitchMode(ModeKind.Disabled, ModeArgument.None);
                var isCalled = false;
                ExpectNotRaise(commandId, () => isCalled = true);

                _keyProcessor.KeyDown(_keyboardDevice.CreateKeyEventArgs(Key.C, ModifierKeys.Shift));

                Assert.False(isCalled, "this command should not be called");
            }

            [Fact]
            public void ShiftAltCtrlCWorks()
            {
                var commandId = AddRemovedBinding("Text Editor::Ctrl+Shift+Alt+C", "Edit.CopyParameterTip");
                _vimBuffer.SwitchMode(ModeKind.Disabled, ModeArgument.None);
                ExpectRaise(commandId);

                _keyProcessor.KeyDown(_keyboardDevice.CreateKeyEventArgs(Key.C, ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt));

                _commands.Verify();
            }

            [Fact]
            public void KeyChordsWorkWhenVsVimIsDisabled()
            {
                var commandId = AddRemovedBinding("Text Editor::Ctrl+K, Ctrl+U", "Uncomment Selection");
                _vimBuffer.SwitchMode(ModeKind.Disabled, ModeArgument.None);
                ExpectRaise(commandId);

                _keyProcessor.KeyDown(_keyboardDevice.CreateKeyEventArgs(Key.K, ModifierKeys.Control));
                _keyProcessor.KeyDown(_keyboardDevice.CreateKeyEventArgs(Key.U, ModifierKeys.Control));

                _commands.Verify();
            }

            [Fact(Skip = "https://github.com/jaredpar/VsVim/issues/1863")]
            public void InvalidChordFollowedByGoodChordWorks()
            {
                var commandId = AddRemovedBinding("Text Editor::Ctrl+K, Ctrl+U", "Uncomment Selection");
                _vimBuffer.SwitchMode(ModeKind.Disabled, ModeArgument.None);
                ExpectRaise(commandId);
                var isCalled = false;
                ExpectNotRaise(commandId, () => isCalled = true);

                _keyProcessor.KeyDown(_keyboardDevice.CreateKeyEventArgs(Key.K, ModifierKeys.Control));
                _keyProcessor.KeyDown(_keyboardDevice.CreateKeyEventArgs(Key.G, ModifierKeys.Control));
                Assert.False(isCalled, "the command should not be called yet");

                _keyProcessor.KeyDown(_keyboardDevice.CreateKeyEventArgs(Key.K, ModifierKeys.Control));
                _keyProcessor.KeyDown(_keyboardDevice.CreateKeyEventArgs(Key.U, ModifierKeys.Control));
                _commands.Verify();
            }

            [Fact]
            public void TestEditorScopeCommandsAreBeforeGlobal()
            {
                var commandId = AddRemovedBinding("Text Editor::Ctrl+K, Ctrl+U", "Uncomment Selection");
                var commandIdGlobal = AddRemovedBinding("Global::Ctrl+K, Ctrl+U", "DoSomethingElse");
                _vimBuffer.SwitchMode(ModeKind.Disabled, ModeArgument.None);
                ExpectRaise(commandId);
                var isCalled = false;
                ExpectNotRaise(commandIdGlobal, () => isCalled = true);

                _keyProcessor.KeyDown(_keyboardDevice.CreateKeyEventArgs(Key.K, ModifierKeys.Control));
                _keyProcessor.KeyDown(_keyboardDevice.CreateKeyEventArgs(Key.U, ModifierKeys.Control));

                Assert.False(isCalled, "the global command should not be called");
                _commands.Verify();
            }
        }

        public sealed class TextViewOnlyTest : FallbackKeyProcessorTest
        {
            public TextViewOnlyTest() : base(useVimBuffer: false)
            {
            }

            [Fact]
            public void SimpleRemoved()
            {
                var commandId = AddRemovedBinding("Global::ctrl+k");
                ExpectRaise(commandId);
                _keyProcessor.KeyDown(_keyboardDevice.CreateKeyEventArgs(Key.K, ModifierKeys.Control));
                _commands.Verify();
            }

            [Fact]
            public void SimpleNotRemoved()
            {
                var commandId = AddRemovedBinding("Global::ctrl+k");
                ExpectRaise(commandId);
                _keyProcessor.KeyDown(_keyboardDevice.CreateKeyEventArgs(Key.K, ModifierKeys.Control));
                _commands.Verify();
            }
        }
    }
}
