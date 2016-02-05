using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vim.VisualStudio.Implementation.VisualAssist;
using Xunit;

namespace Vim.VisualStudio.UnitTest
{
    public sealed class VisualAssistExtensionAdapterTest
    {
        private readonly Mock<IVisualAssistUtil> _visualAssistUtil;
        private readonly VisualAssistExtensionAdapter _adapterRaw;
        private readonly IExtensionAdapter _adapter;

        public VisualAssistExtensionAdapterTest()
        {
            _visualAssistUtil = new Mock<IVisualAssistUtil>(MockBehavior.Strict);
            _visualAssistUtil.SetupGet(x => x.IsInstalled).Returns(true);

            _adapterRaw = new VisualAssistExtensionAdapter(_visualAssistUtil.Object);
            _adapter = _adapterRaw;
        }

        [Fact]
        public void CorrectCommands()
        {
            Assert.Equal(true, _adapter.ShouldKeepSelectionAfterHostCommand("VAssistX.SmartSelectExtend", ""));
        }

        [Fact]
        public void IncorrectCommands()
        {
            Assert.Null(_adapter.ShouldKeepSelectionAfterHostCommand("VAssistX.SmartSelectExtendEx", ""));
        }

        [Fact]
        public void CorrectCommandsNotInstalled()
        {
            _visualAssistUtil.SetupGet(x => x.IsInstalled).Returns(false);
            Assert.Null(_adapter.ShouldKeepSelectionAfterHostCommand("VAssistX.SmartSelectExtend", ""));
        }
    }
}
