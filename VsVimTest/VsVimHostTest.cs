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
        private IVimHost Create( IUndoHistoryRegistry undoRegistry = null)
        {
            return CreateRaw(undoRegistry);
        }

        private VsVimHost CreateRaw( IUndoHistoryRegistry undoRegistry = null)
        {
            undoRegistry = undoRegistry ?? (new Mock<IUndoHistoryRegistry>(MockBehavior.Strict)).Object;
            return new VsVimHost(undoRegistry);
        }

    }
}
