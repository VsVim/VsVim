using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Vim.UI.Wpf.Implementation.CommandMargin;
using Vim.UnitTest;
using Vim.UnitTest.Mock;
using Xunit;

namespace Vim.UI.Wpf.UnitTest
{
    public abstract class CommandLineEditIntegrationTest : VimTestBase
    {
        private MockRepository _factory;
        private CommandMarginControl _marginControl;
        private CommandMarginController _controller;
        private IVimBuffer _vimBuffer;
        private ITextBuffer _textBuffer;
        private ITextView _textView;
        private MockKeyboardDevice _keyboardDevice;

        protected void Create(params string[] lines)
        {
            _factory = new MockRepository(MockBehavior.Strict);
            _marginControl = new CommandMarginControl();
            _marginControl.StatusLine = String.Empty;
            _vimBuffer = CreateVimBuffer(lines);
            _textBuffer = _vimBuffer.TextBuffer;
            _textView = _vimBuffer.TextView;
            _keyboardDevice = new MockKeyboardDevice();

            var editorFormatMap = _factory.Create<IEditorFormatMap>(MockBehavior.Loose);
            editorFormatMap.Setup(x => x.GetProperties(It.IsAny<string>())).Returns(new ResourceDictionary());

            var parentVisualElement = _factory.Create<FrameworkElement>();

            _controller = new CommandMarginController(
                _vimBuffer,
                parentVisualElement.Object,
                _marginControl,
                editorFormatMap.Object,
                new List<Lazy<IOptionsProviderFactory>>());
        }

        /// <summary>
        /// This will process the provided string as key notation.  This method is different than
        /// IVimBuffer::ProcessNotation because it will attempt to take into account the focus 
        /// of the CommandMarginControl instance.  It will route the provided key into a WPF key
        /// event when it has focus and give it directly to that control
        /// </summary>
        private void ProcessNotation(string notation)
        {
            var keyInputList = KeyNotationUtil.StringToKeyInputSet(notation).KeyInputs.ToList();
            for (int i = 0; i < keyInputList.Count; i++)
            {
                var keyInput = keyInputList[i];
                if (_marginControl.IsEditEnabled)
                {
                    _keyboardDevice.SendKeyStroke(_marginControl.CommandLineTextBox, keyInput);
                }
                else
                {
                    _vimBuffer.Process(keyInput);
                }
            }
        }

        public sealed class BasicTest : CommandLineEditIntegrationTest
        {
            [Fact]
            public void EscapeKeyExits()
            {
                Create("cat");
                ProcessNotation(@":ab<Left>");
                Assert.Equal(CommandLineEditKind.Command, _controller.CommandLineEditKind);
                ProcessNotation(@"<Esc>");
                Assert.Equal(CommandLineEditKind.None, _controller.CommandLineEditKind);
                Assert.True(_marginControl.IsEditReadOnly);
            }

            [Fact]
            public void HomeKeyMovesToStart()
            {
                Create("");
                ProcessNotation(@":ab<Home>");
                Assert.Equal(1, _marginControl.CommandLineTextBox.CaretIndex);
            }

            [Fact]
            public void LeftKeyMovesBeforeLastCharacter()
            {
                Create("");
                ProcessNotation(@":ab<Left>");
                Assert.Equal(2, _marginControl.CommandLineTextBox.CaretIndex);
            }
        }

        public sealed class LeftKeyInCommandModeTest : CommandLineEditIntegrationTest
        {
            [Fact]
            public void Simple()
            {
                Create("cat", "dog", "fish");
                ProcessNotation(@":e<Left>d<Enter>");
                Assert.Equal("dog", _textBuffer.GetLine(0).GetText());
            }
        }
    }
}
