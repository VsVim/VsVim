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

namespace VimCoreTest
{
    [TestFixture]
    public class MouseProcessorTests
    {
        private IWpfTextView _textView;
        private Mock<IVimBuffer> _buffer;
        private Mock<IMouseDevice> _mouseDevice;
        private MouseProcessor _processor;

        private void Create(params string[] lines)
        {
            _textView = EditorUtil.CreateView(lines);
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

        [Test]
        public void MouseSelect1()
        {
            Create("foo bar");
            var count = 0;
            var visualMode = new Mock<IVisualMode>(MockBehavior.Strict);
            visualMode.Setup(x => x.BeginExplicitMove()).Callback(() => { count++; });
            _buffer.SetupGet(x => x.ModeKind).Returns(ModeKind.VisualCharacter).Verifiable();
            _buffer.SetupGet(x => x.Mode).Returns(visualMode.Object).Verifiable();
            _textView.Selection.Select(new SnapshotSpan(_textView.TextSnapshot, 0,3), false);
            Assert.AreEqual(1, count);
            _buffer.Verify();
        }

    }
}
