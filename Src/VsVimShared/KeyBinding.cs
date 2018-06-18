using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Vim;
using Vim.Extensions;

namespace Vim.VisualStudio
{
    /// <summary>
    /// KeyBinding in Visual Studio as set through the Key board part of the Environment options
    /// panel
    /// </summary>
    public sealed class KeyBinding : IEquatable<KeyBinding>
    {
        public readonly string Scope;
        public readonly ReadOnlyCollection<KeyStroke> KeyStrokes;

        /// <summary>
        /// Visual Studio string which is the equivalent of this KeyBinding instance
        /// </summary>
        public readonly string CommandString;

        public KeyStroke FirstKeyStroke
        {
            get { return KeyStrokes[0]; }
        }

        public KeyBinding(string scope, KeyStroke stroke)
        {
            Scope = scope;
            KeyStrokes = new ReadOnlyCollection<KeyStroke>(new[] { stroke });
            CommandString = CreateCommandString(scope, KeyStrokes);
        }

        public KeyBinding(string scope, IEnumerable<KeyStroke> strokes)
        {
            Scope = scope;
            KeyStrokes = strokes.ToReadOnlyCollection();
            CommandString = CreateCommandString(Scope, KeyStrokes);
        }

        #region Equality

        public override int GetHashCode()
        {
            return Scope.GetHashCode() ^ CommandString.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            var other = obj as KeyBinding;
            return Equals(other);
        }

        public bool Equals(KeyBinding other)
        {
            if (ReferenceEquals(other, null))
            {
                return false;
            }

            var comp = StringComparer.OrdinalIgnoreCase;
            return
                comp.Equals(Scope, other.Scope)
                && comp.Equals(CommandString, other.CommandString);
        }

        public static bool operator ==(KeyBinding left, KeyBinding right)
        {
            return EqualityComparer<KeyBinding>.Default.Equals(left, right);
        }

        public static bool operator !=(KeyBinding left, KeyBinding right)
        {
            return !EqualityComparer<KeyBinding>.Default.Equals(left, right);
        }

        #endregion

        private static string CreateCommandString(string scope, IEnumerable<KeyStroke> keyStrokes)
        {
            var builder = new StringBuilder();
            builder.Append(scope);
            builder.Append("::");
            var isFirst = true;
            foreach (var stroke in keyStrokes)
            {
                if (!isFirst)
                {
                    builder.Append(", ");
                }
                isFirst = false;
                AppendCommandForSingle(stroke, builder);
            }

            return builder.ToString();
        }

        private static void AppendCommandForSingle(KeyStroke stroke, StringBuilder builder)
        {
            if (0 != (stroke.KeyModifiers & VimKeyModifiers.Control))
            {
                builder.Append("Ctrl+");
            }
            if (0 != (stroke.KeyModifiers & VimKeyModifiers.Shift))
            {
                builder.Append("Shift+");
            }
            if (0 != (stroke.KeyModifiers & VimKeyModifiers.Alt))
            {
                builder.Append("Alt+");
            }

            EnsureVsMap();
            var input = stroke.KeyInput;
            var query = s_vsMap.Where(x => x.Value == input);
            if (query.Any())
            {
                builder.Append(query.First().Key);
            }
            else if (char.IsLetter(input.Char))
            {
                builder.Append(char.ToUpper(input.Char));
            }
            else if (input.Char == ' ')
            {
                builder.Append("Space");
            }
            else
            {
                builder.Append(input.Char);
            }
        }

        public override string ToString()
        {
            return CommandString;
        }

        public static string CreateKeyBindingStringForSingleKeyStroke(KeyStroke stroke)
        {
            var builder = new StringBuilder();
            AppendCommandForSingle(stroke, builder);
            return builder.ToString();
        }

        #region Parsing Methods

        private static readonly string[] s_modifierPrefix = new[] { "Shift", "Alt", "Ctrl" };
        private static Dictionary<string, KeyInput> s_vsMap;

