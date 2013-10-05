using EditorUtils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace VimApp.Implementation.NewLineDisplay
{
    /// <summary>
    /// Adds adornments to make new lines visible in the editor
    /// </summary>
    internal sealed class NewLineDisplay
    {
        private static readonly ReadOnlyCollection<ITagSpan<IntraTextAdornmentTag>> EmptyTagColllection = new ReadOnlyCollection<ITagSpan<IntraTextAdornmentTag>>(new List<ITagSpan<IntraTextAdornmentTag>>());
        private readonly IWpfTextView _wpfTextView;
        private readonly IAdornmentLayer _adornmentLayer;
        private readonly IVimAppOptions _vimAppOptions;

        internal NewLineDisplay(IWpfTextView textView, IAdornmentLayer adornmentLayer, IVimAppOptions vimAppOptions)
        {
            _wpfTextView = textView;
            _adornmentLayer = adornmentLayer;
            _vimAppOptions = vimAppOptions;

            _wpfTextView.LayoutChanged += OnLayoutChanged;
            _vimAppOptions.Changed += OnOptionsChanged;
        }

        private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            if (_vimAppOptions.DisplayNewLines)
            {
                CreateVisuals();
            }
        }

        private void OnOptionsChanged(object sender, EventArgs e)
        {
            if (_vimAppOptions.DisplayNewLines)
            {
                CreateVisuals();
            }
            else
            {
                _adornmentLayer.RemoveAllAdornments();
            }
        }

        private static string GetLineBreakText(ITextSnapshot snapshot, int position)
        {
            if (position >= snapshot.Length)
            {
                return null;
            }

            switch (snapshot[position])
            {
                case '\r':
                    if (position + 1 < snapshot.Length && '\n' == snapshot[position + 1])
                    {
                        return @"\r\n";
                    }

                    return @"\r";
                case '\n':
                    return @"\n";
                case '\u2028':
                    return @"\u2028";
                case '\u2029':
                    return @"\u2029";
                default:
                    if (snapshot[position] == (char)85)
                    {
                        return "(char)85";
                    }

                    return null;
            }
        }

        private UIElement CreateAdornment(string text)
        {
            var textBox = new TextBox();
            textBox.Text = text;
            textBox.BorderThickness = new Thickness(1);
            textBox.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            textBox.Foreground = Brushes.Black;
            textBox.FontWeight = FontWeights.Bold;
            return textBox;
        }

        private void CreateVisuals()
        {
            try
            {
                CreateVisuals(_wpfTextView.TextViewLines);
            }
            catch (Exception)
            {

            }
        }

        private void CreateVisuals(IWpfTextViewLineCollection textViewLines)
        {
            foreach (var textViewLine in textViewLines)
            {
                var span = new SnapshotSpan(textViewLine.End, textViewLine.EndIncludingLineBreak);
                if (span.Length == 0)
                {
                    continue;
                }

                var text = GetLineBreakText(span.Snapshot, span.Start.Position);
                if (text == null)
                {
                    continue;
                }
                
                var adornment = CreateAdornment(text);
                Geometry geometry = textViewLines.GetMarkerGeometry(span);
                Canvas.SetLeft(adornment, geometry.Bounds.Left);
                Canvas.SetTop(adornment, geometry.Bounds.Top);

                _adornmentLayer.AddAdornment(AdornmentPositioningBehavior.TextRelative, span, null, adornment, null);
            }
        }
    }
}
