﻿using System;
using Moq;
using NUnit.Framework;
using Vim.UnitTest.Mock;

namespace Vim.UnitTest
{
    [TestFixture]
    public class RegisterMapTest
    {
        private MockRepository _factory;
        private Mock<IClipboardDevice> _clipboard;
        private RegisterMap _rawMap;
        private IRegisterMap _map;
        private string _fileName;

        static void AssertRegister(Register reg, string value, OperationKind kind)
        {
            Assert.AreEqual(value, reg.StringValue);
            Assert.AreEqual(kind, reg.RegisterValue.OperationKind);
        }

        void AssertRegister(RegisterName name, string value, OperationKind kind)
        {
            AssertRegister(_map.GetRegister(name), value, kind);
        }

        void AssertRegister(char name, string value, OperationKind kind)
        {
            AssertRegister(_map.GetRegister(name), value, kind);
        }

        [SetUp]
        public void Setup()
        {
            _factory = new MockRepository(MockBehavior.Strict);
            _clipboard = MockObjectFactory.CreateClipboardDevice(_factory);
            _fileName = null;
            _rawMap = VimUtil.CreateRegisterMap(_clipboard.Object, () => _fileName);
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
            _map.GetRegister('+').RegisterValue = RegisterValue.OfString("bar", OperationKind.CharacterWise);
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
            _map.GetRegister('*').RegisterValue = RegisterValue.OfString("bar", OperationKind.CharacterWise);
            _factory.Verify();
        }

        [Test]
        [Description("Named registers shouldn't use the clipboard")]
        public void NamedRegister1()
        {
            _clipboard.SetupGet(x => x.Text).Throws(new Exception());
            Assert.AreEqual("", _map.GetRegister('a').StringValue);
        }

        [Test]
        public void FileNameRegister1()
        {
            Assert.AreEqual("", _map.GetRegister('%').StringValue);
            Assert.AreEqual(OperationKind.CharacterWise, _map.GetRegister('%').RegisterValue.OperationKind);
        }

        [Test]
        public void FileNameRegister2()
        {
            _fileName = "foo";
            Assert.AreEqual("foo", _map.GetRegister('%').StringValue);
        }

        /// <summary>
        /// Append registers should just use the backing register when asked for a value
        /// </summary>
        [Test]
        public void Append_UseBackingValue()
        {
            _map.GetRegister('c').UpdateValue("dog");
            Assert.AreEqual("dog", _map.GetRegister('C').StringValue);
        }

        /// <summary>
        /// Updating an append register should concat the backing value
        /// </summary>
        [Test]
        public void Append_UpdateValue()
        {
            _map.GetRegister('c').UpdateValue("dog");
            _map.GetRegister('C').UpdateValue("cat");
            Assert.AreEqual("dogcat", _map.GetRegister('C').StringValue);
            Assert.AreEqual("dogcat", _map.GetRegister('c').StringValue);
        }

        /// <summary>
        /// Delete of a singel line should update many registers
        /// </summary>
        [Test]
        public void SetRegisterValue_DeleteSingleLine()
        {
            var reg = _map.GetRegister('c');
            _map.SetRegisterValue(reg, RegisterOperation.Delete, RegisterValue.OfString("foo bar", OperationKind.CharacterWise));
            AssertRegister(reg, "foo bar", OperationKind.CharacterWise);
            AssertRegister(RegisterName.Unnamed, "foo bar", OperationKind.CharacterWise);
            AssertRegister(RegisterName.NewNumbered(NumberedRegister.Register_1), "foo bar", OperationKind.CharacterWise);
            AssertRegister(RegisterName.SmallDelete, "foo bar", OperationKind.CharacterWise);
        }

        /// <summary>
        /// A yank operation shouldn't update the SmallDelete register
        /// </summary>
        [Test]
        public void SetRegisterValue_Yank()
        {
            var reg = _map.GetRegister('c');
            _map.GetRegister(RegisterName.SmallDelete).UpdateValue("", OperationKind.LineWise);
            _map.SetRegisterValue(reg, RegisterOperation.Yank, RegisterValue.OfString("foo bar", OperationKind.CharacterWise));
            AssertRegister(reg, "foo bar", OperationKind.CharacterWise);
            AssertRegister(RegisterName.Unnamed, "foo bar", OperationKind.CharacterWise);
            AssertRegister(RegisterName.SmallDelete, "", OperationKind.LineWise);
        }

