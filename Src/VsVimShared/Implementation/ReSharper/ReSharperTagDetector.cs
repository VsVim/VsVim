using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.Text.Tagging;

namespace Vim.VisualStudio.Implementation.ReSharper
{
    internal interface IReSharperEditTagDetector
    {
        ReSharperVersion Version { get; }
        bool IsEditTag(ITag tag);
    }

    internal abstract class ReSharperEditTagDetectorBase : IReSharperEditTagDetector
    {
        internal const string ExternalEditAttribute1 = "ReSharper Template Editor Template Keyword";
        internal const string ExternalEditAttribute2 = "ReSharper LiveTemplates Current HotSpot";
        internal const string ExternalEditAttribute3 = "ReSharper LiveTemplates Current HotSpot mirror";

        public abstract ReSharperVersion Version { get; }

        public abstract bool IsEditTag(ITag tag);
    }

    internal sealed class ReSharperV7EditTagDetector : ReSharperEditTagDetectorBase
    {
        internal FieldInfo AttributeIdFieldInfo { get; private set; }

        public override ReSharperVersion Version
        {
            get { return ReSharperVersion.Version7AndEarlier; }
        }

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

    internal sealed class ReSharperV8EditTagDetector : ReSharperEditTagDetectorBase
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
        internal FieldInfo HighlighterFieldInfo { get; private set; }
        
        /// <summary>
        /// Cache reflection info for AttributeId string property
        /// This is for a specific type and is thus safe in terms of leaks etc.
        /// </summary>
        internal PropertyInfo AttributeIdPropertyInfo { get; private set; }

        public override ReSharperVersion Version
        {
            get { return ReSharperVersion.Version8; }
        }

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

    internal sealed class ReSharperV81EditTagDetector : ReSharperEditTagDetectorBase
    {
        internal FieldInfo AttributeIdFieldInfo { get; private set; }

        public override ReSharperVersion Version
        {
            get { return ReSharperVersion.Version81; }
        }

        public override bool IsEditTag(ITag tag)
        {
            // In ReSharper 8 the tag implementation (JetBrains.VsIntegration.DevTen.Markup.VsTextAdornmentTag) 
            // no longer stores a reference to the JetBrains.TextControl.DocumentMarkup.IHighlighter implementation
            // Instead the implementation is similar to how ReSharper 7 did it, 
            // it has a field "myHighlighterAttributeId" that stores the highlighter attribute id

            // Cache the FieldInfo/PropertyInfo since we will be using it a lot
            if (AttributeIdFieldInfo == null)
            {
                Type type = tag.GetType();
                AttributeIdFieldInfo = type.GetField("myHighlighterAttributeId", BindingFlags.Instance | BindingFlags.NonPublic);
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
    /// <summary>
    /// Used in the cases where the version of R# cannot be detected 
    /// </summary>
    internal sealed class ReSharperUnknownEditTagDetector : IReSharperEditTagDetector
    {
        #region IReSharperEditTagDetector

        ReSharperVersion IReSharperEditTagDetector.Version
        {
            get { return ReSharperVersion.Unknown; }
        }

        bool IReSharperEditTagDetector.IsEditTag(ITag tag)
        {
            return false;
        }

        #endregion
    }
}