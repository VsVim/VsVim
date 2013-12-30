using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TextManager.Interop;
using System.Runtime.InteropServices.ComTypes;

namespace Vim.UI.Wpf.Implementation.CommandMargin
{
    public class FontPropertiesEventArgs : EventArgs
    {
        public static readonly FontPropertiesEventArgs Empty = new FontPropertiesEventArgs();
    }

    public interface IFontProperties
    {
        FontFamily FontFamily { get; }
        double FontSize { get; }

        event EventHandler<FontPropertiesEventArgs> FontPropertiesChanged;
    }

    /// <summary>
    /// Exposes the font family and font size of the Visual Studio text editor
    /// </summary>
    internal class TextEditorFontProperties : IFontProperties
    {
        private class TextManagerEvents : IVsTextManagerEvents, IDisposable
        {
            private readonly IConnectionPoint _connectionPoint;
            private readonly int _cookie;

            public TextManagerEvents(SVsServiceProvider serviceProvider)
            {
                var textManager = (IVsTextManager)serviceProvider.GetService(typeof(SVsTextManager));
                if (textManager is IConnectionPointContainer)
                {
                    var container = (IConnectionPointContainer)textManager;
                    var eventGuid = typeof(IVsTextManagerEvents).GUID;
                    container.FindConnectionPoint(ref eventGuid, out _connectionPoint);
                    _connectionPoint.Advise(this, out _cookie);
                }
            }

            public void OnRegisterMarkerType(int iMarkerType)
            {
            }

            public void OnRegisterView(IVsTextView pView)
            {
            }

            public void OnUnregisterView(IVsTextView pView)
            {
            }

            public void OnUserPreferencesChanged(VIEWPREFERENCES[] pViewPrefs, FRAMEPREFERENCES[] pFramePrefs, LANGPREFERENCES[] pLangPrefs, FONTCOLORPREFERENCES[] pColorPrefs)
            {
                var handler = UserPreferencesChanged;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }

            public event EventHandler UserPreferencesChanged;

            public void Dispose()
            {
                if (_connectionPoint != null)
                {
                    _connectionPoint.Unadvise(_cookie);
                }
            }
        }

        private readonly SVsServiceProvider _serviceProvider;
        private readonly _DTE _dte;
        private TextManagerEvents _textManagerEvents;
        private event EventHandler<FontPropertiesEventArgs> _fontPropertiesChanged;

        internal const string CategoryFontsAndColors = "FontsAndColors";
        internal const string PageTextEditor = "TextEditor";
        internal const string PropertyFontFamily = "FontFamily";
        internal const string PropertyFontSize = "FontSize";

        public FontFamily FontFamily
        {
            get
            {
                var fontProperties = _dte.Properties[CategoryFontsAndColors, PageTextEditor];
                var fontFamily = fontProperties.Item(PropertyFontFamily).Value.ToString();
                return new FontFamily(fontFamily);
            }
        }

        public double FontSize
        {
            get
            {
                var fontProperties = _dte.Properties[CategoryFontsAndColors, PageTextEditor];
                var fontSize = Convert.ToDouble(fontProperties.Item(PropertyFontSize).Value);
                return fontSize;
            }
        }

        public event EventHandler<FontPropertiesEventArgs> FontPropertiesChanged
        {
            add
            {
                _fontPropertiesChanged += value;
                if (_fontPropertiesChanged != null && _textManagerEvents == null)
                {
                    _textManagerEvents = new TextManagerEvents(_serviceProvider);
                    _textManagerEvents.UserPreferencesChanged += OnUserPreferencesChanged;
                }
            }
            remove
            {
                _fontPropertiesChanged -= value;
                if (_fontPropertiesChanged == null && _textManagerEvents != null)
                {
                    _textManagerEvents.Dispose();
                    _textManagerEvents = null;
                }
            }
        }

        public TextEditorFontProperties(SVsServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _dte = (_DTE)_serviceProvider.GetService(typeof(_DTE));
            _textManagerEvents = null;
        }

        private void OnUserPreferencesChanged(object sender, EventArgs e)
        {
            var handler = _fontPropertiesChanged;
            if (handler != null)
            {
                handler(this, FontPropertiesEventArgs.Empty);
            }
        }
    }
}
