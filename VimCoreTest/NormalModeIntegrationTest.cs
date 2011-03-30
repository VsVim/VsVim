using System;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class NormalModeIntegrationTest
    {
        private IVimBuffer _buffer;
        private IWpfTextView _textView;
        private ITextBuffer _textBuffer;
        private IVimGlobalSettings _globalSettings;
        private IKeyMap _keyMap;
        private bool _assertOnErrorMessage = true;

        internal Register UnnamedRegister
        {
            get { return _buffer.GetRegister(RegisterName.Unnamed); }
        }

        public void Create(params string[] lines)
        {
            var tuple = EditorUtil.CreateViewAndOperations(lines);
            _textView = tuple.Item1;
            _textBuffer = _textView.TextBuffer;
            var service = EditorUtil.FactoryService;
            _buffer = service.Vim.CreateBuffer(_textView);
            _buffer.ErrorMessage +=
                (_, message) =>
                {
                    if (_assertOnErrorMessage)
                    {
                        Assert.Fail("Error Message: " + message);
                    }
                };
            _keyMap = _buffer.Vim.KeyMap;
            _globalSettings = _buffer.Settings.GlobalSettings;
        }

        [TearDown]
        public void TearDown()
        {
            EditorUtil.FactoryService.Vim.KeyMap.ClearAll();
            _buffer.Close();
        }

        [Test]
        public void dd_OnLastLine()
        {
            Create("foo", "bar");
            _textView.MoveCaretTo(_textView.GetLine(1).Start);
            _buffer.Process("dd");
            Assert.AreEqual("foo", _textView.TextSnapshot.GetText());
            Assert.AreEqual(1, _textView.TextSnapshot.LineCount);
        }

        [Test]
        public void RepeatCommand_Repeated()
        {
            Create("the fox chased the bird");
            _buffer.Process("dw");
            Assert.AreEqual("fox chased the bird", _textView.TextSnapshot.GetText());
            _buffer.Process(".");
            Assert.AreEqual("chased the bird", _textView.TextSnapshot.GetText());
            _buffer.Process(".");
            Assert.AreEqual("the bird", _textView.TextSnapshot.GetText());
        }

        [Test]
        public void RepeatCommand_LinkedTextChange1()
        {
            Create("the fox chased the bird");
            _buffer.Process("cw");
            _buffer.Process("hey ");
            _buffer.Process(KeyInputUtil.EscapeKey);
            _textView.MoveCaretTo(4);
            _buffer.Process(KeyInputUtil.CharToKeyInput('.'));
            Assert.AreEqual("hey hey fox chased the bird", _textView.TextSnapshot.GetText());
        }

        [Test]
        public void RepeatCommand_LinkedTextChange2()
        {
            Create("the fox chased the bird");
            _buffer.Process("cw");
            _buffer.Process("hey");
            _buffer.Process(KeyInputUtil.EscapeKey);
            _textView.MoveCaretTo(4);
            _buffer.Process(KeyInputUtil.CharToKeyInput('.'));
            Assert.AreEqual("hey hey chased the bird", _textView.TextSnapshot.GetText());
        }

        [Test]
        public void RepeatCommand_LinkedTextChange3()
        {
            Create("the fox chased the bird");
            _buffer.Process("cw");
            _buffer.Process("hey");
            _buffer.Process(KeyInputUtil.EscapeKey);
            _textView.MoveCaretTo(4);
            _buffer.Process(KeyInputUtil.CharToKeyInput('.'));
            _buffer.Process(KeyInputUtil.CharToKeyInput('.'));
            Assert.AreEqual("hey hehey chased the bird", _textView.TextSnapshot.GetText());
        }

        [Test]
        [Description("See issue 288")]
        public void dj_1()
        {
            Create("abc", "def", "ghi", "jkl");
            _buffer.Process("dj");
            Assert.AreEqual("ghi", _textView.GetLine(0).GetText());
            Assert.AreEqual("jkl", _textView.GetLine(1).GetText());
        }

        [Test]
        [Description("A d with Enter should delete the line break")]
        public void Issue317_1()
        {
            Create("dog", "cat", "jazz", "band");
            _buffer.Process("2d", enter: true);
            Assert.AreEqual("band", _textView.GetLine(0).GetText());
        }

        [Test]
        [Description("Verify the contents after with a paste")]
        public void Issue317_2()
        {
            Create("dog", "cat", "jazz", "band");
            _buffer.Process("2d", enter: true);
            _buffer.Process("p");
            Assert.AreEqual("band", _textView.GetLine(0).GetText());
            Assert.AreEqual("dog", _textView.GetLine(1).GetText());
            Assert.AreEqual("cat", _textView.GetLine(2).GetText());
            Assert.AreEqual("jazz", _textView.GetLine(3).GetText());
        }

        [Test]
        [Description("Plain old Enter should just move the cursor one line")]
        public void Issue317_3()
        {
            Create("dog", "cat", "jazz", "band");
            _buffer.Process(KeyInputUtil.EnterKey);
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test]
        [Description("[[ motion should put the caret on the target character")]
        public void SectionMotion1()
        {
            Create("hello", "{world");
            _buffer.Process("]]");
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test]
        [Description("[[ motion should put the caret on the target character")]
        public void SectionMotion2()
        {
            Create("hello", "\fworld");
            _buffer.Process("]]");
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test]
        public void SectionMotion3()
        {
            Create("foo", "{", "bar");
            _textView.MoveCaretTo(_textView.GetLine(2).End);
            _buffer.Process("[[");
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test]
        public void SectionMotion4()
        {
            Create("foo", "{", "bar", "baz");
            _textView.MoveCaretTo(_textView.GetLine(3).End);
            _buffer.Process("[[");
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test]
        public void SectionMotion5()
        {
            Create("foo", "{", "bar", "baz", "jazz");
            _textView.MoveCaretTo(_textView.GetLine(4).Start);
            _buffer.Process("[[");
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test]
        public void ParagraphMotion1()
        {
            Create("foo", "{", "bar", "baz", "jazz");
            _textView.MoveCaretTo(_textView.TextSnapshot.GetEndPoint());
            _buffer.Process("{{");
        }

        [Test]
        public void RepeatLastSearch1()
        {
            Create("random text", "pig dog cat", "pig dog cat", "pig dog cat");
            _buffer.Process("/pig", enter: true);
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            _textView.MoveCaretTo(0);
            _buffer.Process('n');
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test]
        public void RepeatLastSearch2()
        {
            Create("random text", "pig dog cat", "pig dog cat", "pig dog cat");
            _buffer.Process("/pig", enter: true);
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            _buffer.Process('n');
            Assert.AreEqual(_textView.GetLine(2).Start, _textView.GetCaretPoint());
        }

        [Test]
        public void RepeatLastSearch3()
        {
            Create("random text", "pig dog cat", "random text", "pig dog cat", "pig dog cat");
            _buffer.Process("/pig", enter: true);
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            _textView.MoveCaretTo(_textView.GetLine(2).Start);
            _buffer.Process('N');
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test]
        [Description("With no virtual edit the cursor should move backwards after x")]
        public void CursorPositionWith_x_1()
        {
            Create("test");
            _buffer.Settings.GlobalSettings.VirtualEdit = string.Empty;
            _textView.MoveCaretTo(3);
            _buffer.Process('x');
            Assert.AreEqual("tes", _textView.GetLineRange(0).GetText());
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
        }

        [Test]
        [Description("With virtual edit the cursor should not move and stay at the end of the line")]
        public void CursorPositionWith_x_2()
        {
            Create("test", "bar");
            _buffer.Settings.GlobalSettings.VirtualEdit = "onemore";
            _textView.MoveCaretTo(3);
            _buffer.Process('x');
            Assert.AreEqual("tes", _textView.GetLineRange(0).GetText());
            Assert.AreEqual(3, _textView.GetCaretPoint().Position);
        }

        [Test]
        [Description("Caret position should remain the same in the middle of a word")]
        public void CursorPositionWith_x_3()
        {
            Create("test", "bar");
            _buffer.Settings.GlobalSettings.VirtualEdit = string.Empty;
            _textView.MoveCaretTo(1);
            _buffer.Process('x');
            Assert.AreEqual("tst", _textView.GetLineRange(0).GetText());
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void RepeatCommand_DeleteWord1()
        {
            Create("the cat jumped over the dog");
            _buffer.Process("dw");
            _buffer.Process(".");
            Assert.AreEqual("jumped over the dog", _textView.GetLine(0).GetText());
        }

        [Test]
        [Description("Make sure that movement doesn't reset the last edit command")]
        public void RepeatCommand_DeleteWord2()
        {
            Create("the cat jumped over the dog");
            _buffer.Process("dw");
            _buffer.Process(VimKey.Right);
            _buffer.Process(VimKey.Left);
            _buffer.Process(".");
            Assert.AreEqual("jumped over the dog", _textView.GetLine(0).GetText());
        }

        [Test]
        [Description("Delete word with a count")]
        public void RepeatCommand_DeleteWord3()
        {
            Create("the cat jumped over the dog");
            _buffer.Process("2dw");
            _buffer.Process(".");
            Assert.AreEqual("the dog", _textView.GetLine(0).GetText());
        }

        [Test]
        public void RepeatCommand_DeleteLine1()
        {
            Create("bear", "dog", "cat", "zebra", "fox", "jazz");
            _buffer.Process("dd");
            _buffer.Process(".");
            Assert.AreEqual("cat", _textView.GetLine(0).GetText());
        }

        [Test]
        public void RepeatCommand_DeleteLine2()
        {
            Create("bear", "dog", "cat", "zebra", "fox", "jazz");
            _buffer.Process("2dd");
            _buffer.Process(".");
            Assert.AreEqual("fox", _textView.GetLine(0).GetText());
        }

        [Test]
        public void RepeatCommand_ShiftLeft1()
        {
            Create("    bear", "    dog", "    cat", "    zebra", "    fox", "    jazz");
            _buffer.Settings.GlobalSettings.ShiftWidth = 1;
            _buffer.Process("<<");
            _buffer.Process(".");
            Assert.AreEqual("  bear", _textView.GetLine(0).GetText());
        }

        [Test]
        public void RepeatCommand_ShiftLeft2()
        {
            Create("    bear", "    dog", "    cat", "    zebra", "    fox", "    jazz");
            _buffer.Settings.GlobalSettings.ShiftWidth = 1;
            _buffer.Process("2<<");
            _buffer.Process(".");
            Assert.AreEqual("  bear", _textView.GetLine(0).GetText());
            Assert.AreEqual("  dog", _textView.GetLine(1).GetText());
        }

        [Test]
        public void RepeatCommand_ShiftRight1()
        {
            Create("bear", "dog", "cat", "zebra", "fox", "jazz");
            _buffer.Settings.GlobalSettings.ShiftWidth = 1;
            _buffer.Process(">>");
            _buffer.Process(".");
            Assert.AreEqual("  bear", _textView.GetLine(0).GetText());
        }

        [Test]
        public void RepeatCommand_ShiftRight2()
        {
            Create("bear", "dog", "cat", "zebra", "fox", "jazz");
            _buffer.Settings.GlobalSettings.ShiftWidth = 1;
            _buffer.Process("2>>");
            _buffer.Process(".");
            Assert.AreEqual("  bear", _textView.GetLine(0).GetText());
            Assert.AreEqual("  dog", _textView.GetLine(1).GetText());
        }

        [Test]
        public void RepeatCommand_DeleteChar1()
        {
            Create("longer");
            _buffer.Process("x");
            _buffer.Process(".");
            Assert.AreEqual("nger", _textView.GetLine(0).GetText());
        }

        [Test]
        public void RepeatCommand_DeleteChar2()
        {
            Create("longer");
            _buffer.Process("2x");
            _buffer.Process(".");
            Assert.AreEqual("er", _textView.GetLine(0).GetText());
        }

        [Test]
        [Description("After a search operation")]
        public void RepeatCommand_DeleteChar3()
        {
            Create("bear", "dog", "cat", "zebra", "fox", "jazz");
            _buffer.Process("/e", enter: true);
            _buffer.Process("x");
            _buffer.Process("n");
            _buffer.Process(".");
            Assert.AreEqual("bar", _textView.GetLine(0).GetText());
            Assert.AreEqual("zbra", _textView.GetLine(3).GetText());
        }

        [Test]
        public void RepeatCommand_Put1()
        {
            Create("cat");
            _buffer.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateValue("lo");
            _buffer.Process("p");
            _buffer.Process(".");
            Assert.AreEqual("cloloat", _textView.GetLine(0).GetText());
        }

        [Test]
        public void RepeatCommand_Put2()
        {
            Create("cat");
            _buffer.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateValue("lo");
            _buffer.Process("2p");
            _buffer.Process(".");
            Assert.AreEqual("clolololoat", _textView.GetLine(0).GetText());
        }

        [Test]
        public void RepeatCommand_JoinLines1()
        {
            Create("bear", "dog", "cat", "zebra", "fox", "jazz");
            _buffer.Process("J");
            _buffer.Process(".");
            Assert.AreEqual("bear dog cat", _textView.GetLine(0).GetText());
        }

        [Test]
        public void RepeatCommand_Change1()
        {
            Create("bear", "dog", "cat", "zebra", "fox", "jazz");
            _buffer.Process("cl");
            _buffer.Process(VimKey.Delete);
            _buffer.Process(KeyInputUtil.EscapeKey);
            _buffer.Process(VimKey.Down);
            _buffer.Process(".");
            Assert.AreEqual("ar", _textView.GetLine(0).GetText());
            Assert.AreEqual("g", _textView.GetLine(1).GetText());
        }

        [Test]
        public void RepeatCommand_Change2()
        {
            Create("bear", "dog", "cat", "zebra", "fox", "jazz");
            _buffer.Process("cl");
            _buffer.Process("u");
            _buffer.Process(KeyInputUtil.EscapeKey);
            _buffer.Process(VimKey.Down);
            _buffer.Process(".");
            Assert.AreEqual("uear", _textView.GetLine(0).GetText());
            Assert.AreEqual("uog", _textView.GetLine(1).GetText());
        }

        [Test]
        public void RepeatCommand_Substitute1()
        {
            Create("bear", "dog", "cat", "zebra", "fox", "jazz");
            _buffer.Process("s");
            _buffer.Process("u");
            _buffer.Process(KeyInputUtil.EscapeKey);
            _buffer.Process(VimKey.Down);
            _buffer.Process(".");
            Assert.AreEqual("uear", _textView.GetLine(0).GetText());
            Assert.AreEqual("uog", _textView.GetLine(1).GetText());
        }

        [Test]
        public void RepeatCommand_Substitute2()
        {
            Create("bear", "dog", "cat", "zebra", "fox", "jazz");
            _buffer.Process("s");
            _buffer.Process("u");
            _buffer.Process(KeyInputUtil.EscapeKey);
            _buffer.Process(VimKey.Down);
            _buffer.Process("2.");
            Assert.AreEqual("uear", _textView.GetLine(0).GetText());
            Assert.AreEqual("ug", _textView.GetLine(1).GetText());
        }

        [Test]
        public void RepeatCommand_TextInsert1()
        {
            Create("bear", "dog", "cat", "zebra", "fox", "jazz");
            _buffer.Process("i");
            _buffer.Process("abc");
            _buffer.Process(KeyInputUtil.EscapeKey);
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
            _buffer.Process(".");
            Assert.AreEqual("ababccbear", _textView.GetLine(0).GetText());
        }

        [Test]
        public void RepeatCommand_TextInsert2()
        {
            Create("bear", "dog", "cat", "zebra", "fox", "jazz");
            _buffer.Process("i");
            _buffer.Process("abc");
            _buffer.Process(KeyInputUtil.EscapeKey);
            _textView.MoveCaretTo(0);
            _buffer.Process(".");
            Assert.AreEqual("abcabcbear", _textView.GetLine(0).GetText());
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void RepeatCommand_TextInsert3()
        {
            Create("bear", "dog", "cat", "zebra", "fox", "jazz");
            _buffer.Process("i");
            _buffer.Process("abc");
            _buffer.Process(KeyInputUtil.EscapeKey);
            _textView.MoveCaretTo(0);
            _buffer.Process(".");
            _buffer.Process(".");
            Assert.AreEqual("ababccabcbear", _textView.GetLine(0).GetText());
        }

        [Test]
        [Description("The first repeat of I should go to the first non-blank")]
        public void RepeatCommand_CapitalI1()
        {
            Create("bear", "dog", "cat", "zebra", "fox", "jazz");
            _buffer.Process("I");
            _buffer.Process("abc");
            _buffer.Process(KeyInputUtil.EscapeKey);
            _textView.MoveCaretTo(_textView.GetLine(1).Start.Add(2));
            _buffer.Process(".");
            Assert.AreEqual("abcdog", _textView.GetLine(1).GetText());
            Assert.AreEqual(_textView.GetLine(1).Start.Add(2), _textView.GetCaretPoint());
        }

        [Test]
        [Description("The first repeat of I should go to the first non-blank")]
        public void RepeatCommand_CapitalI2()
        {
            Create("bear", "  dog", "cat", "zebra", "fox", "jazz");
            _buffer.Process("I");
            _buffer.Process("abc");
            _buffer.Process(KeyInputUtil.EscapeKey);
            _textView.MoveCaretTo(_textView.GetLine(1).Start.Add(2));
            _buffer.Process(".");
            Assert.AreEqual("  abcdog", _textView.GetLine(1).GetText());
            Assert.AreEqual(_textView.GetLine(1).Start.Add(4), _textView.GetCaretPoint());
        }

        /// <summary>
        /// Repeating a replace char command should move the caret to the end just like
        /// the original command did
        /// </summary>
        [Test]
        public void RepeatCommand_ReplaceChar_ShouldMoveCaret()
        {
            Create("the dog kicked the ball");
            _buffer.Process("3ru");
            Assert.AreEqual("uuu dog kicked the ball", _textView.GetLine(0).GetText());
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
            _textView.MoveCaretTo(4);
            _buffer.Process(".");
            Assert.AreEqual("uuu uuu kicked the ball", _textView.GetLine(0).GetText());
            Assert.AreEqual(6, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Repeating a 
        /// replace char command from visual mode should not move the caret
        /// </summary>
        [Test]
        public void RepeatCommand_ReplaceCharVisual_ShouldNotMoveCaret()
        {
            Create("the dog kicked the ball");
            _buffer.VimData.LastCommand = FSharpOption.Create(StoredCommand.NewVisualCommand(
                VisualCommand.NewReplaceSelection(KeyInputUtil.VimKeyToKeyInput(VimKey.LowerB)),
                VimUtil.CreateCommandData(),
                StoredVisualSpan.OfVisualSpan(VisualSpan.NewCharacter(_textView.GetLineSpan(0, 3))),
                CommandFlags.None));
            _textView.MoveCaretTo(1);
            _buffer.Process(".");
            Assert.AreEqual("tbbbdog kicked the ball", _textView.GetLine(0).GetText());
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Make sure the caret movement occurs as part of the repeat
        /// </summary>
        [Test]
        public void RepeatCommand_AppendShouldRepeat()
        {
            Create("{", "}");
            _textView.MoveCaretToLine(0);
            _buffer.Process('a');
            _buffer.Process(';');
            _buffer.Process(VimKey.Escape);
            _textView.MoveCaretToLine(1);
            _buffer.Process('.');
            Assert.AreEqual("};", _textView.GetLine(1).GetText());
        }

        /// <summary>
        /// Make sure the caret movement occurs as part of the repeat
        /// </summary>
        [Test]
        public void RepeatCommand_AppendEndOfLineShouldRepeat()
        {
            Create("{", "}");
            _textView.MoveCaretToLine(0);
            _buffer.Process('A');
            _buffer.Process(';');
            _buffer.Process(VimKey.Escape);
            _textView.MoveCaretToLine(1);
            _buffer.Process('.');
            Assert.AreEqual("};", _textView.GetLine(1).GetText());
        }

        [Test]
        public void Repeat_DeleteWithIncrementalSearch()
        {
            Create("dog cat bear tree");
            _buffer.Process("d/a", enter: true);
            _buffer.Process('.');
            Assert.AreEqual("ar tree", _textView.GetLine(0).GetText());
        }

        [Test]
        public void Map_ToCharDoesNotUseMap()
        {
            Create("bear; again: dog");
            _buffer.Process(":map ; :", enter: true);
            _buffer.Process("dt;");
            Assert.AreEqual("; again: dog", _textView.GetLine(0).GetText());
        }

        [Test]
        public void Map_AlphaToRightMotion()
        {
            Create("dog");
            _buffer.Process(":map a l", enter: true);
            _buffer.Process("aa");
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void Map_OperatorPendingWithAmbiguousCommandPrefix()
        {
            Create("dog chases the ball");
            _buffer.Process(":map a w", enter: true);
            _buffer.Process("da");
            Assert.AreEqual("chases the ball", _textView.GetLine(0).GetText());
        }

        [Test]
        public void Map_ReplaceDoesntUseNormalMap()
        {
            Create("dog");
            _buffer.Process(":map f g", enter: true);
            _buffer.Process("rf");
            Assert.AreEqual("fog", _textView.GetLine(0).GetText());
        }

        [Test]
        public void Map_IncrementalSearchUsesCommandMap()
        {
            Create("dog");
            _buffer.Process(":cmap a o", enter: true);
            _buffer.Process("/a", enter: true);
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void Map_ReverseIncrementalSearchUsesCommandMap()
        {
            Create("dog");
            _textView.MoveCaretTo(_textView.TextSnapshot.GetEndPoint());
            _buffer.Process(":cmap a o", enter: true);
            _buffer.Process("?a", enter: true);
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void MoveEndOfWord_SeveralLines()
        {
            Create("the dog kicked the", "ball. The end. Bear");
            for (var i = 0; i < 10; i++)
            {
                _buffer.Process("e");
            }
            Assert.AreEqual(_textView.GetLine(1).End.Subtract(1), _textView.GetCaretPoint());
        }

        /// <summary>
        /// Make sure the cursor positions correctly on the next line 
        /// </summary>
        [Test]
        public void Handle_BraceClose_MiddleOfParagraph()
        {
            Create("dog", "", "cat");
            _buffer.Process("}");
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        /// <summary>
        /// Caret should maintain position but the text should be deleted.  The caret 
        /// exists in virtual space
        /// </summary>
        [Test]
        public void Handle_cc_AutoIndentShouldPreserveOnSingle()
        {
            Create("  dog", "  cat", "  tree");
            _buffer.Settings.AutoIndent = true;
            _buffer.Process("cc");
            Assert.AreEqual(ModeKind.Insert, _buffer.ModeKind);
            Assert.AreEqual(2, _textView.GetCaretVirtualPoint().VirtualSpaces);
            Assert.AreEqual("", _textView.GetLine(0).GetText());
        }

        [Test]
        public void Handle_cc_NoAutoIndentShouldRemoveAllOnSingle()
        {
            Create("  dog", "  cat");
            _buffer.Settings.AutoIndent = false;
            _buffer.Process("cc");
            Assert.AreEqual(ModeKind.Insert, _buffer.ModeKind);
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
            Assert.AreEqual("", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// Caret position should be preserved in virtual space
        /// </summary>
        [Test]
        public void Handle_cc_AutoIndentShouldPreserveOnMultiple()
        {
            Create("  dog", "  cat", "  tree");
            _buffer.Settings.AutoIndent = true;
            _buffer.Process("2cc");
            Assert.AreEqual(ModeKind.Insert, _buffer.ModeKind);
            Assert.AreEqual(2, _textView.GetCaretVirtualPoint().VirtualSpaces);
            Assert.AreEqual("", _textView.GetLine(0).GetText());
            Assert.AreEqual("  tree", _textView.GetLine(1).GetText());
        }

        /// <summary>
        /// Caret point should be preserved in virtual space
        /// </summary>
        [Test]
        public void Handle_cc_AutoIndentShouldPreserveFirstOneOnMultiple()
        {
            Create("    dog", "  cat", "  tree");
            _buffer.Settings.AutoIndent = true;
            _buffer.Process("2cc");
            Assert.AreEqual(ModeKind.Insert, _buffer.ModeKind);
            Assert.AreEqual(4, _textView.GetCaretVirtualPoint().VirtualSpaces);
            Assert.AreEqual("", _textView.GetLine(0).GetText());
            Assert.AreEqual("  tree", _textView.GetLine(1).GetText());
        }

        [Test]
        public void Handle_cc_NoAutoIndentShouldRemoveAllOnMultiple()
        {
            Create("  dog", "  cat", "  tree");
            _buffer.Settings.AutoIndent = false;
            _buffer.Process("2cc");
            Assert.AreEqual(ModeKind.Insert, _buffer.ModeKind);
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
            Assert.AreEqual("", _textView.GetLine(0).GetText());
            Assert.AreEqual("  tree", _textView.GetLine(1).GetText());
        }

        [Test]
        public void Handle_cb_DeleteWhitespaceAtEndOfSpan()
        {
            Create("public static void Main");
            _textView.MoveCaretTo(19);
            _buffer.Process("cb");
            Assert.AreEqual(ModeKind.Insert, _buffer.ModeKind);
            Assert.AreEqual("public static Main", _textView.GetLine(0).GetText());
            Assert.AreEqual(14, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void Handle_cl_WithCountShouldDeleteWhitespace()
        {
            Create("dog   cat");
            _buffer.Process("5cl");
            Assert.AreEqual(ModeKind.Insert, _buffer.ModeKind);
            Assert.AreEqual(" cat", _textView.GetLine(0).GetText());
        }

        [Test]
        public void Handle_d_WithMarkLineMotion()
        {
            Create("dog", "cat", "bear", "tree");
            _buffer.MarkMap.SetMark(_textView.GetLine(1).Start, 'a');
            _buffer.Process("d'a");
            Assert.AreEqual("bear", _textView.GetLine(0).GetText());
            Assert.AreEqual("tree", _textView.GetLine(1).GetText());
        }

        [Test]
        public void Handle_d_WithMarkMotion()
        {
            Create("dog", "cat", "bear", "tree");
            _buffer.MarkMap.SetMark(_textView.GetLine(1).Start.Add(1), 'a');
            _buffer.Process("d`a");
            Assert.AreEqual("at", _textView.GetLine(0).GetText());
            Assert.AreEqual("bear", _textView.GetLine(1).GetText());
        }

        /// <summary>
        /// Even though the motion will include the second line it should not 
        /// be included in the delete operation.  This hits the special case
        /// listed in :help exclusive
        /// </summary>
        [Test]
        public void Handle_d_WithParagraphMotion()
        {
            Create("dog", "", "cat");
            _buffer.Process("d}");
            Assert.AreEqual("", _textView.GetLine(0).GetText());
            Assert.AreEqual("cat", _textView.GetLine(1).GetText());
        }

        [Test]
        public void Handle_f_WithTabTarget()
        {
            Create("dog\tcat");
            _buffer.Process("f\t");
            Assert.AreEqual(3, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void Handle_Minus_MiddleOfBuffer()
        {
            Create("dog", "  cat", "bear");
            _textView.MoveCaretToLine(2);
            _buffer.Process("-");
            Assert.AreEqual(_textView.GetLine(1).Start.Add(2), _textView.GetCaretPoint());
        }

        /// <summary>
        /// A putafter at the end of the line should still put the text after the caret
        /// </summary>
        [Test]
        public void PutAfter_EndOfLine()
        {
            Create("dog");
            _textView.MoveCaretTo(2);
            Assert.AreEqual('g', _textView.GetCaretPoint().GetChar());
            UnnamedRegister.UpdateValue("cat", OperationKind.CharacterWise);
            _buffer.Process('p');
            Assert.AreEqual("dogcat", _textView.GetLine(0).GetText());
            Assert.AreEqual(5, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// A putafter on an empty line is the only thing that shouldn't move the caret
        /// </summary>
        [Test]
        public void PutAfter_EmptyLine()
        {
            Create("");
            UnnamedRegister.UpdateValue("cat", OperationKind.CharacterWise);
            _buffer.Process('p');
            Assert.AreEqual("cat", _textView.GetLine(0).GetText());
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Caret should be positioned at the start of the inserted line
        /// </summary>
        [Test]
        public void PutAfter_LineWiseSimpleString()
        {
            Create("dog", "cat", "bear", "tree");
            _buffer.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateValue("pig\n", OperationKind.LineWise);
            _buffer.Process("p");
            Assert.AreEqual("dog", _textView.GetLine(0).GetText());
            Assert.AreEqual("pig", _textView.GetLine(1).GetText());
            Assert.AreEqual(_textView.GetCaretPoint(), _textView.GetLine(1).Start);
        }

        /// <summary>
        /// Caret should be positioned at the start of the indent even when autoindent is off
        /// </summary>
        [Test]
        public void PutAfter_LineWiseWithIndent()
        {
            Create("dog", "cat", "bear", "tree");
            UnnamedRegister.UpdateValue("  pig\n", OperationKind.LineWise);
            _buffer.Settings.AutoIndent = false;
            _buffer.Process("p");
            Assert.AreEqual("dog", _textView.GetLine(0).GetText());
            Assert.AreEqual("  pig", _textView.GetLine(1).GetText());
            Assert.AreEqual(_textView.GetCaretPoint(), _textView.GetLine(1).Start.Add(2));
        }

        /// <summary>
        /// Caret should be positioned on the last character of the inserted text
        /// </summary>
        [Test]
        public void PutAfter_CharacterWiseSimpleString()
        {
            Create("dog", "cat", "bear", "tree");
            _buffer.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateValue("pig", OperationKind.CharacterWise);
            _buffer.Process("p");
            Assert.AreEqual("dpigog", _textView.GetLine(0).GetText());
            Assert.AreEqual(3, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Caret should be positioned after the last character of the inserted text
        /// </summary>
        [Test]
        public void PutAfter_CharacterWiseSimpleString_WithCaretMove()
        {
            Create("dog", "cat", "bear", "tree");
            _buffer.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateValue("pig", OperationKind.CharacterWise);
            _buffer.Process("gp");
            Assert.AreEqual("dpigog", _textView.GetLine(0).GetText());
            Assert.AreEqual(4, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// The caret should be positioned at the last character of the first block string
        /// inserted text
        /// </summary>
        [Test]
        public void PutAfter_BlockOverExisting()
        {
            Create("dog", "cat", "bear", "tree");
            UnnamedRegister.UpdateBlockValues("aa", "bb");
            _buffer.Process("p");
            Assert.AreEqual("daaog", _textView.GetLine(0).GetText());
            Assert.AreEqual("cbbat", _textView.GetLine(1).GetText());
            Assert.AreEqual("bear", _textView.GetLine(2).GetText());
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// The new text should be on new lines at the same indetn and the caret posion should
        /// be the same as puting over existing lines
        /// </summary>
        [Test]
        public void PutAfter_BlockOnNewLines()
        {
            Create("dog");
            _textView.MoveCaretTo(1);
            UnnamedRegister.UpdateBlockValues("aa", "bb");
            _buffer.Process("p");
            Assert.AreEqual("doaag", _textView.GetLine(0).GetText());
            Assert.AreEqual("  bb", _textView.GetLine(1).GetText());
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// This should cause the cursor to be put on the first line after the inserted 
        /// lines
        /// </summary>
        [Test]
        public void PutAfter_LineWise_WithCaretMove()
        {
            Create("dog", "cat");
            UnnamedRegister.UpdateValue("pig\ntree\n", OperationKind.LineWise);
            _buffer.Process("gp");
            Assert.AreEqual("dog", _textView.GetLine(0).GetText());
            Assert.AreEqual("pig", _textView.GetLine(1).GetText());
            Assert.AreEqual("tree", _textView.GetLine(2).GetText());
            Assert.AreEqual("cat", _textView.GetLine(3).GetText());
            Assert.AreEqual(_textView.GetLine(3).Start, _textView.GetCaretPoint());
        }

        /// <summary>
        /// Caret should be at the start of the inserted text
        /// </summary>
        [Test]
        public void PutBefore_LineWiseStartOfBuffer()
        {
            Create("dog");
            UnnamedRegister.UpdateValue("pig\n", OperationKind.LineWise);
            _buffer.Process("P");
            Assert.AreEqual("pig", _textView.GetLine(0).GetText());
            Assert.AreEqual("dog", _textView.GetLine(1).GetText());
            Assert.AreEqual(0, _textView.GetCaretPoint());
        }

        /// <summary>
        /// Caret should be positioned at the start of the indented text
        /// </summary>
        [Test]
        public void PutBefore_LineWiseStartOfBufferWithIndent()
        {
            Create("dog");
            UnnamedRegister.UpdateValue("  pig\n", OperationKind.LineWise);
            _buffer.Process("P");
            Assert.AreEqual("  pig", _textView.GetLine(0).GetText());
            Assert.AreEqual("dog", _textView.GetLine(1).GetText());
            Assert.AreEqual(2, _textView.GetCaretPoint());
        }

        /// <summary>
        /// Character should be on the first line of the newly inserted lines
        /// </summary>
        [Test]
        public void PutBefore_LineWiseMiddleOfBuffer()
        {
            Create("dog", "cat");
            _textView.MoveCaretToLine(1);
            UnnamedRegister.UpdateValue("fish\ntree\n", OperationKind.LineWise);
            _buffer.Process("P");
            Assert.AreEqual("dog", _textView.GetLine(0).GetText());
            Assert.AreEqual("fish", _textView.GetLine(1).GetText());
            Assert.AreEqual("tree", _textView.GetLine(2).GetText());
            Assert.AreEqual("cat", _textView.GetLine(3).GetText());
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        /// <summary>
        /// Character should be on the first line after the inserted lines
        /// </summary>
        [Test]
        public void PutBefore_LineWise_WithCaretMove()
        {
            Create("dog", "cat");
            UnnamedRegister.UpdateValue("pig\ntree\n", OperationKind.LineWise);
            _buffer.Process("gP");
            Assert.AreEqual("pig", _textView.GetLine(0).GetText());
            Assert.AreEqual("tree", _textView.GetLine(1).GetText());
            Assert.AreEqual("dog", _textView.GetLine(2).GetText());
            Assert.AreEqual("cat", _textView.GetLine(3).GetText());
            Assert.AreEqual(_textView.GetLine(2).Start, _textView.GetCaretPoint());
        }

        [Test]
        public void PutBefore_CharacterWiseBlockStringOnExistingLines()
        {
            Create("dog", "cat", "bear", "tree");
            _buffer.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateBlockValues("a", "b", "c");
            _buffer.Process("P");
            Assert.AreEqual("adog", _textView.GetLine(0).GetText());
            Assert.AreEqual("bcat", _textView.GetLine(1).GetText());
            Assert.AreEqual("cbear", _textView.GetLine(2).GetText());
            Assert.AreEqual(_textView.GetCaretPoint(), _textView.GetLine(0).Start);
        }

        [Test]
        public void Handle_s_AtEndOfLine()
        {
            Create("dog", "cat");
            _textView.MoveCaretTo(2);
            _buffer.Process('s');
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
            Assert.AreEqual("do", _textView.GetLine(0).GetText());
            Assert.AreEqual(ModeKind.Insert, _buffer.ModeKind);
        }

        /// <summary>
        /// This command should only yank from the current line to the end of the file
        /// </summary>
        [Test]
        public void Handle_yG_NonFirstLine()
        {
            Create("dog", "cat", "bear");
            _textView.MoveCaretToLine(1);
            _buffer.Process("yG");
            Assert.AreEqual("cat" + Environment.NewLine + "bear", _buffer.GetRegister(RegisterName.Unnamed).StringValue);
        }

        [Test]
        public void IncrementalSearch_VeryNoMagic()
        {
            Create("dog", "cat");
            _buffer.Process(@"/\Vog", enter: true);
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void IncrementalSearch_CaseSensitive()
        {
            Create("dogDOG", "cat");
            _buffer.Process(@"/\COG", enter: true);
            Assert.AreEqual(4, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void IncrementalSearch_HandlesEscape()
        {
            Create("dog");
            _buffer.Process("/do");
            _buffer.Process(KeyInputUtil.EscapeKey);
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void IncrementalSearch_HandlesEscapeInOperator()
        {
            Create("dog");
            _buffer.Process("d/do");
            _buffer.Process(KeyInputUtil.EscapeKey);
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void IncrementalSearch_UsedAsOperatorMotion()
        {
            Create("dog cat tree");
            _buffer.Process("d/cat", enter: true);
            Assert.AreEqual("cat tree", _textView.GetLine(0).GetText());
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void IncrementalSearch_DontMoveCaretDuringSearch()
        {
            Create("dog cat tree");
            _buffer.Process("/cat");
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void IncrementalSearch_MoveCaretAfterEnter()
        {
            Create("dog cat tree");
            _buffer.Process("/cat", enter: true);
            Assert.AreEqual(4, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void Mark_SelectionEndIsExclusive()
        {
            Create("the brown dog");
            var span = new SnapshotSpan(_textView.GetPoint(4), _textView.GetPoint(9));
            Assert.AreEqual("brown", span.GetText());
            _textView.Selection.Select(span);
            _textView.Selection.Clear();
            _buffer.Process("y`>");
            Assert.AreEqual("the brow", _buffer.RegisterMap.GetRegister(RegisterName.Unnamed).StringValue);
        }

        [Test]
        public void Mark_NamedMarkIsExclusive()
        {
            Create("the brown dog");
            var point = _textView.GetPoint(8);
            Assert.AreEqual('n', point.GetChar());
            _buffer.MarkMap.SetMark(point, 'b');
            _buffer.Process("y`b");
            Assert.AreEqual("the brow", _buffer.RegisterMap.GetRegister(RegisterName.Unnamed).StringValue);
        }

        [Test]
        public void MatchingToken_Parens()
        {
            Create("cat( )");
            _buffer.Process('%');
            Assert.AreEqual(5, _textView.GetCaretPoint());
            _buffer.Process('%');
            Assert.AreEqual(3, _textView.GetCaretPoint());
        }

        [Test]
        public void MatchingToken_MismatchedBlockComments()
        {
            Create("/* /* */");
            _textView.MoveCaretTo(3);
            _buffer.Process('%');
            Assert.AreEqual(6, _textView.GetCaretPoint());
            _buffer.Process('%');
            Assert.AreEqual(0, _textView.GetCaretPoint());
            _buffer.Process('%');
            Assert.AreEqual(6, _textView.GetCaretPoint());
        }

        /// <summary>
        /// Make sure the caret is properly positioned against a join across 3 lines
        /// </summary>
        [Test]
        public void Join_CaretPositionThreeLines()
        {
            Create("cat", "dog", "bear");
            _buffer.Process("3J");
            Assert.AreEqual("cat dog bear", _textView.GetLine(0).GetText());
            Assert.AreEqual(7, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Ensure the text inserted is repeated after the Escape
        /// </summary>
        [Test]
        public void InsertLineBelowCaret_WithCount()
        {
            Create("dog", "bear");
            _buffer.Process("2o");
            _buffer.Process("cat");
            _buffer.Process(VimKey.Escape);
            Assert.AreEqual("dog", _textView.GetLine(0).GetText());
            Assert.AreEqual("cat", _textView.GetLine(1).GetText());
            Assert.AreEqual("cat", _textView.GetLine(2).GetText());
            Assert.AreEqual("bear", _textView.GetLine(3).GetText());
            Assert.AreEqual(_textView.GetLine(2).Start.Add(2), _textView.GetCaretPoint());
        }

        /// <summary>
        /// Make sure the text is repeated
        /// </summary>
        [Test]
        public void InsertAtEndOfLine_WithCount()
        {
            Create("dog", "bear");
            _buffer.Process("3A");
            _buffer.Process('b');
            _buffer.Process(VimKey.Escape);
            Assert.AreEqual("dogbbb", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// Make sure the matching token behavior fits all of the issues described in 
        /// issue 468
        /// </summary>
        [Test]
        public void MatchingTokens_Issue468()
        {
            Create("(wchar_t*) realloc(pwcsSelFile, (nSelFileLen+1)*sizeof(wchar_t))");
            
            // First open paren to the next closing one
            _buffer.Process("%");
            Assert.AreEqual(9, _textView.GetCaretPoint().Position);

            // From the first closing paren back to the start
            _buffer.Process("%");
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);

            // From the second opening paren to the last one
            var lastPoint = _textView.TextSnapshot.GetEndPoint().Subtract(1);
            Assert.AreEqual(')', lastPoint.GetChar());
            _textView.MoveCaretTo(18);
            Assert.AreEqual('(', _textView.GetCaretPoint().GetChar());
            _buffer.Process("%");
            Assert.AreEqual(lastPoint, _textView.GetCaretPoint());

            // And back to the start one
            _buffer.Process("%");
            Assert.AreEqual(18, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Make sure we jump correctly between matching token values of different types
        /// </summary>
        [Test]
        public void MatchingTokens_DifferentTypes()
        {
            Create("{ { (( } /* a /*) b */ })");
            Action<int, int> del = (start, end) =>
                {
                    _textView.MoveCaretTo(start);
                    _buffer.Process("%");
                    Assert.AreEqual(end, _textView.GetCaretPoint().Position);

                    if (start != end)
                    {
                        _textView.MoveCaretTo(end);
                        _buffer.Process("%");
                        Assert.AreEqual(start, _textView.GetCaretPoint().Position);
                    }
                };
            del(0, 23);
            del(2, 7);
            del(4, 24);
            del(5, 16);
            del(9, 20);
        }

        /// <summary>
        /// Make sure repeat last char search is functioning
        /// </summary>
        [Test]
        public void RepeatLastCharSearch_Forward()
        {
            Create("hello", "world");
            _buffer.Process("fr");
            _textView.MoveCaretToLine(1);
            _buffer.Process(";");
            Assert.AreEqual(_textView.GetLine(1).Start.Add(2), _textView.GetCaretPoint());
        }

        /// <summary>
        /// Enter should not go through normal mode mapping during an incremental search
        /// </summary>
        [Test]
        public void Remap_EnterShouldNotMapDuringSearch()
        {
            Create("cat dog");
            _keyMap.MapWithNoRemap("<Enter>", "o<Esc>", KeyRemapMode.Normal);
            _buffer.Process("/dog");
            _buffer.Process(VimKey.Enter);
            Assert.AreEqual(4, _textView.GetCaretPoint().Position);
            Assert.AreEqual("cat dog", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// Ensure the commands map properly
        /// </summary>
        [Test]
        public void Remap_Issue474()
        {
            Create("cat", "dog", "bear", "pig", "tree", "fish");
            _buffer.Process(":nnoremap gj J");
            _buffer.Process(VimKey.Enter);
            _buffer.Process(":map J 4j");
            _buffer.Process(VimKey.Enter);
            _buffer.Process("J");
            Assert.AreEqual(4, _textView.GetCaretLine().LineNumber);
            _textView.MoveCaretTo(0);
            _buffer.Process("gj");
            Assert.AreEqual("cat dog", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// Incremental search should re-use the last search if the entered search string is
        /// empty.  It should ignore the direction though and base it's search off the '/' or
        /// '?' it was created with
        /// </summary>
        [Test]
        public void LastSearch_IncrementalReuse()
        {
            Create("dog cat dog");
            _textView.MoveCaretTo(1);
            _buffer.Settings.GlobalSettings.WrapScan = false;
            _buffer.VimData.LastPatternData = VimUtil.CreatePatternData("dog", Path.Backward);
            _buffer.Process('/');
            _buffer.Process(VimKey.Enter);
            Assert.AreEqual(8, _textView.GetCaretPoint());
        }

        /// <summary>
        /// Substitute command should set the LastSearch value
        /// </summary>
        [Test]
        public void LastSearch_SetBySubstitute()
        {
            Create("dog cat dog");
            _buffer.Process(":s/dog/cat", enter: true);
            Assert.AreEqual("dog", _buffer.VimData.LastPatternData.Pattern);
        }

        /// <summary>
        /// Substitute command should re-use the LastSearch value if there is no specific 
        /// search value set
        /// </summary>
        [Test]
        public void LastSearch_UsedBySubstitute()
        {
            Create("dog cat dog");
            _buffer.VimData.LastPatternData = VimUtil.CreatePatternData("dog");
            _buffer.Process(":s//cat", enter: true);
            Assert.AreEqual("cat cat dog", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// The search options used by a :s command should not be stored.  For example the 
        /// 'i' flag is used only for the :s command and not for repeats of the search 
        /// later on.
        /// </summary>
        [Test]
        public void LastSearch_DontStoreSearchOptions()
        {
            Create("cat", "dog", "cat");
            _assertOnErrorMessage = false;
            _globalSettings.IgnoreCase = false;
            _globalSettings.WrapScan = true;
            _textView.MoveCaretToLine(2);
            _buffer.Process(":s/CAT/fish/i", enter: true);
            Assert.AreEqual("fish", _textView.GetLine(2).GetText());
            var didHit = false;
            _buffer.ErrorMessage +=
                (sender, message) =>
                {
                    Assert.AreEqual(Resources.Common_PatternNotFound("CAT"), message);
                    didHit = true;
                };
            _buffer.Process("n");
            Assert.IsTrue(didHit);
        }

        /// <summary>
        /// Make sure we properly update register 0 during a yank
        /// </summary>
        [Test]
        public void Yank_Register0()
        {
            Create("dog", "cat", "fish");
            _buffer.Process("yaw");
            _textView.MoveCaretToLine(1);
            _buffer.Process("\"cyaw");
            _textView.MoveCaretToLine(2);
            _buffer.Process("dw");
            _buffer.Process("\"0p");
            Assert.AreEqual("dog", _textView.GetLine(2).GetText());
        }

        /// <summary>
        /// Make sure we move to the column on the current line when there is no count
        /// </summary>
        [Test]
        public void Motion_FirstNonWhiteSpaceOnLine()
        {
            Create(" cat", "  dog", "   fish");
            _textView.MoveCaretToLine(1);
            _buffer.Process("_");
            Assert.AreEqual(_textView.GetLine(1).Start.Add(2), _textView.GetCaretPoint());
        }
    }
}
