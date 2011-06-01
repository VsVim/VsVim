#light

namespace Vim.Modes.Command
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations

/// Command mode operations
type IOperations =

    /// Show the open file dialog
    abstract ShowOpenFileDialog : unit -> unit

    /// For a toggle setting, switch it.  For all others display it
    abstract OperateSetting: settingName:string -> unit

    /// Put the register at the specified line
    abstract PutLine : Register -> ITextSnapshotLine -> putBefore : bool -> unit

    /// Update a setting value to the provided value
    abstract SetSettingValue : settingName:string -> value:string -> unit

    /// Reset the setting if it's a ToggleValue
    abstract ResetSetting : settingName:string -> unit

    /// Invert the setting if it's a ToggleValue
    abstract InvertSetting : settingName:string -> unit

    /// Remap the specified keys
    abstract RemapKeys : lhs:string -> rhs:string -> modes:KeyRemapMode seq -> allowRemap : bool -> unit

    /// Retab the specified line range with the current 'tabstop' and 'expandtab' values
    abstract RetabLineRange : SnapshotLineRange -> includeSpaces : bool -> unit

    /// Remap the specified key sequence
    abstract UnmapKeys : lhs:string -> modes:KeyRemapMode seq -> unit

    /// Clear out the specified Mode in the IKeyMap
    abstract ClearKeyMapModes : KeyRemapMode seq -> unit

    /// Print out the marks in the context of the current buffer
    abstract PrintMarks : IMarkMap -> unit

    /// Print all settings which do not have their default value to the host
    abstract PrintModifiedSettings : unit -> unit

    /// Print all settings to the host.  
    abstract PrintAllSettings : unit -> unit

    /// Print out the key map
    abstract PrintKeyMap : KeyRemapMode list -> unit

    /// Print a single setting out to the host
    abstract PrintSetting : settingName:string -> unit

type ICommandProcessor =

    /// Run the specified command
    abstract RunCommand : char list -> RunResult

