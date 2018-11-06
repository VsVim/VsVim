using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Platform.WindowManagement;
using Microsoft.VisualStudio.PlatformUI.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text.Editor;

namespace Vim.VisualStudio.Specific
{
#if VS_SPECIFIC_2012 || VS_SPECIFIC_2013 || VS_SPECIFIC_2015

    internal partial class SharedService
    {
        private bool HasMultipleCarets(ITextView textView)
        {
            return false;
        }
    }

#else

#if false

    internal partial class SharedService
    {
        private bool HasMultipleCarets(ITextView textView)
        {
            if (textView is ITextView2 textView2)
            {
                var broker = textView2.MultiSelectionBroker;
                return broker.HasMultipleSelections;
            }
            return false;
        }
    }

#else

    internal partial class SharedService
    {
        private bool HasMultipleCarets(ITextView textView)
        {
            var textView2Type = textView
                .GetType()
                .GetInterface("Microsoft.VisualStudio.Text.Editor.ITextView2");
            if (textView2Type != null)
            {
                var brokerProperty = textView2Type
                    .GetProperty("MultiSelectionBroker");
                if (brokerProperty != null)
                {
                    var broker = brokerProperty
                        .GetValue(textView);
                    var brokerType = broker
                        .GetType()
                        .GetInterface("Microsoft.VisualStudio.Text.Editor.IMultiSelectionBroker");
                    if (brokerType != null)
                    {
                        var property = brokerType
                            .GetProperty("HasMultipleSelections");
                        if (property != null)
                        {
                            return (bool)property.GetValue(broker);
                        }
                    }
                }
            }
            return false;
        }
    }

#endif

#endif
    }
