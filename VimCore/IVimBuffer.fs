#light

namespace VimCore
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open System.Windows.Input

/// Main interface for the Vim editor engine so to speak. 
type IVimBuffer =
    abstract VimBufferData : IVimBufferData
    abstract VimHost : IVimHost
    abstract TextView : IWpfTextView
    abstract ModeKind : ModeKind

    /// Sequence of available Modes
    abstract Modes : seq<IMode>

    abstract Settings : VimSettings
    abstract RegisterMap : IRegisterMap
    abstract GetRegister : char -> Register
    
    // Process the char in question and return whether or not it was handled
    abstract ProcessChar : char -> bool
    
    /// Process the key in question.  Returns true if the key was handled by the buffer
    abstract ProcessKey : Key -> bool
    
    /// Process the KeyInput and return whether or not the input was completely handled
    abstract ProcessInput : KeyInput -> bool
    abstract WillProcessInput : KeyInput -> bool
    abstract WillProcessKey : Key -> bool
    abstract SwitchMode : ModeKind -> IMode
    
    /// Raised when the mode is switched
    [<CLIEvent>]
    abstract SwitchedMode : IEvent<IMode>

    /// Raised when a key is processed
    [<CLIEvent>]
    abstract KeyInputProcessed : IEvent<KeyInput>

