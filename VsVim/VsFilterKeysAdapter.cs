using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Vim;

namespace VsVim
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
    /// </summary>
    internal sealed class VsFilterKeysAdapter : IVsFilterKeys
    {
        private static readonly object s_key = new object();
        private const string FieldName = "_filterKeys";

        private readonly IVsFilterKeys _filterKeys;
        private readonly IVsTextLines _textLines;
        private readonly IVsCodeWindow _codeWindow;
        private readonly IVimBuffer _buffer;
        private readonly IEditorOptions _editorOptions;

        internal VsFilterKeysAdapter(
            IVsFilterKeys filterKeys,
            IVsCodeWindow codeWindow,
            IVsTextLines textLines,
            IEditorOptions editorOptions,
            IVimBuffer buffer)
        {
            _filterKeys = filterKeys;
            _buffer = buffer;
            _textLines = textLines;
            _codeWindow = codeWindow;
            _editorOptions = editorOptions;

            _buffer.Closed += delegate { Uninstall(); };
        }

        int IVsFilterKeys.TranslateAccelerator(MSG[] msg, uint flags, out Guid commandGroup, out uint command)
        {
            var hr = _filterKeys.TranslateAccelerator(msg, flags, out commandGroup, out command);
            if (ErrorHandler.Succeeded(hr)
                && IsReadOnly()
                && IsEditCommand(commandGroup, command))
            {
                commandGroup = Guid.Empty;
                command = 0;
                return VSConstants.E_FAIL;
            }

            return hr;
        }

        /// <summary>
        /// Mimic the VsCodeWindowAdapter::IsReadOnly method.  We only want to intercept keys
        /// when this is true
        /// </summary>
        internal bool IsReadOnly()
        {
            if (EditorOptionsUtil.GetOptionValueOrDefault(_editorOptions, DefaultTextViewOptions.ViewProhibitUserInputId, false))
            {
                return true;
            }

            uint flags;
            if (ErrorHandler.Succeeded(_textLines.GetStateFlags(out flags))
                && 0 != (flags & (uint)BUFFERSTATEFLAGS.BSF_USER_READONLY))
            {
                return true;
            }

            return false;
        }

        internal bool IsEditCommand(Guid commandGroup, uint commandId)
        {
            EditCommand command;
            return OleCommandUtil.TryConvert(commandGroup, commandId, out command)
                && command.IsInput;
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
            IEditorOptionsFactoryService optionsFactory,
            IVimBuffer buffer)
        {
            var textView = buffer.TextView;
            if (textView.Properties.ContainsProperty(s_key))
            {
                return true;
            }

            var editorOptions = optionsFactory.GetOptions(buffer.TextView);
            var textLines = adapter.EditorAdapter.GetBufferAdapter(buffer.TextBuffer) as IVsTextLines;
            if (textLines == null)
            {
                return false;
            }

            IVsCodeWindow codeWindow;
            if (!adapter.TryGetCodeWindow(textView, out codeWindow) || textLines == null || editorOptions == null)
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

            var filterKeysAdapter = new VsFilterKeysAdapter(oldValue, codeWindow, textLines, editorOptions, buffer);
            field.SetValue(codeWindow, filterKeysAdapter);
            return true;
        }
    }
}
