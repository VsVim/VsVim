
#light

namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Utilities

type internal VimTextBuffer 
    (
        _textBuffer : ITextBuffer,
        _localSettings : IVimLocalSettings,
        _jumpList : IJumpList,
        _wordNavigator : ITextStructureNavigator,
        _vim : IVim
    ) =

    let _vimHost = _vim.VimHost
    let _globalSettings = _localSettings.GlobalSettings
    let _switchedModeEvent = new Event<_>()
    let mutable _modeKind = ModeKind.Normal

    /// Switch to the desired mode
    member x.SwitchMode modeKind modeArgument =
        _modeKind <- modeKind
        _switchedModeEvent.Trigger (modeKind, modeArgument)

    interface IVimTextBuffer with
        member x.TextBuffer = _textBuffer
        member x.GlobalSettings = _globalSettings
        member x.JumpList = _jumpList
        member x.LocalSettings = _localSettings
        member x.ModeKind = _modeKind
        member x.Name = _vimHost.GetName _textBuffer
        member x.Vim = _vim
        member x.WordNavigator = _wordNavigator
        member x.SwitchMode modeKind modeArgument = x.SwitchMode modeKind modeArgument

        [<CLIEvent>]
        member x.SwitchedMode = _switchedModeEvent.Publish

