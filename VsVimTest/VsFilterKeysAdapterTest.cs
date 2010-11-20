using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Moq;
using NUnit.Framework;
using Vim;

namespace VsVim.UnitTest
{
    [TestFixture]
    public class VsFilterKeysAdapterTest
    {
        private MockRepository _factory;
        private Mock<IVsAdapter> _vsAdapter;
        private Mock<IVsFilterKeys> _filterKeys;
        private Mock<IVsCodeWindow> _codeWindow;
        private Mock<IVimBuffer> _buffer;
        private VsFilterKeysAdapter _adapter;
        private IVsFilterKeys _adapterInterface;

        [SetUp]
        public void Setup()
        {
            _factory = new MockRepository(MockBehavior.Loose);
            _vsAdapter = _factory.Create<IVsAdapter>();
            _filterKeys = _factory.Create<IVsFilterKeys>();
            _codeWindow = _factory.Create<IVsCodeWindow>();
            _buffer = _factory.Create<IVimBuffer>();
            _adapter = new VsFilterKeysAdapter(
                _filterKeys.Object,
                _codeWindow.Object,
                _vsAdapter.Object,
                _buffer.Object);
            _adapterInterface = _adapter;
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
