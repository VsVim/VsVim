using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System.Diagnostics;
using Vim;

namespace VimTestApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly VimComponentHost _vimComponentHost;
        private readonly List<IWpfTextViewHost> _textViewHostList = new List<IWpfTextViewHost>();
        private readonly ITextBuffer _textBuffer;

        public MainWindow()
        {
            InitializeComponent();

#if DEBUG
            VimTrace.TraceSwitch.Level = TraceLevel.Info;
#endif

            _vimComponentHost = new VimComponentHost();
            _vimComponentHost.CompositionContainer.GetExportedValue<DefaultVimHost>().MainWindow = this;

            _textBuffer = _vimComponentHost.TextBufferFactoryService.CreateTextBuffer();
            var textViewHost = CreateTextViewHost();
            _textViewHostList.Add(textViewHost);
            m_dockPanel.Children.Add(textViewHost.HostControl);
        }

        /// <summary>
        /// Create an ITextViewHost instance for the active ITextBuffer
        /// </summary>
        private IWpfTextViewHost CreateTextViewHost()
        {
            var textViewRoleSet = _vimComponentHost.TextEditorFactoryService.CreateTextViewRoleSet(
                PredefinedTextViewRoles.PrimaryDocument,
                PredefinedTextViewRoles.Document,
                PredefinedTextViewRoles.Editable,
                PredefinedTextViewRoles.Interactive,
                PredefinedTextViewRoles.Structured,
                PredefinedTextViewRoles.Analyzable);
            var textView = _vimComponentHost.TextEditorFactoryService.CreateTextView(
                _textBuffer,
                textViewRoleSet);
            return _vimComponentHost.TextEditorFactoryService.CreateTextViewHost(textView, true);
        }

        internal void SplitViewHorizontally(ITextView textView)
        {
            m_dockPanel.Children.Clear();
            _textViewHostList.Add(CreateTextViewHost());

            var grid = BuildGrid();
            var row = 0;
            for (int i = 0; i < _textViewHostList.Count; i++)
            {
                var textViewHost = _textViewHostList[i];
                var control = textViewHost.HostControl;
                control.SetValue(Grid.RowProperty, row++);
                control.SetValue(Grid.ColumnProperty, 0);
                grid.Children.Add(control);

                if (i + 1 < _textViewHostList.Count)
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

            m_dockPanel.Children.Add(grid);
        }

        /// <summary>
        /// Build up the grid to contain the ITextView instances that we are splitting into
        /// </summary>
        internal Grid BuildGrid()
        {
            var grid = new Grid();

            for (int i = 0; i < _textViewHostList.Count; i++)
            {
                // Build up the row for the host control
                var hostRow = new RowDefinition();
                hostRow.Height = new GridLength(1, GridUnitType.Star);
                grid.RowDefinitions.Add(hostRow);

                // Build up the splitter if this isn't the last control in the list
                if (i + 1 < _textViewHostList.Count)
                {
                    var splitterRow = new RowDefinition();
                    splitterRow.Height = new GridLength(0, GridUnitType.Auto);
                    grid.RowDefinitions.Add(splitterRow);
                }
            }

            return grid;
        }
    }
}
