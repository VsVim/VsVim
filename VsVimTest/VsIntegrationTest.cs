using Microsoft.VisualStudio.Text.Editor;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;
using VsVim.Implementation;
using VsVim.UnitTest.Utils;

namespace VsVim.UnitTest
{
    /// <summary>
    /// Used to simulate integration scenarios with Visual Studio
    /// </summary>
    [TestFixture]
    public sealed class VsIntegrationTest
    {
        private VisualStudioSimulation _simulation;
        private ITextView _textView;
        private IVimBuffer _buffer;
        private IVimBufferCoordinator _bufferCoordinator;

        /// <summary>
        /// Create a Visual Studio simulation with the specified set of lines
        /// </summary>
        private void Create(params string[] lines)
        {
            _textView = EditorUtil.CreateTextView(lines);
            _buffer = EditorUtil.FactoryService.Vim.CreateBuffer(_textView);
            _bufferCoordinator = new VimBufferCoordinator(_buffer);
            _simulation = new VisualStudioSimulation(_bufferCoordinator);
        }

        /// <summary>
        /// Simple sanity check to ensure that our simulation is working properly
        /// </summary>
        [Test]
        public void Insert_SanityCheck()
        {
            Create("hello world");
            _textView.MoveCaretTo(0);
            _buffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            _simulation.Run('x');
            Assert.AreEqual("xhello world", _textView.GetLine(0).GetText());
        }
    }
}
