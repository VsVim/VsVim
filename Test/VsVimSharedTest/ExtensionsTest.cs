using System;
using System.Linq;
using System.Runtime.InteropServices;
using EnvDTE;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Moq;
using Xunit;
using Vim.VisualStudio.UnitTest.Mock;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using DteCommand = EnvDTE.Command;

namespace Vim.VisualStudio.UnitTest
{
    public abstract class ExtensionsTest
    {
        private readonly MockRepository _factory;

        public ExtensionsTest()
        {
            _factory = new MockRepository(MockBehavior.Loose);
        }

        public sealed class KeyBindingTest
        {
            /// <summary>
            /// Bindings as an array
            /// </summary>
            [Fact]
            public void GetKeyBindings1()
            {
                var com = MockObjectFactory.CreateCommand(0, "name", "::f");
                var list = Extensions.GetCommandKeyBindings(com.Object).ToList();
                Assert.Equal(1, list.Count);
                Assert.Equal('f', list[0].KeyBinding.FirstKeyStroke.KeyInput.Char);
                Assert.Equal("name", list[0].Name);
            }

            [Fact]
            public void GetKeyBindings2()
            {
                var com = MockObjectFactory.CreateCommand(0, "name", "foo::f", "bar::b");
                var list = Extensions.GetCommandKeyBindings(com.Object).ToList();
                Assert.Equal(2, list.Count);
                Assert.Equal('f', list[0].KeyBinding.FirstKeyStroke.KeyInput.Char);
                Assert.Equal("foo", list[0].KeyBinding.Scope);
                Assert.Equal('b', list[1].KeyBinding.FirstKeyStroke.KeyInput.Char);
                Assert.Equal("bar", list[1].KeyBinding.Scope);
            }

            /// <summary>
            /// Bindings as a string which is what the documentation indicates it should be
            /// </summary>
            [Fact]
            public void GetKeyBindings3()
            {
                var com = MockObjectFactory.CreateCommand(0, "name", "::f");
                var list = Extensions.GetCommandKeyBindings(com.Object).ToList();
                Assert.Equal(1, list.Count);
                Assert.Equal('f', list[0].KeyBinding.FirstKeyStroke.KeyInput.Char);
                Assert.Equal(String.Empty, list[0].KeyBinding.Scope);
            }

            /// <summary>
            /// A bad key binding should just return as an empty result set
            /// </summary>
            [Fact]
            public void GetKeyBindings4()
            {
                var com = MockObjectFactory.CreateCommand(0, "name", "::notavalidkey");
                var e = Extensions.GetCommandKeyBindings(com.Object).ToList();
                Assert.Equal(0, e.Count);
            }
        }

        public sealed class CommandTest : ExtensionsTest
        {
            /// <summary>
            /// Make sure that we can handle the case where the Bindings call throws
            /// </summary>
            [Fact]
            public void GetBindingsThrows()
            {
                var mock = _factory.Create<DteCommand>();
                mock.SetupGet(x => x.Bindings).Throws(new OutOfMemoryException());
                var all = mock.Object.GetBindings();
                Assert.Equal(0, all.Count());
            }

            [Fact]
            public void SafeResetKeyBindings()
            {
                var mock = new Mock<DteCommand>(MockBehavior.Strict);
                mock.SetupSet(x => x.Bindings = It.IsAny<object>()).Verifiable();
                mock.Object.SafeResetBindings();
                mock.Verify();
            }

            /// <summary>
            /// Some Command implementations return E_FAIL, just ignore it
            /// </summary>
            [Fact]
            public void SafeResetKeyBindings2()
            {
                var mock = new Mock<DteCommand>(MockBehavior.Strict);
                mock.SetupSet(x => x.Bindings = It.IsAny<object>()).Throws(new COMException()).Verifiable();
                mock.Object.SafeResetBindings();
                mock.Verify();
            }
        }

        public sealed class VsCodeWindowTest : ExtensionsTest
        {
            [Fact]
            public void IsSplit1()
            {
                var codeWindow = _factory.Create<IVsCodeWindow>();
                var adapter = _factory.Create<IVsAdapter>();
                adapter.SetupGet(x => x.EditorAdapter).Returns(_factory.Create<IVsEditorAdaptersFactoryService>().Object);
                codeWindow.MakeSplit(adapter, factory: _factory);
                Assert.True(codeWindow.Object.IsSplit());
                _factory.Verify();
            }
        }

        public sealed class GetAdornmentLayerNoThrowTest : ExtensionsTest
        {
            private static readonly object LayerKey = new object();
            private static readonly string LayerName = "MyAdornmentLayer";
            private readonly Mock<IWpfTextView> _wpfTextView;
            private readonly PropertyCollection _propertyCollection;

            public GetAdornmentLayerNoThrowTest()
            {
                _propertyCollection = new PropertyCollection();
                _wpfTextView = _factory.Create<IWpfTextView>();
                _wpfTextView.SetupGet(x => x.Properties).Returns(_propertyCollection);
            }

            [Fact]
            public void FirstTimeLayerNotPresent()
            {
                _wpfTextView.Setup(x => x.GetAdornmentLayer(LayerName)).Throws(new Exception());
                Assert.Null(_wpfTextView.Object.GetAdornmentLayerNoThrow(LayerName, LayerKey));
            }

            /// <summary>
            /// The second time around using the same name and key shouldn't call the GetLayer 
            /// method.  No need to keep throwing exceptions and catching them.  It just needlessly
            /// affects perf and kills the debugging experience 
            /// </summary>
            [Fact]
            public void SecondTimeLayerNotPresent()
            {
                _wpfTextView.Setup(x => x.GetAdornmentLayer(LayerName)).Throws(new Exception());
                Assert.Null(_wpfTextView.Object.GetAdornmentLayerNoThrow(LayerName, LayerKey));
                var calledAgain = false;
                _wpfTextView.Setup(x => x.GetAdornmentLayer(LayerName)).Callback(() => { calledAgain = true; }).Throws(new Exception());
                Assert.Null(_wpfTextView.Object.GetAdornmentLayerNoThrow(LayerName, LayerKey));
                Assert.False(calledAgain);
            }

            [Fact]
            public void HasTheLayer()
            {
                var layer = _factory.Create<IAdornmentLayer>().Object;
                _wpfTextView.Setup(x => x.GetAdornmentLayer(LayerName)).Returns(layer);
                Assert.Same(layer, _wpfTextView.Object.GetAdornmentLayerNoThrow(LayerName, LayerKey));
            }
        }
    }
}
