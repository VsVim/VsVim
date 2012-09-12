using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using VsVim;
using VsVim.Implementation.ConflictingKey;

namespace VsVim.UnitTest
{
    public class KeyBindingDataTest
    {
        [Fact]
        public void Ctor1()
        {
            var binding = KeyBinding.Parse("Global::Ctrl+Left Arrow");
            var command = new CommandKeyBinding(new CommandId(), "Foo", binding);
            var data = new KeyBindingData(new CommandKeyBinding[] { command });
            Assert.Equal("Ctrl+Left Arrow", data.KeyName);
            Assert.False(data.HandledByVsVim);
        }
    }
}
