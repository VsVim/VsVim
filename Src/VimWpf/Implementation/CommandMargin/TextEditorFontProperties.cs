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
        private class TextManagerEvents : IVsTextManagerEvents
        {
            private readonly SVsServiceProvider _serviceProvider;

            public TextManagerEvents(SVsServiceProvider serviceProvider)
            {
                _serviceProvider = serviceProvider;
                var textManager = (IVsTextManager)_serviceProvider.GetService(typeof(SVsTextManager));
                if (textManager is IConnectionPointContainer)
                {
                    var container = (IConnectionPointContainer)textManager;
                    IConnectionPoint textManagerEventsConnection;
                    var eventGuid = typeof(IVsTextManagerEvents).GUID;
                    container.FindConnectionPoint(ref eventGuid, out textManagerEventsConnection);
                    int textManagerCookie;
                    textManagerEventsConnection.Advise(this, out textManagerCookie);
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
        }

        private readonly _DTE _dte;
        private readonly TextManagerEvents _textManagerEvents;

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
                return fontSize * 1.25;
            }
        }

        public event EventHandler<FontPropertiesEventArgs> FontPropertiesChanged;

        public TextEditorFontProperties(SVsServiceProvider serviceProvider)
        {
            _dte = (_DTE)serviceProvider.GetService(typeof(_DTE));
            _textManagerEvents = new TextManagerEvents(serviceProvider);
            _textManagerEvents.UserPreferencesChanged += textManagerEvents_UserPreferencesChanged;
        }

        private void textManagerEvents_UserPreferencesChanged(object sender, EventArgs e)
        {
            var handler = FontPropertiesChanged;
            if (handler != null)
            {
                handler(this, FontPropertiesEventArgs.Empty);
            }
        }
    }
}
