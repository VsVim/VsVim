using System;
using Moq;
using Vim.EditorHost;
using Vim.UnitTest.Mock;
using Xunit;

namespace Vim.UnitTest
{
    public abstract class RegisterMapTest
    {
        protected MockRepository _factory;
        protected Mock<IClipboardDevice> _clipboard;
        protected IRegisterMap _map;
        internal RegisterMap _rawMap;
        protected string _fileName;

        protected static void AssertRegister(Register reg, string value, OperationKind kind)
        {
            Assert.Equal(value, reg.StringValue);
            Assert.Equal(kind, reg.RegisterValue.OperationKind);
        }

        protected void AssertRegister(RegisterName name, string value, OperationKind kind)
        {
            AssertRegister(_map.GetRegister(name), value, kind);
        }

        protected void AssertRegister(char name, string value, OperationKind kind)
        {
            AssertRegister(_map.GetRegister(name), value, kind);
        }

        protected RegisterMapTest()
        {
            _factory = new MockRepository(MockBehavior.Strict);
            _clipboard = MockObjectFactory.CreateClipboardDevice(_factory);
            _fileName = null;
            _rawMap = VimUtil.CreateRegisterMap(_clipboard.Object, () => _fileName);
            _map = _rawMap;
        }

        public sealed class MiscTest : RegisterMapTest
        {
            /// <summary>
            /// The + register should use the clipboard backing
            /// </summary>
            [Fact]
            public void PlusRegister1()
            {
                _clipboard.SetupGet(x => x.Text).Returns("foo").Verifiable();
                Assert.Equal("foo", _map.GetRegister('+').StringValue);
                _factory.Verify();
            }

            /// <summary>
            /// The + register should use the clipboard backing
            /// </summary>
            [Fact]
            public void PlusRegister2()
            {
                _clipboard.SetupSet(x => x.Text = "bar").Verifiable();
                _map.GetRegister('+').RegisterValue = new RegisterValue("bar", OperationKind.CharacterWise);
                _factory.Verify();
            }

            /// <summary>
            /// The * register should use the clipboard backing
            /// </summary>
            [Fact]
            public void StarRegister1()
            {
                _clipboard.SetupGet(x => x.Text).Returns("foo").Verifiable();
                Assert.Equal("foo", _map.GetRegister('*').StringValue);
                _factory.Verify();
            }

            /// <summary>
            /// The * register should use the clipboard backing
            /// </summary>
            [Fact]
            public void StarRegister2()
            {
                _clipboard.SetupSet(x => x.Text = "bar").Verifiable();
                _map.GetRegister('*').RegisterValue = new RegisterValue("bar", OperationKind.CharacterWise);
                _factory.Verify();
            }

            /// <summary>
            /// Named registers shouldn't use the clipboard
            /// </summary>
            [Fact]
            public void NamedRegister1()
            {
                _clipboard.SetupGet(x => x.Text).Throws(new Exception());
                Assert.Equal("", _map.GetRegister('a').StringValue);
            }

            [Fact]
            public void FileNameRegister1()
            {
                Assert.Equal("", _map.GetRegister('%').StringValue);
                Assert.Equal(OperationKind.CharacterWise, _map.GetRegister('%').RegisterValue.OperationKind);
            }

            [Fact]
            public void FileNameRegister2()
            {
                _fileName = "foo";
                Assert.Equal("foo", _map.GetRegister('%').StringValue);
            }

            /// <summary>
            /// Append registers should just use the backing register when asked for a value
            /// </summary>
            [Fact]
            public void Append_UseBackingValue()
            {
                _map.GetRegister('c').UpdateValue("dog");
                Assert.Equal("dog", _map.GetRegister('C').StringValue);
            }

            /// <summary>
            /// Updating an append register should concat the backing value
            /// </summary>
            [Fact]
            public void Append_UpdateValue()
            {
                _map.GetRegister('c').UpdateValue("dog");
                _map.GetRegister('C').UpdateValue("cat");
                Assert.Equal("dogcat", _map.GetRegister('C').StringValue);
                Assert.Equal("dogcat", _map.GetRegister('c').StringValue);
            }

            [Fact]
            public void BlackHoleValue()
            {
                _map.GetRegister(RegisterName.Blackhole).UpdateValue("dog");
                Assert.Equal("", _map.GetRegister(RegisterName.Blackhole).StringValue);
            }
        }
    }
}
