using System;
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
        private struct VersionInfo
        {
            internal readonly ReSharperVersion Version;
            internal readonly IReSharperEditTagDetector EditTagDetector;

            /// <summary>
            /// The <see cref="ITaggerProvider"/> specific to R# live templates.  Can be null in the 
            /// cases where it was not detectable.
            /// </summary>
            internal readonly ITaggerProvider TaggerProvider;

            /// <summary>
            /// Before ReSharper 10.0 all of the detection for external edits was tag based.
            /// </summary>
            internal bool UseTagBasedDetection { get { return Version < ReSharperVersion.Version10; } }

            internal VersionInfo(ReSharperVersion version, IReSharperEditTagDetector editTagDetector, ITaggerProvider taggerProvider)
            {
                Contract.Assert(editTagDetector != null);

                Version = version;
                EditTagDetector = editTagDetector;
                TaggerProvider = taggerProvider;
            }
        }

        internal const string ResharperTaggerProviderName = "VsDocumentMarkupTaggerProvider";
        internal static readonly Guid LiveTemplateKey = new Guid("A6FD6EDE-B430-46C3-9991-DA077ECF5C0B");

        private readonly IReSharperUtil _reSharperUtil;
        private readonly IVimBufferCoordinatorFactory _vimBufferCoordinatorFactory;
        private readonly Dictionary<Type, bool> _tagMap = new Dictionary<Type, bool>();
        private VersionInfo? _versionInfo;

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

            if (!_versionInfo.HasValue)
            {
                // There is a possible race in MEF construction which would allow this method to be 
                // called before we had the list of ITaggerProvider instances to query.  In that case
                // defer to the next check.
                if (TaggerProviders == null)
                {
                    tagger = null;
                    return false;
                }

                _versionInfo = DetectVersionInfo();
            }

            return IsInterested(textView.TextBuffer, out tagger);
        }

        internal bool IsExternalEditTag(ITag tag)
        {
            return IsVsAdornmentTagType(tag.GetType()) && IsEditTag(tag);
        }

        internal IReSharperEditTagDetector GetEditTagDetector(ReSharperVersion reSharperVersion)
        {
            switch (reSharperVersion)
            {
                case ReSharperVersion.Version7AndEarlier:
                    return new ReSharperV7EditTagDetector();
                case ReSharperVersion.Version8:
                    return new ReSharperV8EditTagDetector();
                case ReSharperVersion.Version81:
                case ReSharperVersion.Version82:
                    return new ReSharperV81Or2EditTagDetector(reSharperVersion);
                case ReSharperVersion.Version9:
                case ReSharperVersion.Version91:
                case ReSharperVersion.Version92:
                    return new ReSharperV81Or2EditTagDetector(reSharperVersion);
                case ReSharperVersion.Version10:
                    return new ReSharperDefaultEditTagDetector();
                case ReSharperVersion.Unknown:
                    return new ReSharperDefaultEditTagDetector();
                default:
                    throw new Exception("Wrong enum value");
            }
        }

        private bool IsVsAdornmentTagType(Type type)
        {
            if (_tagMap.TryGetValue(type, out bool isType))
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
        private bool IsInterested(ITextBuffer textBuffer, out ITagger<ITag> tagger)
        {
            Contract.Assert(_reSharperUtil.IsInstalled);
            Contract.Assert(_versionInfo.HasValue);

            // R# exposes it's ITaggerProvider instances for the "text" content type.  As much as
            // I would like to query to make sure they always support the content type we don't
            // have access to the metadata and have to hard code "text" here
            if (!textBuffer.ContentType.IsOfType("text"))
            {
                tagger = null;
                return false;
            }

            var versionInfo = _versionInfo.Value;
            if (!versionInfo.UseTagBasedDetection)
            {
                // Version 10 and above doesn't use tag detection, it just looks for the key.  No need
                // for a specific ITagger here.
                tagger = null;
                return true;
            }

            tagger = versionInfo.TaggerProvider?.SafeCreateTagger<ITag>(textBuffer).GetValueOrDefault();
            return tagger != null;
        }

        /// <summary>
        /// The various JetBrains products reuse at a code base level certain constructs
        /// such as ITagger<T> implementations.  For example both R# and dotTrace and 
        /// dotCover use the VsDocumentMarkerTaggerProvider.  If multiple products are
        /// installed then MEF composition will return them in a non-deterministic 
        /// order.  They all have the same fully qualified name.  The only way to 
        /// distinguish them is to look at the assembly name containing the type
        /// </summary>
        private VersionInfo DetectVersionInfo()
        {
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
                        var editTagDetector = GetEditTagDetector(version);
                        return new VersionInfo(version, editTagDetector, provider);
                    }
                }
            }

            return new VersionInfo(
                ReSharperVersion.Unknown,
                GetEditTagDetector(ReSharperVersion.Unknown),
                taggerProvider: null);
        }

        internal void ResetForVersion(ReSharperVersion version, ITaggerProvider taggerProvider = null)
        {
            var editTagDetector = GetEditTagDetector(version);
            _versionInfo = new VersionInfo(version, editTagDetector, taggerProvider);
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
            if (!_versionInfo.HasValue)
            {
                return false;
            }

            var versionInfo = _versionInfo.Value;
            return versionInfo.UseTagBasedDetection && versionInfo.EditTagDetector.IsEditTag(tag);
        }

        private bool? IsExternalEditActive(ITextView textView)
        {
            if (!_versionInfo.HasValue)
            {
                return false;
            }

            var versionInfo = _versionInfo.Value;
            if (versionInfo.UseTagBasedDetection)
            {
                return null;
            }

            return textView.Properties.ContainsProperty(LiveTemplateKey);
        }

        #region IExternalEditAdapter

        bool IExternalEditAdapter.IsInterested(ITextView textView, out ITagger<ITag> tagger)
        {
            return IsInterested(textView, out tagger);
        }

        bool? IExternalEditAdapter.IsExternalEditActive(ITextView textView)
        {
            return IsExternalEditActive(textView);
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
