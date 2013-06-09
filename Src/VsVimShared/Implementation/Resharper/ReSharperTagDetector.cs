using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
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
        /// <summary>
        /// Cache the highlighter instances we get with Reflection, use a ConditionalWeakTable (uses weak references) 
        /// to prevent keeping a reference to internal ReSharper instances
        /// </summary>
        private readonly ConditionalWeakTable<ITag, object> _highlighterInstances = new ConditionalWeakTable<ITag, object>();

        /// <summary>
        /// Cache reflection info for the "myHighligher" field in the ITag type
        /// This is for a specific type and is thus safe in terms of leaks etc.
        /// </summary>
        public FieldInfo HighlighterFieldInfo { get; private set; }
        
        /// <summary>
        /// Cache reflection info for AttributeId string property
        /// This is for a specific type and is thus safe in terms of leaks etc.
        /// </summary>
        public PropertyInfo AttributeIdPropertyInfo { get; private set; }

        public override bool IsEditTag(ITag tag)
        {
            // In ReSharper 8 the tag implementation (JetBrains.VsIntegration.DevTen.Markup.VsTextAdornmentTag) 
            // has the JetBrains.TextControl.DocumentMarkup.IHighlighter implementation 
            // (JetBrains.VsIntegration.DevTen.Markup.Vs10Highlighter) as a readonly field "myHighlighter"

            var highlighterInstance = _highlighterInstances.GetValue(tag, delegate
            {
                Type tagType = tag.GetType();

                HighlighterFieldInfo = tagType.GetField("myHighlighter",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                if (HighlighterFieldInfo != null)
                {
                    return HighlighterFieldInfo.GetValue(tag);
                }
                return null;
            });

            // Cache the PropertyInfo for the AttributeId as well since we will be using it a lot
            if (AttributeIdPropertyInfo == null)
            {
                if (highlighterInstance != null)
                {
                    // the IHighlighter interface has a string property "AttributeId" which is used for detection
                    AttributeIdPropertyInfo = highlighterInstance.GetType().GetProperty("AttributeId",
                        BindingFlags.Instance | BindingFlags.Public);
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