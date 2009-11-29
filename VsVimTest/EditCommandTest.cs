using VsVim;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using VimCore;

namespace VsVimTest
{
    
    
    /// <summary>
    ///This is a test class for EditCommandTest and is intended
    ///to contain all EditCommandTest Unit Tests
    ///</summary>
    [TestClass()]
    public class EditCommandTest
    {

        internal EditCommand Create(char c, EditCommandKind kind)
        {
            return Create(InputUtil.CharToKeyInput(c), kind);
        }

        internal EditCommand Create(KeyInput ki, EditCommandKind kind)
        {
            return new EditCommand(ki, kind, Guid.Empty, 0);
        }

        [TestMethod]
        public void Ctor1()
        {
            var command = Create(InputUtil.CharToKeyInput('a'), EditCommandKind.TypeChar);
            Assert.AreEqual(InputUtil.CharToKeyInput('a'), command.KeyInput);
            Assert.AreEqual(EditCommandKind.TypeChar, command.EditCommandKind);
        }

        [TestMethod]
        public void IsInput1()
        {
            Assert.IsTrue(Create('a', EditCommandKind.TypeChar).IsInput);
            Assert.IsTrue(Create('a', EditCommandKind.Backspace).IsInput);
            Assert.IsTrue(Create('a', EditCommandKind.Delete).IsInput);
            Assert.IsTrue(Create('a', EditCommandKind.Return).IsInput);
        }

        [TestMethod]
        public void IsInput2()
        {
            Assert.IsFalse(Create('a', EditCommandKind.Cancel).IsInput);
            Assert.IsFalse(Create('a', EditCommandKind.Unknown).IsInput);
        }

    }
}
