using EnvDTE;
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
using VsVim.Implementation.Misc;
using Xunit;

namespace VsVim.UnitTest
{
    public abstract class FallbackKeyProcessorTest : VimTestBase
    {
        private FallbackKeyProcessor _keyProcessor;
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
