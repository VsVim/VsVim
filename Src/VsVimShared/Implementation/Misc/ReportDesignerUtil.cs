using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Vim;

namespace VsVim.Implementation.Misc
{
    [Export(typeof(IReportDesignerUtil))]
    internal sealed class ReportDesignerUtil : IReportDesignerUtil
    {
        internal const string RdlContentTypeName = "RDL Expression";
        internal static readonly Guid ReportContextGuid = new Guid("{8F6D573E-7AB8-4d8e-8A7A-73965B903F04}");
        private static readonly HashSet<KeyInput> SpecialHandledSet = new HashSet<KeyInput>();
        private readonly IVsEditorAdaptersFactoryService _vsEditorAdaptersFactoryService;

        static ReportDesignerUtil()
        {
            // The set of keys which are special handled are defined by the CodeWindow::ProcessKeyMessage inside
            // of Microsoft.ReportDesigner.dll
            SpecialHandledSet.Add(KeyInputUtil.EnterKey);
            SpecialHandledSet.Add(KeyInputUtil.TabKey);
            SpecialHandledSet.Add(KeyInputUtil.EscapeKey);
            SpecialHandledSet.Add(KeyInputUtil.VimKeyToKeyInput(VimKey.Delete));
            SpecialHandledSet.Add(KeyInputUtil.VimKeyToKeyInput(VimKey.Back));
            SpecialHandledSet.Add(KeyInputUtil.VimKeyToKeyInput(VimKey.PageUp));
            SpecialHandledSet.Add(KeyInputUtil.ApplyModifiersToVimKey(VimKey.PageUp, KeyModifiers.Shift));
            SpecialHandledSet.Add(KeyInputUtil.VimKeyToKeyInput(VimKey.PageDown));
            SpecialHandledSet.Add(KeyInputUtil.ApplyModifiersToVimKey(VimKey.PageDown, KeyModifiers.Shift));
            SpecialHandledSet.Add(KeyInputUtil.VimKeyToKeyInput(VimKey.End));
            SpecialHandledSet.Add(KeyInputUtil.ApplyModifiersToVimKey(VimKey.End, KeyModifiers.Shift));
            SpecialHandledSet.Add(KeyInputUtil.ApplyModifiersToVimKey(VimKey.End, KeyModifiers.Control));
            SpecialHandledSet.Add(KeyInputUtil.ApplyModifiersToVimKey(VimKey.End, KeyModifiers.Control | KeyModifiers.Shift));
            SpecialHandledSet.Add(KeyInputUtil.VimKeyToKeyInput(VimKey.Home));
            SpecialHandledSet.Add(KeyInputUtil.ApplyModifiersToVimKey(VimKey.Home, KeyModifiers.Shift));
            SpecialHandledSet.Add(KeyInputUtil.ApplyModifiersToVimKey(VimKey.Home, KeyModifiers.Control));
            SpecialHandledSet.Add(KeyInputUtil.ApplyModifiersToVimKey(VimKey.Home, KeyModifiers.Control | KeyModifiers.Shift));
            SpecialHandledSet.Add(KeyInputUtil.VimKeyToKeyInput(VimKey.Left));
            SpecialHandledSet.Add(KeyInputUtil.ApplyModifiersToVimKey(VimKey.Left, KeyModifiers.Shift));
            SpecialHandledSet.Add(KeyInputUtil.ApplyModifiersToVimKey(VimKey.Left, KeyModifiers.Control));
            SpecialHandledSet.Add(KeyInputUtil.ApplyModifiersToVimKey(VimKey.Left, KeyModifiers.Control | KeyModifiers.Shift));
            SpecialHandledSet.Add(KeyInputUtil.VimKeyToKeyInput(VimKey.Right));
            SpecialHandledSet.Add(KeyInputUtil.ApplyModifiersToVimKey(VimKey.Right, KeyModifiers.Shift));
            SpecialHandledSet.Add(KeyInputUtil.ApplyModifiersToVimKey(VimKey.Right, KeyModifiers.Control));
            SpecialHandledSet.Add(KeyInputUtil.ApplyModifiersToVimKey(VimKey.Right, KeyModifiers.Control | KeyModifiers.Shift));
            SpecialHandledSet.Add(KeyInputUtil.VimKeyToKeyInput(VimKey.Up));
            SpecialHandledSet.Add(KeyInputUtil.ApplyModifiersToVimKey(VimKey.Up, KeyModifiers.Shift));
            SpecialHandledSet.Add(KeyInputUtil.VimKeyToKeyInput(VimKey.Down));
            SpecialHandledSet.Add(KeyInputUtil.ApplyModifiersToVimKey(VimKey.Down, KeyModifiers.Shift));
            SpecialHandledSet.Add(KeyInputUtil.ApplyModifiersToChar('a', KeyModifiers.Control));
            SpecialHandledSet.Add(KeyInputUtil.ApplyModifiersToChar('v', KeyModifiers.Control));
            SpecialHandledSet.Add(KeyInputUtil.ApplyModifiersToChar('x', KeyModifiers.Control));
            SpecialHandledSet.Add(KeyInputUtil.ApplyModifiersToChar('y', KeyModifiers.Control));
            SpecialHandledSet.Add(KeyInputUtil.ApplyModifiersToChar('z', KeyModifiers.Control));
            SpecialHandledSet.Add(KeyInputUtil.ApplyModifiersToChar('z', KeyModifiers.Control | KeyModifiers.Shift));
            SpecialHandledSet.Add(KeyInputUtil.CharToKeyInput('\b'));
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
        bool IsExpressionView(ITextView textView)
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

        bool IsSpecialHandled(KeyInput keyInput)
        {
            return SpecialHandledSet.Contains(keyInput);
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
