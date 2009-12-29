#light

namespace Vim

/// Map containing the various VIM registers
type IRegisterMap = 
    abstract DefaultRegisterName : char
    abstract DefaultRegister : Register
    abstract RegisterNames : seq<char>
    abstract IsRegisterName : char -> bool
    abstract GetRegister : char -> Register
    
    
