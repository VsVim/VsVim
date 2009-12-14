#light

namespace Vim

type internal VimData() =
    let _markMap = MarkMap()
    let _registerMap = RegisterMap()
    let _settings = VimSettingsUtil.CreateDefault

    interface IVimData with
        member x.MarkMap = _markMap
        member x.RegisterMap = _registerMap :> IRegisterMap
        member x.Settings = _settings
