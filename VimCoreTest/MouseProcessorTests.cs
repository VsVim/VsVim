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
using System.Windows.Threading;

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
        public void SelectionEvent1()
        {
            Create("foo bar");
            _buffer.SetupGet(x => x.ModeKind).Returns(ModeKind.VisualCharacter).Verifiable();
            _textView.Selection.Select(new SnapshotSpan(_textView.TextSnapshot, 0,3), false);
            _buffer.Verify();
        }

        [Test]
        public void SelectionEvent2()
        {
            Create("foo bar");
            _buffer.Setup(x => x.ModeKind).Returns(ModeKind.Normal).Verifiable();
            _buffer.Setup(x => x.SwitchMode(ModeKind.VisualCharacter)).Returns(_visualMode.Object).Verifiable();
            _mouseDevice.SetupGet(x => x.LeftButtonState).Returns(MouseButtonState.Pressed).Verifiable();
            _textView.Selection.Select(new SnapshotSpan(_textView.TextSnapshot, 0, 3), false);
            Assert.IsTrue(_processor.IsSelectionChanging);
            _buffer.Verify();
            _mouseDevice.Verify();
        }

        [Test, Description("If the selection changes and it's not the mouse then we're not in a selection")]
        public void SelectionEvent3()
        {
            Create("foo bar");
            _buffer.Setup(x => x.ModeKind).Returns(ModeKind.Normal).Verifiable();
            _buffer.Setup(x => x.SwitchMode(ModeKind.VisualCharacter)).Returns(_visualMode.Object).Verifiable();
            _mouseDevice.SetupGet(x => x.LeftButtonState).Returns(MouseButtonState.Released).Verifiable();
            _textView.Selection.Select(new SnapshotSpan(_textView.TextSnapshot, 0, 3), false);
            Assert.IsFalse(_processor.IsSelectionChanging);
            _buffer.Verify();
        }

        [Test, Description("Only care about the left button")]
        public void MouseButtonUp1()
        {
            Create("foo bar");
            _processor.PostprocessMouseLeftButtonUp(new MouseButtonEventArgs(InputManager.Current.PrimaryMouseDevice, 0, MouseButton.Right));
        }

        [Test, Description("Only important if we're in visual mode")]
        public void MouseButtonUp2()
        {
            Create("foo bar");
            _buffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Insert).Verifiable();
            _processor.PostprocessMouseLeftButtonUp(new MouseButtonEventArgs(InputManager.Current.PrimaryMouseDevice, 0, MouseButton.Left));
            _buffer.Verify();
        }

        [Test, Description("End of a selection should do nothing")]
        public void MouseButtonUp3()
        {
            Create("foo bar");
            _processor.IsSelectionChanging = true;
            _processor.PostprocessMouseLeftButtonUp(new MouseButtonEventArgs(InputManager.Current.PrimaryMouseDevice, 0, MouseButton.Left));
            Assert.IsFalse(_processor.IsSelectionChanging);
        }

        [Test, Description("End of a selection should do nothing")]
        public void MouseButtonUp4()
        {
            Create("foo bar");
            _processor.IsSelectionChanging = true;
            _processor.PostprocessMouseLeftButtonUp(new MouseButtonEventArgs(InputManager.Current.PrimaryMouseDevice, 0, MouseButton.Left));
            Assert.IsFalse(_processor.IsSelectionChanging);
        }

        [Test]
        public void MouseButtonUp5()
        {
            Create("foo bar");
            var mode = new Mock<IMode>(MockBehavior.Strict);
            _buffer.Setup(x => x.ModeKind).Returns(ModeKind.VisualCharacter).Verifiable();
            _processor.IsSelectionChanging = false;
            _processor.PostprocessMouseLeftButtonUp(new MouseButtonEventArgs(InputManager.Current.PrimaryMouseDevice, 0, MouseButton.Left));
            _buffer.Verify();
            _buffer.Setup(x => x.SwitchMode(ModeKind.Normal)).Returns(mode.Object).Verifiable();
            Dispatcher.CurrentDispatcher.DoEvents();
            _buffer.Verify();
        }

    }
}
