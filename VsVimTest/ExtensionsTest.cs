using System;
using System.Linq;
using System.Runtime.InteropServices;
using EnvDTE;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using Moq;
using NUnit.Framework;
using Vim.Extensions;
using Vim.UnitTest.Mock;

namespace VsVim.UnitTest
{
    [TestFixture]
    public class ExtensionsTest
    {
        private MockRepository _factory;

        [SetUp]
        public void Setup()
        {
            _factory = new MockRepository(MockBehavior.Loose);
        }

        #region KeyBindings

        [Test, Description("Bindings as an array")]
        public void GetKeyBindings1()
        {
            var com = new Mock<EnvDTE.Command>();
            com.Setup(x => x.Bindings).Returns(new object[] { "::f" });
            com.Setup(x => x.Name).Returns("name");
            var list = Extensions.GetCommandKeyBindings(com.Object).ToList();
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual('f', list[0].KeyBinding.FirstKeyStroke.KeyInput.Char);
            Assert.AreEqual("name", list[0].Name);
        }

        [Test]
        public void GetKeyBindings2()
        {
            var com = new Mock<EnvDTE.Command>();
            com.Setup(x => x.Bindings).Returns(new object[] { "foo::f", "bar::b" });
            com.Setup(x => x.Name).Returns("name");
            var list = Extensions.GetCommandKeyBindings(com.Object).ToList();
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual('f', list[0].KeyBinding.FirstKeyStroke.KeyInput.Char);
            Assert.AreEqual("foo", list[0].KeyBinding.Scope);
            Assert.AreEqual('b', list[1].KeyBinding.FirstKeyStroke.KeyInput.Char);
            Assert.AreEqual("bar", list[1].KeyBinding.Scope);
        }

        [Test, Description("Bindings as a string which is what the documentation indicates it should be")]
        public void GetKeyBindings3()
        {
            var com = new Mock<EnvDTE.Command>();
            com.Setup(x => x.Bindings).Returns("::f");
            com.Setup(x => x.Name).Returns("name");
            var list = Extensions.GetCommandKeyBindings(com.Object).ToList();
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual('f', list[0].KeyBinding.FirstKeyStroke.KeyInput.Char);
            Assert.AreEqual(String.Empty, list[0].KeyBinding.Scope);
        }

        [Test, Description("A bad key binding should just return as an empty result set")]
        public void GetKeyBindings4()
        {
            var com = new Mock<EnvDTE.Command>();
            com.Setup(x => x.Bindings).Returns(new object[] { "::notavalidkey" });
            com.Setup(x => x.Name).Returns("name");
            var e = Extensions.GetCommandKeyBindings(com.Object).ToList();
            Assert.AreEqual(0, e.Count);
        }

        #endregion

        #region PropertyCollection

        [Test]
        public void AddTypedProperty1()
        {
            var col = new PropertyCollection();
            col.AddTypedProperty("foo");
            Assert.AreEqual(1, col.PropertyList.Count);
            Assert.IsTrue(col.ContainsProperty(typeof(string)));
        }

        [Test]
        public void TryGetTypedProperty1()
        {
            var col = new PropertyCollection();
            col.AddTypedProperty("foo");
            var opt = col.TryGetTypedProperty<string>();
            Assert.IsTrue(opt.IsSome());
            Assert.AreEqual("foo", opt.Value);
        }

        [Test]
        public void TryGetTypedProperty2()
        {
            var col = new PropertyCollection();
            var opt = col.TryGetTypedProperty<string>();
            Assert.IsFalse(opt.IsSome());
        }

        #endregion

        #region Command

        [Test]
        public void SafeResetKeyBindings()
        {
            var mock = new Mock<Command>(MockBehavior.Strict);
            mock.SetupSet(x => x.Bindings = It.IsAny<object>()).Verifiable();
            mock.Object.SafeResetBindings();
            mock.Verify();
        }

        [Test, Description("Some Command implementations return E_FAIL, just ignore it")]
        public void SafeResetKeyBindings2()
        {
            var mock = new Mock<Command>(MockBehavior.Strict);
            mock.SetupSet(x => x.Bindings = It.IsAny<object>()).Throws(new COMException()).Verifiable();
            mock.Object.SafeResetBindings();
            mock.Verify();
        }

        #endregion

        [Test]
        public void IsSplit1()
        {
            var codeWindow = _factory.Create<IVsCodeWindow>();
            var adapter = _factory.Create<IVsAdapter>();
            adapter.SetupGet(x => x.EditorAdapter).Returns(_factory.Create<IVsEditorAdaptersFactoryService>().Object);
            codeWindow.MakeSplit(adapter, factory: _factory);
            Assert.IsTrue(codeWindow.Object.IsSplit());
            _factory.Verify();
        }
    }
}
