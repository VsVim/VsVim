using System;
using EditorUtils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Vim.Extensions;
using Xunit;

namespace Vim.UnitTest
{
    /// <summary>
    /// Used to test the integrated behavior if Insert Mode 
    /// </summary>
    public abstract class InsertModeIntegrationTest : VimTestBase
    {
        protected IVimBuffer _vimBuffer;
        protected IWpfTextView _textView;
        protected ITextBuffer _textBuffer;
        protected IVimGlobalSettings _globalSettings;
        protected IVimLocalSettings _localSettings;
        protected Register _register;

        protected void Create(params string[] lines)
        {
            Create(ModeArgument.None, lines);
        }

        protected void Create(ModeArgument argument, params string[] lines)
        {
            _textView = CreateTextView(lines);
            _textBuffer = _textView.TextBuffer;
            _vimBuffer = Vim.CreateVimBuffer(_textView);
            _vimBuffer.SwitchMode(ModeKind.Insert, argument);
            _register = Vim.RegisterMap.GetRegister('c');
            _globalSettings = Vim.GlobalSettings;
            _localSettings = _vimBuffer.LocalSettings;
        }

        public sealed class KeyMapping : InsertModeIntegrationTest
        {
            /// <summary>
            /// Make sure that in the case where there is buffered input and we fail at the mapping 
            /// that both values are inserted into the ITextBuffer
            /// </summary>
            [Fact]
            public void BufferedInputFailsMapping()
            {
                Create("");
                _vimBuffer.Vim.KeyMap.MapWithNoRemap("jj", "<Esc>", KeyRemapMode.Insert);
                _vimBuffer.Process("j");
                Assert.Equal("", _textBuffer.GetLine(0).GetText());
                _vimBuffer.Process("a");
                Assert.Equal("ja", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// Ensure we can use a double keystroke to escape
            /// </summary>
            [Fact]
            public void TwoKeysToEscape()
            {
                Create(ModeArgument.NewInsertWithCount(2), "hello");
                _vimBuffer.Vim.KeyMap.MapWithNoRemap("jj", "<Esc>", KeyRemapMode.Insert);
                _vimBuffer.Process("jj");
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// The Escape should end a multiple key mapping and exit insert mode
            /// </summary>
            [Fact]
            public void TwoKeys_EscapeToEndSequence()
            {
                Create("hello world", "");
                _textView.MoveCaretToLine(1);
                _vimBuffer.Vim.KeyMap.MapWithNoRemap(";;", "<Esc>", KeyRemapMode.Insert);
                _vimBuffer.Process(';');
                _vimBuffer.Process(VimKey.Escape);
                Assert.Equal(";", _textBuffer.GetLine(1).GetText());
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// The CTRL-[ should end a multiple key mapping the same as normal Escape
            /// </summary>
            [Fact]
            public void TwoKeys_AlternateEscapeToEndSequence()
            {
                Create("hello world", "");
                _textView.MoveCaretToLine(1);
                _vimBuffer.Vim.KeyMap.MapWithNoRemap(";;", "<Esc>", KeyRemapMode.Insert);
                _vimBuffer.Process(';');
                _vimBuffer.Process(KeyInputUtil.CharWithControlToKeyInput('['));
                Assert.Equal(";", _textBuffer.GetLine(1).GetText());
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// Spaces need to be allowed in the target mapping
            /// </summary>
            [Fact]
            public void SpacesInTarget()
            {
                Create("");
                _vimBuffer.Process(VimKey.Escape);
                _vimBuffer.Process(":imap cat hello world", enter: true);
                _vimBuffer.Process("icat");
                Assert.Equal("hello world", _textBuffer.GetLine(0).GetText());
            }

            [Fact]
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
            [Fact]
            public void RecursiveInNoRemap()
            {
                Create("");
                _vimBuffer.Process(VimKey.Escape);
                _vimBuffer.Process(@":inoremap x axa", enter: true);
                _vimBuffer.Process("ix");
                Assert.Equal("axa", _textBuffer.GetLine(0).GetText());
            }
        }

        public sealed class Paste : InsertModeIntegrationTest
        {
            [Fact]
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
            [Fact]
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
            [Fact]
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
            [Fact]
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
            [Fact]
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
            [Fact]
            public void EscapeShouldStayInInsert()
            {
                Create("dog");
                _vimBuffer.ProcessNotation("<C-R><Esc>");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
            }
        }

        public sealed class Misc : InsertModeIntegrationTest
        {
            /// <summary>
            /// Make sure that the ITextView isn't accessed in insert mode if it's active and the 
            /// ITextView is closed
            /// </summary>
            [Fact]
            public void CloseInInsertMode()
            {
                Create("foo", "bar");
                _textView.Close();
            }

            [Fact]
            public void Leave_WithControlC()
            {
                Create("hello world");
                _vimBuffer.ProcessNotation("<C-c>");
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// Ensure that normal typing gets passed to TryCustomProcess
            /// </summary>
            [Fact]
            public void TryCustomProcess_DirectInsert()
            {
                Create("world");
                VimHost.TryCustomProcessFunc =
                    (textView, command) =>
                    {
                        if (command.IsDirectInsert)
                        {
                            Assert.Equal('#', command.AsDirectInsert().Item);
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
            [Fact]
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
            /// Repeat of a TryCustomProcess should recall that function vs. repeating the
            /// inserted text
            /// </summary>
            [Fact]
            public void TryCustomProcess_Repeat()
            {
                Create("world");
                var first = true;
                VimHost.TryCustomProcessFunc =
                    (textView, command) =>
                    {
                        if (command.IsDirectInsert)
                        {
                            Assert.Equal('#', command.AsDirectInsert().Item);
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
                _vimBuffer.ProcessNotation("#<Esc>.");
                Assert.Equal("big hello world", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// KeyInput values which are custom processed should still end up in the macro recorder
            /// </summary>
            [Fact]
            public void TryCustomProcess_Macro()
            {
                Create("world");
                Vim.MacroRecorder.StartRecording(_register, false);
                VimHost.TryCustomProcessFunc =
                    (textView, command) =>
                    {
                        if (command.IsDirectInsert)
                        {
                            Assert.Equal('#', command.AsDirectInsert().Item);
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
            [Fact]
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
            [Fact]
            public void Delete_WithShift()
            {
                Create("cat dog");
                _textView.MoveCaretTo(1);
                _vimBuffer.ProcessNotation("<S-BS>");
                Assert.Equal("at dog", _textBuffer.GetLine(0).GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// The delete key when combined with shift should still participate in key mapping
            /// </summary>
            [Fact]
            public void Delete_WithShift_KeyMapping()
            {
                Create(" world");
                KeyMap.MapWithNoRemap("<S-BS>", "hello", KeyRemapMode.Insert);
                _textView.MoveCaretTo(0);
                _vimBuffer.ProcessNotation("<S-BS>");
                Assert.Equal("hello world", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// Verify that inserting tab with a count and inserting tab "count" times is an exchangable
            /// operation
            /// </summary>
            [Fact]
            public void Insert_Tab()
            {
                Create("int Member", "int Member");
                _localSettings.ExpandTab = false;
                _localSettings.TabStop = 8;
                _globalSettings.ShiftWidth = 4;
                _vimBuffer.Process(VimKey.Escape);
                _textView.MoveCaretToLine(0, 3);
                _vimBuffer.Process(VimKey.LowerI, VimKey.Tab, VimKey.Tab, VimKey.Escape);
                Assert.Equal("int\t\t Member", _textBuffer.GetLine(0).GetText());
                _textView.MoveCaretToLine(1, 3);
                _vimBuffer.Process(VimKey.Number2, VimKey.LowerI, VimKey.Tab, VimKey.Escape);
                Assert.Equal("int\t\t Member", _textBuffer.GetLine(1).GetText());
            }

            /// <summary>
            /// Make sure that indentation is still done even when enter occurs with a non-standard mapping
            /// of enter
            /// </summary>
            [Fact]
            public void Insert_NewLine_IndentWithAltMapping()
            {
                Create("  hello", "world");
                _globalSettings.UseEditorIndent = false;
                _localSettings.AutoIndent = true;
                Vim.KeyMap.MapWithNoRemap("<c-e>", "<Enter>", KeyRemapMode.Insert);
                _textView.MoveCaretTo(5);
                _vimBuffer.Process(KeyInputUtil.CharWithControlToKeyInput('e'));
                Assert.Equal("  hel", _textView.GetLine(0).GetText());
                Assert.Equal("  lo", _textView.GetLine(1).GetText());
            }

            /// <summary>
            /// At the end of the line the caret should just move into virtual space.  No need for actual
            /// white space to be inserted
            /// </summary>
            [Fact]
            public void Insert_NewLine_AtEndOfLine()
            {
                Create("  hello", "world");
                _globalSettings.UseEditorIndent = false;
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
            [Fact]
            public void OneTimeCommand_BufferState()
            {
                Create("");
                _vimBuffer.Process(KeyInputUtil.CharWithControlToKeyInput('o'));
                Assert.True(_vimBuffer.InOneTimeCommand.Is(ModeKind.Insert));
            }

            /// <summary>
            /// Execute a one time command of delete word
            /// </summary>
            [Fact]
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
            /// Execute a one time command of delete word
            /// </summary>
            [Fact]
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
            [Fact]
            public void OneTimeCommand_Normal_Escape()
            {
                Create("");
                _vimBuffer.Process(KeyInputUtil.CharWithControlToKeyInput('o'));
                _vimBuffer.Process(VimKey.Escape);
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.True(_vimBuffer.InOneTimeCommand.IsNone());
            }

            /// <summary>
            /// Ensure the single backspace is repeated properly.  It is tricky because it has to both 
            /// backspace and then jump a caret space to the left.
            /// </summary>
            [Fact]
            public void Repeat_Backspace_Single()
            {
                Create("dog toy", "fish chips");
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
            [Fact]
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
            [Fact]
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
            [Fact]
            public void Repeat_Insert_TabCount()
            {
                Create("int Member", "int Member");
                _localSettings.ExpandTab = false;
                _localSettings.TabStop = 8;
                _globalSettings.ShiftWidth = 4;
                _vimBuffer.Process(VimKey.Escape);
                _textView.MoveCaretToLine(0, 3);
                _vimBuffer.Process(VimKey.Number3, VimKey.LowerI, VimKey.Tab, VimKey.Escape);
                Assert.Equal("int\t\t\t Member", _textBuffer.GetLine(0).GetText());
                _textView.MoveCaretToLine(1, 3);
                _vimBuffer.Process('.');
                Assert.Equal("int\t\t\t Member", _textBuffer.GetLine(1).GetText());
            }

            /// <summary>
            /// When repeating a tab the repeat needs to be wary of maintainin the 'tabstop' modulus
            /// of the new line
            /// </summary>
            [Fact]
            public void Repeat_Insert_TabNonEvenOffset()
            {
                Create("hello world", "static LPTSTR pValue");
                _localSettings.ExpandTab = true;
                _localSettings.TabStop = 4;
                _vimBuffer.Process(VimKey.Escape);
                _vimBuffer.Process(VimKey.LowerC, VimKey.LowerW, VimKey.Tab, VimKey.Escape);
                Assert.Equal("     world", _textView.GetLine(0).GetText());
                _textView.MoveCaretTo(_textBuffer.GetPointInLine(1, 13));
                _vimBuffer.Process('.');
                Assert.Equal("static LPTSTR   pValue", _textView.GetLine(1).GetText());
            }

            /// <summary>
            /// Repeat a simple text insertion with a count
            /// </summary>
            [Fact]
            public void Repeat_InsertWithCount()
            {
                Create("", "");
                _vimBuffer.Process('h');
                _vimBuffer.Process(VimKey.Escape);
                Assert.Equal("h", _textView.GetLine(0).GetText());
                _textView.MoveCaretToLine(1);
                _vimBuffer.Process("3.");
                Assert.Equal("hhh", _textView.GetLine(1).GetText());
                Assert.Equal(2, _textView.GetCaretPoint().GetColumn());
            }

            /// <summary>
            /// Repeat a simple text insertion with a count.  Focus on making sure the caret position
            /// is correct.  Added text ensures the end of line doesn't save us by moving the caret
            /// backwards
            /// </summary>
            [Fact]
            public void Repeat_InsertWithCountOverOtherText()
            {
                Create("", "a");
                _vimBuffer.Process('h');
                _vimBuffer.Process(VimKey.Escape);
                Assert.Equal("h", _textView.GetLine(0).GetText());
                _textView.MoveCaretToLine(1);
                _vimBuffer.Process("3.");
                Assert.Equal("hhha", _textView.GetLine(1).GetText());
                Assert.Equal(2, _textView.GetCaretPoint().GetColumn());
            }

            /// <summary>
            /// Ensure when the mode is entered with a count that the escape will cause the
            /// deleted text to be repeated
            /// </summary>
            [Fact]
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
            [Fact]
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
            [Fact]
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
            [Fact]
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
            [Fact]
            public void Repeat_InsertTab()
            {
                Create("cat", "dog");
                _vimBuffer.LocalSettings.ExpandTab = false;
                _vimBuffer.Process(VimKey.Tab);
                _vimBuffer.Process(VimKey.Escape);
                _textView.MoveCaretToLine(1);
                _vimBuffer.Process('.');
                Assert.Equal("\tdog", _textView.GetLine(1).GetText());
            }

            /// <summary>
            /// Make sure that the insert tab repeats as the insert tab command and not as the 
            /// repeat of a text change.  This can be verified by altering the settings between the initial
            /// insert and the repeat
            /// </summary>
            [Fact]
            public void Repeat_InsertTab_ChangedSettings()
            {
                Create("cat", "dog");
                _vimBuffer.LocalSettings.ExpandTab = false;
                _vimBuffer.Process(VimKey.Tab);
                _vimBuffer.Process(VimKey.Escape);
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
            [Fact]
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
            [Fact]
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
            [Fact]
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
            /// This test is mainly a regression test against the selection change logic
            /// </summary>
            [Fact]
            public void SelectionChange1()
            {
                Create("foo", "bar");
                _textView.SelectAndMoveCaret(new SnapshotSpan(_textView.GetLine(0).Start, 0));
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// Make sure that shift left does a round up before it shifts to the left.
            /// </summary>
            [Fact]
            public void ShiftLeft_RoundUp()
            {
                Create("     hello");
                _vimBuffer.GlobalSettings.ShiftWidth = 4;
                _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<C-D>"));
                Assert.Equal("    hello", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// Make sure that when the text is properly rounded to a shift width that the 
            /// shift left just deletes a shift width
            /// </summary>
            [Fact]
            public void ShiftLeft_Normal()
            {
                Create("        hello");
                _vimBuffer.GlobalSettings.ShiftWidth = 4;
                _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<C-D>"));
                Assert.Equal("    hello", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// Simple word completion action which accepts the first match
            /// </summary>
            [Fact]
            public void WordCompletion_Simple()
            {
                Create("c dog", "cat");
                _textView.MoveCaretTo(1);
                _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<C-N>"));
                Assert.Equal("cat dog", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// Simulate choosing the second possibility in the completion list
            /// </summary>
            [Fact]
            public void WordCompletion_ChooseNext()
            {
                Create("c dog", "cat copter");
                _textView.MoveCaretTo(1);
                _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<C-N>"));
                _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<C-N>"));
                Assert.Equal("copter dog", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// Typing a char while the completion list is up should cancel it out and 
            /// cause the char to be added to the IVimBuffer
            /// </summary>
            [Fact]
            public void WordCompletion_TypeAfter()
            {
                Create("c dog", "cat");
                _textView.MoveCaretTo(1);
                _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<C-N>"));
                _vimBuffer.Process('s');
                Assert.Equal("cats dog", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// Esacpe should cancel both word completion and insert mode.  It's just
            /// like normal intellisense in that respect
            /// </summary>
            [Fact]
            public void WordCompletion_Escape()
            {
                Create("c dog", "cat");
                _textView.MoveCaretTo(1);
                _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<C-N>"));
                _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<Esc>"));
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                Assert.Equal(2, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// When there are no matches then no active IWordCompletion should be created and 
            /// it should continue in insert mode
            /// </summary>
            [Fact]
            public void WordCompletion_NoMatches()
            {
                Create("c dog");
                _textView.MoveCaretTo(1);
                _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<C-N>"));
                Assert.Equal("c dog", _textView.GetLine(0).GetText());
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.True(_vimBuffer.InsertMode.ActiveWordCompletionSession.IsNone());
            }
        }
    }
}
