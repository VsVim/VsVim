using System.Threading;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class VisualModeIntegrationTest
    {
        private IVimBuffer _buffer;
        private IWpfTextView _textView;
        private TestableSynchronizationContext _context;

        public void Create(params string[] lines)
        {
            _context = new TestableSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(_context);
            var tuple = EditorUtil.CreateViewAndOperations(lines);
            _textView = tuple.Item1;
            var service = EditorUtil.FactoryService;
            _buffer = service.vim.CreateBuffer(_textView);
            Assert.IsTrue(_context.IsEmpty);
        }

        private void EnterMode(SnapshotSpan span, TextSelectionMode mode = TextSelectionMode.Stream)
        {
            _textView.SelectAndUpdateCaret(span, mode);
            Assert.IsFalse(_context.IsEmpty);
            _context.RunAll();
            Assert.IsTrue(_context.IsEmpty);
        }

        private void EnterMode(ModeKind kind, SnapshotSpan span, TextSelectionMode mode = TextSelectionMode.Stream)
        {
            EnterMode(span, mode);
            _buffer.SwitchMode(kind, ModeArgument.None);
        }

        [Test]
        public void Repeat1()
        {
            Create("dog again", "cat again", "chicken");
            EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0, 1).ExtentIncludingLineBreak);
            _buffer.Settings.GlobalSettings.ShiftWidth = 2;
            _buffer.Process(">.");
            Assert.AreEqual("    dog again", _textView.GetLine(0).GetText());
        }

        [Test]
        public void Repeat2()
        {
            Create("dog again", "cat again", "chicken");
            EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0, 1).ExtentIncludingLineBreak);
            _buffer.Settings.GlobalSettings.ShiftWidth = 2;
            _buffer.Process(">..");
            Assert.AreEqual("      dog again", _textView.GetLine(0).GetText());
        }

        [Test]
        public void ResetCaretFromShiftLeft1()
        {
            Create("  hello", "  world");
            EnterMode(_textView.GetLineRange(0, 1).Extent);
            _buffer.Process("<");
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void ResetCaretFromShiftLeft2()
        {
            Create("  hello", "  world");
            EnterMode(_textView.GetLineRange(0, 1).Extent);
            _buffer.Process("<");
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void ResetCaretFromYank1()
        {
            Create("  hello", "  world");
            EnterMode(_textView.TextBuffer.GetSpan(0, 2));
            _buffer.Process("y");
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        [Test]
        [Description("Moving the caret which resets the selection should go to normal mode")]
        public void SelectionChange1()
        {
            Create("  hello", "  world");
            EnterMode(_textView.TextBuffer.GetSpan(0, 2));
            Assert.AreEqual(ModeKind.VisualCharacter, _buffer.ModeKind);
            _textView.Selection.Select(
                new SnapshotSpan(_textView.GetLine(1).Start, 0),
                false);
            _context.RunAll();
            Assert.AreEqual(ModeKind.Normal, _buffer.ModeKind);
        }

        [Test]
        [Description("Moving the caret which resets the selection should go visual if there is still a selection")]
        public void SelectionChange2()
        {
            Create("  hello", "  world");
            EnterMode(_textView.TextBuffer.GetSpan(0, 2));
            Assert.AreEqual(ModeKind.VisualCharacter, _buffer.ModeKind);
            _textView.Selection.Select(
                new SnapshotSpan(_textView.GetLine(1).Start, 1),
                false);
            _context.RunAll();
            Assert.AreEqual(ModeKind.VisualCharacter, _buffer.ModeKind);
        }

        [Test]
        [Description("Make sure we reset the span we need")]
        public void SelectionChange3()
        {
            Create("  hello", "  world");
            EnterMode(_textView.GetLine(0).Extent);
            Assert.AreEqual(ModeKind.VisualCharacter, _buffer.ModeKind);
            _textView.Selection.Select(_textView.GetLine(1).Extent, false);
            _buffer.Process(KeyInputUtil.CharToKeyInput('y'));
            _context.RunAll();
            Assert.AreEqual("  world", _buffer.RegisterMap.GetRegister(RegisterName.Unnamed).StringValue);
        }

        [Test]
        [Description("Make sure we reset the span we need")]
        public void SelectionChange4()
        {
            Create("  hello", "  world");
            EnterMode(_textView.GetLine(0).Extent);
            Assert.AreEqual(ModeKind.VisualCharacter, _buffer.ModeKind);
            _textView.SelectAndUpdateCaret(new SnapshotSpan(_textView.GetLine(1).Start, 2));
            _context.RunAll();
            _buffer.Process(KeyInputUtil.CharToKeyInput('l'));
            _buffer.Process(KeyInputUtil.CharToKeyInput('y'));
            Assert.AreEqual("  wo", _buffer.RegisterMap.GetRegister(RegisterName.Unnamed).StringValue);
        }

        [Test]
        [Description("Enter Visual Line Mode")]
        public void EnterVisualLine1()
        {
            Create("hello", "world");
            _buffer.Process(KeyNotationUtil.StringToKeyInput("<S-v>"));
            Assert.AreEqual(ModeKind.VisualLine, _buffer.ModeKind);
        }

        [Test]
        public void SwitchToCommandModeShouldPreserveSelection()
        {
            Create("dog", "pig", "chicken");
            EnterMode(_textView.GetLineRange(0, 1).Extent);
            _buffer.Process(':');
            Assert.IsFalse(_textView.Selection.IsEmpty);
        }

        [Test]
        [Description("Even though a text span is selected, substitute should operate on the line")]
        public void Substitute1()
        {
            Create("the boy hit the cat", "bat");
            EnterMode(new SnapshotSpan(_textView.TextSnapshot, 0, 2));
            _buffer.Process(":s/a/o", enter: true);
            Assert.AreEqual("the boy hit the cot", _textView.GetLine(0).GetText());
            Assert.AreEqual("bat", _textView.GetLine(1).GetText());
        }

        [Test]
        [Description("Muliline selection should cause a replace per line")]
        public void Substitute2()
        {
            Create("the boy hit the cat", "bat");
            EnterMode(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak);
            _buffer.Process(":s/a/o", enter: true);
            Assert.AreEqual("the boy hit the cot", _textView.GetLine(0).GetText());
            Assert.AreEqual("bot", _textView.GetLine(1).GetText());
        }

        [Test]
        public void IncrementalSearch_LineModeShouldSelectFullLine()
        {
            Create("dog", "cat", "tree");
            EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0, 1).ExtentIncludingLineBreak);
            _buffer.Process("/c");
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            Assert.AreEqual(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak, _textView.GetSelectionSpan());
        }

        [Test]
        public void IncrementalSearch_LineModeShouldSelectFullLineAcrossBlanks()
        {
            Create("dog", "", "cat", "tree");
            EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0, 1).ExtentIncludingLineBreak);
            _buffer.Process("/ca");
            Assert.AreEqual(_textView.GetLine(2).Start, _textView.GetCaretPoint());
            Assert.AreEqual(_textView.GetLineRange(0, 2).ExtentIncludingLineBreak, _textView.GetSelectionSpan());
        }

        [Test]
        public void Handle_p_SimpleReplacesCharSpan()
        {
            Create("dog", "cat", "bear", "tree");
            EnterMode(ModeKind.VisualCharacter, _textView.GetLineRange(0).Extent);
            _buffer.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateValue("pig");
            _buffer.Process("p");
            Assert.AreEqual("pig", _textView.GetLine(0).GetText());
            Assert.AreEqual("cat", _textView.GetLine(1).GetText());
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void Handle_p_SimpleReplacesCharSpanNotFullLine()
        {
            Create("dog", "cat", "bear", "tree");
            EnterMode(ModeKind.VisualCharacter, new SnapshotSpan(_textView.TextSnapshot, 0, 2));
            _buffer.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateValue("pig");
            _buffer.Process("p");
            Assert.AreEqual("pigg", _textView.GetLine(0).GetText());
            Assert.AreEqual("cat", _textView.GetLine(1).GetText());
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void Handle_p_SimpleReplacesCharSpanMultiline()
        {
            Create("dog", "cat", "bear", "tree");
            var span = new SnapshotSpan(
                _textView.GetLine(0).Start.Add(1),
                _textView.GetLine(1).Start.Add(2));
            EnterMode(ModeKind.VisualCharacter, span);
            _buffer.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateValue("pig");
            _buffer.Process("p");
            Assert.AreEqual("dpigt", _textView.GetLine(0).GetText());
            Assert.AreEqual("bear", _textView.GetLine(1).GetText());
            Assert.AreEqual(3, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void Handle_P_SimpleReplacesCharSpan()
        {
            Create("dog", "cat", "bear", "tree");
            EnterMode(ModeKind.VisualCharacter, _textView.GetLineRange(0).Extent);
            _buffer.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateValue("pig");
            _buffer.Process("P");
            Assert.AreEqual("pig", _textView.GetLine(0).GetText());
            Assert.AreEqual("cat", _textView.GetLine(1).GetText());
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
        }
    }
}
