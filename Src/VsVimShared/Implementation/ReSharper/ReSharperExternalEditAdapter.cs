﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using Vim;
using System.Diagnostics;

namespace Vim.VisualStudio.Implementation.ReSharper
{
    [ContentType(VimConstants.ContentType)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    [Export(typeof(IExternalEditAdapter))]
    internal sealed class ReSharperExternalEditAdapter : IExternalEditAdapter
    {
        internal const string ResharperTaggerProviderName = "VsDocumentMarkupTaggerProvider";

        private readonly IReSharperUtil _reSharperUtil;
        private readonly IVimBufferCoordinatorFactory _vimBufferCoordinatorFactory;
        private readonly Dictionary<Type, bool> _tagMap = new Dictionary<Type, bool>();
        private IReSharperEditTagDetector _reSharperEditTagDetector;

        /// <summary>
        /// Need to have an Import on a property vs. a constructor parameter to break a dependency
        /// loop.
        /// </summary>
        [ImportMany(typeof(ITaggerProvider))]
        internal List<Lazy<ITaggerProvider>> TaggerProviders { get; set; }

        [ImportingConstructor]
        internal ReSharperExternalEditAdapter(IReSharperUtil reSharperUtil, IVimBufferCoordinatorFactory vimBufferCoordinatorFactory)
        {
            _reSharperUtil = reSharperUtil;
            _vimBufferCoordinatorFactory = vimBufferCoordinatorFactory;
        }

        internal bool IsInterested(ITextView textView, out ITagger<ITag> tagger)
        {
            if (!_reSharperUtil.IsInstalled)
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

        internal void SetReSharperVersion(ReSharperVersion reSharperVersion)
        {
            switch (reSharperVersion)
            {
                case ReSharperVersion.Version7AndEarlier:
                    _reSharperEditTagDetector = new ReSharperV7EditTagDetector();
                    break;
                case ReSharperVersion.Version8:
                    _reSharperEditTagDetector = new ReSharperV8EditTagDetector();
                    break;
                case ReSharperVersion.Version81:
                case ReSharperVersion.Version82:
                    _reSharperEditTagDetector = new ReSharperV81Or2EditTagDetector(reSharperVersion);
                    break;
                case ReSharperVersion.Version9:
                case ReSharperVersion.Version91:
                case ReSharperVersion.Version92:
                    _reSharperEditTagDetector = new ReSharperV81Or2EditTagDetector(reSharperVersion);
                    break;
                case ReSharperVersion.Unknown:
                    _reSharperEditTagDetector = new ReSharperUnknownEditTagDetector();
                    break;
                default:
                    throw new Exception("Wrong enum value");
            }
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
            Contract.Assert(_reSharperUtil.IsInstalled);

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

            foreach (var provider in GetTaggerProvidersSafe())
            {
                var providerType = provider.GetType();

                // First step is to check the name of the tagger.  The ReSharper taggers we care
                // about all have the same name
                if (providerType.Name == ResharperTaggerProviderName)
                {
                    // Next we need to make sure this is actually a ReSharper tagger.  Both dotCover 
                    // and ReSharper use the same tagger name.  The only way to differentiate them is
                    // to look at the assembly version
                    var version = ResharperVersionUtility.DetectFromAssembly(providerType.Assembly);
                    if (version != ReSharperVersion.Unknown)
                    {
                        SetReSharperVersion(version);
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
        /// This method is to try to account for ReSharper versions that are not released at
        /// this time.  Instead of looking for the version specific names we just look at the
        /// jet brains assembly that have the correct tagger name
        ///
        /// One problem here is that the tagger implementations are shared between a couple
        /// of JetBrains products including dotCover.  Hence we need to keep in mind both the
        /// assembly and tagger name here
        /// </summary>
        private bool TryGetGeneralTagger(ITextBuffer textBuffer, out ITagger<ITag> tagger)
        {
            foreach (var provider in GetTaggerProvidersSafe())
            {
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

        /// <summary>
        /// The Lazy(Of ITaggerProvider) value can throw on the Value property.  The call back 
        /// is what invokes composition and that can fail.  Handle the exception here and ignore
        /// the individual property
        /// </summary>
        private IEnumerable<ITaggerProvider> GetTaggerProvidersSafe()
        {
            foreach (var lazy in TaggerProviders)
            {
                ITaggerProvider taggerProvider = null;
                try
                {
                    taggerProvider = lazy.Value;
                }
                catch
                {
                    // Ignore this lazy and move onto the next
                }

                if (taggerProvider != null)
                {
                    yield return taggerProvider;
                }
            }
        }

        private bool IsEditTag(ITag tag)
        {
            if (_reSharperEditTagDetector == null)
            {
                return false;
            }

            return _reSharperEditTagDetector.IsEditTag(tag);
        }

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
