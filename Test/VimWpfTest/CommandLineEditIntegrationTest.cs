﻿using Vim.EditorHost;
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
            _marginControl.CommandLineTextBox.Text = string.Empty;
            _vimBuffer = CreateVimBuffer(lines);
            _textBuffer = _vimBuffer.TextBuffer;
            _textView = _vimBuffer.TextView;
            _keyboardDevice = new MockKeyboardDevice();

            var parentVisualElement = _factory.Create<FrameworkElement>();

            _controller = new CommandMarginController(
                _vimBuffer,
                parentVisualElement.Object,
                _marginControl,
                VimEditorHost.EditorFormatMapService.GetEditorFormatMap(_vimBuffer.TextView),
                VimEditorHost.ClassificationFormatMapService.GetClassificationFormatMap(_vimBuffer.TextView));
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
            for (var i = 0; i < keyInputList.Count; i++)
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
            [WpfFact]
            public void EscapeKeyExits()
            {
                Create("cat");
                ProcessNotation(@":ab<Left>");
                Assert.Equal(EditKind.Command, _controller.CommandLineEditKind);
                ProcessNotation(@"<Esc>");
                Assert.Equal(EditKind.None, _controller.CommandLineEditKind);
                Assert.True(_marginControl.IsEditReadOnly);
            }

            [WpfFact]
            public void HomeKeyMovesToStart()
            {
                Create("");
                ProcessNotation(@":ab<Home>");
                Assert.Equal(1, _marginControl.CommandLineTextBox.CaretIndex);
            }

            [WpfFact]
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
            [WpfFact]
            public void HandleEnterKey()
            {
                Create("cat", "dog");
                ProcessNotation(@"/og<Left><Left>d");
                var keyEventArgs = _keyboardDevice.CreateKeyEventArgs(KeyInputUtil.EnterKey);
                _controller.HandleKeyEvent(keyEventArgs);
                Assert.True(keyEventArgs.Handled);
            }
        }

        public sealed class ClearTest : CommandLineEditIntegrationTest
        {
            [WpfFact]
            public void ClearCommandEditStart()
            {
                Create();
                ProcessNotation(@":cat<Left><c-u>");
                Assert.Equal(":t", _marginControl.CommandLineTextBox.Text);
                Assert.Equal("t", _vimBuffer.CommandMode.Command);
            }

            [WpfFact]
            public void ClearCommand()
            {
                Create();
                ProcessNotation(@":cat<c-u>");
                Assert.Equal(":", _marginControl.CommandLineTextBox.Text);
                Assert.Equal("", _vimBuffer.CommandMode.Command);
            }

            [WpfFact]
            public void ClearSearch()
            {
                Create();
                ProcessNotation(@"/foo<c-u>");
                Assert.Equal("/", _marginControl.CommandLineTextBox.Text);
                Assert.Equal("", _vimBuffer.IncrementalSearch.CurrentSearchText);
            }

            [WpfFact]
            public void ClearSearchEdit()
            {
                Create();
                ProcessNotation(@"/foo<Left><c-u>");
                Assert.Equal("/o", _marginControl.CommandLineTextBox.Text);
                Assert.Equal("o", _vimBuffer.IncrementalSearch.CurrentSearchText);
            }
        }

        public sealed class CommandModeTest : CommandLineEditIntegrationTest
        {
            [WpfFact]
            public void SingleLeftKey()
            {
                Create("cat", "dog", "fish");
                ProcessNotation(@":e<Left>d<Enter>");
                Assert.Equal("dog", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// If the edit box is cleared it should be changed by to the ':' item
            /// </summary>
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
                [WpfFact]
                public void Simple()
                {
                    Create("cat", "dog", "fish");
                    ProcessNotation(@"/dg<Left>o");
                    Assert.Equal("dog", _incrementalSearch.CurrentSearchData.Pattern);
                }

                [WpfFact]
                public void ClearEditBox()
                {
                    Create("");
                    ProcessNotation(@"/e<Left>");
                    _marginControl.CommandLineTextBox.Text = "";
                    Assert.Equal("/", _marginControl.CommandLineTextBox.Text);
                }

                [WpfFact]
                public void ClearAndRestart()
                {
                    Create("cat", "dog", "fish");
                    ProcessNotation(@"/dg<Left><BS><Del>fish");
                    Assert.Equal("fish", _incrementalSearch.CurrentSearchData.Pattern);
                }

                [WpfFact]
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
                [WpfFact]
                public void EnterToCompleteFind()
                {
                    Create("cat", "dog", "fish");
                    ProcessNotation(@"/og<Home>d<Enter>");
                    Assert.Equal(_textBuffer.GetLine(1).Start, _textView.GetCaretPoint());
                }
            }
        }

        public abstract class PasteTest : CommandLineEditIntegrationTest
        {
            public sealed class PasteFromVimTest : PasteTest
            {
                [WpfFact]
                public void Simple()
                {
                    Create("cat");
                    Vim.RegisterMap.GetRegister('c').UpdateValue("test");
                    ProcessNotation(@":<C-R>c");
                    Assert.Equal(":test", _marginControl.CommandLineTextBox.Text);
                }

                [WpfFact]
                public void InPasteWait()
                {
                    Create("cat");
                    ProcessNotation(@":<C-R>");
                    Assert.True(_controller.InPasteWait);
                }
            }

            public sealed class PasteInEditTest : PasteTest
            {
                [WpfFact]
                public void Simple()
                {
                    Create("cat");
                    Vim.RegisterMap.GetRegister('c').UpdateValue("ca");
                    ProcessNotation(@":t<Left><C-r>c");
                    Assert.Equal(":cat", _marginControl.CommandLineTextBox.Text);
                }

                [WpfFact]
                public void InPasteWait()
                {
                    Create("cat");
                    ProcessNotation(@":t<Left><C-r>");
                    Assert.True(_controller.InPasteWait);
                }

                [WpfFact]
                public void EscapeCancels()
                {
                    Create("cat");
                    ProcessNotation(@":t<Left><C-r><Esc>");
                    Assert.False(_controller.InPasteWait);
                }

                [WpfFact]
                public void PasteStartCaretPosition()
                {
                    Create();
                    ProcessNotation(@":t<Left><C-r>");
                    Assert.Equal(":\"t", _marginControl.CommandLineTextBox.Text);
                    Assert.Equal(1, _marginControl.CommandLineTextBox.SelectionStart);
                }
            }
        }
    }
}
