#light

namespace Vim

module internal Resources =
    let MotionCapture_InvalidMotion = "Invalid Motion"
    let SelectionTracker_AlreadyRunning = "Already running"
    let SelectionTracker_NotRunning = "Not Running"
    let VisualMode_Banner = "--Visual--"
    let KeyNotationUtil_InvalidNotation notation = sprintf "%s is an invalid key notatin" notation

    let KeyInput_InvalidVimKey = "Invalid Vim Key provided"
    let KeyInput_DuplicateCharRepresentation = "Duplicate char representation detected"

    let Common_GotoDefNoWordUnderCursor = "No word under cursor to go to the definition of"
    let Common_GotoDefFailed word = sprintf "Could not navigate to definition of %s" word
    let Common_MarkInvalid = "Argument must be a letter or forward / back quote"
    let Common_MarkNotSet = "Mark not set"
    let Common_BulkEdit = "VsVim Bulk Edit"
    let Common_NoFoldFound = "No fold found"
    let Common_InvalidLineNumber = "Invalid Line Number"
    let Common_PatternNotFound pattern = sprintf "Pattern not found: %s" pattern
    let Common_SubstituteComplete subs lines = sprintf "%d substitutions on %d lines" subs lines
    let Common_NoWriteSinceLastChange = "No write since last change (add ! to override)"
    let Common_SelectionInvalid = "An invalid selection was detected"
    let Common_SearchForwardWrapped = "search hit BOTTOM, continuing at TOP"
    let Common_SearchBackwardWrapped = "search hit TOP, continuing at BOTTOM"
    let Common_SearchHitBottomWithout name = sprintf "search hit BOTTOM without match for: %s" name
    let Common_SearchHitTopWithout name = sprintf "search hit TOP without match for: %s" name
    let Common_SaveFailed = "Save operation failed"

    let NormalMode_NoPreviousSearch = "No previous search"
    let NormalMode_NoWordUnderCursor = "No word under cursor"
    let NormalMode_NoStringUnderCursor = "No string under cursor"
    let NormalMode_RecursiveRepeatDetected = "Recursive repeat command detected"
    let NormalMode_RepeatNotSupportedOnCommand name = sprintf "Repeat not yet supported on command %s" name
    let NormalMode_UnableToRepeatMotion = "Unable to repeat motion"
    let NormalMode_CantFindFile fileName = sprintf "Can't find file for %s in path" fileName

    let CommandMode_InvalidCommand = "Invalid command"
    let CommandMode_NotSupported msg = sprintf "Command not currently supported: %s" msg
    let CommandMode_NotSupported_SourceNormal = "source! commands are not currently supported"
    let CommandMode_NotSupported_KeyMapping lhs rhs = sprintf "The key mapping %s -> %s is not currently supported" lhs rhs
    let CommandMode_UnknownOption optionName = sprintf "Unknown option: %s" optionName
    let CommandMode_InvalidArgument name = sprintf "Invalid argument: %s" name
    let CommandMode_InvalidValue name value = sprintf "Invalid value given for %s: %s" name value
    let CommandMode_CannotRun command = sprintf "Cannot run \"%s\"" command
    let CommandMode_CouldNotOpenFile file = sprintf "Could not open file \"%s\"" file
    let CommandMode_NoSuchMapping = "No such mapping"
    let CommandMode_TrailingCharacters = "Trailing characters"
    let CommandMode_NoPreviousSubstitute =  "No previous substitute regular expression"
    let CommandMode_NoFileName = "No file name"
    let CommandMode_RegisterBanner = "--- Registers ---"

    let VisualMode_BoxSelectionNotSupported = "Box selection is not supported for this operation"
    let VisualMode_MultiSelectNotSupported = "Multiple selections is not supported for this operation"

    let Vim_ViewAlreadyHasBuffer = "View is already associated with an IVimBuffer"
    let Vim_RecursiveMapping = "Recursive key mapping detected"

    let VimBuffer_AlreadyClosed = "IVimBuffer instance is already closed"

    let Range_Invalid = "Invalid range"
    let Range_MarkMissingIdentifier = "Invalid Range: Missing mark after '"
    let Range_MarkNotValidInFile = "Invalid Range: Mark is invalid in this file"
    let Range_EmptyRange = "InvalidRange: Expected a range"
    let Range_ConnectionMissing = "Invalid Range: Expected , or ;"

    let CommandRunner_CommandNameAlreadyAdded = "A Command with the given name is already present"

    let Internal_UndoRedoNotSupported = "Undo / Redo is not supported on this buffer"
    let Internal_FoldsNotSupported = "Folds are not supported on this buffer"
    let Internal_CannotUndo = "Cannot undo the last action"
    let Internal_CannotRedo = "Cannot redo the last action"
    let Internal_ErrorMappingToVisual = "Error mapping to the visual buffer defaulting to edit buffer"
    let Internal_ErrorMappingBackToEdit = "Error mapping data back to the edit buffer"

