using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Vim;
using VimCoreTest.Utils;
using Vim.Modes.Visual;
using Microsoft.VisualStudio.Text;
using System.Windows.Input;

namespace VimCoreTest
{
    [TestFixture]
    public class MouseProcessorTests
    {
        private IWpfTextView _textView;
        private Mock<IVimBuffer> _buffer;
        private Mock<IMouseDevice> _mouseDevice;
        private Mock<IVisualMode> _visualMode;
        private MouseProcessor _processor;

        private void Create(params string[] lines)
        {
            _textView = EditorUtil.CreateView(lines);
            _visualMode = new Mock<IVisualMode>(MockBehavior.Strict);
            _buffer = new Mock<IVimBuffer>(MockBehavior.Strict);
            _buffer.SetupGet(x => x.TextView).Returns(_textView);
            _mouseDevice = new Mock<IMouseDevice>(MockBehavior.Strict);
            _processor = new MouseProcessor(_buffer.Object, _mouseDevice.Object);
        }

        [TearDown]
        public void TearDown()
        {
            _textView = null;
        }

        [Test, Description("If we are already in Visual Mode then it's their responsibility")]
        public void MouseSelect1()
        {
            Create("foo bar");
            _buffer.SetupGet(x => x.ModeKind).Returns(ModeKind.VisualCharacter).Verifiable();
            _textView.Selection.Select(new SnapshotSpan(_textView.TextSnapshot, 0,3), false);
            _buffer.Verify();
        }

        

    }
}
