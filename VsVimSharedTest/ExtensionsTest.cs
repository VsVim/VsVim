using System;
using System.Linq;
using System.Runtime.InteropServices;
using EnvDTE;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Moq;
using Xunit;

namespace VsVim.UnitTest
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
                var com = new Mock<EnvDTE.Command>();
                com.Setup(x => x.Bindings).Returns(new object[] { "::f" });
                com.Setup(x => x.Name).Returns("name");
                var list = Extensions.GetCommandKeyBindings(com.Object).ToList();
                Assert.Equal(1, list.Count);
                Assert.Equal('f', list[0].KeyBinding.FirstKeyStroke.KeyInput.Char);
                Assert.Equal("name", list[0].Name);
            }

            [Fact]
            public void GetKeyBindings2()
            {
                var com = new Mock<EnvDTE.Command>();
                com.Setup(x => x.Bindings).Returns(new object[] { "foo::f", "bar::b" });
                com.Setup(x => x.Name).Returns("name");
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
                var com = new Mock<EnvDTE.Command>();
                com.Setup(x => x.Bindings).Returns("::f");
                com.Setup(x => x.Name).Returns("name");
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
                var com = new Mock<EnvDTE.Command>();
                com.Setup(x => x.Bindings).Returns(new object[] { "::notavalidkey" });
                com.Setup(x => x.Name).Returns("name");
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
                var mock = _factory.Create<Command>();
                mock.SetupGet(x => x.Bindings).Throws(new OutOfMemoryException());
                var all = mock.Object.GetBindings();
                Assert.Equal(0, all.Count());
            }

            [Fact]
            public void SafeResetKeyBindings()
            {
                var mock = new Mock<Command>(MockBehavior.Strict);
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
                var mock = new Mock<Command>(MockBehavior.Strict);
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
    }
}
