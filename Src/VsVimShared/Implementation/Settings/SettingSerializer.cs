using System;
using System.Collections.Generic;
using System.Text;

namespace Vim.VisualStudio.Implementation.Settings
{
    internal static class SettingSerializer
    {
        private const char SeparatorChar = '!';
        private const char EscapeChar = '\\';

        internal static string ConvertToString(IEnumerable<CommandKeyBinding> bindings)
        {
            var builder = new StringBuilder();
            var first = true;
            foreach (var binding in bindings)
            {
                if (!first)
                {
                    builder.Append(SeparatorChar);
                }

                builder.Append(binding.Id.Group.ToString());
                builder.Append(SeparatorChar);
                builder.Append(binding.Id.Id);
                builder.Append(SeparatorChar);
                builder.Append(ConvertToEscapedString(binding.Name));
                builder.Append(SeparatorChar);
                builder.Append(ConvertToEscapedString(binding.KeyBinding.CommandString));
                first = false;
            }

            return builder.ToString();
        }

        internal static List<CommandKeyBinding> ConvertToCommandKeyBindings(string text)
        {
            if (TryConvertToCommandKeyBindings(text, out List<CommandKeyBinding> list))
            {
                return list;
            }

            return new List<CommandKeyBinding>();
        }

        private static bool TryConvertToCommandKeyBindings(string text, out List<CommandKeyBinding> bindingsList)
        {
            bindingsList = null;
            var list = new List<CommandKeyBinding>();
            var items = ConvertToList(text);
            if (items.Count % 4 != 0)
            {
                return false;
            }

            for (var i = 0; i < items.Count; i += 4)
            {
                if (!Guid.TryParse(items[i], out Guid group) ||
                    !UInt32.TryParse(items[i + 1], out uint id) ||
                    !KeyBinding.TryParse(items[i + 3], out KeyBinding keyBinding))
                {
                    return false;
                }

                var commandId = new CommandId(group, id);
                list.Add(new CommandKeyBinding(commandId, items[i + 2], keyBinding));
            }

            bindingsList = list;
            return true;
        }

        internal static string ConvertToEscapedString(string text)
        {
            // Don't do wasteful allocations if they're unnecessary
            if (text.IndexOf(EscapeChar) < 0 && text.IndexOf(SeparatorChar) < 0)
            {
                return text;
            }

            var builder = new StringBuilder();
            ConvertToEscapedString(builder, text);
            return builder.ToString();
        }

        internal static List<string> ConvertToList(string text)
        {
            var list = new List<string>();
            var builder = new StringBuilder();
            var takeNext = false;

            foreach (var cur in text)
            {
                if (takeNext)
                {
                    builder.Append(cur);
                    takeNext = false;
                    continue;
                }

                switch (cur)
                {
                    case EscapeChar:
                        takeNext = true;
                        break;
                    case SeparatorChar:
                        list.Add(builder.ToString());
                        builder.Length = 0;
                        break;
                    default:
                        builder.Append(cur);
                        break;
                }
            }

            // If the stream ended with a \ but no following character then just add an unescaped
            // \ value
            if (takeNext)
            {
                builder.Append(EscapeChar);
            }

            list.Add(builder.ToString());
            return list;
        }

        /// <summary>
        /// Our serialization tactic uses ! to represent a break.  This will escape every
        /// use of ! with a \ and also escape every \ with another \.  
        /// </summary>
        private static void ConvertToEscapedString(StringBuilder builder, string text)
        {
            foreach (var c in text)
            {
                switch (c)
                {
                    case EscapeChar:
                        builder.Append(EscapeChar);
                        builder.Append(EscapeChar);
                        break;
                    case SeparatorChar:
                        builder.Append(EscapeChar);
                        builder.Append(SeparatorChar);
                        break;
                    default:
                        builder.Append(c);
                        break;
                }
            }
        }
    }
}
