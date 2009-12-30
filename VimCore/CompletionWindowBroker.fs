#light

namespace Vim
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Language.Intellisense

type internal CompletionWindowBroker 
    ( 
        _textView : ITextView,
        _completionBroker : ICompletionBroker,
        _signatureBroker : ISignatureHelpBroker ) = 
    interface ICompletionWindowBroker with
        member x.TextView = _textView
        member x.IsCompletionWindowActive = 
            _completionBroker.IsCompletionActive(_textView) || _signatureBroker.IsSignatureHelpActive(_textView)
        member x.DismissCompletionWindow() = 
            if _completionBroker.IsCompletionActive(_textView) then
                _completionBroker.DismissAllSessions(_textView)
            if _signatureBroker.IsSignatureHelpActive(_textView) then
                _signatureBroker.DismissAllSessions(_textView)
