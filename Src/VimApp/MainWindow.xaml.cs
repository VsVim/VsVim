using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;
using System.Diagnostics;
using System.Linq;
using Vim;
using Vim.UI.Wpf;
using Vim.Extensions;
using System.Text;
using System;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Formatting;
using System.Collections.ObjectModel;
using Microsoft.Win32;
using Microsoft.VisualStudio.Utilities;
using IOPath = System.IO.Path;
using EditorUtils;

namespace VimApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly VimComponentHost _vimComponentHost;
        private readonly IClassificationFormatMapService _classificationFormatMapService;
        private readonly IVimAppOptions _vimAppOptions;
        private readonly IVimWindowManager _vimWindowManager;
        private readonly EditorHost _editorHost;

        internal IVimWindow ActiveVimWindowOpt
        {
            get
            {
                var tabItem = (TabItem)_tabControl.SelectedItem;
                if (tabItem != null)
                {
                    return _vimWindowManager.GetVimWindow(tabItem);
                }

                return null;
            }
        }

        internal IVimBuffer ActiveVimBufferOpt
        {
            get
            {
                var tabInfo = ActiveVimWindowOpt;
                var found = tabInfo.VimViewInfoList.First(x => x.TextViewHost.TextView.HasAggregateFocus);
                return found != null ? found.VimBuffer : null;
            }
        }

        internal TabControl TabControl
        {
            get { return _tabControl; }
        }

        public MainWindow()
        {
            InitializeComponent();

#if DEBUG
            VimTrace.TraceSwitch.Level = TraceLevel.Info;
#endif

            _vimComponentHost = new VimComponentHost();
            _editorHost = _vimComponentHost.EditorHost;
            _classificationFormatMapService = _vimComponentHost.CompositionContainer.GetExportedValue<IClassificationFormatMapService>();
            _vimAppOptions = _vimComponentHost.CompositionContainer.GetExportedValue<IVimAppOptions>();
            _vimWindowManager = _vimComponentHost.CompositionContainer.GetExportedValue<IVimWindowManager>();
            var vimAppHost = _vimComponentHost.CompositionContainer.GetExportedValue<VimAppHost>();
            vimAppHost.MainWindow = this;
            vimAppHost.VimWindowManager = _vimWindowManager;

            _vimWindowManager.VimWindowCreated += OnVimWindowCreated;

            // Create the initial view to display 
            AddNewTab("Empty Doc");
        }

        internal IWpfTextView CreateTextView(ITextBuffer textBuffer)
        {
            return CreateTextView(
                textBuffer,
                PredefinedTextViewRoles.PrimaryDocument,
                PredefinedTextViewRoles.Document,
                PredefinedTextViewRoles.Editable,
                PredefinedTextViewRoles.Interactive,
                PredefinedTextViewRoles.Structured,
                PredefinedTextViewRoles.Analyzable);
        }

        internal IWpfTextView CreateTextView(ITextBuffer textBuffer, params string[] roles)
        {
            var textViewRoleSet = _vimComponentHost.EditorHost.TextEditorFactoryService.CreateTextViewRoleSet(roles);
            var textView =  _vimComponentHost.EditorHost.TextEditorFactoryService.CreateTextView(
                textBuffer,
                textViewRoleSet);

            textView.GotAggregateFocus += delegate
            {
                var hasFocus = textView.HasAggregateFocus;
                
            };
            return textView;    
        }

        /// <summary>
        /// Create an ITextViewHost instance for the active ITextBuffer
        /// </summary>
        internal IWpfTextViewHost CreateTextViewHost(IWpfTextView textView)
        {
            textView.Options.SetOptionValue(DefaultTextViewOptions.UseVisibleWhitespaceId, true);
            var textViewHost = _vimComponentHost.EditorHost.TextEditorFactoryService.CreateTextViewHost(textView, setFocus: true);

            var classificationFormatMap = _classificationFormatMapService.GetClassificationFormatMap(textViewHost.TextView);
            classificationFormatMap.DefaultTextProperties = TextFormattingRunProperties.CreateTextFormattingRunProperties(
                new Typeface(Constants.FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                Constants.FontSize,
                Colors.Black);

            return textViewHost;
        }

        internal void AddNewTab(string name)
        {
            var textBuffer = _vimComponentHost.EditorHost.TextBufferFactoryService.CreateTextBuffer();
            var textView = CreateTextView(textBuffer);
            AddNewTab(name, textView);
        }

        internal void AddNewTab(string name, IWpfTextView textView)
        {
            var textViewHost = CreateTextViewHost(textView);
            var tabItem = new TabItem();
            var vimWindow = _vimWindowManager.CreateVimWindow(tabItem);
            tabItem.Header = name;
            tabItem.Content = textViewHost.HostControl;
            _tabControl.Items.Add(tabItem);

            vimWindow.AddVimViewInfo(textViewHost);
        }

        private Grid BuildGrid(ReadOnlyCollection<IVimViewInfo> viewInfoList)
        {
            Contract.Requires(viewInfoList.Count > 1);
            var grid = BuildGridCore(viewInfoList);
            var row = 0;
            for (int i = 0; i < viewInfoList.Count; i++)
            {
                var viewInfo = viewInfoList[i];
                var textViewHost = viewInfo.TextViewHost;
                var control = textViewHost.HostControl;
                control.SetValue(Grid.RowProperty, row++);
                control.SetValue(Grid.ColumnProperty, 0);
                grid.Children.Add(control);

                if (i + 1 < viewInfoList.Count)
                {
                    var splitter = new GridSplitter();
                    splitter.ResizeDirection = GridResizeDirection.Rows;
                    splitter.HorizontalAlignment = HorizontalAlignment.Stretch;
                    splitter.VerticalAlignment = VerticalAlignment.Stretch;
                    splitter.ShowsPreview = true;
                    splitter.SetValue(Grid.RowProperty, row++);
                    splitter.SetValue(Grid.ColumnProperty, 0);
                    splitter.Height = 5;
                    splitter.Background = Brushes.Black;
                    grid.Children.Add(splitter);
                }
            }

            return grid;
        }

        /// <summary>
        /// Build up the grid to contain the ITextView instances that we are splitting into
        /// </summary>
        private Grid BuildGridCore(ReadOnlyCollection<IVimViewInfo> viewInfoList)
        {
            var grid = new Grid();

            for (int i = 0; i < viewInfoList.Count; i++)
            {
                // Build up the row for the host control
                var hostRow = new RowDefinition();
                hostRow.Height = new GridLength(1, GridUnitType.Star);
                grid.RowDefinitions.Add(hostRow);

                // Build up the splitter if this isn't the last control in the list
                if (i + 1 < viewInfoList.Count)
                {
                    var splitterRow = new RowDefinition();
                    splitterRow.Height = new GridLength(0, GridUnitType.Auto);
                    grid.RowDefinitions.Add(splitterRow);
                }
            }

            return grid;
        }

        private UIElement CreateWindowContent(IVimWindow vimWindow)
        {
            var viewInfoList = vimWindow.VimViewInfoList;
            if (viewInfoList.Count == 0)
            {
                var textBlock = new TextBlock();
                textBlock.Text = "No buffer associated with this window";
                return textBlock;
            }

            if (viewInfoList.Count == 1)
            {
                return viewInfoList[0].TextViewHost.HostControl;
            }

            return BuildGrid(viewInfoList);
        }

        private void OnRunGarbageCollectorClick(object sender, EventArgs e)
        {
            for (var i = 0; i < 15; i++)
            {
                Dispatcher.DoEvents();
                GC.Collect(2, GCCollectionMode.Forced);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Forced);
                GC.Collect();
            }
        }

        private void OnVimWindowChanged(IVimWindow vimWindow)
        {
            vimWindow.TabItem.Content = null;
            foreach (var vimViewInfo in vimWindow.VimViewInfoList)
            {
                var textViewHost = vimViewInfo.TextViewHost;
                var parent = LogicalTreeHelper.GetParent(textViewHost.HostControl);

                var grid = parent as Grid;
                if (grid != null)
                {
                    grid.Children.Remove(textViewHost.HostControl);
                    continue;
                }

                var tabItem = parent as TabItem;
                if (tabItem != null)
                {
                    tabItem.Content = null;
                    continue;
                }
            }

            vimWindow.TabItem.Content = CreateWindowContent(vimWindow);
        }

        private void OnVimWindowCreated(object sender, VimWindowEventArgs e)
        {
            e.VimWindow.Changed += (sender2, e2 ) => OnVimWindowChanged(e.VimWindow);
        }

        #region Issue 1074 Helpers

        private void OnLeaderSetClick(object sender, RoutedEventArgs e)
        {
            ActiveVimBufferOpt.Process(@":let mapleader='ö'", enter: true);
            ActiveVimBufferOpt.Process(@":nmap <Leader>x ihit it<Esc>", enter: true);
        }

        private void OnLeaderTypeClick(object sender, RoutedEventArgs e)
        {
            ActiveVimBufferOpt.Process(@"ö", enter: false);
        }

        #endregion

        private void OnInsertControlCharactersClick(object sender, RoutedEventArgs e)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Begin");
            for (int i = 0; i < 32; i++)
            {
                builder.AppendFormat("{0} - {1}{2}", i, (char)i, Environment.NewLine);
            }
            builder.AppendLine("End");
            ActiveVimBufferOpt.TextBuffer.Insert(0, builder.ToString());
        }

        private void OnDisplayNewLinesChecked(object sender, RoutedEventArgs e)
        {
            _vimAppOptions.DisplayNewLines = _displayNewLinesMenuItem.IsChecked;
        }

        private void OnOpenClick(object sender, EventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.CheckFileExists = true;
            if ((bool)openFileDialog.ShowDialog(this))
            {
                // TODO: Get a real content type
                var filePath = openFileDialog.FileName;
                var fileName = IOPath.GetFileName(filePath);
                var textDocumentFactoryService = _editorHost.CompositionContainer.GetExportedValue<ITextDocumentFactoryService>();
                var textDocument = textDocumentFactoryService.CreateAndLoadTextDocument(filePath, _editorHost.ContentTypeRegistryService.GetContentType("text"));
                var wpfTextView = CreateTextView(textDocument.TextBuffer);
                AddNewTab(fileName, wpfTextView);
            }
        }

        private void OnNewTabClick(object sender, RoutedEventArgs e)
        {
            // TODO: Move the title to IVimWindow
            var name = String.Format("Empty Doc {0}", _vimWindowManager.VimWindowList.Count + 1);
            AddNewTab(name);
        }
    }
}
