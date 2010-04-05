using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Moq;
using VsVim;
using System.Windows.Input;
using Microsoft.VisualStudio.Utilities;
using System.Windows;
using EnvDTE;
using System.Runtime.InteropServices;

namespace VsVimTest
{
    [TestFixture]
    public class ExtensionsTest
    {
        #region KeyBindings

        [Test, Description("Bindings as an array")]
        public void GetKeyBindings1()
        {
            var com = new Mock<EnvDTE.Command>();
            com.Setup(x => x.Bindings).Returns(new object[] { "::f" });
            com.Setup(x => x.Name).Returns("name");
            var list = Extensions.GetCommandKeyBindings(com.Object).ToList();
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual('f', list[0].KeyBinding.FirstKeyInput.Char);
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
            Assert.AreEqual('f', list[0].KeyBinding.FirstKeyInput.Char);
            Assert.AreEqual("foo", list[0].KeyBinding.Scope);
            Assert.AreEqual('b', list[1].KeyBinding.FirstKeyInput.Char);
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
            Assert.AreEqual('f', list[0].KeyBinding.FirstKeyInput.Char);
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
            mock.SetupSet(x => x.Bindings).Verifiable();
            mock.Object.SafeResetBindings();
            mock.Verify();
        }

        [Test, Description("Some Command implementations return E_FAIL, just ignore it")]
        public void SafeResetKeyBindings2()
        {
            var mock = new Mock<Command>(MockBehavior.Strict);
            mock.SetupSet(x => x.Bindings).Throws(new COMException()).Verifiable();
            mock.Object.SafeResetBindings();
            mock.Verify();
        }

        #endregion
    }
}
