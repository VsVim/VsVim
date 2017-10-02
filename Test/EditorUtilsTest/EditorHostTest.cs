using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Outlining;
using Microsoft.VisualStudio.Text.Projection;
using Microsoft.VisualStudio.Utilities;
using System.Reflection;

namespace EditorUtils.UnitTest
{
    public abstract class EditorHostTest : IDisposable
    {
        private static EditorHost EditorHostCache;

        private readonly EditorHost _editorHost;
        private readonly TestableSynchronizationContext _synchronizationContext;

        public EditorHost EditorHost
        {
            get { return _editorHost; }
        }

        public ISmartIndentationService SmartIndentationService
        {
            get { return _editorHost.SmartIndentationService; }
        }

        public ITextBufferFactoryService TextBufferFactoryService
        {
            get { return _editorHost.TextBufferFactoryService; }
        }

        public ITextEditorFactoryService TextEditorFactoryService
        {
            get { return _editorHost.TextEditorFactoryService; }
        }

        public IProjectionBufferFactoryService ProjectionBufferFactoryService
        {
            get { return _editorHost.ProjectionBufferFactoryService; }
        }

        public IEditorOperationsFactoryService EditorOperationsFactoryService
        {
            get { return _editorHost.EditorOperationsFactoryService; }
        }

        public IEditorOptionsFactoryService EditorOptionsFactoryService
        {
            get { return _editorHost.EditorOptionsFactoryService; }
        }

        public ITextSearchService TextSearchService
        {
            get { return _editorHost.TextSearchService; }
        }

        public ITextBufferUndoManagerProvider TextBufferUndoManagerProvider
        {
            get { return _editorHost.TextBufferUndoManagerProvider; }
        }

        public IOutliningManagerService OutliningManagerService
        {
            get { return _editorHost.OutliningManagerService; }
        }

        public IContentTypeRegistryService ContentTypeRegistryService
        {
            get { return _editorHost.ContentTypeRegistryService; }
        }

        public IProtectedOperations ProtectedOperations
        {
            get { return _editorHost.ProtectedOperations; }
        }

        public IBasicUndoHistoryRegistry BasicUndoHistoryRegistry
        {
            get { return _editorHost.BasicUndoHistoryRegistry; }
        }

        public TestableSynchronizationContext TestableSynchronizationContext
        {
            get { return _synchronizationContext; }
        }

        public EditorHostTest()
        {
            try
            {
                _editorHost = GetOrCreateEditorHost();
            }
            catch (ReflectionTypeLoadException e)
            {
                // When this fails in AppVeyor the error message is useless.  Need to construct a more actionable
                // error message here. 
                var builder = new StringBuilder();
                builder.AppendLine(e.Message);
                foreach (var item in e.LoaderExceptions)
                {
                    builder.AppendLine(item.Message);
                }
                throw new Exception(builder.ToString(), e);
            }
            _synchronizationContext = new TestableSynchronizationContext();
            _synchronizationContext.Install();
        }

        protected virtual void Dispose()
        {
            _synchronizationContext.Uninstall();
        }

        private EditorHost GetOrCreateEditorHost()
        {
            if (EditorHostCache != null)
            {
                return EditorHostCache;
            }

            var editorHostFactory = new EditorHostFactory();
            EditorHostCache = editorHostFactory.CreateEditorHost();
            return EditorHostCache;
        }

        public ITextBuffer CreateTextBuffer(params string[] lines)
        {
            return _editorHost.CreateTextBuffer(lines);
        }

        public ITextBuffer CreateTextBuffer(IContentType contentType, params string[] lines)
        {
            return _editorHost.CreateTextBuffer(contentType, lines);
        }

        public IProjectionBuffer CreateProjectionBuffer(params SnapshotSpan[] spans)
        {
            return _editorHost.CreateProjectionBuffer(spans);
        }

        public IWpfTextView CreateTextView(params string[] lines)
        {
            return _editorHost.CreateTextView(lines);
        }

        public IWpfTextView CreateTextView(IContentType contentType, params string[] lines)
        {
            return _editorHost.CreateTextView(contentType, lines);
        }

        /// <summary>
        /// Get or create a content type of the specified name with the specified base content type
        /// </summary>
        public IContentType GetOrCreateContentType(string type, string baseType)
        {
            return _editorHost.GetOrCreateContentType(type, baseType);
        }

        void IDisposable.Dispose()
        {
            Dispose();
        }
    }
}
