using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using VsVim;
using EnvDTE;
using Microsoft.VisualStudio.UI.Undo;
using Moq;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text.Editor;
using Vim;

namespace VsVimTest
{
    [TestFixture]
    public class VsVimHostTest
    {
        private IVimHost Create(
            Microsoft.VisualStudio.OLE.Interop.IServiceProvider sp = null,
            _DTE dte = null,
            IUndoHistoryRegistry undoRegistry = null)
        {
            return CreateRaw(sp, dte, undoRegistry);
        }

        private VsVimHost CreateRaw(
            Microsoft.VisualStudio.OLE.Interop.IServiceProvider sp = null,
            _DTE dte = null,
            IUndoHistoryRegistry undoRegistry = null)
        {
            sp = sp ?? (new Mock<Microsoft.VisualStudio.OLE.Interop.IServiceProvider>(MockBehavior.Strict)).Object;
            dte = dte ?? (new Mock<_DTE>(MockBehavior.Strict)).Object;
            undoRegistry = undoRegistry ?? (new Mock<IUndoHistoryRegistry>(MockBehavior.Strict)).Object;
            return new VsVimHost(sp, undoRegistry, dte);
        }

    }
}
