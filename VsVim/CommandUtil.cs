using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using VimCore;
using Microsoft.VisualStudio;
using Microsoft.FSharp.Core;
using System.Windows.Input;

namespace VsVim
{
    public static class CommandUtil
    {
        /// <summary>
        /// Is this a command we should immediately filter out in debug mode so that I can see what is actually 
        /// new and interesteing
        /// </summary>
        public static bool IsDebugIgnore(Guid commandGroup, uint commandId)
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

        public static bool IsDebugIgnore(VSConstants.VSStd2KCmdID commandId)
        {
            switch (commandId)
            {
                // A lot of my debugging is essentially figuring out which command is messing up
                // normal mode.  Unfortunately VS likes to throw a lot of commands all of the time.  I 
                // list them here so they don' come through my default mode where I can then set a 
                // break point
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

        public static bool IsDebugIgnore(VSConstants.VSStd97CmdID commandId)
        {
            switch (commandId)
            {
                case VSConstants.VSStd97CmdID.SolutionCfg:
                case VSConstants.VSStd97CmdID.SearchCombo:
                    return true;
            }

            return false;
        }

        public static bool TryConvert(Guid commandGroup, uint commandId, out KeyInput ki)
        {
            return TryConvert(commandGroup, commandId, IntPtr.Zero, out ki);
        }

        public static bool TryConvert(Guid commandGroup, uint commandId, IntPtr pVariableIn, out KeyInput ki)
        {
            if (VSConstants.GUID_VSStandardCommandSet97 == commandGroup)
            {
                return TryConvert((VSConstants.VSStd97CmdID)commandId, pVariableIn, out ki);
            }
            else if (VSConstants.VSStd2K == commandGroup)
            {
                return TryConvert((VSConstants.VSStd2KCmdID)commandId, pVariableIn, out ki);
            }
            else
            {
                ki = null;
            }

            ki = null;
            return false;
        }

        public static bool TryConvert(VSConstants.VSStd2KCmdID cmdId, IntPtr pVariantIn, out KeyInput ki)
        {
            ki = null;
            switch (cmdId)
            {
                case VSConstants.VSStd2KCmdID.TYPECHAR:
                    if (pVariantIn == IntPtr.Zero)
                    {
                        return false;
                    }

                    var obj = Marshal.GetObjectForNativeVariant(pVariantIn);
                    var c = (char)(ushort)obj;
                    ki = InputUtil.CharToKeyInput(c);
                    break;
                case VSConstants.VSStd2KCmdID.RETURN:
                    ki = InputUtil.KeyToKeyInput(Key.Enter);
                    break;
                case VSConstants.VSStd2KCmdID.CANCEL:
                    ki = InputUtil.KeyToKeyInput(Key.Escape);
                    break;
                case VSConstants.VSStd2KCmdID.DELETE:
                    ki = InputUtil.KeyToKeyInput(Key.Delete);
                    break;
                case VSConstants.VSStd2KCmdID.BACKSPACE:
                    ki = InputUtil.KeyToKeyInput(Key.Back);
                    break;

                default:
                    break;
            }

            return ki != null;
        }

        public static bool TryConvert(VSConstants.VSStd97CmdID cmdId, IntPtr pVariantIn, out KeyInput ki)
        {
            ki = null;
            switch (cmdId)
            {
                case VSConstants.VSStd97CmdID.SingleChar:
                    var obj = Marshal.GetObjectForNativeVariant(pVariantIn);
                    var c = (char)(ushort)obj;
                    ki = InputUtil.CharToKeyInput(c);
                    break;
                case VSConstants.VSStd97CmdID.Escape:
                    ki = InputUtil.KeyToKeyInput(Key.Escape);
                    break;
                case VSConstants.VSStd97CmdID.Delete:
                    ki = InputUtil.KeyToKeyInput(Key.Delete);
                    break;
                case VSConstants.VSStd97CmdID.FindInFiles:
                    ki = new KeyInput(Char.MinValue, Key.D, ModifierKeys.Control);
                    break;
            }

            return ki != null;
        }
    }
}