        private static void BuildVsMap()
        {
            var map = new Dictionary<string, KeyInput>(StringComparer.OrdinalIgnoreCase)
            {
                { "Down Arrow", KeyInputUtil.VimKeyToKeyInput(VimKey.Down) },
                { "Up Arrow", KeyInputUtil.VimKeyToKeyInput(VimKey.Up) },
                { "Left Arrow", KeyInputUtil.VimKeyToKeyInput(VimKey.Left) },
                { "Right Arrow", KeyInputUtil.VimKeyToKeyInput(VimKey.Right) },
                { "Bkspce", KeyInputUtil.VimKeyToKeyInput(VimKey.Back) },
                { "PgDn", KeyInputUtil.VimKeyToKeyInput(VimKey.PageDown) },
                { "PgUp", KeyInputUtil.VimKeyToKeyInput(VimKey.PageUp) },
                { "Ins", KeyInputUtil.VimKeyToKeyInput(VimKey.Insert) },
                { "Del", KeyInputUtil.VimKeyToKeyInput(VimKey.Delete) },
                { "Esc", KeyInputUtil.EscapeKey },
                { "Break", KeyInputUtil.CharWithControlToKeyInput('c') },
                { "Num +", KeyInputUtil.VimKeyToKeyInput(VimKey.KeypadPlus) },
                { "Num -", KeyInputUtil.VimKeyToKeyInput(VimKey.KeypadMinus) },
                { "Num /", KeyInputUtil.VimKeyToKeyInput(VimKey.KeypadDivide) },
                { "Num *", KeyInputUtil.VimKeyToKeyInput(VimKey.KeypadMultiply) },
                { "Enter", KeyInputUtil.EnterKey },
                { "Tab", KeyInputUtil.TabKey },
                { "Home", KeyInputUtil.VimKeyToKeyInput(VimKey.Home) },
                { "End", KeyInputUtil.VimKeyToKeyInput(VimKey.End) },
                { "F1", KeyInputUtil.VimKeyToKeyInput(VimKey.F1) },
                { "F2", KeyInputUtil.VimKeyToKeyInput(VimKey.F2) },
                { "F3", KeyInputUtil.VimKeyToKeyInput(VimKey.F3) },
                { "F4", KeyInputUtil.VimKeyToKeyInput(VimKey.F4) },
                { "F5", KeyInputUtil.VimKeyToKeyInput(VimKey.F5) },
                { "F6", KeyInputUtil.VimKeyToKeyInput(VimKey.F6) },
                { "F7", KeyInputUtil.VimKeyToKeyInput(VimKey.F7) },
                { "F8", KeyInputUtil.VimKeyToKeyInput(VimKey.F8) },
                { "F9", KeyInputUtil.VimKeyToKeyInput(VimKey.F9) },
                { "F10", KeyInputUtil.VimKeyToKeyInput(VimKey.F10) },
                { "F11", KeyInputUtil.VimKeyToKeyInput(VimKey.F11) },
                { "F12", KeyInputUtil.VimKeyToKeyInput(VimKey.F12) },
                { "Space", KeyInputUtil.CharToKeyInput(' ') }
            };

            s_vsMap = map;
        }

        private static void EnsureVsMap()
        {
            if (null == s_vsMap)
            {
                BuildVsMap();
            }
        }

