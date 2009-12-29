#light

namespace Vim

type ModeKind = 
    | Normal = 1
    | Insert = 2
    | Command = 3
    | VisualCharacter = 4
    | VisualLineWise = 5
    | VisualBlock = 6 

    // Mode when Vim is disabled via the user
    | Disabled = 42

