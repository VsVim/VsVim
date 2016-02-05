﻿using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.ComponentModel.Design;
using Microsoft.Win32;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.ComponentModelHost;
using Vim.UI.Wpf;
using System.ComponentModel.Composition.Hosting;
using Vim;
using Vim.Extensions;
using System.Text;
using IOPath = System.IO.Path;
using EnvDTE;

namespace Vim.VisualStudio
{
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", productId: VimConstants.VersionNumber, IconResourceID = 400)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideOptionPage(typeof(Vim.VisualStudio.Implementation.OptionPages.DefaultOptionPage), categoryName: "VsVim", pageName: "Defaults", categoryResourceID: 0, pageNameResourceID: 0, supportsAutomation: true)]
    [ProvideOptionPage(typeof(Vim.VisualStudio.Implementation.OptionPages.KeyboardOptionPage), categoryName: "VsVim", pageName: "Keyboard", categoryResourceID: 0, pageNameResourceID: 0, supportsAutomation: true)]
    [Guid(GuidList.VsVimPackageString)]
    public sealed class VsVimPackage : Package, IOleCommandTarget
    {
        private IComponentModel _componentModel;
        private ExportProvider _exportProvider;
        private IVim _vim;

        public VsVimPackage()
        {
        }

        protected override void Initialize()
        {
            base.Initialize();

            _componentModel = (IComponentModel)GetService(typeof(SComponentModel));
            _exportProvider = _componentModel.DefaultExportProvider;
            _vim = _exportProvider.GetExportedValue<IVim>();
        }

        private void DumpKeyboard()
        {
            var keyBindingService = _exportProvider.GetExportedValue<IKeyBindingService>();
            var folder = IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"VsVim");
            try
            {
                Directory.CreateDirectory(folder);
            }
            catch
            {
                // Don't care if it throws, just want to make sure the folder exists
            }

            var filePath = IOPath.Combine(folder, "keyboard.txt");
            using (var streamWriter = new StreamWriter(filePath, append: false, encoding: Encoding.Unicode))
            {
                keyBindingService.DumpKeyboard(streamWriter);
            }
        }

        /// <summary>
        /// There are a set of TSQL command entries which a) have no name and b) use keystrokes 
        /// Vim users would like to use.  In general this isn't a problem becuase VsVim is 
        /// capable of removing conflicting key bindings via the options page
        /// 
        /// The problem here is the commands are unnamed.  Any attempt to change a key binding
        /// on an unnamed command (via Command::put_Bindings) will fail.  There is no good reason
        /// for this that I was able to track down, it's just the behavior.  
        /// 
        /// There is some hope this can be changed in 2015 or the next release but in the 
        /// short term I need to use a different hammer for changing the key bindings.  This method
        /// method removes the binding by doing the following
        /// 
        ///   1. Add a key binding to a VsVim command where the key binding has the same scope as the 
        ///      TSQL key bindings 
        ///   2. Remove the key bindings on the VsVim command
        ///
        /// As a consequence of adding the key binding in #1 Visual Studio will delete the key 
        /// binding on the TSQL command.  This type of deletion doesn't check for a name and hence 
        /// succeeds.  The subsequent removal of the key binding to the fake VsVim command effectively 
        /// fully clears the key binding which is what we want
        /// </summary>
        private void ClearTSQLBindings()
        {
            try
            {
                var vsServiceProvider = _exportProvider.GetExportedValue<SVsServiceProvider>();
                var dte = vsServiceProvider.GetService<SDTE, _DTE>();

                var vsvimGuidGroup = GuidList.VsVimCommandSet;
                var vimCommand = dte.Commands.GetCommands()
                    .Where(c => new Guid(c.Guid) == vsvimGuidGroup && c.ID == CommandIds.ClearTSQLBindings)
                    .FirstOrDefault();
                if (vimCommand == null)
                {
                    return;
                }

                var targetKeyStroke = new KeyStroke(KeyInputUtil.CharToKeyInput('d'), VimKeyModifiers.Control);
                var tsqlGuidGroup = new Guid("{b371c497-6d81-4b13-9db8-8e3e6abad0c3}");
                var tsqlCommands = dte.Commands.GetCommands().Where(c => new Guid(c.Guid) == tsqlGuidGroup);
                foreach (var tsqlCommand in tsqlCommands)
                {
                    foreach (var commandKeyBinding in tsqlCommand.GetCommandKeyBindings().ToList())
                    {
                        if (commandKeyBinding.KeyBinding.FirstKeyStroke == targetKeyStroke)
                        {
                            // Set the binding to the existing command in the existing scope, this will delete it from
                            // the TSQL command
                            vimCommand.SafeSetBindings(commandKeyBinding.KeyBinding);

                            // Clear the bindings here which will effectively clear the binding completely
                            vimCommand.SafeResetBindings();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Fail(ex.Message);
            }
        }

        private void ToggleEnabled()
        {
            _vim.IsDisabled = !_vim.IsDisabled;
        }

        #region IOleCommandTarget

        int IOleCommandTarget.Exec(ref Guid commandGroup, uint commandId, uint commandExecOpt, IntPtr variantIn, IntPtr variantOut)
        {
            if (commandGroup == GuidList.VsVimCommandSet)
            {
                switch (commandId)
                {
                    case CommandIds.Options:
                        ShowOptionPage(typeof(Vim.VisualStudio.Implementation.OptionPages.KeyboardOptionPage));
                        break;
                    case CommandIds.DumpKeyboard:
                        DumpKeyboard();
                        break;
                    case CommandIds.ClearTSQLBindings:
                        ClearTSQLBindings();
                        break;
                    case CommandIds.ToggleEnabled:
                        ToggleEnabled();
                        break;
                    default:
                        Debug.Assert(false);
                        break;
                }

                return VSConstants.S_OK;
            }

            return VSConstants.E_FAIL;
        }

        int IOleCommandTarget.QueryStatus(ref Guid commandGroup, uint commandsCount, OLECMD[] commands, IntPtr pCmdText)
        {
            if (commandGroup == GuidList.VsVimCommandSet && commandsCount == 1)
            {
                switch (commands[0].cmdID)
                {
                    case CommandIds.Options:
                    case CommandIds.DumpKeyboard:
                        commands[0].cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
                        break;
                    default:
                        commands[0].cmdf = 0;
                        break;
                }

                return VSConstants.S_OK;
            }

            return VSConstants.E_FAIL;
        }

        #endregion 
    }
}
