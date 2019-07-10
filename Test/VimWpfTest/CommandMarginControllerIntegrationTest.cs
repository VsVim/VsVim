using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Vim.UI.Wpf.Implementation.CommandMargin;
using Vim.UnitTest;
using Vim.UnitTest.Exports;
using Xunit;

namespace Vim.UI.Wpf.UnitTest
{
    public class CommandMarginControllerIntegrationTest : VimTestBase
    {
        private readonly IVimBuffer _vimBuffer;
        private readonly CommandMarginController _controller;
        private readonly CommandMarginControl _control;
        private readonly TestableClipboardDevice _clipboardDevice;

        public CommandMarginControllerIntegrationTest()
        {
            _vimBuffer = CreateVimBuffer();
            _clipboardDevice = (TestableClipboardDevice)CompositionContainer.GetExportedValue<IClipboardDevice>();

            var parentElement = new FrameworkElement();
            _control = new CommandMarginControl();
            _controller = new CommandMarginController(
                _vimBuffer,
                parentElement,
                _control,
                VimEditorHost.EditorFormatMapService.GetEditorFormatMap(_vimBuffer.TextView),
                VimEditorHost.ClassificationFormatMapService.GetClassificationFormatMap(_vimBuffer.TextView),
                CommonOperationsFactory.GetCommonOperations(_vimBuffer.VimBufferData),
                _clipboardDevice,
                false);
        }

        [WpfFact]
        public void QuitDirtyBuffer()
        {
            VimHost.IsDirtyFunc = _ => true;
            _vimBuffer.ProcessNotation(":q<CR>");
            Assert.Equal(Resources.Common_NoWriteSinceLastChange, _control.CommandLineTextBox.Text);
        }

        /// <summary>
        /// A mapped left arrow key should affect the caret position
        /// </summary>
        [WpfFact]
        public void MappedLeft()
        {
            // Reported in issue #1103.
            var command = "%s//g";
            _vimBuffer.Process($":map Q :{command}<Left><Left>", enter: true);
            Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
            _vimBuffer.Process("Q");
            Assert.Equal(ModeKind.Command, _vimBuffer.ModeKind);
            var expectedCaretPosition = command.Length - 2;
            var expectedCommand = new EditableCommand(command, expectedCaretPosition);
            Assert.Equal(expectedCommand, _vimBuffer.CommandMode.EditableCommand);
            Assert.Equal(":" + command, _control.CommandLineTextBox.Text);
            Assert.Equal(expectedCaretPosition + 1, _control.CommandLineTextBox.SelectionStart);
        }

        /// <summary>
        /// A mapped special key without a binding should not result in a null
        /// character being inserted into the command
        /// </summary>
        [WpfFact]
        public void MappedSpecialKey()
        {
            // Reported in #2683.
            var command = "%s//g";
            _vimBuffer.Process($":map Q :{command}<PageUp>", enter: true);
            Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
            _vimBuffer.Process("Q");
            Assert.Equal(ModeKind.Command, _vimBuffer.ModeKind);
            var expectedCaretPosition = command.Length;
            var expectedCommand = new EditableCommand(command, expectedCaretPosition);
            Assert.Equal(expectedCommand, _vimBuffer.CommandMode.EditableCommand);
            Assert.Equal(":" + command, _control.CommandLineTextBox.Text);
            Assert.Equal(expectedCaretPosition + 1, _control.CommandLineTextBox.SelectionStart);
        }
    }
}
