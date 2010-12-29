using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;

namespace VsVim.ExternalEdit
{
    internal sealed class ResharperExternalEditorAdapter : IExternalEditorAdapter
    {
        private Dictionary<Type,bool> _tagMap = new Dictionary<Type,bool>();

        public ExternalEditMarker? TryCreateExternalEditMarker(IVsTextLineMarker marker, ITextSnapshot snapshot)
        {
            return null;
        }

        public ExternalEditMarker? TryCreateExternalEditMarker(ITag tag, SnapshotSpan tagSpan)
        {
            if (IsVsAdornmentTagType(tag.GetType()) && IsEditTag(tag))
            {
                return  new ExternalEditMarker(ExternalEditKind.Resharper, tagSpan);
            }

            return null;
        }

        private bool IsVsAdornmentTagType(Type type)
        {
            bool isType;
            if (_tagMap.TryGetValue(type, out isType))
            {
                return isType;
            }

            isType = type.Name == "VsTextAdornmentTag";
            _tagMap[type] = isType;
            return isType;
        }

        private static bool IsEditTag(ITag tag)
        {
            var field = tag.GetType().GetField("myAttributeId", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                return false;
            }

            var value = field.GetValue(tag) as string;
            if (value == null)
            {
                return false;
            }

            switch (value)
            {
                case "ReSharper Template Editor Template Keyword":
                case "ReSharper LiveTemplates Current HotSpot":
                case "ReSharper LiveTemplates Current HotSpot mirror":
                    return true;
                default:
                    return false;
            }
        }
    }
}
