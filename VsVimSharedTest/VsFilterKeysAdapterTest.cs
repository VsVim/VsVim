using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Moq;
using Xunit;
using Vim;

namespace VsVim.UnitTest
{
    public class VsFilterKeysAdapterTest
    {
        private readonly MockRepository _factory;
        private readonly Mock<IVsAdapter> _vsAdapter;
        private readonly Mock<IVsFilterKeys> _filterKeys;
        private readonly Mock<IVsCodeWindow> _codeWindow;
        private readonly Mock<IVimBuffer> _buffer;
        private readonly VsFilterKeysAdapter _adapter;
        private readonly IVsFilterKeys _adapterInterface;

        public VsFilterKeysAdapterTest()
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

        [Fact]
        public void IsEditCommand1()
        {
            Assert.True(_adapter.IsEditCommand(VSConstants.VSStd2K, (uint)VSConstants.VSStd2KCmdID.TYPECHAR));
            Assert.True(_adapter.IsEditCommand(VSConstants.VSStd2K, (uint)VSConstants.VSStd2KCmdID.RETURN));
            Assert.True(_adapter.IsEditCommand(VSConstants.VSStd2K, (uint)VSConstants.VSStd2KCmdID.BACKSPACE));
        }


    }
}
