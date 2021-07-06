using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text.Classification;
using Vim.UnitTest.Mock;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Projection;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Outlining;
using Microsoft.VisualStudio.Utilities;
using Vim.UI.Wpf;

namespace Vim.EditorHost
{
    /// <summary>
    /// Base class for hosting editor components.  This is primarily used for unit 
    /// testing. Any test base can derive from this and use the Create* methods to get
    /// ITextBuffer instances to run their tests against. 
    /// </summary>
    public sealed class VimEditorHost 
    {
        public CompositionContainer CompositionContainer { get; }
        public ISmartIndentationService SmartIndentationService { get; }
        public ITextBufferFactoryService TextBufferFactoryService { get; }
        public ITextEditorFactoryService TextEditorFactoryService { get; }
        public IProjectionBufferFactoryService ProjectionBufferFactoryService { get; }
        public IEditorOperationsFactoryService EditorOperationsFactoryService { get; }
        public IEditorOptionsFactoryService EditorOptionsFactoryService { get; }
        public ITextSearchService TextSearchService { get; }
        public ITextBufferUndoManagerProvider TextBufferUndoManagerProvider { get; }
        public IOutliningManagerService OutliningManagerService { get; }
        public IContentTypeRegistryService ContentTypeRegistryService { get; }
        public IBasicUndoHistoryRegistry BasicUndoHistoryRegistry { get; }
        public IClassificationTypeRegistryService ClassificationTypeRegistryService { get; }
        public IVim Vim { get; }
        internal IVimBufferFactory VimBufferFactory {get;}
        public ICommonOperationsFactory CommonOperationsFactory {get;}
        public IFoldManagerFactory FoldManagerFactory {get;}
        public IBufferTrackingService BufferTrackingService {get;}
        public IKeyUtil KeyUtil { get; }
        public IClipboardDevice ClipboardDevice {get;}
        public IMouseDevice MouseDevice {get;}
        public IKeyboardDevice KeyboardDevice {get;}
        public IProtectedOperations ProtectedOperations {get;}
        public IVimErrorDetector VimErrorDetector {get;}
        internal IBulkOperations BulkOperations {get;}
        public IEditorFormatMapService EditorFormatMapService {get;}
        public IClassificationFormatMapService ClassificationFormatMapService {get;}

        public IVimData VimData => Vim.VimData;
        public MockVimHost VimHost => (MockVimHost)Vim.VimHost;
        public IVimGlobalKeyMap GlobalKeyMap => Vim.GlobalKeyMap;

        public VimEditorHost(CompositionContainer compositionContainer)
        {
            CompositionContainer = compositionContainer;
            TextBufferFactoryService = CompositionContainer.GetExportedValue<ITextBufferFactoryService>();
            TextEditorFactoryService = CompositionContainer.GetExportedValue<ITextEditorFactoryService>();
            ProjectionBufferFactoryService = CompositionContainer.GetExportedValue<IProjectionBufferFactoryService>();
            SmartIndentationService = CompositionContainer.GetExportedValue<ISmartIndentationService>();
            EditorOperationsFactoryService = CompositionContainer.GetExportedValue<IEditorOperationsFactoryService>();
            EditorOptionsFactoryService = CompositionContainer.GetExportedValue<IEditorOptionsFactoryService>();
            TextSearchService = CompositionContainer.GetExportedValue<ITextSearchService>();
            OutliningManagerService = CompositionContainer.GetExportedValue<IOutliningManagerService>();
            TextBufferUndoManagerProvider = CompositionContainer.GetExportedValue<ITextBufferUndoManagerProvider>();
            ContentTypeRegistryService = CompositionContainer.GetExportedValue<IContentTypeRegistryService>();
            ClassificationTypeRegistryService = CompositionContainer.GetExportedValue<IClassificationTypeRegistryService>();
            BasicUndoHistoryRegistry = CompositionContainer.GetExportedValue<IBasicUndoHistoryRegistry>();
            Vim = CompositionContainer.GetExportedValue<IVim>();
            VimBufferFactory = CompositionContainer.GetExportedValue<IVimBufferFactory>();
            VimErrorDetector = CompositionContainer.GetExportedValue<IVimErrorDetector>();
            CommonOperationsFactory = CompositionContainer.GetExportedValue<ICommonOperationsFactory>();
            BufferTrackingService = CompositionContainer.GetExportedValue<IBufferTrackingService>();
            FoldManagerFactory = CompositionContainer.GetExportedValue<IFoldManagerFactory>();
            BulkOperations = CompositionContainer.GetExportedValue<IBulkOperations>();
            KeyUtil = CompositionContainer.GetExportedValue<IKeyUtil>();
            ProtectedOperations = CompositionContainer.GetExportedValue<IProtectedOperations>();
            KeyboardDevice = CompositionContainer.GetExportedValue<IKeyboardDevice>();
            MouseDevice = CompositionContainer.GetExportedValue<IMouseDevice>();
            ClipboardDevice = CompositionContainer.GetExportedValue<IClipboardDevice>();
            EditorFormatMapService = CompositionContainer.GetExportedValue<IEditorFormatMapService>();
            ClassificationFormatMapService = CompositionContainer.GetExportedValue<IClassificationFormatMapService>();
        }

        /// <summary>
        /// Create an ITextBuffer instance with the given content
        /// </summary>
        public ITextBuffer CreateTextBufferRaw(string content, IContentType contentType = null)
        {
            return TextBufferFactoryService.CreateTextBufferRaw(content, contentType);
        }

        /// <summary>
        /// Create an ITextBuffer instance with the given lines
        /// </summary>
        public ITextBuffer CreateTextBuffer(params string[] lines)
        {
            return TextBufferFactoryService.CreateTextBuffer(lines);
        }

        /// <summary>
        /// Create an ITextBuffer instance with the given IContentType
        /// </summary>
        public ITextBuffer CreateTextBuffer(IContentType contentType, params string[] lines)
        {
            return TextBufferFactoryService.CreateTextBuffer(contentType, lines);
        }

        /// <summary>
        /// Create a simple IProjectionBuffer from the specified SnapshotSpan values
        /// </summary>
        public IProjectionBuffer CreateProjectionBuffer(params SnapshotSpan[] spans)
        {
            var list = new List<object>();
            foreach (var span in spans)
            {
                var snapshot = span.Snapshot;
                var trackingSpan = snapshot.CreateTrackingSpan(span.Span, SpanTrackingMode.EdgeInclusive);
                list.Add(trackingSpan);
            }

            return ProjectionBufferFactoryService.CreateProjectionBuffer(
                null,
                list,
                ProjectionBufferOptions.None);
        }

        /// <summary>
        /// Create an ITextView instance with the given lines
        /// </summary>
        public IWpfTextView CreateTextView(params string[] lines)
        {
            var textBuffer = CreateTextBuffer(lines);
            return TextEditorFactoryService.CreateTextView(textBuffer);
        }

        public IWpfTextView CreateTextView(IContentType contentType, params string[] lines)
        {
            var textBuffer = TextBufferFactoryService.CreateTextBuffer(contentType, lines);
            return TextEditorFactoryService.CreateTextView(textBuffer);
        }

        /// <summary>
        /// Get or create a content type of the specified name with the specified base content type
        /// </summary>
        public IContentType GetOrCreateContentType(string type, string baseType)
        {
            var ct = ContentTypeRegistryService.GetContentType(type);
            if (ct == null)
            {
                ct = ContentTypeRegistryService.AddContentType(type, new[] { baseType });
            }

            return ct;
        }
    }
}
