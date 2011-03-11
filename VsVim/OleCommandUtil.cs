using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Vim;
using Vim.Extensions;

namespace VsVim
{
    internal static class OleCommandUtil
    {
        /// <summary>
        /// Is this a command we should immediately filter out in debug mode so that I can see what is actually 
        /// new and interesteing
        /// </summary>
        internal static bool IsDebugIgnore(Guid commandGroup, uint commandId)
        {
            if (VSConstants.VSStd2K == commandGroup)
            {
                return IsDebugIgnore((VSConstants.VSStd2KCmdID)commandId);
            }
            else if (VSConstants.GUID_VSStandardCommandSet97 == commandGroup)
            {
                return IsDebugIgnore((VSConstants.VSStd97CmdID)commandId);
            }

            return false;
        }

        internal static bool IsDebugIgnore(VSConstants.VSStd2KCmdID commandId)
        {
            switch (commandId)
            {
                // A lot of my debugging is essentially figuring out which command is messing up normal mode.
                // Unfortunately VS likes to throw a lot of commands all of the time.  I list them here so they don't
                // come through my default mode where I can then set a break point
                case VSConstants.VSStd2KCmdID.SolutionPlatform:
                case VSConstants.VSStd2KCmdID.FILESYSTEMEDITOR:
                case VSConstants.VSStd2KCmdID.REGISTRYEDITOR:
                case VSConstants.VSStd2KCmdID.FILETYPESEDITOR:
                case VSConstants.VSStd2KCmdID.USERINTERFACEEDITOR:
                case VSConstants.VSStd2KCmdID.CUSTOMACTIONSEDITOR:
                case VSConstants.VSStd2KCmdID.LAUNCHCONDITIONSEDITOR:
                case VSConstants.VSStd2KCmdID.EDITOR:
                case VSConstants.VSStd2KCmdID.VIEWDEPENDENCIES:
                case VSConstants.VSStd2KCmdID.VIEWFILTER:
                case VSConstants.VSStd2KCmdID.VIEWOUTPUTS:
                case VSConstants.VSStd2KCmdID.RENAME:
                case VSConstants.VSStd2KCmdID.ADDOUTPUT:
                case VSConstants.VSStd2KCmdID.ADDFILE:
                case VSConstants.VSStd2KCmdID.MERGEMODULE:
                case VSConstants.VSStd2KCmdID.ADDCOMPONENTS:
                case VSConstants.VSStd2KCmdID.ADDWFCFORM:
                    return true;
            }

            return false;
        }

        internal static bool IsDebugIgnore(VSConstants.VSStd97CmdID commandId)
        {
            switch (commandId)
            {
                case VSConstants.VSStd97CmdID.SolutionCfg:
                case VSConstants.VSStd97CmdID.SearchCombo:
                    return true;
            }

            return false;
        }

        internal static bool TryConvert(Guid commandGroup, uint commandId, out EditCommand command)
        {
            return TryConvert(commandGroup, commandId, IntPtr.Zero, out command);
        }

        internal static bool TryConvert(Guid commandGroup, uint commandId, out KeyInput ki, out EditCommandKind kind)
        {
            return TryConvert(commandGroup, commandId, IntPtr.Zero, out ki, out kind);
        }

        internal static bool TryConvert(Guid commandGroup, uint commandId, IntPtr pVariableIn, out EditCommand command)
        {
            KeyInput ki;
            EditCommandKind kind;
            if (!TryConvert(commandGroup, commandId, pVariableIn, out ki, out kind))
            {
                command = null;
                return false;
            }

            command = new EditCommand(ki, kind, commandGroup, commandId);
            return true;
        }

        internal static bool TryConvert(Guid commandGroup, OleCommandData oleCommandData, out KeyInput keyInput)
        {
            EditCommandKind editCommandKind;
            return TryConvert(commandGroup, oleCommandData.CommandId, oleCommandData.VariantIn, out keyInput, out editCommandKind);
        }

