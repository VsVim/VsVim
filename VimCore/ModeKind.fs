#light

namespace Vim

type ModeKind = 
    | Normal = 1
    | Insert = 2
    | Command = 3

    // Mode when Vim is disabled via the user
    | Disabled = 42

