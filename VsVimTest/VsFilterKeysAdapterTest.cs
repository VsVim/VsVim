using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Moq;
using NUnit.Framework;
using Vim;
using VsVim;

namespace VsVim.UnitTest
{
    [TestFixture]
    public class VsFilterKeysAdapterTest
    {
        private MockRepository _factory;
        private Mock<IVsFilterKeys> _filterKeys;
        private Mock<IVsCodeWindow> _codeWindow;
        private Mock<IVsTextLines> _textLines;
        private Mock<IEditorOptions> _editorOptions;
        private Mock<IVimBuffer> _buffer;
        private VsFilterKeysAdapter _adapter;
        private IVsFilterKeys _adapterInterface;

        [SetUp]
        public void Setup()
        {
            _factory = new MockRepository(MockBehavior.Loose);
            _filterKeys = _factory.Create<IVsFilterKeys>();
            _codeWindow = _factory.Create<IVsCodeWindow>();
            _textLines = _factory.Create<IVsTextLines>();
            _editorOptions = _factory.Create<IEditorOptions>();
            _buffer = _factory.Create<IVimBuffer>();
            _adapter = new VsFilterKeysAdapter(
                _filterKeys.Object,
                _codeWindow.Object,
                _textLines.Object,
                _editorOptions.Object,
                _buffer.Object);
            _adapterInterface = _adapter;
        }

        [Test]
        public void IsReadOnly1()
        {
            _editorOptions
                .Setup(x => x.GetOptionValue<bool>(DefaultTextViewOptions.ViewProhibitUserInputId))
                .Throws(new ArgumentException())
                .Verifiable();
            Assert.IsFalse(_adapter.IsReadOnly());
            _factory.Verify();
        }

        [Test]
        public void IsReadOnly2()
        {
            _editorOptions
                .Setup(x => x.GetOptionValue<bool>(DefaultTextViewOptions.ViewProhibitUserInputId))
                .Throws(new InvalidOperationException())
                .Verifiable();
            Assert.IsFalse(_adapter.IsReadOnly());
            _factory.Verify();
        }

        [Test]
        public void IsReadOnly3()
        {
            _editorOptions
                .Setup(x => x.GetOptionValue<bool>(DefaultTextViewOptions.ViewProhibitUserInputId))
                .Returns(true)
                .Verifiable();
            Assert.IsTrue(_adapter.IsReadOnly());
            _factory.Verify();
        }

        [Test]
        public void IsReadOnly4()
        {
            _editorOptions
                .Setup(x => x.GetOptionValue<bool>(DefaultTextViewOptions.ViewProhibitUserInputId))
                .Returns(false)
                .Verifiable();
            var flags = 0u;
            _textLines
                .Setup(x => x.GetStateFlags(out flags))
                .Returns(VSConstants.E_FAIL);
            Assert.IsFalse(_adapter.IsReadOnly());
            _factory.Verify();
        }

        [Test]
        public void IsReadOnly5()
        {
            _editorOptions
                .Setup(x => x.GetOptionValue<bool>(DefaultTextViewOptions.ViewProhibitUserInputId))
                .Returns(false)
                .Verifiable();
            var flags = 0u;
            _textLines
                .Setup(x => x.GetStateFlags(out flags))
                .Returns(VSConstants.S_OK);
            Assert.IsFalse(_adapter.IsReadOnly());
            _factory.Verify();
        }

        [Test]
        public void IsReadOnly6()
        {
            _editorOptions
                .Setup(x => x.GetOptionValue<bool>(DefaultTextViewOptions.ViewProhibitUserInputId))
                .Returns(false)
                .Verifiable();
            var flags = (uint)BUFFERSTATEFLAGS.BSF_USER_READONLY;
            _textLines
                .Setup(x => x.GetStateFlags(out flags))
                .Returns(VSConstants.S_OK);
            Assert.IsTrue(_adapter.IsReadOnly());
            _factory.Verify();
        }

        [Test]
        public void IsEditCommand1()
        {
            Assert.IsTrue(_adapter.IsEditCommand(VSConstants.VSStd2K, (uint)VSConstants.VSStd2KCmdID.TYPECHAR));
            Assert.IsTrue(_adapter.IsEditCommand(VSConstants.VSStd2K, (uint)VSConstants.VSStd2KCmdID.RETURN));
            Assert.IsTrue(_adapter.IsEditCommand(VSConstants.VSStd2K, (uint)VSConstants.VSStd2KCmdID.BACKSPACE));
        }


    }
}
