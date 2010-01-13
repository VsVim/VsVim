using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EnvDTE;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.OLE.Interop;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Utilities;
using Vim;
using System.Windows.Input;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text;

namespace VsVim
{
    public static class Extensions
    {
        #region FSharpOption<T>

        public static bool IsSome<T>(this FSharpOption<T> opt)
        {
            return FSharpOption<T>.get_IsSome(opt);
        }

        #endregion

        #region Command

        public static IEnumerable<CommandKeyBinding> GetKeyBindings(this Command command)
        {
            if (null == command)
            {
                throw new ArgumentNullException("command");
            }

            var name = command.Name;
            var bindings = command.Bindings as object[];
            if (bindings != null)
            {
                var bindingStrings = bindings
                    .Where(x => x is string)
                    .Cast<string>()
                    .Where(x => !String.IsNullOrEmpty(x));
                return ParseBindingStrings(name, bindingStrings);
            }

            var singleBinding = command.Bindings as string;
            if (singleBinding != null)
            {
                return ParseBindingStrings(name, Enumerable.Repeat(singleBinding, 1));
            }

            return Enumerable.Empty<CommandKeyBinding>();
        }

        private static IEnumerable<CommandKeyBinding> ParseBindingStrings(string name, IEnumerable<string> bindings)
        {
            foreach (var cur in bindings)
            {
                KeyBinding binding;
                if (KeyBinding.TryParse(cur, out binding))
                {
                    yield return new CommandKeyBinding(name, binding);
                }
            }
        }

        public static void SafeResetBindings(this Command command)
        {
            try
            {
                command.Bindings = new object[] { };
            }
            catch (COMException)
            {
                // Several implementations, Transact SQL in particular, return E_FAIL for this
                // operation.  Simply ignore the failure and continue
            }
        }

        #endregion

        #region Commands

        public static IEnumerable<Command> GetCommands(this Commands commands)
        {
            return commands.Cast<Command>();
        }

        #endregion

        #region IObjectWithSite

        public static Microsoft.VisualStudio.OLE.Interop.IServiceProvider GetServiceProvider(this IObjectWithSite ows)
        {
            var ptr = IntPtr.Zero;
            try
            {
                var guid = typeof(Microsoft.VisualStudio.OLE.Interop.IServiceProvider).GUID;
                ows.GetSite(ref guid, out ptr);
                var site = Marshal.GetObjectForIUnknown(ptr);
                return (Microsoft.VisualStudio.OLE.Interop.IServiceProvider)site;
            }
            finally
            {
                if (ptr != IntPtr.Zero)
                {
                    Marshal.Release(ptr);
                }
            }
        }


        #endregion

        #region PropertyCollection

        public static void AddTypedProperty<T>(this PropertyCollection col, T value)
        {
            col.AddProperty(typeof(T), value);
        }

        public static FSharpOption<T> TryGetTypedProperty<T>(this PropertyCollection col)
        {
            T value;
            if (col.TryGetProperty(typeof(T), out value))
            {
                return FSharpOption<T>.Some(value);
            }

            return FSharpOption<T>.None;
        }

        public static bool RemoveTypedProperty<T>(this PropertyCollection col)
        {
            return col.RemoveProperty(typeof(T));
        }

        #endregion

        #region KeyEventArgs

        public static KeyInput ConvertToKeyInput(this KeyEventArgs e)
        {
            return InputUtil.KeyAndModifierToKeyInput(e.Key, e.KeyboardDevice.Modifiers);
        }

        #endregion

        #region IVsTextLines

        /// <summary>
        /// Get the file name of the presented view.  If the name cannot be discovered an empty string will be returned
        /// </summary>
        public static string GetFileName(this IVsTextLines lines)
        {
            try
            {
                // GUID_VsBufferMoniker
                var monikerId = new Guid(0x978a8e17, 0x4df8, 0x432a, 150, 0x23, 0xd5, 0x30, 0xa2, 100, 0x52, 0xbc);
                var userData = (IVsUserData)lines;
                object data = null;
                if (Microsoft.VisualStudio.VSConstants.S_OK != userData.GetData(ref monikerId, out data)
                    || String.IsNullOrEmpty(data as string))
                {
                    return String.Empty;
                }

                return (string)data;
            }
            catch (InvalidCastException)
            {
                return String.Empty;
            }
        }

        #endregion

        #region ITextView

        /// <summary>
        /// Use a randomly generated GUID for the Key.  Forces consumers to go through the accessor instead of taking
        /// a dependency on the key in the property bag
        /// </summary>
        private static Guid s_VsVimBufferGuid = Guid.NewGuid();

        internal static bool TryGetVimBuffer(this ITextView textView, out VsVimBuffer buffer)
        {
            return textView.TextBuffer.TryGetVimBuffer(out buffer);
        }

        internal static bool TryGetVimBuffer(this ITextBuffer textBuffer, out VsVimBuffer buffer)
        {
            return textBuffer.Properties.TryGetProperty<VsVimBuffer>(s_VsVimBufferGuid, out buffer);
        }

        internal static void SetVimBuffer(this ITextView textView, VsVimBuffer buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }

            textView.TextBuffer.Properties.AddProperty(s_VsVimBufferGuid, buffer);
        }

        internal static bool RemoveVimBuffer(this ITextView textView)
        {
            return textView.TextBuffer.Properties.RemoveProperty(s_VsVimBufferGuid);
        }

        #endregion
    }
}