        /// <summary>
        /// Ensure the numbered registers are updated correctly for deletes
        /// </summary>
        [Test]
        public void SetRegisterValue_Numbered()
        {
            var reg = _map.GetRegister('c');
            _map.SetRegisterValue(reg, RegisterOperation.Delete, RegisterValue.OfString("f", OperationKind.CharacterWise));
            _map.SetRegisterValue(reg, RegisterOperation.Delete, RegisterValue.OfString("o", OperationKind.CharacterWise));
            AssertRegister(reg, "o", OperationKind.CharacterWise);
            AssertRegister(RegisterName.Unnamed, "o", OperationKind.CharacterWise);
            AssertRegister(RegisterName.NewNumbered(NumberedRegister.Register_1), "o", OperationKind.CharacterWise);
            AssertRegister(RegisterName.NewNumbered(NumberedRegister.Register_2), "f", OperationKind.CharacterWise);
        }

        /// <summary>
        /// Ensure the small delete register is properly updated
        /// </summary>
        [Test]
        public void SetRegisterValue_SmallDelete()
        {
            var reg = _map.GetRegister('c');
            _map.SetRegisterValue(reg, RegisterOperation.Delete, RegisterValue.OfString("foo", OperationKind.CharacterWise));
            AssertRegister(RegisterName.SmallDelete, "foo", OperationKind.CharacterWise);
        }

        /// <summary>
        /// The SmallDelete register shouldn't update for a delete of multiple lines
        /// </summary>
        [Test]
        public void SetRegisterValue_DeleteOfMultipleLines()
        {
            _map.GetRegister(RegisterName.SmallDelete).UpdateValue("", OperationKind.LineWise);
            var reg = _map.GetRegister('c');
            var text = "cat" + Environment.NewLine + "dog";
            _map.SetRegisterValue(reg, RegisterOperation.Delete, RegisterValue.OfString(text, OperationKind.CharacterWise));
            AssertRegister(RegisterName.SmallDelete, "", OperationKind.LineWise);
        }

        /// <summary>
        /// Deleting to the black hole register shouldn't affect unnamed or others
        /// </summary>
        [Test]
        public void SetRegisterValue_ForSpan_DeleteToBlackHole()
        {
            _map.GetRegister(RegisterName.Blackhole).UpdateValue("", OperationKind.LineWise);
            _map.GetRegister(RegisterName.NewNumbered(NumberedRegister.Register_1)).UpdateValue("hey", OperationKind.CharacterWise);
            var namedReg = _map.GetRegister('c');
            _map.SetRegisterValue(namedReg, RegisterOperation.Yank, RegisterValue.OfString("foo bar", OperationKind.CharacterWise));
            _map.SetRegisterValue(_map.GetRegister(RegisterName.Blackhole), RegisterOperation.Delete, RegisterValue.OfString("foo bar", OperationKind.CharacterWise));
            AssertRegister(namedReg, "foo bar", OperationKind.CharacterWise);
            AssertRegister(RegisterName.Unnamed, "foo bar", OperationKind.CharacterWise);
            AssertRegister(RegisterName.NewNumbered(NumberedRegister.Register_1), "hey", OperationKind.CharacterWise);
            AssertRegister(RegisterName.Blackhole, "", OperationKind.LineWise);
        }

        /// <summary>
        /// Unnamed registers should use system clipboard backing
        /// </summary>
        [Test]
        public void UnnamedRegister_To_Use_Clipboard_Backing()
        {
            var unnamedReg = _map.GetRegister(RegisterName.Unnamed);
            var backing = unnamedReg._valueBacking;
            Assert.True(backing.GetType().IsAssignableFrom(typeof(ClipboardRegisterValueBacking)));
        }

    }
}