        internal static bool TryConvert(Guid commandGroup, uint commandId, IntPtr pVariableIn, out KeyInput ki, out EditCommandKind kind)
        {
            if (VSConstants.GUID_VSStandardCommandSet97 == commandGroup)
            {
                return TryConvert((VSConstants.VSStd97CmdID)commandId, pVariableIn, out ki, out kind);
            }
            else if (VSConstants.VSStd2K == commandGroup)
            {
                return TryConvert((VSConstants.VSStd2KCmdID)commandId, pVariableIn, out ki, out kind);
            }
            else
            {
                ki = null;
                kind = EditCommandKind.Unknown;
                return false;
            }
        }

        internal static bool TryConvert(VSConstants.VSStd2KCmdID cmdId, IntPtr pVariantIn, out KeyInput ki, out EditCommandKind kind)
        {
            kind = EditCommandKind.Unknown;
            ki = null;
            switch (cmdId)
            {
                case VSConstants.VSStd2KCmdID.TYPECHAR:
                    if (pVariantIn == IntPtr.Zero)
                    {
                        ki = KeyInputUtil.CharToKeyInput(Char.MinValue);
                    }
                    else
                    {
                        var obj = Marshal.GetObjectForNativeVariant(pVariantIn);
                        var c = (char)(ushort)obj;
                        ki = KeyInputUtil.CharToKeyInput(c);
                    }
                    kind = EditCommandKind.TypeChar;
                    break;
                case VSConstants.VSStd2KCmdID.RETURN:
                    ki = KeyInputUtil.EnterKey;
                    kind = EditCommandKind.Return;
                    break;
                case VSConstants.VSStd2KCmdID.CANCEL:
                    ki = KeyInputUtil.EscapeKey;
                    kind = EditCommandKind.Cancel;
                    break;
                case VSConstants.VSStd2KCmdID.DELETE:
                    ki = KeyInputUtil.VimKeyToKeyInput(VimKey.Delete);
                    kind = EditCommandKind.Delete;
                    break;
                case VSConstants.VSStd2KCmdID.BACKSPACE:
                    ki = KeyInputUtil.VimKeyToKeyInput(VimKey.Back);
                    kind = EditCommandKind.Backspace;
                    break;
                case VSConstants.VSStd2KCmdID.LEFT:
                case VSConstants.VSStd2KCmdID.LEFT_EXT:
                case VSConstants.VSStd2KCmdID.LEFT_EXT_COL:
                    ki = KeyInputUtil.VimKeyToKeyInput(VimKey.Left);
                    kind = EditCommandKind.CursorMovement;
                    break;
                case VSConstants.VSStd2KCmdID.RIGHT:
                case VSConstants.VSStd2KCmdID.RIGHT_EXT:
                case VSConstants.VSStd2KCmdID.RIGHT_EXT_COL:
                    ki = KeyInputUtil.VimKeyToKeyInput(VimKey.Right);
                    kind = EditCommandKind.CursorMovement;
                    break;
                case VSConstants.VSStd2KCmdID.UP:
                case VSConstants.VSStd2KCmdID.UP_EXT:
                case VSConstants.VSStd2KCmdID.UP_EXT_COL:
                    ki = KeyInputUtil.VimKeyToKeyInput(VimKey.Up);
                    kind = EditCommandKind.CursorMovement;
                    break;
                case VSConstants.VSStd2KCmdID.DOWN:
                case VSConstants.VSStd2KCmdID.DOWN_EXT:
                case VSConstants.VSStd2KCmdID.DOWN_EXT_COL:
                    ki = KeyInputUtil.VimKeyToKeyInput(VimKey.Down);
                    kind = EditCommandKind.CursorMovement;
                    break;
                case VSConstants.VSStd2KCmdID.TAB:
                    ki = KeyInputUtil.TabKey;
                    kind = EditCommandKind.TypeChar;
                    break;
                case VSConstants.VSStd2KCmdID.PAGEDN:
                case VSConstants.VSStd2KCmdID.PAGEDN_EXT:
                    ki = KeyInputUtil.VimKeyToKeyInput(VimKey.PageDown);
                    kind = EditCommandKind.CursorMovement;
                    break;
                case VSConstants.VSStd2KCmdID.PAGEUP:
                case VSConstants.VSStd2KCmdID.PAGEUP_EXT:
                    ki = KeyInputUtil.VimKeyToKeyInput(VimKey.PageUp);
                    kind = EditCommandKind.CursorMovement;
                    break;
                default:
                    break;
            }

            return ki != null;
        }

