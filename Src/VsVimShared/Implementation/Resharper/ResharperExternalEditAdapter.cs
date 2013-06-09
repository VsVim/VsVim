﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using Vim;

namespace VsVim.Implementation.Resharper
{
    [Export(typeof(IResharperUtil))]
    [Export(typeof(IExternalEditAdapter))]
    internal sealed class ResharperExternalEditAdapter : IExternalEditAdapter, IResharperUtil
    {
        internal const string ResharperTaggerProviderName = "VsDocumentMarkupTaggerProvider";

        private ReSharperVersion _reSharperVersion;

        private static readonly Guid Resharper5Guid = new Guid("0C6E6407-13FC-4878-869A-C8B4016C57FE");

        private readonly Dictionary<Type, bool> _tagMap = new Dictionary<Type, bool>();
        private readonly bool _isResharperInstalled;
        private IReSharperEditTagDetector _reSharperEditTagDetector;

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

            bool sawName;
            if (TryGetSpecificTagger(textBuffer, out sawName, out tagger))
            {
                return true;
            }

            if (sawName && TryGetGeneralTagger(textBuffer, out tagger))
            {
                return true;
            }

            tagger = null;
            return false;
        }

        /// <summary>
        /// The various JetBrains products reuse at a code base level certain constructs
        /// such as ITagger<T> implementations.  For example both R# and dotTrace and 
        /// dotCover use the VsDocumentMarkerTaggerProvider.  If multiple products are
        /// installed then MEF composition will return them in a non-deterministic 
        /// order.  They all have the same fully qualified name.  The only way to 
        /// distinguish them is to look at the assembly name containing the type
        /// </summary>
        private bool TryGetSpecificTagger(ITextBuffer textBuffer, out bool sawName, out ITagger<ITag> tagger)
        {
            sawName = false;
            tagger = null;

            foreach (var pair in TaggerProviders)
            {
                var provider = pair.Value;
                var providerType = provider.GetType();
                if (providerType.Name == ResharperTaggerProviderName)
                {
                    _reSharperVersion = ResharperVersionUtility.DetectFromAssembly(providerType.Assembly);
                    if (_reSharperVersion != ReSharperVersion.Unknown)
                    {
                        var taggerResult = provider.SafeCreateTagger<ITag>(textBuffer);
                        if (taggerResult.IsSuccess)
                        {
                            tagger = taggerResult.Value;
                            return true;
                        }
                    }
                    else
                    {
                        sawName = true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// This method is to try to account for Resharper versions that are not released at
        /// this time.  Instead of looking for the version specific names we just look at the
        /// jet brains assembly that have the correct tagger name
        ///
        /// One problem here is that the tagger implementations are shared between a couple
        /// of JetBrains products including dotCover.  Hence we need to keep in mind both the
        /// assembly and tagger name here
        /// </summary>
        private bool TryGetGeneralTagger(ITextBuffer textBuffer, out ITagger<ITag> tagger)
        {
            foreach (var pair in TaggerProviders)
            {
                var provider = pair.Value;
                var providerType = provider.GetType();
                if (providerType.Name == ResharperTaggerProviderName &&
                    providerType.Assembly.FullName.StartsWith("JetBrains", StringComparison.OrdinalIgnoreCase))
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

        private bool IsEditTag(ITag tag)
        {
            if (_reSharperEditTagDetector == null)
            {
                switch (_reSharperVersion)
                {
                    case ReSharperVersion.Version7AndEarlier:
                        _reSharperEditTagDetector = new ReSharperV7EditTagDetector();
                        break;
                    case ReSharperVersion.Version8:
                        _reSharperEditTagDetector = new ReSharperV8EditTagDetector();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            return _reSharperEditTagDetector.IsEditTag(tag);
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
