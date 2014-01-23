namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor

/// Factory for creating certain commands which are shared between visual and normal mode
type internal CommandFactory =
    new : ICommonOperations * IMotionCapture -> CommandFactory

    /// Returns the set of commands which move the cursor.  This includes all motions which are 
    /// valid as movements.  Several of these are overridden with custom movement behavior though.
    member CreateMovementCommands : unit -> CommandBinding list

    /// Returns the set of commands which move the cursor that are a result of a text object
    /// motion
    member CreateMovementTextObjectCommands : unit -> CommandBinding list

    /// Returns the set of commands which move the caret as a scroll operation
    member CreateScrollCommands : unit -> CommandBinding list

    /// Returns the set of commands that initiate select mode
    member CreateSelectionCommands : unit -> CommandBinding list

    /// Adds in the macro edit commands
    member CreateMacroEditCommands : ICommandRunner -> IMacroRecorder -> DisposableBag -> unit