        internal static bool TryConvert(VSConstants.VSStd97CmdID cmdId, IntPtr pVariantIn, out KeyInput ki, out EditCommandKind kind)
        {
            ki = null;
            kind = EditCommandKind.Unknown;
            switch (cmdId)
            {
                case VSConstants.VSStd97CmdID.SingleChar:
                    var obj = Marshal.GetObjectForNativeVariant(pVariantIn);
                    var c = (char)(ushort)obj;
                    ki = KeyInputUtil.CharToKeyInput(c);
                    kind = EditCommandKind.TypeChar;
                    break;
                case VSConstants.VSStd97CmdID.Escape:
                    ki = KeyInputUtil.EscapeKey;
                    kind = EditCommandKind.Cancel;
                    break;
                case VSConstants.VSStd97CmdID.Delete:
                    ki = KeyInputUtil.VimKeyToKeyInput(VimKey.Delete);
                    kind = EditCommandKind.Delete;
                    break;
                case VSConstants.VSStd97CmdID.F1Help:
                    ki = KeyInputUtil.VimKeyToKeyInput(VimKey.F1);
                    kind = EditCommandKind.Unknown;
                    break;
            }

            return ki != null;
        }

        /// <summary>
        /// Try and convert the KeyInput value into an OleCommandData instance
        /// </summary>
        internal static bool TryConvert(KeyInput keyInput, out Guid commandGroup, out OleCommandData oleCommandData)
        {
            var success = true;
            commandGroup = VSConstants.VSStd2K;
            switch (keyInput.Key)
            {
                case VimKey.Enter:
                    oleCommandData = new OleCommandData(VSConstants.VSStd2KCmdID.RETURN);
                    break;
                case VimKey.Escape:
                    oleCommandData = new OleCommandData(VSConstants.VSStd2KCmdID.CANCEL);
                    break;
                case VimKey.Delete:
                    oleCommandData = new OleCommandData(VSConstants.VSStd2KCmdID.DELETE);
                    break;
                case VimKey.Back:
                    oleCommandData = new OleCommandData(VSConstants.VSStd2KCmdID.BACKSPACE);
                    break;
                case VimKey.Up:
                    oleCommandData = new OleCommandData(VSConstants.VSStd2KCmdID.UP);
                    break;
                case VimKey.Down:
                    oleCommandData = new OleCommandData(VSConstants.VSStd2KCmdID.DOWN);
                    break;
                case VimKey.Left:
                    oleCommandData = new OleCommandData(VSConstants.VSStd2KCmdID.LEFT);
                    break;
                case VimKey.Right:
                    oleCommandData = new OleCommandData(VSConstants.VSStd2KCmdID.RIGHT);
                    break;
                case VimKey.Tab:
                    oleCommandData = new OleCommandData(VSConstants.VSStd2KCmdID.TAB);
                    break;
                case VimKey.PageUp:
                    oleCommandData = new OleCommandData(VSConstants.VSStd2KCmdID.PAGEUP);
                    break;
                case VimKey.PageDown:
                    oleCommandData = new OleCommandData(VSConstants.VSStd2KCmdID.PAGEDN);
                    break;
                default:
                    if (keyInput.RawChar.IsSome())
                    {
                        oleCommandData = OleCommandData.Allocate(keyInput.Char);
                    }
                    else
                    {
                        oleCommandData = new OleCommandData();
                        success = false;
                    }
                    break;
            }

            return success;
        }
    }
}
