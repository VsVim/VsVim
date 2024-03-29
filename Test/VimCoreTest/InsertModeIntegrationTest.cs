﻿using System;
using Vim.EditorHost;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Vim.Extensions;
using Vim.Modes.Insert;
using Xunit;
using Vim.UnitTest;
using System.Linq;

namespace Vim.UnitTest
{
    /// <summary>
    /// Used to test the integrated behavior if Insert Mode 
    /// </summary>
    public abstract class InsertModeIntegrationTest : VimTestBase
    {
        private IVimBuffer _vimBuffer;
        private IWpfTextView _textView;
        private ITextBuffer _textBuffer;
        private IVimGlobalSettings _globalSettings;
        private IVimLocalSettings _localSettings;
        private TestableMouseDevice _testableMouseDevice;
        private Register _register;
        private IInsertMode _insertMode;
        private InsertMode _insertModeRaw;

        protected void Create(params string[] lines)
        {
            Create(ModeArgument.None, lines);
        }

        protected virtual void Create(ModeArgument argument, params string[] lines)
        {
            _textView = CreateTextView(lines);
            _textBuffer = _textView.TextBuffer;
            _vimBuffer = Vim.CreateVimBuffer(_textView);
            _vimBuffer.SwitchMode(ModeKind.Insert, argument);
            _insertMode = _vimBuffer.InsertMode;
            _insertModeRaw = (InsertMode)_insertMode;
            _register = Vim.RegisterMap.GetRegister('c');
            _globalSettings = Vim.GlobalSettings;
            _localSettings = _vimBuffer.LocalSettings;

            _testableMouseDevice = (TestableMouseDevice)MouseDevice;
            _testableMouseDevice.IsLeftButtonPressed = false;
            _testableMouseDevice.Point = null;
            _testableMouseDevice.YOffset = 0;

        }

        public override void Dispose()
        {
            _testableMouseDevice.IsLeftButtonPressed = false;
            _testableMouseDevice.Point = null;
            _testableMouseDevice.YOffset = 0;
            base.Dispose();
        }

        public sealed class InsertCharacterAboveTest : InsertModeIntegrationTest
        {
            [WpfFact]
            public void Simple()
            {
                Create("cat", "dog");
                _textView.MoveCaretToLine(1);
                _vimBuffer.ProcessNotation("<C-y>");
                Assert.Equal("cdog", _textBuffer.GetLine(1).GetText());
            }

            [WpfFact]
            public void Multiple()
            {
                Create("cat", "dog");
                _textView.MoveCaretToLine(1);
                for (var i = 0; i < 3; i++)
                {
                    _vimBuffer.ProcessNotation("<C-y>");
                }
                Assert.Equal("catdog", _textBuffer.GetLine(1).GetText());
            }

            [WpfFact]
            public void NothingAbove()
            {
                Create("", "dog");
                _textView.MoveCaretToLine(1);
                _vimBuffer.ProcessNotation("<C-y>");
                Assert.Equal(1, VimHost.BeepCount);
            }

            [WpfFact]
            public void FirstLine()
            {
                Create("", "dog");
                _vimBuffer.ProcessNotation("<C-y>");
                Assert.Equal(1, VimHost.BeepCount);
            }

            [WpfFact]
            public void UnicodeCharacter()
            {
                const string alien = "\U0001F47D"; // 👽
                Create($"{alien}{alien}cat", "");
                _textView.MoveCaretToLine(1);
                _vimBuffer.ProcessNotation("<C-y>");
                Assert.Equal($"{alien}", _textBuffer.GetLineText(1));
                Assert.Equal(_textBuffer.GetPointInLine(line: 1, column: 2), _textView.GetCaretPoint());
                _vimBuffer.ProcessNotation("<C-y>");
                Assert.Equal($"{alien}{alien}", _textBuffer.GetLineText(1));
                Assert.Equal(_textBuffer.GetPointInLine(line: 1, column: 4), _textView.GetCaretPoint());
            }
        }

        public sealed class InsertCharacterBelowTest : InsertModeIntegrationTest
        {
            [WpfFact]
            public void Simple()
            {
                Create("cat", "dog");
                _vimBuffer.ProcessNotation("<C-e>");
                Assert.Equal("dcat", _textBuffer.GetLine(0).GetText());
            }

            [WpfFact]
            public void Multiple()
            {
                Create("cat", "dog");
                for (var i = 0; i < 3; i++)
                {
                    _vimBuffer.ProcessNotation("<C-e>");
                }
                Assert.Equal("dogcat", _textBuffer.GetLine(0).GetText());
            }

            [WpfFact]
            public void NothingBelow()
            {
                Create("cat", "");
                _vimBuffer.ProcessNotation("<C-e>");
                Assert.Equal(1, VimHost.BeepCount);
            }

            [WpfFact]
            public void LastLine()
            {
                Create("cat", "");
                _textView.MoveCaretToLine(1);
                _vimBuffer.ProcessNotation("<C-e>");
                Assert.Equal(1, VimHost.BeepCount);
            }
        }

