using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using Vim.Extensions;
using EditorUtils;

namespace Vim.UI.Wpf.Implementation.Directory
{
    [Export(typeof(IDirectoryUtil))]
    internal sealed class DirectoryUtil : IDirectoryUtil
    {
        private static readonly object NameKey = new object();

        private readonly IFileSystem _fileSystem;
        private readonly IContentTypeRegistryService _contentTypeRegistryService;
        private readonly ITextBufferFactoryService _textBufferFactoryService;

        [ImportingConstructor]
        internal DirectoryUtil(IFileSystem fileSystem, IContentTypeRegistryService contentTypeRegistryService, ITextBufferFactoryService textBufferFactoryService)
        {
            _fileSystem = fileSystem;
            _contentTypeRegistryService = contentTypeRegistryService;
            _textBufferFactoryService = textBufferFactoryService;
        }

        internal static bool TryGetDirectorySpan(ITextSnapshotLine line, out SnapshotSpan span)
        {
            var snapshot = line.Snapshot;
            if (line.Length > 0 && snapshot[line.End.Position - 1] == '/')
            {
                span = new SnapshotSpan(line.Start, line.Length - 1);
                return true;
            }

            span = default(SnapshotSpan);
            return false;
        }

        private bool TryCreateDirectoryTextBuffer(string directoryPath, out ITextBuffer textBuffer)
        {
            var contents = _fileSystem.ReadDirectoryContents(directoryPath);
            if (contents.IsNone())
            {
                textBuffer = null;
                return false;
            }

            var contentType = _contentTypeRegistryService.GetContentType(DirectoryContentType.Name);
            var text = string.Join(Environment.NewLine, contents.Value);
            textBuffer = _textBufferFactoryService.CreateTextBuffer(text, contentType);
            textBuffer.Properties.AddProperty(NameKey, directoryPath);
            return true;
        }

        private string GetDirectoryPath(ITextBuffer textBuffer)
        {
            string name = "";
            if (!textBuffer.Properties.TryGetPropertySafe(NameKey, out name))
            {
                name = null;
            }

            return name;
        }

        #region IDirectoryUtil

        string IDirectoryUtil.GetDirectoryPath(ITextBuffer textBuffer)
        {
            return GetDirectoryPath(textBuffer);
        }

        bool IDirectoryUtil.TryCreateDirectoryTextBuffer(string directoryPath, out ITextBuffer textBuffer)
        {
            return TryCreateDirectoryTextBuffer(directoryPath, out textBuffer);
        }

        #endregion
    }
}
