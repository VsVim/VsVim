using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Vim;
using Vim.UI.Wpf;

namespace VsVim.ExternalEdit
{
    [Export(typeof(IResharperUtil))]
    [Export(typeof(IVimBufferCreationListener))]
    internal sealed class ExternalEditorManager : IResharperUtil, IVimBufferCreationListener
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
        internal List<Lazy<ITaggerProvider>> TaggerProviders { get; set; }

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
            if (_isResharperInstalled)
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
        /// Get the R# tagger for the ITextBuffer if it exists
        /// </summary>
        private Result<ITagger<ITag>> GetResharperTagger(ITextBuffer textBuffer)
        {
            Contract.Assert(_isResharperInstalled);

            // This is available as a post construction MEF import so it's very possible
            // that this is null if it's not initialized
            if (TaggerProviders == null)
            {
                return Result.Error;
            }

            // R# exposes it's ITaggerProvider instances for the "text" content type.  As much as
            // I would like to query to make sure they always support the content type we don't
            // have access to the metadata and have to hard code "text" here
            if (!textBuffer.ContentType.IsOfType("text"))
            {
                return Result.Error;
            }

            foreach (var pair in TaggerProviders)
            {
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
            return _vsShell.IsPackageInstalled(Resharper5Guid);
        }

        bool IResharperUtil.IsInstalled
        {
            get { return _isResharperInstalled; }
        }
    }
}
