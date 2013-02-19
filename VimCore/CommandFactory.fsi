namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor

/// Factory for creating certain commands which are shared between visual and normal mode
type internal CommandFactory =
    new : ICommonOperations * IMotionCapture * IMotionUtil * IJumpList * IVimLocalSettings -> CommandFactory

    /// Returns the set of commands which move the cursor.  This includes all motions which are 
    /// valid as movements.  Several of these are overridden with custom movement behavior though.
    member CreateMovementCommands : unit -> CommandBinding seq

    /// Returns the set of commands which move the cursor that are a result of a text object
    /// motion
    member CreateMovementTextObjectCommands : unit -> CommandBinding seq

    /// Returns the set of commands which move the caret as a scroll operation
    member CreateScrollCommands : unit -> CommandBinding seq

    /// Adds in the macro edit commands
    member CreateMacroEditCommands : ICommandRunner -> IMacroRecorder -> DisposableBag -> unit


