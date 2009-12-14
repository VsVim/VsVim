#light

namespace Vim

[<System.Flags>]
type internal SearchOptions = 
    | None = 0
    | IgnoreCase = 0x1
    
    
