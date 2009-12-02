#light

namespace VimCore.Modes.Normal

/// Operation in the normal mode
type internal Operation =  {
    KeyInput : VimCore.KeyInput;
    RunFunc : NormalModeData -> NormalModeResult
}
