#light

namespace Vim.Modes.Normal

/// Operation in the normal mode
type internal Operation =  {
    KeyInput : Vim.KeyInput;
    RunFunc : NormalModeData -> NormalModeResult
}