        private static bool TryConvertToModifierKeys(string mod, out VimKeyModifiers modKeys)
        {
            var comp = StringComparer.OrdinalIgnoreCase;
            if (comp.Equals(mod, "shift"))
            {
                modKeys = VimKeyModifiers.Shift;
            }
            else if (comp.Equals(mod, "ctrl"))
            {
                modKeys = VimKeyModifiers.Control;
            }
            else if (comp.Equals(mod, "alt"))
            {
                modKeys = VimKeyModifiers.Alt;
            }
            else
            {
                modKeys = VimKeyModifiers.None;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Convert the single character to a KeyInput. Visual Studio doesn't 
        /// differentiate between upper and lower case alpha characters.  
        /// Use all lower case for simplicity elsewhere
        /// </summary>
        private static KeyInput ConvertToKeyInput(char c)
        {
            c = char.IsLetter(c) ? char.ToLower(c) : c;
            return KeyInputUtil.CharToKeyInput(c);
        }

        private static KeyInput ConvertToKeyInput(string keystroke)
        {
            if (keystroke.Length == 1)
            {
                return ConvertToKeyInput(keystroke[0]);
            }

            if (TryConvertVsSpecificKey(keystroke, out KeyInput vs))
            {
                return vs;
            }

            return null;
        }

        /// <summary>
        /// Try and convert the given string into a Visual Studio specific key stroke.
        /// </summary>
        private static bool TryConvertVsSpecificKey(string keystroke, out KeyInput keyInput)
        {
            EnsureVsMap();
            if (s_vsMap.TryGetValue(keystroke, out keyInput))
            {
                return true;
            }

            if (keystroke.StartsWith("Num ", StringComparison.OrdinalIgnoreCase))
            {
                keyInput = null;
                switch (keystroke.ToLower())
                {
                    case "num +":
                        keyInput = KeyInputUtil.VimKeyToKeyInput(VimKey.KeypadPlus);
                        break;
                    case "num /":
                        keyInput = KeyInputUtil.VimKeyToKeyInput(VimKey.KeypadDivide);
                        break;
                    case "num *":
                        keyInput = KeyInputUtil.VimKeyToKeyInput(VimKey.KeypadMultiply);
                        break;
                    case "num -":
                        keyInput = KeyInputUtil.VimKeyToKeyInput(VimKey.KeypadMinus);
                        break;
                }
                return keyInput != null;
            }

            keyInput = null;
            return false;
        }

        private static KeyStroke ParseOne(string entry)
        {
            // If it's of length 1 it can only be a single keystroke entry
            if (entry.Length == 1)
            {
                return new KeyStroke(ConvertToKeyInput(entry), VimKeyModifiers.None);
            }

            // First get rid of the Modifiers
            var mod = VimKeyModifiers.None;
            while (s_modifierPrefix.Any(x => entry.StartsWith(x, StringComparison.OrdinalIgnoreCase)))
            {
                var index = entry.IndexOf('+');
                if (index < 0)
                {
                    return null;
                }

                var value = entry.Substring(0, index);
                var modKeys = VimKeyModifiers.None;
                if (!TryConvertToModifierKeys(value, out modKeys))
                {
                    return null;
                }
                mod |= modKeys;
                entry = entry.Substring(index + 1).TrimStart();
            }

            var ki = ConvertToKeyInput(entry);
            if (ki == null)
            {
                return null;
            }

            return new KeyStroke(ki, mod);
        }

        /// <summary>
        /// Parse the key binding format as described by the Command.Bindings documentation
        /// 
        /// http://msdn.microsoft.com/en-us/library/envdte.command.bindings.aspx
        /// </summary>
        public static KeyBinding Parse(string binding)
        {
            if (!TryParse(binding, out KeyBinding keyBinding))
            {
                throw new ArgumentException("Invalid key binding");
            }

            return keyBinding;
        }

        public static bool TryParse(string binding, out KeyBinding keyBinding)
        {
            keyBinding = default;
            var scopeEnd = binding.IndexOf(':');
            if (scopeEnd < 0)
            {
                return false;
            }

            var scope = binding.Substring(0, scopeEnd);
            var rest = binding.Substring(scopeEnd + 2);
            var entries = rest
                .Split(new string[] { ", " }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => ParseOne(x));
            if (entries.Any(x => x == null))
            {
                return false;
            }

            keyBinding = new KeyBinding(scope, entries);
            return true;
        }

        #endregion
    }
}
