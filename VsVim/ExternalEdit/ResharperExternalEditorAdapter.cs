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

        public bool TryCreateExternalEditMarker(IVsTextLineMarker marker, ITextSnapshot snapshot, out ExternalEditMarker editMarker)
        {
            editMarker = new ExternalEditMarker();
            return false;
        }

        public bool TryCreateExternalEditMarker(ITag tag, SnapshotSpan tagSpan, out ExternalEditMarker editMarker)
        {
            if (IsVsAdornmentTagType(tag.GetType()) && IsEditTag(tag))
            {
                editMarker = new ExternalEditMarker(ExternalEditKind.Resharper, tagSpan);
                return true;
            }

            editMarker = new ExternalEditMarker();
            return false;
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
