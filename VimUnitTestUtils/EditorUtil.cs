using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.Linq;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Outlining;
using Microsoft.VisualStudio.Utilities;
using Vim.UnitTest.Exports;
using IOPath = System.IO.Path;

namespace Vim.UnitTest
{
    /// <summary>
    /// Utility for caching and creating MEF based components for unit tests
    /// </summary>
    public static class EditorUtil
    {
        #region Factory

        public sealed class Factory
        {
            [Import]
            public ITextBufferFactoryService TextBufferFactory;

            [Import]
            public ITextEditorFactoryService TextEditorFactory;

            [Import]
            public IEditorOperationsFactoryService EditorOperationsFactory;

            [Import]
            public IEditorOptionsFactoryService EditorOptionsFactory;

            [Import]
            public IVim Vim;

            [Import]
            public IVimHost VimHost;

            [Import]
            public ITextSearchService TextSearchService;

            [Import]
            public IVimBufferFactory VimBufferFactory;

            [Import]
            public ITextBufferUndoManagerProvider UndoManagerProvider;

            [Import]
            public IContentTypeRegistryService ContentTypeRegistryService;

            [Import]
            public ITextStructureNavigatorSelectorService TextStructureNavigatorSelectorService;

            [Import]
            public ISmartIndentationService SmartIndentationService;

            [Import]
            public ICompletionBroker CompletionBroker;

            [Import]
            public IVimErrorDetector VimErrorDetector;

            [Import]
            public IWordUtilFactory WordUtilFactory;

            [Import]
            public IFoldManagerFactory FoldManagerFactory;

            [Import]
            public IOutliningManagerService OutliningManagerService;

            [Import]
            public IAdhocOutlinerFactory AdhocOutlinerFactory;
        }

        #endregion

        [ThreadStatic]
        private static CompositionContainer _compositionContainer;
        [ThreadStatic]
        private static Factory _factory;

        /// <summary>
        /// The MEF composition container for the current thread.  We cache all of our compositions in this
        /// container to speed up the unit tests
        /// </summary>
        public static CompositionContainer Container
        {
            get
            {
                if (null == _compositionContainer)
                {
                    _compositionContainer = CreateContainer();
                }
                return _compositionContainer;
            }
        }

        /// <summary>
        /// Factory service for gaining access to our MEF types
        /// </summary>
        public static Factory FactoryService
        {
            get
            {
                if (null == _factory)
                {
                    _factory = new Factory();
                    Container.ComposeParts(_factory);
                }
                return _factory;
            }
        }

        /// <summary>
        /// Create an ITextBuffer instance with the given lines of code
        /// </summary>
        public static ITextBuffer CreateTextBuffer(params string[] lines)
        {
            return CreateTextBuffer(null, lines);
        }

        /// <summary>
        /// Create an ITextBuffer instance with the given lines of code
        /// </summary>
        public static ITextBuffer CreateTextBuffer(IContentType contentType, params string[] lines)
        {
            var factory = FactoryService.TextBufferFactory;
            var buffer = contentType != null
                ? factory.CreateTextBuffer(contentType)
                : factory.CreateTextBuffer();

            if (lines.Length != 0)
            {
                var text = CreateLines(lines);
                buffer.Replace(new Span(0, 0), text);
            }

            return buffer;
        }

        /// <summary>
        /// Create an ITextView / ITextBuffer pair with the specified lines
        /// </summary>
        public static IWpfTextView CreateTextView(params string[] lines)
        {
            return CreateTextView(null, lines);
        }

        /// <summary>
        /// Create an ITextView / ITextBuffer pair with the specified lines and content type
        /// </summary>
        public static IWpfTextView CreateTextView(IContentType contentType, params string[] lines)
        {
            var buffer = CreateTextBuffer(contentType, lines);
            var view = FactoryService.TextEditorFactory.CreateTextView(buffer);
            return view;
        }

