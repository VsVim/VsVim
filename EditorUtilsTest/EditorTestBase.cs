using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.IO;
using EditorUtils.UnitTest.Utils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Outlining;
using Microsoft.VisualStudio.Utilities;
using NUnit.Framework;

namespace EditorUtils.UnitTest
{
    /// <summary>
    /// Standard test base for vim services which wish to do standard error monitoring like
    ///   - No silent swallowed MEF errors
    /// </summary>
    [TestFixture]
    public abstract class EditorTestBase
    {
        [ThreadStatic]
        private static CompositionContainer _editorUtilsCompositionContainer;

        private CompositionContainer _compositionContainer;
        private ITextBufferFactoryService _textBufferFactoryService;
        private ITextEditorFactoryService _textEditorFactoryService;
        private ISmartIndentationService _smartIndentationService;
        private IEditorOperationsFactoryService _editorOperationsFactoryService;
        private IEditorOptionsFactoryService _editorOptionsFactoryService;
        private IOutliningManagerService _outliningManagerService;
        private ITextSearchService _textSearchService;
        private ITextBufferUndoManagerProvider _textBufferUndoManagerProvider;
        private IContentTypeRegistryService _contentTypeRegistryService;
        private IAdhocOutlinerFactory _adhocOutlinerFactory;

        protected CompositionContainer CompositionContainer
        {
            get { return _compositionContainer; }
        }

        protected ISmartIndentationService SmartIndentationService
        {
            get { return _smartIndentationService; }
        }

        protected ITextEditorFactoryService TextEditorFactoryService
        {
            get { return _textEditorFactoryService; }
        }

        protected IEditorOperationsFactoryService EditorOperationsFactoryService
        {
            get { return _editorOperationsFactoryService; }
        }

        protected IEditorOptionsFactoryService EditorOptionsFactoryService
        {
            get { return _editorOptionsFactoryService; }
        }

        protected ITextSearchService TextSearchService
        {
            get { return _textSearchService; }
        }

        protected ITextBufferUndoManagerProvider TextBufferUndoManagerProvider
        {
            get { return _textBufferUndoManagerProvider; }
        }

        protected IOutliningManagerService OutliningManagerService
        {
            get { return _outliningManagerService; }
        }

        protected IContentTypeRegistryService ContentTypeRegistryService
        {
            get { return _contentTypeRegistryService; }
        }

        protected IAdhocOutlinerFactory AdhocOutlinerFactory
        {
            get { return _adhocOutlinerFactory; }
        }

        [SetUp]
        public virtual void SetupBase()
        {
            _compositionContainer = GetOrCreateCompositionContainer();
            _textBufferFactoryService = _compositionContainer.GetExportedValue<ITextBufferFactoryService>();
            _textEditorFactoryService = _compositionContainer.GetExportedValue<ITextEditorFactoryService>();
            _smartIndentationService = _compositionContainer.GetExportedValue<ISmartIndentationService>();
            _editorOperationsFactoryService = _compositionContainer.GetExportedValue<IEditorOperationsFactoryService>();
            _editorOptionsFactoryService = _compositionContainer.GetExportedValue<IEditorOptionsFactoryService>();
            _textSearchService = _compositionContainer.GetExportedValue<ITextSearchService>();
            _outliningManagerService = _compositionContainer.GetExportedValue<IOutliningManagerService>();
            _textBufferUndoManagerProvider = _compositionContainer.GetExportedValue<ITextBufferUndoManagerProvider>();
            _contentTypeRegistryService = _compositionContainer.GetExportedValue<IContentTypeRegistryService>();
            _adhocOutlinerFactory = _compositionContainer.GetExportedValue<IAdhocOutlinerFactory>();
        }

        [TearDown]
        public virtual void TearDownBase()
        {

        }

        /// <summary>
        /// Create an ITextBuffer instance with the given lines
        /// </summary>
        protected ITextBuffer CreateTextBuffer(params string[] lines)
        {
            return _textBufferFactoryService.CreateTextBuffer(lines);
        }

        /// <summary>
        /// Create an ITextView instance with the given lines
        /// </summary>
        protected IWpfTextView CreateTextView(params string[] lines)
        {
            var textBuffer = CreateTextBuffer(lines);
            return _textEditorFactoryService.CreateTextView(textBuffer);
        }

        protected IWpfTextView CreateTextView(IContentType contentType, params string[] lines)
        {
            var textBuffer = _textBufferFactoryService.CreateTextBuffer(contentType, lines);
            return _textEditorFactoryService.CreateTextView(textBuffer);
        }

        /// <summary>
        /// Get or create a content type of the specified name with the specified base content type
        /// </summary>
        protected IContentType GetOrCreateContentType(string type, string baseType)
        {
            var ct = ContentTypeRegistryService.GetContentType(type);
            if (ct == null)
            {
                ct = ContentTypeRegistryService.AddContentType(type, new[] { baseType });
            }

            return ct;
        }

        /// <summary>
        /// The MEF composition container for the current thread.  We cache all of our compositions in this
        /// container to speed up the unit tests
        /// </summary>
        protected virtual CompositionContainer GetOrCreateCompositionContainer()
        {
            if (_editorUtilsCompositionContainer == null)
            {
                var list = GetEditorUtilsCatalog();
                var catalog = new AggregateCatalog(list.ToArray());
                _editorUtilsCompositionContainer = new CompositionContainer(catalog);
            }

            return _editorUtilsCompositionContainer;
        }

        /// <summary>
        /// Get the Catalog parts which are necessary to spin up instances of the editor
        /// </summary>
        protected static List<ComposablePartCatalog> GetEditorCatalog()
        {
            var uri = new Uri(typeof(EditorTestBase).Assembly.CodeBase);
            var root = Path.GetDirectoryName(uri.LocalPath);
            var list = new List<ComposablePartCatalog>();

            list.Add(new AssemblyCatalog(Path.Combine(root, "Microsoft.VisualStudio.Platform.VSEditor.dll")));

            // Not entirely sure why this is suddenly needed
            list.Add(new AssemblyCatalog(Path.Combine(root, "Microsoft.VisualStudio.Text.Internal.dll")));

            // Must include this because several editor options are actually stored as exported information 
            // on this DLL.  Including most importantly, the tabsize information
            list.Add(new AssemblyCatalog(Path.Combine(root, "Microsoft.VisualStudio.Text.Logic.dll")));

            // Include this DLL to get several more EditorOptions including WordWrapStyle
            list.Add(new AssemblyCatalog(Path.Combine(root, "Microsoft.VisualStudio.Text.UI.dll")));

            // Include this DLL to get more EditorOptions values
            list.Add(new AssemblyCatalog(Path.Combine(root, "Microsoft.VisualStudio.Text.UI.Wpf.dll")));

            // There is no default IUndoHistoryRegistry provided so I need to provide it here just to 
            // satisfy the MEF import.  
            list.Add(new TypeCatalog(typeof(TextUndoHistoryRegistry)));

            return list;
        }

        protected static List<ComposablePartCatalog> GetEditorUtilsCatalog()
        {
            var list = GetEditorCatalog();
            list.Add(new AssemblyCatalog(typeof(ITaggerFactory).Assembly));
            return list;
        }
    }
}
