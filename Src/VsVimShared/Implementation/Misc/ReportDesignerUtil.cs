using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Vim;

namespace Vim.VisualStudio.Implementation.Misc
{
    [Export(typeof(IReportDesignerUtil))]
    internal sealed class ReportDesignerUtil : IReportDesignerUtil
    {
        internal const string RdlContentTypeName = "RDL Expression";
        internal static readonly Guid ReportContextGuid = new Guid("{8F6D573E-7AB8-4d8e-8A7A-73965B903F04}");
        private static readonly HashSet<KeyInput> s_specialHandledSet = new HashSet<KeyInput>();
        private readonly IVsEditorAdaptersFactoryService _vsEditorAdaptersFactoryService;

        static ReportDesignerUtil()
        {
            // The set of keys which are special handled are defined by the CodeWindow::ProcessKeyMessage inside
            // of Microsoft.ReportDesigner.dll
            s_specialHandledSet.Add(KeyInputUtil.EnterKey);
            s_specialHandledSet.Add(KeyInputUtil.TabKey);
            s_specialHandledSet.Add(KeyInputUtil.EscapeKey);
            s_specialHandledSet.Add(KeyInputUtil.VimKeyToKeyInput(VimKey.Delete));
            s_specialHandledSet.Add(KeyInputUtil.VimKeyToKeyInput(VimKey.Back));
            s_specialHandledSet.Add(KeyInputUtil.VimKeyToKeyInput(VimKey.PageUp));
            s_specialHandledSet.Add(KeyInputUtil.ApplyKeyModifiersToKey(VimKey.PageUp, VimKeyModifiers.Shift));
            s_specialHandledSet.Add(KeyInputUtil.VimKeyToKeyInput(VimKey.PageDown));
            s_specialHandledSet.Add(KeyInputUtil.ApplyKeyModifiersToKey(VimKey.PageDown, VimKeyModifiers.Shift));
            s_specialHandledSet.Add(KeyInputUtil.VimKeyToKeyInput(VimKey.End));
            s_specialHandledSet.Add(KeyInputUtil.ApplyKeyModifiersToKey(VimKey.End, VimKeyModifiers.Shift));
            s_specialHandledSet.Add(KeyInputUtil.ApplyKeyModifiersToKey(VimKey.End, VimKeyModifiers.Control));
            s_specialHandledSet.Add(KeyInputUtil.ApplyKeyModifiersToKey(VimKey.End, VimKeyModifiers.Control | VimKeyModifiers.Shift));
            s_specialHandledSet.Add(KeyInputUtil.VimKeyToKeyInput(VimKey.Home));
            s_specialHandledSet.Add(KeyInputUtil.ApplyKeyModifiersToKey(VimKey.Home, VimKeyModifiers.Shift));
            s_specialHandledSet.Add(KeyInputUtil.ApplyKeyModifiersToKey(VimKey.Home, VimKeyModifiers.Control));
            s_specialHandledSet.Add(KeyInputUtil.ApplyKeyModifiersToKey(VimKey.Home, VimKeyModifiers.Control | VimKeyModifiers.Shift));
            s_specialHandledSet.Add(KeyInputUtil.VimKeyToKeyInput(VimKey.Left));
            s_specialHandledSet.Add(KeyInputUtil.ApplyKeyModifiersToKey(VimKey.Left, VimKeyModifiers.Shift));
            s_specialHandledSet.Add(KeyInputUtil.ApplyKeyModifiersToKey(VimKey.Left, VimKeyModifiers.Control));
            s_specialHandledSet.Add(KeyInputUtil.ApplyKeyModifiersToKey(VimKey.Left, VimKeyModifiers.Control | VimKeyModifiers.Shift));
            s_specialHandledSet.Add(KeyInputUtil.VimKeyToKeyInput(VimKey.Right));
            s_specialHandledSet.Add(KeyInputUtil.ApplyKeyModifiersToKey(VimKey.Right, VimKeyModifiers.Shift));
            s_specialHandledSet.Add(KeyInputUtil.ApplyKeyModifiersToKey(VimKey.Right, VimKeyModifiers.Control));
            s_specialHandledSet.Add(KeyInputUtil.ApplyKeyModifiersToKey(VimKey.Right, VimKeyModifiers.Control | VimKeyModifiers.Shift));
            s_specialHandledSet.Add(KeyInputUtil.VimKeyToKeyInput(VimKey.Up));
            s_specialHandledSet.Add(KeyInputUtil.ApplyKeyModifiersToKey(VimKey.Up, VimKeyModifiers.Shift));
            s_specialHandledSet.Add(KeyInputUtil.VimKeyToKeyInput(VimKey.Down));
            s_specialHandledSet.Add(KeyInputUtil.ApplyKeyModifiersToKey(VimKey.Down, VimKeyModifiers.Shift));
            s_specialHandledSet.Add(KeyInputUtil.ApplyKeyModifiersToChar('a', VimKeyModifiers.Control));
            s_specialHandledSet.Add(KeyInputUtil.ApplyKeyModifiersToChar('v', VimKeyModifiers.Control));
            s_specialHandledSet.Add(KeyInputUtil.ApplyKeyModifiersToChar('x', VimKeyModifiers.Control));
            s_specialHandledSet.Add(KeyInputUtil.ApplyKeyModifiersToChar('y', VimKeyModifiers.Control));
            s_specialHandledSet.Add(KeyInputUtil.ApplyKeyModifiersToChar('z', VimKeyModifiers.Control));
            s_specialHandledSet.Add(KeyInputUtil.ApplyKeyModifiersToChar('z', VimKeyModifiers.Control | VimKeyModifiers.Shift));
            s_specialHandledSet.Add(KeyInputUtil.CharToKeyInput('\b'));
        }

        [ImportingConstructor]
        internal ReportDesignerUtil(IVsEditorAdaptersFactoryService vsEditorAdaptersFactoryService)
        {
            _vsEditorAdaptersFactoryService = vsEditorAdaptersFactoryService;
        }

        /// <summary>
        /// Ideally we should be checking for both the correct content type and some tag to indicate
        /// that this is actually hosted in the report expression editor.  I can't find such a tag though
        /// so go with the lesser property of the content type matching
        /// </summary>
        private bool IsExpressionView(ITextView textView)
        {
            // First step is to check for the correct content type.  
            if (!textView.TextBuffer.ContentType.IsOfType(RdlContentTypeName))
            {
                return false;
            }

            // Next dig into the shims and look for the report context GUID
            var vsTextBuffer = _vsEditorAdaptersFactoryService.GetBufferAdapter(textView.TextBuffer);
            if (vsTextBuffer == null)
            {
                return false;
            }

            var vsUserData = vsTextBuffer as IVsUserData;
            if (vsUserData == null)
            {
                return false;
            }

            try
            {
                var guid = ReportContextGuid;
                object data;
                return ErrorHandler.Succeeded(vsUserData.GetData(ref guid, out data)) && data != null;
            }
            catch
            {
                return false;
            }
        }

        private bool IsSpecialHandled(KeyInput keyInput)
        {
            return s_specialHandledSet.Contains(keyInput);
        }

        #region IReportDesignerUtil

        bool IReportDesignerUtil.IsExpressionView(ITextView textView)
        {
            return IsExpressionView(textView);
        }

        bool IReportDesignerUtil.IsSpecialHandled(KeyInput keyInput)
        {
            return IsSpecialHandled(keyInput);
        }

        #endregion
    }
}
