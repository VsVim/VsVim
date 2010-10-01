using System.Threading;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;

namespace VimCore.Test
{
    [TestFixture]
    public class VisualModeIntegrationTest
    {
        private IVimBuffer _buffer;
        private IWpfTextView _textView;
        private TestableSynchronizationContext _context;

        public void CreateBuffer(params string[] lines)
        {
            _context = new TestableSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(_context);
            var tuple = EditorUtil.CreateViewAndOperations(lines);
            _textView = tuple.Item1;
            var service = EditorUtil.FactoryService;
            _buffer = service.vim.CreateBuffer(_textView);
            Assert.IsTrue(_context.IsEmpty);
        }

        public void EnterModeWithSelection(SnapshotSpan span, TextSelectionMode mode = TextSelectionMode.Stream)
        {
            _textView.SelectAndUpdateCaret(span, mode);
            Assert.IsFalse(_context.IsEmpty);
            _context.RunAll();
            Assert.IsTrue(_context.IsEmpty);
        }

        public void EnterMode(ModeKind kind, SnapshotSpan span, TextSelectionMode mode = TextSelectionMode.Stream)
        {
            EnterModeWithSelection(span, mode);
            _buffer.SwitchMode(kind, ModeArgument.None);
        }

        [Test]
        public void Repeat1()
        {
            CreateBuffer("dog again", "cat again", "chicken");
            EnterMode(ModeKind.VisualLine, _textView.GetLineSpanIncludingLineBreak(0, 1));
            _buffer.Settings.GlobalSettings.ShiftWidth = 2;
            _buffer.Process(">.");
            Assert.AreEqual("    dog again", _textView.GetLine(0).GetText());
        }

        [Test]
        public void Repeat2()
        {
            CreateBuffer("dog again", "cat again", "chicken");
            EnterMode(ModeKind.VisualLine, _textView.GetLineSpanIncludingLineBreak(0, 1));
            _buffer.Settings.GlobalSettings.ShiftWidth = 2;
            _buffer.Process(">..");
            Assert.AreEqual("      dog again", _textView.GetLine(0).GetText());
        }

        [Test]
        public void ResetCaretFromShiftLeft1()
        {
            CreateBuffer("  hello", "  world");
            EnterModeWithSelection(_textView.GetLineSpan(0, 1));
            _buffer.Process("<");
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void ResetCaretFromShiftLeft2()
        {
            CreateBuffer("  hello", "  world");
            EnterModeWithSelection(_textView.GetLineSpan(0, 1));
            _buffer.Process("<");
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void ResetCaretFromYank1()
        {
            CreateBuffer("  hello", "  world");
            EnterModeWithSelection(_textView.TextBuffer.GetSpan(0, 2));
            _buffer.Process("y");
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        [Test]
        [Description("Moving the caret which resets the selection should go to normal mode")]
        public void SelectionChange1()
        {
            CreateBuffer("  hello", "  world");
            EnterModeWithSelection(_textView.TextBuffer.GetSpan(0, 2));
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
            CreateBuffer("  hello", "  world");
            EnterModeWithSelection(_textView.TextBuffer.GetSpan(0, 2));
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
            CreateBuffer("  hello", "  world");
            EnterModeWithSelection(_textView.GetLine(0).Extent);
            Assert.AreEqual(ModeKind.VisualCharacter, _buffer.ModeKind);
            _textView.Selection.Select(_textView.GetLine(1).Extent, false);
            _buffer.Process(KeyInputUtil.CharToKeyInput('y'));
            _context.RunAll();
            Assert.AreEqual("  world", _buffer.RegisterMap.DefaultRegister.StringValue);
        }

        [Test]
        [Description("Make sure we reset the span we need")]
        public void SelectionChange4()
        {
            CreateBuffer("  hello", "  world");
            EnterModeWithSelection(_textView.GetLine(0).Extent);
            Assert.AreEqual(ModeKind.VisualCharacter, _buffer.ModeKind);
            _textView.SelectAndUpdateCaret(new SnapshotSpan(_textView.GetLine(1).Start, 2));
            _context.RunAll();
            _buffer.Process(KeyInputUtil.CharToKeyInput('l'));
            _buffer.Process(KeyInputUtil.CharToKeyInput('y'));
            Assert.AreEqual("  wo", _buffer.RegisterMap.DefaultRegister.StringValue);
        }

        [Test]
        [Description("Enter Visual Line Mode")]
        public void EnterVisualLine1()
        {
            CreateBuffer("hello", "world");
            _buffer.Process(KeyNotationUtil.StringToKeyInput("<S-v>"));
            Assert.AreEqual(ModeKind.VisualLine, _buffer.ModeKind);
        }

        [Test]
        public void SwitchToCommandModeShouldPreserveSelection()
        {
            CreateBuffer("dog", "pig", "chicken");
            EnterModeWithSelection(_textView.GetLineSpan(0, 1));
            _buffer.Process(':');
            Assert.IsFalse(_textView.Selection.IsEmpty);
        }


    }
}
