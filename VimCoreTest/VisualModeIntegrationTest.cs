using System.Threading;
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

        public void CreateBuffer(ModeKind kind, params string[] lines)
        {
            _context = new TestableSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(_context);
            var tuple = EditorUtil.CreateViewAndOperations(lines);
            _textView = tuple.Item1;
            var service = EditorUtil.FactoryService;
            _buffer = service.vim.CreateBuffer(_textView);
            _buffer.SwitchMode(kind, ModeArgument.None);
            _context.RunAll();
        }

        [Test]
        public void Repeat1()
        {
            CreateBuffer(ModeKind.VisualLine, "dog again", "cat again", "chicken");
            var span = _textView.GetLineSpanIncludingLineBreak(0, 1);
            _textView.Selection.Select(span, false);
            _buffer.Settings.GlobalSettings.ShiftWidth = 2;
            _buffer.ProcessAsString(">.");
            Assert.AreEqual("    dog again", _textView.GetLine(0).GetText());
        }

        [Test]
        public void Repeat2()
        {
            CreateBuffer(ModeKind.VisualLine, "dog again", "cat again", "chicken");
            var span = _textView.GetLineSpanIncludingLineBreak(0, 1);
            _textView.Selection.Select(span, false);
            _buffer.Settings.GlobalSettings.ShiftWidth = 2;
            _buffer.ProcessAsString(">..");
            Assert.AreEqual("      dog again", _textView.GetLine(0).GetText());
        }
    }
}
