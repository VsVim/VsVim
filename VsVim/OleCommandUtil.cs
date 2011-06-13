using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Vim;
using Vim.Extensions;

namespace VsVim
{
    internal static class OleCommandUtil
    {
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
                kind = EditCommandKind.UserInput;
                return false;
            }
        }

        /// <summary>
        /// Try and convert a Visual Studio 2000 style command into the associated KeyInput and EditCommand items
        /// </summary>
        internal static bool TryConvert(VSConstants.VSStd2KCmdID cmdId, IntPtr variantIn, out KeyInput ki, out EditCommandKind kind)
        {
            switch (cmdId)
            {
                case VSConstants.VSStd2KCmdID.TYPECHAR:
                    if (variantIn == IntPtr.Zero)
                    {
                        ki = KeyInputUtil.CharToKeyInput(Char.MinValue);
                    }
                    else
                    {
                        var obj = Marshal.GetObjectForNativeVariant(variantIn);
                        var c = (char)(ushort)obj;
                        ki = KeyInputUtil.CharToKeyInput(c);
                    }
                    kind = EditCommandKind.UserInput;
                    break;
                case VSConstants.VSStd2KCmdID.RETURN:
                    ki = KeyInputUtil.EnterKey;
                    kind = EditCommandKind.UserInput;
                    break;
                case VSConstants.VSStd2KCmdID.CANCEL:
                    ki = KeyInputUtil.EscapeKey;
                    kind = EditCommandKind.UserInput;
                    break;
                case VSConstants.VSStd2KCmdID.DELETE:
                    ki = KeyInputUtil.VimKeyToKeyInput(VimKey.Delete);
                    kind = EditCommandKind.UserInput;
                    break;
                case VSConstants.VSStd2KCmdID.BACKSPACE:
                    ki = KeyInputUtil.VimKeyToKeyInput(VimKey.Back);
                    kind = EditCommandKind.UserInput;
                    break;
                case VSConstants.VSStd2KCmdID.LEFT:
                case VSConstants.VSStd2KCmdID.LEFT_EXT:
                case VSConstants.VSStd2KCmdID.LEFT_EXT_COL:
                    ki = KeyInputUtil.VimKeyToKeyInput(VimKey.Left);
                    kind = EditCommandKind.UserInput;
                    break;
                case VSConstants.VSStd2KCmdID.RIGHT:
                case VSConstants.VSStd2KCmdID.RIGHT_EXT:
                case VSConstants.VSStd2KCmdID.RIGHT_EXT_COL:
                    ki = KeyInputUtil.VimKeyToKeyInput(VimKey.Right);
                    kind = EditCommandKind.UserInput;
                    break;
                case VSConstants.VSStd2KCmdID.UP:
                case VSConstants.VSStd2KCmdID.UP_EXT:
                case VSConstants.VSStd2KCmdID.UP_EXT_COL:
                    ki = KeyInputUtil.VimKeyToKeyInput(VimKey.Up);
                    kind = EditCommandKind.UserInput;
                    break;
                case VSConstants.VSStd2KCmdID.DOWN:
                case VSConstants.VSStd2KCmdID.DOWN_EXT:
                case VSConstants.VSStd2KCmdID.DOWN_EXT_COL:
                    ki = KeyInputUtil.VimKeyToKeyInput(VimKey.Down);
                    kind = EditCommandKind.UserInput;
                    break;
                case VSConstants.VSStd2KCmdID.TAB:
                    ki = KeyInputUtil.TabKey;
                    kind = EditCommandKind.UserInput;
                    break;
                case VSConstants.VSStd2KCmdID.PAGEDN:
                case VSConstants.VSStd2KCmdID.PAGEDN_EXT:
                    ki = KeyInputUtil.VimKeyToKeyInput(VimKey.PageDown);
                    kind = EditCommandKind.UserInput;
                    break;
                case VSConstants.VSStd2KCmdID.PAGEUP:
                case VSConstants.VSStd2KCmdID.PAGEUP_EXT:
                    ki = KeyInputUtil.VimKeyToKeyInput(VimKey.PageUp);
                    kind = EditCommandKind.UserInput;
                    break;
                case VSConstants.VSStd2KCmdID.UNDO:
                case VSConstants.VSStd2KCmdID.UNDONOMOVE:
                    ki = KeyInput.DefaultValue;
                    kind = EditCommandKind.Undo;
                    break;
                case VSConstants.VSStd2KCmdID.REDO:
                case VSConstants.VSStd2KCmdID.REDONOMOVE:
                    ki = KeyInput.DefaultValue;
                    kind = EditCommandKind.Redo;
                    break;
                default:
                    ki = null;
                    kind = EditCommandKind.UserInput;
                    break;
            }

            return ki != null;
        }

        /// <summary>
        /// Try and convert the Visual Studio 97 based command into KeyInput and EditCommandKind values
        /// </summary>
        internal static bool TryConvert(VSConstants.VSStd97CmdID cmdId, IntPtr variantIn, out KeyInput ki, out EditCommandKind kind)
        {
            ki = null;
            kind = EditCommandKind.UserInput;

            switch (cmdId)
            {
                case VSConstants.VSStd97CmdID.SingleChar:
                    var obj = Marshal.GetObjectForNativeVariant(variantIn);
                    var c = (char)(ushort)obj;
                    ki = KeyInputUtil.CharToKeyInput(c);
                    kind = EditCommandKind.UserInput;
                    break;
                case VSConstants.VSStd97CmdID.Escape:
                    ki = KeyInputUtil.EscapeKey;
                    kind = EditCommandKind.UserInput;
                    break;
                case VSConstants.VSStd97CmdID.Delete:
                    ki = KeyInputUtil.VimKeyToKeyInput(VimKey.Delete);
                    kind = EditCommandKind.UserInput;
                    break;
                case VSConstants.VSStd97CmdID.F1Help:
                    ki = KeyInputUtil.VimKeyToKeyInput(VimKey.F1);
                    kind = EditCommandKind.UserInput;
                    break;
                case VSConstants.VSStd97CmdID.Undo:
                    ki = KeyInput.DefaultValue;
                    kind = EditCommandKind.Undo;
                    break;
                case VSConstants.VSStd97CmdID.Redo:
                    ki = KeyInput.DefaultValue;
                    kind = EditCommandKind.Redo;
                    break;
                case VSConstants.VSStd97CmdID.MultiLevelUndo:
                    // This occurs when the undo button is pressed.  If it's just simply pressed we get 
                    // a IntPtr.Zero 'variantIn' value and can proceed with Vim undo.  Else user selected
                    // a very specific undo point and we shouldn't mess with it
                    if (variantIn == IntPtr.Zero)
                    {
                        ki = KeyInput.DefaultValue;
                        kind = EditCommandKind.Undo;
                    }
                    break;
                case VSConstants.VSStd97CmdID.MultiLevelRedo:
                    // This occurs when the redo button is pressed.  If it's just simply pressed we get 
                    // a IntPtr.Zero 'variantIn' value and can proceed with Vim redo .  Else user selected
                    // a very specific redo point and we shouldn't mess with it
                    if (variantIn == IntPtr.Zero)
                    {
                        ki = KeyInput.DefaultValue;
                        kind = EditCommandKind.Redo;
                    }
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
