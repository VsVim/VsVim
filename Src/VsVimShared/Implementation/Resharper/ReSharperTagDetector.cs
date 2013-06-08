using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.VisualStudio.Text.Tagging;

namespace VsVim.Implementation.Resharper
{
    internal interface IReSharperEditTagDetector
    {
        bool IsEditTag(ITag tag);
    }

    internal abstract class ResharperEditTagDetectorBase : IReSharperEditTagDetector
    {
        internal const string ExternalEditAttribute1 = "ReSharper Template Editor Template Keyword";
        internal const string ExternalEditAttribute2 = "ReSharper LiveTemplates Current HotSpot";
        internal const string ExternalEditAttribute3 = "ReSharper LiveTemplates Current HotSpot mirror";

        public abstract bool IsEditTag(ITag tag);
    }

    internal class ReSharperV7EditTagDetector : ResharperEditTagDetectorBase
    {
        public FieldInfo AttributeIdFieldInfo { get; private set; }

        public override bool IsEditTag(ITag tag)
        {
            // Cache the FieldInfo/PropertyInfo since we will be using it a lot
            if (AttributeIdFieldInfo == null)
            {
                Type type = tag.GetType();
                AttributeIdFieldInfo = type.GetField("myAttributeId", BindingFlags.Instance | BindingFlags.NonPublic);
            }

            if (AttributeIdFieldInfo == null)
            {
                return false;
            }

            var value = AttributeIdFieldInfo.GetValue(tag) as string;
            if (value == null)
            {
                return false;
            }

            switch (value)
            {
                case ExternalEditAttribute1:
                case ExternalEditAttribute2:
                case ExternalEditAttribute3:
                    return true;
                default:
                    return false;
            }
        }
    }

    internal class ReSharperV8EditTagDetector : ResharperEditTagDetectorBase
    {
        private readonly Dictionary<ITag, object> _highlighterInstances = new Dictionary<ITag, object>();

        public PropertyInfo AttributeIdPropertyInfo { get; private set; }

        public override bool IsEditTag(ITag tag)
        {
            object highlighterInstance = null;
 
            // Cache the PropertyInfo since we will be using it a lot
            if (AttributeIdPropertyInfo == null)
            {
                Type type = tag.GetType();

                // cache the highlighter instances
                if (!_highlighterInstances.TryGetValue(tag, out highlighterInstance))
                {
                    // In ReSharper 8 the tag implementation (JetBrains.VsIntegration.DevTen.Markup.VsTextAdornmentTag) 
                    // has the JetBrains.TextControl.DocumentMarkup.IHighlighter implementation 
                    // (JetBrains.VsIntegration.DevTen.Markup.Vs10Highlighter) as a readonly field "myHighlighter"
                    FieldInfo highlighterFieldInfo = type.GetField("myHighlighter",
                        BindingFlags.Instance | BindingFlags.NonPublic);

                    if (highlighterFieldInfo != null)
                    {
                        // this is safe to cache because the field is readonly
                        highlighterInstance = highlighterFieldInfo.GetValue(tag);
                        _highlighterInstances[tag] = highlighterInstance;

                        // the IHighlighter interface has a string property "AttributeId" which is used for detection
                        AttributeIdPropertyInfo = highlighterInstance.GetType().GetProperty("AttributeId",
                            BindingFlags.Instance | BindingFlags.Public);
                    }
                }
            }

            if (AttributeIdPropertyInfo == null)
            {
                return false;
            }

            var value = AttributeIdPropertyInfo.GetValue(highlighterInstance, null) as string;
            if (value == null)
            {
                return false;
            }

            switch (value)
            {
                case ExternalEditAttribute1:
                case ExternalEditAttribute2:
                case ExternalEditAttribute3:
                    return true;
                default:
                    return false;
            }
        }
    }
}