        /// <summary>
        /// Create an ITextView / IEditorOperations pair with the specified lines and content type
        /// </summary>
        public static Tuple<IWpfTextView, IEditorOperations> CreateTextViewAndEditorOperations(params string[] lines)
        {
            var view = CreateTextView(lines);
            var opts = FactoryService.EditorOperationsFactory.GetEditorOperations(view);
            return Tuple.Create(view, opts);
        }

        /// <summary>
        /// Create a single string that is the combination of the provided strings and a new
        /// line between each
        /// </summary>
        public static string CreateLines(params string[] lines)
        {
            return lines.Aggregate((x, y) => x + Environment.NewLine + y);
        }

        /// <summary>
        /// Create a single string that is the combination of the provided strings and a new
        /// line between each and at the end
        /// </summary>
        public static string CreateLinesWithLineBreak(params string[] lines)
        {
            return CreateLines(lines) + Environment.NewLine;
        }

        public static IEditorOperations GetEditorOperations(ITextView view)
        {
            return FactoryService.EditorOperationsFactory.GetEditorOperations(view);
        }

        public static IEditorOptions GetEditorOptions(ITextView textView)
        {
            return FactoryService.EditorOptionsFactory.GetOptions(textView);
        }

        public static ITextUndoHistory GetUndoHistory(ITextBuffer textBuffer)
        {
            return FactoryService.UndoManagerProvider.GetTextBufferUndoManager(textBuffer).TextBufferUndoHistory;
        }

        /// <summary>
        /// Get or create a content type of the specified name with the specified base content type
        /// </summary>
        public static IContentType GetOrCreateContentType(string type, string baseType)
        {
            var ct = FactoryService.ContentTypeRegistryService.GetContentType(type);
            if (ct == null)
            {
                ct = FactoryService.ContentTypeRegistryService.AddContentType(type, new[] { baseType });
            }

            return ct;
        }

        public static List<ComposablePartCatalog> GetEditorCatalog()
        {
            var uri = new Uri(typeof(EditorUtil).Assembly.CodeBase);
            var root = IOPath.GetDirectoryName(uri.LocalPath);
            var list = new List<ComposablePartCatalog>();
            list.Add(new AssemblyCatalog(IOPath.Combine(root, "Microsoft.VisualStudio.Platform.VSEditor.dll")));

            // Not entirely sure why this is suddenly needed
            list.Add(new AssemblyCatalog(IOPath.Combine(root, "Microsoft.VisualStudio.Text.Internal.dll")));

            // Must include this because several editor options are actually stored as exported information 
            // on this DLL.  Including most importantly, the tabsize information
            list.Add(new AssemblyCatalog(IOPath.Combine(root, "Microsoft.VisualStudio.Text.Logic.dll")));

            // Include this DLL to get several more EditorOptions including WordWrapStyle
            list.Add(new AssemblyCatalog(IOPath.Combine(root, "Microsoft.VisualStudio.Text.UI.dll")));

            // Include this DLL to get more EditorOptions values
            list.Add(new AssemblyCatalog(IOPath.Combine(root, "Microsoft.VisualStudio.Text.UI.Wpf.dll")));

            // There is no default IUndoHistoryRegistry provided so I need to provide it here just to 
            // satisfy the MEF import.  
            list.Add(new TypeCatalog(typeof(TextUndoHistoryRegistry)));

            return list;
        }

        public static List<ComposablePartCatalog> GetUnitTestCatalog()
        {
            var list = GetEditorCatalog();

            // IBlockCaret needs to be satisfied for integration tests
            list.Add(new AssemblyCatalog(typeof(IVim).Assembly));

            // Other Exports needed to construct VsVim
            list.Add(new TypeCatalog(
                typeof(ClipboardDevice),
                typeof(KeyboardDevice),
                typeof(MouseDevice),
                typeof(VimHost),
                typeof(VimErrorDetector),
                typeof(AdhocOutlinerFactory)));

            return list;
        }

        public static CompositionContainer CreateContainer()
        {
            var list = GetUnitTestCatalog();
            var catalog = new AggregateCatalog(list.ToArray());
            return new CompositionContainer(catalog);
        }
    }
}
