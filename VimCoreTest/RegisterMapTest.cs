using System;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;
using Vim.UnitTest.Mock;

namespace VimCore.Test
{
    [TestFixture]
    public class RegisterMapTest
    {
        private MockRepository _factory;
        private Mock<IClipboardDevice> _clipboard;
        private RegisterMap _rawMap;
        private IRegisterMap _map;

        [SetUp]
        public void Setup()
        {
            _factory = new MockRepository(MockBehavior.Strict);
            _clipboard = MockObjectFactory.CreateClipboardDevice(_factory);
            _rawMap = new RegisterMap(_clipboard.Object);
            _map = _rawMap;
        }

        [Test]
        [Description("The + register should use the clipboard backing")]
        public void PlusRegister1()
        {
            _clipboard.SetupGet(x => x.Text).Returns("foo").Verifiable();
            Assert.AreEqual("foo", _map.GetRegister('+').StringValue);
            _factory.Verify();
        }

        [Test]
        [Description("The + register should use the clipboard backing")]
        public void PlusRegister2()
        {
            _clipboard.SetupSet(x => x.Text = "bar").Verifiable();
            _map.GetRegister('+').Value = RegisterValue.CreateFromText("bar");
            _factory.Verify();
        }

        [Test]
        [Description("The * register should use the clipboard backing")]
        public void StarRegister1()
        {
            _clipboard.SetupGet(x => x.Text).Returns("foo").Verifiable();
            Assert.AreEqual("foo", _map.GetRegister('*').StringValue);
            _factory.Verify();
        }

        [Test]
        [Description("The * register should use the clipboard backing")]
        public void StarRegister2()
        {
            _clipboard.SetupSet(x => x.Text = "bar").Verifiable();
            _map.GetRegister('*').Value = RegisterValue.CreateFromText("bar");
            _factory.Verify();
        }

        [Test]
        [Description("Named registers shouldn't use the clipboard")]
        public void NamedRegister1()
        {
            _clipboard.SetupGet(x => x.Text).Throws(new Exception());
            Assert.AreEqual("", _map.GetRegister('a').StringValue);
        }
    }
}
