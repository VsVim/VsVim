using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Vim;
using System.Diagnostics;
using System.Windows.Input;
using Vim.UI.Wpf;

namespace VsVim
{
    /// <summary>
    /// KeyBinding in Visual Studio as set through the Key board part of the Environment options
    /// panel
    /// </summary>
    public struct KeyBinding
    {
        public readonly string Scope;
        public readonly IEnumerable<KeyInput> KeyInputs;

        public KeyInput FirstKeyInput
        {
            get { return KeyInputs.First(); }
        }

        public KeyBinding(string scope, KeyInput input)
        {
            Scope = scope;
            KeyInputs = Enumerable.Repeat(input, 1);
        }

        public KeyBinding(string scope, IEnumerable<KeyInput> inputs)
        {
            Scope = scope;
            KeyInputs = inputs.ToList();
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.Append(Scope);
            builder.Append("::");
            var isfirst = true;
            foreach (var input in KeyInputs)
            {
                if ( !isfirst ) 
                {
                    builder.Append(',');
                }
                isfirst = false;
                builder.AppendFormat("{0}+{1}", input.Key, input.KeyModifiers);
            }
            return builder.ToString();
        }

        #region Parsing Methods

        private static Dictionary<string, VimKey> s_vsMap;

        private static void BuildVsMap()
        {
            var map = new Dictionary<string, VimKey>(StringComparer.OrdinalIgnoreCase);
            map.Add("Down Arrow", VimKey.DownKey);
            map.Add("Up Arrow", VimKey.UpKey);
            map.Add("Left Arrow", VimKey.LeftKey);
            map.Add("Right Arrow", VimKey.RightKey);
            map.Add("bkspce", VimKey.BackKey);
            map.Add("PgDn", VimKey.PageDownKey);
            map.Add("PgUp", VimKey.PageUpKey);
            map.Add("Ins", VimKey.InsertKey);
            map.Add("Del", VimKey.DeleteKey);
            map.Add("Esc", VimKey.EscapeKey);
            map.Add("Break", VimKey.BreakKey);
            s_vsMap = map;
        }

        private static void EnsureVsMap()
        {
            if (null == s_vsMap)
            {
                BuildVsMap();
            }
        }

        private static bool TryConvertToModifierKeys(string mod, out KeyModifiers modKeys )
        {
            var comp = StringComparer.OrdinalIgnoreCase;
            if ( comp.Equals(mod, "shift+"))
            {
                modKeys = KeyModifiers.Shift;
            }
            else if (comp.Equals(mod, "ctrl+"))
            {
                modKeys = KeyModifiers.Control;
            }
            else if ( comp.Equals(mod, "alt+"))
            {
                modKeys = KeyModifiers.Alt;
            }
            else 
            {
                modKeys = KeyModifiers.None;
                return false;
            }

            return true;
        }

        private static KeyInput ConvertToKeyInput(string keystroke)
        {
            if (keystroke.Length == 1)
            {
                var opt = InputUtil.TryCharToKeyInput(keystroke[0]);
                if (opt.IsSome())
                {
                    // Visual Studio doesn't differentiate between upper and lower case
                    // alpha characters.  Use all lower case for simplicity elsewhere
                    var v = opt.Value;
                    if (Char.IsLetter(v.Char) && 0 != (KeyModifiers.Shift & v.KeyModifiers))
                    {
                        return new KeyInput(
                            Char.ToLower(v.Char),
                            v.Key,
                            v.KeyModifiers & ~KeyModifiers.Shift);
                    }

                    return v;
                }
            }

            KeyInput vs = null;
            if (TryConvertVsSpecificKey(keystroke, out vs))
            {
                return vs;
            }

            try
            {
                var key = (Key)Enum.Parse(typeof(Key), keystroke, ignoreCase: true);
                return KeyUtil.ConvertToKeyInput(key);
            }
            catch (Exception)
            {
                
            }

            return null;
        }

        /// <summary>
        /// Maybe convert a Visual Studio specific keystroke
        /// </summary>
        private static bool TryConvertVsSpecificKey(string keystroke, out KeyInput ki)
        {
            EnsureVsMap();
            VimKey wellKnownKey;
            if (!s_vsMap.TryGetValue(keystroke, out wellKnownKey))
            {
                ki = null;
                return false;
            }

            ki = InputUtil.VimKeyToKeyInput(wellKnownKey);
            return true;
        }

        private static KeyInput ParseOne(string entry)
        {
            // If it's of length 1 it can only be a single keystroke entry
            if (entry.Length == 1)
            {
                return ConvertToKeyInput(entry);
            }

            KeyInput ki = null;
            var match = Regex.Match(entry, @"^([\w ]+\+)+(.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            var mod = KeyModifiers.None;
            if (match.Success)
            {
                foreach (var cap in match.Groups[1].Captures.Cast<Capture>())
                {
                    var modKeys = KeyModifiers.None;
                    if (!TryConvertToModifierKeys(cap.Value, out modKeys))
                    {
                        return null;
                    }
                    mod |= modKeys;
                }
                ki = ConvertToKeyInput(match.Groups[2].Value);
            }
            else
            {
                ki = ConvertToKeyInput(entry);
            }

            if (ki == null)
            {
                return null;
            }

            if (mod != KeyModifiers.None )
            {
                ki = new KeyInput(ki.Char, ki.Key, ki.KeyModifiers);
            }

            return ki;
        }

        /// <summary>
        /// Parse the key binding format as described by the Command.Bindings documentation
        /// 
        /// http://msdn.microsoft.com/en-us/library/envdte.command.bindings.aspx
        /// </summary>
        public static KeyBinding Parse(string binding)
        {
            KeyBinding keyBinding;
            if (!TryParse(binding, out keyBinding))
            {
                throw new ArgumentException("Invalid key binding");
            }

            return keyBinding;
        }

        public static bool TryParse(string binding, out KeyBinding keyBinding)
        {
            keyBinding = default(KeyBinding);
            var scopeEnd = binding.IndexOf(':');
            if (scopeEnd < 0)
            {
                return false;
            }

            // Num key binding not supported at this time
            if (binding.IndexOf("Num") >= 0)
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
