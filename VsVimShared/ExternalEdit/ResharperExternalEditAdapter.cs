using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Reflection;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using Vim;

namespace VsVim.ExternalEdit
{
    [Export(typeof(IResharperUtil))]
    [Export(typeof(IExternalEditAdapter))]
    internal sealed class ResharperExternalEditAdapter : IExternalEditAdapter, IResharperUtil
    {
        internal const string ExternalEditAttribute1 = "ReSharper Template Editor Template Keyword";
        internal const string ExternalEditAttribute2 = "ReSharper LiveTemplates Current HotSpot";
        internal const string ExternalEditAttribute3 = "ReSharper LiveTemplates Current HotSpot mirror";

        private static readonly Guid Resharper5Guid = new Guid("0C6E6407-13FC-4878-869A-C8B4016C57FE");
        private static readonly string ResharperTaggerName = "VsDocumentMarkupTaggerProvider";
        private static FieldInfo AttributeIdFieldInfo;

        private readonly Dictionary<Type,bool> _tagMap = new Dictionary<Type,bool>();
        private readonly bool _isResharperInstalled;

        /// <summary>
        /// Need to have an Import on a property vs. a constructor parameter to break a dependency
        /// loop.
        /// </summary>
        [ImportMany(typeof(ITaggerProvider))]
        internal List<Lazy<ITaggerProvider>> TaggerProviders { get; set; }

        [ImportingConstructor]
        internal ResharperExternalEditAdapter(SVsServiceProvider serviceProvider)
        {
            var vsShell = serviceProvider.GetService<SVsShell, IVsShell>();
            _isResharperInstalled = vsShell.IsPackageInstalled(Resharper5Guid);
        }

        internal ResharperExternalEditAdapter(bool isResharperInstalled)
        {
            _isResharperInstalled = isResharperInstalled;
        }

        internal bool IsInterested(ITextView textView, out ITagger<ITag> tagger)
        {
            if (!_isResharperInstalled)
            {
                tagger = null;
                return false;
            }

            return TryGetResharperTagger(textView.TextBuffer, out tagger);
        }

        internal bool IsExternalEditTag(ITag tag)
        {
            return IsVsAdornmentTagType(tag.GetType()) && IsEditTag(tag);
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

        /// <summary>
        /// Get the R# tagger for the ITextBuffer if it exists
        /// </summary>
        private bool TryGetResharperTagger(ITextBuffer textBuffer, out ITagger<ITag> tagger)
        {
            Contract.Assert(_isResharperInstalled);

            // This is available as a post construction MEF import so it's very possible
            // that this is null if it's not initialized
            if (TaggerProviders == null)
            {
                tagger = null;
                return false;
            }

            // R# exposes it's ITaggerProvider instances for the "text" content type.  As much as
            // I would like to query to make sure they always support the content type we don't
            // have access to the metadata and have to hard code "text" here
            if (!textBuffer.ContentType.IsOfType("text"))
            {
                tagger = null;
                return false;
            }

            foreach (var pair in TaggerProviders)
            {
                var provider = pair.Value;
                var name = provider.GetType().Name;
                if (name == ResharperTaggerName)
                {
                    var taggerResult = provider.SafeCreateTagger<ITag>(textBuffer);
                    if (taggerResult.IsSuccess)
                    {
                        tagger = taggerResult.Value;
                        return true;
                    }
                }
            }

            tagger = null;
            return false;
        }

        private static bool IsEditTag(ITag tag)
        {
            // Cache the FieldInfo since we will be using it a lot
            if (AttributeIdFieldInfo == null)
            {
                AttributeIdFieldInfo = tag.GetType().GetField("myAttributeId", BindingFlags.Instance | BindingFlags.NonPublic);
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

        #region IResharperUtil

        bool IResharperUtil.IsInstalled
        {
            get { return _isResharperInstalled; }
        }

        #endregion

        #region IExternalEditAdapter

        bool IExternalEditAdapter.IsInterested(ITextView textView, out ITagger<ITag> tagger)
        {
            return IsInterested(textView, out tagger);
        }

        bool IExternalEditAdapter.IsExternalEditMarker(IVsTextLineMarker marker)
        {
            return false;
        }

        bool IExternalEditAdapter.IsExternalEditTag(ITag tag)
        {
            return IsExternalEditTag(tag);
        }

        #endregion
    }
}
