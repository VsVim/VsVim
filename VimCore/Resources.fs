#light

namespace Vim

module internal Resources =
    let MotionCapture_InvalidMotion = "Invalid Motion"
    let SelectionTracker_AlreadyRunning = "Already running"
    let SelectionTracker_NotRunning = "Not Running"
    let VisualMode_Banner = "--Visual--"

    let Common_GotoDefNoWordUnderCursor = "No word under cursor to go to the definition of"
    let Common_GotoDefFailed word = sprintf "Could not navigate to definition of %s" word
    let Common_MarkInvalid = "Argument must be a letter or forward / back quote"
    let Common_MarkNotSet = "Mark not set"

    let NormalMode_PatternNotFound pattern = sprintf "Pattern not found: %s" pattern
    let NormalMode_NoPreviousSearch = "No previous search"
    let NormalMode_NoWordUnderCursor = "No word under cursor"
    let NormalMode_NoStringUnderCursor = "No string under cursor"
    let NormalMode_RecursiveRepeatDetected = "Recursive repeat command detected"

    let CommandMode_InvalidCommand = "Invalid command"
    let CommandMode_PatternNotFound pattern = NormalMode_PatternNotFound pattern
    let CommandMode_SubstituteComplete subs lines = sprintf "%d substitutions on %d lines" subs lines
    let CommandMode_NotSupported msg = sprintf "Command not currently supported: %s" msg
    let CommandMode_NotSupported_SubstituteConfirm = CommandMode_NotSupported "Substitute Confirm"
    let CommandMode_NotSupported_SourceNormal = "source! commands are not currently supported"
    let CommandMode_NotSupported_KeyMapping lhs rhs = sprintf "The key mapping %s -> %s is not currently supported" lhs rhs
    let CommandMode_UnknownOption optionName = sprintf "Unknown option: %s" optionName
    let CommandMode_InvalidArgument name = sprintf "Invalid argument: %s" name
    let CommandMode_InvalidValue name value = sprintf "Invalid value given for %s: %s" name value
    let CommandMode_CannotRun command = sprintf "Cannot run \"%s\"" command
    let CommandMode_CouldNotOpenFile file = sprintf "Could not open file \"%s\"" file
    let CommandMode_NoSuchMapping = "No such mapping"

    let Vim_ViewAlreadyHasBuffer = "View is already associated with an IVimBuffer"
    let Vim_RecursiveMapping = "Recursive key mapping detected"

    let Range_Invalid msg = sprintf "Invalid Range: %s" msg
    let Range_MarkMissingIdentifier = Range_Invalid "Missing mark after '"
    let Range_MarkNotValidInFile = Range_Invalid "Mark is invalid in this file"
    let Range_EmptyRange = Range_Invalid "Expected a range"
    let Range_ConnectionMissing = Range_Invalid "Expected , or ;"

    let CommandRunner_CommandNameAlreadyAdded = "A Command with the given name is already present"
