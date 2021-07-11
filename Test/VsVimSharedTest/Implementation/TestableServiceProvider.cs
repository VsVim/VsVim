using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Windows.Threading;
using Vim.EditorHost;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Moq;
using Vim;
using Vim.Extensions;
using Vim.UI.Wpf;
using Vim.UnitTest;
using Vim.VisualStudio.UnitTest.Mock;
using Xunit;
using System.Threading;
using EnvDTE;
using Thread = System.Threading.Thread;

namespace Vim.VisualStudio.UnitTest
{
    [Export(typeof(SVsServiceProvider))]
    [Export(typeof(TestableServiceProvider))]
    internal sealed class TestableServiceProvider : SVsServiceProvider
    {
        /// <summary>
        /// This smooths out the nonsense type equality problems that come with having NoPia
        /// enabled on only some of the assemblies.  
        /// </summary>
        private sealed class TypeEqualityComparer : IEqualityComparer<Type>
        {
            public bool Equals(Type x, Type y)
            {
                return
                    x.FullName == y.FullName &&
                    x.GUID == y.GUID;
            }

            public int GetHashCode(Type obj)
            {
                return obj != null ? obj.GUID.GetHashCode() : 0;
            }
        }

        private MockRepository _factory = new MockRepository(MockBehavior.Loose);
        private readonly Dictionary<Type, object> _serviceMap = new Dictionary<Type, object>(new TypeEqualityComparer());

        public TestableServiceProvider()
        {
            _serviceMap[typeof(SVsShell)] = _factory.Create<IVsShell>().Object;
            _serviceMap[typeof(SVsTextManager)] = _factory.Create<IVsTextManager>().Object;
            _serviceMap[typeof(SVsRunningDocumentTable)] = _factory.Create<IVsRunningDocumentTable>().Object;
            _serviceMap[typeof(SVsUIShell)] = MockObjectFactory.CreateVsUIShell4(MockBehavior.Strict).Object;
            _serviceMap[typeof(SVsShellMonitorSelection)] = _factory.Create<IVsMonitorSelection>().Object;
            _serviceMap[typeof(IVsExtensibility)] = _factory.Create<IVsExtensibility>().Object;
            var dte = MockObjectFactory.CreateDteWithCommands();
            _serviceMap[typeof(_DTE)] = dte.Object;
            _serviceMap[typeof(SVsStatusbar)] = _factory.Create<IVsStatusbar>().Object;
            _serviceMap[typeof(SDTE)] = dte.Object;
            _serviceMap[typeof(SVsSettingsManager)] = CreateSettingsManager().Object;
            _serviceMap[typeof(SVsFindManager)] = _factory.Create<IVsFindManager>().Object;
        }

        private Mock<IVsSettingsManager> CreateSettingsManager()
        {
            var settingsManager = _factory.Create<IVsSettingsManager>();

            var writableSettingsStore = _factory.Create<IVsWritableSettingsStore>();
            var local = writableSettingsStore.Object;
            settingsManager.Setup(x => x.GetWritableSettingsStore(It.IsAny<uint>(), out local)).Returns(VSConstants.S_OK);

            return settingsManager;
        }

        public object GetService(Type serviceType)
        {
            return _serviceMap[serviceType];
        }
    }
}
