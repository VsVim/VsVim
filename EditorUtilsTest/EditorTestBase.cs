using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using EditorUtils.UnitTest.Utils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Outlining;
using Microsoft.VisualStudio.Text.Projection;
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
        private static readonly string[] s_editorComponents =
            new []
            {
                // Core editor components
                "Microsoft.VisualStudio.Platform.VSEditor.dll",

                // Not entirely sure why this is suddenly needed
                "Microsoft.VisualStudio.Text.Internal.dll",

                // Must include this because several editor options are actually stored as exported information 
                // on this DLL.  Including most importantly, the tabsize information
                "Microsoft.VisualStudio.Text.Logic.dll",

                // Include this DLL to get several more EditorOptions including WordWrapStyle
                "Microsoft.VisualStudio.Text.UI.dll",

                // Include this DLL to get more EditorOptions values
                "Microsoft.VisualStudio.Text.UI.Wpf.dll"

            };

        [ThreadStatic]
        private static CompositionContainer _editorUtilsCompositionContainer;

        private CompositionContainer _compositionContainer;
        private ITextBufferFactoryService _textBufferFactoryService;
        private ITextEditorFactoryService _textEditorFactoryService;
        private IProjectionBufferFactoryService _projectionBufferFactoryService;
        private ISmartIndentationService _smartIndentationService;
        private IEditorOperationsFactoryService _editorOperationsFactoryService;
        private IEditorOptionsFactoryService _editorOptionsFactoryService;
        private IOutliningManagerService _outliningManagerService;
        private ITextSearchService _textSearchService;
        private ITextBufferUndoManagerProvider _textBufferUndoManagerProvider;
        private IContentTypeRegistryService _contentTypeRegistryService;
        private IAdhocOutlinerFactory _adhocOutlinerFactory;
        private ITaggerFactory _taggerFactory;

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

        protected IProjectionBufferFactoryService ProjectionBufferFactoryService
        {
            get { return _projectionBufferFactoryService; }
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

        protected ITaggerFactory TaggerFactory
        {
            get { return _taggerFactory; }
        }

        [SetUp]
        public virtual void SetupBase()
        {
            _compositionContainer = GetOrCreateCompositionContainer();
            _textBufferFactoryService = _compositionContainer.GetExportedValue<ITextBufferFactoryService>();
            _textEditorFactoryService = _compositionContainer.GetExportedValue<ITextEditorFactoryService>();
            _projectionBufferFactoryService = _compositionContainer.GetExportedValue<IProjectionBufferFactoryService>();
            _smartIndentationService = _compositionContainer.GetExportedValue<ISmartIndentationService>();
            _editorOperationsFactoryService = _compositionContainer.GetExportedValue<IEditorOperationsFactoryService>();
            _editorOptionsFactoryService = _compositionContainer.GetExportedValue<IEditorOptionsFactoryService>();
            _textSearchService = _compositionContainer.GetExportedValue<ITextSearchService>();
            _outliningManagerService = _compositionContainer.GetExportedValue<IOutliningManagerService>();
            _textBufferUndoManagerProvider = _compositionContainer.GetExportedValue<ITextBufferUndoManagerProvider>();
            _contentTypeRegistryService = _compositionContainer.GetExportedValue<IContentTypeRegistryService>();
            _adhocOutlinerFactory = _compositionContainer.GetExportedValue<IAdhocOutlinerFactory>();
            _taggerFactory = _compositionContainer.GetExportedValue<ITaggerFactory>();
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
        /// Create an ITextBuffer instance with the given IContentType
        /// </summary>
        protected ITextBuffer CreateTextBuffer(IContentType contentType, params string[] lines)
        {
            return _textBufferFactoryService.CreateTextBuffer(contentType, lines);
        }

        /// <summary>
        /// Create a simple IProjectionBuffer from the specified SnapshotSpan values
        /// </summary>
        protected IProjectionBuffer CreateProjectionBuffer(params SnapshotSpan[] spans)
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
            var list = new List<ComposablePartCatalog>();
            if (!TryGetEditorCatalog(list))
            {
                var uri = new Uri(typeof(EditorTestBase).Assembly.CodeBase);
                var root = Path.GetDirectoryName(uri.LocalPath);
                var builder = new StringBuilder();
                builder.AppendLine("Could not locate the editor components.  Make sure you have run PopulateReferences.ps1");
                builder.AppendLine("Code Base: " + root);
                throw new Exception(builder.ToString());
            }

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

        private static bool TryGetEditorCatalog(List<ComposablePartCatalog> list)
        {
            return TryGetEditorCatalogFromDisk(list) || TryGetEditorCatalogFromGac(list);
        }

        private static bool TryGetEditorCatalogFromDisk(List<ComposablePartCatalog> list)
        {
            // First look for the editor components in the same directory as the unit test assembly
            var uri = new Uri(typeof(EditorTestBase).Assembly.CodeBase);
            var root = Path.GetDirectoryName(uri.LocalPath);
            if (TryGetEditorCatalogFromDirectory(root, list))
            {
                return true;
            }

            // If that didn't work walk backwards until we find the References directory and grab the
            // assemblies from there 
            root = Path.GetDirectoryName(root);
            while (root != null)
            {
                var referencesPath = Path.Combine(root, "References");
                if (Directory.Exists(referencesPath) && TryGetEditorCatalogFromDirectory(referencesPath, list))
                {
                    return true;
                }
                
                root = Path.GetDirectoryName(root);
            }

            return false;
        }

        /// <summary>
        /// Look for the editor components in the given directory.  
        /// </summary>
        private static bool TryGetEditorCatalogFromDirectory(string root, List<ComposablePartCatalog> list)
        {
            // Make sure they all exist in the given path
            var componentPaths = s_editorComponents.Select(x => Path.Combine(root, x));
            if (componentPaths.All(File.Exists))
            {
                foreach (var componentPath in componentPaths)
                {
                    list.Add(new AssemblyCatalog(componentPath));
                }

                return true;
            }

            return false;
        }

        private static bool TryGetEditorCatalogFromGac(List<ComposablePartCatalog> list)
        {
            try
            {
                foreach (var name in s_editorComponents)
                {
                    var simpleName = name.Substring(0, name.Length - 4);
                    var qualifiedName = simpleName + ", Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a, processorArchitecture=MSIL";
                    var assembly = Assembly.Load(qualifiedName);
                    list.Add(new AssemblyCatalog(assembly));
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
