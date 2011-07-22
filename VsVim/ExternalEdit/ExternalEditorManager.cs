using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Vim;
using Vim.UI.Wpf;

namespace VsVim.ExternalEdit
{
    [Export(typeof(IExternalEditorManager))]
    [Export(typeof(IVimBufferCreationListener))]
    internal sealed class ExternalEditorManager : IExternalEditorManager, IVimBufferCreationListener
    {
        private static readonly Guid Resharper5Guid = new Guid("0C6E6407-13FC-4878-869A-C8B4016C57FE");
        private static readonly string ResharperTaggerName = "VsDocumentMarkupTaggerProvider";

        private readonly IProtectedOperations _protectedOperations;
        private readonly IVsAdapter _vsAdapter;
        private readonly IVsShell _vsShell;
        private readonly List<IExternalEditAdapter> _adapterList = new List<IExternalEditAdapter>();
        private readonly Dictionary<IVimBuffer, ExternalEditMonitor> _monitorMap = new Dictionary<IVimBuffer, ExternalEditMonitor>();
        private readonly bool _isResharperInstalled;

        /// <summary>
        /// Need to have an Import on a property vs. a constructor parameter to break a dependency
        /// loop.
        /// </summary>
        [ImportMany(typeof(ITaggerProvider))]
        internal List<Lazy<ITaggerProvider, ITaggerMetadata>> TaggerProviders { get; set; }

        /// <summary>
        /// Is R# installed
        /// </summary>
        public bool IsResharperInstalled
        {
            get { return _isResharperInstalled; }
        }

        [ImportingConstructor]
        internal ExternalEditorManager(SVsServiceProvider serviceProvider, IVsAdapter vsAdapter, IProtectedOperations protectedOperations)
        {
            _vsAdapter = vsAdapter;
            _protectedOperations = protectedOperations;
            _vsShell = serviceProvider.GetService<SVsShell, IVsShell>();
            _adapterList.Add(new SnippetExternalEditAdapter());
            _isResharperInstalled = CheckResharperInstalled();
            if (_isResharperInstalled)
            {
                _adapterList.Add(new ResharperExternalEditAdapter());
            }
        }

        public void VimBufferCreated(IVimBuffer buffer)
        {
            Result<ITagger<ITag>> tagger = Result.Error;
            if (IsResharperInstalled)
            {
                tagger = GetResharperTagger(buffer.TextBuffer);
            }

            _monitorMap[buffer] = new ExternalEditMonitor(
                buffer,
                _protectedOperations,
                _vsAdapter.GetTextLines(buffer.TextBuffer),
                tagger,
                new ReadOnlyCollection<IExternalEditAdapter>(_adapterList));
            buffer.Closed += delegate { _monitorMap.Remove(buffer); };
        }

        /// <summary>
        /// Determine if this ITaggerProvider would be a match for our ITextBuffer based on the 
        /// metadata.  Mainly just check and see if it has the appropriate content type and 
        /// supports ITag
        /// </summary>
        private static bool IsMatch(ITextBuffer textBuffer, ITaggerMetadata metadata)
        {
            if (!textBuffer.ContentType.IsOfAnyType(metadata.ContentTypes))
            {
                return false;
            }

            foreach (var tag in metadata.TagTypes)
            {
                if (typeof(ITag).IsAssignableFrom(tag))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Get the R# tagger for the ITextBuffer if it exists
        /// </summary>
        private Result<ITagger<ITag>> GetResharperTagger(ITextBuffer textBuffer)
        {
            Contract.Assert(IsResharperInstalled);

            // This is available as a post construction MEF import so it's very possible
            // that this is null if it's not initialized
            if (TaggerProviders == null)
            {
                return Result.Error;
            }

            foreach (var pair in TaggerProviders)
            {
                if (!IsMatch(textBuffer, pair.Metadata))
                {
                    continue;
                }

                var provider = pair.Value;
                var name = provider.GetType().Name;
                if (name == ResharperTaggerName)
                {
                    var tagger = provider.SafeCreateTagger<ITag>(textBuffer);
                    if (tagger.IsSuccess)
                    {
                        return tagger;
                    }
                }
            }

            return Result.Error;
        }

        private bool CheckResharperInstalled()
        {
            var guid = Resharper5Guid;
            int isInstalled;
            return ErrorHandler.Succeeded(_vsShell.IsPackageInstalled(ref guid, out isInstalled)) && 1 == isInstalled;
        }
    }
}
