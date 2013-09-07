using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;
using System.Diagnostics;
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
        private sealed class TabInfo
        {
            internal readonly TabItem TabItem;
            internal readonly List<IWpfTextViewHost> TextViewHostList;

            internal TabInfo(TabItem tabItem, IWpfTextViewHost textViewHost)
            {
                TabItem = tabItem;
                TextViewHostList = new List<IWpfTextViewHost>();
                TextViewHostList.Add(textViewHost);
            }
        }

        private readonly VimComponentHost _vimComponentHost;
        private readonly IClassificationFormatMapService _classificationFormatMapService;
        private readonly Dictionary<ITextView, TabInfo> _textViewMap = new Dictionary<ITextView, TabInfo>();
        private readonly Dictionary<TabItem, IWpfTextView> _tabItemMap = new Dictionary<TabItem, IWpfTextView>();

        // TODO: This is hacky.  We should track the active window and use that
        private IVimBuffer ActiveVimBuffer
        {
            get
            {
                var tabItem = (TabItem)_tabControl.SelectedItem;
                var textView = _tabItemMap[tabItem];
                var tabInfo = _textViewMap[textView];
                var textViewHostList = tabInfo.TextViewHostList;
                if (textViewHostList.Count == 0)
                {
                    return null;
                }

                IVimBuffer vimBuffer;
                _vimComponentHost.Vim.TryGetVimBuffer(textViewHostList[0].TextView, out vimBuffer);
                return vimBuffer;
            }
        }

        public MainWindow()
        {
            InitializeComponent();

#if DEBUG
            VimTrace.TraceSwitch.Level = TraceLevel.Info;
#endif

            _vimComponentHost = new VimComponentHost();
            _vimComponentHost.CompositionContainer.GetExportedValue<DefaultVimHost>().MainWindow = this;
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
            var textViewHost = _vimComponentHost.TextEditorFactoryService.CreateTextViewHost(textView, true);

            var classificationFormatMap = _classificationFormatMapService.GetClassificationFormatMap(textViewHost.TextView);
            classificationFormatMap.DefaultTextProperties = TextFormattingRunProperties.CreateTextFormattingRunProperties(
                new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                14,
                Colors.Black);

            return textViewHost;
        }

        private void AddNewTab(string name)
        {
            var textBuffer = _vimComponentHost.TextBufferFactoryService.CreateTextBuffer();
            var textView = CreateTextView(textBuffer);
            AddNewTab(name, textView);
        }

        private void AddNewTab(string name, IWpfTextView textView)
        {
            var textViewHost = CreateTextViewHost(textView);
            var tabItem = new TabItem();
            tabItem.Header = name;
            tabItem.Content = textViewHost.HostControl;
            _tabControl.Items.Add(tabItem);
            _textViewMap.Add(textViewHost.TextView, new TabInfo(tabItem, textViewHost));
            _tabItemMap[tabItem] = textViewHost.TextView;
        }

        internal void SplitViewHorizontally(IWpfTextView textView)
        {
            var tabInfo = _textViewMap[textView];
            var textViewHostList = tabInfo.TextViewHostList;
            textViewHostList.Add(CreateTextViewHost(textView));
            var grid = BuildGrid(textViewHostList);
            var row = 0;
            for (int i = 0; i < textViewHostList.Count; i++)
            {
                var textViewHost = textViewHostList[i];
                var control = textViewHost.HostControl;
                control.SetValue(Grid.RowProperty, row++);
                control.SetValue(Grid.ColumnProperty, 0);
                grid.Children.Add(control);

                if (i + 1 < textViewHostList.Count)
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
        internal Grid BuildGrid(List<IWpfTextViewHost> textViewHostList)
        {
            var grid = new Grid();

            for (int i = 0; i < textViewHostList.Count; i++)
            {
                // Build up the row for the host control
                var hostRow = new RowDefinition();
                hostRow.Height = new GridLength(1, GridUnitType.Star);
                grid.RowDefinitions.Add(hostRow);

                // Build up the splitter if this isn't the last control in the list
                if (i + 1 < textViewHostList.Count)
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
            var name = String.Format("Empty Doc {0}", _tabItemMap.Count + 1);
            AddNewTab(name);
        }
    }
}
