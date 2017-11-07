﻿using System;
using System.Collections.Generic;
using Vim.VisualStudio.Implementation.Settings;
using Xunit;

namespace Vim.VisualStudio.UnitTest
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
            private static List<CommandKeyBinding> s_commandKeyBindings;

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

                s_commandKeyBindings = list;
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
                foreach (var commandKeyBinding in s_commandKeyBindings)
                {
                    var text = SettingSerializer.ConvertToString(new[] { commandKeyBinding });
                    var list = SettingSerializer.ConvertToCommandKeyBindings(text);
                    Assert.Single(list);
                    ExpectEqual(commandKeyBinding, list[0]);
                }
            }

            [Fact]
            public void WellKnownAll()
            {
                var text = SettingSerializer.ConvertToString(s_commandKeyBindings);
                var list = SettingSerializer.ConvertToCommandKeyBindings(text);
                Assert.Equal(s_commandKeyBindings.Count, list.Count);
                for (int i = 0; i < list.Count; i++)
                {
                    ExpectEqual(s_commandKeyBindings[i], list[i]);
                }
            }
        }
    }
}
