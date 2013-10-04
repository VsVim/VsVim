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
using Vim.Extensions;
using System.Text;
using System;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Formatting;

namespace VimApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly VimComponentHost _vimComponentHost;
        private readonly IClassificationFormatMapService _classificationFormatMapService;
        private readonly AppInfo _appInfo = new AppInfo();

        internal TabInfo ActiveTabInfo
        {
            get
            {
                var tabItem = (TabItem)_tabControl.SelectedItem;
                return _appInfo.GetTabInfo(tabItem);
            }
        }

        internal IVimBuffer ActiveVimBuffer
        {
            get
            {
                var tabInfo = ActiveTabInfo;
                var found = tabInfo.ViewInfoList.First(x => x.TextViewHost.TextView.HasAggregateFocus);
                return found != null ? found.VimBuffer : null;
            }
        }

        public MainWindow()
        {
            InitializeComponent();

#if DEBUG
            VimTrace.TraceSwitch.Level = TraceLevel.Info;
#endif

            _vimComponentHost = new VimComponentHost();
            _vimComponentHost.CompositionContainer.GetExportedValue<VimAppHost>().MainWindow = this;
            _classificationFormatMapService = _vimComponentHost.CompositionContainer.GetExportedValue<IClassificationFormatMapService>();

            // Create the initial view to display 
            AddNewTab("Empty Doc");
        }

        private IWpfTextView CreateTextView(ITextBuffer textBuffer)
        {
            var textViewRoleSet = _vimComponentHost.TextEditorFactoryService.CreateTextViewRoleSet(
                PredefinedTextViewRoles.PrimaryDocument,
                PredefinedTextViewRoles.Document,
                PredefinedTextViewRoles.Editable,
                PredefinedTextViewRoles.Interactive,
                PredefinedTextViewRoles.Structured,
                PredefinedTextViewRoles.Analyzable);
            return _vimComponentHost.TextEditorFactoryService.CreateTextView(
                textBuffer,
                textViewRoleSet);
        }

        /// <summary>
        /// Create an ITextViewHost instance for the active ITextBuffer
        /// </summary>
        private IWpfTextViewHost CreateTextViewHost(IWpfTextView textView)
        {
            textView.Options.SetOptionValue(DefaultTextViewOptions.UseVisibleWhitespaceId, true);
            var textViewHost = _vimComponentHost.TextEditorFactoryService.CreateTextViewHost(textView, setFocus: true);

            var classificationFormatMap = _classificationFormatMapService.GetClassificationFormatMap(textViewHost.TextView);
            classificationFormatMap.DefaultTextProperties = TextFormattingRunProperties.CreateTextFormattingRunProperties(
                new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                14,
                Colors.Black);

            return textViewHost;
        }

        internal void AddNewTab(string name)
        {
            var textBuffer = _vimComponentHost.TextBufferFactoryService.CreateTextBuffer();
            var textView = CreateTextView(textBuffer);
            AddNewTab(name, textView);
        }

        private void AddNewTab(string name, IWpfTextView textView)
        {
            var textViewHost = CreateTextViewHost(textView);
            var tabItem = new TabItem();
            var tabInfo = _appInfo.GetOrCreateTabInfo(tabItem);
            tabItem.Header = name;
            tabItem.Content = textViewHost.HostControl;
            _tabControl.Items.Add(tabItem);

            var vimBuffer = _vimComponentHost.Vim.GetOrCreateVimBuffer(textView);
            tabInfo.AddViewInfo(vimBuffer, textViewHost);
        }

        internal void SplitViewHorizontally(IWpfTextView textView)
        {
            var tabInfo = ActiveTabInfo;
            var newTextViewHost = CreateTextViewHost(textView);
            var vimBuffer = _vimComponentHost.Vim.GetOrCreateVimBuffer(textView);
            tabInfo.AddViewInfo(vimBuffer, newTextViewHost);

            var viewInfoList = tabInfo.ViewInfoList.ToList();
            var grid = BuildGrid(viewInfoList);
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

            tabInfo.TabItem.Content = grid;
        }

        /// <summary>
        /// Build up the grid to contain the ITextView instances that we are splitting into
        /// </summary>
        internal Grid BuildGrid(List<ViewInfo> viewInfoList)
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

        #region Issue 1074 Helpers

        private void OnLeaderSetClick(object sender, RoutedEventArgs e)
        {
            ActiveVimBuffer.Process(@":let mapleader='ö'", enter: true);
            ActiveVimBuffer.Process(@":nmap <Leader>x ihit it<Esc>", enter: true);
        }

        private void OnLeaderTypeClick(object sender, RoutedEventArgs e)
        {
            ActiveVimBuffer.Process(@"ö", enter: false);
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
            ActiveVimBuffer.TextBuffer.Insert(0, builder.ToString());
        }

        private void OnNewTabClick(object sender, RoutedEventArgs e)
        {
            var name = String.Format("Empty Doc {0}", _appInfo.Count + 1);
            AddNewTab(name);
        }
    }
}
