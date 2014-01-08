using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Input;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text.Editor;
using Vim;
using Vim.UI.Wpf;
using EnvDTE;

namespace VsVim.Implementation.Misc
{
    internal sealed class ForwardingKeyProcessor : KeyProcessor
    {
        private readonly _DTE _dte;
        private readonly IKeyUtil _keyUtil;
        private readonly IWpfTextView _textView;

        internal ForwardingKeyProcessor(_DTE dte, IKeyUtil keyUtil, IWpfTextView wpfTextView)
        {
            _dte = dte;
            _keyUtil = keyUtil;
            _textView = wpfTextView;
        }

        public override void KeyDown(KeyEventArgs args)
        {
            VimTrace.TraceInfo("ForwardingKeyProcessor::KeyDown {0} {1}", args.Key, args.KeyboardDevice.Modifiers);
            if (args.Key == Key.Left && args.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                args.Handled = SafeExecuteCommand("Edit.CharLeftExtend");
            }
            base.KeyDown(args);
        }

        private bool SafeExecuteCommand(string command, string args = "")
        {
            try
            {
                _dte.ExecuteCommand(command, args);
                return true;
            }
            catch
            {
                return false;
            }
        }

    }
}
