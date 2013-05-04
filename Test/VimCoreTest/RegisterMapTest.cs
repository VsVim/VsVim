using System;
using Moq;
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

        public sealed class SetRegisterValueTest : RegisterMapTest
        {
            /// <summary>
            /// Delete of a singel line should update many registers
            /// </summary>
            [Fact]
            public void DeleteSingleLine()
            {
                var reg = _map.GetRegister('c');
                _map.SetRegisterValue(reg, RegisterOperation.Delete, new RegisterValue("foo bar\n", OperationKind.CharacterWise));
                AssertRegister(reg, "foo bar\n", OperationKind.CharacterWise);
                AssertRegister(RegisterName.Unnamed, "foo bar\n", OperationKind.CharacterWise);
                AssertRegister('1', "foo bar\n", OperationKind.CharacterWise);
            }

            /// <summary>
            /// This shouldn't update the numbered registers since it was less than a line
            /// </summary>
            [Fact]
            public void DeletePartialLine()
            {
                var reg = _map.GetRegister('c');
                _map.SetRegisterValue(reg, RegisterOperation.Delete, new RegisterValue("foo bar", OperationKind.CharacterWise));
                AssertRegister(reg, "foo bar", OperationKind.CharacterWise);
                AssertRegister(RegisterName.Unnamed, "foo bar", OperationKind.CharacterWise);
                AssertRegister(RegisterName.SmallDelete, "", OperationKind.CharacterWise);
                AssertRegister('1', "", OperationKind.CharacterWise);
            }

            /// <summary>
            /// A yank operation shouldn't update the SmallDelete register
            /// </summary>
            [Fact]
            public void Yank()
            {
                var reg = _map.GetRegister('c');
                _map.GetRegister(RegisterName.SmallDelete).UpdateValue("", OperationKind.CharacterWise);
                _map.SetRegisterValue(reg, RegisterOperation.Yank, new RegisterValue("foo bar", OperationKind.CharacterWise));
                AssertRegister(reg, "foo bar", OperationKind.CharacterWise);
                AssertRegister(RegisterName.Unnamed, "foo bar", OperationKind.CharacterWise);
                AssertRegister(RegisterName.SmallDelete, "", OperationKind.CharacterWise);
            }

            /// <summary>
            /// Ensure the numbered registers are updated correctly for deletes
            /// </summary>
            [Fact]
            public void Numbered()
            {
                var reg = _map.GetRegister('c');
                _map.SetRegisterValue(reg, RegisterOperation.Delete, new RegisterValue("f\n", OperationKind.CharacterWise));
                _map.SetRegisterValue(reg, RegisterOperation.Delete, new RegisterValue("o\n", OperationKind.CharacterWise));
                AssertRegister(reg, "o\n", OperationKind.CharacterWise);
                AssertRegister(RegisterName.Unnamed, "o\n", OperationKind.CharacterWise);
                AssertRegister('1', "o\n", OperationKind.CharacterWise);
                AssertRegister('2', "f\n", OperationKind.CharacterWise);
            }

            /// <summary>
            /// Ensure the small delete register isn't update when a named register is used 
            /// </summary>
            [Fact]
            public void IgnoreSmallDelete()
            {
                var reg = _map.GetRegister('c');
                _map.SetRegisterValue(reg, RegisterOperation.Delete, new RegisterValue("foo", OperationKind.CharacterWise));
                AssertRegister(RegisterName.SmallDelete, "", OperationKind.CharacterWise);
            }

            /// <summary>
            /// Ensure the small delete register is updated when a delete occurs on the unnamed register
            /// </summary>
            [Fact]
            public void UpdateSmallDelete()
            {
                var reg = _map.GetRegister(RegisterName.Unnamed);
                _map.SetRegisterValue(reg, RegisterOperation.Delete, new RegisterValue("foo", OperationKind.CharacterWise));
                AssertRegister(RegisterName.SmallDelete, "foo", OperationKind.CharacterWise);
            }

            /// <summary>
            /// The SmallDelete register shouldn't update for a delete of multiple lines
            /// </summary>
            [Fact]
            public void DeleteOfMultipleLines()
            {
                _map.GetRegister(RegisterName.SmallDelete).UpdateValue("", OperationKind.CharacterWise);
                var reg = _map.GetRegister('c');
                var text = "cat" + Environment.NewLine + "dog";
                _map.SetRegisterValue(reg, RegisterOperation.Delete, new RegisterValue(text, OperationKind.CharacterWise));
                AssertRegister(RegisterName.SmallDelete, "", OperationKind.CharacterWise);
            }

            /// <summary>
            /// Deleting to the black hole register shouldn't affect unnamed or others
            /// </summary>
            [Fact]
            public void ForSpan_DeleteToBlackHole()
            {
                _map.GetRegister(RegisterName.Blackhole).UpdateValue("", OperationKind.CharacterWise);
                _map.GetRegister(RegisterName.NewNumbered(NumberedRegister.Number1)).UpdateValue("hey", OperationKind.CharacterWise);
                var namedReg = _map.GetRegister('c');
                _map.SetRegisterValue(namedReg, RegisterOperation.Yank, new RegisterValue("foo bar", OperationKind.CharacterWise));
                _map.SetRegisterValue(_map.GetRegister(RegisterName.Blackhole), RegisterOperation.Delete, new RegisterValue("foo bar", OperationKind.CharacterWise));
                AssertRegister(namedReg, "foo bar", OperationKind.CharacterWise);
                AssertRegister(RegisterName.Unnamed, "foo bar", OperationKind.CharacterWise);
                AssertRegister(RegisterName.NewNumbered(NumberedRegister.Number1), "hey", OperationKind.CharacterWise);
                AssertRegister(RegisterName.Blackhole, "", OperationKind.CharacterWise);
            }
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
        }
    }
}
