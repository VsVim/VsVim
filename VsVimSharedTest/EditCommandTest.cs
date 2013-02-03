using System;
using Xunit;
using Vim;

namespace VsVim.UnitTest
{
    /// <summary>
    ///This is a test class for EditCommandTest and is intended
    ///to contain all EditCommandTest Unit Tests
    ///</summary>
    public sealed class EditCommandTest
    {
        internal EditCommand Create(char c, EditCommandKind kind)
        {
            return Create(KeyInputUtil.CharToKeyInput(c), kind);
        }

        internal EditCommand Create(KeyInput ki, EditCommandKind kind)
        {
            return new EditCommand(ki, kind, Guid.Empty, 0);
        }

        [Fact]
        public void Ctor1()
        {
            var command = Create(KeyInputUtil.CharToKeyInput('a'), EditCommandKind.UserInput);
            Assert.Equal(KeyInputUtil.CharToKeyInput('a'), command.KeyInput);
            Assert.Equal(EditCommandKind.UserInput, command.EditCommandKind);
        }

        [Fact]
        public void IsInput1()
        {
            Assert.True(Create('a', EditCommandKind.UserInput).HasKeyInput);
            Assert.True(Create('a', EditCommandKind.UserInput).HasKeyInput);
            Assert.True(Create('a', EditCommandKind.UserInput).HasKeyInput);
            Assert.True(Create('a', EditCommandKind.UserInput).HasKeyInput);
        }
    }
}
