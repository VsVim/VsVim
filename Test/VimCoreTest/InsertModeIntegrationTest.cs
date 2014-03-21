using System;
using EditorUtils;
using Microsoft.FSharp.Core;
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

        public sealed class InsertCharacterAboveTest : InsertModeIntegrationTest
        {
            [Fact]
            public void Simple()
            {
                Create("cat", "dog");
                _textView.MoveCaretToLine(1);
                _vimBuffer.ProcessNotation("<C-y>");
                Assert.Equal("cdog", _textBuffer.GetLine(1).GetText());
            }

            [Fact]
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

            [Fact]
            public void NothingAbove()
            {
                Create("", "dog");
                _textView.MoveCaretToLine(1);
                _vimBuffer.ProcessNotation("<C-y>");
                Assert.Equal(1, VimHost.BeepCount);
            }

            [Fact]
            public void FirstLine()
            {
                Create("", "dog");
                _vimBuffer.ProcessNotation("<C-y>");
                Assert.Equal(1, VimHost.BeepCount);
            }
        }

        public sealed class InsertCharacterBelowTest : InsertModeIntegrationTest
        {
            [Fact]
            public void Simple()
            {
                Create("cat", "dog");
                _vimBuffer.ProcessNotation("<C-e>");
                Assert.Equal("dcat", _textBuffer.GetLine(0).GetText());
            }

            [Fact]
            public void Multiple()
            {
                Create("cat", "dog");
                for (var i = 0; i < 3; i++)
                {
                    _vimBuffer.ProcessNotation("<C-e>");
                }
                Assert.Equal("dogcat", _textBuffer.GetLine(0).GetText());
            }

            [Fact]
            public void NothingBelow()
            {
                Create("cat", "");
                _vimBuffer.ProcessNotation("<C-e>");
                Assert.Equal(1, VimHost.BeepCount);
            }

            [Fact]
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

            /// <summary>
            /// When a &lt; or &gt; tag is not a special name it should be interpreted character
            /// by character 
            /// </summary>
            [Fact]
            public void TagNotASpecialName()
            {
                Create("");
                _vimBuffer.Process(VimKey.Escape);
                _vimBuffer.Process(@":inoremap a <dest>", enter: true);
                _vimBuffer.Process("ia");
                Assert.Equal("<dest>", _textBuffer.GetLine(0).GetText());
            }

            [Fact]
            public void UnmatchedLessThan()
            {
                Create("");
                _vimBuffer.Process(VimKey.Escape);
                _vimBuffer.Process(@":inoremap a <<s-a>", enter: true);
                _vimBuffer.Process("ia");
                Assert.Equal("<A", _textBuffer.GetLine(0).GetText());
            }

            [Fact]
            public void Issue1059()
            {
                Create("");
                _vimBuffer.Process(VimKey.Escape);
                _vimBuffer.Process(@":imap /v VARCHAR(MAX) = '<%= ""test"" %>'", enter: true);
                _vimBuffer.Process("i/v");
                Assert.Equal(@"VARCHAR(MAX) = '<%= ""test"" %>'", _textBuffer.GetLine(0).GetText());
            }
        }

        public sealed class PasteTest : InsertModeIntegrationTest
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

            /// <summary>
            /// Make sure that the line endings are normalized on the paste operation
            /// </summary>
            [Fact]
            public void NormalizeLineEndings()
            {
                Create("cat", "dog");
                RegisterMap.GetRegister('c').UpdateValue("fish\ntree\n", OperationKind.LineWise);
                _vimBuffer.ProcessNotation("<C-R>c");
                Assert.Equal(
                    new[] { "fish", "tree", "cat", "dog" },
                    _textBuffer.GetLines());
                for (int i = 0; i < 3; i++)
                {
                    Assert.Equal(Environment.NewLine, _textBuffer.GetLine(i).GetLineBreakText());
                }
            }
        }

        /// <summary>
        /// Test the behavior of the '^ and `^ motion in insert mode
        /// </summary>
        public sealed class LastCaretEditMarkTest : InsertModeIntegrationTest
        {
            [Fact]
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
            [Fact]
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
            [Fact]
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
            [Fact]
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
            [Fact]
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
            [Fact]
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
            [Fact]
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
            [Fact]
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
            [Fact]
            public void GoToLastEditPosition()
            {
                Create("cat", "dog");
                _textView.MoveCaretToLine(1, 3);
                _vimBuffer.ProcessNotation(" bird<Esc>");
                _textView.MoveCaretToLine(0, 0);

                Assert.Equal("dog bird", _textView.GetLine(1).GetText());

                Assert.Equal(0, _textView.Caret.Position.BufferPosition.GetColumn().Column);
                Assert.Equal(0, _textView.GetCaretLine().LineNumber);

                _vimBuffer.ProcessNotation("`.");

                Assert.Equal(7, _textView.Caret.Position.BufferPosition.GetColumn().Column);
                Assert.Equal(1, _textView.GetCaretLine().LineNumber);
            }

            /// <summary>
            /// When text is inserted into the buffer then the last edit point should be the
            /// last character that was inserted
            /// </summary>
            [Fact]
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
            [Fact]
            public void InsertNonVim()
            {
                Create("big");
                _textBuffer.Insert(3, " cat");
                Assert.Equal(6, LastEditPoint.Value);
            }

            /// <summary>
            /// When there is a deletion of text then the LastEditPoint should point to the start
            /// of the deleted text
            /// </summary>
            [Fact]
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
            [Fact]
            public void DeleteNonVim()
            {
                Create("a big dog");
                _textBuffer.Delete(new Span(2, 3));
                Assert.Equal(2, LastEditPoint.Value);
            }

            [Fact]
            public void MiddleOfLine()
            {
                Create("cat", "dg", "fish");
                _textView.MoveCaretToLine(1, 1);
                _vimBuffer.ProcessNotation("o<Esc>");
                _textView.MoveCaretToLine(0);
                _vimBuffer.ProcessNotation("`.");
                Assert.Equal(_textBuffer.GetPointInLine(1, 1), _textView.GetCaretPoint());
            }

            [Fact]
            public void BeginningOfLine()
            {
                Create("cat", "og", "fish");
                _textView.MoveCaretToLine(1, 0);
                _vimBuffer.ProcessNotation("d<Esc>");
                _textView.MoveCaretToLine(0);
                _vimBuffer.ProcessNotation("`.");
                Assert.Equal(_textBuffer.GetPointInLine(1, 0), _textView.GetCaretPoint());
            }

            [Fact]
            public void TypingCompleteWord()
            {
                Create("cat", "", "fish");
                _textView.MoveCaretToLine(1, 0);
                _vimBuffer.ProcessNotation("dog<Esc>");
                _textView.MoveCaretToLine(0);
                _vimBuffer.ProcessNotation("`.");
                Assert.Equal(_textBuffer.GetPointInLine(1, 2), _textView.GetCaretPoint());
            }

            [Fact]
            public void DeleteLineContainingLastEditPoint()
            {
                Create("cat", "", "fish");
                _textView.MoveCaretToLine(1, 0);
                _vimBuffer.ProcessNotation("dog<Esc>ddk`.");
                Assert.Equal(_textBuffer.GetPointInLine(1, 0), _textView.GetCaretPoint());
            }

            [Fact]
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
            [Fact]
            public void Simple()
            {
                Create("cat", "dog");
                _localSettings.ShiftWidth = 4;
                _vimBuffer.ProcessNotation("<C-t>");
                Assert.Equal("    cat", _textBuffer.GetLine(0).GetText());
                Assert.Equal(4, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void CustomShift()
            {
                Create("cat", "dog");
                _localSettings.ShiftWidth = 2;
                _vimBuffer.ProcessNotation("<C-t>");
                Assert.Equal("  cat", _textBuffer.GetLine(0).GetText());
                Assert.Equal(2, _textView.GetCaretPoint().Position);
            }
        }

        public sealed class BackspacingTest : InsertModeIntegrationTest
        {
            /// <summary>
            /// Make sure backspace over char at start without
            /// 'backspace=start' works
            /// </summary>
            [Fact]
            public void BackspaceOverChar_NoStart()
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
            [Fact]
            public void BackspaceOverChar_Start()
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
            [Fact]
            public void BackspaceOverChar_NoIndent()
            {
                Create("    dog");
                _globalSettings.Backspace = "start";
                _textView.MoveCaretTo(4);
                _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<BS>"));
                Assert.Equal("    dog", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// Make sure backspace over char at indent with
            /// 'backspace=indent' works
            /// </summary>
            [Fact]
            public void BackspaceOverChar_Indent()
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
            [Fact]
            public void BackspaceOverChar_NoEol()
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
            [Fact]
            public void BackspaceOverChar_Eol()
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
            [Fact]
            public void BackspaceOverChar_FromVirtualSpace()
            {
                Create("  hello", "world");
                _globalSettings.Backspace = "start,indent,eol";
                _globalSettings.UseEditorIndent = false;
                _localSettings.AutoIndent = true;
                _textView.MoveCaretTo(_textView.GetLine(0).End);
                _vimBuffer.ProcessNotation("<Enter><BS>");
                Assert.Equal("  hello", _textView.GetLine(0).GetText());
                Assert.Equal(" ", _textView.GetLine(1).GetText());
            }

            /// <summary>
            /// Make sure backspace over word at start without
            /// 'backspace=start' works
            /// </summary>
            [Fact]
            public void BackspaceOverWord_NoStart()
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
            [Fact]
            public void BackspaceOverWord_Start()
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
            [Fact]
            public void BackspaceOverWord_NoIndent()
            {
                Create("    dog");
                _globalSettings.Backspace = "start";
                _textView.MoveCaretTo(4);
                _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<C-w>"));
                Assert.Equal("    dog", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// Make sure backspace over word at indent with
            /// 'backspace=indent' works
            /// </summary>
            [Fact]
            public void BackspaceOverWord_Indent()
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
            [Fact]
            public void BackspaceOverWord_NoEol()
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
            [Fact]
            public void BackspaceOverWord_Eol()
            {
                Create("cat", "dog");
                _globalSettings.Backspace = "start,indent,eol";
                _textView.MoveCaretTo(5);
                _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<C-w>"));
                Assert.Equal("catdog", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// Make sure backspacing over line starting from an empty line
            /// works
            /// </summary>
            [Fact]
            public void BackspaceOverLine_FromEmptyLine()
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
            [Fact]
            public void BackspaceOverLine_FromStarrtOfNonEmpyLine()
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
            [Fact]
            public void BackspaceOverLine_FromEndOfNonEmpyLine()
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
            [Fact]
            public void BackspaceOverLine_FromMiddleOfNonEmpyLine()
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
            [Fact]
            public void BackspaceOverLine_AtStart_NoBackspaceStart()
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
            [Fact]
            public void BackspaceOverLine_AgainAtStart_NoBackspaceStart()
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
            [Fact]
            public void BackspaceOverLine_AtStart_BackspaceStart()
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
            [Fact]
            public void BackspaceOverLine_AgainAtStart_BackspaceStart()
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
            [Fact]
            public void BackspaceOverLine_FromStartOfNextLine_NoBackspaceEol()
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
            [Fact]
            public void BackspaceOverLine_FromStartOfNextLine_BackspaceEol()
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
            [Fact]
            public void BackspaceOverLine_TwiceFromSameEdit()
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
            [Fact]
            public void BackspaceOverLine_WithRedo()
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
            [Fact]
            public void BackspaceOverWord_RepeatedOverMultiLine()
            {
                Create("aaa bbb");
                _globalSettings.Backspace = "indent,eol,start";
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
            [Fact]
            public void BackspaceOverLine_RepeatedOverMultiLine()
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

        public sealed class MiscTest : InsertModeIntegrationTest
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
                _globalSettings.Backspace = "start";
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
            [Fact]
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
                Assert.Equal(2, _textView.GetCaretPoint().GetColumn().Column);
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
                Assert.Equal(2, _textView.GetCaretPoint().GetColumn().Column);
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
            [Fact]
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
                _vimBuffer.LocalSettings.ShiftWidth = 4;
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
                _vimBuffer.LocalSettings.ShiftWidth = 4;
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

            [Fact]
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
            [Fact]
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
            [Fact]
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
        }
    }
}
