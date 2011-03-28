using System;
using NUnit.Framework;
using Vim;

namespace VsVim.UnitTest
{


    /// <summary>
    ///This is a test class for EditCommandTest and is intended
    ///to contain all EditCommandTest Unit Tests
    ///</summary>
    [TestFixture()]
    public class EditCommandTest
    {

        internal EditCommand Create(char c, EditCommandKind kind)
        {
            return Create(KeyInputUtil.CharToKeyInput(c), kind);
        }

        internal EditCommand Create(KeyInput ki, EditCommandKind kind)
        {
            return new EditCommand(ki, kind, Guid.Empty, 0);
        }

        [Test]
        public void Ctor1()
        {
            var command = Create(KeyInputUtil.CharToKeyInput('a'), EditCommandKind.TypeChar);
            Assert.AreEqual(KeyInputUtil.CharToKeyInput('a'), command.KeyInput);
            Assert.AreEqual(EditCommandKind.TypeChar, command.EditCommandKind);
        }

        [Test]
        public void IsInput1()
        {
            Assert.IsTrue(Create('a', EditCommandKind.TypeChar).HasKeyInput);
            Assert.IsTrue(Create('a', EditCommandKind.Backspace).HasKeyInput);
            Assert.IsTrue(Create('a', EditCommandKind.Delete).HasKeyInput);
            Assert.IsTrue(Create('a', EditCommandKind.Return).HasKeyInput);
        }
    }
}
