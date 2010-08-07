using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Moq;
using Microsoft.VisualStudio.TextManager.Interop;

namespace VsVimTest
{
    internal static class VsVimTestExtensions
    {
        internal static void MakeSplit(
            this Mock<IVsCodeWindow> mock,
            MockFactory factory = null)
        {
            factory = factory ?? new MockFactory(MockBehavior.Loose);
            var primary = factory.Create<IVsTextView>().Object;
            var secondary = factory.Create<IVsTextView>().Object;
            mock.Setup(x => x.GetPrimaryView(out primary)).Verifiable();
            mock.Setup(x => x.GetSecondaryView(out secondary)).Verifiable();
        }
    }
}
