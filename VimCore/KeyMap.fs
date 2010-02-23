#light

namespace Vim

type internal KeyMap() =
    
    let mutable _map : Map<KeyInput,KeyInput*KeyRemapMode> = Map.empty

    member x.GetKeyMapping ki = _map.TryFind ki
    member x.MapWithNoRemap (lhs:string) (rhs:string) (mode:KeyRemapMode) = 
        if StringUtil.Length lhs <> 1 || StringUtil.Length rhs <> 1 then 
            false
        else
            let lhs = lhs.Chars(0)
            let rhs = rhs.Chars(0)
            if System.Char.IsLetterOrDigit(lhs) && System.Char.IsLetterOrDigit(rhs) then
                let leftKi = InputUtil.CharToKeyInput lhs
                let rightKi = InputUtil.CharToKeyInput rhs
                let value = (rightKi,mode)
                _map <- Map.add leftKi value _map
                true
            else
                false

    interface IKeyMap with
        member x.GetKeyMapping ki = x.GetKeyMapping ki
        member x.MapWithNoRemap lhs rhs mode = x.MapWithNoRemap lhs rhs mode
