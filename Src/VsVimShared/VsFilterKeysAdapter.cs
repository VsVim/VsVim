using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Vim;

namespace Vim.VisualStudio
{
    /// <summary>
    /// In several modes where the native COM adapter layer is considered readonly, the
    /// IVsCodeWindow implementation will block keystrokes it interprets to be edit
    /// commands.  This includes items like Backspace, Enter, etc ...
    /// 
    /// This check is most frequently done while debugging in 64 bit mode (since ENC edits
    /// are illegal).  But also done in C# generated metadata file.  Really it will happen
    /// anytime the editor considers the view to be readonly as specified in 
    /// VsCodeWindowAdapter::IsReadOnly.  We must mimic that function
    ///
    /// When we detect this situation we avoid having keystrokes be translated as 
    /// Command's by interpcepting the TranslateAccelerator method.  If a command is created
    /// which is considered an edit then we simply return E_FAIL instead.  This causes the 
    /// message to be ignored and propagated up
    /// 
    /// This class might seem surperfulous at first in the face of the VsKeyProcessor 
    /// class.  That is a class of last resort.  Handling character input at that level 
    /// is not guaranteed to be accurate because of input conversion issues.  Much better
    /// to let VS or Windows to convert the input for us and handle it here or in TextInput
    /// </summary>
    internal sealed class VsFilterKeysAdapter : IVsFilterKeys
    {
        private static readonly object s_key = new object();
        private const string FieldName = "_filterKeys";

        private readonly IVsFilterKeys _filterKeys;
        private readonly IVsCodeWindow _codeWindow;
        private readonly IVimBuffer _buffer;
        private readonly IVsAdapter _vsAdapter;

        internal VsFilterKeysAdapter(
            IVsFilterKeys filterKeys,
            IVsCodeWindow codeWindow,
            IVsAdapter vsAdapter,
            IVimBuffer buffer)
        {
            _filterKeys = filterKeys;
            _buffer = buffer;
            _vsAdapter = vsAdapter;
            _codeWindow = codeWindow;

            _buffer.Closed += delegate { Uninstall(); };
        }

        int IVsFilterKeys.TranslateAccelerator(MSG[] msg, uint flags, out Guid commandGroup, out uint command)
        {
            var hr = _filterKeys.TranslateAccelerator(msg, flags, out commandGroup, out command);
            if (ErrorHandler.Succeeded(hr)
                && _vsAdapter.IsReadOnly(_buffer.TextView)
                && IsEditCommand(commandGroup, command))
            {
                commandGroup = Guid.Empty;
                command = 0;
                return VSConstants.E_FAIL;
            }

            return hr;
        }

        internal bool IsEditCommand(Guid commandGroup, uint commandId)
        {
            EditCommand command;
            return 
                OleCommandUtil.TryConvert(commandGroup, commandId, IntPtr.Zero, KeyModifiers.None, out command) && 
                command.HasKeyInput;
        }

        /// <summary>
        /// Uninstall the adapter
        /// </summary>
        private void Uninstall()
        {
            var type = _codeWindow.GetType();
            var flags = System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic;
            var field = type.GetField(FieldName, flags);
            field.SetValue(_codeWindow, _filterKeys);
        }

        /// <summary>
        /// Try and install the IVsFilterKeys adapter for the given ITextView.
        /// </summary>
        internal static bool TryInstallFilterKeysAdapter(
            IVsAdapter adapter,
            IVimBuffer buffer)
        {
            var textView = buffer.TextView;
            if (textView.Properties.ContainsProperty(s_key))
            {
                return true;
            }

            var textLines = adapter.EditorAdapter.GetBufferAdapter(buffer.TextBuffer) as IVsTextLines;
            if (textLines == null)
            {
                return false;
            }

            IVsCodeWindow codeWindow;
            if (!adapter.GetCodeWindow(textView).TryGetValue(out codeWindow))
            {
                return false;
            }

            // Grab the field we need to replace.  Be wary that Venus uses a different implementation
            // and the field will not be available 
            var type = codeWindow.GetType();
            var flags = System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic;
            var field = type.GetField(FieldName, flags);
            if (field == null)
            {
                return false;
            }

            var oldValue = field.GetValue(codeWindow) as IVsFilterKeys;
            if (oldValue == null)
            {
                return false;
            }

            var filterKeysAdapter = new VsFilterKeysAdapter(oldValue, codeWindow, adapter, buffer);
            field.SetValue(codeWindow, filterKeysAdapter);
            return true;
        }
    }
}
