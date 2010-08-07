using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using IServiceProvider = System.IServiceProvider;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio;

namespace VsVim
{
    /// <summary>
    /// In Debug Mode when Vim is enabled certain key strokes are incorrectly interpretted 
    /// as being edits when Vim actually processes them as commands. The only place which 
    /// is configurable is the translation of the accelerator keys.  We need to lie about
    /// the keys in debug mode so Visual Studio won't interpret them as edits
    /// </summary>
    internal sealed class VsFilterKeysAdapter : IVsFilterKeys
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IVsFilterKeys _filterKeys;

        internal VsFilterKeysAdapter(
            IServiceProvider serviceProvider,
            IVsFilterKeys filterKeys)
        {
            _serviceProvider = serviceProvider;
            _filterKeys = filterKeys;
        }
        
        public int TranslateAccelerator(MSG[] pMsg, uint dwFlags, out Guid pguidCmd, out uint pdwCmd)
        {
            switch (VsShellUtilities.GetDebugMode(_serviceProvider))
            {
                case DBGMODE.DBGMODE_Break:
                case DBGMODE.DBGMODE_Run:
                    pguidCmd = Guid.Empty;
                    pdwCmd = 0;
                    return VSConstants.E_FAIL;
                default:
                    return _filterKeys.TranslateAccelerator(pMsg, dwFlags, out pguidCmd, out pdwCmd);
            }
        }
    }
}
