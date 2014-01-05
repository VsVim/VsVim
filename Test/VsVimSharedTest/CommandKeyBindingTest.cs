using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vim.UnitTest;
using Xunit;

namespace VsVim.UnitTest
{
    public abstract class CommandKeyBindingTest
    {
        public sealed class EqualityTest : CommandKeyBindingTest
        {
            [Fact]
            public void CommandId()
            {
                var id1 = new CommandId(Guid.NewGuid(), 42);
                var id2 = new CommandId(id1.Group, 13);
                var id3 = new CommandId(Guid.NewGuid(), 42);
                var keyBinding = KeyBinding.Parse("Global::Enter");

                EqualityUtil.RunAll(
                    (left, right) => left == right,
                    (left, right) => left != right,
                    false,
                    false,
                    EqualityUnit.Create(new CommandKeyBinding(id1, "Test", keyBinding))
                        .WithEqualValues(new CommandKeyBinding(id1, "Test", keyBinding))
                        .WithNotEqualValues(new CommandKeyBinding(id2, "Test", keyBinding))
                        .WithNotEqualValues(new CommandKeyBinding(id3, "Test", keyBinding)));
            }

            /// <summary>
            /// Right now we treat the name as case insensitive.  I don't know if this is the correct way
            /// or not but most items in VS are case insensitive so we are starting there 
            /// </summary>
            [Fact]
            public void Name()
            {
                var id = new CommandId(Guid.NewGuid(), 42);
                var keyBinding = KeyBinding.Parse("Global::Enter");

                EqualityUtil.RunAll(
                    (left, right) => left == right,
                    (left, right) => left != right,
                    false,
                    false,
                    EqualityUnit.Create(new CommandKeyBinding(id, "Test", keyBinding))
                        .WithEqualValues(new CommandKeyBinding(id, "Test", keyBinding))
                        .WithEqualValues(new CommandKeyBinding(id, "test", keyBinding))
                        .WithNotEqualValues(new CommandKeyBinding(id, "Blah", keyBinding)));
            }

            [Fact]
            public void KeyBindings()
            {
                var id = new CommandId(Guid.NewGuid(), 42);
                var keyBinding1 = KeyBinding.Parse("Global::Enter");
                var keyBinding2 = KeyBinding.Parse("Global::h");

                EqualityUtil.RunAll(
                    (left, right) => left == right,
                    (left, right) => left != right,
                    false,
                    false,
                    EqualityUnit.Create(new CommandKeyBinding(id, "Test", keyBinding1))
                        .WithEqualValues(new CommandKeyBinding(id, "Test", keyBinding1))
                        .WithNotEqualValues(new CommandKeyBinding(id, "Test", keyBinding2)));
            }
        }
    }
}
