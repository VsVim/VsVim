using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vim.VisualStudio.Specific
{
#if DEBUG
    /// <summary>
    /// The version specific DLLs will change whether or not the actually depend on editor components as 
    /// our minimum version changes and features become available in the core code base. This means tests 
    /// written to verify version consistency can get out sync as references in project files won't
    /// be written into the compiled assemblies unless the types are used.
    /// 
    /// This class is present to make sure we always reference the editor binaries even as our minimum
    /// supported version changes.
    /// </summary>
    internal sealed class TestUtil
    {
        internal ITextBuffer TextBuffer { get; set; }
        internal IWpfTextView WpfTextView { get; set; }
        internal ICompletionSession CompletionSession { get; set; }

        internal ISettingsList SettinsgsList { get; set; }
    }
#endif
}
