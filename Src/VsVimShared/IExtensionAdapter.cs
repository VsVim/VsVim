﻿using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Vim.VisualStudio
{
    /// <summary>
    /// Interface for adding support for other extensions that interact with VsVim.  It allows
    /// them to plug into decisions which are typically Visual Studio based but can be altered 
    /// in interesting ways by extensions
    /// </summary>
    internal interface IExtensionAdapter
    {
        bool? IsUndoRedoExpected { get; }

        bool? ShouldKeepSelectionAfterHostCommand(string command, string argument);

        bool? ShouldCreateVimBuffer(ITextView textView);

        bool? IsIncrementalSearchActive(ITextView textView);
    }

    /// <summary>
    /// MEF importable interface which aggregates all of the IExtensionAdapter values in 
    /// the system
    /// </summary>
    internal interface IExtensionAdapterBroker : IExtensionAdapter
    {
        IEnumerable<IExtensionAdapter> ExtensionAdapters
        {
            get;
        }
    }
}
