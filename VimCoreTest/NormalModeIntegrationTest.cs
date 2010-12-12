using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;

namespace VimCore.Test
{
    [TestFixture]
    public class NormalModeIntegrationTest
    {
        private IVimBuffer _buffer;
        private IWpfTextView _textView;

        public void CreateBuffer(params string[] lines)
        {
            var tuple = EditorUtil.CreateViewAndOperations(lines);
            _textView = tuple.Item1;
            var service = EditorUtil.FactoryService;
            _buffer = service.vim.CreateBuffer(_textView);
        }

        [Test]
        public void dd_OnLastLine()
        {
            CreateBuffer("foo", "bar");
            _textView.MoveCaretTo(_textView.GetLine(1).Start);
            _buffer.Process("dd");
            Assert.AreEqual("foo", _textView.TextSnapshot.GetText());
            Assert.AreEqual(1, _textView.TextSnapshot.LineCount);
        }

        [Test]
        public void dot_Repeated1()
        {
            CreateBuffer("the fox chased the bird");
            _buffer.Process("dw");
            Assert.AreEqual("fox chased the bird", _textView.TextSnapshot.GetText());
            _buffer.Process(".");
            Assert.AreEqual("chased the bird", _textView.TextSnapshot.GetText());
            _buffer.Process(".");
            Assert.AreEqual("the bird", _textView.TextSnapshot.GetText());
        }

