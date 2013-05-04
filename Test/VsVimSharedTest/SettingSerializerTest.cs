using System;
using System.Collections.Generic;
using VsVim.Implementation.Settings;
using Xunit;

namespace VsVim.UnitTest
{
    public abstract class SettingSerializerTest
    {
        public sealed class ConvertToEscapedString
        {
            private static void Expect(string input, string expect)
            {
                Assert.Equal(expect, SettingSerializer.ConvertToEscapedString(input));
            }

            [Fact]
            public void Identity()
            {
                Expect(@"hello", @"hello");
                Expect(@"dog", @"dog");
            }

            [Fact]
            public void EscapeInBack()
            {
                Expect(@"dog!", @"dog\!");
            }

            [Fact]
            public void EscapeInMiddle()
            {
                Expect(@"dog!cat", @"dog\!cat");
            }

            [Fact]
            public void EscapeInFront()
            {
                Expect(@"!cat", @"\!cat");
            }

            [Fact]
            public void EscapeEscape()
            {
                Expect(@"dog\cat", @"dog\\cat");
            }
        }

        public sealed class ConvertToList
        {
            private static void Expect(string input, string[] expected)
            {
                var list = SettingSerializer.ConvertToList(input);
                Assert.Equal(expected, list);
            }

            [Fact]
            public void Identity()
            {
                Expect(@"dog", new[] { @"dog" });
            }

            [Fact]
            public void EmptyStringShouldHaveSingleEntry()
            {
                Expect(@"", new[] { @"" });
            }

            [Fact]
            public void EscapeAtEndShouldHaveEmptyTrailingEntry()
            {
                Expect(@"dog!", new[] { @"dog", @"" });
            }

            [Fact]
            public void Simple()
            {
                Expect(@"dog!cat!bear", new[] { @"dog", @"cat", @"bear" });
            }

            [Fact]
            public void HandleEscapedCharacter()
            {
                Expect(@"dog\!cat", new[] { @"dog!cat" });
            }
        }

        public sealed class MiscTest : SettingSerializerTest
        {
            private static List<CommandKeyBinding> CommandKeyBindings;

            static MiscTest()
            {
                var list = new List<CommandKeyBinding>();
                var commandId = new CommandId(Guid.NewGuid(), 42);
                var name = "Name";
                foreach (var bindingText in KeyBindingTest.SampleCommands)
                {
                    var keyBinding = KeyBinding.Parse(bindingText);
                    var commandKeyBinding = new CommandKeyBinding(commandId, name, keyBinding);
                    list.Add(commandKeyBinding);
                }

                CommandKeyBindings = list;
            }

            private void ExpectEqual(CommandKeyBinding left, CommandKeyBinding right)
            {
                Assert.Equal(left.Id, right.Id);
                Assert.Equal(left.Name, right.Name);
                Assert.Equal(left.KeyBinding, right.KeyBinding);
            }

            [Fact]
            public void WellKnown()
            {
                foreach (var commandKeyBinding in CommandKeyBindings)
                {
                    var text = SettingSerializer.ConvertToString(new[] { commandKeyBinding });
                    var list = SettingSerializer.ConvertToCommandKeyBindings(text);
                    Assert.Equal(1, list.Count);
                    ExpectEqual(commandKeyBinding, list[0]);
                }
            }

            [Fact]
            public void WellKnownAll()
            {
                var text = SettingSerializer.ConvertToString(CommandKeyBindings);
                var list = SettingSerializer.ConvertToCommandKeyBindings(text);
                Assert.Equal(CommandKeyBindings.Count, list.Count);
                for (int i = 0; i < list.Count; i++)
                {
                    ExpectEqual(CommandKeyBindings[i], list[i]);
                }
            }
        }
    }
}
