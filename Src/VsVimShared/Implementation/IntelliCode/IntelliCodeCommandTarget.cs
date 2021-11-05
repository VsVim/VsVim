#nullable enable
#if VS_SPECIFIC_2019 || VS_SPECIFIC_2022
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Vim.VisualStudio;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Vim;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text.Editor;
using System.Reflection;
using System.Diagnostics;
using Microsoft.VisualStudio.Text;

namespace Vim.VisualStudio.Implementation.IntelliCode
{
    internal sealed class IntelliCodeCommandTarget : ICommandTarget
    {
        private (object CascadingCompletionSource, FieldInfo CurrentPrediction, FieldInfo CompletionState)? _data;
        private bool _lookedForData;

        internal IVimBufferCoordinator VimBufferCoordinator { get; }
        internal IAsyncCompletionBroker AsyncCompletionBroker { get; }
        internal ITextDocumentFactoryService TextDocumentFactoryService { get; }

        internal IVimBuffer VimBuffer => VimBufferCoordinator.VimBuffer;
        internal ITextView TextView => VimBuffer.TextView;
        internal object? CascadingCompletionSource
        {
            get
            {
                EnsureLookedForData();
                return _data?.CascadingCompletionSource;
            }
        }

        internal IntelliCodeCommandTarget(
            IVimBufferCoordinator vimBufferCoordinator,
            IAsyncCompletionBroker asyncCompletionBroker,
            ITextDocumentFactoryService textDocumentFactoryService)
        {
            VimBufferCoordinator = vimBufferCoordinator;
            AsyncCompletionBroker = asyncCompletionBroker;
            TextDocumentFactoryService = textDocumentFactoryService;
        }

        private void EnsureLookedForData()
        {
            if (_lookedForData)
            {
                return;
            }

            _lookedForData = true;
            try
            {
                var cascadingCompletionSource = TextView
                    .Properties
                    .PropertyList
                    .Where(pair => pair.Key is Type { Name: "CascadingCompletionSource" })
                    .Select(x => x.Value)
                    .FirstOrDefault();
                if (cascadingCompletionSource is object)
                {
                    var type = cascadingCompletionSource.GetType();
                    var currentPrediction = type.GetField("_currentPrediction", BindingFlags.NonPublic | BindingFlags.Instance);
                    var completionState = type.GetField("_completionState", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (currentPrediction is object && completionState is object)
                    {
                        _data = (cascadingCompletionSource, currentPrediction, completionState);
                    }
                }
            }
            catch (Exception ex)
            {
                VimTrace.TraceInfo($"IntelliCodeCommandTarget::EnsureLookedForData error {ex.Message}");
                Debug.Assert(false);
            }
        }

        private bool IsIntelliCodeHandlingEscape()
        {
            EnsureLookedForData();
            if (_data is { } data)
            {
                try
                {
                    var completionState = data.CompletionState.GetValue(data.CascadingCompletionSource);
                    var currentPrediction = data.CurrentPrediction.GetValue(data.CascadingCompletionSource);
                    return completionState is object || currentPrediction is object;
                }
                catch (Exception ex)
                {
                    VimTrace.TraceInfo($"IntelliCodeCommandTarget::IsIntelliCodeHandlingEscape error {ex.Message}");
                    Debug.Assert(false);
                }
            }

            return false;
        }

        private bool Exec(EditCommand editCommand, out Action? preAction, out Action? postAction)
        {
            preAction = null;
            postAction = null;
            return false;
        }

        private CommandStatus QueryStatus(EditCommand editCommand)
        {
            if (editCommand.HasKeyInput &&
                editCommand.KeyInput == KeyInputUtil.EscapeKey && 
                VimBuffer.CanProcess(editCommand.KeyInput) &&
                IsIntelliCodeHandlingEscape())
            {
                VimTrace.TraceInfo($"IntelliCodeCommandTarget::QueryStatus handling escape");
                VimBuffer.Process(editCommand.KeyInput);
            }

            return CommandStatus.PassOn;
        }

        #region ICommandTarget

        bool ICommandTarget.Exec(EditCommand editCommand, out Action? preAction, out Action? postAction) =>
            Exec(editCommand, out preAction, out postAction);

        CommandStatus ICommandTarget.QueryStatus(EditCommand editCommand) =>
            QueryStatus(editCommand);

        #endregion
    }

    [Export(typeof(ICommandTargetFactory))]
    [Name("IntelliCode Command Target")]
    [Order(Before = VsVimConstants.StandardCommandTargetName)]
    internal sealed class IntelliCodeCommandTargetFactory : ICommandTargetFactory
    {
        internal IAsyncCompletionBroker AsyncCompletionBroker { get; }
        internal ITextDocumentFactoryService TextDocumentFactoryService { get; }

        [ImportingConstructor]
        public IntelliCodeCommandTargetFactory(
            IAsyncCompletionBroker asyncCompletionBroker,
            ITextDocumentFactoryService textDocumentFactoryService)
        {
            AsyncCompletionBroker = asyncCompletionBroker;
            TextDocumentFactoryService = textDocumentFactoryService;
        }

        ICommandTarget ICommandTargetFactory.CreateCommandTarget(IOleCommandTarget nextCommandTarget, IVimBufferCoordinator vimBufferCoordinator) =>
            new IntelliCodeCommandTarget(vimBufferCoordinator, AsyncCompletionBroker, TextDocumentFactoryService);
    }
}

#elif VS_SPECIFIC_2017
#else
#error Unsupported configurationi
#endif