        [Test]
        public void dot_LinkedTextChange1()
        {
            CreateBuffer("the fox chased the bird");
            _buffer.Process("cw");
            _buffer.TextBuffer.Insert(0, "hey ");
            _buffer.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Escape));
            _textView.MoveCaretTo(4);
            _buffer.Process(KeyInputUtil.CharToKeyInput('.'));
            Assert.AreEqual("hey hey fox chased the bird", _textView.TextSnapshot.GetText());
        }

        [Test]
        public void dot_LinkedTextChange2()
        {
            CreateBuffer("the fox chased the bird");
            _buffer.Process("cw");
            _buffer.TextBuffer.Insert(0, "hey");
            _buffer.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Escape));
            _textView.MoveCaretTo(4);
            _buffer.Process(KeyInputUtil.CharToKeyInput('.'));
            Assert.AreEqual("hey hey chased the bird", _textView.TextSnapshot.GetText());
        }

        [Test]
        public void dot_LinkedTextChange3()
        {
            CreateBuffer("the fox chased the bird");
            _buffer.Process("cw");
            _buffer.TextBuffer.Insert(0, "hey");
            _buffer.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Escape));
            _textView.MoveCaretTo(4);
            _buffer.Process(KeyInputUtil.CharToKeyInput('.'));
            _buffer.Process(KeyInputUtil.CharToKeyInput('.'));
            Assert.AreEqual("hey hehey chased the bird", _textView.TextSnapshot.GetText());
        }

        [Test]
        [Description("See issue 288")]
        public void dj_1()
        {
            CreateBuffer("abc", "def", "ghi", "jkl");
            _buffer.Process("dj");
            Assert.AreEqual("ghi", _textView.GetLine(0).GetText());
            Assert.AreEqual("jkl", _textView.GetLine(1).GetText());
        }

        [Test]
        [Description("A d with Enter should delete the line break")]
        public void Issue317_1()
        {
            CreateBuffer("dog", "cat", "jazz", "band");
            _buffer.Process("2d");
            _buffer.Process(VimKey.Enter);
            Assert.AreEqual("band", _textView.GetLine(0).GetText());
        }

        [Test]
        [Description("Verify the contents after with a paste")]
        public void Issue317_2()
        {
            CreateBuffer("dog", "cat", "jazz", "band");
            _buffer.Process("2d");
            _buffer.Process(VimKey.Enter);
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
            CreateBuffer("dog", "cat", "jazz", "band");
            _buffer.Process(VimKey.Enter);
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test]
        [Description("[[ motion should put the caret on the target character")]
        public void SectionMotion1()
        {
            CreateBuffer("hello", "{world");
            _buffer.Process("]]");
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test]
        [Description("[[ motion should put the caret on the target character")]
        public void SectionMotion2()
        {
            CreateBuffer("hello", "\fworld");
            _buffer.Process("]]");
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test]
        public void SectionMotion3()
        {
            CreateBuffer("foo", "{", "bar");
            _textView.MoveCaretTo(_textView.GetLine(2).End);
            _buffer.Process("[[");
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test]
        public void SectionMotion4()
        {
            CreateBuffer("foo", "{", "bar", "baz");
            _textView.MoveCaretTo(_textView.GetLine(3).End);
            _buffer.Process("[[");
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test]
        public void SectionMotion5()
        {
            CreateBuffer("foo", "{", "bar", "baz", "jazz");
            _textView.MoveCaretTo(_textView.GetLine(4).Start);
            _buffer.Process("[[");
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test]
        public void ParagraphMotion1()
        {
            CreateBuffer("foo", "{", "bar", "baz", "jazz");
            _textView.MoveCaretTo(_textView.TextSnapshot.GetEndPoint());
            _buffer.Process("{{");
        }

        [Test]
        public void RepeatLastSearch1()
        {
            CreateBuffer("random text", "pig dog cat", "pig dog cat", "pig dog cat");
            _buffer.Process("/pig");
            _buffer.Process(VimKey.Enter);
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            _textView.MoveCaretTo(0);
            _buffer.Process('n');
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test]
        public void RepeatLastSearch2()
        {
            CreateBuffer("random text", "pig dog cat", "pig dog cat", "pig dog cat");
            _buffer.Process("/pig");
            _buffer.Process(VimKey.Enter);
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            _buffer.Process('n');
            Assert.AreEqual(_textView.GetLine(2).Start, _textView.GetCaretPoint());
        }

        [Test]
        public void RepeatLastSearch3()
        {
            CreateBuffer("random text", "pig dog cat", "random text", "pig dog cat", "pig dog cat");
            _buffer.Process("/pig");
            _buffer.Process(VimKey.Enter);
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            _textView.MoveCaretTo(_textView.GetLine(2).Start);
            _buffer.Process('N');
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test]
        [Description("With no virtual edit the cursor should move backwards after x")]
        public void CursorPositionWith_x_1()
        {
            CreateBuffer("test");
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
            CreateBuffer("test", "bar");
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
            CreateBuffer("test", "bar");
            _buffer.Settings.GlobalSettings.VirtualEdit = string.Empty;
            _textView.MoveCaretTo(1);
            _buffer.Process('x');
            Assert.AreEqual("tst", _textView.GetLineRange(0).GetText());
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void RepeatCommand_DeleteWord1()
        {
            CreateBuffer("the cat jumped over the dog");
            _buffer.Process("dw");
            _buffer.Process(".");
            Assert.AreEqual("jumped over the dog", _textView.GetLine(0).GetText());
        }

        [Test]
        [Description("Make sure that movement doesn't reset the last edit command")]
        public void RepeatCommand_DeleteWord2()
        {
            CreateBuffer("the cat jumped over the dog");
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
            CreateBuffer("the cat jumped over the dog");
            _buffer.Process("2dw");
            _buffer.Process(".");
            Assert.AreEqual("the dog", _textView.GetLine(0).GetText());
        }

        [Test]
        public void RepeatCommand_DeleteLine1()
        {
            CreateBuffer("bear", "dog", "cat", "zebra", "fox", "jazz");
            _buffer.Process("dd");
            _buffer.Process(".");
            Assert.AreEqual("cat", _textView.GetLine(0).GetText());
        }

        [Test]
        public void RepeatCommand_DeleteLine2()
        {
            CreateBuffer("bear", "dog", "cat", "zebra", "fox", "jazz");
            _buffer.Process("2dd");
            _buffer.Process(".");
            Assert.AreEqual("fox", _textView.GetLine(0).GetText());
        }

        [Test]
        public void RepeatCommand_ShiftLeft1()
        {
            CreateBuffer("    bear", "    dog", "    cat", "    zebra", "    fox", "    jazz");
            _buffer.Settings.GlobalSettings.ShiftWidth = 1;
            _buffer.Process("<<");
            _buffer.Process(".");
            Assert.AreEqual("  bear", _textView.GetLine(0).GetText());
        }

        [Test]
        public void RepeatCommand_ShiftLeft2()
        {
            CreateBuffer("    bear", "    dog", "    cat", "    zebra", "    fox", "    jazz");
            _buffer.Settings.GlobalSettings.ShiftWidth = 1;
            _buffer.Process("2<<");
            _buffer.Process(".");
            Assert.AreEqual("  bear", _textView.GetLine(0).GetText());
            Assert.AreEqual("  dog", _textView.GetLine(1).GetText());
        }

        [Test]
        public void RepeatCommand_ShiftRight1()
        {
            CreateBuffer("bear", "dog", "cat", "zebra", "fox", "jazz");
            _buffer.Settings.GlobalSettings.ShiftWidth = 1;
            _buffer.Process(">>");
            _buffer.Process(".");
            Assert.AreEqual("  bear", _textView.GetLine(0).GetText());
        }

        [Test]
        public void RepeatCommand_ShiftRight2()
        {
            CreateBuffer("bear", "dog", "cat", "zebra", "fox", "jazz");
            _buffer.Settings.GlobalSettings.ShiftWidth = 1;
            _buffer.Process("2>>");
            _buffer.Process(".");
            Assert.AreEqual("  bear", _textView.GetLine(0).GetText());
            Assert.AreEqual("  dog", _textView.GetLine(1).GetText());
        }

        [Test]
        public void RepeatCommand_DeleteChar1()
        {
            CreateBuffer("longer");
            _buffer.Process("x");
            _buffer.Process(".");
            Assert.AreEqual("nger", _textView.GetLine(0).GetText());
        }

        [Test]
        public void RepeatCommand_DeleteChar2()
        {
            CreateBuffer("longer");
            _buffer.Process("2x");
            _buffer.Process(".");
            Assert.AreEqual("er", _textView.GetLine(0).GetText());
        }

        [Test]
        [Description("After a search operation")]
        public void RepeatCommand_DeleteChar3()
        {
            CreateBuffer("bear", "dog", "cat", "zebra", "fox", "jazz");
            _buffer.Process("/e");
            _buffer.Process(VimKey.Enter);
            _buffer.Process("x");
            _buffer.Process("n");
            _buffer.Process(".");
            Assert.AreEqual("bar", _textView.GetLine(0).GetText());
            Assert.AreEqual("zbra", _textView.GetLine(3).GetText());
        }

        [Test]
        public void RepeatCommand_Put1()
        {
            CreateBuffer("cat");
            _buffer.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateValue("lo");
            _buffer.Process("p");
            _buffer.Process(".");
            Assert.AreEqual("cloloat", _textView.GetLine(0).GetText());
        }

        [Test]
        public void RepeatCommand_Put2()
        {
            CreateBuffer("cat");
            _buffer.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateValue("lo");
            _buffer.Process("2p");
            _buffer.Process(".");
            Assert.AreEqual("clolololoat", _textView.GetLine(0).GetText());
        }

        [Test]
        public void RepeatCommand_JoinLines1()
        {
            CreateBuffer("bear", "dog", "cat", "zebra", "fox", "jazz");
            _buffer.Process("J");
            _buffer.Process(".");
            Assert.AreEqual("bear dog cat", _textView.GetLine(0).GetText());
        }

        [Test]
        public void RepeatCommand_Change1()
        {
            CreateBuffer("bear", "dog", "cat", "zebra", "fox", "jazz");
            _buffer.Process("cl");
            _buffer.TextBuffer.Delete(new Span(_textView.GetCaretPoint(), 1));
            _buffer.Process(VimKey.Escape);
            _buffer.Process(VimKey.Down);
            _buffer.Process(".");
            Assert.AreEqual("ar", _textView.GetLine(0).GetText());
            Assert.AreEqual("g", _textView.GetLine(1).GetText());
        }

        [Test]
        public void RepeatCommand_Change2()
        {
            CreateBuffer("bear", "dog", "cat", "zebra", "fox", "jazz");
            _buffer.Process("cl");
            _buffer.TextBuffer.Insert(0, "u");
            _buffer.Process(VimKey.Escape);
            _buffer.Process(VimKey.Down);
            _buffer.Process(".");
            Assert.AreEqual("uear", _textView.GetLine(0).GetText());
            Assert.AreEqual("uog", _textView.GetLine(1).GetText());
        }

        [Test]
        public void RepeatCommand_Substitute1()
        {
            CreateBuffer("bear", "dog", "cat", "zebra", "fox", "jazz");
            _buffer.Process("s");
            _buffer.TextBuffer.Insert(0, "u");
            _buffer.Process(VimKey.Escape);
            _buffer.Process(VimKey.Down);
            _buffer.Process(".");
            Assert.AreEqual("uear", _textView.GetLine(0).GetText());
            Assert.AreEqual("uog", _textView.GetLine(1).GetText());
        }

        [Test]
        public void RepeatCommand_Substitute2()
        {
            CreateBuffer("bear", "dog", "cat", "zebra", "fox", "jazz");
            _buffer.Process("s");
            _buffer.TextBuffer.Insert(0, "u");
            _buffer.Process(VimKey.Escape);
            _buffer.Process(VimKey.Down);
            _buffer.Process("2.");
            Assert.AreEqual("uear", _textView.GetLine(0).GetText());
            Assert.AreEqual("ug", _textView.GetLine(1).GetText());
        }

        [Test]
        public void RepeatCommand_TextInsert1()
        {
            CreateBuffer("bear", "dog", "cat", "zebra", "fox", "jazz");
            _buffer.Process("i");
            _buffer.TextBuffer.Insert(0, "abc");
            _buffer.Process(VimKey.Escape);
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
            _buffer.Process(".");
            Assert.AreEqual("ababccbear", _textView.GetLine(0).GetText());
        }

        [Test]
        public void RepeatCommand_TextInsert2()
        {
            CreateBuffer("bear", "dog", "cat", "zebra", "fox", "jazz");
            _buffer.Process("i");
            _buffer.TextBuffer.Insert(0, "abc");
            _buffer.Process(VimKey.Escape);
            _textView.MoveCaretTo(0);
            _buffer.Process(".");
            Assert.AreEqual("abcabcbear", _textView.GetLine(0).GetText());
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void RepeatCommand_TextInsert3()
        {
            CreateBuffer("bear", "dog", "cat", "zebra", "fox", "jazz");
            _buffer.Process("i");
            _buffer.TextBuffer.Insert(0, "abc");
            _buffer.Process(VimKey.Escape);
            _textView.MoveCaretTo(0);
            _buffer.Process(".");
            _buffer.Process(".");
            Assert.AreEqual("ababccabcbear", _textView.GetLine(0).GetText());
        }

        [Test]
        [Description("The first repeat of I should go to the first non-blank")]
        public void RepeatCommand_CapitalI1()
        {
            CreateBuffer("bear", "dog", "cat", "zebra", "fox", "jazz");
            _buffer.Process("I");
            _buffer.TextBuffer.Insert(0, "abc");
            _buffer.Process(VimKey.Escape);
            _textView.MoveCaretTo(_textView.GetLine(1).Start.Add(2));
            _buffer.Process(".");
            Assert.AreEqual("abcdog", _textView.GetLine(1).GetText());
            Assert.AreEqual(_textView.GetLine(1).Start.Add(2), _textView.GetCaretPoint());
        }

        [Test]
        [Description("The first repeat of I should go to the first non-blank")]
        public void RepeatCommand_CapitalI2()
        {
            CreateBuffer("bear", "  dog", "cat", "zebra", "fox", "jazz");
            _buffer.Process("I");
            _buffer.TextBuffer.Insert(0, "abc");
            _buffer.Process(VimKey.Escape);
            _textView.MoveCaretTo(_textView.GetLine(1).Start.Add(2));
            _buffer.Process(".");
            Assert.AreEqual("  abcdog", _textView.GetLine(1).GetText());
            Assert.AreEqual(_textView.GetLine(1).Start.Add(4), _textView.GetCaretPoint());
        }

        [Test]
        public void Map_ToCharDoesNotUseMap()
        {
            CreateBuffer("bear; again: dog");
            _buffer.Process(":map ; :", enter: true);
            _buffer.Process("dt;");
            Assert.AreEqual("; again: dog", _textView.GetLine(0).GetText());
        }

        [Test]
        public void Map_AlphaToRightMotion()
        {
            CreateBuffer("dog");
            _buffer.Process(":map a l", enter: true);
            _buffer.Process("aa");
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void Map_OperatorPendingWithAmbiguousCommandPrefix()
        {
            CreateBuffer("dog chases the ball");
            _buffer.Process(":map a w", enter: true);
            _buffer.Process("da");
            Assert.AreEqual("chases the ball", _textView.GetLine(0).GetText());
        }

        [Test]
        public void Map_ReplaceDoesntUseNormalMap()
        {
            CreateBuffer("dog");
            _buffer.Process(":map f g", enter: true);
            _buffer.Process("rf");
            Assert.AreEqual("fog", _textView.GetLine(0).GetText());
        }
    }
}