        public sealed class KeyMappingTest : InsertModeIntegrationTest
        {
            /// <summary>
            /// Make sure that in the case where there is buffered input and we fail at the mapping 
            /// that both values are inserted into the ITextBuffer
            /// </summary>
            [WpfFact]
            public void BufferedInputFailsMapping()
            {
                Create("");
                _vimBuffer.Vim.GlobalKeyMap.AddKeyMapping("jj", "<Esc>", allowRemap: false, KeyRemapMode.Insert);
                _vimBuffer.Process("j");
                Assert.Equal("", _textBuffer.GetLine(0).GetText());
                _vimBuffer.Process("a");
                Assert.Equal("ja", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// Ensure we can use a double keystroke to escape
            /// </summary>
            [WpfFact]
            public void TwoKeysToEscape()
            {
                Create(ModeArgument.NewInsertWithCount(2), "hello");
                _vimBuffer.Vim.GlobalKeyMap.AddKeyMapping("jj", "<Esc>", allowRemap: false, KeyRemapMode.Insert);
                _vimBuffer.Process("jj");
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// The Escape should end a multiple key mapping and exit insert mode
            /// </summary>
            [WpfFact]
            public void TwoKeys_EscapeToEndSequence()
            {
                Create("hello world", "");
                _textView.MoveCaretToLine(1);
                _vimBuffer.Vim.GlobalKeyMap.AddKeyMapping(";;", "<Esc>", allowRemap: false, KeyRemapMode.Insert);
                _vimBuffer.Process(';');
                _vimBuffer.Process(VimKey.Escape);
                Assert.Equal(";", _textBuffer.GetLine(1).GetText());
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// The CTRL-[ should end a multiple key mapping the same as normal Escape
            /// </summary>
            [WpfFact]
            public void TwoKeys_AlternateEscapeToEndSequence()
            {
                Create("hello world", "");
                _textView.MoveCaretToLine(1);
                _vimBuffer.Vim.GlobalKeyMap.AddKeyMapping(";;", "<Esc>", allowRemap: false, KeyRemapMode.Insert);
                _vimBuffer.Process(';');
                _vimBuffer.Process(KeyInputUtil.CharWithControlToKeyInput('['));
                Assert.Equal(";", _textBuffer.GetLine(1).GetText());
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// Spaces need to be allowed in the target mapping
            /// </summary>
            [WpfFact]
            public void SpacesInTarget()
            {
                Create("");
                _vimBuffer.Process(VimKey.Escape);
                _vimBuffer.Process(":imap cat hello world", enter: true);
                _vimBuffer.Process("icat");
                Assert.Equal("hello world", _textBuffer.GetLine(0).GetText());
            }

            [WpfFact]
            public void DoubleQuotesInRight()
            {
                Create("");
                _vimBuffer.Process(VimKey.Escape);
                _vimBuffer.Process(@":imap d ""hey""", enter: true);
                _vimBuffer.Process("id");
                Assert.Equal(@"""hey""", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// Make sure that we properly don't cause recursion in the scenario where a 
            /// noremap mapping refers back to itself in the non-0 position 
            /// </summary>
            [WpfFact]
            public void RecursiveInNoRemap()
            {
                Create("");
                _vimBuffer.Process(VimKey.Escape);
                _vimBuffer.Process(@":inoremap x axa", enter: true);
                _vimBuffer.Process("ix");
                Assert.Equal("axa", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// When a &lt; or &gt; tag is not a special name it should be interpreted character
            /// by character 
            /// </summary>
            [WpfFact]
            public void TagNotASpecialName()
            {
                Create("");
                _vimBuffer.Process(VimKey.Escape);
                _vimBuffer.Process(@":inoremap a <dest>", enter: true);
                _vimBuffer.Process("ia");
                Assert.Equal("<dest>", _textBuffer.GetLine(0).GetText());
            }

            [WpfFact]
            public void UnmatchedLessThan()
            {
                Create("");
                _vimBuffer.Process(VimKey.Escape);
                _vimBuffer.Process(@":inoremap a <<s-a>", enter: true);
                _vimBuffer.Process("ia");
                Assert.Equal("<A", _textBuffer.GetLine(0).GetText());
            }

            [WpfFact]
            public void Issue1059()
            {
                Create("");
                _vimBuffer.Process(VimKey.Escape);
                _vimBuffer.Process(@":imap /v VARCHAR(MAX) = '<%= ""test"" %>'", enter: true);
                _vimBuffer.Process("i/v");
                Assert.Equal(@"VARCHAR(MAX) = '<%= ""test"" %>'", _textBuffer.GetLine(0).GetText());
            }

            [WpfFact]
            public void Issue1812()
            {
                Create("dog");
                _vimBuffer.Process(VimKey.Escape);
                _vimBuffer.Process(@":imap ;; <end>;<cr>", enter: true);
                _vimBuffer.Process("i;;");
                Assert.Equal(new[] { "dog;", "" }, _textBuffer.GetLines());
                Assert.Equal(_textBuffer.GetLine(1).Start, _textView.GetCaretPoint());
            }
        }

        public sealed class PasteTest : InsertModeIntegrationTest
        {
            [WpfFact]
            public void Simple()
            {
                Create("world");
                _register.UpdateValue("hello ");
                _vimBuffer.ProcessNotation("<C-R>c");
                Assert.Equal("hello world", _textBuffer.GetLine(0).GetText());
                Assert.Equal(6, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Test out the literal pasting with RR 
            /// </summary>
            [WpfFact]
            public void SpecialLiteralAndFormatting()
            {
                Create("world");
                _register.UpdateValue("hello ");
                _vimBuffer.ProcessNotation("<C-R><C-R>c");
                Assert.Equal("hello world", _textBuffer.GetLine(0).GetText());
                Assert.Equal(6, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Test out the literal + no indent pasting with RO
            /// </summary>
            [WpfFact]
            public void SpecialLiteralAndNoIndent()
            {
                Create("world");
                _register.UpdateValue("hello ");
                _vimBuffer.ProcessNotation("<C-R><C-O>c");
                Assert.Equal("hello world", _textBuffer.GetLine(0).GetText());
                Assert.Equal(6, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Test out the literal + indent pasting with RO
            /// </summary>
            [WpfFact]
            public void SpecialLiteralAndIndent()
            {
                Create("world");
                _register.UpdateValue("hello ");
                _vimBuffer.ProcessNotation("<C-R><C-P>c");
                Assert.Equal("hello world", _textBuffer.GetLine(0).GetText());
                Assert.Equal(6, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Linewise should be inserted literally.  Unlike a normal mode paste the new line isn't
            /// inserted before the text.  Instead it's inserted after which is exactly how it's stored
            /// in the register
            /// </summary>
            [WpfFact]
            public void Linewise()
            {
                Create("dog");
                _register.UpdateValue("cat" + Environment.NewLine, OperationKind.LineWise);
                _vimBuffer.ProcessNotation("<C-R>c");
                Assert.Equal(
                    new[] { "cat", "dog" },
                    _textBuffer.GetLines());
                Assert.Equal(_textBuffer.GetLine(1).Start, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// The Esc key should only leave the paste part of the operation and not leave insert 
            /// mode itself
            /// </summary>
            [WpfFact]
            public void EscapeShouldStayInInsert()
            {
                Create("dog");
                _vimBuffer.ProcessNotation("<C-R><Esc>");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// Make sure that the line endings are normalized on the paste operation
            /// </summary>
            [WpfFact]
            public void NormalizeLineEndings()
            {
                Create("cat", "dog");
                RegisterMap.GetRegister('c').UpdateValue("fish\ntree\n", OperationKind.LineWise);
                _vimBuffer.ProcessNotation("<C-R>c");
                Assert.Equal(
                    new[] { "fish", "tree", "cat", "dog" },
                    _textBuffer.GetLines());
                for (var i = 0; i < 3; i++)
                {
                    Assert.Equal(Environment.NewLine, _textBuffer.GetLine(i).GetLineBreakText());
                }
            }

            [WpfFact]
            public void IsInPaste_Normal()
            {
                Create("");
                _insertMode.ProcessNotation("<C-R>");
                Assert.True(_insertMode.IsInPaste);
                Assert.Equal(FSharpOption<char>.Some('"'), _insertMode.PasteCharacter);
            }

            [WpfFact]
            public void IsInPaste_Special()
            {
                Create();
                var all = new[] { "R", "O", "P" };
                foreach (var suffix in all)
                {
                    var command = $"<C-R><C-{suffix}>";
                    _insertMode.ProcessNotation(command);
                    Assert.True(_insertMode.IsInPaste);
                    Assert.Equal(FSharpOption<char>.Some('"'), _insertMode.PasteCharacter);
                    _insertMode.ProcessNotation("<Esc>");
                }
            }

            [WpfFact]
            public void IsInPaste_Digraph1()
            {
                Create("");
                _insertMode.ProcessNotation("<C-k>");
                Assert.True(_insertMode.IsInPaste);
                Assert.Equal(FSharpOption<char>.Some('?'), _insertMode.PasteCharacter);
            }

            [WpfFact]
            public void IsInPaste_Digraph2()
            {
                Create("");
                _insertMode.ProcessNotation("<C-k>e");
                Assert.True(_insertMode.IsInPaste);
                Assert.Equal(FSharpOption<char>.Some('e'), _insertMode.PasteCharacter);
            }
        }

        public sealed class DigraphTest : InsertModeIntegrationTest
        {
            public DigraphTest()
            {
                Vim.AutoLoadDigraphs = true;
            }

            [WpfFact]
            public void Simple()
            {
                Create("", "");
                _vimBuffer.ProcessNotation("<C-k>e:");
                Assert.Equal("ë", _textBuffer.GetLine(0).GetText());
                Assert.Equal(1, _textView.GetCaretPoint().Position);
            }

            [WpfFact]
            public void Swapped()
            {
                Create("", "");
                _vimBuffer.ProcessNotation("<C-k>:e");
                Assert.Equal("ë", _textBuffer.GetLine(0).GetText());
                Assert.Equal(1, _textView.GetCaretPoint().Position);
            }

            [WpfFact]
            public void Special()
            {
                Create("", "");
                _vimBuffer.ProcessNotation("<C-k><Right>");
                Assert.Equal("<Right>", _textBuffer.GetLine(0).GetText());
                Assert.Equal(7, _textView.GetCaretPoint().Position);
            }

            [WpfFact]
            public void Undefined()
            {
                Create("", "");
                _vimBuffer.ProcessNotation("<C-k>AB");
                Assert.Equal("B", _textBuffer.GetLine(0).GetText());
                Assert.Equal(1, _textView.GetCaretPoint().Position);
            }

            [WpfFact]
            public void Space()
            {
                Create("", "");
                _vimBuffer.ProcessNotation("<C-k><Space>a");
                Assert.Equal("á", _textBuffer.GetLine(0).GetText());
                Assert.Equal(1, _textView.GetCaretPoint().Position);
            }

            [WpfFact]
            public void Escape1()
            {
                Create("", "");
                _vimBuffer.ProcessNotation("<C-k><Esc>");
                Assert.Equal("", _textBuffer.GetLine(0).GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
                Assert.False(_vimBuffer.InsertMode.IsInPaste);
            }

            [WpfFact]
            public void Escape2()
            {
                Create("", "");
                _vimBuffer.ProcessNotation("<C-k>e<Esc>");
                Assert.Equal("", _textBuffer.GetLine(0).GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
                Assert.False(_vimBuffer.InsertMode.IsInPaste);
            }

            [WpfFact]
            public void NoInline()
            {
                Create("", "");
                _vimBuffer.ProcessNotation("e<BS>:");
                Assert.Equal(":", _textBuffer.GetLine(0).GetText());
                Assert.Equal(1, _textView.GetCaretPoint().Position);
            }

            [WpfFact]
            public void Inline()
            {
                Create("", "");
                _globalSettings.Digraph = true;
                _vimBuffer.ProcessNotation("e<BS>:");
                Assert.Equal("ë", _textBuffer.GetLine(0).GetText());
                Assert.Equal(1, _textView.GetCaretPoint().Position);
            }

            [WpfFact]
            public void InlineSwapped()
            {
                Create("", "");
                _globalSettings.Digraph = true;
                _vimBuffer.ProcessNotation(":<BS>e");
                Assert.Equal("ë", _textBuffer.GetLine(0).GetText());
                Assert.Equal(1, _textView.GetCaretPoint().Position);
            }

            [WpfFact]
            public void AsIfTyped()
            {
                Create("", "");
                _globalSettings.Digraph = true;
                RegisterMap.GetRegister('c').UpdateValue("e", OperationKind.CharacterWise);
                _vimBuffer.ProcessNotation("<C-r>c<BS>:");
                Assert.Equal("ë", _textBuffer.GetLine(0).GetText());
                Assert.Equal(1, _textView.GetCaretPoint().Position);
            }
        }

        /// <summary>
        /// Tests for inserting previously inserted text
        /// </summary>
        public sealed class PreviouslyInsertedTextTest : InsertModeIntegrationTest
        {
            [WpfFact]
            public void StopInsertMode()
            {
                Create("world", "");
                _vimBuffer.Vim.VimData.LastTextInsert = FSharpOption<string>.Some("hello ");
                _vimBuffer.ProcessNotation("<C-@>");
                Assert.Equal("hello world", _textBuffer.GetLine(0).GetText());
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                Assert.Equal(5, _textView.GetCaretPoint().Position);
            }

            [WpfFact]
            public void DontStopInsertMode()
            {
                Create("world", "");
                _vimBuffer.Vim.VimData.LastTextInsert = FSharpOption<string>.Some("hello ");
                _vimBuffer.ProcessNotation("<C-a>");
                Assert.Equal("hello world", _textBuffer.GetLine(0).GetText());
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal(6, _textView.GetCaretPoint().Position);
            }
        }

        /// <summary>
        /// Tests for the '.' register
        /// </summary>
        public sealed class LastTextRegisterTest : InsertModeIntegrationTest
        {
            protected override void Create(ModeArgument argument, params string[] lines)
            {
                base.Create(argument, lines);
                _localSettings.EndOfLine = false;
            }

            [WpfFact]
            public void SimpleWord()
            {
                Create("");
                _vimBuffer.ProcessNotation("dog<Esc>");
                Assert.Equal("dog", RegisterMap.GetRegisterText('.'));
            }

            [WpfFact]
            public void WordsWithSpaces()
            {
                Create("");
                _vimBuffer.ProcessNotation("dog tree<Esc>");
                Assert.Equal("dog tree", RegisterMap.GetRegisterText('.'));
            }

            [WpfFact]
            public void CaretMove()
            {
                Create("");
                _vimBuffer.ProcessNotation("dog");
                _textView.MoveCaretTo(2);
                _vimBuffer.ProcessNotation("<Esc>");
                Assert.Equal("dog", RegisterMap.GetRegisterText('.'));
            }

            /// <summary>
            /// Once the user moves the caret outside the active region and
            /// typing starts again then the register resets
            /// </summary>
            [WpfFact]
            public void TypeAfterCaretMove()
            {
                Create("cat");
                _textView.SetVisibleLineCount(1);
                _vimBuffer.ProcessNotation("dog");
                _testableMouseDevice.Point = _textView.GetPoint(5);
                _vimBuffer.ProcessNotation("<LeftMouse><LeftRelease>");
                _vimBuffer.ProcessNotation("t<Esc>");
                Assert.Equal("t", RegisterMap.GetRegisterText('.'));
            }

            /// <summary>
            /// Even if the user moves the caret back to the original position
            /// the new typing action breaks the repeat text
            /// </summary>
            [WpfFact]
            public void TypeAfterCaretMoveBack()
            {
                Create("");
                _textView.SetVisibleLineCount(1);
                _vimBuffer.ProcessNotation("dog");
                _testableMouseDevice.Point = _textView.GetPoint(2);
                _vimBuffer.ProcessNotation("<LeftMouse><LeftRelease>");
                _testableMouseDevice.Point = _textView.GetPoint(3);
                _vimBuffer.ProcessNotation("<LeftMouse><LeftRelease>");
                _vimBuffer.ProcessNotation("s<Esc>");
                Assert.Equal("dogs", _textBuffer.GetLine(0).GetText());
                Assert.Equal("s", RegisterMap.GetRegisterText('.'));
            }

            [WpfFact]
            public void AccrossMultipleLines()
            {
                Create("");
                _vimBuffer.ProcessNotation("dog<CR>cat<Esc>");
                Assert.Equal("dog" + Environment.NewLine + "cat", RegisterMap.GetRegisterText('.'));
            }
        }

        /// <summary>
        /// Test the behavior of the '^ and `^ motion in insert mode
        /// </summary>
        public sealed class LastCaretEditMarkTest : InsertModeIntegrationTest
        {
            [WpfFact]
            public void Simple()
            {
                Create("cat", "dog", "fish");
                _textView.MoveCaretToLine(1);
                _vimBuffer.ProcessNotation("big <Esc>");
                _textView.MoveCaretToLine(0);
                _vimBuffer.ProcessNotation("'^");
                Assert.Equal(_textBuffer.GetLine(1).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// When using the `^ marker make sure we go to the exact caret position of the 
            /// last edit.  This is the position before insert mode moved it one to the left
            /// as a result of hitting Esc
            /// </summary>
            [WpfFact]
            public void SimpleExact()
            {
                Create("cat", "dog", "fish");
                _textView.MoveCaretToLine(1);
                _vimBuffer.ProcessNotation("big <Esc>");
                _textView.MoveCaretToLine(0);
                _vimBuffer.ProcessNotation("`^");
                Assert.Equal(_textBuffer.GetPointInLine(1, 4), _textView.GetCaretPoint());
            }

            /// <summary>
            /// When a line is added above the '^ marker needs to move down a line and follow
            /// it
            /// </summary>
            [WpfFact]
            public void AddLineAbove()
            {
                Create("cat", "dog", "fish");
                _textView.MoveCaretToLine(1);
                _vimBuffer.ProcessNotation("big <Esc>");
                UnnamedRegister.UpdateValue("tree" + Environment.NewLine, OperationKind.LineWise);
                _textView.MoveCaretToLine(0);
                _vimBuffer.ProcessNotation("p");
                _vimBuffer.ProcessNotation("'^");
                Assert.Equal(_textBuffer.GetLine(2).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Adding a line below the '^ mark shouldn't be affected by a line that is added below
            /// it
            /// </summary>
            [WpfFact]
            public void AddLineBelow()
            {
                Create("cat", "dog", "fish");
                _textView.MoveCaretToLine(1);
                _vimBuffer.ProcessNotation("big <Esc>");
                UnnamedRegister.UpdateValue("tree" + Environment.NewLine, OperationKind.LineWise);
                _textView.MoveCaretToLine(2);
                _vimBuffer.ProcessNotation("p");
                _vimBuffer.ProcessNotation("'^");
                Assert.Equal(_textBuffer.GetLine(1).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// When an edit occurs to the last edit line the column position should be unaffected.  This
            /// stands in contracts to editting lines which does pay attention to tracking the offsets
            /// </summary>
            [WpfFact]
            public void EditTheEditLine()
            {
                Create("cat", "dog", "fish");
                _textView.MoveCaretToLine(1);
                _vimBuffer.ProcessNotation("big <Esc>");
                UnnamedRegister.UpdateValue("cat ", OperationKind.CharacterWise);
                _textView.MoveCaretToLine(1);
                _vimBuffer.ProcessNotation("P");
                _textView.MoveCaretToLine(2);
                _vimBuffer.ProcessNotation("`^");
                Assert.Equal(_textBuffer.GetPointInLine(1, 4), _textView.GetCaretPoint());
            }

            /// <summary>
            /// When the edit line is shrunk so that the tracked caret position isn't possible on that line
            /// anymore it should just put it in the last possible position
            /// </summary>
            [WpfFact]
            public void ClearTheEditLine()
            {
                Create("cat", "dog", "fish");
                _textView.MoveCaretToLine(1);
                _vimBuffer.ProcessNotation("big<Esc>");
                _textView.MoveCaretToLine(1);
                _vimBuffer.ProcessNotation("dw");
                _textView.MoveCaretToLine(2);
                _vimBuffer.ProcessNotation("`^");
                Assert.Equal(_textBuffer.GetPointInLine(1, 0), _textView.GetCaretPoint());
            }

            /// <summary>
            /// The last edit line is shared across different IVimBuffer instances
            /// </summary>
            [WpfFact]
            public void SharedAcrossBuffers()
            {
                Create("cat", "dog", "fish");
                _textView.MoveCaretToLine(1);
                _vimBuffer.ProcessNotation("big <Esc>");

                var textViewRoleSet = TextEditorFactoryService.CreateTextViewRoleSet(
                    PredefinedTextViewRoles.PrimaryDocument,
                    PredefinedTextViewRoles.Document,
                    PredefinedTextViewRoles.Interactive,
                    PredefinedTextViewRoles.Editable);
                var altTextView = TextEditorFactoryService.CreateTextView(_textBuffer, textViewRoleSet);
                var altVimBuffer = CreateVimBuffer(CreateVimBufferData(altTextView));
                altVimBuffer.ProcessNotation("'^");
                Assert.Equal(_textBuffer.GetLine(1).Start, altTextView.GetCaretPoint());
                altVimBuffer.Close();
            }

            /// <summary>
            /// The ^ mark doesn't survive deletes of the line that contains it
            /// </summary>
            [WpfFact]
            public void DoesntSurviveDeletes()
            {
                Create("cat", "dog", "fish");
                _textView.MoveCaretToLine(1);
                _vimBuffer.ProcessNotation("big <Esc>dd");
                Assert.True(_vimBuffer.VimTextBuffer.LastInsertExitPoint.IsNone());
            }
        }

        public sealed class LastEditPointTest : InsertModeIntegrationTest
        {
            private FSharpOption<SnapshotPoint> LastEditPoint
            {
                get { return _vimBuffer.VimTextBuffer.LastEditPoint; }
            }

            /// <summary>
            /// The `. mark should go to the last edit position on the last edit line
            /// </summary>
            [WpfFact]
            public void GoToLastEditPosition()
            {
                Create("cat", "dog");
                _textView.MoveCaretToLine(1, 3);
                _vimBuffer.ProcessNotation(" bird<Esc>");
                _textView.MoveCaretToLine(0, 0);

                Assert.Equal("dog bird", _textView.GetLine(1).GetText());

                Assert.Equal(0, _textView.GetCaretColumn().ColumnNumber);
                Assert.Equal(0, _textView.GetCaretLine().LineNumber);

                _vimBuffer.ProcessNotation("`.");

                Assert.Equal(7, _textView.GetCaretColumn().ColumnNumber);
                Assert.Equal(1, _textView.GetCaretLine().LineNumber);
            }

            /// <summary>
            /// When text is inserted into the buffer then the last edit point should be the
            /// last character that was inserted
            /// </summary>
            [WpfFact]
            public void InsertText()
            {
                Create("cat");
                _vimBuffer.ProcessNotation("big ");
                Assert.Equal(3, LastEditPoint.Value);
            }

            /// <summary>
            /// The behavior of an insert that doesn't come from Vim isn't defined but we choose 
            /// to interpret it as a normal insert of text and update the LastEditPoint as if it
            /// were a Vim based edit
            /// </summary>
            [WpfFact]
            public void InsertNonVim()
            {
                Create("big");
                _textBuffer.Insert(3, " cat");
                Assert.Equal(6, LastEditPoint.Value);
            }

            /// <summary>
            /// Make sure external multi-part edits don't clear the last edit point
            /// </summary>
            [WpfFact]
            public void InsertNonVim_MultiPart()
            {
                // Reported in issue #2440.
                Create("big", "bad", "");
                _textBuffer.Insert(3, " cat");
                Assert.Equal(new[] { "big cat", "bad", "", }, _textBuffer.GetLines());
                Assert.Equal(6, LastEditPoint.Value);
                using (var edit = _textBuffer.CreateEdit())
                {
                    edit.Insert(_textBuffer.GetPointInLine(0, 0), "foo ");
                    edit.Insert(_textBuffer.GetPointInLine(1, 0), "bar ");
                    edit.Apply();
                }
                Assert.Equal(new[] { "foo big cat", "bar bad", "", }, _textBuffer.GetLines());
                Assert.Equal(6, LastEditPoint.Value);
            }

            /// <summary>
            /// When there is a deletion of text then the LastEditPoint should point to the start
            /// of the deleted text
            /// </summary>
            [WpfFact]
            public void DeleteText()
            {
                Create("dog tree");
                _textView.MoveCaretTo(1);
                _vimBuffer.ProcessNotation("<Del>");
                Assert.Equal(1, LastEditPoint.Value);
            }

            /// <summary>
            /// As we do with insert we treat a non-Vim delete as a Vim delete
            /// </summary>
            [WpfFact]
            public void DeleteNonVim()
            {
                Create("a big dog");
                _textBuffer.Delete(new Span(2, 3));
                Assert.Equal(2, LastEditPoint.Value);
            }

            [WpfFact]
            public void MiddleOfLine()
            {
                Create("cat", "dg", "fish");
                _textView.MoveCaretToLine(1, 1);
                _vimBuffer.ProcessNotation("o<Esc>");
                _textView.MoveCaretToLine(0);
                _vimBuffer.ProcessNotation("`.");
                Assert.Equal(_textBuffer.GetPointInLine(1, 1), _textView.GetCaretPoint());
            }

            [WpfFact]
            public void BeginningOfLine()
            {
                Create("cat", "og", "fish");
                _textView.MoveCaretToLine(1, 0);
                _vimBuffer.ProcessNotation("d<Esc>");
                _textView.MoveCaretToLine(0);
                _vimBuffer.ProcessNotation("`.");
                Assert.Equal(_textBuffer.GetPointInLine(1, 0), _textView.GetCaretPoint());
            }

            [WpfFact]
            public void TypingCompleteWord()
            {
                Create("cat", "", "fish");
                _textView.MoveCaretToLine(1, 0);
                _vimBuffer.ProcessNotation("dog<Esc>");
                _textView.MoveCaretToLine(0);
                _vimBuffer.ProcessNotation("`.");
                Assert.Equal(_textBuffer.GetPointInLine(1, 2), _textView.GetCaretPoint());
            }

            [WpfFact]
            public void DeleteLineContainingLastEditPoint()
            {
                Create("cat", "", "fish");
                _textView.MoveCaretToLine(1, 0);
                _vimBuffer.ProcessNotation("dog<Esc>ddk`.");
                Assert.Equal(_textBuffer.GetPointInLine(1, 0), _textView.GetCaretPoint());
            }

            [WpfFact]
            public void DeleteManyLineContainingLastEditPoint()
            {
                Create("pig", "cat", "", "fish", "tree");
                _textView.MoveCaretToLine(2, 0);
                _vimBuffer.ProcessNotation("dog<Esc>k2dd`.");
                Assert.Equal(_textBuffer.GetPointInLine(1, 0), _textView.GetCaretPoint());
            }
        }

        public sealed class ShiftLineRight : InsertModeIntegrationTest
        {
            [WpfFact]
            public void Simple()
            {
                Create("cat", "dog");
                _localSettings.ShiftWidth = 4;
                _vimBuffer.ProcessNotation("<C-t>");
                Assert.Equal("    cat", _textBuffer.GetLine(0).GetText());
                Assert.Equal(4, _textView.GetCaretPoint().Position);
            }

            [WpfFact]
            public void CustomShift()
            {
                Create("cat", "dog");
                _localSettings.ShiftWidth = 2;
                _vimBuffer.ProcessNotation("<C-t>");
                Assert.Equal("  cat", _textBuffer.GetLine(0).GetText());
                Assert.Equal(2, _textView.GetCaretPoint().Position);
            }
        }

        public abstract class BackspacingTest : InsertModeIntegrationTest
        {
            public sealed class BackspaceOverCharTest : BackspacingTest
            {
                /// <summary>
                /// Make sure backspace over char at start without
                /// 'backspace=start' works
                /// </summary>
                [WpfFact]
                public void NoStart()
                {
                    Create("cat dog");
                    _globalSettings.Backspace = "";
                    _textView.MoveCaretTo(4);
                    _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<BS>"));
                    Assert.Equal("cat dog", _textBuffer.GetLine(0).GetText());
                }

                /// <summary>
                /// Make sure backspace over char at start with 'backspace=start'
                /// works
                /// </summary>
                [WpfFact]
                public void Start()
                {
                    Create("cat dog");
                    _globalSettings.Backspace = "start";
                    _textView.MoveCaretTo(4);
                    _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<BS>"));
                    Assert.Equal("catdog", _textBuffer.GetLine(0).GetText());
                }

                /// <summary>
                /// Make sure backspace over char at indent without
                /// 'backspace=indent' works
                /// </summary>
                [WpfFact]
                public void NoIndent()
                {
                    Create("    dog");
                    _globalSettings.Backspace = "start";
                    _textView.MoveCaretTo(4);
                    _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<BS>"));
                    Assert.Equal("   dog", _textBuffer.GetLine(0).GetText());
                }

                /// <summary>
                /// Make sure backspace over char at indent with
                /// 'backspace=indent' works
                /// </summary>
                [WpfFact]
                public void Indent()
                {
                    Create("    dog");
                    _globalSettings.Backspace = "start,indent";
                    _textView.MoveCaretTo(4);
                    _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<BS>"));
                    Assert.Equal("   dog", _textBuffer.GetLine(0).GetText());
                }

                /// <summary>
                /// Make sure backspace over char at beginning of line without
                /// 'backspace=eol' works
                /// </summary>
                [WpfFact]
                public void NoEol()
                {
                    Create("cat", "dog");
                    _globalSettings.Backspace = "start,indent";
                    _textView.MoveCaretTo(5);
                    _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<BS>"));
                    Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                }

                /// <summary>
                /// Make sure backspace over char at beginning of line with
                /// 'backspace=eol' works
                /// </summary>
                [WpfFact]
                public void Eol()
                {
                    Create("cat", "dog");
                    _globalSettings.Backspace = "start,indent,eol";
                    _textView.MoveCaretTo(5);
                    _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<BS>"));
                    Assert.Equal("catdog", _textBuffer.GetLine(0).GetText());
                }

                /// <summary>
                /// Make sure backspace over char from virtual space works
                /// 'backspace=eol' works
                /// </summary>
                [WpfFact]
                public void FromVirtualSpace()
                {
                    Create("  hello", "world");
                    _globalSettings.Backspace = "start,indent,eol";
                    _localSettings.AutoIndent = true;
                    _textView.MoveCaretTo(_textView.GetLine(0).End);
                    _vimBuffer.ProcessNotation("<Enter><BS>");
                    Assert.Equal("  hello", _textView.GetLine(0).GetText());
                    Assert.Equal(" ", _textView.GetLine(1).GetText());
                }

                /// <summary>
                /// A repeat of a backspace operation will perform a check on the 'backspace' option
                /// </summary>
                [WpfFact]
                public void RepeatRechecksBackspaceOption()
                {
                    Create("cats");
                    _textView.MoveCaretTo(3);
                    _globalSettings.Backspace = "start";
                    _vimBuffer.Process(VimKey.Back, VimKey.Escape);
                    Assert.Equal("cas", _textBuffer.GetLine(0).GetText());
                    _globalSettings.Backspace = "";
                    _vimBuffer.Process(".");
                    Assert.Equal("cas", _textBuffer.GetLine(0).GetText());
                    Assert.Equal(1, VimHost.BeepCount);
                }


                /// <summary>
                /// Ensure the check of `backspace` in a repeat operation correctly considers the edits
                /// in progress. 
                ///
                /// Issue #1532
                /// </summary>
                [WpfFact]
                public void RepeatRechecksBackspaceOptionWithExtraInsert()
                {
                    Create("cat", "cat");
                    _globalSettings.Backspace = "";
                    _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                    _textView.MoveCaretTo(_textBuffer.GetPointInLine(0, 1));
                    _vimBuffer.ProcessNotation("ceolf<BS>d<Esc>");
                    Assert.Equal("cold", _textBuffer.GetLine(0).GetText());
                    _textView.MoveCaretTo(_textBuffer.GetPointInLine(1, 1));
                    _vimBuffer.ProcessNotation(".");
                    Assert.Equal("cold", _textBuffer.GetLine(1).GetText());
                    Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                }

                /// <summary>
                /// If the caret moves from anything other than an edit operation in insert mode it resets the 
                /// start point to the new position of the caret 
                /// </summary>
                [WpfFact]
                public void CaretChangeSameLine()
                {
                    Create("");
                    _globalSettings.Backspace = "";
                    _vimBuffer.Process("cat dog");
                    _vimBuffer.Process(VimKey.Back);
                    Assert.Equal("cat do", _textBuffer.GetLine(0).GetText());
                    _textView.MoveCaretTo(3);
                    _vimBuffer.Process(VimKey.Back);
                    Assert.Equal("cat do", _textBuffer.GetLine(0).GetText());
                    Assert.Equal(1, VimHost.BeepCount);
                }

                [WpfFact]
                public void EnterDoesntChangeStartPoint()
                {
                    Create("cat");
                    _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                    _globalSettings.Backspace = "eol";
                    _vimBuffer.ProcessNotation("is<CR><BS><BS>");
                    Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                    _vimBuffer.ProcessNotation("<BS>");
                    Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                    Assert.Equal(1, VimHost.BeepCount);
                }

                /// <summary>
                /// Virtual space is the VsVim equivalent of autoindent.  When indent option is not 
                /// specified a backspace operation shouldn't change the autoindent
                /// </summary>
                [WpfFact]
                public void NoIndentShouldntRemoveVirtualSpace()
                {
                    Create("cat", "");
                    _globalSettings.Backspace = "";
                    _textView.MoveCaretTo(_textBuffer.GetLine(1).Start, 8);
                    _vimBuffer.ProcessNotation("<BS>");
                    Assert.Equal(8, _textView.GetCaretVirtualPoint().VirtualSpaces);
                    Assert.Equal("", _textBuffer.GetLine(1).GetText());
                }

                /// <summary>
                /// A backspace over virtual space should replace it with the appropriate tabs / spaces
                /// </summary>
                [WpfFact]
                public void IndentShouldRemoveVirtualSpace()
                {
                    Create("cat", "");
                    _globalSettings.Backspace = "indent";
                    _localSettings.TabStop = 4;
                    _localSettings.SoftTabStop = 4;
                    _localSettings.ExpandTab = true;
                    _textView.MoveCaretTo(_textBuffer.GetLine(1).Start, 8);
                    _vimBuffer.ProcessNotation("<BS>");
                    Assert.Equal(0, _textView.GetCaretVirtualPoint().VirtualSpaces);
                    Assert.Equal(new string(' ', 4), _textBuffer.GetLine(1).GetText());
                    Assert.Equal(_textView.GetPointInLine(1, 4), _textView.GetCaretPoint());
                }

                /// <summary>
                /// Don't treat virtual space on a non-blank line as indent
                /// </summary>
                [WpfFact]
                public void VirtualSpaceInNonBlankLineIsntIndent()
                {
                    Create("cat", "");
                    _globalSettings.Backspace = "start";
                    _localSettings.TabStop = 4;
                    _localSettings.SoftTabStop = 4;
                    _localSettings.ExpandTab = true;
                    _textView.MoveCaretTo(_textBuffer.GetLine(0).End, 2);
                    _vimBuffer.ProcessNotation("<BS>");
                    Assert.Equal("cat ", _textBuffer.GetLine(0).GetText());
                }
            }

            public sealed class BackspaceOverWordTest : BackspacingTest
            {
                /// <summary>
                /// Make sure backspace over word at start without
                /// 'backspace=start' works
                /// </summary>
                [WpfFact]
                public void NoStart()
                {
                    Create("cat dog elk");
                    _globalSettings.Backspace = "";
                    _textView.MoveCaretTo(8);
                    _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<C-w>"));
                    Assert.Equal("cat dog elk", _textBuffer.GetLine(0).GetText());
                }

                /// <summary>
                /// Make sure backspace over word at start with 'backspace=start'
                /// works
                /// </summary>
                [WpfFact]
                public void Start()
                {
                    Create("cat dog elk");
                    _globalSettings.Backspace = "start";
                    _textView.MoveCaretTo(8);
                    _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<C-w>"));
                    Assert.Equal("cat elk", _textBuffer.GetLine(0).GetText());
                }

                /// <summary>
                /// Make sure backspace over word at indent without
                /// 'backspace=indent' works
                /// </summary>
                [WpfFact]
                public void NoIndent()
                {
                    Create("    dog");
                    _globalSettings.Backspace = "start";
                    _textView.MoveCaretTo(4);
                    _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<C-w>"));
                    Assert.Equal("dog", _textBuffer.GetLine(0).GetText());
                }

                /// <summary>
                /// Make sure backspace over word at indent with
                /// 'backspace=indent' works
                /// </summary>
                [WpfFact]
                public void Indent()
                {
                    Create("    dog");
                    _globalSettings.Backspace = "start,indent";
                    _textView.MoveCaretTo(4);
                    _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<C-w>"));
                    Assert.Equal("dog", _textBuffer.GetLine(0).GetText());
                }

                /// <summary>
                /// Make sure backspace over word at beginning of line without
                /// 'backspace=eol' works
                /// </summary>
                [WpfFact]
                public void NoEol()
                {
                    Create("cat", "dog");
                    _globalSettings.Backspace = "start,indent";
                    _textView.MoveCaretTo(5);
                    _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<C-w>"));
                    Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                }

                /// <summary>
                /// Make sure backspace over word at beginning of line with
                /// 'backspace=eol' works
                /// </summary>
                [WpfFact]
                public void Eol()
                {
                    Create("cat", "dog");
                    _globalSettings.Backspace = "start,indent,eol";
                    _textView.MoveCaretTo(5);
                    _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<C-w>"));
                    Assert.Equal("catdog", _textBuffer.GetLine(0).GetText());
                }
            }

            public sealed class BackspaceOverLineTest : BackspacingTest
            {
                /// <summary>
                /// Make sure backspacing over line starting from an empty line
                /// works
                /// </summary>
                [WpfFact]
                public void FromEmptyLine()
                {
                    Create("");
                    _vimBuffer.Process("cat");
                    Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                    _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<C-U>"));
                    Assert.Equal("", _textBuffer.GetLine(0).GetText());
                }

                /// <summary>
                /// Make sure backspacing over line from the start of a non-empty
                /// line works
                /// </summary>
                [WpfFact]
                public void FromStarrtOfNonEmpyLine()
                {
                    Create("cat");
                    _textView.MoveCaretTo(0);
                    _vimBuffer.Process("dog ");
                    Assert.Equal("dog cat", _textBuffer.GetLine(0).GetText());
                    _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<C-U>"));
                    Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                }

                /// <summary>
                /// Make sure backspacing over line from the end of a non-empty
                /// line works
                /// </summary>
                [WpfFact]
                public void FromEndOfNonEmpyLine()
                {
                    Create("cat");
                    _textView.MoveCaretTo(3);
                    _vimBuffer.Process(" dog");
                    Assert.Equal("cat dog", _textBuffer.GetLine(0).GetText());
                    _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<C-U>"));
                    Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                }

                /// <summary>
                /// Make sure backspacing over line from the middle of a non-empty
                /// line works
                /// </summary>
                [WpfFact]
                public void FromMiddleOfNonEmpyLine()
                {
                    Create("cat dog");
                    _textView.MoveCaretTo(4);
                    _vimBuffer.Process("bear ");
                    Assert.Equal("cat bear dog", _textBuffer.GetLine(0).GetText());
                    _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<C-U>"));
                    Assert.Equal("cat dog", _textBuffer.GetLine(0).GetText());
                }

                /// <summary>
                /// Make sure backspacing over line from the start position stays
                /// put
                /// </summary>
                [WpfFact]
                public void AtStart_NoBackspaceStart()
                {
                    Create("cat dog");
                    _textView.MoveCaretTo(4);
                    _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<C-U>"));
                    Assert.Equal("cat dog", _textBuffer.GetLine(0).GetText());
                }

                /// <summary>
                /// Make sure backspacing over line and then doing it again from
                /// the start position stays put
                /// </summary>
                [WpfFact]
                public void AgainAtStart_NoBackspaceStart()
                {
                    Create("cat dog");
                    _textView.MoveCaretTo(4);
                    _vimBuffer.Process("bear ");
                    Assert.Equal("cat bear dog", _textBuffer.GetLine(0).GetText());
                    _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<C-U>"));
                    Assert.Equal("cat dog", _textBuffer.GetLine(0).GetText());
                    _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<C-U>"));
                    Assert.Equal("cat dog", _textBuffer.GetLine(0).GetText());
                }

                /// <summary>
                /// Make sure backspacing over line from the start position with
                /// 'backspace=start' performs delete line before cursor
                /// </summary>
                [WpfFact]
                public void AtStart_BackspaceStart()
                {
                    Create("cat dog");
                    _globalSettings.Backspace = "start";
                    _textView.MoveCaretTo(4);
                    _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<C-U>"));
                    Assert.Equal("dog", _textBuffer.GetLine(0).GetText());
                }

                /// <summary>
                /// Make sure backspacing over line and then doing it again from
                /// the start position with 'backspace=start' performs delete
                /// line before cursor
                /// </summary>
                [WpfFact]
                public void AgainAtStart_BackspaceStart()
                {
                    Create("cat dog");
                    _globalSettings.Backspace = "start";
                    _textView.MoveCaretTo(4);
                    _vimBuffer.Process("bear ");
                    Assert.Equal("cat bear dog", _textBuffer.GetLine(0).GetText());
                    _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<C-U>"));
                    Assert.Equal("cat dog", _textBuffer.GetLine(0).GetText());
                    _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<C-U>"));
                    Assert.Equal("dog", _textBuffer.GetLine(0).GetText());
                }

                /// <summary>
                /// Make sure backspacing over line from the start of the next line
                /// of an insert without 'backspace=eol' does nothing
                /// 
                /// </summary>
                [WpfFact]
                public void FromStartOfNextLine_NoBackspaceEol()
                {
                    Create("cat dog");
                    _textView.MoveCaretTo(4);
                    _vimBuffer.Process("bear");
                    Assert.Equal("cat beardog", _textBuffer.GetLine(0).GetText());
                    _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<Enter>"));
                    Assert.Equal("cat bear", _textBuffer.GetLine(0).GetText());
                    Assert.Equal("dog", _textBuffer.GetLine(1).GetText());
                    _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<C-U>"));
                    Assert.Equal("cat bear", _textBuffer.GetLine(0).GetText());
                    Assert.Equal("dog", _textBuffer.GetLine(1).GetText());
                }

                /// <summary>
                /// Make sure backspacing over line from the start of the next line
                /// of an insert without 'backspace=eol' wraps to previous line
                /// 
                /// </summary>
                [WpfFact]
                public void FromStartOfNextLine_BackspaceEol()
                {
                    Create("cat dog");
                    _globalSettings.Backspace = "eol";
                    _textView.MoveCaretTo(4);
                    _vimBuffer.Process("bear");
                    Assert.Equal("cat beardog", _textBuffer.GetLine(0).GetText());
                    _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<Enter>"));
                    Assert.Equal("cat bear", _textBuffer.GetLine(0).GetText());
                    Assert.Equal("dog", _textBuffer.GetLine(1).GetText());
                    _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<C-U>"));
                    Assert.Equal("cat beardog", _textBuffer.GetLine(0).GetText());
                }

                /// <summary>
                /// Make sure backspacing over line twice from the same edit
                /// </summary>
                [WpfFact]
                public void TwiceFromSameEdit()
                {
                    Create("cat dog");
                    _textView.MoveCaretTo(4);
                    _vimBuffer.Process("bear ");
                    Assert.Equal("cat bear dog", _textBuffer.GetLine(0).GetText());
                    _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<C-U>"));
                    Assert.Equal("cat dog", _textBuffer.GetLine(0).GetText());
                    _vimBuffer.Process("lion ");
                    Assert.Equal("cat lion dog", _textBuffer.GetLine(0).GetText());
                    _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<C-U>"));
                    Assert.Equal("cat dog", _textBuffer.GetLine(0).GetText());
                }

                /// <summary>
                /// Make sure a redo after an undo in insert works
                /// </summary>
                [WpfFact]
                public void WithRedo()
                {
                    Create("cat dog");
                    _textView.MoveCaretTo(4);
                    _vimBuffer.Process("bear ");
                    Assert.Equal("cat bear dog", _textBuffer.GetLine(0).GetText());
                    _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<C-U>"));
                    Assert.Equal("cat dog", _textBuffer.GetLine(0).GetText());
                    _vimBuffer.Process("lion ");
                    Assert.Equal("cat lion dog", _textBuffer.GetLine(0).GetText());
                    _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<Esc>"));
                    _vimBuffer.Process(" .");
                    Assert.Equal("cat lion lion dog", _textBuffer.GetLine(0).GetText());
                }

                /// <summary>
                /// Make sure backspacing over word hits all the right "pause"
                /// points in a multi-line edit
                /// </summary>
                [WpfFact]
                public void BackspaceOverWord_RepeatedOverMultiLine()
                {
                    Create("aaa bbb");
                    _globalSettings.Backspace = "indent,eol,start";
                    _textView.MoveCaretTo(0);
                    _textView.MoveCaretTo(7);
                    _vimBuffer.ProcessNotation("ccc ddd<Enter>");
                    _vimBuffer.ProcessNotation("    eee fff<Enter>");
                    _vimBuffer.ProcessNotation("<Enter>");
                    _vimBuffer.ProcessNotation("ggg hhh");
                    Assert.Equal(new[] { "aaa bbbccc ddd", "    eee fff", "", "ggg hhh" }, _textBuffer.GetLines());
                    _vimBuffer.ProcessNotation("<C-w>");
                    Assert.Equal(new[] { "aaa bbbccc ddd", "    eee fff", "", "ggg " }, _textBuffer.GetLines());
                    _vimBuffer.ProcessNotation("<C-w>");
                    Assert.Equal(new[] { "aaa bbbccc ddd", "    eee fff", "", "" }, _textBuffer.GetLines());
                    _vimBuffer.ProcessNotation("<C-w>");
                    Assert.Equal(new[] { "aaa bbbccc ddd", "    eee fff", "" }, _textBuffer.GetLines());
                    _vimBuffer.ProcessNotation("<C-w>");
                    Assert.Equal(new[] { "aaa bbbccc ddd", "    eee fff" }, _textBuffer.GetLines());
                    _vimBuffer.ProcessNotation("<C-w>");
                    Assert.Equal(new[] { "aaa bbbccc ddd", "    eee " }, _textBuffer.GetLines());
                    _vimBuffer.ProcessNotation("<C-w>");
                    Assert.Equal(new[] { "aaa bbbccc ddd", "    " }, _textBuffer.GetLines());
                    _vimBuffer.ProcessNotation("<C-w>");
                    Assert.Equal(new[] { "aaa bbbccc ddd", "" }, _textBuffer.GetLines());
                    _vimBuffer.ProcessNotation("<C-w>");
                    Assert.Equal(new[] { "aaa bbbccc ddd", }, _textBuffer.GetLines());
                    _vimBuffer.ProcessNotation("<C-w>");
                    Assert.Equal(new[] { "aaa bbbccc ", }, _textBuffer.GetLines());
                    _vimBuffer.ProcessNotation("<C-w>");
                    Assert.Equal(new[] { "aaa bbb", }, _textBuffer.GetLines());
                    _vimBuffer.ProcessNotation("<C-w>");
                    Assert.Equal(new[] { "aaa ", }, _textBuffer.GetLines());
                    _vimBuffer.ProcessNotation("<C-w>");
                    Assert.Equal(new[] { "", }, _textBuffer.GetLines());
                }

                /// <summary>
                /// Make sure backspacing over line hits all the right "pause"
                /// points in a multi-line edit
                /// </summary>
                [WpfFact]
                public void RepeatedOverMultiLine()
                {
                    Create("aaa bbb");
                    _globalSettings.Backspace = "indent,eol,start";
                    _textView.MoveCaretTo(7);
                    _vimBuffer.ProcessNotation("ccc ddd<Enter>");
                    _vimBuffer.ProcessNotation("    eee fff<Enter>");
                    _vimBuffer.ProcessNotation("<Enter>");
                    _vimBuffer.ProcessNotation("ggg hhh");
                    Assert.Equal(new[] { "aaa bbbccc ddd", "    eee fff", "", "ggg hhh" }, _textBuffer.GetLines());
                    _vimBuffer.ProcessNotation("<C-u>");
                    Assert.Equal(new[] { "aaa bbbccc ddd", "    eee fff", "", "" }, _textBuffer.GetLines());
                    _vimBuffer.ProcessNotation("<C-u>");
                    Assert.Equal(new[] { "aaa bbbccc ddd", "    eee fff", "" }, _textBuffer.GetLines());
                    _vimBuffer.ProcessNotation("<C-u>");
                    Assert.Equal(new[] { "aaa bbbccc ddd", "    eee fff" }, _textBuffer.GetLines());
                    _vimBuffer.ProcessNotation("<C-u>");
                    Assert.Equal(new[] { "aaa bbbccc ddd", "    " }, _textBuffer.GetLines());
                    _vimBuffer.ProcessNotation("<C-u>");
                    Assert.Equal(new[] { "aaa bbbccc ddd", "" }, _textBuffer.GetLines());
                    _vimBuffer.ProcessNotation("<C-u>");
                    Assert.Equal(new[] { "aaa bbbccc ddd", }, _textBuffer.GetLines());
                    _vimBuffer.ProcessNotation("<C-u>");
                    Assert.Equal(new[] { "aaa bbb", }, _textBuffer.GetLines());
                    _vimBuffer.ProcessNotation("<C-u>");
                    Assert.Equal(new[] { "", }, _textBuffer.GetLines());
                }
            }
        }

        public sealed class VirtualEditTest : InsertModeIntegrationTest
        {
            protected override void Create(ModeArgument argument, params string[] lines)
            {
                base.Create(argument, lines);
                _globalSettings.VirtualEdit = "insert";
            }

            /// <summary>
            /// Fill in leading virtual space with tabs when 'noexpandtab' is set
            /// </summary>
            [WpfFact]
            public void FillInLeadingVirtualSpaceWithTabs()
            {
                Create("", "");
                _localSettings.ExpandTab = false;
                _localSettings.TabStop = 4;
                _textView.MoveCaretTo(0, virtualSpaces: 4);
                _vimBuffer.ProcessNotation("foo");
                Assert.Equal(new[] { "\tfoo", "" }, _textBuffer.GetLines());
                Assert.Equal(_textBuffer.GetVirtualPointInLine(0, 4), _textView.GetCaretVirtualPoint());
            }

            /// <summary>
            /// Fill in leading virtual space with spaces when 'expandtab' is set
            /// </summary>
            [WpfFact]
            public void FillInLeadingVirtualSpaceWithSpaces()
            {
                Create("", "");
                _localSettings.ExpandTab = true;
                _localSettings.TabStop = 4;
                _textView.MoveCaretTo(0, virtualSpaces: 4);
                _vimBuffer.ProcessNotation("foo");
                Assert.Equal(new[] { "    foo", "" }, _textBuffer.GetLines());
                Assert.Equal(_textBuffer.GetVirtualPointInLine(0, 7), _textView.GetCaretVirtualPoint());
            }

            /// <summary>
            /// Always fill in non-leading virtual space with spaces
            /// </summary>
            /// <param name="expandTab"></param>
            [WpfTheory]
            [InlineData(true)]
            [InlineData(false)]
            public void FillInNonLeadingVirtualSpaceWithSpaces(bool expandTab)
            {
                Create("f", "");
                _localSettings.ExpandTab = expandTab;
                _localSettings.TabStop = 4;
                _textView.MoveCaretTo(1, virtualSpaces: 3);
                _vimBuffer.ProcessNotation("oo");
                Assert.Equal(new[] { "f   oo", "" }, _textBuffer.GetLines());
                Assert.Equal(_textBuffer.GetVirtualPointInLine(0, 6), _textView.GetCaretVirtualPoint());
            }

            [WpfFact]
            public void CharRightRealToVirtual()
            {
                Create("foo", "");
                _textView.MoveCaretTo(3);
                _vimBuffer.ProcessNotation("<Right>");
                Assert.Equal(new VirtualSnapshotPoint(_textBuffer.GetLine(0), 4),
                    _textView.Caret.Position.VirtualBufferPosition);
            }

            [WpfFact]
            public void CharRightVirtualToVirtual()
            {
                Create("foo", "");
                _textView.MoveCaretTo(3, virtualSpaces: 1);
                _vimBuffer.ProcessNotation("<Right>");
                Assert.Equal(new VirtualSnapshotPoint(_textBuffer.GetLine(0), 5),
                    _textView.Caret.Position.VirtualBufferPosition);
            }

            [WpfFact]
            public void CharLeftVirtualToVirtual()
            {
                Create("foo", "");
                _textView.MoveCaretTo(3, virtualSpaces: 2);
                _vimBuffer.ProcessNotation("<Left>");
                Assert.Equal(new VirtualSnapshotPoint(_textBuffer.GetLine(0), 4),
                    _textView.Caret.Position.VirtualBufferPosition);
            }

            [WpfFact]
            public void CharLeftVirtualToReal()
            {
                Create("foo", "");
                _textView.MoveCaretTo(3, virtualSpaces: 1);
                _vimBuffer.ProcessNotation("<Left>");
                Assert.Equal(new VirtualSnapshotPoint(_textBuffer.GetLine(0), 3),
                    _textView.Caret.Position.VirtualBufferPosition);
            }

            [WpfFact]
            public void LineDownRealToVirtual()
            {
                Create("foo", "", "");
                _textView.MoveCaretTo(2);
                _vimBuffer.ProcessNotation("<Down>");
                Assert.Equal(new VirtualSnapshotPoint(_textBuffer.GetLine(1), 2),
                    _textView.Caret.Position.VirtualBufferPosition);
            }

            [WpfFact]
            public void LineDownVirtualToVirtual()
            {
                Create("foo", "bar", "");
                _textView.MoveCaretTo(3, virtualSpaces: 3);
                _vimBuffer.ProcessNotation("<Down>");
                Assert.Equal(new VirtualSnapshotPoint(_textBuffer.GetLine(1), 6),
                    _textView.Caret.Position.VirtualBufferPosition);
            }

            [WpfFact]
            public void LineDownVirtualToVirtualWithTab()
            {
                Create("foo", "\tx", "");
                _vimBuffer.LocalSettings.TabStop = 4;
                _textView.MoveCaretTo(3, virtualSpaces: 3);
                _vimBuffer.ProcessNotation("<Down>");
                Assert.Equal(new VirtualSnapshotPoint(_textBuffer.GetLine(1), 3),
                    _textView.Caret.Position.VirtualBufferPosition);
            }

            [WpfFact]
            public void LineDownVirtualToReal()
            {
                Create("", "bar", "");
                _textView.MoveCaretTo(0, virtualSpaces: 2);
                _vimBuffer.ProcessNotation("<Down>");
                Assert.Equal(new VirtualSnapshotPoint(_textBuffer.GetLine(1), 2),
                    _textView.Caret.Position.VirtualBufferPosition);
            }

            [WpfFact]
            public void LineUpRealToVirtual()
            {
                Create("", "bar", "");
                _textView.MoveCaretToLine(1, 2);
                _vimBuffer.ProcessNotation("<Up>");
                Assert.Equal(new VirtualSnapshotPoint(_textBuffer.GetLine(0), 2),
                    _textView.Caret.Position.VirtualBufferPosition);
            }

            [WpfFact]
            public void LineUpVirtualToVirtual()
            {
                Create("foo", "bar", "");
                _textView.MoveCaretToLine(1, 3, virtualSpaces: 3);
                _vimBuffer.ProcessNotation("<Up>");
                Assert.Equal(new VirtualSnapshotPoint(_textBuffer.GetLine(0), 6),
                    _textView.Caret.Position.VirtualBufferPosition);
            }

            [WpfFact]
            public void LineUpVirtualToVirtualWithTab()
            {
                Create("foo", "\tx", "");
                _localSettings.TabStop = 4;
                _textView.MoveCaretToLine(1, 2, virtualSpaces: 1);
                _vimBuffer.ProcessNotation("<Up>");
                Assert.Equal(new VirtualSnapshotPoint(_textBuffer.GetLine(0), 6),
                    _textView.Caret.Position.VirtualBufferPosition);
            }

            [WpfFact]
            public void LineUpVirtualToReal()
            {
                Create("foo", "", "");
                _textView.MoveCaretToLine(1, 0, virtualSpaces: 2);
                _vimBuffer.ProcessNotation("<Up>");
                Assert.Equal(new VirtualSnapshotPoint(_textBuffer.GetLine(0), 2),
                    _textView.Caret.Position.VirtualBufferPosition);
            }
        }

        public sealed class WordCompletionTest : InsertModeIntegrationTest
        {

        }

        public sealed class OneTimeCommandTests : InsertModeIntegrationTest
        {
            [WpfFact]
            public void Esc()
            {
                Create("");
                _vimBuffer.ProcessNotation("<C-o>");
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                Assert.Equal(ModeKind.Insert, _vimBuffer.InOneTimeCommand);
                _vimBuffer.ProcessNotation("<Esc>");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal(FSharpOption<ModeKind>.None, _vimBuffer.InOneTimeCommand);
            }
            
            [WpfFact]
            public void Replace_Esc()
            {
                Create("");
                _vimBuffer.ProcessNotation("<C-o>R");
                Assert.Equal(ModeKind.Replace, _vimBuffer.ModeKind);
                Assert.Equal(FSharpOption<ModeKind>.None, _vimBuffer.InOneTimeCommand);
                _vimBuffer.ProcessNotation("<Esc>");
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                Assert.Equal(FSharpOption<ModeKind>.None, _vimBuffer.InOneTimeCommand);
            }
            
            [WpfFact]
            public void ExCommand_Esc()
            {
                Create("");
                _vimBuffer.ProcessNotation("<C-o>:");
                Assert.Equal(ModeKind.Command, _vimBuffer.ModeKind);
                Assert.Equal(ModeKind.Insert, _vimBuffer.InOneTimeCommand);
                _vimBuffer.ProcessNotation("<Esc>");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal(FSharpOption<ModeKind>.None, _vimBuffer.InOneTimeCommand);
            }
            
            [WpfFact]
            public void ExCommand()
            {
                Create("");
                _vimBuffer.ProcessNotation("<C-o>:pwd<CR>");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal(FSharpOption<ModeKind>.None, _vimBuffer.InOneTimeCommand);
            }
            
            [WpfFact]
            public void ExCommand_Normal()
            {
                Create("");
                _vimBuffer.ProcessNotation("<C-o>:norm d<CR>");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal(FSharpOption<ModeKind>.None, _vimBuffer.InOneTimeCommand);
            }
            
            [WpfFact]
            public void Visual_Esc()
            {
                Create("");
                _vimBuffer.ProcessNotation("<C-o>v");
                Assert.Equal(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
                Assert.Equal(ModeKind.Insert, _vimBuffer.InOneTimeCommand);
                _vimBuffer.ProcessNotation("<Esc>");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal(FSharpOption<ModeKind>.None, _vimBuffer.InOneTimeCommand);
            }
            
            [WpfFact]
            public void Visual_ExCommand()
            {
                Create("");
                _vimBuffer.ProcessNotation("<C-o>v:pwd<CR>");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal(FSharpOption<ModeKind>.None, _vimBuffer.InOneTimeCommand);
            }
            
            [WpfFact]
            public void Visual_ExCommand_Esc()
            {
                Create("");
                _vimBuffer.ProcessNotation("<C-o>v:");
                Assert.Equal(ModeKind.Command, _vimBuffer.ModeKind);
                Assert.Equal(ModeKind.Insert, _vimBuffer.InOneTimeCommand);
                _vimBuffer.ProcessNotation("<Esc>");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal(FSharpOption<ModeKind>.None, _vimBuffer.InOneTimeCommand);
            }
            
            [WpfFact]
            public void SelectModeOneCommand_Visual_Esc()
            {
                Create("");
                _vimBuffer.ProcessNotation("<C-o>gh<C-g>");
                Assert.Equal(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
                Assert.Equal(ModeKind.Insert, _vimBuffer.InOneTimeCommand);
                _vimBuffer.ProcessNotation("<Esc>");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal(FSharpOption<ModeKind>.None, _vimBuffer.InOneTimeCommand);
            }
            
            [WpfFact]
            public void SelectModeOneCommand_Visual_ExCommand_Esc()
            {
                Create("");
                _vimBuffer.ProcessNotation("<C-o>gh<C-g>:");
                Assert.Equal(ModeKind.Command, _vimBuffer.ModeKind);
                Assert.Equal(ModeKind.Insert, _vimBuffer.InOneTimeCommand);
                _vimBuffer.ProcessNotation("<Esc>");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal(FSharpOption<ModeKind>.None, _vimBuffer.InOneTimeCommand);
            }
            
            [WpfFact]
            public void SelectModeOneCommand_ExCommand()
            {
                Create("");
                _vimBuffer.ProcessNotation("<C-o>gh<C-o>:pwd<CR>");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal(FSharpOption<ModeKind>.None, _vimBuffer.InOneTimeCommand);
            }
            
            [WpfFact]
            public void SelectModeOneCommand_ExCommand_Esc()
            {
                Create("");
                _vimBuffer.ProcessNotation("<C-o>gh<C-o>:<Esc>");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal(FSharpOption<ModeKind>.None, _vimBuffer.InOneTimeCommand);
            }
            
            [WpfFact]
            public void SelectModeOneCommand_Printable()
            {
                Create("foo bar baz");
                _vimBuffer.ProcessNotation("<C-o>gh<C-o>e");
                Assert.Equal("foo", _textView.GetSelectionSpan().GetText());
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal(ModeKind.Insert, _vimBuffer.InOneTimeCommand);
                _vimBuffer.ProcessNotation("x");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal(FSharpOption<ModeKind>.None, _vimBuffer.InOneTimeCommand);
                Assert.Equal("x bar baz", _textBuffer.GetLine(0).GetText());
            }
            
            [WpfFact]
            public void SelectModeOneCommand_Esc()
            {
                Create("foo bar baz");
                _vimBuffer.ProcessNotation("<C-o>gh<C-o><Esc>");
                Assert.Equal(FSharpOption<ModeKind>.None, _vimBuffer.InOneTimeCommand);
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal(FSharpOption<ModeKind>.None, _vimBuffer.InOneTimeCommand);
                Assert.Equal("foo bar baz", _textBuffer.GetLine(0).GetText());
            }
            
            [WpfFact]
            public void SelectModeOneCommand_CursorMove_StartStopSelection()
            {
                Create("foo bar baz");
                _globalSettings.SelectModeOptions = SelectModeOptions.Keyboard;
                _globalSettings.KeyModelOptions = KeyModelOptions.StartSelection | KeyModelOptions.StopSelection;
                _vimBuffer.ProcessNotation("<C-o><S-Right><S-Right>");
                Assert.Equal("foo", _textView.GetSelectionSpan().GetText());
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer.ModeKind);
                Assert.Equal(ModeKind.Insert, _vimBuffer.InOneTimeCommand);
                _vimBuffer.ProcessNotation("<Right>");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal(FSharpOption<ModeKind>.None, _vimBuffer.InOneTimeCommand);
            }
        }

        public sealed class MiscTest : InsertModeIntegrationTest
        {
            /// <summary>
            /// Make sure that the ITextView isn't accessed in insert mode if it's active and the 
            /// ITextView is closed
            /// </summary>
            [WpfFact]
            public void CloseInInsertMode()
            {
                Create("foo", "bar");
                _textView.Close();
            }

            [WpfFact]
            public void Leave_WithControlC()
            {
                Create("hello world");
                _vimBuffer.ProcessNotation("<C-c>");
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// Ensure that normal typing gets passed to TryCustomProcess
            /// </summary>
            [WpfFact]
            public void TryCustomProcess_DirectInsert()
            {
                Create("world");
                VimHost.TryCustomProcessFunc =
                    (textView, command) =>
                    {
                        if (command.IsInsert)
                        {
                            Assert.Equal("#", command.AsInsert().Text);
                            _textBuffer.Insert(0, "hello ");
                            return true;
                        }

                        return false;
                    };
                _vimBuffer.Process('#');
                Assert.Equal("hello world", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// Ensure that other commands go through TryCustomProcess
            /// </summary>
            [WpfFact]
            public void TryCustomProcess_Enter()
            {
                Create("world");
                VimHost.TryCustomProcessFunc =
                    (textView, command) =>
                    {
                        if (command.IsInsertNewLine)
                        {
                            _textBuffer.Insert(0, "hello ");
                            return true;
                        }

                        return false;
                    };
                _vimBuffer.Process(VimKey.Enter);
                Assert.Equal("hello world", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// Repeat of a TryCustomProcess for a context sensitive command should
            /// recall that function vs. repeating the
            /// inserted text
            /// </summary>
            [WpfFact]
            public void TryCustomProcess_Repeat()
            {
                Create("world");
                var first = true;
                VimHost.TryCustomProcessFunc =
                    (textView, command) =>
                    {
                        if (command.IsInsertTab)
                        {
                            if (first)
                            {
                                _textBuffer.Insert(0, "hello ");
                                first = false;
                            }
                            else
                            {
                                _textBuffer.Insert(0, "big ");
                            }
                            return true;
                        }

                        return false;
                    };
                _vimBuffer.ProcessNotation("<Tab><Esc>.");
                Assert.Equal("big hello world", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// KeyInput values which are custom processed should still end up in the macro recorder
            /// </summary>
            [WpfFact]
            public void TryCustomProcess_Macro()
            {
                Create("world");
                Vim.MacroRecorder.StartRecording(_register, false);
                VimHost.TryCustomProcessFunc =
                    (textView, command) =>
                    {
                        if (command.IsInsert)
                        {
                            Assert.Equal("#", command.AsInsert().Text);
                            _textBuffer.Insert(0, "hello ");
                            return true;
                        }

                        return false;
                    };
                _vimBuffer.Process('#');
                Vim.MacroRecorder.StopRecording();
                Assert.Equal("hello world", _textBuffer.GetLine(0).GetText());
                Assert.Equal("#", _register.StringValue);
            }

            /// <summary>
            /// Ensure that delete all indent both deletes the indent and preserves the caret position
            /// </summary>
            [WpfFact]
            public void DeleteAllIndent()
            {
                Create("       hello");
                _textView.MoveCaretTo(8);
                _vimBuffer.Process("0");
                _vimBuffer.Process(KeyInputUtil.CharWithControlToKeyInput('d'));
                Assert.Equal("hello", _textView.GetLine(0).GetText());
                Assert.Equal(1, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// The delete key when combined with shift should still cause a standard delete
            /// </summary>
            [WpfFact]
            public void Delete_WithShift()
            {
                Create("cat dog");
                _globalSettings.Backspace = "start";
                _textView.MoveCaretTo(1);
                _vimBuffer.ProcessNotation("<S-BS>");
                Assert.Equal("at dog", _textBuffer.GetLine(0).GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// The delete key when combined with shift should still participate in key mapping
            /// </summary>
            [WpfFact]
            public void Delete_WithShift_KeyMapping()
            {
                Create(" world");
                GlobalKeyMap.AddKeyMapping("<S-BS>", "hello", allowRemap: false, KeyRemapMode.Insert);
                _textView.MoveCaretTo(0);
                _vimBuffer.ProcessNotation("<S-BS>");
                Assert.Equal("hello world", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// Verify that inserting tab with a count and inserting tab "count" times is an exchangable
            /// operation
            /// </summary>
            [WpfFact]
            public void Insert_Tab()
            {
                Create("int Member", "int Member");
                _localSettings.ExpandTab = false;
                _localSettings.TabStop = 8;
                _localSettings.ShiftWidth = 4;
                _vimBuffer.Process(VimKey.Escape);
                _textView.MoveCaretToLine(0, 3);
                _vimBuffer.ProcessNotation("i<Tab><Tab><Esc>");
                Assert.Equal("int\t\t Member", _textBuffer.GetLine(0).GetText());
                _textView.MoveCaretToLine(1, 3);
                _vimBuffer.ProcessNotation("2i<Tab><Esc>");
                Assert.Equal("int\t\t Member", _textBuffer.GetLine(1).GetText());
            }

            /// <summary>
            /// Make sure that indentation is still done even when enter occurs with a non-standard mapping
            /// of enter
            /// </summary>
            [WpfFact]
            public void Insert_NewLine_IndentWithAltMapping()
            {
                Create("  hello", "world");
                _localSettings.AutoIndent = true;
                Vim.GlobalKeyMap.AddKeyMapping("<c-e>", "<Enter>", allowRemap: false, KeyRemapMode.Insert);
                _textView.MoveCaretTo(5);
                _vimBuffer.Process(KeyInputUtil.CharWithControlToKeyInput('e'));
                Assert.Equal("  hel", _textView.GetLine(0).GetText());
                Assert.Equal("  lo", _textView.GetLine(1).GetText());
            }

            /// <summary>
            /// At the end of the line the caret should just move into virtual space.  No need for actual
            /// white space to be inserted
            /// </summary>
            [WpfFact]
            public void Insert_NewLine_AtEndOfLine()
            {
                Create("  hello", "world");
                _localSettings.AutoIndent = true;
                _textView.MoveCaretTo(_textView.GetLine(0).End);
                _vimBuffer.Process(VimKey.Enter);
                Assert.Equal("  hello", _textView.GetLine(0).GetText());
                Assert.Equal("", _textView.GetLine(1).GetText());
                Assert.Equal(2, _textView.GetCaretVirtualPoint().VirtualSpaces);
            }

            /// <summary>
            /// Make sure executing the one time command correctly sets the buffer state
            /// </summary>
            [WpfFact]
            public void OneTimeCommand_BufferState()
            {
                Create("");
                _vimBuffer.Process(KeyInputUtil.CharWithControlToKeyInput('o'));
                Assert.True(_vimBuffer.InOneTimeCommand.Is(ModeKind.Insert));
            }

            /// <summary>
            /// Execute a one time command of delete word
            /// </summary>
            [WpfFact]
            public void OneTimeCommand_DeleteWord()
            {
                Create("hello world");
                _textView.MoveCaretTo(0);
                _vimBuffer.Process(KeyInputUtil.CharWithControlToKeyInput('o'));
                _vimBuffer.Process("dw");
                Assert.Equal("world", _textBuffer.GetLine(0).GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.True(_vimBuffer.InOneTimeCommand.IsNone());
            }

            /// <summary>
            /// Execute a one time command of ':put'
            /// </summary>
            [WpfFact]
            public void OneTimeCommand_CommandMode_Put()
            {
                Create("hello world");
                Vim.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateValue("the dog");
                _textView.MoveCaretTo(0);
                _vimBuffer.Process(KeyInputUtil.CharWithControlToKeyInput('o'));
                _vimBuffer.Process(":put", enter: true);
                Assert.Equal("hello world", _textBuffer.GetLine(0).GetText());
                Assert.Equal("the dog", _textBuffer.GetLine(1).GetText());
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.True(_vimBuffer.InOneTimeCommand.IsNone());
            }

            /// <summary>
            /// Normal mode usually doesn't handle the Escape key but it must during a 
            /// one time command
            /// </summary>
            [WpfFact]
            public void OneTimeCommand_Normal_Escape()
            {
                Create("");
                _vimBuffer.Process(KeyInputUtil.CharWithControlToKeyInput('o'));
                _vimBuffer.Process(VimKey.Escape);
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.True(_vimBuffer.InOneTimeCommand.IsNone());
            }

            /// <summary>
            /// Using put as a one-time command should always place the caret
            /// after the inserted text
            /// </summary>
            [WpfFact]
            public void OneTimeCommand_Put_MiddleOfLine()
            {
                // Reported in issue #1065.
                Create("cat", "");
                Vim.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateValue("dog");
                _textView.MoveCaretTo(1);
                _vimBuffer.ProcessNotation("<C-o>p");
                Assert.Equal("cadogt", _textBuffer.GetLine(0).GetText());
                Assert.Equal(5, _textView.GetCaretPoint().Position);
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.True(_vimBuffer.InOneTimeCommand.IsNone());
            }

            /// <summary>
            /// Using put as a one-time command should always place the caret
            /// after the inserted text, even at the end of a line
            /// </summary>
            [WpfFact]
            public void OneTimeCommand_Put_EndOfLine()
            {
                // Reported in issue #1065.
                Create("cat", "");
                Vim.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateValue("dog");
                _textView.MoveCaretTo(3);
                _vimBuffer.ProcessNotation("<C-o>p");
                Assert.Equal("catdog", _textBuffer.GetLine(0).GetText());
                Assert.Equal(6, _textView.GetCaretPoint().Position);
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.True(_vimBuffer.InOneTimeCommand.IsNone());
            }

            /// <summary>
            /// Ensure the single backspace is repeated properly.  It is tricky because it has to both 
            /// backspace and then jump a caret space to the left.
            /// </summary>
            [WpfFact]
            public void Repeat_Backspace_Single()
            {
                Create("dog toy", "fish chips");
                _globalSettings.Backspace = "start";
                _textView.MoveCaretToLine(1, 5);
                _vimBuffer.Process(VimKey.Back, VimKey.Escape);
                Assert.Equal("fishchips", _textView.GetLine(1).GetText());
                _textView.MoveCaretTo(4);
                _vimBuffer.Process(".");
                Assert.Equal("dogtoy", _textView.GetLine(0).GetText());
                Assert.Equal(2, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Ensure when the mode is entered with a count that the escape will cause the 
            /// text to be repeated
            /// </summary>
            [WpfFact]
            public void Repeat_Insert()
            {
                Create(ModeArgument.NewInsertWithCount(2), "the cat");
                _vimBuffer.Process("hi");
                Assert.Equal(2, _textView.GetCaretPoint().Position);
                _vimBuffer.Process(VimKey.Escape);
                Assert.Equal("hihithe cat", _textView.GetLine(0).GetText());
                Assert.Equal(3, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Insert mode tracks direct input by keeping a reference to the inserted text vs. the actual
            /// key strokes which were used.  This can be demonstrated by repeating an insert after 
            /// introducing a key remapping
            /// </summary>
            [WpfFact]
            public void Repeat_Insert_WithKeyMap()
            {
                Create("", "", "hello world");
                _vimBuffer.Process("abc");
                Assert.Equal("abc", _textView.GetLine(0).GetText());
                _vimBuffer.Process(VimKey.Escape);
                _textView.MoveCaretToLine(1);
                _vimBuffer.Process(":imap a b", enter: true);
                _vimBuffer.Process(".");
                Assert.Equal("abc", _textView.GetLine(1).GetText());
            }

            /// <summary>
            /// Verify that we properly repeat an insert which is a tab count 
            /// </summary>
            [WpfFact]
            public void Repeat_Insert_TabCount()
            {
                Create("int Member", "int Member");
                _localSettings.ExpandTab = false;
                _localSettings.TabStop = 8;
                _localSettings.ShiftWidth = 4;
                _vimBuffer.Process(VimKey.Escape);
                _textView.MoveCaretToLine(0, 3);
                _vimBuffer.ProcessNotation("3i<Tab><Esc>");
                Assert.Equal("int\t\t\t Member", _textBuffer.GetLine(0).GetText());
                _textView.MoveCaretToLine(1, 3);
                _vimBuffer.Process('.');
                Assert.Equal("int\t\t\t Member", _textBuffer.GetLine(1).GetText());
            }

            /// <summary>
            /// When repeating a tab the repeat needs to be wary of maintainin the 'tabstop' modulus
            /// of the new line
            /// </summary>
            [WpfFact]
            public void Repeat_Insert_TabNonEvenOffset()
            {
                Create("hello world", "static LPTSTR pValue");
                _localSettings.ExpandTab = true;
                _localSettings.TabStop = 4;
                _vimBuffer.Process(VimKey.Escape);
                _vimBuffer.ProcessNotation("cw<Tab><Esc>");
                Assert.Equal("     world", _textView.GetLine(0).GetText());
                _textView.MoveCaretTo(_textBuffer.GetPointInLine(1, 13));
                _vimBuffer.Process('.');
                Assert.Equal("static LPTSTR   pValue", _textView.GetLine(1).GetText());
            }

            /// <summary>
            /// Repeat a simple text insertion with a count
            /// </summary>
            [WpfFact]
            public void Repeat_InsertWithCount()
            {
                Create("", "");
                _vimBuffer.Process('h');
                _vimBuffer.Process(VimKey.Escape);
                Assert.Equal("h", _textView.GetLine(0).GetText());
                _textView.MoveCaretToLine(1);
                _vimBuffer.Process("3.");
                Assert.Equal("hhh", _textView.GetLine(1).GetText());
                Assert.Equal(2, _textView.GetCaretColumn().ColumnNumber);
            }

            /// <summary>
            /// Repeat a simple text insertion with a count.  Focus on making sure the caret position
            /// is correct.  Added text ensures the end of line doesn't save us by moving the caret
            /// backwards
            /// </summary>
            [WpfFact]
            public void Repeat_InsertWithCountOverOtherText()
            {
                Create("", "a");
                _vimBuffer.Process('h');
                _vimBuffer.Process(VimKey.Escape);
                Assert.Equal("h", _textView.GetLine(0).GetText());
                _textView.MoveCaretToLine(1);
                _vimBuffer.Process("3.");
                Assert.Equal("hhha", _textView.GetLine(1).GetText());
                Assert.Equal(2, _textView.GetCaretColumn().ColumnNumber);
            }

            /// <summary>
            /// Ensure when the mode is entered with a count that the escape will cause the
            /// deleted text to be repeated
            /// </summary>
            [WpfFact]
            public void Repeat_Delete()
            {
                Create(ModeArgument.NewInsertWithCount(2), "doggie");
                _textView.MoveCaretTo(1);
                _vimBuffer.Process(VimKey.Delete);
                _vimBuffer.Process(VimKey.Escape);
                Assert.Equal("dgie", _textView.GetLine(0).GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Repeated white space change to tabs should only repeat the normalized change
            /// </summary>
            [WpfFact]
            public void Repeat_WhiteSpaceChange()
            {
                Create(ModeArgument.NewInsertWithCount(2), "blue\t\t    dog");
                _vimBuffer.LocalSettings.TabStop = 4;
                _vimBuffer.LocalSettings.ExpandTab = false;
                _textView.MoveCaretTo(10);
                _textBuffer.Replace(new Span(6, 4), "\t\t");
                _textView.MoveCaretTo(8);
                Assert.Equal("blue\t\t\t\tdog", _textBuffer.GetLine(0).GetText());
                _vimBuffer.Process(VimKey.Escape);
                Assert.Equal("blue\t\t\t\t\tdog", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// Ensure that multi-line changes are properly recorded and repeated in the ITextBuffer
            /// </summary>
            [WpfFact]
            public void Repeat_MultilineChange()
            {
                Create("cat", "dog");
                _vimBuffer.LocalSettings.TabStop = 4;
                _vimBuffer.LocalSettings.ExpandTab = false;
                _vimBuffer.Process("if (condition)", enter: true);
                _vimBuffer.Process("\t");
                _vimBuffer.Process(VimKey.Escape);
                Assert.Equal("if (condition)", _textBuffer.GetLine(0).GetText());
                Assert.Equal("\tcat", _textBuffer.GetLine(1).GetText());
                _textView.MoveCaretToLine(2);
                _vimBuffer.Process(".");
                Assert.Equal("if (condition)", _textBuffer.GetLine(2).GetText());
                Assert.Equal("\tdog", _textBuffer.GetLine(3).GetText());
            }

            /// <summary>
            /// Verify that we can repeat the DeleteAllIndent command.  Make sure that the command repeats
            /// and not the literal change of the text
            /// </summary>
            [WpfFact]
            public void Repeat_DeleteAllIndent()
            {
                Create("     hello", "          world");
                _vimBuffer.Process("0");
                _vimBuffer.Process(KeyInputUtil.CharWithControlToKeyInput('d'));
                _vimBuffer.Process(VimKey.Escape);
                Assert.Equal("hello", _textView.GetLine(0).GetText());
                _textView.MoveCaretToLine(1);
                _vimBuffer.Process(".");
                Assert.Equal("world", _textView.GetLine(1).GetText());
            }

            /// <summary>
            /// Make sure that the tab operation can be properly repeated
            /// </summary>
            [WpfFact]
            public void Repeat_InsertTab()
            {
                Create("cat", "dog");
                _vimBuffer.LocalSettings.ExpandTab = false;
                _vimBuffer.ProcessNotation("<Tab><Esc>");
                _textView.MoveCaretToLine(1);
                _vimBuffer.Process('.');
                Assert.Equal("\tdog", _textView.GetLine(1).GetText());
            }

            /// <summary>
            /// Make sure that the insert tab repeats as the insert tab command and not as the 
            /// repeat of a text change.  This can be verified by altering the settings between the initial
            /// insert and the repeat
            /// </summary>
            [WpfFact]
            public void Repeat_InsertTab_ChangedSettings()
            {
                Create("cat", "dog");
                _vimBuffer.LocalSettings.ExpandTab = false;
                _vimBuffer.ProcessNotation("<Tab><Esc>");
                _textView.MoveCaretToLine(1);
                _vimBuffer.LocalSettings.ExpandTab = true;
                _vimBuffer.LocalSettings.TabStop = 2;
                _vimBuffer.Process('.');
                Assert.Equal("  dog", _textView.GetLine(1).GetText());
            }

            /// <summary>
            /// Make sure that the insert tab command when linked with before and after text changes is treated
            /// as a separate command and not straight text.  This can be verified by changing the tab insertion
            /// settings between the initial insert and the repeat
            /// </summary>
            [WpfFact]
            public void Repeat_InsertTab_CombinedWithText()
            {
                Create("", "");
                _vimBuffer.LocalSettings.ExpandTab = false;
                _vimBuffer.Process("cat\tdog");
                _vimBuffer.Process(VimKey.Escape);
                Assert.Equal("cat\tdog", _textView.GetLine(0).GetText());
                _vimBuffer.LocalSettings.ExpandTab = true;
                _vimBuffer.LocalSettings.TabStop = 1;
                _textView.MoveCaretToLine(1);
                _vimBuffer.Process('.');
                Assert.Equal("cat dog", _textView.GetLine(1).GetText());
            }

            /// <summary>
            /// Test the special case of repeating an insert mode action which doesn't actually edit any
            /// items.  This may seem like a trivial action, and really it is, but the behavior being right
            /// is core to us being able to correctly repeat insert mode actions
            /// </summary>
            [WpfFact]
            public void Repeat_NoChange()
            {
                Create("cat");
                _textView.MoveCaretTo(2);
                _vimBuffer.Process(VimKey.Escape);
                Assert.Equal(1, _textView.GetCaretPoint().Position);
                _vimBuffer.Process('.');
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Make sure we don't accidentally link the move caret left action with a command coming
            /// from normal mode
            /// </summary>
            [WpfFact]
            public void Repeat_NoChange_DontLinkWithNormalCommand()
            {
                Create("cat dog");
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                _textView.MoveCaretTo(0);
                _vimBuffer.Process("dwi");
                _vimBuffer.Process(VimKey.Escape);
                Assert.Equal("dog", _textView.GetLine(0).GetText());
                _textView.MoveCaretTo(1);
                _vimBuffer.Process('.');
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            
            /// <summary>
            /// When moving using the arrrow keys, it behaves the same way as stopping the insert mode, 
            /// move the cursor, and then enter the insert mode again. Therefore only the text written after
            /// the move will be repeated.
            /// </summary>
            [WpfFact]
            public void Repeat_With_Arrow_Right()
            {
                Create("cat dog");
                _vimBuffer.Process("dog");
                _vimBuffer.Process(VimKey.Right);
                _vimBuffer.Process("cat");
                _vimBuffer.Process(VimKey.Escape);
                Assert.Equal("dogccatat dog", _textView.GetLine(0).GetText());
                Assert.Equal(6, _textView.GetCaretPoint().Position);
                _textView.MoveCaretTo(0);
                _vimBuffer.Process(".");
                Assert.Equal("catdogccatat dog", _textView.GetLine(0).GetText());
                Assert.Equal(2, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// When moving using the arrrow keys, it behaves the same way as stopping the insert mode, 
            /// move the cursor, and then enter the insert mode again. Therefore only the text written after
            /// the move will be repeated.
            /// </summary>
            [WpfFact]
            public void Repeat_With_Arrow_Left()
            {
                Create("cat dog");
                _vimBuffer.Process("dog");
                _vimBuffer.Process(VimKey.Left);
                _vimBuffer.Process("cat");
                _vimBuffer.Process(VimKey.Escape);
                Assert.Equal("docatgcat dog", _textView.GetLine(0).GetText());
                Assert.Equal(4, _textView.GetCaretPoint().Position);
                _textView.MoveCaretTo(0);
                _vimBuffer.Process(".");
                Assert.Equal("catdocatgcat dog", _textView.GetLine(0).GetText());
                Assert.Equal(2, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// When moving the cursor using the mouse, it behaves the same way as stopping the insert mode, 
            /// move the cursor, and then enter the insert mode again. Therefore only the text written after
            /// the move will be repeated.
            /// </summary>
            [WpfFact]
            public void Repeat_With_Mouse_Move()
            {
                Create("cat dog");
                _vimBuffer.Process("dog");
                _textView.MoveCaretTo(6);
                _vimBuffer.Process("cat");
                _vimBuffer.Process(VimKey.Escape);
                Assert.Equal("dogcatcat dog", _textView.GetLine(0).GetText());
                Assert.Equal(8, _textView.GetCaretPoint().Position);
                _textView.MoveCaretTo(0);
                _vimBuffer.Process(".");
                Assert.Equal("catdogcatcat dog", _textView.GetLine(0).GetText());
                Assert.Equal(2, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// This test is mainly a regression test against the selection change logic
            /// </summary>
            [WpfFact]
            public void SelectionChange1()
            {
                Create("foo", "bar");
                _textView.SelectAndMoveCaret(new SnapshotSpan(_textView.GetLine(0).Start, 0));
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// Make sure that shift left does a round up before it shifts to the left.
            /// </summary>
            [WpfFact]
            public void ShiftLeft_RoundUp()
            {
                Create("     hello");
                _vimBuffer.LocalSettings.ShiftWidth = 4;
                _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<C-D>"));
                Assert.Equal("    hello", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// Make sure that when the text is properly rounded to a shift width that the 
            /// shift left just deletes a shift width
            /// </summary>
            [WpfFact]
            public void ShiftLeft_Normal()
            {
                Create("        hello");
                _vimBuffer.LocalSettings.ShiftWidth = 4;
                _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<C-D>"));
                Assert.Equal("    hello", _textBuffer.GetLine(0).GetText());
            }

            [WpfFact]
            public void WordCompletion_Simple_Async()
            {
                Create("c dog", "cat");
                _textView.MoveCaretTo(1);
                _vimBuffer.ProcessNotation("<C-N>");
                Dispatcher.DoEvents();
                _vimBuffer.ProcessNotation("<CR>");
                Assert.Equal("cat", _textView.GetLine(0).GetText());
            }
            
            [WpfFact]
            public void WordCompletion_Async_Commit_Space()
            {
                Create("c dog", "cat");
                _textView.MoveCaretTo(1);
                _vimBuffer.ProcessNotation("<C-N>");
                Dispatcher.DoEvents();
                _vimBuffer.ProcessNotation("<Space>");
                Assert.Equal("cat  dog", _textView.GetLine(0).GetText());
            }
            
            [WpfFact]
            public void WordCompletion_Async_Commit_CtrlY()
            {
                Create("c dog", "cat");
                _textView.MoveCaretTo(1);
                _vimBuffer.ProcessNotation("<C-N>");
                Dispatcher.DoEvents();
                _vimBuffer.ProcessNotation("<C-Y>");
                Assert.Equal("cat dog", _textView.GetLine(0).GetText());
            }


#if false
            /// <summary>
            /// Simulate choosing the second possibility in the completion list
            /// </summary>
            [WpfFact]
            private void WordCompletion_ChooseNext_Async()
            {
                Create("c dog", "cat copter");
                _textView.MoveCaretTo(1);
                _vimBuffer.ProcessNotation("<C-N>");
                Dispatcher.DoEvents();
                _vimBuffer.ProcessNotation("<C-N><C-Y>");
                Assert.Equal("copter dog", _textView.GetLine(0).GetText());
            }
#endif

            /// <summary>
            /// Simulate Aborting / Exiting a completion
            /// </summary>
            [WpfFact]
            public void WordCompletion_Abort_Legacy()
            {
                Create("c dog", "cat copter");
                _textView.MoveCaretTo(1);
                _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<C-N>"));
                _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<C-E>"));
                _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<Esc>"));
                Assert.Equal("c dog", _textView.GetLine(0).GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Simulate Aborting / Exiting a completion
            /// </summary>
            [WpfFact]
            public void WordCompletion_Abort_Async()
            {
                Create("c dog", "cat copter");
                _textView.MoveCaretTo(1);
                _vimBuffer.ProcessNotation("<C-N>");
                Dispatcher.DoEvents();
                _vimBuffer.ProcessNotation("<C-E>");
                _vimBuffer.ProcessNotation("<Esc>");
                Assert.Equal("c dog", _textView.GetLine(0).GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            [WpfFact]
            public void WordCompletion_TypeAfter_Async()
            {
                Create("c dog", "cat");
                _textView.MoveCaretTo(1);
                _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<C-N>"));
                _vimBuffer.Process('s');
                Assert.Equal("cats dog", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// Esacpe should both stop word completion and leave insert mode.
            /// </summary>
            [WpfFact]
            public void WordCompletion_Escape_Async()
            {
                Create("c dog", "cat");
                _textView.MoveCaretTo(1);
                _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<C-N>"));
                Dispatcher.DoEvents();
                _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<Esc>"));
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                Assert.Equal("cat dog", _textView.GetLine(0).GetText());
                Assert.Equal(2, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// When there are no matches then no active IWordCompletion should be created and 
            /// it should continue in insert mode
            /// </summary>
            [WpfFact]
            public void WordCompletion_NoMatches()
            {
                Create("c dog");
                _textView.MoveCaretTo(1);
                _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<C-N>"));
                Assert.Equal("c dog", _textView.GetLine(0).GetText());
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.True(_vimBuffer.InsertMode.ActiveWordCompletionSession.IsNone());
            }

            [WpfFact]
            public void EscapeInColumnZero()
            {
                Create("cat", "dog");
                _textView.MoveCaretToLine(1);
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                _vimBuffer.ProcessNotation(@"<Esc>");
                Assert.Equal(0, VimHost.BeepCount);
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// If 'cw' is issued on an indented line consisting of a single
            /// word, the caret shouldn't move
            /// </summary>
            [WpfFact]
            public void ChangeWord_OneIndentedWord()
            {
                Create("    cat", "dog");
                _vimBuffer.Process(VimKey.Escape);
                _textView.MoveCaretTo(4);
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                _vimBuffer.ProcessNotation("cw");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal(4, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// If 'cw' is issued on an indented line consisting of a single
            /// word, and the line is followed by a blank line, the caret
            /// still shouldn't move
            /// </summary>
            [WpfFact]
            public void ChangeWord_OneIndentedWordBeforeBlankLine()
            {
                Create("    cat", "", "dog");
                _vimBuffer.Process(VimKey.Escape);
                _textView.MoveCaretTo(4);
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                _vimBuffer.ProcessNotation("cw");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal(4, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// In general when the caret moves between lines this changes the 'start' point of the
            /// insert mode edit to be the new caret point.  This is not the case when Enter is used
            /// </summary>
            [WpfFact]
            public void EnterDoesntChangeEditStartPoint()
            {
                Create("");
                Assert.Equal(0, _vimBuffer.VimTextBuffer.InsertStartPoint.Value.Position);
                _vimBuffer.ProcessNotation("a<CR>");
                Assert.Equal(_textBuffer.GetLine(1).Start, _textView.GetCaretPoint());
                Assert.Equal(0, _vimBuffer.VimTextBuffer.InsertStartPoint.Value.Position);
            }

            [WpfFact]
            public void Issue498()
            {
                Create("helloworld");
                _textView.MoveCaretTo(5);
                _vimBuffer.ProcessNotation("<c-j>");
                Assert.Equal(new[] { "hello", "world" }, _textBuffer.GetLines());
            }

            /// <summary>
            /// Word forward in insert reaches the end of the buffer
            /// </summary>
            [WpfFact]
            public void WordToEnd()
            {
                Create("cat", "dog");
                _textView.MoveCaretTo(0);
                _vimBuffer.ProcessNotation("<C-Right><C-Right><C-Right>");
                Assert.Equal(_textBuffer.GetLine(1).Start.Add(3), _textView.GetCaretPoint());
            }

            /// <summary>
            /// Word forward in insert reaches the end of the buffer with a final newline
            /// </summary>
            [WpfFact]
            public void WordToEndWithFinalNewLine()
            {
                Create("cat", "dog", "");
                _textView.MoveCaretTo(0);
                _vimBuffer.ProcessNotation("<C-Right><C-Right><C-Right>");
                Assert.Equal(_textBuffer.GetLine(1).Start.Add(3), _textView.GetCaretPoint());
            }

            /// <summary>
            /// Make sure we can process escape
            /// </summary>
            [WpfFact]
            public void CanProcess_Escape()
            {
                Create("");
                Assert.True(_insertMode.CanProcess(KeyInputUtil.EscapeKey));
            }

            /// <summary>
            /// If the active IWordCompletionSession is dismissed via the API it should cause the 
            /// ActiveWordCompletionSession value to be reset as well
            /// </summary>
            [WpfFact]
            public void ActiveWordCompletionSession_Dismissed()
            {
                Create("c cat");
                _textView.MoveCaretTo(1);
                _vimBuffer.ProcessNotation("<C-N>");
                Assert.True(_insertMode.ActiveWordCompletionSession.IsSome());
                _insertMode.ActiveWordCompletionSession.Value.Dismiss();
                Assert.True(_insertMode.ActiveWordCompletionSession.IsNone());
            }

            /// <summary>
            /// When there is an active IWordCompletionSession we should still process all input even though 
            /// the word completion session can only process a limited set of key strokes.  The extra key 
            /// strokes are used to cancel the session and then be processed as normal
            /// </summary>
            [WpfFact]
            public void CanProcess_ActiveWordCompletion()
            {
                Create("c cat");
                _textView.MoveCaretTo(1);
                _vimBuffer.ProcessNotation("<C-N>");
                Assert.True(_insertMode.CanProcess(KeyInputUtil.CharToKeyInput('a')));
            }

            /// <summary>
            /// After a word should return the entire word 
            /// </summary>
            [WpfFact]
            public void GetWordCompletionSpan_AfterWord()
            {
                Create("cat dog");
                _textView.MoveCaretTo(3);
                Assert.Equal("cat", _insertModeRaw.GetWordCompletionSpan().Value.GetText());
            }

            /// <summary>
            /// In the middle of the word should only consider the word up till the caret for the 
            /// completion section
            /// </summary>
            [WpfFact]
            public void GetWordCompletionSpan_MiddleOfWord()
            {
                Create("cat dog");
                _textView.MoveCaretTo(1);
                Assert.Equal("c", _insertModeRaw.GetWordCompletionSpan().Value.GetText());
            }

            /// <summary>
            /// When the caret is on a closing paren and after a word the completion should be for the
            /// word and not for the paren
            /// </summary>
            [WpfFact]
            public void GetWordCompletionSpan_OnParen()
            {
                Create("m(arg)");
                _textView.MoveCaretTo(5);
                Assert.Equal(')', _textView.GetCaretPoint().GetChar());
                Assert.Equal("arg", _insertModeRaw.GetWordCompletionSpan().Value.GetText());
            }

            /// <summary>
            /// This is a sanity check to make sure we don't try anything like jumping backwards.  The 
            /// test should be for the character immediately preceding the caret position.  Here it's 
            /// a blank and there should be nothing returned
            /// </summary>
            [WpfFact]
            public void GetWordCompletionSpan_OnParenWithBlankBefore()
            {
                Create("m(arg )");
                _textView.MoveCaretTo(6);
                Assert.Equal(')', _textView.GetCaretPoint().GetChar());
                Assert.True(_insertModeRaw.GetWordCompletionSpan().IsNone());
            }

            /// <summary>
            /// When provided an empty SnapshotSpan the words should be returned in order from the given
            /// point
            /// </summary>
            [WpfFact]
            public void GetWordCompletions_All()
            {
                Create("cat dog tree");
                var words = _insertModeRaw.WordCompletionUtil.GetWordCompletions(new SnapshotSpan(_textView.TextSnapshot, 3, 0));
                Assert.Equal(
                    new[] { "dog", "tree", "cat" },
                    words.ToList());
            }

            /// <summary>
            /// Don't include any comments or non-words when getting the words from the buffer
            /// </summary>
            [WpfFact]
            public void GetWordCompletions_All_JustWords()
            {
                Create("cat dog // tree &&");
                var words = _insertModeRaw.WordCompletionUtil.GetWordCompletions(new SnapshotSpan(_textView.TextSnapshot, 3, 0));
                Assert.Equal(
                    new[] { "dog", "tree", "cat" },
                    words.ToList());
            }

            /// <summary>
            /// When given a word span only include strings which start with the given prefix
            /// </summary>
            [WpfFact]
            public void GetWordCompletions_Prefix()
            {
                Create("c cat dog // tree && copter");
                var words = _insertModeRaw.WordCompletionUtil.GetWordCompletions(new SnapshotSpan(_textView.TextSnapshot, 0, 1));
                Assert.Equal(
                    new[] { "cat", "copter" },
                    words.ToList());
            }

            /// <summary>
            /// Starting from the middle of a word should consider the part of the word to the right of 
            /// the caret as a word
            /// </summary>
            [WpfFact]
            public void GetWordCompletions_MiddleOfWord()
            {
                Create("test", "ccrook cat caturday");
                var words = _insertModeRaw.WordCompletionUtil.GetWordCompletions(new SnapshotSpan(_textView.GetLine(1).Start, 1));
                Assert.Equal(
                    new[] { "crook", "cat", "caturday" },
                    words.ToList());
            }

            /// <summary>
            /// Don't include any one length values in the return because Vim doesn't include them
            /// </summary>
            [WpfFact]
            public void GetWordCompletions_ExcludeOneLengthValues()
            {
                Create("c cat dog // tree && copter a b c");
                var words = _insertModeRaw.WordCompletionUtil.GetWordCompletions(new SnapshotSpan(_textView.TextSnapshot, 0, 1));
                Assert.Equal(
                    new[] { "cat", "copter" },
                    words.ToList());
            }

            /// <summary>
            /// Ensure that all known character values are considered direct input.  They cause direct
            /// edits to the buffer.  They are not commands.
            /// </summary>
            [WpfFact]
            public void IsDirectInput_Chars()
            {
                Create();
                foreach (var cur in KeyInputUtilTest.CharAll)
                {
                    var input = KeyInputUtil.CharToKeyInput(cur);
                    Assert.True(_insertMode.CanProcess(input));
                    Assert.True(_insertMode.IsDirectInsert(input));
                }
            }

            /// <summary>
            /// Certain keys do cause buffer edits but are not direct input.  They are interpreted by Vim
            /// and given specific values based on settings.  While they cause edits the values passed down
            /// don't directly go to the buffer
            /// </summary>
            [WpfFact]
            public void IsDirectInput_SpecialKeys()
            {
                Create();
                Assert.False(_insertMode.IsDirectInsert(KeyInputUtil.EnterKey));
                Assert.False(_insertMode.IsDirectInsert(KeyInputUtil.CharToKeyInput('\t')));
            }

            /// <summary>
            /// Make sure that Escape in insert mode runs a command even if the caret is in virtual 
            /// space
            /// </summary>
            [WpfFact]
            public void Escape_RunCommand()
            {
                Create();
                _textView.SetText("hello world", "", "again");
                _textView.MoveCaretTo(_textView.GetLine(1).Start.Position, 4);
                var didRun = false;
                _insertMode.CommandRan += (sender, e) => didRun = true;
                _insertMode.Process(KeyInputUtil.EscapeKey);
                Assert.True(didRun);
            }

            /// <summary>
            /// Make sure to dismiss any active completion windows when exiting.  We had the choice
            /// between having escape cancel only the window and escape canceling and returning
            /// to presambly normal mode.  The unanimous user feedback is that Escape should leave 
            /// insert mode no matter what.  
            /// </summary>
            [WpfTheory]
            [InlineData("<Esc>")]
            [InlineData("<C-[>")]
            public void Escape_DismissCompletionWindows(string notation)
            {
                Create();
                _textView.SetText("h hello world", 1);
                _vimBuffer.ProcessNotation("<C-N>");
                Assert.True(_insertMode.ActiveWordCompletionSession.IsSome());
                _vimBuffer.ProcessNotation(notation);
                Assert.True(_insertMode.ActiveWordCompletionSession.IsNone());
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
            }

            [WpfFact]
            public void Control_OpenBracket1()
            {
                Create();
                var ki = KeyInputUtil.CharWithControlToKeyInput('[');
                var name = new KeyInputSet(ki);
                Assert.Contains(name, _insertMode.CommandNames);
            }

            /// <summary>
            /// The CTRL-O command should bind to a one time command for normal mode
            /// </summary>
            [WpfFact]
            public void OneTimeCommand()
            {
                Create();
                var res = _insertMode.Process(KeyNotationUtil.StringToKeyInput("<C-o>"));
                Assert.True(res.IsSwitchModeOneTimeCommand());
            }

            /// <summary>
            /// Ensure that Enter maps to the appropriate InsertCommand and shows up as the LastCommand
            /// after processing
            /// </summary>
            [WpfFact]
            public void Process_InsertNewLine()
            {
                Create("");
                _vimBuffer.ProcessNotation("<CR>");
                Assert.True(_insertModeRaw._sessionData.CombinedEditCommand.IsSome());
                Assert.True(_insertModeRaw._sessionData.CombinedEditCommand.Value.IsInsertNewLine);
            }

            /// <summary>
            /// Ensure that a character maps to the DirectInsert and shows up as the LastCommand
            /// after processing
            /// </summary>
            [WpfFact]
            public void Process_DirectInsert()
            {
                Create("");
                _vimBuffer.ProcessNotation("c");
                Assert.True(_insertModeRaw._sessionData.CombinedEditCommand.IsSome());
                Assert.True(_insertModeRaw._sessionData.CombinedEditCommand.Value.IsInsert);
            }
        }

        public abstract class TabSettingsTest : InsertModeIntegrationTest
        {
            public sealed class Configuration1 : TabSettingsTest
            {
                public Configuration1()
                {
                    Create();
                    _vimBuffer.GlobalSettings.Backspace = "eol,start,indent";
                    _vimBuffer.LocalSettings.TabStop = 5;
                    _vimBuffer.LocalSettings.SoftTabStop = 6;
                    _vimBuffer.LocalSettings.ShiftWidth = 6;
                    _vimBuffer.LocalSettings.ExpandTab = false;
                    _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                }

                [WpfFact]
                public void SimpleIndent()
                {
                    _vimBuffer.Process("\t");
                    Assert.Equal("\t ", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void SimpleIndentAndType()
                {
                    _vimBuffer.Process("\th");
                    Assert.Equal("\t h", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void DeleteSimpleIndent()
                {
                    _vimBuffer.Process(VimKey.Tab, VimKey.Back);
                    Assert.Equal("", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void DeleteIndentWithChanges()
                {
                    _textBuffer.SetText("\t cat");
                    _textView.MoveCaretTo(2);
                    Assert.Equal('c', _textView.GetCaretPoint().GetChar());
                    _vimBuffer.Process(VimKey.Back);
                    Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                }
            }

            public sealed class Configuration2 : TabSettingsTest
            {
                public Configuration2()
                {
                    Create();
                    _vimBuffer.GlobalSettings.Backspace = "eol,start,indent";
                    _vimBuffer.LocalSettings.TabStop = 8;
                    _vimBuffer.LocalSettings.SoftTabStop = 0;
                    _vimBuffer.LocalSettings.ShiftWidth = 8;
                    _vimBuffer.LocalSettings.ExpandTab = false;
                    _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                }

                [WpfFact]
                public void SimpleIndent()
                {
                    _vimBuffer.Process(VimKey.Tab);
                    Assert.Equal("\t", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void DoubleIndent()
                {
                    _vimBuffer.Process(VimKey.Tab, VimKey.Tab);
                    Assert.Equal("\t\t", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void SimpleIndentAndType()
                {
                    _vimBuffer.Process("\ta");
                    Assert.Equal("\ta", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void DeleteSimpleIndent()
                {
                    _vimBuffer.Process(VimKey.Tab, VimKey.Back);
                    Assert.Equal("", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void DeleteDoubleIndent()
                {
                    _vimBuffer.Process(VimKey.Tab, VimKey.Tab, VimKey.Back);
                    Assert.Equal("\t", _textBuffer.GetLine(0).GetText());
                    _vimBuffer.Process(VimKey.Back);
                    Assert.Equal("", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void DeleteIndentWithContent()
                {
                    _textBuffer.SetText("\tcat");
                    _textView.MoveCaretTo(1);
                    _vimBuffer.Process(VimKey.Back);
                    Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                }
            }

            public sealed class Configuration3 : TabSettingsTest
            {
                public Configuration3()
                {
                    Create();
                    _vimBuffer.GlobalSettings.Backspace = "eol,start,indent";
                    _vimBuffer.LocalSettings.TabStop = 4;
                    _vimBuffer.LocalSettings.SoftTabStop = 0;
                    _vimBuffer.LocalSettings.ShiftWidth = 4;
                    _vimBuffer.LocalSettings.ExpandTab = true;
                    _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                }

                [WpfFact]
                public void SimpleIndent()
                {
                    _vimBuffer.Process(VimKey.Tab);
                    Assert.Equal("    ", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void DoubleIndent()
                {
                    _vimBuffer.Process(VimKey.Tab, VimKey.Tab);
                    Assert.Equal("        ", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void SimpleIndentAndType()
                {
                    _vimBuffer.Process("\ta");
                    Assert.Equal("    a", _textBuffer.GetLine(0).GetText());
                }

                /// <summary>
                /// 'sts' isn't set here hence the backspace is just interpretted as deleting a single
                /// character 
                /// </summary>
                [WpfFact]
                public void DeleteSimpleIndent()
                {
                    _vimBuffer.Process(VimKey.Tab, VimKey.Back);
                    Assert.Equal("   ", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void DeleteDoubleIndent()
                {
                    _vimBuffer.Process(VimKey.Tab, VimKey.Tab, VimKey.Back, VimKey.Back);
                    Assert.Equal("      ", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void DeleteIndentWithContent()
                {
                    _textBuffer.SetText("    cat");
                    _textView.MoveCaretTo(4);
                    _vimBuffer.Process(VimKey.Back);
                    Assert.Equal("   cat", _textBuffer.GetLine(0).GetText());
                }
            }

            public sealed class Configuration4 : TabSettingsTest
            {
                public Configuration4()
                {
                    Create();
                    _vimBuffer.GlobalSettings.Backspace = "eol,start,indent";
                    _vimBuffer.LocalSettings.TabStop = 4;
                    _vimBuffer.LocalSettings.SoftTabStop = 4;
                    _vimBuffer.LocalSettings.ShiftWidth = 4;
                    _vimBuffer.LocalSettings.ExpandTab = true;
                    _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                }

                [WpfFact]
                public void SimpleIndent()
                {
                    _vimBuffer.Process(VimKey.Tab);
                    Assert.Equal("    ", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void DoubleIndent()
                {
                    _vimBuffer.Process(VimKey.Tab, VimKey.Tab);
                    Assert.Equal("        ", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void SimpleIndentAndType()
                {
                    _vimBuffer.Process("\ta");
                    Assert.Equal("    a", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void IndentMixed()
                {
                    _textBuffer.SetText("c\t");
                    _textView.MoveCaretTo(2);
                    _vimBuffer.Process("\t");
                    Assert.Equal("c\t    ", _textBuffer.GetLine(0).GetText());
                }

                /// <summary>
                /// 'sts' isn't set here hence the backspace is just interpretted as deleting a single
                /// character 
                /// </summary>
                [WpfFact]
                public void DeleteSimpleIndent()
                {
                    _vimBuffer.Process(VimKey.Tab, VimKey.Back);
                    Assert.Equal("", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void DeleteDoubleIndent()
                {
                    _vimBuffer.Process(VimKey.Tab, VimKey.Tab, VimKey.Back);
                    Assert.Equal("    ", _textBuffer.GetLine(0).GetText());
                    _vimBuffer.Process(VimKey.Back);
                    Assert.Equal("", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void DeleteIndentWithContent()
                {
                    _textBuffer.SetText("    cat");
                    _textView.MoveCaretTo(4);
                    _vimBuffer.Process(VimKey.Back);
                    Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void DeleteMixedIndentWithContent()
                {
                    _textBuffer.SetText("     cat");
                    _textView.MoveCaretTo(5);
                    _vimBuffer.Process(VimKey.Back);
                    Assert.Equal("    cat", _textBuffer.GetLine(0).GetText());
                    _vimBuffer.Process(VimKey.Back);
                    Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void DeleteRealTabWithContent()
                {
                    _textBuffer.SetText("\tcat");
                    _textView.MoveCaretTo(1);
                    _vimBuffer.Process(VimKey.Back);
                    Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                }

                /// <summary>
                /// When the caret is in virtual space the tab command should fill in the 
                /// virtual spaces on the blank line
                /// </summary>
                [WpfFact]
                public void TabFillInVirtualSpacesBlankLine()
                {
                    _textView.MoveCaretTo(0, virtualSpaces: 4);
                    _vimBuffer.Process(VimKey.Tab);
                    Assert.Equal(new string(' ', 8), _textBuffer.GetLine(0).GetText());
                    Assert.Equal(8, _textView.GetCaretPoint().Position);
                }

                /// <summary>
                /// When the caret is in virtual space the tab command should fill in the 
                /// virtual spaces on the blank line
                /// </summary>
                [WpfFact]
                public void TabFillInVirtualSpacesNonBlankLine()
                {
                    _textBuffer.SetText("ba");
                    _textView.MoveCaretTo(2, virtualSpaces: 2);
                    _vimBuffer.Process(VimKey.Tab);
                    Assert.Equal("ba" + new string(' ', 6), _textBuffer.GetLine(0).GetText());
                    Assert.Equal(8, _textView.GetCaretPoint().Position);
                }
            }

            public sealed class Configuration5 : TabSettingsTest
            {
                public Configuration5()
                {
                    Create();
                    _vimBuffer.GlobalSettings.Backspace = "eol,start,indent";
                    _vimBuffer.LocalSettings.TabStop = 8;
                    _vimBuffer.LocalSettings.SoftTabStop = 4;
                    _vimBuffer.LocalSettings.ShiftWidth = 4;
                    _vimBuffer.LocalSettings.ExpandTab = false;
                    _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                }

                /// <summary>
                /// 'sts' has precedence over 'ts' here 
                /// </summary>
                [WpfFact]
                public void SimpleIndent()
                {
                    _vimBuffer.Process(VimKey.Tab);
                    Assert.Equal("    ", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void DoubleIndent()
                {
                    _vimBuffer.Process(VimKey.Tab, VimKey.Tab);
                    Assert.Equal("\t", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void SimpleIndentAndType()
                {
                    _vimBuffer.Process("\ta");
                    Assert.Equal("    a", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void DeleteSimpleIndent()
                {
                    _vimBuffer.Process(VimKey.Tab, VimKey.Back);
                    Assert.Equal("", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void DeleteDoubleIndent()
                {
                    _vimBuffer.Process(VimKey.Tab, VimKey.Tab);
                    Assert.Equal("\t", _textBuffer.GetLine(0).GetText());
                    _vimBuffer.Process(VimKey.Back);
                    Assert.Equal("    ", _textBuffer.GetLine(0).GetText());
                    _vimBuffer.Process(VimKey.Back);
                    Assert.Equal("", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void DeleteIndentWithContent()
                {
                    _textBuffer.SetText("    cat");
                    _textView.MoveCaretTo(4);
                    _vimBuffer.Process(VimKey.Back);
                    Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void DeleteMixedIndentWithContent()
                {
                    _textBuffer.SetText("     cat");
                    _textView.MoveCaretTo(5);
                    _vimBuffer.Process(VimKey.Back);
                    Assert.Equal("    cat", _textBuffer.GetLine(0).GetText());
                    _vimBuffer.Process(VimKey.Back);
                    Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void DeleteRealTabWithContent()
                {
                    _textBuffer.SetText("\tcat");
                    _textView.MoveCaretTo(1);
                    _vimBuffer.Process(VimKey.Back);
                    Assert.Equal("    cat", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void DeleteRealTabWithContentTwitce()
                {
                    _textBuffer.SetText("\tcat");
                    _textView.MoveCaretTo(1);
                    _vimBuffer.Process(VimKey.Back, VimKey.Back);
                    Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                }

                /// <summary>
                /// When deleting indent we don't convert spaces to tabs even if it lines up correctly
                /// with the tabstop setting
                /// </summary>
                [WpfFact]
                public void DeleteTripleIndentWithContent()
                {
                    _vimBuffer.Process("\t\t\tcat");
                    Assert.Equal("\t" + (new string(' ', 4)) + "cat", _textBuffer.GetLine(0).GetText());
                    _textView.MoveCaretTo(5);
                    _vimBuffer.Process(VimKey.Back);
                    Assert.Equal("\tcat", _textBuffer.GetLine(0).GetText());
                    _vimBuffer.Process(VimKey.Back);
                    Assert.Equal((new string(' ', 4)) + "cat", _textBuffer.GetLine(0).GetText());
                    _vimBuffer.Process(VimKey.Back);
                    Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                }
            }

            public sealed class Configuration6 : TabSettingsTest
            {
                public Configuration6()
                {
                    Create();
                    _vimBuffer.GlobalSettings.Backspace = "eol,start,indent";
                    _vimBuffer.LocalSettings.TabStop = 4;
                    _vimBuffer.LocalSettings.SoftTabStop = 4;
                    _vimBuffer.LocalSettings.ShiftWidth = 4;
                    _vimBuffer.LocalSettings.ExpandTab = false;
                    _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                }

                /// <summary>
                /// 'sts' has precedence over 'ts' here 
                /// </summary>
                [WpfFact]
                public void SimpleIndent()
                {
                    _vimBuffer.Process(VimKey.Tab);
                    Assert.Equal("\t", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void DoubleIndent()
                {
                    _vimBuffer.Process(VimKey.Tab, VimKey.Tab);
                    Assert.Equal("\t\t", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void IndentOverSpaces()
                {
                    _textBuffer.Insert(0, " ");
                    _vimBuffer.Process("\t");
                    Assert.Equal("\t", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void IndentOverText()
                {
                    _textBuffer.Insert(0, "c");
                    _vimBuffer.Process("\t");
                    Assert.Equal("c\t", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void IndentBetweenText()
                {
                    _textBuffer.SetText("c m");
                    _textView.MoveCaretTo(1);
                    _vimBuffer.Process("\t");
                    Assert.Equal(2, _textView.GetCaretPoint());
                    Assert.Equal("c\t m", _textBuffer.GetLine(0).GetText());
                    _vimBuffer.Process("\t");
                    Assert.Equal("c\t\t m", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void SimpleIndentAndType()
                {
                    _vimBuffer.Process("\ta");
                    Assert.Equal("\ta", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void IndentNormalizesTabsSpaces()
                {
                    _vimBuffer.Process("c   \t");
                    Assert.Equal("c\t\t", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void DeleteSimpleIndent()
                {
                    _vimBuffer.Process(VimKey.Tab, VimKey.Back);
                    Assert.Equal("", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void DeleteDoubleIndent()
                {
                    _vimBuffer.Process(VimKey.Tab, VimKey.Tab);
                    Assert.Equal("\t\t", _textBuffer.GetLine(0).GetText());
                    _vimBuffer.Process(VimKey.Back);
                    Assert.Equal("\t", _textBuffer.GetLine(0).GetText());
                    _vimBuffer.Process(VimKey.Back);
                    Assert.Equal("", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void DeleteIndentSpacesWithContent()
                {
                    _textBuffer.SetText("    cat");
                    _textView.MoveCaretTo(4);
                    _vimBuffer.Process(VimKey.Back);
                    Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void DeleteMixedIndentWithContent()
                {
                    _textBuffer.SetText("\t cat");
                    _textView.MoveCaretTo(2);
                    _vimBuffer.Process(VimKey.Back);
                    Assert.Equal("\tcat", _textBuffer.GetLine(0).GetText());
                    _vimBuffer.Process(VimKey.Back);
                    Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void DeleteRealTabWithContent()
                {
                    _textBuffer.SetText("\tcat");
                    _textView.MoveCaretTo(1);
                    _vimBuffer.Process(VimKey.Back);
                    Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                }

                /// <summary>
                /// When deleting indent we don't convert spaces to tabs even if it lines up correctly
                /// with the tabstop setting
                /// </summary>
                [WpfFact]
                public void DeleteTripleIndentWithContent()
                {
                    _vimBuffer.Process("\t\t\tcat");
                    Assert.Equal("\t\t\tcat", _textBuffer.GetLine(0).GetText());
                    _textView.MoveCaretTo(3);
                    _vimBuffer.Process(VimKey.Back);
                    Assert.Equal("\t\tcat", _textBuffer.GetLine(0).GetText());
                    _vimBuffer.Process(VimKey.Back);
                    Assert.Equal("\tcat", _textBuffer.GetLine(0).GetText());
                    _vimBuffer.Process(VimKey.Back);
                    Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                }
            }

            public sealed class TabSettingsMiscTest : TabSettingsTest
            {
                /// <summary>
                /// Typing a space while editting will disable 'sts' for that line
                /// </summary>
                [WpfFact]
                public void TypingSpaceDisablesSoftTabStop()
                {
                    Create("");
                    _globalSettings.Backspace = "start,eol,indent";
                    _localSettings.SoftTabStop = 4;
                    _localSettings.TabStop = 4;
                    _localSettings.ExpandTab = true;
                    _vimBuffer.ProcessNotation("<Tab> <BS><BS>");
                    Assert.Equal(new string(' ', 3), _textBuffer.GetLine(0).GetText());
                }

                /// <summary>
                /// Once insert mode is left and re-entered 'sts' is restored 
                /// </summary>
                [WpfFact]
                public void TypingSpaceThenEscapeRestoresSoftTabStop()
                {
                    Create("");
                    _globalSettings.Backspace = "start,eol,indent";
                    _localSettings.SoftTabStop = 4;
                    _localSettings.TabStop = 4;
                    _localSettings.ExpandTab = true;
                    _vimBuffer.ProcessNotation("<Tab> <Esc>a<BS><BS>");
                    Assert.Equal("", _textBuffer.GetLine(0).GetText());
                }

                /// <summary>
                /// A caret movement also restores 'sts'
                /// </summary>
                [WpfFact]
                public void TypingSpaceThenCaretMoveRestoresSoftTabStop()
                {
                    // handling is different without VirtualEdit=onemore
                    Create("");
                    _globalSettings.Backspace = "start,eol,indent";
                    _localSettings.SoftTabStop = 4;
                    _localSettings.TabStop = 4;
                    _localSettings.ExpandTab = true;
                    _vimBuffer.ProcessNotation("<Tab> <Esc>");
                    _textView.MoveCaretTo(0);
                    _textView.MoveCaretTo(4);
                    _vimBuffer.ProcessNotation("i<BS><BS>");
                    Assert.Equal(" ", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void TypingSpaceThenCaretMoveRestoresSoftTabStopWithVeOneMore()
                {
                    Create("");
                    _globalSettings.Backspace = "start,eol,indent";
                    _globalSettings.VirtualEdit = "onemore";
                    _localSettings.SoftTabStop = 4;
                    _localSettings.TabStop = 4;
                    _localSettings.ExpandTab = true;
                    _vimBuffer.ProcessNotation("<Tab> <Esc>");
                    _textView.MoveCaretTo(0);
                    _textView.MoveCaretTo(5);
                    _vimBuffer.ProcessNotation("i<BS><BS>");
                    Assert.Equal("", _textBuffer.GetLine(0).GetText());
                }
            }
        }

        public sealed class InsertStartColumnTest : InsertModeIntegrationTest
        {
            [WpfTheory]
            [InlineData("<Up>")]
            [InlineData("k")]
            [InlineData("<C-k>")]
            public void LineUp(string key)
            {
                Create("abc def", "ghi jkl", "");
                _textView.MoveCaretToLine(1, 4);
                _vimBuffer.ProcessNotation($"foo <C-g>{key}bar <Esc>");
                Assert.Equal(new[] { "abc bar def", "ghi foo jkl", "" }, _textBuffer.GetLines());
            }
            [WpfTheory]
            [InlineData("<Down>")]
            [InlineData("j")]
            [InlineData("<C-j>")]
            public void LineDown(string key)
            {
                Create("abc def", "ghi jkl", "");
                _textView.MoveCaretToLine(0, 4);
                _vimBuffer.ProcessNotation($"foo <C-g>{key}bar <Esc>");
                Assert.Equal(new[] { "abc foo def", "ghi bar jkl", "" }, _textBuffer.GetLines());
            }
        }

        public sealed class InsertLiteralTests : InsertModeIntegrationTest
        {
            /// <summary>
            /// Insert a literal escape
            /// </summary>
            [WpfFact]
            public void InsertEscape()
            {
                Create("", "");
                _vimBuffer.ProcessNotation("<C-q><Esc>");
                Assert.Equal("\u001b", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// Make sure a literal tab can be inserted even if expandtab is
            /// set and even if the host custom processes ordinary text
            /// </summary>
            [WpfFact]
            public void InsertTab()
            {
                Create("", "");
                _localSettings.ExpandTab = true;
                var count = 0;
                VimHost.TryCustomProcessFunc =
                    (textView, command) =>
                    {
                        if (command.IsInsertLiteral)
                        {
                            Assert.Equal("\t", command.AsInsertLiteral().Text);
                            count += 1;
                        }

                        return false;
                    };
                _vimBuffer.ProcessNotation("<C-q><Tab>");
                Assert.Equal("\t", _textBuffer.GetLine(0).GetText());
                Assert.Equal(1, count);
            }

            /// <summary>
            /// Insert a decimal escape
            /// </summary>
            [WpfFact]
            public void InsertDecimalEscape()
            {
                Create("", "");
                _vimBuffer.ProcessNotation("<C-q>027");
                Assert.Equal("\u001b", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// Insert a decimal escape
            /// </summary>
            [WpfFact]
            public void InsertShortDecimalEscape()
            {
                Create("", "");
                _vimBuffer.ProcessNotation("<C-q>27 ");
                Assert.Equal("\u001b ", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// Insert an octal escape
            /// </summary>
            [WpfFact]
            public void InsertOctalEscape()
            {
                Create("", "");
                _vimBuffer.ProcessNotation("<C-q>o033");
                Assert.Equal("\u001b", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// Insert an uppercase octal escape
            /// </summary>
            [WpfFact]
            public void InsertUppercaseOctalEscape()
            {
                Create("", "");
                _vimBuffer.ProcessNotation("<C-q>O033");
                Assert.Equal("\u001b", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// Insert a hex escape
            /// </summary>
            [WpfFact]
            public void InsertHexEscape()
            {
                Create("", "");
                _vimBuffer.ProcessNotation("<C-q>x1b");
                Assert.Equal("\u001b", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// Insert an uppercase hex escape
            /// </summary>
            [WpfFact]
            public void InsertUppercaseHexEscape()
            {
                Create("", "");
                _vimBuffer.ProcessNotation("<C-q>X1B");
                Assert.Equal("\u001b", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// Insert an utf16 escape
            /// </summary>
            [WpfFact]
            public void InsertUnicodeEscape()
            {
                Create("", "");
                _vimBuffer.ProcessNotation("<C-q>u001b");
                Assert.Equal("\u001b", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// Insert an utf32 alien
            /// </summary>
            [WpfFact]
            public void InsertUnicodeAlien()
            {
                Create("", "");
                _vimBuffer.ProcessNotation("<C-q>U0001F47D");
                Assert.Equal("\U0001F47D", _textBuffer.GetLine(0).GetText()); // 👽
            }
        }

        public sealed class AbbreviationTests : InsertModeIntegrationTest
        {
            [WpfTheory]
            [InlineData(" ")]
            [InlineData("#")]
            [InlineData("$")]
            public void Simple(string keyNotation)
            {
                Create("");
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                _vimBuffer.ProcessNotation(":ab cc comment this", enter: true);
                _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                _vimBuffer.ProcessNotation("cc");
                Assert.Equal("cc", _textBuffer.GetLineText(0));
                _vimBuffer.ProcessNotation(keyNotation);
                Assert.Equal($"comment this{keyNotation}", _textBuffer.GetLineText(0));
            }

            /// <summary>
            /// There are a lot of rules and cases around when an expansion should or should not occur for 
            /// the various abbreviation kinds. This theory just enumerates all the cases covered in 
            /// `:help abbreviate`
            /// </summary>
            [WpfTheory]
            [InlineData("cc hello", "", "cc ", "hello ")]
            [InlineData("cc hello", "", "cc", "cc")] // Won't expand until non-keyword is typed
            [InlineData("cc hello", "", "ccc", "ccc")] // Won't expand until non-keyword is typed
            [InlineData("cc hello", "", "ccc ", "ccc ")] // The ccc doesn't match the abbreviation cc
            [InlineData("cc hello", "", "lcc ", "lcc ")] // The lcc doesn't match the abbreviation cc
            [InlineData("cc hello", "c", "c ", "cc ")]
            [InlineData("cc hello", "c", "cc ", "chello ")] // Match computed against typed text hence
            [InlineData("cc hello", "", "#cc ", "#hello ")] // Match starts at the non-keyword
            [InlineData("cc hello", "#", "cc ", "#hello ")]
            [InlineData("d dog", "", "#d ", "#d ")] // Single character abbreviation only works after space / tab / newline
            [InlineData("d dog", "", " d ", " dog ")] // Single character abbreviation only works after space / tab / newline
            [InlineData("d dog", "a", "d ", "adog ")] // Even for single character it only checks typed text
            [InlineData("#d dog", "", "#d ", "dog ")] // End-id
            [InlineData("#d dog", "", "##d ", "##d ")]
            [InlineData("#d dog", "", "#d#", "dog#")]
            [InlineData("#r rog", "", "f#r ", "frog ")]
            [InlineData("#r rog", "f", "#r ", "frog ")]
            [InlineData("#d dog", "#", "d ", "#d ")]
            [InlineData("dog# dog pound", "", "dog# ", "dog pound ")]
            [InlineData("dg dog", "", "dg<C-]>", "dog")] // <C-]> completes insertion without adding extra space
            [InlineData("dg dog", "", "dg<C-]><C-]>", "dog")] // <C-]> 
            public void RulesSingleLine(string abbreviate, string text, string typed, string expectedText)
            {
                Create();
                _textBuffer.SetTextContent(text);
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                _vimBuffer.Process($":ab {abbreviate}", enter: true);
                _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                _textView.MoveCaretTo(_vimBuffer.TextBuffer.GetEndPoint());
                _vimBuffer.ProcessNotation(typed);
                Assert.Equal(expectedText, _textBuffer.GetLineText(0));
            }

            [WpfTheory]
            [InlineData("dg dog", "", "dg<CR>", "dog\r\n")]
            [InlineData("d dog", "", "d<CR>", "dog\r\n")] // Single character completes when only thing on the line
            [InlineData("<CR>d dog", "", "<CR>d ", "\r\nd ")]
            [InlineData("<CR>d dog", "", "<CR>d<CR>", "\r\nd\r\n")]
            public void RulesMultiLine(string abbreviate, string text, string typed, string expectedText)
            {
                Create();
                _textBuffer.SetTextContent(text);
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                _vimBuffer.Process(":set noeol", enter: true);
                _vimBuffer.Process($":ab {abbreviate}", enter: true);
                _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                _textView.MoveCaretTo(_vimBuffer.TextBuffer.GetEndPoint());
                _vimBuffer.ProcessNotation(typed);
                Assert.Equal(expectedText, _textBuffer.CurrentSnapshot.GetText());
            }

            [WpfTheory]
            [InlineData("dog cat", "dd dog", "", "dd ", "cat ")]
            [InlineData("dog cat", "cat dog", "", "cat ", "cat ")]
            [InlineData("dogs all the toys", "dd dog", "", "dd ", "dog ")] // Partial remap is treated as no remap
            [InlineData("dog# dog pound", "dd dog", "", "dd#", "dog pound#")] // Trigger key is repeated when it completes a key mapping
            [InlineData("dog tree", "dd dog", "", "dd!", "tree!")] // Trigger key is repeated when it completes a key mapping
            [InlineData("dog tree", "dd dogs", "", "dd!", "trees!")] // Trigger key is repeated when it completes a key mapping
            public void RulesRemap(string keyMap, string abbreviate, string text, string typed, string expectedText)
            {
                Create();
                _textBuffer.SetTextContent(text);
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                _vimBuffer.Process(":set noeol", enter: true);
                _vimBuffer.Process($":imap {keyMap}", enter: true);
                _vimBuffer.Process($":ab {abbreviate}", enter: true);
                _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                _textView.MoveCaretTo(_vimBuffer.TextBuffer.GetEndPoint());
                _vimBuffer.ProcessNotation(typed);
                Assert.Equal(expectedText, _textBuffer.CurrentSnapshot.GetText());
            }

            [WpfTheory]
            [InlineData(":ab dd dog<CR>:unab dd<CR>idd ", "dd ")]
            [InlineData(":ab <buffer> dd dog<CR>:unab dd<CR>idd ", "dog ")] // Global clear doesn't affect buffer
            [InlineData(":iab dd global<CR>:iab <buffer> dd local<CR>:iunab dd<CR>idd ", "local ")]
            [InlineData(":iab dd global<CR>:iab <buffer> dd local<CR>:iunab <buffer>dd<CR>idd ", "global ")]
            public void Unabbreviate(string command, string expectedText)
            {
                Create();
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                _vimBuffer.Process(":set noeol", enter: true);
                _vimBuffer.ProcessNotation(command);
                Assert.Equal(expectedText, _textBuffer.CurrentSnapshot.GetText());
            }

            [WpfFact]
            public void EscapeCompletes()
            {
                Create();
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                _vimBuffer.Process(":set noeol", enter: true);
                _vimBuffer.Process($":ab dg dog", enter: true);
                _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                _vimBuffer.ProcessNotation("dg<Esc>");
                Assert.Equal("dog", _textBuffer.CurrentSnapshot.GetText());
            }

            [WpfFact]
            public void Repeat()
            {
                Create("");
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                _vimBuffer.Process(":set noeol", enter: true);
                _vimBuffer.Process(":ab dg dog", enter: true);
                _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                _vimBuffer.ProcessNotation("dg <Esc>.");
                Assert.Equal("dogdog  ", _textBuffer.GetLineText(0));
            }

            [WpfFact]
            public void RepeatDoesntCheckMapping()
            {
                Create("");
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                _vimBuffer.Process(":set noeol", enter: true);
                _vimBuffer.Process(":ab dg dog", enter: true);
                _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                _vimBuffer.ProcessNotation("dg <Esc>");
                _vimBuffer.Vim.GlobalAbbreviationMap.ClearAbbreviations();
                _vimBuffer.ProcessNotation(".");
                Assert.Equal("dogdog  ", _textBuffer.GetLineText(0));
            }

            /// <summary>
            /// This makes sure the trigger key goes through normal insert mode processing vs. being inserted
            /// literally. Inserting it literally would ignore settings like expandtab, shiftwidth, etc ...
            /// </summary>
            [WpfFact]
            public void TriggerKeyInputNormalProcessingTab()
            {
                Create("");
                _localSettings.ShiftWidth = 6;
                _localSettings.SoftTabStop = 6;
                _localSettings.TabStop = 6;
                _localSettings.ExpandTab = true;
                _localSettings.EndOfLine = false;
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                _vimBuffer.Process(":ab dg dog", enter: true);
                _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                _vimBuffer.ProcessNotation("dg<tab>");
                Assert.Equal("dog   ", _textBuffer.GetLineText(0));
            }

            [WpfFact]
            public void TriggerKeyInputNormalProcessingCursor()
            {
                Create("");
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                _vimBuffer.Process(":set noeol", enter: true);
                _vimBuffer.Process(":ab if if ()<Left>", enter: true);
                _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                _vimBuffer.ProcessNotation("if ");
                Assert.Equal("if ( )", _textBuffer.GetLineText(0));
                Assert.Equal(5, _textView.GetCaretPoint().Position);
            }

            [WpfFact]
            public void BufferOnlyReplace()
            {
                Create("");
                var vimBuffer2 = CreateVimBuffer();
                vimBuffer2.ProcessNotation(":ab <buffer> dg dog", enter: true);
                _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                _vimBuffer.ProcessNotation("dg<Space>");
                Assert.Equal("dg ", _vimBuffer.TextBuffer.GetLineText(0));
                vimBuffer2.SwitchMode(ModeKind.Insert, ModeArgument.None);
                vimBuffer2.ProcessNotation("dg<Space>");
                Assert.Equal("dog ", vimBuffer2.TextBuffer.GetLineText(0));
            }

            /// <summary>
            /// A buffer only clear should clear mappings specific to that buffer, not to other buffers
            /// </summary>
            [WpfFact]
            public void BufferOnlyClear()
            {
                Create("");
                var vimBuffer2 = CreateVimBuffer();
                vimBuffer2.ProcessNotation(":ab <buffer> dg dog", enter: true);
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                _vimBuffer.ProcessNotation(":abc <buffer>", enter: true);
                vimBuffer2.SwitchMode(ModeKind.Insert, ModeArgument.None);
                vimBuffer2.ProcessNotation("dg<Space>");
                Assert.Equal("dog ", vimBuffer2.TextBuffer.GetLineText(0));
            }

            /// <summary>
            /// A global clear should not clear any buffer specific abbreviations
            /// </summary>
            [WpfFact]
            public void GlobalClearLeavesBuffer()
            {
                Create("");
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                _vimBuffer.ProcessNotation(":ab <buffer> dd dog", enter: true);
                _vimBuffer.ProcessNotation(":ab cc hello", enter: true);
                _vimBuffer.ProcessNotation(":abc", enter: true);
                _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                _vimBuffer.ProcessNotation("cc dd ");
                Assert.Equal("cc dog ", _vimBuffer.TextBuffer.GetLineText(0));
            }

            [WpfFact]
            public void FailedAbbreviationCompleteIsNotError()
            {
                Create("");
                _vimBuffer.ProcessNotation("<C-]>");
                Assert.Equal(0, VimHost.BeepCount);
            }

            [WpfFact]
            public void CommandAndInsertCanHaveDifferentAbbreviationReplacements()
            {
                Create("");
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                _vimBuffer.ProcessNotation(":iab dd dog", enter: true);
                _vimBuffer.ProcessNotation(":cab dd cat", enter: true);
                _vimBuffer.ProcessNotation("idd ");
                Assert.Equal("dog ", _textBuffer.GetLineText(0));
            }
        }

        public sealed class AtomicInsertTests : InsertModeIntegrationTest
        {
            protected override void Create(ModeArgument argument, params string[] lines)
            {
                base.Create(argument, lines);
                _globalSettings.AtomicInsert = true;
                _localSettings.EndOfLine = false;
            }

            /// <summary>
            /// Just a sanity check that insert works normally when atomic inserts are enabled
            /// </summary>
            [WpfFact]
            public void SimpleInsert()
            {
                Create("");
                _vimBuffer.Process("hello");
                _vimBuffer.Process(VimKey.Escape);
                Assert.Equal("hello", _textBuffer.GetLine(0).GetText());
                _textView.MoveCaretTo(0);
                _vimBuffer.Process(".");
                Assert.Equal("hellohello", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// Simulate a typo, followed by the left arrow key to correct
            /// </summary>
            [WpfFact]
            public void InsertWithArrowLeft()
            {
                Create("");
                _vimBuffer.Process("helo");
                _vimBuffer.Process(VimKey.Left);
                _vimBuffer.Process("l");
                _vimBuffer.Process(VimKey.Escape);
                Assert.Equal("hello", _textBuffer.GetLine(0).GetText());
                Assert.Equal(3, _textView.GetCaretPoint().Position);
                _textView.MoveCaretTo(0);
                _vimBuffer.Process(".");
                Assert.Equal("hellohello", _textBuffer.GetLine(0).GetText());
                Assert.Equal(3, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Simulate a typo, followed by two left arrow keys to correct
            /// </summary>
            [WpfFact]
            public void InsertWithArrowLeftTwice()
            {
                Create("");
                _vimBuffer.Process("at");
                _vimBuffer.Process(VimKey.Left);
                _vimBuffer.Process(VimKey.Left);
                _vimBuffer.Process("c");
                _vimBuffer.Process(VimKey.Escape);
                Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
                _vimBuffer.Process(".");
                Assert.Equal("catcat", _textBuffer.GetLine(0).GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Simulate entering the insert mode at the wrong position, and use the arrow keys to navigate
            /// </summary>
            [WpfFact]
            public void InsertWithArrowRight()
            {
                Create("cat dog");
                _vimBuffer.Process(VimKey.Right);
                _vimBuffer.Process(VimKey.Right);
                _vimBuffer.Process(VimKey.Right);
                _vimBuffer.Process(VimKey.Right);
                _vimBuffer.Process("dog ");
                _vimBuffer.Process(VimKey.Escape);
                Assert.Equal("cat dog dog", _textBuffer.GetLine(0).GetText());
                Assert.Equal(7, _textView.GetCaretPoint().Position);
                _textView.MoveCaretTo(0);
                _vimBuffer.Process(".");
                Assert.Equal("cat dog dog dog", _textBuffer.GetLine(0).GetText());
                Assert.Equal(7, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Simulate entering the insert mode, add some text, and then use the arrow keys to change text on the next line as well
            /// </summary>
            [WpfFact]
            public void InsertWithArrowDown()
            {
                Create("cat dog", "rabbit mouse");
                _vimBuffer.Process("horse ");
                _vimBuffer.Process(VimKey.Down);
                _vimBuffer.Process(VimKey.Right);
                _vimBuffer.Process("cow ");
                _vimBuffer.Process(VimKey.Escape);
                Assert.Equal("horse cat dog", _textBuffer.GetLine(0).GetText());
                Assert.Equal("rabbit cow mouse", _textBuffer.GetLine(1).GetText());
                _textView.MoveCaretTo(0);
                _vimBuffer.Process(".");
                Assert.Equal("horse horse cat dog", _textBuffer.GetLine(0).GetText());
                Assert.Equal("rabbit cow cow mouse", _textBuffer.GetLine(1).GetText());
            }

            /// <summary>
            /// Move a word to the right in insert mode, and repeat from the beginning of word
            /// </summary>
            [WpfFact]
            public void MoveWordForward()
            {
                Create("cat dog");
                _vimBuffer.Process(KeyInputUtil.ApplyKeyModifiersToKey(VimKey.Right, VimKeyModifiers.Control));
                _vimBuffer.Process("horse ");
                Assert.Equal("cat horse dog", _textBuffer.GetLine(0).GetText());
                _vimBuffer.Process(VimKey.Escape);
                _vimBuffer.Process("b");
                _vimBuffer.Process(".");
                Assert.Equal("cat horse horse dog", _textBuffer.GetLine(0).GetText());
            }
        }
    }
}

