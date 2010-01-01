#light

namespace Vim

type internal ToggleHandler<'Del when 'Del :> System.Delegate >
    ( _del : IDelegateEvent<'Del> ) = 

    let mutable _func : 'Del option = None

    member x.Add() = 
        _del.AddHandler(Option.get _func)
    member x.Remove() =
        _del.RemoveHandler(Option.get _func)
    member x.Reset(func) = _func <- Some func
    
module internal Util = 
    let CreateToggleHandler<'a when 'a :> System.Delegate> (e: IDelegateEvent<'a>) = ToggleHandler<'a>(e)
    
    
