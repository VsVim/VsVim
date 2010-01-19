#light

namespace Vim

module internal Resources =
    let SelectionTracker_AlreadyRunning = "Already running"
    let SelectionTracker_NotRunning = "Not Running"
    let VisualMode_Banner = "--Visual--"

    let NormalMode_PatternNotFound pattern = sprintf "Pattern not found: %s" pattern
    let NormalMode_NoPreviousSearch = "No previous search"
    let NormalMode_NoWordUnderCursor = "No word under cursor"
    let NormalMode_NoStringUnderCursor = "No string under cursor"

    let CommandMode_InvalidCommand = "Invalid command"
    let CommandMode_PatternNotFound pattern = NormalMode_PatternNotFound pattern
    let CommandMode_SubstituteComplete subs lines = sprintf "%d substitutions on %d lines" subs lines
    let CommandMode_NotSupported msg = sprintf "Command not currently supported: %s" msg
    let CommandMode_NotSupported_SubstituteConfirm = CommandMode_NotSupported "Substitute Confirm"
