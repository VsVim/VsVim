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
        /// In Visual Studio 2010 this is a hidden command that is bound to "Ctrl-;".  There is no way to 
        /// unbind this command through the UI or even at an API level.  To work around this we intercept
        /// it at the command level and translate it to Ctlr-;.  
        /// </summary>
        internal static readonly CommandId HiddenCommand = new CommandId(new Guid("{5D7E7F65-A63F-46EE-84F1-990B2CAB23F9}"), 6144);

        internal static bool TryConvert(Guid commandGroup, uint commandId, IntPtr pVariableIn, KeyModifiers modifiers, out EditCommand command)
        {
            KeyInput keyInput;
            EditCommandKind kind;
            bool isRawText;
            if (!TryConvert(commandGroup, commandId, pVariableIn, out keyInput, out kind, out isRawText))
            {
                command = null;
                return false;
            }

            // When raw text is provided it already includes the active keyboard modifiers. Don't reapply them
            // here else it can incorrectly modify the provided character.
            if (!isRawText && keyInput != KeyInput.DefaultValue)
            {
                keyInput = KeyInputUtil.ApplyModifiers(keyInput, modifiers);
            }

            command = new EditCommand(keyInput, kind, commandGroup, commandId);
            return true;
        }

        internal static bool TryConvert(OleCommandData oleCommandData, out KeyInput keyInput)
        {
            EditCommandKind editCommandKind;
            return TryConvert(oleCommandData.Group, oleCommandData.Id, oleCommandData.VariantIn, out keyInput, out editCommandKind);
        }

        internal static bool TryConvert(Guid commandGroup, uint commandId, IntPtr variantIn, out KeyInput keyInput, out EditCommandKind kind)
        {
            bool unused;
            return TryConvert(commandGroup, commandId, variantIn, out keyInput, out kind, out unused);
        }

        internal static bool TryConvert(Guid commandGroup, uint commandId, IntPtr variantIn, out KeyInput keyInput, out EditCommandKind kind, out bool isRawText)
        {
            if (VSConstants.GUID_VSStandardCommandSet97 == commandGroup)
            {
                return TryConvert((VSConstants.VSStd97CmdID)commandId, variantIn, out keyInput, out kind, out isRawText);
            }

            if (VSConstants.VSStd2K == commandGroup)
            {
                return TryConvert((VSConstants.VSStd2KCmdID)commandId, variantIn, out keyInput, out kind, out isRawText);
            }

            if (commandGroup == HiddenCommand.Group && commandId == HiddenCommand.Id)
            {
                keyInput = KeyInputUtil.CharWithControlToKeyInput(';');
                kind = EditCommandKind.UserInput;
                isRawText = true;
                return true;
            }

            keyInput = null;
            kind = EditCommandKind.UserInput;
            isRawText = false;
            return false;
        }

        /// <summary>
        /// Try and convert a Visual Studio 2000 style command into the associated KeyInput and EditCommand items
        /// </summary>
        internal static bool TryConvert(VSConstants.VSStd2KCmdID cmdId, IntPtr variantIn, out KeyInput keyInput, out EditCommandKind kind, out bool isRawText)
        {
            isRawText = false;
            switch (cmdId)
            {
                case VSConstants.VSStd2KCmdID.TYPECHAR:
                    if (variantIn == IntPtr.Zero)
                    {
                        keyInput = KeyInputUtil.CharToKeyInput(Char.MinValue);
                    }
                    else
                    {
                        var obj = Marshal.GetObjectForNativeVariant(variantIn);
                        var c = (char)(ushort)obj;
                        keyInput = KeyInputUtil.CharToKeyInput(c);
                    }
                    kind = EditCommandKind.UserInput;
                    isRawText = true;
                    break;
                case VSConstants.VSStd2KCmdID.RETURN:
                    keyInput = KeyInputUtil.EnterKey;
                    kind = EditCommandKind.UserInput;
                    break;
                case VSConstants.VSStd2KCmdID.CANCEL:
                    keyInput = KeyInputUtil.EscapeKey;
                    kind = EditCommandKind.UserInput;
                    break;
                case VSConstants.VSStd2KCmdID.DELETE:
                    keyInput = KeyInputUtil.VimKeyToKeyInput(VimKey.Delete);
                    kind = EditCommandKind.UserInput;
                    break;
                case VSConstants.VSStd2KCmdID.BACKSPACE:
                    keyInput = KeyInputUtil.VimKeyToKeyInput(VimKey.Back);
                    kind = EditCommandKind.UserInput;
                    break;
                case VSConstants.VSStd2KCmdID.LEFT:
                    keyInput = KeyInputUtil.VimKeyToKeyInput(VimKey.Left);
                    kind = EditCommandKind.UserInput;
                    break;
                case VSConstants.VSStd2KCmdID.LEFT_EXT:
                case VSConstants.VSStd2KCmdID.LEFT_EXT_COL:
                    keyInput = KeyInputUtil.ApplyModifiersToVimKey(VimKey.Left, KeyModifiers.Shift);
                    kind = EditCommandKind.VisualStudioCommand;
                    break;
                case VSConstants.VSStd2KCmdID.RIGHT:
                    keyInput = KeyInputUtil.VimKeyToKeyInput(VimKey.Right);
                    kind = EditCommandKind.UserInput;
                    break;
                case VSConstants.VSStd2KCmdID.RIGHT_EXT:
                case VSConstants.VSStd2KCmdID.RIGHT_EXT_COL:
                    keyInput = KeyInputUtil.ApplyModifiersToVimKey(VimKey.Right, KeyModifiers.Shift);
                    kind = EditCommandKind.VisualStudioCommand;
                    break;
                case VSConstants.VSStd2KCmdID.UP:
                    keyInput = KeyInputUtil.VimKeyToKeyInput(VimKey.Up);
                    kind = EditCommandKind.UserInput;
                    break;
                case VSConstants.VSStd2KCmdID.UP_EXT:
                case VSConstants.VSStd2KCmdID.UP_EXT_COL:
                    keyInput = KeyInputUtil.ApplyModifiersToVimKey(VimKey.Up, KeyModifiers.Shift);
                    kind = EditCommandKind.VisualStudioCommand;
                    break;
                case VSConstants.VSStd2KCmdID.DOWN:
                    keyInput = KeyInputUtil.VimKeyToKeyInput(VimKey.Down);
                    kind = EditCommandKind.UserInput;
                    break;
                case VSConstants.VSStd2KCmdID.DOWN_EXT:
                case VSConstants.VSStd2KCmdID.DOWN_EXT_COL:
                    keyInput = KeyInputUtil.ApplyModifiersToVimKey(VimKey.Down, KeyModifiers.Shift);
                    kind = EditCommandKind.VisualStudioCommand;
                    break;
                case VSConstants.VSStd2KCmdID.TAB:
                    keyInput = KeyInputUtil.TabKey;
                    kind = EditCommandKind.UserInput;
                    break;
                case VSConstants.VSStd2KCmdID.BACKTAB:
                    keyInput = KeyInputUtil.ApplyModifiersToVimKey(VimKey.Tab, KeyModifiers.Shift);
                    kind = EditCommandKind.UserInput;
                    break;
                case VSConstants.VSStd2KCmdID.PAGEDN:
                    keyInput = KeyInputUtil.VimKeyToKeyInput(VimKey.PageDown);
                    kind = EditCommandKind.UserInput;
                    break;
                case VSConstants.VSStd2KCmdID.PAGEDN_EXT:
                    keyInput = KeyInputUtil.ApplyModifiersToVimKey(VimKey.PageDown, KeyModifiers.Shift);
                    kind = EditCommandKind.VisualStudioCommand;
                    break;
                case VSConstants.VSStd2KCmdID.PAGEUP:
                    keyInput = KeyInputUtil.VimKeyToKeyInput(VimKey.PageUp);
                    kind = EditCommandKind.UserInput;
                    break;
                case VSConstants.VSStd2KCmdID.PAGEUP_EXT:
                    keyInput = KeyInputUtil.ApplyModifiersToVimKey(VimKey.PageUp, KeyModifiers.Shift);
                    kind = EditCommandKind.VisualStudioCommand;
                    break;
                case VSConstants.VSStd2KCmdID.UNDO:
                case VSConstants.VSStd2KCmdID.UNDONOMOVE:
                    // Visual Studio was asked to undo.  This happens when either the undo button
                    // was hit or the visual studio key combination bound to the undo command 
                    // was executed
                    keyInput = KeyInput.DefaultValue;
                    kind = EditCommandKind.Undo;
                    break;
                case VSConstants.VSStd2KCmdID.REDO:
                case VSConstants.VSStd2KCmdID.REDONOMOVE:
                    // Visual Studio was asked to redo.  This happens when either the redo button
                    // was hit or the visual studio key combination bound to the redo command 
                    // was executed
                    keyInput = KeyInput.DefaultValue;
                    kind = EditCommandKind.Redo;
                    break;
                case VSConstants.VSStd2KCmdID.BOL:
                    // Even though there as a HOME value defined, Visual Studio apparently maps the 
                    // Home key to BOL
                    keyInput = KeyInputUtil.VimKeyToKeyInput(VimKey.Home);
                    kind = EditCommandKind.UserInput;
                    break;
                case VSConstants.VSStd2KCmdID.BOL_EXT:
                case VSConstants.VSStd2KCmdID.BOL_EXT_COL:
                    keyInput = KeyInputUtil.ApplyModifiersToVimKey(VimKey.Home, KeyModifiers.Shift);
                    kind = EditCommandKind.VisualStudioCommand;
                    break;
                case VSConstants.VSStd2KCmdID.EOL:
                    // Even though there as a END value defined, Visual Studio apparently maps the 
                    // Home key to EOL
                    keyInput = KeyInputUtil.VimKeyToKeyInput(VimKey.End);
                    kind = EditCommandKind.UserInput;
                    break;
                case VSConstants.VSStd2KCmdID.EOL_EXT:
                case VSConstants.VSStd2KCmdID.EOL_EXT_COL:
                    keyInput = KeyInputUtil.ApplyModifiersToVimKey(VimKey.End, KeyModifiers.Shift);
                    kind = EditCommandKind.VisualStudioCommand;
                    break;
                case VSConstants.VSStd2KCmdID.TOGGLE_OVERTYPE_MODE:
                    // The <Insert> key is expressed in the toggle overtype mode flag.  In general
                    // over write mode is referred to as overtype in the code / documentation
                    keyInput = KeyInputUtil.VimKeyToKeyInput(VimKey.Insert);
                    kind = EditCommandKind.UserInput;
                    break;
                case VSConstants.VSStd2KCmdID.PASTE:
                    keyInput = KeyInput.DefaultValue;
                    kind = EditCommandKind.Paste;
                    break;
                case VSConstants.VSStd2KCmdID.COMMENT_BLOCK:
                case VSConstants.VSStd2KCmdID.COMMENTBLOCK:
                    keyInput = KeyInput.DefaultValue;
                    kind = EditCommandKind.Comment;
                    break;
                case VSConstants.VSStd2KCmdID.UNCOMMENT_BLOCK:
                case VSConstants.VSStd2KCmdID.UNCOMMENTBLOCK:
                    keyInput = KeyInput.DefaultValue;
                    kind = EditCommandKind.Uncomment;
                    break;
                default:
                    keyInput = null;
                    kind = EditCommandKind.UserInput;
                    break;
            }

            return keyInput != null;
        }

        /// <summary>
        /// Try and convert the Visual Studio 97 based command into KeyInput and EditCommandKind values
        /// </summary>
        internal static bool TryConvert(VSConstants.VSStd97CmdID cmdId, IntPtr variantIn, out KeyInput ki, out EditCommandKind kind, out bool isRawText)
        {
            ki = null;
            kind = EditCommandKind.UserInput;
            isRawText = false;

            switch (cmdId)
            {
                case VSConstants.VSStd97CmdID.SingleChar:
                    var obj = Marshal.GetObjectForNativeVariant(variantIn);
                    var c = (char)(ushort)obj;
                    ki = KeyInputUtil.CharToKeyInput(c);
                    kind = EditCommandKind.UserInput;
                    isRawText = true;
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
                case VSConstants.VSStd97CmdID.GotoDecl:
                case VSConstants.VSStd97CmdID.GotoDefn:
                    ki = KeyInput.DefaultValue;
                    kind = EditCommandKind.GoToDefinition;
                    break;
                case VSConstants.VSStd97CmdID.Paste:
                    ki = KeyInput.DefaultValue;
                    kind = EditCommandKind.Paste;
                    break;
            }

            return ki != null;
        }

        /// <summary>
        /// Try and convert the KeyInput into the appropriate Visual Studio command.  The conversion will be done
        /// without any consideration of Visual Studio standard commands.  It will map as if VsVim was in 
        /// complete control of key bindings
        /// </summary>
        internal static bool TryConvert(KeyInput keyInput, out OleCommandData oleCommandData)
        {
            return TryConvert(keyInput, false, out oleCommandData);
        }

        /// <summary>
        /// Try and convert the KeyInput value into an OleCommandData instance.  If simulateStandardKeyBindings is set
        /// to true then "standard" Visual Studio key bindings will be assumed and this will be reflected in the 
        /// resulting command information
        /// </summary>
        internal static bool TryConvert(KeyInput keyInput, bool simulateStandardKeyBindings, out OleCommandData oleCommandData)
        {
            var hasShift = 0 != (keyInput.KeyModifiers & KeyModifiers.Shift);
            VSConstants.VSStd2KCmdID? cmdId = null;
            switch (keyInput.Key)
            {
                case VimKey.Enter:
                    cmdId = VSConstants.VSStd2KCmdID.RETURN;
                    break;
                case VimKey.Escape:
                    cmdId = VSConstants.VSStd2KCmdID.CANCEL;
                    break;
                case VimKey.Delete:
                    cmdId = VSConstants.VSStd2KCmdID.DELETE;
                    break;
                case VimKey.Back:
                    cmdId = VSConstants.VSStd2KCmdID.BACKSPACE;
                    break;
                case VimKey.Up:
                    cmdId = simulateStandardKeyBindings && hasShift
                        ? VSConstants.VSStd2KCmdID.UP_EXT
                        : VSConstants.VSStd2KCmdID.UP;
                    break;
                case VimKey.Down:
                    cmdId = simulateStandardKeyBindings && hasShift
                        ? VSConstants.VSStd2KCmdID.DOWN_EXT
                        : VSConstants.VSStd2KCmdID.DOWN;
                    break;
                case VimKey.Left:
                    cmdId = simulateStandardKeyBindings && hasShift
                        ? VSConstants.VSStd2KCmdID.LEFT_EXT
                        : VSConstants.VSStd2KCmdID.LEFT;
                    break;
                case VimKey.Right:
                    cmdId = simulateStandardKeyBindings && hasShift
                        ? VSConstants.VSStd2KCmdID.RIGHT_EXT
                        : VSConstants.VSStd2KCmdID.RIGHT;
                    break;
                case VimKey.Tab:
                    cmdId = simulateStandardKeyBindings && hasShift
                        ? VSConstants.VSStd2KCmdID.BACKTAB
                        : VSConstants.VSStd2KCmdID.TAB;
                    break;
                case VimKey.PageUp:
                    cmdId = simulateStandardKeyBindings && hasShift
                        ? VSConstants.VSStd2KCmdID.PAGEUP_EXT
                        : VSConstants.VSStd2KCmdID.PAGEUP;
                    break;
                case VimKey.PageDown:
                    cmdId = simulateStandardKeyBindings && hasShift
                        ? VSConstants.VSStd2KCmdID.PAGEDN_EXT
                        : VSConstants.VSStd2KCmdID.PAGEDN;
                    break;
                case VimKey.Insert:
                    cmdId = VSConstants.VSStd2KCmdID.TOGGLE_OVERTYPE_MODE;
                    break;
            }

            if (cmdId.HasValue)
            {
                oleCommandData = new OleCommandData(cmdId.Value);
                return true;
            }

            if (keyInput.RawChar.IsSome())
            {
                oleCommandData = OleCommandData.CreateTypeChar(keyInput.Char);
                return true;
            }
            else
            {
                oleCommandData = OleCommandData.Empty;
                return false;
            }
        }

        internal static bool TryConvert(EditCommand editCommand, out OleCommandData oleCommandData)
        {
            switch (editCommand.EditCommandKind)
            {
                case EditCommandKind.GoToDefinition:
                    oleCommandData = new OleCommandData(VSConstants.VSStd97CmdID.GotoDecl);
                    return true;
                case EditCommandKind.Paste:
                    oleCommandData = new OleCommandData(VSConstants.VSStd2KCmdID.PASTE);
                    return true;
                case EditCommandKind.Undo:
                    oleCommandData = new OleCommandData(VSConstants.VSStd2KCmdID.UNDO);
                    return true;
                case EditCommandKind.Redo:
                    oleCommandData = new OleCommandData(VSConstants.VSStd2KCmdID.REDO);
                    return true;
                case EditCommandKind.Comment:
                    oleCommandData = new OleCommandData(VSConstants.VSStd2KCmdID.COMMENTBLOCK);
                    return true;
                case EditCommandKind.Uncomment:
                    oleCommandData = new OleCommandData(VSConstants.VSStd2KCmdID.UNCOMMENTBLOCK);
                    return true;
                case EditCommandKind.UserInput:
                    return TryConvert(editCommand.KeyInput, out oleCommandData);
                case EditCommandKind.VisualStudioCommand:
                default:
                    oleCommandData = OleCommandData.Empty;
                    return false;
            }
        }
    }
}
