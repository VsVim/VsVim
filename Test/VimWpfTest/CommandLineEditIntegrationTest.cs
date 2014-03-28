using EditorUtils;
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

        protected virtual void Create(params string[] lines)
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
            var fontProperties = MockObjectFactory.CreateFontProperties("Courier New", 10, _factory);

            var parentVisualElement = _factory.Create<FrameworkElement>();

            _controller = new CommandMarginController(
                _vimBuffer,
                parentVisualElement.Object,
                _marginControl,
                editorFormatMap.Object,
                fontProperties.Object);
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
                Assert.Equal(EditKind.Command, _controller.CommandLineEditKind);
                ProcessNotation(@"<Esc>");
                Assert.Equal(EditKind.None, _controller.CommandLineEditKind);
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

            /// <summary>
            /// Make sure the key event is marked as handled for enter when in the middle of 
            /// an edit.  If it's not marked as handled then it will propagate to the editor
            /// and register as a key stroke
            /// </summary>
            [Fact]
            public void HandleEnterKey()
            {
                Create("cat", "dog");
                ProcessNotation(@"/og<Left><Left>d");
                var keyEventArgs = _keyboardDevice.CreateKeyEventArgs(KeyInputUtil.EnterKey);
                _controller.HandleKeyEvent(keyEventArgs);
                Assert.True(keyEventArgs.Handled);
            }
        }

        public sealed class CommandModeTest : CommandLineEditIntegrationTest
        {
            [Fact]
            public void SingleLeftKey()
            {
                Create("cat", "dog", "fish");
                ProcessNotation(@":e<Left>d<Enter>");
                Assert.Equal("dog", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// If the edit box is cleared it should be changed by to the ':' item
            /// </summary>
            [Fact]
            public void ClearEditBox()
            {
                Create("");
                ProcessNotation(@":e<Left>");
                _marginControl.CommandLineTextBox.Text = "";
                Assert.Equal(":", _marginControl.CommandLineTextBox.Text);
            }

            /// <summary>
            /// Don't let the first character get deleted
            /// </summary>
            [Fact]
            public void DeleteFirstCharacter()
            {
                Create("");
                ProcessNotation(@":e<Left>");
                _marginControl.CommandLineTextBox.Text = "e";
                Assert.Equal(":e", _marginControl.CommandLineTextBox.Text);
            }

            /// <summary>
            /// Previously keys like caused issues because their literal char value was being appended to the
            /// beginning of the edit box
            /// </summary>
            [Fact]
            public void HomeKey()
            {
                Create("");
                ProcessNotation(@":e<Home>b");
                Assert.Equal(":be", _marginControl.CommandLineTextBox.Text);
                Assert.True(_marginControl.IsEditEnabled);
            }

            /// <summary>
            /// The delete key should function to delete text as expected 
            /// </summary>
            [Fact]
            public void DeleteKey()
            {
                Create("");
                ProcessNotation(@":bait<Left><Left><Del>");
                Assert.Equal(":bat", _marginControl.CommandLineTextBox.Text);
                Assert.True(_marginControl.IsEditEnabled);
            }
        }

        public abstract class IncrementalSearchTest : CommandLineEditIntegrationTest
        {
            private IIncrementalSearch _incrementalSearch;

            protected override void Create(params string[] lines)
            {
                base.Create(lines);
                _incrementalSearch = _vimBuffer.IncrementalSearch;
            }

            public sealed class ForwardTest : IncrementalSearchTest
            {
                /// <summary>
                /// The search should be updating as edits are made 
                /// </summary>
                [Fact]
                public void Simple()
                {
                    Create("cat", "dog", "fish");
                    ProcessNotation(@"/dg<Left>o");
                    Assert.Equal("dog", _incrementalSearch.CurrentSearchData.Pattern);
                }

                [Fact]
                public void ClearEditBox()
                {
                    Create("");
                    ProcessNotation(@"/e<Left>");
                    _marginControl.CommandLineTextBox.Text = "";
                    Assert.Equal("/", _marginControl.CommandLineTextBox.Text);
                }

                [Fact]
                public void ClearAndRestart()
                {
                    Create("cat", "dog", "fish");
                    ProcessNotation(@"/dg<Left><BS><Del>fish");
                    Assert.Equal("fish", _incrementalSearch.CurrentSearchData.Pattern);
                }

                [Fact]
                public void BackKeyUpdateText()
                {
                    Create("cat", "dog", "fish");
                    ProcessNotation(@"/u<BS>");
                    Assert.Equal("/", _marginControl.CommandLineTextBox.Text);
                }

                /// <summary>
                /// When the Enter key is run it should complete the search and cause the cursor
                /// to be placed at the start of the successful find
                /// </summary>
                [Fact]
                public void EnterToCompleteFind()
                {
                    Create("cat", "dog", "fish");
                    ProcessNotation(@"/og<Home>d<Enter>");
                    Assert.Equal(_textBuffer.GetLine(1).Start, _textView.GetCaretPoint());
                }
            }
        }
    }
}
