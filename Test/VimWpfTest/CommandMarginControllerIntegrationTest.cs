using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Vim.UI.Wpf.Implementation.CommandMargin;
using Vim.UnitTest;
using Xunit;

namespace Vim.UI.Wpf.UnitTest
{
    public class CommandMarginControllerIntegrationTest : VimTestBase
    {
        private readonly IVimBuffer _vimBuffer;
        private readonly CommandMarginController _controller;
        private readonly CommandMarginControl _control;

        public CommandMarginControllerIntegrationTest()
        {
            _vimBuffer = CreateVimBuffer();

            var parentElement = new FrameworkElement();
            _control = new CommandMarginControl();
            _controller = new CommandMarginController(
                _vimBuffer,
                parentElement,
                _control,
                VimEditorHost.EditorFormatMapService.GetEditorFormatMap(_vimBuffer.TextView),
                VimEditorHost.ClassificationFormatMapService.GetClassificationFormatMap(_vimBuffer.TextView));
        }

        [WpfFact]
        public void QuitDirtyBuffer()
        {
            VimHost.IsDirtyFunc = _ => true;
            _vimBuffer.ProcessNotation(":q<CR>");
            Assert.Equal(Resources.Common_NoWriteSinceLastChange, _control.CommandLineTextBox.Text);
        }
    }
}
