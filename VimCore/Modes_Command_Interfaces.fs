#light

namespace Vim.Modes.Command
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations

/// Flags for the substitute command
type SubstituteFlags = 
    | None = 0
    /// Replace all occurances on the line
    | ReplaceAll = 0x1
    /// Ignore case for the search pattern
    | IgnoreCase = 0x2
    /// Report only 
    | ReportOnly = 0x4
    | Confirm = 0x8
    | UsePrevious = 0x10
    | SuppressError = 0x20
    | OrdinalCase = 0x40
    | Invalid = 0xf000

/// Command mode operations
type IOperations =

    /// Handle the :edit command
    abstract EditFile : fileName : string -> unit

    /// Show the open file dialog
    abstract ShowOpenFileDialog : unit -> unit

    /// Put the text into the file 
    abstract Put : text : string -> ITextSnapshotLine -> isAfter : bool -> unit

    /// Substitute Command implementation
    abstract Substitute : pattern : string -> replace : string -> SnapshotSpan -> SubstituteFlags -> unit

    /// For a toggle setting, switch it.  For all others display it
    abstract OperateSetting: settingName:string -> unit

    /// Update a setting value to the provided value
    abstract SetSettingValue : settingName:string -> value:string -> unit

    /// Reset the setting if it's a ToggleValue
    abstract ResetSetting : settingName:string -> unit

    /// Invert the setting if it's a ToggleValue
    abstract InvertSetting : settingName:string -> unit

    /// Remap the specified keys
    abstract RemapKeys : lhs:string -> rhs:string -> modes:KeyRemapMode seq -> allowRemap : bool -> unit

    /// Remap the specified key sequence
    abstract UnmapKeys : lhs:string -> modes:KeyRemapMode seq -> unit

    /// Clear out the specified Mode in the IKeyMap
    abstract ClearKeyMapModes : KeyRemapMode seq -> unit

    /// Print out the marks in the context of the current buffer
    abstract PrintMarks : IMarkMap -> unit

    /// Print all settings which do not have their default value to the host
    abstract PrintModifiedSettings : unit -> unit

    /// Print all settings to the host
    abstract PrintAllSettings : unit -> unit

    /// Print a single setting out to the host
    abstract PrintSetting : settingName:string -> unit

    inherit Modes.ICommonOperations

type ICommandProcessor =

    /// Run the specified command
    abstract RunCommand : char list -> unit

